using _Scripts.Camera;
using _Scripts.Player.Data;
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
    /// Manages player movement, gravity, rotation, and smoothing.
    /// Handles camera-relative movement with lock-on orbital support.
    /// </summary>
    [RequireComponent(typeof(PlayerController))]
    [RequireComponent(typeof(CharacterController))]
    public class MovementController : MonoBehaviour
    {
        // References
        private PlayerController _player;
        private CharacterController _characterController;
        private AnimationController _animationController;
        private LockOnController _lockOnController;
        private Transform _cameraTransform;

        // Gravity
        private float _verticalVelocity;
        private const float Gravity = -20f;

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
        [Tooltip("Time in seconds to reach full speed from standstill")]
        private float accelerationDuration = 0.2f;

        [SerializeField]
        [Range(0.05f, 0.5f)]
        [Tooltip("Time in seconds to stop from full speed")]
        private float decelerationDuration = 0.15f;

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
        [Range(0.1f, 2f)]
        [Tooltip("Speed threshold below which character is considered stationary for turn-in-place")]
        private float turnInPlaceSpeedThreshold = 0.5f;

        [Header("Debug")]
        [SerializeField]
        private bool debugMode;

        #region Properties

        /// <summary>
        /// Whether the character is grounded.
        /// </summary>
        public bool IsGrounded => _characterController?.isGrounded ?? false;

        /// <summary>
        /// Current vertical velocity (gravity).
        /// </summary>
        public float VerticalVelocity => _verticalVelocity;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            _player = GetComponent<PlayerController>();
            _characterController = GetComponent<CharacterController>();
        }

        private void Start()
        {
            _animationController = _player.AnimationController;
            _lockOnController = _player.LockOnController;

            // Auto-find camera if not assigned (prefer ThirdPersonCameraSystem)
            if (_cameraTransform == null)
            {
                _cameraTransform = ThirdPersonCameraSystem.Instance?.transform
                    ?? UnityEngine.Camera.main?.transform;
            }
        }

        private void Update()
        {
            ApplyGravity();
            UpdateSmoothingDecay();
        }

        #endregion

        #region Movement

        /// <summary>
        /// Apply movement based on input. Called by states via PlayerController.
        /// Branches between lock-on movement (existing) and momentum-based movement (new).
        /// </summary>
        public void ApplyMovement(Vector2 moveInput, LocomotionMode mode)
        {
            if (_characterController == null || _cameraTransform == null)
                return;

            bool isLockedOn = _lockOnController?.IsLockedOn ?? false;

            if (isLockedOn)
            {
                // Use existing lock-on movement logic (preserved)
                ApplyLockedOnMovement(moveInput, mode);
            }
            else
            {
                // Use new momentum-based movement
                ApplyMomentumMovement(moveInput, mode);
            }
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

                // During turn-in-place: only apply gravity, no locomotion
                Vector3 gravityMotion = Vector3.zero;
                gravityMotion.y = _verticalVelocity * Time.deltaTime;
                _characterController.Move(gravityMotion);

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

            // Apply rotation toward movement direction
            if (hasInput || _smoothing.SmoothedMoveDirection.sqrMagnitude > 0.01f)
            {
                ApplyMomentumRotation(targetDirection, turnAngle);
            }

            // Apply movement
            if (_smoothing.CurrentVelocityMagnitude > 0.01f && _smoothing.SmoothedMoveDirection.sqrMagnitude > 0.01f)
            {
                Vector3 motion = _smoothing.CurrentVelocityMagnitude * Time.deltaTime * _smoothing.SmoothedMoveDirection.normalized;
                motion.y = _verticalVelocity * Time.deltaTime;
                _characterController.Move(motion);
            }
            else
            {
                // Still apply gravity when not moving
                Vector3 motion = Vector3.zero;
                motion.y = _verticalVelocity * Time.deltaTime;
                _characterController.Move(motion);
            }

            // Update animation parameters
            UpdateMomentumAnimationParameters(moveInput, mode);
        }

        /// <summary>
        /// Original lock-on movement logic (preserved for compatibility).
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

            // Use SmoothDamp for smooth direction changes
            _smoothing.SmoothedMoveDirection = Vector3.SmoothDamp(
                _smoothing.SmoothedMoveDirection,
                targetDirection,
                ref _smoothing.MoveDirectionVelocity,
                movementSmoothTime
            );

            if (_smoothing.SmoothedMoveDirection.sqrMagnitude > 0.01f)
            {
                float speed = GetSpeedForMode(mode);

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

                // Apply movement
                Vector3 motion = Time.deltaTime * speed * _smoothing.SmoothedMoveDirection;
                motion.y = _verticalVelocity * Time.deltaTime;
                _characterController.Move(motion);

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
                        Vector3 correctedPosition = currentTarget.LockOnPoint - toTargetNorm * _lockOnController.LockedOnDistance;
                        correctedPosition.y = transform.position.y;
                        Vector3 correction = correctedPosition - transform.position;
                        _characterController.Move(correction);
                    }
                    else
                    {
                        _lockOnController.SetLockedOnDistance(currentDistance);
                    }
                }
            }
            else
            {
                Vector3 motion = Vector3.zero;
                motion.y = _verticalVelocity * Time.deltaTime;
                _characterController.Move(motion);
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
            if (Mathf.Abs(currentTurnAngle) < 10f)
            {
                ExitTurnInPlace();
                return;
            }

            // Update turn progress (0-1 based on remaining angle)
            float startAngle = CalculateTurnAngle(transform.forward, _smoothing.TurnTargetDirection);
            _smoothing.TurnProgress = 1f - Mathf.Clamp01(Mathf.Abs(currentTurnAngle) / Mathf.Max(Mathf.Abs(startAngle), 1f));

            // Update animator with current turn angle for animation selection
            _smoothing.TurnAngle = currentTurnAngle;

            // Apply rotation (faster than normal movement rotation)
            float turnRotationMultiplier = 2f; // Turn-in-place rotates faster
            float adjustedSmoothTime = characterRotationSmoothTime / turnRotationMultiplier;
            float targetAngle = Mathf.Atan2(targetDirection.x, targetDirection.z) * Mathf.Rad2Deg;

            float smoothedAngle = Mathf.SmoothDampAngle(
                transform.eulerAngles.y,
                targetAngle,
                ref _smoothing.RotationVelocity,
                adjustedSmoothTime
            );
            transform.rotation = Quaternion.Euler(0f, smoothedAngle, 0f);
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
                }
                _smoothing.AccelerationTimer += Time.deltaTime;
                _smoothing.DecelerationTimer = 0f;
            }
            else
            {
                if (_smoothing.IsAccelerating)
                {
                    // Just stopped - reset deceleration timer
                    _smoothing.DecelerationTimer = 0f;
                    _smoothing.IsAccelerating = false;
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

            // Animation speed matching
            _animationController.UpdateAnimationSpeedMultiplier(_smoothing.CurrentVelocityMagnitude, mode);
        }

        private void UpdateAnimationParameters(Vector2 moveInput, LocomotionMode mode, bool isLockedOn)
        {
            if (_animationController == null) return;

            // Normalize speed for animator: 0 = idle, 0.5 = walk, 1 = run, 2 = sprint
            float normalizedSpeed = mode switch
            {
                LocomotionMode.Walk => moveInput.magnitude * 0.5f,
                LocomotionMode.Run => moveInput.magnitude,
                LocomotionMode.Sprint => moveInput.magnitude * 2f,
                _ => moveInput.magnitude
            };
            _smoothing.CurrentAnimatorSpeed = Mathf.SmoothDamp(
                _smoothing.CurrentAnimatorSpeed,
                normalizedSpeed,
                ref _smoothing.AnimatorSpeedVelocity,
                animatorSmoothTime
            );
            _animationController.SetSpeed(_smoothing.CurrentAnimatorSpeed);

            // Calculate local movement direction for lock-on animations
            if (isLockedOn && _smoothing.SmoothedMoveDirection.sqrMagnitude > 0.01f)
            {
                _smoothing.LocalMoveX = Mathf.SmoothDamp(_smoothing.LocalMoveX, moveInput.x, ref _smoothing.MoveXVelocity, animatorSmoothTime);
                _smoothing.LocalMoveY = Mathf.SmoothDamp(_smoothing.LocalMoveY, moveInput.y, ref _smoothing.MoveYVelocity, animatorSmoothTime);
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

        private void ApplyGravity()
        {
            if (_characterController.isGrounded)
            {
                _verticalVelocity = -2f; // Small downward force to keep grounded
            }
            else
            {
                _verticalVelocity += Gravity * Time.deltaTime;
            }
        }

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
    }
}
