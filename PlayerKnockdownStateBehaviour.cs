using UnityEngine;

namespace MolimoByLulucaGameDev
{
    /// <summary>
    /// Provides the player knockdown state behaviour component.
    /// </summary>
    public class PlayerKnockdownStateBehaviour : StateMachineBehaviour
    {
        override public void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
        {
            PlayerBrain brain = animator.GetComponent<PlayerBrain>();
            if (brain == null) brain = animator.GetComponentInParent<PlayerBrain>();

            if (brain != null)
            {
                brain.EndKnockdown();
            }
        }
    }
}
