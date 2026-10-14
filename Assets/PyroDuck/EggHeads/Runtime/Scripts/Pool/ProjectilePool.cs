using UnityEngine;
using com.pyroduck.eggheads.Runtime.Scripts.Combat;

namespace com.pyroduck.eggheads.Runtime.Scripts.Pool
{
    /// <summary>
    /// Scene-level singleton that manages Projectile, ThrowableProjectile, and
    /// ExplosionEffect instances through PoolManager / ObjectPool&lt;T&gt;.
    ///
    /// Assign prefabs in the Inspector; Awake registers the initial pools.
    /// </summary>
    public class ProjectilePool : MonoBehaviour
    {
        // ── Singleton ─────────────────────────────────────────────────────────

        private sealed class StaticState
        {
            public ProjectilePool Instance;
        }

        private static readonly StaticState State = new();

        public static ProjectilePool Instance => State.Instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            State.Instance = null;
        }

        private void Awake()
        {
            if (State.Instance != null && State.Instance != this)
            {
                Debug.LogWarning($"[ProjectilePool] More than one ProjectilePool exists. " +
                                 $"Destroying '{gameObject.name}'.");
                Destroy(gameObject);
                return;
            }
            State.Instance = this;

            RegisterPools();
        }

        private void OnEnable()
        {
            // Awake is skipped when Fast Enter Play Mode disables Scene Reload.
            if (State.Instance == null)
                State.Instance = this;
        }

        private void OnDestroy()
        {
            if (State.Instance == this)
                State.Instance = null;
        }

        // ── Inspector ─────────────────────────────────────────────────────────

       private Projectile          _projectilePrefab;
       private ThrowableProjectile _throwablePrefab;
       private ExplosionEffect     _explosionPrefab;

        // ── Internal ──────────────────────────────────────────────────────────

        private PoolManager _poolManager;

        private void RegisterPools()
        {
            _poolManager = new PoolManager(transform);

            if (_projectilePrefab != null)
                _poolManager.RegisterPool(_projectilePrefab);

            if (_throwablePrefab != null)
                _poolManager.RegisterPool(_throwablePrefab);

            if (_explosionPrefab != null)
                _poolManager.RegisterPool(_explosionPrefab);
        }

        // ── Public SetPrefab API ──────────────────────────────────────────────

        /// <summary>
        /// RangedWeapon provides its prefab here; the matching pool is registered
        /// or rebuilt when the prefab changes.
        /// </summary>
        public void SetProjectilePrefab(Projectile prefab)
        {
            if (prefab == null || prefab == _projectilePrefab) return;
            _projectilePrefab = prefab;
            _poolManager.RegisterPool(_projectilePrefab, force: true);
        }

        /// <summary>ThrowableWeapon provides its prefab here.</summary>
        public void SetThrowablePrefab(ThrowableProjectile prefab)
        {
            if (prefab == null || prefab == _throwablePrefab) return;
            _throwablePrefab = prefab;
            _poolManager.RegisterPool(_throwablePrefab, force: true);
        }

        /// <summary>Sets the explosion effect prefab.</summary>
        public void SetExplosionPrefab(ExplosionEffect prefab)
        {
            if (prefab == null || prefab == _explosionPrefab) return;
            _explosionPrefab = prefab;
            _poolManager.RegisterPool(_explosionPrefab, force: true);
        }

        // ── Get / Return API ──────────────────────────────────────────────────

        /// <summary>Uses the default projectile prefab assigned in the Inspector.</summary>
        public Projectile GetProjectile(Vector3 position)
        {
            if (_projectilePrefab == null)
            {
                Debug.LogError("[ProjectilePool] Projectile prefab is not assigned.");
                return null;
            }
            return _poolManager.Get<Projectile>(_projectilePrefab,position, Quaternion.identity);
        }

        /// <summary>Gets from a specific prefab pool, registering it if needed.</summary>
        public Projectile GetProjectile(Projectile prefab, Vector3 position)
        {
            if (prefab == null) return null;
            if (prefab != _projectilePrefab)
            {
                _projectilePrefab = prefab;
                _poolManager.RegisterPool(_projectilePrefab, force: true);
            }
            return _poolManager.Get(prefab,position, Quaternion.identity);
        }

       

        public void ReturnProjectile(Projectile proj)
        {
            if (proj == null) return;
            _poolManager.Return(proj);
        }

        public ThrowableProjectile GetThrowable(ThrowableProjectile prefab,Vector3 position)
        {
            if (prefab == null)
            {
                Debug.LogError("[ProjectilePool] GetThrowable was called with a null prefab.");
                return null;
            }
            if (prefab != _throwablePrefab)
            {
                _throwablePrefab = prefab;
                _poolManager.RegisterPool(_throwablePrefab, force: true);
            }
            return _poolManager.Get<ThrowableProjectile>(prefab,position, Quaternion.identity);
        }

        public void ReturnThrowable(ThrowableProjectile proj)
        {
            if (proj == null) return;
            _poolManager.Return(proj);
        }

        /// <summary>Uses the default explosion effect prefab assigned in the Inspector.</summary>
        public ExplosionEffect GetExplosionFromPool(Vector3 position)
        {
            if (_explosionPrefab == null)
            {
                Debug.LogError("[ProjectilePool] ExplosionEffect prefab is not assigned.");
                return null;
            }
            return _poolManager.Get(_explosionPrefab,position, Quaternion.identity);
        }

        /// <summary>Gets from a specific explosion effect prefab pool, registering it if needed.</summary>
        public ExplosionEffect GetExplosionFromPool(ExplosionEffect prefab, Vector3 position)
        {
            if (prefab == null) return null;
            if (prefab != _explosionPrefab)
            {
                _explosionPrefab = prefab;
                _poolManager.RegisterPool(_explosionPrefab, force: true);
            }
            return _poolManager.Get(prefab,position, Quaternion.identity);
        }

        public void ReturnExplosion(ExplosionEffect fx)
        {
            if (fx == null) return;
            _poolManager.Return(fx);
        }

        /// <summary>
        /// Clears pooled instances and rebuilds pools for the currently assigned prefabs.
        /// </summary>
        public void ClearPool()
        {
            _poolManager?.Clear();
            RegisterPools();
        }

        /// <summary>Called by RangedWeapon on enable to register the projectile prefab early.</summary>
        public void PrewarmProjectile(Projectile prefab)
        {
            if (prefab == null) return;
            if (prefab != _projectilePrefab)
            {
                _projectilePrefab = prefab;
                _poolManager.RegisterPool(_projectilePrefab, force: true);
            }
        }
        public void PrewarmThrowable(ThrowableProjectile throwablePrefab)
        {
            if (throwablePrefab == null) return;
            if (throwablePrefab != _throwablePrefab)
            {
                _throwablePrefab = throwablePrefab;
                _poolManager.RegisterPool(_throwablePrefab, force: true);
            }
        }

        public void PrewarmExplosion(ExplosionEffect explosionEffectPrefab)
        {
            if (explosionEffectPrefab == null) return;
            if (explosionEffectPrefab != _explosionPrefab)
            {
                _explosionPrefab = explosionEffectPrefab;
                _poolManager.RegisterPool(_explosionPrefab, force: true);
            }
        }
    }
}
