using CrossLite.QueryBuilder;
using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.Linq;

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

                // Grab the foreign key constraints
                foreach (var group in ForeignKeyValues)
                {
                    // Append each key => value to the query
                    foreach (var kvp in group)
                    {
                        // Get the attribute name and value
                        string attrName = kvp.Key;
                        object attrValue = kvp.Value;
                        query.Where(attrName, Comparison.Equals, attrValue);
                    }

                    // Create a new clause, to seperate by an OR
                    query.WhereStatement.CreateNewClause();
                }

                return query.ExecuteScalar<int>();
            }
        }

        /// <summary>
        /// Creates a new instance of <see cref="ChildDbSet{TParentEntity, TChildEntity}"/>
        /// </summary>
        /// <param name="entity"></param>
        /// <param name="context"></param>
        public ChildDbSet(TParentEntity entity, SQLiteContext context)
        {
            Context = context;
            Entity = entity;
            ConnectionString = context.ConnectionString;
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
                    // Initialize the foreign key values dictionary
                    var collection = new Dictionary<string, object>();
                    ForeignKeyValues[i++] = collection;

                    // Get the value of the foreign key attribute from the parent entity
                    for (int j = 0; j < fkinfo.ForeignKey.Attributes.Length; j++)
                    {
                        string attrName = fkinfo.ForeignKey.Attributes[j];
                        string parentColName = fkinfo.Reference.Attributes[j];

                        // Add column expression
                        AttributeInfo attribute = ParentTable.GetAttributeByColumnName(parentColName);
                       collection.Add(attrName, attribute.Property.GetValue(Entity));
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

            // Begin a new Select Query
            SelectQueryBuilder query = new SelectQueryBuilder(context);
            query.From(ChildTable.TableName).SelectAll();

            // Grab the foreign key constraints
            foreach (var group in ForeignKeyValues)
            {
                // Append each key => value to the query
                foreach (var kvp in group)
                {
                    // Get the attribute name and value
                    string attrName = kvp.Key;
                    object attrValue = kvp.Value;
                    query.Where(attrName, Comparison.Equals, attrValue);
                }

                // Create a new clause, to seperate by an OR
                query.WhereStatement.CreateNewClause();
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
        /// <exception cref="InvalidOperationException">Thrown if updating an Entity, and changing a foreign key attribute that is also a primary key.</exception>
        public override void Add(TChildEntity entity)
        {
            // Ensure we have the table mappings, and load the context
            bool wasOpen = false;
            SQLiteContext context = LazyLoad(ref wasOpen);

            // Get foreign key properties for the child entity, and the parent entity
            ChildTable.ForeignKeys
                .Where(fk => fk.ParentEntityType == ParentTable.EntityType)
                .ToList()
                .ForEach(fk =>
                {
                    for (int i = 0; i < fk.ForeignKey.Attributes.Length; i++)
                    {
                        // Get the foreign key attribute name
                        string childColName = fk.ForeignKey.Attributes[i];
                        string parentColName = fk.Reference.Attributes[i];

                        // Get the value of the foreign key attribute from the parent entity
                        object parentAttrValue = ParentTable.GetAttributeByColumnName(parentColName).Property.GetValue(Entity);

                        // Set the foreign key value on the child entity
                        ChildTable.GetAttributeByColumnName(childColName).Property.SetValue(entity, parentAttrValue);
                    }
                });

            // Insert or Update the child entity
            ChildCollection.AddOrUpdate(entity);
        }

        public override void Remove(TChildEntity entity)
        {
            // Ensure we have the table mappings, and load the context
            bool wasOpen = false;
            SQLiteContext context = LazyLoad(ref wasOpen);

            
        }

        public override void Clear()
        {
            // Ensure we have the table mappings, and load the context
            bool wasOpen = false;
            SQLiteContext context = LazyLoad(ref wasOpen);

            
        }

        public override bool Contains(TChildEntity entity)
        {
            // Ensure we have the table mappings, and load the context
            bool wasOpen = false;
            SQLiteContext context = LazyLoad(ref wasOpen);

            return ChildCollection.Contains(entity);
        }
    }
}
