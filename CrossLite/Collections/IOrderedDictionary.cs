using System.Collections.Generic;
using System.Collections.Specialized;

namespace CrossLite.Collections
{
    /// <summary>
    /// Represents a collection of key/value pairs that are accessible by the key or index,  and maintains the order in
    /// which the items were added.
    /// </summary>
    /// <remarks>This interface extends <see cref="IDictionary{TKey, TValue}"/> by adding methods and
    /// properties  to access elements by their index in the collection. It also provides methods to insert or remove 
    /// elements at specific indices, and to retrieve the index of a specific key. <para> The order of elements in the
    /// collection is preserved based on the sequence in which they were added.  Modifications to the collection, such
    /// as inserting or removing elements, will adjust the indices  of subsequent elements accordingly.
    /// </para></remarks>
    /// <typeparam name="TKey">The type of the keys in the dictionary.</typeparam>
    /// <typeparam name="TValue">The type of the values in the dictionary.</typeparam>
    public interface IOrderedDictionary<TKey, TValue> : IDictionary<TKey, TValue>, IOrderedDictionary
    {
        new TValue this[int index] { get; set; }
        new TValue this[TKey key] { get; set; }
        new int Count { get; }
        new ICollection<TKey> Keys { get; }
        new ICollection<TValue> Values { get; }
        new void Add(TKey key, TValue value);
        new void Clear();
        void Insert(int index, TKey key, TValue value);
        int IndexOf(TKey key);
        bool ContainsValue(TValue value);
        bool ContainsValue(TValue value, IEqualityComparer<TValue> comparer);
        new bool ContainsKey(TKey key);
        new IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator();
        new bool Remove(TKey key);
        new void RemoveAt(int index);
        new bool TryGetValue(TKey key, out TValue value);
        TValue GetValue(TKey key);
        void SetValue(TKey key, TValue value);
        KeyValuePair<TKey, TValue> GetItem(int index);
        void SetItem(int index, TValue value);
    }
}
