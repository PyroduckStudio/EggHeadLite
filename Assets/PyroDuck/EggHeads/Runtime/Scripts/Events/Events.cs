using com.pyroduck.eggheads.Runtime.Scripts.Audio;
using com.pyroduck.eggheads.Runtime.Scripts.Data;
using com.pyroduck.eggheads.Runtime.Scripts.Enums;
using com.pyroduck.eggheads.Runtime.Scripts.Combat;
using UnityEngine;

namespace com.pyroduck.eggheads.Runtime.Scripts.Events
{
    /// <summary>
    /// Mobile/UI fire button was pressed. Published by MobileFireButtonController;
    /// consumed by WeaponController.
    /// </summary>
    public struct FireButtonPressedEvent
    {
    }

    /// <summary>
    /// Mobile/UI fire button was released. Published by MobileFireButtonController;
    /// consumed by WeaponController.
    /// </summary>
    public struct FireButtonReleaseEvent
    {
    }
 
    /// <summary>
    /// Animation preview button is held down in the creator UI. Published by
    /// AnimationPanelController; consumed by AnimationsController.
    /// </summary>
    public struct AnimationButtonPressedEvent
    {
        public AnimationType AnimationType;
    }

    /// <summary>
    /// Animation preview button was released in the creator UI. Published by
    /// AnimationPanelController; consumed by AnimationsController.
    /// </summary>
    public struct AnimationButtonReleasedEvent
    {
        public AnimationType AnimationType;
    }


    /// <summary>
    /// A visual option was selected in the creator UI. Published by VisualButton;
    /// consumed by color UI coordinators.
    /// </summary>
    public struct VisualSelectedEvent
    {
        public VisualDataSO VisualData;
        public VisualType VisualType;
    }



    /// <summary>
    /// Request/response event used to resolve the active CharacterColorizer without
    /// forcing UI systems to keep a direct EggHeadCreator reference.
    /// </summary>
    public class GetCharacterColorizerEvent : IResetable
    {
        public Character.CharacterColorizer Result;

        public void Reset()
        {
            Result = null;
        }
    }

    /// <summary>
    /// Requests that the active creator character swaps one visual slot. Published
    /// by VisualButton; consumed by EggHeadCreator.
    /// </summary>
    public struct CreateItemEvent
    {
        public GameObject VisualPrefab;
        public VisualType VisualType;

        /// <summary>Optional — if set, consumers use this to spawn and tag the instance for serialization.</summary>
        public Data.VisualDataSO VisualData;
    }

    /// <summary>
    /// Requests saving the current creator character as a prefab. Published by
    /// PrefabSaveController; consumed by EggHeadCreator in the Unity Editor.
    /// </summary>
    public struct SavePrefabEvent
    {
        public string FileName;
        public EggHeadDatabaseSO Database;
    }

    /// <summary>
    /// Broadcasts the current movement state for animator synchronization.
    /// Published by character controllers; consumed by animation controllers.
    /// </summary>
    public struct SetMovementEvent
    {
        public float Direction;
        public bool IsWalking;
        public bool IsCrouching;
        public bool IsRunning;
    }

    /// <summary>
    /// Health changed because damage was applied. Published by HealthComponent;
    /// consumed by animation, VFX, UI, or gameplay listeners.
    /// </summary>
    public struct TakeDamage
    {
        public float DamageAmount;
        public float CurrentHealth;
        public float MaxHealth;
        public Vector2 HitPoint;
        public GameObject Source;
        public GameObject Target;
    }

    /// <summary>
    /// Character reached zero health. Source identifies the character GameObject.
    /// </summary>
    public struct CharacterDiedEvent
    {
        public GameObject Source;
    }

    /// <summary>
    /// Character returned to a living health state. Source identifies the character GameObject.
    /// </summary>
    public struct CharacterRevivedEvent
    {
        public GameObject Source;
    }

    /// <summary>
    /// A character prefab was selected from CharacterSelectUI.
    /// </summary>
    public struct CharacterSelectedEvent
    {
        public GameObject Prefab;
    }

    /// <summary>
    /// Jump animation should begin. Published by BaseCharacterController when input
    /// requests a grounded jump; consumed by AnimationsController.
    /// </summary>
    public struct TriggerJumpStartEvent
    {
    }

    /// <summary>
    /// Jump animation should end. Published when the controller detects landing.
    /// </summary>
    public struct TriggerJumpEndEvent
    {
    }

    /// <summary>
    /// Physical jump impulse should be applied. Published by animation/UI flow;
    /// consumed by EggHeadController and EggHeadCreator.
    /// </summary>
    public struct TriggerJumpEvent
    {
    }

    /// <summary>
    /// Requests randomizing the creator character from the provided database.
    /// Published by UI; consumed by EggHeadCreator.
    /// </summary>
    public struct RandomizeCharacterEvent
    {
        public EggHeadDatabaseSO Database;
    }

    /// <summary>
    /// A dropped weapon pickup was touched. Published by WeaponPickup; consumed by
    /// WeaponLoadoutController.
    /// </summary>
    public struct WeaponPickupEvent
    {
        public Data.VisualDataSO WeaponData;
    }

    /// <summary>
    /// Fire-and-forget 2D sound playback (no spatialisation).
    /// </summary>
    public struct PlaySoundEvent
    {
        public SoundId Id;
        /// <summary>Multiplier on top of the SoundEntry volume. 0 (default) is treated as 1.</summary>
        public float VolumeScale;
    }

    /// <summary>
    /// Fire-and-forget 3D sound playback at a world position.
    /// </summary>
    public struct PlaySoundAtEvent
    {
        public SoundId Id;
        public Vector3 Position;
        /// <summary>Multiplier on top of the SoundEntry volume. 0 (default) is treated as 1.</summary>
        public float VolumeScale;
    }

    /// <summary>
    /// Starts or updates a looping positional sound owned by a GameObject.
    /// Re-publish while the sound should keep following the owner.
    /// </summary>
    public struct PlayLoopingSoundAtEvent
    {
        public SoundId Id;
        public GameObject Owner;
        public Vector3 Position;
        [Tooltip("Multiplier on top of the SoundEntry volume. 0 means silent.")]
        public float VolumeScale;
        [Tooltip("Multiplier on top of the SoundEntry pitch. 0 is treated as 1.")]
        public float PitchScale;
    }

    /// <summary>
    /// Stops a looping sound owned by a GameObject. Use SoundId.None to stop all loops for the owner.
    /// </summary>
    public struct StopLoopingSoundEvent
    {
        public GameObject Owner;
        public SoundId Id;
    }

    /// <summary>
    /// Sets the global multiplier the AudioManager applies to every playback.
    /// </summary>
    public struct SetAudioMasterVolumeEvent
    {
        /// <summary>Clamped to [0, 1] by the AudioManager.</summary>
        public float Volume;
    }
}