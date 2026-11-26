using UnityEngine;

namespace MolimoByLulucaGameDev
{
    [DisallowMultipleComponent]
    /// <summary>
    /// Provides the shadow controller component.
    /// </summary>
    public class ShadowController : MonoBehaviour
    {
        [Header("references")]
        [Tooltip("leave in blank is use automatically the object parent imediato (Transform.parent).")]
        [SerializeField] private Transform targetParent;

        [Header("settings of height of the ground")]
        [Tooltip("if ativado, the height of the ground will be captured automatically in the position Y initial of the shadow.")]
        [SerializeField] private bool useInitialYAsGround = true;
        [Tooltip("height fixa of the ground (used only if the booleano above is false).")]
        [SerializeField] private float fixedGroundY = 0f;

        [Header("settings of scale (illusion of Salto)")]
        [Tooltip("height maximum that the parent can rise before of the shadow atingir the size minimum.")]
        [SerializeField] private float maxHeight = 5f;
        [Tooltip("size of the shadow when the parent is in the ground.")]
        [SerializeField] private float normalScale = 1f;
        [Tooltip("size minimum of the shadow when the parent is in the ar (ex: 0.25 = 25%).")]
        [SerializeField] private float minScale = 0.25f;

        [Header("smoothing")]
        [Tooltip("how fast the shadow changes of size (0 = instant).")]
        [SerializeField, Min(0f)] private float scaleSmoothness = 10f;

        private float _groundY;

        private void Awake()
        {
            // if the targetParent not was arrastado in the Inspector, pega automatically the parent in the hierarchy
            if (targetParent == null)
            {
                targetParent = transform.parent;
            }

            if (targetParent == null)
            {
                Debug.LogError("[ShadowController] none object parent found! Coloque the shadow the child of the personagem or atribua the Target Parent.", this);
                enabled = false;
                return;
            }

            // sets the height of the ground where the shadow must remain fixada
            _groundY = useInitialYAsGround ? transform.position.y : fixedGroundY;
        }

        private void LateUpdate()
        {
            if (targetParent == null) return;

            // 1. position: force the shadow the follow the X and Z of the parent, travando the Y in the ground
            transform.position = new Vector3(targetParent.position.x, _groundY, targetParent.position.z);

            // 2. height relative: Calcula how alto the parent is of the ground
            float height = targetParent.position.y - _groundY;

            // normalizes the height between 0 and 1 with base in the height maximum allowed
            float heightRatio = Mathf.Clamp01(height / maxHeight);

            // 3. scale: Quanto more alto the parent, smaller remains the shadow
            float targetScale = Mathf.Lerp(normalScale, minScale, heightRatio);

            // 4. smoothing of the scale
            float currentScale = transform.localScale.x;
            float finalScale = targetScale;

            if (scaleSmoothness > 0f)
            {
                finalScale = Mathf.Lerp(currentScale, targetScale, Time.deltaTime * scaleSmoothness);
            }

            // applies the scale unificada in X, Y and Z is that all encolham or grow juntos
            transform.localScale = new Vector3(finalScale, finalScale, finalScale);
        }
    }
}