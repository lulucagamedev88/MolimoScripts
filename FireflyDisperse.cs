using UnityEngine;

namespace MolimoByRodrigoLuluca
{
    [RequireComponent(typeof(ParticleSystem))]
    /// <summary>
    /// Provides the firefly disperse component.
    /// </summary>
    public class FireflyDisperse : MonoBehaviour
    {
        [Header("references")]
        [SerializeField] private Transform playerTransform;

        [Header("dispersion")]
        [SerializeField] private float detectionRadius = 3f;
        [SerializeField] private float repulsionStrength = 6f;
        [SerializeField] private float disperseSpeed = 6f;

        [Header("return")]
        [SerializeField] private float returnSpeed = 2f;

        [Header("Particle System Settings")]
        [Tooltip("limit of speed while dispersam — has of be well larger that the repulsion")]
        [SerializeField] private float disperseMaxSpeed = 5f;

        private ParticleSystem ps;
        private ParticleSystem.Particle[] particles;
        private Vector3[] baseVelocities;
        private bool[] hasBase;      // correction 3: flags separadas
        private bool[] isDispersed;  // "captured base" != "the dispersar"

        private float naturalLimit;

        private void Awake()
        {
            ps = GetComponent<ParticleSystem>();

            int max = ps.main.maxParticles;
            particles = new ParticleSystem.Particle[max];
            baseVelocities = new Vector3[max];
            hasBase = new bool[max];
            isDispersed = new bool[max];

            // correction 2: World Space → positions/speeds comparable
            // directly with playerTransform.position
            var main = ps.main;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            // correction 1: store the limit natural is restore after
            naturalLimit = ps.limitVelocityOverLifetime.limit.constant;
        }

        private void Update()
        {
            if (playerTransform == null) return;

            int count = ps.GetParticles(particles);
            bool anyInRange = false;

            for (int i = 0; i < count; i++)
            {
                Vector3 particleWorldPos = particles[i].position;

                float distToPlayer = Vector3.Distance(
                    playerTransform.position, particleWorldPos);

                if (distToPlayer <= detectionRadius)
                {
                    anyInRange = true;

                    // correction 3: capturar the base once, without gate of
                    // magnitude — picos of noise already not the prevent
                    if (!hasBase[i])
                    {
                        baseVelocities[i] = particles[i].velocity;
                        hasBase[i] = true;
                    }
                    isDispersed[i] = true;

                    Vector3 awayFromPlayer = (particleWorldPos -
                        playerTransform.position).normalized;

                    float t = Mathf.Clamp01(1f - distToPlayer / detectionRadius);
                    float proximity = t * t * (3f - 2f * t); // smoothstep: more suave

                    Vector3 targetVelocity = baseVelocities[i] +
                        awayFromPlayer * repulsionStrength * proximity;

                    particles[i].velocity = Vector3.Lerp(
                        particles[i].velocity, targetVelocity,
                        disperseSpeed * Time.deltaTime);
                }
                else if (isDispersed[i])
                {
                    // regressa to the speed base and after is released
                    particles[i].velocity = Vector3.Lerp(
                        particles[i].velocity, baseVelocities[i],
                        returnSpeed * Time.deltaTime);

                    if (Vector3.Distance(particles[i].velocity,
                        baseVelocities[i]) < 0.01f)
                    {
                        isDispersed[i] = false; // Particle System retoma controlo
                    }
                }
                // if !hasBase: never was alterada, the system natural follows intacto
            }

            // correction 1: while someone disperses, the limit of speed
            // rises; outside the zona, restores the value natural
            var lim = ps.limitVelocityOverLifetime;
            lim.limit = new ParticleSystem.MinMaxCurve(
                anyInRange ? disperseMaxSpeed : naturalLimit);

            ps.SetParticles(particles, count);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 1f, 0f, 0.2f);
            Gizmos.DrawSphere(transform.position, detectionRadius);
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, detectionRadius);
        }
    }
}