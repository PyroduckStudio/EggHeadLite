using UnityEngine;
using com.pyroduck.eggheads.Runtime.Scripts.Combat; 
using com.pyroduck.eggheads.Runtime.Scripts.Enums;
using com.pyroduck.eggheads.Runtime.Scripts.Events;
using com.pyroduck.eggheads.Runtime.Scripts.Utils;
using com.pyroduck.eggheads.Runtime.Scripts.UI;

namespace com.pyroduck.eggheads.Runtime.Scripts.Character
{
    [RequireComponent(typeof(HealthComponent))]
    [RequireComponent(typeof(WeaponLoadoutController))]
    public class EggHeadController : BaseCharacterController, IDamageable
    {
        [Header("Movement Settings")] [SerializeField]
        private float moveSpeed = 5f;

        [SerializeField] private float crouchMultiplier = 0.5f;
        [SerializeField] private float runSpeedMultiplier = 1.5f;

        [Header("Hazard Effects")]
        [SerializeField] private ParticleSystem hazardBloodParticlePrefab;

        [SerializeField] private CharacterState _characterState;

        private HealthComponent _health;
        private WeaponLoadoutController _weaponLoadout;
        private GameObject _characterVisualInstance;
        private HealthComponent health => _health != null ? _health : (_health = GetComponent<HealthComponent>());
        public CharacterVisualController CurrentVisualController => characterVisualController;
        public bool IsAlive => _characterState == CharacterState.Alive;
        public GameObject CharacterBasePrefab => characterBasePrefab;
 

        protected override void Awake()
        {
            base.Awake();
            _weaponLoadout = GetComponent<WeaponLoadoutController>();
            _characterState = CharacterState.Alive;
        }

        protected override void Start()
        {
            base.Start();

            if (characterBasePrefab != null && characterVisualController == null)
                ApplyCharacterPrefab(characterBasePrefab);
            else if (characterBasePrefab == null)
                CharacterSelectUI.TrySelectFirstCharacter();
        }

        public void SetCharacterPrefab(GameObject prefab) { ApplyCharacterPrefab(prefab); }

        private void ApplyCharacterPrefab(GameObject prefab)
        {
            if (prefab == null) return;

            characterBasePrefab = prefab;
            RemoveCurrentCharacterVisual();

            var obj = Instantiate(prefab, characterVisualParentTransform);
            _characterVisualInstance = obj;
            obj.transform.localPosition = Vector3.zero;
            obj.transform.localRotation = Quaternion.identity;
            obj.transform.localScale = Vector3.one;

            characterVisualController = obj.GetComponent<CharacterVisualController>()
                                        ?? obj.GetComponentInChildren<CharacterVisualController>(true);
            if (characterVisualController != null)
            {
                characterVisualController.SetAnimatedBody(animatedBodyParent);
                characterVisualController.SetRotatingBody(rotatingBodyParent);
            }

            _weaponLoadout?.BindCurrentWeaponOwner();
        }

        private void RemoveCurrentCharacterVisual()
        {
            // Hide immediately so deferred Destroy cannot leave two visuals visible
            // during the character-selection frame.
            if (_characterVisualInstance != null)
            {
                _characterVisualInstance.SetActive(false);
                Destroy(_characterVisualInstance);
            }

            if (characterVisualParentTransform != null)
            {
                for (int i = characterVisualParentTransform.childCount - 1; i >= 0; i--)
                {
                    var child = characterVisualParentTransform.GetChild(i);
                    if (child == null || child.gameObject == _characterVisualInstance)
                        continue;

                    var visual = child.GetComponent<CharacterVisualController>()
                                 ?? child.GetComponentInChildren<CharacterVisualController>(true);
                    if (visual == null)
                        continue;

                    child.gameObject.SetActive(false);
                    Destroy(child.gameObject);
                }
            }

            _characterVisualInstance = null;
            characterVisualController = null;
        }
        
        public GameObject GetRootOwner()
        {
            return transform.root != null ? transform.root.gameObject : gameObject;
        }
        
        protected override void OnEnable()
        {
            EventManager.Subscribe<TriggerJumpEvent>(OnTriggerJumpEvent);
            health.OnHealthUpdated += SyncCharacterStateFromHealth;
            SyncCharacterStateFromHealth(health.Health);
            EventManager.Subscribe<CharacterSelectedEvent>(OnCharacterSelected);
            base.OnEnable();
        }

        protected override void OnDisable()
        {
            if (_health != null)
                _health.OnHealthUpdated -= SyncCharacterStateFromHealth;
            EventManager.Unsubscribe<TriggerJumpEvent>(OnTriggerJumpEvent);
            EventManager.Unsubscribe<CharacterSelectedEvent>(OnCharacterSelected);
            base.OnDisable();
        }

        private void OnCharacterSelected(CharacterSelectedEvent evt)
        {
            ApplyCharacterPrefab(evt.Prefab);
        }

        private void SyncCharacterStateFromHealth(float currentHealth)
        {
            bool wasAlive = _characterState == CharacterState.Alive;
            bool wasDead  = _characterState == CharacterState.Dead;
            _characterState = currentHealth > 0f ? CharacterState.Alive : CharacterState.Dead;

            if (wasAlive && _characterState == CharacterState.Dead)
            {
                EventManager.Publish(new SetMovementEvent
                {
                    Direction   = 0f,
                    IsWalking   = false,
                    IsCrouching = false,
                    IsRunning   = false,
                });
            }
            else if (wasDead && _characterState == CharacterState.Alive)
            {
                EventManager.Publish(new CharacterRevivedEvent { Source = gameObject });
            }
        }

        // ── Update ────────────────────────────────────────────────────────────

        protected override void Update()
        {
            if (_characterState != CharacterState.Alive)
                return;

            base.Update();
            _weaponLoadout?.TickInput();
        }

        private void FixedUpdate()
        {
            if (eggRigidBody2D == null) return;

            Vector2 v = eggRigidBody2D.GetLinearVelocity();

            if (_characterState != CharacterState.Alive)
            {
                eggRigidBody2D.SetLinearVelocity(new Vector2(0f, v.y));
                return;
            }

            eggRigidBody2D.SetLinearVelocity(new Vector2(
                (IsRunning ? runSpeedMultiplier : 1f) *
                (IsCrouching ? crouchMultiplier : 1f) *
                Move.x * moveSpeed,
                v.y));
        }

        // ── Jump ──────────────────────────────────────────────────────────────

        private void OnTriggerJumpEvent(TriggerJumpEvent obj)
        {
            if (_characterState != CharacterState.Alive)
                return;

            if (eggRigidBody2D != null)
            {
                eggRigidBody2D.AddForce(Vector2.up * jumpForce, ForceMode2D.Impulse);
                isJumping = true;
                isGrounded = false;
                jumpGroundCooldown = JumpGroundCooldownDuration;
            }
        }

        // ── Damage ────────────────────────────────────────────────────────────

        public void TakeDamage(float amount, GameObject source, Vector2 hitPoint)
        { 
            if (health.ApplyDamage(amount, source, hitPoint))
                animationsController?.OnTakeDamageButtonDown();
        }


        /// <summary>
        /// Called from <see cref="EggHeadPhysicsContactRelay"/> on the collider object; hazard
        /// callbacks do not reach this script when the capsule lives on a child GameObject.
        /// </summary>
        public void TryApplyHazardDamage(Collider2D other)
        {
            TryApplyHazardDamage(other, transform.position);
        }

        public void TryApplyHazardDamage(Collider2D other, Vector2 hitPoint)
        {
            if (other == null) return;

            var hazard = other.GetComponent<SpikeHazard>()
                         ?? other.GetComponentInParent<SpikeHazard>();
            if (hazard == null) return;

            if (ApplyDamage(hazard.Damage, hazard.gameObject))
                PlayHazardBloodParticle(hitPoint);
        }

        private bool ApplyDamage(float amount, GameObject source)
        {
            if (amount <= 0f) return false;
            return health.ApplyDamage(amount, source);
        }

        private void PlayHazardBloodParticle(Vector2 hitPoint)
        {
            if (hazardBloodParticlePrefab == null) return;

            var blood = Instantiate(hazardBloodParticlePrefab, hitPoint, Quaternion.identity);
            var main = blood.main;
            if (main.stopAction == ParticleSystemStopAction.None)
                main.stopAction = ParticleSystemStopAction.Destroy;
            blood.Play();
        }
    }
}
