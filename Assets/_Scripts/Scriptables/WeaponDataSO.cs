using UnityEngine;

namespace _Scripts.Scriptables
{
    /// <summary>
    /// Weapon type enumeration.
    /// </summary>
    public enum WeaponType
    {
        Sword,
        Greatsword,
        Dagger,
        Spear,
        Fist,
        Staff
    }

    /// <summary>
    /// Weapon configuration data.
    /// Defines attack properties, damage, and animation timings for a weapon.
    /// </summary>
    [CreateAssetMenu(fileName = "WeaponData", menuName = "Combat/Weapon Data", order = 1)]
    public class WeaponDataSO : ScriptableObject
    {
        [Header("Identity")]
        public string WeaponName = "New Weapon";
        public WeaponType Type = WeaponType.Sword;

        [TextArea(2, 4)]
        public string Description;

        [Header("Damage")]
        [Range(1, 500)]
        [Tooltip("Base physical damage")]
        public int BaseDamage = 20;

        [Range(0, 200)]
        [Tooltip("Base poise damage")]
        public int BasePoiseDamage = 15;

        [Range(0.5f, 2f)]
        [Tooltip("Damage multiplier for light attacks")]
        public float LightDamageMultiplier = 1f;

        [Range(0.5f, 3f)]
        [Tooltip("Damage multiplier for heavy attacks")]
        public float HeavyDamageMultiplier = 1.8f;

        [Header("Attack Timing")]
        [Range(0.1f, 2f)]
        [Tooltip("Duration of light attack animation")]
        public float LightAttackDuration = 0.5f;

        [Range(0.2f, 3f)]
        [Tooltip("Duration of heavy attack animation")]
        public float HeavyAttackDuration = 1.0f;

        [Range(0f, 1f)]
        [Tooltip("Startup time before hitbox activates (as % of duration)")]
        public float StartupPercent = 0.15f;

        [Range(0f, 1f)]
        [Tooltip("Active hitbox time (as % of duration)")]
        public float ActivePercent = 0.3f;

        [Header("Combo")]
        [Range(1, 5)]
        [Tooltip("Maximum light attack combo count")]
        public int MaxComboCount = 3;

        [Range(0.3f, 0.8f)]
        [Tooltip("When combo window opens (as % of attack duration)")]
        public float ComboWindowStart = 0.5f;

        [Range(0.6f, 1f)]
        [Tooltip("When recovery window opens (as % of attack duration)")]
        public float RecoveryWindowStart = 0.8f;

        [Header("Stamina Costs")]
        [Range(5f, 50f)]
        [Tooltip("Stamina cost per light attack")]
        public float LightStaminaCost = 15f;

        [Range(10f, 100f)]
        [Tooltip("Stamina cost per heavy attack")]
        public float HeavyStaminaCost = 30f;

        [Header("Range & Reach")]
        [Range(0.5f, 5f)]
        [Tooltip("Horizontal attack range")]
        public float AttackRange = 2f;

        [Range(0.1f, 3f)]
        [Tooltip("Hitbox width")]
        public float HitboxWidth = 1f;

        [Header("Animation")]
        [Tooltip("Animation controller override for this weapon")]
        public RuntimeAnimatorController AnimatorOverride;

        [Header("Audio")]
        [Tooltip("Sound effect for light swing")]
        public AudioClip LightSwingSound;

        [Tooltip("Sound effect for heavy swing")]
        public AudioClip HeavySwingSound;

        [Tooltip("Sound effect on hit")]
        public AudioClip HitSound;

        /// <summary>
        /// Calculate final damage for light attack.
        /// </summary>
        public float GetLightDamage() => BaseDamage * LightDamageMultiplier;

        /// <summary>
        /// Calculate final damage for heavy attack.
        /// </summary>
        public float GetHeavyDamage() => BaseDamage * HeavyDamageMultiplier;

        /// <summary>
        /// Get startup time in seconds for light attack.
        /// </summary>
        public float GetLightStartup() => LightAttackDuration * StartupPercent;

        /// <summary>
        /// Get startup time in seconds for heavy attack.
        /// </summary>
        public float GetHeavyStartup() => HeavyAttackDuration * StartupPercent;

        /// <summary>
        /// Get active hitbox time in seconds for light attack.
        /// </summary>
        public float GetLightActiveTime() => LightAttackDuration * ActivePercent;

        /// <summary>
        /// Get active hitbox time in seconds for heavy attack.
        /// </summary>
        public float GetHeavyActiveTime() => HeavyAttackDuration * ActivePercent;
    }
}
