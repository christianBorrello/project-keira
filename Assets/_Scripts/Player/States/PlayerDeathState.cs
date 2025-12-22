using System;
using _Scripts.Combat.Data;
using _Scripts.Combat.Hitbox;
using _Scripts.Combat.Interfaces;
using _Scripts.Utilities;
using Systems;
using UnityEngine;

namespace _Scripts.Player.States
{
    /// <summary>
    /// Player death state - final state when health reaches zero.
    /// Handles death animation and cleanup.
    /// </summary>
    public class PlayerDeathState : BasePlayerState
    {
        // Configuration with defaults
        private readonly float _deathDuration = 3f;
        private readonly float _respawnPromptDelay = 2f;
        private readonly bool _useRagdoll = false;
        private readonly bool _disableCollisions = true;
        private readonly bool _deathSlowMotion = true;
        private readonly float _slowMotionScale = 0.3f;
        private readonly float _slowMotionDuration = 1f;

        // Events
        public event Action OnDeathAnimationComplete;
        public event Action OnRespawnReady;

        // State
        private bool _animationComplete = false;
        private bool _respawnReady = false;
        private float _slowMotionEndTime;
        private float _originalTimeScale;

        public override PlayerState StateType => PlayerState.Death;

        public override void Enter()
        {
            base.Enter();

            stateDuration = _deathDuration;
            _animationComplete = false;
            _respawnReady = false;

            // Clear combat states
            controller?.SetParrying(false);
            controller?.SetInvulnerable(false);

            // Stop all movement (disable KCC motor)
            if (controller != null && controller.Motor != null)
            {
                controller.Motor.enabled = false;
            }

            // Disable collisions if configured
            if (_disableCollisions && controller != null)
            {
                // Disable hurtboxes
                var hurtboxes = controller.GetComponentsInChildren<Hurtbox>();
                foreach (var hurtbox in hurtboxes)
                {
                    hurtbox.IsActive = false;
                }
            }

            // Apply slow motion
            if (_deathSlowMotion)
            {
                _originalTimeScale = Time.timeScale;
                Time.timeScale = _slowMotionScale;
                _slowMotionEndTime = Time.unscaledTime + _slowMotionDuration;
            }

            // Trigger death animation
            if (controller != null && controller.Animator != null)
            {
                controller.Animator.SetTriggerSafe("Death");
                controller.Animator.SetBoolSafe("IsDead", true);
            }

            // Ragdoll (if enabled)
            if (_useRagdoll)
            {
                EnableRagdoll();
            }

            // Unregister from combat system
            if (CombatSystem.Instance != null && controller is ICombatant combatant)
            {
                CombatSystem.Instance.UnregisterCombatant(combatant);
            }
        }

        public override void Execute()
        {
            base.Execute();

            // Handle slow motion end
            if (_deathSlowMotion && Time.unscaledTime >= _slowMotionEndTime && Time.timeScale != 1f)
            {
                Time.timeScale = _originalTimeScale;
            }

            // Check for respawn prompt timing
            if (!_respawnReady && StateTime >= _respawnPromptDelay)
            {
                _respawnReady = true;
                OnRespawnReady?.Invoke();
            }
        }

        public override void HandleInput(InputSnapshot input)
        {
            // Only accept respawn input after ready
            if (_respawnReady)
            {
                // Accept any action button to respawn
                if (input.LightAttackPressed || input.HeavyAttackPressed ||
                    input.DodgePressed || input.BlockHeld)
                {
                    RequestRespawn();
                }
            }
        }

        protected override void OnStateComplete()
        {
            _animationComplete = true;
            OnDeathAnimationComplete?.Invoke();

            // Stay in death state - don't auto-transition
        }

        public override void Exit()
        {
            base.Exit();

            // Restore time scale
            if (_deathSlowMotion)
            {
                Time.timeScale = _originalTimeScale;
            }

            // Re-enable KCC motor
            if (controller != null && controller.Motor != null)
            {
                controller.Motor.enabled = true;
            }

            // Clear dead flag
            if (controller != null && controller.Animator != null)
            {
                controller.Animator.SetBoolSafe("IsDead", false);
            }

            // Disable ragdoll
            if (_useRagdoll)
            {
                DisableRagdoll();
            }
        }

        public override bool CanTransitionTo(PlayerState targetState, InputAction action = InputAction.None)
        {
            // Death state only exits through explicit respawn
            return false;
        }

        /// <summary>
        /// Cannot be interrupted.
        /// </summary>
        public override void OnInterrupted()
        {
            // Death cannot be interrupted
        }

        private void EnableRagdoll()
        {
            if (controller == null) return;

            // Disable animator
            if (controller.Animator != null)
            {
                controller.Animator.enabled = false;
            }

            // Enable ragdoll rigidbodies
            var rigidbodies = controller.GetComponentsInChildren<Rigidbody>();
            foreach (var rb in rigidbodies)
            {
                rb.isKinematic = false;
            }

            // Enable ragdoll colliders
            var colliders = controller.GetComponentsInChildren<Collider>();
            foreach (var col in colliders)
            {
                if (col.GetComponent<Hurtbox>() == null) // Don't enable hurtbox colliders
                {
                    col.enabled = true;
                }
            }
        }

        private void DisableRagdoll()
        {
            if (controller == null) return;

            // Re-enable animator
            if (controller.Animator != null)
            {
                controller.Animator.enabled = true;
            }

            // Disable ragdoll rigidbodies
            var rigidbodies = controller.GetComponentsInChildren<Rigidbody>();
            foreach (var rb in rigidbodies)
            {
                rb.isKinematic = true;
            }
        }

        /// <summary>
        /// Request respawn from external system.
        /// </summary>
        public void RequestRespawn()
        {
            // This would typically trigger a game manager to handle respawn
            // For now, just notify via event
            Debug.Log("[PlayerDeathState] Respawn requested");

            // Game manager would call Respawn() when ready
        }

        /// <summary>
        /// Perform respawn (called by game manager).
        /// </summary>
        public void Respawn()
        {
            // Force transition to Idle
            stateMachine?.ForceInterrupt(PlayerState.Idle);
        }

        /// <summary>
        /// Whether death animation is complete.
        /// </summary>
        public bool AnimationComplete => _animationComplete;

        /// <summary>
        /// Whether respawn is ready.
        /// </summary>
        public bool RespawnReady => _respawnReady;
    }
}
