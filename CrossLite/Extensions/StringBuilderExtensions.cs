using System.Text;

namespace System
{
    /// <summary>
    /// Provides extension methods for the <see cref="StringBuilder"/> class, enabling additional functionality such as
    /// conditional appending and clearing the contents of a <see cref="StringBuilder"/> instance.
    /// </summary>
    /// <remarks>These extension methods are designed to simplify common operations on <see
    /// cref="StringBuilder"/> objects, such as clearing their contents or appending values conditionally based on a
    /// predicate. The methods return the modified <see cref="StringBuilder"/> instance to allow for method
    /// chaining.</remarks>
    public static class StringBuilderExtensions
    {
        /// <summary>
        /// Clears the current contents of this StringBuilder
        /// </summary>
        /// <param name="builder"></param>
        public static void Clear(this StringBuilder builder)
        {
            builder.Length = 0;
        }

        /// <summary>
        /// Appends an Object to this string builder if the <paramref name="predicate"/> is true.
        /// </summary>
        /// <param name="predicate">Indicates whether we append this object to the end of this StringBuilder</param>
        /// <param name="value">The value to append</param>
        public static StringBuilder AppendIf(this StringBuilder builder, bool predicate, object value)
        {
            if (predicate)
                builder.Append(value);
            return builder;
        }

        /// <summary>
        /// Appends a character to this string builder if the <paramref name="predicate"/> is true.
        /// </summary>
        /// <param name="predicate">Indicates whether we append this object to the end of this StringBuilder</param>
        /// <param name="value">The value to append</param>
        public static StringBuilder AppendIf(this StringBuilder builder, bool predicate, char value)
        {
            if (predicate)
                builder.Append(value);
            return builder;
        }

        /// <summary>
        /// Appends a string to this string builder if the <paramref name="condition"/> is true, followed by a line terminator.
        /// </summary>
        /// <param name="condition">Indicates whether we append this object to the end of this StringBuilder</param>
        /// <param name="value">The value to append</param>
        public static StringBuilder AppendIf(this StringBuilder builder, bool condition, string trueValue, string falseValue)
        {
            if (condition)
                builder.Append(trueValue);
            else
                builder.Append(falseValue);
            return builder;
        }
    }
}
