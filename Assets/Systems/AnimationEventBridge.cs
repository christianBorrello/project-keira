using System;
using _Scripts.Combat.Hitbox;
using UnityEngine;

namespace Systems
{
    /// <summary>
    /// Bridges animation events to game systems.
    /// Attach to the same GameObject as the Animator.
    /// </summary>
    public class AnimationEventBridge : MonoBehaviour
    {
        [Header("References")]
        [SerializeField]
        [Tooltip("Hitbox controller for attack events")]
        private HitboxController _hitboxController;

        [SerializeField]
        [Tooltip("Audio source for sound effects")]
        private AudioSource _audioSource;

        [SerializeField]
        [Tooltip("Particle system controller")]
        private ParticleSystem _particleSystem;

        [Header("Sound Effects")]
        [SerializeField]
        private AudioClip[] _attackSwooshSounds;

        [SerializeField]
        private AudioClip[] _footstepSounds;

        [SerializeField]
        private AudioClip[] _impactSounds;

        [Header("Settings")]
        [SerializeField]
        [Range(0f, 1f)]
        private float _soundVolume = 1f;

        [SerializeField]
        private bool _randomizePitch = true;

        [SerializeField]
        private Vector2 _pitchRange = new Vector2(0.9f, 1.1f);

        // Events for external systems to hook into
        public event Action OnAttackStart;
        public event Action OnAttackHitWindow;
        public event Action OnAttackEnd;
        public event Action OnComboWindowOpen;
        public event Action OnComboWindowClose;
        public event Action OnRecoveryStart;
        public event Action<bool> OnInvulnerabilityChanged; // true = invulnerable
        public event Action OnFootstep;

        private void Awake()
        {
            // Try to find components if not assigned
            if (_hitboxController == null)
            {
                _hitboxController = GetComponentInParent<HitboxController>();
            }

            if (_audioSource == null)
            {
                _audioSource = GetComponent<AudioSource>();
                if (_audioSource == null)
                {
                    _audioSource = gameObject.AddComponent<AudioSource>();
                    _audioSource.playOnAwake = false;
                    _audioSource.spatialBlend = 1f; // 3D sound
                }
            }
        }

        #region Hitbox Events

        /// <summary>
        /// Animation event: Start hitbox by group name.
        /// </summary>
        public void StartHitbox(string groupName)
        {
            _hitboxController?.ActivateGroup(groupName);
            OnAttackHitWindow?.Invoke();
        }

        /// <summary>
        /// Animation event: End hitbox by group name.
        /// </summary>
        public void EndHitbox(string groupName)
        {
            _hitboxController?.DeactivateGroup(groupName);
        }

        /// <summary>
        /// Animation event: Start hitbox by index.
        /// </summary>
        public void StartHitboxIndex(int index)
        {
            _hitboxController?.AnimEvent_StartHitboxByIndex(index);
            OnAttackHitWindow?.Invoke();
        }

        /// <summary>
        /// Animation event: End hitbox by index.
        /// </summary>
        public void EndHitboxIndex(int index)
        {
            _hitboxController?.AnimEvent_EndHitboxByIndex(index);
        }

        /// <summary>
        /// Animation event: End all hitboxes.
        /// </summary>
        public void EndAllHitboxes()
        {
            _hitboxController?.DeactivateAll();
        }

        #endregion

        #region Attack Phase Events

        /// <summary>
        /// Animation event: Attack animation started.
        /// </summary>
        public void AttackStart()
        {
            OnAttackStart?.Invoke();
        }

        /// <summary>
        /// Animation event: Attack animation ended.
        /// </summary>
        public void AttackEnd()
        {
            _hitboxController?.DeactivateAll();
            OnAttackEnd?.Invoke();
        }

        /// <summary>
        /// Animation event: Combo input window opened.
        /// </summary>
        public void ComboWindowOpen()
        {
            OnComboWindowOpen?.Invoke();
        }

        /// <summary>
        /// Animation event: Combo input window closed.
        /// </summary>
        public void ComboWindowClose()
        {
            OnComboWindowClose?.Invoke();
        }

        /// <summary>
        /// Animation event: Recovery phase started.
        /// </summary>
        public void RecoveryStart()
        {
            OnRecoveryStart?.Invoke();
        }

        #endregion

        #region Invulnerability Events

        /// <summary>
        /// Animation event: Start invulnerability frames.
        /// </summary>
        public void StartInvulnerability()
        {
            OnInvulnerabilityChanged?.Invoke(true);

            // If we have a reference to the player controller, set it directly
            var controller = GetComponentInParent<_Scripts.Player.PlayerController>();
            controller?.SetInvulnerable(true);
        }

        /// <summary>
        /// Animation event: End invulnerability frames.
        /// </summary>
        public void EndInvulnerability()
        {
            OnInvulnerabilityChanged?.Invoke(false);

            var controller = GetComponentInParent<_Scripts.Player.PlayerController>();
            controller?.SetInvulnerable(false);
        }

        #endregion

        #region Audio Events

        /// <summary>
        /// Animation event: Play attack swoosh sound.
        /// </summary>
        public void PlaySwoosh()
        {
            PlayRandomSound(_attackSwooshSounds);
        }

        /// <summary>
        /// Animation event: Play footstep sound.
        /// </summary>
        public void PlayFootstep()
        {
            PlayRandomSound(_footstepSounds);
            OnFootstep?.Invoke();
        }

        /// <summary>
        /// Animation event: Play impact sound.
        /// </summary>
        public void PlayImpact()
        {
            PlayRandomSound(_impactSounds);
        }

        /// <summary>
        /// Animation event: Play sound by index.
        /// </summary>
        public void PlaySoundIndex(int soundIndex)
        {
            AudioClip[] allSounds = CombineAllSounds();
            if (soundIndex >= 0 && soundIndex < allSounds.Length)
            {
                PlaySound(allSounds[soundIndex]);
            }
        }

        private void PlayRandomSound(AudioClip[] clips)
        {
            if (clips == null || clips.Length == 0) return;

            AudioClip clip = clips[UnityEngine.Random.Range(0, clips.Length)];
            PlaySound(clip);
        }

        private void PlaySound(AudioClip clip)
        {
            if (clip == null || _audioSource == null) return;

            if (_randomizePitch)
            {
                _audioSource.pitch = UnityEngine.Random.Range(_pitchRange.x, _pitchRange.y);
            }

            _audioSource.PlayOneShot(clip, _soundVolume);
        }

        private AudioClip[] CombineAllSounds()
        {
            int totalLength = 0;
            if (_attackSwooshSounds != null) totalLength += _attackSwooshSounds.Length;
            if (_footstepSounds != null) totalLength += _footstepSounds.Length;
            if (_impactSounds != null) totalLength += _impactSounds.Length;

            AudioClip[] result = new AudioClip[totalLength];
            int index = 0;

            if (_attackSwooshSounds != null)
            {
                Array.Copy(_attackSwooshSounds, 0, result, index, _attackSwooshSounds.Length);
                index += _attackSwooshSounds.Length;
            }
            if (_footstepSounds != null)
            {
                Array.Copy(_footstepSounds, 0, result, index, _footstepSounds.Length);
                index += _footstepSounds.Length;
            }
            if (_impactSounds != null)
            {
                Array.Copy(_impactSounds, 0, result, index, _impactSounds.Length);
            }

            return result;
        }

        #endregion

        #region VFX Events

        /// <summary>
        /// Animation event: Play particle effect.
        /// </summary>
        public void PlayParticle()
        {
            if (_particleSystem != null)
            {
                _particleSystem.Play();
            }
        }

        /// <summary>
        /// Animation event: Stop particle effect.
        /// </summary>
        public void StopParticle()
        {
            if (_particleSystem != null)
            {
                _particleSystem.Stop();
            }
        }

        #endregion

        #region Utility

        /// <summary>
        /// Animation event: Log message (for debugging).
        /// </summary>
        public void DebugLog(string message)
        {
            Debug.Log($"[AnimEvent] {message}");
        }

        /// <summary>
        /// Set the hitbox controller reference.
        /// </summary>
        public void SetHitboxController(HitboxController controller)
        {
            _hitboxController = controller;
        }

        #endregion
    }
}
