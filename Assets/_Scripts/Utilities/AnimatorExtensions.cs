using System.Collections.Generic;
using UnityEngine;

namespace _Scripts.Utilities
{
    /// <summary>
    /// Extension methods for Animator to safely set parameters without warnings.
    /// </summary>
    public static class AnimatorExtensions
    {
        // Cache for parameter existence checks
        private static readonly Dictionary<int, HashSet<int>> ParameterCache = new();

        /// <summary>
        /// Safely set an integer parameter if it exists.
        /// </summary>
        public static void SetIntegerSafe(this Animator animator, string name, int value)
        {
            if (animator == null) return;
            if (HasParameter(animator, name))
            {
                animator.SetInteger(name, value);
            }
        }

        /// <summary>
        /// Safely set a float parameter if it exists.
        /// </summary>
        public static void SetFloatSafe(this Animator animator, string name, float value)
        {
            if (animator == null) return;
            if (HasParameter(animator, name))
            {
                animator.SetFloat(name, value);
            }
        }

        /// <summary>
        /// Safely set a bool parameter if it exists.
        /// </summary>
        public static void SetBoolSafe(this Animator animator, string name, bool value)
        {
            if (animator == null) return;
            if (!HasParameter(animator, name)) return;
                
            animator.SetBool(name, value);
        }

        /// <summary>
        /// Safely set a trigger if it exists.
        /// </summary>
        public static void SetTriggerSafe(this Animator animator, string name)
        {
            if (animator == null) return;
            if (HasParameter(animator, name))
            {
                animator.SetTrigger(name);
            }
        }

        /// <summary>
        /// Safely reset a trigger if it exists.
        /// </summary>
        public static void ResetTriggerSafe(this Animator animator, string name)
        {
            if (animator == null) return;
            if (HasParameter(animator, name))
            {
                animator.ResetTrigger(name);
            }
        }

        /// <summary>
        /// Check if the animator has a parameter with the given name.
        /// Results are cached for performance.
        /// </summary>
        public static bool HasParameter(this Animator animator, string parameterName)
        {
            if (animator == null || animator.runtimeAnimatorController == null)
                return false;

            int animatorId = animator.GetInstanceID();
            int paramHash = Animator.StringToHash(parameterName);

            // Check cache first
            if (!ParameterCache.TryGetValue(animatorId, out var paramSet))
            {
                // Build cache for this animator
                paramSet = new HashSet<int>();
                foreach (var param in animator.parameters)
                {
                    paramSet.Add(param.nameHash);
                }
                ParameterCache[animatorId] = paramSet;
            }

            return paramSet.Contains(paramHash);
        }

        /// <summary>
        /// Clear the parameter cache (call when animator controllers change).
        /// </summary>
        public static void ClearCache()
        {
            ParameterCache.Clear();
        }

        /// <summary>
        /// Clear cache for a specific animator.
        /// </summary>
        public static void ClearCache(Animator animator)
        {
            if (animator != null)
            {
                ParameterCache.Remove(animator.GetInstanceID());
            }
        }
    }
}
