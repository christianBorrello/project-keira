using System;
using _Scripts.Combat.Data;
using _Scripts.Combat.Hitbox;
using _Scripts.Combat.Interfaces;
using Systems;
using UnityEngine;
using UnityEngine.AI;

namespace _Scripts.Enemies
{
    /// <summary>
    /// Main enemy controller component.
    /// Implements combat interfaces and coordinates with the state machine.
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    [RequireComponent(typeof(EnemyStateMachine))]
    public class EnemyController : MonoBehaviour, ICombatant, IDamageableWithPoise, ILockOnTarget
    {
        [Header("Configuration")]
        [SerializeField]
        [Tooltip("Enable/disable enemy AI and behavior at runtime")]
        private bool isEnabled = true;

        [SerializeField]
        private CombatStats baseStats = CombatStats.CreateDefaultEnemy();

        [Header("AI Settings")]
        [SerializeField]
        [Tooltip("Detection range for player")]
        private float detectionRange = 15f;

        [SerializeField]
        [Tooltip("Range to start attacking")]
        private float attackRange = 2f;

        [SerializeField]
        [Tooltip("Range where enemy loses interest")]
        private float maxChaseRange = 25f;

        [SerializeField]
        [Tooltip("Cooldown between attacks")]
        private float attackCooldown = 2f;

        [SerializeField]
        [Tooltip("Layer mask for line of sight checks")]
        private LayerMask lineOfSightMask;

        [Header("Lock-On")]
        [SerializeField]
        private Transform lockOnPoint;

        [SerializeField]
        private int lockOnPriority = 1;

        [Header("Debug")]
        [SerializeField]
        private bool debugMode;

        // Components
        private NavMeshAgent _navAgent;
        private EnemyStateMachine _stateMachine;
        private Animator _animator;
        private HitboxController _hitboxController;

        // Runtime data
        private CombatRuntimeData _runtimeData;
        private static int _nextCombatantId = 1000; // Start at 1000 to differentiate from player
        private int _combatantId;

        // Combat tracking
        private ICombatant _currentTarget;
        private float _lastAttackTime;
        private bool _isInvulnerable;
        private bool _isParrying;
        private float _parryWindowStart;

        #region Properties

        /// <summary>
        /// Whether this enemy's AI is enabled. Can be toggled at runtime.
        /// </summary>
        public bool IsEnabled
        {
            get => isEnabled;
            set
            {
                isEnabled = value;
                if (_navAgent != null)
                {
                    _navAgent.isStopped = !value;
                }
                if (_stateMachine != null)
                {
                    _stateMachine.enabled = value;
                }
            }
        }

        public Animator Animator => _animator;
        public NavMeshAgent NavAgent => _navAgent;
        public HitboxController HitboxController => _hitboxController;
        public ICombatant CurrentTarget => _currentTarget;
        public float DetectionRange => detectionRange;
        public float AttackRange => attackRange;
        public float MaxChaseRange => maxChaseRange;

        public float DistanceToTarget
        {
            get
            {
                if (_currentTarget == null) return float.MaxValue;
                return Vector3.Distance(transform.position, _currentTarget.Position);
            }
        }

        #endregion

        #region ICombatant Implementation

        public int CombatantId => _combatantId;
        public string CombatantName => gameObject.name;
        public Faction Faction => Faction.Enemy;
        public bool IsAlive => _runtimeData.IsAlive;
        public Transform Transform => transform;
        public Vector3 Position => transform.position;
        public Vector3 Forward => transform.forward;

        public CombatStats GetBaseStats() => baseStats;
        public CombatRuntimeData GetRuntimeData() => _runtimeData;

        public bool IsParrying(out ParryTiming timing)
        {
            if (_isParrying)
            {
                timing = new ParryTiming(
                    _parryWindowStart,
                    baseStats.ParryWindowDuration,
                    baseStats.PerfectParryWindow
                );
                return true;
            }
            timing = default;
            return false;
        }

        public bool IsInvulnerable => _isInvulnerable;

        public void ApplyStagger(float duration)
        {
            _stateMachine?.ForceInterrupt(EnemyState.Stagger);
            OnStaggered?.Invoke(this, duration);
        }

        public event Action<ICombatant, AttackData> OnAttackStarted;
        public event Action<ICombatant, IDamageable, DamageResult> OnHitLanded;
        public event Action<ICombatant, float> OnStaggered;

        #endregion

        #region IDamageable Implementation

        public float CurrentHealth => _runtimeData.CurrentHealth;
        public float MaxHealth => baseStats.MaxHealth;
        public float HealthNormalized => MaxHealth > 0 ? CurrentHealth / MaxHealth : 0f;

        public DamageResult TakeDamage(DamageInfo damageInfo)
        {
            if (!IsAlive) return new DamageResult(0, 0);

            if (_isInvulnerable)
            {
                return new DamageResult(0, 0, wasDodged: true);
            }

            // Check parry (some enemies can parry)
            if (IsParrying(out var parryTiming) && damageInfo.CanBeParried)
            {
                if (parryTiming.IsPerfect)
                {
                    damageInfo.Source?.ApplyStagger(baseStats.StaggerRecoveryTime);
                    return new DamageResult(0, 0, wasParried: true);
                }
            }

            // Apply damage
            float finalDamage = damageInfo.Amount * (1f - baseStats.PhysicalDefense);
            bool causedPoiseBreak = ApplyDamageInternal(finalDamage, damageInfo.PoiseDamage);
            bool causedDeath = !IsAlive;

            var result = new DamageResult(
                finalDamage,
                damageInfo.PoiseDamage,
                causedPoiseBreak: causedPoiseBreak,
                causedDeath: causedDeath
            );

            OnDamaged?.Invoke(damageInfo, result);

            // Set aggro to attacker
            if (damageInfo.Source != null && _currentTarget == null)
            {
                SetTarget(damageInfo.Source);
            }

            if (causedDeath)
            {
                Die();
            }
            else if (causedPoiseBreak)
            {
                ApplyStagger(baseStats.StaggerRecoveryTime);
            }

            return result;
        }

        private bool ApplyDamageInternal(float damage, float poiseDamage)
        {
            float previousHealth = _runtimeData.CurrentHealth;
            _runtimeData.CurrentHealth = Mathf.Max(0, _runtimeData.CurrentHealth - Mathf.RoundToInt(damage));

            float delta = _runtimeData.CurrentHealth - previousHealth;
            OnHealthChanged?.Invoke(_runtimeData.CurrentHealth, baseStats.MaxHealth, delta);

            return ApplyPoiseDamage(poiseDamage);
        }

        public float Heal(float amount)
        {
            if (!IsAlive) return 0f;

            float previousHealth = _runtimeData.CurrentHealth;
            _runtimeData.CurrentHealth = Mathf.Min(baseStats.MaxHealth, _runtimeData.CurrentHealth + Mathf.RoundToInt(amount));

            float actualHeal = _runtimeData.CurrentHealth - previousHealth;
            OnHealthChanged?.Invoke(_runtimeData.CurrentHealth, baseStats.MaxHealth, actualHeal);

            return actualHeal;
        }

        public void Die()
        {
            _stateMachine?.ForceInterrupt(EnemyState.Death);
            OnDeath?.Invoke();
        }

        public event Action<float, float, float> OnHealthChanged;
        public event Action<DamageInfo, DamageResult> OnDamaged;
        public event Action OnDeath;

        #endregion

        #region IDamageableWithPoise Implementation

        public int CurrentPoise => _runtimeData.CurrentPoise;
        public int MaxPoise => baseStats.MaxPoise;
        public float PoiseNormalized => MaxPoise > 0 ? (float)CurrentPoise / MaxPoise : 0f;

        public bool ApplyPoiseDamage(float amount)
        {
            _runtimeData.CurrentPoise += Mathf.RoundToInt(amount);
            _runtimeData.LastPoiseHitTime = Time.time;

            if (_runtimeData.IsPoisebroken(baseStats.MaxPoise))
            {
                OnPoiseBreak?.Invoke();
                return true;
            }
            return false;
        }

        public void ResetPoise()
        {
            _runtimeData.CurrentPoise = 0;
        }

        public event Action OnPoiseBreak;

        #endregion

        #region ILockOnTarget Implementation

        public Vector3 LockOnPoint => lockOnPoint != null ? lockOnPoint.position : transform.position + Vector3.up * 1.2f;
        public bool CanBeLocked => IsAlive;
        public int LockOnPriority => lockOnPriority;
        public Transform TargetTransform => transform;

        public void OnLockedOn()
        {
            if (debugMode)
                Debug.Log($"[EnemyController] {gameObject.name} locked on");
        }

        public void OnLockReleased()
        {
            if (debugMode)
                Debug.Log($"[EnemyController] {gameObject.name} lock released");
        }

#pragma warning disable CS0067 // Event required by ILockOnTarget interface
        public event Action<bool> OnTargetValidityChanged;
#pragma warning restore CS0067

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            _combatantId = _nextCombatantId++;
            _navAgent = GetComponent<NavMeshAgent>();
            _stateMachine = GetComponent<EnemyStateMachine>();
            _animator = GetComponentInChildren<Animator>();
            _hitboxController = GetComponentInChildren<HitboxController>();

            _runtimeData = new CombatRuntimeData(baseStats);
        }

        private void Start()
        {
            _stateMachine.Initialize(this);
            _stateMachine.StartMachine(EnemyState.Idle);

            // Register with the combat system
            CombatSystem.Instance?.RegisterCombatant(this);

            // Apply initial enabled state
            if (!isEnabled)
            {
                IsEnabled = false;
            }
        }

        private void Update()
        {
            UpdatePoiseRegen();
        }

        private void OnDestroy()
        {
            CombatSystem.Instance?.UnregisterCombatant(this);
        }

        #endregion

        #region AI Methods

        public void SetTarget(ICombatant target)
        {
            _currentTarget = target;

            if (debugMode && target != null)
                Debug.Log($"[EnemyController] {gameObject.name} targeting {target.CombatantName}");
        }

        public void ClearTarget()
        {
            _currentTarget = null;
        }

        public bool HasLineOfSightToTarget()
        {
            if (_currentTarget == null) return false;

            Vector3 origin = transform.position + Vector3.up;
            Vector3 direction = (_currentTarget.Position + Vector3.up) - origin;

            if (Physics.Raycast(origin, direction.normalized, out RaycastHit hit, direction.magnitude, lineOfSightMask))
            {
                // Check if we hit the target
                var targetComponent = hit.collider.GetComponentInParent<ICombatant>();
                return targetComponent == _currentTarget;
            }

            return true; // No obstruction
        }

        public bool CanAttack()
        {
            return Time.time >= _lastAttackTime + attackCooldown &&
                   _runtimeData.CurrentStamina >= baseStats.LightAttackStaminaCost;
        }

        public void NotifyAttackStarted(AttackData attack)
        {
            _lastAttackTime = Time.time;
            _hitboxController?.SetAttackData(attack);
            OnAttackStarted?.Invoke(this, attack);
        }

        public void NotifyHitLanded(IDamageable target, DamageResult result)
        {
            OnHitLanded?.Invoke(this, target, result);
        }

        public void ConsumeStamina(float amount)
        {
            _runtimeData.CurrentStamina = Mathf.Max(0, _runtimeData.CurrentStamina - amount);
            _runtimeData.LastStaminaUseTime = Time.time;
        }

        public void SetInvulnerable(bool isInvulnerable)
        {
            _isInvulnerable = isInvulnerable;
        }

        public void SetParrying(bool parrying)
        {
            _isParrying = parrying;
            if (parrying)
            {
                _parryWindowStart = Time.time;
            }
        }

        #endregion

        #region Navigation

        public void MoveTo(Vector3 position)
        {
            if (_navAgent != null && _navAgent.isActiveAndEnabled)
            {
                _navAgent.SetDestination(position);
            }
        }

        public void StopMovement()
        {
            if (_navAgent != null && _navAgent.isActiveAndEnabled)
            {
                _navAgent.ResetPath();
            }
        }

        public void FaceTarget()
        {
            if (_currentTarget == null) return;

            Vector3 direction = (_currentTarget.Position - transform.position);
            direction.y = 0;

            if (direction.sqrMagnitude > 0.01f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(direction);
                transform.rotation = Quaternion.RotateTowards(
                    transform.rotation,
                    targetRotation,
                    baseStats.RotationSpeed * Time.deltaTime
                );
            }
        }

        #endregion

        private void UpdatePoiseRegen()
        {
            if (_runtimeData.CurrentPoise > 0 &&
                Time.time - _runtimeData.LastPoiseHitTime >= baseStats.PoiseRegenDelay)
            {
                _runtimeData.CurrentPoise = Mathf.Max(
                    0,
                    _runtimeData.CurrentPoise - Mathf.RoundToInt(baseStats.PoiseRegenRate * Time.deltaTime)
                );
            }
        }

        #region Debug

        private void OnDrawGizmosSelected()
        {
            if (!debugMode) return;

            // Detection range
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, detectionRange);

            // Attack range
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, attackRange);

            // Max chase range
            Gizmos.color = Color.gray;
            Gizmos.DrawWireSphere(transform.position, maxChaseRange);

            // Lock-on point
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(LockOnPoint, 0.2f);
        }

        #endregion
    }
}
