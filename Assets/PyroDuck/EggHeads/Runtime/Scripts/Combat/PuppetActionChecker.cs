using System;
using UnityEngine;

namespace com.pyroduck.eggheads.Runtime.Scripts.Combat
{
    public class PuppetActionChecker : MonoBehaviour, IDamageable
    {
        public Transform parent;
        private Rigidbody2D rb;
        public Action OnTakeDamage;

        private void Awake()
        {
            rb = GetComponent<Rigidbody2D>();
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
            if (rb != null)
                rb.AddForce(pushDir.normalized * amount, ForceMode2D.Impulse);

            if (!(amount > 1f)) return;
            OnTakeDamage?.Invoke();
        }
    }
}