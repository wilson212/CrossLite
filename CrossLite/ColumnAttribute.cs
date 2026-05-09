using System;
using System.Runtime.CompilerServices;

namespace CrossLite
{
    /// <summary>
    /// Represents an Entity property to attribute relationship. Only used
    /// in CodeFirst table creation <see cref="SQLiteContext.CreateTable{TEntity}(bool)"/>
    /// </summary>
    [AttributeUsage(AttributeTargets.Property)]
    public class ColumnAttribute : Attribute
    {
        /// <summary>
        /// Gets the attribute (column) name in the database
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Gets the column ordering within the table schema.
        /// </summary>
        public int Order { get; set; }

        /// <summary>
        /// Specifies that a property within an entity corresponds to a specific
        /// column in the database. Utilized in CodeFirst table creation to establish
        /// a mapping between entity properties and database fields.
        /// </summary>
        /// <remarks>
        /// This attribute can be specifically applied to the properties of an entity
        /// to designate the target database column name and, optionally, establish
        /// column ordering within the table schema.
        /// </remarks>
        public ColumnAttribute(string name = null, [CallerLineNumber] int order = 99)
        {
            this.Name = name;
            this.Order = order;
        }
    }
}
