using com.pyroduck.eggheads.Runtime.Scripts.Character;
using UnityEngine; 
using com.pyroduck.eggheads.Runtime.Scripts.Pool;
using com.pyroduck.eggheads.Runtime.Scripts.Audio;
using com.pyroduck.eggheads.Runtime.Scripts.Events;

namespace com.pyroduck.eggheads.Runtime.Scripts.Combat
{
    public enum WeaponCategory
    {
        Throwable,
        Ranged,
        Melee
    }

    public enum ThrowableAttackType
    {
        ThrowOnly,       // Throw, stick 2s, drop — must be retrieved
        ThrowAndReturn,  // Boomerang — returns to owner
        Explosive,       // Explodes on impact — respawns in hand
        Shuriken         // Unlimited ammo — sticks 5s then destroys
    }

    public enum RangedAttackType
    {
        Gun,
        Rifle,
        Shotgun,
        Sniper,
        RocketLauncher
    }

    public enum MeleeAttackType
    {
        Sword,
        BaseballBat,
        Pan
    }

    public interface IWeaponAttack
    {
        bool TryAttack(Vector2 attackDirection, bool fireDown, bool fireHeld);
    }

    public interface IMeleeWeapon
    {
        void ExecuteMeleeAttack(Vector2 attackDirection);
    }

    public interface IRangedWeapon
    {
        void ExecuteRangedAttack(Vector2 attackDirection);
    }

    public interface IThrowableWeapon
    {
        void ExecuteThrowableAttack(Vector2 attackDirection);
    }

    public abstract class Weapon : MonoBehaviour, IWeaponAttack
    {
        [Header("Legacy Lite Audio")]
        [SerializeField] protected AudioClip equipSound;
        [SerializeField] protected AudioClip dropSound;
        [SerializeField] protected AudioSource audioSource;

        [Header("Audio")] 
        [SerializeField] protected SoundId equipSoundId = SoundId.WeaponEquip;
        [SerializeField] protected SoundId dropSoundId = SoundId.WeaponDrop;
        [SerializeField] protected SoundId hitSoundId = SoundId.WeaponHit;
       

        protected ProjectilePool _pool;
        [SerializeField] private GameObject _owner;
        public GameObject Owner => _owner;
        protected virtual void Awake()
        {
            _pool = GetComponentInParent<ProjectilePool>();
        }
 
        public void SetOwner(GameObject owner)
        {
            _owner = owner;
        }
        public virtual void SetPool(ProjectilePool pool)
        {
            _pool = pool;
        }

        [Header("Common Settings")] [SerializeField]
        protected float attackCooldown = 0.2f;

        protected float _nextAttackTime;

        public virtual bool TryAttack(Vector2 attackDirection, bool fireDown, bool fireHeld)
        {
            if (Time.time < _nextAttackTime) return false;
            if (!CanAttack(fireDown, fireHeld)) return false;

            EnsureOwnerBound();
            _nextAttackTime = Time.time + attackCooldown;
            ExecuteAttack(attackDirection); 
            return true;
        }

        protected void EnsureOwnerBound()
        {
            if (_owner != null) return;

            var character = GetComponentInParent<BaseCharacterController>();
            if (character != null)
            {
                _owner = character.transform.root != null
                    ? character.transform.root.gameObject
                    : character.gameObject;
            }
        }
         
        public virtual void PlayEquipSound()
        {
            if (equipSound != null) { PlayLegacySound(equipSound, audioSource, transform.position); return; }
            PublishSoundAt(equipSoundId, transform.position);
        }

        public virtual void PlayDropSound()
        {
            if (dropSound != null) { PlayLegacySound(dropSound, audioSource, transform.position); return; }
            PublishSoundAt(dropSoundId, transform.position);
        }
        
        public virtual void PlayHitSound()
        {
            PublishSoundAt(hitSoundId, transform.position);
        }

        protected static void PlayLegacySound(AudioClip clip, AudioSource source, Vector3 point)
        {
            if (source != null) source.PlayOneShot(clip);
            else AudioSource.PlayClipAtPoint(clip, point);
        }

        protected static void PublishSoundAt(SoundId id, Vector3 position, float volumeScale = 1f)
        {
            if (id == SoundId.None) return;

            AudioManager.EnsureInstance();
            EventManager.Publish(new PlaySoundAtEvent
            {
                Id = id,
                Position = position,
                VolumeScale = volumeScale
            });
        }

        protected abstract bool CanAttack(bool fireDown, bool fireHeld);
        protected abstract void ExecuteAttack(Vector2 attackDirection);
 
    }
}
