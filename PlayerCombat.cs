using UnityEngine;
using UnityEngine.InputSystem;

namespace MolimoByLulucaGameDev
{
    /// <summary>
    /// ANIMATION EVENTS (todos com parâmetro INT = número do estágio):
    ///
    ///   Clip            Estágio   Eventos
    ///   Attack1         1         AE_OpenComboWindow(1)  ... AE_EndAttack(1)
    ///   Combat_Attack2  2         AE_OpenComboWindow(2)  ... AE_EndAttack(2)
    ///   K_Combo01_1     3         AE_OpenComboWindow(3)  ... AE_EndAttack(3)
    ///   K_Combo01_2     4         AE_OpenComboWindow(4)  ... AE_EndAttack(4)
    ///   K_Combo01_3     5         AE_OpenComboWindow(5)  ... AE_EndAttack(5)
    ///   K_Combo01_4     6         (sem Open)             ... AE_EndAttack(6)
    ///
    /// AE_CloseComboWindow(n) é opcional (só se quiseres fechar a janela antes do fim do clip).
    /// AE_EndCombo foi removido: usa AE_EndAttack(6).
    ///
    /// ANTI-MASHING: se o jogador esmagar o botão de ataque (vários cliques muito rápidos),
    /// o input é ignorado e o combo não avança durante mashReleaseDelay segundos após a ativação.
    /// </summary>
    public class PlayerCombat : MonoBehaviour
    {
        // ============================================================
        // STEP FORWARD DATA
        // ============================================================
        [System.Serializable]
        /// <summary>
        /// Represents step forward data data.
        /// </summary>
        public struct StepForwardData
        {
            public float speed;
            public float duration;
            public float delay;
            public float Displacement => speed * duration;
        }

        // ============================================================
        // ATTACK STAGES
        // ============================================================
        private enum AttackStage
        {
            None = 0,
            Attack1 = 1,
            Attack2 = 2,
            Combo1 = 3,
            Combo2 = 4,
            Combo3 = 5,
            Combo4 = 6
        }

        // ============================================================
        // ANIMATION
        // ============================================================
        [Header("Animation")]
        [SerializeField] private Animator animator;

        [Header("Parameter Names")]
        [SerializeField] private string isAttackingParameterName = "IsAttacking";
        [SerializeField] private string attack1TriggerName = "Attack1";
        [SerializeField] private string attack2TriggerName = "Attack2";
        [SerializeField] private string comboTriggerName = "Combo";
        [SerializeField] private string blockTriggerName = "IsBlock";

        [Tooltip("Nome of the state of locomotion (Base Layer) used when the attack is cancelado.")]
        [SerializeField] private string locomotionStateName = "Locomotion";

        // ============================================================
        // COMBO / INPUT BUFFER
        // ============================================================
        [Header("Combo / Input Buffer")]
        [Tooltip("time (s) that the click done before of the window open remains guardado. " +
                 "when the window opens, the click is consumido if still is valid.")]
        [SerializeField] private float inputBufferDuration = 0.35f;

        [Tooltip("FAILSAFE: force the end of the attack stuck (evento esquecido num clip). " +
                 "never smaller that the clip more longo.")]
        [SerializeField] private float attackSafetyTimeout = 4f;

        // ============================================================
        // ANTI-MASHING
        // ============================================================
        [Header("Anti Button-Mashing")]
        [SerializeField] private bool enableAntiMash = true;

        [Tooltip("the click done less of X seconds after of the previous conta the 'fast'. " +
                 "if the your ritmo of esmagar is more lento, rises this value.")]
        [SerializeField] private float mashMaxClickInterval = 0.4f;

        [Tooltip("the click done in the primeiros X seconds of the hit (still in the start of the animation) " +
                 "also conta the 'fast'.")]
        [SerializeField] private float mashEarlyClickTime = 0.2f;

        [Tooltip("in the. of clicks 'fast' consecutive is enable the lock. " +
                 "2 = bloqueia in the 3º click (the 1º never conta).")]
        [SerializeField] private int mashStrikesToLock = 2;

        [Tooltip("writes in the Console the interval, the strikes and the state of the lock the each click. " +
                 "Desliga when is afinado.")]
        [SerializeField] private bool debugAntiMash = true;

        [Tooltip("duration (s) of the lock anti-mash after the its activation. " +
                 "clicks during the lock are ignorados and not prolongam the prazo.")]
        [SerializeField] private float mashReleaseDelay = 0.6f;

        [Tooltip("if active, the attack in curso also is cut when the mashing is detected.")]
        [SerializeField] private bool cancelCurrentAttackOnMash = false;

        /// <summary>Disparado when the mashing é detetado. Liga aqui feedback (sound, VFX, UI).</summary>
        /// <summary>
        /// Raised when on mash penalty changes.
        /// </summary>
        public event System.Action OnMashPenalty;

        private float lastAttackClickTime = -999f;
        private int rapidClickCount = 0;
        private bool mashLockActive = false;
        private float mashLockEndTime = 0f;

        // ============================================================
        // MOVEMENT LOCK
        // ============================================================
        [Header("Movement Lock")]
        [Tooltip("if active, the movement remains locked during all the attack (until when the end/cancel). " +
                 "if disabled, uses the durations below.")]
        [SerializeField] private bool lockMovementForWholeAttack = true;
        [SerializeField] private float attack1MovementLockDuration = 0.3f;
        [SerializeField] private float attack2MovementLockDuration = 0.3f;
        [SerializeField] private float comboMovementLockDuration = 0.3f;

        // ============================================================
        // STEP FORWARD (indexado by stage - 1)
        // ============================================================
        [Header("Attack Step Forward Settings")]
        [SerializeField] private bool useAttackStepForward = true;

        [Tooltip("[0] Attack1  [1] Attack2  [2] Combo01_1  [3] Combo01_2  [4] Combo01_3  [5] Combo01_4. not reordene.")]
        [SerializeField]
        private StepForwardData[] stepForwardData = DefaultStepForward();

        // ============================================================
        // EDGE / ABYSS PROTECTION
        // ============================================================
        [Header("Edge / Abyss Protection")]
        [SerializeField] private bool enableEdgeProtection = true;
        [SerializeField] private PlayerSurfaceProbe surfaceProbe;
        [SerializeField] private bool stopHorizontallyOnEdge = true;

        // ============================================================
        // HARDENING
        // ============================================================
        [Header("Step Forward Hardening")]
        [SerializeField] private bool hardenStepForward = true;
        [SerializeField] private bool suppressRootMotionDuringStep = true;
        [SerializeField] private float attackStartupGracePeriod = 0.2f;

        private float attackStartupGraceTimer = 0f;

        // ============================================================
        // STEP FORWARD: stop when the CONTACTO with enemies
        // ============================================================
        [Header("Step Forward: Contacto with enemies")]
        [Tooltip("the step stops when there is the enemy to the forward. prevents atravessar the target and deixa the knockback be visible.")]
        [SerializeField] private bool stopStepOnEnemyContact = true;
        [Tooltip("Folga (m) between the Player and the enemy where the step stops.")]
        [SerializeField] private float stepContactGap = 0.15f;
        [Tooltip("radius of the sonda. must be ligeiramente smaller that the radius of the capsule of the player.")]
        [SerializeField] private float stepProbeRadius = 0.2f;
        [SerializeField] private float stepProbeBottom = 0.2f;
        [SerializeField] private float stepProbeTop = 1.2f;

        // ============================================================
        // GROUP COMBAT & TARGETING
        // ============================================================
        [Header("Group Combat Settings")]
        [SerializeField] private float groupDetectionRadius = 3.5f;
        [SerializeField] private float visionConeDotThreshold = 0.2f;
        [SerializeField] private LayerMask enemyLayer;

        // ============================================================
        // BLOCK SETTINGS
        // ============================================================
        [Header("Block Settings")]
        [SerializeField] private float blockDuration = 0.4f;
        [SerializeField] private Collider shieldCollider;

        [Tooltip("fraction of the damage REDUZIDA when the lock. 0 = none, 1 = imunidade total.")]
        [Range(0f, 1f)]
        [SerializeField] private float blockDamageReduction = 0.7f;

        [SerializeField] private bool faceClosestEnemyOnBlockStart = true;
        [SerializeField] private bool keepFacingEnemyDuringBlock = true;
        [Tooltip("smoothing of the rotation during the Block. 0 = instant.")]
        [SerializeField] private float blockFacingSlerpSpeed = 20f;

        // ============================================================
        // PRIVATE VARIABLES
        // ============================================================
        private PlayerBrain brain;
        private Rigidbody rb;

        private InputSystem_Actions inputActions;
        private bool isInitialized = false;

        private bool hasWeapon = false;
        private bool hasShield = false;
        private bool combatAllowed = true;

        private bool isAttacking = false;
        private AttackStage currentStage = AttackStage.None;
        private float stageStartTime = 0f;

        private bool comboWindowOpen = false;

        private bool attackInputBuffered = false;
        private float attackInputBufferedTime = 0f;
        private bool preserveBufferedInputThroughMashLock = false;

        private float movementLockTimer = 0f;

        private bool isStepForwardActive = false;
        private float stepDelayTimer = 0f;
        private float stepDurationTimer = 0f;
        private float currentStepSpeed = 0f;
        private Vector3 stepDirection = Vector3.forward;

        private bool isBlocking = false;
        private float blockTimer = 0f;
        private Transform currentBlockTarget = null;

        private int isAttackingParameterHash;
        private int attack1TriggerHash;
        private int attack2TriggerHash;
        private int comboTriggerHash;
        private int blockTriggerHash;
        private int locomotionStateHash;

        private bool hasIsAttackingParameter = false;
        private bool hasAttack1TriggerParameter = false;
        private bool hasAttack2TriggerParameter = false;
        private bool hasComboTriggerParameter = false;
        private bool hasBlockTriggerParameter = false;

        private static readonly Collider[] targetScanBuffer = new Collider[24];

        // ============================================================
        // PUBLIC PROPERTIES
        // ============================================================
        public bool IsInitialized => isInitialized;
        public bool HasWeapon => hasWeapon;
        public bool HasShield => hasShield;
        public bool IsAttacking => isAttacking;
        public int CurrentAttackStep => (int)currentStage;
        public bool IsBlocking => isBlocking;
        public bool IsStepForwardActive => isStepForwardActive;
        public float BlockDamageReduction => blockDamageReduction;
        public bool IsMashPenaltyActive => mashLockActive;

        public Vector3 BlockFacingDirection
        {
            get
            {
                Vector3 fwd = transform.forward;
                fwd.y = 0f;
                return fwd.sqrMagnitude > 0.0001f ? fwd.normalized : Vector3.forward;
            }
        }

        // ============================================================
        // INITIALIZATION
        // ============================================================
        /// <summary>
        /// Performs the initialize operation.
        /// </summary>
        public void Initialize(PlayerBrain brainReference, Rigidbody rigidbody)
        {
            brain = brainReference;
            rb = rigidbody;

            if (animator == null) animator = GetComponent<Animator>();
            if (animator == null) animator = GetComponentInChildren<Animator>();

            if (surfaceProbe == null) surfaceProbe = GetComponent<PlayerSurfaceProbe>();
            if (surfaceProbe == null) surfaceProbe = GetComponentInParent<PlayerSurfaceProbe>();
            if (surfaceProbe == null) surfaceProbe = GetComponentInChildren<PlayerSurfaceProbe>();

            InitializeAnimatorParameters();
            ValidateCombatAnimatorParameters();
            InitializeInputSystem();

            if (shieldCollider != null) shieldCollider.isTrigger = true;

            EnsureStepForwardArrayShape();

            isInitialized = true;

            // Reaplica the state caso SetHasWeapon/SetHasShield tenham sido chamados before of the Initialize.
            ApplyEquipmentState();
        }

        private void InitializeAnimatorParameters()
        {
            if (animator == null) return;

            isAttackingParameterHash = Animator.StringToHash(isAttackingParameterName);
            attack1TriggerHash = Animator.StringToHash(attack1TriggerName);
            attack2TriggerHash = Animator.StringToHash(attack2TriggerName);
            comboTriggerHash = Animator.StringToHash(comboTriggerName);
            blockTriggerHash = Animator.StringToHash(blockTriggerName);
            locomotionStateHash = Animator.StringToHash(locomotionStateName);
        }

        private void InitializeInputSystem()
        {
            if (inputActions != null)
            {
                inputActions.Player.Attack.performed -= OnAttackPerformed;
                inputActions.Player.Block.performed -= OnBlockPerformed;
                inputActions.Dispose();
            }

            inputActions = new InputSystem_Actions();
            inputActions.Player.Attack.performed += OnAttackPerformed;
            inputActions.Player.Block.performed += OnBlockPerformed;
        }

        private bool HasAnimatorParameter(int parameterHash)
        {
            if (animator == null) return false;
            foreach (AnimatorControllerParameter parameter in animator.parameters)
                if (parameter.nameHash == parameterHash) return true;
            return false;
        }

        private void ValidateCombatAnimatorParameters()
        {
            if (animator == null) return;

            hasIsAttackingParameter = HasAnimatorParameter(isAttackingParameterHash);
            hasAttack1TriggerParameter = HasAnimatorParameter(attack1TriggerHash);
            hasAttack2TriggerParameter = HasAnimatorParameter(attack2TriggerHash);
            hasComboTriggerParameter = HasAnimatorParameter(comboTriggerHash);
            hasBlockTriggerParameter = HasAnimatorParameter(blockTriggerHash);

            if (!hasIsAttackingParameter) Debug.LogWarning($"[Combat] parameter '{isAttackingParameterName}' does not exist in the Animator.", this);
            if (!hasAttack1TriggerParameter) Debug.LogWarning($"[Combat] parameter '{attack1TriggerName}' does not exist in the Animator.", this);
            if (!hasAttack2TriggerParameter) Debug.LogWarning($"[Combat] parameter '{attack2TriggerName}' does not exist in the Animator.", this);
            if (!hasComboTriggerParameter) Debug.LogWarning($"[Combat] parameter '{comboTriggerName}' does not exist in the Animator.", this);
            if (!hasBlockTriggerParameter) Debug.LogWarning($"[Combat] parameter '{blockTriggerName}' does not exist in the Animator.", this);
        }

        private static StepForwardData[] DefaultStepForward()
        {
            return new StepForwardData[6]
            {
                new StepForwardData { speed = 4f, duration = 0.15f, delay = 0.05f },
                new StepForwardData { speed = 5f, duration = 0.15f, delay = 0.05f },
                new StepForwardData { speed = 6f, duration = 0.18f, delay = 0.05f },
                new StepForwardData { speed = 6f, duration = 0.18f, delay = 0.05f },
                new StepForwardData { speed = 7f, duration = 0.20f, delay = 0.05f },
                new StepForwardData { speed = 7f, duration = 0.20f, delay = 0.05f },
            };
        }

        private void EnsureStepForwardArrayShape()
        {
            if (stepForwardData == null || stepForwardData.Length != 6)
                stepForwardData = DefaultStepForward();
        }

        // ============================================================
        // EDGE PROTECTION
        // ============================================================
        private bool IsGroundAhead()
        {
            if (!enableEdgeProtection) return true;
            if (surfaceProbe == null) return true;
            if (!surfaceProbe.IsGrounded) return true;
            return surfaceProbe.HasSurfaceAhead(stepDirection);
        }

        // ============================================================
        // WEAPON / SHIELD / PERMISSION STATE
        // ============================================================
        /// <summary>
        /// Sets has weapon.
        /// </summary>
        public void SetHasWeapon(bool equipped)
        {
            hasWeapon = equipped;
            ApplyEquipmentState();
            if (!hasWeapon) CancelAttack();
        }

        /// <summary>
        /// Sets has shield.
        /// </summary>
        public void SetHasShield(bool equipped)
        {
            hasShield = equipped;
            ApplyEquipmentState();
            if (!hasShield) CancelBlock();
        }


        /// <summary>
        /// O PlayerBrain chama isto quando o Player rola, trepa, cai, leva knockdown, etc.
        /// allowed = false cancela ataque/block e ignora input de combate.
        /// </summary>
        public void SetCombatAllowed(bool allowed)
        {
            combatAllowed = allowed;
            if (!allowed)
            {
                CancelAttack();
                CancelBlock();
                rapidClickCount = 0; // anti-mash: restarts the count
            }
        }

        /// <summary>Interrompe the attack current (hit stun, knockdown, sword clash, roll...).</summary>
        /// <summary>
        /// Performs the interrupt attack operation.
        /// </summary>
        public void InterruptAttack() => CancelAttack();

        private void ApplyEquipmentState()
        {
            if (inputActions == null) return;

            if (hasWeapon) inputActions.Player.Attack.Enable();
            else inputActions.Player.Attack.Disable();

            if (hasShield) inputActions.Player.Block.Enable();
            else inputActions.Player.Block.Disable();
        }

        private void OnEnable()
        {
            if (isInitialized) ApplyEquipmentState();
        }

        private void OnDisable()
        {
            if (inputActions == null) return;
            inputActions.Player.Attack.Disable();
            inputActions.Player.Block.Disable();
        }

        // ============================================================
        // UPDATE LOOPS
        // ============================================================
        /// <summary>
        /// Performs the tick combat operation.
        /// </summary>
        public void TickCombat()
        {
            if (!isInitialized) return;
            if (!hasWeapon && !hasShield) return;

            if (brain != null && !brain.IsAlive)
            {
                CancelAttack();
                CancelBlock();
                return;
            }

            if (isBlocking && !hasShield) CancelBlock();

            if (attackStartupGraceTimer > 0f)
                attackStartupGraceTimer -= Time.deltaTime;

            UpdateMashLock();
            UpdateBufferedAttackInput();
            UpdateAttackSafetyTimeout();
            UpdateMovementLockTimer();
            UpdateBlockFacing();
            UpdateBlockTimer();
        }

        // called the partir of the FixedUpdate of the PlayerBrain: timers of the step ficam in time of physics.
        /// <summary>
        /// Performs the tick combat physics operation.
        /// </summary>
        public void TickCombatPhysics()
        {
            if (!isInitialized || !hasWeapon) return;
            UpdateStepForwardTimers(Time.fixedDeltaTime);
            ApplyStepForwardPhysics();
        }

        /// <summary>
        /// Performs the tick combat late physics operation.
        /// </summary>
        public void TickCombatLatePhysics()
        {
            if (!hardenStepForward) return;
            if (!isInitialized || !hasWeapon) return;
            if (!isStepForwardActive) return;
            if (stepDelayTimer > 0f) return;
            if (stepDurationTimer <= 0f) return;
            if (rb == null) return;

            if (!IsGroundAhead() || IsEnemyAheadOfStep())
            {
                AbortStepForwardAtEdge();
                return;
            }

            float y = rb.linearVelocity.y;
            rb.linearVelocity = new Vector3(
                stepDirection.x * currentStepSpeed,
                y,
                stepDirection.z * currentStepSpeed);
        }

        // attention: only can existir the OnAnimatorMove by GameObject that chame ApplyBuiltinRootMotion,
        // and only is called if this script is in the same GameObject of the Animator.
        private void OnAnimatorMove()
        {
            if (animator == null) return;
            if (!animator.applyRootMotion) return;

            if (suppressRootMotionDuringStep
                && isStepForwardActive
                && stepDelayTimer <= 0f
                && stepDurationTimer > 0f)
            {
                return;
            }

            animator.ApplyBuiltinRootMotion();
        }

        // ============================================================
        // ANTI-MASHING
        // ============================================================
        /// <summary>returns true if este clique must be ignored by causa of mashing.</summary>
        private bool IsMashingBlocked()
        {
            if (!enableAntiMash) return false;

            float now = Time.time;

            // lock ativo: ignores the clique without prolongar the lock.
            if (mashLockActive)
            {
                if (debugAntiMash)
                    Debug.Log("[Combat/AntiMash] clique ignored (lock ativo)", this);
                return true;
            }

            float interval = now - lastAttackClickTime;
            lastAttackClickTime = now;

            bool tooFastSinceLastClick = interval < mashMaxClickInterval;
            bool tooEarlyInStage = isAttacking && (now - stageStartTime) < mashEarlyClickTime;
            bool rapid = tooFastSinceLastClick || tooEarlyInStage;

            if (rapid) rapidClickCount++;
            else rapidClickCount = 0; // clique calmo resets the contagem

            if (debugAntiMash)
                Debug.Log($"[Combat/AntiMash] intervalo={interval:F2}s precoce={tooEarlyInStage} " +
                          $"strikes={rapidClickCount}/{mashStrikesToLock} estágio={currentStage}", this);

            if (rapidClickCount >= mashStrikesToLock)
            {
                bool bufferedInputWasValid = IsBufferValid;
                rapidClickCount = 0;
                mashLockActive = true;
                mashLockEndTime = now + mashReleaseDelay;
                preserveBufferedInputThroughMashLock = bufferedInputWasValid;

                if (debugAntiMash) Debug.Log("[Combat/AntiMash] >>> lock ATIVADO", this);

                if (cancelCurrentAttackOnMash && isAttacking) CancelAttack();

                OnMashPenalty?.Invoke();
                return true;
            }

            return false;
        }

        // the lock ends mashReleaseDelay seconds depois of the activation.
        private void UpdateMashLock()
        {
            if (!mashLockActive) return;
            if (Time.time < mashLockEndTime) return;

            mashLockActive = false;
            rapidClickCount = 0;
        }

        // ============================================================
        // INPUT BUFFER
        // ============================================================
        private bool IsBufferValid => attackInputBuffered
                                      && (preserveBufferedInputThroughMashLock
                                          ? Time.time <= mashLockEndTime
                                          : Time.time - attackInputBufferedTime <= inputBufferDuration);

        private void UpdateBufferedAttackInput()
        {
            if (!attackInputBuffered) return;

            if (enableAntiMash && mashLockActive) return;

            if (!IsBufferValid)
            {
                attackInputBuffered = false;
                preserveBufferedInputThroughMashLock = false;
                return;
            }

            if (comboWindowOpen && CanAdvanceFrom(currentStage))
            {
                attackInputBuffered = false;
                preserveBufferedInputThroughMashLock = false;
                AdvanceCombo();
            }
        }

        private static bool CanAdvanceFrom(AttackStage stage)
        {
            return stage >= AttackStage.Attack1 && stage <= AttackStage.Combo3;
        }

        // ============================================================
        // TARGETING
        // ============================================================
        private static bool IsTargetAlive(Collider col)
        {
            // GetComponentInParent already inclui the own GameObject.
            IDamageable damageable = col.GetComponentInParent<IDamageable>();
            return damageable == null || damageable.CurrentHealth > 0f;
        }

        private Transform GetBestTarget()
        {
            int count = Physics.OverlapSphereNonAlloc(transform.position, groupDetectionRadius, targetScanBuffer, enemyLayer);
            if (count == 0) return null;

            Transform closestInFront = null;
            float minDistanceInFront = float.MaxValue;
            Transform absoluteClosest = null;
            float minDistanceAbsolute = float.MaxValue;

            for (int i = 0; i < count; i++)
            {
                Collider col = targetScanBuffer[i];
                if (col == null || !IsTargetAlive(col)) continue;

                Vector3 dirToEnemy = col.transform.position - transform.position;
                dirToEnemy.y = 0f;
                float distance = dirToEnemy.magnitude;

                if (distance < minDistanceAbsolute)
                {
                    minDistanceAbsolute = distance;
                    absoluteClosest = col.transform;
                }

                if (distance > 0.001f)
                {
                    float dot = Vector3.Dot(transform.forward, dirToEnemy.normalized);
                    if (dot >= visionConeDotThreshold && distance < minDistanceInFront)
                    {
                        minDistanceInFront = distance;
                        closestInFront = col.transform;
                    }
                }
            }

            return closestInFront != null ? closestInFront : absoluteClosest;
        }

        private Transform GetClosestEnemy()
        {
            int count = Physics.OverlapSphereNonAlloc(transform.position, groupDetectionRadius, targetScanBuffer, enemyLayer);
            if (count == 0) return null;

            Transform closest = null;
            float minDistance = float.MaxValue;

            for (int i = 0; i < count; i++)
            {
                Collider col = targetScanBuffer[i];
                if (col == null || !IsTargetAlive(col)) continue;

                Vector3 dirToEnemy = col.transform.position - transform.position;
                dirToEnemy.y = 0f;
                float distance = dirToEnemy.magnitude;

                if (distance < minDistance)
                {
                    minDistance = distance;
                    closest = col.transform;
                }
            }

            return closest;
        }

        private void SetRotation(Quaternion rotation)
        {
            transform.rotation = rotation;
            if (rb != null) rb.rotation = rotation;
        }

        private void FaceTarget()
        {
            Transform target = GetBestTarget();
            if (target == null) return;

            Vector3 direction = target.position - transform.position;
            direction.y = 0f;
            if (direction.sqrMagnitude > 0.001f)
                SetRotation(Quaternion.LookRotation(direction));
        }

        private void FaceClosestEnemy(bool smooth)
        {
            Transform target = GetClosestEnemy();
            currentBlockTarget = target;
            if (target == null) return;

            Vector3 direction = target.position - transform.position;
            direction.y = 0f;
            if (direction.sqrMagnitude <= 0.001f) return;

            Quaternion targetRotation = Quaternion.LookRotation(direction);

            if (smooth && blockFacingSlerpSpeed > 0f)
                SetRotation(Quaternion.Slerp(transform.rotation, targetRotation, blockFacingSlerpSpeed * Time.deltaTime));
            else
                SetRotation(targetRotation);
        }

        // ============================================================
        // TARGET KILL & GROUP COMBAT
        // ============================================================
        private int GetNearbyAliveEnemiesCount(GameObject ignoreEnemy = null)
        {
            int count = Physics.OverlapSphereNonAlloc(transform.position, groupDetectionRadius, targetScanBuffer, enemyLayer);
            int aliveCount = 0;

            for (int i = 0; i < count; i++)
            {
                Collider col = targetScanBuffer[i];
                if (col == null) continue;

                if (ignoreEnemy != null && (col.gameObject == ignoreEnemy || col.transform.IsChildOf(ignoreEnemy.transform)))
                    continue;

                if (!IsTargetAlive(col)) continue;
                aliveCount++;
            }

            return aliveCount;
        }

        /// <summary>
        /// Handles target killed events.
        /// </summary>
        public void OnTargetKilled(GameObject killedEnemy = null, bool stopCurrentAnimationImmediately = false)
        {
            int remainingEnemies = GetNearbyAliveEnemiesCount(killedEnemy);
            if (remainingEnemies > 0) return;

            if (stopCurrentAnimationImmediately && isAttacking)
                CancelAttack();
        }

        /// <summary>
        /// Performs the ae_check combat end operation.
        /// </summary>
        public void AE_CheckCombatEnd()
        {
            if (attackStartupGraceTimer > 0f) return;
            if (GetNearbyAliveEnemiesCount() == 0)
                CancelAttack();
        }

        // ============================================================
        // ANIMATION EVENTS — all validate the stage, so events
        // atrasados of the clip previous (during the blend) are ignorados.
        // ============================================================

        /// <summary>Abre the window of continuação. remains open until when the end of the attack.</summary>
        /// <summary>
        /// Performs the ae_open combo window operation.
        /// </summary>
        public void AE_OpenComboWindow(int stage)
        {
            if (!isAttacking || (int)currentStage != stage) return;
            if (!CanAdvanceFrom(currentStage)) return;

            comboWindowOpen = true;

            // consumes already the click guardado (without wait by next TickCombat),
            // but keeps-in the during the lock anti-mash.
            if (IsBufferValid && !(enableAntiMash && mashLockActive))
            {
                attackInputBuffered = false;
                preserveBufferedInputThroughMashLock = false;
                AdvanceCombo();
            }
        }

        /// <summary>optional: fecha the window before of the end of the clip.</summary>
        /// <summary>
        /// Performs the ae_close combo window operation.
        /// </summary>
        public void AE_CloseComboWindow(int stage)
        {
            if ((int)currentStage != stage) return;
            comboWindowOpen = false;
        }

        /// <summary>end of the clip. Colocar in all the 6 clips with the respetivo number.</summary>
        /// <summary>
        /// Performs the ae_end attack operation.
        /// </summary>
        public void AE_EndAttack(int stage)
        {
            if (!isAttacking || (int)currentStage != stage) return;
            FinishAttack();
        }

        // ============================================================
        // STEP FORWARD
        // ============================================================
        private void TriggerStepForward(int stepIndex)
        {
            if (!useAttackStepForward) return;
            if (stepForwardData == null || stepIndex < 0 || stepIndex >= stepForwardData.Length) return;

            StepForwardData d = stepForwardData[stepIndex];
            if (d.speed <= 0f || d.duration <= 0f) return;

            Vector3 dir = transform.forward;
            dir.y = 0f;

            currentStepSpeed = d.speed;
            stepDurationTimer = d.duration;
            stepDelayTimer = d.delay;
            stepDirection = dir.sqrMagnitude > 0.0001f ? dir.normalized : Vector3.forward;
            isStepForwardActive = true;
        }

        private void StopStepForward()
        {
            isStepForwardActive = false;
            stepDelayTimer = 0f;
            stepDurationTimer = 0f;
            currentStepSpeed = 0f;
        }

        // CapsuleCast (and not esfera) because enemies baixos, the the spider, passavam by down of the sonda.
        // Queries ignore Physics.IgnoreCollision, so they work even when Player/Enemy do not collide.
        private bool IsEnemyAheadOfStep()
        {
            if (!stopStepOnEnemyContact) return false;

            Vector3 p1 = transform.position + Vector3.up * stepProbeBottom;
            Vector3 p2 = transform.position + Vector3.up * stepProbeTop;
            float reach = currentStepSpeed * Time.fixedDeltaTime + stepContactGap;

            return Physics.CapsuleCast(p1, p2, stepProbeRadius, stepDirection, out _,
                                       reach, enemyLayer, QueryTriggerInteraction.Ignore);
        }

        private void AbortStepForwardAtEdge()
        {
            if (stopHorizontallyOnEdge && rb != null)
            {
                float y = rb.linearVelocity.y;
                rb.linearVelocity = new Vector3(0f, y, 0f);
            }

            StopStepForward();
        }

        private void UpdateStepForwardTimers(float dt)
        {
            if (!isStepForwardActive) return;

            if (stepDelayTimer > 0f)
            {
                stepDelayTimer -= dt;
                return;
            }

            if (stepDurationTimer > 0f)
            {
                stepDurationTimer -= dt;
                if (stepDurationTimer <= 0f) StopStepForward();
            }
        }

        private void ApplyStepForwardPhysics()
        {
            if (!isStepForwardActive || stepDelayTimer > 0f || rb == null) return;

            if (!IsGroundAhead() || IsEnemyAheadOfStep())
            {
                AbortStepForwardAtEdge();
                return;
            }

            float verticalVelocity = rb.linearVelocity.y;
            rb.linearVelocity = new Vector3(
                stepDirection.x * currentStepSpeed,
                verticalVelocity,
                stepDirection.z * currentStepSpeed);
        }

        // ============================================================
        // ATTACK FLOW
        // ============================================================
        private void HandleAttackInput()
        {
            if (currentStage == AttackStage.None)
            {
                EnterStage(AttackStage.Attack1, faceTarget: true);
                return;
            }

            if (!CanAdvanceFrom(currentStage)) return;

            if (comboWindowOpen)
            {
                AdvanceCombo();
            }
            else
            {
                attackInputBuffered = true;
                attackInputBufferedTime = Time.time;
            }
        }

        private void AdvanceCombo()
        {
            if (!CanAdvanceFrom(currentStage)) return;

            // Anti-mash: with the lock active the combo not advances (the attack current acaba and ready).
            if (enableAntiMash && mashLockActive)
                return;

            AttackStage nextStage = currentStage + 1;

            // Reenquadra the target when entering in the combo; inside the combo keeps rotation.
            EnterStage(nextStage, faceTarget: nextStage == AttackStage.Combo1);
        }

        private void EnterStage(AttackStage stage, bool faceTarget)
        {
            if (animator == null) return;

            currentStage = stage;
            isAttacking = true;
            stageStartTime = Time.time;

            comboWindowOpen = false;
            attackInputBuffered = false;
            preserveBufferedInputThroughMashLock = false;

            if (faceTarget) FaceTarget();

            GetStageSettings(stage, out float lockDuration, out int triggerHash, out bool hasTrigger);

            attackStartupGraceTimer = attackStartupGracePeriod;

            if (lockMovementForWholeAttack)
            {
                movementLockTimer = 0f;
                brain?.SetMovementInputLocked(true);
            }
            else
            {
                LockMovement(lockDuration);
            }

            TriggerStepForward((int)stage - 1);

            if (hasIsAttackingParameter) animator.SetBool(isAttackingParameterHash, true);
            ResetAttackTriggers();
            if (hasTrigger) animator.SetTrigger(triggerHash);
        }

        private void GetStageSettings(AttackStage stage, out float lockDuration, out int triggerHash, out bool hasTrigger)
        {
            switch (stage)
            {
                case AttackStage.Attack1:
                    lockDuration = attack1MovementLockDuration;
                    triggerHash = attack1TriggerHash;
                    hasTrigger = hasAttack1TriggerParameter;
                    break;
                case AttackStage.Attack2:
                    lockDuration = attack2MovementLockDuration;
                    triggerHash = attack2TriggerHash;
                    hasTrigger = hasAttack2TriggerParameter;
                    break;
                case AttackStage.Combo1:
                case AttackStage.Combo2:
                case AttackStage.Combo3:
                case AttackStage.Combo4: // Combo4 also uses the trigger "Combo" (Combo01_3 -> Combo01_4)
                    lockDuration = comboMovementLockDuration;
                    triggerHash = comboTriggerHash;
                    hasTrigger = hasComboTriggerParameter;
                    break;
                default:
                    lockDuration = 0f;
                    triggerHash = 0;
                    hasTrigger = false;
                    break;
            }
        }

        private void ResetAttackTriggers()
        {
            if (animator == null) return;

            if (hasAttack1TriggerParameter) animator.ResetTrigger(attack1TriggerHash);
            if (hasAttack2TriggerParameter) animator.ResetTrigger(attack2TriggerHash);
            if (hasComboTriggerParameter) animator.ResetTrigger(comboTriggerHash);
        }

        // ============================================================
        // TIMERS
        // ============================================================
        private void UpdateAttackSafetyTimeout()
        {
            if (!isAttacking) return;
            if (Time.time - stageStartTime <= attackSafetyTimeout) return;

            Debug.LogWarning("[Combat] attack terminado by safety timeout. " +
                             "checks whether the AE_EndAttack(n) is colocado in all the clips.", this);
            FinishAttack();
        }

        private void LockMovement(float duration)
        {
            brain?.SetMovementInputLocked(true);
            movementLockTimer = duration;
        }

        private void UpdateMovementLockTimer()
        {
            if (movementLockTimer <= 0f) return;
            movementLockTimer -= Time.deltaTime;
            if (movementLockTimer <= 0f)
            {
                movementLockTimer = 0f;
                if (!isBlocking) brain?.SetMovementInputLocked(false);
            }
        }

        // ============================================================
        // END / CANCEL
        // ============================================================
        private void ResetAttackState()
        {
            isAttacking = false;
            currentStage = AttackStage.None;
            stageStartTime = 0f;

            comboWindowOpen = false;
            attackInputBuffered = false;
            preserveBufferedInputThroughMashLock = false;

            StopStepForward();

            movementLockTimer = 0f;
            if (!isBlocking) brain?.SetMovementInputLocked(false);
        }

        // end natural: the Animator exits by IsAttacking == false.
        private void FinishAttack()
        {
            ResetAttackState();

            if (animator != null)
            {
                if (hasIsAttackingParameter) animator.SetBool(isAttackingParameterHash, false);
                ResetAttackTriggers(); // prevents triggers "presos" the disparar transitions more tarde
            }
        }

        // Cancelamento forced.
        // forceReturnToLocomotion = true  → does CrossFade is Locomotion (behavior old).
        // forceReturnToLocomotion = false → not toca in the Animator (deixa the PlayerBrain controlar the animation:
        //                                    Knockdown, Roll, PlayerClimb, Death, etc.).
        // the CrossFade only ocorre also if combatAllowed == true, is never cortar animations
        // controladas by other system when SetCombatAllowed(false) is the correr.
        private void CancelAttack(bool forceReturnToLocomotion = true)
        {
            bool wasAttacking = isAttacking;

            ResetAttackState();

            if (animator == null) return;

            if (hasIsAttackingParameter) animator.SetBool(isAttackingParameterHash, false);
            ResetAttackTriggers();

            if (forceReturnToLocomotion && combatAllowed
                && wasAttacking && animator.HasState(0, locomotionStateHash))
            {
                animator.CrossFadeInFixedTime(locomotionStateHash, 0.1f);
            }
        }

        // ============================================================
        // BLOCK METHODS
        // ============================================================
        private void HandleBlockInput()
        {
            if (isBlocking) return;
            if (!hasShield) return;
            if (isAttacking) CancelAttack();
            StartBlock();
        }

        private void StartBlock()
        {
            if (faceClosestEnemyOnBlockStart) FaceClosestEnemy(smooth: false);
            else FaceTarget();

            isBlocking = true;
            blockTimer = blockDuration;
            brain?.SetMovementInputLocked(true);

            if (shieldCollider != null) shieldCollider.isTrigger = false;

            if (animator != null && hasBlockTriggerParameter)
                animator.SetTrigger(blockTriggerHash);
        }

        // Equivale when the behavior previous: enfrenta always the enemy more next.
        private void UpdateBlockFacing()
        {
            if (!isBlocking || !keepFacingEnemyDuringBlock) return;
            if (brain != null && !brain.IsAlive) return;

            FaceClosestEnemy(smooth: true);
        }

        private void UpdateBlockTimer()
        {
            if (!isBlocking) return;
            blockTimer -= Time.deltaTime;
            if (blockTimer <= 0f) EndBlock();
        }

        private void EndBlock()
        {
            isBlocking = false;
            blockTimer = 0f;
            currentBlockTarget = null;
            if (shieldCollider != null) shieldCollider.isTrigger = true;
            brain?.SetMovementInputLocked(false);
        }

        private void CancelBlock()
        {
            if (isBlocking) EndBlock();
        }

        // ============================================================
        // INPUT CALLBACKS
        // ============================================================
        private void OnAttackPerformed(InputAction.CallbackContext context)
        {
            if (!isInitialized || !hasWeapon || !combatAllowed) return;
            if (brain != null && !brain.IsAlive) return;
            if (isBlocking) return;

            if (IsMashingBlocked()) return; // anti-mash: ignores the click

            // the buffer existing cannot be replaced, renewed or
            // transformed num second advance by same period of attack.
            if (isAttacking && CanAdvanceFrom(currentStage) && attackInputBuffered)
                return;

            // preserves in the maximum the input of continuation during the attack.
            // this input not dispara attacks adicionais: only will be consumido
            // when the window of combo is valid.
            if (isAttacking && CanAdvanceFrom(currentStage))
            {
                attackInputBuffered = true;
                attackInputBufferedTime = Time.time;
                return;
            }

            HandleAttackInput();
        }

        private void OnBlockPerformed(InputAction.CallbackContext context)
        {
            if (!isInitialized || !hasShield || !combatAllowed) return;
            if (brain != null && !brain.IsAlive) return;
            HandleBlockInput();
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, groupDetectionRadius);

            if (isBlocking)
            {
                Gizmos.color = Color.cyan;
                Vector3 origin = transform.position + Vector3.up * 1.5f;
                Gizmos.DrawRay(origin, BlockFacingDirection * 2f);
            }
        }

        // ============================================================
        // CLEANUP
        // ============================================================
        private void OnDestroy()
        {
            if (inputActions == null) return;

            inputActions.Player.Attack.performed -= OnAttackPerformed;
            inputActions.Player.Block.performed -= OnBlockPerformed;

            inputActions.Disable();
            inputActions.Dispose();
        }
    }
}
