using System;
using System.Collections.Generic;
using _Scripts.Combat.Data;
using _Scripts.Combat.Interfaces;
using _Scripts.Player;
using Systems;
using UnityEngine;

namespace _Scripts.Combat.Hitbox
{
    /// <summary>
    /// Hitbox component for detecting hits on targets.
    /// Attach to weapon colliders or attack zones.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class Hitbox : MonoBehaviour
    {
        [Header("Configuration")]
        [SerializeField]
        [Tooltip("Owner of this hitbox (set automatically if null)")]
        private ICombatant _owner;

        [SerializeField]
        [Tooltip("Damage multiplier for this specific hitbox")]
        private float _damageMultiplier = 1f;

        [SerializeField]
        [Tooltip("Poise damage multiplier for this specific hitbox")]
        private float _poiseDamageMultiplier = 1f;

        [Header("Debug")]
        [SerializeField]
        private bool _debugMode = false;

        [SerializeField]
        private Color _debugColor = Color.red;

        // State
        private bool _isActive = false;
        private AttackData _currentAttack;
        private HashSet<int> _hitTargets = new HashSet<int>();
        private Collider _collider;

        // Events
        public event Action<Hitbox, Hurtbox, DamageResult> OnHitConfirmed;

        /// <summary>
        /// Whether this hitbox is currently active.
        /// </summary>
        public bool IsActive => _isActive;

        /// <summary>
        /// Owner combatant of this hitbox.
        /// </summary>
        public ICombatant Owner
        {
            get => _owner;
            set => _owner = value;
        }

        private void Awake()
        {
            _collider = GetComponent<Collider>();
            _collider.isTrigger = true;
            _collider.enabled = false;

            // Try to find owner if not set
            if (_owner == null)
            {
                _owner = GetComponentInParent<ICombatant>();
            }
        }

        /// <summary>
        /// Activate the hitbox with attack data.
        /// </summary>
        /// <param name="attackData">Data for the current attack</param>
        public void Activate(AttackData attackData)
        {
            _currentAttack = attackData;
            _hitTargets.Clear();
            _isActive = true;
            _collider.enabled = true;

            if (_debugMode)
            {
                Debug.Log($"[Hitbox] Activated: {gameObject.name}, Layer: {gameObject.layer} ({LayerMask.LayerToName(gameObject.layer)})");
                Debug.Log($"[Hitbox] Collider enabled: {_collider.enabled}, IsTrigger: {_collider.isTrigger}, Bounds: {_collider.bounds}");
            }
        }

        /// <summary>
        /// Deactivate the hitbox.
        /// </summary>
        public void Deactivate()
        {
            _isActive = false;
            _collider.enabled = false;
            _hitTargets.Clear();

            if (_debugMode)
                Debug.Log($"[Hitbox] Deactivated: {gameObject.name}");
        }

        private void OnTriggerEnter(Collider other)
        {
            if (_debugMode)
            {
                Debug.Log($"[Hitbox] OnTriggerEnter: {other.name}, Layer: {other.gameObject.layer} ({LayerMask.LayerToName(other.gameObject.layer)}), IsActive: {_isActive}");
            }

            if (!_isActive) return;

            // Try to get hurtbox
            var hurtbox = other.GetComponent<Hurtbox>();
            if (hurtbox == null)
            {
                if (_debugMode)
                    Debug.Log($"[Hitbox] No Hurtbox component on {other.name}");
                return;
            }

            // Get target damageable
            var target = hurtbox.Owner;
            if (target == null)
            {
                if (_debugMode)
                    Debug.Log($"[Hitbox] Hurtbox.Owner is NULL on {other.name}");
                return;
            }

            // Check if already hit this target
            int targetId = target.GetHashCode();
            if (_hitTargets.Contains(targetId))
            {
                if (_debugMode)
                    Debug.Log($"[Hitbox] Already hit target {target}");
                return;
            }

            // Check faction (don't hit allies)
            if (_owner != null)
            {
                var ownerCombatant = _owner;
                var targetCombatant = target as ICombatant;

                if (targetCombatant != null && !CombatSystem.Instance.AreHostile(ownerCombatant, targetCombatant))
                {
                    if (_debugMode)
                        Debug.Log($"[Hitbox] Faction check failed - not hostile. Owner: {ownerCombatant?.Faction}, Target: {targetCombatant?.Faction}");
                    return;
                }
            }

            // Mark as hit
            _hitTargets.Add(targetId);

            // Calculate damage (DamageMultiplier acts as base damage value)
            float finalDamage = _currentAttack.DamageMultiplier * _damageMultiplier * hurtbox.DamageMultiplier;
            float finalPoise = _currentAttack.PoiseDamage * _poiseDamageMultiplier;

            // Create damage info
            var damageInfo = new DamageInfo(
                finalDamage,
                finalPoise,
                DamageType.Physical,
                _owner,
                transform.position,
                (_owner?.Forward ?? transform.forward),
                _currentAttack.CanBeParried
            );

            // Process damage through combat system
            DamageResult result;
            if (CombatSystem.Instance != null)
            {
                result = CombatSystem.Instance.ProcessDamage(_owner, target, damageInfo);
            }
            else
            {
                // Fallback direct damage
                result = target.TakeDamage(damageInfo);
            }

            // Notify hit
            OnHitConfirmed?.Invoke(this, hurtbox, result);

            // Notify owner
            if (_owner is PlayerController playerController)
            {
                playerController.NotifyHitLanded(target, result);
            }

            if (_debugMode)
                Debug.Log($"[Hitbox] Hit {other.name}: {result.FinalDamage} damage");
        }

        /// <summary>
        /// Get the number of targets hit in this activation.
        /// </summary>
        public int HitCount => _hitTargets.Count;

        private void OnDrawGizmos()
        {
            if (!_debugMode) return;

            Gizmos.color = _isActive ? _debugColor : new Color(_debugColor.r, _debugColor.g, _debugColor.b, 0.3f);

            var col = GetComponent<Collider>();
            if (col is BoxCollider box)
            {
                Gizmos.matrix = transform.localToWorldMatrix;
                Gizmos.DrawWireCube(box.center, box.size);
            }
            else if (col is SphereCollider sphere)
            {
                Gizmos.DrawWireSphere(transform.TransformPoint(sphere.center), sphere.radius);
            }
            else if (col is CapsuleCollider capsule)
            {
                // Simplified capsule gizmo
                Gizmos.DrawWireSphere(transform.position, capsule.radius);
            }
        }
    }
}
