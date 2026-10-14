using UnityEngine;
using com.pyroduck.eggheads.Runtime.Scripts.Audio;
using com.pyroduck.eggheads.Runtime.Scripts.Events;
using com.pyroduck.eggheads.Runtime.Scripts.Pool;

namespace com.pyroduck.eggheads.Runtime.Scripts.Combat
{
    public class ThrowableWeapon : Weapon, IThrowableWeapon
    {
        [SerializeField] private float spawnForwardOffset;

        [Header("Throwable Settings")]
        [SerializeField] private ThrowableAttackType throwableType = ThrowableAttackType.ThrowOnly;
        [SerializeField] private float throwPower = 12f;
        [SerializeField] private float returnSpeed = 14f;
        [SerializeField] private float spinSpeed = 540f;

        [Header("Damage Settings")]
        [SerializeField] private float damage = 15f;

        [Header("Explosion Settings")]
        [SerializeField] private float explosionRadius = 3f;
        [SerializeField] private ExplosionEffect explosionEffectPrefab;

        [Header("References")]
        /// <summary>
        /// Prefab that becomes the physical throwable copy.
        /// Must have Rigidbody2D + Collider2D + ThrowableProjectile components.
        /// </summary>
        [SerializeField] private ThrowableProjectile throwablePrefab;

        /// <summary>
        /// The character's root transform used as the throw origin.
        /// Assign the character root; if left null, falls back to this weapon's transform.
        /// </summary>
        [SerializeField] private Transform ownerTransform;

        [Tooltip("Return target for boomerang-style throws, for example Character Visual Parent. If empty, ownerTransform is used.")]
        [SerializeField] private Transform returnTarget;

        [Tooltip("Layer that does not collide with the Player layer. Set up Physics 2D collision matrix accordingly.")]
        [SerializeField] private string throwableLayerName = "Throwable";

        private Renderer _weaponRenderer;
        private ThrowableProjectile _activeProjectile;
        private int _throwableLayerIndex;

        protected override void Awake()
        {
            base.Awake();
            _weaponRenderer     = GetComponentInChildren<Renderer>();
            _throwableLayerIndex = LayerMask.NameToLayer(throwableLayerName);
            if (_throwableLayerIndex < 0)
                Debug.LogWarning($"{name}: Layer '{throwableLayerName}' not found. Create it in Project Settings > Tags & Layers.");
        }

        /// <summary>Called by EggHeadCreator to inject the character root transform at runtime.</summary>
        public void SetOwnerTransform(Transform root) => ownerTransform = root;

        private void OnEnable()
        {
            if (ProjectilePool.Instance != null)
            {
                if (throwablePrefab != null) ProjectilePool.Instance.PrewarmThrowable(throwablePrefab);
                if (explosionEffectPrefab != null) ProjectilePool.Instance.PrewarmExplosion(explosionEffectPrefab);
            }
        }

        public override void SetPool(ProjectilePool pool)
        {
            base.SetPool(pool);
            if (ProjectilePool.Instance != null)
            {
                if (throwablePrefab != null) ProjectilePool.Instance.PrewarmThrowable(throwablePrefab);
                if (explosionEffectPrefab != null) ProjectilePool.Instance.PrewarmExplosion(explosionEffectPrefab);
            }
        }

        protected override bool CanAttack(bool fireDown, bool fireHeld)
        {
            // Shuriken: unlimited simultaneous projectiles
            if (throwableType == ThrowableAttackType.Shuriken) return fireDown;
            // All others: block re-throw while copy is in flight
            if (_activeProjectile != null) return false;
            return fireDown;
        }

        protected override void ExecuteAttack(Vector2 attackDirection)
        {
            ExecuteThrowableAttack(attackDirection);
        }

        public void ExecuteThrowableAttack(Vector2 attackDirection)
        {
            if (throwablePrefab == null)
            {
                Debug.LogWarning($"{name}: throwablePrefab is not assigned.");
                return;
            }

            Vector2 dir = attackDirection.sqrMagnitude > 0.0001f
                ? attackDirection.normalized
                : Vector2.right;

            // Explosive and ThrowAndReturn hide the weapon during flight
            bool hidesWeapon = throwableType == ThrowableAttackType.ThrowAndReturn
                            || throwableType == ThrowableAttackType.Explosive;

            if (_weaponRenderer != null) _weaponRenderer.enabled = false;

            // Spawn at character root position (not weapon child position)
            Vector3 spawnPos = ownerTransform != null
                ? ownerTransform.position
                : transform.position;

            spawnPos += (Vector3)(dir * spawnForwardOffset);

            ThrowableProjectile proj = null;
            if (ProjectilePool.Instance != null)
                proj = ProjectilePool.Instance.GetThrowable(throwablePrefab, spawnPos);

            if (proj == null)
            {
                proj = Instantiate(throwablePrefab, spawnPos, Quaternion.identity);
            }

            proj.Initialize(
                throwVelocity: dir * throwPower,
                damage: damage,
                owner: Owner != null ? Owner : (ownerTransform != null ? ownerTransform.gameObject : transform.root.gameObject),
                behavior: throwableType,
                returnSpeed: returnSpeed,
                weaponRenderer: hidesWeapon ? _weaponRenderer : null,
                throwableLayer: _throwableLayerIndex >= 0 ? _throwableLayerIndex : gameObject.layer,
                spinSpeed: spinSpeed,
                explosionRadius: explosionRadius,
                explosionEffectPrefab: explosionEffectPrefab,
                returnTarget: returnTarget != null ? returnTarget : ownerTransform
            );

            if (!hidesWeapon && _weaponRenderer != null)
                _weaponRenderer.enabled = true;

            EventManager.Publish(new PlaySoundAtEvent
            {
                Id       = SoundId.ThrowableThrow,
                Position = spawnPos
            });

            // Shuriken: no tracking — unlimited throws
            if (throwableType == ThrowableAttackType.Shuriken) return;

            _activeProjectile = proj;
            StartCoroutine(WaitForProjectileResolved(proj));
        }

        private System.Collections.IEnumerator WaitForProjectileResolved(ThrowableProjectile proj)
        {
            // Wait until the projectile deactivates (returned to pool) or is destroyed
            while (proj != null && proj.gameObject.activeSelf)
                yield return null;

            _activeProjectile = null;

            if (_weaponRenderer != null) _weaponRenderer.enabled = true;
        }
    }
}
