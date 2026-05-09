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
        /// Builds the SQL query string for an update operation based on the specified parameters and conditions.
        /// </summary>
        /// <returns>A string representing the constructed SQL query for the update operation.</returns>
        public override string BuildQuery() => BuildSql(null);

        /// <summary>
        /// Builds and returns a configured SQLite command object with the generated SQL statement and associated parameters.
        /// </summary>
        /// <returns>A <see cref="SqliteCommand"/> object containing the SQL query and its parameters for execution.</returns>
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
        /// Builds an SQL command string for updating rows in a database table and optionally assigns parameters for the query.
        /// </summary>
        /// <param name="parameters">A list of <see cref="SqliteParameter"/> objects that will be populated with query parameter values, or null if no parameterization is needed.</param>
        /// <returns>A string containing the generated SQL update statement.</returns>
        /// <exception cref="Exception">Thrown if the database context is not set, the table name is not specified, or no column values are provided for the update.</exception>
        private string BuildSql(List<SqliteParameter> parameters)
        {
            // Make sure we have a valid DB driver
            if (Context == null)
                throw new Exception("Cannot build a command when the Context hasn't been specified. Call SetContext first.");

            if (String.IsNullOrWhiteSpace(Table))
                throw new Exception("Table to update was not set.");

            if (Columns.Count == 0)
                throw new Exception("No column values to update");

            var query = new StringBuilder("UPDATE ", 256);
            query.Append(Context.QuoteIdentifier(Table));
            query.Append(" SET ");

            bool first = true;
            foreach (var column in Columns)
            {
                if (!first) query.Append(", ");
                else first = false;

                string quotedCol = Context.QuoteIdentifier(column.Key);

                if (parameters != null && column.Value.Value != null && column.Value.Value != DBNull.Value && !(column.Value.Value is SqlLiteral))
                {
                    var param = Context.CreateParameter();
                    param.ParameterName = "@P" + parameters.Count;
                    param.Value = column.Value.Value;
                    parameters.Add(param);

                    if (column.Value.Mode == ValueMode.Set)
                    {
                        query.Append(quotedCol).Append(" = ").Append(param.ParameterName);
                    }
                    else
                    {
                        query.Append(quotedCol).Append(" = ").Append(quotedCol)
                             .Append(' ').Append(GetSign(column.Value.Mode)).Append(' ')
                             .Append(param.ParameterName);
                    }
                }
                else
                {
                    string formatted = SqlExpression<WhereStatement>.FormatSQLValue(column.Value.Value);
                    if (column.Value.Mode == ValueMode.Set)
                    {
                        query.Append(quotedCol).Append(" = ").Append(formatted);
                    }
                    else
                    {
                        query.Append(quotedCol).Append(" = ").Append(quotedCol)
                             .Append(' ').Append(GetSign(column.Value.Mode)).Append(' ')
                             .Append(formatted);
                    }
                }
            }

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

        public override void Dispose() { }

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
