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
        /// <summary>
        /// Determines whether the specified value exists in a given set. This method is intended for use in LINQ-based
        /// expression trees and cannot be executed directly at runtime.
        /// </summary>
        /// <typeparam name="T">The type of the value and elements in the set.</typeparam>
        /// <param name="value">The value to check for existence within the provided set.</param>
        /// <param name="set">A collection of elements to search for the specified value.</param>
        /// <returns>Returns true if the value exists in the set; otherwise, false.</returns>
        /// <exception cref="InvalidOperationException">Thrown if this method is executed directly, as it is only intended for use in LINQ expressions.</exception>
        public static bool In<T>(this T value, params T[] set)
            => throw new InvalidOperationException("This method is only for use in expression trees.");

        /// <summary>
        /// Determines whether the specified value exists in a given set. This method is intended for use in LINQ-based
        /// expression trees and cannot be executed directly at runtime.
        /// </summary>
        /// <typeparam name="T">The type of the value and elements in the set.</typeparam>
        /// <param name="value">The value to check for existence within the provided set.</param>
        /// <param name="set">A collection of elements to search for the specified value.</param>
        /// <returns>Returns true if the value exists in the set; otherwise, false.</returns>
        /// <exception cref="InvalidOperationException">Thrown if this method is executed directly, as it is only intended for use in LINQ expressions.</exception>
        public static bool In<T>(this T value, IEnumerable<T> set)
            => throw new InvalidOperationException("This method is only for use in expression trees.");

        /// <summary>
        /// Determines whether the specified value does not exist in a given set. This method is intended for use in
        /// LINQ-based expression trees and cannot be executed directly at runtime.
        /// </summary>
        /// <typeparam name="T">The type of the value and elements in the set.</typeparam>
        /// <param name="value">The value to check for non-existence within the provided set.</param>
        /// <param name="set">A collection of elements to search for the specified value.</param>
        /// <returns>Returns true if the value does not exist in the set; otherwise, false.</returns>
        /// <exception cref="InvalidOperationException">Thrown if this method is executed directly, as it is only intended for use in LINQ expressions.</exception>
        public static bool NotIn<T>(this T value, params T[] set)
            => throw new InvalidOperationException("This method is only for use in expression trees.");

        /// <summary>
        /// Determines whether the specified value does not exist in a given set. This method is intended for use in
        /// LINQ-based expression trees and cannot be executed directly at runtime.
        /// </summary>
        /// <typeparam name="T">The type of the value and elements in the set.</typeparam>
        /// <param name="value">The value to check for non-existence within the provided set.</param>
        /// <param name="set">A collection of elements to search for the specified value.</param>
        /// <returns>Returns true if the value does not exist in the set; otherwise, false.</returns>
        /// <exception cref="InvalidOperationException">Thrown if this method is executed directly, as it is only intended for use in LINQ expressions.</exception>
        public static bool NotIn<T>(this T value, IEnumerable<T> set)
            => throw new InvalidOperationException("This method is only for use in expression trees.");

        /// <summary>
        /// Determines whether the specified value falls inclusively between the given lower and upper bounds.
        /// This method is intended for use in LINQ-based expression trees and cannot be executed directly at runtime.
        /// </summary>
        /// <typeparam name="T">The type of the value and boundary parameters.</typeparam>
        /// <param name="value">The value to evaluate if it falls within the specified range.</param>
        /// <param name="low">The inclusive lower bound of the range.</param>
        /// <param name="high">The inclusive upper bound of the range.</param>
        /// <returns>Returns true if the value is greater than or equal to the lower bound and less than or equal to the upper bound; otherwise, false.</returns>
        /// <exception cref="InvalidOperationException">Thrown if this method is executed directly, as it is only intended for use in expression trees.</exception>
        public static bool Between<T>(this T value, T low, T high)
            => throw new InvalidOperationException("This method is only for use in expression trees.");

        /// <summary>
        /// Determines whether the specified value does not fall within the specified range. This method is designed for use within
        /// LINQ-based expression trees and cannot be executed directly at runtime.
        /// </summary>
        /// <typeparam name="T">The type of the value and the boundaries of the range.</typeparam>
        /// <param name="value">The value to evaluate against the specified range.</param>
        /// <param name="low">The lower boundary of the range, inclusive.</param>
        /// <param name="high">The upper boundary of the range, inclusive.</param>
        /// <returns>Returns true if the value does not fall within the range [low, high]; otherwise, false.</returns>
        /// <exception cref="InvalidOperationException">
        /// Thrown when this method is executed directly, as it is only intended for use in LINQ expression trees.
        /// </exception>
        public static bool NotBetween<T>(this T value, T low, T high)
            => throw new InvalidOperationException("This method is only for use in expression trees.");

        /// <summary>
        /// Determines whether the specified string matches the given pattern. This method is intended for use in LINQ-based
        /// expression trees and cannot be executed directly at runtime.
        /// </summary>
        /// <param name="value">The input string to evaluate against the specified pattern.</param>
        /// <param name="pattern">The pattern to match the input string against.</param>
        /// <returns>Returns true if the input string matches the pattern; otherwise, false.</returns>
        /// <exception cref="InvalidOperationException">Thrown if this method is executed directly, as it is only intended for use in LINQ expressions.</exception>
        public static bool Like(this string value, string pattern)
            => throw new InvalidOperationException("This method is only for use in expression trees.");

        /// <summary>
        /// Determines whether the specified string does not match a given pattern. This method is intended for use in LINQ-based
        /// expression trees and cannot be executed directly at runtime.
        /// </summary>
        /// <param name="value">The string value to evaluate against the pattern.</param>
        /// <param name="pattern">The pattern to compare with the specified string.</param>
        /// <returns>Returns true if the string does not match the pattern; otherwise, false.</returns>
        /// <exception cref="InvalidOperationException">Thrown if this method is executed directly, as it is only intended for use in LINQ expressions.</exception>
        public static bool NotLike(this string value, string pattern)
            => throw new InvalidOperationException("This method is only for use in expression trees.");
    }
}