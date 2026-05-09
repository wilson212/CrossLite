using System;
using System.Collections.Generic;

namespace CrossLite
{
    /// <summary>
    /// Marker methods for use inside Where() expression trees.
    /// These are NEVER executed at runtime — the <see cref="WhereExpressionVisitor{T}"/> intercepts them.
    /// </summary>
    public static class SqlMethods
    {
        public static bool In<T>(this T value, params T[] set)
            => throw new InvalidOperationException("This method is only for use in expression trees.");

        public static bool In<T>(this T value, IEnumerable<T> set)
            => throw new InvalidOperationException("This method is only for use in expression trees.");

        public static bool NotIn<T>(this T value, params T[] set)
            => throw new InvalidOperationException("This method is only for use in expression trees.");

        public static bool NotIn<T>(this T value, IEnumerable<T> set)
            => throw new InvalidOperationException("This method is only for use in expression trees.");

        public static bool Between<T>(this T value, T low, T high)
            => throw new InvalidOperationException("This method is only for use in expression trees.");

        public static bool NotBetween<T>(this T value, T low, T high)
            => throw new InvalidOperationException("This method is only for use in expression trees.");

        public static bool Like(this string value, string pattern)
            => throw new InvalidOperationException("This method is only for use in expression trees.");

        public static bool NotLike(this string value, string pattern)
            => throw new InvalidOperationException("This method is only for use in expression trees.");
    }
}