using _Scripts.Combat.Data;
using _Scripts.Utilities;
using Systems;
using UnityEngine;

namespace _Scripts.Player.States
{
    /// <summary>
    /// Player dodge state - evasive action with invulnerability frames.
    /// Supports directional rolls and backsteps.
    /// </summary>
    public class PlayerDodgeState : BasePlayerState
    {
        public enum DodgeType
        {
            Roll,
            Backstep,
            Sidestep
        }

        // Configuration with defaults
        private readonly float _dodgeDuration = 0.6f;
        private readonly float _dodgeDistance = 3f;
        private readonly float _backstepDistance = 2f;
        private readonly float _sidestepDistance = 2.5f;
        private readonly float _iFrameStart = 0.05f;
        private readonly float _iFrameEnd = 0.4f;
        private readonly float _recoveryWindowStart = 0.7f;
        private readonly bool _allowDodgeChain = true;

        // State tracking
        private DodgeType _currentDodgeType;
        private Vector3 _dodgeDirection;
        private float _currentDodgeDistance;
        private bool _isInvulnerable;
        private bool _inRecoveryWindow;
        private float _dodgeProgress;

        private static readonly AnimationCurve DodgeCurve = new AnimationCurve(
            new Keyframe(0f, 0f, 0f, 3f),
            new Keyframe(0.3f, 0.7f, 1f, 1f),
            new Keyframe(1f, 1f, 0f, 0f)
        );

        public override PlayerState StateType => PlayerState.Dodge;

        public override void Enter()
        {
            base.Enter();

            stateDuration = _dodgeDuration;

            if (controller != null)
            {
                var stats = controller.GetBaseStats();
                controller.ConsumeStamina(stats.DodgeStaminaCost);
            }

            DetermineDodgeType();

            _isInvulnerable = false;
            _inRecoveryWindow = false;
            _dodgeProgress = 0f;

            // Set animator parameters (will be used when animations are added)
            if (controller != null && controller.Animator != null && controller.Animator.runtimeAnimatorController != null)
            {
                controller.Animator.SetTriggerSafe("Dodge");
                controller.Animator.SetIntegerSafe("DodgeType", (int)_currentDodgeType);
                controller.Animator.SetFloatSafe("DodgeDirectionX", _dodgeDirection.x);
                controller.Animator.SetFloatSafe("DodgeDirectionZ", _dodgeDirection.z);
            }
        }

        private void DetermineDodgeType()
        {
            Vector2 moveInput = Vector2.zero;
            if (InputHandler.Instance != null)
            {
                moveInput = InputHandler.Instance.GetMoveInput();
            }

            bool isLockedOn = controller?.IsLockedOn ?? false;

            if (moveInput.sqrMagnitude < 0.1f)
            {
                _currentDodgeType = DodgeType.Backstep;
                _dodgeDirection = -(controller?.transform.forward ?? Vector3.back);
                _currentDodgeDistance = _backstepDistance;
            }
            else if (isLockedOn)
            {
                float absX = Mathf.Abs(moveInput.x);
                float absY = Mathf.Abs(moveInput.y);

                if (absX > absY * 1.5f)
                {
                    _currentDodgeType = DodgeType.Sidestep;
                    _dodgeDirection = GetCameraRelativeDirection(new Vector2(moveInput.x, 0)).normalized;
                    _currentDodgeDistance = _sidestepDistance;
                }
                else
                {
                    _currentDodgeType = DodgeType.Roll;
                    _dodgeDirection = GetCameraRelativeDirection(moveInput).normalized;
                    _currentDodgeDistance = _dodgeDistance;
                }
            }
            else
            {
                _currentDodgeType = DodgeType.Roll;
                _dodgeDirection = GetCameraRelativeDirection(moveInput).normalized;
                _currentDodgeDistance = _dodgeDistance;
            }

            _dodgeDirection.y = 0;
            _dodgeDirection.Normalize();
        }

        private Vector3 GetCameraRelativeDirection(Vector2 input)
        {
            UnityEngine.Camera cam = UnityEngine.Camera.main;
            if (cam is null)
            {
                return new Vector3(input.x, 0, input.y);
            }

            Vector3 cameraForward = cam.transform.forward;
            Vector3 cameraRight = cam.transform.right;
            cameraForward.y = 0;
            cameraRight.y = 0;
            cameraForward.Normalize();
            cameraRight.Normalize();

            return (cameraForward * input.y + cameraRight * input.x);
        }

        public override void Execute()
        {
            // IMPORTANT: Apply movement BEFORE base.Execute() because base.Execute()
            // can trigger state change, which resets StateTime to 0, causing
            // NormalizedTime to return 0 and resulting in negative frameDelta

            float normalizedTime = NormalizedTime;

            // Apply movement first
            if (controller != null && controller.CharacterController != null)
            {
                float previousProgress = _dodgeProgress;
                _dodgeProgress = DodgeCurve.Evaluate(normalizedTime);
                float frameDelta = _dodgeProgress - previousProgress;

                Vector3 movement = frameDelta * _currentDodgeDistance * _dodgeDirection;
                float gravity = -20f * Time.deltaTime;
                movement.y = gravity;

                controller.CharacterController.Move(movement);

                if (_currentDodgeType == DodgeType.Roll && _dodgeDirection.sqrMagnitude > 0.01f)
                {
                    Quaternion targetRotation = Quaternion.LookRotation(_dodgeDirection);
                    controller.transform.rotation = Quaternion.Slerp(
                        controller.transform.rotation,
                        targetRotation,
                        Time.deltaTime * 15f
                    );
                }
            }

            UpdateInvulnerability(normalizedTime);

            if (!_inRecoveryWindow && normalizedTime >= _recoveryWindowStart)
            {
                _inRecoveryWindow = true;
            }

            // Call base.Execute() AFTER movement - this may trigger state change
            base.Execute();
        }

        private void UpdateInvulnerability(float normalizedTime)
        {
            bool shouldBeInvulnerable = normalizedTime >= _iFrameStart && normalizedTime <= _iFrameEnd;

            if (shouldBeInvulnerable != _isInvulnerable)
            {
                _isInvulnerable = shouldBeInvulnerable;
                controller?.SetInvulnerable(_isInvulnerable);
            }
        }

        public override void HandleInput(InputSnapshot input)
        {
            if (_allowDodgeChain && _inRecoveryWindow &&
                InputHandler.Instance is not null &&
                InputHandler.Instance.HasBufferedAction(InputAction.Dodge) &&
                CanDodge())
            {
                if (InputHandler.Instance.TryConsumeAction(InputAction.Dodge, out _))
                {
                    ChangeState(PlayerState.Dodge);
                    return;
                }
            }

            if (_inRecoveryWindow)
            {
                if (InputHandler.Instance != null)
                {
                    if (InputHandler.Instance.HasBufferedAction(InputAction.LightAttack) && CanAttack())
                    {
                        if (InputHandler.Instance.TryConsumeAction(InputAction.LightAttack, out _))
                        {
                            ChangeState(PlayerState.LightAttack);
                            return;
                        }
                    }

                    if (InputHandler.Instance.HasBufferedAction(InputAction.HeavyAttack) && CanAttack())
                    {
                        if (InputHandler.Instance.TryConsumeAction(InputAction.HeavyAttack, out _))
                        {
                            ChangeState(PlayerState.HeavyAttack);
                        }
                    }
                }
            }
        }

        protected override void OnStateComplete()
        {
            if (InputHandler.Instance != null)
            {
                var input = InputHandler.Instance.CreateSnapshot();
                if (input.HasMoveInput)
                {
                    if (input.SprintHeld && CanSprint())
                    {
                        ChangeState(PlayerState.Sprint);
                    }
                    else
                    {
                        ChangeState(PlayerState.Walk);
                    }
                    return;
                }
            }

            ChangeState(PlayerState.Idle);
        }

        public override void Exit()
        {
            base.Exit();

            if (_isInvulnerable)
            {
                _isInvulnerable = false;
                controller?.SetInvulnerable(false);
            }

            // Update locked-on distance after dodge movement
            controller?.UpdateLockedOnDistance();
        }

        public override bool CanTransitionTo(PlayerState targetState, InputAction action = InputAction.None)
        {
            switch (targetState)
            {
                case PlayerState.Dodge:
                    return _inRecoveryWindow;
                case PlayerState.LightAttack:
                case PlayerState.HeavyAttack:
                case PlayerState.Parry:
                    return _inRecoveryWindow;
                case PlayerState.Stagger:
                case PlayerState.Death:
                    return !_isInvulnerable;
                case PlayerState.Idle:
                case PlayerState.Walk:
                case PlayerState.Sprint:
                    return _inRecoveryWindow || NormalizedTime >= 1f;
                default:
                    return false;
            }
        }

        public DodgeType CurrentDodgeType => _currentDodgeType;
        public bool IsInvulnerable => _isInvulnerable;
        public Vector3 DodgeDirection => _dodgeDirection;
    }
}
