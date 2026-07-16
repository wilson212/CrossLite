using CrossLite.QueryBuilder;
using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace CrossLite.CodeFirst
{
    /// <summary>
    /// Represents a database set that manages the child entities of a parent entity within a relational context.
    /// This class facilitates querying, adding, removing, and managing child entities associated with the parent entity.
    /// </summary>
    /// <typeparam name="TParentEntity">The type of the parent entity. Must inherit from <see cref="EntityBase"/>.</typeparam>
    /// <typeparam name="TChildEntity">The type of the child entity. Must inherit from <see cref="EntityBase"/> and have a parameterless constructor.</typeparam>
    public class ChildDbSet<TParentEntity, TChildEntity> : EntitySet<TChildEntity>
        where TParentEntity : EntityBase
        where TChildEntity : EntityBase, new()
    {
        /// <summary>
        /// The SQLiteContext associated with this ChildDbSet.
        /// </summary>
        protected SQLiteContext Context { get; set; }

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
        /// Gets or sets the cached set of foreign key property names.
        /// </summary>
        private HashSet<string> _cachedFkPropertyNames;

        /// <summary>
        /// Gets the total number of entities present in the associated child entity set.
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

                // Execute the command
                var retVal = query.ExecuteScalar<int>();

                // Dispose
                if (!wasOpen)
                {
                    context.Dispose();
                }

                return retVal;
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ChildDbSet{TParentEntity}"/> class, associating it with a
        /// parent entity and its corresponding property in the database context.
        /// </summary>
        /// <remarks>This constructor establishes the necessary foreign key constraint to enable lazy
        /// loading of the related data.</remarks>
        /// <param name="entity">The parent entity to which this child set is related. Cannot be <see langword="null"/>.</param>
        /// <param name="parentProperty">The property containing the ID on the parent entity that represents the relationship. Cannot be <see langword="null"/>.</param>
        /// <param name="context">The database context used to manage the connection and operations. Cannot be <see langword="null"/>.</param>
        public ChildDbSet(TParentEntity entity, PropertyInfo parentProperty, SQLiteContext context)
        {
            Context = context ?? throw new ArgumentNullException(nameof(context));
            Entity = entity ?? throw new ArgumentNullException(nameof(entity));

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
        /// <remarks>
        /// This method requires an active database context. If the context is not connected, an InvalidOperationException
        /// will be thrown. To access child collections outside a 'using' block, keep the context open for the duration
        /// of your operations.
        /// </remarks>
        /// <param name="wasOpen">A reference parameter that indicates whether the database context was already open.
        /// Set to <see langword="true"/> if the context was already connected; otherwise, <see langword="false"/>.</param>
        /// <returns>An instance of <see cref="SQLiteContext"/> representing the database context to use for the operation.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the database context is not connected.</exception>
        protected SQLiteContext LazyLoad(ref bool wasOpen)
        {
            // Grab table mappings
            if (ParentTable == null)
            {
                // Get the table mapping for the parent entity
                ParentTable = TableCache.GetTableMap(typeof(TParentEntity));
            }
            if (ChildTable == null)
            {
                // Get the table mapping for the child entity
                ChildTable = TableCache.GetTableMap(typeof(TChildEntity));
            }

            // If we already have the foreign key values, skip loading them
            if (ForeignKeyValues == null)
            {
                var fkinfos = ChildTable.ForeignKeys.Where(x => x.ParentEntityType == ParentTable.EntityType).ToArray();
                var validGroups = new List<Dictionary<string, object>>();

                foreach (ForeignKeyConstraint fkinfo in fkinfos)
                {
                    if (InverseForeignKeyAttribute != null)
                    {
                        // Only proceed if this FK matches the specific property requested
                        bool matches = fkinfo.ForeignKey.PropertyNames.SequenceEqual(InverseForeignKeyAttribute.Attributes);
                        if (!matches) continue;
                    }

                    var collection = new Dictionary<string, object>();
                    for (int j = 0; j < fkinfo.ForeignKey.PropertyNames.Length; j++)
                    {
                        string childPropName = fkinfo.ForeignKey.PropertyNames[j];
                        string parentPropName = fkinfo.Reference.PropertyNames[j];

                        AttributeInfo parentAttr = ParentTable.GetAttributeByPropertyName(parentPropName);
                        AttributeInfo childAttr = ChildTable.GetAttributeByPropertyName(childPropName);
                        collection.Add(childAttr.ColumnName, parentAttr.GetValue(Entity));
                    }
                    validGroups.Add(collection);
                }

                ForeignKeyValues = validGroups.ToArray(); // Now it's a clean array with no nulls
            }

            // Fail fast if the context is not connected
            if (Context == null || !Context.IsConnected())
            {
                throw new InvalidOperationException(
                    $"Cannot access child collection '{typeof(TChildEntity).Name}' because the database context is not connected. " +
                    $"To access child collections outside a 'using' block, keep the context open for the duration of your operations:\n\n" +
                    $"Example:\n" +
                    $"  using (var context = new SQLiteContext())\n" +
                    $"  {{\n" +
                    $"      var parent = context.Select<{typeof(TParentEntity).Name}>(x => ...).FirstOrDefault();\n" +
                    $"      \n" +
                    $"      // Access child collections while context is still open\n" +
                    $"      foreach (var child in parent.{typeof(TChildEntity).Name}s)\n" +
                    $"      {{\n" +
                    $"          // Process child...\n" +
                    $"      }}\n" +
                    $"  }}"
                );
            }

            wasOpen = true;

            // Lazy Load the DbSet for the child entity — recreate if context changed
            if (ChildCollection == null || ChildCollection.Context != Context)
            {
                ChildCollection = new DbSet<TChildEntity>(Context);
            }

            return Context;
        }
        
        /// <summary>
        /// Queries child entities using a LINQ-style predicate expression,
        /// automatically scoped to the parent entity's foreign key.
        /// Returns a deferred <see cref="DbQuery{TEntity}"/> supporting fluent chaining.
        /// </summary>
        public DbQuery<TChildEntity> Where(Expression<Func<TChildEntity, bool>> predicate)
        {
            bool wasOpen = false;
            SQLiteContext context = LazyLoad(ref wasOpen);

            // 1. Translate the user's expression
            var visitor = new WhereExpressionVisitor<TChildEntity>();
            string exprWhere = visitor.Translate(predicate);
            var allParams = new List<SqliteParameter>(visitor.Parameters);

            // 2. Build the parent FK scoping clause
            string fkWhere = BuildFkWhereClause(allParams);

            // 3. Combine: (FK scope) AND (user expression)
            string combined;
            if (!string.IsNullOrEmpty(fkWhere))
                combined = $"({fkWhere}) AND ({exprWhere})";
            else
                combined = exprWhere;

            return new DbQuery<TChildEntity>(context, ChildTable, combined, allParams);
        }
        
        /// <summary>
        /// Projects all entities into a new form, selecting only the specified columns.
        /// Equivalent to: SELECT [col1], [col2] FROM [Table]
        /// </summary>
        public DbProjection<TChildEntity, TResult> Select<TResult>(Expression<Func<TChildEntity, TResult>> selector)
        {
            bool wasOpen = false;
            SQLiteContext context = LazyLoad(ref wasOpen);

            var (columns, projector) = SelectExpressionAnalyzer.Analyze(ChildTable, selector);

            // Build the parent FK scoping clause
            var allParams = new List<SqliteParameter>();
            string fkWhere = BuildFkWhereClause(allParams);

            return new DbProjection<TChildEntity, TResult>(
                context, ChildTable, fkWhere,
                allParams,
                new List<(string, bool)>(),
                null, null,
                columns, projector);
        }

        /// <summary>
        /// Builds the FK-scoping WHERE clause from <see cref="ForeignKeyValues"/>.
        /// Each group is AND'd internally; groups are OR'd together.
        /// </summary>
        private string BuildFkWhereClause(List<SqliteParameter> parameters)
        {
            if (ForeignKeyValues == null || ForeignKeyValues.Length == 0)
                return string.Empty;

            var groups = new List<string>();
            foreach (var group in ForeignKeyValues)
            {
                var conditions = new List<string>();
                foreach (var kvp in group)
                {
                    string col = SQLiteContext.QuoteIdentifier(
                        kvp.Key, Context.IdentifierQuoteMode, Context.IdentifierQuoteKind);
                    string paramName = $"@P{parameters.Count}";
                    parameters.Add(new SqliteParameter(paramName, kvp.Value));
                    conditions.Add($"{col} = {paramName}");
                }
                groups.Add(string.Join(" AND ", conditions));
            }

            return groups.Count == 1
                ? groups[0]
                : string.Join(" OR ", groups.Select(g => $"({g})"));
        }

        /// <summary>
        /// Lazy loads the child entities of a foreign key constraint
        /// </summary>
        public override IEnumerator<TChildEntity> GetEnumerator()
        {
            bool wasOpen = false;
            SQLiteContext context = null;
            try
            {
                context = LazyLoad(ref wasOpen);

                // Refresh foreign key values if the parent properties have changed
                if (Entity.DirtyProperties.Count > 0)
                {
                    // Cache the FK property names on first access
                    if (_cachedFkPropertyNames == null)
                    {
                        _cachedFkPropertyNames = ChildTable.ForeignKeys
                            .Where(x => x.ParentEntityType == ParentTable.EntityType)
                            .SelectMany(x => x.Reference.PropertyNames)
                            .ToHashSet();
                    }

                    // Note: DirtyProperties is a HashSet<string>, so .Overlaps() is O(min(N,M)) — much faster than .Intersect().Any() which allocates an iterator.
                    if (Entity.DirtyProperties.Overlaps(_cachedFkPropertyNames))
                    {
                        ForeignKeyValues = null;
                        LazyLoad(ref wasOpen);
                    }
                }

                // Begin a new Select Query
                SelectQueryBuilder query = new SelectQueryBuilder(context);
                query.From(ChildTable.TableName).SelectAll();
                var whereStetement = query.WhereStatement;

                foreach (var group in ForeignKeyValues)
                {
                    foreach (var kvp in group)
                    {
                        string colName = kvp.Key;
                        object attrValue = kvp.Value;
                        whereStetement.And(colName, Comparison.Equals, attrValue);
                    }
                    whereStetement.CreateNewClause();
                }

                using (SqliteCommand command = query.BuildCommand())
                using (SqliteDataReader reader = command.ExecuteReader())
                {
                    int[] pkOrdinals = null;
                    AttributeInfo[] columnMap = null;

                    if (reader.HasRows)
                    {
                        columnMap = new AttributeInfo[reader.FieldCount];
                        for (int i = 0; i < reader.FieldCount; i++)
                            columnMap[i] = ChildTable.GetAttributeByColumnName(reader.GetName(i));

                        if (context.UseIdentityMapping && ChildTable.PrimaryKeys.Count > 0)
                        {
                            pkOrdinals = ChildTable.PrimaryKeys
                                .Select(pk => reader.GetOrdinal(pk.ColumnName))
                                .ToArray();
                        }
                    }

                    while (reader.Read())
                        yield return context.ConvertToEntity<TChildEntity>(ChildTable, reader, pkOrdinals, columnMap);
                }
            }
            finally
            {
                if (!wasOpen && context != null)
                {
                    context.Dispose();
                }
            }
        }

        /// <summary>
        /// Moves the specified Entities from this Collection to a different one
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="entities"></param>
        /// <param name="target"></param>
        public override void MoveTo(IEnumerable<TChildEntity> entities, EntitySet<TChildEntity> target)
        {
            // Use the existing transaction if available to avoid nested transaction crashes
            bool autoTransaction = Context.Transaction == null;
            using (var ts = autoTransaction ? Context.BeginTransaction() : null)
            {
                try
                {
                    target.AddRange(entities); // Sets new FKs and saves
                    ts?.Commit();
                }
                catch
                {
                    ts?.Rollback();
                    throw;
                }
            }
        }

        /// <summary>
        /// Retrieves all child entities that match the specified criteria from the database.
        /// </summary>
        /// <remarks>
        /// This method applies the provided filtering condition to retrieve a subset of child entities
        /// associated with the parent entity. It uses foreign key constraints and the provided filter to build
        /// and execute an SQL query for data retrieval.
        /// </remarks>
        /// <param name="where">The filtering condition to apply when querying the database. Must be an implementation
        /// of <see cref="IWhereStatement"/>. Unsupported types will result in an <see cref="ArgumentException"/>.</param>
        /// <returns>A list of child entities that satisfy the specified filter condition.</returns>
        /// <exception cref="ArgumentException">Thrown if the provided <paramref name="where"/> is not a recognized implementation
        /// of <see cref="IWhereStatement"/>.</exception>
        public override List<TChildEntity> FindAll(IWhereStatement where)
        {
            bool wasOpen = false;
            SQLiteContext context = LazyLoad(ref wasOpen);

            try
            {
                var query = new SelectQueryBuilder(context)
                    .From(ChildTable.TableName)
                    .SelectAll();

                // MergeFrom needs the concrete base type — check both possibilities
                if (where is WhereStatement ws)
                    query.WhereStatement.MergeFrom(ws);
                else if (where is SelectWhereStatement sws)
                    query.WhereStatement.MergeFrom(sws);
                else
                    throw new ArgumentException("Unsupported IWhereStatement implementation.", nameof(where));
                
                // FK scope
                foreach (var group in ForeignKeyValues)
                {
                    foreach (var kvp in group)
                        query.Where(kvp.Key, Comparison.Equals, kvp.Value);
                }

                using var command = query.BuildCommand();
                using var reader = command.ExecuteReader();
                var results = new List<TChildEntity>();

                if (reader.HasRows)
                {
                    var columnMap = new AttributeInfo[reader.FieldCount];
                    for (int i = 0; i < reader.FieldCount; i++)
                        columnMap[i] = ChildTable.GetAttributeByColumnName(reader.GetName(i));

                    int[] pkOrdinals = null;
                    if (context.UseIdentityMapping && ChildTable.PrimaryKeys.Count > 0)
                    {
                        pkOrdinals = ChildTable.PrimaryKeys
                            .Select(pk => reader.GetOrdinal(pk.ColumnName))
                            .ToArray();
                    }

                    while (reader.Read())
                        results.Add(context.ConvertToEntity<TChildEntity>(ChildTable, reader, pkOrdinals, columnMap));
                }
                
                return results;
            }
            finally
            {
                if (!wasOpen) context.Dispose();
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
            // Add the entity
            AddInternal(entity);

            // Trigger the event
            OnEntityAdded(entity);
        }

        /// <summary>
        /// Adds a range of child entities to the collection, ensuring that foreign key relationships between the parent and child
        /// entities are properly established.
        /// </summary>
        /// <remarks>This method automatically sets the foreign key values on the child entities based on
        /// the corresponding attributes of the parent entity. If a child entity already exists in the collection, it
        /// will be updated instead of added.</remarks>
        /// <param name="entities">The entities to add as children to the collection</param>
        /// <exception cref="InvalidOperationException">Thrown if updating an Entity, and changing a foreign key parentAttr that is also a primary key.</exception>
        public override void AddRange(IEnumerable<TChildEntity> entities)
        {
            bool wasOpen = false;
            SQLiteContext context = LazyLoad(ref wasOpen);

            bool autoTransaction = context.Transaction == null;
            using (var ts = autoTransaction ? context.BeginTransaction() : null)
            {
                try
                {
                    foreach (var entity in entities)
                    {
                        AddInternal(entity, context, disposeContext: false);
                    }
                    ts?.Commit();
                    OnEntitiesAdded(entities);
                }
                catch
                {
                    ts?.Rollback();
                    throw;
                }
            }

            if (!wasOpen) context.Dispose();
        }

        /// <summary>
        /// Removes the specified child entity from the current context.
        /// </summary>
        /// <remarks>This method disassociates the specified child entity from the parent entity. Ensure
        /// that the entity is part of the current context before calling this method.</remarks>
        /// <param name="entity">The child entity to remove. Cannot be <see langword="null"/>.</param>
        public override void Remove(TChildEntity entity)
        {
            bool wasOpen = false;
            SQLiteContext context = LazyLoad(ref wasOpen);

            try
            {
                Disassociate([entity], context);

                // Trigger the event
                OnEntityRemoved(entity);
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
        /// Removess the specified child entities from the current context.
        /// </summary>
        /// <param name="entities"></param>
        public override void RemoveRange(IEnumerable<TChildEntity> entities)
        {
            bool wasOpen = false;
            SQLiteContext context = LazyLoad(ref wasOpen);

            bool autoTransaction = context.Transaction == null;
            using (var ts = autoTransaction ? context.BeginTransaction() : null)
            {
                try
                {
                    Disassociate(entities, context);
                    ts?.Commit();

                    // Only fire the bulk event AFTER the commit is successful
                    OnEntitiesRemoved(entities);
                }
                catch
                {
                    ts?.Rollback();
                    throw;
                }
                finally
                {
                    if (!wasOpen)
                        context.Dispose();
                }
            }
        }

        /// <summary>
        /// Removes all items from the collection and disassociates them.
        /// </summary>
        /// <remarks>This method clears the collection by disassociating each item before removal. Any
        /// necessary cleanup or state changes related to the disassociation process are performed for each
        /// item.</remarks>
        public override void Clear()
        {
            bool wasOpen = false;
            SQLiteContext context = LazyLoad(ref wasOpen);

            try
            {
                using var query = new UpdateQueryBuilder(context);
                query.SetTable(ChildTable.TableName);

                foreach (var group in ForeignKeyValues)
                {
                    foreach (var kvp in group)
                    {
                        query.Set(kvp.Key, null);
                        query.WhereStatement.And(kvp.Key, Comparison.Equals, kvp.Value);
                    }
                    query.WhereStatement.CreateNewClause();
                }

                query.Execute();

                if (context.UseIdentityMapping)
                    context.ClearIdentityMap(typeof(TChildEntity));
            }
            finally
            {
                if (!wasOpen)
                    context.Dispose();
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
        /// Returns the first child entity matching the predicate, or null if none found.
        /// </summary>
        public TChildEntity FirstOrDefault(Expression<Func<TChildEntity, bool>> predicate)
        {
            return Where(predicate).FirstOrDefault();
        }
        
        /// <summary>
        /// Returns the last child entity matching the predicate, or null if none found.
        /// </summary>
        public TChildEntity LastOrDefault(Expression<Func<TChildEntity, bool>> predicate)
        {
            return Where(predicate).LastOrDefault();
        }
        
        /// <summary>
        /// Determines whether any child entities match the predicate.
        /// </summary>
        public bool Any(Expression<Func<TChildEntity, bool>> predicate)
        {
            return Where(predicate).Any();
        }

        /// <summary>
        /// Determines whether any child entities exist.
        /// </summary>
        public bool Any()
        {
            bool wasOpen = false;
            SQLiteContext context = LazyLoad(ref wasOpen);

            try
            {
                var query = new SelectQueryBuilder(context)
                    .From(ChildTable.TableName)
                    .SelectCount()
                    .Take(1);

                foreach (var group in ForeignKeyValues)
                {
                    foreach (var kvp in group)
                        query.Where(kvp.Key, Comparison.Equals, kvp.Value);
                }

                return query.ExecuteScalar<int>() > 0;
            }
            finally
            {
                if (!wasOpen) context.Dispose();
            }
        }
        
        /// <summary>
        /// Returns the count of child entities matching the predicate.
        /// </summary>
        public int CountWhere(Expression<Func<TChildEntity, bool>> predicate)
        {
            return Where(predicate).Count();
        }

        /// <summary>
        /// Disassociates the specified child entity from its parent entities by clearing the foreign key constraints in
        /// the database.
        /// </summary>
        /// <remarks>This method updates the database to remove the association between the specified
        /// child entity and its parent entities. It clears the foreign key constraints by setting the corresponding
        /// columns to null. The operation is performed within the context of the associated SQLite database.</remarks>
        /// <param name="entities">The child entities to be disassociated. This entity's foreign key values will be set to null in the database.</param>
        private void Disassociate(IEnumerable<TChildEntity> entities, SQLiteContext context)
        {
            foreach (var item in entities)
            {
                if (item.State == EntityState.New)
                    continue;

                // Create a fresh builder per entity to avoid accumulated SET clauses
                using var query = new UpdateQueryBuilder(context);
                query.SetTable(ChildTable.TableName);
                var whereStatement = query.WhereStatement;

                foreach (var group in ForeignKeyValues)
                {
                    foreach (var kvp in group)
                    {
                        string colName = kvp.Key;
                        object attrValue = kvp.Value;

                        var childAttr = ChildTable.GetAttributeByColumnName(colName);
                        if (!childAttr.IsNullable)
                        {
                            throw new InvalidOperationException(
                                $"Cannot disassociate entity \"{typeof(TChildEntity)}\" because foreign key attribute \"{childAttr.Property.Name}\" is not nullable.");
                        }

                        whereStatement.And(colName, Comparison.Equals, attrValue);
                        query.Set(colName, null);
                    }

                    whereStatement.CreateNewClause();
                }

                // Add PK-based WHERE to target only this specific entity
                foreach (var pk in ChildTable.PrimaryKeys)
                {
                    whereStatement.And(pk.ColumnName, Comparison.Equals, pk.GetValue(item));
                }

                using var command = query.BuildCommand();
                command.ExecuteNonQuery();
            }
        }

        private void AddInternal(TChildEntity entity, SQLiteContext context = null, bool disposeContext = false)
        {
            bool wasOpen = true;
            if (context == null)
            {
                wasOpen = false;
                bool wo = false;
                context = LazyLoad(ref wo);
                wasOpen = wo;
                disposeContext = !wasOpen;
            }

            foreach (var group in ForeignKeyValues)
            {
                foreach (var kvp in group)
                {
                    string childColName = kvp.Key;
                    object parentAttrValue = kvp.Value;

                    AttributeInfo childAttr = ChildTable.GetAttributeByColumnName(childColName);

                    if (entity.State != EntityState.New)
                    {
                        if (ChildTable.PrimaryKeys.Any(x => x.ColumnName == childColName))
                        {
                            object currentValue = childAttr.GetValue(entity);
                            if (!currentValue.Equals(parentAttrValue))
                            {
                                throw new InvalidOperationException(
                                    $"Cannot change the value of foreign key attribute \"{childAttr.Property.Name}\" on entity \"{typeof(TChildEntity)}\" because it is also a primary key.");
                            }
                        }
                    }

                    childAttr.SetValue(entity, parentAttrValue);
                }
            }

            ChildCollection.AddOrUpdate(entity);

            if (disposeContext)
            {
                context.Dispose();
            }
        }
    }
}
