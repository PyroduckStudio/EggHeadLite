using com.pyroduck.eggheads.Runtime.Scripts.Utils;
using UnityEngine;
using System.Collections.Generic;

namespace com.pyroduck.eggheads.Runtime.Scripts.Combat
{
    /// <summary>
    /// Attach to spike / hazard objects in the scene.
    /// The character resolves this component from its collision or trigger
    /// contact and applies <see cref="Damage"/> to itself.
    /// </summary>
    public class SpikeHazard : MonoBehaviour
    {
        [SerializeField] private float damage = 20f;
        [SerializeField] private float hitCooldown = 0.25f;

        private readonly Dictionary<ulong, float> _lastHitTimesByTarget = new();

        public float Damage => damage;

        private void OnCollisionEnter2D(Collision2D collision)
        {
            if (collision == null || collision.collider == null)
                return;

            var hitPoint = collision.contactCount > 0
                ? collision.GetContact(0).point
                : collision.collider.ClosestPoint(transform.position);
            TryDamage(collision.collider, hitPoint);
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (other == null)
                return;

            TryDamage(other, other.ClosestPoint(transform.position));
        }

        public bool TryDamage(Collider2D targetCollider, Vector2 hitPoint)
        {
            if (targetCollider == null || damage <= 0f)
                return false;

            var damageable = targetCollider.GetComponent<IDamageable>()
                             ?? targetCollider.GetComponentInParent<IDamageable>();
            if (damageable == null)
                return false;

            ulong targetId = GetDamageableKey(damageable);
            if (_lastHitTimesByTarget.TryGetValue(targetId, out float lastHitTime) &&
                Time.time - lastHitTime < hitCooldown)
            {
                return false;
            }

            _lastHitTimesByTarget[targetId] = Time.time;
            damageable.TakeDamage(damage, gameObject, hitPoint);
            return true;
        }

        private static ulong GetDamageableKey(IDamageable damageable)
        {
            if (damageable is Component component)
                return component.GetObjectId();

            return unchecked((ulong)(uint)damageable.GetHashCode());
        }
    }
}
