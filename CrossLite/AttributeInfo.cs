using System;
using System.Linq.Expressions;
using System.Reflection;
using CrossLite.CodeFirst;

namespace CrossLite
{
    /// <summary>
    /// Represents metadata about a database column, including its constraints, attributes, and associated properties.
    /// </summary>
    /// <remarks>This class provides detailed information about a database column, such as whether it is a
    /// primary key, auto-incrementing, unique, indexed, nullable, or has a default value. It also includes metadata
    /// about foreign key relationships and collation settings. The information is typically used to map database 
    /// schema details to application-level entities.</remarks>
    public class AttributeInfo
    {
        /// <summary>
        /// Gets the attribute (column) name in the database
        /// </summary>
        public string ColumnName { get; internal set; }

        /// <summary>
        /// Indicates whether this attribute is a Key
        /// </summary>
        public bool IsPrimaryKey { get; internal set; } = false;

        /// <summary>
        /// Indicates whether this attribute Auto Increments (Must be a Key!).
        /// AUTOINCREMENT is to prevent the reuse of ROWIDs from previously deleted rows.
        /// </summary>
        public bool IsAutoIncrement { get; internal set; } = false;

        /// <summary>
        /// Indicates whether this attribute value is unique
        /// </summary>
        public bool IsUnique { get; internal set; } = false;

        /// <summary>
        /// Indicates whether this attribute is indexed
        /// </summary>
        public bool IsIndexed { get; internal set; } = false;

        /// <summary>
        /// Gets the name of the index associated with the database column if the column is indexed.
        /// </summary>
        /// <remarks>Ignored unless IsIndexed is true</remarks>
        public string IndexName { get; internal set; }

        /// <summary>
        /// Gets the default value for this attribute
        /// </summary>
        public DefaultAttribute DefaultValue { get; internal set; } = null;

        /// <summary>
        /// Gets a value indicating whether the associated entity or value can be null.
        /// </summary>
        public bool IsNullable { get; internal set; } = false;

        /// <summary>
        /// Indicates whether this Attribute Requires a value and
        /// cannot be NULL during Entity insertion into the database
        /// </summary>
        public bool HasRequiredAttribute { get; internal set; } = false;

        /// <summary>
        /// Gets the COLLATE type definition that is used to define alternative 
        /// collating functions for a column.
        /// </summary>
        public Collation Collation { get; internal set; } = Collation.Default;

        /// <summary>
        /// Gets the Property for this attribute
        /// </summary>
        public PropertyInfo Property { get; internal set; }

        /// <summary>
        /// If this attribute is a foreign key, then that information is stored here
        /// </summary>
        public ForeignKeyAttribute ForeignKey { get; internal set; }

        /// <summary>
        /// Gets the order in which this item is processed relative to others.
        /// </summary>
        public int Order { get; internal set; } = 99;
        
        /// <summary>
        /// The underlying type (unwrapped from Nullable if applicable).
        /// </summary>
        public Type UnderlyingType { get; private set; }

        /// <summary>
        /// The TypeCode of the underlying type, cached for the hot loop.
        /// </summary>
        public TypeCode UnderlyingTypeCode { get; private set; }
        
        /// <summary>
        /// Whether the underlying type is an enum.
        /// </summary>
        public bool IsEnum { get; private set; }
        
        /// <summary>
        /// Pre-cached "@ColumnName" string for use in parameterized queries,
        /// eliminating per-entity string allocations during bulk writes.
        /// </summary>
        public string ParameterName { get; private set; }

        /// <summary>
        /// Sets the value for a specified Entity and member.
        /// </summary>
        public Action<object, object> SetValue { get; private set; }

        /// <summary>
        /// Retrieves the value of the attribute using the provided delegate function.
        /// </summary>
        public Func<object, object> GetValue { get; private set; }
        
        public Action<object, int> SetInt32 { get; private set; }
        public Action<object, long> SetInt64 { get; private set; }
        public Action<object, double> SetDouble { get; private set; }
        public Action<object, bool> SetBool { get; private set; }
        public Action<object, float> SetFloat { get; private set; }
        public Action<object, short> SetInt16 { get; private set; }
        public Action<object, byte> SetByte { get; private set; }
        public Action<object, decimal> SetDecimal { get; private set; }
        public Action<object, char> SetChar { get; private set; }
        public Action<object, DateTime> SetDateTime { get; private set; }

        /// <summary>
        /// Processes and compiles internal data structures / metadata.
        /// This method performs the core functionality required to prepare attributes for
        /// further use or execution within the application.
        /// </summary>
        internal void Compile()
        {
            // Pre-compute type metadata
            UnderlyingType = Nullable.GetUnderlyingType(Property.PropertyType) ?? Property.PropertyType;
            UnderlyingTypeCode = Type.GetTypeCode(UnderlyingType);
            IsEnum = UnderlyingType.IsEnum;
            
            // Pre-cache the parameter name to avoid string interpolation in hot loops
            ParameterName = $"@{ColumnName}";
            
            // Build them once in the constructor:
            var setter = Property.GetSetMethod(true);
            var getter = Property.GetGetMethod(true);
            
            // Using compiled expressions:
            var instance = Expression.Parameter(typeof(object));
            var value = Expression.Parameter(typeof(object));
            SetValue = Expression.Lambda<Action<object, object>>(
                Expression.Call(Expression.Convert(instance, Property.DeclaringType), setter,
                    Expression.Convert(value, Property.PropertyType)),
                instance, value).Compile();

            GetValue = Expression.Lambda<Func<object, object>>(
                Expression.Convert(
                    Expression.Call(Expression.Convert(instance, Property.DeclaringType), getter),
                    typeof(object)),
                instance).Compile();
            
            if (IsNullable || IsEnum) return;
            
            // Build type-specific setters to avoid boxing in the hot loop
            switch (UnderlyingTypeCode)
            {
                case TypeCode.Int32:
                    var vi32 = Expression.Parameter(typeof(int));
                    SetInt32 = Expression.Lambda<Action<object, int>>(
                        Expression.Call(Expression.Convert(instance, Property.DeclaringType),
                            Property.GetSetMethod(true),
                            Expression.Convert(vi32, Property.PropertyType)),
                        instance, vi32).Compile();
                    break;
                case TypeCode.Int64:
                    var vi64 = Expression.Parameter(typeof(long));
                    SetInt64 = Expression.Lambda<Action<object, long>>(
                        Expression.Call(Expression.Convert(instance, Property.DeclaringType),
                            Property.GetSetMethod(true),
                            Expression.Convert(vi64, Property.PropertyType)),
                        instance, vi64).Compile();
                    break;
                case TypeCode.Double:
                    var vd = Expression.Parameter(typeof(double));
                    SetDouble = Expression.Lambda<Action<object, double>>(
                        Expression.Call(Expression.Convert(instance, Property.DeclaringType),
                            Property.GetSetMethod(true),
                            Expression.Convert(vd, Property.PropertyType)),
                        instance, vd).Compile();
                    break;
                case TypeCode.Boolean:
                    var vb = Expression.Parameter(typeof(bool));
                    SetBool = Expression.Lambda<Action<object, bool>>(
                        Expression.Call(Expression.Convert(instance, Property.DeclaringType),
                            Property.GetSetMethod(true),
                            Expression.Convert(vb, Property.PropertyType)),
                        instance, vb).Compile();
                    break;
                case TypeCode.Int16:
                    var vi16 = Expression.Parameter(typeof(short));
                    SetInt16 = Expression.Lambda<Action<object, short>>(
                        Expression.Call(Expression.Convert(instance, Property.DeclaringType),
                            Property.GetSetMethod(true),
                            Expression.Convert(vi16, Property.PropertyType)),
                        instance, vi16).Compile();
                    break;
                case TypeCode.Byte:
                    var vby = Expression.Parameter(typeof(byte));
                    SetByte = Expression.Lambda<Action<object, byte>>(
                        Expression.Call(Expression.Convert(instance, Property.DeclaringType),
                            Property.GetSetMethod(true),
                            Expression.Convert(vby, Property.PropertyType)),
                        instance, vby).Compile();
                    break;
                case TypeCode.Decimal:
                    var vdec = Expression.Parameter(typeof(decimal));
                    SetDecimal = Expression.Lambda<Action<object, decimal>>(
                        Expression.Call(Expression.Convert(instance, Property.DeclaringType),
                            Property.GetSetMethod(true),
                            Expression.Convert(vdec, Property.PropertyType)),
                        instance, vdec).Compile();
                    break;
                case TypeCode.Char:
                    var vc = Expression.Parameter(typeof(char));
                    SetChar = Expression.Lambda<Action<object, char>>(
                        Expression.Call(Expression.Convert(instance, Property.DeclaringType),
                            Property.GetSetMethod(true),
                            Expression.Convert(vc, Property.PropertyType)),
                        instance, vc).Compile();
                    break;
                case TypeCode.DateTime:
                    var vdt = Expression.Parameter(typeof(DateTime));
                    SetDateTime = Expression.Lambda<Action<object, DateTime>>(
                        Expression.Call(Expression.Convert(instance, Property.DeclaringType),
                            Property.GetSetMethod(true),
                            Expression.Convert(vdt, Property.PropertyType)),
                        instance, vdt).Compile();
                    break;
            }
        }

        public override string ToString() => ColumnName;

        public override bool Equals(object obj)
        {
            if (obj is AttributeInfo item)
            {
                return item.ColumnName.Equals(this.ColumnName);
            }
            return false;
        }

        public override int GetHashCode()
        {
            return ColumnName?.GetHashCode() ?? 0;
        }
    }
}
