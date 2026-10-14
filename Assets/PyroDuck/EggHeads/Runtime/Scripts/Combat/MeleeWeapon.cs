using System.Collections.Generic;
using com.pyroduck.eggheads.Runtime.Scripts.Audio;
using com.pyroduck.eggheads.Runtime.Scripts.Character;
using com.pyroduck.eggheads.Runtime.Scripts.Utils;
using UnityEngine;
namespace com.pyroduck.eggheads.Runtime.Scripts.Combat
{
    /// <summary>
    /// Swing-based melee weapon. Damage scales with the angular speed (deg/s) of the
    /// parent weapon pivot, so faster swings deal more damage.
    /// Each press of the attack input defines a single "swing window" during which
    /// a given target can only be hit once.
    /// </summary>
    [DefaultExecutionOrder(100)]
    public class MeleeWeapon : Weapon, IMeleeWeapon
    {
        [SerializeField] private AudioClip airSwingClip;
        [SerializeField] private AudioSource airSwingAudioSource;

        [Header("Damage Settings")]
        [SerializeField] private float baseDamage = 10f;

        [Tooltip("Final damage = baseDamage * (1 + angularSpeedDegPerSec * velocityMultiplier). Set velocityMultiplier to 0 to ignore swing speed.")]
        [SerializeField] private float velocityMultiplier = 0.01f;

        [Tooltip("Clamp applied to the derived swing multiplier to avoid one-hit kills.")]
        [SerializeField] private float maxDamageMultiplier = 3f;

        [Tooltip("Optional collision filter. Set to Everything (-1) to hit any layer that carries an IDamageable.")]
        [SerializeField] private LayerMask damageLayers = ~0;

        [Tooltip("How long a swing window lasts (seconds). Defaults to the attack cooldown.")]
        [SerializeField] private float swingWindow = -1f;

        [Header("Impact Sounds")]
        [Tooltip("First entry matching surfaceTag is used. Entries with an empty surfaceTag act as the fallback.")]
        [SerializeField] private List<SoundData> impactSounds = new List<SoundData>();
        [Tooltip("Played when no surface-specific impact clip is configured. AudioLibrary maps this to KnifeStab.wav by default.")]
        [SerializeField] private SoundId defaultImpactSoundId = SoundId.MeleeImpact;
        [Tooltip("Minimum time between melee contact sounds while staying inside the same target trigger.")]
        [SerializeField] private float impactSoundCooldown = 0.12f;
        [Tooltip("KnifeStab is only played while the weapon is rotating faster than this angular speed (deg/s).")]
        [SerializeField] private float minImpactSoundSpeed = 160f;

        [Header("Blood Effect")]
        [Tooltip("Blood effect prefab spawned at the impact point. ParticleSystem prefabs are recommended.")]
        [SerializeField] private GameObject bloodEffectPrefab;

        [Header("Swing Sound")]
        [SerializeField] private SoundId swingSoundId = SoundId.MeleeSwing;
        [Tooltip("Angular speed (deg/s) where swing sound starts and uses minimum volume.")]
        [SerializeField] private float minSwingSpeed = 150f;
        [Tooltip("Angular speed (deg/s) where swing sound reaches maximum volume/pitch.")]
        [SerializeField] private float maxSwingSpeed = 600f;
        [SerializeField] private float minSwingVolume = 0.03f;
        [SerializeField] private float maxSwingVolume = 0.75f;
        [Tooltip("Higher values keep slow movement quieter and bring the whoosh in later.")]
        [SerializeField] private float swingVolumeCurve = 2.2f;
        [SerializeField] private float minSwingPitch = 0.75f;
        [SerializeField] private float maxSwingPitch = 0.95f;
        [Tooltip("Higher values react faster to speed changes; lower values feel smoother.")]
        [SerializeField] private float swingSpeedSmoothing = 12f;
        [Tooltip("Response used when swing speed rises. Higher values make the whoosh start immediately.")]
        [SerializeField] private float swingSpeedAttackSmoothing = 32f;
        [Tooltip("Higher values fade volume/pitch faster; lower values reduce harsh crackle at threshold.")]
        [SerializeField] private float swingAudioSmoothing = 40f;
        [Tooltip("Response used when swing volume rises. Higher values remove audible start delay.")]
        [SerializeField] private float swingAudioAttackSmoothing = 36f;

        /// <summary>Current angular speed of the weapon in degrees/second.</summary>
        private float _angularSpeedDegPerSec;
        private float _lastWorldAngle;
        private bool _hasLastAngle;

        private readonly HashSet<ulong> _hitThisSwing = new HashSet<ulong>();
        private readonly List<Collider2D> _overlapBuffer = new List<Collider2D>(16);
        private float _swingEndsAt;
        private float _smoothedSwingSpeed;
        private float _smoothedSwingVolume;
        private float _smoothedSwingPitch = 1f;
        private float _nextImpactSoundTime;
        private Collider2D _weaponCollider;

        protected override bool CanAttack(bool fireDown, bool fireHeld) => fireDown;

        protected override void Awake()
        {
            base.Awake();

            _weaponCollider = GetComponent<Collider2D>();
        }

        protected override void ExecuteAttack(Vector2 attackDirection)
        {
            _hitThisSwing.Clear();
            float window = swingWindow > 0f ? swingWindow : attackCooldown;
            _swingEndsAt = Time.time + window;

            // Targets already overlapping the trigger do not get another OnTriggerEnter; scan overlaps now.
            if (_weaponCollider != null && _weaponCollider.isTrigger)
            {
                var filter = new ContactFilter2D
                {
                    useTriggers = true,
                    useLayerMask = true,
                    layerMask = damageLayers
                };
                _overlapBuffer.Clear();
                int n = Physics2D.OverlapCollider(_weaponCollider, filter, _overlapBuffer);
                for (int i = 0; i < n; i++)
                {
                    var other = _overlapBuffer[i];
                    if (other == null) continue;
                    ApplyMeleeDamage(other.gameObject, _angularSpeedDegPerSec, other.ClosestPoint(transform.position));
                }
            }
        }

        public void ExecuteMeleeAttack(Vector2 attackDirection)
        {
            // Kept for IMeleeWeapon compatibility. Actual damage is handled by the collider.
        }

        private void LateUpdate()
        {
            if (!isActiveAndEnabled)
            {
                _hasLastAngle = false;
                _angularSpeedDegPerSec = 0f;
                StopSwingSound();
                return;
            }

            float current = transform.rotation.eulerAngles.z;
            if (current > 180f) current -= 360f;

            if (!_hasLastAngle)
            {
                _lastWorldAngle = current;
                _hasLastAngle = true;
                _angularSpeedDegPerSec = 0f;
                StopSwingSound();
                return;
            }

            float delta = Mathf.DeltaAngle(_lastWorldAngle, current);
            _lastWorldAngle = current;

            float dt = Time.deltaTime;
            _angularSpeedDegPerSec = dt > 0f ? Mathf.Abs(delta) / dt : 0f;
            UpdateSwingSound();
        }

        private void OnDisable()
        {
            _hasLastAngle = false;
            _angularSpeedDegPerSec = 0f;
            _smoothedSwingSpeed = 0f;
            _smoothedSwingVolume = 0f;
            _smoothedSwingPitch = minSwingPitch;
            StopSwingSound();
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (other == null) return;
            ApplyMeleeDamage(other.gameObject, _angularSpeedDegPerSec, other.ClosestPoint(transform.position));
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            if (other == null) return;
            var damageable = ResolveTargetDamageable(other.gameObject);
            if (damageable != null)
                _hitThisSwing.Remove(DedupKeyFor(damageable));
        }

        private void OnTriggerStay2D(Collider2D other)
        {
            if (other == null) return;
            ApplyMeleeDamage(other.gameObject, _angularSpeedDegPerSec, other.ClosestPoint(transform.position));
        }

        private void ApplyMeleeDamage(GameObject target, float angularSpeedDegPerSec, Vector2 point)
        {
            if (target == null) return;

            var myDamageable = GetComponentInParent<IDamageable>();
            var targetDamageable = ResolveTargetDamageable(target);

            if (myDamageable != null && targetDamageable != null && ReferenceEquals(myDamageable, targetDamageable)) return;

            if (((1 << target.layer) & damageLayers.value) == 0) return;

            if (targetDamageable == null) return;

            ulong key = DedupKeyFor(targetDamageable);
            if (!_hitThisSwing.Add(key)) return;

            float swingFactor = 1f + Mathf.Max(0f, angularSpeedDegPerSec) * velocityMultiplier;
            swingFactor = Mathf.Min(swingFactor, maxDamageMultiplier);
            float finalDamage = baseDamage * swingFactor;

            targetDamageable.TakeDamage(finalDamage, gameObject, point);
            if (SpawnBloodEffect(point))
                TryPlayImpactSound(target, point, angularSpeedDegPerSec);
        }

        private IDamageable ResolveTargetDamageable(GameObject target)
        {
            var damageable = target.GetComponent<IDamageable>() ?? target.GetComponentInParent<IDamageable>();
            if (damageable != null) return damageable;

            var enemyChecker = target.GetComponent<com.pyroduck.eggheads.Runtime.Scripts.EnemyActionChecker>()
                               ?? target.GetComponentInParent<com.pyroduck.eggheads.Runtime.Scripts.EnemyActionChecker>();
            if (enemyChecker != null) return new EnemyActionCheckerDamageableAdapter(enemyChecker);

            var puppetChecker = target.GetComponent<PuppetActionChecker>()
                                ?? target.GetComponentInParent<PuppetActionChecker>();
            if (puppetChecker != null) return puppetChecker;

            return null;
        }

        private static ulong DedupKeyFor(IDamageable damageable)
        {
            if (damageable is EnemyActionCheckerDamageableAdapter enemyAdapter)
                return enemyAdapter.DedupKey;

            if (damageable is UnityEngine.Object unityObj && unityObj != null)
            {
                if (unityObj is Component component)
                {
                    var health = component.GetComponentInParent<HealthComponent>();
                    if (health != null)
                        return health.GetObjectId();
                }

                return unityObj.GetObjectId();
            }

            return unchecked((ulong)(uint)damageable.GetHashCode());
        }

        private sealed class EnemyActionCheckerDamageableAdapter : IDamageable
        {
            private readonly com.pyroduck.eggheads.Runtime.Scripts.EnemyActionChecker _checker;
            public ulong DedupKey { get; }

            public EnemyActionCheckerDamageableAdapter(com.pyroduck.eggheads.Runtime.Scripts.EnemyActionChecker checker)
            {
                _checker = checker;
                DedupKey = checker != null && checker.Health != null
                    ? checker.Health.GetObjectId()
                    : checker != null
                        ? checker.GetObjectId()
                        : 0;
            }

            public void TakeDamage(float amount, GameObject source, Vector2 hitPoint)
            {
                _checker.TakeDamage(amount, source, hitPoint);
            }

            public override int GetHashCode()
            {
                return DedupKey.GetHashCode();
            }
        }

        private void PlayImpactSound(GameObject target, Vector2 point)
        {
            if (impactSounds == null || impactSounds.Count == 0)
            {
                PlayDefaultImpactSound(point);
                return;
            }

            SoundData chosen = default;
            SoundData fallback = default;
            bool found = false;
            bool hasFallback = false;

            foreach (var data in impactSounds)
            {
                if (string.IsNullOrEmpty(data.surfaceTag))
                {
                    if (!hasFallback && (data.Id != SoundId.None || data.clip != null))
                    {
                        fallback = data;
                        hasFallback = true;
                    }
                    continue;
                }

                if ((data.Id != SoundId.None || data.clip != null) && target.CompareTag(data.surfaceTag))
                {
                    chosen = data;
                    found = true;
                    break;
                }
            }

            if (!found)
            {
                if (!hasFallback)
                {
                    PlayDefaultImpactSound(point);
                    return;
                }

                chosen = fallback;
            }

            if (chosen.clip != null)
                AudioSource.PlayClipAtPoint(chosen.clip, point, chosen.volumeScale > 0f ? chosen.volumeScale : 1f);
            else PlayImpactSoundId(chosen.Id, point, chosen.volumeScale > 0f ? chosen.volumeScale : 1f);
        }

        private void TryPlayImpactSound(GameObject target, Vector2 point, float angularSpeedDegPerSec)
        {
            if (Mathf.Abs(angularSpeedDegPerSec) < minImpactSoundSpeed)
                return;

            if (Time.time < _nextImpactSoundTime)
                return;

            _nextImpactSoundTime = Time.time + Mathf.Max(0.02f, impactSoundCooldown);
            PlayImpactSound(target, point);
        }

        private void PlayDefaultImpactSound(Vector2 point)
        {
            if (defaultImpactSoundId == SoundId.None)
                return;

            PlayImpactSoundId(defaultImpactSoundId, point, 1f);
        }

        private static void PlayImpactSoundId(SoundId id, Vector2 point, float volumeScale)
        {
            if (id == SoundId.None)
                return;

            AudioManager.PlaySoundAt(id, point, volumeScale);
        }

        private void UpdateSwingSound()
        {
            float minDeg = Mathf.Max(0f, minSwingSpeed);
            float maxDeg = Mathf.Max(minDeg + 0.01f, maxSwingSpeed);

            float dt = Time.deltaTime;
            float speedSmoothing = _angularSpeedDegPerSec > _smoothedSwingSpeed
                ? Mathf.Max(swingSpeedSmoothing, swingSpeedAttackSmoothing)
                : swingSpeedSmoothing;
            _smoothedSwingSpeed = Mathf.Lerp(
                _smoothedSwingSpeed,
                _angularSpeedDegPerSec,
                SmoothingFactor(speedSmoothing, dt));

            // Stop driving the loop as soon as the actual motion stops;
            // smoothing should shape the whoosh, not prolong a finished swing.
            bool overThreshold = _angularSpeedDegPerSec >= minDeg && _smoothedSwingSpeed >= minDeg;
            float t = overThreshold ? Mathf.InverseLerp(minDeg, maxDeg, _smoothedSwingSpeed) : 0f;
            float volumeT = Mathf.Pow(Mathf.Clamp01(t), Mathf.Max(0.1f, swingVolumeCurve));
            float targetVolume = overThreshold ? Mathf.Lerp(minSwingVolume, maxSwingVolume, volumeT) : 0f;
            float targetPitch = overThreshold ? Mathf.Lerp(minSwingPitch, maxSwingPitch, t) : minSwingPitch;

            float audioResponse = targetVolume > _smoothedSwingVolume
                ? Mathf.Max(swingAudioSmoothing, swingAudioAttackSmoothing)
                : swingAudioSmoothing;
            float audioSmoothing = SmoothingFactor(audioResponse, dt);
            _smoothedSwingVolume = Mathf.Lerp(_smoothedSwingVolume, targetVolume, audioSmoothing);
            _smoothedSwingPitch = Mathf.Lerp(_smoothedSwingPitch, targetPitch, audioSmoothing);

            if (airSwingClip != null && airSwingAudioSource != null)
            {
                airSwingAudioSource.clip = airSwingClip;
                airSwingAudioSource.loop = true;
                airSwingAudioSource.volume = _smoothedSwingVolume;
                airSwingAudioSource.pitch = _smoothedSwingPitch;
                if (_smoothedSwingVolume > 0.005f && !airSwingAudioSource.isPlaying) airSwingAudioSource.Play();
                else if (_smoothedSwingVolume <= 0.005f) airSwingAudioSource.Stop();
                return;
            }
            if (swingSoundId == SoundId.None)
                return;

            if (_smoothedSwingVolume > 0.005f)
            {
                AudioManager.PlayLoopingSoundAt(
                    swingSoundId,
                    gameObject,
                    transform.position,
                    _smoothedSwingVolume,
                    _smoothedSwingPitch);
                return;
            }

            StopSwingSound();
        }

        private static float SmoothingFactor(float response, float deltaTime)
        {
            if (response <= 0f)
                return 1f;

            return 1f - Mathf.Exp(-response * deltaTime);
        }

        private bool SpawnBloodEffect(Vector2 point)
        {
            if (bloodEffectPrefab == null) return false;

            var go = Instantiate(bloodEffectPrefab, point, Quaternion.identity);
            if (go == null) return false;

            var ps = go.GetComponent<ParticleSystem>();
            if (ps != null)
                Destroy(go, ps.main.duration + ps.main.startLifetime.constantMax);
            else
                Destroy(go, 3f);

            return true;
        }

        private void StopSwingSound()
        {
            if (airSwingAudioSource != null) airSwingAudioSource.Stop();
            if (swingSoundId == SoundId.None)
                return;

            AudioManager.StopLoopingSound(gameObject, swingSoundId);
        }

    }
}
