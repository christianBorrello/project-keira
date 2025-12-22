using UnityEngine;
using UnityEngine.Events;

namespace _Scripts.Core.Events
{
    /// <summary>
    /// Abstract base for typed event listeners.
    /// Inherit from this to create concrete listeners (e.g., FloatEventListener).
    /// </summary>
    /// <typeparam name="TEvent">The concrete GameEvent type</typeparam>
    /// <typeparam name="TData">The data type carried by the event</typeparam>
    public abstract class GameEventListener<TEvent, TData> : MonoBehaviour, IGameEventListener<TData>
        where TEvent : GameEvent<TData>
    {
        [Tooltip("The typed GameEvent asset to listen to")]
        [SerializeField] private TEvent _event;

        [Tooltip("Response to invoke when the event is raised, receives the event data")]
        [SerializeField] private UnityEvent<TData> _response;

        protected TEvent Event => _event;

        private void OnEnable()
        {
            _event?.Register(this);
        }

        private void OnDisable()
        {
            _event?.Unregister(this);
        }

        public void OnEventRaised(TData value)
        {
            _response?.Invoke(value);
        }
    }
}
