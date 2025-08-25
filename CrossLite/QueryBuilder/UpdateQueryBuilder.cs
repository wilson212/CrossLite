using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.Text;

namespace CrossLite.QueryBuilder
{
    /// <summary>
    /// Provides an object interface that can properly put together an Update Query string.
    /// </summary>
    /// <remarks>
    /// By using the BuildCommand() method, all parameters in the WHERE statement will be 
    /// escaped by the underlaying SQLiteCommand object, making the Execute() method SQL injection safe.
    /// </remarks>
    public class UpdateQueryBuilder : NonQueryBuilder, IDisposable
    {
        /// <summary>
        /// A list of FieldValuePairs
        /// </summary>
        protected Dictionary<string, ColumnValuePair> Columns = new Dictionary<string, ColumnValuePair>();

        /// <summary>
        /// The Where statement for this query
        /// </summary>
        public WhereStatement WhereStatement { get; set; }

        /// <summary>
        /// Creates a new instance of UpdateQueryBuilder with the provided SQLite connection.
        /// </summary>
        /// <param name="context">The SQLiteContext that will be used to build and query this SQL statement</param>
        public UpdateQueryBuilder(SQLiteContext context) : base(context)
        {
            this.Context = context;
            this.WhereStatement = new WhereStatement(context);
        }

        /// <summary>
        /// Creates a new instance of UpdateQueryBuilder with the provided SQLite connection.
        /// </summary>
        /// <param name="table">The table name we are updating data in</param>
        /// <param name="context">The SQLiteContext that will be used to build and query this SQL statement</param>
        public UpdateQueryBuilder(string table, SQLiteContext context) : base(context)
        {
            this.Table = table;
            this.Context = context;
            this.WhereStatement = new WhereStatement(context);
        }

        /// <summary>
        /// Sets a value for the specified column
        /// </summary>
        /// <param name="column">The column or attribute name</param>
        /// <param name="value">The new value to update</param>
        public UpdateQueryBuilder Set(string column, object value) => Set(column, value, ValueMode.Set);

        /// <summary>
        /// Sets a value for the specified column
        /// </summary>
        /// <param name="column">The column or attribute name</param>
        /// <param name="value">The new value to update</param>
        /// <param name="mode">Sets how the update value will be applied to the existing field value</param>
        internal UpdateQueryBuilder Set(string column, object value, ValueMode mode)
        {
            // Check parameter
            if (String.IsNullOrWhiteSpace(column))
                throw new ArgumentNullException("column");

            // Add column to list
            if (Columns.ContainsKey(column))
                Columns[column] = new ColumnValuePair(column, value, mode);
            else
                Columns.Add(column, new ColumnValuePair(column, value, mode));

            // Return this instance for chaining
            return this;
        }

        /// <summary>
        /// Increments the current value in the database on the specified column by the specified value.
        /// </summary>
        /// <typeparam name="T">A numeric type to increment the value by</typeparam>
        /// <param name="column">The column or attribute name</param>
        /// <param name="value">The value to increment by</param>
        public UpdateQueryBuilder Increment<T>(string column, T value) where T : struct
            => Set(column, value, ValueMode.Add);

        /// <summary>
        /// Decrements the current value in the database on the specified column by the specified value.
        /// </summary>
        /// <typeparam name="T">A numeric type to decrement the value by</typeparam>
        /// <param name="column">The column or attribute name</param>
        /// <param name="value">The value to decrement by</param>
        public UpdateQueryBuilder Decrement<T>(string column, T value) where T : struct
            => Set(column, value, ValueMode.Subtract);

        /// <summary>
        /// Divides the current value in the database on the specified column by the specified value.
        /// </summary>
        /// <typeparam name="T">A numeric type to divide the value by</typeparam>
        /// <param name="column">The column or attribute name</param>
        /// <param name="value">The value to divide by</param>
        public UpdateQueryBuilder Divide<T>(string column, T value) where T : struct
            => Set(column, value, ValueMode.Divide);

        /// <summary>
        /// Multiplies the current value in the database on the specified column by the specified value.
        /// </summary>
        /// <typeparam name="T">A numeric type to multiply the value by</typeparam>
        /// <param name="column">The column or attribute name</param>
        /// <param name="value">The value to multiply by</param>
        public UpdateQueryBuilder Multiply<T>(string column, T value) where T : struct
            => Set(column, value, ValueMode.Multiply);

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
        protected object BuildQuery(bool buildCommand, bool useNamedParams = false)
        {
            // Make sure we have a valid DB driver
            if (buildCommand && Context == null)
                throw new Exception("Cannot build a command when the Context hasn't been specified. Call SetContext first.");

            // Make sure we have a table name
            if (String.IsNullOrWhiteSpace(Table))
                throw new Exception("Table to update was not set.");

            // Make sure we have at least 1 field to update
            if (Columns.Count == 0)
                throw new Exception("No column values to update");

            // Start Query
            var query = new StringBuilder($"UPDATE {Context.QuoteIdentifier(Table)} SET ", 256);
            var parameters = new List<SqliteParameter>();

            // Add Fields
            bool first = true;
            foreach (var column in Columns)
            {
                // Append comma
                if (!first) query.Append(", ");
                else first = false;

                // If using a command, Convert values to Parameters
                if (buildCommand && column.Value.Value != null && column.Value.Value != DBNull.Value && !(column.Value.Value is SqlLiteral))
                {
                    // Create param for value
                    var param = Context.CreateParameter();
                    param.ParameterName = "@P" + parameters.Count;
                    param.Value = column.Value.Value;

                    // Add Params to command
                    parameters.Add(param);

                    // Append Query
                    if (column.Value.Mode == ValueMode.Set)
                        query.AppendFormat("{0} = {1}", Context.QuoteIdentifier(column.Key), param.ParameterName);
                    else
                        query.AppendFormat("{0} = {0} {1} {2}", Context.QuoteIdentifier(column.Key), GetSign(column.Value.Mode), param.ParameterName);
                }
                else
                {
                    if (column.Value.Mode == ValueMode.Set)
                        query.AppendFormat("{0} = {1}",
                            Context.QuoteIdentifier(column.Key), SqlExpression<WhereStatement>.FormatSQLValue(column.Value.Value));
                    else
                        query.AppendFormat("{0} = {0} {1} {2}", Context.QuoteIdentifier(column.Key),
                            GetSign(column.Value.Mode), SqlExpression<WhereStatement>.FormatSQLValue(column.Value.Value));
                }
            }

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
        public override int Execute()
        {
            using (SqliteCommand command = BuildCommand())
                return command.ExecuteNonQuery();
        }

        /// <summary>
        /// Returns the sign for the given value mode
        /// </summary>
        /// <param name="mode"></param>
        /// <returns></returns>
        protected string GetSign(ValueMode mode)
        {
            switch (mode)
            {
                default:
                case ValueMode.Set: return "=";
                case ValueMode.Add: return "+";
                case ValueMode.Divide: return "/";
                case ValueMode.Multiply: return "*";
                case ValueMode.Subtract: return "-";
            }
        }

        public override void Dispose()
        {
            WhereStatement = null;
            Columns = null;

            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Internal ColumnValuePair object
        /// </summary>
        protected struct ColumnValuePair
        {
            public string Name;
            public object Value;
            public ValueMode Mode;

            public ColumnValuePair(string column, object value, ValueMode mode = ValueMode.Set)
            {
                this.Name = column;
                this.Value = value;
                this.Mode = mode;
            }
        }
    }
}
