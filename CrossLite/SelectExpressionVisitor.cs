using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;

namespace CrossLite
{
    /// <summary>
    /// Analyzes a Select projection expression like <c>x => new { x.Name, x.TIS }</c>
    /// and extracts the column list + builds a compiled reader delegate.
    /// </summary>
    internal static class SelectExpressionAnalyzer
    {
        /// <summary>
        /// Analyzes the projection and returns the selected columns and a compiled
        /// delegate that reads from a SqliteDataReader using ordinal indices.
        /// </summary>
        public static (
            List<(string ColumnName, string PropertyName)> Columns,
            Func<SqliteDataReader, int[], TResult> Projector
        ) Analyze<TEntity, TResult>(
            TableMapping table,
            Expression<Func<TEntity, TResult>> selector)
        {
            var columns = new List<(string ColumnName, string PropertyName)>();
            Expression body = selector.Body;

            // ── Case 1: new { x.Name, x.TIS } (anonymous type / DTO via constructor)
            if (body is NewExpression newExpr)
            {
                for (int i = 0; i < newExpr.Arguments.Count; i++)
                {
                    var col = ResolveColumn<TEntity>(table, newExpr.Arguments[i]);
                    columns.Add(col);
                }
            }
            // ── Case 2: new SomeDto { Name = x.Name, TIS = x.TIS } (MemberInit)
            else if (body is MemberInitExpression memberInit)
            {
                // Include constructor args if any
                if (memberInit.NewExpression.Arguments.Count > 0)
                {
                    for (int i = 0; i < memberInit.NewExpression.Arguments.Count; i++)
                    {
                        var col = ResolveColumn<TEntity>(table, memberInit.NewExpression.Arguments[i]);
                        columns.Add(col);
                    }
                }

                foreach (var binding in memberInit.Bindings)
                {
                    if (binding is MemberAssignment assignment)
                    {
                        var col = ResolveColumn<TEntity>(table, assignment.Expression);
                        columns.Add(col);
                    }
                }
            }
            // ── Case 3: x => x.Name (single property → TResult is that property's type)
            else if (body is MemberExpression || 
                     (body is UnaryExpression unary && unary.Operand is MemberExpression))
            {
                var col = ResolveColumn<TEntity>(table, body);
                columns.Add(col);
            }
            else
            {
                throw new NotSupportedException(
                    $"Select expression type '{body.NodeType}' is not supported. " +
                    "Use: x => x.Prop, x => new {{ x.Prop1, x.Prop2 }}, or x => new Dto {{ Prop = x.Prop }}");
            }

            // Build the projector delegate using expression trees for maximum performance
            var projector = BuildProjector<TEntity, TResult>(table, columns, selector);
            return (columns, projector);
        }

        /// <summary>
        /// Builds a compiled delegate: (SqliteDataReader reader, int[] ordinals) => TResult
        /// </summary>
        private static Func<SqliteDataReader, int[], TResult> BuildProjector<TEntity, TResult>(
            TableMapping table,
            List<(string ColumnName, string PropertyName)> columns,
            Expression<Func<TEntity, TResult>> originalSelector)
        {
            var readerParam = Expression.Parameter(typeof(SqliteDataReader), "reader");
            var ordinalsParam = Expression.Parameter(typeof(int[]), "ordinals");

            // For each column index, build: reader.GetValue(ordinals[i])
            // then convert to the target type
            Expression body = originalSelector.Body;

            if (body is NewExpression newExpr)
            {
                // Rebuild: new TResult(reader.GetValue(ordinals[0]), reader.GetValue(ordinals[1]), ...)
                var args = new Expression[newExpr.Arguments.Count];
                for (int i = 0; i < newExpr.Arguments.Count; i++)
                {
                    Type targetType = newExpr.Arguments[i].Type;
                    args[i] = BuildReadExpression(readerParam, ordinalsParam, i, targetType);
                }

                var newCall = Expression.New(newExpr.Constructor, args, newExpr.Members);
                var lambda = Expression.Lambda<Func<SqliteDataReader, int[], TResult>>(
                    newCall, readerParam, ordinalsParam);
                return lambda.Compile();
            }
            else if (body is MemberInitExpression memberInit)
            {
                int argOffset = memberInit.NewExpression.Arguments.Count;
                
                // Constructor args
                var ctorArgs = new Expression[argOffset];
                for (int i = 0; i < argOffset; i++)
                {
                    Type targetType = memberInit.NewExpression.Arguments[i].Type;
                    ctorArgs[i] = BuildReadExpression(readerParam, ordinalsParam, i, targetType);
                }

                var newCall = argOffset > 0
                    ? Expression.New(memberInit.NewExpression.Constructor, ctorArgs)
                    : memberInit.NewExpression;

                // Member bindings
                var bindings = new List<MemberBinding>();
                for (int i = 0; i < memberInit.Bindings.Count; i++)
                {
                    if (memberInit.Bindings[i] is MemberAssignment assignment)
                    {
                        Type targetType = assignment.Expression.Type;
                        var readExpr = BuildReadExpression(readerParam, ordinalsParam, argOffset + i, targetType);
                        bindings.Add(Expression.Bind(assignment.Member, readExpr));
                    }
                }

                var initExpr = Expression.MemberInit(newCall, bindings);
                var lambda = Expression.Lambda<Func<SqliteDataReader, int[], TResult>>(
                    initExpr, readerParam, ordinalsParam);
                return lambda.Compile();
            }
            else
            {
                // Single property: x => x.Name
                Type targetType = typeof(TResult);
                var readExpr = BuildReadExpression(readerParam, ordinalsParam, 0, targetType);
                var lambda = Expression.Lambda<Func<SqliteDataReader, int[], TResult>>(
                    readExpr, readerParam, ordinalsParam);
                return lambda.Compile();
            }
        }

        /// <summary>
        /// Builds an expression that reads a value from the reader at the given ordinal index
        /// and converts it to the target CLR type, handling nullables and enums.
        /// </summary>
        private static Expression BuildReadExpression(
            ParameterExpression reader, ParameterExpression ordinals,
            int index, Type targetType)
        {
            // ordinals[index]
            var ordinalAccess = Expression.ArrayIndex(ordinals, Expression.Constant(index));

            // reader.IsDBNull(ordinals[index])
            var isDbNull = Expression.Call(reader,
                typeof(SqliteDataReader).GetMethod("IsDBNull", new[] { typeof(int) }),
                ordinalAccess);

            // reader.GetValue(ordinals[index])
            var getValue = Expression.Call(reader,
                typeof(SqliteDataReader).GetMethod("GetValue", new[] { typeof(int) }),
                ordinalAccess);

            Type underlying = Nullable.GetUnderlyingType(targetType);
            bool isNullable = underlying != null;
            Type effectiveType = underlying ?? targetType;

            // Handle enums: Convert.ChangeType to underlying, then cast
            Expression convertedValue;
            if (effectiveType.IsEnum)
            {
                // Convert the DB value to the enum's underlying type, then cast to enum
                Type enumBase = Enum.GetUnderlyingType(effectiveType);
                var changeType = Expression.Call(
                    typeof(Convert), "ChangeType",
                    Type.EmptyTypes,
                    getValue, Expression.Constant(enumBase));
                convertedValue = Expression.Convert(Expression.Convert(changeType, enumBase), effectiveType);
            }
            else
            {
                convertedValue = Expression.Convert(getValue, effectiveType);
            }

            // Wrap in nullable if needed
            if (isNullable)
            {
                // reader.IsDBNull(...) ? (TargetType?)null : (TargetType?)value
                return Expression.Condition(
                    isDbNull,
                    Expression.Constant(null, targetType),
                    Expression.Convert(convertedValue, targetType));
            }
            else if (!effectiveType.IsValueType)
            {
                // Reference type: return null if DBNull
                return Expression.Condition(
                    isDbNull,
                    Expression.Default(targetType),
                    convertedValue);
            }
            else
            {
                return convertedValue;
            }
        }

        private static (string ColumnName, string PropertyName) ResolveColumn<TEntity>(
            TableMapping table, Expression expr)
        {
            if (expr is UnaryExpression unary && unary.NodeType == ExpressionType.Convert)
                expr = unary.Operand;

            if (expr is MemberExpression member && member.Member is PropertyInfo prop)
            {
                if (table.EntityProperties.TryGetValue(prop.Name, out var info))
                    return (info.ColumnName, prop.Name);

                throw new NotSupportedException(
                    $"Property '{prop.Name}' is not a mapped column on '{typeof(TEntity).Name}'.");
            }

            throw new NotSupportedException(
                $"Select projection must reference entity properties. Got: {expr.NodeType}");
        }
    }
}