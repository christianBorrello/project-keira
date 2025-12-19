using _Scripts.Enemies;
using UnityEngine;
using UnityEngine.UI;

namespace _Scripts.UI
{
    /// <summary>
    /// Health bar UI component that displays an enemy's health.
    /// Connects directly to EnemyController's C# events for 1:1 binding.
    /// </summary>
    public class EnemyHealthBar : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField]
        [Tooltip("The slider or fill image representing health")]
        private Slider _healthSlider;

        [SerializeField]
        [Tooltip("Optional: Canvas group for fading")]
        private CanvasGroup _canvasGroup;

        [Header("Behavior")]
        [SerializeField]
        [Tooltip("Hide the health bar when enemy is at full health")]
        private bool _hideWhenFull = true;

        [SerializeField]
        [Tooltip("Hide the health bar when enemy dies")]
        private bool _hideOnDeath = true;

        [SerializeField]
        [Tooltip("Time to fade out on death (0 = instant)")]
        private float _deathFadeTime = 0.5f;

        [Header("Billboard")]
        [SerializeField]
        [Tooltip("Always face the camera")]
        private bool _billboardToCamera = true;

        private EnemyController _enemy;
        private UnityEngine.Camera _mainCamera;
        private bool _isInitialized;

        private void Awake()
        {
            _mainCamera = UnityEngine.Camera.main;

            // Try to find EnemyController on parent if not manually initialized
            if (_enemy == null)
            {
                _enemy = GetComponentInParent<EnemyController>();
                if (_enemy != null)
                {
                    Initialize(_enemy);
                }
            }
        }

        /// <summary>
        /// Initialize the health bar with an enemy controller.
        /// Call this if the health bar is instantiated separately from the enemy.
        /// </summary>
        public void Initialize(EnemyController enemy)
        {
            if (_isInitialized && _enemy != null)
            {
                // Unsubscribe from previous enemy
                _enemy.OnHealthChanged -= HandleHealthChanged;
                _enemy.OnDeath -= HandleDeath;
            }

            _enemy = enemy;

            if (_enemy == null)
            {
                Debug.LogWarning("[EnemyHealthBar] Initialize called with null enemy", this);
                return;
            }

            // Subscribe to events
            _enemy.OnHealthChanged += HandleHealthChanged;
            _enemy.OnDeath += HandleDeath;

            // Initialize with current values
            UpdateHealthDisplay(_enemy.CurrentHealth, _enemy.MaxHealth);

            _isInitialized = true;
        }

        private void LateUpdate()
        {
            if (_billboardToCamera && _mainCamera != null)
            {
                // Face the camera
                transform.rotation = Quaternion.LookRotation(
                    transform.position - _mainCamera.transform.position
                );
            }
        }

        private void HandleHealthChanged(float current, float max, float delta)
        {
            UpdateHealthDisplay(current, max);
        }

        private void UpdateHealthDisplay(float current, float max)
        {
            float normalized = max > 0 ? current / max : 0f;

            if (_healthSlider != null)
            {
                _healthSlider.value = normalized;
            }

            // Hide when full health
            if (_hideWhenFull)
            {
                SetVisible(normalized < 1f);
            }
        }

        private void HandleDeath()
        {
            if (_hideOnDeath)
            {
                if (_deathFadeTime > 0 && _canvasGroup != null)
                {
                    StartCoroutine(FadeOutAndDisable());
                }
                else
                {
                    SetVisible(false);
                }
            }
        }

        private System.Collections.IEnumerator FadeOutAndDisable()
        {
            float elapsed = 0f;
            float startAlpha = _canvasGroup.alpha;

            while (elapsed < _deathFadeTime)
            {
                elapsed += Time.deltaTime;
                _canvasGroup.alpha = Mathf.Lerp(startAlpha, 0f, elapsed / _deathFadeTime);
                yield return null;
            }

            SetVisible(false);
        }

        private void SetVisible(bool visible)
        {
            if (_canvasGroup != null)
            {
                _canvasGroup.alpha = visible ? 1f : 0f;
                _canvasGroup.blocksRaycasts = visible;
            }
            else
            {
                gameObject.SetActive(visible);
            }
        }

        private void OnDestroy()
        {
            // Unsubscribe to prevent memory leaks
            if (_enemy != null)
            {
                _enemy.OnHealthChanged -= HandleHealthChanged;
                _enemy.OnDeath -= HandleDeath;
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            // Auto-find slider if not assigned
            if (_healthSlider == null)
            {
                _healthSlider = GetComponentInChildren<Slider>();
            }
        }
#endif
    }
}
