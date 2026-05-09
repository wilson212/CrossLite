using System.Collections.Generic;

namespace CrossLite.QueryBuilder
{
    /// <summary>
    /// A WHERE statement specifically designed for use with <see cref="SelectQueryBuilder"/>.
    /// 
    /// Inherits all clause-building and SQL generation logic from <see cref="WhereStatementBase{TSelf}"/>.
    /// The only addition is a back-reference to the parent <see cref="SelectQueryBuilder"/>, which enables
    /// fluent re-chaining methods (e.g., <c>.Where("x", Equals, 5).SelectAll().Take(10)</c>).
    /// 
    /// These re-chaining methods are purely ergonomic shortcuts — they delegate directly to the
    /// parent query builder. They exist here so the caller doesn't have to break the fluent chain
    /// after adding a WHERE clause.
    /// </summary>
    public class SelectWhereStatement : WhereStatementBase<SelectWhereStatement>
    {
        /// <summary>
        /// The parent <see cref="SelectQueryBuilder"/> this WHERE statement is attached to.
        /// Null when the statement is used standalone (rare for this type).
        /// </summary>
        internal SelectQueryBuilder Query { get; set; }

        /// <summary>
        /// Creates a new empty <see cref="SelectWhereStatement"/> with default quoting settings.
        /// </summary>
        public SelectWhereStatement() { }

        /// <summary>
        /// Creates a new <see cref="SelectWhereStatement"/> using the quoting settings
        /// from the supplied <see cref="SQLiteContext"/>.
        /// </summary>
        /// <param name="context">The database context whose quoting settings will be applied.</param>
        public SelectWhereStatement(SQLiteContext context) : this()
        {
            AttributeQuoteMode = context.IdentifierQuoteMode;
            AttributeQuoteKind = context.IdentifierQuoteKind;
        }

        /// <summary>
        /// Creates a new <see cref="SelectWhereStatement"/> attached to the specified query builder.
        /// Quoting settings are inherited from the query builder's context.
        /// </summary>
        /// <param name="query">The parent query builder to attach to for re-chaining.</param>
        public SelectWhereStatement(SelectQueryBuilder query) : this()
        {
            Query = query;
            AttributeQuoteMode = query.Context.IdentifierQuoteMode;
            AttributeQuoteKind = query.Context.IdentifierQuoteKind;
        }

        #region Re-Chaining Methods
        // These methods allow the caller to fluently chain back to the SelectQueryBuilder
        // after adding WHERE expressions, without needing to store a separate reference.
        // Example: query.Where("rank", Equals, 5).SelectAll().Take(10).OrderBy("name", Ascending);

        /// <summary>
        /// Selects all columns in the query. Re-chains to <see cref="SelectQueryBuilder.SelectAll"/>.
        /// </summary>
        public SelectQueryBuilder SelectAll() => Query?.SelectAll();

        /// <summary>
        /// Selects a single column, optionally with an alias.
        /// Re-chains to <see cref="SelectQueryBuilder.SelectColumn"/>.
        /// </summary>
        /// <param name="column">The column name to select.</param>
        /// <param name="alias">Optional alias for the column in the result set.</param>
        /// <param name="escape">Whether to quote the column name. Default is true.</param>
        public SelectQueryBuilder SelectColumn(string column, string alias = null, bool escape = true)
            => Query?.SelectColumn(column, alias, escape);

        /// <summary>
        /// Selects multiple columns by name. Re-chains to <see cref="SelectQueryBuilder.Select(string[])"/>.
        /// </summary>
        /// <param name="columns">The column names to select.</param>
        public SelectQueryBuilder Select(params string[] columns) => Query?.Select(columns);

        /// <summary>
        /// Selects multiple columns by name. Re-chains to <see cref="SelectQueryBuilder.Select(IEnumerable{string})"/>.
        /// </summary>
        /// <param name="columns">The column names to select.</param>
        public SelectQueryBuilder Select(IEnumerable<string> columns) => Query?.Select(columns);

        /// <summary>
        /// Adds a COUNT aggregate to the SELECT clause.
        /// COUNT(*) returns total rows; COUNT(column) returns non-NULL count.
        /// </summary>
        /// <param name="columnName">The column to count, or "*" for all rows. Default is "*".</param>
        /// <param name="alias">Optional alias for the result column.</param>
        public SelectQueryBuilder SelectCount(string columnName = "*", string alias = null)
            => Query?.Aggregate(columnName, alias, AggregateFunction.Count);

        /// <summary>
        /// Adds a COUNT(DISTINCT column) aggregate to the SELECT clause.
        /// Returns the number of unique non-NULL values in the specified column.
        /// </summary>
        /// <param name="columnName">The column to count distinct values of.</param>
        /// <param name="alias">Optional alias for the result column.</param>
        public SelectQueryBuilder SelectDistinctCount(string columnName, string alias = null)
            => Query?.Aggregate(columnName, alias, AggregateFunction.DistinctCount);

        /// <summary>
        /// Adds an AVG aggregate to the SELECT clause.
        /// Returns the average of all non-NULL values in the group.
        /// </summary>
        /// <param name="columnName">The column to average.</param>
        /// <param name="alias">Optional alias for the result column.</param>
        public SelectQueryBuilder SelectAverage(string columnName, string alias = null)
            => Query?.Aggregate(columnName, alias, AggregateFunction.Average);

        /// <summary>
        /// Adds a MIN aggregate to the SELECT clause.
        /// Returns the minimum non-NULL value in the group.
        /// </summary>
        /// <param name="columnName">The column to find the minimum of.</param>
        /// <param name="alias">Optional alias for the result column.</param>
        public SelectQueryBuilder SelectMin(string columnName, string alias = null)
            => Query?.Aggregate(columnName, alias, AggregateFunction.Min);

        /// <summary>
        /// Adds a MAX aggregate to the SELECT clause.
        /// Returns the maximum value in the group.
        /// </summary>
        /// <param name="columnName">The column to find the maximum of.</param>
        /// <param name="alias">Optional alias for the result column.</param>
        public SelectQueryBuilder SelectMax(string columnName, string alias = null)
            => Query?.Aggregate(columnName, alias, AggregateFunction.Max);

        /// <summary>
        /// Adds a SUM aggregate to the SELECT clause.
        /// Returns the sum of all non-NULL values in the group.
        /// </summary>
        /// <param name="columnName">The column to sum.</param>
        /// <param name="alias">Optional alias for the result column.</param>
        public SelectQueryBuilder SelectSum(string columnName, string alias = null)
            => Query?.Aggregate(columnName, alias, AggregateFunction.Sum);

        /// <summary>
        /// Sets the OFFSET (number of rows to skip) in the result set.
        /// </summary>
        /// <param name="count">The number of rows to skip.</param>
        /// <returns>The parent <see cref="SelectQueryBuilder"/>, or null if not attached.</returns>
        public SelectQueryBuilder Skip(int count) => Query?.Skip(count);

        /// <summary>
        /// Sets the LIMIT (maximum number of rows to return) in the result set.
        /// </summary>
        /// <param name="count">The maximum number of rows to return.</param>
        /// <returns>The parent <see cref="SelectQueryBuilder"/>, or null if not attached.</returns>
        public SelectQueryBuilder Take(int count) => Query?.Take(count);

        /// <summary>
        /// Adds an ORDER BY clause using a pre-built <see cref="OrderByClause"/>.
        /// </summary>
        /// <param name="clause">The order-by clause to add.</param>
        /// <returns>The parent <see cref="SelectQueryBuilder"/>, or null if not attached.</returns>
        public SelectQueryBuilder OrderBy(OrderByClause clause) => Query?.OrderBy(clause);

        /// <summary>
        /// Adds an ORDER BY clause for the specified column and sort direction.
        /// </summary>
        /// <param name="fieldName">The column name to sort by.</param>
        /// <param name="order">The sort direction (Ascending or Descending).</param>
        /// <returns>The parent <see cref="SelectQueryBuilder"/>, or null if not attached.</returns>
        public SelectQueryBuilder OrderBy(string fieldName, Sorting order) => Query?.OrderBy(fieldName, order);

        /// <summary>
        /// Adds a GROUP BY clause for the specified column.
        /// </summary>
        /// <param name="fieldName">The column name to group by.</param>
        /// <returns>The parent <see cref="SelectQueryBuilder"/>, or null if not attached.</returns>
        public SelectQueryBuilder GroupBy(string fieldName) => Query?.GroupBy(fieldName);

        #endregion
    }
}