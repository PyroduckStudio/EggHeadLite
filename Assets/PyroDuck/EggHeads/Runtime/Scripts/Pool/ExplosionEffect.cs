using UnityEngine;
using com.pyroduck.eggheads.Runtime.Scripts.Audio;
using com.pyroduck.eggheads.Runtime.Scripts.Events;

namespace com.pyroduck.eggheads.Runtime.Scripts.Pool
{
    /// <summary>
    /// Wraps an explosion ParticleSystem. When the particle finishes playing,
    /// it returns itself to the ProjectilePool singleton automatically.
    /// Attach to an explosion effect prefab that has a ParticleSystem component.
    /// </summary>
    [RequireComponent(typeof(ParticleSystem))]
    public class ExplosionEffect : MonoBehaviour, IPoolable
    {
        [SerializeField] private AudioClip explosionSound;
        [SerializeField] private AudioSource audioSource;

        [Header("Audio")]
        [SerializeField] private SoundId explosionSoundId = SoundId.Explosion;

        private ParticleSystem _ps;

        private void Awake()
        {
            _ps = GetComponent<ParticleSystem>();

            // Stop & clear so pooled instances don't auto-play on Awake
            var main = _ps.main;
            main.stopAction = ParticleSystemStopAction.Callback;
        }

        // ── IPoolable ─────────────────────────────────────────────────────────

        public void OnSpawn()
        {
            // Activated by ObjectPool; Play() must be called explicitly afterward.
        }

        public void OnDespawn()
        {
            if (_ps != null)
                _ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        private void OnDisable()
        {
            if (_ps != null)
                _ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        // ── Public API ────────────────────────────────────────────────────────

        /// <summary>Plays explosion particles and configured explosion audio.</summary>
        public void Play(Vector3 position)
        {
            transform.position = position;
            gameObject.SetActive(true);
            _ps.Play();
            PlayConfiguredSoundAt(position);
        }

        /// <summary>Plays explosion sound at a world position.</summary>
        public static void PlaySoundAt(Vector3 position)
        {
            PlaySoundAt(position, SoundId.Explosion);
        }

        /// <summary>Plays a specific explosion sound at a world position.</summary>
        public static void PlaySoundAt(Vector3 position, SoundId soundId)
        {
            PublishSound(position, soundId);
        }

        /// <summary>Plays this prefab's configured explosion sound at a world position.</summary>
        public void PlayConfiguredSoundAt(Vector3 position)
        {
            if (explosionSound != null)
            {
                if (audioSource != null) audioSource.PlayOneShot(explosionSound);
                else AudioSource.PlayClipAtPoint(explosionSound, position);
                return;
            }
            PublishSound(position, explosionSoundId);
        }

        private static void PublishSound(Vector3 position, SoundId soundId)
        {
            if (soundId == SoundId.None)
                return;

            AudioManager.PlayGameplaySound(soundId);
        }

        private void OnParticleSystemStopped()
        {
            ReturnToPool();
        }

        private void ReturnToPool()
        {
            if (ProjectilePool.Instance != null)
                ProjectilePool.Instance.ReturnExplosion(this);
            else
                gameObject.SetActive(false);
        }
    }
}
