using System.Collections.Generic;
using UnityEngine;
using com.pyroduck.eggheads.Runtime.Scripts.Utils;

namespace com.pyroduck.eggheads.Runtime.Scripts.Audio
{
    /// <summary>
    /// Shared impact-sound helper used by Projectile and ThrowableProjectile to avoid duplicating
    /// the random-clip-selection and spatial one-shot logic in both classes.
    /// </summary>
    public static class ProjectileImpactAudio
    {
        public static void PlayAtPoint(List<SoundData> sounds, AudioSource referenceSource, Vector3 point)
        {
            if (!TryPickRandom(sounds, out var chosen)) return;
            PlayAtPoint(chosen, referenceSource, point);
        }

        public static void PlayAtPoint(SoundData chosen, AudioSource referenceSource, Vector3 point)
        {
            if (chosen.clip == null) return;
            float volume = chosen.volume > 0f ? chosen.volume : 1f;

            if (referenceSource == null)
            {
                AudioSource.PlayClipAtPoint(chosen.clip, point, volume);
                return;
            }

            var temp = new GameObject("ProjectileImpactSound");
            temp.transform.SetParent(SceneOrganizer.Get(SceneOrganizer.Buckets.Audio), worldPositionStays: false);
            temp.transform.position = point;

            var src = temp.AddComponent<AudioSource>();
            src.outputAudioMixerGroup = referenceSource.outputAudioMixerGroup;
            src.spatialBlend         = referenceSource.spatialBlend;
            src.minDistance          = referenceSource.minDistance;
            src.maxDistance          = referenceSource.maxDistance;
            src.rolloffMode          = referenceSource.rolloffMode;
            src.pitch                = referenceSource.pitch;
            src.PlayOneShot(chosen.clip, volume);

            float lifetime = chosen.clip.length / Mathf.Max(0.01f, Mathf.Abs(src.pitch));
            Object.Destroy(temp, lifetime);
        }

        private static bool TryPickRandom(List<SoundData> sounds, out SoundData chosen)
        {
            chosen = default;
            if (sounds == null || sounds.Count == 0) return false;

            int validCount = 0;
            for (int i = 0; i < sounds.Count; i++)
                if (sounds[i].clip != null) validCount++;

            if (validCount == 0) return false;

            int pick = Random.Range(0, validCount);
            for (int i = 0; i < sounds.Count; i++)
            {
                var d = sounds[i];
                if (d.clip == null) continue;
                if (pick == 0) { chosen = d; return true; }
                pick--;
            }
            return false;
        }
    }
}
