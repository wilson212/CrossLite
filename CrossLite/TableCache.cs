using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace CrossLite
{
    /// <summary>
    /// Provides functionality for managing and caching <see cref="TableMapping"/> objects
    /// associated with entity types. This static class ensures mappings are efficiently reused
    /// and avoids recreating mappings for the same entity types multiple times.
    /// </summary>
    public static class TableCache
    {
        /// <summary>
        /// Gets a list of Entity => table mappings
        /// </summary>
        private static ConcurrentDictionary<Type, TableMapping> Mappings { get; set; }

        static TableCache()
        {
            Mappings = new ConcurrentDictionary<Type, TableMapping>();
        }
        
        /// <summary>
        /// Registers a partially-constructed mapping so that re-entrant calls
        /// during construction can find it instead of getting null.
        /// Called from inside the TableMapping constructor.
        /// </summary>
        internal static void RegisterPartial(Type type, TableMapping mapping)
        {
            Mappings.TryAdd(type, mapping);
        }

        /// <summary>
        /// Gets or Creates a new <see cref="TableMapping"/> for the provided
        /// Entity Type provided.
        /// </summary>
        /// <param name="objType"></param>
        /// <returns></returns>
        public static TableMapping GetTableMap(Type objType)
        {
            // If it's already cached (fully or partially), return it
            if (Mappings.TryGetValue(objType, out var existing))
                return existing;

            // Construct — the constructor will call RegisterPartial() 
            // early, before processing child relationships
            var mapping = new TableMapping(objType);
        
            // Ensure final version is stored (RegisterPartial may have 
            // already added it; TryAdd is idempotent for same key)
            Mappings.TryAdd(objType, mapping);
            return Mappings[objType];
        }
    }
}
