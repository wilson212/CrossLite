using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CrossLite.QueryBuilder
{
    /// <summary>
    /// Represents a WHERE statement inside an SQL query
    /// </summary>
    public class WhereStatement : IWhereStatement
    {
        /// <summary>
        /// Gets the current Clause group in this Statement
        /// </summary>
        public WhereClause<WhereStatement> CurrentClause { get; protected set; }

        /// <summary>
        /// Gets a list of all Where Clauses in this statement
        /// </summary>
        public List<WhereClause<WhereStatement>> Clauses { get; protected set; }

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
        /// Creates a new instance of <see cref="WhereStatement"/>
        /// </summary>
        public WhereStatement()
        {
            CurrentClause = new WhereClause<WhereStatement>();
            Clauses = new List<WhereClause<WhereStatement>>() { CurrentClause };
        }

        /// <summary>
        /// Creates a new instance of <see cref="WhereStatement"/> using the quoting settings
        /// from the supplied SQLiteContext
        /// </summary>
        public WhereStatement(SQLiteContext context) : this()
        {
            AttributeQuoteMode = context.IdentifierQuoteMode;
            AttributeQuoteKind = context.IdentifierQuoteKind;
        }

        /// <summary>
        /// Ends the current active clause, and creates a new one.
        /// </summary>
        public void CreateNewClause()
        {
            // Create new Group
            if (CurrentClause.Expressions.Count > 0)
            {
                CurrentClause = new WhereClause<WhereStatement>();
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
        public WhereStatement And(string fieldName, Comparison @operator, object value, bool literal = false)
        {
            // Create new Group
            if (InnerClauseOperator == LogicOperator.Or && HasClause)
                this.CreateNewClause();

            SqlExpression<WhereStatement> expression;
            // Convert value
            if (literal)
                expression = new SqlExpression<WhereStatement>(fieldName, @operator, new SqlLiteral(value.ToString()), this);
            else
                expression = new SqlExpression<WhereStatement>(fieldName, @operator, value, this);

            // Allow chaining
            CurrentClause.Expressions.Add(expression);
            return this;
        }

        /// <summary>
        /// Appends a new expression evaluation to the current Statement
        /// </summary>
        /// <param name="fieldName">The attribute name we are performing the evaluation on</param>
        /// <returns>Returns this object to allow method chaining</returns>
        public SqlExpression<WhereStatement> And(string fieldName)
        {
            // Create new Group
            if (InnerClauseOperator == LogicOperator.Or && HasClause)
                this.CreateNewClause();

            // Create Expression
            var expression = new SqlExpression<WhereStatement>(fieldName, this);
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
        public WhereStatement Or(string fieldName, Comparison @operator, object value, bool literal = false)
        {
            // Create new Group
            if (InnerClauseOperator == LogicOperator.And && HasClause)
                this.CreateNewClause();

            // Create Expression
            SqlExpression<WhereStatement> expression;

            // Convert value
            if (literal)
                expression = new SqlExpression<WhereStatement>(fieldName, @operator, new SqlLiteral(value.ToString()), this);
            else
                expression = new SqlExpression<WhereStatement>(fieldName, @operator, value, this);

            // Allow chaining
            CurrentClause.Expressions.Add(expression);
            return this;
        }

        /// <summary>
        /// Appends a new expression evaluation to the current Statement
        /// </summary>
        /// <param name="fieldName">The attribute name we are performing the evaluation on</param>
        /// <returns>Returns this object to allow method chaining</returns>
        public SqlExpression<WhereStatement> Or(string fieldName)
        {
            // Create new Group
            if (InnerClauseOperator == LogicOperator.And && HasClause)
                this.CreateNewClause();

            // Create Expression
            var expression = new SqlExpression<WhereStatement>(fieldName, this);
            CurrentClause.Expressions.Add(expression);

            return expression;
        }

        /// <summary>
        /// Resets the current WHERE expressions
        /// </summary>
        public void Reset()
        {
            CurrentClause.Expressions.Clear();
            Clauses.Clear();
        }

        /// <summary>
        /// Builds the current set of Clauses and returns the output as a string.
        /// </summary>
        /// <returns></returns>
        public string BuildStatement() =>  BuildStatement(null);

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
                    builder.Append( (parameters == null) ? expression.ToString() : expression.BuildExpression(parameters) );
                }

                // Close Parent Clause grouping
                builder.AppendIf(clause.Expressions.Count > 1 && Clauses.Count > 0, ')');

                // If we have more clauses, append operator
                builder.AppendIf(++counter < Clauses.Count, (InnerClauseOperator == LogicOperator.And) ? " OR " : " AND ");
            }

            return builder.ToString();
        }
    }
}
