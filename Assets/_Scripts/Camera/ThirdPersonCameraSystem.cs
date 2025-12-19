using _Scripts.Combat.Interfaces;
using _Scripts.Player;
using _Scripts.Utilities;
using Systems;
using UnityEngine;

namespace _Scripts.Camera
{
    /// <summary>
    /// Souls-like third-person camera system with soft lock-on support.
    /// Handles camera orbit, collision avoidance, and lock-on tracking.
    /// Inspired by Lies of P camera behavior.
    /// </summary>
    public class ThirdPersonCameraSystem : Singleton<ThirdPersonCameraSystem>
    {
        #region Serialized Fields

        [Header("Target References")]
        [SerializeField]
        [Tooltip("Transform to follow (player). Auto-finds PlayerController if null.")]
        private Transform followTarget;

        [SerializeField]
        [Tooltip("Pivot point offset from follow target (shoulder height)")]
        private Vector3 followOffset = new Vector3(0f, 1.5f, 0f);

        [Header("Orbit Settings")]
        [SerializeField]
        [Tooltip("Default distance from pivot point")]
        [Range(1f, 15f)]
        private float defaultDistance = 2.5f;

        [SerializeField]
        [Tooltip("Minimum zoom distance (when colliding)")]
        [Range(0.5f, 5f)]
        private float minDistance = 1f;

        [SerializeField]
        [Tooltip("Maximum orbit distance")]
        [Range(5f, 20f)]
        private float maxDistance = 8f;

        [Header("Rotation Sensitivity")]
        [SerializeField]
        [Tooltip("Horizontal rotation sensitivity")]
        [Range(0.1f, 10f)]
        private float horizontalSensitivity = 2f;

        [SerializeField]
        [Tooltip("Vertical rotation sensitivity")]
        [Range(0.1f, 10f)]
        private float verticalSensitivity = 1.5f;

        [SerializeField] [Tooltip("Invert Y-axis for vertical look")]
        private bool invertY;

        [Header("Vertical Limits")]
        [SerializeField]
        [Tooltip("Minimum pitch angle (looking up)")]
        [Range(-89f, 0f)]
        private float minPitchAngle = -30f;

        [SerializeField]
        [Tooltip("Maximum pitch angle (looking down)")]
        [Range(0f, 89f)]
        private float maxPitchAngle = 70f;

        [Header("Smoothing")]
        [SerializeField]
        [Tooltip("Position follow smoothness (lower = smoother)")]
        [Range(0.01f, 1f)]
        private float positionSmoothTime = 0.1f;

        [SerializeField]
        [Tooltip("Rotation smoothness factor")]
        [Range(1f, 30f)]
        private float rotationSmoothSpeed = 15f;

        [SerializeField]
        [Tooltip("Zoom recovery speed when obstacle clears")]
        [Range(1f, 20f)]
        private float zoomRecoverySpeed = 5f;

        [Header("Collision Detection")]
        [SerializeField]
        [Tooltip("Spherecast radius for collision detection")]
        [Range(0.1f, 0.5f)]
        private float collisionRadius = 0.25f;

        [SerializeField]
        [Tooltip("Layer mask for camera collision (Environment, Geometry)")]
        private LayerMask collisionMask;

        [SerializeField]
        [Tooltip("Extra padding from obstacles")]
        [Range(0.01f, 0.5f)]
        private float collisionPadding = 0.1f;

        [Header("Lock-On Settings")]
        [SerializeField]
        [Tooltip("How much camera centers on target (0 = player only, 1 = target only)")]
        [Range(0f, 1f)]
        private float lockOnTargetWeight = 0.35f;

        [SerializeField]
        [Tooltip("Maximum manual rotation offset during lock-on (degrees)")]
        [Range(0f, 90f)]
        private float lockOnMaxRotationOffset = 45f;

        [SerializeField]
        [Tooltip("Speed to recenter camera during lock-on")]
        [Range(1f, 10f)]
        private float lockOnRecenterSpeed = 3f;

        [SerializeField]
        [Tooltip("Rotation sensitivity multiplier during lock-on")]
        [Range(0.1f, 1f)]
        private float lockOnRotationMultiplier = 0.3f;

        [SerializeField]
        [Tooltip("Fixed pitch angle during lock-on (lower = more horizontal)")]
        [Range(5f, 45f)]
        private float lockOnPitchAngle = 15f;

        [SerializeField]
        [Tooltip("Distance multiplier during lock-on (1 = same, >1 = further)")]
        [Range(0.8f, 2f)]
        private float lockOnDistanceMultiplier = 1.2f;

        [SerializeField]
        [Tooltip("Height offset during lock-on (raises camera)")]
        [Range(0f, 2f)]
        private float lockOnHeightOffset = 0.5f;

        [Header("Debug")] [SerializeField] private bool debugMode;

        #endregion

        #region Private Fields

        // Camera state
        private float _currentYaw;
        private float _currentPitch;
        private float _targetDistance;
        private float _currentDistance;
        private Vector3 _pivotPosition;
        private Vector3 _positionVelocity;

        // Lock-on state
        private bool _isLockedOn;
        private Transform _lockOnTarget;
        private Vector3 _lockOnPoint;
        private float _lockOnRotationOffset;
        private bool _subscribedToLockOn;
        private float _currentHeightOffset;

        // References
        private UnityEngine.Camera _camera;
        private InputHandler _inputHandler;
        private LockOnSystem _lockOnSystem;

        #endregion

        #region Properties

        /// <summary>
        /// Whether camera is currently in lock-on mode.
        /// </summary>
        public bool IsLockedOn => _isLockedOn;

        /// <summary>
        /// Current camera distance from pivot.
        /// </summary>
        public float CurrentDistance => _currentDistance;

        /// <summary>
        /// The main camera component.
        /// </summary>
        public UnityEngine.Camera Camera => _camera;

        #endregion

        #region Unity Lifecycle

        protected override void Awake()
        {
            base.Awake();
            _camera = GetComponentInChildren<UnityEngine.Camera>();
            CacheInitialRotation();
        }

        private void Start()
        {
            AcquireReferences();
            SubscribeToLockOnEvents();
            InitializeState();
        }

        private void OnDestroy()
        {
            UnsubscribeFromLockOnEvents();
        }

        private void LateUpdate()
        {
            if (followTarget is null) return;

            // Fallback: sync with LockOnSystem directly if events aren't working
            SyncWithLockOnSystem();

            ProcessInput();
            UpdatePivotPosition();

            if (_isLockedOn && _lockOnTarget is not null)
            {
                UpdateLockOnCamera();
            }
            else
            {
                UpdateFreeCamera();
            }

            HandleCollision();
            ApplyCameraTransform();
        }

        /// <summary>
        /// Sync camera lock-on state directly with LockOnSystem.
        /// Fallback in case event subscription failed or for robustness.
        /// </summary>
        private void SyncWithLockOnSystem()
        {
            // Try to subscribe if we haven't yet
            if (!_subscribedToLockOn && _lockOnSystem == null)
            {
                SubscribeToLockOnEvents();
            }

            // Direct sync with LockOnSystem state
            if (_lockOnSystem != null)
            {
                bool systemLockedOn = _lockOnSystem.IsLockedOn;
                var systemTarget = _lockOnSystem.CurrentTarget;

                // Sync lock-on state
                if (systemLockedOn && !_isLockedOn)
                {
                    // LockOnSystem is locked but camera isn't - sync up
                    _isLockedOn = true;
                    _lockOnTarget = systemTarget?.TargetTransform;
                    _lockOnPoint = systemTarget?.LockOnPoint ?? Vector3.zero;
                    _lockOnRotationOffset = 0f;

                    if (debugMode)
                        Debug.Log("[ThirdPersonCameraSystem] Synced lock-on from LockOnSystem");
                }
                else if (!systemLockedOn && _isLockedOn)
                {
                    // LockOnSystem released but camera still locked - release
                    _isLockedOn = false;
                    _lockOnTarget = null;
                    _lockOnRotationOffset = 0f;

                    if (debugMode)
                        Debug.Log("[ThirdPersonCameraSystem] Synced lock release from LockOnSystem");
                }
                else if (systemLockedOn && _isLockedOn)
                {
                    // Both locked - update target point
                    _lockOnPoint = _lockOnSystem.TargetLockOnPoint;

                    // Check if target changed
                    if (systemTarget?.TargetTransform != _lockOnTarget)
                    {
                        _lockOnTarget = systemTarget?.TargetTransform;
                    }
                }
            }
        }

        #endregion

        #region Initialization

        /// <summary>
        /// Cache initial rotation from current transform.
        /// </summary>
        private void CacheInitialRotation()
        {
            Vector3 euler = transform.eulerAngles;
            _currentYaw = euler.y;
            _currentPitch = euler.x;

            // Normalize pitch to -180 to 180 range
            if (_currentPitch > 180f)
                _currentPitch -= 360f;
        }

        /// <summary>
        /// Acquire references to other systems.
        /// </summary>
        private void AcquireReferences()
        {
            _inputHandler = InputHandler.Instance;
            if (_inputHandler is null)
            {
                Debug.LogWarning("[ThirdPersonCameraSystem] InputHandler not found");
            }

            if (followTarget is null)
            {
                var player = FindFirstObjectByType<PlayerController>();
                if (player is not null)
                {
                    followTarget = player.transform;
                    if (debugMode)
                        Debug.Log($"[ThirdPersonCameraSystem] Auto-found player: {player.name}");
                }
            }

            if (followTarget == null)
            {
                Debug.LogError("[ThirdPersonCameraSystem] No follow target assigned or found!");
            }
        }

        /// <summary>
        /// Initialize camera state.
        /// </summary>
        private void InitializeState()
        {
            _targetDistance = defaultDistance;
            _currentDistance = defaultDistance;

            if (followTarget != null)
            {
                _pivotPosition = GetPivotPosition();
            }
        }

        #endregion

        #region Input Processing

        /// <summary>
        /// Process look input and apply to rotation values.
        /// </summary>
        private void ProcessInput()
        {
            if (_inputHandler is null) return;

            Vector2 lookInput = _inputHandler.GetLookInput();

            // Mouse input is already per-frame, don't multiply by deltaTime
            // Only apply sensitivity multiplier
            float yawDelta = lookInput.x * horizontalSensitivity;
            float pitchDelta = lookInput.y * verticalSensitivity;

            // Handle Y inversion
            if (!invertY)
                pitchDelta = -pitchDelta;

            if (_isLockedOn)
            {
                // During lock-on, apply reduced rotation as offset
                yawDelta *= lockOnRotationMultiplier;
                pitchDelta *= lockOnRotationMultiplier;

                _lockOnRotationOffset = Mathf.Clamp(
                    _lockOnRotationOffset + yawDelta,
                    -lockOnMaxRotationOffset,
                    lockOnMaxRotationOffset
                );
            }
            else
            {
                // Free camera - full rotation control (immediate response)
                _currentYaw += yawDelta;
            }
            _currentPitch = Mathf.Clamp(_currentPitch + pitchDelta, minPitchAngle, maxPitchAngle);
        }

        #endregion

        #region Free Camera Mode

        /// <summary>
        /// Update camera in free orbit mode (no lock-on).
        /// </summary>
        private void UpdateFreeCamera()
        {
            _targetDistance = defaultDistance;
        }

        #endregion

        #region Lock-On Camera Mode

        /// <summary>
        /// Update camera in soft lock-on mode (Lies of P style).
        /// Centers between player and target while allowing manual adjustment.
        /// </summary>
        private void UpdateLockOnCamera()
        {
            if (_lockOnTarget == null)
            {
                _isLockedOn = false;
                return;
            }

            // Update lock-on point from system
            UpdateLockOnPoint();

            // Calculate direction from player to target
            Vector3 playerPivot = GetPivotPosition();
            Vector3 toTarget = _lockOnPoint - playerPivot;
            toTarget.y = 0f;

            if (toTarget.sqrMagnitude < 0.01f)
                return;

            // Base yaw from player-to-target direction (camera behind player facing target)
            float baseYaw = Mathf.Atan2(toTarget.x, toTarget.z) * Mathf.Rad2Deg;

            // Decay manual offset back to center
            _lockOnRotationOffset = Mathf.Lerp(
                _lockOnRotationOffset,
                0f,
                lockOnRecenterSpeed * Time.deltaTime
            );

            float targetYaw = baseYaw + _lockOnRotationOffset;

            // Smooth yaw transition
            _currentYaw = Mathf.LerpAngle(_currentYaw, targetYaw, 8f * Time.deltaTime);

            // Use fixed pitch for lock-on (more horizontal, combat-focused view)
            float targetPitch = lockOnPitchAngle;
            _currentPitch = Mathf.Lerp(_currentPitch, targetPitch, 6f * Time.deltaTime);

            // Increase distance during lock-on to see both combatants
            _targetDistance = defaultDistance * lockOnDistanceMultiplier;
        }

        /// <summary>
        /// Update lock-on point from LockOnSystem.
        /// </summary>
        private void UpdateLockOnPoint()
        {
            if (_lockOnSystem != null && _lockOnSystem.IsLockedOn)
            {
                _lockOnPoint = _lockOnSystem.TargetLockOnPoint;
            }
        }

        #endregion

        #region Collision Detection

        /// <summary>
        /// Handle camera collision using spherecast from pivot to desired position.
        /// Zooms camera in when obstructed to avoid clipping.
        /// </summary>
        private void HandleCollision()
        {
            Vector3 pivotPos = _pivotPosition;
            Vector3 direction = GetCameraDirection();
            float desiredDistance = _targetDistance;

            // Spherecast from pivot towards desired camera position
            if (Physics.SphereCast(
                    pivotPos,
                    collisionRadius,
                    direction,
                    out RaycastHit hit,
                    desiredDistance,
                    collisionMask,
                    QueryTriggerInteraction.Ignore))
            {
                // Hit something - zoom in to avoid collision
                float adjustedDistance = hit.distance - collisionPadding;
                _currentDistance = Mathf.Max(minDistance, adjustedDistance);
            }
            else
            {
                // No obstruction - smoothly recover to target distance
                _currentDistance = Mathf.Lerp(
                    _currentDistance,
                    _targetDistance,
                    zoomRecoverySpeed * Time.deltaTime
                );
            }

            // Clamp to valid range
            _currentDistance = Mathf.Clamp(_currentDistance, minDistance, maxDistance);
        }

        #endregion

        #region Position Calculation

        /// <summary>
        /// Update pivot position with smooth follow.
        /// </summary>
        private void UpdatePivotPosition()
        {
            // Smoothly interpolate height offset (prevents snap when lock-on ends)
            float targetHeightOffset = _isLockedOn ? lockOnHeightOffset : 0f;
            _currentHeightOffset = Mathf.Lerp(_currentHeightOffset, targetHeightOffset, 5f * Time.deltaTime);

            // Get base pivot and apply smoothed height offset
            Vector3 targetPivot = GetPivotPosition();
            targetPivot.y += _currentHeightOffset;

            _pivotPosition = Vector3.SmoothDamp(
                _pivotPosition,
                targetPivot,
                ref _positionVelocity,
                positionSmoothTime
            );
        }

        /// <summary>
        /// Get the pivot point the camera orbits around.
        /// </summary>
        private Vector3 GetPivotPosition()
        {
            if (followTarget is null) return transform.position;
            return followTarget.position + followOffset;
        }

        /// <summary>
        /// Get camera direction vector from current rotation.
        /// </summary>
        private Vector3 GetCameraDirection()
        {
            Quaternion rotation = Quaternion.Euler(_currentPitch, _currentYaw, 0f);
            return rotation * Vector3.back;
        }

        /// <summary>
        /// Get the desired camera position based on current rotation and target distance.
        /// </summary>
        private Vector3 GetDesiredCameraPosition()
        {
            return _pivotPosition + GetCameraDirection() * _targetDistance;
        }

        /// <summary>
        /// Apply final camera position and rotation.
        /// </summary>
        private void ApplyCameraTransform()
        {
            // Calculate final position using current (collision-adjusted) distance
            Vector3 finalPosition = _pivotPosition + GetCameraDirection() * _currentDistance;
            transform.position = finalPosition;

            // Apply rotation directly from yaw/pitch for immediate response
            if (_isLockedOn && _lockOnTarget != null)
            {
                // Look at weighted point between player and target
                Vector3 lookTarget = Vector3.Lerp(_pivotPosition, _lockOnPoint, lockOnTargetWeight);
                Vector3 lookDirection = lookTarget - transform.position;

                if (lookDirection.sqrMagnitude > 0.001f)
                {
                    Quaternion targetRotation = Quaternion.LookRotation(lookDirection);
                    // Smooth only during lock-on for cinematic feel
                    transform.rotation = Quaternion.Slerp(
                        transform.rotation,
                        targetRotation,
                        rotationSmoothSpeed * Time.deltaTime
                    );
                }
            }
            else
            {
                // Free camera - apply rotation directly (no smoothing = responsive)
                transform.rotation = Quaternion.Euler(_currentPitch, _currentYaw, 0f);
            }
        }

        #endregion

        #region Lock-On System Integration

        /// <summary>
        /// Subscribe to LockOnSystem events for target tracking.
        /// </summary>
        private void SubscribeToLockOnEvents()
        {
            if (_subscribedToLockOn) return;

            _lockOnSystem = LockOnSystem.Instance;
            if (_lockOnSystem == null)
            {
                if (debugMode)
                    Debug.LogWarning("[ThirdPersonCameraSystem] LockOnSystem not found - will retry via sync");
                return;
            }

            _lockOnSystem.OnTargetAcquired += HandleTargetAcquired;
            _lockOnSystem.OnTargetLost += HandleTargetLost;
            _lockOnSystem.OnTargetSwitched += HandleTargetSwitched;
            _subscribedToLockOn = true;

            if (debugMode)
                Debug.Log("[ThirdPersonCameraSystem] Subscribed to LockOnSystem events");
        }

        /// <summary>
        /// Unsubscribe from LockOnSystem events.
        /// </summary>
        private void UnsubscribeFromLockOnEvents()
        {
            if (_lockOnSystem == null) return;

            _lockOnSystem.OnTargetAcquired -= HandleTargetAcquired;
            _lockOnSystem.OnTargetLost -= HandleTargetLost;
            _lockOnSystem.OnTargetSwitched -= HandleTargetSwitched;
        }

        /// <summary>
        /// Called when a new target is acquired.
        /// </summary>
        private void HandleTargetAcquired(ILockOnTarget target)
        {
            _isLockedOn = true;
            _lockOnTarget = target.TargetTransform;
            _lockOnPoint = target.LockOnPoint;
            _lockOnRotationOffset = 0f;

            if (debugMode)
                Debug.Log($"[ThirdPersonCameraSystem] Target acquired: {_lockOnTarget.name}");
        }

        /// <summary>
        /// Called when lock is released.
        /// </summary>
        private void HandleTargetLost(ILockOnTarget target)
        {
            _isLockedOn = false;
            _lockOnTarget = null;
            _lockOnRotationOffset = 0f;

            if (debugMode)
                Debug.Log("[ThirdPersonCameraSystem] Target lost");
        }

        /// <summary>
        /// Called when switching to a different target.
        /// </summary>
        private void HandleTargetSwitched(ILockOnTarget oldTarget, ILockOnTarget newTarget)
        {
            _lockOnTarget = newTarget.TargetTransform;
            _lockOnPoint = newTarget.LockOnPoint;

            if (debugMode)
                Debug.Log($"[ThirdPersonCameraSystem] Target switched to: {_lockOnTarget.name}");
        }

        #endregion

        #region Public API

        /// <summary>
        /// Force camera to look at a specific world position.
        /// </summary>
        public void LookAt(Vector3 worldPosition)
        {
            Vector3 direction = worldPosition - _pivotPosition;
            direction.y = 0;

            if (direction.sqrMagnitude > 0.01f)
            {
                _currentYaw = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
            }
        }

        /// <summary>
        /// Reset camera to behind the player.
        /// </summary>
        public void ResetBehindPlayer()
        {
            if (followTarget == null) return;

            _currentYaw = followTarget.eulerAngles.y;
            _currentPitch = 10f;
            _lockOnRotationOffset = 0f;
        }

        /// <summary>
        /// Set follow target at runtime.
        /// </summary>
        public void SetFollowTarget(Transform target)
        {
            followTarget = target;
            if (target != null)
            {
                _pivotPosition = GetPivotPosition();
            }
        }

        #endregion

        #region Debug

        private void OnDrawGizmos()
        {
            if (!debugMode) return;

            Vector3 pivot = followTarget != null
                ? followTarget.position + followOffset
                : transform.position;

            // Draw pivot point
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(pivot, 0.2f);

            // Draw collision sphere at camera position
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, collisionRadius);

            // Draw line from pivot to camera
            Gizmos.color = Color.white;
            Gizmos.DrawLine(pivot, transform.position);

            // Draw target distance sphere
            Gizmos.color = new Color(1f, 1f, 1f, 0.2f);
            Gizmos.DrawWireSphere(pivot, _targetDistance);

            // Draw lock-on visualization
            if (_isLockedOn && _lockOnTarget != null)
            {
                // Lock-on point
                Gizmos.color = Color.red;
                Gizmos.DrawWireSphere(_lockOnPoint, 0.3f);

                // Weighted look point
                Vector3 weightedPoint = Vector3.Lerp(pivot, _lockOnPoint, lockOnTargetWeight);
                Gizmos.color = Color.magenta;
                Gizmos.DrawWireSphere(weightedPoint, 0.15f);

                // Line to weighted point
                Gizmos.DrawLine(transform.position, weightedPoint);

                // Line from player to target
                Gizmos.color = Color.red;
                Gizmos.DrawLine(pivot, _lockOnPoint);
            }
        }

        #endregion
    }
}
