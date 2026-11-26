using UnityEngine;
using MolimoByLulucaGameDev;

namespace MolimoByLulucaGameDev
{
    /// <summary>
    /// Provides the molimo aiposition from prologue component.
    /// </summary>
    public class MolimoAIPositionFromPrologue : MonoBehaviour
    {
        /// <summary>Fica true quando este script posicionou o objeto nesta ativação.</summary>
        /// <summary>
        /// Gets or sets the has pending prologue position value.
        /// </summary>
        public static bool HasPendingProloguePosition { get; private set; }

        private void OnEnable()
        {
            HasPendingProloguePosition = false;

            if (PrologueMolimoController.HasValidProloguePosition)
            {
                transform.position = PrologueMolimoController.LastProloguePosition;
                transform.rotation = PrologueMolimoController.LastPrologueRotation;
                HasPendingProloguePosition = true;

            }
        }
    }
}