using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using RobaColores.Game.Core;
using static RobaColores.Game.Presentation.MenuWidgets;

namespace RobaColores.Game.Presentation
{
    // Pantalla de seleccion: un panel por jugador (3 x 2 para seis) con retrato, flechas, estado y confirmacion.
    // Toda la validacion vive en CharacterSelection (nucleo); esta vista solo la muestra y le envia ordenes.
    public sealed class CharacterSelectionView : MonoBehaviour
    {
        sealed class SlotUi
        {
            public Image frame, chip, portraitBack, portrait;
            public Text title, chipText, mark, nameText;
            public Button prev, next, confirm;
            public Image[] swatchBorders, swatchFills;
            public Button[] swatchButtons;
        }

        GameContent content;
        CharacterSelection model;
        SlotUi[] slots;
        Text status;
        Button startButton;

        public Button StartButton { get { return startButton; } }
        public Text StatusText { get { return status; } }

        public static CharacterSelectionView Create(RectTransform parent, GameContent content, CharacterSelection model, Action onStart, Action onBack)
        {
            var go = new GameObject("SeleccionPersonajes", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            UiKit.Stretch((RectTransform)go.transform);
            var view = go.AddComponent<CharacterSelectionView>();
            view.Build((RectTransform)go.transform, content, model, onStart, onBack);
            return view;
        }

        void Build(RectTransform root, GameContent gameContent, CharacterSelection selection, Action onStart, Action onBack)
        {
            content = gameContent; model = selection;

            var title = UiKit.Label(root, "Selección de personajes", 88, TextAnchor.MiddleCenter, UiKit.Ink);
            Abs(title.rectTransform, 100, 945, 1193, 120);
            var hint = UiKit.Label(root, "Cada jugador elige su personaje y pulsa CONFIRMAR. Con los " + SpanishCount(model.SlotCount) + " confirmados se activa COMENZAR.", 30, TextAnchor.MiddleCenter, UiKit.Wood);
            Abs(hint.rectTransform, 100, 898, 1193, 46);

            int cols = 3, rows = Mathf.CeilToInt(model.SlotCount / (float)cols);
            const float pw = 400f, ph = 330f, gapX = 28f, gapY = 18f;
            float totalW = cols * pw + (cols - 1) * gapX;
            float left = (1393f - totalW) / 2f;
            float top = 890f;
            slots = new SlotUi[model.SlotCount];
            for (int i = 0; i < model.SlotCount; i++)
            {
                int col = i % cols, row = i / cols;
                BuildSlot(root, i, left + col * (pw + gapX), top - ph - row * (ph + gapY), pw, ph);
            }

            var back = FrameButton(root, "VOLVER", 42, onBack, UiKit.ParchmentDark);
            Abs((RectTransform)back.transform, 60, 52, 260, 76);
            status = UiKit.Label(root, "", 34, TextAnchor.MiddleCenter, UiKit.Ink);
            Abs(status.rectTransform, 340, 52, 700, 76);
            startButton = FrameButton(root, "COMENZAR", 52, onStart, new Color32(0xF2, 0xC2, 0x30, 255));
            Abs((RectTransform)startButton.transform, 1050, 40, 290, 96);

            Refresh();
        }

        static string SpanishCount(int n)
        {
            switch (n) { case 2: return "dos"; case 3: return "tres"; case 4: return "cuatro"; case 5: return "cinco"; case 6: return "seis"; default: return n.ToString(); }
        }

        void BuildSlot(RectTransform parent, int index, float x, float y, float w, float h)
        {
            var ui = new SlotUi();
            slots[index] = ui;
            var root = UiKit.Rect(parent, "Jugador" + (index + 1));
            Abs(root, x, y, w, h);

            ui.frame = FramePanel(root, UiKit.Parchment);
            UiKit.Stretch(ui.frame.rectTransform);

            ui.title = UiKit.Label(root, "", 32, TextAnchor.MiddleLeft, UiKit.Ink);
            Abs(ui.title.rectTransform, 24, 284, 200, 40);
            ui.chip = FramePanel(root, UiKit.ParchmentDark);
            Abs(ui.chip.rectTransform, 214, 288, 166, 34);
            ui.chipText = UiKit.Label(ui.chip.transform, "", 21, TextAnchor.MiddleCenter, UiKit.Ink);
            UiKit.Stretch(ui.chipText.rectTransform);

            ui.portraitBack = UiKit.Panel(root, "FondoRetrato", UiKit.ParchmentDark);
            Abs(ui.portraitBack.rectTransform, 125, 130, 150, 150);
            var pImg = UiKit.Panel(ui.portraitBack.transform, "Retrato", Color.white);
            UiKit.Place(pImg.rectTransform, 0, 0, 1, 1, 6, 6, -6, -6);
            pImg.preserveAspect = true;
            pImg.raycastTarget = false;
            ui.portrait = pImg;
            ui.mark = UiKit.Label(ui.portraitBack.transform, "?", 110, TextAnchor.MiddleCenter, UiKit.Ink);
            UiKit.Stretch(ui.mark.rectTransform);

            int slot = index;
            ui.prev = FrameButton(root, "<", 52, () => { model.Cycle(slot, -1); Refresh(); });
            Abs((RectTransform)ui.prev.transform, 24, 168, 64, 76);
            ui.next = FrameButton(root, ">", 52, () => { model.Cycle(slot, 1); Refresh(); });
            Abs((RectTransform)ui.next.transform, 312, 168, 64, 76);

            ui.nameText = UiKit.Label(root, "", 36, TextAnchor.MiddleCenter, UiKit.Ink);
            Abs(ui.nameText.rectTransform, 20, 90, 360, 40);

            // Un cuadro de color por personaje: elegir directamente, y ver cuales estan ocupados.
            int count = content.characters.Count;
            float step = Mathf.Min(40f, 340f / Mathf.Max(1, count));
            float swW = step - 8f;
            float sx = (w - (count * step - 8f)) / 2f;
            ui.swatchBorders = new Image[count]; ui.swatchFills = new Image[count]; ui.swatchButtons = new Button[count];
            for (int j = 0; j < count; j++)
            {
                int character = j;
                var border = UiKit.Panel(root, "Ficha" + j, UiKit.Ink);
                Abs(border.rectTransform, sx + j * step, 60, swW, swW);
                var fill = UiKit.Panel(border.transform, "Color", content.characters[j].mainColor);
                UiKit.Place(fill.rectTransform, 0, 0, 1, 1, 3, 3, -3, -3);
                fill.raycastTarget = false;
                var btn = border.gameObject.AddComponent<Button>();
                btn.transition = Selectable.Transition.None;
                btn.targetGraphic = border;
                btn.onClick.AddListener(() => { model.Select(slot, character); Refresh(); });
                ui.swatchBorders[j] = border; ui.swatchFills[j] = fill; ui.swatchButtons[j] = btn;
            }

            ui.confirm = FrameButton(root, "CONFIRMAR", 32, () =>
            {
                if (model.Slot(slot).State == SlotState.Confirmed) model.Unconfirm(slot); else model.Confirm(slot);
                Refresh();
            }, new Color32(0xF2, 0xC2, 0x30, 255));
            Abs((RectTransform)ui.confirm.transform, 70, 8, 260, 46);
        }

        // Vuelve a pintar todo desde el modelo: la interfaz nunca guarda su propio estado.
        public void Refresh()
        {
            for (int i = 0; i < slots.Length; i++)
            {
                var ui = slots[i];
                var s = model.Slot(i);
                bool has = s.CharacterId >= 0;
                var ch = has ? content.characters[s.CharacterId] : null;
                bool confirmed = s.State == SlotState.Confirmed;

                ui.title.text = "JUGADOR " + s.Number;
                ui.frame.color = confirmed ? new Color(0.80f, 0.92f, 0.72f) : UiKit.Parchment;
                switch (s.State)
                {
                    case SlotState.Pending: ui.chipText.text = "PENDIENTE"; ui.chip.color = new Color(0.72f, 0.68f, 0.58f); break;
                    case SlotState.Selected: ui.chipText.text = "SELECCIONADO"; ui.chip.color = new Color32(0xF2, 0xC2, 0x30, 255); break;
                    default: ui.chipText.text = "CONFIRMADO"; ui.chip.color = new Color32(0x6F, 0xB5, 0x5E, 255); break;
                }

                ui.portraitBack.color = has ? Color.Lerp(ch.mainColor, UiKit.Parchment, 0.45f) : UiKit.ParchmentDark;
                if (has && ch.portrait != null) { ui.portrait.sprite = ch.portrait; ui.portrait.enabled = true; ui.mark.enabled = false; }
                else
                {
                    ui.portrait.enabled = false;
                    ui.mark.enabled = true;
                    ui.mark.text = has ? (string.IsNullOrEmpty(ch.displayName) ? "?" : ch.displayName.Substring(0, 1)) : "?";
                }
                ui.nameText.text = has ? ch.displayName : "Elige un personaje";

                ui.prev.interactable = !confirmed;
                ui.next.interactable = !confirmed;
                for (int j = 0; j < ui.swatchButtons.Length; j++)
                {
                    bool available = model.IsAvailable(i, j);
                    var fill = content.characters[j].mainColor;
                    fill.a = available ? 1f : 0.22f;
                    ui.swatchFills[j].color = fill;
                    ui.swatchBorders[j].color = j == s.CharacterId ? new Color32(0xF2, 0xC2, 0x30, 255) : UiKit.Ink;
                    ui.swatchButtons[j].interactable = !confirmed && available;
                }

                UiKit.SetLabel(ui.confirm, confirmed ? "CAMBIAR" : "CONFIRMAR");
                ui.confirm.interactable = confirmed || s.State == SlotState.Selected;
            }

            var problems = model.Validate();
            bool canStart = problems.Count == 0;
            startButton.interactable = canStart;
            if (canStart)
            {
                status.text = "Todos listos";
                status.color = new Color32(0x2F, 0x6E, 0x2A, 255);
            }
            else
            {
                var missing = model.Missing();
                if (missing.Count > 0)
                {
                    var names = new List<string>();
                    foreach (var m in missing) names.Add(model.Slot(m).Number.ToString());
                    status.text = "Falta confirmar: jugador " + string.Join(", ", names.ToArray());
                }
                else status.text = problems[0];
                status.color = new Color32(0x9C, 0x2B, 0x2B, 255);
            }
        }
    }
}
