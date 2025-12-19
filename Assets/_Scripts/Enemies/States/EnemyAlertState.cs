using _Scripts.Utilities;
using UnityEngine;

namespace _Scripts.Enemies.States
{
    /// <summary>
    /// Enemy alert state - brief awareness phase before engaging.
    /// "Noticed something" moment.
    /// </summary>
    public class EnemyAlertState : BaseEnemyState
    {
        // Configuration with defaults
        private readonly float _alertDuration = 0.8f;
        private readonly bool _turnToFaceTarget = true;

        public override EnemyState StateType => EnemyState.Alert;

        public override void Enter()
        {
            base.Enter();

            _stateDuration = _alertDuration;

            // Stop movement
            _controller?.StopMovement();

            // Set animation
            if (_controller != null && _controller.Animator != null)
            {
                _controller.Animator.SetTriggerSafe("Alert");
                _controller.Animator.SetBoolSafe("InCombat", true);
            }
        }

        public override void Execute()
        {
            base.Execute();

            // Turn to face target
            if (_turnToFaceTarget)
            {
                _controller?.FaceTarget();
            }
        }

        protected override void OnStateComplete()
        {
            // Transition to chase if target still valid
            if (HasValidTarget() && IsTargetDetected())
            {
                ChangeState(EnemyState.Chase);
            }
            else
            {
                ChangeState(EnemyState.Idle);
            }
        }

        public override bool CanTransitionTo(EnemyState targetState)
        {
            switch (targetState)
            {
                case EnemyState.Chase:
                case EnemyState.Idle:
                case EnemyState.Stagger:
                case EnemyState.Death:
                    return true;
                default:
                    return false;
            }
        }
    }
}
