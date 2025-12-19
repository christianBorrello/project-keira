using UnityEngine;

/// <summary>
/// Data payload for health change events.
/// </summary>
[System.Serializable]
public struct HealthEventData
{
    [Tooltip("Current health after the change")]
    public float CurrentHealth;

    [Tooltip("Maximum health capacity")]
    public float MaxHealth;

    [Tooltip("Amount of change (positive = heal, negative = damage)")]
    public float Delta;

    /// <summary>
    /// Health as a normalized 0-1 value for UI sliders.
    /// </summary>
    public float NormalizedHealth => MaxHealth > 0 ? CurrentHealth / MaxHealth : 0f;

    /// <summary>
    /// Whether the entity is still alive.
    /// </summary>
    public bool IsAlive => CurrentHealth > 0;

    public HealthEventData(float current, float max, float delta)
    {
        CurrentHealth = current;
        MaxHealth = max;
        Delta = delta;
    }

    public override string ToString() =>
        $"Health: {CurrentHealth:F0}/{MaxHealth:F0} (Delta: {Delta:+0;-0;0})";
}

/// <summary>
/// GameEvent that carries health change data.
/// Provides full health context for UI and game systems.
/// </summary>
[CreateAssetMenu(fileName = "NewHealthEvent", menuName = "Events/Combat/Health Event", order = 20)]
public class HealthEvent : GameEvent<HealthEventData>
{
#if UNITY_EDITOR
    [Header("Debug")]
    [SerializeField] private HealthEventData _debugValue = new(100f, 100f, 0f);

    [ContextMenu("Raise Debug Value")]
    private void RaiseDebug() => Raise(_debugValue);
#endif
}

/// <summary>
/// Listener component for HealthEvent.
/// </summary>
[AddComponentMenu("Events/Health Event Listener")]
public class HealthEventListener : GameEventListener<HealthEvent, HealthEventData> { }
