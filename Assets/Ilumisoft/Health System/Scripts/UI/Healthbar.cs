using Ilumisoft.Health_System.Scripts.Base;
using UnityEngine;
using UnityEngine.UI;

namespace Ilumisoft.Health_System.Scripts.UI
{
    [AddComponentMenu("Health System/UI/Healthbar")]
    public class Healthbar : MonoBehaviour
    {
        [field:SerializeField]
        public HealthComponent Health { get; set; }

        [SerializeField]
        Canvas canvas;

        [SerializeField]
        Image fillImage;

        [SerializeField, Tooltip("Whether the healthbar should be hidden when health is empty")]
        bool hideEmpty = false;

        [SerializeField, Tooltip("Makes the healthbar being aligned with the camera")]
        bool alignWithCamera = false;

        [SerializeField, Min(0.1f), Tooltip("Controls how fast changes will be animated in points/second")]
        float changeSpeed = 100;

        float currentValue;

        private Camera _cam;

        protected virtual void Reset()
        {
            if (Health == null)
            {
                Health = GetComponentInParent<HealthComponent>();
            }
        }

        private void Start()
        {
            currentValue = Health.CurrentHealth;
            _cam = Camera.main;
        }

        private void Update()
        {
            if (alignWithCamera)
            {
                AlignWithCamera();
            }

            currentValue = Mathf.MoveTowards(currentValue, Health.CurrentHealth, Time.deltaTime * changeSpeed);
            
            UpdateFillbar();
            UpdateVisibility();
        }

        private void AlignWithCamera()
        {
            transform.forward = _cam.transform.forward;
        }

        void UpdateFillbar()
        {
            // Update the fill amount
            float value = Mathf.InverseLerp(0, Health.MaxHealth, currentValue);

            fillImage.fillAmount = value;
        }

        void UpdateVisibility()
        {
            float value = fillImage.fillAmount;

            if (canvas is not null)
            {
                // Hide if empty
                if (Mathf.Approximately(value, 0))
                {
                    if (hideEmpty && canvas.gameObject.activeSelf)
                    {
                        canvas.gameObject.SetActive(false);
                    }
                }
                // Make sure the canvas is enabled if health is not empty
                else if (value > 0 && canvas.gameObject.activeSelf is false)
                {
                    canvas.gameObject.SetActive(true);
                }
            }
        }
    }
}