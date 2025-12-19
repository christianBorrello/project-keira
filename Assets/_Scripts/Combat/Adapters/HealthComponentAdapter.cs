using Ilumisoft.Health_System.Scripts.Base;
using _Scripts.Combat.Interfaces;
using UnityEngine;

namespace _Scripts.Combat.Adapters
{
    /// <summary>
    /// Adapter that bridges IDamageable (custom combat system)
    /// with HealthComponent (Ilumisoft Health System).
    /// Attach to enemy and assign to Healthbar.Health field.
    /// </summary>
    [AddComponentMenu("Combat/Health Component Adapter")]
    public class HealthComponentAdapter : HealthComponent
    {
        [SerializeField, Tooltip("Auto-finds IDamageable in parent if not set")]
        private GameObject targetObject;

        private IDamageable _damageable;

        public override float MaxHealth
        {
            get => _damageable?.MaxHealth ?? 0f;
            set { } // Read-only from combat system
        }

        public override float CurrentHealth
        {
            get => _damageable?.CurrentHealth ?? 0f;
            set { } // Read-only from combat system
        }

        public override bool IsAlive => _damageable?.IsAlive ?? false;

        private void Awake()
        {
            FindDamageable();
        }

        private void FindDamageable()
        {
            // Find IDamageable reference
            if (targetObject != null)
            {
                _damageable = targetObject.GetComponent<IDamageable>();
            }

            if (_damageable == null)
            {
                _damageable = GetComponentInParent<IDamageable>();
            }

            if (_damageable == null)
            {
                Debug.LogError($"[HealthComponentAdapter] No IDamageable found on {gameObject.name}");
            }
        }

        private void OnEnable()
        {
            if (_damageable != null)
            {
                _damageable.OnHealthChanged += HandleHealthChanged;
                _damageable.OnDeath += HandleDeath;
            }
        }

        private void OnDisable()
        {
            if (_damageable != null)
            {
                _damageable.OnHealthChanged -= HandleHealthChanged;
                _damageable.OnDeath -= HandleDeath;
            }
        }

        private void HandleHealthChanged(float current, float max, float delta)
        {
            // Forward to Ilumisoft event system
            OnHealthChanged?.Invoke(current);
        }

        private void HandleDeath()
        {
            OnHealthEmpty?.Invoke();
        }

        // Required overrides - forward to combat system when possible
        public override void AddHealth(float amount)
        {
            _damageable?.Heal(amount);
        }

        public override void ApplyDamage(float damage)
        {
            // Damage should be handled by combat system via TakeDamage()
            // This is here for Ilumisoft compatibility but shouldn't be used directly
            Debug.LogWarning("[HealthComponentAdapter] Use TakeDamage() on IDamageable instead of ApplyDamage()");
        }

        public override void SetHealth(float health)
        {
            // Not supported - health is managed by combat system
        }

        // Editor helper to auto-find reference
        private void Reset()
        {
            if (targetObject == null)
            {
                var damageable = GetComponentInParent<IDamageable>();
                if (damageable != null)
                {
                    targetObject = (damageable as MonoBehaviour)?.gameObject;
                }
            }
        }
    }
}
