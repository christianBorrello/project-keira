using System;
using _Scripts.Combat.Data;
using _Scripts.Combat.Interfaces;
using _Scripts.Core.Events;
using _Scripts.Core.Events.Types;
using UnityEngine;

namespace _Scripts.Player.Components
{
    /// <summary>
    /// Manages health, poise, and stamina for the player.
    /// Implements IDamageable and IDamageableWithPoise interfaces.
    /// </summary>
    [RequireComponent(typeof(PlayerController))]
    public class HealthPoiseController : MonoBehaviour, IDamageable, IDamageableWithPoise
    {
        #region ScriptableObject Events

        [Header("ScriptableObject Events (Optional)")]
        [Tooltip("Raised when health changes. Assign OnPlayerHealthChanged.asset")]
        [SerializeField] private HealthEvent _onHealthChangedEvent;

        [Tooltip("Raised when player dies. Assign OnPlayerDeath.asset")]
        [SerializeField] private GameEvent _onDeathEvent;

        [Tooltip("Raised when poise breaks. Assign OnPoiseBreak.asset")]
        [SerializeField] private GameEvent _onPoiseBreakEvent;

        [Tooltip("Raised on successful parry. Assign OnParrySuccess.asset")]
        [SerializeField] private GameEvent _onParrySuccessEvent;

        #endregion

        // References
        private PlayerController _player;
        private PlayerStateMachine _stateMachine;

        // Runtime data (owned by this component)
        private CombatRuntimeData _runtimeData;

        // Cached stats reference
        private CombatStats _baseStats;

        #region IDamageable Implementation

        public float CurrentHealth => _runtimeData.CurrentHealth;
        public float MaxHealth => _baseStats.MaxHealth;
        public float HealthNormalized => MaxHealth > 0 ? CurrentHealth / MaxHealth : 0f;
        public bool IsAlive => _runtimeData.IsAlive;

        public DamageResult TakeDamage(DamageInfo damageInfo)
        {
            if (!IsAlive) return new DamageResult(0, 0);

            // Check invulnerability (i-frames) - state from PlayerController
            if (_player.IsInvulnerable)
            {
                return new DamageResult(0, 0, wasDodged: true);
            }

            // Check parry (including Block's parry window)
            if (_player.IsParrying(out var parryTiming) && damageInfo.CanBeParried)
            {
                if (parryTiming.IsPerfect)
                {
                    // Perfect parry - no damage, stagger attacker
                    if (damageInfo.Source != null)
                    {
                        damageInfo.Source.ApplyStagger(_baseStats.StaggerRecoveryTime);
                    }
                    OnParrySuccess?.Invoke(true);
                    _onParrySuccessEvent?.Raise();
                    return new DamageResult(0, 0, wasParried: true);
                }
                else if (parryTiming.IsPartial)
                {
                    // Partial parry - reduced damage
                    float reducedDamage = damageInfo.Amount * _baseStats.PartialParryDamageReduction;
                    ApplyDamageInternal(reducedDamage, damageInfo.PoiseDamage * 0.5f);
                    OnParrySuccess?.Invoke(false);
                    _onParrySuccessEvent?.Raise();
                    return new DamageResult(reducedDamage, damageInfo.PoiseDamage * 0.5f, wasPartialParried: true);
                }
            }

            // Check blocking (after parry check - passive damage reduction)
            if (_player.IsBlocking)
            {
                // Get block state for damage reduction values
                var blockState = _stateMachine?.CurrentState as States.PlayerBlockState;
                float reduction = blockState?.BlockDamageReduction ?? 0.5f;
                float staminaCost = blockState?.BlockStaminaCostOnHit ?? 15f;

                // Reduce damage by block percentage
                float blockedDamage = damageInfo.Amount * (1f - _baseStats.PhysicalDefense) * reduction;

                // Consume stamina on blocked hit
                ConsumeStamina(staminaCost);

                // Apply reduced damage (no poise damage when blocking)
                ApplyDamageInternal(blockedDamage, 0);

                OnBlocked?.Invoke(damageInfo);
                return new DamageResult(blockedDamage, 0, wasBlocked: true);
            }

            // Apply full damage
            float finalDamage = damageInfo.Amount * (1f - _baseStats.PhysicalDefense);
            bool causedPoiseBreak = ApplyDamageInternal(finalDamage, damageInfo.PoiseDamage);
            bool causedDeath = !IsAlive;

            var result = new DamageResult(
                finalDamage,
                damageInfo.PoiseDamage,
                causedPoiseBreak: causedPoiseBreak,
                causedDeath: causedDeath
            );

            OnDamaged?.Invoke(damageInfo, result);

            if (causedDeath)
            {
                Die();
            }
            else if (causedPoiseBreak)
            {
                ApplyStagger(_baseStats.StaggerRecoveryTime);
            }

            return result;
        }

        private bool ApplyDamageInternal(float damage, float poiseDamage)
        {
            float previousHealth = _runtimeData.CurrentHealth;
            _runtimeData.CurrentHealth = Mathf.Max(0, _runtimeData.CurrentHealth - Mathf.RoundToInt(damage));

            float delta = _runtimeData.CurrentHealth - previousHealth;
            OnHealthChanged?.Invoke(_runtimeData.CurrentHealth, _baseStats.MaxHealth, delta);
            _onHealthChangedEvent?.Raise(new HealthEventData(_runtimeData.CurrentHealth, _baseStats.MaxHealth, delta));

            // Apply poise damage
            return ApplyPoiseDamage(poiseDamage);
        }

        public float Heal(float amount)
        {
            if (!IsAlive) return 0f;

            float previousHealth = _runtimeData.CurrentHealth;
            _runtimeData.CurrentHealth = Mathf.Min(_baseStats.MaxHealth, _runtimeData.CurrentHealth + Mathf.RoundToInt(amount));

            float actualHeal = _runtimeData.CurrentHealth - previousHealth;
            OnHealthChanged?.Invoke(_runtimeData.CurrentHealth, _baseStats.MaxHealth, actualHeal);
            _onHealthChangedEvent?.Raise(new HealthEventData(_runtimeData.CurrentHealth, _baseStats.MaxHealth, actualHeal));

            return actualHeal;
        }

        public void Die()
        {
            _stateMachine?.ForceInterrupt(PlayerState.Death);
            OnDeath?.Invoke();
            _onDeathEvent?.Raise();
        }

        public event Action<float, float, float> OnHealthChanged;
        public event Action<DamageInfo, DamageResult> OnDamaged;
        public event Action OnDeath;
        public event Action<bool> OnParrySuccess; // true = perfect, false = partial
        public event Action<DamageInfo> OnBlocked;

        #endregion

        #region IDamageableWithPoise Implementation

        public int CurrentPoise => _runtimeData.CurrentPoise;
        public int MaxPoise => _baseStats.MaxPoise;
        public float PoiseNormalized => MaxPoise > 0 ? (float)CurrentPoise / MaxPoise : 0f;

        public bool ApplyPoiseDamage(float amount)
        {
            _runtimeData.CurrentPoise += Mathf.RoundToInt(amount);
            _runtimeData.LastPoiseHitTime = Time.time;

            if (_runtimeData.IsPoisebroken(_baseStats.MaxPoise))
            {
                OnPoiseBreak?.Invoke();
                _onPoiseBreakEvent?.Raise();
                return true;
            }
            return false;
        }

        public void ResetPoise()
        {
            _runtimeData.CurrentPoise = 0;
        }

        public void ApplyStagger(float duration)
        {
            _stateMachine?.ForceInterrupt(PlayerState.Stagger);
            OnStaggered?.Invoke(duration);
        }

        public event Action OnPoiseBreak;
        public event Action<float> OnStaggered;

        #endregion

        #region Stamina

        public float CurrentStamina => _runtimeData.CurrentStamina;
        public float MaxStamina => _baseStats.MaxStamina;
        public float StaminaNormalized => MaxStamina > 0 ? CurrentStamina / MaxStamina : 0f;

        public void ConsumeStamina(float amount)
        {
            _runtimeData.CurrentStamina = Mathf.Max(0, _runtimeData.CurrentStamina - amount);
            _runtimeData.LastStaminaUseTime = Time.time;
        }

        public bool HasStamina(float amount)
        {
            return _runtimeData.CurrentStamina >= amount;
        }

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            _player = GetComponent<PlayerController>();
            _stateMachine = GetComponent<PlayerStateMachine>();
        }

        /// <summary>
        /// Initialize with combat stats. Called by PlayerController.
        /// </summary>
        public void Initialize(CombatStats stats)
        {
            _baseStats = stats;
            _runtimeData = new CombatRuntimeData(stats);
        }

        private void Update()
        {
            UpdateStaminaRegen();
            UpdatePoiseRegen();
        }

        private void UpdateStaminaRegen()
        {
            if (Time.time - _runtimeData.LastStaminaUseTime >= _baseStats.StaminaRegenDelay)
            {
                _runtimeData.CurrentStamina = Mathf.Min(
                    _baseStats.MaxStamina,
                    _runtimeData.CurrentStamina + _baseStats.StaminaRegenRate * Time.deltaTime
                );
            }
        }

        private void UpdatePoiseRegen()
        {
            if (_runtimeData.CurrentPoise > 0 &&
                Time.time - _runtimeData.LastPoiseHitTime >= _baseStats.PoiseRegenDelay)
            {
                _runtimeData.CurrentPoise = Mathf.Max(
                    0,
                    _runtimeData.CurrentPoise - Mathf.RoundToInt(_baseStats.PoiseRegenRate * Time.deltaTime)
                );
            }
        }

        #endregion

        #region Data Access

        /// <summary>
        /// Get runtime data for external systems (e.g., UI).
        /// </summary>
        public CombatRuntimeData GetRuntimeData() => _runtimeData;

        /// <summary>
        /// Get base stats reference.
        /// </summary>
        public CombatStats GetBaseStats() => _baseStats;

        #endregion
    }
}
