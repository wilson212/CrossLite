using CrossLite.CodeFirst;
using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace CrossLite
{
    /// <summary>
    /// Represents the mapping between an entity type and its corresponding database table.
    /// </summary>
    /// <remarks>This class provides metadata about the table, including its name, primary keys, column
    /// mappings, foreign key relationships, and other constraints. It is used to facilitate operations such as table
    /// creation, data retrieval, and entity-to-database mapping.</remarks>
    public class TableMapping
    {
        /// <summary>
        /// Gets the Entity object type for this table
        /// </summary>
        public Type EntityType { get; protected set; }

        /// <summary>
        /// Gets the table name this Entity represents
        /// </summary>
        public string TableName { get; protected set; }

        /// <summary>
        /// Gets the table composite indexes if any
        /// </summary>
        public CompositeIndexAttribute[] CompositeIndexes { get; protected set; }

        /// <summary>
        /// Gets a collection of keys on this table, that are considered Primary Keys. These are the property names, 
        /// not the column names in the database. By default, the column names are the same as the Property names,
        /// but this can be overridden using the <see cref="ColumnAttribute"/> on the Entity.
        /// </summary>
        public IReadOnlyCollection<AttributeInfo> PrimaryKeys { get; protected set; }

        /// <summary>
        /// Get or sets the RowId column
        /// </summary>
        public AttributeInfo RowIdColumn { get; protected set; }

        /// <summary>
        /// Indicates whether this Table has a single Integer Primary Key
        /// </summary>
        public bool HasRowIdAlias => (!WithoutRowID && RowIdColumn != null);

        /// <summary>
        /// Gets or Sets whether the "WITHOUT ROWID" command is used
        /// when creating a table using Code First 
        /// (see <see cref="SQLiteContext.CreateTable{TEntity}(bool)"/>)
        /// </summary>
        public bool WithoutRowID { get; protected set; } = false;

        /// <summary>
        /// Gets a value indicating whether the object contains virtual members.
        /// </summary>
        public bool HasVirtuals { get; protected set; } = false;

        /// <summary>
        /// If true, the <see cref="ForeignKeyLoader{TEntity}"/> attributes will be filled after insertion,
        /// otherwise they are left null. There is a slight performance hit when true.
        /// </summary>
        public bool BuildInstanceForeignKeys { get; set; } = true;

        /// <summary>
        /// Gets a collection of Column to Property mappings. The key is the column name in the database (not the property name in the Entity), 
        /// and the value is the atribute information
        /// </summary>
        public IReadOnlyDictionary<string, AttributeInfo> DatabaseColumns { get; protected set; }

        /// <summary>
        /// Gets a collection of Property Column mappings. The key is the property name in the Entity (not the column name in the Database), 
        /// and the value is the atribute information
        /// </summary>
        public IReadOnlyDictionary<string, AttributeInfo> EntityProperties { get; protected set; }

        /// <summary>
        /// Gets a collection of Foreign keys on this table, where this Entity is a
        /// child (Many) to a parent Entity (One)
        /// </summary>
        public IReadOnlyCollection<ForeignKeyConstraint> ForeignKeys { get; protected set; }

        /// <summary>
        /// Gets a collection of IsUnique constraints on this table
        /// </summary>
        public IReadOnlyCollection<CompositeUniqueAttribute> UniqueConstraints { get; protected set; }

        /// <summary>
        /// Contains a list of Foreign key relationships, where this Entity is a
        /// child (Many) to a parent Entity (One). [Property => Generic Type]
        /// </summary>
        /// <remarks>
        /// Contains both Lazy loaded props and Eager loaded props.
        /// </remarks>
        internal Dictionary<PropertyInfo, Type> ParentRelationships { get; set; }

        /// <summary>
        /// Contains a list of Foreign key relationships, where this Entity is a
        /// parent Entity (one) to many child Entities (many). 
        /// [Property => Child Generic Type]
        /// </summary>
        internal Dictionary<PropertyInfo, Type> ChildRelationships { get; set; }

        /// <summary>
        /// Gets the name of the Auto Increment attribute, or NULL
        /// </summary>
        public AttributeInfo AutoIncrementAttribute { get; internal set; }

        /// <summary>
        /// Gets the collection of property names that correspond to primary key columns in the database table.
        /// </summary>
        /// <remarks>
        /// This collection is used to identify and reference the properties of the entity
        /// that are part of the primary key defined for the table.
        /// </remarks>
        internal FrozenSet<string> PrimaryKeyPropertyNames { get; set; }

        /// <summary>
        /// Gets a dictionary that maps child property names to their respective foreign key constraints.
        /// </summary>
        internal Dictionary<string, ForeignKeyConstraint> FkByChildProperty { get; private set; }

        /// <summary>
        /// Gets a dictionary that maps foreign key property names to their associated
        /// <see cref="ForeignKeyConstraint"/> objects. This facilitates lookup of foreign key
        /// constraints based on property names in the table mapping.
        /// </summary>
        internal Dictionary<string, ForeignKeyConstraint> FkByFkPropertyName { get; private set; }

        /// <summary>
        /// Represents a mapping of child relationship property names to their associated entity set keys.
        /// This dictionary is used to track foreign key dependencies within child entities
        /// and manage invalidation of cached collections when these keys are modified.
        /// </summary>
        internal Dictionary<string, List<string>> ChildRelPropertyToEntitySetKeys { get; private set; }

        /// <summary>
        /// Stores a mapping of property names to their corresponding child relationship metadata,
        /// represented as key-value pairs containing the property info and the related entity type.
        /// </summary>
        internal Dictionary<string, KeyValuePair<PropertyInfo, Type>> ChildRelByPropertyName { get; private set; }
        
        /// <summary>
        /// Cached factory delegates for ForeignEntityLoader and ChildDbSet construction,
        /// keyed by navigation property name. Eliminates MakeGenericType + Activator.CreateInstance
        /// reflection on every lazy-load access.
        /// </summary>
        internal Dictionary<string, Func<object, ForeignKeyConstraint, SQLiteContext, IEntityFetcher>> ForeignLoaderFactories { get; private set; }
        internal Dictionary<string, Func<object, PropertyInfo, SQLiteContext, object>> ChildDbSetFactories { get; private set; }

        /// <summary>
        /// Creates a new instance of <see cref="TableMapping"/>
        /// </summary>
        /// <param name="entityType"></param>
        public TableMapping(Type entityType)
        {
            // Set critical props
            EntityType = entityType;
            TableName = entityType.Name;
            ParentRelationships = new Dictionary<PropertyInfo, Type>();
            ChildRelationships = new Dictionary<PropertyInfo, Type>();

            // Get table related instructions
            var tableAttr = (TableAttribute)entityType.GetCustomAttribute(typeof(TableAttribute));
            if (tableAttr != null)
            {
                TableName = tableAttr.Name ?? entityType.Name;
                WithoutRowID = tableAttr.WithoutRowID;
                BuildInstanceForeignKeys = tableAttr.BuildInstanceRelationships;
            }

            // Get table related composite indexies
            var compAttr = entityType.GetCustomAttributes(typeof(CompositeIndexAttribute)).ToArray();
            if (compAttr.Length > 0)
            {
                CompositeIndexes = new CompositeIndexAttribute[compAttr.Length];
                for (int i = 0; i < compAttr.Length; i++)
                {
                    CompositeIndexes[i] = (CompositeIndexAttribute)compAttr[i];
                }
            }
            else
            {
                CompositeIndexes = [];
            }

            // Get a list of props from the Entity that represents an Attribute
            var entityProps = entityType.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            // Temporary variables
            var cols = new Dictionary<string, AttributeInfo>(entityProps.Length);
            var props = new Dictionary<string, AttributeInfo>(entityProps.Length);
            var primaryKeys = new List<AttributeInfo>();

            // Loop through each attribute, and generate an attribute map
            foreach (PropertyInfo property in entityProps)
            {
                // Grab type
                bool isNullable = property.PropertyType.IsGenericType && property.PropertyType.GetGenericTypeDefinition() == typeof(Nullable<>);
                Type type = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;

                // Column attribute?
                if (Attribute.IsDefined(property, typeof(ColumnAttribute)))
                {
                    // All columns must be marked virtual
                    if (property.GetMethod == null || !property.GetMethod.IsVirtual || property.GetMethod.IsFinal)
                    {
                        throw new EntityException($"All properties marked with ColumnAttribute must be virtual. Property: {entityType.Name}.{property.Name}");
                    }

                    // Create our attribute info class
                    AttributeInfo info = new AttributeInfo
                    {
                        Property = property,
                        IsNullable = isNullable,
                    };

                    // Now itterate through each attribute
                    foreach (Attribute attr in property.GetCustomAttributes())
                    {
                        Type attrType = attr.GetType();
                        if (attrType == typeof(ColumnAttribute))
                        {
                            // Get our attribute name
                            ColumnAttribute colAttr = (ColumnAttribute)attr;
                            info.ColumnName = colAttr.Name ?? property.Name;
                            info.Order = colAttr.Order;
                        }
                        else if (attrType == typeof(ForeignKeyAttribute))
                        {
                            throw new EntityException($"Invalid foreign key attribute on {entityType.Name}.{property.Name}");
                        }
                        else if (attrType == typeof(PrimaryKeyAttribute))
                        {
                            // Primary keys cannot be nullable
                            if (isNullable)
                            {
                                throw new EntityException($"PrimaryKeyAttribute cannot be applied to a nullable property. Property: {entityType.Name}.{property.Name}");
                            }

                            // Check for RowID Alias column (INTEGER PRIMARY KEY)
                            if (info.Property.PropertyType.IsInteger())
                            {
                                if (primaryKeys.Count == 0)
                                    RowIdColumn = info;
                                else
                                    RowIdColumn = null;
                            }

                            // Add primary key to the list
                            info.IsPrimaryKey = true;
                            primaryKeys.Add(info);
                        }
                        else if (attrType == typeof(DefaultAttribute))
                        {
                            info.DefaultValue = (DefaultAttribute)attr;
                        }
                        else if (attrType == typeof(RequiredAttribute))
                        {
                            if (isNullable)
                                throw new EntityException($"RequiredAttribute cannot be applied to a nullable property. Property: {entityType.Name}.{property.Name}");

                            info.HasRequiredAttribute = true;
                        }
                        else if (attrType == typeof(UniqueAttribute))
                        {
                            info.IsUnique = true;
                        }
                        else if (attrType == typeof(CollationAttribute))
                        {
                            info.Collation = ((CollationAttribute)attr).Collation;
                        }
                        else if (attrType == typeof(IndexAttribute))
                        {
                            var indexAttr = (IndexAttribute)attr;
                            info.IsIndexed = true;
                            info.IndexName = indexAttr.Name;
                            if (indexAttr.Unique)
                                info.IsUnique = true;
                        }
                        else if (attrType == typeof(AutoIncrementAttribute))
                        {
                            // Cannot have more than 1 auto increment column
                            if (AutoIncrementAttribute != null)
                                throw new EntityException($"Entity `{EntityType.Name}` cannot contain multiple AutoIncrement attributes.");

                            // set values
                            AutoIncrementAttribute = info;
                            info.IsAutoIncrement = true;
                        }
                    }
                    
                    // Compile attribute
                    info.Compile();

                    // Add to column list
                    cols[info.ColumnName] = info;
                    props[info.Property.Name] = info;
                    HasVirtuals = true;
                }
                // Check for foreign key collections
                else if (Attribute.IsDefined(property, typeof(ForeignKeyAttribute)))
                {
                    // Eager Loaded Property!
                    ParentRelationships.Add(property, type);
                    HasVirtuals |= (property.GetMethod != null && property.GetMethod.IsVirtual && !property.GetMethod.IsFinal);
                }
                else if (type.IsGenericType)
                {
                    // Check for EntitySet<TEntity> without ForeignEntityLoader attribute
                    Type def = type.GetGenericTypeDefinition();
                    type = type.GenericTypeArguments[0];
                    if (def == typeof(EntitySet<>))
                    {
                        // EntitySet means this is a parent entity
                        ChildRelationships.Add(property, type);
                        HasVirtuals |= (property.GetMethod != null && property.GetMethod.IsVirtual && !property.GetMethod.IsFinal);
                    }
                }
            }

            // Check for unique composites  
            UniqueConstraints = entityType.GetCustomAttributes<CompositeUniqueAttribute>().ToHashSet();

            // Set internals
            DatabaseColumns = cols.ToFrozenDictionary();
            EntityProperties = props.ToFrozenDictionary();
            PrimaryKeys = [.. primaryKeys.OrderBy(x => x.Order)];
            PrimaryKeyPropertyNames = PrimaryKeys.Select(x => x.Property.Name).ToFrozenSet();
            var foreignKeys = new HashSet<ForeignKeyConstraint>();
            
            // *** ADD THIS LINE ***
            // Pre-register ourselves so circular references find a usable partial mapping
            // that already has EntityProperties, DatabaseColumns, and PrimaryKeys populated.
            TableCache.RegisterPartial(EntityType, this);

            // ------------------------------------
            // Always check foreign keys after setting the DatabaseColumns property!
            // 
            // We must always check foreign keys after loading all of the entities props,
            // because the props may not be ordered correctly in the class itself. This
            // would cause errors when checking for column matches between the parent
            // and child entities when creating the ForeignKeyInfo class.
            // ------------------------------------

            // Loop through each attribute, and generate an attribute map
            foreach (PropertyInfo property in ParentRelationships.Keys)
            {
                var fkey = (ForeignKeyAttribute)property.GetCustomAttribute(typeof(ForeignKeyAttribute));
                var inverse = (ReferencesAttribute)property.GetCustomAttribute(typeof(ReferencesAttribute));

                // Grab generic type
                Type parentType = (property.PropertyType.IsGenericType)
                    ? property.PropertyType.GetGenericArguments()[0]
                    : property.PropertyType;

                // Create ForeignKeyInfo
                ReferencesAttribute inv = inverse ?? new ReferencesAttribute(fkey.PropertyNames);
                ForeignKeyConstraint info = new ForeignKeyConstraint(this, property.Name, parentType, fkey, inv);  
                foreignKeys.Add(info);

                // Foreign keys cannot be an alias for RowID!
                if (RowIdColumn != null && info.ForeignKey.PropertyNames.Contains(RowIdColumn.Property.Name))
                {
                    RowIdColumn = null;
                }
            }

            // Finally, set our class ForeignEntityLoader property
            ForeignKeys = foreignKeys;
            
            // ── Build cached metadata lookup dictionaries ──
            // These are schema-level indexes that never change per entity instance.

            // Maps navigation property name → ForeignKeyConstraint
            FkByChildProperty = new Dictionary<string, ForeignKeyConstraint>(foreignKeys.Count);
            foreach (var fk in foreignKeys)
            {
                FkByChildProperty[fk.ChildPropertyName] = fk;
            }

            // Maps each FK column property name → its owning ForeignKeyConstraint
            FkByFkPropertyName = new Dictionary<string, ForeignKeyConstraint>();
            foreach (var fk in foreignKeys)
            {
                foreach (var propName in fk.ForeignKey.PropertyNames)
                {
                    FkByFkPropertyName[propName] = fk;
                }
            }

            // Maps child-collection property name → (PropertyInfo, ChildEntityType)
            ChildRelByPropertyName = new Dictionary<string, KeyValuePair<PropertyInfo, Type>>(ChildRelationships.Count);
            foreach (var rel in ChildRelationships)
            {
                ChildRelByPropertyName[rel.Key.Name] = rel;
            }

            // Maps a local PK/reference property name → list of child-collection cache keys
            // that depend on it (for invalidation when PK columns change).
            ChildRelPropertyToEntitySetKeys = new Dictionary<string, List<string>>();
            foreach (var rel in ChildRelationships)
            {
                var childTable = TableCache.GetTableMap(rel.Value);
                
                // If the child mapping is still partially constructed (circular reference),
                // its ForeignKeys won't be populated yet — skip it safely.
                var fk = childTable?.ForeignKeys?.FirstOrDefault(f => f.ParentEntityType == EntityType);
                if (fk == null) continue;

                foreach (var refProp in fk.Reference.PropertyNames)
                {
                    if (!ChildRelPropertyToEntitySetKeys.TryGetValue(refProp, out var list))
                    {
                        list = new List<string>();
                        ChildRelPropertyToEntitySetKeys[refProp] = list;
                    }
                    list.Add(rel.Key.Name);
                }
            }
            
            // ── Cache compiled constructor delegates for lazy-load types ──
            // This eliminates MakeGenericType + Activator.CreateInstance reflection per nav-prop access.
            ForeignLoaderFactories = new Dictionary<string, Func<object, ForeignKeyConstraint, SQLiteContext, IEntityFetcher>>(foreignKeys.Count);
            foreach (var fk in foreignKeys)
            {
                var loaderType = typeof(ForeignEntityLoader<,>).MakeGenericType(fk.ParentEntityType, EntityType);
                var ctor = loaderType.GetConstructor(new[] { EntityType, typeof(ForeignKeyConstraint), typeof(SQLiteContext) });

                // Build: (entity, constraint, ctx) => (IEntityFetcher)new ForeignEntityLoader<P,C>((C)entity, constraint, ctx)
                var pEntity = Expression.Parameter(typeof(object), "entity");
                var pConstraint = Expression.Parameter(typeof(ForeignKeyConstraint), "constraint");
                var pContext = Expression.Parameter(typeof(SQLiteContext), "context");

                var newExpr = Expression.New(ctor,
                    Expression.Convert(pEntity, EntityType),
                    pConstraint,
                    pContext);

                var lambda = Expression.Lambda<Func<object, ForeignKeyConstraint, SQLiteContext, IEntityFetcher>>(
                    newExpr, pEntity, pConstraint, pContext);

                ForeignLoaderFactories[fk.ChildPropertyName] = lambda.Compile();
            }

            ChildDbSetFactories = new Dictionary<string, Func<object, PropertyInfo, SQLiteContext, object>>(ChildRelationships.Count);
            foreach (var rel in ChildRelationships)
            {
                var dbSetType = typeof(ChildDbSet<,>).MakeGenericType(EntityType, rel.Value);
                var ctor = dbSetType.GetConstructor(new[] { EntityType, typeof(PropertyInfo), typeof(SQLiteContext) });

                var pEntity = Expression.Parameter(typeof(object), "entity");
                var pProp = Expression.Parameter(typeof(PropertyInfo), "prop");
                var pContext = Expression.Parameter(typeof(SQLiteContext), "context");

                var newExpr = Expression.New(ctor,
                    Expression.Convert(pEntity, EntityType),
                    pProp,
                    pContext);

                var lambda = Expression.Lambda<Func<object, PropertyInfo, SQLiteContext, object>>(
                    newExpr, pEntity, pProp, pContext);

                ChildDbSetFactories[rel.Key.Name] = lambda.Compile();
            }
        }

        /// <summary>
        /// Retrieves the attribute information associated with the specified column name.
        /// </summary>
        /// <param name="attributeName">The name of the column whose attribute information is to be retrieved.  This value must correspond to a
        /// valid column name defined in the entity type.</param>
        /// <returns>An <see cref="AttributeInfo"/> object representing the attribute information for the specified column name.</returns>
        /// <exception cref="Exception">Thrown if the specified <paramref name="attributeName"/> does not exist in the entity type.</exception>
        public AttributeInfo GetAttributeByColumnName(string attributeName)
        {
            if (!DatabaseColumns.TryGetValue(attributeName, out var info))
                throw new Exception("Entity type \"" + EntityType.Name + "\" does not contain a definition for \"" + attributeName + "\"");

            return info;
        }

        /// <summary>
        /// Retrieves the attribute information associated with the specified property name.
        /// </summary>
        /// <param name="propertyName">The name of the property for which to retrieve the attribute information.  This value is case-sensitive and
        /// must match a defined property name.</param>
        /// <returns>An <see cref="AttributeInfo"/> object representing the attribute information for the specified property.</returns>
        /// <exception cref="Exception">Thrown if the specified <paramref name="propertyName"/> does not exist in the entity type.</exception>
        public AttributeInfo GetAttributeByPropertyName(string propertyName)
        {
            if (!EntityProperties.TryGetValue(propertyName, out var info))
                throw new Exception("Entity type \"" + EntityType.Name + "\" does not contain a definition for \"" + propertyName + "\"");

            return info;
        }
        
        /// <summary>
        /// Retrieves a set of column names corresponding to the specified property names.
        /// </summary>
        /// <remarks>This method maps property names to their corresponding column names based on the
        /// entity's property definitions. If a property name is not found, an <see cref="EntityException"/> is
        /// thrown.</remarks>
        /// <param name="properties">A collection of property names for which to retrieve the corresponding column names.</param>
        /// <returns>A <see cref="HashSet{T}"/> containing the column names associated with the specified property names.</returns>
        /// <exception cref="EntityException">Thrown if any property name in <paramref name="properties"/> does not exist in the entity.</exception>
        public HashSet<string> GetColumnsFromProperties(IEnumerable<string> properties)
        {
            HashSet<string> cols = new HashSet<string>();
            foreach (string prop in properties)
            {
                if (EntityProperties.TryGetValue(prop, out var info))
                {
                    cols.Add(info.ColumnName);
                }
                else
                {
                    throw new EntityException($"Property '{prop}' does not exist in entity '{EntityType.Name}'");
                }
            }
            return cols;
        }

        /// <summary>
        /// Gets an array of child entity types that reference this Entity
        /// </summary>
        /// <returns></returns>
        public Type[] GetChildRelationshipTypes()
        {
            return ChildRelationships.Values.ToArray();
        }

        /// <summary>
        /// Gets an array of parent entity types thatthis Entity references
        /// </summary>
        /// <returns></returns>
        public Type[] GetParentRelationshipTypes()
        {
            return ParentRelationships.Values.ToArray();
        }

        /// <summary>
        /// Retrieves a mapping of foreign key property names to their corresponding parent entity property names for
        /// the specified entity type.
        /// </summary>
        /// <remarks>This method filters the foreign key mappings to include only those that reference the
        /// specified parent entity type <typeparamref name="T"/>. The resulting dictionary provides a mapping of child
        /// entity property names to their parent entity counterparts.</remarks>
        /// <typeparam name="T">The type of the parent entity for which the foreign key mappings are retrieved.</typeparam>
        /// <returns>A dictionary where the keys are the foreign key property names in the current entity, and the values are the
        /// corresponding property names in the parent entity.</returns>
        public Dictionary<string, string> GetForeignKeyPropertyMappingsTo<T>()
        {
            Dictionary<string, string> foreignKeys = new Dictionary<string, string>();
            foreach (var fk in ForeignKeys.Where(x => x.ParentEntityType == typeof(T)))
            {
                for (int i = 0; i < fk.ForeignKey.PropertyNames.Length; i++)
                {
                    // Get the foreign key attribute name
                    string childColName = fk.ForeignKey.PropertyNames[i];
                    string parentColName = fk.Reference.PropertyNames[i];

                    // Add the foreign key column name and the parent table name
                    foreignKeys[childColName] = parentColName;
                }
            }

            return foreignKeys;
        }
    }
}
