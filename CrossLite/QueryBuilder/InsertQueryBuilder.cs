using System;
using System.Collections.Generic;
using Microsoft.Data.Sqlite;
using System.Text;

namespace CrossLite.QueryBuilder
{
    /// <summary>
    /// Provides an object interface that can properly put together an Insert Query string.
    /// </summary>
    /// <remarks>
    /// By using the BuildCommand() method, all parameters in the WHERE and HAVING statements will 
    /// be escaped by the underlaying SqliteCommand object, making the Execute*() methods SQL injection 
    /// safe.
    /// </remarks>
    public class InsertQueryBuilder : NonQueryBuilder, IDisposable
    {
        /// <summary>
        /// A list of FieldValuePairs
        /// </summary>
        protected Dictionary<string, object> Columns = new Dictionary<string, object>();

        /// <summary>
        /// Creates a new instance of InsertQueryBuilder with the provided SQLite connection.
        /// </summary>
        /// <param name="context">The SQLiteContext that will be used to build and query this SQL statement</param>
        public InsertQueryBuilder(SQLiteContext context) : base(context) { }

        /// <summary>
        /// Creates a new instance of InsertQueryBuilder with the provided SQLite connection.
        /// </summary>
        /// <param name="table">The table name we are inserting data into</param>
        /// <param name="context">The SQLiteContext that will be used to build and query this SQL statement</param>
        public InsertQueryBuilder(string table, SQLiteContext context) : base(context)
        {
            this.Table = table;
        }

        /// <summary>
        /// Sets a value for the specified column
        /// </summary>
        /// <param name="column">The column or attribute name</param>
        /// <param name="value">The value of the column</param>
        public InsertQueryBuilder Set(string column, object value)
        {
            if (Columns.ContainsKey(column))
                Columns[column] = value;
            else
                Columns.Add(column, value);

            return this;
        }

        #region Query

        /// <summary>
        /// Builds the query string with the current SQL Statement, and returns
        /// the querystring. This method is NOT Sql Injection safe!
        /// </summary>
        /// <returns></returns>
        public override string BuildQuery() => BuildQuery(false) as String;

        /// <summary>
        /// Builds the query string with the current SQL Statement, and
        /// returns the DbCommand to be executed. All WHERE paramenters
        /// are propery escaped, making this command SQL Injection safe.
        /// </summary>
        /// <returns></returns>
        public override SqliteCommand BuildCommand() => BuildQuery(true) as SqliteCommand;

        /// <summary>
        /// Builds the query string or DbCommand
        /// </summary>
        /// <param name="buildCommand"></param>
        /// <returns></returns>
        protected object BuildQuery(bool buildCommand)
        {
            // Make sure we have a valid DB driver
            if (buildCommand && Context == null)
                throw new Exception("Cannot build a command when the Db Drvier hasn't been specified. Call SetContext first.");

            // Make sure we have a table name
            if (String.IsNullOrWhiteSpace(Table))
                throw new Exception("Table to insert into was not set.");

            // Make sure we have at least 1 field to update
            if (Columns.Count == 0)
                throw new Exception("No column values specified to insert");

            // Start Query
            StringBuilder query = new StringBuilder($"INSERT INTO {Context.QuoteIdentifier(Table)} (", 256);
            StringBuilder values = new StringBuilder();
            List<SqliteParameter> parameters = new List<SqliteParameter>();
            bool first = true;

            // Add fields and values
            foreach (KeyValuePair<string, object> Item in Columns)
            {
                // Append comma
                if (!first)
                {
                    query.Append(", ");
                    values.Append(", ");
                }
                else
                    first = false;

                // If using a command, Convert values to Parameters
                if (buildCommand && Item.Value != null && Item.Value != DBNull.Value && !(Item.Value is SqlLiteral))
                {
                    // Create param for value
                    SqliteParameter Param = Context.CreateParameter();
                    Param.ParameterName = "@P" + parameters.Count;
                    Param.Value = Item.Value;

                    // Add Params to command
                    parameters.Add(Param);

                    // Append query's
                    query.Append(Context.QuoteIdentifier(Item.Key));
                    values.Append(Param.ParameterName);
                }
                else
                {
                    query.Append(Context.QuoteIdentifier(Item.Key));
                    values.Append(SqlExpression<WhereStatement>.FormatSQLValue(Item.Value));
                }
            }

            // Finish the query string, and return the proper object
            query.AppendFormat(") VALUES ({0})", values);

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
        /// Executes the built SQL statement on the Database connection that was passed
        /// in the contructor. All WHERE paramenters are propery escaped, 
        /// making this command SQL Injection safe.
        /// </summary>
        public override int Execute()
        {
            using (SqliteCommand command = BuildCommand())
                return command.ExecuteNonQuery();
        }

        public override void Dispose()
        {
            Columns = null;
            GC.SuppressFinalize(this);
        }

        #endregion Query
    }
}
