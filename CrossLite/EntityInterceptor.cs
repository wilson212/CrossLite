using Castle.DynamicProxy;
using CrossLite.CodeFirst;
using System;
using System.Collections.Generic;

namespace CrossLite
{
    /// <summary>
    /// A Castle DynamicProxy interceptor that hooks into every property get/set on a
    /// proxied entity. It provides three core behaviors:
    /// <list type="number">
    ///   <item>Lazy-loading of parent navigation properties (foreign objects).</item>
    ///   <item>Lazy-loading of child collection properties (entity sets).</item>
    ///   <item>Dirty-tracking of scalar properties and cache invalidation of
    ///         related navigation/collection caches when foreign-key columns change.</item>
    /// </list>
    /// </summary>
    internal class EntityInterceptor : IInterceptor
    {
        /// <summary>
        /// The active database context used for lazy-load queries.
        /// </summary>
        protected SQLiteContext Context { get; }

        /// <summary>
        /// The table mapping metadata for the entity type being intercepted.
        /// </summary>
        protected TableMapping Table { get; }

        // ── Lazy-initialized per-instance state ──
        // These cost zero memory during the Ghost Run hydration phase
        // (where State == Loading bypasses everything).

        private Dictionary<string, object> _foreignObjectCache;
        private Dictionary<string, object> _entitySetCache;
        private HashSet<string> _loadedNavigationProperties;

        /// <summary>
        /// Cache for parent (foreign-object) navigation properties.
        /// Key = the navigation property name (e.g. "ParentUnit").
        /// Value = the loaded parent entity, or null if not yet loaded / explicitly null.
        /// </summary>
        private Dictionary<string, object> ForeignObjectCache
            => _foreignObjectCache ??= BuildForeignObjectCache();

        /// <summary>
        /// Cache for child-collection navigation properties.
        /// Key = the collection property name (e.g. "Soldiers").
        /// Value = the materialised <c>ChildDbSet</c> instance, or null if not yet loaded.
        /// </summary>
        private Dictionary<string, object> EntitySetCache
            => _entitySetCache ??= BuildEntitySetCache();

        /// <summary>
        /// Tracks which navigation properties have been resolved at least once,
        /// distinguishing "loaded but null" from "never loaded".
        /// </summary>
        private HashSet<string> LoadedNavigationProperties
            => _loadedNavigationProperties ??= new HashSet<string>();

        /// <summary>
        /// Provides interception logic for entities within a database context, enabling
        /// the handling of related entity associations, navigation properties, and
        /// state tracking. Utilized in conjunction with dynamic proxy generation to
        /// extend entity behavior at runtime.
        /// </summary>
        public EntityInterceptor(SQLiteContext context, TableMapping table)
        {
            Context = context;
            Table = table;
            // Zero heap allocations on startup — all state is lazy-initialized.
        }

        /// <summary>
        /// Builds the initial ForeignObjectCache, seeding every FK navigation property with null.
        /// </summary>
        private Dictionary<string, object> BuildForeignObjectCache()
        {
            var cache = new Dictionary<string, object>(Table.ForeignKeys.Count);
            foreach (var fk in Table.ForeignKeys)
            {
                cache[fk.ChildPropertyName] = null;
            }
            return cache;
        }

        /// <summary>
        /// Builds the initial EntitySetCache, seeding every child-collection property with null.
        /// </summary>
        private Dictionary<string, object> BuildEntitySetCache()
        {
            var cache = new Dictionary<string, object>(Table.ChildRelationships.Count);
            foreach (var rel in Table.ChildRelationships)
            {
                cache[rel.Key.Name] = null;
            }
            return cache;
        }

        /// <summary>
        /// Intercepts method calls on proxied entity objects, providing custom logic
        /// for handling property getters and setters, as well as a passthrough for
        /// non-property methods. Ensures proper handling of entity state tracking
        /// during operations such as hydration, dirty-tracking, and caching.
        /// </summary>
        /// <param name="invocation">The intercepted method invocation, containing details of the
        /// method being called, arguments supplied, and the proxied object instance.</param>
        public void Intercept(IInvocation invocation)
        {
            var entity = invocation.Proxy as EntityBase;

            // While the ORM is hydrating the entity, let every set_ pass through
            // untouched so we don't trigger dirty-tracking or cache logic.
            if (entity.State == EntityState.Loading)
            {
                invocation.Proceed();
                return;
            }

            var methodName = invocation.Method.Name;
            if (methodName.Length > 4 && methodName[3] == '_')
            {
                if (methodName[0] == 'g') // get_
                {
                    // Fast-path: if the lazy caches haven't been initialized yet,
                    // this property CAN'T be a navigation property — it's a scalar.
                    // Skip all dictionary lookups and string slicing.
                    if (_foreignObjectCache == null && _entitySetCache == null)
                    {
                        invocation.Proceed();
                        return;
                    }

                    HandleGetter(invocation, entity);
                }
                else if (methodName[0] == 's') // set_
                {
                    HandleSetter(invocation, entity);
                }
                else
                {
                    invocation.Proceed();
                }
            }
            else
            {
                // Non-property method (e.g. ToString, Equals) — pass through.
                invocation.Proceed();
            }
        }

        /// <summary>
        /// Handles the property setter operation for the intercepted entity.
        /// </summary>
        private void HandleSetter(IInvocation invocation, EntityBase entity)
        {
            string propertyName = invocation.Method.Name[4..];

            // ── Case 1: Setting a parent navigation property (e.g. entity.Parent = x) ──
            if (ForeignObjectCache.ContainsKey(propertyName))
            {
                SetForeignNavigationProperty(invocation, entity, propertyName);
            }
            // ── Case 2: Setting a child-collection property — just pass through ──
            else if (EntitySetCache.ContainsKey(propertyName))
            {
                invocation.Proceed();
                return; // Skip state transition below; collection assignment is benign.
            }
            // ── Case 3: Setting a plain scalar / FK-column property ──
            else
            {
                SetScalarProperty(invocation, entity, propertyName);
            }

            // Mark the entity as modified if it was previously clean.
            if (entity.State == EntityState.Fresh)
            {
                entity.State = EntityState.Modified;
            }
        }

        /// <summary>
        /// When the user assigns a parent navigation property directly
        /// (e.g. <c>soldier.Rank = newRank</c>), we back-fill the underlying
        /// FK column(s) from the parent's PK and update the cache.
        /// </summary>
        private void SetForeignNavigationProperty(IInvocation invocation, EntityBase entity, string propertyName)
        {
            if (!Table.FkByChildProperty.TryGetValue(propertyName, out var info))
                throw new Exception("Foreign key property info not found in table mapping.");

            var foreignObj = invocation.Arguments[0];
            var foreignKey = info.ForeignKey;
            var foreignTable = TableCache.GetTableMap(info.ParentEntityType);

            // Synchronize each FK column on this entity with the corresponding
            // PK value from the assigned parent object.
            for (int i = 0; i < foreignKey.PropertyNames.Length; i++)
            {
                var localAttr = Table.GetAttributeByPropertyName(foreignKey.PropertyNames[i]);
                if (localAttr == null) continue;

                object foreignValue = null;
                if (foreignObj != null)
                {
                    var parentAttr = foreignTable.GetAttributeByPropertyName(info.Reference.PropertyNames[i]);
                    foreignValue = parentAttr.GetValue(foreignObj);
                }

                localAttr.SetValue(entity, foreignValue);
            }

            // Update the cache so subsequent gets return the same instance.
            ForeignObjectCache[propertyName] = foreignObj;
            LoadedNavigationProperties.Add(propertyName);
        }

        /// <summary>
        /// Handles a plain scalar or FK-column property being set.
        /// Marks the property dirty and invalidates any navigation/collection
        /// caches that depend on the changed column.
        /// </summary>
        private void SetScalarProperty(IInvocation invocation, EntityBase entity, string propertyName)
        {
            entity.DirtyProperties.Add(propertyName);

            // If this column is part of a foreign key, invalidate the cached
            // parent navigation property so the next get_ re-fetches it.
            if (Table.FkByFkPropertyName.TryGetValue(propertyName, out var affectedParentFk))
            {
                ForeignObjectCache[affectedParentFk.ChildPropertyName] = null;
                LoadedNavigationProperties.Remove(affectedParentFk.ChildPropertyName);
            }

            // If this column is a referenced PK that child collections depend on,
            // invalidate those collection caches as well.
            if (Table.ChildRelPropertyToEntitySetKeys.TryGetValue(propertyName, out var affectedKeys))
            {
                foreach (var key in affectedKeys)
                {
                    EntitySetCache[key] = null;
                }
            }

            invocation.Proceed();
        }

        /// <summary>
        /// Handles property getter invocations for proxied entities.
        /// </summary>
        private void HandleGetter(IInvocation invocation, EntityBase entity)
        {
            string propertyName = invocation.Method.Name[4..];

            // ── Case 1: Getting a parent navigation property ──
            if (ForeignObjectCache.ContainsKey(propertyName))
            {
                invocation.ReturnValue = GetForeignNavigationProperty(entity, propertyName);
                return;
            }

            // ── Case 2: Getting a child-collection navigation property ──
            if (EntitySetCache.ContainsKey(propertyName))
            {
                invocation.ReturnValue = GetEntitySetProperty(entity, propertyName);
                return;
            }

            // ── Case 3: Plain scalar property — pass through to backing field ──
            invocation.Proceed();
        }

        /// <summary>
        /// Lazy-loads a parent entity via <see cref="ForeignEntityLoader{TParent,TChild}"/>
        /// the first time the navigation property is accessed, then returns the
        /// cached instance on subsequent calls.
        /// </summary>
        private object GetForeignNavigationProperty(EntityBase entity, string propertyName)
        {
            // Already resolved — return cached value (may be null).
            if (LoadedNavigationProperties.Contains(propertyName))
                return ForeignObjectCache[propertyName];

            var fkInfo = Table.FkByChildProperty[propertyName];

            // If any local FK column is null, the relationship is empty.
            foreach (var localPropName in fkInfo.ForeignKey.PropertyNames)
            {
                var attr = Table.GetAttributeByPropertyName(localPropName);
                if (attr.GetValue(entity) == null)
                {
                    ForeignObjectCache[propertyName] = null;
                    LoadedNavigationProperties.Add(propertyName);
                    return null;
                }
            }

            // All FK columns are populated — issue a lazy-load query.
            var loader = Table.ForeignLoaderFactories[propertyName](entity, fkInfo, Context);
            object loadedValue = loader.Fetch();

            ForeignObjectCache[propertyName] = loadedValue;
            LoadedNavigationProperties.Add(propertyName);
            return loadedValue;
        }

        /// <summary>
        /// Lazy-loads a child collection via <see cref="ChildDbSet{TParent,TChild}"/>
        /// the first time the collection property is accessed, then returns the
        /// cached instance on subsequent calls. Returns null if the parent's PK
        /// columns are dirty (unsaved), since the DB query would be stale.
        /// </summary>
        private object GetEntitySetProperty(EntityBase entity, string propertyName)
        {
            // Already materialised — return cached collection.
            if (EntitySetCache[propertyName] != null)
                return EntitySetCache[propertyName];

            if (!Table.ChildRelByPropertyName.TryGetValue(propertyName, out var item))
                return null; // Unknown property — shouldn't happen.

            // If any of the local PK columns referenced by the child FK are dirty,
            // we can't reliably query the DB, so return null until the entity is saved.
            var childTable = TableCache.GetTableMap(item.Value);
            foreach (var fk in childTable.ForeignKeys)
            {
                if (fk.ParentEntityType != Table.EntityType) continue;

                foreach (var propName in fk.ForeignKey.PropertyNames)
                {
                    if (entity.DirtyProperties.Contains(propName))
                        return null;
                }
            }

            // Construct and cache the ChildDbSet<TParent, TChild>.
            var entitySetInstance = Table.ChildDbSetFactories[propertyName](entity, item.Key, Context);
            EntitySetCache[propertyName] = entitySetInstance;
            return entitySetInstance;
        }
    }
}