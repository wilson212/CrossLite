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
    /// A projected query that SELECTs specific columns and maps them to <typeparamref name="TResult"/>.
    /// Returned by <see cref="DbQuery{TEntity}.Select{TResult}"/>.
    /// </summary>
    public class DbProjection<TEntity, TResult> : IEnumerable<TResult>
        where TEntity : EntityBase, new()
    {
        private readonly SQLiteContext _context;
        private readonly TableMapping _table;
        private readonly string _whereClause;
        private readonly List<SqliteParameter> _parameters;
        private readonly List<(string Column, bool Descending)> _orderByClauses;
        private int? _limit;
        private int? _offset;

        // Projection state
        private readonly List<(string ColumnName, string PropertyName)> _selectedColumns;
        private readonly Func<SqliteDataReader, int[], TResult> _projector;

        internal DbProjection(
            SQLiteContext context,
            TableMapping table,
            string whereClause,
            List<SqliteParameter> parameters,
            List<(string Column, bool Descending)> orderByClauses,
            int? limit,
            int? offset,
            List<(string ColumnName, string PropertyName)> selectedColumns,
            Func<SqliteDataReader, int[], TResult> projector)
        {
            _context = context;
            _table = table;
            _whereClause = whereClause;
            _parameters = parameters;
            _orderByClauses = orderByClauses;
            _limit = limit;
            _offset = offset;
            _selectedColumns = selectedColumns;
            _projector = projector;
        }

        public DbProjection<TEntity, TResult> OrderBy<TKey>(Expression<Func<TEntity, TKey>> keySelector)
        {
            _orderByClauses.Add((ResolveColumnName(keySelector), false));
            return this;
        }

        public DbProjection<TEntity, TResult> OrderByDescending<TKey>(Expression<Func<TEntity, TKey>> keySelector)
        {
            _orderByClauses.Add((ResolveColumnName(keySelector), true));
            return this;
        }

        public DbProjection<TEntity, TResult> ThenBy<TKey>(Expression<Func<TEntity, TKey>> keySelector)
            => OrderBy(keySelector);

        public DbProjection<TEntity, TResult> ThenByDescending<TKey>(Expression<Func<TEntity, TKey>> keySelector)
            => OrderByDescending(keySelector);

        public DbProjection<TEntity, TResult> Take(int count)
        {
            _limit = count;
            return this;
        }

        public DbProjection<TEntity, TResult> Skip(int count)
        {
            _offset = count;
            return this;
        }

        public TResult FirstOrDefault()
        {
            _limit = 1;
            return Execute().FirstOrDefault();
        }

        public List<TResult> ToList() => Execute().ToList();

        private IEnumerable<TResult> Execute()
        {
            string table = _context.QuoteIdentifier(_table.TableName);
            var sb = new StringBuilder();

            // SELECT specific columns instead of *
            sb.Append("SELECT ");
            for (int i = 0; i < _selectedColumns.Count; i++)
            {
                if (i > 0) sb.Append(", ");
                sb.Append(_context.QuoteIdentifier(_selectedColumns[i].ColumnName));
            }
            sb.Append(" FROM ").Append(table);

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

            command.Connection = _context.Connection;
            using var reader = command.ExecuteReader();

            if (!reader.HasRows)
                yield break;

            // Build ordinal map once
            int[] ordinals = new int[_selectedColumns.Count];
            for (int i = 0; i < _selectedColumns.Count; i++)
                ordinals[i] = reader.GetOrdinal(_selectedColumns[i].ColumnName);

            while (reader.Read())
                yield return _projector(reader, ordinals);
        }

        public IEnumerator<TResult> GetEnumerator() => Execute().GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        private string ResolveColumnName<TKey>(Expression<Func<TEntity, TKey>> selector)
        {
            Expression body = selector.Body;
            if (body is UnaryExpression unary && unary.NodeType == ExpressionType.Convert)
                body = unary.Operand;

            if (body is MemberExpression member && member.Member is PropertyInfo prop)
            {
                if (_table.EntityProperties.TryGetValue(prop.Name, out var info))
                    return info.ColumnName;
            }
            throw new NotSupportedException("OrderBy selector must be a simple property access.");
        }
    }
}