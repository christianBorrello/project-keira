using UnityEngine;

namespace _Scripts.Core.Events.Types
{
    /// <summary>
    /// GameEvent that carries a float value.
    /// Useful for normalized values (0-1) like health percentage.
    /// </summary>
    [CreateAssetMenu(fileName = "NewFloatEvent", menuName = "Events/Float Event", order = 10)]
    public class FloatEvent : GameEvent<float>
    {
#if UNITY_EDITOR
        [Header("Debug")]
        [SerializeField] private float _debugValue = 1f;

        [ContextMenu("Raise Debug Value")]
        private void RaiseDebug() => Raise(_debugValue);
#endif
    }

    /// <summary>
    /// Listener component for FloatEvent.
    /// </summary>
    [AddComponentMenu("Events/Float Event Listener")]
    public class FloatEventListener : GameEventListener<FloatEvent, float> { }
}