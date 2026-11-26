using UnityEngine;

namespace MolimoByLulucaGameDev
{
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(Collider))]
    /// <summary>
    /// Provides the throwable item component.
    /// </summary>
    public class ThrowableItem : MonoBehaviour
    {
        [Header("settings of damage")]
        [SerializeField] private float impactDamage = 10f;
        [SerializeField] private float impactForce = 5f;

        [Header("settings of audio")]
        [Tooltip("sound tocado when the acertar the target.")]
        [SerializeField] private SoundData impactSound;

        [Header("Ciclo of health")]
        [SerializeField] private float lifetime = 5f;

        private bool _canDamage = false;
        private float _lifetimeTimer;

        private void OnEnable()
        {
            _canDamage = false;
            _lifetimeTimer = lifetime;
        }

        private void Update()
        {
            if (!_canDamage) return;

            _lifetimeTimer -= Time.deltaTime;
            if (_lifetimeTimer <= 0f)
            {
                Destroy(gameObject);
            }
        }

        /// <summary>
        /// Performs the activate damage operation.
        /// </summary>
        public void ActivateDamage() => _canDamage = true;
        /// <summary>
        /// Performs the deactivate damage operation.
        /// </summary>
        public void DeactivateDamage() => _canDamage = false;

        private void OnCollisionEnter(Collision collision)
        {
            if (!_canDamage) return;

            if (collision.gameObject.TryGetComponent<IDamageable>(out IDamageable damageable))
            {
                damageable.TakeDamage(impactDamage);

                if (collision.rigidbody != null)
                {
                    Vector3 pushDir = collision.contacts[0].normal * -1f;
                    collision.rigidbody.AddForce(pushDir * impactForce, ForceMode.Impulse);
                }

                // plays the sound criando the object temporary is the audio not be cut when the item is destroyed
                if (impactSound != null && impactSound.clip != null)
                {
                    GameObject soundGO = new GameObject("TempImpactSFX");
                    soundGO.transform.position = transform.position;

                    AudioSource source = soundGO.AddComponent<AudioSource>();
                    source.clip = impactSound.clip;
                    source.volume = impactSound.volume;
                    source.pitch = impactSound.GetPitch(); // uses the variation of pitch of the its SoundData
                    source.spatialBlend = 1f; // Torna the sound 3D
                    source.minDistance = impactSound.minDistance;
                    source.maxDistance = impactSound.maxDistance;

                    source.Play();

                    // destroys the object of audio after the end of the clip
                    Destroy(soundGO, impactSound.clip.length);
                }

                Destroy(gameObject);
            }
        }
    }
}