namespace _Scripts.StateMachine.Interfaces
{
    /// <summary>
    /// Generic base interface for all state machine states.
    /// TContext provides type-safe access to the state machine owner.
    /// </summary>
    /// <typeparam name="TContext">The context type (e.g., PlayerController, EnemyController).</typeparam>
    /// <remarks>
    /// This generic version eliminates casting when accessing context.
    /// States inherit from a base class that implements this interface
    /// and provides the typed Context property.
    /// </remarks>
    public interface IState<in TContext> where TContext : class
    {
        /// <summary>
        /// Initialize the state with its context reference.
        /// Called once during state machine setup, before any Enter() calls.
        /// </summary>
        /// <param name="context">The state machine owner/context.</param>
        void Initialize(TContext context);

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
}
