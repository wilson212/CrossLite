using Microsoft.Data.Sqlite;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;

namespace CrossLite
{
    /// <summary>
    /// A deferred, fluent query builder returned by <c>DbSet.Where()</c>.
    /// No SQL is executed until enumeration (foreach / ToList / etc.).
    /// </summary>
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
        /// Internally appends LIMIT 1.
        /// </summary>
        public TEntity FirstOrDefault()
        {
            _limit = 1;
            return Execute().FirstOrDefault();
        }

        /// <summary>
        /// Materializes the query into a list.
        /// </summary>
        public List<TEntity> ToList() => Execute().ToList();

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
    }
}