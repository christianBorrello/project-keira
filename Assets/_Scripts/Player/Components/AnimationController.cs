using _Scripts.Player.Data;
using _Scripts.Utilities;
using UnityEngine;

namespace _Scripts.Player.Components
{
    /// <summary>
    /// Handles all animator interactions for the player.
    /// Centralizes parameter setting, layer management, and animation speed matching.
    /// </summary>
    [RequireComponent(typeof(PlayerController))]
    public class AnimationController : MonoBehaviour
    {
        // Static hash cache - computed once at class load, shared by all instances
        private static readonly int SpeedHash = Animator.StringToHash("Speed");
        private static readonly int MoveXHash = Animator.StringToHash("MoveX");
        private static readonly int MoveYHash = Animator.StringToHash("MoveY");
        private static readonly int IsLockedOnHash = Animator.StringToHash("IsLockedOn");

        // Momentum system parameters (ADR-005: additive, non-breaking)
        private static readonly int TurnAngleHash = Animator.StringToHash("TurnAngle");
        private static readonly int VelocityMagnitudeHash = Animator.StringToHash("VelocityMagnitude");

        // Start/Stop animation parameters
        private static readonly int IsAcceleratingHash = Animator.StringToHash("IsAccelerating");
        private static readonly int LocomotionModeHash = Animator.StringToHash("LocomotionMode");
        private static readonly int WasMovingHash = Animator.StringToHash("WasMoving");

        // Turn type parameter (cleaner than checking TurnAngle ranges in animator)
        private static readonly int TurnTypeHash = Animator.StringToHash("TurnType");

        // Layer indices
        private const int LockedLocomotionLayerIndex = 1;
        private const float LayerTransitionSpeed = 5f;

        [Header("Animation Speed Matching")]
        [SerializeField]
        [Tooltip("Natural speed of walk animation in units/second")]
        private float walkAnimationSpeed = 1.4f;

        [SerializeField]
        [Tooltip("Natural speed of run animation in units/second")]
        private float runAnimationSpeed = 5.0f;

        [SerializeField]
        [Tooltip("Natural speed of sprint animation in units/second")]
        private float sprintAnimationSpeed = 6.5f;

        [SerializeField]
        [Range(0.5f, 2.0f)]
        [Tooltip("Minimum animation speed multiplier")]
        private float minSpeedMultiplier = 0.5f;

        [SerializeField]
        [Range(0.5f, 2.0f)]
        [Tooltip("Maximum animation speed multiplier")]
        private float maxSpeedMultiplier = 2.0f;

        [SerializeField]
        [Tooltip("Smoothing time for animation speed changes")]
        private float speedSmoothTime = 0.1f;

        [Header("Locked-On Animation Blending")]
        [SerializeField]
        [Range(0.05f, 0.5f)]
        [Tooltip("Blend time for MoveX/MoveY transitions (jog to idle and vice versa)")]
        private float moveDirectionDampTime = 0.15f;

        // References
        private PlayerController _player;
        private Animator _animator;
        private MovementController _movementController;

        // Animation speed smoothing (local to this component)
        private float _currentSpeedMultiplier = 1f;
        private float _speedMultiplierVelocity;

        // Root motion state for turn-in-place (ADR-003)
        private bool _applyRootRotation;

        /// <summary>
        /// The controlled Animator component.
        /// </summary>
        public Animator Animator => _animator;

        private void Awake()
        {
            _player = GetComponent<PlayerController>();
            _animator = GetComponentInChildren<Animator>();
            _movementController = GetComponent<MovementController>();

            if (_animator != null)
            {
                // Keep root motion disabled by default - we handle it manually in OnAnimatorMove
                _animator.applyRootMotion = false;
            }
        }

        /// <summary>
        /// OnAnimatorMove handles partial root motion for turn-in-place animations.
        /// Per ADR-003: Apply rotation from animation, ignore position.
        /// </summary>
        private void OnAnimatorMove()
        {
            if (_animator == null || !_applyRootRotation) return;

            // Apply rotation from animation (gives visual weight to turns)
            transform.rotation = _animator.rootRotation;

            // Position is ignored - MovementController handles all translation
        }

        /// <summary>
        /// Enable root motion rotation for turn-in-place animations.
        /// Call this when entering a turn animation state.
        /// </summary>
        public void EnableRootRotation()
        {
            _applyRootRotation = true;
        }

        /// <summary>
        /// Disable root motion rotation.
        /// Call this when exiting turn animation or entering other states.
        /// </summary>
        public void DisableRootRotation()
        {
            _applyRootRotation = false;
        }

        /// <summary>
        /// LateUpdate handles layer weight transitions.
        /// Using LateUpdate ensures animation state is updated after all movement logic.
        /// </summary>
        private void LateUpdate()
        {
            if (_animator == null) return;
            UpdateLockedLocomotionLayer();
        }

        /// <summary>
        /// Updates the locked locomotion layer weight based on lock-on state.
        /// </summary>
        private void UpdateLockedLocomotionLayer()
        {
            bool isLockedOn = _player.IsLockedOn;

            // Update IsLockedOn parameter using cached hash
            _animator.SetBool(IsLockedOnHash, isLockedOn);

            // Smoothly transition layer weight
            float targetWeight = isLockedOn ? 1f : 0f;
            float currentWeight = _animator.GetLayerWeight(LockedLocomotionLayerIndex);
            _animator.SetLayerWeight(
                LockedLocomotionLayerIndex,
                Mathf.MoveTowards(currentWeight, targetWeight, Time.deltaTime * LayerTransitionSpeed)
            );
        }

        /// <summary>
        /// Set the Speed parameter for locomotion blending.
        /// Uses damping for smooth transitions between locomotion states.
        /// </summary>
        /// <param name="normalizedSpeed">0=idle, 0.5=walk, 1=run, 2=sprint</param>
        public void SetSpeed(float normalizedSpeed)
        {
            if (_animator == null) return;
            // Use damped SetFloat for smooth blend tree transitions
            _animator.SetFloat(SpeedHash, normalizedSpeed, moveDirectionDampTime, Time.deltaTime);
        }

        /// <summary>
        /// Set the Speed parameter immediately without damping.
        /// Use for instant state changes (e.g., teleporting, respawning).
        /// </summary>
        public void SetSpeedImmediate(float normalizedSpeed)
        {
            if (_animator == null) return;
            _animator.SetFloat(SpeedHash, normalizedSpeed);
        }

        /// <summary>
        /// Set the directional movement parameters for strafe animations.
        /// Uses Animator's built-in damping for smooth blend transitions between jog and idle.
        /// </summary>
        /// <param name="moveX">-1=left, 0=neutral, 1=right</param>
        /// <param name="moveY">-1=back, 0=neutral, 1=forward</param>
        public void SetMoveDirection(float moveX, float moveY)
        {
            if (_animator == null) return;
            // Use damped SetFloat for smooth transitions in the blend tree
            _animator.SetFloat(MoveXHash, moveX, moveDirectionDampTime, Time.deltaTime);
            _animator.SetFloat(MoveYHash, moveY, moveDirectionDampTime, Time.deltaTime);
        }

        /// <summary>
        /// Set the directional movement parameters immediately without damping.
        /// Use for instant state changes (e.g., teleporting, respawning).
        /// </summary>
        public void SetMoveDirectionImmediate(float moveX, float moveY)
        {
            if (_animator == null) return;
            _animator.SetFloat(MoveXHash, moveX);
            _animator.SetFloat(MoveYHash, moveY);
        }

        /// <summary>
        /// Set IsLockedOn state (also handled automatically in LateUpdate).
        /// </summary>
        public void SetLockedOn(bool isLockedOn)
        {
            if (_animator == null) return;
            _animator.SetBool(IsLockedOnHash, isLockedOn);
        }

        /// <summary>
        /// Set the turn angle for turn-in-place animation triggers.
        /// Used by animator transitions to detect when turn animations should play.
        /// </summary>
        /// <param name="turnAngle">Angle in degrees (-180 to 180). Negative = left turn, positive = right turn</param>
        public void SetTurnAngle(float turnAngle)
        {
            if (_animator == null) return;
            _animator.SetFloat(TurnAngleHash, turnAngle);
        }

        /// <summary>
        /// Set the actual velocity magnitude for animator transition conditions.
        /// Useful for triggering start/stop animations based on real movement speed.
        /// </summary>
        /// <param name="velocityMagnitude">Current movement speed in units/second</param>
        public void SetVelocityMagnitude(float velocityMagnitude)
        {
            if (_animator == null) return;
            _animator.SetFloat(VelocityMagnitudeHash, velocityMagnitude);
        }

        /// <summary>
        /// Set whether the player is currently accelerating (has input).
        /// Used to distinguish start vs stop animations.
        /// </summary>
        public void SetIsAccelerating(bool isAccelerating)
        {
            if (_animator == null) return;
            _animator.SetBool(IsAcceleratingHash, isAccelerating);
        }

        /// <summary>
        /// Set the current locomotion mode for animation selection.
        /// </summary>
        /// <param name="mode">0 = Walk, 1 = Run, 2 = Sprint</param>
        public void SetLocomotionMode(int mode)
        {
            if (_animator == null) return;
            _animator.SetInteger(LocomotionModeHash, mode);
        }

        /// <summary>
        /// Set whether the player was moving (for stop animation detection).
        /// This tracks the previous frame's movement state.
        /// </summary>
        public void SetWasMoving(bool wasMoving)
        {
            if (_animator == null) return;
            _animator.SetBool(WasMovingHash, wasMoving);
        }

        /// <summary>
        /// Set the turn type for cleaner animator transitions.
        /// </summary>
        /// <param name="turnType">0=None, 1=Turn90Left, 2=Turn90Right, 3=Turn180</param>
        public void SetTurnType(int turnType)
        {
            if (_animator == null) return;
            _animator.SetInteger(TurnTypeHash, turnType);
        }

        /// <summary>
        /// Calculate and apply animation speed multiplier to match movement speed.
        /// Prevents foot sliding by synchronizing animation playback rate with actual velocity.
        /// </summary>
        /// <param name="actualSpeed">Current movement speed in units/second</param>
        /// <param name="mode">Current locomotion mode</param>
        public void UpdateAnimationSpeedMultiplier(float actualSpeed, LocomotionMode mode)
        {
            if (_animator == null) return;

            // Handle idle/near-idle: reset to normal speed
            if (actualSpeed < 0.1f)
            {
                _currentSpeedMultiplier = Mathf.SmoothDamp(
                    _currentSpeedMultiplier,
                    1f,
                    ref _speedMultiplierVelocity,
                    speedSmoothTime
                );
                _animator.speed = _currentSpeedMultiplier;
                return;
            }

            // Get the natural animation speed for current mode
            float animationNaturalSpeed = mode switch
            {
                LocomotionMode.Walk => walkAnimationSpeed,
                LocomotionMode.Run => runAnimationSpeed,
                LocomotionMode.Sprint => sprintAnimationSpeed,
                _ => runAnimationSpeed
            };

            // Calculate target multiplier: actual/natural
            float rawMultiplier = actualSpeed / animationNaturalSpeed;

            // Clamp to reasonable range
            float clampedMultiplier = Mathf.Clamp(rawMultiplier, minSpeedMultiplier, maxSpeedMultiplier);

            // Smooth the transition
            _currentSpeedMultiplier = Mathf.SmoothDamp(
                _currentSpeedMultiplier,
                clampedMultiplier,
                ref _speedMultiplierVelocity,
                speedSmoothTime
            );

            _animator.speed = _currentSpeedMultiplier;
        }

        /// <summary>
        /// Reset animation speed multiplier to default (1.0).
        /// Call when entering states that don't use locomotion animations.
        /// </summary>
        public void ResetAnimationSpeed()
        {
            if (_animator != null)
            {
                _animator.speed = 1f;
            }
            _currentSpeedMultiplier = 1f;
            _speedMultiplierVelocity = 0f;
        }

        /// <summary>
        /// Play a trigger animation (e.g., attacks, dodges).
        /// </summary>
        public void PlayTrigger(string triggerName)
        {
            _animator?.SetTriggerSafe(triggerName);
        }

        /// <summary>
        /// Play a trigger animation using pre-computed hash.
        /// </summary>
        public void PlayTrigger(int triggerHash)
        {
            _animator?.SetTrigger(triggerHash);
        }

        /// <summary>
        /// Set a bool parameter.
        /// </summary>
        public void SetBool(string paramName, bool value)
        {
            _animator?.SetBoolSafe(paramName, value);
        }

        /// <summary>
        /// Set a float parameter.
        /// </summary>
        public void SetFloat(string paramName, float value)
        {
            _animator?.SetFloatSafe(paramName, value);
        }

        /// <summary>
        /// Set an integer parameter.
        /// </summary>
        public void SetInteger(string paramName, int value)
        {
            _animator?.SetIntegerSafe(paramName, value);
        }

        /// <summary>
        /// Get the current animation state info for a layer.
        /// </summary>
        public AnimatorStateInfo GetCurrentAnimatorStateInfo(int layerIndex = 0)
        {
            return _animator != null
                ? _animator.GetCurrentAnimatorStateInfo(layerIndex)
                : default;
        }

        /// <summary>
        /// Check if animator is in a specific state.
        /// </summary>
        public bool IsInState(int stateNameHash, int layerIndex = 0)
        {
            if (_animator == null) return false;
            return _animator.GetCurrentAnimatorStateInfo(layerIndex).shortNameHash == stateNameHash;
        }
    }
}
