using _Scripts.Combat.Data;
using _Scripts.Combat.Interfaces;
using UnityEngine;

/// <summary>
/// Data payload for damage events.
/// Contains full context about damage dealt for logging, effects, and UI.
/// </summary>
[System.Serializable]
public struct DamageEventData
{
    [Tooltip("The combatant that dealt the damage")]
    public ICombatant Attacker;

    [Tooltip("The combatant that received the damage")]
    public ICombatant Target;

    [Tooltip("Original damage information before defenses")]
    public DamageInfo DamageInfo;

    [Tooltip("Final damage result after defenses")]
    public DamageResult Result;

    public DamageEventData(ICombatant attacker, ICombatant target, DamageInfo damageInfo, DamageResult result)
    {
        Attacker = attacker;
        Target = target;
        DamageInfo = damageInfo;
        Result = result;
    }

    public override string ToString() =>
        $"Damage: {Attacker?.GetType().Name ?? "Unknown"} -> {Target?.GetType().Name ?? "Unknown"}: {Result.FinalDamage:F0} dmg";
}

/// <summary>
/// GameEvent that carries damage event data.
/// Use for combat logging, hit effects, and damage number UI.
/// </summary>
[CreateAssetMenu(fileName = "NewDamageEvent", menuName = "Events/Combat/Damage Event", order = 21)]
public class DamageEvent : GameEvent<DamageEventData> { }

/// <summary>
/// Listener component for DamageEvent.
/// </summary>
[AddComponentMenu("Events/Damage Event Listener")]
public class DamageEventListener : GameEventListener<DamageEvent, DamageEventData> { }
