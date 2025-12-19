using _Scripts.Combat.Data;
using _Scripts.Player.Components;
using _Scripts.Utilities;
using Systems;

namespace _Scripts.Player.States
{
    /// <summary>
    /// Player walking state - slow movement with Control held.
    /// </summary>
    public class PlayerWalkState : BasePlayerState
    {
        public override PlayerState StateType => PlayerState.Walk;

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

            if (controller is null || InputHandler.Instance is null)
                return;

            var moveInput = InputHandler.Instance.GetMoveInput();

            if (moveInput.sqrMagnitude > 0.01f)
            {
                controller.ApplyMovement(moveInput, LocomotionMode.Walk);
            }
        }

        public override void HandleInput(InputSnapshot input)
        {
            // Check combat actions first
            CheckCombatTransition(input);

            // Check for state transitions
            if (!input.HasMoveInput)
            {
                ChangeState(PlayerState.Idle);
                return;
            }

            // Sprint has priority over walk
            if (input.SprintHeld && CanSprint())
            {
                ChangeState(PlayerState.Sprint);
                return;
            }

            // Return to run when walk (Control) is released
            if (!input.WalkHeld)
            {
                ChangeState(PlayerState.Run);
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
                case PlayerState.Run:
                case PlayerState.Sprint:
                case PlayerState.LightAttack:
                case PlayerState.HeavyAttack:
                case PlayerState.Parry:
                case PlayerState.Block:
                case PlayerState.Dodge:
                case PlayerState.Stagger:
                case PlayerState.Death:
                    return true;
                default:
                    return false;
            }
        }
    }
}
