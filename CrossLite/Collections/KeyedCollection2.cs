using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace CrossLite.Collections
{
    /// <summary>
    /// Represents a collection of objects that are keyed by a specific property, with additional functionality for
    /// sorting and searching.
    /// </summary>
    /// <remarks>This collection extends <see cref="KeyedCollection{TKey, TItem}"/> by providing additional
    /// methods for sorting elements by their keys or values, as well as searching for elements by their keys. The key
    /// for each element is determined by a delegate provided during construction.</remarks>
    /// <typeparam name="TKey">The type of the key used to identify elements in the collection.</typeparam>
    /// <typeparam name="TItem">The type of the elements in the collection.</typeparam>
    internal class KeyedCollection2<TKey, TItem> : KeyedCollection<TKey, TItem>
    {
        /// <summary>
        /// The delegate used to get the key from a <typeparamref name="TItem"/> value
        /// </summary>
        private Func<TItem, TKey> _getKeyForItemDelegate;

        /// <summary>
        /// Creates a new instance of <see cref="KeyedCollection2{TKey, TItem}"/>
        /// </summary>
        /// <param name="getKeyForItemDelegate">
        /// The delegate method used to extract the key from the specified element.
        /// </param>
        public KeyedCollection2(Func<TItem, TKey> getKeyForItemDelegate) : base()
        {
            // Ensure the delegate is not null
            if (getKeyForItemDelegate == null)
                throw new ArgumentNullException("Delegate passed cannot be null");

            _getKeyForItemDelegate = getKeyForItemDelegate;
        }

        /// <summary>
        /// Creates a new instance of <see cref="KeyedCollection2{TKey, TItem}"/>
        /// </summary>
        /// <param name="getKeyForItemDelegate">
        /// The delegate method used to extract the key from the specified element.
        /// </param>
        /// <param name="comparer">
        /// The implementation of the <see cref="IEqualityComparer{TKey}"/> generic interface to use when comparing keys
        /// </param>
        public KeyedCollection2(Func<TItem, TKey> getKeyForItemDelegate, IEqualityComparer<TKey> comparer) : base(comparer)
        {
            // Ensure the delegate is not null
            if (getKeyForItemDelegate == null)
                throw new ArgumentNullException("Delegate passed cannot be null");

            _getKeyForItemDelegate = getKeyForItemDelegate;
        }

        /// <summary>
        /// Extracts the key from the specified element.
        /// </summary>
        /// <remarks>MUST be overriden for this collection to work.</remarks>
        /// <param name="item">The element from which to extract the key.</param>
        /// <returns></returns>
        protected override TKey GetKeyForItem(TItem item) => _getKeyForItemDelegate(item);

        /// <summary>
        /// Sorts the elements in the <see cref="KeyedCollection2{TKey, TItem}"/> by their key value,
        /// using the default <see cref="IComparer{TKey}"/>.
        /// </summary>
        public void SortByKeys()
        {
            var comparer = Comparer<TKey>.Default;
            SortByKeys(comparer);
        }

        /// <summary>
        /// Sorts the elements in the <see cref="KeyedCollection2{TKey, TItem}"/> by their key value,
        /// using the specified <see cref="IComparer{TKey}"/>.
        /// </summary>
        public void SortByKeys(IComparer<TKey> keyComparer)
        {
            var comparer = new Comparer2<TItem>((x, y) => keyComparer.Compare(GetKeyForItem(x), GetKeyForItem(y)));
            Sort(comparer);
        }

        /// <summary>
        /// Sorts the elements in the <see cref="KeyedCollection2{TKey, TItem}"/> by their key value,
        /// using the specified <see cref="Comparison{TKey}"/>.
        /// </summary>
        /// <param name="keyComparison"></param>
        public void SortByKeys(Comparison<TKey> keyComparison)
        {
            var comparer = new Comparer2<TItem>((x, y) => keyComparison(GetKeyForItem(x), GetKeyForItem(y)));
            Sort(comparer);
        }

        /// <summary>
        /// Sorts the elements in the <see cref="KeyedCollection2{TKey, TItem}"/> by their values,
        /// using the default <see cref="Comparer<TItem>"/>.
        /// </summary>
        public void Sort()
        {
            var comparer = Comparer<TItem>.Default;
            Sort(comparer);
        }

        /// <summary>
        /// Sorts the elements in the <see cref="KeyedCollection2{TKey, TItem}"/> by their values,
        /// using the specified <see cref="Comparison{TItem}"/>.
        /// </summary>
        /// <param name="comparison"></param>
        public void Sort(Comparison<TItem> comparison)
        {
            var newComparer = new Comparer2<TItem>((x, y) => comparison(x, y));
            Sort(newComparer);
        }

        /// <summary>
        /// Sorts the elements in the <see cref="KeyedCollection2{TKey, TItem}"/> by their values,
        /// using the specified <see cref="IComparer{TItem}"/>.
        /// </summary>
        /// <param name="comparer"></param>
        public void Sort(IComparer<TItem> comparer)
        {
            List<TItem> list = base.Items as List<TItem>;
            if (list != null)
                list.Sort(comparer);
        }

        /// <summary>
        /// Searches the internal list for an element with the key that matches the keyToFind, 
        /// and returns the zero based index of the element, or -1 if not found.
        /// </summary>
        /// <remarks>
        /// This method is an O(1) operation if the key does not exist in the internal dictionary.
        /// Otherwise, this method performs a linear search; therefore, this method is an O(n) operation, where n is Count.
        /// </remarks>
        public int GetItemIndexByKey(TKey keyToFind)
        {
            // Skip the search if the key doesnt exist!
            if (!Contains(keyToFind)) return -1;

            // Perform search using the specified comparer
            var list = base.Items as List<TItem>;
            return list.FindIndex(existingItem => Comparer.Equals(GetKeyForItem(existingItem), keyToFind)); 
        }

        /// <summary>
        /// Do NOT use this method, it is for testing purposes only
        /// </summary>
        internal void SetValueByKey(TKey key, TItem value)
        {
            if (Dictionary == null || !Dictionary.ContainsKey(key))
            {
               base.Add(value);
            }
            else
            {
                Dictionary[key] = value;
            }
        }
    }
}
