using System;
using System.Collections.Generic;
using _Scripts.Combat.Data;
using _Scripts.Utilities;
using UnityEngine;
using UnityInputAction = UnityEngine.InputSystem.InputAction;

namespace Systems
{
    /// <summary>
    /// Handles input processing with buffering for souls-like combat.
    /// Wraps Unity's New Input System with buffer queue for combat actions.
    ///
    /// Input bindings configured in InputSystem_Actions:
    /// - Move: WASD / Left Stick
    /// - Look: Mouse / Right Stick
    /// - Attack (Light): LMB / Button West (Square/X)
    /// - HeavyAttack: RMB / Right Trigger
    /// - Parry: Q / Left Shoulder (L1/LB)
    /// - Dodge: Space / Button East (Circle/B)
    /// - LockOn: Tab / Right Stick Press (R3)
    /// - Sprint: Left Shift / Left Stick Press (L3)
    /// </summary>
    public class InputHandler : Singleton<InputHandler>
    {
        [Header("Buffer Settings")]
        [SerializeField]
        [Tooltip("How long buffered inputs stay valid (seconds)")]
        [Range(0.05f, 0.5f)]
        private float _bufferWindow = 0.15f;

        [SerializeField]
        [Tooltip("Deadzone for movement input")]
        [Range(0.05f, 0.3f)]
        private float _moveDeadzone = 0.1f;

        [Header("Debug")]
        [SerializeField]
        private bool _debugLog = false;

        // Input Actions reference
        private InputSystem_Actions _inputActions;

        // Current frame input snapshot
        private InputSnapshot _currentInput;
        public InputSnapshot CurrentInput => _currentInput;

        // Input buffer for combat actions
        private Queue<BufferedInput> _inputBuffer = new Queue<BufferedInput>();
        private const int MaxBufferSize = 5;

        // Pre-allocated list for TryConsumeAction (avoids GC allocation per call)
        private readonly List<BufferedInput> _tempBufferList = new List<BufferedInput>(MaxBufferSize);

        // Raw input values
        private Vector2 _moveInput;
        private Vector2 _lookInput;
        private bool _lightAttackPressed;
        private bool _heavyAttackPressed;
        private bool _dodgePressed;
        private bool _sprintHeld;
        private bool _walkHeld;
        private bool _lockOnPressed;

        // MB1 hold tracking for heavy attack
        private bool _attackButtonHeld;
        private float _attackHoldStartTime;
        private bool _heavyAttackTriggered;
        private const float HeavyAttackHoldThreshold = 0.2f;

        // MB2 block tracking
        private bool _blockHeld;

        // Events
        public event Action<InputAction> OnActionBuffered;
        public event Action<InputAction> OnActionConsumed;

        protected override void Awake()
        {
            base.Awake();
            _inputActions = new InputSystem_Actions();
        }

        private void OnEnable()
        {
            _inputActions.Enable();
            SubscribeToInputActions();
        }

        private void OnDisable()
        {
            UnsubscribeFromInputActions();
            _inputActions.Disable();
        }

        private void SubscribeToInputActions()
        {
            var player = _inputActions.Player;

            // Movement (continuous)
            player.Move.performed += OnMovePerformed;
            player.Move.canceled += OnMoveCanceled;

            // Look (continuous)
            player.Look.performed += OnLookPerformed;
            player.Look.canceled += OnLookCanceled;

            // Attack (MB1) - hold detection for light/heavy
            player.Attack.performed += OnAttackPerformed;
            player.Attack.canceled += OnAttackCanceled;

            // Sprint (hold)
            player.Sprint.performed += OnSprintPerformed;
            player.Sprint.canceled += OnSprintCanceled;

            // Walk (hold)
            player.Walk.performed += OnWalkPerformed;
            player.Walk.canceled += OnWalkCanceled;

            // Combat actions
            player.Dodge.performed += OnDodgePerformed;

            // Block (MB2) - repurposed from HeavyAttack
            player.Block.performed += OnBlockPerformed;
            player.Block.canceled += OnBlockCanceled;

            player.LockOn.performed += OnLockOnPerformed;
        }

        private void UnsubscribeFromInputActions()
        {
            var player = _inputActions.Player;

            player.Move.performed -= OnMovePerformed;
            player.Move.canceled -= OnMoveCanceled;
            player.Look.performed -= OnLookPerformed;
            player.Look.canceled -= OnLookCanceled;

            // Attack (MB1)
            player.Attack.performed -= OnAttackPerformed;
            player.Attack.canceled -= OnAttackCanceled;

            player.Sprint.performed -= OnSprintPerformed;
            player.Sprint.canceled -= OnSprintCanceled;

            // Walk (hold)
            player.Walk.performed -= OnWalkPerformed;
            player.Walk.canceled -= OnWalkCanceled;

            // Combat actions
            player.Dodge.performed -= OnDodgePerformed;

            // Block (MB2)
            player.Block.performed -= OnBlockPerformed;
            player.Block.canceled -= OnBlockCanceled;

            player.LockOn.performed -= OnLockOnPerformed;
        }

        private void Update()
        {
            // Check for heavy attack trigger while holding MB1
            if (_attackButtonHeld && !_heavyAttackTriggered)
            {
                if (Time.time - _attackHoldStartTime >= HeavyAttackHoldThreshold)
                {
                    _heavyAttackTriggered = true;
                    _heavyAttackPressed = true;
                    BufferAction(InputAction.HeavyAttack);
                }
            }

            // Build current frame snapshot
            _currentInput = new InputSnapshot(
                _moveInput,
                _lookInput,
                _lightAttackPressed,
                _heavyAttackPressed,
                _attackButtonHeld,
                _blockHeld,
                _dodgePressed,
                _sprintHeld,
                _walkHeld,
                _lockOnPressed
            );

            // Clear one-frame pressed flags
            _lightAttackPressed = false;
            _heavyAttackPressed = false;
            _dodgePressed = false;
            _lockOnPressed = false;

            // Clean expired buffer entries
            CleanBuffer();
        }

        #region Input Callbacks

        private void OnMovePerformed(UnityInputAction.CallbackContext ctx)
        {
            _moveInput = ctx.ReadValue<Vector2>();
            if (_moveInput.magnitude < _moveDeadzone)
                _moveInput = Vector2.zero;
        }

        private void OnMoveCanceled(UnityInputAction.CallbackContext ctx)
        {
            _moveInput = Vector2.zero;
        }

        private void OnLookPerformed(UnityInputAction.CallbackContext ctx)
        {
            _lookInput = ctx.ReadValue<Vector2>();
        }

        private void OnLookCanceled(UnityInputAction.CallbackContext ctx)
        {
            _lookInput = Vector2.zero;
        }

        /// <summary>
        /// MB1 pressed - start tracking hold duration.
        /// </summary>
        private void OnAttackPerformed(UnityInputAction.CallbackContext ctx)
        {
            _attackButtonHeld = true;
            _attackHoldStartTime = Time.time;
            _heavyAttackTriggered = false;
        }

        /// <summary>
        /// MB1 released - determine light or heavy attack.
        /// </summary>
        private void OnAttackCanceled(UnityInputAction.CallbackContext ctx)
        {
            _attackButtonHeld = false;

            // Only trigger light attack if heavy wasn't already triggered
            if (!_heavyAttackTriggered)
            {
                float holdDuration = Time.time - _attackHoldStartTime;
                if (holdDuration < HeavyAttackHoldThreshold)
                {
                    _lightAttackPressed = true;
                    BufferAction(InputAction.LightAttack);
                }
            }

            _heavyAttackTriggered = false;
        }

        /// <summary>
        /// MB2 pressed - enter block state.
        /// </summary>
        private void OnBlockPerformed(UnityInputAction.CallbackContext ctx)
        {
            _blockHeld = true;
            BufferAction(InputAction.Block);
        }

        /// <summary>
        /// MB2 released - exit block state.
        /// </summary>
        private void OnBlockCanceled(UnityInputAction.CallbackContext ctx)
        {
            _blockHeld = false;
        }

        private void OnDodgePerformed(UnityInputAction.CallbackContext ctx)
        {
            _dodgePressed = true;
            BufferAction(InputAction.Dodge, _moveInput);
        }

        private void OnSprintPerformed(UnityInputAction.CallbackContext ctx)
        {
            _sprintHeld = true;
        }

        private void OnSprintCanceled(UnityInputAction.CallbackContext ctx)
        {
            _sprintHeld = false;
        }

        private void OnWalkPerformed(UnityInputAction.CallbackContext ctx)
        {
            _walkHeld = true;
        }

        private void OnWalkCanceled(UnityInputAction.CallbackContext ctx)
        {
            _walkHeld = false;
        }

        private void OnLockOnPerformed(UnityInputAction.CallbackContext ctx)
        {
            _lockOnPressed = true;
            BufferAction(InputAction.LockOn);
        }

        #endregion

        #region Buffer Management

        /// <summary>
        /// Adds an action to the input buffer.
        /// </summary>
        private void BufferAction(InputAction action, Vector2 direction = default)
        {
            // Limit buffer size
            while (_inputBuffer.Count >= MaxBufferSize)
            {
                _inputBuffer.Dequeue();
            }

            var buffered = new BufferedInput(action, direction);
            _inputBuffer.Enqueue(buffered);

            if (_debugLog)
                Debug.Log($"[InputHandler] Buffered: {action}");

            OnActionBuffered?.Invoke(action);
        }

        /// <summary>
        /// Removes expired entries from the buffer.
        /// </summary>
        private void CleanBuffer()
        {
            while (_inputBuffer.Count > 0 && !_inputBuffer.Peek().IsValid(_bufferWindow))
            {
                _inputBuffer.Dequeue();
            }
        }

        /// <summary>
        /// Tries to consume a buffered action of the specified type.
        /// Uses pre-allocated list to avoid GC allocation per call.
        /// </summary>
        /// <param name="action">The action type to consume.</param>
        /// <param name="buffered">The consumed buffered input if found.</param>
        /// <returns>True if action was found and consumed.</returns>
        public bool TryConsumeAction(InputAction action, out BufferedInput buffered)
        {
            buffered = default;

            // Copy to pre-allocated list (no allocation)
            _tempBufferList.Clear();
            foreach (var item in _inputBuffer)
            {
                _tempBufferList.Add(item);
            }

            for (int i = 0; i < _tempBufferList.Count; i++)
            {
                var input = _tempBufferList[i];
                if (input.Action == action && input.IsValid(_bufferWindow))
                {
                    input.Consume();
                    _tempBufferList[i] = input;
                    buffered = input;

                    // Rebuild queue from pre-allocated list
                    _inputBuffer.Clear();
                    for (int j = 0; j < _tempBufferList.Count; j++)
                    {
                        if (!_tempBufferList[j].Consumed)
                            _inputBuffer.Enqueue(_tempBufferList[j]);
                    }

                    if (_debugLog)
                        Debug.Log($"[InputHandler] Consumed: {action}");

                    OnActionConsumed?.Invoke(action);
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Tries to consume any combat action from the buffer.
        /// Returns the first valid buffered action.
        /// </summary>
        public bool TryConsumeAnyAction(out BufferedInput buffered)
        {
            buffered = default;

            foreach (var input in _inputBuffer)
            {
                if (input.IsValid(_bufferWindow) && !input.Consumed)
                {
                    return TryConsumeAction(input.Action, out buffered);
                }
            }

            return false;
        }

        /// <summary>
        /// Checks if a specific action is buffered (without consuming).
        /// </summary>
        public bool HasBufferedAction(InputAction action)
        {
            foreach (var input in _inputBuffer)
            {
                if (input.Action == action && input.IsValid(_bufferWindow))
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Clears all buffered inputs.
        /// </summary>
        public void ClearBuffer()
        {
            _inputBuffer.Clear();
            if (_debugLog)
                Debug.Log("[InputHandler] Buffer cleared");
        }

        #endregion

        #region Direct Input Queries

        /// <summary>
        /// Gets raw movement input (not buffered).
        /// </summary>
        public Vector2 GetMoveInput() => _moveInput;

        /// <summary>
        /// Gets raw look input (not buffered).
        /// </summary>
        public Vector2 GetLookInput() => _lookInput;

        /// <summary>
        /// Is sprint currently held.
        /// </summary>
        public bool IsSprintHeld() => _sprintHeld;

        /// <summary>
        /// Is walk currently held.
        /// </summary>
        public bool IsWalkHeld() => _walkHeld;

        /// <summary>
        /// Is there any movement input.
        /// </summary>
        public bool HasMoveInput() => _moveInput.sqrMagnitude > _moveDeadzone * _moveDeadzone;

        /// <summary>
        /// Creates a fresh snapshot of current input state.
        /// </summary>
        public InputSnapshot CreateSnapshot()
        {
            return new InputSnapshot(
                _moveInput,
                _lookInput,
                _lightAttackPressed,
                _heavyAttackPressed,
                _attackButtonHeld,
                _blockHeld,
                _dodgePressed,
                _sprintHeld,
                _walkHeld,
                _lockOnPressed
            );
        }

        /// <summary>
        /// Is block button currently held.
        /// </summary>
        public bool IsBlockHeld() => _blockHeld;

        /// <summary>
        /// Is attack button currently held (for charge tracking).
        /// </summary>
        public bool IsAttackHeld() => _attackButtonHeld;

        #endregion
    }
}
