using _Scripts.Combat.Interfaces;
using UnityEngine;

namespace _Scripts.Core.Events.Types
{
    /// <summary>
    /// GameEvent that carries a combatant reference.
    /// Use for death notifications, target changes, and combatant-specific events.
    /// </summary>
    [CreateAssetMenu(fileName = "NewCombatantEvent", menuName = "Events/Combat/Combatant Event", order = 22)]
    public class CombatantEvent : GameEvent<ICombatant> { }

    /// <summary>
    /// Listener component for CombatantEvent.
    /// </summary>
    [AddComponentMenu("Events/Combatant Event Listener")]
    public class CombatantEventListener : GameEventListener<CombatantEvent, ICombatant> { }
}