using System;
using _Scripts.Combat.Data;
using _Scripts.Combat.Hitbox;
using _Scripts.Combat.Interfaces;
using UnityEngine;

namespace _Scripts.Player.Components
{
    /// <summary>
    /// Manages combat state for the player.
    /// Coordinates parry/block/invulnerable states and ICombatant event notifications.
    /// </summary>
    [RequireComponent(typeof(PlayerController))]
    public class CombatController : MonoBehaviour
    {
        // References
        private PlayerController _player;
        private PlayerStateMachine _stateMachine;
        private HitboxController _hitboxController;

        // Combat state flags
        private bool _isParrying;
        private float _parryWindowStart;
        private bool _isBlocking;
        private bool _isInvulnerable;

        #region Properties

        /// <summary>
        /// Whether player is currently in parry window.
        /// </summary>
        public bool IsParrying => _isParrying;

        /// <summary>
        /// Whether player is currently blocking.
        /// </summary>
        public bool IsBlocking => _isBlocking;

        /// <summary>
        /// Whether player has invulnerability (i-frames).
        /// </summary>
        public bool IsInvulnerable => _isInvulnerable;

        /// <summary>
        /// Access to the hitbox controller for attack states.
        /// </summary>
        public HitboxController HitboxController => _hitboxController;

        #endregion

        #region ICombatant Events

        /// <summary>
        /// Fired when player starts an attack.
        /// </summary>
        public event Action<ICombatant, AttackData> OnAttackStarted;

        /// <summary>
        /// Fired when player lands a hit on a target.
        /// </summary>
        public event Action<ICombatant, IDamageable, DamageResult> OnHitLanded;

        /// <summary>
        /// Fired when player is staggered.
        /// </summary>
        public event Action<ICombatant, float> OnStaggered;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            _player = GetComponent<PlayerController>();
            _stateMachine = GetComponent<PlayerStateMachine>();
            _hitboxController = GetComponentInChildren<HitboxController>();
        }

        #endregion

        #region Parry/Block/Invulnerable State

        /// <summary>
        /// Set parry state. When entering parry, records the start time for timing calculations.
        /// </summary>
        public void SetParrying(bool isParrying)
        {
            _isParrying = isParrying;
            if (isParrying)
            {
                _parryWindowStart = Time.time;
            }
        }

        /// <summary>
        /// Set blocking state.
        /// </summary>
        public void SetBlocking(bool isBlocking)
        {
            _isBlocking = isBlocking;
        }

        /// <summary>
        /// Set invulnerability state (i-frames during dodge, etc.).
        /// </summary>
        public void SetInvulnerable(bool isInvulnerable)
        {
            _isInvulnerable = isInvulnerable;
        }

        /// <summary>
        /// Check if currently parrying and get timing info.
        /// </summary>
        /// <param name="timing">Output parry timing for perfect/partial parry calculation.</param>
        /// <returns>True if currently in parry window.</returns>
        public bool IsParryingWithTiming(out ParryTiming timing)
        {
            if (_isParrying)
            {
                var stats = _player.GetBaseStats();
                timing = new ParryTiming(
                    _parryWindowStart,
                    stats.ParryWindowDuration,
                    stats.PerfectParryWindow
                );
                return true;
            }
            timing = default;
            return false;
        }

        #endregion

        #region Combat Notifications

        /// <summary>
        /// Notify that an attack has started. Called by attack states.
        /// </summary>
        public void NotifyAttackStarted(AttackData attack)
        {
            OnAttackStarted?.Invoke(_player, attack);
        }

        /// <summary>
        /// Notify that a hit was landed on a target. Called by hitbox system.
        /// </summary>
        public void NotifyHitLanded(IDamageable target, DamageResult result)
        {
            OnHitLanded?.Invoke(_player, target, result);
        }

        /// <summary>
        /// Apply stagger to player. Forces state machine to stagger state.
        /// </summary>
        public void ApplyStagger(float duration)
        {
            _stateMachine?.ForceInterrupt(PlayerState.Stagger);
            OnStaggered?.Invoke(_player, duration);
        }

        #endregion
    }
}
