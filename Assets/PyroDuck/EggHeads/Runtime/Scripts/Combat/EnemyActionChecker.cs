using System;
using UnityEngine;
using com.pyroduck.eggheads.Runtime.Scripts.Character;
using com.pyroduck.eggheads.Runtime.Scripts.Combat;

namespace com.pyroduck.eggheads.Runtime.Scripts
{
    [RequireComponent(typeof(HealthComponent))]
    public class EnemyActionChecker : MonoBehaviour, IDamageable
    {
        public Transform parent;
        private Rigidbody2D _rb;
        private HealthComponent _health;

        public Action OnTakeDamage;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody2D>();
            ResolveHealth();
        }

        public HealthComponent Health => _health != null ? _health : ResolveHealth();

        private HealthComponent ResolveHealth()
        {
            var parentHealth = transform.parent != null
                ? transform.parent.GetComponentInParent<HealthComponent>()
                : null;

            _health = parentHealth != null ? parentHealth : GetComponent<HealthComponent>();
            return _health;
        }

        public void TakeDamage(float amount, GameObject source, Vector2 hitPoint)
        {
            int throwableLayer = LayerMask.NameToLayer("Throwable");
            if (source != null && throwableLayer >= 0 && source.layer == throwableLayer)
            {
                source.transform.parent = parent;
            }

            Vector2 pushDir = ((Vector2)transform.position - hitPoint);
            if (pushDir.sqrMagnitude < 0.0001f) pushDir = Vector2.up;
            if (_rb != null)
                _rb.AddForce(pushDir.normalized * amount, ForceMode2D.Impulse);

            bool damageApplied = _health != null && _health.ApplyDamage(amount, source);

            if (!damageApplied || !(amount > 1f)) return;
            OnTakeDamage?.Invoke();
        }
    }
}