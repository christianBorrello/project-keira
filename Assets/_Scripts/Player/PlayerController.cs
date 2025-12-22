using System;
using _Scripts.Combat.Data;
using _Scripts.Combat.Hitbox;
using _Scripts.Combat.Interfaces;
using _Scripts.Player.Components;
using Imports.Core;
using Systems;
using UnityEngine;
using Faction = _Scripts.Combat.Interfaces.Faction;

namespace _Scripts.Player
{
    /// <summary>
    /// Main player controller component.
    /// Implements combat interfaces and coordinates with state machine.
    /// </summary>
    [RequireComponent(typeof(KinematicCharacterMotor))]
    [RequireComponent(typeof(PlayerStateMachine))]
    [RequireComponent(typeof(AnimationController))]
    [RequireComponent(typeof(HealthPoiseController))]
    [RequireComponent(typeof(CombatController))]
    [RequireComponent(typeof(LockOnController))]
    [RequireComponent(typeof(MovementController))]
    public class PlayerController : MonoBehaviour, ICombatant, IDamageableWithPoise, ILockOnTarget
    {
        [Header("Configuration")]
        [SerializeField]
        private CombatStats baseStats = CombatStats.CreateDefaultPlayer();

        [Header("References")]
        [SerializeField]
        private Transform lockOnPoint;

        [Header("Debug")]
        [SerializeField]
        private bool debugMode;

        // Components
        private KinematicCharacterMotor _motor;
        private PlayerStateMachine _stateMachine;
        private AnimationController _animationController;
        private HealthPoiseController _healthPoiseController;
        private CombatController _combatController;
        private LockOnController _lockOnController;
        private MovementController _movementController;

        // Combatant ID
        private static int _nextCombatantId = 1;
        private int _combatantId;

        #region Properties

        public Animator Animator => _animationController?.Animator;
        public AnimationController AnimationController => _animationController;
        public HealthPoiseController HealthPoiseController => _healthPoiseController;
        public CombatController CombatController => _combatController;
        public LockOnController LockOnController => _lockOnController;
        public MovementController MovementController => _movementController;
        public KinematicCharacterMotor Motor => _motor;
        public HitboxController HitboxController => _combatController?.HitboxController;
        public bool IsLockedOn => _lockOnController?.IsLockedOn ?? false;
        public ILockOnTarget CurrentTarget => _lockOnController?.CurrentTarget;

        #endregion

        #region ICombatant Implementation (delegated to CombatController)

        public int CombatantId => _combatantId;
        public string CombatantName => gameObject.name;
        public Faction Faction => Faction.Player;
        public bool IsAlive => _healthPoiseController.IsAlive;
        public Transform Transform => transform;
        public Vector3 Position => transform.position;
        public Vector3 Forward => transform.forward;

        public CombatStats GetBaseStats() => _healthPoiseController.GetBaseStats();
        public CombatRuntimeData GetRuntimeData() => _healthPoiseController.GetRuntimeData();

        public bool IsParrying(out ParryTiming timing) => _combatController.IsParryingWithTiming(out timing);
        public bool IsInvulnerable => _combatController.IsInvulnerable;
        public bool IsBlocking => _combatController.IsBlocking;

        public void ApplyStagger(float duration) => _combatController.ApplyStagger(duration);

        // Events forwarded from CombatController (subscribed in Start)
        public event Action<ICombatant, AttackData> OnAttackStarted;
        public event Action<ICombatant, IDamageable, DamageResult> OnHitLanded;
        public event Action<ICombatant, float> OnStaggered;

        #endregion

        #region IDamageable Implementation (delegated to HealthPoiseController)

        public float CurrentHealth => _healthPoiseController.CurrentHealth;
        public float MaxHealth => _healthPoiseController.MaxHealth;
        public float HealthNormalized => _healthPoiseController.HealthNormalized;

        public DamageResult TakeDamage(DamageInfo damageInfo) => _healthPoiseController.TakeDamage(damageInfo);
        public float Heal(float amount) => _healthPoiseController.Heal(amount);
        public void Die() => _healthPoiseController.Die();

        // Events forwarded from HealthPoiseController (subscribed in Start)
        public event Action<float, float, float> OnHealthChanged;
        public event Action<DamageInfo, DamageResult> OnDamaged;
        public event Action OnDeath;
        public event Action<bool> OnParrySuccess; // true = perfect, false = partial
        public event Action<DamageInfo> OnBlocked;

        #endregion

        #region IDamageableWithPoise Implementation (delegated to HealthPoiseController)

        public int CurrentPoise => _healthPoiseController.CurrentPoise;
        public int MaxPoise => _healthPoiseController.MaxPoise;
        public float PoiseNormalized => _healthPoiseController.PoiseNormalized;

        public bool ApplyPoiseDamage(float amount) => _healthPoiseController.ApplyPoiseDamage(amount);
        public void ResetPoise() => _healthPoiseController.ResetPoise();

        // Event forwarded from HealthPoiseController (subscribed in Start)
        public event Action OnPoiseBreak;

        #endregion

        #region ILockOnTarget Implementation

        public Vector3 LockOnPoint => lockOnPoint != null ? lockOnPoint.position : transform.position + Vector3.up * 1.5f;
        public bool CanBeLocked => IsAlive;
        public int LockOnPriority => 0; // Player has lowest priority (enemies target player anyway)
        public Transform TargetTransform => transform;

        public void OnLockedOn() { } // Player can be locked onto by enemies
        public void OnLockReleased() { }

#pragma warning disable CS0067 // Event required by ILockOnTarget interface
        public event Action<bool> OnTargetValidityChanged;
#pragma warning restore CS0067

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            _combatantId = _nextCombatantId++;
            _motor = GetComponent<KinematicCharacterMotor>();
            _stateMachine = GetComponent<PlayerStateMachine>();
            _animationController = GetComponent<AnimationController>();
            _healthPoiseController = GetComponent<HealthPoiseController>();
            _combatController = GetComponent<CombatController>();
            _lockOnController = GetComponent<LockOnController>();
            _movementController = GetComponent<MovementController>();

            // Initialize HealthPoiseController with base stats
            _healthPoiseController.Initialize(baseStats);
        }

        private void Start()
        {
            _stateMachine.Initialize(this);
            _stateMachine.StartMachine(PlayerState.Idle);

            // Register with combat system so enemies can detect us
            CombatSystem.Instance?.RegisterCombatant(this);

            // Forward events from HealthPoiseController to maintain interface compatibility
            _healthPoiseController.OnHealthChanged += (current, max, delta) => OnHealthChanged?.Invoke(current, max, delta);
            _healthPoiseController.OnDamaged += (info, result) => OnDamaged?.Invoke(info, result);
            _healthPoiseController.OnDeath += () => OnDeath?.Invoke();
            _healthPoiseController.OnParrySuccess += isPerfect => OnParrySuccess?.Invoke(isPerfect);
            _healthPoiseController.OnBlocked += info => OnBlocked?.Invoke(info);
            _healthPoiseController.OnPoiseBreak += () => OnPoiseBreak?.Invoke();
            _healthPoiseController.OnStaggered += duration => OnStaggered?.Invoke(this, duration);

            // Forward events from CombatController to maintain ICombatant compatibility
            _combatController.OnAttackStarted += (combatant, attack) => OnAttackStarted?.Invoke(combatant, attack);
            _combatController.OnHitLanded += (combatant, target, result) => OnHitLanded?.Invoke(combatant, target, result);
            _combatController.OnStaggered += (combatant, duration) => OnStaggered?.Invoke(combatant, duration);
        }

        private void OnDestroy() => CombatSystem.Instance?.UnregisterCombatant(this);

        #endregion

        #region Movement (delegated to MovementController)

        public void ApplyMovement(Vector2 moveInput, LocomotionMode mode) => _movementController?.ApplyMovement(moveInput, mode);
        public void ResetAnimationSpeed() => _movementController?.ResetAnimationSpeed();

        #endregion

        #region Stamina (delegated to HealthPoiseController)

        public float CurrentStamina => _healthPoiseController.CurrentStamina;
        public float MaxStamina => _healthPoiseController.MaxStamina;
        public float StaminaNormalized => _healthPoiseController.StaminaNormalized;

        public void ConsumeStamina(float amount) => _healthPoiseController.ConsumeStamina(amount);
        public bool HasStamina(float amount) => _healthPoiseController.HasStamina(amount);

        #endregion

        #region Combat State Helpers (delegated to CombatController)

        public void SetParrying(bool isParrying) => _combatController.SetParrying(isParrying);
        public void SetBlocking(bool isBlocking) => _combatController.SetBlocking(isBlocking);
        public void SetInvulnerable(bool isInvulnerable) => _combatController.SetInvulnerable(isInvulnerable);

        public void NotifyAttackStarted(AttackData attack) => _combatController.NotifyAttackStarted(attack);
        public void NotifyHitLanded(IDamageable target, DamageResult result) => _combatController.NotifyHitLanded(target, result);

        #endregion

        #region Lock-On Helpers (delegated to LockOnController)

        public void UpdateLockedOnDistance() => _lockOnController?.UpdateLockedOnDistance();
        public void ToggleLockOn() => _lockOnController?.ToggleLockOn();

        #endregion

        #region Debug

        private void OnDrawGizmosSelected()
        {
            if (!debugMode) return;

            // Draw lock-on point
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(LockOnPoint, 0.2f);
        }

        #endregion
    }
}
