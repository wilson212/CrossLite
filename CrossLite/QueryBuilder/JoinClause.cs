using System.Collections.Generic;

namespace CrossLite.QueryBuilder
{
    /// <summary>
    /// This object represents a Join Statement for an SQL query
    /// </summary>
    public class JoinClause
    {
        /// <summary>
        /// Gets the <see cref="SelectQueryBuilder"/> this clause is attached to.
        /// </summary>
        public SelectQueryBuilder Query { get; protected set; }

        /// <summary>
        /// The Join Type
        /// </summary>
        public JoinType JoinType { get; protected set; }

        /// <summary>
        /// Gets the <see cref="JoinExpression"/> for this clause
        /// </summary>
        public JoinExpression Expression { get; protected set; }

        /// <summary>
        /// Gets or sets the Expression Type
        /// </summary>
        internal JoinExpressionType ExpressionType { get; set; } = JoinExpressionType.None;

        /// <summary>
        /// Specifies which table we are joining INTO
        /// </summary>
        public string FromTable { get; set; }

        /// <summary>
        /// Specifies the From Table Comparison Field
        /// </summary>
        public string FromColumn { get; set; }

        /// <summary>
        /// Specifies the Comparison Operator used for the joining of the
        /// two tables
        /// </summary>
        public Comparison ComparisonOperator { get; set; }

        /// <summary>
        /// Specifies the Joining Table ColumnName
        /// </summary>
        public TableIndentifier JoiningTable { get; internal set; }

        /// <summary>
        /// Specifies the Joining Table Comparison Field
        /// </summary>
        public string JoiningColumn { get; set; }

        /// <summary>
        /// Creates a new Join Clause
        /// </summary>
        /// <param name="join">Specifies the Type of Join statement this is.</param>
        /// <param name="joiningTable">The Joining Table name</param>
        public JoinClause(SelectQueryBuilder queryBuilder, JoinType join, TableIndentifier joiningTable)
        {
            this.JoinType = join;
            this.JoiningTable = joiningTable;
            this.Expression = new JoinExpression(this);
            this.Query = queryBuilder;
        }

        /// <summary>
        /// Assigns a temporary rename of the joining table
        /// </summary>
        /// <param name="tableAlias"></param>
        /// <returns></returns>
        public JoinClause As(string tableAlias)
        {
            JoiningTable.Alias = tableAlias;
            return this;
        }

        /// <summary>
        /// Assigns the attaching column name for this clause
        /// </summary>
        /// <param name="joiningColumn"></param>
        /// <returns></returns>
        public JoinExpression On(string joiningColumn)
        {
            this.JoiningColumn = joiningColumn;
            this.ExpressionType = JoinExpressionType.On;
            return this.Expression;
        }

        /// <summary>
        /// Specifies which columns to test for equality this table is joined.
        /// </summary>
        /// <remarks>Can be used instead of an ON clause in the JOIN operations that have an explicit join clause</remarks>
        /// <param name="columnNames"></param>
        /// <returns></returns>
        public SelectQueryBuilder Using(params string[] columnNames)
        {
            this.JoiningColumn = string.Join(",", columnNames);
            this.ExpressionType = JoinExpressionType.Using;
            return Query;
        }

        /// <summary>
        /// Selects all columns in the joining table
        /// </summary>
        public SelectQueryBuilder SelectAll() => Query?.SelectAll();

        /// <summary>
        /// Selects a specified column in the joining table.
        /// </summary>
        /// <param name="column">The Column name to select</param>
        public SelectQueryBuilder SelectColumn(string column, string alias = null, bool escape = true)
             => Query?.SelectColumn(column, alias, escape);

        /// <summary>
        /// Adds the specified column selectors in the joining table.
        /// </summary>
        /// <param name="columns">The column names to select</param>
        public SelectQueryBuilder Select(params string[] columns) => Query?.Select(columns);

        /// <summary>
        /// Adds the specified column selectors in the joining table.
        /// </summary>
        /// <param name="columns">The column names to select</param>
        public SelectQueryBuilder Select(IEnumerable<string> columns) => Query?.Select(columns);

        /// <summary>
        /// The count(X) function returns a count of the number of times that X is not NULL in a group. 
        /// The count(*) function (with no arguments) returns the total number of rows in the group.
        /// </summary>
        /// <param name="columnName">The column name to perform the aggregate on</param>
        /// <param name="alias">The return alias of the aggregate result, if any.</param>
        public SelectQueryBuilder SelectCount(string columnName = "*", string alias = null)
            => Query?.Aggregate(columnName, alias, AggregateFunction.Count);

        /// <summary>
        /// The count(distinct X) function returns the number of distinct values of column X instead of the 
        /// total number of non-null values in column X.
        /// </summary>
        /// <param name="columnName">The column name to perform the aggregate on</param>
        /// <param name="alias">The return alias of the aggregate result, if any.</param>
        public SelectQueryBuilder SelectDistinctCount(string columnName, string alias = null)
            => Query?.Aggregate(columnName, alias, AggregateFunction.DistinctCount);

        /// <summary>
        /// The avg() function returns the average value of all non-NULL X within a group
        /// </summary>
        /// <param name="columnName">The column name to perform the aggregate on</param>
        /// <param name="alias">The return alias of the aggregate result, if any.</param>
        public SelectQueryBuilder SelectAverage(string columnName, string alias = null)
            => Query?.Aggregate(columnName, alias, AggregateFunction.Average);

        /// <summary>
        /// The min() aggregate function returns the minimum non-NULL value of all values in the group
        /// </summary>
        /// <param name="columnName">The column name to perform the aggregate on</param>
        /// <param name="alias">The return alias of the aggregate result, if any.</param>
        public SelectQueryBuilder SelectMin(string columnName, string alias = null)
            => Query?.Aggregate(columnName, alias, AggregateFunction.Min);

        /// <summary>
        /// The max() aggregate function returns the maximum value of all values in the group
        /// </summary>
        /// <param name="columnName">The column name to perform the aggregate on</param>
        /// <param name="alias">The return alias of the aggregate result, if any.</param>
        public SelectQueryBuilder SelectMax(string columnName, string alias = null)
            => Query?.Aggregate(columnName, alias, AggregateFunction.Max);

        /// <summary>
        /// The sum() aggregate functions return sum of all non-NULL values in the group.
        /// </summary>
        /// <param name="columnName">The column name to perform the aggregate on</param>
        /// <param name="alias">The return alias of the aggregate result, if any.</param>
        public SelectQueryBuilder SelectSum(string columnName, string alias = null)
            => Query?.Aggregate(columnName, alias, AggregateFunction.Sum);
    }
}
