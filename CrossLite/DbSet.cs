using CrossLite.QueryBuilder;
using Microsoft.Data.Sqlite;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace CrossLite
{
    /// <summary>
    /// A <see cref="DbSet{TEntity}"/> represents the collection
    /// of all Entities (rows of data) in the context that can be 
    /// queried from the database.
    /// </summary>
    /// <typeparam name="TEntity"></typeparam>
    public class DbSet<TEntity> : ICollection<TEntity>
        where TEntity : EntityBase, new()
    {
        /// <summary>
        /// Represents the set of allowed key types that can be used in the application.
        /// </summary>
        /// <remarks>This collection includes commonly used primitive and value types such as <see
        /// cref="int"/>,  <see cref="long"/>, <see cref="string"/>, <see cref="Guid"/>, <see cref="short"/>,  <see
        /// cref="byte"/>, and <see cref="decimal"/>.  It is intended to enforce type safety and restrict the types of
        /// keys that can be used.</remarks>
        private static readonly HashSet<Type> AllowedKeyTypes =
        [
            typeof(int), typeof(long), typeof(string), typeof(Guid),
            typeof(short), typeof(byte), typeof(decimal)
        ];

        /// <summary>
        /// The database context
        /// </summary>
        protected SQLiteContext Context { get; set; }

        /// <summary>
        /// Gets the <see cref="TableMapping"/> for this TChildEntity type
        /// </summary>
        protected TableMapping EntityTable { get; set; }

        /// <summary>
        /// Gets the record at the selected index
        /// </summary>
        /// <param name="index"></param>
        /// <returns>
        /// Returns a <see cref="TEntity"/> at the specified index within the database, 
        /// or null if the index is out of range
        /// </returns>
        public TEntity this[int index]
        {
            get
            {
                string table = Context.QuoteIdentifier(EntityTable.TableName);
                string query = $"SELECT * FROM {table} LIMIT 1 OFFSET {index}";
                return Context.Query<TEntity>(query).FirstOrDefault();
            }
        }

        /// <summary>
        /// Returns the total number of entities in the database
        /// </summary>
        public int Count
        {
            get
            {
                string table = Context.QuoteIdentifier(EntityTable.TableName);
                string query = $"SELECT COUNT(1) FROM {table}";
                return Context.ExecuteScalar<int>(query);
            }
        }

        /// <summary>
        /// Indicates whether this ICollection is read only
        /// </summary>
        public bool IsReadOnly => false;

        /// <summary>
        /// A prepared Insert query
        /// </summary>
        private PreparedNonQuery InsertQuery { get; set; }

        /// <summary>
        /// A prepared Delete query
        /// </summary>
        private PreparedNonQuery DeleteQuery { get; set; }

        /// <summary>
        /// Creates a new instance of <see cref="DbSet{TEntity}"/>
        /// </summary>
        /// <param name="context">An active SQLite connection</param>
        public DbSet(SQLiteContext context)
        {
            // Since this instance will live as long as the SQLiteContext,
            // we can store the open connection instead of the connection string
            Context = context;

            // Get our Table Mapping for thie TChildEntity type
            EntityTable = EntityCache.GetTableMap(typeof(TEntity));
        }

        /// <summary>
        /// Creates a new instance of the entity and associates it with the current context.
        /// </summary>
        /// <remarks>The created entity is linked to the specified entity table within the context. 
        /// Ensure that the context and entity table are properly configured before calling this method.</remarks>
        /// <returns>A new instance of the entity of type <typeparamref name="TEntity"/>.</returns>
        public TEntity Create()
        {
            return Context.CreateEntity<TEntity>(EntityTable);
        }

        /// <summary>
        /// Inserts a new Entity into the database. If the entity table has a single integer primary key, 
        /// the primary key value will be updated with the <see cref="SQLiteConnection.LastInsertRowId"/>.
        /// </summary>
        /// <remarks>This method utilizes the <see cref="PreparedNonQuery"/> to speed things along.</remarks>
        /// <param name="obj">The <see cref="TEntity"/> object to add to the dataset</param>
        public void Add(TEntity obj)
        {
            // For fetching the RowID
            AttributeInfo rowid = EntityTable.RowIdColumn;

            // Generate the SQL
            if (InsertQuery == null)
            {
                using (var query = new InsertQueryBuilder(EntityTable.TableName, Context))
                {
                    foreach (var attribute in EntityTable.DatabaseColumns)
                    {
                        // Grab value
                        PropertyInfo property = attribute.Value.Property;
                        bool isKey = attribute.Value.IsPrimaryKey;

                        // Check for integer primary keys
                        if (isKey && EntityTable.HasRowIdAlias && EntityTable.RowIdColumn == attribute.Value)
                        {
                            continue;
                        }

                        // Add attribute to the field list
                        query.Set(attribute.Key, new SqlLiteral($"@{attribute.Key}"));
                    }

                    InsertQuery = new PreparedNonQuery(query.BuildCommand());
                }
            }

            // Execute the SQL Command
            lock (InsertQuery)
            {
                InsertQuery.SetParameters(obj, EntityTable);
                int result = InsertQuery.Execute();

                // If the insert was successful, lets build our Entity relationships
                if (result > 0)
                {
                    // If we have a Primary key that is determined database side,
                    // than we can update the current object's key value here
                    if (EntityTable.HasRowIdAlias)
                    {
                        var selectCommand = Context.Connection.CreateCommand();
                        selectCommand.CommandText = "SELECT last_insert_rowid();";

                        // ExecuteScalar is used to get a single value
                        long lastId = (long)selectCommand.ExecuteScalar();
                        rowid.Property.SetValue(obj, Convert.ChangeType(lastId, rowid.Property.PropertyType));
                    }
                }
            }

            // Clear dirty properties after insert
            obj.DirtyProperties.Clear();
            obj.State = EntityState.Fresh;
        }

        /// <summary>
        /// Inserts a range of new Entities into the database
        /// </summary>
        /// <remarks>This method utilizes the <see cref="PreparedNonQuery"/> to speed things along.</remarks>
        /// <param name="obj">The <see cref="TEntity"/> objects to add to the dataset</param>
        public void AddRange(params TEntity[] entities)
        {
            foreach (TEntity obj in entities) Add(obj);
        }

        /// <summary>
        /// Inserts a range of new Entities into the database
        /// </summary>
        /// <remarks>This method utilizes the <see cref="PreparedNonQuery"/> to speed things along.</remarks>
        /// <param name="collection">The <see cref="TEntity"/> objects to add to the dataset</param>
        public void AddRange(IEnumerable<TEntity> collection)
        {
            foreach (TEntity obj in collection) Add(obj);
        }

        /// <summary>
        /// If the Entity exists in the database already, than it is updated with
        /// the new values, otherwise the Entity object is inserted into the database
        /// </summary>
        /// <param name="obj">The <see cref="TEntity"/> object to add or update in the dataset</param>
        /// <returns></returns>
        public void AddOrUpdate(TEntity obj)
        {
            if (Contains(obj))
                Update(obj);
            else
                Add(obj);
        }

        /// <summary>
        /// Checks to see if the Entity exists in the database already. If not, the
        /// entity is added to the database.
        /// </summary>
        /// <param name="obj">The <see cref="TEntity"/> object to add to the dataset</param>
        /// <returns>true if the entity did not exist and was added to the database; false otherwise.</returns>
        public bool AddIfNotExists(TEntity obj)
        {
            // If this entity exists already, return false
            if (Contains(obj))
                return false;

            Add(obj);
            return true;
        }

        /// <summary>
        /// Deletes an Entity from the database
        /// </summary>
        /// <remarks>This method utilizes the <see cref="PreparedNonQuery"/> to speed things along.</remarks>
        /// <param name="obj">The <see cref="TEntity"/> object to remove from the dataset</param>
        /// <returns>true if an entity was removed from the dataset; false otherwise.</returns>
        public bool Remove(TEntity obj)
        {
            // Generate the SQL
            if (DeleteQuery == null)
            {
                // Start the query using a query builder
                var builder = new DeleteQueryBuilder(Context).From(EntityTable.TableName);

                // build the where statement, using primary keys only
                foreach (string keyName in EntityTable.PrimaryKeys)
                {
                    PropertyInfo info = EntityTable.DatabaseColumns[keyName].Property;
                    builder.Where(keyName, Comparison.Equals, new SqlLiteral($"@{keyName}"));
                }

                DeleteQuery = new PreparedNonQuery(builder.BuildCommand());
            }

            // Execute the SQL Command
            lock (DeleteQuery)
            {
                obj.State = EntityState.Deleted;
                DeleteQuery.SetParameters(obj, EntityTable);
                return DeleteQuery.Execute() > 0;
            }
        }

        /// <summary>
        /// Deletes a range of new Entities into the database
        /// </summary>
        /// <remarks>This method utilizes the <see cref="PreparedNonQuery"/> to speed things along.</remarks>
        /// <param name="obj">The <see cref="TEntity"/> objects to remove from the dataset</param>
        public void RemoveRange(params TEntity[] entities)
        {
            foreach (TEntity obj in entities) Remove(obj);
        }

        /// <summary>
        /// Deletes a range of new Entities into the database
        /// </summary>
        /// <remarks>This method utilizes the <see cref="PreparedNonQuery"/> to speed things along.</remarks>
        /// <param name="collection">The <see cref="TEntity"/> objects to remove from thedataset</param>
        public void RemoveRange(IEnumerable<TEntity> collection)
        {
            foreach (TEntity obj in collection) Remove(obj);
        }

        /// <summary>
        /// Updates an Entity in the database, provided that none of the Primary
        /// keys were modified.
        /// </summary>
        /// <remarks>This method utilizes the <see cref="PreparedNonQuery"/> to speed things along.</remarks>
        /// <param name="obj">The <see cref="TEntity"/> object to update in the dataset</param>
        /// <returns>true if any records in the database were affected; false otherwise.</returns>
        /// <exception cref="InvalidOperationException">
        /// Thrown if the Entity has modified any of its Primary Key(s).
        /// </exception>
        public bool Update(TEntity obj)
        {
            // Ensure that we are in a modified state before checking for dirty properties
            if (obj.State == EntityState.Modified)
            {
                // Ensure that the primary key or composite key is not modified
                var dirtyColumns = EntityTable.GetColumnsFromProperties(obj.DirtyProperties);
                if (EntityTable.PrimaryKeys.Intersect(dirtyColumns).Any())
                {
                    throw new InvalidOperationException("Cannot update an entity with modified primary key(s).");
                }
            }

            // if there are no dirty properties, return false
            if (obj.DirtyProperties.Count == 0)
                return false;

            // If we have no dirty properties, then we update
            using (var updateQuery = new UpdateQueryBuilder(EntityTable.TableName, Context))
            {
                // Generate the SQL
                foreach (var attribute in EntityTable.DatabaseColumns)
                {
                    PropertyInfo info = attribute.Value.Property;

                    // Keys go in the WHERE statement, not the SET statement
                    if (EntityTable.PrimaryKeys.Contains(attribute.Key))
                    {
                        updateQuery.Where(attribute.Key, Comparison.Equals, info.GetValue(obj));
                    }
                    else if (obj.DirtyProperties.Contains(attribute.Value.Property.Name))
                    {
                        updateQuery.Set(attribute.Key, info.GetValue(obj));
                    }
                }

                // Update parameters and execute the SQL Command
                bool result = updateQuery.Execute() > 0;

                // Clear dirty properties after update
                if (result)
                {
                    obj.DirtyProperties.Clear();
                    obj.State = EntityState.Fresh;
                }

                return result;
            } 
        }

        /// <summary>
        /// This method will requery an entity from the database, refreshing
        /// the values of all attributes to match that in the database.
        /// </summary>
        /// <param name="entity">The entity object to reload attributes to</param>
        /// <returns>
        /// true if the entity was successfully retrieved from the databse 
        /// and its attributes reloaded; false otherwise
        /// </returns>
        public bool Reload(ref TEntity entity)
        {
            // Begin a new Select Query
            SelectQueryBuilder query = new SelectQueryBuilder(Context);
            query.From(EntityTable.TableName).SelectAll().Take(1);

            // Grab the primary keys
            foreach (string colName in EntityTable.PrimaryKeys)
            {
                // Add column expression
                AttributeInfo attribute = EntityTable.GetAttributeByColumnName(colName);
                query.Where(colName, Comparison.Equals, attribute.Property.GetValue(entity));
            }

            // Create command
            using (SqliteCommand command = query.BuildCommand())
            using (SqliteDataReader reader = command.ExecuteReader())
            {
                // Do we have a result?
                if (reader.HasRows)
                {
                    // Read the row
                    reader.Read();
                    entity = Context.ConvertToEntity<TEntity>(EntityTable, reader);

                    // Close reader and return positive
                    reader.Close();
                    return true;
                }
                else
                {
                    reader.Close();
                    return false;
                }
            }
        }

        /// <summary>
        /// Returns whether an Entity exists in the database, by comparing its 
        /// Primary/Composite Key(s).
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        public bool Contains(TEntity obj)
        {
            // Create a WHERE statement
            WhereStatement where = new WhereStatement(Context);

            // build the where statement, using primary keys
            foreach (string keyName in EntityTable.PrimaryKeys)
            {
                PropertyInfo info = EntityTable.DatabaseColumns[keyName].Property;
                object val = info.GetValue(obj);

                // Add value to where statement
                where.And(keyName, Comparison.Equals, val);
            }

            return Contains(EntityTable.TableName, where);
        }

        internal bool Contains(string tableName, WhereStatement where)
        {
            // Build the SQL query
            List<SqliteParameter> parameters;
            string sql = String.Format("SELECT EXISTS(SELECT 1 FROM {0} WHERE {1} LIMIT 1);",
                Context.QuoteIdentifier(tableName),
                where.BuildStatement(out parameters)
            );

            // Execute the command
            using (SqliteCommand command = Context.CreateCommand(sql))
            {
                command.Parameters.AddRange(parameters.ToArray());
                return Context.ExecuteScalar<int>(command) == 1;
            }
        }

        /// <summary>
        /// Deletes all records from the database table.
        /// </summary>
        public void Clear()
        {
            // Build the SQL query
            string table = Context.QuoteIdentifier(EntityTable.TableName);
            string sql = $"DELETE FROM {table}";
            using (SqliteCommand command = Context.CreateCommand(sql))
                command.ExecuteNonQuery();
        }

        /// <summary>
        /// Finds and returns an entity of type <typeparamref name="TEntity"/> by its primary key.
        /// </summary>
        /// <remarks>This method performs a database query to locate the entity with the specified primary
        /// key. It assumes that the entity type has a primary key alias (e.g., RowID) defined in the database
        /// schema.</remarks>
        /// <typeparam name="TKey">The type of the primary key.</typeparam>
        /// <param name="id">The value of the primary key to search for. Must match the type of the entity's primary key.</param>
        /// <returns>The entity of type <typeparamref name="TEntity"/> that matches the specified primary key,  or <see
        /// langword="null"/> if no matching entity is found.</returns>
        /// <exception cref="InvalidOperationException">Thrown if the entity type does not have a primary key alias defined.</exception>
        public TEntity Find<TKey>(TKey id)
        {
            // Ensure we have a RowID alias
            if (EntityTable.PrimaryKeys.Count > 1)
                throw new InvalidOperationException("Cannot use Find<TKey>(TKey id) on an entity with a composite primary key. Use Find(params object[] keyValues) instead.");

            // Ensure id is not null
            if (id == null)
                throw new ArgumentNullException(nameof(id), "The primary key value cannot be null.");

            // Inside your Find method, before the database logic
            if (!AllowedKeyTypes.Contains(typeof(TKey)))
            {
                throw new NotSupportedException(
                    $"The key type '{typeof(TKey).Name}' is not supported. " +
                    "Only primitive types, strings, and Guids can be used as primary keys."
                );
            }

            // Build the SQL query
            var primaryKey = EntityTable.PrimaryKeys.First();
            var query = new SelectQueryBuilder(Context)
                .From(EntityTable.TableName)
                .SelectAll()
                .Where(primaryKey, Comparison.Equals, id)
                .Take(1);

            // Create command
            var command = query.BuildCommand();
            return Context.ConvertToEntity<TEntity>(EntityTable, command.ExecuteReader());
        }

        /// <summary>
        /// Finds and retrieves an entity of type <typeparamref name="TEntity"/> based on the specified primary key
        /// values.
        /// </summary>
        /// <remarks>This method constructs a query to retrieve the entity from the database using the
        /// provided primary key values. If no entity matches the specified keys, the method returns <see
        /// langword="null"/>.</remarks>
        /// <param name="keyValues">An array of primary key values used to locate the entity. The order of the values must match the order of
        /// the primary keys defined for the entity.</param>
        /// <returns>The entity of type <typeparamref name="TEntity"/> if found; otherwise, <see langword="null"/>.</returns>
        public TEntity Find(params object[] keyValues)
        {
            // Build the SQL query
            var query = new SelectQueryBuilder(Context)
                .From(EntityTable.TableName)
                .SelectAll()
                .Take(1);
            query.WhereStatement.InnerClauseOperator = LogicOperator.And;

            // Ensure we have the correct number of key values
            if (keyValues.Length != EntityTable.PrimaryKeys.Count)
                throw new ArgumentException("The number of key values provided does not match the number of primary keys.", nameof(keyValues));

            // build the where statement, using primary keys
            int i = 0;
            foreach (string keyName in EntityTable.PrimaryKeys)
            {
                // Grab value
                var value = keyValues[i];
                if (value == null)
                    throw new ArgumentNullException(nameof(value), "A primary key value cannot be null.");

                // Add value to where statement
                PropertyInfo info = EntityTable.DatabaseColumns[keyName].Property;
                query.Where(keyName, Comparison.Equals, value);
                i++;
            }

            // Create command
            var command = query.BuildCommand();
            return Context.ConvertToEntity<TEntity>(EntityTable, command.ExecuteReader());
        }

        /// <summary>
        /// Copies the entities in this DbSet to an Array, starting at a particular Array index.
        /// </summary>
        /// <param name="array">
        /// The one-dimensional Array that is the destination of the elements copied from ICollection. 
        /// The Array must have zero-based indexing.
        /// </param>
        /// <param name="arrayIndex">
        /// The zero-based index in array at which copying begins.
        /// </param>
        public void CopyTo(TEntity[] array, int arrayIndex)
        {
            // Ensure we have an array to work with
            if (array == null)
                throw new ArgumentNullException("array");

            int i = arrayIndex;
            foreach (TEntity entity in Context.Select<TEntity>())
            {
                array[i++] = entity;
            }
        }

        /// <summary>
        /// Selects all of the records in the database, and returns the Enumerator
        /// </summary>
        public IEnumerator<TEntity> GetEnumerator() => Context.Select<TEntity>().GetEnumerator();

        /// <summary>
        /// Selects all of the records in the database, and returns the Enumerator
        /// </summary>
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
