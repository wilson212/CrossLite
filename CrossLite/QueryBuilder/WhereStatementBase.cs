using System;
using Microsoft.Data.Sqlite;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CrossLite.QueryBuilder
{
    /// <summary>
    /// Abstract base class that contains all shared WHERE clause building logic.
    /// Both <see cref="WhereStatement"/> and <see cref="SelectWhereStatement"/> inherit from this,
    /// eliminating duplicated BuildStatement, And, Or, and clause management code.
    /// </summary>
    /// <typeparam name="TSelf">The concrete type inheriting from this base, enabling fluent method chaining
    /// that returns the correct derived type (CRTP pattern).</typeparam>
    public abstract class WhereStatementBase<TSelf> : IWhereStatement
        where TSelf : WhereStatementBase<TSelf>
    {
        /// <summary>
        /// Tracks the total number of expressions added across all clauses.
        /// Used for an O(1) <see cref="HasClause"/> check instead of LINQ enumeration.
        /// </summary>
        private int _expressionCount = 0;

        /// <summary>
        /// Gets the current active clause group in this statement.
        /// New expressions are appended to this clause until <see cref="CreateNewClause"/> is called.
        /// </summary>
        public WhereClause<TSelf> CurrentClause { get; protected set; }

        /// <summary>
        /// Gets the list of all WHERE clause groups in this statement.
        /// Multiple clauses are joined by the *opposite* of <see cref="InnerClauseOperator"/>
        /// (e.g., if inner is AND, clauses are joined by OR).
        /// </summary>
        public List<WhereClause<TSelf>> Clauses { get; protected set; }

        /// <summary>
        /// Gets or sets the logic operator used *within* each clause group.
        /// The opposite operator is used to separate clause groups from each other.
        /// Default is <see cref="LogicOperator.And"/> — should not be changed unless you know what you're doing.
        /// </summary>
        public LogicOperator InnerClauseOperator { get; set; } = LogicOperator.And;

        /// <summary>
        /// Indicates whether this WHERE statement contains any expressions at all.
        /// Uses the cached <c>_expressionCount</c> field for O(1) performance.
        /// </summary>
        public bool HasClause => _expressionCount > 0;

        /// <summary>
        /// Gets or sets the <see cref="IdentifierQuoteMode"/> controlling whether column names
        /// are quoted in the generated SQL (e.g., Always, KeywordsOnly, Never).
        /// </summary>
        public IdentifierQuoteMode AttributeQuoteMode { get; set; } = SQLiteContext.DefaultIdentifierQuoteMode;

        /// <summary>
        /// Gets or sets the <see cref="IdentifierQuoteKind"/> controlling the quote character style
        /// used for identifiers (e.g., DoubleQuotes, SquareBrackets, Backticks).
        /// </summary>
        public IdentifierQuoteKind AttributeQuoteKind { get; set; } = SQLiteContext.DefaultIdentifierQuoteKind;

        /// <summary>
        /// Initializes the base state with a single empty clause group.
        /// </summary>
        protected WhereStatementBase()
        {
            CurrentClause = new WhereClause<TSelf>();
            Clauses = new List<WhereClause<TSelf>> { CurrentClause };
        }

        /// <summary>
        /// Ends the current active clause group and starts a new one.
        /// Only creates a new clause if the current one actually contains expressions,
        /// preventing empty clause groups from accumulating.
        /// </summary>
        public void CreateNewClause()
        {
            if (CurrentClause.Expressions.Count > 0)
            {
                CurrentClause = new WhereClause<TSelf>();
                Clauses.Add(CurrentClause);
            }
        }

        /// <summary>
        /// Appends a new AND expression to the current clause group.
        /// If <see cref="InnerClauseOperator"/> is <see cref="LogicOperator.Or"/>, a new clause group
        /// is automatically created first (since AND expressions must be in separate groups when the
        /// inner operator is OR).
        /// </summary>
        /// <param name="fieldName">The column name to evaluate.</param>
        /// <param name="operator">The comparison operator (e.g., Equals, GreaterThan).</param>
        /// <param name="value">The value to compare against.</param>
        /// <param name="literal">If true, the value is treated as a raw SQL literal and will not be parameterized.</param>
        /// <returns>This instance for fluent method chaining.</returns>
        public TSelf And(string fieldName, Comparison @operator, object value, bool literal = false)
        {
            // When inner operator is OR, each AND expression needs its own clause group
            if (InnerClauseOperator == LogicOperator.Or && HasClause)
                CreateNewClause();

            SqlExpression<TSelf> expression;

            // Wrap in SqlLiteral if the caller wants raw SQL injection (e.g., subqueries, functions)
            if (literal)
                expression = new SqlExpression<TSelf>(fieldName, @operator, new SqlLiteral(value?.ToString()), (TSelf)this);
            else
                expression = new SqlExpression<TSelf>(fieldName, @operator, value, (TSelf)this);

            CurrentClause.Expressions.Add(expression);
            _expressionCount++;
            return (TSelf)this;
        }

        /// <summary>
        /// Begins a new AND expression using the fluent <see cref="SqlExpression{T}"/> builder.
        /// The caller completes the expression by chaining a comparison method on the returned object
        /// (e.g., <c>.And("name").Equals("Steve")</c>).
        /// </summary>
        /// <param name="fieldName">The column name to evaluate.</param>
        /// <returns>An <see cref="SqlExpression{TSelf}"/> that the caller completes with a comparison.</returns>
        public SqlExpression<TSelf> And(string fieldName)
        {
            if (InnerClauseOperator == LogicOperator.Or && HasClause)
                CreateNewClause();

            var expression = new SqlExpression<TSelf>(fieldName, (TSelf)this);
            CurrentClause.Expressions.Add(expression);
            _expressionCount++;

            return expression;
        }

        /// <summary>
        /// Appends a new OR expression to the current clause group.
        /// If <see cref="InnerClauseOperator"/> is <see cref="LogicOperator.And"/>, a new clause group
        /// is automatically created first (since OR expressions must be in separate groups when the
        /// inner operator is AND).
        /// </summary>
        /// <param name="fieldName">The column name to evaluate.</param>
        /// <param name="operator">The comparison operator.</param>
        /// <param name="value">The value to compare against.</param>
        /// <param name="literal">If true, the value is treated as a raw SQL literal.</param>
        /// <returns>This instance for fluent method chaining.</returns>
        public TSelf Or(string fieldName, Comparison @operator, object value, bool literal = false)
        {
            if (InnerClauseOperator == LogicOperator.And && HasClause)
                CreateNewClause();

            SqlExpression<TSelf> expression;

            if (literal)
                expression = new SqlExpression<TSelf>(fieldName, @operator, new SqlLiteral(value.ToString()), (TSelf)this);
            else
                expression = new SqlExpression<TSelf>(fieldName, @operator, value, (TSelf)this);

            CurrentClause.Expressions.Add(expression);
            _expressionCount++;
            return (TSelf)this;
        }

        /// <summary>
        /// Begins a new OR expression using the fluent <see cref="SqlExpression{T}"/> builder.
        /// </summary>
        /// <param name="fieldName">The column name to evaluate.</param>
        /// <returns>An <see cref="SqlExpression{TSelf}"/> that the caller completes with a comparison.</returns>
        public SqlExpression<TSelf> Or(string fieldName)
        {
            if (InnerClauseOperator == LogicOperator.And && HasClause)
                CreateNewClause();

            var expression = new SqlExpression<TSelf>(fieldName, (TSelf)this);
            CurrentClause.Expressions.Add(expression);
            _expressionCount++;

            return expression;
        }
        
        /// <summary>
        /// Merges all clauses and expressions from the specified <see cref="IWhereStatement"/> source
        /// into this instance. Expressions are replayed using their public properties (Identifier,
        /// ComparisonOperator, Value), so the generic TWhere type difference doesn't matter.
        /// Clause group boundaries from the source are preserved.
        /// </summary>
        /// <typeparam name="TOther">The concrete type of the source WHERE statement.</typeparam>
        /// <param name="source">The WHERE statement to merge from. Must not be null.</param>
        /// <returns>This instance for fluent chaining.</returns>
        public TSelf MergeFrom<TOther>(WhereStatementBase<TOther> source)
            where TOther : WhereStatementBase<TOther>
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));

            if (!source.HasClause)
                return (TSelf)this;

            foreach (var clause in source.Clauses)
            {
                if (clause.Expressions.Count == 0)
                    continue;

                // If we already have expressions, start a new clause group
                // to preserve the source's clause boundary separation
                if (HasClause)
                    CreateNewClause();

                foreach (var expr in clause.Expressions)
                {
                    // Replay each expression into this statement using And().
                    // The value may be a SqlLiteral — And() handles that via the literal parameter.
                    bool isLiteral = expr.Value is SqlLiteral;
                    And(expr.Identifier, expr.ComparisonOperator, 
                        isLiteral ? ((SqlLiteral)expr.Value).Value : expr.Value, 
                        isLiteral);
                }
            }

            return (TSelf)this;
        }

        /// <summary>
        /// Builds the WHERE clause SQL string without parameterization.
        /// Values are inlined directly into the SQL text. Useful for debugging/logging only.
        /// </summary>
        /// <returns>The WHERE clause as a raw SQL string.</returns>
        public string BuildStatement() => BuildStatement(null);

        /// <summary>
        /// Builds the WHERE clause SQL string with parameterized values.
        /// Creates a new parameter list and returns it via the out parameter.
        /// </summary>
        /// <param name="parameters">Output: the list of <see cref="SqliteParameter"/> objects generated.</param>
        /// <returns>The WHERE clause as a parameterized SQL string.</returns>
        public string BuildStatement(out List<SqliteParameter> parameters)
        {
            parameters = new List<SqliteParameter>();
            return BuildStatement(parameters);
        }

        /// <summary>
        /// Builds the full WHERE clause SQL string, appending any generated parameters to the provided list.
        /// 
        /// Logic:
        /// 1. Filters out empty clause groups (non-mutating — uses .Where() instead of RemoveAll()).
        /// 2. Wraps multi-expression clause groups in parentheses for correct operator precedence.
        /// 3. Joins expressions within a group using <see cref="InnerClauseOperator"/>.
        /// 4. Joins clause groups using the *opposite* operator (AND↔OR).
        /// </summary>
        /// <param name="parameters">An existing list to append <see cref="SqliteParameter"/> objects to,
        /// or null to inline values directly (non-parameterized mode).</param>
        /// <returns>The complete WHERE clause as a SQL string (without the "WHERE" keyword).</returns>
        public string BuildStatement(List<SqliteParameter> parameters)
        {
            StringBuilder builder = new StringBuilder();
            int counter = 0;

            // Pre-count active clauses without allocating (no LINQ, no ToList)
            int activeCount = 0;
            for (int i = 0; i < Clauses.Count; i++)
            {
                if (Clauses[i].Expressions.Count > 0)
                    activeCount++;
            }

            // Loop through each clause group, skipping empty ones inline
            foreach (var clause in Clauses)
            {
                if (clause.Expressions.Count == 0)
                    continue;

                int subCounter = 0;

                // Open parenthesis for multi-expression groups when there are multiple active clause groups
                builder.AppendIf(clause.Expressions.Count > 1 && activeCount > 1, '(');

                // Append each expression within this clause group
                foreach (var expression in clause.Expressions)
                {
                    // Separate expressions within a group using the InnerClauseOperator
                    builder.AppendIf(++subCounter > 1, (InnerClauseOperator == LogicOperator.Or) ? " OR " : " AND ");

                    // Use parameterized form if a parameter list was provided, otherwise inline values
                    builder.Append((parameters == null) ? expression.ToString() : expression.BuildExpression(parameters));
                }

                // Close parenthesis
                builder.AppendIf(clause.Expressions.Count > 1 && activeCount > 1, ')');

                // Separate clause groups using the OPPOSITE of InnerClauseOperator
                builder.AppendIf(++counter < activeCount, (InnerClauseOperator == LogicOperator.And) ? " OR " : " AND ");
            }

            return builder.ToString();
        }

        /// <summary>
        /// Resets the WHERE statement to its initial empty state.
        /// Clears all clause groups and the expression counter.
        /// </summary>
        public void Reset()
        {
            CurrentClause = new WhereClause<TSelf>();
            Clauses.Clear();
            Clauses.Add(CurrentClause);
            _expressionCount = 0;
        }
    }
}