using UnityEngine;
using com.pyroduck.eggheads.Runtime.Scripts.Combat;
using com.pyroduck.eggheads.Runtime.Scripts.Events;
using com.pyroduck.eggheads.Runtime.Scripts.Utils;
using System.Collections.Generic;

namespace com.pyroduck.eggheads.Runtime.Scripts.Character
{
    /// <summary>
    /// Reusable <see cref="IDamageable"/> implementation. Drop it on any GameObject
    /// (player, enemy, destructible) to get a working hit-point pipeline with a
    /// per-source damage cooldown and strongly typed <see cref="TakeDamage"/> events.
    /// </summary>
    [DisallowMultipleComponent]
    public class HealthComponent : MonoBehaviour, IDamageable
    {
        [Header("Health")]
        [SerializeField] private float maxHealth = 100f;

        [Tooltip("Minimum seconds between two consecutive damage applications.")]
        [SerializeField] private float damageCooldown = 0.25f;

        [Header("VFX")]
        [Tooltip("Blood particle prefab spawned at the hit point each time damage is applied.")]
        [SerializeField] private ParticleSystem bloodParticlePrefab;

        public float Health      { get; private set; }
        public float MaxHealth   => maxHealth;
        public bool  IsAlive     => Health > 0f;
        public float LastDamageTime { get; private set; } = -999f;

        private readonly Dictionary<ulong, float> _lastDamageTimesBySource = new();

        public event System.Action<float, GameObject> OnDamaged;
        public event System.Action<float> OnHealed;
        public event System.Action OnDied;

        /// <summary>
        /// Fires whenever <see cref="Health"/> changes (spawn, damage, heal, reset).
        /// </summary>
        public event System.Action<float> OnHealthUpdated;

        private void Awake()
        {
            Health = maxHealth;
            UpdateHealth();
        }

        /// <summary>
        /// Notifies listeners of the current <see cref="Health"/> value (same pattern as <see cref="OnHealed"/>).
        /// </summary>
        private void UpdateHealth()
        {
            OnHealthUpdated?.Invoke(Health); 
        }

        public void ResetHealth()
        {
            bool wasDead = !IsAlive;
            Health = maxHealth;
            LastDamageTime = -999f;
            _lastDamageTimesBySource.Clear();
            OnHealed?.Invoke(Health); 
            UpdateHealth();

            if (wasDead)
                EventManager.Publish(new CharacterRevivedEvent { Source = gameObject });
        }

        public void TakeDamage(float amount, GameObject source, Vector2 hitPoint)
        { 
            ApplyDamage(amount, source, hitPoint);
        }

        public bool ApplyDamage(float amount, GameObject source, Vector2 hitPoint = default)
        {
            if (amount <= 0f || !IsAlive) return false;

            ulong sourceId = source != null ? source.GetObjectId() : 0;
            if (_lastDamageTimesBySource.TryGetValue(sourceId, out float lastSourceDamageTime) &&
                Time.time - lastSourceDamageTime < damageCooldown)
            {
                return false;
            }

            LastDamageTime = Time.time;
            _lastDamageTimesBySource[sourceId] = Time.time;
            Health = Mathf.Max(0f, Health - amount);

            UpdateHealth();

            OnDamaged?.Invoke(amount, source);

            EventManager.Publish(new TakeDamage
            {
                DamageAmount  = amount,
                CurrentHealth = Health,
                MaxHealth     = maxHealth,
                Source        = source,
                Target        = gameObject,
                HitPoint      = hitPoint
            });

            SpawnBlood(hitPoint);

            if (Health <= 0f)
            {
                OnDied?.Invoke();
                EventManager.Publish(new CharacterDiedEvent { Source = gameObject });
            }

            return true;
        }

        private void SpawnBlood(Vector2 hitPoint)
        {
            if (bloodParticlePrefab == null) return;

            Vector3 spawnPos = hitPoint != default ? (Vector3)hitPoint : transform.position;
            var blood = Instantiate(bloodParticlePrefab, spawnPos, Quaternion.identity,
                SceneOrganizer.Get(SceneOrganizer.Buckets.Effects));
            var main = blood.main;
            if (main.stopAction == ParticleSystemStopAction.None)
                main.stopAction = ParticleSystemStopAction.Destroy;
            blood.Play();
        }
    }
}
