using System.Collections;
using UnityEngine;
using com.pyroduck.eggheads.Runtime.Scripts.Character;
using com.pyroduck.eggheads.Runtime.Scripts.Combat;
using com.pyroduck.eggheads.Runtime.Scripts.Enums;
using com.pyroduck.eggheads.Runtime.Scripts.Events;
using com.pyroduck.eggheads.Runtime.Scripts.Pool;

namespace com.pyroduck.eggheads.Runtime.Scripts
{
    public class EnemyController : MonoBehaviour
    {
        private static readonly int TakeDamageId = Animator.StringToHash("TakeDamage");
        private static readonly int DamageDirectionId = Animator.StringToHash("DamageDirection");
        private static readonly int DeathId = Animator.StringToHash("Death");
        private static readonly int AliveId = Animator.StringToHash("Alive");

        [Header("References")] public EnemyActionChecker actionChecker;
        public SpriteRenderer spriteRenderer;
        public Sprite enemySprite;
        public Sprite diedEnemySprite;
        public SpriteRenderer hitVisualizerRenderer;
        public Transform attackOrigin;
        [SerializeField] private Transform animatedBodyParent;
        [SerializeField] private Transform rotatingBodyParent;
        [SerializeField] private EnemyAnimationsController enemyAnimationsController;
        [SerializeField] private Animator animator;

        [Tooltip("Weapon or weapon-holder pivot. Rotated toward the target.")]
        public Transform weaponPivot;

        [Header("Target")] public Transform target;

        [Header("Shooting Settings")] [Tooltip("Seconds the enemy aims at the target before firing.")]
        public float aimDuration = 1.2f;

        [Tooltip("Seconds between each fire cycle.")]
        public float fireCooldown = 2.5f;

        [Tooltip("Enemy only fires when no blocking collider is between attack origin and target.")]
        [SerializeField] private bool requireLineOfSight = true;

        [Tooltip("Layers that can block enemy shooting. Leave as Everything, self and target colliders are ignored.")]
        [SerializeField] private LayerMask lineOfSightBlockingLayers = ~0;

        // ── Internal ──────────────────────────────────────────────────────────
        private RangedWeapon _rangedWeapon;
        private HealthComponent _health;
        private HealthComponent _targetHealth;
        private Coroutine _flashCoroutine;
        private Coroutine _shootLoopCoroutine;
        [SerializeField] private CharacterState _characterState;

        private bool IsDead => _characterState == CharacterState.Dead || (_health != null && !_health.IsAlive);
        private bool CanShoot =>
            _characterState == CharacterState.Alive &&
            (_health == null || _health.IsAlive) &&
            (_targetHealth == null || _targetHealth.IsAlive);

        private void Awake()
        {
            _characterState = CharacterState.Alive;

            if (actionChecker == null)
                actionChecker = GetComponentInChildren<EnemyActionChecker>(includeInactive: true);

            if (enemyAnimationsController == null)
                enemyAnimationsController = GetComponentInChildren<EnemyAnimationsController>(includeInactive: true);

            if (animator == null)
                animator = GetComponentInChildren<Animator>(includeInactive: true);

            _rangedWeapon = GetComponentInChildren<RangedWeapon>(includeInactive: true);

            // Weapons resolve the singleton ProjectilePool automatically via OnEnable/SetPool.
            // If a weapon needs an explicit SetPool call (e.g. for prefab registration), use the singleton.
            var weapon = GetComponentInChildren<Weapon>(includeInactive: true);
            if (weapon != null && ProjectilePool.Instance != null)
            {
                weapon.SetPool(ProjectilePool.Instance);
            }

            if (weapon != null)
                weapon.SetOwner(transform.root.gameObject);
        }

        private void Update()
        {
            EnsureShootingState();

            if (!IsDead) FaceTarget();
        }

        private void OnEnable()
        {
            ResolveHealth();

            if (_health != null)
            {
                _health.OnDamaged += HandleDamaged;
                _health.OnHealthUpdated += SyncCharacterStateFromHealth;
            }

            EventManager.Subscribe<CharacterDiedEvent>(OnCharacterDiedEvent);
            EventManager.Subscribe<CharacterRevivedEvent>(OnCharacterRevivedEvent);
            ResolveTargetHealth();
            SyncCharacterStateFromHealth(_health != null ? _health.Health : 1f);
            if (CanShoot)
                StartShootLoop();
        }

        private void OnDisable()
        {
            if (_health != null)
            {
                _health.OnDamaged -= HandleDamaged;
                _health.OnHealthUpdated -= SyncCharacterStateFromHealth;
            }

            EventManager.Unsubscribe<CharacterDiedEvent>(OnCharacterDiedEvent);
            EventManager.Unsubscribe<CharacterRevivedEvent>(OnCharacterRevivedEvent);
            StopShootLoop();
        }

        private HealthComponent ResolveHealth()
        {
            if (actionChecker == null)
                actionChecker = GetComponentInChildren<EnemyActionChecker>(includeInactive: true);

            _health = GetComponent<HealthComponent>();
            if (_health == null && actionChecker != null)
                _health = actionChecker.Health;

            return _health;
        }

        private HealthComponent ResolveTargetHealth()
        {
            ResolveTarget();

            _targetHealth = target != null
                ? target.GetComponentInParent<HealthComponent>()
                : null;

            return _targetHealth;
        }

        private Transform ResolveTarget()
        {
            if (target != null)
                return target;

            var player =
#if UNITY_6000_5_OR_NEWER
                FindAnyObjectByType<EggHeadController>();
#elif UNITY_2023_1_OR_NEWER || UNITY_6000_0_OR_NEWER
                FindFirstObjectByType<EggHeadController>();
#else
                FindObjectOfType<EggHeadController>();
#endif
            if (player != null)
                target = player.transform;

            return target;
        }

        private void EnsureShootingState()
        {
            ResolveTargetHealth();

            if (!CanShoot)
            {
                StopShootLoop();
                return;
            }

            if (_shootLoopCoroutine == null && _rangedWeapon != null && target != null)
                StartShootLoop();
        }


        private void HandleDamaged(float amount, GameObject source)
        {
            if (IsDead) return;

            TriggerTakeDamageAnimation();
        }


        // ── Health ────────────────────────────────────────────────────────────

        private void SyncCharacterStateFromHealth(float currentHealth)
        {
            bool wasAlive = _characterState == CharacterState.Alive;
            bool wasDead = _characterState == CharacterState.Dead;
            _characterState = currentHealth > 0f ? CharacterState.Alive : CharacterState.Dead;

            ApplySpriteForState();

            if (wasAlive && _characterState == CharacterState.Dead)
            {
                HandleDeath();
            }
            else if (wasDead && _characterState == CharacterState.Alive)
            {
                HandleRevived();
            }
        }

        private void ApplySpriteForState()
        {
            if (spriteRenderer != null)
            {
                if (_characterState == CharacterState.Dead)
                {
                    if (spriteRenderer != null && diedEnemySprite != null)
                    {
                        spriteRenderer.sprite = diedEnemySprite;
                    }
                }
                else if (_characterState == CharacterState.Alive)
                {
                    if (spriteRenderer != null && enemySprite != null)
                    {
                        spriteRenderer.sprite = enemySprite;
                    }
                }
            }
        }

        private void HandleDeath()
        {
            _characterState = CharacterState.Dead;
            ApplySpriteForState();
            StopShootLoop();
            TriggerDeathAnimation();
            
            EventManager.Publish(new SetMovementEvent
            {
                Direction   = 0f,
                IsWalking   = false,
                IsCrouching = false,
                IsRunning   = false,
            });
        }

        private void HandleRevived()
        {
            ApplySpriteForState();

            if (enemyAnimationsController != null)
            {
                enemyAnimationsController.OnDeathButtonUp();
            }
            else
            {
                animator?.SetTrigger(AliveId);
            }

            _rangedWeapon = GetComponentInChildren<RangedWeapon>(includeInactive: true);
            EventManager.Publish(new CharacterRevivedEvent { Source = gameObject });
            StartShootLoop();
        }

        private void OnCharacterDiedEvent(CharacterDiedEvent evt)
        {
            if (IsTarget(evt.Source))
            {
                ResolveTargetHealth();
                StopShootLoop();
                return;
            }

            if (!IsForMe(evt.Source)) return;
            if (_characterState == CharacterState.Dead)
            {
                StopShootLoop();
                return;
            }

            _characterState = CharacterState.Dead;
            HandleDeath();
        }

        private void OnCharacterRevivedEvent(CharacterRevivedEvent evt)
        {
            if (IsTarget(evt.Source))
            {
                ResolveTargetHealth();
                if (CanShoot)
                    StartShootLoop();
                return;
            }

            if (!IsForMe(evt.Source)) return;
            if (_characterState == CharacterState.Alive) return;

            _characterState = CharacterState.Alive;
            HandleRevived();
        }

        private bool IsForMe(GameObject source)
        {
            if (source == null) return false;
            if (source == gameObject) return true;
            if (source.transform.IsChildOf(transform)) return true;
            if (transform.IsChildOf(source.transform)) return true;
            return false;
        }

        private bool IsTarget(GameObject source)
        {
            if (source == null || target == null) return false;
            if (source == target.gameObject) return true;
            if (source.transform.IsChildOf(target)) return true;
            if (target.IsChildOf(source.transform)) return true;
            return false;
        }


        private void TriggerTakeDamageAnimation()
        {
            if (animator == null) return;

            int[] directions = { -1, 0, 1 };
            int direction = directions[Random.Range(0, directions.Length)];

            animator.SetFloat(DamageDirectionId, direction);
            animator.SetTrigger(TakeDamageId);
        }

        private void TriggerDeathAnimation()
        {
            if (enemyAnimationsController != null)
            {
                enemyAnimationsController.OnDeathButtonDown();
                return;
            }

            animator?.SetTrigger(DeathId);
        }


        // ── Shoot Loop ────────────────────────────────────────────────────────

        private void StartShootLoop()
        {
            StopShootLoop();
            ResolveTargetHealth();
            if (_rangedWeapon == null || target == null || !CanShoot) return;
            _shootLoopCoroutine = StartCoroutine(ShootLoop());
        }

        private void StopShootLoop()
        {
            if (_shootLoopCoroutine != null)
            {
                StopCoroutine(_shootLoopCoroutine);
                _shootLoopCoroutine = null;
            }
        }

        private IEnumerator ShootLoop()
        {
            while (CanShoot)
            {
                // — Aim phase —
                float aimTimer = 0f;
                while (aimTimer < aimDuration && CanShoot)
                {
                    aimTimer += Time.deltaTime;
                    yield return null;
                }

                if (!CanShoot) yield break;

                // — Fire phase —
                Fire();

                // — Cooldown phase —
                float cooldownTimer = 0f;
                while (cooldownTimer < fireCooldown && CanShoot)
                {
                    cooldownTimer += Time.deltaTime;
                    yield return null;
                }
            }
        }

        private void FaceTarget()
        {
            if (target == null) return;

            Vector2 toTarget = (Vector2)(target.position - transform.position);
            bool targetIsRight = toTarget.x > 0f;

            if (spriteRenderer != null)
                spriteRenderer.flipX = targetIsRight;
            if (hitVisualizerRenderer != null)
                hitVisualizerRenderer.flipX = targetIsRight;

            if (weaponPivot != null)
            {
                float angle = Mathf.Atan2(toTarget.y, Mathf.Abs(toTarget.x)) * Mathf.Rad2Deg;
                if (!targetIsRight) angle = 180f - angle;
                weaponPivot.rotation = Quaternion.Euler(0f, 0f, angle);
                weaponPivot.localScale = new Vector3(1f, targetIsRight ? 1f : -1f, 1f);
            }
        }

        private void Fire()
        {
            ResolveTargetHealth();
            if (target == null || _rangedWeapon == null || !CanShoot) return;

            FaceTarget();

            if (!CanShoot) return;

            Vector2 origin = GetShootOrigin();
            Vector2 targetPosition = target.position;
            if (!HasLineOfSight(origin, targetPosition)) return;

            Vector2 direction = ((Vector2)target.position - origin).normalized;

            _rangedWeapon.ExecuteRangedAttack(direction);
            _rangedWeapon.TriggerRecoil();
        }

        private Vector2 GetShootOrigin()
        {
            if (_rangedWeapon != null && _rangedWeapon.MuzzlePlace != null)
                return _rangedWeapon.MuzzlePlace.position;

            if (attackOrigin != null)
                return attackOrigin.position;

            return transform.position;
        }

        private bool HasLineOfSight(Vector2 origin, Vector2 targetPosition)
        {
            if (!requireLineOfSight || target == null) return true;

            var hits = Physics2D.LinecastAll(origin, targetPosition, lineOfSightBlockingLayers);
            foreach (var hit in hits)
            {
                if (hit.collider == null || hit.collider.isTrigger) continue;

                Transform hitTransform = hit.collider.transform;
                if (hitTransform.IsChildOf(transform)) continue;
                if (hitTransform == target || hitTransform.IsChildOf(target) || target.IsChildOf(hitTransform)) continue;

                return false;
            }

            return true;
        }
    }
}