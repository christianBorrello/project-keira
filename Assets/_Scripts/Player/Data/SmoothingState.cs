using System;
using UnityEngine;

namespace _Scripts.Player.Data
{
    /// <summary>
    /// Consolidates all smoothing and velocity state variables for player movement.
    /// Used for smooth interpolation of movement, rotation, and animation parameters.
    /// </summary>
    /// <remarks>
    /// This struct groups related velocity fields that were previously scattered
    /// throughout PlayerController. Pass by reference to avoid copy overhead.
    /// </remarks>
    [Serializable]
    public struct SmoothingState
    {
        [Header("Movement Smoothing")]
        [Tooltip("Current smoothed movement direction")]
        public Vector3 SmoothedMoveDirection;

        [Tooltip("Velocity reference for movement direction smoothing")]
        public Vector3 MoveDirectionVelocity;

        [Header("Animator Speed")]
        [Tooltip("Current smoothed animator speed parameter (0=idle, 0.5=walk, 1=run, 2=sprint)")]
        public float CurrentAnimatorSpeed;

        [Tooltip("Velocity reference for animator speed smoothing")]
        public float AnimatorSpeedVelocity;

        [Header("Animation Speed Matching")]
        [Tooltip("Current animation playback speed multiplier to prevent foot sliding")]
        public float CurrentAnimationSpeedMultiplier;

        [Tooltip("Velocity reference for animation speed multiplier smoothing")]
        public float AnimationSpeedMultiplierVelocity;

        [Header("Character Rotation")]
        [Tooltip("Velocity reference for character rotation smoothing")]
        public float RotationVelocity;

        [Header("Lock-On Directional Animation")]
        [Tooltip("Current local X movement for strafe animation (-1=left, 1=right)")]
        public float LocalMoveX;

        [Tooltip("Current local Y movement for forward/back animation (-1=back, 1=forward)")]
        public float LocalMoveY;

        [Tooltip("Velocity reference for MoveX smoothing")]
        public float MoveXVelocity;

        [Tooltip("Velocity reference for MoveY smoothing")]
        public float MoveYVelocity;

        [Header("Lock-On Distance Tracking")]
        [Tooltip("Distance to target when lock-on was acquired (for orbital movement)")]
        public float LockedOnDistance;

        [Tooltip("Target-relative strafe component of movement")]
        public float TargetRelativeStrafe;

        [Tooltip("Target-relative approach component of movement")]
        public float TargetRelativeApproach;

        [Tooltip("Velocity correction to maintain locked distance during strafe")]
        public Vector3 LockOnDistanceCorrection;

        [Header("Momentum System")]
        [Tooltip("Current actual velocity magnitude (0 to max speed)")]
        public float CurrentVelocityMagnitude;

        [Tooltip("Target velocity magnitude based on input and mode")]
        public float TargetVelocityMagnitude;

        [Tooltip("Time since movement started (for acceleration curve)")]
        public float AccelerationTimer;

        [Tooltip("Time since input stopped (for deceleration curve)")]
        public float DecelerationTimer;

        [Tooltip("True when actively accelerating (has input)")]
        public bool IsAccelerating;

        [Tooltip("Preserved direction during deceleration (prevents early direction decay)")]
        public Vector3 DecelerationDirection;

        [Header("Turn Tracking")]
        [Tooltip("Angle between current forward and target direction (-180 to 180)")]
        public float TurnAngle;

        [Tooltip("True when in turn-in-place state")]
        public bool IsTurningInPlace;

        [Tooltip("Progress through turn-in-place animation (0-1)")]
        public float TurnProgress;

        [Tooltip("Target direction for turn-in-place")]
        public Vector3 TurnTargetDirection;

        /// <summary>
        /// Creates a SmoothingState with default values.
        /// </summary>
        public static SmoothingState CreateDefault()
        {
            return new SmoothingState
            {
                SmoothedMoveDirection = Vector3.zero,
                MoveDirectionVelocity = Vector3.zero,
                CurrentAnimatorSpeed = 0f,
                AnimatorSpeedVelocity = 0f,
                CurrentAnimationSpeedMultiplier = 1f,
                AnimationSpeedMultiplierVelocity = 0f,
                RotationVelocity = 0f,
                LocalMoveX = 0f,
                LocalMoveY = 0f,
                MoveXVelocity = 0f,
                MoveYVelocity = 0f,
                LockedOnDistance = 0f,
                TargetRelativeStrafe = 0f,
                TargetRelativeApproach = 0f,
                LockOnDistanceCorrection = Vector3.zero,
                // Momentum system
                CurrentVelocityMagnitude = 0f,
                TargetVelocityMagnitude = 0f,
                AccelerationTimer = 0f,
                DecelerationTimer = 0f,
                IsAccelerating = false,
                DecelerationDirection = Vector3.zero,
                // Turn tracking
                TurnAngle = 0f,
                IsTurningInPlace = false,
                TurnProgress = 0f,
                TurnTargetDirection = Vector3.forward
            };
        }

        /// <summary>
        /// Resets all smoothing velocities to zero without changing current values.
        /// Call when teleporting or during state transitions that shouldn't blend.
        /// </summary>
        public void ResetVelocities()
        {
            MoveDirectionVelocity = Vector3.zero;
            AnimatorSpeedVelocity = 0f;
            AnimationSpeedMultiplierVelocity = 0f;
            RotationVelocity = 0f;
            MoveXVelocity = 0f;
            MoveYVelocity = 0f;
            // Reset momentum timers
            AccelerationTimer = 0f;
            DecelerationTimer = 0f;
        }

        /// <summary>
        /// Resets all values to default state.
        /// Call when respawning or resetting player.
        /// </summary>
        public void Reset()
        {
            this = CreateDefault();
        }
    }
}
