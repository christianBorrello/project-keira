using System.Collections.Generic;
using UnityEngine;

namespace _Scripts.Player.Components
{
    /// <summary>
    /// Force type determines how forces are applied and decayed.
    /// </summary>
    public enum ForceType
    {
        Instant,    // Applied once, decays immediately (explosions)
        Impulse,    // Applied once, decays over duration (knockback)
        Continuous  // Applied every frame while active (wind, currents)
    }

    /// <summary>
    /// Instance of an active force being applied to the character.
    /// </summary>
    public struct ForceInstance
    {
        public Vector3 Direction;           // Normalized direction
        public float InitialMagnitude;      // Starting force strength
        public float CurrentMagnitude;      // Current force after decay
        public ForceType Type;              // How force is applied
        public AnimationCurve DecayCurve;   // Custom decay (null = linear)
        public float Duration;              // Total duration in seconds
        public float ElapsedTime;           // Time since force applied
        public int Priority;                // Higher priority overrides lower

        /// <summary>
        /// Normalized progress (0-1) through the force duration.
        /// </summary>
        public float NormalizedTime => Duration > 0 ? Mathf.Clamp01(ElapsedTime / Duration) : 1f;

        /// <summary>
        /// Whether this force has completed its duration.
        /// </summary>
        public bool IsExpired => ElapsedTime >= Duration;

        /// <summary>
        /// Get current force vector.
        /// </summary>
        public Vector3 CurrentForce => Direction * CurrentMagnitude;
    }

    /// <summary>
    /// Manages external forces applied to the character (knockback, explosions, environmental).
    /// Integrated with KCC's UpdateVelocity callback.
    /// </summary>
    public class ExternalForcesManager : MonoBehaviour
    {
        [Header("Configuration")]
        [SerializeField]
        [Tooltip("Maximum simultaneous active forces")]
        private int maxActiveForces = 8;

        [SerializeField]
        [Tooltip("Minimum force magnitude before removal")]
        private float forceThreshold = 0.1f;

        [Header("Default Curves")]
        [SerializeField]
        [Tooltip("Default decay curve for impulse forces")]
        private AnimationCurve defaultImpulseDecay;

        [Header("Debug")]
        [SerializeField]
        private bool debugMode;

        // Active forces list
        private readonly List<ForceInstance> _activeForces = new List<ForceInstance>();

        // Cached combined force for this frame
        private Vector3 _currentCombinedForce;
        private bool _forceCacheDirty = true;

        #region Public API

        /// <summary>
        /// Add an instant force (applied once, immediate full effect then gone).
        /// Use for: explosions, landing impacts, single-frame pushes.
        /// </summary>
        public void AddInstantForce(Vector3 force)
        {
            // P0 FIX: Validate input
            if (!IsValidVector3(force))
            {
                if (debugMode)
                    Debug.LogWarning("[ExternalForces] Rejected instant force with invalid vector (NaN/Infinity)");
                return;
            }

            // Clamp magnitude
            float magnitude = Mathf.Min(force.magnitude, MaxForceMagnitude);

            if (magnitude < forceThreshold)
                return;

            // Instant forces are consumed immediately in GetCurrentForce()
            var instance = new ForceInstance
            {
                Direction = force.normalized,
                InitialMagnitude = magnitude,
                CurrentMagnitude = magnitude,
                Type = ForceType.Instant,
                DecayCurve = null,
                Duration = Time.fixedDeltaTime, // One physics frame
                ElapsedTime = 0f,
                Priority = 0
            };

            AddForceInstance(instance);
        }

        /// <summary>
        /// Add an impulse force that decays over duration.
        /// Use for: knockback, stagger push, dash momentum.
        /// </summary>
        public void AddImpulse(Vector3 direction, float magnitude, float duration, AnimationCurve decayCurve = null)
        {
            // P0 FIX: Validate and clamp inputs
            if (!ValidateAndClampForceParams(ref direction, ref magnitude, ref duration))
                return;

            if (magnitude < forceThreshold)
                return;

            var instance = new ForceInstance
            {
                Direction = direction.normalized,
                InitialMagnitude = magnitude,
                CurrentMagnitude = magnitude,
                Type = ForceType.Impulse,
                DecayCurve = decayCurve ?? defaultImpulseDecay,
                Duration = duration,
                ElapsedTime = 0f,
                Priority = 1 // Impulses have priority over continuous
            };

            AddForceInstance(instance);
        }

        /// <summary>
        /// Add a knockback force using the standard knockback curve.
        /// Convenience method for combat knockback.
        /// </summary>
        public void AddKnockback(Vector3 direction, float distance, float duration = 0.3f)
        {
            // Convert distance to force magnitude
            // Force = distance / duration (simplified, actual uses curve)
            float magnitude = distance / duration * 2f; // Multiply by 2 for curve compensation
            AddImpulse(direction, magnitude, duration, CreateKnockbackDecay());
        }

        /// <summary>
        /// Add a continuous force applied every frame while active.
        /// Use for: wind, water currents, magnetic fields.
        /// </summary>
        public void AddContinuousForce(Vector3 direction, float magnitude, float duration)
        {
            // P0 FIX: Validate and clamp inputs
            if (!ValidateAndClampForceParams(ref direction, ref magnitude, ref duration))
                return;

            if (magnitude < forceThreshold)
                return;

            var instance = new ForceInstance
            {
                Direction = direction.normalized,
                InitialMagnitude = magnitude,
                CurrentMagnitude = magnitude,
                Type = ForceType.Continuous,
                DecayCurve = null, // Continuous forces don't decay
                Duration = duration,
                ElapsedTime = 0f,
                Priority = 0
            };

            AddForceInstance(instance);
        }

        /// <summary>
        /// Get the current combined force vector.
        /// Called by MovementController's UpdateVelocity.
        /// </summary>
        public Vector3 GetCurrentForce()
        {
            if (_forceCacheDirty)
            {
                RecalculateCombinedForce();
            }
            return _currentCombinedForce;
        }

        /// <summary>
        /// Clear all active forces immediately.
        /// Use when: respawning, teleporting, state reset.
        /// </summary>
        public void Clear()
        {
            _activeForces.Clear();
            _currentCombinedForce = Vector3.zero;
            _forceCacheDirty = false;

            if (debugMode)
            {
                Debug.Log("[ExternalForces] All forces cleared");
            }
        }

        /// <summary>
        /// Whether any forces are currently active.
        /// </summary>
        public bool HasActiveForces => _activeForces.Count > 0;

        /// <summary>
        /// Number of currently active forces.
        /// </summary>
        public int ActiveForceCount => _activeForces.Count;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            // Initialize default curve if not set in inspector
            if (defaultImpulseDecay == null || defaultImpulseDecay.keys.Length == 0)
            {
                defaultImpulseDecay = CreateDefaultImpulseDecay();
            }
        }

        private void FixedUpdate()
        {
            UpdateForces(Time.fixedDeltaTime);
        }

        #endregion

        #region Input Validation

        /// <summary>
        /// Maximum allowed force magnitude to prevent physics instability.
        /// Values above this are clamped.
        /// </summary>
        private const float MaxForceMagnitude = 100f;

        /// <summary>
        /// Maximum allowed force duration in seconds.
        /// </summary>
        private const float MaxForceDuration = 10f;

        /// <summary>
        /// Validates that a Vector3 does not contain NaN or Infinity values.
        /// </summary>
        private static bool IsValidVector3(Vector3 v)
        {
            return !float.IsNaN(v.x) && !float.IsNaN(v.y) && !float.IsNaN(v.z)
                && !float.IsInfinity(v.x) && !float.IsInfinity(v.y) && !float.IsInfinity(v.z);
        }

        /// <summary>
        /// Validates and clamps force parameters. Returns false if input is invalid (NaN/Infinity).
        /// </summary>
        private bool ValidateAndClampForceParams(ref Vector3 direction, ref float magnitude, ref float duration)
        {
            // Reject NaN/Infinity
            if (!IsValidVector3(direction))
            {
                if (debugMode)
                    Debug.LogWarning("[ExternalForces] Rejected force with invalid direction (NaN/Infinity)");
                return false;
            }

            if (float.IsNaN(magnitude) || float.IsInfinity(magnitude))
            {
                if (debugMode)
                    Debug.LogWarning("[ExternalForces] Rejected force with invalid magnitude (NaN/Infinity)");
                return false;
            }

            if (float.IsNaN(duration) || float.IsInfinity(duration))
            {
                if (debugMode)
                    Debug.LogWarning("[ExternalForces] Rejected force with invalid duration (NaN/Infinity)");
                return false;
            }

            // Clamp to bounds
            magnitude = Mathf.Clamp(magnitude, 0f, MaxForceMagnitude);
            duration = Mathf.Clamp(duration, 0f, MaxForceDuration);

            return true;
        }

        #endregion

        #region Internal

        private void AddForceInstance(ForceInstance instance)
        {
            // P0 FIX: Paranoid bounds check
            if (_activeForces.Count >= maxActiveForces * 2)
            {
                Debug.LogError("[ExternalForces] Force buffer critically overflowed! Clearing all forces.");
                Clear();
                return;
            }

            // Remove expired forces to make room
            CleanupExpiredForces();

            // Check capacity
            if (_activeForces.Count >= maxActiveForces)
            {
                // P0 FIX: Find lowest priority with oldest-force tiebreaker
                int victimIndex = FindLowestPriorityOldestForce();

                // P0 FIX: Reject new force if it has lower priority than all existing
                if (instance.Priority < _activeForces[victimIndex].Priority)
                {
                    if (debugMode)
                        Debug.Log($"[ExternalForces] Rejected low-priority force (priority={instance.Priority}) - buffer full");
                    return;
                }

                _activeForces.RemoveAt(victimIndex);
            }

            _activeForces.Add(instance);
            _forceCacheDirty = true;

            if (debugMode)
            {
                Debug.Log($"[ExternalForces] Added {instance.Type}: dir={instance.Direction}, mag={instance.InitialMagnitude:F2}, dur={instance.Duration:F2}s");
            }
        }

        private void UpdateForces(float deltaTime)
        {
            if (_activeForces.Count == 0)
                return;

            bool anyExpired = false;

            for (int i = 0; i < _activeForces.Count; i++)
            {
                var force = _activeForces[i];
                force.ElapsedTime += deltaTime;

                // Update magnitude based on decay curve
                if (force.Type == ForceType.Impulse && force.DecayCurve != null)
                {
                    float decayFactor = force.DecayCurve.Evaluate(force.NormalizedTime);
                    force.CurrentMagnitude = force.InitialMagnitude * decayFactor;
                }
                else if (force.Type == ForceType.Instant)
                {
                    // Instant forces are consumed after one frame
                    force.CurrentMagnitude = 0f;
                }
                // Continuous forces maintain their magnitude

                _activeForces[i] = force;

                if (force.IsExpired || force.CurrentMagnitude < forceThreshold)
                {
                    anyExpired = true;
                }
            }

            if (anyExpired)
            {
                CleanupExpiredForces();
            }

            _forceCacheDirty = true;
        }

        private void CleanupExpiredForces()
        {
            _activeForces.RemoveAll(f => f.IsExpired || f.CurrentMagnitude < forceThreshold);
        }

        /// <summary>
        /// Finds the index of the lowest priority force.
        /// Uses oldest-force as tiebreaker when priorities are equal.
        /// </summary>
        private int FindLowestPriorityOldestForce()
        {
            if (_activeForces.Count == 0)
                return -1;

            int victimIndex = 0;
            int lowestPriority = _activeForces[0].Priority;
            float longestElapsed = _activeForces[0].ElapsedTime;

            for (int i = 1; i < _activeForces.Count; i++)
            {
                var force = _activeForces[i];

                // Lower priority always wins
                if (force.Priority < lowestPriority)
                {
                    lowestPriority = force.Priority;
                    longestElapsed = force.ElapsedTime;
                    victimIndex = i;
                }
                // Same priority: prefer older force (higher elapsed time)
                else if (force.Priority == lowestPriority && force.ElapsedTime > longestElapsed)
                {
                    longestElapsed = force.ElapsedTime;
                    victimIndex = i;
                }
            }

            return victimIndex;
        }

        private void RecalculateCombinedForce()
        {
            _currentCombinedForce = Vector3.zero;

            foreach (var force in _activeForces)
            {
                _currentCombinedForce += force.CurrentForce;
            }

            _forceCacheDirty = false;

            if (debugMode && _currentCombinedForce.sqrMagnitude > 0.01f)
            {
                Debug.Log($"[ExternalForces] Combined force: {_currentCombinedForce.magnitude:F2} m/s ({_activeForces.Count} active)");
            }
        }

        #endregion

        #region Curve Factories

        /// <summary>
        /// Default impulse decay: quick initial drop, then gradual slowdown.
        /// </summary>
        private static AnimationCurve CreateDefaultImpulseDecay()
        {
            return new AnimationCurve(
                new Keyframe(0f, 1f, 0f, -2f),
                new Keyframe(0.5f, 0.3f, -0.5f, -0.5f),
                new Keyframe(1f, 0f, -0.2f, 0f)
            );
        }

        /// <summary>
        /// Knockback decay: fast start (impact feel), quick decay (responsive recovery).
        /// </summary>
        private static AnimationCurve CreateKnockbackDecay()
        {
            return new AnimationCurve(
                new Keyframe(0f, 1f, 0f, 0f),
                new Keyframe(0.2f, 0.8f, -2f, -2f),
                new Keyframe(0.5f, 0.2f, -0.5f, -0.5f),
                new Keyframe(1f, 0f, 0f, 0f)
            );
        }

        #endregion
    }
}
