using System;

namespace CrossLite.CodeFirst
{
    /// <summary>
    /// This Attribute is used on a Foreign key when the Parent
    /// and Child's attribute names do not match. Only used in CodeFirst table 
    /// creation: <see cref="SQLiteContext.CreateTable{TEntity}(bool)"/>
    /// </summary>
    [AttributeUsage(AttributeTargets.Property)]
    public class ReferencesAttribute : Attribute
    {
        /// <summary>
        /// Gets an array of parent attribute names on a foreign key constraint
        /// </summary>
        public string[] PropertyNames { get; protected set; }

        /// <summary>
        /// Gets the <see cref="ReferentialIntegrity"/> for this key restraint 
        /// when a row in the parent table is deleted
        /// </summary>
        public ReferentialIntegrity OnDelete { get; set; } = ReferentialIntegrity.NoAction;

        /// <summary>
        /// Gets the <see cref="ReferentialIntegrity"/> for this key restraint 
        /// when a row in the parent table is updated
        /// </summary>
        public ReferentialIntegrity OnUpdate { get; set; } = ReferentialIntegrity.NoAction;

        /// <summary>
        /// Creates a new instance of <see cref="ReferencesAttribute"/>
        /// </summary>
        /// <param name="attributes">The Parent Entity column name(s) in the parent table.</param>
        public ReferencesAttribute(params string[]  attributes)
        {
            this.PropertyNames = attributes;
        }
    }
}
