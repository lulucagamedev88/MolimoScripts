using System;
using System.Collections.Generic;
using UnityEngine;

namespace MolimoByLulucaGameDev
{
    /// <summary>
    /// Provides the molimo health container component.
    /// </summary>
    public class MolimoHealthContainer : MonoBehaviour
    {
        private const float Epsilon = 0.0001f;

        #region Events

        public static event Action<int, int> OnMolimoHPChanged;
        public static event Action<EnergyTier> OnEnergyTierRaised;
        /// <summary>
        /// Raised when on item collected changes.
        /// </summary>
        public event Action<int> OnItemCollected;

        /// <summary>
        /// Defines the available energy tier values.
        /// </summary>
        public enum EnergyTier
        {
            Low = 0,
            Medium = 1,
            Powerful = 2
        }

        /// <summary>
        /// Performs the raise hpchanged operation.
        /// </summary>
        public void RaiseHPChanged()
        {
            OnMolimoHPChanged?.Invoke(currentHP, maxHP);
        }

        #endregion

        #region Inspector Fields

        [Header("basket of HP")]
        [Tooltip("capacity of the Container. recommended: 5 (= energy of the layer of the Heal).")]
        [SerializeField, Range(1, 1000)] private int maxHP = 5;
        [SerializeField, Range(0, 1000)] private int currentHP = 0;

        [Tooltip("Se ativo, quando o container atinge a capacidade máxima deixa de atrair itens " +
                 "até voltar a 0. Recria o ciclo 'encher → esvaziar → voltar a encher'.")]
        [SerializeField] private bool latchAttractionUntilEmpty = true;

        [Header("levels of energy (alerts HUD)")]
        [Tooltip("have of be <= to the capacity of the Container. Ex: Medium 3, Powerful 5.")]
        [SerializeField, Range(1, 1000)] private int mediumEnergyThreshold = 3;
        [SerializeField, Range(1, 1000)] private int powerfulEnergyThreshold = 5;

        [Header("Attraction of Items of health")]
        [SerializeField] private LayerMask itemLayer = ~0;
        [SerializeField, Range(0.5f, 30f)] private float attractionRadius = 5f;
        [SerializeField] private Vector3 centerOffset = Vector3.zero;
        [SerializeField] private float pullSpeed = 8f;

        [Header("Gizmos")]
        [SerializeField] private Color gizmoColor = new Color(0f, 1f, 0.5f, 0.3f);

        #endregion

        #region Public Properties

        public int CurrentHP => currentHP;
        public int MaxHP => maxHP;
        public bool HasHP => currentHP > 0;
        public bool IsFull => currentHP >= maxHP;
        public float AttractionRadius => attractionRadius;

        /// <summary>
        /// Energia exata (com fração) usada pelas transferências progressivas e pela barra do HUD.
        /// CurrentHP é o valor inteiro (arredondado para cima) desta energia.
        /// </summary>
        public float ExactEnergy => Mathf.Max(0f, currentHP - _spentFraction);

        #endregion

        #region Internal State

        private readonly Collider[] _overlapResults = new Collider[20];

        // fraction already spends of the unidade inteira of topo (0 the 1). prevents perder energy by arredondamento.
        private float _spentFraction;

        // Items that the Container already attracted and that still will deposit (reserve of space).
        private readonly List<MolimoCollectableHP> _incoming = new List<MolimoCollectableHP>();

        // true quando o container atingiu o máximo e ainda não voltou a 0.
        private bool _attractionLatched;

        #endregion

        #region Unity Lifecycle

        private void Start()
        {
            RaiseHPChanged();
        }

        private void Update()
        {
            // Liberta o latch quando o container volta a estar vazio.
            if (_attractionLatched && currentHP <= 0)
                _attractionLatched = false;

            if (IsFull) return;
            if (latchAttractionUntilEmpty && _attractionLatched) return;

            DetectAndAttractItems();
        }

        private void OnValidate()
        {
            currentHP = Mathf.Clamp(currentHP, 0, maxHP);
            mediumEnergyThreshold = Mathf.Clamp(mediumEnergyThreshold, 1, maxHP);
            powerfulEnergyThreshold = Mathf.Clamp(powerfulEnergyThreshold, mediumEnergyThreshold, maxHP);
        }

        #endregion

        #region Item Detection

        private void DetectAndAttractItems()
        {
            PruneIncoming();

            // only attracts the that fits: space free less the that already comes the path.
            int room = maxHP - currentHP - IncomingCharge();
            if (room <= 0) return;

            Vector3 center = transform.position + centerOffset;
            int count = Physics.OverlapSphereNonAlloc(
                center, attractionRadius, _overlapResults, itemLayer,
                QueryTriggerInteraction.Collide);

            for (int i = 0; i < count; i++)
            {
                Collider col = _overlapResults[i];
                if (col == null) continue;

                MolimoCollectableHP item = col.GetComponentInParent<MolimoCollectableHP>();
                if (item == null) continue;
                if (item.IsAttracted) continue;
                if (item.HealCharge > room) continue;

                item.AttractTo(transform, pullSpeed);

                if (item.IsAttracted)
                {
                    _incoming.Add(item);
                    room -= item.HealCharge;
                    if (room <= 0) break;
                }
            }
        }

        private void PruneIncoming()
        {
            for (int i = _incoming.Count - 1; i >= 0; i--)
            {
                MolimoCollectableHP item = _incoming[i];
                if (item == null || !item.IsAttracted)
                    _incoming.RemoveAt(i);
            }
        }

        private int IncomingCharge()
        {
            int sum = 0;
            for (int i = 0; i < _incoming.Count; i++)
                sum += _incoming[i].HealCharge;
            return sum;
        }

        #endregion

        #region Energy Tier API

        /// <summary>
        /// Returns current tier.
        /// </summary>
        public EnergyTier GetCurrentTier()
        {
            if (currentHP >= powerfulEnergyThreshold) return EnergyTier.Powerful;
            if (currentHP >= mediumEnergyThreshold) return EnergyTier.Medium;
            return EnergyTier.Low;
        }

        private void FireTierRaisedIfNeeded(EnergyTier before)
        {
            EnergyTier after = GetCurrentTier();
            if (after <= before) return;

            if (before < EnergyTier.Medium && after >= EnergyTier.Medium)
                OnEnergyTierRaised?.Invoke(EnergyTier.Medium);

            if (before < EnergyTier.Powerful && after >= EnergyTier.Powerful)
                OnEnergyTierRaised?.Invoke(EnergyTier.Powerful);
        }

        #endregion

        #region Basket API

        /// <summary>
        /// Attempts to add hp.
        /// </summary>
        public bool TryAddHP(int amount)
        {
            if (amount <= 0) return false;
            if (IsFull) return false;

            EnergyTier tierBefore = GetCurrentTier();

            int before = currentHP;
            currentHP = Mathf.Clamp(currentHP + amount, 0, maxHP);
            int added = currentHP - before;

            if (added > 0)
            {
                OnMolimoHPChanged?.Invoke(currentHP, maxHP);
                OnItemCollected?.Invoke(added);
                FireTierRaisedIfNeeded(tierBefore);

                // Latch: ao atingir o máximo, para de atrair até voltar a 0.
                if (latchAttractionUntilEmpty && currentHP >= maxHP)
                    _attractionLatched = true;
            }

            return added > 0;
        }

        /// <summary>
        /// Attempts to consume hp.
        /// </summary>
        public bool TryConsumeHP(int amount)
        {
            if (amount <= 0) return false;
            if (currentHP < amount) return false;

            currentHP -= amount;
            if (currentHP <= 0) _spentFraction = 0f;
            OnMolimoHPChanged?.Invoke(currentHP, maxHP);

            return true;
        }

        /// <summary>
        /// Consome energia de forma progressiva (valores fracionários, ex: taxa * deltaTime).
        /// Nunca consome mais do que existe. Devolve a energia REALMENTE consumida.
        /// </summary>
        public float ConsumeEnergy(float amount)
        {
            if (amount <= 0f || currentHP <= 0) return 0f;

            float exact = currentHP - _spentFraction;
            float consumed = Mathf.Min(amount, exact);
            if (consumed <= 0f) return 0f;

            float newExact = exact - consumed;

            int newHP = Mathf.Max(0, Mathf.CeilToInt(newExact - Epsilon));
            _spentFraction = newHP > 0 ? Mathf.Clamp01(newHP - newExact) : 0f;

            if (newHP != currentHP)
            {
                currentHP = newHP;
                OnMolimoHPChanged?.Invoke(currentHP, maxHP);
            }

            return consumed;
        }

        #endregion

        #region Helpers

        private void OnDrawGizmosSelected()
        {
            Vector3 center = transform.position + centerOffset;
            Gizmos.color = gizmoColor;
            Gizmos.DrawWireSphere(center, attractionRadius);
            Gizmos.color = new Color(gizmoColor.r, gizmoColor.g, gizmoColor.b, 0.1f);
            Gizmos.DrawSphere(center, attractionRadius);
        }

        #endregion

        #region Static Reset

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticEvents()
        {
            OnMolimoHPChanged = null;
            OnEnergyTierRaised = null;
        }

        #endregion
    }
}