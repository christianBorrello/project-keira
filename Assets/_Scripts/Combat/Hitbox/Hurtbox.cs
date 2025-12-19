using System;
using _Scripts.Combat.Data;
using _Scripts.Combat.Interfaces;
using UnityEngine;

namespace _Scripts.Combat.Hitbox
{
    /// <summary>
    /// Hurtbox component - receives damage from hitboxes.
    /// Attach to body parts that can be damaged.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class Hurtbox : MonoBehaviour
    {
        /// <summary>
        /// Type of hurtbox for damage modifiers.
        /// </summary>
        public enum HurtboxType
        {
            Normal,
            Head,      // Critical hits
            Limb,      // Can be disabled
            Armored,   // Reduced damage
            Weakpoint  // Increased damage
        }

        [Header("Configuration")]
        [SerializeField]
        [Tooltip("Owner of this hurtbox")]
        private MonoBehaviour _ownerComponent;

        [SerializeField]
        [Tooltip("Type of this hurtbox")]
        private HurtboxType _hurtboxType = HurtboxType.Normal;

        [SerializeField]
        [Tooltip("Damage multiplier based on hurtbox type")]
        private float _damageMultiplier = 1f;

        [Header("Debug")]
        [SerializeField]
        private bool _debugMode = false;

        [SerializeField]
        private Color _debugColor = Color.green;

        private IDamageable _owner;
        private Collider _collider;
        private bool _isActive = true;

        /// <summary>
        /// Owner damageable of this hurtbox.
        /// </summary>
        public IDamageable Owner => _owner;

        /// <summary>
        /// Damage multiplier for this hurtbox.
        /// </summary>
        public float DamageMultiplier => _damageMultiplier;

        /// <summary>
        /// Type of this hurtbox.
        /// </summary>
        public HurtboxType Type => _hurtboxType;

        /// <summary>
        /// Whether this hurtbox is active.
        /// </summary>
        public bool IsActive
        {
            get => _isActive;
            set
            {
                _isActive = value;
                if (_collider != null)
                {
                    _collider.enabled = value;
                }
            }
        }

        /// <summary>
        /// Fired when this hurtbox receives a hit.
        /// </summary>
        public event Action<Hurtbox, DamageInfo> OnHurtboxHit;

        private void Awake()
        {
            _collider = GetComponent<Collider>();
            _collider.isTrigger = true;

            // Find owner
            if (_ownerComponent != null)
            {
                _owner = _ownerComponent as IDamageable;
            }

            if (_owner == null)
            {
                _owner = GetComponentInParent<IDamageable>();
            }

            // Set damage multiplier based on type if not manually set
            if (Mathf.Approximately(_damageMultiplier, 1f))
            {
                _damageMultiplier = GetDefaultMultiplier(_hurtboxType);
            }

            if (_debugMode)
            {
                Debug.Log($"[Hurtbox] Initialized: {gameObject.name}, Layer: {gameObject.layer} ({LayerMask.LayerToName(gameObject.layer)}), Owner: {(_owner != null ? _owner.GetType().Name : "NULL")}");
            }
        }

        private float GetDefaultMultiplier(HurtboxType type)
        {
            switch (type)
            {
                case HurtboxType.Head:
                    return 1.5f; // 50% more damage to head
                case HurtboxType.Limb:
                    return 0.8f; // 20% less damage to limbs
                case HurtboxType.Armored:
                    return 0.5f; // 50% less damage to armored parts
                case HurtboxType.Weakpoint:
                    return 2.0f; // Double damage to weakpoints
                default:
                    return 1f;
            }
        }

        /// <summary>
        /// Notify that this hurtbox was hit.
        /// Called by Hitbox when collision occurs.
        /// </summary>
        internal void NotifyHit(DamageInfo damageInfo)
        {
            if (!_isActive) return;

            OnHurtboxHit?.Invoke(this, damageInfo);

            if (_debugMode)
            {
                Debug.Log($"[Hurtbox] {gameObject.name} hit for {damageInfo.Amount * _damageMultiplier} damage");
            }
        }

        /// <summary>
        /// Temporarily disable this hurtbox.
        /// </summary>
        public void Disable(float duration)
        {
            IsActive = false;

            if (duration > 0)
            {
                Invoke(nameof(Enable), duration);
            }
        }

        /// <summary>
        /// Enable this hurtbox.
        /// </summary>
        public void Enable()
        {
            IsActive = true;
        }

        private void OnDrawGizmos()
        {
            if (!_debugMode) return;

            Color color = _isActive ? _debugColor : new Color(_debugColor.r, _debugColor.g, _debugColor.b, 0.3f);

            // Tint by type
            switch (_hurtboxType)
            {
                case HurtboxType.Head:
                    color = Color.yellow;
                    break;
                case HurtboxType.Weakpoint:
                    color = Color.magenta;
                    break;
                case HurtboxType.Armored:
                    color = Color.cyan;
                    break;
            }

            if (!_isActive)
            {
                color.a = 0.3f;
            }

            Gizmos.color = color;

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
                Gizmos.DrawWireSphere(transform.position, capsule.radius);
            }
        }
    }
}
