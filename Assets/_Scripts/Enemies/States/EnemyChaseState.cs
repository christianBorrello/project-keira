using _Scripts.Utilities;
using UnityEngine;

namespace _Scripts.Enemies.States
{
    /// <summary>
    /// Enemy chase state - pursuing the target.
    /// </summary>
    public class EnemyChaseState : BaseEnemyState
    {
        // Configuration with defaults
        private readonly float _chaseSpeedMultiplier = 1.2f;
        private readonly float _stoppingDistance = 1.5f;
        private readonly float _loseSightTime = 3f;

        private float _baseSpeed;
        private float _lastTargetSeenTime;

        public override EnemyState StateType => EnemyState.Chase;

        public override void Enter()
        {
            base.Enter();

            _lastTargetSeenTime = Time.time;

            // Store and modify speed
            if (_controller != null && _controller.NavAgent != null)
            {
                _baseSpeed = _controller.NavAgent.speed;
                _controller.NavAgent.speed = _baseSpeed * _chaseSpeedMultiplier;
                _controller.NavAgent.stoppingDistance = _stoppingDistance;
            }

            // Set animation
            if (_controller != null && _controller.Animator != null)
            {
                _controller.Animator.SetBoolSafe("IsMoving", true);
                _controller.Animator.SetBoolSafe("InCombat", true);
            }
        }

        public override void Execute()
        {
            base.Execute();

            // Check target validity
            if (!HasValidTarget())
            {
                ChangeState(EnemyState.Idle);
                return;
            }

            // Check if target is too far
            if (DistanceToTarget > _controller.MaxChaseRange)
            {
                _controller.ClearTarget();
                ChangeState(EnemyState.Idle);
                return;
            }

            // Track line of sight
            if (HasLineOfSight())
            {
                _lastTargetSeenTime = Time.time;
            }
            else if (Time.time - _lastTargetSeenTime > _loseSightTime)
            {
                // Lost sight for too long
                ChangeState(EnemyState.Investigate);
                return;
            }

            // Check if in attack range
            if (IsInAttackRange() && CanAttack())
            {
                ChangeState(EnemyState.Attack);
                return;
            }

            // Update destination
            _controller?.MoveTo(Target.Position);

            // Update animation speed
            if (_controller != null && _controller.Animator != null && _controller.NavAgent != null)
            {
                float speed = _controller.NavAgent.velocity.magnitude / _baseSpeed;
                _controller.Animator.SetFloatSafe("MoveSpeed", speed);
            }
        }

        public override void Exit()
        {
            base.Exit();

            // Restore speed
            if (_controller != null && _controller.NavAgent != null)
            {
                _controller.NavAgent.speed = _baseSpeed;
            }
        }

        public override bool CanTransitionTo(EnemyState targetState)
        {
            switch (targetState)
            {
                case EnemyState.Attack:
                case EnemyState.Stagger:
                case EnemyState.Death:
                case EnemyState.Idle:
                case EnemyState.Investigate:
                case EnemyState.CircleStrafe:
                    return true;
                default:
                    return false;
            }
        }
    }
}
