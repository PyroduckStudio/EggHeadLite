using UnityEngine;

namespace com.pyroduck.eggheads.Runtime.Scripts.Character
{
    /// <summary>
    /// Forwards 2D physics callbacks to <see cref="EggHeadController"/> because Unity only
    /// invokes collision/trigger messages on the GameObject that owns the <see cref="Collider2D"/>,
    /// not on parent objects (where the controller typically lives).
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public sealed class EggHeadPhysicsContactRelay : MonoBehaviour
    {
        private EggHeadController _eggHead;
        private Collider2D _relayCollider;

        private void Awake()
        {
            _eggHead = GetComponentInParent<EggHeadController>();
            _relayCollider = GetComponent<Collider2D>();
        }

        private void OnCollisionEnter2D(Collision2D collision)
        {
            Vector2 hitPoint = collision.contactCount > 0
                ? collision.GetContact(0).point
                : ResolveClosestPointOnEgg(collision.collider);

            _eggHead?.TryApplyHazardDamage(collision.collider, hitPoint);
        }
        //
        // private void OnCollisionStay2D(Collision2D collision)
        // {
        //     _eggHead?.TryApplyHazardDamage(collision.collider);
        // }

        private void OnTriggerEnter2D(Collider2D other)
        {
            _eggHead?.TryApplyHazardDamage(other, ResolveClosestPointOnEgg(other));
        }

        // private void OnTriggerStay2D(Collider2D other)
        // {
        //     _eggHead?.TryApplyHazardDamage(other);
        // }

        private Vector2 ResolveClosestPointOnEgg(Collider2D other)
        {
            if (_relayCollider == null || other == null)
                return transform.position;

            ColliderDistance2D distance = _relayCollider.Distance(other);
            return distance.isValid
                ? distance.pointA
                : _relayCollider.ClosestPoint(other.bounds.center);
        }
    }
}
