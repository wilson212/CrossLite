using System;

namespace CrossLite.CodeFirst
{
    /// <summary>
    /// Represents an attribute that can be applied to a class to specify the creation
    /// of a composite index when the class is mapped to a database table.
    /// </summary>
    /// <remarks>
    /// The CompositeIndexAttribute is intended to be used in code-first database
    /// designs where you need to define composite indexes. A composite index is
    /// based on multiple columns in a table. This attribute allows specifying
    /// those columns, an optional name for the index, and whether the index should
    /// enforce uniqueness.
    /// </remarks>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    public class CompositeIndexAttribute : Attribute
    {
        /// <summary>
        /// Gets or sets the collection of property names that define the columns used to create
        /// a composite index in the database table. These property names correspond to the
        /// properties or fields of the class that this attribute is applied to.
        /// </summary>
        /// <remarks>
        /// When using the CompositeIndexAttribute to define a composite index, the Properties
        /// property specifies the names of the columns that will form the composite index.
        /// The order of the names in this collection determines the order of the columns
        /// in the database index, which can affect index performance and query results.
        /// </remarks>
        public string[] Properties { get; set; }

        /// <summary>
        /// Gets or sets the name of the composite index to be created in the database table.
        /// </summary>
        /// <remarks>
        /// The Name property specifies a custom name for the composite index. If this property
        /// is not set, a default name will be generated based on the table name and the index
        /// order. Providing a custom name allows for greater control and clarity when managing
        /// database indexes, especially in scenarios where naming conventions or specific naming
        /// requirements are enforced.
        /// </remarks>
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the composite index should enforce uniqueness
        /// across the combined values of the specified columns.
        /// </summary>
        /// <remarks>
        /// When set to true, the composite index ensures that the combination of values in the
        /// defined columns is unique across all rows in the table. This is useful for preventing
        /// duplicate entries in scenarios where a unique constraint based on multiple columns
        /// is required. If set to false, the index will not enforce uniqueness but can still
        /// improve query performance for the specified column combination.
        /// </remarks>
        public bool Unique { get; set; }

        public CompositeIndexAttribute(params string[] properties)
        {
            this.Properties = properties;
        }
    }
}
