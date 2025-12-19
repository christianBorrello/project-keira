using _Scripts.Combat.Data;
using _Scripts.Combat.Interfaces;
using Systems;
using UnityEngine;

namespace _Scripts.Player
{
    /// <summary>
    /// Abstract base class for all player states.
    /// Plain C# class - no MonoBehaviour required.
    /// </summary>
    public abstract class BasePlayerState : IStateWithInput, IInterruptibleState
    {
        // Configuration - set by derived classes
        protected float stateDuration = 0f;
        protected bool canBeInterrupted = true;

        // References set during initialization
        protected PlayerStateMachine stateMachine;
        protected PlayerController controller;

        /// <summary>
        /// The state type this class represents.
        /// </summary>
        public abstract PlayerState StateType { get; }

        /// <summary>
        /// Duration of this state in seconds (0 = indefinite).
        /// </summary>
        public float StateDuration => stateDuration;

        /// <summary>
        /// Whether this state can be interrupted.
        /// </summary>
        public bool CanBeInterrupted => canBeInterrupted;

        /// <summary>
        /// Time spent in this state since Enter().
        /// </summary>
        protected float StateTime => stateMachine?.StateTime ?? 0f;

        /// <summary>
        /// Normalized time (0-1) in this state.
        /// </summary>
        protected float NormalizedTime => stateMachine?.StateNormalizedTime ?? 0f;

        /// <summary>
        /// Initialize the state with references.
        /// Called by PlayerStateMachine during setup.
        /// </summary>
        public virtual void Initialize(PlayerStateMachine playerStateMachine)
        {
            stateMachine = playerStateMachine;
            controller = stateMachine.Context;
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
            if (stateDuration > 0 && StateTime >= stateDuration)
            {
                OnStateComplete();
            }
        }

        /// <summary>
        /// Called in FixedUpdate for physics operations.
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
        /// Handle input for this state.
        /// </summary>
        public virtual void HandleInput(InputSnapshot input)
        {
        }

        /// <summary>
        /// Check if transition to another action is allowed.
        /// </summary>
        public virtual bool CanTransitionTo(InputAction action)
        {
            return true;
        }

        /// <summary>
        /// Check if transition to another state is allowed.
        /// </summary>
        public virtual bool CanTransitionTo(PlayerState targetState, InputAction action = InputAction.None)
        {
            return true;
        }

        /// <summary>
        /// Called when state is forcibly interrupted.
        /// </summary>
        public virtual void OnInterrupted()
        {
        }

        /// <summary>
        /// Called when state duration completes naturally.
        /// </summary>
        protected virtual void OnStateComplete()
        {
            stateMachine.ChangeState(PlayerState.Idle);
        }

        /// <summary>
        /// Helper to change state via the state machine.
        /// </summary>
        protected void ChangeState(PlayerState newState)
        {
            stateMachine.ChangeState(newState);
        }

        /// <summary>
        /// Helper to try changing state with permission check.
        /// </summary>
        protected bool TryChangeState(PlayerState newState, InputAction action = InputAction.None)
        {
            return stateMachine.TryChangeState(newState, action);
        }

        #region Common Transition Checks

        protected void CheckMovementTransition(InputSnapshot input)
        {
            if (input.HasMoveInput)
            {
                if (input.SprintHeld && CanSprint())
                {
                    TryChangeState(PlayerState.Sprint);
                }
                else
                {
                    TryChangeState(PlayerState.Walk);
                }
            }
            else
            {
                TryChangeState(PlayerState.Idle);
            }
        }

        protected void CheckCombatTransition(InputSnapshot input)
        {
            if (InputHandler.Instance is null)
                return;

            // Dodge (highest priority)
            if (InputHandler.Instance.HasBufferedAction(InputAction.Dodge) 
                && CanDodge()
                && InputHandler.Instance.TryConsumeAction(InputAction.Dodge, out _))
            {
                TryChangeState(PlayerState.Dodge, InputAction.Dodge);
                return;
            }

            // Block (MB2 - includes parry window)
            if (InputHandler.Instance.HasBufferedAction(InputAction.Block)
                && CanParry()
                && InputHandler.Instance.TryConsumeAction(InputAction.Block, out _))
            {
                TryChangeState(PlayerState.Block, InputAction.Block);
                return;
            }

            // Light Attack (MB1 tap)
            if (InputHandler.Instance.HasBufferedAction(InputAction.LightAttack) 
                && CanAttack()
                && InputHandler.Instance.TryConsumeAction(InputAction.LightAttack, out _))
            {
                TryChangeState(PlayerState.LightAttack, InputAction.LightAttack);
                return;
            }

            // Heavy Attack (MB1 hold)
            if (InputHandler.Instance.HasBufferedAction(InputAction.HeavyAttack) 
                && CanAttack()
                && InputHandler.Instance.TryConsumeAction(InputAction.HeavyAttack, out _))
            {
                TryChangeState(PlayerState.HeavyAttack, InputAction.HeavyAttack);
                return;
            }
        }

        #endregion

        #region Stamina Checks

        protected bool CanSprint()
        {
            if (controller is null) return false;
            var stats = controller.GetBaseStats();
            var runtime = controller.GetRuntimeData();
            return runtime.CurrentStamina > stats.SprintStaminaCost * Time.deltaTime;
        }

        protected bool CanDodge()
        {
            if (controller is null) return false;
            var stats = controller.GetBaseStats();
            var runtime = controller.GetRuntimeData();
            return runtime.CurrentStamina >= stats.DodgeStaminaCost;
        }

        protected bool CanParry()
        {
            return true;
        }

        protected bool CanAttack()
        {
            if (controller is null) return false;
            var stats = controller.GetBaseStats();
            var runtime = controller.GetRuntimeData();
            return runtime.CurrentStamina >= stats.LightAttackStaminaCost;
        }

        #endregion
    }
}
