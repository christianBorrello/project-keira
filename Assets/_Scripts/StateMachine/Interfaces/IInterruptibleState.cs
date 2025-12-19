namespace _Scripts.StateMachine.Interfaces
{
    /// <summary>
    /// State that can be interrupted by certain actions (e.g., taking damage).
    /// Implement this when a state should react to external interruption events.
    /// </summary>
    /// <typeparam name="TContext">The context type.</typeparam>
    public interface IInterruptibleState<in TContext> : IState<TContext> where TContext : class
    {
        /// <summary>
        /// Check if this state can currently be interrupted.
        /// May depend on animation progress, invincibility frames, etc.
        /// </summary>
        bool CanBeInterrupted { get; }

        /// <summary>
        /// Called when the state is forcibly interrupted (e.g., hit stagger).
        /// Perform cleanup before forced exit.
        /// </summary>
        void OnInterrupted();
    }
}
