using Castle.DynamicProxy;
using CrossLite.CodeFirst;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CrossLite
{
    /// <summary>
    /// Intercepts method calls on proxied entities to enable dirty tracking for property setters  and lazy loading for
    /// property getters.
    /// </summary>
    /// <remarks>This class is designed to work with entities that inherit from <see cref="EntityBase"/> and 
    /// provides the following functionality: <list type="bullet"> <item> <description> **Property Setters**: Tracks
    /// changes to entity properties. If the property being set is a foreign key,  the corresponding foreign key columns
    /// are updated, and the foreign object is cached for future comparisons.  Non-foreign key properties are added to
    /// the entity's dirty properties list, and the entity's state is updated  to <see cref="EntityState.Modified"/> if
    /// it was previously <see cref="EntityState.Fresh"/>. </description> </item> <item> <description> **Property
    /// Getters**: Supports lazy loading of related entities or collections. For foreign key properties,  the related
    /// entity is loaded and cached for subsequent accesses. For `EntitySet<T>` properties, the related  collection is
    /// loaded and cached. </description> </item> </list> If the entity is in the <see cref="EntityState.Loading"/>
    /// state, all operations are allowed to proceed  without interception.</remarks>
    internal class EntityInterceptor : IInterceptor
    {
        /// <summary>
        /// Gets the <see cref="SQLiteContext"/> instance used to interact with the SQLite database.
        /// </summary>
        /// <remarks>This property provides access to the database context, which can be used to query or
        /// manipulate data in the SQLite database. The context is typically initialized and managed by the containing
        /// class.</remarks>
        protected SQLiteContext Context { get; }

        /// <summary>
        /// Gets the mapping information for the database table associated with the current context.
        /// </summary>
        protected TableMapping Table { get; }

        /// <summary>
        /// Gets the cache of foreign objects, indexed by their string keys.
        /// </summary>
        /// <remarks>This property provides access to a cache of foreign objects, which can be used to
        /// store and retrieve objects by their associated string keys.  The cache is read-only and cannot be modified
        /// directly.</remarks>
        protected Dictionary<string, object> ForeignObjectCache { get; }

        /// <summary>
        /// Gets the cache of child entity sets, where each entry maps an entity set name to its corresponding object.
        /// </summary>
        protected Dictionary<string, object> EntitySetCache { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="EntityInterceptor"/> class, which provides caching and
        /// relationship management for entities in the specified table.
        /// </summary>
        /// <remarks>This constructor initializes caches for managing foreign key relationships and child
        /// entity sets  based on the provided table's schema. The foreign object cache is pre-filled with null values
        /// for  each foreign key property defined in the table mapping.</remarks>
        /// <param name="context">The <see cref="SQLiteContext"/> instance that provides database access and context for the interceptor.</param>
        /// <param name="table">The <see cref="TableMapping"/> that defines the schema and relationships for the table being intercepted.</param>
        public EntityInterceptor(SQLiteContext context, TableMapping table)
        {
            // Store context and table mapping
            Context = context;
            Table = table;
            ForeignObjectCache = new Dictionary<string, object>(Table.ForeignKeys.Count);
            EntitySetCache = new Dictionary<string, object>(Table.ChildRelationships.Count);

            // Pre-fill foreign object cache with nulls for each foreign key property
            foreach (var fk in Table.ForeignKeys)
            {
                ForeignObjectCache[fk.ChildPropertyName] = null;
            }

            // Pre-fill entity set cache with nulls for each child relationship
            foreach (var rel in Table.ChildRelationships)
            {
                EntitySetCache[rel.Key.Name] = null;
            }
        }

        /// <summary>
        /// Intercepts method calls on a proxied entity to enable dirty tracking for property setters and lazy loading
        /// for property getters.
        /// </summary>
        /// <remarks>This method is designed to handle property accessors (`get_` and `set_` methods) on
        /// entities that inherit from <see cref="EntityBase"/>. For property setters, it tracks changes to the entity's
        /// state and updates the dirty properties list. For property getters, it supports lazy loading of related
        /// entities or collections.  - **Setters**: If the property being set is a foreign key, the corresponding
        /// foreign key columns   are updated, and the foreign object is cached for future comparisons. If the property
        /// is not a   foreign key, it is added to the dirty properties list, and the entity's state is updated to  
        /// <see cref="EntityState.Modified"/> if it was previously <see cref="EntityState.Fresh"/>. - **Getters**: If
        /// the property being accessed is a foreign key or an `EntitySet<T>`, the method   attempts to load the related
        /// entity or collection. The loaded value is cached for subsequent   accesses.  If the entity is in the <see
        /// cref="EntityState.Loading"/> state, all operations are allowed to proceed without interception.</remarks>
        /// <param name="invocation">The invocation context containing details about the method being called, the proxy instance, and the
        /// arguments passed to the method.</param>
        /// <exception cref="Exception">Thrown if a foreign key property is not found in the table mapping during a setter operation.</exception>
        public void Intercept(IInvocation invocation)
        {
            var entity = invocation.Proxy as EntityBase;
            if (entity.State == EntityState.Loading)
            {
                // If the entity is still loading, allow all operations to proceed without interception.
                invocation.Proceed();
                return;
            }

            // --- Handle SETTERS for Dirty Tracking ---
            if (invocation.Method.Name.StartsWith("set_"))
            {
                // Add the property name to the entity's dirty list.
                string propertyName = invocation.Method.Name[4..];

                // Check if this is a foreign key property
                if (ForeignObjectCache.ContainsKey(propertyName))
                {
                    var info = Table.ForeignKeys.FirstOrDefault(fk => fk.ChildPropertyName == propertyName);
                    if (info != null)
                    {
                        // Get the value from the new foreign object
                        var foreignObj = invocation.Arguments[0];
                        var foreignKey = info.ForeignKey;
                        var foreignTable = EntityCache.GetTableMap(info.ParentEntityType);

                        // Update the foreign key columns on this entity
                        for (int index = 0; index < foreignKey.PropertyNames.Length; index++)
                        {
                            // Get the attribute info for this foreign key column
                            var propName = foreignKey.PropertyNames[index];
                            var attrInfo = Table.GetAttributeByPropertyName(propName);
                            if (attrInfo != null)
                            {
                                // Set the foreign key id property on this entity
                                var foreignAttrInfo = foreignTable.GetAttributeByPropertyName(info.Reference.PropertyNames[index]);
                                var foreignValue = foreignAttrInfo.Property.GetValue(foreignObj);

                                // This will recusively call this interceptor, but since it's not a foreign key property,
                                // it will just add it to the dirty list.
                                attrInfo.Property.SetValue(entity, foreignValue);
                            }
                        }

                        // Store the loaded value for comparison later
                        ForeignObjectCache[propertyName] = foreignObj;
                    }
                    else
                    {
                        // If we are here, something went very wrong
                        throw new Exception("Foreign key property info not found in table mapping.");
                    }
                }
                else if (EntitySetCache.ContainsKey(propertyName))
                {
                    // Future implementation for EntitySet<T> properties
                    invocation.Proceed();
                    return;
                }
                else
                {
                    entity.DirtyProperties.Add(propertyName);
                }

                // If the entity was Fresh, mark it as Modified.
                if (entity.State == EntityState.Fresh)
                {
                    entity.State = EntityState.Modified;
                }
            }
            // --- Handle GETTERS for Lazy Loading ---
            else if (invocation.Method.Name.StartsWith("get_"))
            {
                string propertyName = invocation.Method.Name[4..];

                // Is this a foreign key property?
                if (!ForeignObjectCache.TryGetValue(propertyName, out object value))
                {
                    // Check for EntitySet<T> properties here for future implementation
                    if (EntitySetCache.ContainsKey(propertyName))
                    {
                        // If we have a loaded value, return it directly
                        if (EntitySetCache[propertyName] != null)
                        {
                            invocation.ReturnValue = EntitySetCache[propertyName];
                            return;
                        }

                        // -- Load the Child Entities into an EntitySet<T> --

                        // Check if foreign key properties are dirty; if so, we cannot load the child entities
                        var childTable = EntityCache.GetTableMap(Table.ChildRelationships.First(r => r.Key.Name == propertyName).Value);
                        foreach (var fk in childTable.ForeignKeys.Where(fk => fk.ParentEntityType == Table.EntityType))
                        {
                            var isDirty = fk.ForeignKey.PropertyNames.Any(entity.DirtyProperties.Contains);
                            if (isDirty)
                            {
                                // Cannot load child entities if foreign key properties are dirty
                                invocation.ReturnValue = null;
                                return;
                            }
                        }

                        // Create a new EntitySet<T> instance and load it
                        var item = Table.ChildRelationships.FirstOrDefault(r => r.Key.Name == propertyName);
                        var entitySetType = typeof(ChildDbSet<,>).MakeGenericType(Table.EntityType, item.Value);
                        var entitySetInstance = Activator.CreateInstance(entitySetType, [entity, item.Key, Context]);

                        // Cache the loaded EntitySet<T>
                        EntitySetCache[propertyName] = entitySetInstance;
                        invocation.ReturnValue = entitySetInstance;
                        return;
                    }
                }
                else if (value == null)
                {
                    // Load the foreign object
                    var foreignKey = Table.ForeignKeys.FirstOrDefault(fk => fk.ChildPropertyName == propertyName);
                    var loaderType = typeof(ForeignEntityLoader<,>).MakeGenericType(foreignKey.ParentEntityType, Table.EntityType);
                    dynamic loaderInstance = Activator.CreateInstance(loaderType, [entity, foreignKey, Context]);

                    // If we have a loaded value, return it directly
                    value = loaderInstance.Fetch();
                    ForeignObjectCache[propertyName] = value;
                    invocation.ReturnValue = value;
                    return;
                }
                else
                {
                    // Return the cached foreign object
                    invocation.ReturnValue = value;
                    return;
                }
            }

            // Allow the original getter/setter to run to update the value.
            invocation.Proceed();
            return;
        }
    }
}
