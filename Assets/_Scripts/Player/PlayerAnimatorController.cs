using UnityEngine;

namespace _Scripts.Player
{
    /*
    /// <summary>
    /// Handles animator root motion override.
    /// Place this on the same GameObject that has the Animator component.
    /// </summary>
    [RequireComponent(typeof(Animator))]
    public class PlayerAnimatorController : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("If true, root motion is completely ignored. If false, root motion is applied to parent.")]
        private bool disableRootMotion = true;

        private Animator _animator;
        private CharacterController _characterController;

        private void Awake()
        {
            _animator = GetComponent<Animator>();

            // Find CharacterController on parent (PlayerController)
            _characterController = GetComponentInParent<CharacterController>();
        }

        /// <summary>
        /// Called by Unity when the Animator has root motion to apply.
        /// This MUST be on the same GameObject as the Animator.
        /// </summary>
        private void OnAnimatorMove()
        {
            if (disableRootMotion)
            {
                // Completely ignore root motion - movement is handled by PlayerController
                return;
            }

            // Alternative: Apply root motion to CharacterController if needed
            if (_characterController != null && _animator != null)
            {
                _characterController.Move(_animator.deltaPosition);
            }
        }
    } */
}

