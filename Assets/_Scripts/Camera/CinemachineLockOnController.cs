using _Scripts.Combat.Interfaces;
using _Scripts.Player;
using _Scripts.Utilities;
using Systems;
using Unity.Cinemachine;
using UnityEngine;

namespace _Scripts.Camera
{
    /// <summary>
    /// Controls Cinemachine cameras for lock-on functionality.
    /// Implements soft lock-on behavior similar to Lies of P:
    /// - Camera stays BEHIND the player (follows player)
    /// - Camera LOOKS AT a weighted point between player and enemy
    /// - Player can manually adjust rotation with decay back to center
    /// </summary>
    /// <remarks>
    /// <para><b>Unity Editor Setup Instructions:</b></para>
    /// <para>
    /// 1. <b>Create Lock-On Camera:</b>
    ///    - GameObject → Cinemachine → Cinemachine Camera
    ///    - Name it "LockOnCamera"
    ///    - Tracking Target: Player (or player shoulder point)
    ///    - Look At Target: Leave empty (assigned at runtime to LookAtHelper)
    ///    - Add Body: Cinemachine Orbital Follow (for rotation around player)
    ///    - Add Aim: Cinemachine Rotation Composer
    ///    - Set Priority: 0 (inactive by default)
    /// </para>
    /// <para>
    /// 2. <b>Configure Orbital Follow:</b>
    ///    - Orbit Style: Sphere
    ///    - Radius: 3-4 (camera distance from player)
    ///    - Target Offset: (0, 1.5, 0) for shoulder height
    ///    - Binding Mode: Lock To Target On Assign (important!)
    /// </para>
    /// <para>
    /// 3. <b>Configure Rotation Composer:</b>
    ///    - Target Offset: (0, 0, 0)
    ///    - Lookahead Time: 0
    ///    - Damping: Low values (0.1-0.3) for responsive tracking
    /// </para>
    /// <para>
    /// 4. <b>Attach this component:</b>
    ///    - Add to a GameObject in the scene
    ///    - Assign FreeLook Camera and LockOn Camera references
    ///    - Player references auto-found if not assigned
    /// </para>
    /// </remarks>
    public class CinemachineLockOnController : Singleton<CinemachineLockOnController>
    {
        #region Serialized Fields

        [Header("Camera References")]
        [SerializeField]
        [Tooltip("The FreeLook camera used for free-roam (lower priority)")]
        private CinemachineCamera freeLookCamera;

        [SerializeField]
        [Tooltip("The lock-on camera (higher priority when locked)")]
        private CinemachineCamera lockOnCamera;

        [Header("Target References")]
        [SerializeField]
        [Tooltip("Player transform (auto-finds PlayerController if null)")]
        private Transform playerTransform;

        [SerializeField]
        [Tooltip("Point on the player to track (shoulder height). If null, uses playerTransform + offset")]
        private Transform playerLockOnPoint;

        [SerializeField]
        [Tooltip("Offset from player position if playerLockOnPoint is not assigned")]
        private Vector3 playerOffset = new Vector3(0f, 1.5f, 0f);

        [Header("Priority Settings")]
        [SerializeField]
        [Tooltip("Priority for free-roam camera")]
        private int freeLookPriority = 10;

        [SerializeField]
        [Tooltip("Priority for lock-on camera when active")]
        private int lockOnActivePriority = 20;

        [SerializeField]
        [Tooltip("Priority for lock-on camera when inactive")]
        private int lockOnInactivePriority = 0;

        [Header("Lock-On Behavior (Lies of P Style)")]
        [SerializeField]
        [Tooltip("How much camera looks at enemy vs player (0 = player only, 1 = enemy only)")]
        [Range(0f, 1f)]
        private float lookAtTargetWeight = 0.35f;

        [SerializeField]
        [Tooltip("Camera distance from player during lock-on")]
        [Range(2f, 10f)]
        private float lockOnDistance = 4f;

        [SerializeField]
        [Tooltip("Camera height offset during lock-on")]
        [Range(0f, 3f)]
        private float lockOnHeightOffset = 0.5f;

        [SerializeField]
        [Tooltip("Fixed pitch angle during lock-on (degrees)")]
        [Range(5f, 45f)]
        private float lockOnPitchAngle = 15f;

        [Header("Soft Lock-On Settings")]
        [SerializeField]
        [Tooltip("Maximum manual rotation offset during lock-on (degrees)")]
        [Range(0f, 90f)]
        private float maxRotationOffset = 45f;

        [SerializeField]
        [Tooltip("Speed to recenter camera during lock-on")]
        [Range(1f, 10f)]
        private float recenterSpeed = 3f;

        [SerializeField]
        [Tooltip("Rotation sensitivity multiplier during lock-on")]
        [Range(0.1f, 1f)]
        private float rotationMultiplier = 0.3f;

        [SerializeField]
        [Tooltip("How fast camera rotates to face enemy")]
        [Range(1f, 20f)]
        private float rotationSpeed = 8f;

        [Header("Smoothing")]
        [SerializeField]
        [Tooltip("Position smoothing speed")]
        [Range(1f, 20f)]
        private float positionSmoothSpeed = 10f;

        [SerializeField]
        [Tooltip("Look-at smoothing speed")]
        [Range(1f, 20f)]
        private float lookAtSmoothSpeed = 12f;

        [Header("Debug")]
        [SerializeField]
        private bool debugMode;

        #endregion

        #region Private Fields

        private LockOnSystem _lockOnSystem;
        private InputHandler _inputHandler;
        private bool _isLockedOn;
        private Transform _currentTarget;
        private Vector3 _currentTargetPoint;
        private float _rotationOffset;
        private bool _subscribedToEvents;

        // Helper transform for look-at point
        private Transform _lookAtHelper;
        private Vector3 _smoothedLookAtPosition;

        // Camera state
        private float _currentYaw;
        private float _currentPitch;

        // Cinemachine components
        private CinemachineOrbitalFollow _orbitalFollow;

        #endregion

        #region Properties

        public bool IsLockedOn => _isLockedOn;
        public float RotationOffset => _rotationOffset;

        #endregion

        #region Unity Lifecycle

        protected override void Awake()
        {
            base.Awake();
            CreateLookAtHelper();
            CacheComponents();
        }

        private void Start()
        {
            AcquireReferences();
            SubscribeToLockOnEvents();
            SetCameraPriorities(false);
            ConfigureLockOnCamera();
        }

        private void OnDestroy()
        {
            UnsubscribeFromLockOnEvents();
            DestroyLookAtHelper();
        }

        private void LateUpdate()
        {
            if (!_isLockedOn) return;

            SyncWithLockOnSystem();
            ProcessSoftLockOnInput();
            UpdateLookAtPosition();
            UpdateCameraOrientation();
        }

        #endregion

        #region Initialization

        private void CreateLookAtHelper()
        {
            var helperGO = new GameObject("LockOn_LookAtHelper");
            helperGO.transform.SetParent(transform);
            _lookAtHelper = helperGO.transform;

            if (debugMode)
                Debug.Log("[CinemachineLockOnController] Created LookAtHelper transform");
        }

        private void DestroyLookAtHelper()
        {
            if (_lookAtHelper != null)
            {
                Destroy(_lookAtHelper.gameObject);
            }
        }

        private void CacheComponents()
        {
            if (lockOnCamera != null)
            {
                _orbitalFollow = lockOnCamera.GetComponent<CinemachineOrbitalFollow>();
            }
        }

        private void AcquireReferences()
        {
            _inputHandler = InputHandler.Instance;
            if (_inputHandler == null)
            {
                Debug.LogWarning("[CinemachineLockOnController] InputHandler not found");
            }

            if (playerTransform == null)
            {
                var player = FindFirstObjectByType<PlayerController>();
                if (player != null)
                {
                    playerTransform = player.transform;
                    if (debugMode)
                        Debug.Log($"[CinemachineLockOnController] Auto-found player: {player.name}");
                }
            }
        }

        private void ConfigureLockOnCamera()
        {
            if (lockOnCamera == null) return;

            // Set the camera to follow player and look at our helper
            lockOnCamera.Follow = playerTransform;
            lockOnCamera.LookAt = _lookAtHelper;

            if (debugMode)
                Debug.Log("[CinemachineLockOnController] Configured lock-on camera targets");
        }

        #endregion

        #region Lock-On System Integration

        private void SubscribeToLockOnEvents()
        {
            if (_subscribedToEvents) return;

            _lockOnSystem = LockOnSystem.Instance;
            if (_lockOnSystem == null)
            {
                if (debugMode)
                    Debug.LogWarning("[CinemachineLockOnController] LockOnSystem not found - will retry");
                return;
            }

            _lockOnSystem.OnTargetAcquired += HandleTargetAcquired;
            _lockOnSystem.OnTargetLost += HandleTargetLost;
            _lockOnSystem.OnTargetSwitched += HandleTargetSwitched;
            _subscribedToEvents = true;

            if (debugMode)
                Debug.Log("[CinemachineLockOnController] Subscribed to LockOnSystem events");
        }

        private void UnsubscribeFromLockOnEvents()
        {
            if (_lockOnSystem == null) return;

            _lockOnSystem.OnTargetAcquired -= HandleTargetAcquired;
            _lockOnSystem.OnTargetLost -= HandleTargetLost;
            _lockOnSystem.OnTargetSwitched -= HandleTargetSwitched;
        }

        private void SyncWithLockOnSystem()
        {
            if (!_subscribedToEvents && _lockOnSystem == null)
            {
                SubscribeToLockOnEvents();
            }

            if (_lockOnSystem == null) return;

            if (_lockOnSystem.IsLockedOn && _lockOnSystem.CurrentTarget != null)
            {
                _currentTargetPoint = _lockOnSystem.TargetLockOnPoint;
            }
        }

        private void HandleTargetAcquired(ILockOnTarget target)
        {
            _isLockedOn = true;
            _currentTarget = target.TargetTransform;
            _currentTargetPoint = target.LockOnPoint;
            _rotationOffset = 0f;

            // Initialize camera orientation based on player-to-enemy direction
            InitializeCameraOrientation();

            // Initialize smoothed look-at position
            _smoothedLookAtPosition = CalculateLookAtPosition();
            _lookAtHelper.position = _smoothedLookAtPosition;

            SetCameraPriorities(true);

            if (debugMode)
                Debug.Log($"[CinemachineLockOnController] Target acquired: {_currentTarget.name}");
        }

        private void HandleTargetLost(ILockOnTarget target)
        {
            _isLockedOn = false;
            _currentTarget = null;
            _rotationOffset = 0f;

            SetCameraPriorities(false);

            if (debugMode)
                Debug.Log("[CinemachineLockOnController] Target lost");
        }

        private void HandleTargetSwitched(ILockOnTarget oldTarget, ILockOnTarget newTarget)
        {
            _currentTarget = newTarget.TargetTransform;
            _currentTargetPoint = newTarget.LockOnPoint;
            _rotationOffset = 0f;

            if (debugMode)
                Debug.Log($"[CinemachineLockOnController] Target switched to: {_currentTarget.name}");
        }

        #endregion

        #region Camera Orientation

        private void InitializeCameraOrientation()
        {
            Vector3 playerPos = GetPlayerPivotPosition();
            Vector3 toTarget = _currentTargetPoint - playerPos;
            toTarget.y = 0f;

            if (toTarget.sqrMagnitude > 0.01f)
            {
                _currentYaw = Mathf.Atan2(toTarget.x, toTarget.z) * Mathf.Rad2Deg;
            }

            _currentPitch = lockOnPitchAngle;
        }

        private void UpdateCameraOrientation()
        {
            if (_currentTarget == null) return;

            // Calculate desired yaw from player-to-target direction
            Vector3 playerPos = GetPlayerPivotPosition();
            Vector3 toTarget = _currentTargetPoint - playerPos;
            toTarget.y = 0f;

            if (toTarget.sqrMagnitude < 0.01f) return;

            float baseYaw = Mathf.Atan2(toTarget.x, toTarget.z) * Mathf.Rad2Deg;
            float targetYaw = baseYaw + _rotationOffset;

            // Smooth yaw transition
            _currentYaw = Mathf.LerpAngle(_currentYaw, targetYaw, rotationSpeed * Time.deltaTime);

            // Fixed pitch during lock-on
            _currentPitch = Mathf.Lerp(_currentPitch, lockOnPitchAngle, rotationSpeed * Time.deltaTime);

            // Apply to orbital follow if available
            ApplyOrientationToCamera();
        }

        private void ApplyOrientationToCamera()
        {
            if (_orbitalFollow == null) return;

            try
            {
                // Set the horizontal axis to position camera behind player facing enemy
                _orbitalFollow.HorizontalAxis.Value = _currentYaw;
                _orbitalFollow.VerticalAxis.Value = _currentPitch;
            }
            catch (System.Exception e)
            {
                if (debugMode)
                    Debug.LogWarning($"[CinemachineLockOnController] Could not set orbital axis: {e.Message}");
            }
        }

        #endregion

        #region Look-At Position

        private void UpdateLookAtPosition()
        {
            Vector3 targetPosition = CalculateLookAtPosition();

            // Smooth the look-at position
            _smoothedLookAtPosition = Vector3.Lerp(
                _smoothedLookAtPosition,
                targetPosition,
                lookAtSmoothSpeed * Time.deltaTime
            );

            _lookAtHelper.position = _smoothedLookAtPosition;
        }

        private Vector3 CalculateLookAtPosition()
        {
            Vector3 playerPos = GetPlayerPivotPosition();

            // Weighted blend between player and target
            // 0 = look at player, 1 = look at enemy
            return Vector3.Lerp(playerPos, _currentTargetPoint, lookAtTargetWeight);
        }

        private Vector3 GetPlayerPivotPosition()
        {
            if (playerLockOnPoint != null)
            {
                return playerLockOnPoint.position;
            }

            if (playerTransform != null)
            {
                return playerTransform.position + playerOffset;
            }

            return Vector3.zero;
        }

        #endregion

        #region Soft Lock-On Behavior

        private void ProcessSoftLockOnInput()
        {
            if (_inputHandler == null) return;

            Vector2 lookInput = _inputHandler.GetLookInput();

            // Apply rotation with reduced sensitivity
            float yawDelta = lookInput.x * rotationMultiplier;

            // Clamp rotation offset
            _rotationOffset = Mathf.Clamp(
                _rotationOffset + yawDelta,
                -maxRotationOffset,
                maxRotationOffset
            );

            // Decay back to center
            _rotationOffset = Mathf.Lerp(
                _rotationOffset,
                0f,
                recenterSpeed * Time.deltaTime
            );
        }

        #endregion

        #region Camera Priority Management

        private void SetCameraPriorities(bool lockOnActive)
        {
            if (freeLookCamera != null)
            {
                freeLookCamera.Priority = freeLookPriority;
            }

            if (lockOnCamera != null)
            {
                lockOnCamera.Priority = lockOnActive ? lockOnActivePriority : lockOnInactivePriority;
            }

            if (debugMode)
            {
                Debug.Log($"[CinemachineLockOnController] Camera priorities - " +
                          $"FreeLook: {freeLookPriority}, " +
                          $"LockOn: {(lockOnActive ? lockOnActivePriority : lockOnInactivePriority)}");
            }
        }

        #endregion

        #region Public API

        /// <summary>
        /// Force reset the rotation offset to center.
        /// </summary>
        public void ResetRotationOffset()
        {
            _rotationOffset = 0f;
        }

        /// <summary>
        /// Set the look-at weight dynamically.
        /// </summary>
        public void SetLookAtWeight(float weight)
        {
            lookAtTargetWeight = Mathf.Clamp01(weight);
        }

        /// <summary>
        /// Get the current look-at position for external use.
        /// </summary>
        public Vector3 GetCurrentLookAtPosition()
        {
            return _smoothedLookAtPosition;
        }

        #endregion

        #region Debug

        private void OnDrawGizmos()
        {
            if (!debugMode) return;

            Vector3 playerPos = GetPlayerPivotPosition();

            if (_isLockedOn && _currentTarget != null)
            {
                // Lock-on point on enemy
                Gizmos.color = Color.red;
                Gizmos.DrawWireSphere(_currentTargetPoint, 0.3f);

                // Line from player to target
                Gizmos.color = Color.yellow;
                Gizmos.DrawLine(playerPos, _currentTargetPoint);

                // Weighted look-at point
                Gizmos.color = Color.magenta;
                Gizmos.DrawWireSphere(_smoothedLookAtPosition, 0.2f);

                // Line from camera to look-at point
                if (lockOnCamera != null)
                {
                    Gizmos.color = Color.cyan;
                    Gizmos.DrawLine(lockOnCamera.transform.position, _smoothedLookAtPosition);
                }

                // Show rotation offset arc
                if (Mathf.Abs(_rotationOffset) > 0.1f)
                {
                    Gizmos.color = Color.green;
                    Vector3 toTarget = (_currentTargetPoint - playerPos).normalized;
                    Vector3 offsetDir = Quaternion.Euler(0, _rotationOffset, 0) * toTarget;
                    Gizmos.DrawRay(playerPos, offsetDir * 3f);
                }
            }
            else
            {
                // Show player pivot when not locked on
                Gizmos.color = Color.cyan;
                Gizmos.DrawWireSphere(playerPos, 0.2f);
            }
        }

        #endregion
    }
}
