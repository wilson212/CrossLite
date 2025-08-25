using System.Collections.Generic;

namespace CrossLite
{
    /// <summary>
    /// Serves as the base class for entities that track changes to their properties.
    /// </summary>
    /// <remarks>This class provides functionality to track modified properties, enabling scenarios such as
    /// change tracking in data persistence layers. Derived classes can use the <see cref="SetProperty{T}"/> method to
    /// update fields and automatically mark the corresponding property as modified.</remarks>
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
    }
}
