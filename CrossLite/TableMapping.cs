using CrossLite.CodeFirst;
using System;
using System.Collections.Generic;
using System.Linq;
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
        /// Gets a collection of keys on this table, that are considered Primary Keys. These are the column names, 
        /// not the Property names in the Entity. By default, the column names are the same as the Property names,
        /// but this can be overridden using the <see cref="ColumnAttribute"/> on the Entity.
        /// </summary>
        public IReadOnlyCollection<string> PrimaryKeys { get; protected set; }

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
        /// Creates a new instance of <see cref="TableMapping"/>
        /// </summary>
        /// <param name="entityType"></param>
        public TableMapping(Type entityType)
        {
            // Set critical props
            this.EntityType = entityType;
            this.TableName = entityType.Name;
            this.ParentRelationships = new Dictionary<PropertyInfo, Type>();
            this.ChildRelationships = new Dictionary<PropertyInfo, Type>();

            // Get table related instructions
            var tableAttr = (TableAttribute)entityType.GetCustomAttribute(typeof(TableAttribute));
            if (tableAttr != null)
            {
                this.TableName = tableAttr.Name ?? entityType.Name;
                this.WithoutRowID = tableAttr.WithoutRowID;
                this.BuildInstanceForeignKeys = tableAttr.BuildInstanceRelationships;
            }

            // Get table related composite indexies
            var compAttr = entityType.GetCustomAttributes(typeof(CompositeIndexAttribute)).ToArray();
            if (compAttr != null && compAttr.Length > 0)
            {
                this.CompositeIndexes = new CompositeIndexAttribute[compAttr.Length];
                for (int i = 0; i < compAttr.Length; i++)
                {
                    this.CompositeIndexes[i] = (CompositeIndexAttribute)compAttr[i];
                }
            }
            else
            {
                this.CompositeIndexes = [];
            }

            // Get a list of props from the Entity that represents an Attribute
            var entityProps = entityType.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            // Temporary variables
            Dictionary<string, AttributeInfo> cols = new Dictionary<string, AttributeInfo>(entityProps.Length);
            Dictionary<string, AttributeInfo> props = new Dictionary<string, AttributeInfo>(entityProps.Length);
            List<AttributeInfo> primaryKeys = new List<AttributeInfo>();

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
                            info.IsIndexed = true;
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

                    // Add to column list
                    cols[info.ColumnName] = info;
                    props[info.Property.Name] = info;
                    HasVirtuals |= true;
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
                    // Check for EntitySet<T> without ForeignEntityLoader attribute
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
            DatabaseColumns = cols;
            EntityProperties = props;
            PrimaryKeys = [.. primaryKeys.OrderBy(x => x.Order).Select(x => x.ColumnName)];
            var foreignKeys = new HashSet<ForeignKeyConstraint>();

            // ------------------------------------
            // Always check foreign keys after sett ing the DatabaseColumns property!
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
                ReferencesAttribute inv = inverse ?? new ReferencesAttribute(fkey.Attributes);
                ForeignKeyConstraint info = new ForeignKeyConstraint(this, property.Name, parentType, fkey, inv);  
                foreignKeys.Add(info);

                // Foreign keys cannot be an alias for RowID!
                if (RowIdColumn != null && info.ForeignKey.Attributes.Contains(RowIdColumn.ColumnName))
                {
                    RowIdColumn = null;
                }
            }

            // Finally, set our class ForeignEntityLoader property
            ForeignKeys = foreignKeys;
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
            if (!DatabaseColumns.ContainsKey(attributeName))
                throw new Exception("Entity type \"" + EntityType.Name + "\" does not contain a definition for \"" + attributeName + "\"");

            return DatabaseColumns[attributeName];
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
            if (!EntityProperties.ContainsKey(propertyName))
                throw new Exception("Entity type \"" + EntityType.Name + "\" does not contain a definition for \"" + propertyName + "\"");

            return EntityProperties[propertyName];
        }

        /// <summary>
        /// Retrieves a set of property names corresponding to the specified column names.
        /// </summary>
        /// <remarks>This method maps column names to their corresponding property names based on the
        /// entity's column-to-property mapping. If a column name is not found in the mapping, an <see
        /// cref="EntityException"/> is thrown.</remarks>
        /// <param name="columns">A collection of column names to map to property names.</param>
        /// <returns>A <see cref="HashSet{T}"/> containing the names of the properties associated with the specified columns.</returns>
        /// <exception cref="EntityException">Thrown if any column in <paramref name="columns"/> does not exist in the entity.</exception>
        public HashSet<string> GetPropertiesFromColumns(IEnumerable<string> columns)
        {
            HashSet<string> props = new HashSet<string>();
            foreach (string col in columns)
            {
                if (DatabaseColumns.ContainsKey(col))
                {
                    props.Add(DatabaseColumns[col].Property.Name);
                }
                else
                {
                    throw new EntityException($"Column '{col}' does not exist in entity '{EntityType.Name}'");
                }
            }
            return props;
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
                if (EntityProperties.ContainsKey(prop))
                {
                    cols.Add(EntityProperties[prop].ColumnName);
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
        /// Retrieves a dictionary of foreign key column mappings for the specified entity type.
        /// </summary>
        /// <remarks>This method identifies foreign key relationships for the specified entity type by
        /// examining the foreign key metadata. The returned dictionary maps each foreign key column in the child entity
        /// to its corresponding column in the parent entity.</remarks>
        /// <typeparam name="T">The type of the entity for which foreign key mappings are retrieved.</typeparam>
        /// <param name="EnityType">An instance of the entity type. This parameter is not used internally but provides type context for the
        /// operation.</param>
        /// <returns>A dictionary where the keys represent the foreign key column names in the child entity,  and the values
        /// represent the corresponding column names in the parent entity.</returns>
        public Dictionary<string, string> GetForeignKeyColumnMappingsTo<T>()
        {
            Dictionary<string, string> foreignKeys = new Dictionary<string, string>();
            foreach (var fk in ForeignKeys.Where(x => x.ParentEntityType == typeof(T)))
            {
                for (int i = 0; i < fk.ForeignKey.Attributes.Length; i++)
                {
                    // Get the foreign key attribute name
                    string childColName = fk.ForeignKey.Attributes[i];
                    string parentColName = fk.Reference.Attributes[i];

                    // Add the foreign key column name and the parent table name
                    foreignKeys[childColName] = parentColName;
                }
            }

            return foreignKeys;
        }
    }
}
