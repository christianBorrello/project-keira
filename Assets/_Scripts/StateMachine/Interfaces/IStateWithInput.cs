using _Scripts.Combat.Data;

namespace _Scripts.StateMachine.Interfaces
{
    /// <summary>
    /// Extended state interface with input handling capabilities.
    /// States implementing this can respond to player/AI input.
    /// </summary>
    /// <typeparam name="TContext">The context type (e.g., PlayerController).</typeparam>
    public interface IStateWithInput<in TContext> : IState<TContext> where TContext : class
    {
        /// <summary>
        /// Process input specific to this state.
        /// Called before Execute() to handle input-driven transitions.
        /// </summary>
        /// <param name="input">Current input snapshot.</param>
        void HandleInput(InputSnapshot input);

        /// <summary>
        /// Check if this state can transition to another state based on input action.
        /// Used for priority-based state transitions (e.g., dodge can interrupt attack).
        /// </summary>
        /// <param name="action">The action requesting transition.</param>
        /// <returns>True if transition is allowed.</returns>
        bool CanTransitionTo(InputAction action);
    }
}
