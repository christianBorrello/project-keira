using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ScriptableObject-based event for decoupled communication.
/// Create assets via Assets > Create > Events > Game Event.
/// </summary>
[CreateAssetMenu(fileName = "NewGameEvent", menuName = "Events/Game Event", order = 0)]
public class GameEvent : ScriptableObject
{
    private readonly HashSet<IGameEventListener> _listeners = new();

#if UNITY_EDITOR
    [TextArea(2, 4)]
    [SerializeField] private string _description;

    [SerializeField] private bool _logRaises;
#endif

    public void Raise()
    {
#if UNITY_EDITOR
        if (_logRaises)
            Debug.Log($"[GameEvent] {name} raised with {_listeners.Count} listeners", this);
#endif

        foreach (var listener in _listeners)
            listener.OnEventRaised();
    }

    public void Register(IGameEventListener listener)
    {
        if (listener != null)
            _listeners.Add(listener);
    }

    public void Unregister(IGameEventListener listener)
    {
        if (listener != null)
            _listeners.Remove(listener);
    }

#if UNITY_EDITOR
    [ContextMenu("Raise Event (Debug)")]
    private void RaiseDebug() => Raise();

    [ContextMenu("Log Listener Count")]
    private void LogListenerCount() => Debug.Log($"{name} has {_listeners.Count} listeners");
#endif
}

/// <summary>
/// Interface for components that listen to GameEvent.
/// </summary>
public interface IGameEventListener
{
    void OnEventRaised();
}
