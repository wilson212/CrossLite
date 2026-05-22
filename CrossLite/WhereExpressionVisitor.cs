using CrossLite.QueryBuilder;
using Microsoft.Data.Sqlite;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;

namespace CrossLite
{
    /// <summary>
    /// Translates a lambda predicate like <c>x => x.Name == "foo" || x.Race == RaceKind.White</c>
    /// into a parameterized SQL WHERE clause using CrossLite's existing infrastructure.
    /// </summary>
    public class WhereExpressionVisitor<T> : ExpressionVisitor
    {
        private readonly TableMapping _table;
        private readonly List<SqliteParameter> _parameters = new();
        private readonly Stack<string> _sqlFragments = new();

        public WhereExpressionVisitor()
        {
            _table = TableCache.GetTableMap(typeof(T));
        }

        /// <summary>
        /// The collected SqliteParameters (use these on the command).
        /// </summary>
        public IReadOnlyList<SqliteParameter> Parameters => _parameters;

        /// <summary>
        /// Entry point: pass in the predicate, get back the SQL WHERE clause string.
        /// </summary>
        public string Translate(Expression<Func<T, bool>> predicate)
        {
            _parameters.Clear();
            _sqlFragments.Clear();
            Visit(predicate.Body);
            return _sqlFragments.Pop();
        }

        protected override Expression VisitBinary(BinaryExpression node)
        {
            // Handle logical operators (&& , ||)
            if (node.NodeType == ExpressionType.AndAlso || node.NodeType == ExpressionType.OrElse)
            {
                Visit(node.Left);
                Visit(node.Right);

                string right = _sqlFragments.Pop();
                string left = _sqlFragments.Pop();
                string op = node.NodeType == ExpressionType.AndAlso ? "AND" : "OR";
                _sqlFragments.Push($"({left} {op} {right})");
                return node;
            }

            // Handle comparison operators (==, !=, <, >, <=, >=)
            string columnName = ResolveColumnName(node.Left);
            object value = ResolveValue(node.Right);

            // Handle enums: store the underlying integer value
            if (value != null && value.GetType().IsEnum)
                value = Convert.ChangeType(value, Enum.GetUnderlyingType(value.GetType()));

            string paramName = $"@P{_parameters.Count}";

            // NULL handling
            if (value == null)
            {
                string col = QuoteColumn(columnName);
                string fragment = node.NodeType == ExpressionType.Equal
                    ? $"{col} IS NULL"
                    : $"{col} IS NOT NULL";
                _sqlFragments.Push(fragment);
                return node;
            }

            var param = new SqliteParameter(paramName, value);
            _parameters.Add(param);

            string sqlOp = node.NodeType switch
            {
                ExpressionType.Equal => "=",
                ExpressionType.NotEqual => "!=",
                ExpressionType.LessThan => "<",
                ExpressionType.LessThanOrEqual => "<=",
                ExpressionType.GreaterThan => ">",
                ExpressionType.GreaterThanOrEqual => ">=",
                _ => throw new NotSupportedException($"Binary operator '{node.NodeType}' is not supported.")
            };

            _sqlFragments.Push($"{QuoteColumn(columnName)} {sqlOp} {paramName}");
            return node;
        }

        protected override Expression VisitUnary(UnaryExpression node)
        {
            if (node.NodeType == ExpressionType.Not)
            {
                Visit(node.Operand);
                string operand = _sqlFragments.Pop();
                _sqlFragments.Push($"NOT ({operand})");
                return node;
            }

            // Convert nodes (e.g., enum casts) — just pass through
            if (node.NodeType == ExpressionType.Convert)
                return Visit(node.Operand);

            return base.VisitUnary(node);
        }

        protected override Expression VisitMethodCall(MethodCallExpression node)
        {
            string methodName = node.Method.Name;

            if (node.Method.DeclaringType == typeof(SqlMethods))
            {
                switch (methodName)
                {
                    case "In":
                    case "NotIn":
                        return VisitInOrNotIn(node, methodName == "NotIn");

                    case "Between":
                    case "NotBetween":
                        return VisitBetween(node, methodName == "NotBetween");

                    case "Like":
                    case "NotLike":
                        return VisitLike(node, methodName == "NotLike");
                }
            }
            
            // Support: someList.Contains(x.Property)
            if (methodName == "Contains" && node.Arguments.Count == 1 && node.Object != null)
            {
                // Instance method: List<T>.Contains(item)
                string columnName = ResolveColumnName(node.Arguments[0]);
                string col = QuoteColumn(columnName);
                object rawValues = ResolveValue(node.Object);
                var enumerable = rawValues as IEnumerable
                                 ?? throw new NotSupportedException("Contains requires an IEnumerable instance.");

                var paramNames = new List<string>();
                foreach (object item in enumerable)
                {
                    object val = item;
                    if (val != null && val.GetType().IsEnum)
                        val = Convert.ChangeType(val, Enum.GetUnderlyingType(val.GetType()));

                    string paramName = $"@P{_parameters.Count}";
                    _parameters.Add(new SqliteParameter(paramName, val));
                    paramNames.Add(paramName);
                }

                _sqlFragments.Push($"{col} IN ({string.Join(", ", paramNames)})");
                return node;
            }

            throw new NotSupportedException($"Method '{methodName}' is not supported in expression trees.");
        }
        
        protected override Expression VisitMember(MemberExpression node)
        {
            // If we hit a property access that IS NOT part of a BinaryExpression 
            // (e.g. x => x.IsActive or x => !x.IsActive)
            if (node.Expression != null && node.Expression.NodeType == ExpressionType.Parameter)
            {
                string columnName = ResolveColumnName(node);
                string col = QuoteColumn(columnName);

                // SQLite doesn't have a true boolean type, so we compare to 1
                _sqlFragments.Push($"{col} = 1");
                return node;
            }

            // Otherwise, let the base class handle closure variables
            return base.VisitMember(node);
        }

        private Expression VisitInOrNotIn(MethodCallExpression node, bool negate)
        {
            // Extension method: arg[0] = x.Property, arg[1] = values
            string columnName = ResolveColumnName(node.Arguments[0]);
            string col = QuoteColumn(columnName);

            object rawValues = ResolveValue(node.Arguments[1]);
            var enumerable = rawValues as IEnumerable
                ?? throw new NotSupportedException("In/NotIn requires an IEnumerable argument.");

            var paramNames = new List<string>();
            foreach (object item in enumerable)
            {
                object val = item;
                if (val != null && val.GetType().IsEnum)
                    val = Convert.ChangeType(val, Enum.GetUnderlyingType(val.GetType()));

                string paramName = $"@P{_parameters.Count}";
                _parameters.Add(new SqliteParameter(paramName, val));
                paramNames.Add(paramName);
            }

            string op = negate ? "NOT IN" : "IN";
            _sqlFragments.Push($"{col} {op} ({string.Join(", ", paramNames)})");
            return node;
        }

        private Expression VisitBetween(MethodCallExpression node, bool negate)
        {
            // Extension method: arg[0] = x.Property, arg[1] = low, arg[2] = high
            string columnName = ResolveColumnName(node.Arguments[0]);
            string col = QuoteColumn(columnName);

            object low = ResolveValue(node.Arguments[1]);
            object high = ResolveValue(node.Arguments[2]);

            if (low != null && low.GetType().IsEnum)
                low = Convert.ChangeType(low, Enum.GetUnderlyingType(low.GetType()));
            if (high != null && high.GetType().IsEnum)
                high = Convert.ChangeType(high, Enum.GetUnderlyingType(high.GetType()));

            string p1 = $"@P{_parameters.Count}";
            _parameters.Add(new SqliteParameter(p1, low));
            string p2 = $"@P{_parameters.Count}";
            _parameters.Add(new SqliteParameter(p2, high));

            string op = negate ? "NOT BETWEEN" : "BETWEEN";
            _sqlFragments.Push($"{col} {op} {p1} AND {p2}");
            return node;
        }

        private Expression VisitLike(MethodCallExpression node, bool negate)
        {
            // Extension method: arg[0] = x.Property (string), arg[1] = pattern
            string columnName = ResolveColumnName(node.Arguments[0]);
            string col = QuoteColumn(columnName);

            object pattern = ResolveValue(node.Arguments[1]);
            string paramName = $"@P{_parameters.Count}";
            _parameters.Add(new SqliteParameter(paramName, pattern));

            string op = negate ? "NOT LIKE" : "LIKE";
            _sqlFragments.Push($"{col} {op} {paramName}");
            return node;
        }

        /// <summary>
        /// Resolves the left-hand side of a comparison to a database column name.
        /// </summary>
        private string ResolveColumnName(Expression expr)
        {
            if (expr is UnaryExpression unary && unary.NodeType == ExpressionType.Convert)
                expr = unary.Operand;

            if (expr is MemberExpression member && member.Member is PropertyInfo prop)
            {
                if (_table.EntityProperties.TryGetValue(prop.Name, out var info))
                    return info.ColumnName;

                throw new NotSupportedException(
                    $"Property '{prop.Name}' is not a mapped column on entity '{typeof(T).Name}'.");
            }

            throw new NotSupportedException(
                $"Left-hand side of comparison must be a property access. Got: {expr.NodeType}");
        }

        /// <summary>
        /// Evaluates the right-hand side of a comparison to a constant value.
        /// Handles constants, captured closures, enum values, and static fields/properties.
        /// </summary>
        private object ResolveValue(Expression expr)
        {
            if (expr is UnaryExpression unary && unary.NodeType == ExpressionType.Convert)
                expr = unary.Operand;

            if (expr is ConstantExpression constant)
                return constant.Value;

            // Fast path for simple closure captures (avoids Lambda.Compile)
            if (expr is MemberExpression memberExpr)
            {
                if (memberExpr.Expression is ConstantExpression closureObj && memberExpr.Member is FieldInfo field)
                    return field.GetValue(closureObj.Value);

                // Fallback: compile and invoke
                var lambda = Expression.Lambda<Func<object>>(Expression.Convert(expr, typeof(object)));
                return lambda.Compile().Invoke();
            }

            // Final fallback
            var fallback = Expression.Lambda<Func<object>>(Expression.Convert(expr, typeof(object)));
            return fallback.Compile().Invoke();
        }

        private string QuoteColumn(string columnName)
        {
            return SQLiteContext.QuoteIdentifier(columnName, IdentifierQuoteMode.All, IdentifierQuoteKind.Default);
        }
    }
}