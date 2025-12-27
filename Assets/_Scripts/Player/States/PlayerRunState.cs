using _Scripts.Combat.Data;
using _Scripts.Player.Components;
using _Scripts.Utilities;
using Systems;

namespace _Scripts.Player.States
{
    /// <summary>
    /// Player running state - default movement speed.
    /// This is the standard locomotion state when moving without modifiers.
    /// </summary>
    public class PlayerRunState : BasePlayerState
    {
        public override PlayerState StateType => PlayerState.Run;

        public override void Enter()
        {
            base.Enter();

            // Speed is set by ApplyMovement - Blend Tree handles animation
            if (controller != null && controller.Animator != null)
            {
                controller.Animator.SetBoolSafe("IsMoving", true);
            }
        }

        public override void Execute()
        {
            base.Execute();

            if (controller == null || InputHandler.Instance == null)
                return;

            var moveInput = InputHandler.Instance.GetMoveInput();

            if (moveInput.sqrMagnitude > 0.01f)
            {
                controller.ApplyMovement(moveInput, LocomotionMode.Run);
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

            // Walk when control is held
            if (input.WalkHeld)
            {
                ChangeState(PlayerState.Walk);
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
