using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CrossLite.QueryBuilder
{
    /// <summary>
    /// This object represents an SQL expression within a Where clause.
    /// </summary>
    public sealed class SqlExpression<TWhere> where TWhere : IWhereStatement
    {
        /// <summary>
        /// The column name for this expression
        /// </summary>
        public string Identifier { get; private set; }

        /// <summary>
        /// The Comaparison Operator to use
        /// </summary>
        public Comparison ComparisonOperator { get; private set; }

        /// <summary>
        /// The Value of this expression
        /// </summary>
        public object Value { get; private set; }

        /// <summary>
        /// The <see cref="WhereStatement"/> that this expression is attached to
        /// </summary>
        private TWhere Statement;

        /// <summary>
        /// Creates a new instance of <see cref="SqlExpression"/>
        /// </summary>
        /// <param name="columnName">The column name we are expressing</param>
        /// <param name="operator">The comparison operator</param>
        /// <param name="value">The value of this expression comparison.</param>
        /// <param name="statement">The <see cref="WhereStatement"/> we are attached to. This
        /// allows chaining methods easily.</param>
        public SqlExpression(string columnName, Comparison @operator, object value, TWhere statement)
        {
            // Set property values
            this.Identifier = columnName;
            this.ComparisonOperator = @operator;
            this.Value = value;
            this.Statement = statement;
            Type valueType = value.GetType();

            // Do some checking
            if (@operator == Comparison.Between || @operator == Comparison.NotBetween)
            {
                // Ensure that this is an array with 2 values
                if (!valueType.IsArray || ((Array)Value).Length != 2)
                    throw new ArgumentException("The value of a Between clause must be an array, with 2 values.");
            }
            else if (valueType.IsArray && @operator != Comparison.In && @operator != Comparison.NotIn)
            {
                throw new ArgumentException($"The value type {@operator} cannot compare array values.");
            }

            // Cant use NULL values for most operators
            if ((Value == null || Value == DBNull.Value) && (@operator != Comparison.Equals && @operator != Comparison.NotEqualTo))
            {
                throw new Exception("Cannot use comparison operator " + @operator + " for NULL values.");
            }
        }

        /// <summary>
        /// Creates a new instance of <see cref="SqlExpression"/>
        /// </summary>
        /// <param name="columnName">The column name we are expressing</param>
        /// <param name="statement">The <see cref="WhereStatement"/> we are attached to. This
        /// allows chaining methods easily.</param>
        public SqlExpression(string columnName, TWhere statement)
        {
            this.Identifier = columnName;
            this.Statement = statement;
        }

        /// <summary>
        /// Sets the internal value
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="value"></param>
        /// <param name="operator"></param>
        /// <returns></returns>
        private TWhere SetValue<T>(T value, Comparison @operator) where T : struct
        {
            Value = GetUnderlyingValue(value);
            ComparisonOperator = @operator;
            return Statement;
        }

        /// <summary>
        /// If the struct is an enumeration, returns the underlying value type. Otherwise
        /// just the value is returned
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="value"></param>
        /// <returns></returns>
        private object GetUnderlyingValue<T>(T value)
        {
            Type valueType = typeof(T);
            if (valueType.IsEnum)
            {
                Type type = Enum.GetUnderlyingType(valueType);
                return Convert.ChangeType(value, type);
            }

            return value;
        }

        /// <summary>
        /// Specifies the comparison of this expression with an Equal operator
        /// </summary>
        public TWhere Equals(string value)
        {
            ComparisonOperator = Comparison.Equals;
            Value = value;
            return Statement;
        }

        /// <summary>
        /// Specifies the comparison of this expression with an Equal operator
        /// </summary>
        public TWhere Equals<T>(T value) where T : struct
            => SetValue(value, Comparison.Equals);

        /// <summary>
        /// Specifies the comparison of this expression with an Not equal operator
        /// </summary>
        public TWhere NotEqualTo(string value)
        {
            ComparisonOperator = Comparison.NotEqualTo;
            Value = value;
            return Statement;
        }

        /// <summary>
        /// Specifies the comparison of this expression with an Not equal operator
        /// </summary>
        public TWhere NotEqualTo<T>(T value) where T : struct
            => SetValue(value, Comparison.NotEqualTo);

        /// <summary>
        /// Specifies the comparison of this expression with a Simple pattern matching operator
        /// </summary>
        public TWhere Like(string value)
        {
            ComparisonOperator = Comparison.Like;
            Value = value;
            return Statement;
        }

        /// <summary>
        /// Specifies the comparison of this expression with a Negation of simple pattern matching
        /// </summary>
        public TWhere NotLike(string value)
        {
            ComparisonOperator = Comparison.NotLike;
            Value = value;
            return Statement;
        }

        /// <summary>
        /// Specifies the comparison of this expression with a Greater than or equal operator
        /// </summary>
        public TWhere GreaterOrEquals<T>(T value) where T : struct
            => SetValue(value, Comparison.GreaterOrEquals);

        /// <summary>
        /// Specifies the comparison of this expression with a Greater than operator
        /// </summary>
        public TWhere GreaterThan<T>(T value) where T : struct
            => SetValue(value, Comparison.GreaterThan);

        /// <summary>
        /// Specifies the comparison of this expression with a Less than or equal operator
        /// </summary>
        public TWhere LessOrEquals<T>(T value) where T : struct
            => SetValue(value, Comparison.LessOrEquals);

        /// <summary>
        /// Specifies the comparison of this expression with a Less than operator
        /// </summary>
        public TWhere LessThan<T>(T value) where T : struct
            => SetValue(value, Comparison.LessThan);

        /// <summary>
        /// Specifies the comparison of this expression is within a set of values
        /// </summary>
        public TWhere In(params string[] values)
        {
            ComparisonOperator = Comparison.In;
            Value = values;
            return Statement;
        }

        /// <summary>
        /// Specifies the comparison of this expression is within a set of values
        /// </summary>
        public TWhere In<T>(params T[] values) where T : struct
        {
            ComparisonOperator = Comparison.In;
            Value = values.Select(x => GetUnderlyingValue(x));
            return Statement;
        }

        /// <summary>
        /// Specifies the comparison of this expression is within a set of values
        /// </summary>
        public TWhere In(IEnumerable<string> values)
        {
            ComparisonOperator = Comparison.In;
            Value = values;
            return Statement;
        }

        /// <summary>
        /// Specifies the comparison of this expression is within a set of values
        /// </summary>
        public TWhere In<T>(IEnumerable<T> values) where T : struct
        {
            ComparisonOperator = Comparison.NotIn;
            Value = values.Select(x => GetUnderlyingValue(x));
            return Statement;
        }

        /// <summary>
        /// Specifies the comparison of this expression is not within a set of values
        /// </summary>
        public TWhere NotIn(params string[] values)
        {
            ComparisonOperator = Comparison.NotIn;
            Value = values;
            return Statement;
        }

        /// <summary>
        /// Specifies the comparison of this expression is not within a set of values
        /// </summary>
        public TWhere NotIn<T>(params T[] values) where T : struct
        {
            ComparisonOperator = Comparison.NotIn;
            Value = values.Select(x => GetUnderlyingValue(x));
            return Statement;
        }

        /// <summary>
        /// Specifies the comparison of this expression is not within a set of values
        /// </summary>
        public TWhere NotIn(IEnumerable<string> values)
        {
            ComparisonOperator = Comparison.NotIn;
            Value = values;
            return Statement;
        }

        /// <summary>
        /// Specifies the comparison of this expression is not within a set of values
        /// </summary>
        public TWhere NotIn<T>(IEnumerable<T> values) where T : struct
        {
            ComparisonOperator = Comparison.NotIn;
            Value = values.Select(x => GetUnderlyingValue(x));
            return Statement;
        }

        /// <summary>
        /// Specifies the comparison of this expression is within a range of values
        /// </summary>
        public TWhere Between<T>(T value1, T value2) where T : struct
        {
            ComparisonOperator = Comparison.Between;
            Value = new object[] { GetUnderlyingValue(value1), GetUnderlyingValue(value2) };
            return Statement;
        }

        /// <summary>
        /// Specifies the comparison of this expression is not within a range of values
        /// </summary>
        public TWhere NotBetween<T>(T value1, T value2) where T : struct
        {
            ComparisonOperator = Comparison.NotBetween;
            Value = new object[] { GetUnderlyingValue(value1), GetUnderlyingValue(value2) };
            return Statement;
        }

        /// <summary>
        /// Generates and returns an SQL expression string, storing the values as SQL
        /// parameters for SQL injection safty.
        /// </summary>
        /// <param name="parameters"></param>
        /// <returns></returns>
        public string BuildExpression(List<SqliteParameter> parameters)
        {
            // Check for literals
            var isLiteral = (Value is SqlLiteral);

            // If using a command, Convert values to Parameters for SQL safety
            if (Value != null && Value != DBNull.Value && !isLiteral)
            {
                // --------------------------------------
                // BETWEEN and NOT BETWEEN
                //--------------------------------------
                if (ComparisonOperator == Comparison.Between || ComparisonOperator == Comparison.NotBetween)
                {
                    // Add the between values to the command parameters
                    Array between = ((Array)Value);

                    var param1 = new SqliteParameter();
                    param1.ParameterName = "@P" + parameters.Count;
                    param1.Value = between.GetValue(0);

                    var param2 = new SqliteParameter();
                    param2.ParameterName = "@P" + (parameters.Count + 1);
                    param2.Value = between.GetValue(1);

                    // Add Params to command
                    parameters.Add(param1);
                    parameters.Add(param2);

                    // Add statement
                    return CreateExpressionString(param1, param2);
                }
                else if (ComparisonOperator == Comparison.In || ComparisonOperator == Comparison.NotIn)
                {
                    // Add the between values to the command parameters
                    Array values = ((Array)Value);
                    int offset = parameters.Count;

                    foreach (var obj in values)
                    {
                        var param = new SqliteParameter();
                        param.ParameterName = "@P" + parameters.Count;
                        param.Value = obj;
                        parameters.Add(param);
                    }

                    // Add statement
                    return CreateExpressionString(parameters.Skip(offset).ToArray());
                }
                // --------------------------------------
                // All Other Clauses
                //--------------------------------------
                else
                {
                    // Create param for value
                    var param = new SqliteParameter();
                    param.ParameterName = "@P" + parameters.Count;
                    param.Value = Value;

                    // Add Params to command
                    parameters.Add(param);

                    // Add statement
                    return CreateExpressionString(param);
                }
            }
            else if (isLiteral)
            {
                return CreateExpressionString((SqlLiteral)Value);
            }
            else // Null and SqlLiteral values
            {
                return CreateExpressionString();
            }
        }

        /// <summary>
        /// ToString override
        /// </summary>
        public override string ToString() => CreateExpressionString();

        /// <summary>
        /// Creates an SQL expression with the value of the <see cref="SQLiteParameter.ParameterName"/>. 
        /// </summary>
        private string CreateExpressionString(SqliteParameter param)
        {
            // Correct ColumnName and define variables
            string fieldName = SQLiteContext.QuoteIdentifier(Identifier, Statement.AttributeQuoteMode, Statement.AttributeQuoteKind);
            switch (ComparisonOperator)
            {
                case Comparison.Equals:
                    return $"{fieldName} = {param.ParameterName}";
                case Comparison.NotEqualTo:
                    return $"{fieldName} != {param.ParameterName}";
                case Comparison.Like:
                    return $"{fieldName} LIKE {param.ParameterName}";
                case Comparison.NotLike:
                    return $"{fieldName} NOT LIKE {param.ParameterName}";
                case Comparison.GreaterThan:
                    return $"{fieldName} > {param.ParameterName}";
                case Comparison.GreaterOrEquals:
                    return $"{fieldName} >= {param.ParameterName}";
                case Comparison.LessThan:
                    return $"{fieldName} < {param.ParameterName}";
                case Comparison.LessOrEquals:
                    return $"{fieldName} <= {param.ParameterName}";
                case Comparison.In:
                    return $"{fieldName} IN ({param.ParameterName})";
                case Comparison.NotIn:
                    return $"{fieldName} NOT IN ({param.ParameterName})";
                default:
                case Comparison.Between:
                case Comparison.NotBetween:
                    string name = Enum.GetName(typeof(Comparison), ComparisonOperator);
                    throw new Exception($"The operator {name} does not support just 1 SQLiteParameter.");
            }
        }

        /// <summary>
        /// Creates an SQL expression with the values of the <see cref="SqliteParameter.ParameterName"/>'s. 
        /// </summary>
        private string CreateExpressionString(params SqliteParameter[] parameters)
        {
            // Correct ColumnName and define variables
            string fieldName = SQLiteContext.QuoteIdentifier(Identifier, Statement.AttributeQuoteMode, Statement.AttributeQuoteKind);
            switch (ComparisonOperator)
            {
                case Comparison.In:
                    return $"{fieldName} IN ({String.Join(", ", parameters.Select(x => x.ParameterName))})";
                case Comparison.NotIn:
                    return $"{fieldName} NOT IN ({String.Join(", ", parameters.Select(x => x.ParameterName))})";
                case Comparison.Between:
                case Comparison.NotBetween:
                    // Ensure length
                    if (parameters.Length != 2)
                        throw new ArgumentException($"Invalid parameter count. Expecting 2 got {parameters.Length}.", "parameters");

                    // Build sql
                    StringBuilder builder = new StringBuilder(fieldName);
                    builder.AppendIf(ComparisonOperator == Comparison.NotBetween, " NOT BETWEEN ", " BETWEEN ");
                    builder.Append(parameters[0].ParameterName).Append(" AND ").Append(parameters[1].ParameterName);
                    return builder.ToString();
                default:
                    string name = Enum.GetName(typeof(Comparison), ComparisonOperator);
                    throw new Exception($"The operator {name} does not support multiple SQLiteParameters.");
            }
        }

        /// <summary>
        /// Creates an SQL expression based on the literal value. The object value will not be escaped.
        /// </summary>
        private string CreateExpressionString(SqlLiteral literal)
        {
            // Correct ColumnName and define variables
            string fieldName = SQLiteContext.QuoteIdentifier(Identifier, Statement.AttributeQuoteMode, Statement.AttributeQuoteKind);
            switch (ComparisonOperator)
            {
                case Comparison.Equals:
                    return $"{fieldName} = {literal.Value}";
                case Comparison.NotEqualTo:
                    return $"{fieldName} != {literal.Value}";
                case Comparison.Like:
                    return $"{fieldName} LIKE {literal.Value}";
                case Comparison.NotLike:
                    return $"{fieldName} NOT LIKE {literal.Value}";
                case Comparison.GreaterThan:
                    return $"{fieldName} > {literal.Value}";
                case Comparison.GreaterOrEquals:
                    return $"{fieldName} >= {literal.Value}";
                case Comparison.LessThan:
                    return $"{fieldName} < {literal.Value}";
                case Comparison.LessOrEquals:
                    return $"{fieldName} <= {literal.Value}";
                default:
                    string name = Enum.GetName(typeof(Comparison), ComparisonOperator);
                    throw new Exception($"Cannot parse operator {name} from an SqlLiteral.");
            }
        }

        /// <summary>
        /// Creates an SQL expression based on the passed parameters. The object value will automatically
        /// be escaped and properly quoted.
        /// </summary>
        /// <returns></returns>
        private string CreateExpressionString()
        {
            // Correct ColumnName and define variables
            string fieldName = SQLiteContext.QuoteIdentifier(Identifier, Statement.AttributeQuoteMode, Statement.AttributeQuoteKind);

            // Only 2 options for null values
            if (Value == null || Value == DBNull.Value)
            {
                switch (ComparisonOperator)
                {
                    default:
                    case Comparison.Equals:
                        return $"{fieldName} IS NULL";
                    case Comparison.NotEqualTo:
                        return $"{fieldName} IS NOT NULL";
                }
            }
            else
            {
                // Create local vars
                StringBuilder builder = new StringBuilder();
                Array array;

                // Build sql string based on the operator
                switch (ComparisonOperator)
                {
                    default:
                    case Comparison.Equals:
                        return $"{fieldName} = {FormatSQLValue(Value)}";
                    case Comparison.NotEqualTo:
                        return $"{fieldName} != {FormatSQLValue(Value)}";
                    case Comparison.Like:
                        return $"{fieldName} LIKE {FormatSQLValue(Value)}";
                    case Comparison.NotLike:
                        return $"{fieldName} NOT LIKE {FormatSQLValue(Value)}";
                    case Comparison.GreaterThan:
                        return $"{fieldName} > {FormatSQLValue(Value)}";
                    case Comparison.GreaterOrEquals:
                        return $"{fieldName} >= {FormatSQLValue(Value)}";
                    case Comparison.LessThan:
                        return $"{fieldName} < {FormatSQLValue(Value)}";
                    case Comparison.LessOrEquals:
                        return $"{fieldName} <= {FormatSQLValue(Value)}";
                    case Comparison.In:
                    case Comparison.NotIn:
                        array = (Array)Value;
                        builder.Append(fieldName);
                        builder.AppendIf(ComparisonOperator == Comparison.NotIn, " NOT IN (", " IN (");
                        for (int i = 0; i < array.Length; i++)
                        {
                            builder.Append(FormatSQLValue(array.GetValue(i)));
                            builder.AppendIf(i + 1 != array.Length, ',');
                        }
                        return builder.Append(')').ToString();
                    case Comparison.Between:
                    case Comparison.NotBetween:
                        array = (Array)Value;
                        builder.Append(fieldName);
                        builder.AppendIf(ComparisonOperator == Comparison.NotBetween, " NOT BETWEEN ", " BETWEEN ");
                        builder.Append(FormatSQLValue(array.GetValue(0)));
                        builder.Append(" AND ");
                        builder.Append(FormatSQLValue(array.GetValue(1)));
                        return builder.ToString();
                }
            }
        }

        /// <summary>
        /// Creates an SQL expression based on the literal value. The object value will not be escaped.
        /// </summary>
        public static string CreateExpressionString(string fieldName, Comparison @operator, SqlLiteral literal)
        {
            // Correct ColumnName and define variables
            switch (@operator)
            {
                case Comparison.Equals:
                    return $"{fieldName} = {literal.Value}";
                case Comparison.NotEqualTo:
                    return $"{fieldName} != {literal.Value}";
                case Comparison.Like:
                    return $"{fieldName} LIKE {literal.Value}";
                case Comparison.NotLike:
                    return $"{fieldName} NOT LIKE {literal.Value}";
                case Comparison.GreaterThan:
                    return $"{fieldName} > {literal.Value}";
                case Comparison.GreaterOrEquals:
                    return $"{fieldName} >= {literal.Value}";
                case Comparison.LessThan:
                    return $"{fieldName} < {literal.Value}";
                case Comparison.LessOrEquals:
                    return $"{fieldName} <= {literal.Value}";
                default:
                    string name = Enum.GetName(typeof(Comparison), @operator);
                    throw new Exception($"Cannot parse operator {name} from an SqlLiteral.");
            }
        }

        /// <summary>
        /// Formats and escapes a Value object, to the proper SQL format.
        /// </summary>
        /// <param name="someValue"></param>
        /// <returns></returns>
        public static string FormatSQLValue(object someValue)
        {
            // Just return null if our value is null
            if (someValue == null) return "NULL";

            // Check for numbers first
            Type valType = someValue.GetType();
            if (valType.IsNumericType())
                return someValue.ToString();

            // Not a numeric, so...
            switch (valType.Name)
            {
                default:
                case "String": return $"'{someValue.ToString().Replace("'", "''")}'";
                case "SqlLiteral": return ((SqlLiteral)someValue).Value;
                case "DateTime": return $"'{((DateTime)someValue).ToString("yyyy-MM-dd HH:mm:ss")}'";
                case "DBNull": return "NULL";
                case "Boolean": return (bool)someValue ? "1" : "0";
                case "Guid": return $"'{((Guid)someValue).ToString()}'";
                case "SelectQueryBuilder":
                    throw new ArgumentException("Using SelectQueryBuilder in another Querybuilder statement is unsupported!", "someValue");
            }
        }
    }
}