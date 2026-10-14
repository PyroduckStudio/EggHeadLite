using System;
using System.Collections.Generic;
using UnityEngine;

namespace com.pyroduck.eggheads.Runtime.Scripts.Pool
{
    /// <summary>
    /// Non-generic pool contract used by PoolManager to keep different typed
    /// pools in a single registry.
    /// </summary>
    public interface IObjectPool
    {
        void ReturnObject(Component instance);
        void Clear();
    }

    public class ObjectPool<T> : IObjectPool where T : Component, IPoolable
    {
        private readonly Queue<T> _pool = new Queue<T>();
        private readonly T _prefab;
        private readonly Transform _parent;
        private readonly Action<Component, IObjectPool> _onInstanceCreated;

        public ObjectPool(
            T prefab,
            Transform parent,
            Action<Component, IObjectPool> onInstanceCreated = null)
        {
            _prefab = prefab;
            _parent = parent;
            _onInstanceCreated = onInstanceCreated;
        }

        public T Get(Vector3 position, Quaternion rotation)
        {
            T obj;

            if (_pool.Count > 0)
            {
                obj = _pool.Dequeue();
                obj.gameObject.SetActive(true);
            }
            else
            {
                obj = UnityEngine.Object.Instantiate(_prefab, _parent);
                _onInstanceCreated?.Invoke(obj, this);
            }

            obj.transform.SetPositionAndRotation(position, rotation);
            obj.OnSpawn();

            return obj;
        }

        public void Return(T obj)
        {
            if (obj == null) return;

            obj.OnDespawn();
            obj.gameObject.SetActive(false);
            obj.transform.SetParent(_parent);

            _pool.Enqueue(obj);
        }

        public void ReturnObject(Component instance)
        {
            Return(instance as T);
        }

        public void Clear()
        {
            foreach (var item in _pool)
            {
                if (item != null)
                    UnityEngine.Object.Destroy(item.gameObject);
            }

            _pool.Clear();
        }
    }
}
