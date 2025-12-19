using _Scripts.Combat.Interfaces;
using _Scripts.Utilities;
using Systems;
using UnityEngine;

namespace _Scripts.Enemies.States
{
    /// <summary>
    /// Enemy idle state - standing still, scanning for targets.
    /// </summary>
    public class EnemyIdleState : BaseEnemyState
    {
        // Configuration with defaults
        private readonly float _detectionCheckInterval = 0.5f;
        private readonly float _idleToPatrolTime = 5f;

        private float _lastDetectionCheck;
        private float _idleStartTime;

        public override EnemyState StateType => EnemyState.Idle;

        public override void Enter()
        {
            base.Enter();

            _lastDetectionCheck = 0f;
            _idleStartTime = Time.time;

            _controller?.StopMovement();

            // Set animation
            if (_controller != null && _controller.Animator != null)
            {
                _controller.Animator.SetBoolSafe("IsMoving", false);
                _controller.Animator.SetBoolSafe("InCombat", false);
            }
        }

        public override void Execute()
        {
            base.Execute();

            // Periodic detection check
            if (Time.time - _lastDetectionCheck >= _detectionCheckInterval)
            {
                _lastDetectionCheck = Time.time;
                CheckForTargets();
            }

            // Already has target from previous combat
            if (HasValidTarget() && IsTargetDetected())
            {
                ChangeState(EnemyState.Chase);
                return;
            }

            // Transition to patrol after idle time
            if (_idleToPatrolTime > 0 && Time.time - _idleStartTime >= _idleToPatrolTime)
            {
                if (_stateMachine.HasState(EnemyState.Patrol))
                {
                    ChangeState(EnemyState.Patrol);
                }
            }
        }

        private void CheckForTargets()
        {
            if (_controller == null) return;

            // Find player in detection range
            var combatants = CombatSystem.Instance?.GetCombatantsByFaction(Faction.Player);
            if (combatants == null) return;

            foreach (var combatant in combatants)
            {
                if (!combatant.IsAlive) continue;

                float distance = Vector3.Distance(_controller.Position, combatant.Position);
                if (distance <= _controller.DetectionRange)
                {
                    // Check line of sight
                    _controller.SetTarget(combatant);
                    if (_controller.HasLineOfSightToTarget())
                    {
                        ChangeState(EnemyState.Alert);
                        return;
                    }
                    _controller.ClearTarget();
                }
            }
        }

        public override bool CanTransitionTo(EnemyState targetState)
        {
            return true; // Idle can transition to any state
        }
    }
}
