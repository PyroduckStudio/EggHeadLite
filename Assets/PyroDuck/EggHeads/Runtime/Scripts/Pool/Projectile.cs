using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using com.pyroduck.eggheads.Runtime.Scripts.Events;
using com.pyroduck.eggheads.Runtime.Scripts.Audio;
using com.pyroduck.eggheads.Runtime.Scripts.Combat;

namespace com.pyroduck.eggheads.Runtime.Scripts.Pool
{
    public class Projectile : MonoBehaviour, IPoolable
    {
        [SerializeField] private AudioSource audioSource;

        [Header("Motion")]
        [FormerlySerializedAs("speed")]
        [SerializeField] private float _speed = 15f;

        [FormerlySerializedAs("lifetime")]
        [SerializeField] private float _lifetime = 2f;

        [Header("Explosion")]
        [FormerlySerializedAs("isExplosive")]
        [SerializeField] private bool _isExplosive = false;

        [FormerlySerializedAs("explosionRadius")]
        [SerializeField] private float _explosionRadius = 3f;

        [SerializeField] private ExplosionEffect _explosionEffectPrefab;

        [Header("Effects")]
        [SerializeField] private ParticleSystem _bloodParticlePrefab;

        [Header("Impact Sounds")]
        [Tooltip("Random impact sounds played on any surface. surfaceTag values on entries are ignored here.")]
        [SerializeField] private List<SoundData> impactSounds = new List<SoundData>();
        [SerializeField] private SoundId defaultImpactSoundId = SoundId.ProjectileImpact;

        public float Speed => _speed;
        public float Lifetime => _lifetime;
        public bool IsExplosive => _isExplosive;
        public float ExplosionRadius => _explosionRadius;
        public float Damage { get; private set; }

        private float _lifeTimer;
        private Vector2 _moveDirection;
        private GameObject _owner;
        private bool _detonated;

        // NEW
        private Collider2D _myCollider;
        private Rigidbody2D _rb;
        private List<Collider2D> _ignoredColliders = new List<Collider2D>();

        private void Awake()
        {
            CacheCollider();
            _rb = GetComponent<Rigidbody2D>();
            ProjectileCollisionRules.EnsureBulletThrowableIgnored();
        }

        private void CacheCollider()
        {
            if (_myCollider == null)
                _myCollider = GetComponent<Collider2D>();
        }

        // ── Pool ─────────────────────────────────────────

        public void OnSpawn()
        {
            ProjectileCollisionRules.EnsureBulletThrowableIgnored();
            _lifeTimer     = _lifetime;
            _detonated     = false;
            _moveDirection = Vector2.right;
            _owner         = null;
            Damage         = 0f;
            CacheCollider();
        }

        public void OnDespawn()
        {
            ResetIgnoredCollisions();
        }

        // ── Init ─────────────────────────────────────────

        public void Initialize(Vector2 direction, float damage = 0f, GameObject owner = null)
        {
            _moveDirection = direction.normalized;
            Damage         = damage;
            _owner         = owner;
            _lifeTimer     = _lifetime;
            _detonated     = false;

            CacheCollider();
            ResetIgnoredCollisions();

            float angle = Mathf.Atan2(_moveDirection.y, _moveDirection.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Euler(0, 0, angle);

            if (_rb != null)
                ProjectileCollisionRules.ConfigureRigidbodyExcludeLayers(_rb, gameObject.layer, excludeCharacterLayer: true);

            SetupOwnerCollisionIgnore();
        }

        private void SetupOwnerCollisionIgnore()
        {
            if (_owner == null || _myCollider == null) return;

            var ownerColliders = _owner.GetComponentsInChildren<Collider2D>();

            foreach (var col in ownerColliders)
            {
                if (col == null) continue;

                Physics2D.IgnoreCollision(_myCollider, col, true);
                _ignoredColliders.Add(col);
            }
        }

        private void ResetIgnoredCollisions()
        {
            if (_myCollider == null) return;

            foreach (var col in _ignoredColliders)
            {
                if (col == null) continue;

                Physics2D.IgnoreCollision(_myCollider, col, false);
            }

            _ignoredColliders.Clear();
        }

        // ── Update ───────────────────────────────────────

        private void Update()
        {
            if (_detonated) return;

            float step = _speed * Time.deltaTime;
            if (TrySweepHit(transform.position, _moveDirection, step, out var hitCollider, out var hitPoint))
            {
                HandleHit(hitCollider, hitPoint);
                return;
            }

            transform.Translate(_moveDirection * step, Space.World);

            _lifeTimer -= Time.deltaTime;
            if (_lifeTimer <= 0f)
            {
                if (_isExplosive)
                    Explode(transform.position);

                ReturnToPool();
            }
        }

        private bool TrySweepHit(Vector2 origin, Vector2 direction, float distance, out Collider2D hitCollider, out Vector2 hitPoint)
        {
            hitCollider = null;
            hitPoint = default;

            if (distance <= 0f)
                return false;

            float radius = GetSweepRadius();
            var hits = Physics2D.CircleCastAll(origin, radius, direction, distance);
            if (hits == null || hits.Length == 0)
                return false;

            float closestDistance = float.MaxValue;
            for (int i = 0; i < hits.Length; i++)
            {
                var hit = hits[i];
                if (hit.collider == null || hit.collider == _myCollider)
                    continue;

                if (hit.collider.isTrigger)
                    continue;

                if (IsIgnoredCollider(hit.collider))
                    continue;

                if (IsOwnerHierarchy(hit.collider.transform))
                    continue;

                if (ProjectileCollisionRules.IsProjectileLike(hit.collider))
                    continue;

                if (hit.distance >= closestDistance)
                    continue;

                closestDistance = hit.distance;
                hitCollider = hit.collider;
                hitPoint = hit.point;
            }

            return hitCollider != null;
        }

        private float GetSweepRadius()
        {
            if (_myCollider is CircleCollider2D circle)
            {
                float scale = Mathf.Max(Mathf.Abs(transform.lossyScale.x), Mathf.Abs(transform.lossyScale.y));
                return circle.radius * scale;
            }

            return 0.25f;
        }

        private bool IsIgnoredCollider(Collider2D collider)
        {
            for (int i = 0; i < _ignoredColliders.Count; i++)
            {
                if (_ignoredColliders[i] == collider)
                    return true;
            }

            return false;
        }

        // ── Collision ────────────────────────────────────

        private void OnCollisionEnter2D(Collision2D other)
        {
            if (_detonated) return;
            if (other.collider.isTrigger) return;
            if (TryIgnoreProjectileCollision(other.collider)) return;

            HandleHit(other.collider, other.contacts[0].point);
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (_detonated) return;
            if (other.isTrigger) return;
            if (TryIgnoreProjectileCollision(other)) return;

            HandleHit(other, other.bounds.center);
        }

        private void HandleHit(Collider2D other, Vector2 hitPoint)
        {
            if (IsOwnerHierarchy(other.transform)) return;

            if (_isExplosive)
            {
                Explode(hitPoint != default ? hitPoint : transform.position);
            }
            else
            {
                var damageable = other.GetComponent<IDamageable>()
                                 ?? other.GetComponentInParent<IDamageable>();

                if (damageable != null)
                {
                    damageable.TakeDamage(Damage, _owner, hitPoint);
                    PlayBloodParticle(hitPoint);
                }

                PlayImpactSound(hitPoint);
            }

            ReturnToPool();
        }

        private bool TryIgnoreProjectileCollision(Collider2D other)
        {
            if (!ProjectileCollisionRules.IsProjectileLike(other)) return false;

            ProjectileCollisionRules.IgnoreCollision(_myCollider, other, _ignoredColliders);
            return true;
        }

        private void PlayBloodParticle(Vector2 hitPoint)
        {
            if (_bloodParticlePrefab == null) return;
            var blood = Instantiate(_bloodParticlePrefab, hitPoint, Quaternion.identity);
            var main = blood.main;
            if (main.stopAction == ParticleSystemStopAction.None)
                main.stopAction = ParticleSystemStopAction.Destroy;
            blood.Play();
        }

        private void PlayImpactSound(Vector2 point)
        {
            if (TryGetRandomImpactSound(out var chosen))
            {
                if (chosen.clip != null)
                    ProjectileImpactAudio.PlayAtPoint(chosen, audioSource, point);
                else PublishImpactSound(chosen.Id, point, chosen.volumeScale > 0f ? chosen.volumeScale : 1f);
                return;
            }

            PublishImpactSound(defaultImpactSoundId, point, 1f);
        }

        private bool TryGetRandomImpactSound(out SoundData chosen)
        {
            chosen = default;
            if (impactSounds == null || impactSounds.Count == 0) return false;

            int validCount = 0;
            for (int i = 0; i < impactSounds.Count; i++)
            {
                if (impactSounds[i].Id != SoundId.None || impactSounds[i].clip != null)
                    validCount++;
            }

            if (validCount == 0) return false;

            int selectedIndex = Random.Range(0, validCount);
            for (int i = 0; i < impactSounds.Count; i++)
            {
                var data = impactSounds[i];
                if (data.Id == SoundId.None && data.clip == null) continue;

                if (selectedIndex == 0)
                {
                    chosen = data;
                    return true;
                }

                selectedIndex--;
            }

            return false;
        }

        private static void PublishImpactSound(SoundId id, Vector3 point, float volumeScale)
        {
            if (id == SoundId.None)
                return;

            AudioManager.EnsureInstance();
            EventManager.Publish(new PlaySoundAtEvent
            {
                Id = id,
                Position = point,
                VolumeScale = volumeScale
            });
        }

        // ── Explosion ────────────────────────────────────

        private void Explode(Vector2 origin)
        {
            _detonated = true;

            var hits = Physics2D.OverlapCircleAll(origin, _explosionRadius);

            foreach (var hit in hits)
            {
                if (IsOwnerHierarchy(hit.transform)) continue;

                var damageable = hit.GetComponent<IDamageable>()
                                 ?? hit.GetComponentInParent<IDamageable>();

                if (damageable != null)
                {
                    float dist     = Vector2.Distance(origin, hit.bounds.center);
                    float falloff  = Mathf.Clamp01(1f - dist / _explosionRadius);
                    float finalDmg = Damage * falloff;

                    damageable.TakeDamage(finalDmg, _owner, origin);
                }
            }

            ExplosionEffect fx = null;
            if (ProjectilePool.Instance != null && _explosionEffectPrefab != null)
                fx = ProjectilePool.Instance.GetExplosionFromPool(_explosionEffectPrefab, origin);

            if (fx == null && _explosionEffectPrefab != null)
                fx = Instantiate(_explosionEffectPrefab, origin, Quaternion.identity);

            if (fx != null)
                fx.Play(origin);
            else
                ExplosionEffect.PlaySoundAt(origin);
        }

        private bool IsOwnerHierarchy(Transform candidate)
        {
            if (_owner == null || candidate == null) return false;
            return candidate.IsChildOf(_owner.transform) || _owner.transform.IsChildOf(candidate);
        }

        // ── Return ───────────────────────────────────────

        private void ReturnToPool()
        {
            if (ProjectilePool.Instance != null)
                ProjectilePool.Instance.ReturnProjectile(this);
            else
                Destroy(gameObject);
        }
    }
}
