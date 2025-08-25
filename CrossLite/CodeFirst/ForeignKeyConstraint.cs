using System;
using System.Collections.Generic;
using System.Linq;

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
            var invalid = inverseKey.PropertyNames.Except(parent.EntityProperties.Keys);
            if (invalid.Any())
            {
                throw new EntityException($"Parent Entity does not contain an attribute named \"{invalid.First()}\"");
            }
            invalid = foreignKey.PropertyNames.Except(child.EntityProperties.Keys);
            if (invalid.Any())
            {
                throw new EntityException($"Child Entity \"{ChildEntityType}\" does not contain an attribute named \"{invalid.First()}\"");
            }
        }

        /// <summary>
        /// Retrieves the set of column names that correspond to the reference attributes of the parent entity.
        /// </summary>
        /// <remarks>This method collects the column names associated with the reference attributes of the
        /// parent entity type as defined in the table mapping. The returned set contains unique column names.</remarks>
        /// <returns>A <see cref="HashSet{T}"/> containing the column names corresponding to the reference attributes. The set
        /// will be empty if no reference attributes are defined.</returns>
        public HashSet<string> GetReferenceColumnNames()
        {
            var returnSet = new HashSet<string>();
            TableMapping parent = EntityCache.GetTableMap(ParentEntityType);
            foreach (var attr in Reference.PropertyNames)
            {
                returnSet.Add(parent.EntityProperties[attr].ColumnName);
            }

            return returnSet;
        }

        /// <summary>
        /// Retrieves the names of the foreign key columns associated with the child entity type.
        /// </summary>
        /// <remarks>This method uses the child entity type and the foreign key attributes to determine
        /// the corresponding column names.</remarks>
        /// <returns>A <see cref="HashSet{T}"/> containing the names of the foreign key columns. The set will be empty if no
        /// foreign key columns are defined.</returns>
        public HashSet<string> GetForeignKeyColumnNames()
        {
            var returnSet = new HashSet<string>();
            TableMapping child = EntityCache.GetTableMap(ChildEntityType);
            foreach (var attr in ForeignKey.PropertyNames)
            {
                returnSet.Add(child.EntityProperties[attr].ColumnName);
            }
            return returnSet;
        }
    }
}
