using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using RobaColores.Game.Core;
using static RobaColores.Game.Presentation.MenuWidgets;

namespace RobaColores.Game.Presentation
{
    // Pantalla de selección: usa el arte de referencia si existe y conserva una vista generica como alternativa.
    // Toda la validacion vive en CharacterSelection (nucleo); esta vista solo la muestra y le envia ordenes.
    public sealed class CharacterSelectionView : MonoBehaviour
    {
        sealed class SlotUi
        {
            public Image frame, chip, portraitBack, portrait;
            public RawImage artwork;
            public Text title, chipText, mark, nameText;
            public Button prev, next, confirm;
            public Image[] swatchBorders, swatchFills;
            public Button[] swatchButtons;
        }

        GameContent content;
        CharacterSelection model;
        SlotUi[] slots;
        RectTransform slotsRoot;
        Text playerCount;
        Text status;
        Button startButton;
        Button fewerPlayers, morePlayers;
        Material artworkMaterial;
        bool referenceLayout;
        int activeSlot = -1;
        Button[] referenceCharacterButtons;
        Image[] referenceCharacterPlates;
        Text[] referenceCharacterLabels;
        Button referenceConfirmButton;
        Button referenceStartButton;
        Button referenceFewerPlayers;
        Button referenceMorePlayers;
        Text referencePlayerCount;
        Text referenceCurrentPlayer;

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
            if (content.menuArt != null && content.menuArt.playerSelectionBackground != null)
            {
                BuildReferenceLayout(root, onStart, onBack);
                RefreshReferenceLayout();
                return;
            }
            artworkMaterial = CharacterPortraitLibrary.GetArtworkMaterial();

            var theater = UiKit.Panel(root, "EscenarioPirata", Color.white);
            theater.sprite = MenuSprites.Theater();
            theater.raycastTarget = false;
            UiKit.Stretch(theater.rectTransform);
            theater.transform.SetAsFirstSibling();

            var titleShadow = UiKit.Label(root, "ESCOGE TU TRIPULACIÓN", 58, TextAnchor.MiddleLeft, new Color32(28, 17, 18, 255));
            Abs(titleShadow.rectTransform, 74, 962, 940, 70);
            var title = UiKit.Label(root, "ESCOGE TU TRIPULACIÓN", 58, TextAnchor.MiddleLeft, UiKit.Gold);
            Abs(title.rectTransform, 70, 966, 940, 70);
            var hint = UiKit.Label(root, "Elijan entre dos y seis jugadores. Cada quien elige y confirma su personaje.", 25, TextAnchor.MiddleLeft, UiKit.Parchment);
            Abs(hint.rectTransform, 76, 929, 900, 34);

            var countPanel = FramePanel(root, new Color32(75, 43, 29, 255));
            Abs(countPanel.rectTransform, 1010, 922, 328, 72);
            var countLabel = UiKit.Label(countPanel.transform, "JUGADORES", 20, TextAnchor.MiddleCenter, UiKit.Parchment);
            Abs(countLabel.rectTransform, 8, 41, 136, 25);
            fewerPlayers = FrameButton(root, "-", 30, () => ChangePlayerCount(-1));
            Abs((RectTransform)fewerPlayers.transform, 1158, 929, 48, 48);
            playerCount = UiKit.Label(root, "", 28, TextAnchor.MiddleCenter, UiKit.Parchment);
            Abs(playerCount.rectTransform, 1207, 929, 55, 48);
            morePlayers = FrameButton(root, "+", 30, () => ChangePlayerCount(1));
            Abs((RectTransform)morePlayers.transform, 1264, 929, 48, 48);

            slotsRoot = UiKit.Rect(root, "EspaciosJugadores");
            UiKit.Stretch(slotsRoot);

            var back = FrameButton(root, "VOLVER", 34, onBack, new Color32(90, 50, 34, 255));
            Abs((RectTransform)back.transform, 44, 40, 230, 72);
            status = UiKit.Label(root, "", 26, TextAnchor.MiddleCenter, UiKit.Parchment);
            Abs(status.rectTransform, 320, 38, 750, 72);
            startButton = FrameButton(root, "¡A JUGAR!", 38, onStart, new Color32(0xD6, 0xA2, 0x39, 255));
            Abs((RectTransform)startButton.transform, 1112, 36, 245, 76);

            RebuildSlots();
        }

        void BuildReferenceLayout(RectTransform root, Action onStart, Action onBack)
        {
            referenceLayout = true;
            var backgroundObject = new GameObject("DisenoSeleccionJugadores", typeof(RectTransform), typeof(RawImage));
            backgroundObject.transform.SetParent(root, false);
            var background = backgroundObject.GetComponent<RawImage>();
            background.texture = content.menuArt.playerSelectionBackground;
            background.raycastTarget = false;
            Abs(background.rectTransform, 0, 0, 1920, 1080);
            background.transform.SetAsFirstSibling();

            AddArtworkMask(root, "LimpiarEtiquetaJugador", 510, 645, 202, 48);
            AddArtworkMask(root, "LimpiarFlechaSeleccion", 554, 520, 116, 140);
            AddArtworkMask(root, "LimpiarJugadorImpreso", 805, 106, 315, 40, new Color32(155, 11, 37, 255));

            var countPanel = UiKit.Panel(root, "ControlCantidadJugadores", new Color(0.09f, 0.035f, 0.06f, 0.88f));
            Abs(countPanel.rectTransform, 1450, 830, 420, 145);
            var countLabel = UiKit.Label(countPanel.transform, "JUGADORES", 24, TextAnchor.MiddleLeft, UiKit.Parchment);
            Abs(countLabel.rectTransform, 18, 92, 175, 42);
            referenceCurrentPlayer = UiKit.Label(countPanel.transform, "", 21, TextAnchor.MiddleLeft, UiKit.Gold);
            Abs(referenceCurrentPlayer.rectTransform, 18, 43, 380, 40);

            referenceFewerPlayers = FrameButton(countPanel.transform, "−", 30, () => ChangePlayerCount(-1));
            Abs((RectTransform)referenceFewerPlayers.transform, 200, 89, 55, 48);
            referencePlayerCount = UiKit.Label(countPanel.transform, "", 30, TextAnchor.MiddleCenter, UiKit.Parchment);
            Abs(referencePlayerCount.rectTransform, 258, 89, 58, 48);
            referenceMorePlayers = FrameButton(countPanel.transform, "+", 30, () => ChangePlayerCount(1));
            Abs((RectTransform)referenceMorePlayers.transform, 320, 89, 55, 48);

            float[] centers = { 415f, 624f, 849f, 1115f, 1354f, 1576f };
            int characterCount = content.characters.Count;
            referenceCharacterButtons = new Button[characterCount];
            referenceCharacterPlates = new Image[characterCount];
            referenceCharacterLabels = new Text[characterCount];
            for (int i = 0; i < characterCount; i++)
            {
                float center = centers[Mathf.Min(i, centers.Length - 1)];
                int character = i;
                var hitArea = UiKit.Panel(root, "ElegirPersonaje" + (i + 1), new Color(1f, 1f, 1f, 0f));
                var choose = hitArea.gameObject.AddComponent<Button>();
                choose.transition = Selectable.Transition.None;
                choose.targetGraphic = hitArea;
                choose.onClick.AddListener(() => SelectReferenceCharacter(character));
                Abs(hitArea.rectTransform, center - 103f, 195f, 206f, 450f);
                referenceCharacterButtons[i] = choose;

                var plate = UiKit.Panel(root, "NombrePersonaje" + (i + 1), new Color(0.08f, 0.035f, 0.055f, 0.88f));
                plate.raycastTarget = false;
                Abs(plate.rectTransform, center - 101f, 145f, 202f, 42f);
                referenceCharacterPlates[i] = plate;
                var name = UiKit.Label(plate.transform, content.characters[i].displayName, 19, TextAnchor.MiddleCenter, UiKit.Parchment);
                UiKit.Stretch(name.rectTransform);
                referenceCharacterLabels[i] = name;
            }

            var back = FrameButton(root, "VOLVER", 29, onBack, new Color32(90, 50, 34, 255));
            Abs((RectTransform)back.transform, 46, 34, 245, 76);
            referenceConfirmButton = FrameButton(root, "ELIGE UN PERSONAJE", 27, ConfirmReferencePlayer,
                new Color32(0xD6, 0xA2, 0x39, 255));
            Abs((RectTransform)referenceConfirmButton.transform, 690, 34, 500, 76);
            referenceStartButton = FrameButton(root, "¡A JUGAR!", 31, onStart, new Color32(0xD6, 0xA2, 0x39, 255));
            Abs((RectTransform)referenceStartButton.transform, 1510, 34, 350, 76);
            startButton = referenceStartButton;
            fewerPlayers = referenceFewerPlayers;
            morePlayers = referenceMorePlayers;
            status = referenceCurrentPlayer;
        }

        static void AddArtworkMask(Transform parent, string name, float x, float y, float width, float height, Color? tint = null)
        {
            var mask = UiKit.Panel(parent, name, tint ?? new Color32(2, 10, 15, 255));
            mask.raycastTarget = false;
            Abs(mask.rectTransform, x, y, width, height);
        }

        void SelectReferenceCharacter(int character)
        {
            int slot = FindActiveSlot();
            if (slot < 0)
            {
                // Todos confirmados: el retrato pulsado se convierte en el jugador que se quiere cambiar.
                int confirmedOwner = OwnerSlot(character);
                if (confirmedOwner < 0) return;
                model.Unconfirm(confirmedOwner);
                activeSlot = confirmedOwner;
                RefreshReferenceLayout();
                return;
            }

            // Si el personaje pertenece a otro jugador, se libera para poder intercambiarlo.
            int owner = OwnerSlot(character);
            if (owner >= 0 && owner != slot)
            {
                model.Unconfirm(owner);
                model.Clear(owner);
            }
            activeSlot = slot;
            model.Select(slot, character);
            RefreshReferenceLayout();
        }

        void ConfirmReferencePlayer()
        {
            int slot = FindActiveSlot();
            if (slot < 0 || !model.Confirm(slot)) return;
            activeSlot = -1;
            RefreshReferenceLayout();
        }

        int OwnerSlot(int character)
        {
            for (int i = 0; i < model.SlotCount; i++)
                if (model.Slot(i).CharacterId == character) return i;
            return -1;
        }

        int FindActiveSlot()
        {
            if (activeSlot >= 0 && activeSlot < model.SlotCount && model.Slot(activeSlot).State != SlotState.Confirmed)
                return activeSlot;
            for (int i = 0; i < model.SlotCount; i++)
                if (model.Slot(i).State != SlotState.Confirmed) return i;
            return -1;
        }

        void RefreshReferenceLayout()
        {
            int activeSlot = FindActiveSlot();
            referencePlayerCount.text = model.SlotCount.ToString();
            referenceFewerPlayers.interactable = model.SlotCount > content.settings.minPlayers;
            referenceMorePlayers.interactable = model.SlotCount < content.settings.maxPlayers;
            referenceStartButton.interactable = model.CanStart;
            referenceConfirmButton.interactable = activeSlot >= 0 &&
                model.Slot(activeSlot).State == SlotState.Selected;
            referenceConfirmButton.GetComponentInChildren<Text>().text = activeSlot < 0
                ? "TODOS CONFIRMADOS"
                : model.Slot(activeSlot).State == SlotState.Selected
                    ? "CONFIRMAR JUGADOR " + model.Slot(activeSlot).Number
                    : "ELIGE UN PERSONAJE";
            referenceCurrentPlayer.text = activeSlot < 0
                ? "TRIPULACIÓN LISTA"
                : "TURNO: JUGADOR " + model.Slot(activeSlot).Number;

            for (int i = 0; i < referenceCharacterButtons.Length; i++)
            {
                bool available = activeSlot >= 0 && model.IsAvailable(activeSlot, i);
                // Con un jugador activo se puede elegir un personaje libre o quitarle el suyo a
                // otro jugador; si todos estan confirmados, pulsar un retrato lo pone en edicion.
                referenceCharacterButtons[i].interactable = OwnerSlot(i) >= 0 || available;
                bool selected = false;
                bool confirmed = false;
                for (int slot = 0; slot < model.SlotCount; slot++)
                {
                    if (model.Slot(slot).CharacterId != i) continue;
                    selected = true;
                    confirmed = model.Slot(slot).State == SlotState.Confirmed;
                    break;
                }
                referenceCharacterPlates[i].color = confirmed
                    ? new Color(0.12f, 0.36f, 0.18f, 0.96f)
                    : selected
                        ? new Color(0.68f, 0.39f, 0.08f, 0.96f)
                        : new Color(0.08f, 0.035f, 0.055f, available ? 0.88f : 0.5f);
                referenceCharacterLabels[i].color = available || selected
                    ? UiKit.Parchment
                    : new Color(0.55f, 0.52f, 0.49f, 1f);
            }
        }

        void ChangePlayerCount(int delta)
        {
            int count = model.SlotCount + delta;
            if (count < content.settings.minPlayers || count > content.settings.maxPlayers) return;
            activeSlot = -1;
            model.SetSlotCount(count);
            if (referenceLayout)
            {
                RefreshReferenceLayout();
                return;
            }
            RebuildSlots();
        }

        void RebuildSlots()
        {
            UiKit.Clear(slotsRoot);
            int count = model.SlotCount;
            const float margin = 52f, gap = 18f, height = 650f;
            float width = (1393f - margin * 2f - gap * (count - 1)) / count;
            float left = margin;
            slots = new SlotUi[count];
            for (int i = 0; i < count; i++)
            {
                BuildSlot(slotsRoot, i, left + i * (width + gap), 182f, width, height);
            }
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

            ui.frame = FramePanel(root, new Color32(25, 30, 45, 244));
            UiKit.Stretch(ui.frame.rectTransform);

            var header = UiKit.Panel(root, "BanderaJugador", UiKit.Wood);
            UiKit.Place(header.rectTransform, 0, 1, 1, 1, 10, -60, -10, -10);
            ui.title = UiKit.Label(header.transform, "", Mathf.Clamp(Mathf.RoundToInt(w * 0.075f), 21, 32), TextAnchor.MiddleCenter, Color.white);
            UiKit.Stretch(ui.title.rectTransform);
            ui.chip = FramePanel(root, new Color32(67, 45, 39, 255));
            Abs(ui.chip.rectTransform, (w - Mathf.Min(175f, w - 32f)) * 0.5f, h - 108f, Mathf.Min(175f, w - 32f), 36f);
            ui.chipText = UiKit.Label(ui.chip.transform, "", 18, TextAnchor.MiddleCenter, UiKit.Parchment);
            UiKit.Stretch(ui.chipText.rectTransform);

            float artHeight = Mathf.Min(350f, h - 320f);
            float artWidth = Mathf.Min(w - 76f, artHeight * 0.72f);
            float artX = (w - artWidth) * 0.5f;
            ui.portraitBack = UiKit.Panel(root, "ResplandorPersonaje", new Color(1f, 1f, 1f, 0.22f));
            ui.portraitBack.sprite = MenuSprites.Circle();
            ui.portraitBack.raycastTarget = false;
            Abs(ui.portraitBack.rectTransform, artX, 194f, artWidth, artHeight);
            var pImg = UiKit.Panel(ui.portraitBack.transform, "Retrato", Color.white);
            UiKit.Stretch(pImg.rectTransform);
            pImg.preserveAspect = true;
            pImg.raycastTarget = false;
            ui.portrait = pImg;
            var artworkObject = new GameObject("ArteIlustrado", typeof(RectTransform), typeof(RawImage));
            artworkObject.transform.SetParent(ui.portraitBack.transform, false);
            ui.artwork = artworkObject.GetComponent<RawImage>();
            UiKit.Place(ui.artwork.rectTransform, 0, 0, 1, 1, 4, 4, -4, -4);
            ui.artwork.material = artworkMaterial;
            ui.artwork.uvRect = CharacterPortraitLibrary.GetArtworkUvRect(
                CharacterPortraitLibrary.GetCharacterArtwork(0), artWidth / artHeight);
            ui.artwork.raycastTarget = false;
            ui.mark = UiKit.Label(ui.portraitBack.transform, "?", 110, TextAnchor.MiddleCenter, UiKit.Ink);
            UiKit.Stretch(ui.mark.rectTransform);

            int slot = index;
            ui.prev = FrameButton(root, "‹", 44, () => { model.Cycle(slot, -1); Refresh(); }, new Color32(92, 54, 35, 255));
            Abs((RectTransform)ui.prev.transform, 10, 320, 48, 58);
            ui.next = FrameButton(root, "›", 44, () => { model.Cycle(slot, 1); Refresh(); }, new Color32(92, 54, 35, 255));
            Abs((RectTransform)ui.next.transform, w - 58, 320, 48, 58);

            var plank = UiKit.Panel(root, "Plataforma", new Color32(109, 65, 37, 255));
            Abs(plank.rectTransform, 24, 176, w - 48, 14);
            var plankEdge = UiKit.Panel(root, "BordePlataforma", new Color32(205, 157, 76, 255));
            Abs(plankEdge.rectTransform, 24, 174, w - 48, 3);
            ui.nameText = UiKit.Label(root, "", Mathf.Clamp(Mathf.RoundToInt(w * 0.085f), 21, 36), TextAnchor.MiddleCenter, UiKit.Parchment);
            Abs(ui.nameText.rectTransform, 16, 132, w - 32, 38);

            // Un cuadro de color por personaje: elegir directamente, y ver cuales estan ocupados.
            int count = content.characters.Count;
            float step = Mathf.Min(36f, (w - 24f) / Mathf.Max(1, count));
            float swW = Mathf.Max(20f, step - 5f);
            float sx = (w - (count * step - 5f)) / 2f;
            ui.swatchBorders = new Image[count]; ui.swatchFills = new Image[count]; ui.swatchButtons = new Button[count];
            for (int j = 0; j < count; j++)
            {
                int character = j;
                var border = UiKit.Panel(root, "Ficha" + j, UiKit.Ink);
                border.sprite = MenuSprites.Frame();
                border.type = Image.Type.Sliced;
                Abs(border.rectTransform, sx + j * step, 76, swW, swW);
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
            Abs((RectTransform)ui.confirm.transform, 24, 14, w - 48, 48);
        }

        // Vuelve a pintar todo desde el modelo: la interfaz nunca guarda su propio estado.
        public void Refresh()
        {
            if (referenceLayout)
            {
                RefreshReferenceLayout();
                return;
            }
            playerCount.text = model.SlotCount.ToString();
            fewerPlayers.interactable = model.SlotCount > content.settings.minPlayers;
            morePlayers.interactable = model.SlotCount < content.settings.maxPlayers;
            for (int i = 0; i < slots.Length; i++)
            {
                var ui = slots[i];
                var s = model.Slot(i);
                bool has = s.CharacterId >= 0;
                var ch = has ? content.characters[s.CharacterId] : null;
                bool confirmed = s.State == SlotState.Confirmed;

                ui.title.text = "JUGADOR " + s.Number;
                ui.frame.color = confirmed ? new Color32(42, 69, 51, 248) : new Color32(25, 30, 45, 244);
                switch (s.State)
                {
                    case SlotState.Pending: ui.chipText.text = "PENDIENTE"; ui.chip.color = new Color(0.72f, 0.68f, 0.58f); break;
                    case SlotState.Selected: ui.chipText.text = "SELECCIONADO"; ui.chip.color = new Color32(0xD6, 0xA2, 0x39, 255); break;
                    default: ui.chipText.text = "CONFIRMADO"; ui.chip.color = new Color32(0x45, 0x87, 0x53, 255); break;
                }

                ui.portraitBack.color = has
                    ? new Color(ch.mainColor.r, ch.mainColor.g, ch.mainColor.b, 0.26f)
                    : new Color(0.7f, 0.6f, 0.4f, 0.1f);
                var art = has ? CharacterPortraitLibrary.GetCharacterArtwork(s.CharacterId) : null;
                ui.artwork.texture = art;
                bool showArtwork = art != null && artworkMaterial != null;
                ui.artwork.enabled = showArtwork;
                if (has && showArtwork) { ui.portrait.enabled = false; ui.mark.enabled = false; }
                else if (has && ch.portrait != null) { ui.portrait.sprite = ch.portrait; ui.portrait.enabled = true; ui.mark.enabled = false; }
                else
                {
                    ui.portrait.enabled = false;
                    ui.mark.enabled = true;
                    ui.mark.text = has ? (string.IsNullOrEmpty(ch.displayName) ? "?" : ch.displayName.Substring(0, 1)) : "?";
                }
                ui.nameText.text = has ? ch.displayName : "Elige un personaje";
                ui.nameText.color = has ? UiKit.Parchment : new Color32(191, 176, 142, 255);

                ui.prev.interactable = !confirmed;
                ui.next.interactable = !confirmed;
                for (int j = 0; j < ui.swatchButtons.Length; j++)
                {
                    bool available = model.IsAvailable(i, j);
                    var fill = content.characters[j].mainColor;
                    fill.a = available ? 1f : 0.22f;
                    ui.swatchFills[j].color = fill;
                    ui.swatchBorders[j].color = j == s.CharacterId ? UiKit.Gold : new Color32(20, 18, 20, 255);
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
                status.text = "Todos los jugadores listos";
                status.color = new Color32(0xB8, 0xDC, 0xA0, 255);
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
                status.color = new Color32(0xF0, 0xA0, 0x78, 255);
            }
        }
    }
}
