using _Scripts.Combat.Interfaces;
using UnityEngine;

namespace _Scripts.Combat.Data
{
    /// <summary>
    /// Types of damage for resistance calculation.
    /// MVP: Only Physical. Extensible for future elemental types.
    /// </summary>
    public enum DamageType
    {
        Physical = 0
        // Future: Fire, Lightning, Toxic, etc.
    }

    /// <summary>
    /// Complete information about damage being applied.
    /// Immutable struct for thread safety and value semantics.
    /// </summary>
    public readonly struct DamageInfo
    {
        /// <summary>Base damage amount before defense calculation.</summary>
        public readonly float Amount;

        /// <summary>Poise damage (contributes to stagger).</summary>
        public readonly float PoiseDamage;

        /// <summary>Type of damage for resistance calculation.</summary>
        public readonly DamageType Type;

        /// <summary>The combatant that dealt this damage. Can be null for environmental damage.</summary>
        public readonly ICombatant Source;

        /// <summary>World position where the hit landed.</summary>
        public readonly Vector3 HitPoint;

        /// <summary>Direction the hit came from (for knockback/reactions).</summary>
        public readonly Vector3 HitDirection;

        /// <summary>Whether this attack can be parried.</summary>
        public readonly bool CanBeParried;

        /// <summary>Timestamp when this damage was created.</summary>
        public readonly float Timestamp;

        public DamageInfo(
            float amount,
            float poiseDamage,
            DamageType type,
            ICombatant source,
            Vector3 hitPoint,
            Vector3 hitDirection,
            bool canBeParried = true)
        {
            Amount = amount;
            PoiseDamage = poiseDamage;
            Type = type;
            Source = source;
            HitPoint = hitPoint;
            HitDirection = hitDirection.normalized;
            CanBeParried = canBeParried;
            Timestamp = Time.time;
        }

        /// <summary>
        /// Creates damage info for a standard physical attack.
        /// </summary>
        public static DamageInfo CreatePhysical(
            float damage,
            float poiseDamage,
            ICombatant source,
            Vector3 hitPoint,
            Vector3 hitDirection)
        {
            return new DamageInfo(
                damage,
                poiseDamage,
                DamageType.Physical,
                source,
                hitPoint,
                hitDirection,
                canBeParried: true);
        }

        /// <summary>
        /// Creates damage info for unblockable attacks.
        /// </summary>
        public static DamageInfo CreateUnblockable(
            float damage,
            float poiseDamage,
            ICombatant source,
            Vector3 hitPoint,
            Vector3 hitDirection)
        {
            return new DamageInfo(
                damage,
                poiseDamage,
                DamageType.Physical,
                source,
                hitPoint,
                hitDirection,
                canBeParried: false);
        }
    }

    /// <summary>
    /// Result of a damage calculation after applying defenses and modifiers.
    /// </summary>
    public readonly struct DamageResult
    {
        /// <summary>Final damage after all reductions.</summary>
        public readonly float FinalDamage;

        /// <summary>Final poise damage after modifiers.</summary>
        public readonly float FinalPoiseDamage;

        /// <summary>Whether the attack was fully parried.</summary>
        public readonly bool WasParried;

        /// <summary>Whether the attack was partially parried.</summary>
        public readonly bool WasPartialParried;

        /// <summary>Whether the attack was dodged (i-frames).</summary>
        public readonly bool WasDodged;

        /// <summary>Whether the attack was blocked (passive block).</summary>
        public readonly bool WasBlocked;

        /// <summary>Whether this hit broke the target's poise.</summary>
        public readonly bool CausedPoiseBreak;

        /// <summary>Whether this hit killed the target.</summary>
        public readonly bool CausedDeath;

        public DamageResult(
            float finalDamage,
            float finalPoiseDamage,
            bool wasParried = false,
            bool wasPartialParried = false,
            bool wasDodged = false,
            bool wasBlocked = false,
            bool causedPoiseBreak = false,
            bool causedDeath = false)
        {
            FinalDamage = finalDamage;
            FinalPoiseDamage = finalPoiseDamage;
            WasParried = wasParried;
            WasPartialParried = wasPartialParried;
            WasDodged = wasDodged;
            WasBlocked = wasBlocked;
            CausedPoiseBreak = causedPoiseBreak;
            CausedDeath = causedDeath;
        }

        /// <summary>No damage was taken (dodged or perfect parried).</summary>
        public bool NoDamageTaken => FinalDamage <= 0f;

        /// <summary>Attack was successfully defended against (parry, dodge, or block).</summary>
        public bool WasDefended => WasParried || WasDodged || WasBlocked;
    }
}
