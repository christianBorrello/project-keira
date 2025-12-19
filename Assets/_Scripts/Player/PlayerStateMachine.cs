using _Scripts.Combat.Data;
using _Scripts.StateMachine;
using Systems;
using UnityEngine;

namespace _Scripts.Player
{
    /// <summary>
    /// Finite State Machine controller for the player.
    /// Inherits from BaseStateMachine for static reflection caching.
    /// Auto-registers all BasePlayerState derived classes via reflection.
    /// </summary>
    public class PlayerStateMachine : BaseStateMachine<PlayerController, PlayerState, BasePlayerState>
    {
        /// <summary>
        /// Get the state enum value from a state instance.
        /// </summary>
        protected override PlayerState GetStateType(BasePlayerState state) => state.StateType;

        /// <summary>
        /// Initialize a state with this state machine reference.
        /// </summary>
        protected override void InitializeState(BasePlayerState state) => state.Initialize(this);

        /// <summary>
        /// Enter a state.
        /// </summary>
        protected override void EnterState(BasePlayerState state) => state?.Enter();

        /// <summary>
        /// Exit a state.
        /// </summary>
        protected override void ExitState(BasePlayerState state) => state?.Exit();

        /// <summary>
        /// Execute state Update logic.
        /// </summary>
        protected override void ExecuteState(BasePlayerState state) => state?.Execute();

        /// <summary>
        /// Execute state FixedUpdate logic.
        /// </summary>
        protected override void PhysicsExecuteState(BasePlayerState state) => state?.PhysicsExecute();

        /// <summary>
        /// Check if state can transition to target state.
        /// </summary>
        protected override bool CanTransitionTo(BasePlayerState state, PlayerState targetState)
            => state?.CanTransitionTo(targetState) ?? true;

        /// <summary>
        /// Check if state can be interrupted.
        /// </summary>
        protected override bool CanBeInterrupted(BasePlayerState state)
            => state?.CanBeInterrupted ?? true;

        /// <summary>
        /// Called when state is interrupted.
        /// </summary>
        protected override void OnInterrupted(BasePlayerState state) => state?.OnInterrupted();

        /// <summary>
        /// Get state duration for normalized time calculation.
        /// </summary>
        protected override float GetStateDuration(BasePlayerState state)
            => state?.StateDuration ?? 0f;

        /// <summary>
        /// Override Update to handle input before state execution.
        /// </summary>
        protected override void Update()
        {
            if (currentState == null) return;

            // Handle input first (player-specific)
            if (InputHandler.Instance != null)
            {
                currentState.HandleInput(InputHandler.Instance.CurrentInput);
            }

            // Execute state logic
            ExecuteState(currentState);
        }

        /// <summary>
        /// Try to change state with action trigger (player-specific).
        /// </summary>
        public bool TryChangeState(PlayerState newStateType, InputAction triggerAction)
        {
            if (currentState != null && !currentState.CanTransitionTo(newStateType, triggerAction))
            {
                if (debugLog)
                    Debug.Log($"[PlayerStateMachine] Transition denied: {currentStateType} -> {newStateType}");
                return false;
            }

            return ChangeState(newStateType);
        }
    }
}
