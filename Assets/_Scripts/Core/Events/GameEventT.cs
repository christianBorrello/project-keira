using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Generic ScriptableObject-based event that carries typed data.
/// Inherit from this to create concrete typed events (e.g., FloatEvent, DamageEvent).
/// </summary>
/// <typeparam name="T">The type of data this event carries</typeparam>
public abstract class GameEvent<T> : ScriptableObject
{
    private readonly HashSet<IGameEventListener<T>> _listeners = new();

#if UNITY_EDITOR
    [TextArea(2, 4)]
    [SerializeField] private string _description;

    [SerializeField] private bool _logRaises;
#endif

    public void Raise(T value)
    {
#if UNITY_EDITOR
        if (_logRaises)
            Debug.Log($"[GameEvent<{typeof(T).Name}>] {name} raised with value: {value}, {_listeners.Count} listeners", this);
#endif

        foreach (var listener in _listeners)
            listener.OnEventRaised(value);
    }

    public void Register(IGameEventListener<T> listener)
    {
        if (listener != null)
            _listeners.Add(listener);
    }

    public void Unregister(IGameEventListener<T> listener)
    {
        if (listener != null)
            _listeners.Remove(listener);
    }

#if UNITY_EDITOR
    [ContextMenu("Log Listener Count")]
    private void LogListenerCount() => Debug.Log($"{name} has {_listeners.Count} listeners");
#endif
}

/// <summary>
/// Interface for components that listen to typed GameEvent{T}.
/// </summary>
/// <typeparam name="T">The type of data received from the event</typeparam>
public interface IGameEventListener<T>
{
    void OnEventRaised(T value);
}
