using System.Collections;
using System.Collections.Generic;

namespace CrossLite
{
    public abstract class EntitySet<TEntity> : IEnumerable<TEntity>, IEnumerable
        where TEntity : EntityBase
    {
        public abstract IEnumerator<TEntity> GetEnumerator();

        public abstract void Add(TEntity entity);

        public abstract void Remove(TEntity entity);

        public abstract void Clear();

        public abstract bool Contains(TEntity entity);

        public abstract int Count { get; }

        /// <summary>
        /// Lazy loads the child entities of a foreign key constraint
        /// </summary>
        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
