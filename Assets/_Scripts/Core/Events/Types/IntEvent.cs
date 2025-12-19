using UnityEngine;

/// <summary>
/// GameEvent that carries an integer value.
/// Useful for counts, scores, or discrete values.
/// </summary>
[CreateAssetMenu(fileName = "NewIntEvent", menuName = "Events/Int Event", order = 11)]
public class IntEvent : GameEvent<int>
{
#if UNITY_EDITOR
    [Header("Debug")]
    [SerializeField] private int _debugValue = 0;

    [ContextMenu("Raise Debug Value")]
    private void RaiseDebug() => Raise(_debugValue);
#endif
}

/// <summary>
/// Listener component for IntEvent.
/// </summary>
[AddComponentMenu("Events/Int Event Listener")]
public class IntEventListener : GameEventListener<IntEvent, int> { }
