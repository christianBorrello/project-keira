using System;
using UnityEngine;

namespace _Scripts.Combat.Data
{
    /// <summary>
    /// Configuration data for a single attack.
    /// Used by weapons and enemies to define attack properties.
    /// </summary>
    [Serializable]
    public struct AttackData
    {
        [Header("Identity")]
        [Tooltip("Name for debugging and animation triggers")]
        public string AttackName;

        [Header("Damage")]
        [Range(0.1f, 10f)]
        [Tooltip("Damage multiplier applied to base damage")]
        public float DamageMultiplier;

        [Range(0f, 100f)]
        [Tooltip("Poise damage dealt to target")]
        public float PoiseDamage;

        [Header("Cost")]
        [Range(0f, 100f)]
        [Tooltip("Stamina consumed by this attack")]
        public float StaminaCost;

        [Header("Hitbox")]
        [Range(0.1f, 5f)]
        [Tooltip("Radius of the attack hitbox")]
        public float HitboxRadius;

        [Tooltip("Local offset from attacker for hitbox center")]
        public Vector3 HitboxOffset;

        [Range(0.5f, 5f)]
        [Tooltip("Maximum range of the attack")]
        public float Range;

        [Header("Timing (Normalized 0-1)")]
        [Range(0f, 1f)]
        [Tooltip("When hitbox becomes active (0 = start, 1 = end)")]
        public float ActiveFrameStart;

        [Range(0f, 1f)]
        [Tooltip("When hitbox deactivates")]
        public float ActiveFrameEnd;

        [Header("Animation")]
        [Range(0.1f, 3f)]
        [Tooltip("Total animation duration in seconds")]
        public float AnimationDuration;

        [Tooltip("Animation trigger name")]
        public string AnimationTrigger;

        [Header("Combo")]
        [Tooltip("Can chain into next attack")]
        public bool CanCombo;

        [Range(0f, 1f)]
        [Tooltip("When combo input window opens (normalized)")]
        public float ComboWindowStart;

        [Range(0f, 1f)]
        [Tooltip("When combo input window closes (normalized)")]
        public float ComboWindowEnd;

        [Range(0, 10)]
        [Tooltip("Position in combo chain (0 = first attack)")]
        public int ComboIndex;

        [Header("Properties")]
        [Tooltip("Can this attack be parried")]
        public bool CanBeParried;

        [Tooltip("Does this attack have super armor (can't be interrupted)")]
        public bool HasSuperArmor;

        /// <summary>
        /// Creates a default light attack configuration.
        /// </summary>
        public static AttackData CreateLightAttack(int comboIndex = 0) => new AttackData
        {
            AttackName = $"LightAttack_{comboIndex + 1}",
            DamageMultiplier = 1f,
            PoiseDamage = 10f,
            StaminaCost = 15f,
            HitboxRadius = 0.8f,
            HitboxOffset = new Vector3(0f, 1f, 1f),
            Range = 2f,
            ActiveFrameStart = 0.2f,
            ActiveFrameEnd = 0.4f,
            AnimationDuration = 0.6f,
            AnimationTrigger = $"LightAttack{comboIndex + 1}",
            CanCombo = true,
            ComboWindowStart = 0.5f,
            ComboWindowEnd = 0.8f,
            ComboIndex = comboIndex,
            CanBeParried = true,
            HasSuperArmor = false
        };

        /// <summary>
        /// Creates a default heavy attack configuration.
        /// </summary>
        public static AttackData CreateHeavyAttack(int comboIndex = 0) => new AttackData
        {
            AttackName = $"HeavyAttack_{comboIndex + 1}",
            DamageMultiplier = 1.8f,
            PoiseDamage = 25f,
            StaminaCost = 30f,
            HitboxRadius = 1.2f,
            HitboxOffset = new Vector3(0f, 1f, 1.2f),
            Range = 2.5f,
            ActiveFrameStart = 0.35f,
            ActiveFrameEnd = 0.5f,
            AnimationDuration = 1f,
            AnimationTrigger = $"HeavyAttack{comboIndex + 1}",
            CanCombo = false,
            ComboWindowStart = 0f,
            ComboWindowEnd = 0f,
            ComboIndex = comboIndex,
            CanBeParried = true,
            HasSuperArmor = true
        };

        /// <summary>
        /// Gets the active frame duration in seconds.
        /// </summary>
        public float ActiveDuration => (ActiveFrameEnd - ActiveFrameStart) * AnimationDuration;

        /// <summary>
        /// Gets the time when the hitbox activates in seconds.
        /// </summary>
        public float ActiveStartTime => ActiveFrameStart * AnimationDuration;

        /// <summary>
        /// Gets the time when the hitbox deactivates in seconds.
        /// </summary>
        public float ActiveEndTime => ActiveFrameEnd * AnimationDuration;

        /// <summary>
        /// Checks if a normalized time is within the combo window.
        /// </summary>
        public bool IsInComboWindow(float normalizedTime)
        {
            return CanCombo && normalizedTime >= ComboWindowStart && normalizedTime <= ComboWindowEnd;
        }

        /// <summary>
        /// Checks if a normalized time is within the active hitbox window.
        /// </summary>
        public bool IsHitboxActive(float normalizedTime)
        {
            return normalizedTime >= ActiveFrameStart && normalizedTime <= ActiveFrameEnd;
        }
    }
}
