using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MolimoByLulucaGameDev
{
    /// <summary>
    /// Provides the player climb component.
    /// </summary>
    public class PlayerClimb : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("reference when the Animator of the player.")]
        [SerializeField] private Animator animator;
        [Tooltip("reference when the Brain primary of the player.")]
        [SerializeField] private PlayerBrain brain;
        [Tooltip("reference when the Movement of the player (usada is suspender Jump/Fall/Land/Sprint during the climbing).")]
        [SerializeField] private PlayerMovement movement;
        [Tooltip("reference when the WeaponEquip of the player is gerenciar the espadas.")]
        [SerializeField] private PlayerWeaponEquipment weaponEquip;
        [Tooltip("reference when the PlayerVFX is disparar the particles of climbing.")]
        [SerializeField] private PlayerVFX playerVFX;

        [Header("Sensors (Triggers)")]
        [Tooltip("Collider of Trigger in the head is detect the TOPO of the wall (exclusivamente Roof).")]
        [SerializeField] private Collider floorSensor;
        [Tooltip("Collider of Trigger in the body is detect the wall/surface climbable.")]
        [SerializeField] private Collider wallSensor;

        [Header("Detection Layers")]
        [Tooltip("layer (Layer) assigned the when the Roof/roof €” and the ONLY that the sensor of the head accepts.")]
        [SerializeField] private LayerMask roofLayer;

        [Tooltip("layer (Layer) assigned the to the surfaces escalaveis (climbable).")]
        [SerializeField] private LayerMask climbableLayer;

        [Header("Input System")]
        [Tooltip("reference is the action of Interact of the its InputActions.")]
        [SerializeField] private InputActionReference interactAction;
        [Tooltip("reference is the action of Move (Vector2) of the its InputActions.")]
        [SerializeField] private InputActionReference moveAction;

        [Header("Animation")]
        [Tooltip("Nome of the state of the Animator is where the Player returns after concluir the climbing in the topo.\n" +
                 "must bater with the nome exact of the state in the Animator Controller (level root).\n" +
                 "in the its controller current, and \"Idle\".")]
        [SerializeField] private string locomotionStateName = "Idle";

        [Tooltip("duration of the crossfade when the return is the locomocathe after the climbing.\n" +
                 "Serve the cinto of segurancthe caso the transicathe Climb ?' Exit of the Animator falhe.")]
        [SerializeField] private float climbToLocomotionCrossfade = 0.1f;

        [Tooltip("if enabled, force the crossfade in the Animator when the concluir the climbing.\n" +
                 "can turn off if you confiar 100% in the transicathe Climb ?' Exit of the Animator.")]
        [SerializeField] private bool forceExitOnRoofTransition = true;

        [Header("Climb Settings")]
        [Tooltip("speed vertical when the rise and descend in the wall.")]
        [SerializeField] private float climbSpeed = 1f;

        [Header("Climb Facing Lock")]
        [Tooltip("locks the rotation of the player during the climbing numa of the directions permitidas.")]
        [SerializeField] private bool lockClimbFacing = true;

        [Tooltip("directions permitidas (Y and ignored; only X/Z importam).\n" +
                 "the Player and rotated is the more next when the agarrar the wall.\n" +
                 "Ex. padrathe: < Oeste, ^ Norte, > Leste.")]
        [SerializeField]
        private Vector3[] allowedClimbFacings = new Vector3[]
        {
            new Vector3(-1f, 0f, 0f), // < Oeste
            new Vector3( 0f, 0f, 1f), // ^ Norte
            new Vector3( 1f, 0f, 0f), // > Leste
        };

        [Tooltip("Reforcthe the rotation during all the climbing (not only when the start). " +
                 "�stil if some other system try rotate the Player.")]
        [SerializeField] private bool enforceFacingWhileClimbing = true;

        [Header("Roof Transition Settings")]
        [Tooltip("duration in seconds of the movement transitional is rise in the roof.")]
        [SerializeField] private float transitionDuration = 0.4f;
        [Tooltip("Quanto the player rises verticalmente when the transitar is the roof.")]
        [SerializeField] private float upOffset = 2f;
        [Tooltip("How far the player advances forward when stepping onto the roof.")]
        [SerializeField] private float forwardOffset = 0.4f;

        [Header("Status Debug")]
        [SerializeField] private bool isTouchingRoof;
        [SerializeField] private bool isTouchingClimbable;
        [SerializeField] private bool isClimbing;
        [SerializeField] private bool isTransitioningToRoof;

        private Rigidbody rb;
        private Vector2 moveInput;

        // Hashes of animation
        private int climbSpeedHash;
        private int isClimbingHash;

        // Buffer static is checagem of physics without alocacathe of GC
        private static readonly Collider[] sensorResults = new Collider[4];

        public bool IsTouchingRoof => isTouchingRoof;
        public bool IsTouchingClimbable => isTouchingClimbable;
        public bool IsClimbing => isClimbing;
        public bool IsTransitioningToRoof => isTransitioningToRoof;

        private void Awake()
        {
            if (brain == null) brain = GetComponentInParent<PlayerBrain>();
            if (brain != null) rb = brain.PlayerRigidbody;
            if (rb == null) rb = GetComponentInParent<Rigidbody>();

            if (animator == null && brain != null)
                animator = brain.GetComponent<Animator>();
            if (animator == null)
                animator = GetComponentInParent<Animator>();

            if (movement == null)
                movement = GetComponentInParent<PlayerMovement>();

            if (weaponEquip == null)
                weaponEquip = GetComponentInParent<PlayerWeaponEquipment>();
            if (weaponEquip == null)
                weaponEquip = GetComponent<PlayerWeaponEquipment>();

            if (playerVFX == null)
                playerVFX = GetComponentInParent<PlayerVFX>();
            if (playerVFX == null)
                playerVFX = GetComponent<PlayerVFX>();

            // Hashes of the Animator
            climbSpeedHash = Animator.StringToHash("ClimbSpeed");
            isClimbingHash = Animator.StringToHash("IsClimbing");

            if (roofLayer.value == 0)
            {
                Debug.LogWarning("[Climb] roofLayer is empty. the sensor of the head never will detect the roof. " +
                                 "Atribua the Layer of the Roof in the Inspector.");
            }

            if (lockClimbFacing && (allowedClimbFacings == null || allowedClimbFacings.Length == 0))
            {
                Debug.LogWarning("[Climb] lockClimbFacing = ON but allowedClimbFacings is empty. " +
                                 "the locks of direcathe sera ignored.");
            }

            if (forceExitOnRoofTransition && string.IsNullOrEmpty(locomotionStateName))
            {
                Debug.LogWarning("[Climb] forceExitOnRoofTransition = ON but locomotionStateName is empty. " +
                                 "the crossfade sera ignored. Defina the nome of the state base (ex: \"Idle\").");
            }
        }

        private void OnEnable()
        {
            if (interactAction != null && interactAction.action != null)
                interactAction.action.Enable();

            if (moveAction != null && moveAction.action != null)
                moveAction.action.Enable();
        }

        private void Update()
        {
            if (isTransitioningToRoof) return;

            // entry of interact
            if (interactAction != null && interactAction.action != null)
            {
                if (interactAction.action.WasPressedThisFrame())
                {
                    ToggleClimb();
                }
            }

            // entry of movement (forced in -1, 0 or 1)
            if (moveAction != null && moveAction.action != null)
            {
                Vector2 rawInput = moveAction.action.ReadValue<Vector2>();

                float targetY = 0f;
                if (rawInput.y > 0.2f) targetY = 1f;
                else if (rawInput.y < -0.2f) targetY = -1f;

                float targetX = 0f;
                if (rawInput.x > 0.2f) targetX = 1f;
                else if (rawInput.x < -0.2f) targetX = -1f;

                moveInput = new Vector2(targetX, targetY);
            }
        }

        private void FixedUpdate()
        {
            UpdateClimbSensors();

            if (isTransitioningToRoof) return;

            if (isClimbing)
            {
                // Reforcthe the direction travada during all the climbing
                if (enforceFacingWhileClimbing)
                    EnforceClimbFacing();

                // 1. TRANSI�?�fO of TOPO: Chegou in the limit of the Roof subindo
                if (isTouchingRoof && moveInput.y > 0)
                {
                    StartCoroutine(ClimbToRoofRoutine());
                    return;
                }

                // 2. Perdeu the wall (ex: soltou in the ar)
                if (!isTouchingClimbable)
                {
                    StopClimbing();
                    return;
                }

                // 3. Chegou when the Roof descendo (raro, but consistente)
                if (isTouchingRoof && moveInput.y < 0)
                {
                    StopClimbing();
                    return;
                }

                // applies speed vertical in the wall
                float verticalVelocity = moveInput.y * climbSpeed;
                if (rb != null)
                {
                    rb.linearVelocity = new Vector3(0f, verticalVelocity, 0f);
                }

                // updates the Blend Tree (-1 = descend, 0 = stopped, 1 = rise)
                if (animator != null)
                {
                    animator.SetFloat(climbSpeedHash, moveInput.y);
                }

                // ============================================================
                // VFX of climbing
                // - in movement (subindo or descendo): Play
                // - stopped in the middle of the climbing: Stop
                //
                // SetClimbVFXState and idempotente: calls repetidas with the
                // same value not of the nothing, entathe and safe chamar the each frame.
                // ============================================================
                if (playerVFX != null)
                {
                    bool isMovingOnWall = Mathf.Abs(moveInput.y) > 0.01f;
                    playerVFX.SetClimbVFXState(isMovingOnWall);
                }
            }
        }

        /// <summary>
        /// Performs the toggle climb operation.
        /// </summary>
        public void ToggleClimb()
        {
            if (isTransitioningToRoof) return;

            if (isClimbing)
            {
                StopClimbing();
            }
            else if (isTouchingClimbable)
            {
                StartClimbing();
            }
        }

        private void StartClimbing()
        {
            isClimbing = true;

            // locks the rotation before any coisa €” the Player "vira" is the
            // direction canonica of the wall (< ^ >), independente of the chegou.
            SnapToNearestAllowedFacing();

            if (rb != null)
            {
                rb.useGravity = false;
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }

            if (animator != null)
            {
                animator.SetBool(isClimbingHash, true);
            }

            if (movement != null)
            {
                movement.SetSuspended(true);
            }

            if (brain != null)
            {
                brain.SetMovementInputLocked(true);
            }

            // hides the sword of the mathe and shows the of the cintura during the climbing
            if (weaponEquip != null)
            {
                weaponEquip.SetClimbing(true);
            }

            // VFX of climbing starts disabled €” only vai play when the Player
            // efetivamente start the rise or descend. the FixedUpdate cuida disso.
            if (playerVFX != null)
            {
                playerVFX.SetClimbVFXState(false);
            }

            Debug.Log($"[Climb] Player grudou in the wall. Facing={transform.eulerAngles.y:F1}°");
        }

        private void StopClimbing()
        {
            isClimbing = false;

            if (rb != null)
            {
                rb.useGravity = true;
            }

            if (animator != null)
            {
                animator.SetBool(isClimbingHash, false);
                animator.SetFloat(climbSpeedHash, 0f);
            }

            if (movement != null)
            {
                movement.SetSuspended(false);
            }

            if (brain != null)
            {
                brain.SetMovementInputLocked(false);
            }

            // restores the state visual normal of the espadas when exiting of the climb
            if (weaponEquip != null)
            {
                weaponEquip.SetClimbing(false);
            }

            // Desliga the VFX of climbing (Stop)
            if (playerVFX != null)
            {
                playerVFX.SetClimbVFXState(false);
            }

            Debug.Log("[Climb] Player soltou of the wall.");
        }

        // ============================================================
        // CLIMB FACING LOCK
        // ============================================================

        /// <summary>
        /// Rotaciona o Player para a direÃ§Ã£o permitida mais prÃ³xima da sua forward atual.
        /// Ã‰ chamado uma vez em StartClimbing.
        /// </summary>
        private void SnapToNearestAllowedFacing()
        {
            if (!TryGetClimbFacingRotation(out Quaternion target)) return;

            // applies in the Rigidbody (autoridade physics) and in the transform (visual imediato)
            if (rb != null)
            {
                rb.rotation = target;
                rb.angularVelocity = Vector3.zero;
            }
            transform.rotation = target;
        }

        /// <summary>
        /// ReforÃ§o contÃ­nuo durante a escalada â€” barra qualquer tentativa
        /// externa de girar o Player enquanto ele estÃ¡ na parede.
        /// </summary>
        private void EnforceClimbFacing()
        {
            if (!TryGetClimbFacingRotation(out Quaternion target)) return;

            // only corrige if the rotation divergir (prevents cost unnecessary)
            Quaternion currentRotation = (rb != null) ? rb.rotation : transform.rotation;
            if (Quaternion.Angle(currentRotation, target) > 0.5f)
            {
                if (rb != null)
                {
                    rb.rotation = target;
                    rb.angularVelocity = Vector3.zero;
                }
                transform.rotation = target;
            }
        }

        private bool TryGetClimbFacingRotation(out Quaternion target)
        {
            target = Quaternion.identity;
            if (!lockClimbFacing) return false;
            if (allowedClimbFacings == null || allowedClimbFacings.Length == 0) return false;

            Vector3 bestDir = GetNearestAllowedFacing();
            if (bestDir.sqrMagnitude < 0.0001f) return false;

            target = Quaternion.LookRotation(bestDir, Vector3.up);
            return true;
        }

        /// <summary>
        /// Retorna qual das direÃ§Ãµes em allowedClimbFacings estÃ¡ mais alinhada
        /// com a forward atual do Player (usa Dot product no plano XZ).
        /// </summary>
        private Vector3 GetNearestAllowedFacing()
        {
            Vector3 forward = transform.forward;
            forward.y = 0f;

            if (forward.sqrMagnitude < 0.0001f)
                forward = Vector3.forward;

            forward.Normalize();

            Vector3 best = Vector3.zero;
            float bestDot = -2f; // smaller that -1

            for (int i = 0; i < allowedClimbFacings.Length; i++)
            {
                Vector3 candidate = allowedClimbFacings[i];
                candidate.y = 0f;

                if (candidate.sqrMagnitude < 0.0001f) continue;

                candidate.Normalize();

                float dot = Vector3.Dot(forward, candidate);
                if (dot > bestDot)
                {
                    bestDot = dot;
                    best = candidate;
                }
            }

            return best;
        }

        // ============================================================
        // TRANSITION TO ROOF
        // ============================================================

        private IEnumerator ClimbToRoofRoutine()
        {
            isTransitioningToRoof = true;

            if (rb != null)
            {
                rb.linearVelocity = Vector3.zero;
                rb.useGravity = false;
            }

            Vector3 startPosition = transform.position;
            Vector3 targetPosition = startPosition + (Vector3.up * upOffset) + (transform.forward * forwardOffset);

            float elapsedTime = 0f;

            while (elapsedTime < transitionDuration)
            {
                elapsedTime += Time.deltaTime;
                float t = elapsedTime / transitionDuration;

                transform.position = Vector3.Lerp(startPosition, targetPosition, t);
                yield return null;
            }

            transform.position = targetPosition;
            isTransitioningToRoof = false;

            // exits of the state PlayerClimb (isClimbing = false, animator.SetBool("IsClimbing", false),
            // VFX of climb is, etc.)
            StopClimbing();

            // ============================================================
            // �� CORRE�?�fO of the BUG of stuck state
            //
            // when the Player chega when the topo, it and teleportado JA GROUNDED in the Roof.
            // the transicathe PlayerClimb ?' Fall of the Animator exige (IsGrounded == false &&
            // IsFalling == true), the that never acontece nesse caso.
            //
            // CORRE�?�fO PRIMARIA (required): add in the Animator Controller,
            // inside the sub-state-machine PlayerClimb, the transicathe:
            //     PlayerClimb ?' Exit    with condicathe  IsGrounded == true
            // (with Has Exit Time unchecked and duration 0.1).
            //
            // this line below and SAFETY BELT�?the: force the crossfade caso
            // the transicathe of the Animator falhe or is removida without querer.
            // ============================================================
            if (forceExitOnRoofTransition && animator != null && !string.IsNullOrEmpty(locomotionStateName))
            {
                animator.CrossFadeInFixedTime(locomotionStateName, climbToLocomotionCrossfade);
                Debug.Log($"[Climb] CrossFade forced is \"{locomotionStateName}\".");
            }

            Debug.Log("[Climb] Transicathe is the roof concluiof the!");
        }

        // ============================================================
        // SENSORS
        // ============================================================

        private void UpdateClimbSensors()
        {
            // TOPO €” only Roof
            bool roofDetected = false;
            if (floorSensor != null && roofLayer.value != 0)
            {
                int hits = Physics.OverlapBoxNonAlloc(
                    floorSensor.bounds.center,
                    floorSensor.bounds.extents,
                    sensorResults,
                    floorSensor.transform.rotation,
                    roofLayer
                );
                roofDetected = hits > 0;
            }

            // wall €” climbable
            bool climbableDetected = false;
            if (wallSensor != null && climbableLayer.value != 0)
            {
                int hits = Physics.OverlapBoxNonAlloc(
                    wallSensor.bounds.center,
                    wallSensor.bounds.extents,
                    sensorResults,
                    wallSensor.transform.rotation,
                    climbableLayer
                );
                climbableDetected = hits > 0;
            }

            isTouchingRoof = roofDetected;
            isTouchingClimbable = climbableDetected;
        }

        // ============================================================
        // GIZMOS
        // ============================================================

        private void OnDrawGizmosSelected()
        {
            if (!lockClimbFacing) return;
            if (allowedClimbFacings == null) return;

            // Desenha each direction allowed the the seta the partir of the player
            Gizmos.color = Color.magenta;
            for (int i = 0; i < allowedClimbFacings.Length; i++)
            {
                Vector3 dir = allowedClimbFacings[i];
                dir.y = 0f;
                if (dir.sqrMagnitude < 0.0001f) continue;
                dir.Normalize();

                Vector3 origin = transform.position + Vector3.up * 0.1f;
                Vector3 end = origin + dir * 1.5f;
                Gizmos.DrawLine(origin, end);
                Gizmos.DrawSphere(end, 0.08f);
            }
        }
    }
}
