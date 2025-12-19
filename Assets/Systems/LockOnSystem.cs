using System;
using System.Collections.Generic;
using _Scripts.Combat.Interfaces;
using _Scripts.Player;
using _Scripts.Utilities;
using UnityEngine;

namespace Systems
{
    /// <summary>
    /// Lock-on targeting system for souls-like combat.
    /// Handles target acquisition, switching, and camera focus.
    /// </summary>
    public class LockOnSystem : Singleton<LockOnSystem>
    {
        [Header("Lock-On Settings")]
        [SerializeField]
        [Tooltip("Maximum lock-on distance")]
        private float _maxLockDistance = 20f;

        [SerializeField]
        [Tooltip("Field of view for target acquisition (degrees)")]
        private float _lockOnFOV = 60f;

        [SerializeField]
        [Tooltip("Layer mask for valid targets")]
        private LayerMask _targetLayerMask;

        [SerializeField]
        [Tooltip("Layer mask for line of sight check")]
        private LayerMask _obstacleMask;

        [Header("Target Switching")]
        [SerializeField]
        [Tooltip("Deadzone for stick input when switching targets")]
        private float _switchDeadzone = 0.5f;

        [SerializeField]
        [Tooltip("Cooldown between target switches")]
        private float _switchCooldown = 0.3f;

        [Header("Camera")]
        [SerializeField]
        [Tooltip("Camera transform (auto-find if null)")]
        private Transform _cameraTransform;

        [SerializeField]
        [Tooltip("Player transform (auto-find if null)")]
        private Transform _playerTransform;

        [Header("UI")]
        [SerializeField]
        [Tooltip("Lock-on indicator prefab")]
        private GameObject _lockOnIndicatorPrefab;

        [Header("Debug")]
        [SerializeField]
        private bool _debugMode = false;

        // State
        private ILockOnTarget _currentTarget;
        private readonly List<ILockOnTarget> _potentialTargets = new List<ILockOnTarget>();
        private float _lastSwitchTime;
        private GameObject _indicatorInstance;

        // Events
        public event Action<ILockOnTarget> OnTargetAcquired;
        public event Action<ILockOnTarget> OnTargetLost;
        public event Action<ILockOnTarget, ILockOnTarget> OnTargetSwitched;

        /// <summary>
        /// Currently locked target.
        /// </summary>
        public ILockOnTarget CurrentTarget => _currentTarget;

        /// <summary>
        /// Whether currently locked onto a target.
        /// </summary>
        public bool IsLockedOn => _currentTarget != null;

        /// <summary>
        /// Position of the lock-on point on current target.
        /// </summary>
        public Vector3 TargetLockOnPoint => _currentTarget?.LockOnPoint ?? Vector3.zero;

        protected override void Awake()
        {
            base.Awake();

            // Auto-find references
            if (_cameraTransform == null)
            {
                _cameraTransform = UnityEngine.Camera.main?.transform;
            }

            if (_playerTransform == null)
            {
                var player = FindFirstObjectByType<PlayerController>();
                _playerTransform = player?.transform;
            }
        }

        private void Update()
        {
            if (!IsLockedOn) return;

            // Validate current target
            if (!ValidateTarget(_currentTarget))
            {
                TrySwitchToNextTarget();
            }

            // Update indicator position
            UpdateIndicator();
        }

        #region Lock-On Control

        /// <summary>
        /// Toggle lock-on state.
        /// </summary>
        public void ToggleLockOn()
        {
            if (IsLockedOn)
            {
                ReleaseLock();
            }
            else
            {
                AcquireTarget();
            }
        }

        /// <summary>
        /// Attempt to acquire a target.
        /// </summary>
        public bool AcquireTarget()
        {
            if (_playerTransform == null || _cameraTransform == null)
                return false;

            UpdatePotentialTargets();

            if (_potentialTargets.Count == 0)
                return false;

            // Find best target (closest to screen center)
            ILockOnTarget bestTarget = FindBestTarget();

            if (bestTarget != null)
            {
                SetTarget(bestTarget);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Release current lock.
        /// </summary>
        public void ReleaseLock()
        {
            if (_currentTarget == null) return;

            var previousTarget = _currentTarget;
            _currentTarget.OnLockReleased();
            _currentTarget = null;

            HideIndicator();
            OnTargetLost?.Invoke(previousTarget);

            if (_debugMode)
                Debug.Log("[LockOnSystem] Lock released");
        }

        /// <summary>
        /// Switch to a different target.
        /// </summary>
        /// <param name="direction">Screen-space direction to search (left/right)</param>
        public void SwitchTarget(Vector2 direction)
        {
            if (!IsLockedOn) return;
            if (Time.time - _lastSwitchTime < _switchCooldown) return;
            if (direction.magnitude < _switchDeadzone) return;

            UpdatePotentialTargets();

            if (_potentialTargets.Count <= 1) return;

            ILockOnTarget newTarget = FindTargetInDirection(direction);

            if (newTarget != null && newTarget != _currentTarget)
            {
                var previousTarget = _currentTarget;
                SetTarget(newTarget);
                _lastSwitchTime = Time.time;

                OnTargetSwitched?.Invoke(previousTarget, newTarget);
            }
        }

        /// <summary>
        /// Force lock onto a specific target.
        /// </summary>
        public void ForceLockOn(ILockOnTarget target)
        {
            if (target == null || !target.CanBeLocked) return;

            SetTarget(target);
        }

        #endregion

        #region Target Finding

        private void UpdatePotentialTargets()
        {
            _potentialTargets.Clear();

            if (_playerTransform == null) return;

            // Find all lockable targets in range
            var colliders = Physics.OverlapSphere(_playerTransform.position, _maxLockDistance, _targetLayerMask);

            foreach (var col in colliders)
            {
                var target = col.GetComponentInParent<ILockOnTarget>();
                if (target == null) continue;
                if (!target.CanBeLocked) continue;
                if (_potentialTargets.Contains(target)) continue;

                // Check distance
                float distance = Vector3.Distance(_playerTransform.position, target.LockOnPoint);
                if (distance > _maxLockDistance) continue;

                // Check FOV
                if (!IsInFieldOfView(target)) continue;

                // Check line of sight
                if (!HasLineOfSight(target)) continue;

                _potentialTargets.Add(target);
            }

            // Sort by priority (higher priority first) then by distance
            _potentialTargets.Sort((a, b) =>
            {
                int priorityCompare = b.LockOnPriority.CompareTo(a.LockOnPriority);
                if (priorityCompare != 0) return priorityCompare;

                float distA = Vector3.Distance(_playerTransform.position, a.LockOnPoint);
                float distB = Vector3.Distance(_playerTransform.position, b.LockOnPoint);
                return distA.CompareTo(distB);
            });
        }

        private bool IsInFieldOfView(ILockOnTarget target)
        {
            if (_cameraTransform == null) return false;

            Vector3 directionToTarget = (target.LockOnPoint - _cameraTransform.position).normalized;
            float angle = Vector3.Angle(_cameraTransform.forward, directionToTarget);

            return angle <= _lockOnFOV * 0.5f;
        }

        private bool HasLineOfSight(ILockOnTarget target)
        {
            if (_playerTransform == null) return false;

            Vector3 origin = _playerTransform.position + Vector3.up;
            Vector3 targetPoint = target.LockOnPoint;
            Vector3 direction = targetPoint - origin;

            if (Physics.Raycast(origin, direction.normalized, out RaycastHit hit, direction.magnitude, _obstacleMask))
            {
                // Check if we hit the target itself
                var hitTarget = hit.collider.GetComponentInParent<ILockOnTarget>();
                return hitTarget == target;
            }

            return true; // No obstruction
        }

        private ILockOnTarget FindBestTarget()
        {
            if (_potentialTargets.Count == 0) return null;
            if (_cameraTransform == null) return _potentialTargets[0];

            // Find target closest to screen center
            ILockOnTarget best = null;
            float bestScore = float.MaxValue;

            UnityEngine.Camera cam = UnityEngine.Camera.main;
            if (cam == null) return _potentialTargets[0];

            Vector2 screenCenter = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);

            foreach (var target in _potentialTargets)
            {
                Vector3 screenPos = cam.WorldToScreenPoint(target.LockOnPoint);

                // Skip targets behind camera
                if (screenPos.z < 0) continue;

                float distanceToCenter = Vector2.Distance(new Vector2(screenPos.x, screenPos.y), screenCenter);

                // Factor in priority
                float score = distanceToCenter - (target.LockOnPriority * 100f);

                if (score < bestScore)
                {
                    bestScore = score;
                    best = target;
                }
            }

            return best ?? _potentialTargets[0];
        }

        private ILockOnTarget FindTargetInDirection(Vector2 direction)
        {
            if (_currentTarget == null) return null;

            UnityEngine.Camera cam = UnityEngine.Camera.main;
            if (cam == null) return null;

            Vector2 currentScreenPos = cam.WorldToScreenPoint(_currentTarget.LockOnPoint);

            ILockOnTarget best = null;
            float bestScore = float.MaxValue;

            foreach (var target in _potentialTargets)
            {
                if (target == _currentTarget) continue;

                Vector3 screenPos3D = cam.WorldToScreenPoint(target.LockOnPoint);
                if (screenPos3D.z < 0) continue;

                Vector2 screenPos = new Vector2(screenPos3D.x, screenPos3D.y);
                Vector2 toTarget = screenPos - currentScreenPos;

                // Check if target is in the desired direction
                float dot = Vector2.Dot(direction.normalized, toTarget.normalized);
                if (dot < 0.3f) continue; // Must be somewhat in the direction

                // Score based on alignment and distance
                float distance = toTarget.magnitude;
                float score = distance / Mathf.Max(dot, 0.1f);

                if (score < bestScore)
                {
                    bestScore = score;
                    best = target;
                }
            }

            return best;
        }

        private bool ValidateTarget(ILockOnTarget target)
        {
            if (target == null) return false;
            if (!target.CanBeLocked) return false;

            float distance = Vector3.Distance(_playerTransform.position, target.LockOnPoint);
            if (distance > _maxLockDistance * 1.2f) return false; // Slight tolerance

            return true;
        }

        private void TrySwitchToNextTarget()
        {
            UpdatePotentialTargets();

            if (_potentialTargets.Count > 0)
            {
                var previousTarget = _currentTarget;
                SetTarget(_potentialTargets[0]);
                OnTargetSwitched?.Invoke(previousTarget, _currentTarget);
            }
            else
            {
                ReleaseLock();
            }
        }

        #endregion

        #region Internal

        private void SetTarget(ILockOnTarget target)
        {
            var previousTarget = _currentTarget;

            if (previousTarget != null)
            {
                previousTarget.OnLockReleased();
            }

            _currentTarget = target;
            _currentTarget.OnLockedOn();

            ShowIndicator();

            if (previousTarget == null)
            {
                OnTargetAcquired?.Invoke(_currentTarget);
            }

            if (_debugMode)
                Debug.Log($"[LockOnSystem] Locked on to: {_currentTarget.TargetTransform?.name}");
        }

        #endregion

        #region UI Indicator

        private void ShowIndicator()
        {
            if (_lockOnIndicatorPrefab == null) return;

            if (_indicatorInstance == null)
            {
                _indicatorInstance = Instantiate(_lockOnIndicatorPrefab);
            }

            _indicatorInstance.SetActive(true);
            UpdateIndicator();
        }

        private void HideIndicator()
        {
            if (_indicatorInstance != null)
            {
                _indicatorInstance.SetActive(false);
            }
        }

        private void UpdateIndicator()
        {
            if (_indicatorInstance == null || _currentTarget == null) return;

            _indicatorInstance.transform.position = _currentTarget.LockOnPoint;

            // Optional: Make indicator face camera
            if (_cameraTransform != null)
            {
                _indicatorInstance.transform.LookAt(_cameraTransform);
            }
        }

        #endregion

        #region Public Helpers

        /// <summary>
        /// Get direction from player to current target.
        /// </summary>
        public Vector3 GetDirectionToTarget()
        {
            if (!IsLockedOn || _playerTransform == null) return Vector3.forward;

            Vector3 direction = (_currentTarget.LockOnPoint - _playerTransform.position);
            direction.y = 0;
            return direction.normalized;
        }

        /// <summary>
        /// Get distance to current target.
        /// </summary>
        public float GetDistanceToTarget()
        {
            if (!IsLockedOn || _playerTransform == null) return 0f;

            return Vector3.Distance(_playerTransform.position, _currentTarget.LockOnPoint);
        }

        /// <summary>
        /// Check if a specific target is the current lock target.
        /// </summary>
        public bool IsTargetLocked(ILockOnTarget target)
        {
            return _currentTarget == target;
        }

        #endregion

        #region Debug

        private void OnDrawGizmos()
        {
            if (!_debugMode) return;

            if (_playerTransform != null)
            {
                // Draw lock range
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireSphere(_playerTransform.position, _maxLockDistance);
            }

            if (IsLockedOn && _currentTarget != null)
            {
                // Draw line to target
                Gizmos.color = Color.red;
                Gizmos.DrawLine(_playerTransform?.position ?? Vector3.zero, _currentTarget.LockOnPoint);

                // Draw lock point
                Gizmos.DrawWireSphere(_currentTarget.LockOnPoint, 0.3f);
            }
        }

        #endregion
    }
}
