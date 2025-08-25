using System;

namespace CrossLite.CodeFirst
{
    /// <summary>
    /// Represents a Foreign Key constraint on a table. Only used
    /// in CodeFirst table creation: <see cref="SQLiteContext.CreateTable{TEntity}(bool)"/>
    /// </summary>
    [AttributeUsage(AttributeTargets.Property)]
    public class ForeignKeyAttribute : Attribute
    {
        /// <summary>
        /// Gets an array of child attribute names on a foreign key constraint
        /// </summary>
        public string[] Attributes { get; internal set; }

        /// <summary>
        /// Creates a single foreign key restraint between the attached Entity
        /// attribute, and the specifed parent Entity attribute
        /// </summary>
        public ForeignKeyAttribute(string attribute)
        {
            this.Attributes = new string[] { attribute };
        }

        /// <summary>
        /// Creates a foreign key constraint between the attached Entity
        /// attributes, and the specifed parent Entity attributes
        /// </summary>
        public ForeignKeyAttribute(params string[] attributes)
        {
            this.Attributes = attributes;
        }
    }
}
