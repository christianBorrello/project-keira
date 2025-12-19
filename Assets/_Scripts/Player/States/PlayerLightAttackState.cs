using _Scripts.Combat.Data;
using _Scripts.Utilities;
using Systems;

namespace _Scripts.Player.States
{
    /// <summary>
    /// Player light attack state.
    /// Fast attacks that can combo into subsequent attacks.
    /// </summary>
    public class PlayerLightAttackState : BasePlayerState
    {
        // Configuration with defaults
        private readonly int _maxComboCount = 3;
        private readonly float _comboWindowStart = 0.5f;
        private readonly float _recoveryWindowStart = 0.8f;
        private readonly string _hitboxGroupName = "weapon_light";

        // Runtime state
        private AttackData _attackData;

        private int _currentComboIndex;
        private bool _comboQueued;
        private bool _inComboWindow;
        private bool _inRecoveryWindow;
        private bool _hitboxActive;

        public override PlayerState StateType => PlayerState.LightAttack;

        public override void Enter()
        {
            base.Enter();

            // Set attack data for current combo (MUST be before stamina check)
            _attackData = AttackData.CreateLightAttack(_currentComboIndex);
            stateDuration = _attackData.AnimationDuration;

            // Consume stamina
            if (controller is not null)
            {
                var stats = controller.GetBaseStats();
                controller.ConsumeStamina(stats.LightAttackStaminaCost);
            }

            // Trigger attack animation
            if (controller is not null && controller.Animator is not null)
            {
                controller.Animator.SetTriggerSafe("LightAttack");
                controller.Animator.SetIntegerSafe("ComboIndex", _currentComboIndex);
            }

            // Notify combat system
            controller?.NotifyAttackStarted(_attackData);

            // Reset state
            _comboQueued = false;
            _inComboWindow = false;
            _inRecoveryWindow = false;
            _hitboxActive = false;
        }

        public override void Execute()
        {
            base.Execute();

            // Update windows based on normalized time
            float normalizedTime = NormalizedTime;

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

            // Check combo window
            if (!_inComboWindow && normalizedTime >= _comboWindowStart && normalizedTime < _recoveryWindowStart)
            {
                _inComboWindow = true;
            }
            else if (_inComboWindow && normalizedTime >= _recoveryWindowStart)
            {
                _inComboWindow = false;
            }

            // Check recovery window
            if (!_inRecoveryWindow && normalizedTime >= _recoveryWindowStart)
            {
                _inRecoveryWindow = true;
            }

            // Process queued combo
            if (_comboQueued && _inComboWindow && _currentComboIndex < _maxComboCount - 1)
            {
                _comboQueued = false;
                ExecuteCombo();
            }
        }

        public override void HandleInput(InputSnapshot input)
        {
            // Check for dodge cancel (high priority)
            if (_inRecoveryWindow && InputHandler.Instance != null &&
                InputHandler.Instance.HasBufferedAction(InputAction.Dodge) &&
                CanDodge())
            {
                if (InputHandler.Instance.TryConsumeAction(InputAction.Dodge, out _))
                {
                    ChangeState(PlayerState.Dodge);
                    return;
                }
            }

            // Check for combo input
            if (InputHandler.Instance != null &&
                InputHandler.Instance.HasBufferedAction(InputAction.LightAttack))
            {
                // Queue the combo if we're before or in the combo window
                if (!_inRecoveryWindow && _currentComboIndex < _maxComboCount - 1 && CanAttack())
                {
                    _comboQueued = true;
                    InputHandler.Instance.TryConsumeAction(InputAction.LightAttack, out _);
                }
            }

            // Check for heavy attack follow-up
            if (_inRecoveryWindow && InputHandler.Instance is not null &&
                InputHandler.Instance.HasBufferedAction(InputAction.HeavyAttack) &&
                CanAttack())
            {
                if (InputHandler.Instance.TryConsumeAction(InputAction.HeavyAttack, out _))
                    ChangeState(PlayerState.HeavyAttack);
            }
        }

        private void ExecuteCombo()
        {
            _currentComboIndex++;

            // Consume stamina for combo
            if (controller is not null)
            {
                var stats = controller.GetBaseStats();
                controller.ConsumeStamina(stats.LightAttackStaminaCost);

                // Update attack data for combo hit
                _attackData = AttackData.CreateLightAttack(_currentComboIndex);
            }

            // Trigger next combo animation
            if (controller is not null && controller.Animator is not null)
            {
                controller.Animator.SetTriggerSafe("LightAttack");
                controller.Animator.SetIntegerSafe("ComboIndex", _currentComboIndex);
            }

            // Reset timer for new attack
            // Note: State machine handles this through animation events

            // Notify combat system
            controller?.NotifyAttackStarted(_attackData);

            // Reset windows
            _inComboWindow = false;
            _inRecoveryWindow = false;
        }

        protected override void OnStateComplete()
        {
            // Reset combo counter
            _currentComboIndex = 0;

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

            // Reset combo on exit
            _currentComboIndex = 0;
            _comboQueued = false;
        }

        public override bool CanTransitionTo(PlayerState targetState, InputAction action = InputAction.None)
        {
            switch (targetState)
            {
                case PlayerState.Dodge:
                    // Can only dodge in recovery window
                    return _inRecoveryWindow;

                case PlayerState.HeavyAttack:
                    // Can chain to heavy in recovery
                    return _inRecoveryWindow;

                case PlayerState.LightAttack:
                    // Combo handled internally
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
        /// Get current combo index.
        /// </summary>
        public int CurrentComboIndex => _currentComboIndex;
    }
}
