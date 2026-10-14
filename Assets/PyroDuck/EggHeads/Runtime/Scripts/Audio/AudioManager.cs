using System.Collections.Generic;
using UnityEngine;
using com.pyroduck.eggheads.Runtime.Scripts.Events;
using com.pyroduck.eggheads.Runtime.Scripts.Utils;

namespace com.pyroduck.eggheads.Runtime.Scripts.Audio
{
    /// <summary>
    /// Pooled, event-driven audio playback.
    /// Drop one of these in the scene, assign an <see cref="AudioLibrarySO"/>, and any
    /// system can fire <see cref="PlaySoundEvent"/> or <see cref="PlaySoundAtEvent"/>
    /// without holding a direct reference.
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public class AudioManager : MonoBehaviour
    {
        private const string DefaultManagerResourcePath = "Prefabs/AudioManager";

        private sealed class StaticState
        {
            public AudioManager Instance;
        }

        // The holder itself is immutable; its contents are explicitly reset for
        // Fast Enter Play Mode by ResetStaticState below.
        private static readonly StaticState State = new();

        public static void EnsureInstance()
        {
            if (State.Instance != null) return;

            var prefab = Resources.Load<GameObject>(DefaultManagerResourcePath);
            if (prefab != null)
            {
                Instantiate(prefab);
                return;
            }

            new GameObject(nameof(AudioManager)).AddComponent<AudioManager>();
        }

        /// <summary>
        /// Plays a gameplay SFX at full volume (2D). Use for explosions and other
        /// effects that must stay audible in side-scrollers regardless of camera distance.
        /// </summary>
        public static void PlayGameplaySound(SoundId id, float volumeScale = 1f)
        {
            PlaySound(id, volumeScale);
        }

        public static void PlaySound(SoundId id, float volumeScale = 1f)
        {
            if (id == SoundId.None) return;

            EnsureInstance();
            State.Instance?.PlayInternal(id, null, false, ResolveVolumeScale(volumeScale));
        }

        public static void PlaySoundAt(SoundId id, Vector3 position, float volumeScale = 1f)
        {
            if (id == SoundId.None) return;

            EnsureInstance();
            State.Instance?.PlayInternal(id, position, true, ResolveVolumeScale(volumeScale));
        }

        public static void PlayLoopingSoundAt(SoundId id, GameObject owner, Vector3 position, float volumeScale, float pitchScale = 1f)
        {
            if (id == SoundId.None || owner == null) return;

            EnsureInstance();
            State.Instance?.PlayOrUpdateLoop(new PlayLoopingSoundAtEvent
            {
                Id = id,
                Owner = owner,
                Position = position,
                VolumeScale = volumeScale,
                PitchScale = pitchScale
            });
        }

        public static void StopLoopingSound(GameObject owner, SoundId id = SoundId.None)
        {
            if (State.Instance == null || owner == null) return;

            State.Instance.OnStopLoopingSound(new StopLoopingSoundEvent
            {
                Owner = owner,
                Id = id
            });
        }

        /// <summary>
        /// Resets the static singleton reference when the runtime starts.
        /// Required when "Enter Play Mode Options → Reload Domain" is disabled,
        /// otherwise the stale reference from the previous play session would
        /// survive and every new AudioManager would self-destruct on Awake.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            State.Instance = null;
        }

        [Header("Library")]
        [SerializeField] private AudioLibrarySO library;
        [Tooltip("When no library is assigned, load Runtime/Resources/Audio/AudioLibrary.asset.")]
        [SerializeField] private bool autoLoadDefaultLibrary = true;
        [SerializeField] private string defaultLibraryResourcePath = "Audio/AudioLibrary";

        [Header("Pool")]
        [Tooltip("Number of AudioSource components created at startup.")]
        [SerializeField] private int initialPoolSize = 8;

        [Tooltip("Hard cap of simultaneously playing sources. At the cap, the oldest one-shot SFX is reused.")]
        [SerializeField] private int maxPoolSize = 32;

        [Header("Behaviour")]
        [Tooltip("If true, this manager survives scene loads.")]
        [SerializeField] private bool dontDestroyOnLoad = false;

        [Tooltip("Skip identical SoundIds requested within this window (seconds). 0 disables.")]
        [SerializeField] private float duplicateGuardWindow = 0.04f;

        [Tooltip("Master multiplier applied on top of every sound's volume.")]
        [Range(0f, 1f)]
        [SerializeField] private float masterVolume = 1f;

        // ── Runtime ───────────────────────────────────────────────────────────

        private readonly Queue<AudioSource> _free            = new Queue<AudioSource>();
        private readonly List<AudioSource>  _busy            = new List<AudioSource>();
        private readonly Dictionary<SoundId, float> _lastPlayedAt = new Dictionary<SoundId, float>();
        private readonly Dictionary<LoopKey, LoopingSource> _looping = new Dictionary<LoopKey, LoopingSource>();
        private readonly List<LoopKey> _loopCleanupBuffer = new List<LoopKey>();
        private bool _isDisabling;
        // ── Lifecycle ─────────────────────────────────────────────────────────

        private void Awake()
        {
            if (State.Instance != null && State.Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            State.Instance = this;
            if (dontDestroyOnLoad) DontDestroyOnLoad(gameObject);

            ResolveLibrary();

            for (int i = 0; i < initialPoolSize; i++)
                _free.Enqueue(CreateSource());
        }

        private void OnEnable()
        {
            _isDisabling = false;
            // Awake is skipped when Fast Enter Play Mode also disables Scene Reload.
            // Rebind the instance after ResetStaticState clears the previous session.
            if (State.Instance == null)
                State.Instance = this;

            if (State.Instance != this)
                return;

            EventManager.Subscribe<PlaySoundEvent>(OnPlaySound);
            EventManager.Subscribe<PlaySoundAtEvent>(OnPlaySoundAt);
            EventManager.Subscribe<PlayLoopingSoundAtEvent>(OnPlayLoopingSoundAt);
            EventManager.Subscribe<StopLoopingSoundEvent>(OnStopLoopingSound);
            EventManager.Subscribe<SetAudioMasterVolumeEvent>(OnSetMasterVolume);
        }

        private void OnDisable()
        {
            _isDisabling = true;
            EventManager.Unsubscribe<PlaySoundEvent>(OnPlaySound);
            EventManager.Unsubscribe<PlaySoundAtEvent>(OnPlaySoundAt);
            EventManager.Unsubscribe<PlayLoopingSoundAtEvent>(OnPlayLoopingSoundAt);
            EventManager.Unsubscribe<StopLoopingSoundEvent>(OnStopLoopingSound);
            EventManager.Unsubscribe<SetAudioMasterVolumeEvent>(OnSetMasterVolume);
            StopAllLoopingSounds();

            for (int i = _busy.Count - 1; i >= 0; i--)
            {
                if (_busy[i] != null)
                    ReturnToPool(_busy[i]);
            }

            _busy.Clear();
            _lastPlayedAt.Clear();
        }

        private void OnDestroy()
        {
            if (State.Instance == this) State.Instance = null;
        }

        private void Update()
        {
            // Recycle finished sources back to the free queue.
            for (int i = _busy.Count - 1; i >= 0; i--)
            {
                var src = _busy[i];
                if (src == null)
                {
                    _busy.RemoveAt(i);
                    continue;
                }

                if (!src.isPlaying)
                {
                    ReturnToPool(src);
                    _busy.RemoveAt(i);
                }
            }

            _loopCleanupBuffer.Clear();
            foreach (var pair in _looping)
            {
                if (pair.Value.Owner == null || pair.Value.Source == null)
                    _loopCleanupBuffer.Add(pair.Key);
            }

            for (int i = 0; i < _loopCleanupBuffer.Count; i++)
                StopLoopingSound(_loopCleanupBuffer[i]);
        }

        // ── Event handlers ────────────────────────────────────────────────────

        private void OnPlaySound(PlaySoundEvent evt)
        {
            PlayInternal(evt.Id, null, false, ResolveVolumeScale(evt.VolumeScale));
        }

        private void OnPlaySoundAt(PlaySoundAtEvent evt)
        {
            PlayInternal(evt.Id, evt.Position, true, ResolveVolumeScale(evt.VolumeScale));
        }

        private void OnPlayLoopingSoundAt(PlayLoopingSoundAtEvent evt)
        {
            PlayOrUpdateLoop(evt);
        }

        private void OnStopLoopingSound(StopLoopingSoundEvent evt)
        {
            if (evt.Owner == null)
                return;

            if (evt.Id != SoundId.None)
            {
                StopLoopingSound(new LoopKey(evt.Owner.GetObjectId(), evt.Id));
                return;
            }

            _loopCleanupBuffer.Clear();
            ulong ownerId = evt.Owner.GetObjectId();
            foreach (var key in _looping.Keys)
            {
                if (key.OwnerId == ownerId)
                    _loopCleanupBuffer.Add(key);
            }

            for (int i = 0; i < _loopCleanupBuffer.Count; i++)
                StopLoopingSound(_loopCleanupBuffer[i]);
        }

        private void OnSetMasterVolume(SetAudioMasterVolumeEvent evt)
        {
            masterVolume = Mathf.Clamp01(evt.Volume);
            foreach (var loop in _looping.Values)
                ApplyLoopVolume(loop);
        }

        // ── Core ──────────────────────────────────────────────────────────────

        private void PlayInternal(SoundId id, Vector3? position, bool positional, float volumeScale)
        {
            if (id == SoundId.None || library == null) return;

            // Prevent amplitude doubling for non-positional sounds requested many times same frame.
            // Positional playback (explosions, impacts, etc.) must not dedupe by id alone.
            if (duplicateGuardWindow > 0f
                && !positional
                && _lastPlayedAt.TryGetValue(id, out float last)
                && Time.unscaledTime - last < duplicateGuardWindow)
                return;

            var entry = library.Get(id);
            if (entry == null)
            {
                Debug.LogWarning($"[AudioManager] No SoundEntry registered for '{id}'.");
                return;
            }

            var clip = entry.GetRandomClip();
            if (clip == null) return;

            var src = AcquireSource();
            if (src == null)
                return;

            ConfigureSource(src, entry, clip, positional, volumeScale, 1f, false);

            if (positional && position.HasValue)
            {
                src.transform.SetParent(null, true);
                src.transform.position = position.Value;
            }
            else
            {
                src.transform.SetParent(transform, false);
                src.transform.localPosition = Vector3.zero;
            }

            src.Play();
            _busy.Add(src);
            if (!positional)
                _lastPlayedAt[id] = Time.unscaledTime;
        }

        private void PlayOrUpdateLoop(PlayLoopingSoundAtEvent evt)
        {
            if (evt.Id == SoundId.None || evt.Owner == null || library == null)
                return;

            var key = new LoopKey(evt.Owner.GetObjectId(), evt.Id);
            if (!_looping.TryGetValue(key, out var loop) || loop.Source == null)
            {
                var entry = library.Get(evt.Id);
                if (entry == null)
                {
                    Debug.LogWarning($"[AudioManager] No SoundEntry registered for looping sound '{evt.Id}'.");
                    return;
                }

                var clip = entry.GetRandomClip();
                if (clip == null) return;

                var src = AcquireSource();
                if (src == null) return;

                loop = new LoopingSource
                {
                    Owner = evt.Owner,
                    Source = src,
                    BaseVolume = entry.GetRandomVolume(),
                    BasePitch = entry.GetRandomPitch(),
                };

                ConfigureSource(src, entry, clip, true, 1f, 1f, true);
                _looping[key] = loop;
            }

            loop.VolumeScale = Mathf.Clamp01(evt.VolumeScale);
            loop.PitchScale = evt.PitchScale > 0f ? evt.PitchScale : 1f;

            var source = loop.Source;
            source.transform.SetParent(null, true);
            source.transform.position = evt.Position;
            source.pitch = Mathf.Clamp(loop.BasePitch * loop.PitchScale, 0.1f, 3f);
            ApplyLoopVolume(loop);

            if (!source.isPlaying && source.volume > 0f)
                source.Play();
        }

        private void ApplyLoopVolume(LoopingSource loop)
        {
            if (loop?.Source == null)
                return;

            loop.Source.volume = Mathf.Clamp01(loop.BaseVolume * loop.VolumeScale * masterVolume);
        }

        private void StopLoopingSound(LoopKey key)
        {
            if (!_looping.TryGetValue(key, out var loop))
                return;

            _looping.Remove(key);
            if (loop.Source != null)
                ReturnToPool(loop.Source);
        }

        private void StopAllLoopingSounds()
        {
            _loopCleanupBuffer.Clear();
            foreach (var key in _looping.Keys)
                _loopCleanupBuffer.Add(key);

            for (int i = 0; i < _loopCleanupBuffer.Count; i++)
                StopLoopingSound(_loopCleanupBuffer[i]);
        }

        private void ResolveLibrary()
        {
            if (library != null || !autoLoadDefaultLibrary) return;
            if (string.IsNullOrWhiteSpace(defaultLibraryResourcePath)) return;

            library = Resources.Load<AudioLibrarySO>(defaultLibraryResourcePath);
            if (library == null)
                Debug.LogWarning($"[AudioManager] Could not load AudioLibrarySO at Resources/{defaultLibraryResourcePath}.");
        }

        private void ConfigureSource(AudioSource src, SoundEntry entry, AudioClip clip,
                                     bool positional, float volumeScale, float pitchScale, bool loop)
        {
            src.clip                  = clip;
            src.volume                = Mathf.Clamp01(entry.GetRandomVolume() * volumeScale * masterVolume);
            src.pitch                 = Mathf.Clamp(entry.GetRandomPitch() * Mathf.Max(0.1f, pitchScale), 0.1f, 3f);
            src.outputAudioMixerGroup = entry.MixerGroup;
            src.spatialBlend          = positional ? entry.SpatialBlend : 0f;
            src.minDistance           = entry.MinDistance;
            src.maxDistance           = entry.MaxDistance;
            src.rolloffMode           = AudioRolloffMode.Linear;
            src.loop                  = loop;
            src.dopplerLevel          = 0f;
        }

        private static float ResolveVolumeScale(float requested)
            => requested > 0f ? requested : 1f;

        // ── Pool ──────────────────────────────────────────────────────────────

        private AudioSource AcquireSource()
        {
            if (_free.Count > 0) return _free.Dequeue();

            int total = _busy.Count + _free.Count + _looping.Count;
            if (total < maxPoolSize) return CreateSource();

            // Reuse the oldest one-shot source at the cap. Looping sources are
            // intentionally kept alive, so null means every source is reserved.
            while (_busy.Count > 0)
            {
                var oldest = _busy[0];
                _busy.RemoveAt(0);

                if (oldest == null)
                    continue;

                oldest.Stop();
                return oldest;
            }

            return null;
        }

        private void ReturnToPool(AudioSource src)
        {
            if (src == null)
                return;
            
            src.Stop();
            src.clip = null;
            src.volume = 1f;
            src.pitch = 1f;
            src.loop = false;
            if (!_isDisabling)
            {
                src.transform.SetParent(transform, false);
                src.transform.localPosition = Vector3.zero;
            }
            _free.Enqueue(src);
        }

        private AudioSource CreateSource()
        {
            int index = _busy.Count + _free.Count + 1;
            var go = new GameObject($"AudioSource_{index:00}");
            go.transform.SetParent(transform, false);

            var src = go.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.loop        = false;
            return src;
        }

        private readonly struct LoopKey : System.IEquatable<LoopKey>
        {
            public readonly ulong OwnerId;
            public readonly SoundId Id;

            public LoopKey(ulong ownerId, SoundId id)
            {
                OwnerId = ownerId;
                Id = id;
            }

            public bool Equals(LoopKey other) => OwnerId == other.OwnerId && Id == other.Id;
            public override bool Equals(object obj) => obj is LoopKey other && Equals(other);
            public override int GetHashCode() => (OwnerId.GetHashCode() * 397) ^ (int)Id;
        }

        private sealed class LoopingSource
        {
            public GameObject Owner;
            public AudioSource Source;
            public float BaseVolume = 1f;
            public float BasePitch = 1f;
            public float VolumeScale = 1f;
            public float PitchScale = 1f;
        }
    }
}
