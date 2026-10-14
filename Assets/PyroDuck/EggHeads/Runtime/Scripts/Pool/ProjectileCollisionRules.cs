using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace com.pyroduck.eggheads.Runtime.Scripts.Pool
{
    internal static class ProjectileCollisionRules
    {
        private const string BulletLayerName = "Bullet";
        private const string ThrowableLayerName = "Throwable";
        private const string GroundLayerName = "Ground";
        private const string SecondGroundLayerName = "SecondGround";
        private const string CharacterLayerName = "Character";

        private sealed class StaticState
        {
            public bool LayerCollisionConfigured;
            public ulong CachedSceneHandle = ulong.MaxValue;
            public bool CachedHasSecondGroundColliders;

            public void Reset()
            {
                LayerCollisionConfigured = false;
                CachedSceneHandle = ulong.MaxValue;
                CachedHasSecondGroundColliders = false;
            }
        }

        private static readonly StaticState State = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            State.Reset();
        }

        public static void EnsureBulletThrowableIgnored()
        {
            if (State.LayerCollisionConfigured) return;
            State.LayerCollisionConfigured = true;

            int bulletLayer = LayerMask.NameToLayer(BulletLayerName);
            int throwableLayer = LayerMask.NameToLayer(ThrowableLayerName);
            if (bulletLayer < 0 || throwableLayer < 0) return;

            Physics2D.IgnoreLayerCollision(bulletLayer, throwableLayer, true);
        }

        /// <summary>
        /// Gameplay scenes use separate Ground (walk) and SecondGround (projectile) colliders.
        /// Scenes such as PrefabCreator only have Ground colliders, so keep that layer hittable.
        /// </summary>
        public static void ConfigureRigidbodyExcludeLayers(
            Rigidbody2D rb,
            int ownLayer,
            bool excludeCharacterLayer)
        {
            if (rb == null) return;

            int excludeMask = 0;
            if (ownLayer >= 0)
                excludeMask |= 1 << ownLayer;

            if (excludeCharacterLayer)
            {
                int characterLayer = LayerMask.NameToLayer(CharacterLayerName);
                if (characterLayer >= 0)
                    excludeMask |= 1 << characterLayer;
            }

            int groundLayer = LayerMask.NameToLayer(GroundLayerName);
            if (groundLayer >= 0 && SceneHasSecondGroundColliders())
                excludeMask |= 1 << groundLayer;

            rb.excludeLayers = excludeMask;
        }

        public static bool IsProjectileLike(Collider2D collider)
        {
            if (collider == null) return false;

            return collider.GetComponentInParent<Projectile>() != null
                   || collider.GetComponentInParent<ThrowableProjectile>() != null;
        }

        public static void IgnoreCollision(
            Collider2D ownCollider,
            Collider2D otherCollider,
            List<Collider2D> ignoredColliders)
        {
            if (ownCollider == null || otherCollider == null) return;

            Physics2D.IgnoreCollision(ownCollider, otherCollider, true);
            ignoredColliders?.Add(otherCollider);
        }

        public static void IgnoreCollisions(
            Collider2D[] ownColliders,
            Collider2D otherCollider,
            List<Collider2D> ignoredColliders)
        {
            if (ownColliders == null || otherCollider == null) return;

            foreach (var ownCollider in ownColliders)
            {
                IgnoreCollision(ownCollider, otherCollider, ignoredColliders);
            }
        }

        private static bool SceneHasSecondGroundColliders()
        {
#if UNITY_6000_5_OR_NEWER
            ulong sceneHandle = SceneManager.GetActiveScene().handle.GetRawData();
#else
            ulong sceneHandle = unchecked((ulong)(uint)SceneManager.GetActiveScene().handle);
#endif
            if (State.CachedSceneHandle == sceneHandle)
                return State.CachedHasSecondGroundColliders;

            State.CachedSceneHandle = sceneHandle;
            State.CachedHasSecondGroundColliders = false;

            int secondGroundLayer = LayerMask.NameToLayer(SecondGroundLayerName);
            if (secondGroundLayer < 0)
                return State.CachedHasSecondGroundColliders;

#if UNITY_6000_5_OR_NEWER
            var colliders = Object.FindObjectsByType<Collider2D>(FindObjectsInactive.Include);
#else
            var colliders = Object.FindObjectsByType<Collider2D>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
#endif

            for (int i = 0; i < colliders.Length; i++)
            {
                var col = colliders[i];
                if (col.gameObject.layer != secondGroundLayer)
                    continue;

                // MapLimit borders may share the SecondGround layer but are not walk/impact surfaces.
                if (!col.gameObject.name.Contains("SecondGround"))
                    continue;

                State.CachedHasSecondGroundColliders = true;
                break;
            }

            return State.CachedHasSecondGroundColliders;
        }
    }
}