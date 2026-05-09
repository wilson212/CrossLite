using System;
using System.Collections;
using System.Collections.Generic;
using CrossLite.QueryBuilder;

namespace CrossLite
{
    public abstract class EntitySet<TEntity> : IEnumerable<TEntity>, IEnumerable
        where TEntity : EntityBase
    {
        // Define simple actions for add and remove operations
        public event Action<TEntity> EntityAdded;
        public event Action<TEntity> EntityRemoved;

        // Range events for bulk performance
        public event Action<IEnumerable<TEntity>> EntitiesAdded;
        public event Action<IEnumerable<TEntity>> EntitiesRemoved;

        // Internal helpers to trigger events safely
        protected void OnEntityAdded(TEntity entity) => EntityAdded?.Invoke(entity);
        protected void OnEntityRemoved(TEntity entity) => EntityRemoved?.Invoke(entity);

        // Triggered after AddRange/RemoveRange
        protected void OnEntitiesAdded(IEnumerable<TEntity> entities) => EntitiesAdded?.Invoke(entities);
        protected void OnEntitiesRemoved(IEnumerable<TEntity> entities) => EntitiesRemoved?.Invoke(entities);

        public abstract void Add(TEntity entity);

        public abstract void AddRange(IEnumerable<TEntity> entities);

        public abstract void Remove(TEntity entity);

        public abstract void RemoveRange(IEnumerable<TEntity> entities);

        public abstract void MoveTo(IEnumerable<TEntity> entities, EntitySet<TEntity> target);

        public abstract void Clear();

        public abstract bool Contains(TEntity entity);

        public abstract List<TEntity> FindAll(IWhereStatement where);

        public abstract int Count { get; }

        public abstract IEnumerator<TEntity> GetEnumerator();

        /// <summary>
        /// Lazy loads the child entities of a foreign key constraint
        /// </summary>
        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
