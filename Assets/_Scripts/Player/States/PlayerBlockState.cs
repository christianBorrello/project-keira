using _Scripts.Combat.Data;
using _Scripts.Utilities;
using Systems;
using UnityEngine;

namespace _Scripts.Player.States
{
    /// <summary>
    /// Player block state - passive defense with integrated parry window.
    /// Inspired by Sekiro deflect system where blocking has an initial parry window.
    /// </summary>
    public class PlayerBlockState : BasePlayerState
    {
        // Configuration
        private readonly float _parryWindowDuration = 0.15f;
        private readonly float _perfectParryWindow = 0.08f;
        private readonly float _blockDamageReduction = 0.5f;
        private readonly float _blockStaminaDrainPerSecond = 10f;
        private readonly float _blockStaminaCostOnHit = 15f;
        private readonly float _blockMoveSpeedMultiplier = 0.4f; // 40% of normal speed

        // State tracking
        private bool _inParryWindow = true;
        private bool _inPerfectWindow = true;

        public override PlayerState StateType => PlayerState.Block;

        public override void Enter()
        {
            base.Enter();

            // Reset state
            _inParryWindow = true;
            _inPerfectWindow = true;

            // Set blocking and parrying flags
            controller?.SetBlocking(true);
            controller?.SetParrying(true);

            // Trigger block animation
            if (controller != null && controller.Animator != null)
            {
                controller.Animator.SetTriggerSafe("Block");
                controller.Animator.SetBoolSafe("IsBlocking", true);
            }
        }

        public override void Execute()
        {
            base.Execute();

            float stateTime = StateTime;

            // Perfect parry window closes first
            if (_inPerfectWindow && stateTime >= _perfectParryWindow)
            {
                _inPerfectWindow = false;
            }

            // Parry window closes after
            if (_inParryWindow && stateTime >= _parryWindowDuration)
            {
                _inParryWindow = false;
                controller?.SetParrying(false);
            }

            // Drain stamina while blocking (after parry window)
            if (!_inParryWindow && controller != null)
            {
                controller.ConsumeStamina(_blockStaminaDrainPerSecond * Time.deltaTime);

                // Check stamina - if depleted, exit block
                var runtimeData = controller.GetRuntimeData();
                if (runtimeData.CurrentStamina <= 0)
                {
                    ExitToAppropriateState();
                    return;
                }
            }

            // Handle movement while blocking (reduced speed)
            if (controller is not null && InputHandler.Instance is not null)
            {
                var moveInput = InputHandler.Instance.GetMoveInput();
                if (moveInput.sqrMagnitude > 0.01f)
                {
                    ApplyBlockMovement(moveInput);
                }

                // Update animator speed for movement blend
                if (controller.Animator is not null)
                {
                    float moveSpeed = moveInput.magnitude * _blockMoveSpeedMultiplier;
                    controller.Animator.SetFloatSafe("Speed", moveSpeed);
                }
            }
        }

        private void ApplyBlockMovement(Vector2 moveInput)
        {
            var cameraTransform = UnityEngine.Camera.main?.transform;
            if (cameraTransform == null || controller.Motor == null) return;

            // Get camera-relative direction
            Vector3 cameraForward = cameraTransform.forward;
            Vector3 cameraRight = cameraTransform.right;
            cameraForward.y = 0f;
            cameraRight.y = 0f;
            cameraForward.Normalize();
            cameraRight.Normalize();

            Vector3 moveDirection = (cameraForward * moveInput.y + cameraRight * moveInput.x).normalized;

            // Calculate reduced speed
            var stats = controller.GetBaseStats();
            float speed = stats.MoveSpeed * _blockMoveSpeedMultiplier;

            // Apply movement
            // TODO KCC Phase 5.5: Block movement should use MovementController.ApplyMovement
            Vector3 motion = Time.deltaTime * speed * moveDirection;
            controller.Motor.SetPositionAndRotation(
                controller.Motor.TransientPosition + motion,
                controller.Motor.TransientRotation
            );

            // Handle rotation
            if (controller.IsLockedOn && controller.CurrentTarget != null)
            {
                // Face target when locked on
                Vector3 toTarget = controller.CurrentTarget.LockOnPoint - controller.transform.position;
                toTarget.y = 0;
                if (toTarget.sqrMagnitude > 0.01f)
                {
                    Quaternion targetRotation = Quaternion.LookRotation(toTarget.normalized);
                    controller.transform.rotation = Quaternion.RotateTowards(
                        controller.transform.rotation,
                        targetRotation,
                        stats.RotationSpeed * Time.deltaTime
                    );
                }
            }
            else if (moveDirection.sqrMagnitude > 0.01f)
            {
                // Face movement direction when not locked on
                Quaternion targetRotation = Quaternion.LookRotation(moveDirection);
                controller.transform.rotation = Quaternion.RotateTowards(
                    controller.transform.rotation,
                    targetRotation,
                    stats.RotationSpeed * Time.deltaTime
                );
            }
        }

        public override void HandleInput(InputSnapshot input)
        {
            // Exit on button release
            if (!input.BlockHeld)
            {
                ExitToAppropriateState();
                return;
            }

            // Dodge cancel (always available during block)
            if (InputHandler.Instance is not null &&
                InputHandler.Instance.HasBufferedAction(InputAction.Dodge) &&
                CanDodge())
            {
                if (InputHandler.Instance.TryConsumeAction(InputAction.Dodge, out _))
                {
                    ChangeState(PlayerState.Dodge);
                }
            }
        }

        private void ExitToAppropriateState()
        {
            if (InputHandler.Instance is not null)
            {
                var input = InputHandler.Instance.CreateSnapshot();
                if (input.HasMoveInput)
                {
                    if (input.SprintHeld && CanSprint())
                    {
                        ChangeState(PlayerState.Sprint);
                    }
                    else if (input.WalkHeld)
                    {
                        ChangeState(PlayerState.Walk);
                    }
                    else
                    {
                        ChangeState(PlayerState.Run);
                    }
                    return;
                }
            }

            ChangeState(PlayerState.Idle);
        }

        protected override void OnStateComplete()
        {
            // Block doesn't have a fixed duration - it exits on button release
            // This method is only called if something forces completion
            ExitToAppropriateState();
        }

        public override void Exit()
        {
            base.Exit();

            // Clear blocking and parrying flags
            controller?.SetBlocking(false);
            controller?.SetParrying(false);

            // Clear animation
            if (controller != null && controller.Animator != null)
            {
                controller.Animator.SetBoolSafe("IsBlocking", false);
            }
        }

        public override bool CanTransitionTo(PlayerState targetState, InputAction action = InputAction.None)
        {
            switch (targetState)
            {
                case PlayerState.Dodge:
                    // Can always dodge out of block
                    return true;

                case PlayerState.Stagger:
                case PlayerState.Death:
                    // Interrupt states always allowed
                    return true;

                case PlayerState.Idle:
                case PlayerState.Walk:
                case PlayerState.Sprint:
                    // Can exit anytime (on button release)
                    return true;

                case PlayerState.LightAttack:
                case PlayerState.HeavyAttack:
                    // Can attack out of block
                    return true;

                default:
                    return false;
            }
        }

        /// <summary>
        /// Whether currently in parry window.
        /// </summary>
        public bool IsInParryWindow => _inParryWindow;

        /// <summary>
        /// Whether currently in perfect parry window.
        /// </summary>
        public bool IsInPerfectWindow => _inPerfectWindow;

        /// <summary>
        /// Block damage reduction multiplier (0.5 = 50% reduction).
        /// </summary>
        public float BlockDamageReduction => _blockDamageReduction;

        /// <summary>
        /// Stamina cost when hit while blocking.
        /// </summary>
        public float BlockStaminaCostOnHit => _blockStaminaCostOnHit;
    }
}
