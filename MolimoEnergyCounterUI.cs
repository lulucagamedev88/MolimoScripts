using TMPro;
using UnityEngine;

namespace MolimoByLulucaGameDev
{
    /// <summary>
    /// Apresenta no HUD (TextMeshPro) apenas a energia atual do Molimo como número inteiro.
    /// Ex.: "0", "1", "3", "5".
    ///
    /// Atualiza-se exclusivamente através do evento estático
    /// <see cref="MolimoHealthContainer.OnMolimoHPChanged"/>, sem qualquer polling em Update().
    /// </summary>
    [DisallowMultipleComponent]
    public class MolimoEnergyCounterUI : MonoBehaviour
    {
        #region Inspector Fields

        [Header("Referências")]
        [Tooltip("Componente TextMeshPro (UI) onde o valor será apresentado.")]
        [SerializeField] private TMP_Text energyText;

        [Tooltip("Container do Molimo. Se ficar vazio, é procurado automaticamente uma única vez na cena.")]
        [SerializeField] private MolimoHealthContainer healthContainer;

        #endregion

        #region Internal State

        private bool _subscribed;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            // Tenta resolver a referência ao TMP caso tenha sido esquecida.
            if (energyText == null)
                energyText = GetComponent<TMP_Text>();

            ResolveContainerIfNeeded();
        }

        private void OnEnable()
        {
            // Garante que a referência existe antes de subscrever.
            ResolveContainerIfNeeded();

            // Subscreve o evento estático existente (sem alterar o MolimoHealthContainer).
            if (!_subscribed)
            {
                MolimoHealthContainer.OnMolimoHPChanged += HandleHPChanged;
                _subscribed = true;
            }

            // Atualização imediata com o estado real (sem esperar pela primeira recolha).
            RefreshNow();
        }

        private void OnDisable()
        {
            // Remove a subscrição para evitar duplicações e leaks.
            if (_subscribed)
            {
                MolimoHealthContainer.OnMolimoHPChanged -= HandleHPChanged;
                _subscribed = false;
            }
        }

        #endregion

        #region Update Logic

        private void HandleHPChanged(int current, int max)
        {
            UpdateText(current);
        }

        /// <summary>
        /// Força uma leitura imediata do estado real do container (sem polling contínuo).
        /// </summary>
        private void RefreshNow()
        {
            if (healthContainer != null)
            {
                UpdateText(healthContainer.CurrentHP);
            }
            else
            {
                // Sem container ainda; mostra "0" como salvaguarda até que exista.
                UpdateText(0);
            }
        }

        private void UpdateText(int current)
        {
            if (energyText == null) return;
            energyText.text = current.ToString();
        }

        #endregion

        #region Helpers

        private void ResolveContainerIfNeeded()
        {
            if (healthContainer != null) return;

            // Pesquisa única (não repetida por frame) — só executa se a referência estiver vazia.
            healthContainer = FindFirstObjectByType<MolimoHealthContainer>();
        }

        #endregion
    }
}