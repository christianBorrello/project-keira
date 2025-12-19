using _Scripts.StateMachine;
using UnityEngine;

namespace _Scripts.Enemies
{
    /// <summary>
    /// State machine controller for enemies.
    /// Inherits from BaseStateMachine for static reflection caching.
    /// Auto-registers all BaseEnemyState derived classes via reflection.
    /// </summary>
    public class EnemyStateMachine : BaseStateMachine<EnemyController, EnemyState, BaseEnemyState>
    {
        /// <summary>
        /// Get the state enum value from a state instance.
        /// </summary>
        protected override EnemyState GetStateType(BaseEnemyState state) => state.StateType;

        /// <summary>
        /// Initialize a state with this state machine reference.
        /// </summary>
        protected override void InitializeState(BaseEnemyState state) => state.Initialize(this);

        /// <summary>
        /// Enter a state.
        /// </summary>
        protected override void EnterState(BaseEnemyState state) => state?.Enter();

        /// <summary>
        /// Exit a state.
        /// </summary>
        protected override void ExitState(BaseEnemyState state) => state?.Exit();

        /// <summary>
        /// Execute state Update logic.
        /// </summary>
        protected override void ExecuteState(BaseEnemyState state) => state?.Execute();

        /// <summary>
        /// Execute state FixedUpdate logic.
        /// </summary>
        protected override void PhysicsExecuteState(BaseEnemyState state) => state?.PhysicsExecute();

        /// <summary>
        /// Check if state can transition to target state.
        /// </summary>
        protected override bool CanTransitionTo(BaseEnemyState state, EnemyState targetState)
            => state?.CanTransitionTo(targetState) ?? true;

        /// <summary>
        /// Check if state can be interrupted.
        /// </summary>
        protected override bool CanBeInterrupted(BaseEnemyState state)
            => state?.CanBeInterrupted ?? true;

        /// <summary>
        /// Called when state is interrupted.
        /// </summary>
        protected override void OnInterrupted(BaseEnemyState state) => state?.OnInterrupted();

        /// <summary>
        /// Get state duration for normalized time calculation.
        /// </summary>
        protected override float GetStateDuration(BaseEnemyState state)
            => state?.StateDuration ?? 0f;

        /// <summary>
        /// Override ChangeState to prevent same-state transitions.
        /// </summary>
        public override bool ChangeState(EnemyState newStateType)
        {
            // Prevent same-state transitions (enemy-specific behavior)
            if (newStateType == EnemyState.None) return false;
            if (newStateType == currentStateType) return false;

            return base.ChangeState(newStateType);
        }
    }
}
