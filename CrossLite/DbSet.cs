using CrossLite.QueryBuilder;
using Microsoft.Data.Sqlite;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Linq.Expressions;

namespace CrossLite
{
    /// <summary>
    /// A <see cref="DbSet{TEntity}"/> represents the collection
    /// of all Entities (rows of data) in the context that can be 
    /// queried from the database.
    /// </summary>
    /// <typeparam name="TEntity"></typeparam>
    public class DbSet<TEntity> : IDisposable, ICollection<TEntity> where TEntity : EntityBase, new()
    {
        /// <summary>
        /// Represents the set of allowed key types that can be used in the application.
        /// </summary>
        private static readonly HashSet<Type> AllowedKeyTypes =
        [
            typeof(int), typeof(long), typeof(string), typeof(Guid),
            typeof(short), typeof(byte), typeof(decimal)
        ];

        /// <summary>
        /// The database context
        /// </summary>
        internal SQLiteContext Context { get; private set; }

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
        /// Event fired when a single entity is added to the <see cref="DbSet{TEntity}"/> using the following methods:
        /// <list type="bullet">
        ///     <item><see cref="AddIfNotExists(TEntity)"/></item>
        ///     <item><see cref="AddOrUpdate(TEntity)"/></item>
        ///     <item><see cref="Add(TEntity)"/></item>
        /// </list>
        /// </summary>
        public event Action<TEntity> EntityAdded;

        /// <summary>
        /// Event fired when a single entity is updated in the <see cref="DbSet{TEntity}"/> using the following methods:
        /// <list type="bullet">
        ///     <item><see cref="AddOrUpdate(TEntity)"/></item>
        ///     <item><see cref="Update(TEntity)"/></item>
        /// </list>
        /// </summary>
        public event Action<TEntity> EntityUpdated;

        /// <summary>
        /// Event fired when a single entity is removed from the <see cref="DbSet{TEntity}"/>.
        ///  using the following methods:
        /// <list type="bullet">
        ///     <item><see cref="Remove(TEntity)"/></item>
        /// </list>
        /// </summary>
        public event Action<TEntity> EntityRemoved;

        /// <summary>
        /// Event fired when a range of entities is added to the <see cref="DbSet{TEntity}"/> using the following methods:
        /// <list type="bullet">
        ///     <item><see cref="AddRange(IEnumerable{TEntity})"/></item>
        ///     <item><see cref="AddRange(TEntity[])"/></item>
        /// </list>
        /// <para>
        ///     This event does NOT fire off when <see cref="BulkInsert(IEnumerable{TEntity})"/> is called.
        /// </para>
        /// </summary>
        public event Action<IEnumerable<TEntity>> EntitiesAdded;

        /// <summary>
        /// Event fired when a range of entities is updated in the <see cref="DbSet{TEntity}"/> using the following methods:
        /// <list type="bullet">
        ///     <item><see cref="UpdateRange(IEnumerable{TEntity})"/></item>
        ///     <item><see cref="UpdateRange(TEntity[])"/></item>
        /// </list>
        /// <para>
        ///     This event does NOT fire off when <see cref="BulkUpdate(Action{UpdateQueryBuilder})"/> is called.
        /// </para>
        /// </summary>
        public event Action<IEnumerable<TEntity>> EntitiesUpdated;

        /// <summary>
        /// Event fired when a range of entities is removed from the <see cref="DbSet{TEntity}"/> using the following methods:
        /// <list type="bullet">
        ///     <item><see cref="RemoveRange(IEnumerable{TEntity})"/></item>
        ///     <item><see cref="RemoveRange(TEntity[])"/></item>
        /// </list>
        /// <para>
        ///     This event does NOT fire off when <see cref="BulkDelete(Action{DeleteQueryBuilder})"/> is called.
        /// </para>
        /// </summary>
        public event Action<IEnumerable<TEntity>> EntitiesRemoved;
        
        private SqliteCommand _lastRowIdCommand;

        /// <summary>
        /// Creates a new instance of <see cref="DbSet{TEntity}"/>
        /// </summary>
        /// <param name="context">An active SQLite connection</param>
        public DbSet(SQLiteContext context)
        {
            Context = context;
            EntityTable = TableCache.GetTableMap(typeof(TEntity));
        }

        /// <summary>
        /// Creates a new instance of the entity and associates it with the current context.
        /// </summary>
        /// <returns>A new instance of the entity of type <typeparamref name="TEntity"/>.</returns>
        public TEntity Create()
        {
            return Context.CreateEntity<TEntity>(EntityTable);
        }
        
        /// <summary>
        /// Creates a new instance of the entity, associates it with the current context,
        /// initializes it using the provided action, and immediately adds it to the database.
        /// </summary>
        /// <param name="initializer">An action to configure the entity's properties.</param>
        /// <returns>A new, initialized, and persisted instance of the entity of type <typeparamref name="TEntity"/>.</returns>
        public TEntity Create(Action<TEntity> initializer)
        {
            ArgumentNullException.ThrowIfNull(initializer);
    
            var entity = Context.CreateEntity<TEntity>(EntityTable);
            initializer(entity);
            Add(entity);
            return entity;
        }

        /// <summary>
        /// Builds a local <see cref="PreparedNonQuery"/> for INSERT operations.
        /// </summary>
        private PreparedNonQuery BuildInsertCommand()
        {
            using var builder = new InsertQueryBuilder(EntityTable.TableName, Context);
            foreach (var attribute in EntityTable.DatabaseColumns)
            {
                if (attribute.Value.IsPrimaryKey && EntityTable.HasRowIdAlias && EntityTable.RowIdColumn == attribute.Value)
                    continue;

                builder.Set(attribute.Key, new SqlLiteral($"@{attribute.Key}"));
            }

            return new PreparedNonQuery(builder.BuildCommand());
        }

        /// <summary>
        /// Builds a local <see cref="PreparedNonQuery"/> for DELETE operations.
        /// </summary>
        private PreparedNonQuery BuildDeleteCommand()
        {
            using var builder = new DeleteQueryBuilder(Context).From(EntityTable.TableName);
            foreach (var attr in EntityTable.PrimaryKeys)
            {
                builder.Where(attr.ColumnName, Comparison.Equals, new SqlLiteral($"@{attr.ColumnName}"));
            }

            return new PreparedNonQuery(builder.BuildCommand());
        }

        /// <summary>
        /// Builds a local <see cref="PreparedNonQuery"/> for UPDATE operations.
        /// </summary>
        private PreparedNonQuery BuildUpdateCommand()
        {
            using var builder = new UpdateQueryBuilder(EntityTable.TableName, Context);
            var primaryKeys = EntityTable.PrimaryKeyPropertyNames;

            foreach (var attribute in EntityTable.DatabaseColumns)
            {
                if (primaryKeys.Contains(attribute.Value.Property.Name))
                {
                    builder.Where(attribute.Key, Comparison.Equals, new SqlLiteral($"@{attribute.Key}"));
                }
                else
                {
                    builder.Set(attribute.Key, new SqlLiteral($"@{attribute.Key}"));
                }
            }

            return new PreparedNonQuery(builder.BuildCommand());
        }
        
        /// <summary>
        /// Queries the database using a LINQ-style predicate expression.
        /// Returns a deferred <see cref="DbQuery{TEntity}"/> that supports
        /// fluent .OrderBy(), .Take(), .Skip() chaining.
        /// </summary>
        /// <example>
        /// dbSet.Where(x => x.Name == "John" || x.RankId.In(3, 4, 5))
        ///      .OrderByDescending(x => x.FormRating)
        ///      .Take(10)
        ///      .ToList();
        /// </example>
        public DbQuery<TEntity> Where(Expression<Func<TEntity, bool>> predicate)
        {
            var visitor = new WhereExpressionVisitor<TEntity>();
            string whereClause = visitor.Translate(predicate);
            return new DbQuery<TEntity>(Context, EntityTable, whereClause, visitor.Parameters);
        }
        
        /// <summary>
        /// Projects all entities into a new form, selecting only the specified columns.
        /// Equivalent to: SELECT [col1], [col2] FROM [Table]
        /// </summary>
        public DbProjection<TEntity, TResult> Select<TResult>(Expression<Func<TEntity, TResult>> selector)
        {
            var (columns, projector) = SelectExpressionAnalyzer.Analyze(EntityTable, selector);

            return new DbProjection<TEntity, TResult>(
                Context, EntityTable, null,
                new List<SqliteParameter>(),
                new List<(string, bool)>(),
                null, null,
                columns, projector);
        }
        
        /// <summary>
        /// Determines whether the table contains any entities.
        /// Executes: SELECT EXISTS(SELECT 1 FROM [Table] LIMIT 1)
        /// </summary>
        /// <returns>true if at least one entity exists; otherwise, false.</returns>
        public bool Any()
        {
            string table = Context.QuoteIdentifier(EntityTable.TableName);
            string sql = $"SELECT EXISTS(SELECT 1 FROM {table} LIMIT 1)";
            return Context.ExecuteScalar<int>(sql) == 1;
        }
        
        /// <summary>
        /// Determines whether any entity in the table matches the specified predicate.
        /// Executes: SELECT EXISTS(SELECT 1 FROM [Table] WHERE ... LIMIT 1)
        /// </summary>
        /// <param name="predicate">A LINQ expression to filter entities.</param>
        /// <returns>true if at least one matching entity exists; otherwise, false.</returns>
        public bool Any(Expression<Func<TEntity, bool>> predicate)
        {
            var visitor = new WhereExpressionVisitor<TEntity>();
            string whereClause = visitor.Translate(predicate);

            string table = Context.QuoteIdentifier(EntityTable.TableName);
            string sql = $"SELECT EXISTS(SELECT 1 FROM {table} WHERE {whereClause} LIMIT 1)";

            using var command = Context.CreateCommand(sql);
            foreach (var p in visitor.Parameters)
                command.Parameters.Add(p);

            return Context.ExecuteScalar<int>(command) == 1;
        }
        
        /// <summary>
        /// Returns the number of entities in the table that match the specified predicate.
        /// Executes: SELECT COUNT(1) FROM [Table] WHERE ...
        /// </summary>
        /// <param name="predicate">A LINQ expression to filter entities.</param>
        /// <returns>The number of matching entities.</returns>
        public int CountWhere(Expression<Func<TEntity, bool>> predicate)
        {
            ArgumentNullException.ThrowIfNull(predicate);

            var visitor = new WhereExpressionVisitor<TEntity>();
            string whereClause = visitor.Translate(predicate);

            string table = Context.QuoteIdentifier(EntityTable.TableName);
            string sql = $"SELECT COUNT(1) FROM {table} WHERE {whereClause}";

            using var command = Context.CreateCommand(sql);
            foreach (var p in visitor.Parameters)
                command.Parameters.Add(p);

            return Context.ExecuteScalar<int>(command);
        }

        /// <summary>
        /// Inserts a new Entity into the database. If the entity table has a single integer primary key, 
        /// the primary key value will be updated with the last insert rowid.
        /// </summary>
        /// <param name="obj">The <see cref="TEntity"/> object to add to the dataset</param>
        public void Add(TEntity obj)
        {
            // We don't allow null
            ArgumentNullException.ThrowIfNull(obj);

            using var prepared = BuildInsertCommand();
            InsertEntity(obj, prepared);

            // Fire event
            OnEntityAdded(obj);
        }

        /// <summary>
        /// Inserts a range of new Entities into the database
        /// </summary>
        /// <param name="entities">The <see cref="TEntity"/> objects to add to the dataset</param>
        public void AddRange(params TEntity[] entities)
        {
            ArgumentNullException.ThrowIfNull(entities);
            
            using var prepared = BuildInsertCommand();

            bool autoTransaction = Context.Transaction == null;
            using var ts = autoTransaction ? Context.BeginTransaction() : null;
            try
            {
                foreach (var entity in entities)
                {
                    InsertEntity(entity, prepared);
                }
                ts?.Commit();

                // Fire event
                OnEntitiesAdded(entities);
            }
            catch
            {
                ts?.Rollback();
                throw;
            }
        }

        /// <summary>
        /// Inserts a range of new Entities into the database
        /// </summary>
        /// <param name="collection">The <see cref="TEntity"/> objects to add to the dataset</param>
        public void AddRange(IEnumerable<TEntity> collection)
        {
            ArgumentNullException.ThrowIfNull(collection);
            
            using var prepared = BuildInsertCommand();

            bool autoTransaction = Context.Transaction == null;
            using var ts = autoTransaction ? Context.BeginTransaction() : null;
            try
            {
                foreach (var entity in collection)
                {
                    InsertEntity(entity, prepared);
                }
                ts?.Commit();

                // Fire event
                OnEntitiesAdded(collection);
            }
            catch
            {
                ts?.Rollback();
                throw;
            }
        }

        /// <summary>
        /// If the Entity exists in the database already, than it is updated with
        /// the new values, otherwise the Entity object is inserted into the database
        /// </summary>
        /// <param name="obj">The <see cref="TEntity"/> object to add or update in the dataset</param>
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
            if (Contains(obj))
                return false;

            Add(obj);
            return true;
        }

        /// <summary>
        /// Deletes an Entity from the database
        /// </summary>
        /// <param name="obj">The <see cref="TEntity"/> object to remove from the dataset</param>
        /// <returns>true if an entity was removed from the dataset; false otherwise.</returns>
        public bool Remove(TEntity obj)
        {
            ArgumentNullException.ThrowIfNull(obj);
            
            using var prepared = BuildDeleteCommand();
            bool result = DeleteEntity(obj, prepared);
            if (result)
            {
                OnEntityRemoved(obj);
            }

            return result;
        }
        
        /// <summary>
        /// Deletes all entities matching the specified predicate directly in the database.
        /// This executes a single DELETE statement — no entities are loaded into memory.
        /// </summary>
        /// <param name="predicate">A LINQ expression that identifies which entities to delete.</param>
        /// <returns>The number of rows deleted.</returns>
        public int RemoveWhere(Expression<Func<TEntity, bool>> predicate)
        {
            ArgumentNullException.ThrowIfNull(predicate);

            var visitor = new WhereExpressionVisitor<TEntity>();
            string whereClause = visitor.Translate(predicate);

            string table = Context.QuoteIdentifier(EntityTable.TableName);
            string sql = $"DELETE FROM {table} WHERE {whereClause}";

            using var command = Context.CreateCommand(sql);
            foreach (var p in visitor.Parameters)
                command.Parameters.Add(p);

            int affected = command.ExecuteNonQuery();

            if (affected > 0 && Context.UseIdentityMapping)
            {
                Context.ClearIdentityMap(typeof(TEntity));
            }

            return affected;
        }

        /// <summary>
        /// Deletes rows from the database table corresponding to the entity set
        /// that match the specified <see cref="WhereStatement"/> condition.
        /// </summary>
        /// <param name="statement">The <see cref="WhereStatement"/> that defines the condition
        /// to select the rows to be deleted.</param>
        /// <returns>The number of rows affected by the DELETE operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown if the provided <paramref name="statement"/> is null.</exception>
        public int RemoveWhere(WhereStatement statement)
        {
            ArgumentNullException.ThrowIfNull(statement);

            string whereClause = statement.BuildStatement(out var parameters);
            string table = Context.QuoteIdentifier(EntityTable.TableName);
            string sql = $"DELETE FROM {table} WHERE {whereClause}";

            using var command = Context.CreateCommand(sql);
            foreach (var p in parameters)
                command.Parameters.Add(p);

            int affected = command.ExecuteNonQuery();

            if (affected > 0 && Context.UseIdentityMapping)
            {
                Context.ClearIdentityMap(typeof(TEntity));
            }

            return affected;
        }

        /// <summary>
        /// Deletes a range of Entities from the database
        /// </summary>
        /// <param name="entities">The <see cref="TEntity"/> objects to remove from the dataset</param>
        public void RemoveRange(params TEntity[] entities)
        {
            ArgumentNullException.ThrowIfNull(entities);
            
            using var prepared = BuildDeleteCommand();

            bool autoTransaction = Context.Transaction == null;
            using var ts = autoTransaction ? Context.BeginTransaction() : null;
            try
            {
                foreach (var entity in entities)
                {
                    DeleteEntity(entity, prepared);
                }
                ts?.Commit();

                // Fire event
                OnEntitiesRemoved(entities);
            }
            catch
            {
                ts?.Rollback();
                throw;
            }
        }

        /// <summary>
        /// Deletes a range of Entities from the database
        /// </summary>
        /// <param name="collection">The <see cref="TEntity"/> objects to remove from the dataset</param>
        public void RemoveRange(IEnumerable<TEntity> collection)
        {
            ArgumentNullException.ThrowIfNull(collection);

            using var prepared = BuildDeleteCommand();

            bool autoTransaction = Context.Transaction == null;
            using var ts = autoTransaction ? Context.BeginTransaction() : null;
            try
            {
                foreach (var entity in collection)
                {
                    DeleteEntity(entity, prepared);
                }
                ts?.Commit();

                // Fire event
                OnEntitiesRemoved(collection);
            }
            catch
            {
                ts?.Rollback();
                throw;
            }
        }

        /// <summary>
        /// Updates an Entity in the database, provided that none of the Primary
        /// keys were modified.
        /// </summary>
        /// <param name="obj">The <see cref="TEntity"/> object to update in the dataset</param>
        /// <returns>true if any records in the database were affected; false otherwise.</returns>
        /// <exception cref="InvalidOperationException">
        /// Thrown if the Entity has modified any of its Primary Key(s).
        /// </exception>
        public bool Update(TEntity obj)
        {
            ArgumentNullException.ThrowIfNull(obj);
            
            var primaryKeys = EntityTable.PrimaryKeyPropertyNames;

            if (obj.State == EntityState.Modified)
            {
                // Note: Overlaps prevents boxing, unlike Intersects.Any()
                if (obj.DirtyProperties.Overlaps(primaryKeys))
                {
                    throw new InvalidOperationException("Cannot update an entity with modified primary key(s).");
                }
            }

            using var prepared = BuildUpdateCommand();
            bool result = UpdateEntity(obj, prepared);
            if (result)
            {
                OnEntityUpdated(obj);
            }

            return result;
        }

        /// <summary>
        /// Updates the collection of entities, and returns the number of rows affected.
        /// </summary>
        /// <param name="entities"></param>
        /// <returns></returns>
        public int UpdateRange(IEnumerable<TEntity> entities)
        {
            ArgumentNullException.ThrowIfNull(entities);

            using var prepared = BuildUpdateCommand();

            bool autoTransaction = Context.Transaction == null;
            using var ts = autoTransaction ? Context.BeginTransaction() : null;
            try
            {
                int count = 0;
                var primaryKeys = EntityTable.PrimaryKeyPropertyNames;

                foreach (var entity in entities)
                {
                    if (entity.State == EntityState.Modified && entity.DirtyProperties.Overlaps(primaryKeys))
                    {
                        throw new InvalidOperationException("Cannot update an entity with modified primary key(s).");
                    }

                    bool result = UpdateEntity(entity, prepared);
                    if (result)
                    {
                        count++;
                    }
                }

                ts?.Commit();

                // Fire Event
                OnEntitiesUpdated(entities);

                return count;
            }
            catch
            {
                ts?.Rollback();
                throw;
            }
        }

        /// <summary>
        /// Updates the collection of entities, and returns the number of rows affected.
        /// </summary>
        /// <param name="entities"></param>
        /// <returns></returns>
        public int UpdateRange(params TEntity[] entities)
        {
            ArgumentNullException.ThrowIfNull(entities);
            
            using var prepared = BuildUpdateCommand();

            bool autoTransaction = Context.Transaction == null;
            using var ts = autoTransaction ? Context.BeginTransaction() : null;
            try
            {
                int count = 0;
                var primaryKeys = EntityTable.PrimaryKeyPropertyNames;

                foreach (var entity in entities)
                {
                    if (entity.State == EntityState.Modified && entity.DirtyProperties.Overlaps(primaryKeys))
                    {
                        throw new InvalidOperationException("Cannot update an entity with modified primary key(s).");
                    }

                    bool result = UpdateEntity(entity, prepared);
                    if (result)
                    {
                        count++;
                    }
                }

                ts?.Commit();

                // Fire Event
                OnEntitiesUpdated(entities);

                return count;
            }
            catch
            {
                ts?.Rollback();
                throw;
            }
        }
        
        /// <summary>
        /// Updates all entities matching the specified predicate directly in the database.
        /// This executes a single UPDATE statement — no entities are loaded into memory.
        /// </summary>
        /// <param name="predicate">A LINQ expression that identifies which entities to update.</param>
        /// <param name="buildAction">An action to configure the SET clauses via <see cref="UpdateQueryBuilder"/>.</param>
        /// <returns>The number of rows updated.</returns>
        public int UpdateWhere(Expression<Func<TEntity, bool>> predicate, Action<UpdateQueryBuilder> buildAction)
        {
            ArgumentNullException.ThrowIfNull(predicate);
            ArgumentNullException.ThrowIfNull(buildAction);

            var visitor = new WhereExpressionVisitor<TEntity>();
            string whereClause = visitor.Translate(predicate);

            string table = Context.QuoteIdentifier(EntityTable.TableName);

            // Build the SET portion using the builder
            using var builder = new UpdateQueryBuilder(EntityTable.TableName, Context);
            buildAction(builder);

            // Build the command (this gives us the parameterized SET clauses)
            using var command = builder.BuildCommand();

            // The builder produced: UPDATE [Table] SET [col]=@P0, ...
            // We need to append our WHERE clause
            command.CommandText = $"{command.CommandText} WHERE {whereClause}";

            // Add the predicate's parameters
            foreach (var p in visitor.Parameters)
                command.Parameters.Add(p);

            int affected = command.ExecuteNonQuery();

            if (affected > 0 && Context.UseIdentityMapping)
            {
                Context.ClearIdentityMap(typeof(TEntity));
            }

            return affected;
        }

        /// <summary>
        /// Updates all entities matching the specified <see cref="WhereStatement"/> directly in the database.
        /// This executes a single UPDATE statement — no entities are loaded into memory.
        /// </summary>
        /// <param name="statement">The <see cref="WhereStatement"/> that defines the condition.</param>
        /// <param name="buildAction">An action to configure the SET clauses via <see cref="UpdateQueryBuilder"/>.</param>
        /// <returns>The number of rows updated.</returns>
        public int UpdateWhere(WhereStatement statement, Action<UpdateQueryBuilder> buildAction)
        {
            ArgumentNullException.ThrowIfNull(statement);
            ArgumentNullException.ThrowIfNull(buildAction);

            string whereClause = statement.BuildStatement(out var whereParams);

            using var builder = new UpdateQueryBuilder(EntityTable.TableName, Context);
            buildAction(builder);

            using var command = builder.BuildCommand();

            command.CommandText = $"{command.CommandText} WHERE {whereClause}";

            foreach (var p in whereParams)
                command.Parameters.Add(p);

            int affected = command.ExecuteNonQuery();

            if (affected > 0 && Context.UseIdentityMapping)
            {
                Context.ClearIdentityMap(typeof(TEntity));
            }

            return affected;
        }

        /// <summary>
        /// This method will requery an entity from the database, refreshing
        /// the values of all attributes to match that in the database.
        /// </summary>
        /// <param name="entity">The entity object to reload attributes to</param>
        /// <returns>
        /// true if the entity was successfully retrieved from the database 
        /// and its attributes reloaded; false otherwise
        /// </returns>
        public bool Reload(ref TEntity entity)
        {
            ArgumentNullException.ThrowIfNull(entity);

            SelectQueryBuilder query = new SelectQueryBuilder(Context);
            query.From(EntityTable.TableName).SelectAll().Take(1);

            foreach (var attribute in EntityTable.PrimaryKeys)
            {
                query.Where(attribute.ColumnName, Comparison.Equals, attribute.GetValue(entity));
            }

            using (SqliteCommand command = query.BuildCommand())
            using (SqliteDataReader reader = command.ExecuteReader())
            {
                if (reader.HasRows)
                {
                    reader.Read();
                    entity = Context.ConvertToEntity<TEntity>(EntityTable, reader);
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
        public bool Contains(TEntity obj)
        {
            ArgumentNullException.ThrowIfNull(obj);
            
            WhereStatement where = new WhereStatement(Context);

            foreach (var attr in EntityTable.PrimaryKeys)
            {
                object val = attr.GetValue(obj);
                where.And(attr.ColumnName, Comparison.Equals, val);
            }

            return Contains(EntityTable.TableName, where);
        }

        /// <summary>
        /// Returns whether an Entity exists in the database, using the given <see cref="WhereStatement"/>
        /// </summary>
        internal bool Contains(string tableName, WhereStatement where)
        {
            string whereClause = where.BuildStatement(out var parameters);
            string sql = $"SELECT EXISTS(SELECT 1 FROM {Context.QuoteIdentifier(tableName)} WHERE {whereClause} LIMIT 1)";

            using (SqliteCommand command = Context.CreateCommand(sql))
            {
                foreach (var p in parameters)
                    command.Parameters.Add(p);
                return Context.ExecuteScalar<int>(command) == 1;
            }
        }

        /// <summary>
        /// Deletes all records from the database table.
        /// </summary>
        public void Clear()
        {
            string table = Context.QuoteIdentifier(EntityTable.TableName);
            string sql = $"DELETE FROM {table}";
            using (SqliteCommand command = Context.CreateCommand(sql))
                command.ExecuteNonQuery();
        }

        /// <summary>
        /// Finds and returns an entity of type <typeparamref name="TEntity"/> by its primary key.
        /// </summary>
        /// <typeparam name="TKey">The type of the primary key.</typeparam>
        /// <param name="id">The value of the primary key to search for.</param>
        /// <returns>The entity matching the key, or <see langword="null"/> if not found.</returns>
        /// <remarks>Skips the database query entirely if the entity is already cached.</remarks>
        public TEntity Find<TKey>(TKey id)
        {
            if (EntityTable.PrimaryKeys.Count > 1)
                throw new InvalidOperationException("Cannot use Find<TKey>(TKey id) on an entity with a composite primary key. Use Find(params object[] keyValues) instead.");

            if (id == null)
                throw new ArgumentNullException(nameof(id), "The primary key value cannot be null.");

            if (!AllowedKeyTypes.Contains(typeof(TKey)))
            {
                throw new NotSupportedException(
                    $"The key type '{typeof(TKey).Name}' is not supported. " +
                    "Only primitive types, strings, and Guids can be used as primary keys."
                );
            }
            
            // Check the identity map FIRST
            if (Context.TryGetCached<TEntity>([id], out var cached))
            {
                return cached;
            }

            var primaryKey = EntityTable.PrimaryKeys.First();
            var query = new SelectQueryBuilder(Context)
                .From(EntityTable.TableName)
                .SelectAll()
                .Where(primaryKey.ColumnName, Comparison.Equals, id)
                .Take(1);

            using var command = query.BuildCommand();
            using var reader = command.ExecuteReader();
            if (reader.HasRows && reader.Read())
            {
                return Context.ConvertToEntity<TEntity>(EntityTable, reader);
            }
            return null;
        }

        /// <summary>
        /// Finds and retrieves an entity of type <typeparamref name="TEntity"/> based on the specified primary key values.
        /// </summary>
        /// <param name="keyValues">An array of primary key values used to locate the entity.</param>
        /// <returns>The entity if found; otherwise, <see langword="null"/>.</returns>
        /// <remarks>Skips the database query entirely if the entity is already cached.</remarks>
        public TEntity Find(params object[] keyValues)
        {
            // Ensure that the number of key values matches the number of primary keys
            if (keyValues.Length != EntityTable.PrimaryKeys.Count)
                throw new ArgumentException("The number of key values provided does not match the number of primary keys.", nameof(keyValues));
            
            // Check the identity map FIRST
            if (Context.TryGetCached<TEntity>(keyValues, out var cached))
            {
                return cached;
            }

            // Build the SQLite query
            var query = new SelectQueryBuilder(Context)
                .From(EntityTable.TableName)
                .SelectAll()
                .Take(1);
            query.WhereStatement.InnerClauseOperator = LogicOperator.And;

            int i = 0;
            foreach (var attr in EntityTable.PrimaryKeys)
            {
                var value = keyValues[i];
                if (value == null)
                    throw new ArgumentNullException(nameof(value), "A primary key value cannot be null.");

                query.Where(attr.ColumnName, Comparison.Equals, value);
                i++;
            }

            using var command = query.BuildCommand();
            using var reader = command.ExecuteReader();
            if (reader.HasRows && reader.Read())
            {
                return Context.ConvertToEntity<TEntity>(EntityTable, reader);
            }
            return null;
        }
        
        /// <summary>
        /// Finds the first entity matching the specified predicate, or null if no match is found.
        /// </summary>
        /// <param name="predicate">A LINQ expression to filter entities.</param>
        /// <returns>The first matching entity, or <see langword="null"/> if none found.</returns>
        public TEntity Find(Expression<Func<TEntity, bool>> predicate)
        {
            return Where(predicate).FirstOrDefault();
        }
        
        /// <summary>
        /// Finds an entity by its complete primary key, specified as a dictionary of property names to values.
        /// </summary>
        /// <param name="keyValues">A dictionary mapping primary key property names to their values.</param>
        /// <returns>The entity if found; otherwise, <see langword="null"/>.</returns>
        /// <remarks>
        /// All primary key properties must be provided. The order of keys in the dictionary does not matter.
        /// Skips the database query entirely if the entity is already cached in the identity map.
        /// </remarks>
        public TEntity Find(Dictionary<string, object> keyValues)
        {
            if (keyValues == null || keyValues.Count == 0)
                throw new ArgumentException("At least one key value must be provided.", nameof(keyValues));

            if (keyValues.Count != EntityTable.PrimaryKeys.Count)
                throw new ArgumentException(
                    $"Expected {EntityTable.PrimaryKeys.Count} primary key(s), but received {keyValues.Count}.",
                    nameof(keyValues));

            // Build ordered array for identity map lookup
            var orderedKeys = new object[EntityTable.PrimaryKeys.Count];
            int i = 0;
            foreach (var pk in EntityTable.PrimaryKeys)
            {
                if (!keyValues.TryGetValue(pk.Property.Name, out var value))
                {
                    throw new ArgumentException(
                        $"Missing primary key property '{pk.Property.Name}' in the provided dictionary.",
                        nameof(keyValues));
                }

                orderedKeys[i++] = value ?? throw new ArgumentNullException(nameof(value), $"Primary key property '{pk.Property.Name}' cannot be null.");
            }

            // Check identity map first (using ordered keys)
            if (Context.TryGetCached<TEntity>(orderedKeys, out var cached))
            {
                return cached;
            }

            // Build query
            var query = new SelectQueryBuilder(Context)
                .From(EntityTable.TableName)
                .SelectAll()
                .Take(1);
            query.WhereStatement.InnerClauseOperator = LogicOperator.And;

            foreach (var pk in EntityTable.PrimaryKeys)
            {
                query.Where(pk.ColumnName, Comparison.Equals, keyValues[pk.Property.Name]);
            }

            using var command = query.BuildCommand();
            using var reader = command.ExecuteReader();
            if (reader.HasRows && reader.Read())
            {
                return Context.ConvertToEntity<TEntity>(EntityTable, reader);
            }
            return null;
        }
        
        /// <summary>
        /// Finds all entities matching a partial composite key.
        /// The provided key values are matched in order against the entity's primary keys.
        /// </summary>
        /// <param name="keyValues">One or more leading primary key values to filter by.</param>
        /// <returns>All matching entities.</returns>
        public IEnumerable<TEntity> FindAll(params object[] keyValues)
        {
            if (keyValues == null || keyValues.Length == 0)
                throw new ArgumentException("At least one key value must be provided.", nameof(keyValues));

            if (keyValues.Length > EntityTable.PrimaryKeys.Count)
                throw new ArgumentException("More key values provided than primary keys exist.", nameof(keyValues));

            var query = new SelectQueryBuilder(Context)
                .From(EntityTable.TableName)
                .SelectAll();
            query.WhereStatement.InnerClauseOperator = LogicOperator.And;

            int i = 0;
            foreach (var attr in EntityTable.PrimaryKeys)
            {
                if (i >= keyValues.Length) break;

                var value = keyValues[i];
                if (value == null)
                    throw new ArgumentNullException(nameof(value), "A primary key value cannot be null.");

                query.Where(attr.ColumnName, Comparison.Equals, value);
                i++;
            }

            var command = query.BuildCommand();
            return Context.ExecuteReader<TEntity>(command);
        }
        
        /// <summary>
        /// Finds all entities matching a partial or complete primary key, specified as a dictionary.
        /// </summary>
        /// <param name="keyValues">A dictionary mapping primary key property names to their values.</param>
        /// <returns>All matching entities.</returns>
        /// <remarks>
        /// Unlike the params overload, this method allows filtering by ANY subset of primary keys,
        /// not just leading keys in order.
        /// </remarks>
        public IEnumerable<TEntity> FindAll(Dictionary<string, object> keyValues)
        {
            if (keyValues == null || keyValues.Count == 0)
                throw new ArgumentException("At least one key value must be provided.", nameof(keyValues));

            if (keyValues.Count > EntityTable.PrimaryKeys.Count)
                throw new ArgumentException(
                    $"More key values provided ({keyValues.Count}) than primary keys exist ({EntityTable.PrimaryKeys.Count}).",
                    nameof(keyValues));

            // Validate that all provided keys are actual primary keys
            foreach (var kvp in keyValues)
            {
                if (!EntityTable.PrimaryKeys.Any(pk => pk.Property.Name == kvp.Key))
                {
                    throw new ArgumentException(
                        $"Property '{kvp.Key}' is not a primary key of {typeof(TEntity).Name}.",
                        nameof(keyValues));
                }

                if (kvp.Value == null)
                    throw new ArgumentNullException(nameof(keyValues), $"Primary key property '{kvp.Key}' cannot be null.");
            }

            // Build query
            var query = new SelectQueryBuilder(Context)
                .From(EntityTable.TableName)
                .SelectAll();
            query.WhereStatement.InnerClauseOperator = LogicOperator.And;

            foreach (var kvp in keyValues)
            {
                var pk = EntityTable.PrimaryKeys.First(p => p.Property.Name == kvp.Key);
                query.Where(pk.ColumnName, Comparison.Equals, kvp.Value);
            }

            var command = query.BuildCommand();
            return Context.ExecuteReader<TEntity>(command);
        }
        
        /// <summary>
        /// Finds all entities matching the specified <see cref="WhereStatement"/>.
        /// </summary>
        /// <param name="where">The where statement to filter entities by.</param>
        /// <returns>All matching entities.</returns>
        public IEnumerable<TEntity> FindAll(WhereStatement where)
        {
            ArgumentNullException.ThrowIfNull(where);

            if (!where.HasClause)
                throw new ArgumentException("The WhereStatement must contain at least one clause.", nameof(where));

            string table = Context.QuoteIdentifier(EntityTable.TableName);
            string sql = $"SELECT * FROM {table} WHERE {where.BuildStatement(out var parameters)}";

            using var command = Context.CreateCommand(sql);
            foreach (var p in parameters)
                command.Parameters.Add(p);
            return Context.ExecuteReader<TEntity>(command);
        }
        
        /// <summary>
        /// Finds all entities matching the specified predicate.
        /// </summary>
        /// <param name="predicate">A LINQ expression to filter entities.</param>
        /// <returns>A list of all matching entities.</returns>
        public List<TEntity> FindAll(Expression<Func<TEntity, bool>> predicate)
        {
            return Where(predicate).ToList();
        }

        /// <summary>
        /// Retrieves the last record from the database table represented by this <see cref="DbSet{TEntity}"/>.
        /// For tables without the "WITHOUT ROWID" optimization, the last record is determined based on the ROWID column.
        /// For tables with "WITHOUT ROWID," the last record is determined based on descending order of the primary key columns.
        /// </summary>
        /// <returns>The last record of type <typeparamref name="TEntity"/> or null if no records exist in the table.</returns>
        public TEntity LastOrDefault()
        {
            string table = Context.QuoteIdentifier(EntityTable.TableName);

            // Regular table — use ROWID for true insertion-order "last"
            if (!EntityTable.WithoutRowID)
            {
                string query = $"SELECT * FROM {table} ORDER BY ROWID DESC LIMIT 1";
                return Context.Query<TEntity>(query).FirstOrDefault();
            }

            // WITHOUT ROWID — use the already-sorted PrimaryKeys collection
            if (EntityTable.PrimaryKeys.Count == 0)
                return null;

            string orderBy = string.Join(", ", EntityTable.PrimaryKeys.Select(
                pk => $"{Context.QuoteIdentifier(pk.ColumnName)} DESC"));

            string query2 = $"SELECT * FROM {table} ORDER BY {orderBy} LIMIT 1";
            return Context.Query<TEntity>(query2).FirstOrDefault();
        }

        /// <summary>
        /// Retrieves the first entity from the database table, or the default value if no entities are found.
        /// </summary>
        /// <remarks>
        /// This is different from <see cref="this[int]"/> in that it returns first entity by Primary Key
        /// </remarks>
        /// <returns>
        /// The first entity of type <typeparamref name="TEntity"/>, or the default value (null) if the table contains no entities.
        /// </returns>
        public TEntity FirstOrDefault()
        {
            string table = Context.QuoteIdentifier(EntityTable.TableName);

            // Regular table — use ROWID for true insertion-order "first"
            if (!EntityTable.WithoutRowID)
            {
                string query = $"SELECT * FROM {table} ORDER BY ROWID ASC LIMIT 1";
                return Context.Query<TEntity>(query).FirstOrDefault();
            }

            // WITHOUT ROWID — use the already-sorted PrimaryKeys collection
            if (EntityTable.PrimaryKeys.Count == 0)
                return null;

            string orderBy = string.Join(", ", EntityTable.PrimaryKeys.Select(
                pk => $"{Context.QuoteIdentifier(pk.ColumnName)} ASC"));

            string query2 = $"SELECT * FROM {table} ORDER BY {orderBy} LIMIT 1";
            return Context.Query<TEntity>(query2).FirstOrDefault();
        }

        /// <summary>
        /// Copies the entities in this DbSet to an Array, starting at a particular Array index.
        /// </summary>
        public void CopyTo(TEntity[] array, int arrayIndex)
        {
            ArgumentNullException.ThrowIfNull(array);

            int i = arrayIndex;
            foreach (TEntity entity in Context.Select<TEntity>())
            {
                array[i++] = entity;
            }
        }

        /// <summary>
        /// Performs a mass update on the database using a query builder.
        /// This method clears the local identity map for this type to maintain consistency.
        /// </summary>
        public int BulkUpdate(Action<UpdateQueryBuilder> buildAction)
        {
            using (var builder = new UpdateQueryBuilder(EntityTable.TableName, Context))
            {
                buildAction(builder);
                int affected = builder.Execute();

                if (affected > 0 && Context.UseIdentityMapping)
                {
                    Context.ClearIdentityMap(typeof(TEntity));
                }

                return affected;
            }
        }

        /// <summary>
        /// Performs a mass delete on the database using a query builder.
        /// This method clears the local identity map for this type to maintain consistency.
        /// </summary>
        public int BulkDelete(Action<DeleteQueryBuilder> buildAction)
        {
            using (var builder = new DeleteQueryBuilder(Context))
            {
                builder.From(EntityTable.TableName);
                buildAction(builder);

                int affected = builder.Execute();

                if (affected > 0 && Context.UseIdentityMapping)
                {
                    Context.ClearIdentityMap(typeof(TEntity));
                }

                return affected;
            }
        }

        /// <summary>
        /// Inserts a large collection of entities using a transaction and a single prepared statement.
        /// This is the fastest way to perform mass inserts, but does not update the entities, filling
        /// their database row ID's. This is a fire and forget method.
        /// </summary>
        public void BulkInsert(IEnumerable<TEntity> entities)
        {
            if (entities == null) return;

            // Materialize once to avoid double-enumeration
            var list = entities as IList<TEntity> ?? entities.ToList();
            if (list.Count == 0) return;

            using (var builder = new InsertQueryBuilder(EntityTable.TableName, Context))
            {
                foreach (var attribute in EntityTable.DatabaseColumns)
                {
                    if (attribute.Value.IsPrimaryKey && EntityTable.HasRowIdAlias && EntityTable.RowIdColumn == attribute.Value)
                        continue;

                    builder.Set(attribute.Key, new SqlLiteral($"@{attribute.Key}"));
                }

                using (var ts = Context.BeginTransaction())
                using (var prepared = new PreparedNonQuery(builder.BuildCommand()))
                {
                    SqliteCommand rowIdCmd = null;
                    
                    try
                    {
                        // OPTIMIZATION: Create the RowID command once outside the loop
                        if (EntityTable.HasRowIdAlias)
                        {
                            rowIdCmd = Context.Connection.CreateCommand();
                            rowIdCmd.CommandText = "SELECT last_insert_rowid();";
                            rowIdCmd.Transaction = Context.Transaction; // Must be tied to the bulk transaction!
                        }

                        foreach (var entity in list)
                        {
                            prepared.SetParameters(entity, EntityTable);
                            prepared.Execute();

                            // Back-fill the ID!
                            if (rowIdCmd != null)
                            {
                                long lastId = (long)rowIdCmd.ExecuteScalar();
                                EntityTable.RowIdColumn.SetValue(entity, Convert.ChangeType(lastId, EntityTable.RowIdColumn.Property.PropertyType));
                            }

                            entity.State = EntityState.Fresh;
                            entity.DirtyProperties.Clear();
                        }

                        ts.Commit();
                    }
                    catch
                    {
                        ts.Rollback();
                        throw;
                    }
                    finally
                    {
                        rowIdCmd?.Dispose();
                    }
                }
            }

            // We still clear the Identity Map because these objects aren't Proxies!
            if (Context.UseIdentityMapping)
            {
                Context.ClearIdentityMap(typeof(TEntity));
            }
        }

        /// <summary>
        /// Gets the identity key from the entity's primary key properties.
        /// </summary>
        private EntityKey GetIdentityKeyFromObject(TEntity obj)
        {
            var pks = EntityTable.PrimaryKeys;
            if (pks.Count == 1) return new EntityKey(pks.First().GetValue(obj));

            var values = pks.Select(pk => pk.GetValue(obj)).ToArray();
            return values.Length switch
            {
                2 => new EntityKey(values[0], values[1]),
                3 => new EntityKey(values[0], values[1], values[2]),
                4 => new EntityKey(values[0], values[1], values[2], values[3]),
                5 => new EntityKey(values[0], values[1], values[2], values[3], values[4]),
                _ => throw new NotSupportedException($"Composite keys with {values.Length} columns are not supported.")
            };
        }

        /// <summary>
        /// Inserts a new Entity into the database using the provided prepared command.
        /// </summary>
        private int InsertEntity(TEntity obj, PreparedNonQuery prepared)
        {
            AttributeInfo rowid = EntityTable.RowIdColumn;
            prepared.SetParameters(obj, EntityTable);
            int result = prepared.Execute();

            if (result > 0)
            {
                if (EntityTable.HasRowIdAlias)
                {
                    // Reuse a single cached command
                    if (_lastRowIdCommand == null)
                    {
                        _lastRowIdCommand = Context.Connection.CreateCommand();
                        _lastRowIdCommand.CommandText = "SELECT last_insert_rowid();";
                    }
                    _lastRowIdCommand.Transaction = Context.Transaction; // Always sync

                    long lastId = (long)_lastRowIdCommand.ExecuteScalar();
                    rowid.SetValue(obj, Convert.ChangeType(lastId, rowid.Property.PropertyType));
                }

                // --- IDENTITY MAP REGISTRATION ---
                if (Context.UseIdentityMapping && EntityTable.PrimaryKeys.Count > 0)
                {
                    var pkValue = GetIdentityKeyFromObject(obj);
                    Type typeKey = typeof(TEntity);

                    if (!Context.EntityIdentityMap.TryGetValue(typeKey, out var typeMap))
                    {
                        typeMap = new Dictionary<EntityKey, EntityBase>();
                        Context.EntityIdentityMap[typeKey] = typeMap;
                    }

                    typeMap[pkValue] = obj;
                }
            }

            obj.DirtyProperties.Clear();
            obj.State = EntityState.Fresh;
 
            return result;
        }

        /// <summary>
        /// Removes an Entity from the database using the provided prepared command.
        /// </summary>
        private bool DeleteEntity(TEntity obj, PreparedNonQuery prepared)
        {
            obj.State = EntityState.Deleted;
            prepared.SetParameters(obj, EntityTable);
            bool result = prepared.Execute() > 0;

            if (result && Context.UseIdentityMapping)
            {
                Context.Detach(obj);
            }

            return result;
        }

        /// <summary>
        /// Updates an Entity in the Database using the provided prepared command.
        /// </summary>
        private bool UpdateEntity(TEntity entity, PreparedNonQuery prepared)
        {
            // Block updates on non-tracked (non-proxied) entities
            // ReSharper disable once SuspiciousTypeConversion.Global
            if (entity is not Castle.DynamicProxy.IProxyTargetAccessor)
            {
                throw new InvalidOperationException(
                    $"Cannot update a non-tracked '{typeof(TEntity).Name}' entity. " +
                    "Entities must be created via DbSet.Create() or loaded from the database to support Update().");
            }
            
            if (entity.DirtyProperties.Count > 0)
            {
                prepared.SetParameters(entity, EntityTable);
                bool result = prepared.Execute() > 0;
                if (result)
                {
                    entity.DirtyProperties.Clear();
                    entity.State = EntityState.Fresh;
                }

                return result;
            }

            return false;
        }

        /// <summary>
        /// Releases all resources used by the current instance of <see cref="DbSet{TEntity}"/>.
        /// </summary>
        public void Dispose()
        {
            _lastRowIdCommand?.Dispose();
            _lastRowIdCommand = null;
        }

        /// <summary>
        /// Selects all of the records in the database, and returns the Enumerator
        /// </summary>
        public IEnumerator<TEntity> GetEnumerator() => Context.Select<TEntity>().GetEnumerator();

        /// <summary>
        /// Selects all of the records in the database, and returns the Enumerator
        /// </summary>
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        // Internal helpers to trigger events safely
        protected void OnEntityAdded(TEntity entity) => EntityAdded?.Invoke(entity);
        protected void OnEntityUpdated(TEntity entity) => EntityUpdated?.Invoke(entity);
        protected void OnEntityRemoved(TEntity entity) => EntityRemoved?.Invoke(entity);

        // Triggered after AddRange/RemoveRange
        protected void OnEntitiesAdded(IEnumerable<TEntity> entities) => EntitiesAdded?.Invoke(entities);
        protected void OnEntitiesUpdated(IEnumerable<TEntity> entities) => EntitiesUpdated?.Invoke(entities);
        protected void OnEntitiesRemoved(IEnumerable<TEntity> entities) => EntitiesRemoved?.Invoke(entities);
    }
}