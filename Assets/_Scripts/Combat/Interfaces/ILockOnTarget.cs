using System;
using UnityEngine;

namespace _Scripts.Combat.Interfaces
{
    /// <summary>
    /// Interface for entities that can be locked onto by the targeting system.
    /// </summary>
    public interface ILockOnTarget
    {
        /// <summary>
        /// World position of the lock-on point (usually chest/center mass).
        /// </summary>
        Vector3 LockOnPoint { get; }

        /// <summary>
        /// Whether this target can currently be locked onto.
        /// False if dead, invisible, or otherwise untargetable.
        /// </summary>
        bool CanBeLocked { get; }

        /// <summary>
        /// Priority for target selection when multiple targets available.
        /// Higher value = more likely to be selected.
        /// </summary>
        int LockOnPriority { get; }

        /// <summary>
        /// Transform of this target for distance/angle calculations.
        /// </summary>
        Transform TargetTransform { get; }

        /// <summary>
        /// Called when this target becomes the active lock-on target.
        /// </summary>
        void OnLockedOn();

        /// <summary>
        /// Called when lock is released from this target.
        /// </summary>
        void OnLockReleased();

        /// <summary>
        /// Event fired when target validity changes.
        /// </summary>
        event Action<bool> OnTargetValidityChanged;
    }
}
