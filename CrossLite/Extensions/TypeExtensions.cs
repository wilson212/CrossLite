using System;

namespace CrossLite
{
    /// <summary>
    /// Provides extension methods for working with <see cref="Type"/> objects,  including methods to determine if a
    /// type is numeric or an integer type.
    /// </summary>
    /// <remarks>These extension methods are designed to simplify type analysis, particularly  for scenarios
    /// where numeric or integer type checks are required. Nullable numeric  types are treated as numeric, while <see
    /// cref="bool"/> is not considered numeric.</remarks>
    internal static class TypeExtensions
    {
        /// <summary>
        /// Determines if a type is numeric. Nullable numeric types are considered numeric.
        /// </summary>
        /// <remarks>
        /// Boolean is not considered numeric.
        /// </remarks>
        public static bool IsNumericType(this Type type)
        {
            if (type == null)
                return false;

            switch (Type.GetTypeCode(type))
            {
                case TypeCode.Byte:
                case TypeCode.Decimal:
                case TypeCode.Double:
                case TypeCode.Int16:
                case TypeCode.Int32:
                case TypeCode.Int64:
                case TypeCode.SByte:
                case TypeCode.Single:
                case TypeCode.UInt16:
                case TypeCode.UInt32:
                case TypeCode.UInt64:
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Determines if a type is an integer type
        /// </summary>
        public static bool IsInteger(this Type type)
        {
            if (type == null)
                return false;

            switch (Type.GetTypeCode(type))
            {
                case TypeCode.Int16:
                case TypeCode.Int32:
                case TypeCode.Int64:
                case TypeCode.UInt16:
                case TypeCode.UInt32:
                case TypeCode.UInt64:
                    return true;
            }
            return false;
        }
    }
}
