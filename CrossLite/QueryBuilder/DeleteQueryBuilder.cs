using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.Text;

namespace CrossLite.QueryBuilder
{
    /// <summary>
    /// Provides an object interface that can properly put together a Delete Query string.
    /// </summary>
    /// <remarks>
    /// By using the BuildCommand() method, all parameters in the WHERE statement will be 
    /// escaped by the underlaying SQLiteCommand object, making the Execute() method SQL injection safe.
    /// </remarks>
    public class DeleteQueryBuilder : IDisposable
    {
        /// <summary>
        /// The SQLiteContext attached to this builder
        /// </summary>
        public SQLiteContext Context { get; protected set; }

        /// <summary>
        /// Gets or Sets the selected table for this query
        /// </summary>
        public string Table { get; set; }

        /// <summary>
        /// The Where statement for this query
        /// </summary>
        public WhereStatement WhereStatement { get; set; }

        /// <summary>
        /// Creates a new instance of <see cref="DeleteQueryBuilder"/> using the
        /// specified <see cref="SQLiteContext"/>
        /// </summary>
        /// <param name="context"></param>
        public DeleteQueryBuilder(SQLiteContext context)
        {
            this.Context = context;
            this.WhereStatement = new WhereStatement(context);
        }

        /// <summary>
        /// Sets the table name to be used in this SQL Statement
        /// </summary>
        /// <param name="table">The table name</param>
        public DeleteQueryBuilder From(string table)
        {
            // Ensure we are not null
            if (String.IsNullOrWhiteSpace(table))
                throw new ArgumentNullException("Tablename cannot be null or empty!", "table");

            Table = table;
            return this;
        }

        /// <summary>
        /// Creates a where clause to add to the query's where statement
        /// </summary>
        /// <param name="column">The column name</param>
        /// <param name="operator">The Comaparison Operator to use</param>
        /// <param name="value">The value, for the column name and comparison operator</param>
        /// <returns></returns>
        public WhereStatement Where(string column, Comparison @operator, object value)
        {
            if (WhereStatement.InnerClauseOperator == LogicOperator.And)
                return WhereStatement.And(column, @operator, value);
            else
                return WhereStatement.Or(column, @operator, value);
        }

        /// <summary>
        /// Creates a where clause to add to the query's where statement
        /// </summary>
        /// <param name="column">The column name</param>
        /// <returns></returns>
        public SqlExpression<WhereStatement> Where(string column)
        {
            if (WhereStatement.InnerClauseOperator == LogicOperator.And)
                return WhereStatement.And(column);
            else
                return WhereStatement.Or(column);
        }

        /// <summary>
        /// Constructs a DELETE SQL query string based on the specified table and WHERE conditions.
        /// </summary>
        /// <returns>
        /// A string representation of a DELETE SQL query.
        /// </returns>
        public string BuildQuery() => BuildSql(null);

        /// <summary>
        /// Builds a <see cref="SqliteCommand"/> representing the DELETE SQL query with all necessary parameters.
        /// </summary>
        /// <returns>Returns a <see cref="SqliteCommand"/> configured with the SQL query and associated parameters for execution.</returns>
        public SqliteCommand BuildCommand()
        {
            var parameters = new List<SqliteParameter>();
            string sql = BuildSql(parameters);
            var command = Context.CreateCommand(sql);
            
            foreach (var p in parameters)
                command.Parameters.Add(p);
            
            return command;
        }

        /// <summary>
        /// Constructs a SQL DELETE statement based on the specified table and WHERE clause.
        /// Optionally adds parameterized values for the WHERE clause if a parameter collection is provided.
        /// </summary>
        /// <param name="parameters">A collection of <see cref="SqliteParameter"/> objects to hold
        /// the parameterized values used in the WHERE clause. Can be null if no parameters are required.</param>
        /// <returns>A string containing the SQL DELETE statement.</returns>
        /// <exception cref="Exception">Thrown if the <see cref="SQLiteContext"/> is not set
        /// or if the table name is not specified.</exception>
        private string BuildSql(List<SqliteParameter> parameters)
        {
            if (Context == null)
                throw new Exception(
                    "Cannot build a command when the Context hasn't been specified. Call SetContext first.");

            if (String.IsNullOrWhiteSpace(Table))
                throw new Exception("Table to update was not set.");

            var query = new StringBuilder("DELETE FROM ", 128);
            query.Append(Context.QuoteIdentifier(Table));

            if (WhereStatement.HasClause)
            {
                query.Append(" WHERE ");
                query.Append(parameters != null
                    ? WhereStatement.BuildStatement(parameters)
                    : WhereStatement.BuildStatement());
            }

            return query.ToString();
        }

        /// <summary>
        /// Executes the command against the database. The database driver must be set!
        /// </summary>
        /// <returns></returns>
        public int Execute()
        {
            using (SqliteCommand command = BuildCommand())
                return command.ExecuteNonQuery();
        }

        public void Dispose() { }
    }
}
