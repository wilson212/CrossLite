using System;
using System.Linq;
using System.Reflection;

namespace CrossLite.CodeFirst
{
    /// <summary>
    /// Represents a One-to-Many foreign key constraint between a child and parent
    /// entity. These constraints are always attached to the child entity (many),
    /// and references the parent entity (one).
    /// </summary>
    /// <remarks>
    /// In relational database design, a Many-to-Many relationship is not allowed,
    /// and a One-to-One relationship does not usually require a foreign key.
    /// </remarks>
    public class ForeignKeyConstraint
    {
        /// <summary>
        /// The parent entity type on this constraint
        /// </summary>
        public Type ParentEntityType { get; protected set; }

        /// <summary>
        /// The child entity type on this constraint
        /// </summary>
        public Type ChildEntityType { get; protected set; }

        /// <summary>
        /// The property name that contains the ForeignEntityLoader<> object
        /// </summary>
        public string ChildPropertyName { get; protected set; }

        /// <summary>
        /// Gets the Parent entities attributes that are referenced in this foreign key
        /// </summary>
        public ReferencesAttribute Reference { get; protected set; }

        /// <summary>
        /// Gets the Child entities attributes that are referenced in this foreign key
        /// </summary>
        public ForeignKeyAttribute ForeignKey { get; protected set; }

        /// <summary>
        /// Creates a new instance of <see cref="ForeignKeyConstraint"/>
        /// </summary>
        /// <param name="child"></param>
        /// <param name="parentType"></param>
        /// <param name="foreignKey"></param>
        /// <param name="inverseKey"></param>
        public ForeignKeyConstraint(
            TableMapping child, 
            string childPropertyName, 
            Type parentType, 
            ForeignKeyAttribute foreignKey, 
            ReferencesAttribute inverseKey)
        {
            this.ForeignKey = foreignKey;
            this.Reference = inverseKey;
            this.ParentEntityType = parentType;
            this.ChildEntityType = child.EntityType;
            this.ChildPropertyName = childPropertyName;

            // Ensure the parent and child have the specified properties
            TableMapping parent = EntityCache.GetTableMap(parentType);
            var invalid = inverseKey.Attributes.Except(parent.DatabaseColumns.Keys);
            if (invalid.Count() > 0)
            {
                throw new EntityException($"Parent Entity does not contain an attribute named \"{invalid.First()}\"");
            }
            invalid = foreignKey.Attributes.Except(child.DatabaseColumns.Keys);
            if (invalid.Count() > 0)
            {
                throw new EntityException($"Child Entity \"{ChildEntityType}\" does not contain an attribute named \"{invalid.First()}\"");
            }
        }
    }
}
