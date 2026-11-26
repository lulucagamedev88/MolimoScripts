using UnityEngine;

namespace MolimoByLulucaGameDev
{
    /// <summary>
    /// Componente leve anexado ao trigger de entrada/saída de QUALQUER área.
    /// Responsabilidade única: detectar o Player e delegar ao TeleportManager.
    ///
    /// ✦ FACING:
    ///   Por padrão, o Player aparece com o +Z (forward) alinhado à direção
    ///   do +Z DESTE SpawnPoint. Ou seja: gire o SpawnPoint para escolher para
    ///   onde o Player vai olhar ao surgir.
    ///
    ///   Se 'Facing Target' estiver atribuído, ele tem PRIORIDADE e o Player
    ///   olhará para esse Transform em vez do +Z do SpawnPoint.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    [AddComponentMenu("Molimo/Teleport/Teleport Node")]
    public class TeleportNode : MonoBehaviour
    {
        [Header("=== areas (funciona is Level or Room) ===")]
        [Tooltip("GameObject root of the area current (that contains this trigger). " +
                 "will be DESATIVADA after the teleporte, the less that appears in 'Keep Active'. " +
                 "if empty, nothing is desativado.")]
        [SerializeField] private GameObject currentArea;

        [Tooltip("GameObject root of the area of DESTINO. will be ATIVADA during the transition.")]
        [SerializeField] private GameObject targetArea;

        [Tooltip("GameObjects that not must be desativados, same estando inside " +
                 "of the 'Current Area'.")]
        [SerializeField] private GameObject[] keepActive;

        [Header("=== Spawn ===")]
        [Tooltip("point of spawn in the area of destino. " +
                 "X/Z are respeitados; Y is corrigido by Snap-To-Ground. " +
                 "the +Z (forward) deste Transform sets is where the Player " +
                 "vai look when the appear.")]
        [SerializeField] private Transform spawnPoint;

        [Header("=== Facing (direction of the player when the appear) ===")]
        [Tooltip("optional. if assigned, the Player will look is this Transform " +
                 "instead of use the +Z of the SpawnPoint. the rotation is always PLANA " +
                 "(only Y).")]
        [SerializeField] private Transform facingTarget;

        [Header("=== detection ===")]
        [Tooltip("Tag of the player. leave empty is detect only by Rigidbody.")]
        [SerializeField] private string playerTag = "Player";

        [Header("=== safety local ===")]
        [Tooltip("time minimum between disparos DESTE node specific.")]
        [SerializeField] private float localCooldown = 0.25f;

        // Runtime
        private Collider _triggerCollider;
        private float _lastFireTime = -999f;

        // properties public (consumidas by TeleportManager)
        public GameObject CurrentArea => currentArea;
        public GameObject TargetArea => targetArea;
        public GameObject[] KeepActive => keepActive;
        public Transform SpawnPoint => spawnPoint;
        public Transform FacingTarget => facingTarget;
        public string PlayerTag => playerTag;

        // =====================================================================
        //  Ciclo of health
        // =====================================================================
        private void Awake()
        {
            _triggerCollider = GetComponent<Collider>();
            if (_triggerCollider != null)
                _triggerCollider.isTrigger = true;
        }

        private void Reset()
        {
            Transform t = transform.parent;
            while (t != null && currentArea == null)
            {
                string n = t.name;
                if (n.StartsWith("Level", System.StringComparison.OrdinalIgnoreCase) ||
                    n.StartsWith("Room", System.StringComparison.OrdinalIgnoreCase) ||
                    n.StartsWith("Area", System.StringComparison.OrdinalIgnoreCase) ||
                    n.StartsWith("Subsolo", System.StringComparison.OrdinalIgnoreCase))
                {
                    currentArea = t.gameObject;
                    break;
                }
                t = t.parent;
            }
        }

        private void OnValidate()
        {
            if (localCooldown < 0f) localCooldown = 0f;
        }

        // =====================================================================
        //  detection
        // =====================================================================
        private void OnTriggerEnter(Collider other)
        {
            if (other == null || other == _triggerCollider) return;

            Rigidbody playerRb = other.attachedRigidbody;
            if (playerRb == null) return;
            if (playerRb.transform.root == transform.root) return;

            if (!string.IsNullOrEmpty(playerTag))
            {
                bool isPlayer = playerRb.CompareTag(playerTag) || other.CompareTag(playerTag);
                if (!isPlayer) return;
            }

            if (Time.time - _lastFireTime < localCooldown) return;

            TeleportManager mgr = TeleportManager.Instance;
            if (mgr == null)
            {
                Debug.LogWarning(
                    $"[TeleportNode:{name}] TeleportManager not found na scene.",
                    this);
                return;
            }

            bool accepted = mgr.RequestTeleport(this, playerRb, other);
            if (accepted) _lastFireTime = Time.time;
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Collider c = GetComponent<Collider>();
            if (c != null)
            {
                Gizmos.color = new Color(0f, 1f, 1f, 0.35f);
                Gizmos.DrawCube(c.bounds.center, c.bounds.size);
            }

            if (spawnPoint != null)
            {
                // Esfera of the SpawnPoint
                Gizmos.color = Color.magenta;
                Gizmos.DrawWireSphere(spawnPoint.position, 0.3f);

                // line of the trigger until the SpawnPoint
                if (targetArea != null)
                {
                    Gizmos.color = new Color(1f, 0.3f, 1f, 0.6f);
                    Gizmos.DrawLine(transform.position, spawnPoint.position);
                }

                // +Z of the SpawnPoint = direction in that the Player vai look when the appear
                Vector3 forward = spawnPoint.forward;
                forward.y = 0f;

                if (forward.sqrMagnitude > 0.0001f)
                {
                    forward.Normalize();
                    Vector3 tip = spawnPoint.position + forward * 1.5f;

                    Gizmos.color = new Color(1f, 0.85f, 0.1f, 1f); // amarelo
                    Gizmos.DrawLine(spawnPoint.position, tip);

                    // "Bico" of the seta
                    Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;
                    Vector3 back = -forward * 0.3f;
                    Gizmos.DrawLine(tip, tip + back + right * 0.15f);
                    Gizmos.DrawLine(tip, tip + back - right * 0.15f);
                }
            }

            // Facing Target: seta of the SpawnPoint until it (if assigned)
            if (spawnPoint != null && facingTarget != null)
            {
                Gizmos.color = new Color(1f, 0.4f, 0.9f, 0.9f);
                Gizmos.DrawLine(spawnPoint.position, facingTarget.position);
                Gizmos.DrawWireSphere(facingTarget.position, 0.25f);
            }
        }
#endif
    }
}
