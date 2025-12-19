using UnityEngine;

namespace _Scripts.Combat.Data
{
    /// <summary>
    /// Types of bufferable combat actions.
    /// </summary>
    public enum InputAction
    {
        None = 0,
        LightAttack = 1,
        HeavyAttack = 2,
        Parry = 3,
        Dodge = 4,
        LockOn = 5,
        Block = 6
    }

    /// <summary>
    /// Snapshot of input state for a single frame.
    /// Passed to state machines for decision making.
    /// </summary>
    public readonly struct InputSnapshot
    {
        /// <summary>Movement direction from stick/WASD.</summary>
        public readonly Vector2 MoveInput;

        /// <summary>Camera/look direction from mouse/right stick.</summary>
        public readonly Vector2 LookInput;

        /// <summary>Light attack triggered (MB1 tap).</summary>
        public readonly bool LightAttackPressed;

        /// <summary>Heavy attack triggered (MB1 hold past threshold).</summary>
        public readonly bool HeavyAttackPressed;

        /// <summary>Attack button currently held (for charge tracking).</summary>
        public readonly bool AttackHeld;

        /// <summary>Block button held (MB2).</summary>
        public readonly bool BlockHeld;

        /// <summary>Dodge button pressed this frame.</summary>
        public readonly bool DodgePressed;

        /// <summary>Sprint button held.</summary>
        public readonly bool SprintHeld;

        /// <summary>Walk button held (slow movement).</summary>
        public readonly bool WalkHeld;

        /// <summary>Lock-on toggle pressed this frame.</summary>
        public readonly bool LockOnPressed;

        /// <summary>Timestamp when this snapshot was created.</summary>
        public readonly float Timestamp;

        public InputSnapshot(
            Vector2 moveInput,
            Vector2 lookInput,
            bool lightAttackPressed,
            bool heavyAttackPressed,
            bool attackHeld,
            bool blockHeld,
            bool dodgePressed,
            bool sprintHeld,
            bool walkHeld,
            bool lockOnPressed)
        {
            MoveInput = moveInput;
            LookInput = lookInput;
            LightAttackPressed = lightAttackPressed;
            HeavyAttackPressed = heavyAttackPressed;
            AttackHeld = attackHeld;
            BlockHeld = blockHeld;
            DodgePressed = dodgePressed;
            SprintHeld = sprintHeld;
            WalkHeld = walkHeld;
            LockOnPressed = lockOnPressed;
            Timestamp = Time.time;
        }

        /// <summary>True if there's any movement input.</summary>
        public bool HasMoveInput => MoveInput.sqrMagnitude > 0.01f;

        /// <summary>True if any combat action was pressed.</summary>
        public bool HasCombatInput => LightAttackPressed || HeavyAttackPressed || DodgePressed;

        /// <summary>Magnitude of movement input (0-1).</summary>
        public float MoveInputMagnitude => Mathf.Clamp01(MoveInput.magnitude);

        /// <summary>Normalized movement direction.</summary>
        public Vector2 MoveDirection => MoveInput.sqrMagnitude > 0.01f ? MoveInput.normalized : Vector2.zero;

        /// <summary>Empty input snapshot.</summary>
        public static InputSnapshot Empty => new InputSnapshot(
            Vector2.zero, Vector2.zero,
            false, false, false, false, false, false, false, false);
    }

    /// <summary>
    /// A buffered input action waiting to be consumed.
    /// </summary>
    public struct BufferedInput
    {
        /// <summary>The action that was buffered.</summary>
        public InputAction Action;

        /// <summary>Timestamp when the input was registered.</summary>
        public float Timestamp;

        /// <summary>Direction for directional actions (like dodge).</summary>
        public Vector2 Direction;

        /// <summary>Whether this input has been consumed.</summary>
        public bool Consumed;

        public BufferedInput(InputAction action, Vector2 direction)
        {
            Action = action;
            Timestamp = Time.time;
            Direction = direction;
            Consumed = false;
        }

        /// <summary>
        /// Checks if this buffered input is still valid.
        /// </summary>
        /// <param name="bufferWindow">Maximum time input stays valid.</param>
        public bool IsValid(float bufferWindow)
        {
            return !Consumed && (Time.time - Timestamp) <= bufferWindow;
        }

        /// <summary>
        /// Marks this input as consumed.
        /// </summary>
        public void Consume()
        {
            Consumed = true;
        }

        /// <summary>
        /// Age of this input in seconds.
        /// </summary>
        public float Age => Time.time - Timestamp;
    }

    /// <summary>
    /// Result of parry timing check.
    /// </summary>
    public readonly struct ParryTiming
    {
        /// <summary>Time when parry window started.</summary>
        public readonly float WindowStart;

        /// <summary>Duration of the parry window.</summary>
        public readonly float WindowDuration;

        /// <summary>Duration of the perfect parry portion.</summary>
        public readonly float PerfectDuration;

        /// <summary>Current time when checked.</summary>
        public readonly float CheckTime;

        public ParryTiming(float windowStart, float windowDuration, float perfectDuration)
        {
            WindowStart = windowStart;
            WindowDuration = windowDuration;
            PerfectDuration = perfectDuration;
            CheckTime = Time.time;
        }

        /// <summary>Time elapsed since parry window started.</summary>
        public float ElapsedTime => CheckTime - WindowStart;

        /// <summary>Normalized position in parry window (0-1).</summary>
        public float NormalizedTime => Mathf.Clamp01(ElapsedTime / WindowDuration);

        /// <summary>True if within perfect parry timing (first portion of window).</summary>
        public bool IsPerfect => ElapsedTime <= PerfectDuration;

        /// <summary>True if within partial parry timing (rest of window).</summary>
        public bool IsPartial => !IsPerfect && ElapsedTime <= WindowDuration;

        /// <summary>True if parry window has expired.</summary>
        public bool IsExpired => ElapsedTime > WindowDuration;

        /// <summary>True if within any valid parry timing.</summary>
        public bool IsValid => !IsExpired;
    }
}
