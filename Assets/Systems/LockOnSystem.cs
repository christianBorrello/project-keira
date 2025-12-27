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
        private float maxLockDistance = 20f;

        [SerializeField]
        [Tooltip("Field of view for target acquisition (degrees)")]
        private float lockOnFOV = 60f;

        [SerializeField]
        [Tooltip("Layer mask for valid targets")]
        private LayerMask targetLayerMask;

        [SerializeField]
        [Tooltip("Layer mask for line of sight check")]
        private LayerMask obstacleMask;

        [Header("Target Switching")]
        [SerializeField]
        [Tooltip("Deadzone for stick input when switching targets")]
        private float switchDeadzone = 0.5f;

        [SerializeField]
        [Tooltip("Cooldown between target switches")]
        private float switchCooldown = 0.3f;

        [Header("Camera")]
        [SerializeField]
        [Tooltip("Camera transform (auto-find if null)")]
        private Transform cameraTransform;

        [SerializeField]
        [Tooltip("Player transform (auto-find if null)")]
        private Transform playerTransform;

        [Header("UI")]
        [SerializeField]
        [Tooltip("Lock-on indicator prefab")]
        private GameObject lockOnIndicatorPrefab;

        [Header("Debug")]
        [SerializeField]
        private bool debugMode;

        // State
        private ILockOnTarget _currentTarget;
        private readonly List<ILockOnTarget> _potentialTargets = new();
        private float _lastSwitchTime;
        private GameObject _indicatorInstance;

        // Performance: Pre-allocated buffers and caches
        private readonly Collider[] _colliderBuffer = new Collider[32];
        private readonly HashSet<ILockOnTarget> _targetSet = new();
        private readonly TargetComparer _targetComparer = new();
        private Camera _mainCamera;
        private float _maxLockDistanceSqr;
        private float _maxLockDistanceWithToleranceSqr;
        private float _halfFOV;
        private float _lastValidationTime;
        private const float ValidationInterval = 0.1f;

        /// <summary>
        /// Cached comparer to avoid lambda allocation in Sort().
        /// </summary>
        private class TargetComparer : IComparer<ILockOnTarget>
        {
            public Vector3 PlayerPosition;

            public int Compare(ILockOnTarget a, ILockOnTarget b)
            {
                int priorityCompare = b!.LockOnPriority.CompareTo(a!.LockOnPriority);
                if (priorityCompare != 0) return priorityCompare;

                float distSqrA = (PlayerPosition - a.LockOnPoint).sqrMagnitude;
                float distSqrB = (PlayerPosition - b.LockOnPoint).sqrMagnitude;
                return distSqrA.CompareTo(distSqrB);
            }
        }

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
            _mainCamera = Camera.main;
            if (cameraTransform == null)
            {
                cameraTransform = _mainCamera?.transform;
            }

            if (playerTransform == null)
            {
                var player = FindFirstObjectByType<PlayerController>();
                playerTransform = player?.transform;
            }

            // Cache computed values to avoid runtime calculations
            _maxLockDistanceSqr = maxLockDistance * maxLockDistance;
            _maxLockDistanceWithToleranceSqr = (maxLockDistance * 1.2f) * (maxLockDistance * 1.2f);
            _halfFOV = lockOnFOV * 0.5f;
        }

        private void Update()
        {
            if (!IsLockedOn) return;

            // Throttled validation: check target validity at fixed intervals, not every frame
            if (Time.time - _lastValidationTime >= ValidationInterval)
            {
                _lastValidationTime = Time.time;

                if (!ValidateTarget(_currentTarget))
                {
                    TrySwitchToNextTarget();
                }
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
            if (playerTransform == null || cameraTransform == null)
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

            if (debugMode)
                Debug.Log("[LockOnSystem] Lock released");
        }

        /// <summary>
        /// Switch to a different target.
        /// </summary>
        /// <param name="direction">Screen-space direction to search (left/right)</param>
        public void SwitchTarget(Vector2 direction)
        {
            if (!IsLockedOn) return;
            if (Time.time - _lastSwitchTime < switchCooldown) return;
            if (direction.magnitude < switchDeadzone) return;

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
            _targetSet.Clear();

            if (playerTransform is null) return;

            // Find all lockable targets in range using non-allocating physics query
            int count = Physics.OverlapSphereNonAlloc(
                playerTransform.position,
                maxLockDistance,
                _colliderBuffer,
                targetLayerMask
            );

            Vector3 playerPos = playerTransform.position;

            for (int i = 0; i < count; i++)
            {
                var target = _colliderBuffer[i].GetComponentInParent<ILockOnTarget>();
                if (target == null) continue;
                if (!target.CanBeLocked) continue;

                // O(1) duplicate check using HashSet instead of O(n) List.Contains
                if (!_targetSet.Add(target)) continue;

                // Use sqrMagnitude to avoid sqrt calculation
                float distSqr = (playerPos - target.LockOnPoint).sqrMagnitude;
                if (distSqr > _maxLockDistanceSqr) continue;

                // Check FOV
                if (!IsInFieldOfView(target)) continue;

                // Check line of sight
                if (!HasLineOfSight(target)) continue;

                _potentialTargets.Add(target);
            }

            // Sort using cached comparer to avoid lambda allocation
            _targetComparer.PlayerPosition = playerPos;
            _potentialTargets.Sort(_targetComparer);
        }

        private bool IsInFieldOfView(ILockOnTarget target)
        {
            if (cameraTransform is null) return false;

            Vector3 directionToTarget = (target.LockOnPoint - cameraTransform.position).normalized;
            float angle = Vector3.Angle(cameraTransform.forward, directionToTarget);

            return angle <= _halfFOV;
        }

        private bool HasLineOfSight(ILockOnTarget target)
        {
            if (playerTransform is null) return false;

            Vector3 origin = playerTransform.position + Vector3.up;
            Vector3 targetPoint = target.LockOnPoint;
            Vector3 direction = targetPoint - origin;

            if (Physics.Raycast(origin, direction.normalized, out RaycastHit hit, direction.magnitude, obstacleMask))
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
            if (_mainCamera == null) return _potentialTargets[0];

            // Find target closest to screen center
            ILockOnTarget best = null;
            float bestScore = float.MaxValue;

            float screenCenterX = Screen.width * 0.5f;
            float screenCenterY = Screen.height * 0.5f;

            foreach (var target in _potentialTargets)
            {
                Vector3 screenPos = _mainCamera.WorldToScreenPoint(target.LockOnPoint);

                // Skip targets behind camera
                if (screenPos.z < 0) continue;

                // Inline distance calculation to avoid Vector2 allocation
                float dx = screenPos.x - screenCenterX;
                float dy = screenPos.y - screenCenterY;
                float distanceToCenter = Mathf.Sqrt(dx * dx + dy * dy);

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
            if (_mainCamera == null) return null;

            Vector3 currentScreenPos3D = _mainCamera.WorldToScreenPoint(_currentTarget.LockOnPoint);
            Vector2 currentScreenPos = new Vector2(currentScreenPos3D.x, currentScreenPos3D.y);

            ILockOnTarget best = null;
            float bestScore = float.MaxValue;

            foreach (var target in _potentialTargets)
            {
                if (target == _currentTarget) continue;

                Vector3 screenPos3D = _mainCamera.WorldToScreenPoint(target.LockOnPoint);
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

            // Use sqrMagnitude to avoid sqrt calculation
            float distSqr = (playerTransform.position - target.LockOnPoint).sqrMagnitude;
            if (distSqr > _maxLockDistanceWithToleranceSqr) return false;

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

            if (debugMode)
                Debug.Log($"[LockOnSystem] Locked on to: {_currentTarget.TargetTransform?.name}");
        }

        #endregion

        #region UI Indicator

        private void ShowIndicator()
        {
            if (lockOnIndicatorPrefab == null) return;

            if (_indicatorInstance == null)
            {
                _indicatorInstance = Instantiate(lockOnIndicatorPrefab);
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
            if (cameraTransform != null)
            {
                _indicatorInstance.transform.LookAt(cameraTransform);
            }
        }

        #endregion

        #region Public Helpers

        /// <summary>
        /// Get direction from player to current target.
        /// </summary>
        public Vector3 GetDirectionToTarget()
        {
            if (!IsLockedOn || playerTransform == null) return Vector3.forward;

            Vector3 direction = (_currentTarget.LockOnPoint - playerTransform.position);
            direction.y = 0;
            return direction.normalized;
        }

        /// <summary>
        /// Get distance to current target.
        /// </summary>
        public float GetDistanceToTarget()
        {
            if (!IsLockedOn || playerTransform == null) return 0f;

            return Vector3.Distance(playerTransform.position, _currentTarget.LockOnPoint);
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
            if (!debugMode) return;

            if (playerTransform != null)
            {
                // Draw lock range
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireSphere(playerTransform.position, maxLockDistance);
            }

            if (IsLockedOn && _currentTarget != null)
            {
                // Draw line to target
                Gizmos.color = Color.red;
                Gizmos.DrawLine(playerTransform?.position ?? Vector3.zero, _currentTarget.LockOnPoint);

                // Draw lock point
                Gizmos.DrawWireSphere(_currentTarget.LockOnPoint, 0.3f);
            }
        }

        #endregion
    }
}
