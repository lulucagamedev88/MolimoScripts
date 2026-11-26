using UnityEngine;

namespace MolimoByLulucaGameDev
{
    [ExecuteAlways] // <- THIS makes the SCRIPT RUN in the VIEWPORT
    /// <summary>
    /// Provides the item idle animation component.
    /// </summary>
    public class ItemIdleAnimation : MonoBehaviour
    {
        [Header("Editor (Viewport)")]
        [Tooltip("Ative is ver the animation in the Scene. Desative is can mover the object by mapa.")]
        [SerializeField] private bool animateInEditor = false;

        [Header("rotation")]
        [SerializeField] private float rotationSpeed = 90f;

        [Header("Flutuacathe")]
        [SerializeField] private float floatSpeed = 2f;
        [SerializeField] private float floatAmplitude = 0.2f;

        [Header("Travar position")]
        [Tooltip("locks X and Z in the position of spawn is prevent sliding by force of spawn")]
        [SerializeField] private bool lockPositionXZ = true;

        private Vector3 _startPosition;

        private void Start()
        {
            // the Start normal of the jogo
            if (Application.isPlaying)
            {
                _startPosition = transform.position;

                if (TryGetComponent<Rigidbody>(out Rigidbody rb))
                {
                    rb.isKinematic = true;
                }
            }
        }

        private void Update()
        {
            // if NOT is in Play mode and the caixinha of the Editor is desmarcada,
            // we only atualizamos the position initial is you can mover the object in paz.
            if (!Application.isPlaying && !animateInEditor)
            {
                _startPosition = transform.position;
                return;
            }

            // in the modo of edicathe (viewport), the Time.time not funciona direito. 
            // Usamos the realtimeSinceStartup when estamos editando.
            float timeToUse = Application.isPlaying ? Time.time : Time.realtimeSinceStartup;
            float deltaTime = Application.isPlaying ? Time.deltaTime : 0.016f; // ~60fps simulado in the editor

            // rotation continua in the axis Y
            transform.Rotate(
                Vector3.up,
                rotationSpeed * deltaTime,
                Space.World
            );

            // oscillation vertical in onda senoidal
            float newY =
                _startPosition.y +
                (Mathf.Sin(timeToUse * floatSpeed) * floatAmplitude);

            if (lockPositionXZ)
            {
                // keeps X and Z exactly in the position initial
                transform.position = new Vector3(
                    _startPosition.x,
                    newY,
                    _startPosition.z
                );
            }
            else
            {
                // keeps X and Z atuais
                transform.position = new Vector3(
                    transform.position.x,
                    newY,
                    transform.position.z
                );
            }
        }
    }
}
