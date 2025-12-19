using _Scripts.Combat.Data;

namespace _Scripts.Combat.Interfaces
{
    /// <summary>
    /// Base interface for all state machine states.
    /// Implements the State pattern for finite state machines.
    /// </summary>
    public interface IState
    {
        /// <summary>
        /// Called once when entering this state.
        /// Initialize state-specific variables, play animations, enable colliders.
        /// </summary>
        void Enter();

        /// <summary>
        /// Called every frame while in this state (Update).
        /// Handle ongoing logic: input checking, timers, conditions.
        /// </summary>
        void Execute();

        /// <summary>
        /// Called in FixedUpdate for physics-related operations.
        /// Movement, forces, physics queries.
        /// </summary>
        void PhysicsExecute();

        /// <summary>
        /// Called once when exiting this state.
        /// Cleanup: disable colliders, reset flags, stop effects.
        /// </summary>
        void Exit();
    }

    /// <summary>
    /// Extended state interface with input handling capabilities.
    /// </summary>
    public interface IStateWithInput : IState
    {
        /// <summary>
        /// Process input specific to this state.
        /// Called before Execute() to handle input-driven transitions.
        /// </summary>
        /// <param name="input">Current input snapshot.</param>
        void HandleInput(InputSnapshot input);

        /// <summary>
        /// Check if this state can transition to another state.
        /// Used for priority-based state transitions.
        /// </summary>
        /// <param name="action">The action requesting transition.</param>
        /// <returns>True if transition is allowed.</returns>
        bool CanTransitionTo(InputAction action);
    }

    /// <summary>
    /// State that can be interrupted by certain actions.
    /// </summary>
    public interface IInterruptibleState : IState
    {
        /// <summary>
        /// Check if this state can be interrupted by an incoming hit.
        /// </summary>
        bool CanBeInterrupted { get; }

        /// <summary>
        /// Called when the state is forcibly interrupted.
        /// </summary>
        void OnInterrupted();
    }
}
