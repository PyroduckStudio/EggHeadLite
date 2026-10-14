using System.Reflection;
using com.pyroduck.eggheads.Runtime.Scripts.Combat;
using NUnit.Framework;
using UnityEngine;

namespace com.pyroduck.eggheadslite.Tests.Editor
{
    public sealed class MeleeSwingAudioTests
    {
        [Test]
        public void StoppedMotion_DoesNotKeepLoopAudibleFromSmoothedSpeed()
        {
            var root = new GameObject("swing-audio-test");
            var clip = AudioClip.Create("whoosh", 4410, 1, 44100, false);
            try
            {
                var source = root.AddComponent<AudioSource>();
                var weapon = root.AddComponent<MeleeWeapon>();
                Set(weapon, "airSwingClip", clip);
                Set(weapon, "airSwingAudioSource", source);
                Set(weapon, "_angularSpeedDegPerSec", 0f);
                Set(weapon, "_smoothedSwingSpeed", 1000f);
                Set(weapon, "_smoothedSwingVolume", 0.8f);
                // Zero response is the supported immediate-update mode and
                // makes this independent of the editor's deltaTime.
                Set(weapon, "swingAudioSmoothing", 0f);
                typeof(MeleeWeapon).GetMethod("UpdateSwingSound", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(weapon, null);
                Assert.AreEqual(0f, source.volume);
                Assert.IsFalse(source.isPlaying);
            }
            finally { Object.DestroyImmediate(root); Object.DestroyImmediate(clip); }
        }

        private static void Set(MeleeWeapon weapon, string field, object value)
            => typeof(MeleeWeapon).GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(weapon, value);
    }
}
