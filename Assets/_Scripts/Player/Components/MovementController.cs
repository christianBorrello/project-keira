using _Scripts.Camera;
using _Scripts.Player.Data;
using _Scripts.Player.Components;
using Imports.Core;
using Systems;
using UnityEngine;

namespace _Scripts.Player.Components
{
    /// <summary>
    /// Locomotion mode for movement speed selection.
    /// </summary>
    public enum LocomotionMode
    {
        Walk,   // Slow movement (Control held)
        Run,    // Default movement
        Sprint  // Fast movement (Shift held)
    }

    /// <summary>
    /// Turn type for animator state selection.
    /// </summary>
    public enum TurnType
    {
        None = 0,
        Turn90Left = 1,
        Turn90Right = 2,
        Turn180 = 3
    }

    /// <summary>
    /// Manages player movement, gravity, rotation, and smoothing.
    /// Handles camera-relative movement with lock-on orbital support.
    /// Implements ICharacterController for KCC integration.
    /// </summary>
    [RequireComponent(typeof(PlayerController))]
    [RequireComponent(typeof(KinematicCharacterMotor))]
    public class MovementController : MonoBehaviour, ICharacterController
    {
        // References
        private PlayerController _player;
        private KinematicCharacterMotor _motor;
        private AnimationController _animationController;
        private LockOnController _lockOnController;
        private ExternalForcesManager _externalForces;
        private Transform _cameraTransform;

        // Gravity
        private float _verticalVelocity;
        private const float Gravity = -20f;

        // KCC Movement Intent - bridges Update (input) to FixedUpdate (physics)
        private MovementIntent _intent;

        /// <summary>
        /// Cached movement input for KCC callback system.
        /// Bridges Update (60-144Hz) to FixedUpdate (50Hz) timing gap.
        /// </summary>
        private struct MovementIntent
        {
            public Vector2 RawInput;           // Original input from InputHandler
            public Vector3 WorldDirection;     // Camera-relative direction (pre-calculated)
            public LocomotionMode Mode;        // Walk/Run/Sprint
            public float Timestamp;            // Time.time when cached
            public bool IsValid;               // Explicitly set when input received

            /// <summary>
            /// Intent is stale if older than 100ms (missed FixedUpdate cycles).
            /// </summary>
            public readonly bool IsStale => IsValid && (Time.time - Timestamp > 0.1f);

            /// <summary>
            /// Reset intent to invalid state after consumption.
            /// </summary>
            public void Invalidate()
            {
                IsValid = false;
                RawInput = Vector2.zero;
                WorldDirection = Vector3.zero;
            }
        }

        // Consolidated smoothing state (movement, rotation, animation)
        [Header("Smoothing State (Debug)")]
        [SerializeField]
        private SmoothingState _smoothing = SmoothingState.CreateDefault();

        [Header("Movement Speeds")]
        [SerializeField]
        [Tooltip("Walking speed - slow movement with Control held (units per second)")]
        private float walkSpeed = 1.1f;

        [SerializeField]
        [Tooltip("Running speed - default movement (units per second)")]
        private float runSpeed = 5f;

        [SerializeField]
        [Tooltip("Sprinting speed - fast movement with Shift held (units per second)")]
        private float sprintSpeed = 7f;

        [Header("Movement Smoothing")]
        [SerializeField]
        private float movementSmoothTime = 0.08f;

        [SerializeField]
        private float animatorSmoothTime = 0.1f;

        [SerializeField]
        [Tooltip("How fast MoveX/MoveY decay to 0 when input stops (lower = faster)")]
        private float moveDirectionDecayTime = 0.02f;

        [SerializeField]
        [Range(1f, 5f)]
        [Tooltip("Controls how aggressively movement reduces when misaligned. Higher = sharper falloff (more pivot-in-place), Lower = smoother transition")]
        private float alignmentFalloffExponent = 2f;

        [SerializeField]
        [Range(1f, 5f)]
        [Tooltip("Rotation speed multiplier when misaligned. Higher = faster turning when facing wrong direction")]
        private float misalignedRotationMultiplier = 2.5f;

        [Header("Character Rotation Smoothing")]
        [SerializeField]
        [Tooltip("Time for character to smoothly rotate toward camera direction during movement")]
        private float characterRotationSmoothTime = 0.2f;

        [Header("Momentum System")]
        [SerializeField]
        [Tooltip("Acceleration curve: X = normalized time (0-1), Y = speed factor (0-1). Fast ease-in for responsive feel.")]
        private AnimationCurve accelerationCurve = CreateDefaultAccelerationCurve();

        [SerializeField]
        [Tooltip("Deceleration curve: X = normalized time (0-1), Y = speed factor (1-0). Smooth ease-out for weight.")]
        private AnimationCurve decelerationCurve = CreateDefaultDecelerationCurve();

        [SerializeField]
        [Range(0.05f, 0.5f)]
        [Tooltip("Time in seconds to reach full speed from standstill (free movement)")]
        private float accelerationDuration = 0.2f;

        [SerializeField]
        [Range(0.05f, 0.5f)]
        [Tooltip("Time in seconds to stop from full speed (free movement)")]
        private float decelerationDuration = 0.15f;

        [Header("Lock-On Momentum (separate tuning)")]
        [SerializeField]
        [Range(0.1f, 1.0f)]
        [Tooltip("Time in seconds to reach full speed when locked on (longer = more gradual start)")]
        private float lockedOnAccelerationDuration = 0.4f;

        [SerializeField]
        [Range(0.1f, 1.0f)]
        [Tooltip("Time in seconds to stop when locked on (longer = more gradual stop)")]
        private float lockedOnDecelerationDuration = 0.35f;

        [Header("Pivot Settings")]
        [SerializeField]
        [Range(30f, 120f)]
        [Tooltip("Angle threshold (degrees) above which pivot speed reduction kicks in")]
        private float pivotAngleThreshold = 60f;

        [SerializeField]
        [Range(0.2f, 0.8f)]
        [Tooltip("Maximum speed reduction factor during hard pivot (0.4 = 40% of normal speed)")]
        private float maxPivotSpeedReduction = 0.4f;

        [Header("Turn In Place")]
        [SerializeField]
        [Range(30f, 90f)]
        [Tooltip("Angle threshold (degrees) to trigger turn-in-place when stationary")]
        private float turnInPlaceThreshold = 45f;

        [SerializeField]
        [Range(90f, 170f)]
        [Tooltip("Angle threshold (degrees) to trigger 180-degree turn instead of 90-degree")]
        private float turn180Threshold = 135f;

        [SerializeField]
        [Range(0.1f, 2f)]
        [Tooltip("Speed threshold below which character is considered stationary for turn-in-place")]
        private float turnInPlaceSpeedThreshold = 0.5f;

        [Header("Debug")]
        [SerializeField]
        private bool debugMode;

        // Start/Stop animation tracking
        private bool _wasMovingLastFrame;

        #region Properties

        /// <summary>
        /// Whether the character is grounded (uses KCC's stable ground detection).
        /// </summary>
        public bool IsGrounded => _motor?.GroundingStatus.IsStableOnGround ?? false;

        /// <summary>
        /// Current vertical velocity (gravity).
        /// </summary>
        public float VerticalVelocity => _verticalVelocity;

        /// <summary>
        /// External forces manager for knockback, explosions, etc.
        /// </summary>
        public ExternalForcesManager ExternalForces => _externalForces;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            _player = GetComponent<PlayerController>();
            _motor = GetComponent<KinematicCharacterMotor>();

            // Wire up KCC - tell the motor that we are its character controller
            _motor.CharacterController = this;
        }

        private void Start()
        {
            _animationController = _player.AnimationController;
            _lockOnController = _player.LockOnController;
            _externalForces = GetComponent<ExternalForcesManager>();

            // Auto-add ExternalForcesManager if not present
            if (_externalForces == null)
            {
                _externalForces = gameObject.AddComponent<ExternalForcesManager>();
            }

            // Auto-find camera if not assigned (prefer ThirdPersonCameraSystem)
            if (_cameraTransform == null)
            {
                _cameraTransform = ThirdPersonCameraSystem.Instance?.transform
                    ?? UnityEngine.Camera.main?.transform;
            }
        }

        private void Update()
        {
            // Gravity is handled in UpdateVelocity (KCC callback)
            UpdateSmoothingDecay();
        }

        #endregion

        #region Movement

        /// <summary>
        /// Apply movement based on input. Called by states via PlayerController.
        /// Caches intent for KCC callbacks, then updates animation/smoothing state.
        /// </summary>
        public void ApplyMovement(Vector2 moveInput, LocomotionMode mode)
        {
            // DEBUG: Log what's null
            if (_motor == null)
            {
                Debug.LogError("[MovementController] _motor is NULL!");
                return;
            }
            if (_cameraTransform == null)
            {
                Debug.LogError("[MovementController] _cameraTransform is NULL! Trying to find camera...");
                _cameraTransform = ThirdPersonCameraSystem.Instance?.transform
                    ?? UnityEngine.Camera.main?.transform;
                if (_cameraTransform == null)
                {
                    Debug.LogError("[MovementController] Could not find any camera!");
                    return;
                }
            }

            // Cache intent for KCC callbacks (Update→FixedUpdate bridge)
            CacheMovementIntent(moveInput, mode);

            bool isLockedOn = _lockOnController?.IsLockedOn ?? false;

            if (isLockedOn)
            {
                // Update smoothing/animation state for lock-on
                ApplyLockedOnMovement(moveInput, mode);
            }
            else
            {
                // Clear lock-on distance correction when not locked on
                // Prevents residual velocity from persisting after unlock
                _smoothing.LockOnDistanceCorrection = Vector3.zero;

                // Update smoothing/animation state for momentum movement
                ApplyMomentumMovement(moveInput, mode);
            }
        }

        /// <summary>
        /// Cache movement intent for consumption by KCC callbacks.
        /// Pre-calculates camera-relative direction in Update for efficiency.
        /// Validates input to protect against NaN/Infinity from hardware glitches.
        /// </summary>
        private void CacheMovementIntent(Vector2 moveInput, LocomotionMode mode)
        {
            // P0 FIX: Validate input before processing to prevent NaN propagation
            if (!IsValidVector2(moveInput))
            {
                if (debugMode)
                    Debug.LogWarning("[MovementController] Invalid input detected (NaN/Infinity), clamping to zero");
                moveInput = Vector2.zero;
            }

            Vector3 worldDirection = GetCameraRelativeDirection(moveInput);

            // P0 FIX: Validate camera calculation result
            if (!IsValidVector3(worldDirection))
            {
                Debug.LogError("[MovementController] Camera calculation produced invalid direction!");
                worldDirection = Vector3.zero;
            }

            _intent = new MovementIntent
            {
                RawInput = moveInput,
                WorldDirection = worldDirection,
                Mode = mode,
                Timestamp = Time.time,
                IsValid = moveInput.sqrMagnitude > 0.001f && IsValidVector2(moveInput)
            };
        }

        /// <summary>
        /// Momentum-based movement for unlocked state.
        /// Features: acceleration curves, pivot speed reduction, smooth rotation, turn-in-place.
        /// </summary>
        private void ApplyMomentumMovement(Vector2 moveInput, LocomotionMode mode)
        {
            // Calculate camera-relative target direction
            Vector3 targetDirection = GetCameraRelativeDirection(moveInput);
            bool hasInput = targetDirection.sqrMagnitude > 0.01f;

            // Check for turn-in-place trigger (must be before movement processing)
            if (ShouldTurnInPlace(targetDirection))
            {
                EnterTurnInPlace(targetDirection);
            }

            // Handle turn-in-place state
            if (_smoothing.IsTurningInPlace)
            {
                HandleTurnInPlace(targetDirection);

                // During turn-in-place: no horizontal movement, rotation handled in UpdateRotation
                // Update animation parameters with turn info
                UpdateMomentumAnimationParameters(moveInput, mode);
                return;
            }

            // Get target speed based on mode
            float targetSpeed = GetSpeedForMode(mode);

            // Calculate turn angle for pivot system
            float turnAngle = 0f;
            if (hasInput)
            {
                turnAngle = CalculateTurnAngle(transform.forward, targetDirection);
                _smoothing.TurnAngle = turnAngle;
            }

            // Update acceleration/deceleration state
            UpdateMomentumTimers(hasInput);

            // Calculate current velocity using curves
            float speedFactor;
            if (hasInput)
            {
                speedFactor = EvaluateAccelerationCurve();

                // Apply pivot speed reduction when turning significantly
                float pivotFactor = CalculatePivotFactor(Mathf.Abs(turnAngle));
                speedFactor *= pivotFactor;
            }
            else
            {
                speedFactor = EvaluateDecelerationCurve();
            }

            // Update velocity magnitude
            _smoothing.TargetVelocityMagnitude = hasInput ? targetSpeed : 0f;
            _smoothing.CurrentVelocityMagnitude = targetSpeed * speedFactor;

            // Smooth direction changes
            _smoothing.SmoothedMoveDirection = Vector3.SmoothDamp(
                _smoothing.SmoothedMoveDirection,
                hasInput ? targetDirection : Vector3.zero,
                ref _smoothing.MoveDirectionVelocity,
                hasInput ? movementSmoothTime : moveDirectionDecayTime
            );

            // Rotation is now handled in UpdateRotation callback (KCC FixedUpdate)
            // using _smoothing.SmoothedMoveDirection calculated above

            // Movement is applied in UpdateVelocity via CalculateGroundVelocity()

            // Update animation parameters
            UpdateMomentumAnimationParameters(moveInput, mode);
        }

        /// <summary>
        /// Lock-on movement with orbital strafing around target.
        /// Updates smoothing state for consumption by UpdateVelocity.
        /// </summary>
        private void ApplyLockedOnMovement(Vector2 moveInput, LocomotionMode mode)
        {
            var currentTarget = _lockOnController?.CurrentTarget;

            // Get camera-relative movement direction
            Vector3 cameraForward = _cameraTransform.forward;
            Vector3 cameraRight = _cameraTransform.right;
            cameraForward.y = 0f;
            cameraRight.y = 0f;
            cameraForward.Normalize();
            cameraRight.Normalize();

            Vector3 targetDirection = (cameraForward * moveInput.y + cameraRight * moveInput.x);

            if (targetDirection.sqrMagnitude > 0.01f)
            {
                targetDirection.Normalize();
            }
            else
            {
                targetDirection = Vector3.zero;
            }

            // Convert to target-relative direction for orbital movement
            if (currentTarget != null && targetDirection.sqrMagnitude > 0.01f)
            {
                Vector3 toTarget = (currentTarget.LockOnPoint - transform.position);
                toTarget.y = 0;
                toTarget.Normalize();

                Vector3 orbitRight = Vector3.Cross(Vector3.up, toTarget).normalized;

                float strafeComponent = Vector3.Dot(targetDirection, orbitRight);
                float approachComponent = Vector3.Dot(targetDirection, toTarget);

                _smoothing.TargetRelativeStrafe = strafeComponent;
                _smoothing.TargetRelativeApproach = approachComponent;

                targetDirection = (orbitRight * strafeComponent) + (toTarget * approachComponent);
                if (targetDirection.sqrMagnitude > 0.01f)
                {
                    targetDirection.Normalize();
                }
            }

            // Check if player has movement input
            bool hasInput = moveInput.sqrMagnitude > 0.01f;

            // Update acceleration/deceleration timers (shared with momentum system)
            UpdateMomentumTimers(hasInput);

            // Use SmoothDamp for smooth direction changes
            _smoothing.SmoothedMoveDirection = Vector3.SmoothDamp(
                _smoothing.SmoothedMoveDirection,
                targetDirection,
                ref _smoothing.MoveDirectionVelocity,
                movementSmoothTime
            );

            if (_smoothing.SmoothedMoveDirection.sqrMagnitude > 0.01f)
            {
                float targetSpeed = GetSpeedForMode(mode);

                if (hasInput)
                {
                    // Accelerating - player is pressing movement keys
                    // Use lock-on specific duration for more gradual acceleration
                    float speedFactor = EvaluateLockedOnAccelerationCurve();
                    _smoothing.TargetVelocityMagnitude = targetSpeed;
                    _smoothing.CurrentVelocityMagnitude = targetSpeed * speedFactor;

                    if (debugMode)
                        Debug.Log($"[LockOn ACCEL] timer={_smoothing.AccelerationTimer:F3}, duration={lockedOnAccelerationDuration:F2}, factor={speedFactor:F2}, vel={_smoothing.CurrentVelocityMagnitude:F2}");
                }
                else
                {
                    // Decelerating - player released input but still has momentum
                    // Use lock-on specific duration for more gradual deceleration
                    float speedFactor = EvaluateLockedOnDecelerationCurve();
                    _smoothing.CurrentVelocityMagnitude = _smoothing.TargetVelocityMagnitude * speedFactor;

                    if (debugMode)
                        Debug.Log($"[LockOn DECEL] timer={_smoothing.DecelerationTimer:F3}, duration={lockedOnDecelerationDuration:F2}, factor={speedFactor:F2}, vel={_smoothing.CurrentVelocityMagnitude:F2}");
                }

                // Face target when locked on
                if (currentTarget != null)
                {
                    Vector3 toTarget = currentTarget.LockOnPoint - transform.position;
                    toTarget.y = 0;
                    if (toTarget.sqrMagnitude > 0.01f)
                    {
                        RotateTowards(toTarget.normalized);
                    }
                }

                // Movement is applied in UpdateVelocity via CalculateGroundVelocity()

                // Maintain distance from target during pure strafe
                if (currentTarget != null && _lockOnController != null)
                {
                    Vector3 toTarget = currentTarget.LockOnPoint - transform.position;
                    toTarget.y = 0;
                    float currentDistance = toTarget.magnitude;

                    Vector3 toTargetNorm = toTarget.normalized;
                    float approachComp = Mathf.Abs(Vector3.Dot(_smoothing.SmoothedMoveDirection, toTargetNorm));

                    if (approachComp < 0.3f && currentDistance > 0.1f)
                    {
                        // Pure strafe - apply velocity correction to maintain locked distance
                        float distanceError = currentDistance - _lockOnController.LockedOnDistance;
                        const float correctionStrength = 3f; // Units per second per unit of error

                        // Add correction velocity toward/away from target
                        Vector3 correctionVelocity = toTargetNorm * (distanceError * correctionStrength);
                        _smoothing.LockOnDistanceCorrection = correctionVelocity;
                    }
                    else
                    {
                        // Approaching or retreating - update locked distance, clear correction
                        _lockOnController.SetLockedOnDistance(currentDistance);
                        _smoothing.LockOnDistanceCorrection = Vector3.zero;
                    }
                }
                else
                {
                    _smoothing.LockOnDistanceCorrection = Vector3.zero;
                }
            }
            else
            {
                // No movement input - use lock-on deceleration curve for gradual stop
                float speedFactor = EvaluateLockedOnDecelerationCurve();
                _smoothing.CurrentVelocityMagnitude = _smoothing.TargetVelocityMagnitude * speedFactor;

                if (debugMode)
                    Debug.Log($"[LockOn DECEL-ELSE] timer={_smoothing.DecelerationTimer:F3}, duration={lockedOnDecelerationDuration:F2}, factor={speedFactor:F2}, vel={_smoothing.CurrentVelocityMagnitude:F2}");

                // Only zero out target velocity after deceleration is complete
                if (speedFactor < 0.01f)
                {
                    _smoothing.TargetVelocityMagnitude = 0f;
                    _smoothing.DecelerationDirection = Vector3.zero; // Clear preserved direction
                }

                // Clear distance correction to prevent sliding when stopped
                _smoothing.LockOnDistanceCorrection = Vector3.zero;
            }

            // Update animation parameters (original logic)
            UpdateAnimationParameters(moveInput, mode, true);
        }

        #endregion

        #region Turn-In-Place

        /// <summary>
        /// Determines if the character should perform a turn-in-place animation.
        /// Returns true when stationary and input direction differs significantly from facing.
        /// </summary>
        private bool ShouldTurnInPlace(Vector3 targetDirection)
        {
            // Not applicable if already turning or no input
            if (_smoothing.IsTurningInPlace || targetDirection.sqrMagnitude < 0.01f)
                return false;

            // Check if velocity is below threshold (effectively stationary)
            if (_smoothing.CurrentVelocityMagnitude > turnInPlaceSpeedThreshold)
                return false;

            // Check turn angle exceeds threshold
            float turnAngle = Mathf.Abs(CalculateTurnAngle(transform.forward, targetDirection));
            return turnAngle > turnInPlaceThreshold;
        }

        /// <summary>
        /// Handle turn-in-place state. Called when character needs to rotate significantly while stationary.
        /// Sets animator parameters and tracks progress until rotation is complete.
        /// NOTE: Actual rotation is handled in UpdateRotation() callback for KCC compatibility.
        /// </summary>
        private void HandleTurnInPlace(Vector3 targetDirection)
        {
            if (targetDirection.sqrMagnitude < 0.01f)
            {
                // No input - exit turn-in-place
                ExitTurnInPlace();
                return;
            }

            float currentTurnAngle = CalculateTurnAngle(transform.forward, targetDirection);

            // Check if turn is complete (within small threshold)
            if (Mathf.Abs(currentTurnAngle) < 15f)
            {
                ExitTurnInPlace();
                return;
            }

            // Update turn progress (0-1 based on remaining angle vs initial angle)
            float initialAngle = Mathf.Abs(CalculateTurnAngle(transform.forward, _smoothing.TurnTargetDirection));
            if (initialAngle > 1f)
            {
                _smoothing.TurnProgress = 1f - Mathf.Clamp01(Mathf.Abs(currentTurnAngle) / initialAngle);
            }

            // Update animator with current turn angle for animation selection
            _smoothing.TurnAngle = currentTurnAngle;

            // Rotation is handled in UpdateRotation() using TurnTargetDirection
        }

        /// <summary>
        /// Begin turn-in-place state.
        /// </summary>
        private void EnterTurnInPlace(Vector3 targetDirection)
        {
            _smoothing.IsTurningInPlace = true;
            _smoothing.TurnProgress = 0f;
            _smoothing.TurnTargetDirection = targetDirection;
            _smoothing.TurnAngle = CalculateTurnAngle(transform.forward, targetDirection);
        }

        /// <summary>
        /// Exit turn-in-place state and reset related values.
        /// </summary>
        private void ExitTurnInPlace()
        {
            _smoothing.IsTurningInPlace = false;
            _smoothing.TurnProgress = 0f;
        }

        /// <summary>
        /// Cancel turn-in-place immediately (e.g., for combat interrupt).
        /// Call this when entering combat states.
        /// </summary>
        public void CancelTurnInPlace()
        {
            ExitTurnInPlace();
            _smoothing.RotationVelocity = 0f;
        }

        /// <summary>
        /// Calculate turn type based on turn angle.
        /// Used for cleaner animator transitions.
        /// </summary>
        private TurnType CalculateTurnType(float turnAngle)
        {
            float absTurnAngle = Mathf.Abs(turnAngle);

            if (absTurnAngle < turnInPlaceThreshold)
                return TurnType.None;

            if (absTurnAngle >= turn180Threshold)
                return TurnType.Turn180;

            return turnAngle < 0 ? TurnType.Turn90Left : TurnType.Turn90Right;
        }

        #endregion

        #region Momentum Helpers

        /// <summary>
        /// Get camera-relative direction from input.
        /// </summary>
        private Vector3 GetCameraRelativeDirection(Vector2 moveInput)
        {
            Vector3 cameraForward = _cameraTransform.forward;
            Vector3 cameraRight = _cameraTransform.right;
            cameraForward.y = 0f;
            cameraRight.y = 0f;
            cameraForward.Normalize();
            cameraRight.Normalize();

            Vector3 direction = (cameraForward * moveInput.y + cameraRight * moveInput.x);
            return direction.sqrMagnitude > 0.01f ? direction.normalized : Vector3.zero;
        }

        /// <summary>
        /// Get movement speed for the specified locomotion mode.
        /// </summary>
        private float GetSpeedForMode(LocomotionMode mode)
        {
            return mode switch
            {
                LocomotionMode.Walk => walkSpeed,
                LocomotionMode.Run => runSpeed,
                LocomotionMode.Sprint => sprintSpeed,
                _ => runSpeed
            };
        }

        /// <summary>
        /// Calculate signed angle between current forward and target direction.
        /// Returns angle in degrees (-180 to 180).
        /// </summary>
        private float CalculateTurnAngle(Vector3 currentForward, Vector3 targetDirection)
        {
            if (targetDirection.sqrMagnitude < 0.01f) return 0f;
            return Vector3.SignedAngle(currentForward, targetDirection, Vector3.up);
        }

        /// <summary>
        /// Update acceleration/deceleration timers based on input state.
        /// Includes hysteresis to handle erratic/stuttery input gracefully.
        /// Also preserves movement direction when deceleration starts.
        /// </summary>
        private void UpdateMomentumTimers(bool hasInput)
        {
            // Grace period: don't reset acceleration if input returns quickly
            // This prevents stuttery input from causing jerky movement
            const float inputGracePeriod = 0.05f; // 50ms grace period

            if (hasInput)
            {
                if (!_smoothing.IsAccelerating)
                {
                    // Check if we're within grace period (quick input return)
                    if (_smoothing.DecelerationTimer < inputGracePeriod && _smoothing.AccelerationTimer > 0f)
                    {
                        // Don't reset - continue from where we were
                        // This handles rapid on-off-on input
                    }
                    else
                    {
                        // Genuine start of movement - reset acceleration timer
                        _smoothing.AccelerationTimer = 0f;
                    }
                    _smoothing.IsAccelerating = true;
                    // Clear deceleration direction when accelerating again
                    _smoothing.DecelerationDirection = Vector3.zero;
                }
                _smoothing.AccelerationTimer += Time.deltaTime;
                _smoothing.DecelerationTimer = 0f;
            }
            else
            {
                if (_smoothing.IsAccelerating)
                {
                    // Just stopped - reset deceleration timer and save current direction
                    _smoothing.DecelerationTimer = 0f;
                    _smoothing.IsAccelerating = false;

                    // Preserve direction for deceleration (prevents early direction decay)
                    if (_smoothing.SmoothedMoveDirection.sqrMagnitude > 0.01f)
                    {
                        _smoothing.DecelerationDirection = _smoothing.SmoothedMoveDirection.normalized;
                    }
                }
                _smoothing.DecelerationTimer += Time.deltaTime;
            }
        }

        /// <summary>
        /// Evaluate acceleration curve based on time since movement started.
        /// Returns 0-1 speed factor.
        /// </summary>
        private float EvaluateAccelerationCurve()
        {
            if (accelerationDuration <= 0f) return 1f;
            float normalizedTime = Mathf.Clamp01(_smoothing.AccelerationTimer / accelerationDuration);
            return accelerationCurve.Evaluate(normalizedTime);
        }

        /// <summary>
        /// Evaluate deceleration curve based on time since input stopped.
        /// Returns 1-0 speed factor.
        /// </summary>
        private float EvaluateDecelerationCurve()
        {
            if (decelerationDuration <= 0f) return 0f;
            float normalizedTime = Mathf.Clamp01(_smoothing.DecelerationTimer / decelerationDuration);
            return decelerationCurve.Evaluate(normalizedTime);
        }

        /// <summary>
        /// Evaluate acceleration curve for lock-on movement (uses separate duration).
        /// Returns 0-1 speed factor.
        /// </summary>
        private float EvaluateLockedOnAccelerationCurve()
        {
            if (lockedOnAccelerationDuration <= 0f) return 1f;
            float normalizedTime = Mathf.Clamp01(_smoothing.AccelerationTimer / lockedOnAccelerationDuration);
            return accelerationCurve.Evaluate(normalizedTime);
        }

        /// <summary>
        /// Evaluate deceleration curve for lock-on movement (uses separate duration).
        /// Returns 1-0 speed factor.
        /// </summary>
        private float EvaluateLockedOnDecelerationCurve()
        {
            if (lockedOnDecelerationDuration <= 0f) return 0f;
            float normalizedTime = Mathf.Clamp01(_smoothing.DecelerationTimer / lockedOnDecelerationDuration);
            return decelerationCurve.Evaluate(normalizedTime);
        }

        /// <summary>
        /// Calculate pivot speed reduction factor based on turn angle.
        /// Returns 1.0 for small angles, reduces to maxPivotSpeedReduction for large angles.
        /// </summary>
        private float CalculatePivotFactor(float turnAngleDegrees)
        {
            if (turnAngleDegrees <= pivotAngleThreshold) return 1f;

            // Smoothly reduce speed from threshold to 180 degrees
            float pivotProgress = Mathf.InverseLerp(pivotAngleThreshold, 180f, turnAngleDegrees);
            return Mathf.Lerp(1f, maxPivotSpeedReduction, pivotProgress);
        }

        /// <summary>
        /// Apply rotation toward target direction with momentum.
        /// Rotation is faster when more misaligned.
        /// </summary>
        private void ApplyMomentumRotation(Vector3 targetDirection, float turnAngle)
        {
            if (targetDirection.sqrMagnitude < 0.01f && _smoothing.SmoothedMoveDirection.sqrMagnitude < 0.01f)
                return;

            // Use smoothed direction if we have it, otherwise target
            Vector3 rotationTarget = _smoothing.SmoothedMoveDirection.sqrMagnitude > 0.01f
                ? _smoothing.SmoothedMoveDirection
                : targetDirection;

            if (rotationTarget.sqrMagnitude < 0.01f) return;

            float targetAngle = Mathf.Atan2(rotationTarget.x, rotationTarget.z) * Mathf.Rad2Deg;

            // Faster rotation when more misaligned
            float rotationMultiplier = 1f + (Mathf.Abs(turnAngle) / 90f) * (misalignedRotationMultiplier - 1f);
            float adjustedSmoothTime = characterRotationSmoothTime / rotationMultiplier;

            float smoothedAngle = Mathf.SmoothDampAngle(
                transform.eulerAngles.y,
                targetAngle,
                ref _smoothing.RotationVelocity,
                adjustedSmoothTime
            );
            transform.rotation = Quaternion.Euler(0f, smoothedAngle, 0f);
        }

        /// <summary>
        /// Update animation parameters for momentum-based movement.
        /// </summary>
        private void UpdateMomentumAnimationParameters(Vector2 moveInput, LocomotionMode mode)
        {
            if (_animationController == null) return;

            // Normalize speed for animator based on actual velocity
            float normalizedSpeed = 0f;
            if (_smoothing.CurrentVelocityMagnitude > 0.1f)
            {
                // Map velocity to animator speed (0=idle, 0.5=walk, 1=run, 2=sprint)
                float walkThreshold = walkSpeed * 0.8f;
                float runThreshold = runSpeed * 0.8f;

                if (_smoothing.CurrentVelocityMagnitude < walkThreshold)
                {
                    normalizedSpeed = (_smoothing.CurrentVelocityMagnitude / walkSpeed) * 0.5f;
                }
                else if (_smoothing.CurrentVelocityMagnitude < runThreshold)
                {
                    normalizedSpeed = 0.5f + ((_smoothing.CurrentVelocityMagnitude - walkSpeed) / (runSpeed - walkSpeed)) * 0.5f;
                }
                else
                {
                    normalizedSpeed = 1f + ((_smoothing.CurrentVelocityMagnitude - runSpeed) / (sprintSpeed - runSpeed));
                }
            }

            _smoothing.CurrentAnimatorSpeed = Mathf.SmoothDamp(
                _smoothing.CurrentAnimatorSpeed,
                normalizedSpeed,
                ref _smoothing.AnimatorSpeedVelocity,
                animatorSmoothTime
            );
            _animationController.SetSpeed(_smoothing.CurrentAnimatorSpeed);

            // For unlocked movement, MoveY is always forward (1) when moving
            _smoothing.LocalMoveX = Mathf.SmoothDamp(_smoothing.LocalMoveX, 0f, ref _smoothing.MoveXVelocity, animatorSmoothTime);
            _smoothing.LocalMoveY = Mathf.SmoothDamp(
                _smoothing.LocalMoveY,
                moveInput.magnitude > 0.01f ? 1f : 0f,
                ref _smoothing.MoveYVelocity,
                animatorSmoothTime
            );
            _animationController.SetMoveDirection(_smoothing.LocalMoveX, _smoothing.LocalMoveY);

            // Momentum system parameters (ADR-005)
            _animationController.SetTurnAngle(_smoothing.TurnAngle);
            _animationController.SetVelocityMagnitude(_smoothing.CurrentVelocityMagnitude);

            // Turn type for cleaner animator transitions
            TurnType turnType = CalculateTurnType(_smoothing.TurnAngle);
            _animationController.SetTurnType((int)turnType);

            // Start/Stop animation parameters
            _animationController.SetIsAccelerating(_smoothing.IsAccelerating);
            _animationController.SetLocomotionMode((int)mode); // 0=Walk, 1=Run, 2=Sprint
            _animationController.SetWasMoving(_wasMovingLastFrame);

            // Track movement state for next frame (for stop detection)
            bool isMovingNow = _smoothing.CurrentVelocityMagnitude > 0.5f;
            _wasMovingLastFrame = isMovingNow;

            // Animation speed matching
            _animationController.UpdateAnimationSpeedMultiplier(_smoothing.CurrentVelocityMagnitude, mode);
        }

        private void UpdateAnimationParameters(Vector2 moveInput, LocomotionMode mode, bool isLockedOn)
        {
            if (_animationController == null) return;

            // Calculate velocity factor (0-1) based on current vs target speed
            // This syncs animation blend with actual movement velocity
            float velocityFactor = 0f;
            float targetSpeed = GetSpeedForMode(mode);
            if (targetSpeed > 0.01f)
            {
                velocityFactor = Mathf.Clamp01(_smoothing.CurrentVelocityMagnitude / targetSpeed);
            }

            // Normalize speed for animator: 0 = idle, 0.5 = walk, 1 = run, 2 = sprint
            // Scale by velocity factor so animation matches movement acceleration
            float normalizedSpeed = mode switch
            {
                LocomotionMode.Walk => velocityFactor * 0.5f,
                LocomotionMode.Run => velocityFactor,
                LocomotionMode.Sprint => velocityFactor * 2f,
                _ => velocityFactor
            };
            _smoothing.CurrentAnimatorSpeed = Mathf.SmoothDamp(
                _smoothing.CurrentAnimatorSpeed,
                normalizedSpeed,
                ref _smoothing.AnimatorSpeedVelocity,
                animatorSmoothTime
            );
            _animationController.SetSpeed(_smoothing.CurrentAnimatorSpeed);

            // Calculate local movement direction for lock-on animations
            // Scale by velocity factor so animation starts slow and accelerates with movement
            if (isLockedOn && _smoothing.SmoothedMoveDirection.sqrMagnitude > 0.01f)
            {
                float targetMoveX = moveInput.x * velocityFactor;
                float targetMoveY = moveInput.y * velocityFactor;
                _smoothing.LocalMoveX = Mathf.SmoothDamp(_smoothing.LocalMoveX, targetMoveX, ref _smoothing.MoveXVelocity, animatorSmoothTime);
                _smoothing.LocalMoveY = Mathf.SmoothDamp(_smoothing.LocalMoveY, targetMoveY, ref _smoothing.MoveYVelocity, animatorSmoothTime);
            }
            else if (isLockedOn)
            {
                _smoothing.LocalMoveX = Mathf.SmoothDamp(_smoothing.LocalMoveX, 0f, ref _smoothing.MoveXVelocity, moveDirectionDecayTime);
                _smoothing.LocalMoveY = Mathf.SmoothDamp(_smoothing.LocalMoveY, 0f, ref _smoothing.MoveYVelocity, moveDirectionDecayTime);
            }
            else
            {
                _smoothing.LocalMoveX = Mathf.SmoothDamp(_smoothing.LocalMoveX, 0f, ref _smoothing.MoveXVelocity, animatorSmoothTime);
                _smoothing.LocalMoveY = Mathf.SmoothDamp(_smoothing.LocalMoveY, 1f, ref _smoothing.MoveYVelocity, animatorSmoothTime);
            }
            _animationController.SetMoveDirection(_smoothing.LocalMoveX, _smoothing.LocalMoveY);

            // Calculate actual movement speed for animation speed matching
            float actualSpeed = 0f;
            if (_smoothing.SmoothedMoveDirection.sqrMagnitude > 0.01f)
            {
                float baseSpeed = mode switch
                {
                    LocomotionMode.Walk => walkSpeed,
                    LocomotionMode.Run => runSpeed,
                    LocomotionMode.Sprint => sprintSpeed,
                    _ => runSpeed
                };
                actualSpeed = baseSpeed * moveInput.magnitude;
            }
            _animationController.UpdateAnimationSpeedMultiplier(actualSpeed, mode);
        }

        private void RotateTowards(Vector3 direction, float speedMultiplier = 1f)
        {
            if (direction.sqrMagnitude < 0.01f) return;

            float rotationSpeed = _player.HealthPoiseController?.GetBaseStats().RotationSpeed ?? 360f;
            Quaternion targetRotation = Quaternion.LookRotation(direction);
            transform.rotation = Quaternion.RotateTowards(
                transform.rotation,
                targetRotation,
                rotationSpeed * speedMultiplier * Time.deltaTime
            );
        }

        #endregion

        #region Gravity

        // Note: Gravity is now handled in UpdateVelocity callback (KCC integration)
        // The _verticalVelocity field and VerticalVelocity property are kept for debugging

        #endregion

        #region Smoothing

        /// <summary>
        /// Handles decay of smoothing values when no input is present.
        /// </summary>
        private void UpdateSmoothingDecay()
        {
            // Check actual input to determine if we should decay
            Vector2 currentInput = InputHandler.Instance != null
                ? InputHandler.Instance.GetMoveInput()
                : Vector2.zero;
            bool hasNoInput = currentInput.sqrMagnitude <= 0.01f;

            // When no input, decay smoothedMoveDirection to zero
            if (hasNoInput)
            {
                _smoothing.SmoothedMoveDirection = Vector3.SmoothDamp(
                    _smoothing.SmoothedMoveDirection,
                    Vector3.zero,
                    ref _smoothing.MoveDirectionVelocity,
                    moveDirectionDecayTime
                );

                // Fast decay MoveX/MoveY to 0 when no input
                _smoothing.LocalMoveX = Mathf.SmoothDamp(_smoothing.LocalMoveX, 0f, ref _smoothing.MoveXVelocity, moveDirectionDecayTime);
                _smoothing.LocalMoveY = Mathf.SmoothDamp(_smoothing.LocalMoveY, 0f, ref _smoothing.MoveYVelocity, moveDirectionDecayTime);

                // Update animator via AnimationController
                _animationController?.SetMoveDirection(_smoothing.LocalMoveX, _smoothing.LocalMoveY);
            }
        }

        /// <summary>
        /// Reset animation speed multiplier to default (1.0).
        /// Call when entering states that don't use locomotion animations (combat, dodge, etc.).
        /// </summary>
        public void ResetAnimationSpeed()
        {
            _animationController?.ResetAnimationSpeed();
            _smoothing.CurrentAnimationSpeedMultiplier = 1f;
            _smoothing.AnimationSpeedMultiplierVelocity = 0f;
        }

        #endregion

        #region Input Validation

        /// <summary>
        /// Validates that a Vector2 does not contain NaN or Infinity values.
        /// Protects against corrupted input from hardware glitches or driver bugs.
        /// </summary>
        private static bool IsValidVector2(Vector2 v)
        {
            return !float.IsNaN(v.x) && !float.IsNaN(v.y)
                && !float.IsInfinity(v.x) && !float.IsInfinity(v.y);
        }

        /// <summary>
        /// Validates that a Vector3 does not contain NaN or Infinity values.
        /// Protects against corrupted calculations from invalid input propagation.
        /// </summary>
        private static bool IsValidVector3(Vector3 v)
        {
            return !float.IsNaN(v.x) && !float.IsNaN(v.y) && !float.IsNaN(v.z)
                && !float.IsInfinity(v.x) && !float.IsInfinity(v.y) && !float.IsInfinity(v.z);
        }

        #endregion

        #region Curve Helpers

        /// <summary>
        /// Creates a default acceleration curve optimized for 80% responsive feel.
        /// Fast initial acceleration (reaches 80% in first half), then eases to full speed.
        /// </summary>
        private static AnimationCurve CreateDefaultAccelerationCurve()
        {
            return new AnimationCurve(
                new Keyframe(0f, 0f, 0f, 4f),      // Start at 0, steep initial slope
                new Keyframe(0.3f, 0.8f, 1.5f, 1f), // 80% at 30% time
                new Keyframe(1f, 1f, 0.5f, 0f)     // Ease into full speed
            );
        }

        /// <summary>
        /// Creates a default deceleration curve for smooth stop with weight feel.
        /// Starts decelerating quickly, then eases out for natural stop.
        /// </summary>
        private static AnimationCurve CreateDefaultDecelerationCurve()
        {
            return new AnimationCurve(
                new Keyframe(0f, 1f, 0f, -2f),     // Start at full speed
                new Keyframe(0.5f, 0.3f, -1f, -0.5f), // Quick initial decel
                new Keyframe(1f, 0f, -0.5f, 0f)    // Ease to stop
            );
        }

        #endregion

        #region ICharacterController Implementation

        /// <summary>
        /// Called before the motor does anything. Validate intent, update forces, check turn-in-place.
        /// </summary>
        public void BeforeCharacterUpdate(float deltaTime)
        {
            // Phase 5.2+: Will validate cached intent and update external forces
        }

        /// <summary>
        /// Called when the motor wants to know what velocity should be.
        /// Core movement logic: momentum curves, lock-on, external forces, gravity.
        /// </summary>
        public void UpdateVelocity(ref Vector3 currentVelocity, float deltaTime)
        {
            // Calculate horizontal velocity from smoothing state
            Vector3 targetVelocity = CalculateGroundVelocity();

            // Apply velocity (XZ plane)
            currentVelocity.x = targetVelocity.x;
            currentVelocity.z = targetVelocity.z;

            // Apply gravity (Y axis)
            if (!_motor.GroundingStatus.IsStableOnGround)
            {
                currentVelocity.y += Gravity * deltaTime;
            }
            else
            {
                // Small downward force to stay grounded and trigger ground detection
                currentVelocity.y = -2f;
            }

            // Add external forces (knockback, explosions, environmental)
            if (_externalForces != null && _externalForces.HasActiveForces)
            {
                Vector3 externalForce = _externalForces.GetCurrentForce();
                currentVelocity += externalForce;
            }

            if (debugMode && targetVelocity.sqrMagnitude > 0.01f)
            {
                Debug.Log($"[KCC] UpdateVelocity: {targetVelocity.magnitude:F2} m/s, grounded: {_motor.GroundingStatus.IsStableOnGround}");
            }
        }

        /// <summary>
        /// Calculate ground velocity from current smoothing state.
        /// Consumes the pre-calculated values from ApplyMomentumMovement/ApplyLockedOnMovement.
        /// </summary>
        private Vector3 CalculateGroundVelocity()
        {
            // During turn-in-place, no horizontal movement
            if (_smoothing.IsTurningInPlace)
            {
                return Vector3.zero;
            }

            Vector3 baseVelocity = Vector3.zero;

            // Use smoothed direction and velocity magnitude calculated in Update
            if (_smoothing.CurrentVelocityMagnitude > 0.01f)
            {
                // Prefer SmoothedMoveDirection if it has magnitude
                if (_smoothing.SmoothedMoveDirection.sqrMagnitude > 0.01f)
                {
                    baseVelocity = _smoothing.SmoothedMoveDirection.normalized * _smoothing.CurrentVelocityMagnitude;
                }
                // Fall back to DecelerationDirection during deceleration (direction decayed but velocity remains)
                else if (_smoothing.DecelerationDirection.sqrMagnitude > 0.01f)
                {
                    baseVelocity = _smoothing.DecelerationDirection.normalized * _smoothing.CurrentVelocityMagnitude;
                }
            }

            // Add lock-on distance correction (maintains orbit radius during strafe)
            if (_smoothing.LockOnDistanceCorrection.sqrMagnitude > 0.01f)
            {
                baseVelocity += _smoothing.LockOnDistanceCorrection;
            }

            return baseVelocity;
        }

        /// <summary>
        /// Called when the motor wants to know what rotation should be.
        /// Syncs rotation calculated in Update to the motor's physics system.
        /// Rotation logic stays in Update for smooth interpolation at high FPS.
        /// </summary>
        public void UpdateRotation(ref Quaternion currentRotation, float deltaTime)
        {
            // Lock-on: face target
            if (_lockOnController != null && _lockOnController.IsLockedOn && _lockOnController.CurrentTarget != null)
            {
                Vector3 toTarget = _lockOnController.CurrentTarget.LockOnPoint - transform.position;
                toTarget.y = 0f;
                if (toTarget.sqrMagnitude > 0.01f)
                {
                    Quaternion targetRotation = Quaternion.LookRotation(toTarget.normalized);
                    currentRotation = Quaternion.Slerp(currentRotation, targetRotation, 10f * deltaTime);
                }
                return;
            }

            // Turn-in-place: rotate faster toward target direction
            if (_smoothing.IsTurningInPlace && _smoothing.TurnTargetDirection.sqrMagnitude > 0.01f)
            {
                Quaternion targetRot = Quaternion.LookRotation(_smoothing.TurnTargetDirection.normalized);

                // Turn-in-place uses faster rotation (2x normal speed)
                float turnInPlaceSpeed = 2f / Mathf.Max(characterRotationSmoothTime, 0.01f);
                currentRotation = Quaternion.Slerp(currentRotation, targetRot, turnInPlaceSpeed * deltaTime);
                return;
            }

            // Free movement: rotate toward movement direction
            Vector3 rotationTarget = _smoothing.SmoothedMoveDirection;
            if (rotationTarget.sqrMagnitude < 0.01f)
            {
                // No movement direction - keep current rotation
                return;
            }

            // Calculate target rotation from movement direction
            Quaternion targetRot2 = Quaternion.LookRotation(rotationTarget.normalized);

            // Smooth rotation using characterRotationSmoothTime
            // Faster rotation when more misaligned
            float angleDiff = Quaternion.Angle(currentRotation, targetRot2);
            float rotationMultiplier = 1f + (angleDiff / 90f) * (misalignedRotationMultiplier - 1f);
            float rotationSpeed = rotationMultiplier / Mathf.Max(characterRotationSmoothTime, 0.01f);

            currentRotation = Quaternion.Slerp(currentRotation, targetRot2, rotationSpeed * deltaTime);
        }

        /// <summary>
        /// Called after ground probing but before velocity/physics handling.
        /// Update animator parameters based on grounding state.
        /// </summary>
        public void PostGroundingUpdate(float deltaTime)
        {
            // Phase 5.3+: Will update animator parameters here
        }

        /// <summary>
        /// Called after the motor has finished everything.
        /// Invalidate intent, cleanup forces, reset per-frame state.
        /// </summary>
        public void AfterCharacterUpdate(float deltaTime)
        {
            // Invalidate movement intent - consumed for this frame
            _intent.Invalidate();
        }

        /// <summary>
        /// Determines if a collider should be considered for collisions.
        /// </summary>
        public bool IsColliderValidForCollisions(Collider coll)
        {
            // Accept all colliders by default
            // Future: Add layer filtering, trigger exclusion
            return true;
        }

        /// <summary>
        /// Called when ground probing detects a ground hit.
        /// </summary>
        public void OnGroundHit(Collider hitCollider, Vector3 hitNormal, Vector3 hitPoint,
            ref HitStabilityReport hitStabilityReport)
        {
            // Future: Footstep sounds, ground material detection
        }

        /// <summary>
        /// Called when movement logic detects a hit.
        /// </summary>
        public void OnMovementHit(Collider hitCollider, Vector3 hitNormal, Vector3 hitPoint,
            ref HitStabilityReport hitStabilityReport)
        {
            // Future: Wall slide, obstacle detection
        }

        /// <summary>
        /// Called after every move hit to allow modifying the stability report.
        /// </summary>
        public void ProcessHitStabilityReport(Collider hitCollider, Vector3 hitNormal, Vector3 hitPoint,
            Vector3 atCharacterPosition, Quaternion atCharacterRotation,
            ref HitStabilityReport hitStabilityReport)
        {
            // Future: Custom slope stability logic
        }

        /// <summary>
        /// Called when discrete collisions are detected (not from movement capsule casts).
        /// </summary>
        public void OnDiscreteCollisionDetected(Collider hitCollider)
        {
            // Future: Trigger interactions, damage zones
        }

        #endregion
    }
}
