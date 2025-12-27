using _Scripts.Combat.Data;
using _Scripts.Utilities;
using UnityEngine;

namespace _Scripts.Player.States
{
    /// <summary>
    /// Player idle state - standing still, ready for any action.
    /// </summary>
    public class PlayerIdleState : BasePlayerState
    {
        public override PlayerState StateType => PlayerState.Idle;

        public override void Enter()
        {
            base.Enter();

            // Set animator for idle - Speed=0 drives Blend Tree to idle animation
            if (controller != null && controller.Animator != null)
            {
                controller.Animator.SetBoolSafe("IsMoving", false);
                controller.Animator.SetFloatSafe("Speed", 0f);
            }
        }

        public override void Execute()
        {
            base.Execute();

            // IMPORTANT: Call ApplyMovement with zero input to trigger deceleration
            // This ensures the momentum system properly decelerates when entering idle
            controller?.ApplyMovement(Vector2.zero, Components.LocomotionMode.Run);
        }

        public override void HandleInput(InputSnapshot input)
        {
            // Check for combat actions first (higher priority)
            CheckCombatTransition(input);

            // Then check movement - Sprint > Walk > Run (default)
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
            }

            // Lock-on toggle
            if (input.LockOnPressed)
            {
                controller?.ToggleLockOn();
            }
        }

        public override void Exit()
        {
            // Speed will be set by the next state via ApplyMovement
            base.Exit();
        }

        public override bool CanTransitionTo(PlayerState targetState, InputAction action = InputAction.None)
        {
            // Idle can transition to any state
            return true;
        }
    }
}
