using UnityEngine;

namespace MolimoByLulucaGameDev
{
    // keeps the Overlay camera of the player sincronizada with the camera primary
    // (projection and clipping), already that the camera Stacking of the URP not does this
    // automatically. required by causa of the zoom dinamico via Cinemachine.
    [RequireComponent(typeof(Camera))]
    /// <summary>
    /// Provides the player overlay camera sync component.
    /// </summary>
    public class PlayerOverlayCameraSync : MonoBehaviour
    {
        [SerializeField] private Camera sourceCamera; // Main camera

        private Camera overlayCamera;

        private void Awake()
        {
            overlayCamera = GetComponent<Camera>();

            if (sourceCamera == null)
            {
                Debug.LogError("[PlayerOverlayCameraSync] sourceCamera not assigned the!");
            }
        }

        private void LateUpdate()
        {
            if (sourceCamera == null || overlayCamera == null) return;

            overlayCamera.orthographic = sourceCamera.orthographic;
            overlayCamera.orthographicSize = sourceCamera.orthographicSize;
            overlayCamera.fieldOfView = sourceCamera.fieldOfView;
            overlayCamera.nearClipPlane = sourceCamera.nearClipPlane;
            overlayCamera.farClipPlane = sourceCamera.farClipPlane;
        }
    }
}
