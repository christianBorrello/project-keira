using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// MonoBehaviour that listens to a GameEvent and invokes UnityEvent responses.
/// Drop this on any GameObject to wire up event responses in the Inspector.
/// </summary>
[AddComponentMenu("Events/Game Event Listener")]
public class GameEventListener : MonoBehaviour, IGameEventListener
{
    [Tooltip("The GameEvent asset to listen to")]
    [SerializeField] private GameEvent _event;

    [Tooltip("Response to invoke when the event is raised")]
    [SerializeField] private UnityEvent _response;

    private void OnEnable()
    {
        _event?.Register(this);
    }

    private void OnDisable()
    {
        _event?.Unregister(this);
    }

    public void OnEventRaised()
    {
        _response?.Invoke();
    }

#if UNITY_EDITOR
    [ContextMenu("Test Response")]
    private void TestResponse() => _response?.Invoke();
#endif
}
