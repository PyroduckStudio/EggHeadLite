using System;
using UnityEngine;
using UnityEngine.Serialization;

namespace com.pyroduck.eggheads.Runtime.Scripts.Audio
{
    
    [Serializable]
    public struct SoundData
    {
        [Tooltip("Tag of the surface (e.g. Metal, Wood, Stone). Leave empty for default fallback.")]
        public string surfaceTag;
        public SoundId Id;
        // Existing Lite prefab clips remain valid alongside Full SoundId entries.
        public AudioClip clip;
        public float volume { get => volumeScale; set => volumeScale = value; }
        [FormerlySerializedAs("volume")]
        [Range(0f, 1f)] public float volumeScale;
    }
}
