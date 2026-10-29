using System;
using UnityEngine;
using UnityEngine.UI;

namespace RobaColores.Game.Presentation
{
    // Piezas de interfaz del menu: botones con marco de doble linea y estados visuales, y paneles enmarcados.
    public static class MenuWidgets
    {
        // Coloca un rectangulo con coordenadas absolutas de diseno (origen abajo a la izquierda del padre).
        public static RectTransform Abs(RectTransform rt, float x, float y, float w, float h)
        {
            return UiKit.Place(rt, 0, 0, 0, 0, x, y, x + w, y + h);
        }

        public static Image FramePanel(Transform parent, Color tint)
        {
            var img = UiKit.Panel(parent, "Marco", tint);
            img.sprite = MenuSprites.Frame();
            img.type = Image.Type.Sliced;
            return img;
        }

        // Boton con marco, texto y estados normal / cursor encima / pulsado / deshabilitado.
        public static Button FrameButton(Transform parent, string text, int fontSize, Action onClick, Color? fill = null)
        {
            Color baseColor = fill ?? UiKit.Parchment;
            var img = FramePanel(parent, baseColor);
            img.name = "Boton";
            var button = img.gameObject.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.targetGraphic = img;
            if (onClick != null) button.onClick.AddListener(() => onClick());

            var label = UiKit.Label(img.transform, text, fontSize, TextAnchor.MiddleCenter, UiKit.Ink);
            UiKit.Stretch(label.rectTransform);

            var fb = img.gameObject.AddComponent<ButtonFeedback>();
            fb.target = img;
            fb.normal = baseColor;
            fb.hover = Color.Lerp(baseColor, Color.white, 0.45f);
            fb.pressed = baseColor * 0.82f;
            fb.pressed.a = 1f;
            fb.disabled = new Color(0.66f, 0.62f, 0.55f, 0.9f);
            fb.textTarget = label;
            fb.textNormal = UiKit.Ink;
            return button;
        }

        // Aspa (X) dibujada con dos barras: icono de SALIDA.
        public static void DrawCross(RectTransform parent, Vector2 center, float size, float thickness, Color color)
        {
            for (int i = 0; i < 2; i++)
            {
                var bar = UiKit.Panel(parent, "Aspa", color);
                var rt = bar.rectTransform;
                rt.anchorMin = rt.anchorMax = new Vector2(0f, 0f);
                rt.sizeDelta = new Vector2(size, thickness);
                rt.anchoredPosition = center;
                rt.localRotation = Quaternion.Euler(0f, 0f, i == 0 ? 45f : -45f);
                bar.raycastTarget = false;
            }
        }
    }
}
