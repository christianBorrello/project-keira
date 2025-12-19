using System;
using UnityEngine;

namespace Systems
{
    /// <summary>
    /// Manages poise (stagger resistance) using Lies of P style accumulative system.
    /// Poise damage accumulates over time and triggers stagger when threshold is reached.
    /// </summary>
    public class PoiseSystem
    {
        private readonly int _maxPoise;
        private readonly float _regenRate;
        private readonly float _regenDelay;

        private int _currentPoise;
        private float _lastDamageTime;
        private bool _isPoisebroken;

        /// <summary>
        /// Current accumulated poise damage.
        /// </summary>
        public int CurrentPoise => _currentPoise;

        /// <summary>
        /// Maximum poise before break.
        /// </summary>
        public int MaxPoise => _maxPoise;

        /// <summary>
        /// Normalized poise value (0-1, where 1 means poise broken).
        /// </summary>
        public float Normalized => _maxPoise > 0 ? (float)_currentPoise / _maxPoise : 0f;

        /// <summary>
        /// Whether poise is currently broken.
        /// </summary>
        public bool IsPoisebroken => _isPoisebroken;

        /// <summary>
        /// Remaining poise before break.
        /// </summary>
        public int RemainingPoise => Mathf.Max(0, _maxPoise - _currentPoise);

        /// <summary>
        /// Fired when poise breaks (accumulated damage exceeds max).
        /// </summary>
        public event Action OnPoiseBreak;

        /// <summary>
        /// Fired when poise resets from broken state.
        /// </summary>
        public event Action OnPoiseReset;

        /// <summary>
        /// Fired when poise damage is taken.
        /// Parameters: (currentPoise, maxPoise, damageAmount)
        /// </summary>
        public event Action<int, int, float> OnPoiseDamage;

        /// <summary>
        /// Fired when poise regenerates.
        /// Parameters: (currentPoise, maxPoise, regenAmount)
        /// </summary>
        public event Action<int, int, float> OnPoiseRegen;

        /// <summary>
        /// Create a new poise system.
        /// </summary>
        /// <param name="maxPoise">Maximum poise before break</param>
        /// <param name="regenRate">Poise reduced per second during regen</param>
        /// <param name="regenDelay">Delay before regeneration starts</param>
        public PoiseSystem(int maxPoise, float regenRate, float regenDelay)
        {
            _maxPoise = maxPoise;
            _regenRate = regenRate;
            _regenDelay = regenDelay;

            _currentPoise = 0;
            _lastDamageTime = -regenDelay;
            _isPoisebroken = false;
        }

        /// <summary>
        /// Apply poise damage.
        /// </summary>
        /// <param name="amount">Amount of poise damage to apply</param>
        /// <returns>True if poise broke from this damage</returns>
        public bool ApplyDamage(float amount)
        {
            if (amount <= 0) return false;

            int intAmount = Mathf.RoundToInt(amount);
            _currentPoise += intAmount;
            _lastDamageTime = Time.time;

            OnPoiseDamage?.Invoke(_currentPoise, _maxPoise, amount);

            // Check for poise break
            if (!_isPoisebroken && _currentPoise >= _maxPoise)
            {
                _isPoisebroken = true;
                OnPoiseBreak?.Invoke();
                return true;
            }

            return false;
        }

        /// <summary>
        /// Check if specific amount of poise damage would cause a break.
        /// </summary>
        public bool WouldBreak(float amount)
        {
            return (_currentPoise + amount) >= _maxPoise;
        }

        /// <summary>
        /// Update poise regeneration. Call every frame.
        /// </summary>
        public void Update()
        {
            // Can't regen while broken (must be explicitly reset)
            if (_isPoisebroken) return;

            // Don't regen if recently damaged
            if (Time.time - _lastDamageTime < _regenDelay) return;

            // Regenerate poise (reduce accumulated damage)
            if (_currentPoise > 0)
            {
                float regenAmount = _regenRate * Time.deltaTime;
                int previousPoise = _currentPoise;

                _currentPoise = Mathf.Max(0, _currentPoise - Mathf.RoundToInt(regenAmount));

                if (_currentPoise < previousPoise)
                {
                    OnPoiseRegen?.Invoke(_currentPoise, _maxPoise, regenAmount);
                }
            }
        }

        /// <summary>
        /// Reset poise to zero (after stagger recovery).
        /// </summary>
        public void Reset()
        {
            bool wasPoisebroken = _isPoisebroken;

            _currentPoise = 0;
            _isPoisebroken = false;
            _lastDamageTime = -_regenDelay;

            if (wasPoisebroken)
            {
                OnPoiseReset?.Invoke();
            }
        }

        /// <summary>
        /// Force poise break (for special attacks).
        /// </summary>
        public void ForceBreak()
        {
            if (_isPoisebroken) return;

            _currentPoise = _maxPoise;
            _isPoisebroken = true;
            OnPoiseBreak?.Invoke();
        }

        /// <summary>
        /// Set poise to specific value (for loading/special effects).
        /// </summary>
        public void SetPoise(int value)
        {
            _currentPoise = Mathf.Clamp(value, 0, _maxPoise);
            _isPoisebroken = _currentPoise >= _maxPoise;
        }

        /// <summary>
        /// Get poise damage required to break.
        /// </summary>
        public int GetDamageToBreak()
        {
            return Mathf.Max(0, _maxPoise - _currentPoise);
        }

        /// <summary>
        /// Check if poise is in danger zone (configurable threshold).
        /// </summary>
        public bool IsInDangerZone(float threshold = 0.7f)
        {
            return Normalized >= threshold;
        }
    }

    /// <summary>
    /// MonoBehaviour wrapper for PoiseSystem to use in Unity inspector.
    /// </summary>
    public class PoiseSystemComponent : MonoBehaviour
    {
        [Header("Configuration")]
        [SerializeField]
        [Tooltip("Maximum poise before stagger")]
        private int _maxPoise = 100;

        [SerializeField]
        [Tooltip("Poise regeneration rate (per second)")]
        private float _regenRate = 20f;

        [SerializeField]
        [Tooltip("Delay before poise starts regenerating")]
        private float _regenDelay = 2f;

        [Header("Visual Feedback")]
        [SerializeField]
        [Tooltip("Warning threshold for UI")]
        private float _dangerThreshold = 0.7f;

        private PoiseSystem _poiseSystem;

        /// <summary>
        /// Access the underlying poise system.
        /// </summary>
        public PoiseSystem System => _poiseSystem;

        public int CurrentPoise => _poiseSystem?.CurrentPoise ?? 0;
        public int MaxPoise => _poiseSystem?.MaxPoise ?? _maxPoise;
        public float Normalized => _poiseSystem?.Normalized ?? 0f;
        public bool IsPoisebroken => _poiseSystem?.IsPoisebroken ?? false;
        public bool IsInDangerZone => _poiseSystem?.IsInDangerZone(_dangerThreshold) ?? false;

        // Events passthrough
        public event Action OnPoiseBreak;
        public event Action OnPoiseReset;
        public event Action<int, int, float> OnPoiseDamage;
        public event Action<int, int, float> OnPoiseRegen;

        private void Awake()
        {
            _poiseSystem = new PoiseSystem(_maxPoise, _regenRate, _regenDelay);

            // Wire up events
            _poiseSystem.OnPoiseBreak += () => OnPoiseBreak?.Invoke();
            _poiseSystem.OnPoiseReset += () => OnPoiseReset?.Invoke();
            _poiseSystem.OnPoiseDamage += (current, max, amount) => OnPoiseDamage?.Invoke(current, max, amount);
            _poiseSystem.OnPoiseRegen += (current, max, amount) => OnPoiseRegen?.Invoke(current, max, amount);
        }

        private void Update()
        {
            _poiseSystem?.Update();
        }

        public bool ApplyDamage(float amount) => _poiseSystem?.ApplyDamage(amount) ?? false;
        public void Reset() => _poiseSystem?.Reset();
        public void ForceBreak() => _poiseSystem?.ForceBreak();
    }
}
