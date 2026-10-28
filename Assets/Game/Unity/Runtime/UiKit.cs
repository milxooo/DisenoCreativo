using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace RobaColores.Game.Presentation
{
    // Utilidades para construir interfaz uGUI por codigo (sin depender de objetos serializados en la escena).
    public static class UiKit
    {
        public static Font Font;

        public static readonly Color Parchment = new Color32(0xEB, 0xDD, 0xB5, 255);
        public static readonly Color ParchmentDark = new Color32(0xD2, 0xBE, 0x8C, 255);
        public static readonly Color Ink = new Color32(0x2A, 0x1C, 0x10, 255);
        public static readonly Color Wood = new Color32(0x6B, 0x45, 0x22, 255);
        public static readonly Color WoodLight = new Color32(0x8B, 0x5A, 0x2B, 255);
        public static readonly Color Gold = new Color32(0xF2, 0xC2, 0x30, 255);
        public static readonly Color Dim = new Color(0.08f, 0.05f, 0.03f, 0.82f);

        public static Canvas CreateCanvas(string name, int sortingOrder)
        {
            var go = new GameObject(name);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            go.AddComponent<GraphicRaycaster>();
            return canvas;
        }

        public static void EnsureEventSystem()
        {
            if (UnityEngine.Object.FindFirstObjectByType<EventSystem>() != null) return;
            var go = new GameObject("EventSystem");
            go.AddComponent<EventSystem>();
            var module = go.AddComponent<InputSystemUIInputModule>();
            module.AssignDefaultActions();
        }

        // Coloca un RectTransform con anclas (0..1) y desplazamientos en pixeles de referencia.
        public static RectTransform Place(RectTransform rt, float ax0, float ay0, float ax1, float ay1, float l, float b, float r, float t)
        {
            rt.anchorMin = new Vector2(ax0, ay0);
            rt.anchorMax = new Vector2(ax1, ay1);
            rt.offsetMin = new Vector2(l, b);
            rt.offsetMax = new Vector2(r, t);
            return rt;
        }

        public static RectTransform Stretch(RectTransform rt) { return Place(rt, 0, 0, 1, 1, 0, 0, 0, 0); }

        public static RectTransform Rect(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        public static Image Panel(Transform parent, string name, Color color)
        {
            var rt = Rect(parent, name);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = color;
            return img;
        }

        public static Text Label(Transform parent, string text, int size, TextAnchor anchor, Color color)
        {
            var rt = Rect(parent, "Texto");
            var t = rt.gameObject.AddComponent<Text>();
            t.font = Font;
            t.text = text;
            t.fontSize = size;
            t.alignment = anchor;
            t.color = color;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            return t;
        }

        public static Button MakeButton(Transform parent, string text, Color bg, int fontSize, Action onClick)
        {
            var img = Panel(parent, "Boton", bg);
            var button = img.gameObject.AddComponent<Button>();
            var colors = button.colors;
            colors.highlightedColor = new Color(1.15f, 1.15f, 1.15f, 1f);
            colors.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
            colors.disabledColor = new Color(0.6f, 0.6f, 0.6f, 0.6f);
            button.colors = colors;
            button.targetGraphic = img;
            if (onClick != null) button.onClick.AddListener(() => onClick());
            var label = Label(img.transform, text, fontSize, TextAnchor.MiddleCenter, Parchment);
            Stretch(label.rectTransform);
            return button;
        }

        public static void SetLabel(Button b, string text)
        {
            var t = b.GetComponentInChildren<Text>();
            if (t != null) t.text = text;
        }

        public static void Clear(Transform t)
        {
            for (int i = t.childCount - 1; i >= 0; i--) UnityEngine.Object.Destroy(t.GetChild(i).gameObject);
        }
    }
}
