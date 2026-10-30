using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using RobaColores.Game.Core;
using static RobaColores.Game.Presentation.MenuWidgets;

namespace RobaColores.Game.Presentation
{
    // Pantalla inicial 16:9; las pantallas secundarias conservan su composicion original de 1393 x 1080.
    public sealed class MenuController : MonoBehaviour
    {
        [SerializeField] GameContent content;
        [Tooltip("Solo pruebas: pantalla con la que arranca (0 principal, 1 seleccion, 2 opciones, 3 personajes).")]
        [SerializeField] int debugStartScreen;
        [Tooltip("Solo pruebas: elige y confirma automaticamente a todos los jugadores.")]
        [SerializeField] bool debugAutoSelect;
        [Tooltip("Solo pruebas: pulsa COMENZAR automaticamente un par de segundos despues.")]
        [SerializeField] bool debugAutoStart;

        const float DesignW = 1920f, DesignH = 1080f;
        const float LegacyDesignW = 1393f;
        const string GameScene = "Partida";

        RectTransform stage;
        CanvasGroup mainGroup, selectionGroup, optionsGroup, galleryGroup, current;
        CharacterSelection selection;
        CharacterSelectionView selectionView;
        DiceShowcase mainDice;
        RectTransform titleArt;
        bool transitioning, starting;
        float time;

        // Elementos animados del menu principal
        RectTransform titleHolder;
        CanvasGroup titleCanvas, exitCanvas;
        readonly RectTransform[] buttonRects = new RectTransform[3];
        readonly CanvasGroup[] buttonCanvases = new CanvasGroup[3];
        RectTransform diceRect;
        bool usesReferenceMain;
        Button repeatButton;
        Text repeatValueText;

        void Awake()
        {
            if (content == null) { Debug.LogError("MenuController necesita el asset GameContent."); enabled = false; return; }
            if (content.settings == null || content.settings.minPlayers < 2 || content.settings.maxPlayers > 6 ||
                content.settings.minPlayers > content.settings.maxPlayers)
            {
                Debug.LogError("GameSettings debe permitir una cantidad de entre dos y seis jugadores.", this);
                enabled = false;
                return;
            }
            UiKit.Font = content.uiFont;
            UiKit.EnsureEventSystem();
            Time.timeScale = 1f;
            SessionSetup.Clear();

            var canvas = UiKit.CreateCanvas("MenuCanvas", 0, new Vector2(1920, 1080));
            canvas.GetComponent<CanvasScaler>().matchWidthOrHeight = 1f;
            var wood = UiKit.Panel(canvas.transform, "Fondo", new Color32(0x3B, 0x24, 0x10, 255));
            wood.sprite = MenuSprites.WoodWall();
            wood.type = Image.Type.Simple;
            UiKit.Stretch(wood.rectTransform);

            stage = UiKit.Rect(canvas.transform, "Escenario");
            stage.anchorMin = stage.anchorMax = new Vector2(0.5f, 0.5f);
            stage.sizeDelta = new Vector2(DesignW, DesignH);
            stage.anchoredPosition = Vector2.zero;
            stage.gameObject.AddComponent<StageFitter>();

            var legacyBackdrop = UiKit.Rect(stage, "FondoPantallasSecundarias");
            legacyBackdrop.anchorMin = legacyBackdrop.anchorMax = new Vector2(0.5f, 0.5f);
            legacyBackdrop.sizeDelta = new Vector2(LegacyDesignW, DesignH);
            var shadow = UiKit.Panel(legacyBackdrop, "Sombra", new Color(0f, 0f, 0f, 0.46f));
            shadow.sprite = MenuSprites.Parchment();
            shadow.raycastTarget = false;
            UiKit.Place(shadow.rectTransform, 0, 0, 1, 1, -4, -26, 24, 4);
            var paper = UiKit.Panel(legacyBackdrop, "Pergamino", Color.white);
            paper.sprite = MenuSprites.Parchment();
            paper.raycastTarget = false;
            UiKit.Stretch(paper.rectTransform);

            selection = new CharacterSelection(content.settings.minPlayers, content.characters.Count, content.settings.allowRepeatedCharacters);
            if (!content.settings.allowRepeatedCharacters && content.characters.Count < content.settings.maxPlayers)
                Debug.LogError("Hay menos personajes (" + content.characters.Count + ") que jugadores (" + content.settings.maxPlayers + ") y no se permite repetir: no se podra empezar.");

            BuildMain();
            BuildSelection();
            BuildOptions();
            BuildGallery();
        }

        void Start()
        {
            if (!enabled) return;
            if (debugAutoSelect) AutoSelectAll();
            switch (debugStartScreen)
            {
                case 1: ShowImmediately(selectionGroup); break;
                case 2: ShowImmediately(optionsGroup); break;
                case 3: ShowImmediately(galleryGroup); break;
                default: mainGroup.gameObject.SetActive(true); current = mainGroup; StartCoroutine(EnterMain()); break;
            }
            if (debugAutoStart) StartCoroutine(AutoStart());
        }

        void ShowImmediately(CanvasGroup group)
        {
            mainGroup.gameObject.SetActive(false);
            group.gameObject.SetActive(true);
            group.alpha = 1f;
            current = group;
        }

        void AutoSelectAll()
        {
            for (int i = 0; i < selection.SlotCount; i++) { selection.Cycle(i, 1); selection.Confirm(i); }
            if (selectionView != null) selectionView.Refresh();
        }

        IEnumerator AutoStart()
        {
            yield return new WaitForSecondsRealtime(2f);
            RequestStart();
        }

        // ---------------- Estructura ----------------

        RectTransform Group(string name, out CanvasGroup group, bool useLegacyLayout = false)
        {
            var rt = UiKit.Rect(stage, name);
            UiKit.Stretch(rt);
            group = rt.gameObject.AddComponent<CanvasGroup>();
            rt.gameObject.SetActive(false);
            if (!useLegacyLayout) return rt;

            var contentRoot = UiKit.Rect(rt, "Contenido");
            contentRoot.anchorMin = contentRoot.anchorMax = new Vector2(0.5f, 0.5f);
            contentRoot.sizeDelta = new Vector2(LegacyDesignW, DesignH);
            return contentRoot;
        }

        // Transicion entre pantallas: la actual se desvanece, la nueva aparece subiendo un poco. No bloquea la navegacion.
        void GoTo(CanvasGroup next, Action onShown = null)
        {
            if (transitioning || next == current || starting) return;
            StartCoroutine(Transition(next, onShown));
        }

        IEnumerator Transition(CanvasGroup next, Action onShown)
        {
            transitioning = true;
            var from = current;
            if (from != null)
            {
                from.interactable = false;
                yield return UiAnim.Fade(from, from.alpha, 0f, 0.16f);
                from.gameObject.SetActive(false);
            }
            next.gameObject.SetActive(true);
            next.alpha = 0f;
            next.interactable = true;
            var rt = (RectTransform)next.transform;
            yield return UiAnim.Run(0.26f, k =>
            {
                if (next == null) return;
                next.alpha = k;
                rt.anchoredPosition = new Vector2(0f, (1f - k) * -28f);
            }, UiAnim.OutCubic);
            rt.anchoredPosition = Vector2.zero;
            next.alpha = 1f;
            current = next;
            transitioning = false;
            if (onShown != null) onShown();
        }

        // ---------------- Principal ----------------

        void BuildMain()
        {
            var art = content.menuArt;
            bool useReferenceMain = art != null && art.mainMenuBackground != null;
            var g = Group("Principal", out mainGroup, !useReferenceMain);
            if (useReferenceMain)
            {
                usesReferenceMain = true;
                var backgroundObject = new GameObject("DisenoPantallaInicial", typeof(RectTransform), typeof(RawImage));
                backgroundObject.transform.SetParent(g, false);
                var background = backgroundObject.GetComponent<RawImage>();
                background.texture = art.mainMenuBackground;
                background.raycastTarget = false;
                Abs(background.rectTransform, 0, 0, DesignW, DesignH);

                AddArtworkButton(g, "Jugar", 1310, 718, 330, 90, () => GoTo(selectionGroup));
                AddArtworkButton(g, "Opciones", 1310, 615, 330, 90, () => GoTo(optionsGroup));
                AddArtworkButton(g, "Personalizar", 1310, 510, 330, 90, () => GoTo(galleryGroup));
                AddArtworkButton(g, "Salir", 315, 18, 280, 95, Quit);
                return;
            }

            titleHolder = UiKit.Rect(g, "TituloContenedor");
            UiKit.Stretch(titleHolder);
            titleCanvas = titleHolder.gameObject.AddComponent<CanvasGroup>();
            titleCanvas.blocksRaycasts = false;
            if (art != null && art.titleArt != null)
            {
                var img = UiKit.Panel(titleHolder, "Arte", Color.white);
                img.sprite = art.titleArt;
                img.preserveAspect = true;
                img.raycastTarget = false;
                img.rectTransform.pivot = new Vector2(0.5f, 0f);   // antes de colocar: cambiar el pivote despues desplaza el rectangulo
                Abs(img.rectTransform, 54, 300, 610, 700);
                titleArt = img.rectTransform;
            }
            else
            {
                var t = UiKit.Label(titleHolder, "ROBA\nCOLORES", 170, TextAnchor.MiddleLeft, UiKit.Ink);
                Abs(t.rectTransform, 70, 300, 900, 600);
            }

            var heroTexture = CharacterPortraitLibrary.GetCharacterArtwork(3);
            var heroGlow = UiKit.Panel(titleHolder, "ResplandorCapitan", new Color32(235, 182, 91, 48));
            heroGlow.sprite = MenuSprites.Circle();
            heroGlow.raycastTarget = false;
            Abs(heroGlow.rectTransform, 505, 120, 470, 730);
            if (heroTexture != null && CharacterPortraitLibrary.GetArtworkMaterial() != null)
            {
                var heroObject = new GameObject("CapitanDeLaTripulacion", typeof(RectTransform), typeof(RawImage));
                heroObject.transform.SetParent(titleHolder, false);
                var hero = heroObject.GetComponent<RawImage>();
                hero.texture = heroTexture;
                hero.material = CharacterPortraitLibrary.GetArtworkMaterial();
                hero.uvRect = CharacterPortraitLibrary.GetArtworkUvRect(heroTexture, 0.58f);
                hero.raycastTarget = false;
                Abs(hero.rectTransform, 495, 105, 500, 835);
            }

            string[] labels = { "JUGAR", "OPCIONES", "PERSONALIZAR" };
            float[] centerY = { 764f, 658f, 560f };
            Sprite[] icons = { art != null ? art.swordIcon : null, art != null ? art.compassIcon : null, art != null ? art.chestIcon : null };
            Action[] actions = { () => GoTo(selectionGroup), () => GoTo(optionsGroup), () => GoTo(galleryGroup) };
            for (int i = 0; i < 3; i++)
            {
                var holder = UiKit.Rect(g, "Entrada" + labels[i]);
                UiKit.Stretch(holder);
                var cg = holder.gameObject.AddComponent<CanvasGroup>();
                buttonRects[i] = holder;
                buttonCanvases[i] = cg;

                var button = FrameButton(holder, labels[i], 54, actions[i]);
                Abs((RectTransform)button.transform, 1065, centerY[i] - 37f, 280, 74);

                if (icons[i] != null)
                {
                    var icon = UiKit.Panel(holder, "Icono", Color.white);
                    icon.sprite = icons[i];
                    icon.preserveAspect = true;
                    icon.raycastTarget = false;
                    Abs(icon.rectTransform, 985, centerY[i] - 40f, 80, 80);
                    button.GetComponent<ButtonFeedback>().wiggle = icon.rectTransform;
                }
            }

            // SALIDA: aspa y texto, abajo a la izquierda.
            var exitHolder = UiKit.Rect(g, "Salida");
            UiKit.Stretch(exitHolder);
            exitCanvas = exitHolder.gameObject.AddComponent<CanvasGroup>();
            var exit = UiKit.Panel(exitHolder, "BotonSalida", new Color(0.35f, 0.22f, 0.1f, 0f));
            var exitButton = exit.gameObject.AddComponent<Button>();
            exitButton.transition = Selectable.Transition.None;
            exitButton.targetGraphic = exit;
            exitButton.onClick.AddListener(Quit);
            Abs(exit.rectTransform, 40, 22, 300, 92);
            var fb = exit.gameObject.AddComponent<ButtonFeedback>();
            fb.target = exit;
            fb.normal = new Color(0.35f, 0.22f, 0.1f, 0f);
            fb.hover = new Color(0.35f, 0.22f, 0.1f, 0.22f);
            fb.pressed = new Color(0.35f, 0.22f, 0.1f, 0.38f);
            fb.hoverScale = 1.05f;
            DrawCross(exit.rectTransform, new Vector2(62, 46), 62, 11, UiKit.Ink);
            var exitLabel = UiKit.Label(exit.transform, "SALIDA", 52, TextAnchor.MiddleLeft, UiKit.Ink);
            Abs(exitLabel.rectTransform, 118, 8, 180, 76);

            // Presentacion de los dados (decorativa y repetible).
            mainDice = DiceShowcase.Create(g, art, 120f, true);
            diceRect = (RectTransform)mainDice.transform;
            diceRect.anchoredPosition = new Vector2(1002f, 20f);
        }

        static void AddArtworkButton(Transform parent, string name, float x, float y, float width, float height, Action action)
        {
            var image = UiKit.Panel(parent, name, new Color(1f, 1f, 1f, 0f));
            var button = image.gameObject.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.targetGraphic = image;
            button.onClick.AddListener(() => action());
            Abs(image.rectTransform, x, y, width, height);
        }

        IEnumerator EnterMain()
        {
            if (usesReferenceMain)
            {
                mainGroup.alpha = 0f;
                yield return UiAnim.Fade(mainGroup, 0f, 1f, 0.4f);
                yield break;
            }

            titleCanvas.alpha = 0f;
            foreach (var cg in buttonCanvases) cg.alpha = 0f;
            exitCanvas.alpha = 0f;
            diceRect.localScale = Vector3.zero;
            mainDice.AutoRoll = false;

            StartCoroutine(UiAnim.Fade(titleCanvas, 0f, 1f, 0.9f, 0.1f));
            StartCoroutine(UiAnim.Slide(titleHolder, new Vector2(-70f, 0f), Vector2.zero, 0.9f, 0.1f));
            for (int i = 0; i < 3; i++)
            {
                float delay = 0.5f + 0.12f * i;
                StartCoroutine(UiAnim.Fade(buttonCanvases[i], 0f, 1f, 0.4f, delay));
                StartCoroutine(UiAnim.Slide(buttonRects[i], new Vector2(110f, 0f), Vector2.zero, 0.45f, delay));
            }
            StartCoroutine(UiAnim.Fade(exitCanvas, 0f, 1f, 0.5f, 1.0f));
            StartCoroutine(UiAnim.Pop(diceRect, 0.55f, 1.0f));
            yield return new WaitForSecondsRealtime(1.25f);
            mainDice.RollAll();
            mainDice.AutoRoll = true;
        }

        void Update()
        {
            time += Time.unscaledDeltaTime;
            // Movimiento sutil del arte del titulo (respiracion y balanceo).
            if (titleArt != null && mainGroup != null && mainGroup.gameObject.activeInHierarchy)
            {
                float s = 1f + 0.006f * Mathf.Sin(time * 0.9f);
                titleArt.localScale = new Vector3(s, s, 1f);
                titleArt.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(time * 0.55f) * 0.35f);
            }
        }

        static void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        // ---------------- Seleccion de personajes ----------------

        void BuildSelection()
        {
            bool useLegacyLayout = content.menuArt == null || content.menuArt.playerSelectionBackground == null;
            var g = Group("Seleccion", out selectionGroup, useLegacyLayout);
            selectionView = CharacterSelectionView.Create(g, content, selection, RequestStart,
                () => GoTo(mainGroup, () => { StartCoroutine(EnterMain()); }));
        }

        // Inicio de la partida: valida la seleccion, la guarda en SessionSetup y lanza los dados antes de cargar la escena.
        // "starting" impide pulsar dos veces o crear partidas duplicadas.
        void RequestStart()
        {
            if (starting || transitioning || !selection.CanStart) return;
            try { SessionSetup.Commit(selection); }
            catch (Exception e)
            {
                Debug.LogError("No se pudo iniciar la partida: " + e.Message);
                return;
            }
            starting = true;
            StartCoroutine(LaunchRoutine());
        }

        IEnumerator LaunchRoutine()
        {
            var canvasRoot = stage.parent;
            var overlay = UiKit.Panel(canvasRoot, "Lanzamiento", new Color(0.06f, 0.035f, 0.02f, 0f));
            UiKit.Stretch(overlay.rectTransform);       // bloquea cualquier otro clic
            yield return UiAnim.Run(0.3f, k => { if (overlay != null) overlay.color = new Color(0.06f, 0.035f, 0.02f, 0.86f * k); });

            var holder = UiKit.Rect(overlay.transform, "Centro");
            holder.anchorMin = holder.anchorMax = new Vector2(0.5f, 0.5f);
            holder.sizeDelta = new Vector2(600f, 400f);
            var dice = DiceShowcase.Create(holder, content.menuArt, 190f, false);
            dice.AutoRoll = false;
            var diceRt = (RectTransform)dice.transform;
            diceRt.anchorMin = diceRt.anchorMax = new Vector2(0.5f, 0.5f);
            diceRt.pivot = new Vector2(0.5f, 0.5f);
            diceRt.anchoredPosition = new Vector2(0f, 30f);
            var label = UiKit.Label(holder, "¿Quién empieza?", 96, TextAnchor.MiddleCenter, UiKit.Gold);
            UiKit.Place(label.rectTransform, 0, 0, 1, 0, 0, -120, 0, -10);
            label.raycastTarget = false;

            bool done = false;
            dice.RollAll(() => done = true);
            while (!done) yield return null;
            yield return new WaitForSecondsRealtime(0.45f);

            var op = SceneManager.LoadSceneAsync(GameScene);
            while (op != null && !op.isDone) yield return null;
        }

        // ---------------- Opciones y personajes ----------------

        void BuildOptions()
        {
            var g = Group("Opciones", out optionsGroup, true);
            AddWoodBackdrop(g);
            Header(g, "OPCIONES");
            var s = content.settings;
            var sheet = UiKit.Panel(g, "PergaminoOpciones", Color.white);
            sheet.sprite = MenuSprites.Parchment();
            Abs(sheet.rectTransform, 92, 130, 1209, 774);

            var rules = UiKit.Label(sheet.transform,
                "DADO DE ORDEN: caras del 1 al 6, para decidir quién empieza.\nDADO DE MOVIMIENTO: avanza del 1 al 4; las otras caras castigan.",
                27, TextAnchor.UpperLeft, UiKit.Ink);
            Abs(rules.rectTransform, 76, 564, 1050, 98);

            var players = FramePanel(sheet.transform, new Color32(221, 202, 158, 255));
            Abs(players.rectTransform, 72, 434, 500, 94);
            var playersTitle = UiKit.Label(players.transform, "JUGADORES", 25, TextAnchor.MiddleLeft, UiKit.Ink);
            Abs(playersTitle.rectTransform, 18, 45, 260, 34);
            var playersValue = UiKit.Label(players.transform, s.minPlayers + " - " + s.maxPlayers, 34, TextAnchor.MiddleRight, UiKit.Wood);
            Abs(playersValue.rectTransform, 280, 45, 185, 34);
            var playersHint = UiKit.Label(players.transform, "Se elige en la pantalla de selección", 18, TextAnchor.MiddleLeft, UiKit.Wood);
            Abs(playersHint.rectTransform, 18, 12, 450, 28);

            var repeat = FramePanel(sheet.transform, new Color32(221, 202, 158, 255));
            Abs(repeat.rectTransform, 594, 434, 500, 94);
            var repeatTitle = UiKit.Label(repeat.transform, "REPETIR PERSONAJES", 23, TextAnchor.MiddleLeft, UiKit.Ink);
            Abs(repeatTitle.rectTransform, 18, 45, 315, 34);
            var repeatValue = FramePanel(repeat.transform, RepeatColor());
            Abs(repeatValue.rectTransform, 350, 43, 126, 38);
            repeatValueText = UiKit.Label(repeatValue.transform, s.allowRepeatedCharacters ? "SÍ" : "NO", 22, TextAnchor.MiddleCenter, Color.white);
            UiKit.Stretch(repeatValueText.rectTransform);
            repeatButton = repeatValue.gameObject.AddComponent<Button>();
            repeatButton.transition = Selectable.Transition.None;
            repeatButton.targetGraphic = repeatValue;
            repeatButton.onClick.AddListener(ToggleRepeats);
            var repeatHint = UiKit.Label(repeat.transform, "Pulsa para cambiar la regla", 18, TextAnchor.MiddleLeft, UiKit.Wood);
            Abs(repeatHint.rectTransform, 18, 12, 450, 28);

            AddOptionSlider(sheet.rectTransform, "DURACIÓN DEL MINIJUEGO", s.minigameSeconds, 30f, 180f, 10f,
                306f, v => s.minigameSeconds = v);
            AddOptionSlider(sheet.rectTransform, "PENALIZACIÓN DE TIEMPO (CARA 6)", s.timePenaltySeconds, 0f, 30f, 5f,
                164f, v => s.timePenaltySeconds = v);
            var note = UiKit.Label(sheet.transform, "Los cambios se aplican a la siguiente partida.", 19, TextAnchor.MiddleCenter, UiKit.Wood);
            Abs(note.rectTransform, 72, 30, 1020, 35);
            BackButton(g);
        }

        Color RepeatColor()
        {
            return content.settings.allowRepeatedCharacters
                ? new Color32(63, 117, 59, 255)
                : new Color32(118, 52, 43, 255);
        }

        void ToggleRepeats()
        {
            var settings = content.settings;
            settings.allowRepeatedCharacters = !settings.allowRepeatedCharacters;
            selection.AllowRepeats = settings.allowRepeatedCharacters;
            var image = repeatButton.GetComponent<Image>();
            if (image != null) image.color = RepeatColor();
            if (repeatValueText != null) repeatValueText.text = settings.allowRepeatedCharacters ? "SÍ" : "NO";
            if (selectionView != null) selectionView.Refresh();
        }

        void AddOptionSlider(RectTransform parent, string label, float value, float min, float max, float step,
            float y, Action<float> setValue)
        {
            var title = UiKit.Label(parent, label, 25, TextAnchor.MiddleLeft, UiKit.Ink);
            Abs(title.rectTransform, 74, y + 52, 890, 36);
            var number = UiKit.Label(parent, "", 25, TextAnchor.MiddleRight, UiKit.Wood);
            Abs(number.rectTransform, 980, y + 52, 135, 36);

            var track = FramePanel(parent, new Color32(84, 49, 30, 255));
            Abs(track.rectTransform, 74, y, 1040, 40);
            var fill = UiKit.Panel(track.transform, "Progreso", new Color32(186, 128, 65, 255));
            var marker = UiKit.Panel(parent, "Moneda", UiKit.Gold);
            marker.sprite = MenuSprites.Circle();
            marker.raycastTarget = false;

            float current = value;
            Action refresh = () =>
            {
                float progress = Mathf.InverseLerp(min, max, current);
                number.text = current.ToString("0") + " s";
                fill.gameObject.SetActive(progress > 0f);
                UiKit.Place(fill.rectTransform, 0, 0, Mathf.Max(0.001f, progress), 1, 5, 5, -2, -5);
                Abs(marker.rectTransform, 74 + 1040 * progress - 16, y - 5, 50, 50);
            };
            Action<float> nudge = delta =>
            {
                current = Mathf.Clamp(Mathf.Round((current + delta) / step) * step, min, max);
                setValue(current);
                refresh();
            };
            refresh();

            var minus = FrameButton(parent, "−", 24, () => nudge(-step), new Color32(143, 87, 47, 255));
            Abs((RectTransform)minus.transform, 24, y, 44, 40);
            var plus = FrameButton(parent, "+", 24, () => nudge(step), new Color32(143, 87, 47, 255));
            Abs((RectTransform)plus.transform, 1120, y, 44, 40);
        }

        void BuildGallery()
        {
            var g = Group("Personajes", out galleryGroup, true);
            AddWoodBackdrop(g);
            Header(g, "PERSONAJES");
            int count = content.characters.Count;
            const int cols = 3;
            const float cw = 380f, ch = 300f, gap = 26f;
            float left = (DesignW - (cols * cw + (cols - 1) * gap)) / 2f;
            for (int i = 0; i < count; i++)
            {
                int col = i % cols, row = i / cols;
                var card = FramePanel(g, new Color32(24, 30, 43, 244));
                Abs(card.rectTransform, left + col * (cw + gap), 800f - ch - row * (ch + gap), cw, ch);
                var c = content.characters[i];
                var glow = UiKit.Panel(card.transform, "Resplandor", new Color(c.mainColor.r, c.mainColor.g, c.mainColor.b, 0.34f));
                glow.sprite = MenuSprites.Circle();
                glow.raycastTarget = false;
                Abs(glow.rectTransform, 20, 36, 138, 225);
                var art = CharacterPortraitLibrary.GetCharacterArtwork(i);
                if (art != null && CharacterPortraitLibrary.GetArtworkMaterial() != null)
                {
                    var portraitObject = new GameObject("ArteDelPersonaje", typeof(RectTransform), typeof(RawImage));
                    portraitObject.transform.SetParent(glow.transform, false);
                    var portrait = portraitObject.GetComponent<RawImage>();
                    UiKit.Stretch(portrait.rectTransform);
                    portrait.texture = art;
                    portrait.material = CharacterPortraitLibrary.GetArtworkMaterial();
                    portrait.uvRect = CharacterPortraitLibrary.GetArtworkUvRect(art, 138f / 225f);
                    portrait.raycastTarget = false;
                }
                else if (c.portrait != null)
                {
                    var portrait = UiKit.Panel(glow.transform, "Retrato", Color.white);
                    portrait.sprite = c.portrait;
                    portrait.preserveAspect = true;
                    portrait.raycastTarget = false;
                    UiKit.Place(portrait.rectTransform, 0, 0, 1, 1, 6, 6, -6, -6);
                }
                var name = UiKit.Label(card.transform, c.displayName, 31, TextAnchor.MiddleLeft, UiKit.Gold);
                Abs(name.rectTransform, 164, 174, 192, 72);
                var desc = UiKit.Label(card.transform, c.description, 21, TextAnchor.UpperLeft, UiKit.Parchment);
                Abs(desc.rectTransform, 164, 42, 192, 124);
            }
            BackButton(g);
        }

        static void AddWoodBackdrop(RectTransform parent)
        {
            var backdrop = UiKit.Panel(parent, "ParedDeMadera", Color.white);
            backdrop.sprite = MenuSprites.WoodWall();
            backdrop.raycastTarget = false;
            UiKit.Stretch(backdrop.rectTransform);
            backdrop.transform.SetAsFirstSibling();
        }

        void Header(RectTransform parent, string text)
        {
            var plaque = FramePanel(parent, new Color32(144, 91, 48, 255));
            Abs(plaque.rectTransform, 445, 886, 500, 128);
            var t = UiKit.Label(parent, text, 66, TextAnchor.MiddleCenter, UiKit.Parchment);
            Abs(t.rectTransform, 455, 897, 480, 105);
        }

        void BackButton(RectTransform parent)
        {
            var back = FrameButton(parent, "VOLVER", 38, () => GoTo(mainGroup, () => StartCoroutine(EnterMain())), new Color32(143, 87, 47, 255));
            Abs((RectTransform)back.transform, 54, 42, 270, 78);
        }
    }
}
