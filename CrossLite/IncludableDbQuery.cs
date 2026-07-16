using System;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace CrossLite
{
    /// <summary>
    /// Represents a query with an included navigation property, supporting further nested includes.
    /// Allows chaining .ThenInclude() for nested relationships or .Include() to go back to the root entity.
    /// </summary>
    public class IncludableDbQuery<TEntity, TPreviousProperty>
        where TEntity : EntityBase, new()
        where TPreviousProperty : EntityBase
    {
        private readonly DbQuery<TEntity> _query;
        private readonly DbQuery<TEntity>.IncludeNode _currentNode;

        internal IncludableDbQuery(DbQuery<TEntity> query, DbQuery<TEntity>.IncludeNode currentNode)
        {
            _query = query;
            _currentNode = currentNode;
        }

        /// <summary>
        /// Eagerly loads a navigation property from the previously included entity (nested include).
        /// </summary>
        public IncludableDbQuery<TEntity, TProperty> ThenInclude<TProperty>(
            Expression<Func<TPreviousProperty, TProperty>> navigationProperty)
            where TProperty : EntityBase
        {
            if (navigationProperty.Body is not MemberExpression memberExpr)
                throw new ArgumentException("Expression must be a simple property access");

            string propertyName = memberExpr.Member.Name;
            var parentTable = TableCache.GetTableMap(typeof(TPreviousProperty));

            // Validate it's a foreign key navigation property
            if (!parentTable.FkByChildProperty.TryGetValue(propertyName, out var fkInfo))
                throw new ArgumentException($"Property '{propertyName}' is not a foreign key navigation property");

            var includeNode = new DbQuery<TEntity>.IncludeNode
            {
                PropertyName = propertyName,
                PropertyType = typeof(TProperty),
                ForeignKeyInfo = fkInfo
            };

            // Add as nested include to the current node
            _query.AddNestedInclude(_currentNode, includeNode);

            return new IncludableDbQuery<TEntity, TProperty>(_query, includeNode);
        }
        
        /// <summary>
        /// Eagerly loads a sibling navigation property from the same parent entity
        /// (avoids having to call .Include() again to go back to the root).
        /// </summary>
        public IncludableDbQuery<TEntity, TProperty> ThenIncludeSibling<TProperty>(
            Expression<Func<TPreviousProperty, TProperty>> navigationProperty)
            where TProperty : EntityBase
        {
            // This is identical to ThenInclude, but returns to the same parent level
            return ThenInclude(navigationProperty);
        }

        /// <summary>
        /// Eagerly loads a sibling navigation property from the root entity (goes back up the nest).
        /// </summary>
        public IncludableDbQuery<TEntity, TProperty> Include<TProperty>(
            Expression<Func<TEntity, TProperty>> navigationProperty)
            where TProperty : EntityBase
        {
            return _query.Include(navigationProperty);
        }

        /// <summary>
        /// Adds an ORDER BY clause (ascending) on the given property of the root entity.
        /// </summary>
        public IncludableDbQuery<TEntity, TPreviousProperty> OrderBy<TKey>(Expression<Func<TEntity, TKey>> keySelector)
        {
            _query.OrderBy(keySelector);
            return this;
        }

        /// <summary>
        /// Adds an ORDER BY clause (descending) on the given property of the root entity.
        /// </summary>
        public IncludableDbQuery<TEntity, TPreviousProperty> OrderByDescending<TKey>(Expression<Func<TEntity, TKey>> keySelector)
        {
            _query.OrderByDescending(keySelector);
            return this;
        }

        /// <summary>
        /// SQL LIMIT — restricts the number of rows returned.
        /// </summary>
        public IncludableDbQuery<TEntity, TPreviousProperty> Take(int count)
        {
            _query.Take(count);
            return this;
        }

        /// <summary>
        /// SQL OFFSET — skips the first N rows.
        /// </summary>
        public IncludableDbQuery<TEntity, TPreviousProperty> Skip(int count)
        {
            _query.Skip(count);
            return this;
        }

        /// <summary>
        /// Materializes the query and eagerly loads all included navigation properties.
        /// </summary>
        public List<TEntity> ToList()
        {
            return _query.ToList();
        }

        /// <summary>
        /// Convenience: returns the first match or default.
        /// </summary>
        public TEntity FirstOrDefault()
        {
            return _query.FirstOrDefault();
        }

        /// <summary>
        /// Determines whether any elements exist in the query result.
        /// </summary>
        public bool Any()
        {
            return _query.Any();
        }
    }
}