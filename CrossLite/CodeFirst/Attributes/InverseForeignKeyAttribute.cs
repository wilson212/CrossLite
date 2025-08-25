using System;

namespace CrossLite.CodeFirst
{
    /// <summary>
    /// Represents a Foreign Key constraint on a child table. Only used
    /// in CodeFirst table creation: <see cref="SQLiteContext.CreateTable{TEntity}(bool)"/>
    /// </summary>
    /// <remarks>
    /// Apply this to a collection property (like an EntitySet) on a parent entity to specify which 
    /// foreign key on the child entity links back to this parent. This is necessary when a child 
    /// entity has multiple foreign keys referencing the same parent type.
    /// </remarks>
    [AttributeUsage(AttributeTargets.Property)]
    public class InverseForeignKeyAttribute : Attribute
    {
        /// <summary>
        /// Gets an array of child attribute names on a foreign key constraint
        /// </summary>
        public string[] Attributes { get; internal set; }

        /// <summary>
        /// Creates a single foreign key restraint between the attached Entity
        /// attribute, and the specifed parent Entity attribute
        /// </summary>
        public InverseForeignKeyAttribute(string attribute)
        {
            this.Attributes = new string[] { attribute };
        }

        /// <summary>
        /// Creates a foreign key constraint between the attached Entity
        /// attributes, and the specifed parent Entity attributes
        /// </summary>
        public InverseForeignKeyAttribute(params string[] attributes)
        {
            this.Attributes = attributes;
        }
    }
}
