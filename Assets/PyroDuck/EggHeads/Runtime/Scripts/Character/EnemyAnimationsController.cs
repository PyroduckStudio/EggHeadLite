using System.Collections;
using com.pyroduck.eggheads.Runtime.Scripts.Enums;
using com.pyroduck.eggheads.Runtime.Scripts.Events;
using UnityEngine;

namespace com.pyroduck.eggheads.Runtime.Scripts.Character
{
    /// <summary>
    /// Animator preview from UI buttons: hold to test the corresponding animation, release to return to Idle.
    /// For each button, EventTrigger → PointerDown / PointerUp should be hooked to the respective On*Down / On*Up methods.
    /// Optional procedural jump squash scale on the assigned squash scale target transform.
    /// </summary>
    public class EnemyAnimationsController : MonoBehaviour
    {
        private static readonly int DirectionId = Animator.StringToHash("Direction");
        private static readonly int WalkId = Animator.StringToHash("Walk");
        private static readonly int JumpStartId = Animator.StringToHash("JumpStart");
        private static readonly int JumpingId = Animator.StringToHash("Jumping");
        private static readonly int CrouchId = Animator.StringToHash("Crouch");
        private static readonly int TakeDamage = Animator.StringToHash("TakeDamage");
        private static readonly int DamageDirection = Animator.StringToHash("DamageDirection");
        private static readonly int DeathId = Animator.StringToHash("Death");
        private static readonly int AliveId = Animator.StringToHash("Alive");

        private float jumpingStartAnimationTime;
        [SerializeField] private Animator animator;

        [Header("Jump squash (procedural scale)")] [SerializeField]
        private Transform squashScaleTarget;

        [SerializeField] private AnimationCurve upStretchCurve;
        [SerializeField] private AnimationCurve downSquashCurve;
        [SerializeField] private float maxUpVelocity = 50f;
        [SerializeField] private float maxDownVelocity = -50f;
        [SerializeField] private float stretchAmount = 0.2f;
        [SerializeField] private float squashAmount = 0.2f;

        private bool _holdIdle;
        private bool _holdWalkCenter;
        private bool _holdWalkForward;
        private bool _holdWalkBackward;
        private bool _holdJump;
        private bool _holdCrouch;
        private bool _holdDeath;

        private bool IsUiDrivingAnimator =>
            _holdIdle || _holdWalkCenter || _holdWalkForward || _holdWalkBackward || _holdJump || _holdCrouch ||
            _holdDeath;

        private void Start()
        {
            var clips = animator.runtimeAnimatorController.animationClips;

            foreach (var clip in clips)
            {
                if (clip.name == "JumpStart") // note: may not match state name exactly
                {
                    jumpingStartAnimationTime = clip.length;
                }
            }
        }

        public void SetSquashScaleTarget(Transform target) => squashScaleTarget = target;

        /// <summary>
        /// Applies procedural squash/stretch scale based on Y velocity during a jump.
        /// Resets scale on landing when <paramref name="isJumping"/> is true and <paramref name="isGrounded"/> is true.
        /// </summary>
        public void TickJumpSquash(float yVelocity, bool isJumping, bool isGrounded)
        {
            if (squashScaleTarget == null) return;

            if (isJumping && isGrounded)
            {
                squashScaleTarget.localScale = Vector3.one;
                return;
            }

            if (!isJumping || isGrounded) return;

            Vector3 targetScale;
            if (yVelocity > 0f)
            {
                float t = Mathf.Clamp01(yVelocity / maxUpVelocity);
                if (upStretchCurve != null && upStretchCurve.length > 0)
                    t = Mathf.Clamp01(upStretchCurve.Evaluate(t));
                float scaleY = Mathf.Lerp(1f, 1f + stretchAmount, t);
                targetScale = new Vector3(1f, scaleY, 1f);
            }
            else if (yVelocity < 0f)
            {
                float t = Mathf.Clamp01(Mathf.Abs(yVelocity) / Mathf.Abs(maxDownVelocity));
                if (downSquashCurve != null && downSquashCurve.length > 0)
                    t = Mathf.Clamp01(downSquashCurve.Evaluate(t));
                float scaleY = Mathf.Lerp(1f, 1f + squashAmount, t);
                targetScale = new Vector3(1f, scaleY, 1f);
            }
            else
            {
                targetScale = Vector3.one;
            }

            squashScaleTarget.localScale = targetScale;
        }

        public void SetMovement(float direction, bool isWalking, bool isCrouching = false)
        {
            if (animator == null || IsUiDrivingAnimator) return;

            animator.SetFloat(DirectionId, direction);
            animator.SetBool(WalkId, isWalking);
            animator.SetBool(CrouchId, isCrouching);
        }

        public void TriggerJumpStart()
        {
            if (animator != null)
            {
                animator.SetTrigger(JumpStartId);
                animator.SetBool(JumpingId, true);
            }

            // Using a coroutine keeps this refactor-safe vs the string-based Invoke API.
            if (isActiveAndEnabled)
                StartCoroutine(DispatchJumpAction(jumpingStartAnimationTime * 0.8f));
        }

        private IEnumerator DispatchJumpAction(float delay)
        {
            if (delay > 0f) yield return new WaitForSeconds(delay);
            TriggerJumpAction();
        }

        public void TriggerJumpAction()
        {
            // Enemy jump should never drive shared gameplay jump events.
        }

        public void TriggerJumpEnd()
        {
            if (animator != null)
            {
                animator.SetBool(JumpingId, false);
            }
        }

        private void OnEnable()
        {
            EventManager.Subscribe<CharacterRevivedEvent>(OnCharacterRevivedEvent);
            EventManager.Subscribe<CharacterDiedEvent>(OnCharacterDiedEvent);
        }

        private void OnDisable()
        {
            EventManager.Unsubscribe<CharacterRevivedEvent>(OnCharacterRevivedEvent);
            EventManager.Unsubscribe<CharacterDiedEvent>(OnCharacterDiedEvent);
            ReleaseAllAndIdle();
        }

        private void OnSetMovementEvent(SetMovementEvent evt) =>
            SetMovement(evt.Direction, evt.IsWalking, evt.IsCrouching);

        private void OnTriggerJumpStartEvent(TriggerJumpStartEvent evt) => TriggerJumpStart();
        private void OnTriggerJumpEndEvent(TriggerJumpEndEvent evt) => TriggerJumpEnd();

        private bool IsForMe(GameObject targetOrSource)
        {
            if (targetOrSource == null) return false;
            if (targetOrSource == gameObject) return true;
            if (transform.IsChildOf(targetOrSource.transform)) return true;
            if (targetOrSource.transform.IsChildOf(transform)) return true;
            return false;
        }

        private void OnTakeDamageEvent(TakeDamage evt)
        {
            if (!IsForMe(evt.Target)) return;
            OnTakeDamageButtonDown();
        }

        private void OnCharacterDiedEvent(CharacterDiedEvent evt)
        {
            if (!IsForMe(evt.Source)) return;
            OnDeathButtonDown();
        }

        private void OnCharacterRevivedEvent(CharacterRevivedEvent evt)
        {
            if (!IsForMe(evt.Source)) return;
            OnDeathButtonUp();
        }

        private void OnAnimationButtonPressed(AnimationButtonPressedEvent evt)
        {
            switch (evt.AnimationType)
            {
                case AnimationType.Idle: OnIdleButtonDown(); break;
                case AnimationType.Jump: OnJumpButtonDown(); break;
                case AnimationType.Walk: OnWalkButtonDown(); break;
                case AnimationType.WalkForward: OnWalkForwardButtonDown(); break;
                case AnimationType.WalkBackward: OnWalkBackwardButtonDown(); break;
                case AnimationType.Death: OnDeathButtonDown(); break;
                case AnimationType.Crouch: OnCrouchButtonDown(); break;
                case AnimationType.TakeDamage: OnTakeDamageButtonDown(); break;
            }
        }

        private void OnAnimationButtonReleased(AnimationButtonReleasedEvent evt)
        {
            switch (evt.AnimationType)
            {
                case AnimationType.Idle: OnIdleButtonUp(); break;
                case AnimationType.Jump: OnJumpButtonUp(); break;
                case AnimationType.Walk: OnWalkButtonUp(); break;
                case AnimationType.WalkForward: OnWalkForwardButtonUp(); break;
                case AnimationType.WalkBackward: OnWalkBackwardButtonUp(); break;
                case AnimationType.Death: OnDeathButtonUp(); break;
                case AnimationType.Crouch: OnCrouchButtonUp(); break;
                case AnimationType.TakeDamage: /* one-shot trigger, no up action */ break;
            }
        }

        private void ReleaseAllAndIdle()
        {
            _holdIdle = false;
            _holdWalkCenter = false;
            _holdWalkForward = false;
            _holdWalkBackward = false;
            _holdJump = false;
            _holdCrouch = false;
            _holdDeath = false;
            ApplyIdleParameters();
        }

        private void ApplyIdleParameters()
        {
            if (animator == null) return;

            animator.SetBool(WalkId, false);
            animator.SetFloat(DirectionId, 0f);
            animator.SetBool(CrouchId, false);
        }

        private void ApplyWalkLocomotion()
        {
            if (animator == null)
            {
                return;
            }

            if (_holdWalkForward)
            {
                animator.SetBool(WalkId, true);
                animator.SetFloat(DirectionId, 1f);
            }
            else if (_holdWalkBackward)
            {
                animator.SetBool(WalkId, true);
                animator.SetFloat(DirectionId, -1f);
            }
            else if (_holdWalkCenter)
            {
                animator.SetBool(WalkId, true);
                animator.SetFloat(DirectionId, 0f);
            }
            else
            {
                ApplyIdleParameters();
            }
        }

        private void RefreshAnimatorFromHolds()
        {
            if (animator == null)
            {
                return;
            }

            if (_holdJump)
            {
                return;
            }

            if (_holdIdle)
            {
                ApplyIdleParameters();
                return;
            }

            if (_holdWalkForward || _holdWalkBackward || _holdWalkCenter)
            {
                ApplyWalkLocomotion();
                return;
            }

            ApplyIdleParameters();
        }

        // --- Idle (Blend Tree center / Idle state) ---
        public void OnIdleButtonDown()
        {
            _holdIdle = true;
            _holdWalkCenter = false;
            _holdWalkForward = false;
            _holdWalkBackward = false;
            _holdJump = false;
            ApplyIdleParameters();
        }

        public void OnIdleButtonUp()
        {
            _holdIdle = false;
            RefreshAnimatorFromHolds();
        }

        // --- Jump (prevents input conflict as long as the trigger is held) ---
        public void OnJumpButtonDown()
        {
            _holdJump = true;
            TriggerJumpStart();
        }

        public void OnJumpButtonUp()
        {
            _holdJump = false;
            RefreshAnimatorFromHolds();
        }

        // --- Walk (Direction = 0, blend tree center clip) ---
        public void OnWalkButtonDown()
        {
            _holdWalkCenter = true;
            _holdIdle = false;
            ApplyWalkLocomotion();
        }

        public void OnWalkButtonUp()
        {
            _holdWalkCenter = false;
            RefreshAnimatorFromHolds();
        }

        // --- Walk forward (Direction = 1) ---
        public void OnWalkForwardButtonDown()
        {
            _holdWalkForward = true;
            _holdIdle = false;
            ApplyWalkLocomotion();
        }

        public void OnWalkForwardButtonUp()
        {
            _holdWalkForward = false;
            RefreshAnimatorFromHolds();
        }

        // --- Walk backward (Direction = -1) ---
        public void OnWalkBackwardButtonDown()
        {
            _holdWalkBackward = true;
            _holdIdle = false;
            ApplyWalkLocomotion();
        }

        public void OnWalkBackwardButtonUp()
        {
            _holdWalkBackward = false;
            RefreshAnimatorFromHolds();
        }

        // --- Death ---
        public void OnDeathButtonDown()
        {
            _holdDeath = true;
            if (animator != null)
                animator.SetTrigger(DeathId);
        }

        public void OnDeathButtonUp()
        {
            _holdDeath = false;
            if (animator != null)
                animator.SetTrigger(AliveId);
            RefreshAnimatorFromHolds();
        }

        // --- Crouch ---
        public void OnCrouchButtonDown()
        {
            _holdCrouch = true;
            if (animator != null)
                animator.SetBool(CrouchId, true);
        }

        public void OnCrouchButtonUp()
        {
            _holdCrouch = false;
            if (animator != null)
                animator.SetBool(CrouchId, false);
            RefreshAnimatorFromHolds();
        }

        // --- Take Damage (one-shot, random direction: -1 left / 0 center / 1 right) ---
        public void OnTakeDamageButtonDown()
        {
            if (animator == null) return;
            int[] directions = { -1, 0, 1 };
            int dir = directions[Random.Range(0, directions.Length)];
            animator.SetFloat(DamageDirection, dir);
            animator.SetTrigger(TakeDamage);
        }
    }
}