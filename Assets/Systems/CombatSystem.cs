using System;
using System.Collections.Generic;
using _Scripts.Combat.Data;
using _Scripts.Combat.Interfaces;
using _Scripts.Core.Events.Types;
using _Scripts.Utilities;
using UnityEngine;

namespace Systems
{
    /// <summary>
    /// Central combat system manager.
    /// Coordinates damage resolution, combat events, and combatant tracking.
    /// </summary>
    public class CombatSystem : Singleton<CombatSystem>
    {
        #region ScriptableObject Events

        [Header("ScriptableObject Events (Optional)")]
        [Tooltip("Raised when any damage is dealt. Assign OnDamageDealt.asset")]
        [SerializeField] private DamageEvent _onDamageDealtEvent;

        [Tooltip("Raised when any combatant dies. Assign OnCombatantDeath.asset")]
        [SerializeField] private CombatantEvent _onCombatantDeathEvent;

        [Tooltip("Raised when poise breaks on any combatant. Assign OnPoiseBreak.asset")]
        [SerializeField] private CombatantEvent _onPoiseBreakEvent;

        #endregion

        [Header("Combat Settings")]
        [SerializeField]
        [Tooltip("Time window for hitstop effect")]
        private float _hitstopDuration = 0.05f;

        [SerializeField]
        [Tooltip("Scale for hitstop based on damage")]
        private float _hitstopDamageScale = 0.001f;

        [Header("Debug")]
        [SerializeField]
        private bool _debugMode = false;

        // Active combatants registry
        private readonly Dictionary<int, ICombatant> _activeCombatants = new Dictionary<int, ICombatant>();

        // Combat events
        public event Action<ICombatant, ICombatant, DamageInfo, DamageResult> OnDamageDealt;
        public event Action<ICombatant> OnCombatantDeath;
        public event Action<ICombatant, bool> OnParryOccurred; // combatant who parried, isPerfect
        public event Action<ICombatant> OnPoiseBreak;

        // Hitstop state
        private float _hitstopEndTime;
        private float _originalTimeScale;
        private bool _isInHitstop;

        protected override void Awake()
        {
            base.Awake();
            _originalTimeScale = Time.timeScale;
        }

        private void Update()
        {
            // Handle hitstop recovery
            if (_isInHitstop && Time.unscaledTime >= _hitstopEndTime)
            {
                EndHitstop();
            }
        }

        #region Combatant Registration

        /// <summary>
        /// Register a combatant with the system.
        /// </summary>
        public void RegisterCombatant(ICombatant combatant)
        {
            if (combatant == null) return;

            if (!_activeCombatants.ContainsKey(combatant.CombatantId))
            {
                _activeCombatants[combatant.CombatantId] = combatant;

                if (_debugMode)
                    Debug.Log($"[CombatSystem] Registered combatant: {combatant.CombatantName} (ID: {combatant.CombatantId})");
            }
        }

        /// <summary>
        /// Unregister a combatant from the system.
        /// </summary>
        public void UnregisterCombatant(ICombatant combatant)
        {
            if (combatant == null) return;

            if (_activeCombatants.Remove(combatant.CombatantId))
            {
                if (_debugMode)
                    Debug.Log($"[CombatSystem] Unregistered combatant: {combatant.CombatantName} (ID: {combatant.CombatantId})");
            }
        }

        /// <summary>
        /// Get a combatant by ID.
        /// </summary>
        public ICombatant GetCombatant(int combatantId)
        {
            _activeCombatants.TryGetValue(combatantId, out var combatant);
            return combatant;
        }

        /// <summary>
        /// Get all active combatants.
        /// </summary>
        public IEnumerable<ICombatant> GetAllCombatants()
        {
            return _activeCombatants.Values;
        }

        /// <summary>
        /// Get combatants of a specific faction.
        /// </summary>
        public IEnumerable<ICombatant> GetCombatantsByFaction(Faction faction)
        {
            foreach (var combatant in _activeCombatants.Values)
            {
                if (combatant.Faction == faction && combatant.IsAlive)
                {
                    yield return combatant;
                }
            }
        }

        #endregion

        #region Damage Resolution

        /// <summary>
        /// Process a damage event between attacker and target.
        /// </summary>
        /// <param name="attacker">The attacking combatant</param>
        /// <param name="target">The target receiving damage</param>
        /// <param name="damageInfo">Damage information</param>
        /// <returns>Result of the damage application</returns>
        public DamageResult ProcessDamage(ICombatant attacker, IDamageable target, DamageInfo damageInfo)
        {
            if (target == null)
            {
                return new DamageResult(0, 0);
            }

            // Apply damage to target
            DamageResult result = target.TakeDamage(damageInfo);

            // Get attacker as combatant for events
            ICombatant targetCombatant = target as ICombatant;

            // Fire events
            if (attacker != null && targetCombatant != null)
            {
                OnDamageDealt?.Invoke(attacker, targetCombatant, damageInfo, result);
                _onDamageDealtEvent?.Raise(new DamageEventData(attacker, targetCombatant, damageInfo, result));
            }

            // Handle special results
            if (result.WasParried)
            {
                if (targetCombatant != null)
                {
                    OnParryOccurred?.Invoke(targetCombatant, !result.WasPartialParried);
                }

                if (_debugMode)
                    Debug.Log($"[CombatSystem] Parry! Perfect: {!result.WasPartialParried}");
            }

            if (result.CausedPoiseBreak)
            {
                if (targetCombatant != null)
                {
                    OnPoiseBreak?.Invoke(targetCombatant);
                    _onPoiseBreakEvent?.Raise(targetCombatant);
                }

                if (_debugMode)
                    Debug.Log($"[CombatSystem] Poise break on {targetCombatant?.CombatantName}");
            }

            if (result.CausedDeath)
            {
                if (targetCombatant != null)
                {
                    OnCombatantDeath?.Invoke(targetCombatant);
                    _onCombatantDeathEvent?.Raise(targetCombatant);
                }

                if (_debugMode)
                    Debug.Log($"[CombatSystem] {targetCombatant?.CombatantName} defeated!");
            }

            // Apply hitstop effect for significant hits
            if (result.FinalDamage > 0 && !result.WasDodged)
            {
                float hitstopTime = _hitstopDuration + (result.FinalDamage * _hitstopDamageScale);
                ApplyHitstop(hitstopTime);
            }

            return result;
        }

        /// <summary>
        /// Check if two combatants are hostile to each other.
        /// </summary>
        public bool AreHostile(ICombatant a, ICombatant b)
        {
            if (a == null || b == null) return false;
            if (a == b) return false;

            return a.Faction != b.Faction;
        }

        /// <summary>
        /// Check if two combatants are allies.
        /// </summary>
        public bool AreAllies(ICombatant a, ICombatant b)
        {
            if (a == null || b == null) return false;
            if (a == b) return true;

            return a.Faction == b.Faction;
        }

        #endregion

        #region Hitstop

        /// <summary>
        /// Apply hitstop effect (brief time slowdown on hit).
        /// </summary>
        /// <param name="duration">Duration of hitstop in unscaled time</param>
        public void ApplyHitstop(float duration)
        {
            if (duration <= 0) return;

            // Extend existing hitstop if longer
            float newEndTime = Time.unscaledTime + duration;
            if (_isInHitstop && newEndTime <= _hitstopEndTime)
            {
                return;
            }

            _hitstopEndTime = newEndTime;

            if (!_isInHitstop)
            {
                _isInHitstop = true;
                _originalTimeScale = Time.timeScale;
                Time.timeScale = 0f;

                if (_debugMode)
                    Debug.Log($"[CombatSystem] Hitstop started for {duration}s");
            }
        }

        private void EndHitstop()
        {
            if (!_isInHitstop) return;

            _isInHitstop = false;
            Time.timeScale = _originalTimeScale;

            if (_debugMode)
                Debug.Log("[CombatSystem] Hitstop ended");
        }

        /// <summary>
        /// Force end hitstop immediately.
        /// </summary>
        public void CancelHitstop()
        {
            EndHitstop();
        }

        #endregion

        #region Utility

        /// <summary>
        /// Find the nearest hostile combatant to a position.
        /// </summary>
        public ICombatant FindNearestHostile(ICombatant from, float maxDistance = float.MaxValue)
        {
            if (from == null) return null;

            ICombatant nearest = null;
            float nearestDistance = maxDistance;

            foreach (var combatant in _activeCombatants.Values)
            {
                if (!combatant.IsAlive) continue;
                if (!AreHostile(from, combatant)) continue;

                float distance = Vector3.Distance(from.Position, combatant.Position);
                if (distance < nearestDistance)
                {
                    nearestDistance = distance;
                    nearest = combatant;
                }
            }

            return nearest;
        }

        /// <summary>
        /// Find all hostiles within a radius.
        /// </summary>
        public List<ICombatant> FindHostilesInRadius(ICombatant from, float radius)
        {
            var results = new List<ICombatant>();
            if (from == null) return results;

            float radiusSqr = radius * radius;

            foreach (var combatant in _activeCombatants.Values)
            {
                if (!combatant.IsAlive) continue;
                if (!AreHostile(from, combatant)) continue;

                float distanceSqr = (from.Position - combatant.Position).sqrMagnitude;
                if (distanceSqr <= radiusSqr)
                {
                    results.Add(combatant);
                }
            }

            return results;
        }

        /// <summary>
        /// Calculate direction from one combatant to another.
        /// </summary>
        public Vector3 GetDirectionTo(ICombatant from, ICombatant to)
        {
            if (from == null || to == null) return Vector3.zero;

            Vector3 direction = to.Position - from.Position;
            direction.y = 0;
            return direction.normalized;
        }

        /// <summary>
        /// Check if target is within attack angle.
        /// </summary>
        public bool IsWithinAttackAngle(ICombatant attacker, ICombatant target, float maxAngle)
        {
            if (attacker == null || target == null) return false;

            Vector3 toTarget = GetDirectionTo(attacker, target);
            if (toTarget.sqrMagnitude < 0.001f) return true;

            float angle = Vector3.Angle(attacker.Forward, toTarget);
            return angle <= maxAngle;
        }

        #endregion

        private void OnDestroy()
        {
            // Ensure time scale is restored
            if (_isInHitstop)
            {
                Time.timeScale = _originalTimeScale;
            }
        }
    }
}
