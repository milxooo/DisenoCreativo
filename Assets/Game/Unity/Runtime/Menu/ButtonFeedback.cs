using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace RobaColores.Game.Presentation
{
    // Estados visuales de un boton: normal, cursor encima, pulsado y deshabilitado.
    // Va junto a un Button (transicion None). Usa tiempo sin escala y suaviza los cambios.
    [RequireComponent(typeof(Selectable))]
    public sealed class ButtonFeedback : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
    {
        public Color normal = Color.white;
        public Color hover = new Color(1.1f, 1.1f, 1.05f, 1f);
        public Color pressed = new Color(0.82f, 0.78f, 0.7f, 1f);
        public Color disabled = new Color(0.6f, 0.58f, 0.52f, 0.85f);
        public float hoverScale = 1.06f;
        public float pressedScale = 0.96f;
        public RectTransform wiggle;          // icono opcional que se menea al pasar el cursor
        public Graphic target;                // por defecto la imagen del propio boton
        public Graphic textTarget;            // texto opcional que se atenua cuando esta deshabilitado
        public Color textNormal = Color.black;
        public Color textDisabled = new Color(0.45f, 0.4f, 0.35f, 0.8f);

        Selectable selectable;
        bool hovering, pressing;
        float scale = 1f;
        Color current;
        Vector3 baseScale = Vector3.one;
        bool initialized;

        void Awake() { Init(); }

        void Init()
        {
            if (initialized) return;
            initialized = true;
            selectable = GetComponent<Selectable>();
            if (target == null) target = GetComponent<Graphic>();
            baseScale = transform.localScale;
            current = normal;
            if (target != null) target.color = normal;
        }

        public void OnPointerEnter(PointerEventData e) { hovering = true; }
        public void OnPointerExit(PointerEventData e) { hovering = false; pressing = false; }
        public void OnPointerDown(PointerEventData e) { pressing = true; }
        public void OnPointerUp(PointerEventData e) { pressing = false; }

        void OnDisable()
        {
            hovering = pressing = false;
            if (initialized) { transform.localScale = baseScale; scale = 1f; }
        }

        void Update()
        {
            Init();
            bool enabledNow = selectable != null && selectable.IsInteractable();
            float targetScale = !enabledNow ? 1f : pressing ? pressedScale : hovering ? hoverScale : 1f;
            Color targetColor = !enabledNow ? disabled : pressing ? pressed : hovering ? hover : normal;
            float k = 1f - Mathf.Exp(-18f * Time.unscaledDeltaTime);
            scale = Mathf.Lerp(scale, targetScale, k);
            current = Color.Lerp(current, targetColor, k);
            transform.localScale = baseScale * scale;
            if (target != null) target.color = current;
            if (textTarget != null) textTarget.color = Color.Lerp(textTarget.color, enabledNow ? textNormal : textDisabled, k);
            if (wiggle != null)
            {
                float angle = enabledNow && hovering ? Mathf.Sin(Time.unscaledTime * 11f) * 7f : 0f;
                wiggle.localRotation = Quaternion.Lerp(wiggle.localRotation, Quaternion.Euler(0f, 0f, angle), k);
            }
        }
    }
}
