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

        // Debug
        private Vector3 _debugEnterPosition;
        private int _debugFrameCount;

        public override void Enter()
        {
            base.Enter();

            _debugFrameCount = 0;

            if (controller is not null)
            {
                _debugEnterPosition = controller.transform.position;
                Debug.Log($"[IDLE] Enter - Position: {_debugEnterPosition}");

                // DEBUG: Try disabling animator to see if it's the cause
                if (controller.Animator is not null)
                {
                    Debug.Log($"[IDLE] Animator enabled: {controller.Animator.enabled}, applyRootMotion: {controller.Animator.applyRootMotion}");
                }
            }

            // Set animator for idle - Speed=0 drives Blend Tree to idle animation
            if (controller is not null && controller.Animator is not null)
            {
                controller.Animator.SetBoolSafe("IsMoving", false);
                controller.Animator.SetFloatSafe("Speed", 0f);
            }
        }

        public override void Execute()
        {
            base.Execute();

            // Debug: Check if position changes in first few frames
            if (_debugFrameCount < 10 && controller != null)
            {
                Vector3 currentPos = controller.transform.position;
                Vector3 ccCenter = controller.CharacterController != null
                    ? controller.CharacterController.transform.position
                    : Vector3.zero;

                if (Vector3.Distance(currentPos, _debugEnterPosition) > 0.01f)
                {
                    Debug.LogWarning($"[IDLE] Frame {_debugFrameCount} - Position CHANGED! " +
                        $"Was: {_debugEnterPosition}, Now: {currentPos}, CC: {ccCenter}");

                    // Log the full stack trace to see what's calling this
                    Debug.LogWarning($"[IDLE] Stack: {UnityEngine.StackTraceUtility.ExtractStackTrace()}");
                }
                _debugFrameCount++;
            }

            // Idle allows transitions to any state, handled in HandleInput
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
