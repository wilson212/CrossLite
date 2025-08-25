using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CrossLite.QueryBuilder
{
    /// <summary>
    /// Represents a WHERE statement inside an SQL SELECT query
    /// </summary>
    public class SelectWhereStatement : IWhereStatement
    {
        /// <summary>
        /// Gets the current Clause group in this Statement
        /// </summary>
        public WhereClause<SelectWhereStatement> CurrentClause { get; protected set; }

        /// <summary>
        /// Gets a list of all Where Clauses in this statement
        /// </summary>
        public List<WhereClause<SelectWhereStatement>> Clauses { get; protected set; }

        /// <summary>
        /// Gets or Sets the Logic Operator to use in Clauses. The opposite operator
        /// will be used to seperate clauses. The Default is And and should not be changed
        /// unless you know what you are doing!
        /// </summary>
        public LogicOperator InnerClauseOperator { get; set; } = LogicOperator.And;

        /// <summary>
        /// Indicates whether this WhereStatement has any clauses, or if its empty.
        /// </summary>
        public bool HasClause => Clauses.Any(x => x.Expressions.Count > 0);

        /// <summary>
        /// Gets or sets the <see cref="CrossLite.IdentifierQuoteMode"/> this instance will use for queries
        /// </summary>
        public IdentifierQuoteMode AttributeQuoteMode { get; set; } = SQLiteContext.DefaultIdentifierQuoteMode;

        /// <summary>
        /// Gets or sets the <see cref="CrossLite.IdentifierQuoteKind"/> this instance will use for queries
        /// </summary>
        public IdentifierQuoteKind AttributeQuoteKind { get; set; } = SQLiteContext.DefaultIdentifierQuoteKind;

        /// <summary>
        /// The query builder this statement is attached to if one exists
        /// </summary>
        internal SelectQueryBuilder Query { get; set; }

        /// <summary>
        /// Creates a new instance of <see cref="WhereStatement"/>
        /// </summary>
        public SelectWhereStatement()
        {
            CurrentClause = new WhereClause<SelectWhereStatement>();
            Clauses = new List<WhereClause<SelectWhereStatement>>() { CurrentClause };
        }

        /// <summary>
        /// Creates a new instance of <see cref="SelectWhereStatement"/> using the quoting settings
        /// from the supplied SQLiteContext
        /// </summary>
        public SelectWhereStatement(SQLiteContext context) : this()
        {
            AttributeQuoteMode = context.IdentifierQuoteMode;
            AttributeQuoteKind = context.IdentifierQuoteKind;
        }

        /// <summary>
        /// Creates a new instance of <see cref="SelectWhereStatement"/> using the quoting settings
        /// from the supplied SelectQueryBuilder
        /// </summary>
        /// <param name="query"></param>
        public SelectWhereStatement(SelectQueryBuilder query) : this()
        {
            this.Query = query;
            AttributeQuoteMode = query.Context.IdentifierQuoteMode;
            AttributeQuoteKind = query.Context.IdentifierQuoteKind;
        }

        /// <summary>
        /// Ends the current active clause, and creates a new one.
        /// </summary>
        public void CreateNewClause()
        {
            // Create new Group
            if (CurrentClause.Expressions.Count > 0)
            {
                CurrentClause = new WhereClause<SelectWhereStatement>();
                Clauses.Add(CurrentClause);
            }
        }

        /// <summary>
        /// Appends a new expression evaluation to the current Statement
        /// </summary>
        /// <param name="fieldName">The attribute name we are performing the evaluation on</param>
        /// <param name="operator">The Comparison we are performing on this attribute</param>
        /// <param name="value">The value at which we require in this evaluation</param>
        /// <param name="literal">If true, than the value will not be escaped and quoted during the query</param>
        /// <returns>Returns this object to allow method chaining</returns>
        public SelectWhereStatement And(string fieldName, Comparison @operator, object value, bool literal = false)
        {
            // Create new Group
            if (InnerClauseOperator == LogicOperator.Or && HasClause)
                this.CreateNewClause();

            SqlExpression<SelectWhereStatement> expression;
            // Convert value
            if (literal)
                expression = new SqlExpression<SelectWhereStatement>(fieldName, @operator, new SqlLiteral(value?.ToString() ?? null), this);
            else
                expression = new SqlExpression<SelectWhereStatement>(fieldName, @operator, value, this);

            // Allow chaining
            CurrentClause.Expressions.Add(expression);
            return this;
        }

        /// <summary>
        /// Appends a new expression evaluation to the current Statement
        /// </summary>
        /// <param name="fieldName">The attribute name we are performing the evaluation on</param>
        /// <returns>Returns this object to allow method chaining</returns>
        public SqlExpression<SelectWhereStatement> And(string fieldName)
        {
            // Create new Group
            if (InnerClauseOperator == LogicOperator.Or && HasClause)
                this.CreateNewClause();

            // Create Expression
            var expression = new SqlExpression<SelectWhereStatement>(fieldName, this);
            CurrentClause.Expressions.Add(expression);

            return expression;
        }

        /// <summary>
        /// Appends a new expression evaluation to the current Statement
        /// </summary>
        /// <param name="fieldName">The attribute name we are performing the evaluation on</param>
        /// <param name="operator">The Comparison we are performing on this attribute</param>
        /// <param name="value">The value at which we require in this evaluation</param>
        /// <param name="literal">If true, than the value will not be escaped and quoted during the query</param>
        /// <returns>Returns this object to allow method chaining</returns>
        public SelectWhereStatement Or(string fieldName, Comparison @operator, object value, bool literal = false)
        {
            // Create new Group
            if (InnerClauseOperator == LogicOperator.And && HasClause)
                this.CreateNewClause();

            // Create Expression
            SqlExpression<SelectWhereStatement> expression;

            // Convert value
            if (literal)
                expression = new SqlExpression<SelectWhereStatement>(fieldName, @operator, new SqlLiteral(value.ToString()), this);
            else
                expression = new SqlExpression<SelectWhereStatement>(fieldName, @operator, value, this);

            // Allow chaining
            CurrentClause.Expressions.Add(expression);
            return this;
        }

        /// <summary>
        /// Appends a new expression evaluation to the current Statement
        /// </summary>
        /// <param name="fieldName">The attribute name we are performing the evaluation on</param>
        /// <returns>Returns this object to allow method chaining</returns>
        public SqlExpression<SelectWhereStatement> Or(string fieldName)
        {
            // Create new Group
            if (InnerClauseOperator == LogicOperator.And && HasClause)
                this.CreateNewClause();

            // Create Expression
            var expression = new SqlExpression<SelectWhereStatement>(fieldName, this);
            CurrentClause.Expressions.Add(expression);

            return expression;
        }

        /// <summary>
        /// Builds the current set of Clauses and returns the output as a string.
        /// </summary>
        /// <returns></returns>
        public string BuildStatement() => BuildStatement(null);

        /// <summary>
        /// Builds the current set of Clauses and returns the output as a string.
        /// </summary>
        /// <param name="parameters">A list of current query parameters</param>
        /// <returns></returns>
        public string BuildStatement(out List<SqliteParameter> parameters)
        {
            parameters = new List<SqliteParameter>();
            return BuildStatement(parameters);
        }

        /// <summary>
        /// Builds the current set of Clauses and returns the full statement as a string.
        /// </summary>
        /// <param name="parameters">A list that will be filled with the statements parameters</param>
        /// <returns></returns>
        public string BuildStatement(List<SqliteParameter> parameters)
        {
            StringBuilder builder = new StringBuilder();
            int paramsCounter = parameters?.Count ?? 0;
            int counter = 0;

            // Remove empty expressions
            Clauses.RemoveAll(x => x.Expressions.Count == 0);

            // Loop through each Where clause (wrapped in parenthesis)
            foreach (var clause in Clauses)
            {
                // Open Parent Clause grouping if we have more then 1 SubClause
                int subCounter = 0;
                builder.AppendIf(clause.Expressions.Count > 1 && Clauses.Count > 0, '(');

                // Append each Sub Clause
                foreach (var expression in clause.Expressions)
                {
                    // If we have more sub clauses in this group, append operator
                    builder.AppendIf(++subCounter > 1, (InnerClauseOperator == LogicOperator.Or) ? " OR " : " AND ");
                    builder.Append((parameters == null) ? expression.ToString() : expression.BuildExpression(parameters));
                }

                // Close Parent Clause grouping
                builder.AppendIf(clause.Expressions.Count > 1 && Clauses.Count > 0, ')');

                // If we have more clauses, append operator
                builder.AppendIf(++counter < Clauses.Count, (InnerClauseOperator == LogicOperator.And) ? " OR " : " AND ");
            }

            return builder.ToString();
        }

        #region Re-Chaining Methods

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

        /// <summary>
        /// Bypasses the specified amount of records (offset) in the result set.
        /// </summary>
        /// <param name="">The offset in the query</param>
        /// <returns>Returns the <see cref="SelectQueryBuilder"/> this instance is attached to, or null</returns>
        public SelectQueryBuilder Skip(int count) => Query?.Skip(count);

        /// <summary>
        /// Specifies the maximum number of records to return in the query (limit)
        /// </summary>
        /// <param name="">The number of records to grab from the result set</param>
        /// <returns>Returns the <see cref="SelectQueryBuilder"/> this instance is attached to, or null</returns>
        public SelectQueryBuilder Take(int count) => Query?.Take(count);

        /// <summary>
        /// Adds an OrderBy clause to the current query object
        /// </summary>
        /// <param name="clause"></param>
        /// /// <returns>Returns the <see cref="SelectQueryBuilder"/> this instance is attached to, or null</returns>
        public SelectQueryBuilder OrderBy(OrderByClause clause) => Query?.OrderBy(clause);

        /// <summary>
        /// Creates and adds a new Oderby clause to the current query object
        /// </summary>
        /// <param name="fieldName"></param>
        /// <param name="order"></param>
        public SelectQueryBuilder OrderBy(string fieldName, Sorting order) => Query?.OrderBy(fieldName, order);

        /// <summary>
        /// Creates and adds a new Groupby clause to the current query object
        /// </summary>
        /// <param name="fieldName"></param>
        public SelectQueryBuilder GroupBy(string fieldName) => Query?.GroupBy(fieldName);

        #endregion
    }
}