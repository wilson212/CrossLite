using CrossLite.QueryBuilder;
using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace CrossLite.CodeFirst
{
    /// <summary>
    /// This object is used to Lazy load Child Entities that are
    /// bound to the Parent Entity via a Foreign Key relationship.
    /// </summary>
    /// <typeparam name="TParentEntity"></typeparam>
    /// <typeparam name="TChildEntity"></typeparam>
    public class ChildDbSet<TParentEntity, TChildEntity> : EntitySet<TChildEntity>
        where TParentEntity : EntityBase
        where TChildEntity : EntityBase, new()
    {
        protected SQLiteContext Context { get; set; }

        /// <summary>
        /// The SQLite connection string from this Entity
        /// </summary>
        protected string ConnectionString { get; set; }

        /// <summary>
        /// The Parent Entity instance that the Child Entities are bound to
        /// </summary>
        protected TParentEntity Entity { get; set; }

        /// <summary>
        /// Gets or sets the parent table mapping associated with this table mapping.
        /// </summary>
        protected TableMapping ParentTable { get; set; } = null;

        /// <summary>
        /// Gets or sets the child table mapping associated with this instance.
        /// </summary>
        protected TableMapping ChildTable { get; set; } = null;

        /// <summary>
        /// Gets or sets an array of dictionaries representing foreign key values on the parent entity.
        /// </summary>
        protected Dictionary<string, object>[] ForeignKeyValues { get; set; } = null;

        /// <summary>
        /// Gets or sets the collection of child entities of type <typeparamref name="TChildEntity"/>  managed by the
        /// database context.
        /// </summary>
        protected DbSet<TChildEntity> ChildCollection { get; set; }

        /// <summary>
        /// Gets or sets the attribute that defines the inverse foreign key relationship.
        /// </summary>
        protected InverseForeignKeyAttribute InverseForeignKeyAttribute { get; set; }

        /// <summary>
        /// Returns the total number of entities in the database
        /// </summary>
        public override int Count
        {
            get
            {
                // Ensure we have the table mappings, and load the context
                bool wasOpen = false;
                SQLiteContext context = LazyLoad(ref wasOpen);

                // Begin a new Select Query
                SelectQueryBuilder query = new SelectQueryBuilder(context);
                query.From(ChildTable.TableName).SelectCount();
                var whereStatement = query.WhereStatement;

                // Grab the foreign key constraints
                foreach (var group in ForeignKeyValues)
                {
                    // Append each key => value to the query
                    foreach (var kvp in group)
                    {
                        // Get the parentAttr name and value
                        string attrName = kvp.Key;
                        object attrValue = kvp.Value;
                        whereStatement.And(attrName, Comparison.Equals, attrValue);
                    }

                    // Create a new clause, to seperate by an OR
                    whereStatement.CreateNewClause();
                }

                return query.ExecuteScalar<int>();
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ChildDbSet{TParentEntity}"/> class, associating it with a
        /// parent entity and its corresponding property in the database context.
        /// </summary>
        /// <remarks>This constructor establishes the necessary foreign key constraint to enable lazy
        /// loading of the related data.</remarks>
        /// <param name="entity">The parent entity to which this child set is related. Cannot be <see langword="null"/>.</param>
        /// <param name="property">The property containing the ID on the parent entity that represents the relationship. Cannot be <see langword="null"/>.</param>
        /// <param name="context">The database context used to manage the connection and operations. Cannot be <see langword="null"/>.</param>
        public ChildDbSet(TParentEntity entity, PropertyInfo parentProperty, SQLiteContext context)
        {
            Context = context ?? throw new ArgumentNullException(nameof(context));
            Entity = entity ?? throw new ArgumentNullException(nameof(entity));
            ConnectionString = context.ConnectionString;

            // GET INVERSE FOREIGN KEY HERE
            var inverseAttr = parentProperty.GetCustomAttribute<InverseForeignKeyAttribute>();
            if (inverseAttr != null)
            {
                InverseForeignKeyAttribute = inverseAttr;
            }
        }

        /// <summary>
        /// Lazily initializes and retrieves a database context for the current operation, ensuring that necessary table
        /// mappings and foreign key values are loaded.
        /// </summary>
        /// <remarks>This method ensures that the parent and child table mappings are initialized and that
        /// foreign key values are loaded if they are not already available. If the database context is not already
        /// connected,  a new connection is established using the provided connection string.</remarks>
        /// <param name="wasOpen">A reference parameter that indicates whether the database context was already open.  Set to <see
        /// langword="true"/> if the context was already connected; otherwise, <see langword="false"/>.</param>
        /// <returns>An instance of <see cref="SQLiteContext"/> representing the database context to use for the operation.</returns>
        protected SQLiteContext LazyLoad(ref bool wasOpen)
        {
            // Grab table mappings
            if (ParentTable == null)
            {
                // Get the table mapping for the parent entity
                ParentTable = EntityCache.GetTableMap(typeof(TParentEntity));
            }
            if (ChildTable == null)
            {
                // Get the table mapping for the child entity
                ChildTable = EntityCache.GetTableMap(typeof(TChildEntity));
            }

            // If we already have the foreign key values, skip loading them
            if (ForeignKeyValues == null)
            {
                // Get the foreign key constraints for the parent entity
                var fkinfos = ChildTable.ForeignKeys.Where(x => x.ParentEntityType == ParentTable.EntityType).ToArray();

                // Initialize the foreign key values dictionary
                int i = 0;
                ForeignKeyValues = new Dictionary<string, object>[fkinfos.Length];

                // Itterate through each foreign key constraints
                foreach (ForeignKeyConstraint fkinfo in fkinfos)
                {
                    // If we have an InverseForeignKeyAttribute, ensure it matches for foreign key selection
                    if (InverseForeignKeyAttribute != null)
                    {
                        bool matches = fkinfo.ForeignKey.PropertyNames.SequenceEqual(InverseForeignKeyAttribute.Attributes);
                        if (!matches)
                            continue;
                    }

                    // Initialize the foreign key values dictionary
                    var collection = new Dictionary<string, object>();
                    ForeignKeyValues[i++] = collection;

                    // Get the value of the foreign key parentAttr from the parent entity
                    for (int j = 0; j < fkinfo.ForeignKey.PropertyNames.Length; j++)
                    {
                        string childPropName = fkinfo.ForeignKey.PropertyNames[j];
                        string parentPropName = fkinfo.Reference.PropertyNames[j];

                        // Add column expression
                        AttributeInfo parentAttr = ParentTable.GetAttributeByPropertyName(parentPropName);
                        AttributeInfo childAttr = ChildTable.GetAttributeByPropertyName(childPropName);
                        collection.Add(childAttr.ColumnName, parentAttr.Property.GetValue(Entity));
                    }
                }
            }

            SQLiteContext context = null;
            wasOpen = false;

            // If we already have a context, use it
            if (Context.IsConnected())
            {
                wasOpen = true;
                context = Context;
            }
            else
            {
                // Open new connection
                context = new SQLiteContext(ConnectionString);
                context.Connect();
            }

            // Lazy Load the DbSet for the child entity
            if (ChildCollection == null)
            {
                ChildCollection = new DbSet<TChildEntity>(context);
            }

            return context;
        }

        /// <summary>
        /// Lazy loads the child entities of a foreign key constraint
        /// </summary>
        public override IEnumerator<TChildEntity> GetEnumerator()
        {
            // Ensure we have the table mappings, and load the context
            bool wasOpen = false;
            SQLiteContext context = LazyLoad(ref wasOpen);

            // Check if any of our key values have changed (dirty properties)
            if (Entity.DirtyProperties.Count > 0)
            {
                // @todo: Re-evaluate ForeignKeyValues
            }

            // Begin a new Select Query
            SelectQueryBuilder query = new SelectQueryBuilder(context);
            query.From(ChildTable.TableName).SelectAll();
            var whereStetement = query.WhereStatement;

            // Grab the foreign key constraints
            foreach (var group in ForeignKeyValues)
            {
                // Append each key => value to the query
                foreach (var kvp in group)
                {
                    // Get the parentAttr name and value
                    string colName = kvp.Key;
                    object attrValue = kvp.Value;
                    whereStetement.And(colName, Comparison.Equals, attrValue);
                }

                // Create a new clause, to seperate by an OR
                whereStetement.CreateNewClause();
            }

            // Create the SQL Command
            using (SqliteCommand command = query.BuildCommand())
            using (SqliteDataReader reader = command.ExecuteReader())
            {
                // If we have rows, return each row
                while (reader.Read())
                    yield return context.ConvertToEntity<TChildEntity>(ChildTable, reader);

                // Cleanup
                reader.Close();
            }

            // Dispose
            if (!wasOpen)
            {
                context.Dispose();
            }
        }

        /// <summary>
        /// Adds a child entity to the collection, ensuring that foreign key relationships between the parent and child
        /// entities are properly established.
        /// </summary>
        /// <remarks>This method automatically sets the foreign key values on the child entity  based on
        /// the corresponding attributes of the parent entity. If the child entity already exists in the collection, it
        /// will be updated instead of added.</remarks>
        /// <param name="entity">The child entity to add. Cannot be null.</param>
        /// <exception cref="InvalidOperationException">Thrown if updating an Entity, and changing a foreign key parentAttr that is also a primary key.</exception>
        public override void Add(TChildEntity entity)
        {
            // Ensure we have the table mappings, and load the context
            bool wasOpen = false;
            SQLiteContext context = LazyLoad(ref wasOpen);

            // Get foreign key properties for the child entity, and the parent entity
            foreach (var group in ForeignKeyValues)
            {
                // Set each foreign key value on the child entity
                foreach (var kvp in group)
                {
                    string childColName = kvp.Key;
                    object parentAttrValue = kvp.Value;

                    // Get the child attribute info
                    AttributeInfo childAttr = ChildTable.GetAttributeByColumnName(childColName);

                    // If we are updating an entity, and the foreign key is also a primary key, throw an exception
                    if (ChildCollection.Contains(entity))
                    {
                        if (ChildTable.PrimaryKeys.Any(x => x.ColumnName == childColName))
                        {
                            object currentValue = childAttr.Property.GetValue(entity);
                            if (!currentValue.Equals(parentAttrValue))
                            {
                                throw new InvalidOperationException($"Cannot change the value of foreign key attribute \"{childAttr.Property.Name}\" on entity \"{typeof(TChildEntity)}\" because it is also a primary key.");
                            }
                        }
                    }

                    // Set the foreign key value on the child entity
                    childAttr.Property.SetValue(entity, parentAttrValue);
                }
            }

            // Insert or Update the child entity
            ChildCollection.AddOrUpdate(entity);

            // Dispose
            if (!wasOpen)
            {
                context.Dispose();
            }
        }

        /// <summary>
        /// Removes the specified child entity from the current context.
        /// </summary>
        /// <remarks>This method disassociates the specified child entity from the parent entity.  Ensure
        /// that the entity is part of the current context before calling this method.</remarks>
        /// <param name="entity">The child entity to remove. Cannot be <see langword="null"/>.</param>
        public override void Remove(TChildEntity entity)
        {
            bool wasOpen = false;
            SQLiteContext context = LazyLoad(ref wasOpen);

            try
            {
                Disassociate(entity, context);
            }
            finally
            {
                if (!wasOpen)
                {
                    context.Dispose();
                }
            }
        }

        /// <summary>
        /// Removes all items from the collection and disassociates them.
        /// </summary>
        /// <remarks>This method clears the collection by disassociating each item before removal.  Any
        /// necessary cleanup or state changes related to the disassociation process  are performed for each
        /// item.</remarks>
        public override void Clear()
        {
            bool wasOpen = false;
            SQLiteContext context = LazyLoad(ref wasOpen);

            try
            {
                foreach (var item in this)
                {
                    Disassociate(item, context);
                }
            }
            finally
            {
                if (!wasOpen)
                {
                    context.Dispose();
                }
            }
        }

        /// <summary>
        /// Determines whether the specified entity exists in the collection.
        /// </summary>
        /// <param name="entity">The entity to locate in the collection.</param>
        /// <returns><see langword="true"/> if the specified entity is found in the collection; otherwise, <see
        /// langword="false"/>.</returns>
        public override bool Contains(TChildEntity entity)
        {
            // Ensure we have the table mappings, and load the context
            bool wasOpen = false;
            SQLiteContext context = LazyLoad(ref wasOpen);

            var value = ChildCollection.Contains(entity);

            if (!wasOpen)
            {
                context.Dispose();
            }

            return value;
        }

        /// <summary>
        /// Disassociates the specified child entity from its parent entities by clearing the foreign key constraints in
        /// the database.
        /// </summary>
        /// <remarks>This method updates the database to remove the association between the specified
        /// child entity and its parent entities. It clears the foreign key constraints by setting the corresponding
        /// columns to null. The operation is performed within the context of the associated SQLite database.</remarks>
        /// <param name="item">The child entity to be disassociated. This entity's foreign key values will be set to null in the database.</param>
        private void Disassociate(TChildEntity item, SQLiteContext context)
        {
            // Check if the item is new
            if (item.State == EntityState.New)
            {
                // Item is not in database, nothing to disassociate
                return;
            }

            // Ensure we have the table mappings, and load the context
            UpdateQueryBuilder query = new(context);
            query.SetTable(ChildTable.TableName);
            var whereStetement = query.WhereStatement;

            // Grab the foreign key constraints
            foreach (var group in ForeignKeyValues)
            {
                // Append each key => value to the query
                foreach (var kvp in group)
                {
                    // Get the parentAttr name and value
                    string colName = kvp.Key;
                    object attrValue = kvp.Value;

                    // Check if this foreign key value can be null
                    var childAttr = ChildTable.GetAttributeByColumnName(colName);
                    if (!childAttr.IsNullable)
                    {
                        throw new InvalidOperationException($"Cannot disassociate entity \"{typeof(TChildEntity)}\" because foreign key attribute \"{childAttr.Property.Name}\" is not nullable.");
                    }

                    // Add where clause
                    whereStetement.And(colName, Comparison.Equals, attrValue);
                    query.Set(colName, null);
                }

                // Create a new clause, to seperate by an OR
                whereStetement.CreateNewClause();
            }

            // Try and execute the command
            using (SqliteCommand command = query.BuildCommand())
            {
                command.ExecuteNonQuery();
            }
        }
    }
}
