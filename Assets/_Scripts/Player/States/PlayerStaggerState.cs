using _Scripts.Combat.Data;
using _Scripts.Combat.Interfaces;
using _Scripts.Utilities;
using Systems;
using UnityEngine;

namespace _Scripts.Player.States
{
    /// <summary>
    /// Player stagger state - triggered by poise break.
    /// Player is vulnerable and cannot act during stagger.
    /// </summary>
    public class PlayerStaggerState : BasePlayerState
    {
        /// <summary>
        /// Severity of stagger (affects duration and animation).
        /// </summary>
        public enum StaggerSeverity
        {
            Light,      // Quick flinch
            Medium,     // Standard stagger
            Heavy,      // Long stagger (poise break)
            Knockdown   // Fall to ground
        }

        // Configuration with defaults
        private const float LightStaggerDuration = 0.3f;
        private const float MediumStaggerDuration = 0.6f;
        private const float HeavyStaggerDuration = 1.0f;
        private const float KnockdownDuration = 2.0f;
        private const float RecoveryWindowStart = 0.7f;
        private const bool CanDodgeRecovery = true;
        private const float KnockbackMultiplier = 1f;

        // State tracking
        private StaggerSeverity CurrentSeverity { get; set; }
        private Vector3 _staggerDirection;
        private float _knockbackDistance;
        private bool InRecoveryWindow { get; set; }
        private float _knockbackProgress;

        // Knockback curve
        private static readonly AnimationCurve KnockbackCurve = new (
            new Keyframe(0f, 0f, 3f, 3f),
            new Keyframe(0.3f, 0.9f, 0f, 0f),
            new Keyframe(1f, 1f, 0f, 0f)
        );

        public override PlayerState StateType => PlayerState.Stagger;

        /// <summary>
        /// Set stagger parameters before entering state.
        /// </summary>
        public void SetStaggerParameters(StaggerSeverity severity, Vector3 direction, float knockbackDistance = 0f)
        {
            CurrentSeverity = severity;
            _staggerDirection = direction.normalized;
            _knockbackDistance = knockbackDistance * KnockbackMultiplier;
        }

        public override void Enter()
        {
            base.Enter();

            // Set duration based on severity
            stateDuration = GetDurationForSeverity(CurrentSeverity);

            // Reset state
            InRecoveryWindow = false;
            _knockbackProgress = 0f;

            // Reset poise on poise break (heavy stagger)
            if (CurrentSeverity >= StaggerSeverity.Heavy)
            {
                if (controller is IDamageableWithPoise poiseTarget)
                {
                    poiseTarget.ResetPoise();
                }
            }

            // Clear any active combat states
            controller?.SetParrying(false);
            controller?.SetInvulnerable(false);

            // Trigger stagger animation
            if (controller != null && controller.Animator != null)
            {
                controller.Animator.SetTriggerSafe("Stagger");
                controller.Animator.SetIntegerSafe("StaggerSeverity", (int)CurrentSeverity);
                controller.Animator.SetFloatSafe("StaggerDirectionX", _staggerDirection.x);
                controller.Animator.SetFloatSafe("StaggerDirectionZ", _staggerDirection.z);
            }
        }

        private float GetDurationForSeverity(StaggerSeverity severity)
        {
            switch (severity)
            {
                case StaggerSeverity.Light:
                    return LightStaggerDuration;
                case StaggerSeverity.Medium:
                    return MediumStaggerDuration;
                case StaggerSeverity.Heavy:
                    return HeavyStaggerDuration;
                case StaggerSeverity.Knockdown:
                    return KnockdownDuration;
                default:
                    return MediumStaggerDuration;
            }
        }

        public override void Execute()
        {
            // IMPORTANT: Apply movement BEFORE base.Execute() to avoid NormalizedTime reset bug
            float normalizedTime = NormalizedTime;

            // Apply knockback movement first
            // TODO KCC Phase 5.5: Use ExternalForcesManager for knockback
            if (controller is not null && controller.Motor is not null && _knockbackDistance > 0)
            {
                float previousProgress = _knockbackProgress;
                _knockbackProgress = KnockbackCurve.Evaluate(Mathf.Min(normalizedTime * 3f, 1f));
                float frameDelta = _knockbackProgress - previousProgress;

                Vector3 movement = frameDelta * _knockbackDistance * _staggerDirection;
                movement.y = -20f * Time.deltaTime;

                // KCC: Use motor's transient position for knockback
                controller.Motor.SetPositionAndRotation(
                    controller.Motor.TransientPosition + movement,
                    controller.Motor.TransientRotation
                );
            }

            // Update recovery window
            if (!InRecoveryWindow && normalizedTime >= RecoveryWindowStart)
            {
                InRecoveryWindow = true;
            }

            // Call base.Execute() AFTER movement - this may trigger state change
            base.Execute();
        }

        public override void HandleInput(InputSnapshot input)
        {
            // Limited input during stagger

            // Dodge recovery (mashing mechanic)
            if (CanDodgeRecovery && InRecoveryWindow &&
                InputHandler.Instance is not null &&
                InputHandler.Instance.HasBufferedAction(InputAction.Dodge) &&
                CanDodge())
            {
                if (InputHandler.Instance.TryConsumeAction(InputAction.Dodge, out _))
                {
                    ChangeState(PlayerState.Dodge);
                    return;
                }
            }
        }

        protected override void OnStateComplete()
        {
            // Return to idle after stagger
            ChangeState(PlayerState.Idle);
        }

        public override void Exit()
        {
            base.Exit();
        }

        public override bool CanTransitionTo(PlayerState targetState, InputAction action = InputAction.None)
        {
            switch (targetState)
            {
                case PlayerState.Dodge:
                    // Can dodge in recovery (if enabled)
                    return CanDodgeRecovery && InRecoveryWindow;

                case PlayerState.Death:
                    // Death always interrupts
                    return true;

                case PlayerState.Stagger:
                    // Can be re-staggered
                    return true;

                case PlayerState.Idle:
                    // Only after stagger completes
                    return NormalizedTime >= 1f;

                default:
                    // No other transitions during stagger
                    return false;
            }
        }
    }
}
