using UnityEngine;
using UnityEngine.Audio;

namespace com.pyroduck.eggheads.Runtime.Scripts.Audio
{
    /// <summary>
    /// Designer-authored definition for a single logical sound.
    /// Holds one or more clips (random pick adds variety) and runtime randomisation
    /// for volume / pitch, plus optional 3D / mixer settings.
    /// </summary>
    [System.Serializable]
    public class SoundEntry
    {
        public SoundId Id = SoundId.None;

        [Tooltip("One clip is picked at random per playback for variety.")]
        public AudioClip[] Clips;

        [Header("Volume")]
        [Range(0f, 1f)] public float MinVolume = 0.9f;
        [Range(0f, 1f)] public float MaxVolume = 1f;

        [Header("Pitch")]
        [Range(0.1f, 3f)] public float MinPitch = 0.95f;
        [Range(0.1f, 3f)] public float MaxPitch = 1.05f;

        [Header("Routing")]
        [Tooltip("Optional output mixer group (e.g. SFX bus).")]
        public AudioMixerGroup MixerGroup;

        [Header("Spatialisation")]
        [Tooltip("0 = pure 2D, 1 = fully positional 3D.")]
        [Range(0f, 1f)] public float SpatialBlend = 1f;

        [Tooltip("Distance at which the source is fully attenuated (3D only).")]
        public float MaxDistance = 25f;

        [Tooltip("Distance below which the source plays at full volume (3D only).")]
        public float MinDistance = 1f;

        public AudioClip GetRandomClip()
        {
            if (Clips == null || Clips.Length == 0) return null;

            int validCount = 0;
            for (int i = 0; i < Clips.Length; i++)
            {
                if (Clips[i] != null)
                    validCount++;
            }

            if (validCount == 0) return null;

            int selectedIndex = Random.Range(0, validCount);
            for (int i = 0; i < Clips.Length; i++)
            {
                var clip = Clips[i];
                if (clip == null) continue;

                if (selectedIndex == 0)
                    return clip;

                selectedIndex--;
            }

            return null;
        }

        public float GetRandomVolume() => Random.Range(MinVolume, MaxVolume);
        public float GetRandomPitch()  => Random.Range(MinPitch, MaxPitch);
    }
}
