using _Scripts.Combat.Data;
using _Scripts.Utilities;
using Systems;
using UnityEngine;

namespace _Scripts.Player.States
{
    /// <summary>
    /// Player parry state - defensive action with timing windows.
    /// Successful parries can stagger enemies and negate damage.
    /// Inspired by Lies of P deflect system.
    /// </summary>
    public class PlayerParryState : BasePlayerState
    {
        // Configuration with defaults
        private readonly float _parryDuration = 0.5f;
        private readonly float _perfectWindowStart = 0.05f;
        private readonly float _perfectWindowEnd = 0.15f;
        private readonly float _partialWindowEnd = 0.35f;
        private readonly float _failedParryStaminaCost = 20f;
        private readonly bool _allowDeflectChain = true;
        private readonly float _deflectChainWindowStart = 0.4f;
        private readonly int _maxDeflectChain = 5;

        // State tracking
        private bool _inPerfectWindow = false;
        private bool _inPartialWindow = false;
        private bool _inRecoveryWindow = false;
        private bool _parrySucceeded = false;
        private int _currentDeflectChain = 0;

        public override PlayerState StateType => PlayerState.Parry;

        public override void Enter()
        {
            base.Enter();

            stateDuration = _parryDuration;

            // Reset state
            _inPerfectWindow = false;
            _inPartialWindow = false;
            _inRecoveryWindow = false;
            _parrySucceeded = false;

            // Set parrying flag on controller
            controller?.SetParrying(true);

            // Trigger parry animation
            if (controller != null && controller.Animator != null)
            {
                controller.Animator.SetTriggerSafe("Parry");
                controller.Animator.SetIntegerSafe("DeflectChain", _currentDeflectChain);
            }
        }

        public override void Execute()
        {
            base.Execute();

            float normalizedTime = NormalizedTime;

            // Update window states
            UpdateWindows(normalizedTime);

            // Update parry timing on controller
            if (controller != null)
            {
                bool isParrying = _inPerfectWindow || _inPartialWindow;
                controller.SetParrying(isParrying);
            }
        }

        private void UpdateWindows(float normalizedTime)
        {
            // Perfect parry window
            if (!_inPerfectWindow && normalizedTime >= _perfectWindowStart && normalizedTime < _perfectWindowEnd)
            {
                _inPerfectWindow = true;
                _inPartialWindow = true;
            }
            else if (_inPerfectWindow && normalizedTime >= _perfectWindowEnd)
            {
                _inPerfectWindow = false;
            }

            // Partial parry window
            if (_inPartialWindow && normalizedTime >= _partialWindowEnd)
            {
                _inPartialWindow = false;
            }

            // Recovery window (after parry windows close)
            if (!_inRecoveryWindow && normalizedTime >= _partialWindowEnd)
            {
                _inRecoveryWindow = true;
                controller?.SetParrying(false);
            }
        }

        public override void HandleInput(InputSnapshot input)
        {
            // Check for deflect chain (Lies of P style continuous parrying)
            if (_allowDeflectChain && _inRecoveryWindow && normalizedTime >= _deflectChainWindowStart)
            {
                if (InputHandler.Instance != null &&
                    InputHandler.Instance.HasBufferedAction(InputAction.Parry) &&
                    _currentDeflectChain < _maxDeflectChain)
                {
                    if (InputHandler.Instance.TryConsumeAction(InputAction.Parry, out _))
                    {
                        ChainDeflect();
                        return;
                    }
                }
            }

            // Dodge cancel (lower priority than chain)
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

            // Attack cancel after successful parry
            if (_parrySucceeded && _inRecoveryWindow)
            {
                if (InputHandler.Instance != null)
                {
                    if (InputHandler.Instance.HasBufferedAction(InputAction.LightAttack) && CanAttack())
                    {
                        if (InputHandler.Instance.TryConsumeAction(InputAction.LightAttack, out _))
                        {
                            ChangeState(PlayerState.LightAttack);
                            return;
                        }
                    }

                    if (InputHandler.Instance.HasBufferedAction(InputAction.HeavyAttack) && CanAttack())
                    {
                        if (InputHandler.Instance.TryConsumeAction(InputAction.HeavyAttack, out _))
                        {
                            ChangeState(PlayerState.HeavyAttack);
                            return;
                        }
                    }
                }
            }
        }

        private void ChainDeflect()
        {
            _currentDeflectChain++;

            // Reset for new deflect
            _inPerfectWindow = false;
            _inPartialWindow = false;
            _inRecoveryWindow = false;
            _parrySucceeded = false;

            // Set parrying flag
            controller?.SetParrying(true);

            // Trigger chain animation
            if (controller != null && controller.Animator != null)
            {
                controller.Animator.SetTriggerSafe("Parry");
                controller.Animator.SetIntegerSafe("DeflectChain", _currentDeflectChain);
            }

            // Reset state timer through state machine (would need method)
            // For now, we rely on animation duration
        }

        /// <summary>
        /// Called by PlayerController when a parry succeeds.
        /// </summary>
        public void OnParrySuccess(bool isPerfect)
        {
            _parrySucceeded = true;

            // Reset deflect chain on successful parry (rewarded with window to attack)
            if (isPerfect)
            {
                _currentDeflectChain = 0;
            }

            // Trigger success animation
            if (controller != null && controller.Animator != null)
            {
                controller.Animator.SetTriggerSafe(isPerfect ? "PerfectParry" : "PartialParry");
            }
        }

        protected override void OnStateComplete()
        {
            // Reset deflect chain
            _currentDeflectChain = 0;

            // Return to appropriate state
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

            // Clear parrying flag
            controller?.SetParrying(false);

            // Don't reset chain here - it persists between parry states
            // Only reset on: attack, dodge, stagger, or state complete
        }

        public override void OnInterrupted()
        {
            base.OnInterrupted();

            // Failed parry - apply stamina penalty
            if (!_parrySucceeded)
            {
                controller?.ConsumeStamina(_failedParryStaminaCost);
            }

            // Reset chain on interrupt
            _currentDeflectChain = 0;
        }

        public override bool CanTransitionTo(PlayerState targetState, InputAction action = InputAction.None)
        {
            switch (targetState)
            {
                case PlayerState.Parry:
                    // Chain deflect handled internally
                    return false;

                case PlayerState.Dodge:
                    // Can dodge in recovery
                    return _inRecoveryWindow;

                case PlayerState.LightAttack:
                case PlayerState.HeavyAttack:
                    // Can attack after successful parry
                    return _parrySucceeded && _inRecoveryWindow;

                case PlayerState.Stagger:
                case PlayerState.Death:
                    // Always allow interrupt states
                    return true;

                case PlayerState.Idle:
                case PlayerState.Walk:
                case PlayerState.Sprint:
                    // Only after parry completes
                    return _inRecoveryWindow || NormalizedTime >= 1f;

                default:
                    return false;
            }
        }

        /// <summary>
        /// Get normalized time safely.
        /// </summary>
        private float normalizedTime => stateMachine?.StateNormalizedTime ?? 0f;

        /// <summary>
        /// Whether currently in a parry window.
        /// </summary>
        public bool IsInParryWindow => _inPerfectWindow || _inPartialWindow;

        /// <summary>
        /// Whether currently in perfect parry window.
        /// </summary>
        public bool IsInPerfectWindow => _inPerfectWindow;

        /// <summary>
        /// Current deflect chain count.
        /// </summary>
        public int DeflectChainCount => _currentDeflectChain;
    }
}
