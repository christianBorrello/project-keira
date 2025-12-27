using System;
using _Scripts.Combat.Hitbox;
using _Scripts.Utilities;
using Systems;
using UnityEngine;

namespace _Scripts.Enemies.States
{
    /// <summary>
    /// Enemy death state - handles death animation and cleanup.
    /// </summary>
    public class EnemyDeathState : BaseEnemyState
    {
        // Configuration with defaults
        private readonly float _deathDuration = 2f;
        private readonly float _despawnDelay = 10f;
        private readonly bool _useRagdoll = false;
        private readonly bool _disableHitboxes = true;

        public event Action OnDeathComplete;

        public override EnemyState StateType => EnemyState.Death;

        public override void Enter()
        {
            base.Enter();

            _stateDuration = _deathDuration;

            // Stop all movement
            _controller?.StopMovement();

            if (_controller != null && _controller.NavAgent != null)
            {
                _controller.NavAgent.enabled = false;
            }

            // Disable hitboxes
            if (_disableHitboxes)
            {
                var hurtboxes = _controller?.GetComponentsInChildren<Hurtbox>();
                if (hurtboxes != null)
                {
                    foreach (var hurtbox in hurtboxes)
                    {
                        hurtbox.IsActive = false;
                    }
                }
            }

            // Unregister from combat system
            if (_controller != null)
            {
                CombatSystem.Instance?.UnregisterCombatant(_controller);
            }

            // Trigger animation
            if (_controller != null && _controller.Animator != null)
            {
                _controller.Animator.SetTriggerSafe("Death");
                _controller.Animator.SetBoolSafe("IsDead", true);
            }

            // Enable ragdoll if configured
            if (_useRagdoll)
            {
                EnableRagdoll();
            }
        }

        protected override void OnStateComplete()
        {
            OnDeathComplete?.Invoke();

            // Schedule despawn
            if (_despawnDelay > 0 && _controller != null)
            {
                UnityEngine.Object.Destroy(_controller.gameObject, _despawnDelay);
            }
        }

        private void EnableRagdoll()
        {
            if (_controller == null) return;

            // Disable animator
            if (_controller.Animator != null)
            {
                _controller.Animator.enabled = false;
            }

            // Enable ragdoll physics
            var rigidbodies = _controller.GetComponentsInChildren<Rigidbody>();
            foreach (var rb in rigidbodies)
            {
                rb.isKinematic = false;
            }
        }

        public override bool CanTransitionTo(EnemyState targetState)
        {
            // Death is final
            return false;
        }

        public override void OnInterrupted()
        {
            // Cannot be interrupted
        }
    }
}
