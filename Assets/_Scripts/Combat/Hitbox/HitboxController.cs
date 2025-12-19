using System;
using System.Collections.Generic;
using _Scripts.Combat.Data;
using _Scripts.Combat.Interfaces;
using UnityEngine;

namespace _Scripts.Combat.Hitbox
{
    /// <summary>
    /// Controls all hitboxes on a character.
    /// Manages activation/deactivation and coordinates with animations.
    /// </summary>
    public class HitboxController : MonoBehaviour
    {
        /// <summary>
        /// Named hitbox group for attack definitions.
        /// </summary>
        [Serializable]
        public class HitboxGroup
        {
            public string groupName;
            public Hitbox[] hitboxes;
        }

        [Header("Configuration")]
        [SerializeField]
        [Tooltip("All hitbox groups on this character")]
        private HitboxGroup[] _hitboxGroups;

        [SerializeField]
        [Tooltip("Default attack data if none provided")]
        private AttackData _defaultAttackData;

        [Header("Debug")]
        [SerializeField]
        private bool _debugMode = false;

        // Runtime state
        private Dictionary<string, HitboxGroup> _groupLookup = new Dictionary<string, HitboxGroup>();
        private HashSet<Hitbox> _activeHitboxes = new HashSet<Hitbox>();
        private ICombatant _owner;
        private AttackData _currentAttack;

        // Events
        public event Action<Hitbox, Hurtbox, DamageResult> OnHitConfirmed;
        public event Action<int> OnAttackComplete; // Total hits from attack

        /// <summary>
        /// Whether any hitbox is currently active.
        /// </summary>
        public bool HasActiveHitboxes => _activeHitboxes.Count > 0;

        /// <summary>
        /// Number of currently active hitboxes.
        /// </summary>
        public int ActiveHitboxCount => _activeHitboxes.Count;

        private void Awake()
        {
            // Build lookup dictionary
            foreach (var group in _hitboxGroups)
            {
                if (!string.IsNullOrEmpty(group.groupName))
                {
                    _groupLookup[group.groupName] = group;
                }
            }

            // Find owner
            _owner = GetComponentInParent<ICombatant>();

            // Set owner on all hitboxes
            foreach (var group in _hitboxGroups)
            {
                foreach (var hitbox in group.hitboxes)
                {
                    if (hitbox != null)
                    {
                        hitbox.Owner = _owner;
                        hitbox.OnHitConfirmed += HandleHitConfirmed;
                    }
                }
            }
        }

        private void OnDestroy()
        {
            // Unsubscribe from events
            foreach (var group in _hitboxGroups)
            {
                foreach (var hitbox in group.hitboxes)
                {
                    if (hitbox != null)
                    {
                        hitbox.OnHitConfirmed -= HandleHitConfirmed;
                    }
                }
            }
        }

        #region Activation Methods

        /// <summary>
        /// Activate hitboxes by group name.
        /// </summary>
        /// <param name="groupName">Name of the hitbox group to activate</param>
        /// <param name="attackData">Attack data for this activation</param>
        public void ActivateGroup(string groupName, AttackData attackData)
        {
            if (!_groupLookup.TryGetValue(groupName, out var group))
            {
                if (_debugMode)
                    Debug.LogWarning($"[HitboxController] Group not found: {groupName}");
                return;
            }

            _currentAttack = attackData;

            foreach (var hitbox in group.hitboxes)
            {
                if (hitbox != null)
                {
                    hitbox.Activate(attackData);
                    _activeHitboxes.Add(hitbox);
                }
            }

            if (_debugMode)
                Debug.Log($"[HitboxController] Activated group: {groupName}");
        }

        /// <summary>
        /// Activate hitboxes by group name using default attack data.
        /// </summary>
        public void ActivateGroup(string groupName)
        {
            ActivateGroup(groupName, _defaultAttackData);
        }

        /// <summary>
        /// Deactivate hitboxes by group name.
        /// </summary>
        public void DeactivateGroup(string groupName)
        {
            if (!_groupLookup.TryGetValue(groupName, out var group))
            {
                return;
            }

            int totalHits = 0;
            foreach (var hitbox in group.hitboxes)
            {
                if (hitbox != null && hitbox.IsActive)
                {
                    totalHits += hitbox.HitCount;
                    hitbox.Deactivate();
                    _activeHitboxes.Remove(hitbox);
                }
            }

            if (_debugMode)
                Debug.Log($"[HitboxController] Deactivated group: {groupName}, hits: {totalHits}");

            if (totalHits > 0)
            {
                OnAttackComplete?.Invoke(totalHits);
            }
        }

        /// <summary>
        /// Activate all hitboxes in a specific index group.
        /// Used for simple setups with indexed groups.
        /// </summary>
        public void ActivateByIndex(int index, AttackData attackData)
        {
            if (index < 0 || index >= _hitboxGroups.Length)
            {
                if (_debugMode)
                    Debug.LogWarning($"[HitboxController] Invalid group index: {index}");
                return;
            }

            _currentAttack = attackData;
            var group = _hitboxGroups[index];

            foreach (var hitbox in group.hitboxes)
            {
                if (hitbox != null)
                {
                    hitbox.Activate(attackData);
                    _activeHitboxes.Add(hitbox);
                }
            }
        }

        /// <summary>
        /// Deactivate hitboxes by index.
        /// </summary>
        public void DeactivateByIndex(int index)
        {
            if (index < 0 || index >= _hitboxGroups.Length)
            {
                return;
            }

            int totalHits = 0;
            var group = _hitboxGroups[index];

            foreach (var hitbox in group.hitboxes)
            {
                if (hitbox != null && hitbox.IsActive)
                {
                    totalHits += hitbox.HitCount;
                    hitbox.Deactivate();
                    _activeHitboxes.Remove(hitbox);
                }
            }

            if (totalHits > 0)
            {
                OnAttackComplete?.Invoke(totalHits);
            }
        }

        /// <summary>
        /// Deactivate all active hitboxes.
        /// </summary>
        public void DeactivateAll()
        {
            int totalHits = 0;

            foreach (var hitbox in _activeHitboxes)
            {
                if (hitbox != null)
                {
                    totalHits += hitbox.HitCount;
                    hitbox.Deactivate();
                }
            }

            _activeHitboxes.Clear();

            if (_debugMode)
                Debug.Log($"[HitboxController] Deactivated all hitboxes, total hits: {totalHits}");

            if (totalHits > 0)
            {
                OnAttackComplete?.Invoke(totalHits);
            }
        }

        #endregion

        #region Animation Event Methods

        /// <summary>
        /// Animation event: Start attack hitbox.
        /// Called from animation events with group name as string parameter.
        /// </summary>
        public void AnimEvent_StartHitbox(string groupName)
        {
            ActivateGroup(groupName, _currentAttack.DamageMultiplier > 0 ? _currentAttack : _defaultAttackData);
        }

        /// <summary>
        /// Animation event: End attack hitbox.
        /// Called from animation events with group name as string parameter.
        /// </summary>
        public void AnimEvent_EndHitbox(string groupName)
        {
            DeactivateGroup(groupName);
        }

        /// <summary>
        /// Animation event: Start hitbox by index.
        /// Called from animation events with int parameter.
        /// </summary>
        public void AnimEvent_StartHitboxByIndex(int index)
        {
            ActivateByIndex(index, _currentAttack.DamageMultiplier > 0 ? _currentAttack : _defaultAttackData);
        }

        /// <summary>
        /// Animation event: End hitbox by index.
        /// </summary>
        public void AnimEvent_EndHitboxByIndex(int index)
        {
            DeactivateByIndex(index);
        }

        /// <summary>
        /// Animation event: End all hitboxes.
        /// </summary>
        public void AnimEvent_EndAllHitboxes()
        {
            DeactivateAll();
        }

        #endregion

        /// <summary>
        /// Set attack data for upcoming hitbox activation.
        /// Call before animation triggers hitbox.
        /// </summary>
        public void SetAttackData(AttackData attackData)
        {
            _currentAttack = attackData;
        }

        private void HandleHitConfirmed(Hitbox hitbox, Hurtbox hurtbox, DamageResult result)
        {
            OnHitConfirmed?.Invoke(hitbox, hurtbox, result);
        }

        /// <summary>
        /// Get a hitbox group by name.
        /// </summary>
        public HitboxGroup GetGroup(string groupName)
        {
            _groupLookup.TryGetValue(groupName, out var group);
            return group;
        }
    }
}
