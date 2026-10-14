using com.pyroduck.eggheads.Runtime.Scripts.Combat;
using com.pyroduck.eggheads.Runtime.Scripts.Enums;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using com.pyroduck.eggheads.Runtime.Scripts.Events;
using com.pyroduck.eggheads.Runtime.Scripts.InputSystem;
using com.pyroduck.eggheads.Runtime.Scripts.Utils;
using com.pyroduck.eggheads.Runtime.Scripts.Pool;
using TMPro;

namespace com.pyroduck.eggheads.Runtime.Scripts.Character
{
    /// <summary>
    /// Shared ground detection, input provider, and movement event broadcasting.
    /// </summary>
    public abstract class BaseCharacterController : MonoBehaviour
    {
        [SerializeField] protected GameObject characterBasePrefab;
        [SerializeField] protected Transform characterVisualParentTransform;
        [SerializeField] protected Transform animatedBodyParent;
        [SerializeField] protected Transform rotatingBodyParent;

        [Header("Ground Check")] [SerializeField]
        protected LayerMask groundLayer;

        [SerializeField] protected float groundCheckDistance = 0.1f;
        [SerializeField] protected Transform groundCheckPoint;

        [Header("Animation")] [SerializeField] protected AnimationsController animationsController;

        protected IInputProvider _input;
        [SerializeField] private Rigidbody2D _eggRigidBody2D;
        private Camera _mainCamera;


        public Rigidbody2D eggRigidBody2D => _eggRigidBody2D;
        public Transform VisualParent => characterVisualParentTransform;

        protected const float JumpGroundCooldownDuration = 0.15f;
        protected bool jumpRequestedFromUI;
        protected bool isGrounded = true;
        protected bool isJumping = false;
        protected float jumpGroundCooldown = 0f;
        [SerializeField] protected float jumpForce = 50f;

        public Vector2 Move { get; private set; } = Vector2.zero;
        public bool IsWalking { get; private set; }
        public bool IsCrouching { get; private set; }
        public bool IsRunning { get; private set; }

        private CharacterVisualController _characterVisualController;

        protected CharacterVisualController characterVisualController
        {
            get => _characterVisualController;
            set => _characterVisualController = value;
        }

        protected virtual void Start()
        {
            if (animationsController != null && characterVisualParentTransform != null)
                animationsController.SetSquashScaleTarget(characterVisualParentTransform);
        }

        protected virtual void Awake()
        {
            _input = InputProviderService.Get();
            _mainCamera = Camera.main;

            if (animationsController == null)
                animationsController = GetComponent<AnimationsController>() ??
                                       GetComponentInChildren<AnimationsController>(true);
            if (_eggRigidBody2D != null) _eggRigidBody2D.freezeRotation = true;
        }


        protected virtual void Update()
        {
            if (_input == null) return;

            PublishMovementState();

            // Process jump (both Gamepad/KB and UI)
            isGrounded = jumpGroundCooldown <= 0f && CheckGrounded();
            if (jumpGroundCooldown > 0f) jumpGroundCooldown -= Time.deltaTime;

            float yVel = _eggRigidBody2D != null ? _eggRigidBody2D.GetLinearVelocity().y : 0f;
            TickJumpSquash(yVel, isJumping, isGrounded);

            if (isJumping && isGrounded)
            {
                EventManager.Publish(new TriggerJumpEndEvent());
                isJumping = false;
            }

            if (!IsGameplayInputBlocked() && (_input.GetJumpDown() || jumpRequestedFromUI) && isGrounded)
            {
                EventSystem.current?.SetSelectedGameObject(null);
                EventManager.Publish(new TriggerJumpStartEvent());
                jumpRequestedFromUI = false;
            }

            if (characterVisualController != null)
            {
                UpdateLookDirection();
            }
        }

        private void UpdateLookDirection()
        {
            if (characterVisualController == null) return;

            if (_input != null && _input.IsPointerActive())
            {
                if (_mainCamera == null)
                    _mainCamera = Camera.main;

                if (_mainCamera != null)
                {
                    Vector3 referencePosition = characterVisualParentTransform != null
                        ? characterVisualParentTransform.position
                        : transform.position;

                    Vector2 pointerScreenPos = _input.GetPointerPosition();
                    Vector3 pointerWorldPos = _mainCamera.ScreenToWorldPoint(
                        new Vector3(pointerScreenPos.x, pointerScreenPos.y,
                            Mathf.Abs(_mainCamera.transform.position.z - referencePosition.z))
                    );

                    float horizontalDelta = pointerWorldPos.x - referencePosition.x;
                    if (Mathf.Abs(horizontalDelta) > 0.01f)
                    {
                        if (horizontalDelta < 0f)
                            characterVisualController.LookBack();
                        else
                            characterVisualController.LookForward();
                        return;
                    }
                }
            }

            if (Move.x < -0.01f)
                characterVisualController.LookBack();
            else if (Move.x > 0.01f)
                characterVisualController.LookForward();
        }


        protected void TickJumpSquash(float yVelocity, bool isJumping, bool isGrounded)
        {
            animationsController?.TickJumpSquash(yVelocity, isJumping, isGrounded);
        }

        protected bool CheckGrounded()
        {
            if (_eggRigidBody2D != null)
            {
                var collider = _eggRigidBody2D.GetComponent<Collider2D>();
                if (collider == null)
                    collider = _eggRigidBody2D.GetComponentInChildren<Collider2D>();

                if (collider != null)
                {
                    var bounds = collider.bounds;
                    const float skin = 0.02f;
                    var feet = new Vector2(bounds.center.x, bounds.min.y + skin);
                    if (ProbeGround(feet, groundCheckDistance + skin))
                        return true;
                }
            }

            return ProbeGround(GetGroundCheckOrigin(), groundCheckDistance);
        }

        private Vector2 GetGroundCheckOrigin()
        {
            return groundCheckPoint != null
                ? (Vector2)groundCheckPoint.position
                : (Vector2)transform.position;
        }

        private bool ProbeGround(Vector2 origin, float distance)
        {
#if UNITY_EDITOR
            Debug.DrawRay(origin, Vector2.down * distance, Color.red);
#endif
            if (Physics2D.Raycast(origin, Vector2.down, distance, groundLayer).collider != null)
                return true;

            // Ray origins on the collider surface can start inside the shape and miss.
            return Physics2D.OverlapCircle(origin - Vector2.down * (distance * 0.5f), 0.08f, groundLayer) != null;
        }

        protected static bool IsGameplayInputBlocked()
        {
            var eventSystem = EventSystem.current;
            if (eventSystem == null)
                return false;

            var selected = eventSystem.currentSelectedGameObject;
            if (selected == null)
                return false;

            if (selected.GetComponent<TMP_InputField>() != null)
                return true;

            return selected.GetComponent<InputField>() != null;
        }

        protected void PublishMovementState()
        {
            if (_input == null) return;

            Move = _input.GetMove();
            IsWalking = Move.sqrMagnitude > 0.01f;
            IsCrouching = _input.GetCrouchHeld();
            IsRunning = _input.GetRunHeld();

            EventManager.Publish(new SetMovementEvent
            {
                Direction = Move.x,
                IsWalking = IsWalking,
                IsCrouching = IsCrouching,
                IsRunning = IsRunning,
            });
        }

        protected virtual void OnDisable()
        {
            // Provider is shared via InputProviderService; do not disable here — the
            // service owns its lifetime and releases on domain reload.
            EventManager.Unsubscribe<CharacterDiedEvent>(OnCharacterDiedEvent);
            EventManager.Unsubscribe<CharacterRevivedEvent>(OnCharacterRevivedEvent);
        }


        protected virtual void OnEnable()
        {
            // Fast Enter Play Mode can skip Awake while InputProviderService
            // intentionally replaces its static provider for each play session.
            _input = InputProviderService.Get();
            // Provider is shared via InputProviderService; do not subscribe here — the
            // service owns its lifetime and subscriptions on domain reload.
            EventManager.Subscribe<CharacterDiedEvent>(OnCharacterDiedEvent);
            EventManager.Subscribe<CharacterRevivedEvent>(OnCharacterRevivedEvent);
        }

        private void OnCharacterDiedEvent(CharacterDiedEvent obj)
        {
            if (obj.Source == gameObject)
            {
                characterVisualController?.SetDeathEyes(true);
            }
        }

        private void OnCharacterRevivedEvent(CharacterRevivedEvent obj)
        {
            if (obj.Source == gameObject)
            {
                characterVisualController?.SetDeathEyes(false);
            }
        }
    }
}