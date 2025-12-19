using System;
using System.Collections.Generic;
using UnityEngine;

namespace _Scripts.StateMachine
{
    /// <summary>
    /// Generic base class for all state machines.
    /// Uses static reflection caching to avoid LINQ allocations on subsequent initializations.
    /// </summary>
    /// <typeparam name="TContext">The controller/context type (e.g., PlayerController).</typeparam>
    /// <typeparam name="TStateEnum">The state enum type (e.g., PlayerState).</typeparam>
    /// <typeparam name="TState">The base state class type (e.g., BasePlayerState).</typeparam>
    public abstract class BaseStateMachine<TContext, TStateEnum, TState> : MonoBehaviour
        where TContext : class
        where TStateEnum : struct, Enum
        where TState : class
    {
        // Static cache: keyed by base state type, stores discovered state types
        // This ensures expensive reflection happens only once per state type hierarchy
        private static readonly Dictionary<Type, List<Type>> _stateTypeCache = new();
        private static readonly object _cacheLock = new();

        [Header("Debug")]
        [SerializeField]
        protected TStateEnum currentStateTypeDebug;

        [SerializeField]
        protected bool debugLog;

        // Instance state registry
        protected readonly Dictionary<TStateEnum, TState> states = new();
        protected TState currentState;
        protected TStateEnum currentStateType;
        protected float stateStartTime;

        // Context reference
        protected TContext context;

        // Events
        public event Action<TStateEnum, TStateEnum> OnStateChanged;

        /// <summary>
        /// Current state type (for Inspector and external queries).
        /// </summary>
        public TStateEnum CurrentStateType => currentStateType;

        /// <summary>
        /// Current state instance.
        /// </summary>
        public TState CurrentState => currentState;

        /// <summary>
        /// Context/controller reference.
        /// </summary>
        public TContext Context => context;

        /// <summary>
        /// Time spent in current state.
        /// </summary>
        public float StateTime => Time.time - stateStartTime;

        /// <summary>
        /// Normalized time in current state (0-1 based on state duration).
        /// Override GetStateDuration() to provide state-specific durations.
        /// </summary>
        public float StateNormalizedTime
        {
            get
            {
                float duration = GetStateDuration(currentState);
                if (duration <= 0) return 0f;
                return Mathf.Clamp01(StateTime / duration);
            }
        }

        /// <summary>
        /// Initialize the state machine with a context reference.
        /// </summary>
        public virtual void Initialize(TContext ctx)
        {
            context = ctx;
            RegisterStatesFromCache();

            if (debugLog)
                Debug.Log($"[{GetType().Name}] Initialized with {states.Count} states");
        }

        /// <summary>
        /// Start the state machine with an initial state.
        /// </summary>
        public virtual void StartMachine(TStateEnum initialState)
        {
            if (!states.ContainsKey(initialState))
            {
                Debug.LogError($"[{GetType().Name}] Initial state not found: {initialState}");
                return;
            }

            ChangeState(initialState);
        }

        /// <summary>
        /// Change to a new state. Returns true if successful.
        /// </summary>
        public virtual bool ChangeState(TStateEnum newStateType)
        {
            if (!states.TryGetValue(newStateType, out var newState))
            {
                Debug.LogError($"[{GetType().Name}] State not found: {newStateType}");
                return false;
            }

            TStateEnum previousStateType = currentStateType;

            // Exit current state
            ExitState(currentState);

            // Switch to new state
            currentState = newState;
            currentStateType = newStateType;
            currentStateTypeDebug = newStateType;
            stateStartTime = Time.time;

            // Enter new state
            EnterState(currentState);

            if (debugLog)
                Debug.Log($"[{GetType().Name}] {previousStateType} -> {newStateType}");

            OnStateChanged?.Invoke(previousStateType, newStateType);
            return true;
        }

        /// <summary>
        /// Try to change state if allowed by current state.
        /// </summary>
        public virtual bool TryChangeState(TStateEnum newStateType)
        {
            if (currentState != null && !CanTransitionTo(currentState, newStateType))
            {
                if (debugLog)
                    Debug.Log($"[{GetType().Name}] Transition denied: {currentStateType} -> {newStateType}");
                return false;
            }

            return ChangeState(newStateType);
        }

        /// <summary>
        /// Force interrupt current state.
        /// </summary>
        public virtual void ForceInterrupt(TStateEnum interruptState)
        {
            if (currentState != null && CanBeInterrupted(currentState))
            {
                OnInterrupted(currentState);
            }

            ChangeState(interruptState);
        }

        /// <summary>
        /// Check if a state type is registered.
        /// </summary>
        public bool HasState(TStateEnum stateType) => states.ContainsKey(stateType);

        /// <summary>
        /// Get a specific state instance.
        /// </summary>
        public T GetState<T>(TStateEnum stateType) where T : class
        {
            if (states.TryGetValue(stateType, out var state))
                return state as T;
            return null;
        }

        #region Abstract/Virtual Methods for Subclasses

        /// <summary>
        /// Get the state enum value for a state instance.
        /// Must be implemented by derived classes.
        /// </summary>
        protected abstract TStateEnum GetStateType(TState state);

        /// <summary>
        /// Initialize a state instance with the state machine reference.
        /// Must be implemented by derived classes.
        /// </summary>
        protected abstract void InitializeState(TState state);

        /// <summary>
        /// Enter a state. Override for custom Enter behavior.
        /// </summary>
        protected abstract void EnterState(TState state);

        /// <summary>
        /// Exit a state. Override for custom Exit behavior.
        /// </summary>
        protected abstract void ExitState(TState state);

        /// <summary>
        /// Execute a state's Update logic. Override for custom Execute behavior.
        /// </summary>
        protected abstract void ExecuteState(TState state);

        /// <summary>
        /// Execute a state's FixedUpdate logic.
        /// </summary>
        protected abstract void PhysicsExecuteState(TState state);

        /// <summary>
        /// Check if a state can transition to another state.
        /// </summary>
        protected abstract bool CanTransitionTo(TState state, TStateEnum targetState);

        /// <summary>
        /// Check if a state can be interrupted.
        /// </summary>
        protected abstract bool CanBeInterrupted(TState state);

        /// <summary>
        /// Called when a state is interrupted.
        /// </summary>
        protected abstract void OnInterrupted(TState state);

        /// <summary>
        /// Get the duration of a state (for normalized time calculation).
        /// </summary>
        protected abstract float GetStateDuration(TState state);

        #endregion

        #region Unity Lifecycle

        protected virtual void Update()
        {
            if (currentState == null) return;
            ExecuteState(currentState);
        }

        protected virtual void FixedUpdate()
        {
            if (currentState == null) return;
            PhysicsExecuteState(currentState);
        }

        #endregion

        #region Reflection Caching

        /// <summary>
        /// Register states from the static cache.
        /// Uses cached type information to avoid repeated reflection scans.
        /// </summary>
        private void RegisterStatesFromCache()
        {
            Type baseStateType = typeof(TState);

            // Thread-safe cache check and population
            List<Type> cachedTypes;
            lock (_cacheLock)
            {
                if (!_stateTypeCache.TryGetValue(baseStateType, out cachedTypes))
                {
                    cachedTypes = CacheStateTypes(baseStateType);
                    _stateTypeCache[baseStateType] = cachedTypes;

                    if (debugLog)
                        Debug.Log($"[{GetType().Name}] Cached {cachedTypes.Count} state types for {baseStateType.Name}");
                }
            }

            // Create instances from cached types
            foreach (var stateType in cachedTypes)
            {
                try
                {
                    var stateInstance = (TState)Activator.CreateInstance(stateType);
                    TStateEnum stateEnumValue = GetStateType(stateInstance);

                    if (states.ContainsKey(stateEnumValue))
                    {
                        Debug.LogWarning($"[{GetType().Name}] Duplicate state type: {stateEnumValue} from {stateType.Name}");
                        continue;
                    }

                    InitializeState(stateInstance);
                    states[stateEnumValue] = stateInstance;

                    if (debugLog)
                        Debug.Log($"[{GetType().Name}] Registered state: {stateEnumValue} ({stateType.Name})");
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[{GetType().Name}] Failed to create state {stateType.Name}: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Scan assemblies for state types. Uses manual iteration instead of LINQ.
        /// Called only once per base state type, result is cached.
        /// </summary>
        private static List<Type> CacheStateTypes(Type baseStateType)
        {
            var result = new List<Type>();
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();

            // Manual iteration instead of LINQ SelectMany/Where
            for (int i = 0; i < assemblies.Length; i++)
            {
                Type[] types;
                try
                {
                    types = assemblies[i].GetTypes();
                }
                catch
                {
                    // Some assemblies may throw on GetTypes() (e.g., dynamic assemblies)
                    continue;
                }

                for (int j = 0; j < types.Length; j++)
                {
                    Type type = types[j];

                    // Check: non-abstract class that inherits from base state type
                    if (type.IsClass &&
                        !type.IsAbstract &&
                        baseStateType.IsAssignableFrom(type) &&
                        type != baseStateType)
                    {
                        result.Add(type);
                    }
                }
            }

            return result;
        }

        #endregion
    }
}
