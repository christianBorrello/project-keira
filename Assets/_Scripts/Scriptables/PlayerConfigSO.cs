using _Scripts.Combat.Data;
using UnityEngine;

namespace _Scripts.Scriptables
{
    /// <summary>
    /// Player configuration data.
    /// Contains base combat stats and player-specific settings.
    /// </summary>
    [CreateAssetMenu(fileName = "PlayerConfig", menuName = "Combat/Player Config", order = 2)]
    public class PlayerConfigSO : ScriptableObject
    {
        [Header("Base Stats")]
        [Tooltip("Combat statistics for this player configuration")]
        public CombatStats BaseStats = CombatStats.CreateDefaultPlayer();

        [Header("Equipment")]
        [Tooltip("Default weapon for this player configuration")]
        public WeaponDataSO DefaultWeapon;

        [Header("Lock-On")]
        [Range(5f, 30f)]
        [Tooltip("Maximum lock-on distance")]
        public float LockOnRange = 20f;

        [Range(30f, 180f)]
        [Tooltip("Lock-on field of view angle")]
        public float LockOnFOV = 90f;

        [Header("Dodge")]
        [Range(0.1f, 1f)]
        [Tooltip("Duration of invincibility frames during dodge")]
        public float DodgeIFrames = 0.3f;

        [Range(0.3f, 1.5f)]
        [Tooltip("Total dodge animation duration")]
        public float DodgeDuration = 0.6f;

        [Range(2f, 10f)]
        [Tooltip("Distance covered during dodge roll")]
        public float DodgeDistance = 4f;

        [Header("Parry")]
        [Range(0.1f, 0.5f)]
        [Tooltip("Total parry window duration")]
        public float ParryWindow = 0.2f;

        [Range(0.05f, 0.25f)]
        [Tooltip("Perfect parry timing window (first portion)")]
        public float PerfectParryWindow = 0.1f;

        [Header("Camera")]
        [Range(0.5f, 5f)]
        [Tooltip("Camera distance when locked on")]
        public float LockOnCameraDistance = 3f;

        [Range(0f, 90f)]
        [Tooltip("Camera vertical offset angle")]
        public float CameraAngle = 20f;

        [Header("Visual")]
        [Tooltip("Player model prefab")]
        public GameObject ModelPrefab;

        [Tooltip("Animator controller for player")]
        public RuntimeAnimatorController AnimatorController;

        [Header("Audio")]
        [Tooltip("Footstep sound effects")]
        public AudioClip[] FootstepSounds;

        [Tooltip("Dodge/roll sound effect")]
        public AudioClip DodgeSound;

        [Tooltip("Parry success sound effect")]
        public AudioClip ParrySound;

        [Tooltip("Damage taken sound effect")]
        public AudioClip HurtSound;

        [Tooltip("Death sound effect")]
        public AudioClip DeathSound;

        /// <summary>
        /// Create runtime combat data from this configuration.
        /// </summary>
        public CombatRuntimeData CreateRuntimeData()
        {
            return new CombatRuntimeData(BaseStats);
        }

        /// <summary>
        /// Get effective dodge i-frame duration.
        /// Returns the configured value or falls back to BaseStats.
        /// </summary>
        public float GetDodgeIFrames()
        {
            return DodgeIFrames > 0 ? DodgeIFrames : BaseStats.DodgeIFrameDuration;
        }

        /// <summary>
        /// Get effective parry window.
        /// </summary>
        public float GetParryWindow()
        {
            return ParryWindow > 0 ? ParryWindow : BaseStats.ParryWindowDuration;
        }

        /// <summary>
        /// Get effective perfect parry window.
        /// </summary>
        public float GetPerfectParryWindow()
        {
            return PerfectParryWindow > 0 ? PerfectParryWindow : BaseStats.PerfectParryWindow;
        }
    }
}
