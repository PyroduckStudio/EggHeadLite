using System.Collections.Generic;
using UnityEngine;

namespace com.pyroduck.eggheads.Runtime.Scripts.Utils
{
    /// <summary>
    /// Provides named root-level containers so runtime-spawned objects (VFX, audio sources,
    /// projectiles, etc.) are grouped in the hierarchy instead of scattered at scene root.
    ///
    /// Usage:
    ///   Instantiate(prefab, pos, rot, SceneOrganizer.Get("Effects"));
    ///
    /// Buckets are created on first access and cleared automatically between play sessions.
    /// </summary>
    public static class SceneOrganizer
    {
        public static class Buckets
        {
            public const string Effects    = "Effects";
            public const string Audio      = "Audio";
            public const string Projectiles = "Projectiles";
        }

        private static readonly Dictionary<string, Transform> _cache = new();

        /// <summary>
        /// Returns the Transform of the named root container, creating it if it does not exist.
        /// </summary>
        public static Transform Get(string bucketName)
        {
            if (_cache.TryGetValue(bucketName, out var t) && t != null)
                return t;

            var go = new GameObject($"[{bucketName}]");
            Object.DontDestroyOnLoad(go);
            _cache[bucketName] = go.transform;
            return go.transform;
        }

#if UNITY_2019_3_OR_NEWER
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState() => _cache.Clear();
#endif
    }
}
