using System;
using UnityEngine;

namespace _Scripts.Combat.Data
{
    /// <summary>
    /// Complete combat statistics for any combatant entity.
    /// Designed for souls-like combat with stamina, poise, and parry mechanics.
    /// </summary>
    [Serializable]
    public struct CombatStats
    {
        [Header("Health")]
        [Range(1, 9999)]
        [Tooltip("Maximum health points")]
        public int MaxHealth;

        [Header("Stamina")]
        [Range(1, 500)]
        [Tooltip("Maximum stamina points")]
        public float MaxStamina;

        [Range(1f, 100f)]
        [Tooltip("Stamina regeneration per second")]
        public float StaminaRegenRate;

        [Range(0f, 3f)]
        [Tooltip("Delay before stamina regenerates after action")]
        public float StaminaRegenDelay;

        [Header("Poise")]
        [Range(0, 1000)]
        [Tooltip("Maximum poise before stagger (Lies of P style - accumulates)")]
        public int MaxPoise;

        [Range(0f, 100f)]
        [Tooltip("Poise recovery per second when not hit")]
        public float PoiseRegenRate;

        [Range(0f, 10f)]
        [Tooltip("Delay before poise starts recovering")]
        public float PoiseRegenDelay;

        [Header("Attack")]
        [Range(1, 999)]
        [Tooltip("Base physical attack power")]
        public int BaseDamage;

        [Range(0.1f, 5f)]
        [Tooltip("Damage multiplier for light attacks")]
        public float LightAttackMultiplier;

        [Range(0.1f, 5f)]
        [Tooltip("Damage multiplier for heavy attacks")]
        public float HeavyAttackMultiplier;

        [Range(0.5f, 2f)]
        [Tooltip("Attack animation speed multiplier")]
        public float AttackSpeed;

        [Header("Defense")]
        [Range(0f, 0.9f)]
        [Tooltip("Physical damage reduction (0.2 = 20% reduction)")]
        public float PhysicalDefense;

        [Range(0f, 1f)]
        [Tooltip("Damage reduction on partial parry")]
        public float PartialParryDamageReduction;

        [Header("Stamina Costs")]
        [Range(1f, 50f)]
        [Tooltip("Stamina per second while sprinting")]
        public float SprintStaminaCost;

        [Range(1f, 50f)]
        [Tooltip("Stamina cost for dodge roll")]
        public float DodgeStaminaCost;

        [Range(1f, 50f)]
        [Tooltip("Stamina cost for light attack")]
        public float LightAttackStaminaCost;

        [Range(1f, 100f)]
        [Tooltip("Stamina cost for heavy attack")]
        public float HeavyAttackStaminaCost;

        [Header("Timing")]
        [Range(0.05f, 1f)]
        [Tooltip("Parry window duration in seconds")]
        public float ParryWindowDuration;

        [Range(0.01f, 0.5f)]
        [Tooltip("Perfect parry window (first portion of parry window)")]
        public float PerfectParryWindow;

        [Range(0.1f, 1f)]
        [Tooltip("Invincibility frames during dodge")]
        public float DodgeIFrameDuration;

        [Range(0.3f, 2f)]
        [Tooltip("Total dodge animation duration")]
        public float DodgeDuration;

        [Range(0.5f, 5f)]
        [Tooltip("Recovery time after stagger")]
        public float StaggerRecoveryTime;

        [Header("Movement")]
        [Range(1f, 20f)]
        [Tooltip("Base movement speed")]
        public float MoveSpeed;

        [Range(1f, 3f)]
        [Tooltip("Sprint speed multiplier")]
        public float SprintMultiplier;

        [Range(90f, 720f)]
        [Tooltip("Rotation speed in degrees per second")]
        public float RotationSpeed;

        [Range(1f, 10f)]
        [Tooltip("Dodge roll distance")]
        public float DodgeDistance;

        /// <summary>
        /// Creates default player stats for MVP.
        /// </summary>
        public static CombatStats CreateDefaultPlayer() => new CombatStats
        {
            // Health
            MaxHealth = 100,

            // Stamina
            MaxStamina = 100f,
            StaminaRegenRate = 30f,
            StaminaRegenDelay = 0.8f,

            // Poise
            MaxPoise = 50,
            PoiseRegenRate = 20f,
            PoiseRegenDelay = 3f,

            // Attack
            BaseDamage = 20,
            LightAttackMultiplier = 1f,
            HeavyAttackMultiplier = 1.8f,
            AttackSpeed = 1f,

            // Defense
            PhysicalDefense = 0.1f,
            PartialParryDamageReduction = 0.5f,

            // Stamina Costs
            SprintStaminaCost = 15f,
            DodgeStaminaCost = 20f,
            LightAttackStaminaCost = 15f,
            HeavyAttackStaminaCost = 30f,

            // Timing
            ParryWindowDuration = 0.2f,
            PerfectParryWindow = 0.1f,
            DodgeIFrameDuration = 0.3f,
            DodgeDuration = 0.6f,
            StaggerRecoveryTime = 1.5f,

            // Movement
            MoveSpeed = 5f,
            SprintMultiplier = 1.6f,
            RotationSpeed = 360f,
            DodgeDistance = 4f
        };

        /// <summary>
        /// Creates default basic enemy stats for MVP.
        /// </summary>
        public static CombatStats CreateDefaultEnemy() => new CombatStats
        {
            // Health
            MaxHealth = 50,

            // Stamina (enemies don't use stamina in MVP)
            MaxStamina = 100f,
            StaminaRegenRate = 100f,
            StaminaRegenDelay = 0f,

            // Poise (lower than player - easier to stagger)
            MaxPoise = 30,
            PoiseRegenRate = 10f,
            PoiseRegenDelay = 5f,

            // Attack
            BaseDamage = 15,
            LightAttackMultiplier = 1f,
            HeavyAttackMultiplier = 1.5f,
            AttackSpeed = 0.8f,

            // Defense
            PhysicalDefense = 0.05f,
            PartialParryDamageReduction = 0.7f,

            // Stamina Costs (not used by basic AI)
            SprintStaminaCost = 0f,
            DodgeStaminaCost = 0f,
            LightAttackStaminaCost = 0f,
            HeavyAttackStaminaCost = 0f,

            // Timing (enemies don't parry/dodge in MVP)
            ParryWindowDuration = 0f,
            PerfectParryWindow = 0f,
            DodgeIFrameDuration = 0f,
            DodgeDuration = 0f,
            StaggerRecoveryTime = 2f,

            // Movement
            MoveSpeed = 3f,
            SprintMultiplier = 1f,
            RotationSpeed = 180f,
            DodgeDistance = 0f
        };
    }

    /// <summary>
    /// Runtime combat data that tracks current values.
    /// Separate from CombatStats to allow base stats to remain immutable.
    /// </summary>
    [Serializable]
    public class CombatRuntimeData
    {
        public int CurrentHealth;
        public float CurrentStamina;
        public int CurrentPoise;
        public float LastStaminaUseTime;
        public float LastPoiseHitTime;

        public CombatRuntimeData(CombatStats stats)
        {
            CurrentHealth = stats.MaxHealth;
            CurrentStamina = stats.MaxStamina;
            CurrentPoise = 0; // Poise starts empty (Lies of P style)
            LastStaminaUseTime = -999f;
            LastPoiseHitTime = -999f;
        }

        public bool IsAlive => CurrentHealth > 0;
        public bool IsPoisebroken(int maxPoise) => CurrentPoise >= maxPoise;
    }
}
