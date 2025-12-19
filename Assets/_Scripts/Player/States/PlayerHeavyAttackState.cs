using _Scripts.Combat.Data;
using _Scripts.Utilities;
using Systems;
using UnityEngine;

namespace _Scripts.Player.States
{
    /// <summary>
    /// Player heavy attack state.
    /// Slower, more powerful attacks with longer commitment.
    /// Can be charged for additional damage.
    /// </summary>
    public class PlayerHeavyAttackState : BasePlayerState
    {
        // Configuration with defaults
        private readonly bool _canCharge = true;
        private readonly float _maxChargeTime = 1.5f;
        private readonly float _recoveryWindowStart = 0.75f;
        private readonly string _hitboxGroupName = "weapon_light";

        // Runtime state
        private AttackData _attackData;

        // State tracking
        private bool _isCharging;
        private float _chargeTime;
        private bool _attackReleased;
        private bool _inRecoveryWindow;
        private bool _hitboxActive;

        public override PlayerState StateType => PlayerState.HeavyAttack;

        public override void Enter()
        {
            base.Enter();

            // Reset state
            _isCharging = _canCharge;
            _chargeTime = 0f;
            _attackReleased = false;
            _inRecoveryWindow = false;

            // Consume base stamina
            if (controller != null)
            {
                var stats = controller.GetBaseStats();
                controller.ConsumeStamina(stats.HeavyAttackStaminaCost);
            }

            if (_canCharge)
            {
                // Start charge animation
                if (controller != null && controller.Animator != null)
                {
                    controller.Animator.SetBoolSafe("IsCharging", true);
                    controller.Animator.SetTriggerSafe("HeavyAttack");
                }
            }
            else
            {
                // Immediate attack (no charge)
                ReleaseAttack();
            }
        }

        public override void Execute()
        {
            base.Execute();

            if (_isCharging)
            {
                // Update charge time
                _chargeTime += Time.deltaTime;
                _chargeTime = Mathf.Min(_chargeTime, _maxChargeTime);

                // Update animator with charge progress
                if (controller != null && controller.Animator != null)
                {
                    float chargeNormalized = _chargeTime / _maxChargeTime;
                    controller.Animator.SetFloatSafe("ChargeProgress", chargeNormalized);
                }
            }
            else if (_attackReleased)
            {
                // Check recovery window
                float normalizedTime = NormalizedTime;
                if (!_inRecoveryWindow && normalizedTime >= _recoveryWindowStart)
                {
                    _inRecoveryWindow = true;
                }

                // Handle hitbox activation based on attack timing
                bool shouldHitboxBeActive = _attackData.IsHitboxActive(normalizedTime);

                if (shouldHitboxBeActive && !_hitboxActive)
                {
                    _hitboxActive = true;
                    controller?.HitboxController?.ActivateGroup(_hitboxGroupName, _attackData);
                }
                else if (!shouldHitboxBeActive && _hitboxActive)
                {
                    _hitboxActive = false;
                    controller?.HitboxController?.DeactivateGroup(_hitboxGroupName);
                }
            }
        }

        public override void HandleInput(InputSnapshot input)
        {
            // While charging, check for release
            if (_isCharging)
            {
                // Release on button release or if fully charged
                if (!input.AttackHeld || _chargeTime >= _maxChargeTime)
                {
                    ReleaseAttack();
                }

                // Can dodge cancel during charge
                if (InputHandler.Instance is not null &&
                    InputHandler.Instance.HasBufferedAction(InputAction.Dodge) &&
                    CanDodge())
                {
                    if (InputHandler.Instance.TryConsumeAction(InputAction.Dodge, out _))
                        ChangeState(PlayerState.Dodge);
                }

                return;
            }

            // After release, check for cancel options
            if (_attackReleased && _inRecoveryWindow)
            {
                // Dodge cancel in recovery
                if (InputHandler.Instance != null &&
                    InputHandler.Instance.HasBufferedAction(InputAction.Dodge) &&
                    CanDodge())
                {
                    if (InputHandler.Instance.TryConsumeAction(InputAction.Dodge, out _))
                    {
                        ChangeState(PlayerState.Dodge);
                        return;
                    }
                }

                // Light attack follow-up
                if (InputHandler.Instance != null &&
                    InputHandler.Instance.HasBufferedAction(InputAction.LightAttack) &&
                    CanAttack())
                {
                    if (InputHandler.Instance.TryConsumeAction(InputAction.LightAttack, out _))
                        ChangeState(PlayerState.LightAttack);
                }
            }
        }

        private void ReleaseAttack()
        {
            _isCharging = false;
            _attackReleased = true;

            // Calculate charge multipliers
            float chargePercent = _canCharge ? (_chargeTime / _maxChargeTime) : 0f;

            // Create attack data
            if (controller is not null)
            {
                _attackData = AttackData.CreateHeavyAttack();
                stateDuration = _attackData.AnimationDuration;
            }

            // Trigger release animation
            if (controller is not null && controller.Animator is not null)
            {
                controller.Animator.SetBoolSafe("IsCharging", false);
                controller.Animator.SetFloatSafe("ChargeProgress", chargePercent);
            }

            // Notify combat system
            controller?.NotifyAttackStarted(_attackData);
        }

        protected override void OnStateComplete()
        {
            // Return to appropriate state based on input
            if (InputHandler.Instance != null)
            {
                var input = InputHandler.Instance.CreateSnapshot();
                if (input.HasMoveInput)
                {
                    if (input.SprintHeld && CanSprint())
                    {
                        ChangeState(PlayerState.Sprint);
                    }
                    else if (input.WalkHeld)
                    {
                        ChangeState(PlayerState.Walk);
                    }
                    else
                    {
                        ChangeState(PlayerState.Run);
                    }
                    return;
                }
            }

            ChangeState(PlayerState.Idle);
        }

        public override void Exit()
        {
            base.Exit();

            // Deactivate hitbox if still active
            if (_hitboxActive && controller?.HitboxController != null)
            {
                controller.HitboxController.DeactivateGroup(_hitboxGroupName);
                _hitboxActive = false;
            }

            // Ensure charging state is reset
            if (controller != null && controller.Animator != null)
            {
                controller.Animator.SetBoolSafe("IsCharging", false);
            }

            _isCharging = false;
            _chargeTime = 0f;
        }

        public override bool CanTransitionTo(PlayerState targetState, InputAction action = InputAction.None)
        {
            switch (targetState)
            {
                case PlayerState.Dodge:
                    // Can dodge during charge or recovery
                    return _isCharging || _inRecoveryWindow;

                case PlayerState.LightAttack:
                    // Can chain to light in recovery
                    return _inRecoveryWindow;

                case PlayerState.HeavyAttack:
                    // No heavy to heavy combo
                    return false;

                case PlayerState.Stagger:
                case PlayerState.Death:
                    // Always allow interrupt states
                    return true;

                case PlayerState.Idle:
                case PlayerState.Walk:
                case PlayerState.Sprint:
                    // Only after attack completes
                    return _inRecoveryWindow || NormalizedTime >= 1f;

                default:
                    return false;
            }
        }

        /// <summary>
        /// Get current attack data for hitbox system.
        /// </summary>
        public AttackData GetCurrentAttackData() => _attackData;

        /// <summary>
        /// Get current charge percentage (0-1).
        /// </summary>
        public float ChargePercent => _canCharge ? (_chargeTime / _maxChargeTime) : 0f;

        /// <summary>
        /// Whether currently charging.
        /// </summary>
        public bool IsCharging => _isCharging;
    }
}
