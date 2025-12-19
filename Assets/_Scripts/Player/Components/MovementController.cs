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
        /// Uses rotation-first approach: reduces movement when turning significantly.
        /// </summary>
        public void ApplyMovement(Vector2 moveInput, LocomotionMode mode)
        {
            if (_characterController == null || _cameraTransform == null)
                return;

            // Get camera-relative movement direction
            Vector3 cameraForward = _cameraTransform.forward;
            Vector3 cameraRight = _cameraTransform.right;
            cameraForward.y = 0f;
            cameraRight.y = 0f;
            cameraForward.Normalize();
            cameraRight.Normalize();

            Vector3 targetDirection = (cameraForward * moveInput.y + cameraRight * moveInput.x);

            // Normalize target if there's input
            if (targetDirection.sqrMagnitude > 0.01f)
            {
                targetDirection.Normalize();
            }
            else
            {
                targetDirection = Vector3.zero;
            }

            // Convert to target-relative direction when locked on (orbital movement)
            bool isLockedOn = _lockOnController?.IsLockedOn ?? false;
            var currentTarget = _lockOnController?.CurrentTarget;

            if (isLockedOn && currentTarget != null && targetDirection.sqrMagnitude > 0.01f)
            {
                // Calculate target-relative axes
                Vector3 toTarget = (currentTarget.LockOnPoint - transform.position);
                toTarget.y = 0;
                toTarget.Normalize();

                Vector3 orbitRight = Vector3.Cross(Vector3.up, toTarget).normalized;

                // Project camera-relative input onto target-relative axes
                float strafeComponent = Vector3.Dot(targetDirection, orbitRight);
                float approachComponent = Vector3.Dot(targetDirection, toTarget);

                // Save for animation blending
                _smoothing.TargetRelativeStrafe = strafeComponent;
                _smoothing.TargetRelativeApproach = approachComponent;

                // Reconstruct direction for orbital movement
                targetDirection = (orbitRight * strafeComponent) + (toTarget * approachComponent);
                if (targetDirection.sqrMagnitude > 0.01f)
                {
                    targetDirection.Normalize();
                }
            }

            // Use SmoothDamp for buttery smooth direction changes
            _smoothing.SmoothedMoveDirection = Vector3.SmoothDamp(
                _smoothing.SmoothedMoveDirection,
                targetDirection,
                ref _smoothing.MoveDirectionVelocity,
                movementSmoothTime
            );

            if (_smoothing.SmoothedMoveDirection.sqrMagnitude > 0.01f)
            {
                // Calculate base speed based on locomotion mode
                float speed = mode switch
                {
                    LocomotionMode.Walk => walkSpeed,
                    LocomotionMode.Run => runSpeed,
                    LocomotionMode.Sprint => sprintSpeed,
                    _ => runSpeed
                };

                // Proportional movement: intensity based on alignment with movement direction
                // Uses exponential falloff for responsive pivot-in-place behavior
                if (!isLockedOn)
                {
                    // Calculate alignment with movement direction for speed reduction
                    Vector3 currentForward = transform.forward;
                    float dotProduct = Vector3.Dot(currentForward, targetDirection);
                    float alignment = (dotProduct + 1f) * 0.5f; // Map from [-1,1] to [0,1]

                    // Apply exponential falloff: higher exponent = sharper curve = more pivot-in-place
                    float alignmentFactor = Mathf.Pow(alignment, alignmentFalloffExponent);

                    // Scale movement speed by alignment factor
                    speed *= alignmentFactor;

                    // Smooth rotation toward movement direction (camera-relative)
                    float targetAngle = Mathf.Atan2(targetDirection.x, targetDirection.z) * Mathf.Rad2Deg;
                    float smoothedAngle = Mathf.SmoothDampAngle(
                        transform.eulerAngles.y,
                        targetAngle,
                        ref _smoothing.RotationVelocity,
                        characterRotationSmoothTime
                    );
                    transform.rotation = Quaternion.Euler(0f, smoothedAngle, 0f);
                }
                else if (currentTarget != null)
                {
                    // Face target when locked on (no speed reduction)
                    Vector3 toTarget = currentTarget.LockOnPoint - transform.position;
                    toTarget.y = 0;
                    if (toTarget.sqrMagnitude > 0.01f)
                    {
                        RotateTowards(toTarget.normalized);
                    }
                }

                // Apply movement using smoothed direction
                Vector3 motion = Time.deltaTime * speed * _smoothing.SmoothedMoveDirection;
                motion.y = _verticalVelocity * Time.deltaTime;
                _characterController.Move(motion);

                // Maintain distance from target during pure strafe (orbital movement)
                if (isLockedOn && currentTarget != null && _lockOnController != null)
                {
                    Vector3 toTarget = currentTarget.LockOnPoint - transform.position;
                    toTarget.y = 0;
                    float currentDistance = toTarget.magnitude;

                    // Check if input is pure strafe (no forward/back component)
                    Vector3 toTargetNorm = toTarget.normalized;
                    float approachComponent = Mathf.Abs(Vector3.Dot(_smoothing.SmoothedMoveDirection, toTargetNorm));

                    // If mostly strafing (approach < 0.3), correct distance
                    if (approachComponent < 0.3f && currentDistance > 0.1f)
                    {
                        // Reposition to maintain locked distance
                        Vector3 correctedPosition = currentTarget.LockOnPoint - toTargetNorm * _lockOnController.LockedOnDistance;
                        correctedPosition.y = transform.position.y;

                        Vector3 correction = correctedPosition - transform.position;
                        _characterController.Move(correction);
                    }
                    else
                    {
                        // Update locked distance when approaching/retreating
                        _lockOnController.SetLockedOnDistance(currentDistance);
                    }
                }
            }
            else
            {
                // Still apply gravity when not moving
                Vector3 motion = Vector3.zero;
                motion.y = _verticalVelocity * Time.deltaTime;
                _characterController.Move(motion);
            }

            // Update animation parameters via AnimationController
            UpdateAnimationParameters(moveInput, mode, isLockedOn);
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
    }
}
