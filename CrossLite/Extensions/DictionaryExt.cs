using System.Collections.Generic;

namespace CrossLite.Extensions
{
    /// <summary>
    /// Provides extension methods for <see cref="IDictionary{TKey, TValue}"/> to enhance its functionality.
    /// </summary>
    internal static class DictionaryExt
    {
        /// <summary>
        /// Renames a key in the dictionary by transferring its associated value to a new key.
        /// </summary>
        /// <remarks>This method removes the entry associated with <paramref name="fromKey"/> and adds a
        /// new entry with  <paramref name="toKey"/> using the same value. The dictionary must not contain <paramref
        /// name="toKey"/>  prior to calling this method.</remarks>
        /// <typeparam name="TKey"></typeparam>
        /// <typeparam name="TValue"></typeparam>
        /// <param name="dic">The dictionary in which the key will be renamed.</param>
        /// <param name="fromKey">The existing key to be renamed. Must exist in the dictionary.</param>
        /// <param name="toKey">The new key to assign the value to. Must not already exist in the dictionary.</param>
        public static void RenameKey<TKey, TValue>(this IDictionary<TKey, TValue> dic, TKey fromKey, TKey toKey)
        {
            TValue value = dic[fromKey];
            dic.Remove(fromKey);
            dic[toKey] = value;
        }
    }
}
