using System;
using _Scripts.Combat.Data;
using UnityEngine;

namespace _Scripts.Combat.Interfaces
{
    /// <summary>
    /// Faction affiliation for friendly fire prevention.
    /// </summary>
    public enum Faction
    {
        Player = 0,
        Enemy = 1,
        Neutral = 2
    }

    /// <summary>
    /// Interface for any entity that can participate in combat.
    /// Implemented by PlayerController and EnemyController.
    /// </summary>
    public interface ICombatant
    {
        /// <summary>
        /// Unique identifier for this combatant.
        /// Used for targeting, event routing, and combat log.
        /// </summary>
        int CombatantId { get; }

        /// <summary>
        /// Display name for UI and debugging.
        /// </summary>
        string CombatantName { get; }

        /// <summary>
        /// Current faction affiliation.
        /// Used for friendly fire prevention and AI targeting.
        /// </summary>
        Faction Faction { get; }

        /// <summary>
        /// Whether this combatant is currently alive.
        /// </summary>
        bool IsAlive { get; }

        /// <summary>
        /// Transform component for position and rotation.
        /// </summary>
        Transform Transform { get; }

        /// <summary>
        /// Current position in world space.
        /// </summary>
        Vector3 Position { get; }

        /// <summary>
        /// Forward direction of this combatant.
        /// </summary>
        Vector3 Forward { get; }

        /// <summary>
        /// Gets the current combat statistics.
        /// </summary>
        CombatStats GetBaseStats();

        /// <summary>
        /// Gets the current runtime combat data.
        /// </summary>
        CombatRuntimeData GetRuntimeData();

        /// <summary>
        /// Whether this combatant is currently parrying.
        /// </summary>
        /// <param name="timing">Output parry timing information.</param>
        /// <returns>True if actively in parry state.</returns>
        bool IsParrying(out ParryTiming timing);

        /// <summary>
        /// Whether this combatant is invulnerable (i-frames active).
        /// </summary>
        bool IsInvulnerable { get; }

        /// <summary>
        /// Apply stagger to this combatant.
        /// Called when poise is broken or parried.
        /// </summary>
        /// <param name="duration">Stagger duration in seconds.</param>
        void ApplyStagger(float duration);

        /// <summary>
        /// Event fired when this combatant initiates an attack.
        /// </summary>
        event Action<ICombatant, AttackData> OnAttackStarted;

        /// <summary>
        /// Event fired when this combatant's attack hits something.
        /// </summary>
        event Action<ICombatant, IDamageable, DamageResult> OnHitLanded;

        /// <summary>
        /// Event fired when this combatant is staggered.
        /// </summary>
        event Action<ICombatant, float> OnStaggered;
    }
}
