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

        public override string ToString() => ColumnName;

        public override bool Equals(object obj)
        {
            if (obj is AttributeInfo)
            {
                var item = (AttributeInfo)obj;
                return item.ColumnName.Equals(this.ColumnName);
            }
            return false;
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }
    }
}
