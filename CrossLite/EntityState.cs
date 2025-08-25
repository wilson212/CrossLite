namespace CrossLite
{
    /// <summary>
    /// Represents the state of an entity in the application's lifecycle.
    /// </summary>
    /// <remarks>This enumeration is typically used to track the current status of an entity, such as whether
    /// it is new, being loaded, modified, or marked for deletion.</remarks>
    public enum EntityState
    {
        /// <summary>
        /// Indicates that the entity is newly created and has not yet been persisted to the database.
        /// </summary>
        New,

        /// <summary>
        /// Indicates that the entity is currently being loaded from the database by the <see cref="SQLiteContext.CreateEntity{TEntity}()"/> method."
        /// </summary>
        Loading,

        /// <summary>
        /// Indicates that the entity is in a fresh state, meaning it has been loaded from the database, and no changes have been made to it since loading.
        /// </summary>
        Fresh,

        /// <summary>
        /// Indicates that the entity has been modified since it was loaded from the database.
        /// </summary>
        Modified,

        /// <summary>
        /// Indicates that the entity has been marked for deletion from the database.
        /// </summary>
        Deleted
    }
}
