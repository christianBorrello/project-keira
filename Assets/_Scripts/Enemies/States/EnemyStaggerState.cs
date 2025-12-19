using _Scripts.Combat.Interfaces;
using _Scripts.Utilities;
using UnityEngine;

namespace _Scripts.Enemies.States
{
    /// <summary>
    /// Enemy stagger state - hit recovery.
    /// </summary>
    public class EnemyStaggerState : BaseEnemyState
    {
        // Configuration with defaults
        private readonly float _lightStaggerDuration = 0.4f;
        private readonly float _heavyStaggerDuration = 1.5f;
        private readonly float _knockbackDistance = 0.5f;

        private bool _isHeavyStagger;
        private Vector3 _knockbackDirection;
        private float _knockbackProgress;

        public override EnemyState StateType => EnemyState.Stagger;

        /// <summary>
        /// Set stagger severity.
        /// </summary>
        public void SetHeavyStagger(bool isHeavy, Vector3 hitDirection)
        {
            _isHeavyStagger = isHeavy;
            _knockbackDirection = -hitDirection.normalized;
            _knockbackDirection.y = 0;
        }

        public override void Enter()
        {
            base.Enter();

            _stateDuration = _isHeavyStagger ? _heavyStaggerDuration : _lightStaggerDuration;
            _knockbackProgress = 0f;

            // Stop movement
            _controller?.StopMovement();

            // Reset poise on heavy stagger
            if (_isHeavyStagger && _controller is IDamageableWithPoise poiseTarget)
            {
                poiseTarget.ResetPoise();
            }

            // Set animation
            if (_controller != null && _controller.Animator != null)
            {
                _controller.Animator.SetTriggerSafe(_isHeavyStagger ? "HeavyStagger" : "Stagger");
            }
        }

        public override void Execute()
        {
            // IMPORTANT: Apply movement BEFORE base.Execute() to avoid NormalizedTime reset bug
            float normalizedTime = NormalizedTime;

            // Apply knockback first
            if (_knockbackDistance > 0 && normalizedTime < 0.3f && _controller != null)
            {
                float targetProgress = normalizedTime / 0.3f;
                float frameDelta = targetProgress - _knockbackProgress;
                _knockbackProgress = targetProgress;

                Vector3 knockback = _knockbackDirection * _knockbackDistance * frameDelta;
                _controller.NavAgent?.Move(knockback);
            }

            // Call base.Execute() AFTER movement
            base.Execute();
        }

        protected override void OnStateComplete()
        {
            // Return to combat
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
                case EnemyState.Death:
                    return true;

                case EnemyState.Stagger:
                    // Can be re-staggered
                    return true;

                case EnemyState.Chase:
                case EnemyState.Idle:
                    // Only after stagger completes
                    return NormalizedTime >= 1f;

                default:
                    return false;
            }
        }

        /// <summary>
        /// Whether this is a heavy stagger.
        /// </summary>
        public bool IsHeavyStagger => _isHeavyStagger;
    }
}
