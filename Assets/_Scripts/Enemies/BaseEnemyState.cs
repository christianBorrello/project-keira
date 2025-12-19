using _Scripts.Combat.Interfaces;

namespace _Scripts.Enemies
{
    /// <summary>
    /// Abstract base class for all enemy states.
    /// Plain C# class - no MonoBehaviour required.
    /// </summary>
    public abstract class BaseEnemyState
    {
        // Configuration - set by derived classes
        protected float _stateDuration = 0f;
        protected bool _canBeInterrupted = true;

        // References set during initialization
        protected EnemyStateMachine _stateMachine;
        protected EnemyController _controller;

        /// <summary>
        /// The state type this class represents.
        /// </summary>
        public abstract EnemyState StateType { get; }

        /// <summary>
        /// Duration of this state in seconds.
        /// </summary>
        public float StateDuration => _stateDuration;

        /// <summary>
        /// Whether this state can be interrupted.
        /// </summary>
        public bool CanBeInterrupted => _canBeInterrupted;

        /// <summary>
        /// Time spent in this state.
        /// </summary>
        protected float StateTime => _stateMachine?.StateTime ?? 0f;

        /// <summary>
        /// Normalized time (0-1) in this state.
        /// </summary>
        protected float NormalizedTime => _stateMachine?.StateNormalizedTime ?? 0f;

        /// <summary>
        /// Reference to the current target.
        /// </summary>
        protected ICombatant Target => _controller?.CurrentTarget;

        /// <summary>
        /// Distance to current target.
        /// </summary>
        protected float DistanceToTarget => _controller?.DistanceToTarget ?? float.MaxValue;

        /// <summary>
        /// Initialize the state.
        /// </summary>
        public virtual void Initialize(EnemyStateMachine stateMachine)
        {
            _stateMachine = stateMachine;
            _controller = stateMachine.Context;
        }

        /// <summary>
        /// Called when entering this state.
        /// </summary>
        public virtual void Enter()
        {
        }

        /// <summary>
        /// Called every frame while in this state.
        /// </summary>
        public virtual void Execute()
        {
            // Check for automatic state completion
            if (_stateDuration > 0 && StateTime >= _stateDuration)
            {
                OnStateComplete();
            }
        }

        /// <summary>
        /// Called in FixedUpdate for physics.
        /// </summary>
        public virtual void PhysicsExecute()
        {
        }

        /// <summary>
        /// Called when exiting this state.
        /// </summary>
        public virtual void Exit()
        {
        }

        /// <summary>
        /// Called when state duration completes.
        /// </summary>
        protected virtual void OnStateComplete()
        {
            _stateMachine.ChangeState(EnemyState.Idle);
        }

        /// <summary>
        /// Called when forcibly interrupted.
        /// </summary>
        public virtual void OnInterrupted()
        {
        }

        /// <summary>
        /// Check if transition to another state is allowed.
        /// </summary>
        public virtual bool CanTransitionTo(EnemyState targetState)
        {
            return true;
        }

        /// <summary>
        /// Helper to change state.
        /// </summary>
        protected void ChangeState(EnemyState newState)
        {
            _stateMachine.ChangeState(newState);
        }

        /// <summary>
        /// Helper to try changing state with permission check.
        /// </summary>
        protected bool TryChangeState(EnemyState newState)
        {
            return _stateMachine.ChangeState(newState);
        }

        #region Common Checks

        /// <summary>
        /// Check if target is valid and alive.
        /// </summary>
        protected bool HasValidTarget()
        {
            return Target != null && Target.IsAlive;
        }

        /// <summary>
        /// Check if target is within attack range.
        /// </summary>
        protected bool IsInAttackRange()
        {
            if (_controller == null) return false;
            return DistanceToTarget <= _controller.AttackRange;
        }

        /// <summary>
        /// Check if target is within detection range.
        /// </summary>
        protected bool IsTargetDetected()
        {
            if (_controller == null) return false;
            return DistanceToTarget <= _controller.DetectionRange;
        }

        /// <summary>
        /// Check if target is in line of sight.
        /// </summary>
        protected bool HasLineOfSight()
        {
            return _controller?.HasLineOfSightToTarget() ?? false;
        }

        /// <summary>
        /// Check if can attack (cooldown, stamina, etc.).
        /// </summary>
        protected bool CanAttack()
        {
            return _controller?.CanAttack() ?? false;
        }

        #endregion
    }
}
