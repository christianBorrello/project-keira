using _Scripts.Combat.Data;
using _Scripts.Utilities;
using UnityEngine;

namespace _Scripts.Enemies.States
{
    /// <summary>
    /// Enemy attack state - performing attack animation and dealing damage.
    /// </summary>
    public class EnemyAttackState : BaseEnemyState
    {
        // Configuration with defaults
        private readonly string[] _attackTriggers = { "Attack1", "Attack2" };
        private readonly float _attackDuration = 1.2f;
        private readonly bool _canRotateDuringAttack = false;
        private readonly float _attackRotationSpeed = 90f;
        private readonly float _attackLungeDistance = 0.5f;
        private readonly string _hitboxGroupName = "weapon_enemy";

        private int _currentAttackIndex;
        private AttackData _currentAttack;
        private float _lungeProgress;
        private bool _hitboxActive;

        public override EnemyState StateType => EnemyState.Attack;

        public override void Enter()
        {
            base.Enter();

            _stateDuration = _attackDuration;
            _lungeProgress = 0f;
            _hitboxActive = false;

            // Stop movement
            _controller?.StopMovement();

            // Face target
            _controller?.FaceTarget();

            // Select random attack
            _currentAttackIndex = Random.Range(0, _attackTriggers.Length);

            // Create attack data
            if (_controller != null)
            {
                var stats = _controller.GetBaseStats();
                _currentAttack = AttackData.CreateLightAttack(_currentAttackIndex);
                _controller.ConsumeStamina(stats.LightAttackStaminaCost);
            }

            // Trigger animation
            if (_controller != null && _controller.Animator != null && _attackTriggers.Length > 0)
            {
                _controller.Animator.SetTriggerSafe(_attackTriggers[_currentAttackIndex]);
            }

            // Notify attack started
            _controller?.NotifyAttackStarted(_currentAttack);
        }

        public override void Execute()
        {
            // IMPORTANT: Do all work BEFORE base.Execute() to avoid NormalizedTime reset bug
            float normalizedTime = NormalizedTime;

            // Handle hitbox activation based on attack timing
            bool shouldHitboxBeActive = _currentAttack.IsHitboxActive(normalizedTime);

            if (shouldHitboxBeActive && !_hitboxActive)
            {
                _hitboxActive = true;
                _controller?.HitboxController?.ActivateGroup(_hitboxGroupName, _currentAttack);
            }
            else if (!shouldHitboxBeActive && _hitboxActive)
            {
                _hitboxActive = false;
                _controller?.HitboxController?.DeactivateGroup(_hitboxGroupName);
            }

            // Rotate towards target if allowed
            if (_canRotateDuringAttack && normalizedTime < 0.5f)
            {
                RotateTowardsTarget();
            }

            // Apply lunge movement in first half of attack
            if (_attackLungeDistance > 0 && normalizedTime < 0.5f && _controller != null)
            {
                float targetProgress = normalizedTime * 2f; // 0 to 1 in first half
                float frameDelta = targetProgress - _lungeProgress;
                _lungeProgress = targetProgress;

                Vector3 lunge = _controller.Forward * _attackLungeDistance * frameDelta;
                _controller.NavAgent?.Move(lunge);
            }

            // Call base.Execute() AFTER all processing
            base.Execute();
        }

        private void RotateTowardsTarget()
        {
            if (_controller == null || Target == null) return;

            Vector3 direction = (Target.Position - _controller.Position);
            direction.y = 0;

            if (direction.sqrMagnitude > 0.01f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(direction);
                _controller.Transform.rotation = Quaternion.RotateTowards(
                    _controller.Transform.rotation,
                    targetRotation,
                    _attackRotationSpeed * Time.deltaTime
                );
            }
        }

        protected override void OnStateComplete()
        {
            // Return to appropriate state
            if (HasValidTarget() && IsTargetDetected())
            {
                if (IsInAttackRange() && CanAttack())
                {
                    // Chain attack
                    ChangeState(EnemyState.Attack);
                }
                else
                {
                    // Resume chase
                    ChangeState(EnemyState.Chase);
                }
            }
            else
            {
                ChangeState(EnemyState.Idle);
            }
        }

        public override void Exit()
        {
            base.Exit();

            // Deactivate hitbox if still active
            if (_hitboxActive)
            {
                _hitboxActive = false;
                _controller?.HitboxController?.DeactivateGroup(_hitboxGroupName);
            }
        }

        public override bool CanTransitionTo(EnemyState targetState)
        {
            switch (targetState)
            {
                case EnemyState.Stagger:
                case EnemyState.Death:
                    return true;

                case EnemyState.Attack:
                case EnemyState.Chase:
                case EnemyState.Idle:
                    // Only after attack completes
                    return NormalizedTime >= 0.9f;

                default:
                    return false;
            }
        }

        /// <summary>
        /// Get current attack data for hitbox.
        /// </summary>
        public AttackData GetCurrentAttack() => _currentAttack;
    }
}
