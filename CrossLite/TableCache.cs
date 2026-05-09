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
        /// Gets or Creates a new <see cref="TableMapping"/> for the provided
        /// Entity Type provided.
        /// </summary>
        /// <param name="objType"></param>
        /// <returns></returns>
        public static TableMapping GetTableMap(Type objType)
        {
            return Mappings.GetOrAdd(objType, type => new TableMapping(type));
        }
    }
}
