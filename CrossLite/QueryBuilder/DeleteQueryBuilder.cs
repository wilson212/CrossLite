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
    public class DeleteQueryBuilder
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
        /// Builds the query string with the current SQL Statement, and returns
        /// the querystring. This method is NOT Sql Injection safe!
        /// </summary>
        /// <returns></returns>
        public string BuildQuery() => BuildQuery(false) as String;

        /// <summary>
        /// Builds the query string with the current SQL Statement, and
        /// returns the DbCommand to be executed. All WHERE paramenters
        /// are propery escaped, making this command SQL Injection safe.
        /// </summary>
        /// <returns></returns>
        public SqliteCommand BuildCommand() => BuildQuery(true) as SqliteCommand;

        /// <summary>
        /// Builds the query string or DbCommand
        /// </summary>
        /// <param name="buildCommand"></param>
        /// <returns></returns>
        protected object BuildQuery(bool buildCommand)
        {
            // Make sure we have a valid DB driver
            if (buildCommand && Context == null)
                throw new Exception("Cannot build a command when the Context hasn't been specified. Call SetContext first.");

            // Make sure we have a table name
            if (String.IsNullOrWhiteSpace(Table))
                throw new Exception("Table to update was not set.");

            // Start Query
            var query = new StringBuilder($"DELETE FROM {Context.QuoteIdentifier(Table)}", 128);
            var parameters = new List<SqliteParameter>();

            // Append Where
            if (WhereStatement.HasClause)
                query.Append(" WHERE " + WhereStatement.BuildStatement(parameters));

            // Create Command
            SqliteCommand command = null;
            if (buildCommand)
            {
                command = Context.CreateCommand(query.ToString());
                command.Parameters.AddRange(parameters.ToArray());
            }

            // Return Result
            return (buildCommand) ? command as object : query.ToString();
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
    }
}
