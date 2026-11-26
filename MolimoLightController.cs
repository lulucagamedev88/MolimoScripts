using UnityEngine;

namespace MolimoByLulucaGameDev
{
    [DisallowMultipleComponent]
    /// <summary>
    /// Provides the molimo light controller component.
    /// </summary>
    public class MolimoLightController : MonoBehaviour
    {
        [Header("references")]
        [Tooltip("drag the Light that is child of Molimo.")]
        [SerializeField] private Light molimoLight;

        [Tooltip("drag the DayNightLightingController (if deixar empty, it searches automatically).")]
        [SerializeField] private DayNightLightingController dayNightController;

        [Header("Rules of time (light acende)")]
        [Tooltip("hour in that the light acende (17:00 / 5 of the tarde).")]
        [SerializeField, Range(0f, 24f)] private float lightsOnHour = 17f;

        [Tooltip("hour in that the light turns off (07:00 / 7 of the morning).")]
        [SerializeField, Range(0f, 24f)] private float lightsOffHour = 7f;

        private void Awake()
        {
            if (molimoLight == null)
                molimoLight = GetComponentInChildren<Light>();

            if (dayNightController == null)
                dayNightController = FindAnyObjectByType<DayNightLightingController>();
        }

        private void Update()
        {
            if (molimoLight == null || dayNightController == null) return;

            // Pega the time current and the state of the DarkZone of the script primary
            float hour = dayNightController.CurrentTimeOfDay;
            bool isInDarkZone = dayNightController.IsInDarkZone;

            // Calcula if the time is in the window of the light (17h at the 7h)
            bool isTimeToBeOn;
            if (lightsOnHour > lightsOffHour) // Ex: 17 > 7 (atravessa the meia-night)
                isTimeToBeOn = hour >= lightsOnHour || hour < lightsOffHour;
            else
                isTimeToBeOn = hour >= lightsOnHour && hour < lightsOffHour;

            // the REGRA FINAL: Liga if is the hour certa or if is in the DarkZone
            bool shouldBeOn = isTimeToBeOn || isInDarkZone;

            // applies only in the checkbox (enabled). without mexer in the intensidade!
            if (molimoLight.enabled != shouldBeOn)
            {
                molimoLight.enabled = shouldBeOn;
            }
        }
    }
}