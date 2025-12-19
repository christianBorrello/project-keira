using UnityEngine;

namespace _Scripts.Scriptables
{
    /// <summary>
    /// Global combat system configuration.
    /// Defines system-wide combat settings like hitstop, damage scaling, and timing.
    /// </summary>
    [CreateAssetMenu(fileName = "CombatConfig", menuName = "Combat/Combat Config", order = 0)]
    public class CombatConfigSO : ScriptableObject
    {
        [Header("Hitstop")]
        [Range(0f, 0.5f)]
        [Tooltip("Duration of hitstop on light attacks")]
        public float LightHitstopDuration = 0.05f;

        [Range(0f, 0.5f)]
        [Tooltip("Duration of hitstop on heavy attacks")]
        public float HeavyHitstopDuration = 0.1f;

        [Range(0f, 0.5f)]
        [Tooltip("Duration of hitstop on critical hits")]
        public float CriticalHitstopDuration = 0.15f;

        [Header("Damage Scaling")]
        [Range(0.5f, 2f)]
        [Tooltip("Global damage multiplier")]
        public float GlobalDamageMultiplier = 1f;

        [Range(0.5f, 3f)]
        [Tooltip("Damage multiplier for backstabs")]
        public float BackstabMultiplier = 1.5f;

        [Range(0.5f, 3f)]
        [Tooltip("Damage multiplier for critical hits (after posture break)")]
        public float CriticalMultiplier = 2f;

        [Header("Poise System")]
        [Range(1f, 5f)]
        [Tooltip("Poise damage multiplier for heavy attacks")]
        public float HeavyPoiseDamageMultiplier = 1.5f;

        [Range(1f, 5f)]
        [Tooltip("Poise damage multiplier for charged attacks")]
        public float ChargedPoiseDamageMultiplier = 2f;

        [Header("Combat Timing")]
        [Range(0.05f, 0.3f)]
        [Tooltip("Input buffer window in seconds")]
        public float InputBufferWindow = 0.15f;

        [Range(0.1f, 1f)]
        [Tooltip("Default combo window as percentage of attack duration")]
        public float DefaultComboWindowStart = 0.5f;

        [Range(0.5f, 1f)]
        [Tooltip("Default recovery window as percentage of attack duration")]
        public float DefaultRecoveryWindowStart = 0.8f;

        [Header("Lock-On")]
        [Range(5f, 50f)]
        [Tooltip("Maximum distance for lock-on targeting")]
        public float LockOnMaxDistance = 25f;

        [Range(30f, 180f)]
        [Tooltip("Field of view angle for lock-on detection")]
        public float LockOnFieldOfView = 90f;

        [Range(0.1f, 2f)]
        [Tooltip("Time before lock-on switches to next target when current dies")]
        public float LockOnSwitchDelay = 0.5f;

        [Header("Physics")]
        [Tooltip("Layer mask for hitbox collision detection")]
        public LayerMask HitboxLayerMask;

        [Tooltip("Layer mask for line of sight checks")]
        public LayerMask LineOfSightMask;
    }
}
