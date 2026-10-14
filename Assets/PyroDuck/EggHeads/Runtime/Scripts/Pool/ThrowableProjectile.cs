using System.Collections.Generic;
using UnityEngine;
using com.pyroduck.eggheads.Runtime.Scripts.Audio;
using com.pyroduck.eggheads.Runtime.Scripts.Combat;
using com.pyroduck.eggheads.Runtime.Scripts.Utils;
using com.pyroduck.eggheads.Runtime.Scripts.Events;

namespace com.pyroduck.eggheads.Runtime.Scripts.Pool
{
    [RequireComponent(typeof(Rigidbody2D))]
    public class ThrowableProjectile : MonoBehaviour, IPoolable
    {
        [SerializeField] private AudioClip flightSound;

        [SerializeField] private AudioSource audioSource;

        [Header("Audio")]
        [Tooltip("Random impact sounds played on any surface. surfaceTag values on entries are ignored here.")]
        [SerializeField] private List<SoundData> impactSounds = new List<SoundData>();
        [SerializeField] private SoundId defaultImpactSoundId = SoundId.ThrowableImpact;
        [SerializeField] private SoundId flightSoundId = SoundId.None;

        [Header("Effects")]
        [SerializeField] private ParticleSystem _bloodParticlePrefab;

        [Header("Lifetime")]
        [Tooltip("Maximum time a throwable can stay active if it never hits, returns, or explodes. Use 0 or less to disable.")]
        [SerializeField] private float maxLifetime = 8f;

        // --- Runtime state ---
        private float _damage;
        private GameObject _owner;
        private Transform _returnTarget;
        private ThrowableAttackType _behavior;
        private float _returnSpeed;
        private float _spinSpeed;
        private Renderer _weaponRenderer;
        private float _explosionRadius;
        private ExplosionEffect _explosionEffectPrefab;

        // --- Internal ---
        private Rigidbody2D _rb;
        private int _originalLayer;
        private bool _hasHit;
        private bool _isReturning;
        private bool _flightSoundPlaying;
        private float _stickTimer;
        private float _lifeTimer;
        private Collider2D[] _myColliders;
        private List<Collider2D> _ignoredColliders = new List<Collider2D>();

        private const float ThrowOnlyDropDelay = 2f;
        private const float ShurikenLifetime = 5f;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody2D>();
            _myColliders = GetComponentsInChildren<Collider2D>();
            _originalLayer = gameObject.layer;
            ProjectileCollisionRules.EnsureBulletThrowableIgnored();
        }

        // ── IPoolable ─────────────────────────────────────────────────────────

        /// <summary>
        /// Called by ObjectPool&lt;T&gt;.Get() right after the object is activated.
        /// </summary>
        public void OnSpawn()
        {
            ProjectileCollisionRules.EnsureBulletThrowableIgnored();
            // Nothing extra needed here; Initialize() sets all runtime state.
        }

        /// <summary>
        /// Called by ObjectPool&lt;T&gt;.Return() before the object is deactivated.
        /// Mirrors the cleanup previously done in OnDisable so it runs as part of the pool handshake.
        /// </summary>
        public void OnDespawn()
        {
            gameObject.layer = _originalLayer;
            ResetIgnoredCollisions();

            _rb.SetLinearVelocity(Vector2.zero);
            _rb.angularVelocity = 0f;
            _rb.gravityScale = 1f;
            _rb.bodyType = RigidbodyType2D.Dynamic;
            _hasHit = false;
            _isReturning = false;
            StopFlightSound();
            _stickTimer = 0f;
            _lifeTimer = 0f;
        }

        private void OnDisable()
        {
            // Safety reset in case the object is deactivated without going through the pool.
            if (_rb == null) return;
            ResetIgnoredCollisions();
            _rb.SetLinearVelocity(Vector2.zero);
            _rb.angularVelocity = 0f;
            _rb.gravityScale = 1f;
            _rb.bodyType = RigidbodyType2D.Dynamic;
            _hasHit = false;
            _isReturning = false;
            StopFlightSound();
            _stickTimer = 0f;
            _lifeTimer = 0f;
        }

        // ── Initialization ────────────────────────────────────────────────────

        /// <summary>Called by ThrowableWeapon right after obtaining from pool.</summary>
        public void Initialize(
            Vector2 throwVelocity,
            float damage,
            GameObject owner,
            ThrowableAttackType behavior,
            float returnSpeed,
            Renderer weaponRenderer,
            int throwableLayer,
            float spinSpeed = 540f,
            float explosionRadius = 3f,
            ExplosionEffect explosionEffectPrefab = null,
            Transform returnTarget = null)
        {
            _damage = damage;
            _owner = owner;
            _returnTarget = returnTarget != null ? returnTarget : (owner != null ? owner.transform : null);
            _behavior = behavior;
            _returnSpeed = returnSpeed;
            _spinSpeed = spinSpeed;
            _weaponRenderer = weaponRenderer;
            _explosionRadius = explosionRadius;
            _explosionEffectPrefab = explosionEffectPrefab;
            _hasHit = false;
            _isReturning = false;
            _flightSoundPlaying = false;
            _stickTimer = 0f;
            _lifeTimer = 0f;

            _rb.simulated = true;
            _rb.bodyType = RigidbodyType2D.Dynamic;
            _rb.SetLinearVelocity(throwVelocity);
            _rb.gravityScale = behavior == ThrowableAttackType.ThrowAndReturn ? 0f : 1f;
            _rb.angularVelocity = throwVelocity.x >= 0f ? -spinSpeed : spinSpeed;
            // Switch to throwable layer — collision matrix must ignore Player↔Throwable
            gameObject.layer = throwableLayer;
            ProjectileCollisionRules.ConfigureRigidbodyExcludeLayers(_rb, throwableLayer, excludeCharacterLayer: false);

            StartFlightSound();

            SetupOwnerCollisionIgnore();
        }

        private void SetupOwnerCollisionIgnore()
        {
            if (_owner == null || _myColliders == null || _myColliders.Length == 0) return;

            var ownerColliders = _owner.GetComponentsInChildren<Collider2D>();

            foreach (var myCol in _myColliders)
            {
                if (myCol == null) continue;
                foreach (var col in ownerColliders)
                {
                    if (col == null) continue;
                    Physics2D.IgnoreCollision(myCol, col, true);
                    _ignoredColliders.Add(col); // Technically adds duplicates if we have multiple own colliders, but that's fine for resetting, though HashSet would be better. Let's just track them.
                }
            }
        }

        private void ResetIgnoredCollisions()
        {
            if (_myColliders == null || _myColliders.Length == 0) return;

            foreach (var myCol in _myColliders)
            {
                if (myCol == null) continue;
                foreach (var col in _ignoredColliders)
                {
                    if (col == null) continue;
                    Physics2D.IgnoreCollision(myCol, col, false);
                }
            }

            _ignoredColliders.Clear();
        }

        private void Update()
        {
            UpdateFlightSound();

            _lifeTimer += Time.deltaTime;
            if (maxLifetime > 0f && _lifeTimer >= maxLifetime)
            {
                Release();
                return;
            }

            if (_isReturning)
            {
                HandleReturn();
                return;
            }

            if (_hasHit)
            {
                _stickTimer += Time.deltaTime;

                switch (_behavior)
                {
                    case ThrowableAttackType.Shuriken:
                        if (_stickTimer >= ShurikenLifetime)
                            Release();
                        break;

                    case ThrowableAttackType.ThrowOnly:
                        if (_stickTimer >= ThrowOnlyDropDelay)
                        {
                            _rb.simulated = true;
                            _rb.bodyType = RigidbodyType2D.Dynamic;
                            _rb.gravityScale = 1f;
                            _hasHit = false;
                        }
                        break;
                }
            }
        }

        private void HandleReturn()
        {
            if (_owner == null)
            {
                Release();
                return;
            }

            Transform target = _returnTarget != null ? _returnTarget : _owner.transform;
            Vector2 toOwner = (Vector2)(target.position - transform.position);
            if (toOwner.sqrMagnitude < 1f)
            {
                if (_weaponRenderer != null) _weaponRenderer.enabled = true;
                Release();
                return;
            }

            _rb.SetLinearVelocity(toOwner.normalized * _returnSpeed);
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (other.isTrigger || _hasHit || _isReturning) return;
            HandleImpact(other, other.ClosestPoint(transform.position));
        }

        private void OnCollisionEnter2D(Collision2D col)
        {
            if (_hasHit || _isReturning) return;
            HandleImpact(col.collider, col.contactCount > 0 ? col.GetContact(0).point : default);
        }

        private void HandleImpact(Collider2D hitCollider, Vector2 hitPoint = default)
        {
            if (hitCollider == null) return;

            GameObject hitObj = hitCollider.gameObject;

            // Never react to the owner or any of its children
            if (IsOwnerHierarchy(hitObj.transform))
                return;

            // Ignore other projectiles
            if (ProjectileCollisionRules.IsProjectileLike(hitCollider))
            {
                ProjectileCollisionRules.IgnoreCollisions(_myColliders, hitCollider, _ignoredColliders);
                return;
            }

            StopFlightSound();

            Vector3 soundPos = hitPoint != default ? (Vector3)hitPoint : transform.position;

            switch (_behavior)
            {
                case ThrowableAttackType.Explosive:
                    Explode();
                    break;

                case ThrowableAttackType.ThrowAndReturn:
                    TryDamage(hitObj, hitPoint);
                    PlayImpactSound(soundPos);
                    _isReturning = true;
                    _rb.SetLinearVelocity(Vector2.zero);
                    _rb.angularVelocity = -_rb.angularVelocity;
                    _rb.gravityScale = 0f;
                    break;

                default: // ThrowOnly, Shuriken
                    TryDamage(hitObj, hitPoint);
                    PlayImpactSound(soundPos);
                    StickToSurface();
                    break;
            }
        }

        private void PlayImpactSound(Vector3 point)
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

        private void StartFlightSound()
        {
            if (flightSound != null && audioSource != null)
            {
                audioSource.clip = flightSound; audioSource.loop = true; audioSource.Play();
                _flightSoundPlaying = true; return;
            }
            if (flightSoundId == SoundId.None)
                return;

            _flightSoundPlaying = true;
            UpdateFlightSound();
        }

        private void UpdateFlightSound()
        {
            if (!_flightSoundPlaying || flightSoundId == SoundId.None)
                return;

            AudioManager.PlayLoopingSoundAt(
                flightSoundId,
                gameObject,
                transform.position,
                1f,
                1f);
        }

        private void StopFlightSound()
        {
            if (audioSource != null) { audioSource.Stop(); audioSource.clip = null; }
            if (!_flightSoundPlaying)
                return;

            _flightSoundPlaying = false;
            AudioManager.StopLoopingSound(gameObject, flightSoundId);
        }

        private void Explode()
        {
            Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, _explosionRadius);
            foreach (var h in hits)
            {
                // Skip the owner's entire hierarchy (root + all children), matching Projectile.IsOwnerHierarchy.
                if (IsOwnerHierarchy(h.transform)) continue;

                var damageable = h.GetComponent<IDamageable>()
                                 ?? h.GetComponentInParent<IDamageable>();
                if (damageable != null)
                    damageable.TakeDamage(_damage, _owner, h.ClosestPoint(transform.position));
            }

            // Try to get the explosion effect from the singleton pool.
            ExplosionEffect fx = null;
            if (ProjectilePool.Instance != null && _explosionEffectPrefab != null)
                fx = ProjectilePool.Instance.GetExplosionFromPool(_explosionEffectPrefab, transform.position);

            if (fx == null && _explosionEffectPrefab != null)
                fx = Instantiate(_explosionEffectPrefab, transform.position, Quaternion.identity);

            if (fx != null)
                fx.Play(transform.position);
            else
                ExplosionEffect.PlaySoundAt(transform.position);

            if (_weaponRenderer != null) _weaponRenderer.enabled = true;

            Release();
        }

        private void StickToSurface()
        {
            _hasHit = true;
            _stickTimer = 0f;
            _rb.SetLinearVelocity(Vector2.zero);
            _rb.angularVelocity = 0f;
            _rb.bodyType = RigidbodyType2D.Kinematic;
            _rb.simulated = false;
        }

        private void TryDamage(GameObject hitObj, Vector2 hitPoint = default)
        {
            if (hitObj.TryGetComponent<IDamageable>(out var d))
            {
                d.TakeDamage(_damage, transform.gameObject, hitPoint);
                PlayBloodParticle(hitPoint != default ? hitPoint : (Vector2)transform.position);
            }
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

        private void Release()
        {
            if (_weaponRenderer != null) _weaponRenderer.enabled = true;

            if (ProjectilePool.Instance != null)
                ProjectilePool.Instance.ReturnThrowable(this);
            else
                Destroy(gameObject);
        }

        /// <summary>
        /// Returns true if <paramref name="candidate"/> lives under the same root
        /// as the throwable's owner (the shooter character and everything parented under it).
        /// Used to prevent self-damage while still allowing other characters to be hit.
        /// </summary>
        private bool IsOwnerHierarchy(Transform candidate)
        {
            if (_owner == null || candidate == null) return false;
            return candidate.IsChildOf(_owner.transform) || _owner.transform.IsChildOf(candidate);
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (_behavior == ThrowableAttackType.Explosive)
            {
                UnityEditor.Handles.color = new Color(1f, 0.4f, 0f, 0.3f);
                UnityEditor.Handles.DrawSolidDisc(transform.position, Vector3.forward, _explosionRadius);
            }
        }
#endif
    }
}
