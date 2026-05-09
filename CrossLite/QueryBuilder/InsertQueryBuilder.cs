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
        /// Builds and returns the SQL query string for the current insert operation.
        /// </summary>
        /// <returns>The constructed SQL query string.</returns>
        public override string BuildQuery() => BuildSql(null);

        /// <summary>
        /// Builds a complete SQL command using the current state of the query and its parameters.
        /// </summary>
        /// <returns>A <see cref="SqliteCommand"/> object that represents the SQL command and its associated parameters.</returns>
        public override SqliteCommand BuildCommand()
        {
            var parameters = new List<SqliteParameter>();
            string sql = BuildSql(parameters);
            var command = Context.CreateCommand(sql);
            
            foreach (var p in parameters)
                command.Parameters.Add(p);
            
            return command;
        }

        /// <summary>
        /// Constructs the SQL INSERT query string based on the specified table and column values.
        /// </summary>
        /// <param name="parameters">
        /// A list of <see cref="SqliteParameter"/> objects that will hold the parameterized values for the query.
        /// If null, the method builds a non-parameterized query.
        /// </param>
        /// <returns>
        /// A string containing the constructed SQL INSERT query.
        /// </returns>
        /// <exception cref="Exception">
        /// Thrown if the database context is not set, the table name is not specified, or no columns and values are provided for the insert operation.
        /// </exception>
        private string BuildSql(List<SqliteParameter> parameters)
        {
            if (Context == null)
                throw new Exception(
                    "Cannot build a command when the Db Driver hasn't been specified. Call SetContext first.");

            if (String.IsNullOrWhiteSpace(Table))
                throw new Exception("Table to insert into was not set.");

            if (Columns.Count == 0)
                throw new Exception("No column values specified to insert");

            StringBuilder query = new StringBuilder("INSERT INTO ", 256);
            query.Append(Context.QuoteIdentifier(Table));
            query.Append(" (");

            StringBuilder values = new StringBuilder();
            bool first = true;

            foreach (var Item in Columns)
            {
                if (!first)
                {
                    query.Append(", ");
                    values.Append(", ");
                }
                else
                    first = false;

                if (parameters != null && Item.Value != null && Item.Value != DBNull.Value && !(Item.Value is SqlLiteral))
                {
                    var Param = Context.CreateParameter();
                    Param.ParameterName = "@P" + parameters.Count;
                    Param.Value = Item.Value;
                    parameters.Add(Param);

                    query.Append(Context.QuoteIdentifier(Item.Key));
                    values.Append(Param.ParameterName);
                }
                else
                {
                    query.Append(Context.QuoteIdentifier(Item.Key));
                    values.Append(SqlExpression<WhereStatement>.FormatSQLValue(Item.Value));
                }
            }

            query.Append(") VALUES (");
            query.Append(values);
            query.Append(')');

            return query.ToString();
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

        public override void Dispose() { }

        #endregion Query
    }
}
