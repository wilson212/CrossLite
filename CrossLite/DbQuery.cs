using Microsoft.Data.Sqlite;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using CrossLite.CodeFirst;

namespace CrossLite
{
    /// <summary>
    /// Represents a queryable interface for fetching and manipulating entities of the specified type from a SQLite database.
    /// Provides functionality for filtering, ordering, skipping, taking, and including related entities in the query results.
    /// </summary>
    /// <typeparam name="TEntity">The type of the entity being queried. Must inherit from <see cref="EntityBase"/> and have a parameterless constructor.</typeparam>
    public class DbQuery<TEntity> : IEnumerable<TEntity> where TEntity : EntityBase, new()
    {
        private readonly SQLiteContext _context;
        private readonly TableMapping _table;

        // Accumulated state
        private readonly string _whereClause;
        private readonly List<SqliteParameter> _parameters = new();
        private readonly List<(string Column, bool Descending)> _orderByClauses = new();
        private int? _limit;
        private int? _offset;
        private readonly List<IncludeNode> _includes = new();

        internal DbQuery(SQLiteContext context, TableMapping table,
            string whereClause, IEnumerable<SqliteParameter> parameters)
        {
            _context = context;
            _table = table;
            _whereClause = whereClause;
            _parameters.AddRange(parameters);
        }

        /// <summary>
        /// Adds an ORDER BY clause (ascending) on the given property.
        /// </summary>
        public DbQuery<TEntity> OrderBy<TKey>(Expression<Func<TEntity, TKey>> keySelector)
        {
            _orderByClauses.Add((ResolveColumnName(keySelector), false));
            return this;
        }

        /// <summary>
        /// Adds an ORDER BY clause (descending) on the given property.
        /// </summary>
        public DbQuery<TEntity> OrderByDescending<TKey>(Expression<Func<TEntity, TKey>> keySelector)
        {
            _orderByClauses.Add((ResolveColumnName(keySelector), true));
            return this;
        }

        /// <summary>
        /// Adds a secondary sort (ascending). Alias for <see cref="OrderBy{TKey}"/>.
        /// </summary>
        public DbQuery<TEntity> ThenBy<TKey>(Expression<Func<TEntity, TKey>> keySelector)
            => OrderBy(keySelector);

        /// <summary>
        /// Adds a secondary sort (descending). Alias for <see cref="OrderByDescending{TKey}"/>.
        /// </summary>
        public DbQuery<TEntity> ThenByDescending<TKey>(Expression<Func<TEntity, TKey>> keySelector)
            => OrderByDescending(keySelector);

        /// <summary>
        /// SQL LIMIT — restricts the number of rows returned.
        /// </summary>
        public DbQuery<TEntity> Take(int count)
        {
            _limit = count;
            return this;
        }

        /// <summary>
        /// SQL OFFSET — skips the first N rows.
        /// </summary>
        public DbQuery<TEntity> Skip(int count)
        {
            _offset = count;
            return this;
        }
        
        /// <summary>
        /// Eagerly loads a navigation property so it's accessible after the context is disposed of.
        /// </summary>
        /// <typeparam name="TProperty">The type of the navigation property.</typeparam>
        /// <param name="navigationProperty">Expression selecting the navigation property.</param>
        /// <returns>An IncludableDbQuery that supports .ThenInclude() for nested loading.</returns>
        public IncludableDbQuery<TEntity, TProperty> Include<TProperty>(
            Expression<Func<TEntity, TProperty>> navigationProperty)
            where TProperty : EntityBase
        {
            if (navigationProperty.Body is not MemberExpression memberExpr)
                throw new ArgumentException("Expression must be a simple property access");

            string propertyName = memberExpr.Member.Name;

            // Validate it's a foreign key navigation property
            if (!_table.FkByChildProperty.TryGetValue(propertyName, out var fkInfo))
                throw new ArgumentException($"Property '{propertyName}' is not a foreign key navigation property");

            var includeNode = new IncludeNode
            {
                PropertyName = propertyName,
                PropertyType = typeof(TProperty),
                ForeignKeyInfo = fkInfo
            };

            _includes.Add(includeNode);

            return new IncludableDbQuery<TEntity, TProperty>(this, includeNode);
        }
        
        internal void AddNestedInclude(IncludeNode parentNode, IncludeNode childNode)
        {
            parentNode.NestedIncludes.Add(childNode);
        }
        
        /// <summary>
        /// Projects each entity into a new form, selecting only the specified columns.
        /// Returns a <see cref="DbProjection{TEntity, TResult}"/> that generates
        /// SELECT [col1], [col2] instead of SELECT *.
        /// </summary>
        /// <example>
        /// dbSet.Where(x => x.IsActive)
        ///      .Select(x => new { x.Name, x.TIS })
        ///      .OrderBy(x => x.TIS)  // Note: OrderBy still references TEntity columns
        ///      .Take(10)
        ///      .ToList();
        /// </example>
        public DbProjection<TEntity, TResult> Select<TResult>(Expression<Func<TEntity, TResult>> selector)
        {
            var (columns, projector) = SelectExpressionAnalyzer.Analyze<TEntity, TResult>(_table, selector);

            return new DbProjection<TEntity, TResult>(
                _context, _table, _whereClause,
                new List<SqliteParameter>(_parameters),
                new List<(string, bool)>(_orderByClauses),
                _limit, _offset,
                columns, projector);
        }

        /// <summary>
        /// Convenience: returns the first match or default.
        /// </summary>
        public TEntity FirstOrDefault()
        {
            var oldLimit = _limit;
            _limit = 1;
            var result = Execute().FirstOrDefault();
            _limit = oldLimit;
            return result;
        }

        /// <summary>
        /// Materializes the query into a list and eagerly loads all included navigation properties.
        /// </summary>
        public List<TEntity> ToList()
        {
            var entities = Execute().ToList();

            // Process includes if any were specified
            if (entities.Count > 0 && _includes.Count > 0)
            {
                foreach (var include in _includes)
                {
                    ProcessInclude(entities, include);
                }
            }

            return entities;
        }

        /// <summary>
        /// Determines whether any elements exist in the query result.
        /// </summary>
        /// <returns>
        /// true if the query contains at least one element; otherwise, false.
        /// </returns>
        public bool Any() => FirstOrDefault() != null;

        /// <summary>
        /// Builds the final SQL and executes it.
        /// </summary>
        private IEnumerable<TEntity> Execute()
        {
            string table = _context.QuoteIdentifier(_table.TableName);
            var sb = new StringBuilder();
            sb.Append("SELECT * FROM ").Append(table);

            if (!string.IsNullOrEmpty(_whereClause))
                sb.Append(" WHERE ").Append(_whereClause);

            if (_orderByClauses.Count > 0)
            {
                sb.Append(" ORDER BY ");
                for (int i = 0; i < _orderByClauses.Count; i++)
                {
                    if (i > 0) sb.Append(", ");
                    sb.Append(_context.QuoteIdentifier(_orderByClauses[i].Column));
                    sb.Append(_orderByClauses[i].Descending ? " DESC" : " ASC");
                }
            }

            if (_limit.HasValue)
                sb.Append(" LIMIT ").Append(_limit.Value);

            if (_offset.HasValue)
                sb.Append(" OFFSET ").Append(_offset.Value);

            using var command = _context.CreateCommand(sb.ToString());
            foreach (var p in _parameters)
                command.Parameters.Add(p);

            return _context.ExecuteReader<TEntity>(command);
        }

        public IEnumerator<TEntity> GetEnumerator() => Execute().GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        /// <summary>
        /// Resolves a property-access lambda to its mapped column name.
        /// </summary>
        private string ResolveColumnName<TKey>(Expression<Func<TEntity, TKey>> selector)
        {
            Expression body = selector.Body;
            if (body is UnaryExpression unary && unary.NodeType == ExpressionType.Convert)
                body = unary.Operand;

            if (body is MemberExpression member && member.Member is PropertyInfo prop)
            {
                if (_table.EntityProperties.TryGetValue(prop.Name, out var info))
                    return info.ColumnName;

                throw new NotSupportedException(
                    $"Property '{prop.Name}' is not a mapped column on '{typeof(TEntity).Name}'.");
            }

            throw new NotSupportedException("OrderBy selector must be a simple property access.");
        }

        /// <summary>
        /// Processes a specific include directive, loading related entities based on the foreign key relationships
        /// defined by the include node. This method retrieves all related entities in a single query and assigns
        /// them to their respective parent entities, enabling eager loading of the specified navigation property
        /// in the provided entity list. It also handles nested include directives recursively.
        /// </summary>
        /// <typeparam name="TParent">
        /// The type of the parent entities being processed, which must inherit from the EntityBase class.
        /// </typeparam>
        /// <param name="parentEntities">
        /// The list of parent entities whose navigation property should be populated with related data.
        /// </param>
        /// <param name="includeNode">
        /// The include directive that specifies the navigation property to be populated, including foreign key
        /// and nested include information.
        /// </param>
        private void ProcessInclude<TParent>(List<TParent> parentEntities, IncludeNode includeNode)
            where TParent : EntityBase
        {
            var parentTable = TableCache.GetTableMap(typeof(TParent));
            var fkInfo = includeNode.ForeignKeyInfo;

            // Collect all foreign key values from parent entities
            var fkValues = new HashSet<object>();
            var fkPropertyName = fkInfo.ForeignKey.PropertyNames[0]; // Single-column FK
            var fkAttribute = parentTable.GetAttributeByPropertyName(fkPropertyName);

            foreach (var entity in parentEntities)
            {
                var fkValue = fkAttribute.GetValue(entity);
                if (fkValue != null)
                    fkValues.Add(fkValue);
            }

            if (fkValues.Count == 0)
                return;

            // Preload all related entities in one query (populates identity map)
            var childTable = TableCache.GetTableMap(includeNode.PropertyType);
            var pkPropertyName = fkInfo.Reference.PropertyNames[0];
            var pkColumnName = childTable.GetAttributeByPropertyName(pkPropertyName).ColumnName;

            // Build IN clause: WHERE Id IN (@p0, @p1, @p2, ...)
            string inClause = string.Join(", ", fkValues.Select((_, i) => $"@p{i}"));
            string sql = $"SELECT * FROM {_context.QuoteIdentifier(childTable.TableName)} WHERE {_context.QuoteIdentifier(pkColumnName)} IN ({inClause})";

            var childEntities = new List<EntityBase>();

            using (var command = _context.CreateCommand(sql))
            {
                int paramIndex = 0;
                foreach (var fkValue in fkValues)
                    command.Parameters.AddWithValue($"@p{paramIndex++}", fkValue);

                // Execute and materialize child entities (populates identity map)
                childEntities.AddRange(_context.ExecuteReader(command, includeNode.PropertyType));
            }

            // Trigger lazy-load once per parent to populate the ForeignObjectCache
            var propertyInfo = typeof(TParent).GetProperty(includeNode.PropertyName);
            foreach (var entity in parentEntities)
            {
                var fkValue = fkAttribute.GetValue(entity);
                if (fkValue != null)
                {
                    // This will hit the identity map (already loaded), not the database
                    _ = propertyInfo.GetValue(entity);
                }
            }

            // Recursively process nested includes
            if (includeNode.NestedIncludes.Count > 0 && childEntities.Count > 0)
            {
                foreach (var nestedInclude in includeNode.NestedIncludes)
                {
                    ProcessIncludeGeneric(childEntities, nestedInclude);
                }
            }
        }

        /// <summary>
        /// Processes include operations for a list of parent entities and their associated include nodes using reflection.
        /// </summary>
        /// <param name="parentEntities">The list of parent entities to process includes for.</param>
        /// <param name="includeNode">The include node containing information about the relationships to include.</param>
        private void ProcessIncludeGeneric(List<EntityBase> parentEntities, IncludeNode includeNode)
        {
            // Use reflection to call ProcessInclude<TParent> with the correct type
            var method = typeof(DbQuery<TEntity>).GetMethod(nameof(ProcessInclude),
                BindingFlags.NonPublic | BindingFlags.Instance);
            var genericMethod = method.MakeGenericMethod(parentEntities[0].GetType());
            genericMethod.Invoke(this, new object[] { parentEntities, includeNode });
        }
        
        internal class IncludeNode
        {
            public string PropertyName { get; set; }
            public Type PropertyType { get; set; }
            public ForeignKeyConstraint ForeignKeyInfo { get; set; }
            public List<IncludeNode> NestedIncludes { get; set; } = new();
        }
    }
}