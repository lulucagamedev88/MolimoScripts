using UnityEngine;

namespace MolimoByLulucaGameDev
{
    /// <summary>
    /// Provides the vfxglobal container component.
    /// </summary>
    public class VFXGlobalContainer : MonoBehaviour
    {
        /// <summary>
        /// Gets or sets the instance value.
        /// </summary>
        public static Transform Instance { get; private set; }

        [Tooltip("if true, this is the container default used by effects that not specify the.")]
        [SerializeField] private bool isDefault = true;

        private void Awake()
        {
            if (isDefault)
            {
                Instance = transform;
            }
        }

        private void OnDestroy()
        {
            if (Instance == transform) Instance = null;
        }
    }
}