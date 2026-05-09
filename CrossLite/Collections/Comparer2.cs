using System;
using System.Collections.Generic;

namespace CrossLite.Collections
{
    /// <summary>
    /// Provides a custom implementation of a comparer for objects of type <typeparamref name="T"/>  using a specified
    /// comparison delegate.
    /// </summary>
    /// <remarks>This class allows you to define a comparer by providing a <see cref="Comparison{T}"/>
    /// delegate,  enabling custom comparison logic without the need to implement the <see cref="IComparer{T}"/>
    /// interface manually.</remarks>
    /// <typeparam name="T">The type of objects to compare.</typeparam>
    internal class Comparer2<T> : Comparer<T>
    {
        //private readonly Func<TEntity, TEntity, int> _compareFunction;
        private readonly Comparison<T> _compareFunction;

        #region Constructors

        public Comparer2(Comparison<T> comparison)
        {
            if (comparison == null) throw new ArgumentNullException("comparison");
            _compareFunction = comparison;
        }

        #endregion

        public override int Compare(T arg1, T arg2)
        {
            return _compareFunction(arg1, arg2);
        }
    }
}
