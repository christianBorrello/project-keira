using _Scripts.Combat.Data;
using _Scripts.Player.Components;
using _Scripts.Utilities;
using Systems;
using UnityEngine;

namespace _Scripts.Player.States
{
    /// <summary>
    /// Player sprinting state - moving at increased speed, consuming stamina.
    /// </summary>
    public class PlayerSprintState : BasePlayerState
    {
        public override PlayerState StateType => PlayerState.Sprint;

        public override void Enter()
        {
            base.Enter();

            // Speed is set by ApplyMovement - Blend Tree handles animation
            if (controller is not null && controller.Animator is not null)
            {
                controller.Animator.SetBoolSafe("IsMoving", true);
            }
        }

        public override void Execute()
        {
            base.Execute();

            // Consume stamina while sprinting
            if (controller is not null)
            {
                var stats = controller.GetBaseStats();
                controller.ConsumeStamina(stats.SprintStaminaCost * Time.deltaTime);

                // Check if we ran out of stamina - transition to Run (default)
                var runtime = controller.GetRuntimeData();
                if (runtime.CurrentStamina <= 0)
                {
                    ChangeState(PlayerState.Run);
                }
            }

            if (controller is null || InputHandler.Instance is null)
                return;

            var moveInput = InputHandler.Instance.GetMoveInput();

            if (moveInput.sqrMagnitude > 0.01f)
            {
                controller.ApplyMovement(moveInput, LocomotionMode.Sprint);
            }
        }

        public override void HandleInput(InputSnapshot input)
        {
            // Dodge has higher priority during sprint (running dodge)
            if (InputHandler.Instance is not null &&
                InputHandler.Instance.HasBufferedAction(InputAction.Dodge) &&
                CanDodge() &&
                InputHandler.Instance.TryConsumeAction(InputAction.Dodge, out _)
                )
            {
                    ChangeState(PlayerState.Dodge);
                    return;
            }

            // Check for other combat actions (can attack out of sprint)
            if (InputHandler.Instance is not null)
            {
                if (InputHandler.Instance.HasBufferedAction(InputAction.LightAttack) && 
                    CanAttack() &&
                    InputHandler.Instance.TryConsumeAction(InputAction.LightAttack, out _))
                {
                        ChangeState(PlayerState.LightAttack);
                        return;
                }

                if (InputHandler.Instance.HasBufferedAction(InputAction.HeavyAttack) && 
                    CanAttack() &&
                    InputHandler.Instance.TryConsumeAction(InputAction.HeavyAttack, out _))
                {
                        ChangeState(PlayerState.HeavyAttack);
                        return;
                }

                // Block (MB2 - includes parry window)
                if (InputHandler.Instance.HasBufferedAction(InputAction.Block) && 
                    CanParry() &&
                    InputHandler.Instance.TryConsumeAction(InputAction.Block, out _))
                {
                        ChangeState(PlayerState.Block);
                        return;
                }
            }

            // Check movement state transitions
            if (!input.HasMoveInput)
            {
                ChangeState(PlayerState.Idle);
                return;
            }

            // When sprint released, transition based on walk modifier
            if (!input.SprintHeld)
            {
                // Walk if Control is held, otherwise Run
                ChangeState(input.WalkHeld ? PlayerState.Walk : PlayerState.Run);
                return;
            }

            // Lock-on toggle
            if (input.LockOnPressed)
            {
                controller?.ToggleLockOn();
            }
        }

        public override void Exit()
        {
            // Blend Tree handles transitions automatically via Speed parameter
            base.Exit();
        }

        public override bool CanTransitionTo(PlayerState targetState, InputAction action = InputAction.None)
        {
            switch (targetState)
            {
                case PlayerState.Idle:
                case PlayerState.Walk:
                case PlayerState.Run:
                case PlayerState.LightAttack:
                case PlayerState.HeavyAttack:
                case PlayerState.Dodge:
                case PlayerState.Block:
                case PlayerState.Stagger:
                case PlayerState.Death:
                case PlayerState.Parry:
                    return true;
                default:
                    return false;
            }
        }
    }
}
