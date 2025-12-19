using System;
using _Scripts.Combat.Data;

namespace _Scripts.Combat.Interfaces
{
    /// <summary>
    /// Interface for any entity that can receive damage.
    /// More general than ICombatant - includes destructible objects.
    /// </summary>
    public interface IDamageable
    {
        /// <summary>
        /// Current health value.
        /// </summary>
        float CurrentHealth { get; }

        /// <summary>
        /// Maximum health value.
        /// </summary>
        float MaxHealth { get; }

        /// <summary>
        /// Normalized health (0-1 range). Useful for UI health bars.
        /// </summary>
        float HealthNormalized { get; }

        /// <summary>
        /// Whether this entity is currently alive.
        /// </summary>
        bool IsAlive { get; }

        /// <summary>
        /// Apply damage to this entity.
        /// </summary>
        /// <param name="damageInfo">Complete damage information.</param>
        /// <returns>Result of the damage calculation including final values and effects.</returns>
        DamageResult TakeDamage(DamageInfo damageInfo);

        /// <summary>
        /// Heal this entity.
        /// </summary>
        /// <param name="amount">Amount to heal.</param>
        /// <returns>Actual amount healed (may be less if near max).</returns>
        float Heal(float amount);

        /// <summary>
        /// Kill this entity immediately. Bypasses damage calculation.
        /// </summary>
        void Die();

        /// <summary>
        /// Event fired when health changes.
        /// Parameters: current health, max health, delta (negative = damage, positive = heal).
        /// </summary>
        event Action<float, float, float> OnHealthChanged;

        /// <summary>
        /// Event fired when entity takes damage.
        /// </summary>
        event Action<DamageInfo, DamageResult> OnDamaged;

        /// <summary>
        /// Event fired when entity dies.
        /// </summary>
        event Action OnDeath;
    }

    /// <summary>
    /// Extended damageable with poise mechanics.
    /// </summary>
    public interface IDamageableWithPoise : IDamageable
    {
        /// <summary>
        /// Current accumulated poise damage.
        /// </summary>
        int CurrentPoise { get; }

        /// <summary>
        /// Maximum poise before stagger.
        /// </summary>
        int MaxPoise { get; }

        /// <summary>
        /// Normalized poise (0-1 range). 1 = about to break.
        /// </summary>
        float PoiseNormalized { get; }

        /// <summary>
        /// Apply poise damage without health damage.
        /// </summary>
        /// <param name="amount">Poise damage amount.</param>
        /// <returns>True if poise was broken.</returns>
        bool ApplyPoiseDamage(float amount);

        /// <summary>
        /// Reset poise to zero after stagger recovery.
        /// </summary>
        void ResetPoise();

        /// <summary>
        /// Event fired when poise is broken.
        /// </summary>
        event Action OnPoiseBreak;
    }
}
