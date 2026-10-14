using UnityEngine;
using com.pyroduck.eggheads.Runtime.Scripts.Data;

namespace com.pyroduck.eggheads.Runtime.Scripts.Combat
{
    /// <summary>
    /// Stateless factory that spawns a physics-enabled <see cref="WeaponPickup"/>
    /// from a <see cref="VisualDataSO"/>. Extracted out of <c>EggHeadController</c>
    /// so any system (AI drops, chest loot, debug spawners…) can reuse the same
    /// behaviour without duplicating GameObject setup.
    /// </summary>
    public static class WeaponPickupFactory
    {
        /// <summary>
        /// Creates a pickup object at <paramref name="worldPosition"/> with a small
        /// tossing impulse of <paramref name="dropImpulse"/>.
        /// </summary>
        /// <param name="data">Visual data describing the weapon to spawn.</param>
        /// <param name="worldPosition">Spawn position in world space.</param>
        /// <param name="dropImpulse">Impulse applied to the rigid body.</param>
        /// <param name="facingDir">Horizontal direction for the X component of the impulse (-1 or +1).</param>
        /// <param name="colliderRadius">Radius of the physics collider. Trigger collider uses 2x this.</param>
        /// <returns>The root GameObject of the spawned pickup (or null on bad input).</returns>
        public static GameObject Create(
            VisualDataSO data,
            Vector3 worldPosition,
            Vector2 dropImpulse,
            float facingDir = 1f,
            float colliderRadius = 0.3f)
        {
            if (data == null) return null;

            var go = new GameObject($"[WeaponPickup] {data.Name}");
            go.transform.position = worldPosition;

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite       = data.IconSprite;
            sr.sortingOrder = 5;

            // Reuse the configured visual material, including optional pipeline support.
            var visualRenderer = data.BodyPartPrefab != null
                ? data.BodyPartPrefab.GetComponentInChildren<SpriteRenderer>(true) : null;
            if (visualRenderer != null && visualRenderer.sharedMaterial != null)
            {
                sr.sharedMaterial = visualRenderer.sharedMaterial;
                foreach (var behaviour in visualRenderer.GetComponents<MonoBehaviour>())
                    if (behaviour != null && behaviour.GetType().FullName == "com.pyroduck.eggheadslite.Rendering.EggHeadsSpritePipelineAdapter")
                        go.AddComponent(behaviour.GetType());
            }

            var physCol = go.AddComponent<CircleCollider2D>();
            physCol.isTrigger = false;
            physCol.radius    = colliderRadius;

            var trigCol = go.AddComponent<CircleCollider2D>();
            trigCol.isTrigger = true;
            trigCol.radius    = colliderRadius * 2f;

            var rb = go.AddComponent<Rigidbody2D>();
            rb.gravityScale   = 2f;
            rb.freezeRotation = true;
            float sign = facingDir >= 0f ? 1f : -1f;
            rb.AddForce(new Vector2(dropImpulse.x * sign, dropImpulse.y), ForceMode2D.Impulse);

            var pickup = go.AddComponent<WeaponPickup>();
            pickup.Init(data);
            return go;
        }
    }
}