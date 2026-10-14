using System.Collections.Generic;
using UnityEngine;

namespace com.pyroduck.eggheads.Runtime.Scripts.Pool
{
    /// <summary>
    /// Keys pools by <b>prefab reference</b>. Multiple prefab variants using the
    /// same Component type stay isolated from each other.
    /// </summary>
    public class PoolManager
    {
        private readonly Transform _root;

        // prefab -> pool dedicated to that prefab
        private readonly Dictionary<Component, IObjectPool> _poolsByPrefab =
            new Dictionary<Component, IObjectPool>();

        // instance -> pool that created it, for fast Return lookup
        private readonly Dictionary<Component, IObjectPool> _poolByInstance =
            new Dictionary<Component, IObjectPool>();

        public PoolManager(Transform root)
        {
            _root = root;
        }

        /// <summary>
        /// Creates a pool for the prefab when needed. When <paramref name="force"/>
        /// is true, the existing pool is cleared and rebuilt.
        /// </summary>
        public void RegisterPool<T>(T prefab, bool force = false) where T : Component, IPoolable
        {
            if (prefab == null) return;

            if (_poolsByPrefab.TryGetValue(prefab, out var existing))
            {
                if (!force) return;
                existing.Clear();
            }

            var pool = new ObjectPool<T>(prefab, _root, RegisterInstance);
            _poolsByPrefab[prefab] = pool;
        }

        private void RegisterInstance(Component instance, IObjectPool pool)
        {
            if (instance == null) return;
            _poolByInstance[instance] = pool;
        }

        /// <summary>
        /// Gets an instance from the prefab-specific pool, creating that pool if needed.
        /// </summary>
        public T Get<T>(T prefab, Vector3 pos, Quaternion rot) where T : Component, IPoolable
        {
            if (prefab == null)
            {
                Debug.LogError("[PoolManager] Get was called with a null prefab.");
                return null;
            }

            if (!_poolsByPrefab.TryGetValue(prefab, out var pool))
            {
                RegisterPool(prefab);
                pool = _poolsByPrefab[prefab];
            }

            return ((ObjectPool<T>)pool).Get(pos, rot);
        }

        /// <summary>
        /// Returns an instance to the pool that created it. Instances from outside
        /// this manager are safely destroyed.
        /// </summary>
        public void Return<T>(T obj) where T : Component, IPoolable
        {
            if (obj == null) return;

            if (_poolByInstance.TryGetValue(obj, out var pool))
            {
                pool.ReturnObject(obj);
            }
            else
            {
                Object.Destroy(obj.gameObject);
            }
        }

        /// <summary>
        /// Destroys pooled instances and clears all registrations.
        /// </summary>
        public void Clear()
        {
            foreach (var pool in _poolsByPrefab.Values)
                pool.Clear();

            _poolsByPrefab.Clear();
            _poolByInstance.Clear();

            if (_root != null)
            {
                for (int i = _root.childCount - 1; i >= 0; i--)
                    Object.Destroy(_root.GetChild(i).gameObject);
            }
        }
    }
}
