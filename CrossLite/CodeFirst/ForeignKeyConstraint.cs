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
        /// A collection of column names mapped to the parent entity's properties
        /// that are referenced by the foreign key constraint.
        /// </summary>
        private HashSet<string> _referenceColumnNames;

        /// <summary>
        /// Stores the column names within the child table that act as foreign key references
        /// to the primary key of the parent table. This is lazily initialized when accessed
        /// and represents the actual database columns involved in the constraint.
        /// </summary>
        private HashSet<string> _foreignKeyColumnNames;

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
            TableMapping parent = TableCache.GetTableMap(parentType);
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
            if (_referenceColumnNames != null) return _referenceColumnNames;

            var result = new HashSet<string>();
            TableMapping parent = TableCache.GetTableMap(ParentEntityType);
            foreach (var attr in Reference.PropertyNames)
            {
                result.Add(parent.EntityProperties[attr].ColumnName);
            }

            _referenceColumnNames = result;
            return _referenceColumnNames;
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
            if (_foreignKeyColumnNames != null) return _foreignKeyColumnNames;

            var result = new HashSet<string>();
            TableMapping child = TableCache.GetTableMap(ChildEntityType);
            foreach (var attr in ForeignKey.PropertyNames)
            {
                result.Add(child.EntityProperties[attr].ColumnName);
            }

            _foreignKeyColumnNames = result;
            return _foreignKeyColumnNames;
        }
    }
}
