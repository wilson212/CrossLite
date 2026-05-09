using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Linq;

namespace CrossLite.Collections
{
    /// <summary>
    /// A dictionary object that allows rapid hash lookups using keys, but also
    /// maintains the key insertion order so that values can be retrieved by
    /// key index.
    /// </summary>
    /// <seealso cref="http://stackoverflow.com/a/9844528/841267"/>
    /// <seealso cref="https://github.com/mattmc3/dotmore/blob/master/dotmore/Collections/Generic/OrderedDictionary.cs"/>
    [DebuggerDisplay("Count = {Count}"), DebuggerTypeProxy(typeof(OrderedDictionaryDebugView))]
    public class OrderedDictionary<TKey, TValue> : IOrderedDictionary<TKey, TValue>
    {
        #region Fields/Properties

        /// <summary>
        /// The internal collection of elements
        /// </summary>
        private KeyedCollection2<TKey, KeyValuePair<TKey, TValue>> Collection;

        /// <summary>
        /// Gets or sets the value associated with the specified key.
        /// </summary>
        /// <param name="key">The key associated with the value to get or set.</param>
        /// <remarks>
        /// Retrieving the value of this property is an O(1) operation; 
        /// however setting the value is an O(n) operation, where n is Count.
        /// </remarks>
        public TValue this[TKey key]
        {
            get { return GetValue(key); }
            set { SetValue(key, value); }
        }

        /// <summary>
        /// Gets or sets the value at the specified index.
        /// </summary>
        /// <param name="index">The index of the value to get or set.</param>
        /// <remarks>
        /// Retrieving the value of this property is an O(1) operation; setting the property is also an O(1) operation.
        /// </remarks>
        public TValue this[int index]
        {
            get { return GetItem(index).Value; }
            set { SetItem(index, value); }
        }

        /// <summary>
        /// Gets the number of elements contained in in the <see cref="OrderedDictionary{TKey, TValue}"/>
        /// </summary>
        public int Count => Collection.Count;

        /// <summary>
        /// Gets a collection containing the keys in the <see cref="OrderedDictionary{TKey, TValue}"/>
        /// </summary>
        public ICollection<TKey> Keys => Collection.Select(x => x.Key).ToArray();

        /// <summary>
        /// Gets a collection containing the values in the <see cref="OrderedDictionary{TKey, TValue}"/>
        /// </summary>
        public ICollection<TValue> Values => Collection.Select(x => x.Value).ToArray();

        /// <summary>
        /// Gets the <see cref="IEqualityComparer{TKey}"/> that is used to determine equality of keys for the dictionary.
        /// </summary>
        public IEqualityComparer<TKey> Comparer { get;  private set; }

        #endregion

        #region Constructors

        /// <summary>
        /// Creates a new instance of <see cref="OrderedDictionary{TKey, TValue}"/>
        /// </summary>
        public OrderedDictionary()
        {
            Initialize();
        }

        /// <summary>
        /// Creates a new instance of <see cref="OrderedDictionary{TKey, TValue}"/> using
        /// the provided <see cref="IEqualityComparer{TKey}"/> to compare keys.
        /// </summary>
        public OrderedDictionary(IEqualityComparer<TKey> comparer)
        {
            Initialize(comparer);
        }

        /// <summary>
        /// Creates a new instance of <see cref="OrderedDictionary{TKey, TValue}"/> with the items
        /// provided in the specified <see cref="IOrderedDictionary{TKey, TValue}"/>
        /// </summary>
        /// <param name="dictionary">Elements to add to this <see cref="OrderedDictionary{TKey, TValue}"/></param>
        public OrderedDictionary(IOrderedDictionary<TKey, TValue> dictionary)
        {
            Initialize();
            foreach (KeyValuePair<TKey, TValue> pair in dictionary)
                Collection.Add(pair);
        }

        /// <summary>
        /// Creates a new instance of <see cref="OrderedDictionary{TKey, TValue}"/> with the items
        /// provided in the specified <see cref="IOrderedDictionary{TKey, TValue}"/>, using 
        /// the provided <see cref="IEqualityComparer{TKey}"/> to compare keys.
        /// </summary>
        public OrderedDictionary(IOrderedDictionary<TKey, TValue> dictionary, IEqualityComparer<TKey> comparer)
        {
            Initialize(comparer);
            foreach (KeyValuePair<TKey, TValue> pair in dictionary)
                Collection.Add(pair);
        }

        #endregion

        #region Methods

        private void Initialize(IEqualityComparer<TKey> comparer = null)
        {
            this.Comparer = comparer;
            if (comparer != null)
                Collection = new KeyedCollection2<TKey, KeyValuePair<TKey, TValue>>(x => x.Key, comparer);
            else
                Collection = new KeyedCollection2<TKey, KeyValuePair<TKey, TValue>>(x => x.Key);
        }

        /// <summary>
        /// Adds the specified key and value to the dictionary.
        /// </summary>
        /// <param name="key">The key of the element to add.</param>
        /// <param name="value">The value of the element to add. The value can be null for reference types.</param>
        public void Add(TKey key, TValue value)
        {
            var kvp = new KeyValuePair<TKey, TValue>(key, value);
            if (ContainsKey(key))
                throw new ArgumentException($@"An element with the key ""{key}"" already exists ", "key");

            Collection.Add(kvp);
        }

        /// <summary>
        /// Removes all keys and values from the <see cref="OrderedDictionary{TKey, TValue}"/>.
        /// </summary>
        public void Clear() => Collection.Clear();

        /// <summary>
        /// Inserts an element at the specified index in the <see cref="OrderedDictionary{TKey, TValue}"/>
        /// </summary>
        /// <remarks>This method is an O(n) operation, where n is Count.</remarks>
        /// <param name="index">The index to add the element too</param>
        /// <param name="key">The key of the element</param>
        /// <param name="value">The value of the element</param>
        public void Insert(int index, TKey key, TValue value) 
            => Collection.Insert(index, new KeyValuePair<TKey, TValue>(key, value));

        /// <summary>
        /// Searches for the specified object and returns the zero-based index of the first occurrence within 
        /// the entire <see cref="OrderedDictionary{TKey, TValue}"/>
        /// </summary>
        /// <remarks>
        /// This method is an O(1) operation if the key does not exist in the internal dictionary.
        /// Otherwise, this method performs a linear search; therefore, this method is an O(n) operation, where n is Count.
        /// </remarks>
        /// <param name="key">The key of the element</param>
        public int IndexOf(TKey key) => Collection.GetItemIndexByKey(key);

        /// <summary>
        /// Determines whether the <see cref="OrderedDictionary{TKey, TValue}"/> contains the specified value.
        /// </summary>
        /// <remarks>This method is an O(n) operation, where n is Count.</remarks>
        /// <param name="value">The value to locate in the <see cref="OrderedDictionary{TKey, TValue}"/></param>
        /// <returns>
        /// true if the <see cref="OrderedDictionary{TKey, TValue}"/> contains an element with the specified value; 
        /// otherwise, false.
        /// </returns>
        public bool ContainsValue(TValue value) => this.Values.Contains(value);

        /// <summary>
        /// Determines whether the <see cref="OrderedDictionary{TKey, TValue}"/> contains the specified value.
        /// </summary>
        /// <remarks>This method is an O(n) operation, where n is Count.</remarks>
        /// <param name="value">The value to locate in the <see cref="OrderedDictionary{TKey, TValue}"/></param>
        /// <param name="comparer">
        /// Gets the <see cref="IEqualityComparer{Value}"/> that is used to determine equality of values for the dictionary.
        /// </param>
        /// <returns>
        /// true if the <see cref="OrderedDictionary{TKey, TValue}"/> contains an element with the specified value; 
        /// otherwise, false.
        /// </returns>
        public bool ContainsValue(TValue value, IEqualityComparer<TValue> comparer) => Values.Contains(value, comparer);

        /// <summary>
        /// Determines whether the <see cref="OrderedDictionary{TKey, TValue}"/> contains the specified key.
        /// </summary>
        /// <remarks>This is an O(1) operation.</remarks>
        /// <param name="key">The key to locate in the <see cref="OrderedDictionary{TKey, TValue}"/></param>
        /// <returns>
        /// true if the <see cref="OrderedDictionary{TKey, TValue}"/> contains an element with the specified key; 
        /// otherwise, false.
        /// </returns>
        public bool ContainsKey(TKey key) => Collection.Contains(key);

        /// <summary>
        /// Returns the element at the specified index.
        /// </summary>
        /// <remarks>This is an O(1) operation</remarks>
        /// <param name="index">The zero-based index of the element to get</param>
        public KeyValuePair<TKey, TValue> GetItem(int index)
        {
            if (index < 0 || index >= Collection.Count)
                throw new ArgumentException($"The index was outside the bounds of the dictionary: {index}");
            
            return Collection[index];
        }

        /// <summary>
        /// Sets the value at the index specified.
        /// </summary>
        /// <remarks>This is an O(1) operation</remarks>
        /// <param name="index">The index of the value desired</param>
        /// <param name="value">The value to set</param>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when the index specified does not refer to a KeyValuePair in this object
        /// </exception>
        public void SetItem(int index, TValue value)
        {
            if (index < 0 || index >= Collection.Count)
                throw new ArgumentException($"The index is outside the bounds of the dictionary: {index}");
            
            Collection[index] = new KeyValuePair<TKey, TValue>(Collection[index].Key, value);
        }

        /// <summary>
        /// Removes the value with the specified key from the <see cref="OrderedDictionary{TKey, TValue}"/>
        /// </summary>
        /// <remarks>
        /// This method is an O(n) operation, where n is Count.
        /// </remarks>
        /// <param name="key">The key of the element to remove</param>
        /// <returns>
        /// true if item is successfully removed; otherwise, false. This method also returns false if item was not found.
        /// </returns>
        public bool Remove(TKey key) => Collection.Remove(key);

        /// <summary>
        /// Removes the element at the specified index of the <see cref="OrderedDictionary{TKey, TValue}"/>
        /// </summary>
        /// <remarks>This method is an O(n) operation, where n is Count.</remarks>
        /// <param name="index">The zero-based index of the element to remove.</param>
        public void RemoveAt(int index)
        {
            if (index < 0 || index >= Collection.Count)
                throw new ArgumentException($"The index is outside the bounds of the dictionary: {index}");
            
            Collection.RemoveAt(index);
        }

        /// <summary>
        /// Gets the value associated with the specified key.
        /// </summary>
        /// <remarks>
        /// Retrieving the value of this property is an O(1) operation.
        /// </remarks>
        /// <param name="key">The key associated with the value to get.</param>
        public TValue GetValue(TKey key)
        {
            if (Collection.Contains(key) == false)
                throw new ArgumentException($"The given key is not present in the dictionary: {key}");

            return Collection[key].Value;
        }

        /// <summary>
        /// Sets the value associated with the specified key.
        /// </summary>
        /// <remarks>
        /// This is an O(n) operation if the key already exists, since IndexOf performs a linear search all of 
        /// the values to get the index. Otherwise, this is an O(1) operation.
        /// </remarks>
        /// <param name="key">The key associated with the value to set.</param>
        /// <param name="value">The the value to set.</param>
        public void SetValue(TKey key, TValue value)
        {
            var kvp = new KeyValuePair<TKey, TValue>(key, value);
            var idx = IndexOf(key);
            if (idx > -1)
                Collection[idx] = kvp;
            else
                Collection.Add(kvp);
        }

        /// <summary>
        /// Gets the value associated with the specified key.
        /// </summary>
        /// <param name="key">The key of the value to get.</param>
        /// <param name="value">
        /// When this method returns, contains the value associated with the specified key, if the key is found; 
        /// otherwise, the default value for the type of the value parameter. This parameter is passed uninitialized.
        /// </param>
        /// <returns>true if the element exists in the collection, otherwise false.</returns>
        public bool TryGetValue(TKey key, out TValue value)
        {
            if (Collection.Contains(key))
            {
                value = Collection[key].Value;
                return true;
            }
            else
            {
                value = default(TValue);
                return false;
            }
        }

        public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator() => Collection.GetEnumerator();

        #endregion

        #region sorting

        /// <summary>
        /// Sorts the elements in the <see cref="OrderedDictionary{TKey, TValue}"/> by their key value.
        /// </summary>
        public void SortKeys() => Collection.SortByKeys();

        /// <summary>
        /// Sorts the elements in the <see cref="OrderedDictionary{TKey, TValue}"/> by their key value,
        /// using the specified <see cref="IComparer{TKey}"/>.
        /// </summary>
        /// <param name="comparer"></param>
        public void SortKeys(IComparer<TKey> comparer) => Collection.SortByKeys(comparer);

        /// <summary>
        /// Sorts the elements in the <see cref="OrderedDictionary{TKey, TValue}"/> by their key value,
        /// using the specified <see cref="Comparison{TKey}"/>.
        /// </summary>
        /// <param name="comparison"></param>
        public void SortKeys(Comparison<TKey> comparison) => Collection.SortByKeys(comparison);

        /// <summary>
        /// Sorts the elements in the <see cref="OrderedDictionary{TKey, TValue}"/> by their values.
        /// </summary>
        public void SortValues()
        {
            var comparer = Comparer<TValue>.Default;
            SortValues(comparer);
        }

        /// <summary>
        /// Sorts the elements in the <see cref="OrderedDictionary{TKey, TValue}"/> by their values,
        /// using the specified <see cref="IComparer{TKey}"/>.
        /// </summary>
        /// <param name="comparer"></param>
        public void SortValues(IComparer<TValue> comparer) => Collection.Sort((x, y) => comparer.Compare(x.Value, y.Value));

        /// <summary>
        /// Sorts the elements in the <see cref="OrderedDictionary{TKey, TValue}"/> by their values,
        /// using the specified <see cref="Comparison{TKey}"/>.
        /// </summary>
        /// <param name="comparison"></param>
        public void SortValues(Comparison<TValue> comparison) => Collection.Sort((x, y) => comparison(x.Value, y.Value));
        
        #endregion

        #region ICollection<KeyValuePair<TKey, TValue>>

        void ICollection<KeyValuePair<TKey, TValue>>.Add(KeyValuePair<TKey, TValue> item) => Collection.Add(item);

        void ICollection<KeyValuePair<TKey, TValue>>.Clear() => Collection.Clear();

        bool ICollection<KeyValuePair<TKey, TValue>>.Contains(KeyValuePair<TKey, TValue> item) => Collection.Contains(item);

        void ICollection<KeyValuePair<TKey, TValue>>.CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex)
            => Collection.CopyTo(array, arrayIndex);

        int ICollection<KeyValuePair<TKey, TValue>>.Count => Collection.Count;

        bool ICollection<KeyValuePair<TKey, TValue>>.IsReadOnly => false;

        bool ICollection<KeyValuePair<TKey, TValue>>.Remove(KeyValuePair<TKey, TValue> item) => Collection.Remove(item);

        #endregion

        #region IEnumerable<KeyValuePair<TKey, TValue>>

        IEnumerator<KeyValuePair<TKey, TValue>> IEnumerable<KeyValuePair<TKey, TValue>>.GetEnumerator()
            => GetEnumerator();

        #endregion

        #region IEnumerable

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        #endregion

        #region IOrderedDictionary

        IDictionaryEnumerator IOrderedDictionary.GetEnumerator() => new DictionaryEnumerator<TKey, TValue>(this);

        void IOrderedDictionary.Insert(int index, object key, object value) => Insert(index, (TKey)key, (TValue)value);

        void IOrderedDictionary.RemoveAt(int index) => RemoveAt(index);

        object IOrderedDictionary.this[int index]
        {
            get { return this[index]; }
            set { this[index] = (TValue)value; }
        }

        #endregion

        #region IDictionary

        void IDictionary.Add(object key, object value) => Add((TKey)key, (TValue)value);

        void IDictionary.Clear() => Clear();

        bool IDictionary.Contains(object key) => Collection.Contains((TKey)key);

        IDictionaryEnumerator IDictionary.GetEnumerator() => new DictionaryEnumerator<TKey, TValue>(this);

        bool IDictionary.IsFixedSize => false;

        bool IDictionary.IsReadOnly => false;

        ICollection IDictionary.Keys => (ICollection)this.Keys;

        void IDictionary.Remove(object key) => Remove((TKey)key);

        ICollection IDictionary.Values => (ICollection)this.Values;

        object IDictionary.this[object key]
        {
            get { return this[(TKey)key]; }
            set { this[(TKey)key] = (TValue)value; }
        }

        #endregion

        #region ICollection

        void ICollection.CopyTo(Array array, int index) => ((ICollection)Collection).CopyTo(array, index);

        int ICollection.Count => ((ICollection)Collection).Count;

        bool ICollection.IsSynchronized => ((ICollection)Collection).IsSynchronized;

        object ICollection.SyncRoot => ((ICollection)Collection).SyncRoot;

        #endregion
    }

    #region Debugging

    [DebuggerDisplay("{Value}", Name = "[{Index}]: {Key}")]
    internal class IndexedKeyValuePairs
    {
        public IDictionary Dictionary { get; private set; }
        public int Index { get; private set; }
        public object Key { get; private set; }
        public object Value { get; private set; }

        public IndexedKeyValuePairs(IDictionary dictionary, int index, object key, object value)
        {
            Index = index;
            Value = value;
            Key = key;
            Dictionary = dictionary;
        }
    }

    internal class OrderedDictionaryDebugView
    {

        private IOrderedDictionary _dict;
        public OrderedDictionaryDebugView(IOrderedDictionary dict)
        {
            _dict = dict;
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Collapsed)]
        public IndexedKeyValuePairs[] IndexedKeyValuePairs
        {
            get
            {
                IndexedKeyValuePairs[] nkeys = new IndexedKeyValuePairs[_dict.Count];

                int i = 0;
                foreach (object key in _dict.Keys)
                {
                    nkeys[i] = new IndexedKeyValuePairs(_dict, i, key, _dict[key]);
                    i += 1;
                }
                return nkeys;
            }
        }
    }

    #endregion
}
