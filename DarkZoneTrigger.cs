using UnityEngine;

namespace MolimoByLulucaGameDev
{
    [RequireComponent(typeof(Collider))]
    [DisallowMultipleComponent]
    /// <summary>
    /// Provides the dark zone trigger component.
    /// </summary>
    public class DarkZoneTrigger : MonoBehaviour
    {
        [Tooltip("drag the object that has the DayNightLightingController here.")]
        [SerializeField] private DayNightLightingController dayNightController;

        private void Awake()
        {
            // ensures that the collider is the Trigger is not lock the Player
            GetComponent<Collider>().isTrigger = true;

            if (dayNightController == null)
                dayNightController = FindAnyObjectByType<DayNightLightingController>();
        }

        private void OnTriggerEnter(Collider other)
        {
            // if the Player enter in the area escura
            if (other.CompareTag("Player"))
            {
                dayNightController.SetDarkZone(true);
            }
        }

        private void OnTriggerExit(Collider other)
        {
            // if the Player exit of the area escura
            if (other.CompareTag("Player"))
            {
                dayNightController.SetDarkZone(false);
            }
        }
    }
}