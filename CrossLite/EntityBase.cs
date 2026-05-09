using System;
using System.Collections.Generic;
using System.Linq;

namespace CrossLite
{
    /// <summary>
    /// Serves as the base class for entities that track changes to their properties.
    /// </summary>
    /// <remarks>This class provides functionality to track modified properties, enabling scenarios such as
    /// change tracking in data persistence layers.</remarks>
    public abstract class EntityBase
    {
        /// <summary>
        /// Gets or sets the current state of the entity.
        /// </summary>
        internal EntityState State { get; set; } = EntityState.New;

        /// <summary>
        /// A HashSet is efficient for storing the unique names of changed properties.
        /// </summary>
        internal HashSet<string> DirtyProperties { get; } = new HashSet<string>();

        /// <summary>
        /// Gets a read-only collection of property names that have been modified.
        /// </summary>
        protected IReadOnlyCollection<string> ChangedProperties => DirtyProperties;

        /// <summary>
        /// Retrieves the unique <see cref="EntityKey"/> for the current entity instance based on its primary key values.
        /// </summary>
        /// <returns>
        /// An <see cref="EntityKey"/> representing the primary key of the entity. This can either be a single value or a composite key.
        /// </returns>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the entity has not been persisted to the database, and the primary key is auto-incremented,
        /// resulting in a meaningless default value.
        /// </exception>
        /// <exception cref="NotSupportedException">
        /// Thrown for composite keys with a number of columns that are not supported by the implementation.
        /// </exception>
        public EntityKey GetEntityKey()
        {
            var table = TableCache.GetTableMap(GetType());
            var pks = table.PrimaryKeys;

            // Guard: if the entity has never been persisted and uses an auto-increment PK,
            // the PK value is a meaningless default (0). Returning it would create
            // colliding keys across all unsaved entities of this type.
            if (State == EntityState.New && table.HasRowIdAlias)
            {
                throw new InvalidOperationException(
                    $"Cannot retrieve EntityKey for a '{table.EntityType.Name}' entity that has not been saved to the database. " +
                    "The auto-increment primary key has not been assigned yet.");
            }

            if (pks.Count == 1)
                return new EntityKey(pks.First().GetValue(this));

            var values = new object[pks.Count];
            int i = 0;
            foreach (var pk in pks)
                values[i++] = pk.GetValue(this);

            return values.Length switch
            {
                2 => new EntityKey(values[0], values[1]),
                3 => new EntityKey(values[0], values[1], values[2]),
                4 => new EntityKey(values[0], values[1], values[2], values[3]),
                5 => new EntityKey(values[0], values[1], values[2], values[3], values[4]),
                _ => throw new NotSupportedException($"Composite keys with {values.Length} columns are not supported.")
            };
        }
    }
}
