using System;

namespace CrossLite.CodeFirst
{
    /// <summary>
    /// Specifies that the associated property should be included in a database index.
    /// </summary>
    /// <remarks>This attribute is used to indicate that a property should be part of an index in the database
    /// schema. The index can be customized using the <see cref="Name"/> and <see cref="Unique"/> properties.</remarks>
    [AttributeUsage(AttributeTargets.Property)]
    public class IndexAttribute(string column) : Attribute
    {
        /// <summary>
        /// Gets or sets the name of the column.
        /// </summary>
        public string Column { get; set; } = column;

        /// <summary>
        /// Gets or sets the name of the index
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the entity is unique.
        /// </summary>
        public bool Unique { get; set; }
    }
}
