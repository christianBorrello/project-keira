using System;
using _Scripts.Combat.Interfaces;
using Systems;
using UnityEngine;

namespace _Scripts.Player.Components
{
    /// <summary>
    /// Manages lock-on targeting state for the player.
    /// Handles LockOnSystem event subscriptions and distance tracking for orbital movement.
    /// </summary>
    [RequireComponent(typeof(PlayerController))]
    public class LockOnController : MonoBehaviour
    {
        // References
        private PlayerController _player;

        // Lock-on state
        private bool _isLockedOn;
        private ILockOnTarget _currentTarget;
        private float _lockedOnDistance;

        [Header("Debug")]
        [SerializeField]
        private bool debugMode;

        #region Properties

        /// <summary>
        /// Whether player is currently locked on to a target.
        /// </summary>
        public bool IsLockedOn => _isLockedOn;

        /// <summary>
        /// Current lock-on target (null if not locked on).
        /// </summary>
        public ILockOnTarget CurrentTarget => _currentTarget;

        /// <summary>
        /// Distance to target when lock-on was acquired (for orbital movement).
        /// </summary>
        public float LockedOnDistance => _lockedOnDistance;

        #endregion

        #region Events

        /// <summary>
        /// Fired when lock-on target is acquired.
        /// </summary>
        public event Action<ILockOnTarget> OnTargetAcquired;

        /// <summary>
        /// Fired when lock-on target is lost.
        /// </summary>
        public event Action<ILockOnTarget> OnTargetLost;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            _player = GetComponent<PlayerController>();
        }

        private void Start()
        {
            // Subscribe to LockOnSystem events
            if (LockOnSystem.Instance != null)
            {
                LockOnSystem.Instance.OnTargetAcquired += HandleTargetAcquired;
                LockOnSystem.Instance.OnTargetLost += HandleTargetLost;
            }
        }

        private void OnDestroy()
        {
            // Unsubscribe from LockOnSystem events
            if (LockOnSystem.Instance != null)
            {
                LockOnSystem.Instance.OnTargetAcquired -= HandleTargetAcquired;
                LockOnSystem.Instance.OnTargetLost -= HandleTargetLost;
            }
        }

        #endregion

        #region Lock-On Management

        /// <summary>
        /// Toggle lock-on state via LockOnSystem.
        /// </summary>
        public void ToggleLockOn()
        {
            LockOnSystem.Instance?.ToggleLockOn();
        }

        /// <summary>
        /// Updates the locked-on distance to current position.
        /// Call after any movement that bypasses normal movement (e.g., dodge/roll).
        /// </summary>
        public void UpdateLockedOnDistance()
        {
            if (_isLockedOn && _currentTarget != null)
            {
                _lockedOnDistance = Vector3.Distance(
                    _player.transform.position,
                    _currentTarget.LockOnPoint
                );
            }
        }

        /// <summary>
        /// Set the locked-on distance directly (for orbital movement calculations).
        /// </summary>
        public void SetLockedOnDistance(float distance)
        {
            _lockedOnDistance = distance;
        }

        private void HandleTargetAcquired(ILockOnTarget target)
        {
            _isLockedOn = true;
            _currentTarget = target;
            _lockedOnDistance = Vector3.Distance(
                _player.transform.position,
                target.LockOnPoint
            );

            if (debugMode)
                Debug.Log($"[LockOnController] Locked on to: {target.TargetTransform?.name}");

            OnTargetAcquired?.Invoke(target);
        }

        private void HandleTargetLost(ILockOnTarget target)
        {
            _isLockedOn = false;
            _currentTarget = null;

            if (debugMode)
                Debug.Log("[LockOnController] Lock-on target lost");

            OnTargetLost?.Invoke(target);
        }

        #endregion
    }
}
