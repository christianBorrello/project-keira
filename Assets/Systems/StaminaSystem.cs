using System;
using UnityEngine;

namespace Systems
{
    /// <summary>
    /// Manages stamina for a combatant.
    /// Handles consumption, regeneration, and events.
    /// </summary>
    public class StaminaSystem
    {
        private readonly float _maxStamina;
        private readonly float _regenRate;
        private readonly float _regenDelay;

        private float _currentStamina;
        private float _lastUseTime;
        private bool _isExhausted;
        private float _exhaustionRecoveryThreshold;

        /// <summary>
        /// Current stamina value.
        /// </summary>
        public float CurrentStamina => _currentStamina;

        /// <summary>
        /// Maximum stamina value.
        /// </summary>
        public float MaxStamina => _maxStamina;

        /// <summary>
        /// Normalized stamina (0-1).
        /// </summary>
        public float Normalized => _maxStamina > 0 ? _currentStamina / _maxStamina : 0f;

        /// <summary>
        /// Whether the system is in exhausted state (prevents actions until recovery).
        /// </summary>
        public bool IsExhausted => _isExhausted;

        /// <summary>
        /// Fired when stamina value changes.
        /// Parameters: (currentStamina, maxStamina, delta)
        /// </summary>
        public event Action<float, float, float> OnStaminaChanged;

        /// <summary>
        /// Fired when entering exhausted state.
        /// </summary>
        public event Action OnExhausted;

        /// <summary>
        /// Fired when recovering from exhausted state.
        /// </summary>
        public event Action OnExhaustionRecovered;

        /// <summary>
        /// Create a new stamina system.
        /// </summary>
        /// <param name="maxStamina">Maximum stamina value</param>
        /// <param name="regenRate">Stamina regenerated per second</param>
        /// <param name="regenDelay">Delay before regeneration starts</param>
        /// <param name="exhaustionRecoveryThreshold">Percentage of max stamina needed to recover from exhaustion (0-1)</param>
        public StaminaSystem(float maxStamina, float regenRate, float regenDelay, float exhaustionRecoveryThreshold = 0.2f)
        {
            _maxStamina = maxStamina;
            _regenRate = regenRate;
            _regenDelay = regenDelay;
            _exhaustionRecoveryThreshold = Mathf.Clamp01(exhaustionRecoveryThreshold);

            _currentStamina = maxStamina;
            _lastUseTime = -regenDelay; // Allow immediate regen at start
            _isExhausted = false;
        }

        /// <summary>
        /// Attempt to consume stamina.
        /// </summary>
        /// <param name="amount">Amount to consume</param>
        /// <returns>True if stamina was consumed, false if not enough stamina</returns>
        public bool TryConsume(float amount)
        {
            if (_isExhausted || _currentStamina < amount)
            {
                return false;
            }

            Consume(amount);
            return true;
        }

        /// <summary>
        /// Force consume stamina (can go negative/zero).
        /// Use TryConsume for action validation.
        /// </summary>
        /// <param name="amount">Amount to consume</param>
        public void Consume(float amount)
        {
            float previousStamina = _currentStamina;
            _currentStamina = Mathf.Max(0, _currentStamina - amount);
            _lastUseTime = Time.time;

            float delta = _currentStamina - previousStamina;
            OnStaminaChanged?.Invoke(_currentStamina, _maxStamina, delta);

            // Check for exhaustion
            if (_currentStamina <= 0 && !_isExhausted)
            {
                _isExhausted = true;
                OnExhausted?.Invoke();
            }
        }

        /// <summary>
        /// Consume stamina continuously (e.g., sprinting).
        /// Automatically scaled by deltaTime.
        /// </summary>
        /// <param name="ratePerSecond">Consumption rate per second</param>
        /// <returns>True if still has stamina, false if depleted</returns>
        public bool ConsumeContinuous(float ratePerSecond)
        {
            if (_isExhausted)
            {
                return false;
            }

            Consume(ratePerSecond * Time.deltaTime);
            return _currentStamina > 0;
        }

        /// <summary>
        /// Check if an action can be performed.
        /// </summary>
        /// <param name="cost">Stamina cost of the action</param>
        /// <returns>True if enough stamina and not exhausted</returns>
        public bool CanPerformAction(float cost)
        {
            return !_isExhausted && _currentStamina >= cost;
        }

        /// <summary>
        /// Update stamina regeneration. Call every frame.
        /// </summary>
        public void Update()
        {
            // Don't regenerate if recently used stamina
            if (Time.time - _lastUseTime < _regenDelay)
            {
                return;
            }

            // Regenerate stamina
            if (_currentStamina < _maxStamina)
            {
                float previousStamina = _currentStamina;
                _currentStamina = Mathf.Min(_maxStamina, _currentStamina + _regenRate * Time.deltaTime);

                float delta = _currentStamina - previousStamina;
                if (delta > 0)
                {
                    OnStaminaChanged?.Invoke(_currentStamina, _maxStamina, delta);
                }

                // Check for exhaustion recovery
                if (_isExhausted && _currentStamina >= _maxStamina * _exhaustionRecoveryThreshold)
                {
                    _isExhausted = false;
                    OnExhaustionRecovered?.Invoke();
                }
            }
        }

        /// <summary>
        /// Restore stamina instantly.
        /// </summary>
        /// <param name="amount">Amount to restore</param>
        /// <returns>Actual amount restored</returns>
        public float Restore(float amount)
        {
            float previousStamina = _currentStamina;
            _currentStamina = Mathf.Min(_maxStamina, _currentStamina + amount);

            float actualRestored = _currentStamina - previousStamina;

            if (actualRestored > 0)
            {
                OnStaminaChanged?.Invoke(_currentStamina, _maxStamina, actualRestored);

                // Check for exhaustion recovery
                if (_isExhausted && _currentStamina >= _maxStamina * _exhaustionRecoveryThreshold)
                {
                    _isExhausted = false;
                    OnExhaustionRecovered?.Invoke();
                }
            }

            return actualRestored;
        }

        /// <summary>
        /// Reset stamina to maximum.
        /// </summary>
        public void Reset()
        {
            _currentStamina = _maxStamina;
            _isExhausted = false;
            _lastUseTime = -_regenDelay;
            OnStaminaChanged?.Invoke(_currentStamina, _maxStamina, 0);
        }

        /// <summary>
        /// Set a temporary stamina modifier (e.g., from buffs/debuffs).
        /// </summary>
        /// <param name="multiplier">Regen rate multiplier (1.0 = normal)</param>
        public float RegenRateMultiplier { get; set; } = 1f;

        /// <summary>
        /// Get actual regen rate with modifiers applied.
        /// </summary>
        public float ActualRegenRate => _regenRate * RegenRateMultiplier;
    }
}
