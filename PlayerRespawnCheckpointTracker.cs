// -----------------------------------------------------------------------------
//  RespawnCheckpointTracker.cs
//  MolimoByLulucaGameDev
//
//  Rastreia continuamente the last point "safe" of the player
//  (ground estavel, without estar in roll/dash/knockback) and the provides
//  is the AbyssHazardTrigger.
//
//  Rules:
//    - only records when the Player is Grounded there is by less
//      minGroundedTimeToRecord seconds (prevents gravar in quinas of platform).
//    - only records if the Player if moved when the less minDistanceToUpdate of the last point.
//    - not records during Knockdown / Suspended / dead.
//    - NOVO: only records if houver ground when the redor (ring of raycasts).
//    - NOVO: snap when the ground real (Y exact).
//    - NOVO: knockback in the respawn in direction when the center of the platform.
//    - NOVO: history of checkpoints is fallback.
// -----------------------------------------------------------------------------

using System.Collections.Generic;
using UnityEngine;

namespace MolimoByLulucaGameDev
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Molimo By Luluca/Respawn Checkpoint Tracker")]
    /// <summary>
    /// Provides the player respawn checkpoint tracker component.
    /// </summary>
    public sealed class PlayerRespawnCheckpointTracker : MonoBehaviour
    {
        #region Inspector

        [Header("Rastreamento")]
        [Tooltip("interval minimum between gravacoes (seconds).")]
        [SerializeField] private float recordInterval = 0.5f;

        [Tooltip("distance minimum (units) that the Player precisa if move away of the last point " +
                 "is that the novo point is gravado.")]
        [SerializeField] private float minDistanceToUpdate = 0.5f;

        [Tooltip("time minimum (seconds) that the Player precisa estar Grounded is the point be considered safe.")]
        [SerializeField] private float minGroundedTimeToRecord = 0.1f;

        [Tooltip("if true, exige that the Player esteja Grounded is gravar.")]
        [SerializeField] private bool requireGrounded = true;

        [Header("validation of ground (anti-edge)")]
        [Tooltip("Layers that count the ground. if empty, the validation remains DESLIGADA " +
                 "(behavior old — respawn can cair in the abismo).")]
        [SerializeField] private LayerMask groundLayer;

        [Tooltip("height above of the point candidato where the raycasts of ground start.")]
        [SerializeField] private float groundProbeHeight = 1.5f;

        [Tooltip("distance that the raycast falls procurando ground.")]
        [SerializeField] private float groundProbeDistance = 3f;

        [Tooltip("radius of the ring of amostragem when the redor of the candidato. " +
                 "all the points of the ring precisam ter ground is the point be valid.")]
        [SerializeField] private float validationRadius = 0.6f;

        [Tooltip("number of directions sampled in the ring. 8 = strict, 4 = light.")]
        [SerializeField] private int validationSampleCount = 8;

        [Tooltip("Offset above of the ground applied to the position refinada (prevents spawn inside the ground).")]
        [SerializeField] private float groundSnapOffsetY = 0.05f;

        [Tooltip("if true, when the point candidato fails, tries push it in the direction opposite " +
                 "at the edges detectadas, instead of simplesmente reject.")]
        [SerializeField] private bool pushAwayFromEdges = true;

        [Header("knockback in the Respawn")]
        [Tooltip("if true, when the calcular the respawn, recua the point in direction when the center of the " +
                 "platform (opposite at the edges), instead of only 'is back' of the personagem.")]
        [SerializeField] private bool smartBackUp = true;

        [Header("history")]
        [Tooltip("Quantos checkpoints valid keep in memory. if the more recente fail " +
                 "the validation in the respawn, tries the previous.")]
        [SerializeField] private int checkpointHistorySize = 5;

        [Header("Debug")]
        [Tooltip("Loga when the point is rejeitado or empurrado.")]
        [SerializeField] private bool debugValidation = false;

        #endregion

        #region state

        private PlayerBrain brain;
        private PlayerMovement movement;
        private Rigidbody rb;

        private Vector3 lastSafePosition;
        private Quaternion lastSafeRotation;
        private bool hasValidCheckpoint = false;

        private float recordTimer = 0f;
        private float groundedTimer = 0f;

        private struct CheckpointEntry
        {
            public Vector3 position;
            public Quaternion rotation;
            public CheckpointEntry(Vector3 p, Quaternion r) { position = p; rotation = r; }
        }

        private readonly List<CheckpointEntry> history = new List<CheckpointEntry>();

        #endregion

        #region properties public

        public bool HasValidCheckpoint => hasValidCheckpoint;
        public Vector3 LastSafePosition => lastSafePosition;
        public Quaternion LastSafeRotation => lastSafeRotation;

        #endregion

        #region Ciclo of health

        private void Awake()
        {
            brain = GetComponent<PlayerBrain>();
            if (brain == null) brain = GetComponentInParent<PlayerBrain>();

            movement = GetComponent<PlayerMovement>();
            if (movement == null) movement = GetComponentInParent<PlayerMovement>();

            rb = GetComponent<Rigidbody>();
            if (rb == null) rb = GetComponentInParent<Rigidbody>();

            // seed initial (only is promovida the "valid" after primeira recording)
            lastSafePosition = transform.position;
            lastSafeRotation = transform.rotation;
            hasValidCheckpoint = false;
        }

        private void Start()
        {
            // tries semear the checkpoint initial already validado (useful if the scene
            // starts with the Player in foot numa platform safe).
            if (groundLayer.value != 0 && TryValidateCheckpointPosition(transform.position, out Vector3 refined, out _))
            {
                RecordCheckpoint(refined, transform.rotation);
                if (debugValidation)
                    Debug.Log($"[checkpoint] seed initial recorded in {refined}.");
            }
        }

        private void Update()
        {
            // not rastreia while dead, in knockdown or suspenso
            if (brain != null && !brain.IsAlive) return;
            if (brain != null && brain.IsKnockedDown) return;
            if (movement != null && movement.IsSuspended) return;

            // Acumula time grounded
            if (IsConsideredGrounded())
                groundedTimer += Time.deltaTime;
            else
                groundedTimer = 0f;

            // Timer of recording
            recordTimer += Time.deltaTime;
            if (recordTimer < recordInterval) return;
            recordTimer = 0f;

            // Requisitos is gravar
            if (requireGrounded && groundedTimer < minGroundedTimeToRecord) return;

            Vector3 currentPos = transform.position;
            Vector3 candidatePos;
            Quaternion candidateRot = transform.rotation;

            // ---- layer 1: valid the position current ----
            if (TryValidateCheckpointPosition(currentPos, out Vector3 refinedPos, out Vector3 pushDir))
            {
                candidatePos = refinedPos;
            }
            else
            {
                // ---- layer 2: tries empurrar is inside the platform ----
                if (pushAwayFromEdges && pushDir.sqrMagnitude > 0.001f)
                {
                    Vector3 pushed = currentPos + pushDir.normalized * validationRadius;

                    if (TryValidateCheckpointPosition(pushed, out Vector3 pushedRefined, out _))
                    {
                        candidatePos = pushedRefined;

                        if (debugValidation)
                            Debug.Log($"[checkpoint] position current in the edge — empurrado is {pushedRefined} (dir={pushDir.normalized}).");
                    }
                    else
                    {
                        // nor empurrando resolveu — keeps the checkpoint previous.
                        if (debugValidation)
                            Debug.Log("[checkpoint] edge detectada; push falhou. Mantendo checkpoint previous.");
                        return;
                    }
                }
                else
                {
                    // without pushDir useful — keeps the previous.
                    if (debugValidation)
                        Debug.Log("[checkpoint] without ground when the redor; mantendo checkpoint previous.");
                    return;
                }
            }

            // ---- layer 3: only records if actually if afastou ----
            if (!hasValidCheckpoint || Vector3.Distance(candidatePos, lastSafePosition) >= minDistanceToUpdate)
            {
                RecordCheckpoint(candidatePos, candidateRot);
            }
        }

        private void RecordCheckpoint(Vector3 position, Quaternion rotation)
        {
            lastSafePosition = position;
            lastSafeRotation = rotation;
            hasValidCheckpoint = true;

            if (checkpointHistorySize > 0)
            {
                if (history.Count >= checkpointHistorySize)
                    history.RemoveAt(0);

                history.Add(new CheckpointEntry(position, rotation));
            }
        }

        private bool IsConsideredGrounded()
        {
            if (movement != null) return movement.IsGrounded;

            // Fallback without PlayerMovement: checa speed vertical
            if (rb != null) return Mathf.Abs(rb.linearVelocity.y) < 0.1f;

            return true;
        }

        #endregion

        #region validation of ground

        /// <summary>
        /// Verifica se a posição candidata está sobre chão suficiente.
        /// Retorna:
        ///   - true  + refinedPos com Y ajustado ao chão real
        ///   - false + pushDir = direção sugerida para empurrar (oposta às bordas)
        /// </summary>
        private bool TryValidateCheckpointPosition(Vector3 candidatePos, out Vector3 refinedPos, out Vector3 pushDir)
        {
            refinedPos = candidatePos;
            pushDir = Vector3.zero;

            if (groundLayer.value == 0) return true; // validation desligada

            // 1) ground in the center?
            Vector3 centerOrigin = candidatePos + Vector3.up * groundProbeHeight;
            float rayLength = groundProbeHeight + groundProbeDistance;

            if (!Physics.Raycast(centerOrigin, Vector3.down, out RaycastHit centerHit,
                                 rayLength, groundLayer, QueryTriggerInteraction.Ignore))
            {
                // not there is ground nor in the center — without pushDir useful.
                if (debugValidation)
                    Debug.LogWarning($"[checkpoint] without ground in the center of {candidatePos}.");
                return false;
            }

            // 2) ring of amostragem
            int missingCount = CollectUnsafeGroundSamples(candidatePos, out Vector3 unsafeAccum);

            // if all the amostras acharam ground → position valid.
            if (missingCount == 0)
            {
                refinedPos = new Vector3(
                    candidatePos.x,
                    centerHit.point.y + groundSnapOffsetY,
                    candidatePos.z);
                return true;
            }

            // there are edges next. pushDir aponta is the lado opposite when the sum
            // of the directions without ground (or is, "is inside" of the platform).
            if (unsafeAccum.sqrMagnitude > 0.001f)
                pushDir = -unsafeAccum.normalized;

            if (debugValidation)
            {
                int samples = Mathf.Max(4, validationSampleCount);
                Debug.LogWarning($"[checkpoint] point rejeitado in {candidatePos}: " +
                                 $"{missingCount}/{samples} amostras without ground. " +
                                 $"pushDir sugerido = {pushDir}.");
            }

            return false;
        }

        private int CollectUnsafeGroundSamples(Vector3 center, out Vector3 unsafeAccum)
        {
            int samples = Mathf.Max(4, validationSampleCount);
            float rayLength = groundProbeHeight + groundProbeDistance;
            unsafeAccum = Vector3.zero;
            int missingCount = 0;

            for (int i = 0; i < samples; i++)
            {
                float angle = (360f / samples) * i;
                Vector3 dir = Quaternion.Euler(0f, angle, 0f) * Vector3.forward;
                Vector3 samplePos = center + dir * validationRadius;
                Vector3 origin = samplePos + Vector3.up * groundProbeHeight;

                bool hit = Physics.Raycast(origin, Vector3.down, rayLength,
                                           groundLayer, QueryTriggerInteraction.Ignore);

                if (!hit)
                {
                    unsafeAccum += dir;
                    missingCount++;
                }
            }

            return missingCount;
        }

        #endregion

        #region API Public

        /// <summary>
        /// Devolve o ponto de respawn.
        ///   - Se smartBackUp = true, recua em direção ao centro da plataforma
        ///     (oposto às bordas detectadas), em vez de apenas "para trás".
        ///   - Se a posição final ainda falhar a validação, percorre o histórico
        ///     do mais recente ao mais antigo, usando o primeiro que validar.
        /// </summary>
        public void GetRespawnPoint(float backUpDistance, out Vector3 position, out Quaternion rotation)
        {
            position = lastSafePosition;
            rotation = lastSafeRotation;

            // 1) knockback
            if (backUpDistance > 0.001f)
            {
                Vector3 backupDir = smartBackUp
                    ? GetSafeBackupDirection()
                    : -(lastSafeRotation * Vector3.forward);

                position -= backupDir * backUpDistance;
            }

            // 2) Re-validation
            if (groundLayer.value != 0)
            {
                if (TryValidateCheckpointPosition(position, out Vector3 validated, out _))
                {
                    position = validated;
                    return;
                }

                // tries the history, of the more recente when the more old.
                for (int i = history.Count - 1; i >= 0; i--)
                {
                    if (TryValidateCheckpointPosition(history[i].position, out Vector3 validPos, out _))
                    {
                        position = validPos;
                        rotation = history[i].rotation;

                        Debug.LogWarning($"[checkpoint] point current invalid in the respawn — using history [{i}] " +
                                         $"in {validPos}.");
                        return;
                    }
                }

                Debug.LogError("[checkpoint] none checkpoint valid available. " +
                               "Respawn can cair in the abismo. Verifique the 'groundLayer' and the level.");
            }
        }

        /// <summary>
        /// Direção de recuo segura: oposta às bordas detectadas ao redor do
        /// último checkpoint. Se não houver bordas, recua "para trás" do personagem.
        /// </summary>
        private Vector3 GetSafeBackupDirection()
        {
            if (groundLayer.value == 0)
                return -(lastSafeRotation * Vector3.forward);

            Vector3 unsafeAccum;
            int missingCount = CollectUnsafeGroundSamples(lastSafePosition, out unsafeAccum);

            if (missingCount == 0 || unsafeAccum.sqrMagnitude < 0.001f)
                return -(lastSafeRotation * Vector3.forward);

            return -unsafeAccum.normalized;
        }

        /// <summary>
        /// Permite que um checkpoint externo (ex.: um Trigger em checkpoint)
        /// sobrescreva o último ponto gravado.
        /// Se a validação estiver ativa e a posição não validar, ele ainda
        /// registra (o designer assumiu o controle), mas avisa no Console.
        /// </summary>
        public void ForceCheckpoint(Vector3 position, Quaternion rotation)
        {
            if (groundLayer.value != 0)
            {
                if (TryValidateCheckpointPosition(position, out Vector3 validated, out _))
                {
                    position = validated;
                }
                else
                {
                    Debug.LogWarning($"[checkpoint] ForceCheckpoint recebeu position without ground suficiente: " +
                                     $"{position}. recording same thus by decision of the designer.");
                }
            }

            history.Clear();
            RecordCheckpoint(position, rotation);
        }

        /// <summary>
        /// Performs the force checkpoint operation.
        /// </summary>
        public void ForceCheckpoint(Transform target)
        {
            if (target == null) return;
            ForceCheckpoint(target.position, target.rotation);
        }

        /// <summary>
        /// Limpa o checkpoint (o próximo respawn usará o fallback do AbyssHazardTrigger).
        /// </summary>
        public void ClearCheckpoint()
        {
            hasValidCheckpoint = false;
            history.Clear();
        }

        #endregion

        #region Gizmos

        private void OnDrawGizmosSelected()
        {
            if (!Application.isPlaying || !hasValidCheckpoint) return;

            // checkpoint primary
            Gizmos.color = new Color(0.30f, 1f, 0.50f, 0.9f);
            Gizmos.DrawWireSphere(lastSafePosition, 0.4f);

            // Seta apontando is "forward" of the rotation gravada
            Gizmos.color = Color.yellow;
            Vector3 tip = lastSafePosition + lastSafeRotation * Vector3.forward * 1.2f;
            Gizmos.DrawLine(lastSafePosition, tip);
            Gizmos.DrawSphere(tip, 0.12f);

            // ring of validation (green = ground, red = without ground)
            if (groundLayer.value != 0 && validationRadius > 0f)
            {
                int samples = Mathf.Max(4, validationSampleCount);
                float rayLength = groundProbeHeight + groundProbeDistance;

                for (int i = 0; i < samples; i++)
                {
                    float angle = (360f / samples) * i;
                    Vector3 dir = Quaternion.Euler(0f, angle, 0f) * Vector3.forward;
                    Vector3 samplePos = lastSafePosition + dir * validationRadius;
                    Vector3 origin = samplePos + Vector3.up * groundProbeHeight;

                    bool hit = Physics.Raycast(origin, Vector3.down, rayLength,
                                               groundLayer, QueryTriggerInteraction.Ignore);

                    Gizmos.color = hit ? Color.green : Color.red;
                    Gizmos.DrawLine(origin, origin + Vector3.down * rayLength);
                    Gizmos.DrawWireSphere(samplePos, 0.05f);
                }
            }

            // history (optional, in blue light)
            if (history.Count > 0)
            {
                Gizmos.color = new Color(0.4f, 0.7f, 1f, 0.5f);
                foreach (var entry in history)
                    Gizmos.DrawWireSphere(entry.position, 0.2f);
            }
        }

        #endregion
    }
}
