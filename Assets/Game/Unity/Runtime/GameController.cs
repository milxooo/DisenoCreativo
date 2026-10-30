using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using RobaColores.Game.Core;
using RobaColores.Map;

namespace RobaColores.Game.Presentation
{
    // Conecta el nucleo (GameSession) con el mapa, los peones y la interfaz de una partida en un solo dispositivo.
    // Toda la logica de reglas vive en el nucleo; aqui solo se muestra el estado y se envian ordenes.
    public sealed class GameController : MonoBehaviour
    {
        sealed class PlayerHudRow
        {
            public Image background, accent;
            public RawImage portrait;
            public Text details, inventory;
        }

        [SerializeField] GameContent content;
        [Tooltip("Solo pruebas: juega la partida sola para comprobar que el flujo completo funciona.")]
        [SerializeField] bool autopilot;
        [Tooltip("Solo pruebas: permite abrir Partida directamente (sin pasar por el menu), con jugadores y personajes por defecto.")]
        [SerializeField] bool allowDirectStart;

        float autopilotTimer;
        bool autopilotLingered;
        float minigameStartedAt;
        GameSession session;
        MapBoard board;
        Canvas canvas;

        readonly List<string> names = new List<string>();
        readonly List<Color> colors = new List<Color>();
        readonly List<string> characterNames = new List<string>();
        readonly Dictionary<int, Transform> pawns = new Dictionary<int, Transform>();
        readonly List<Transform> billboards = new List<Transform>();
        readonly List<Material> artworkMaterials = new List<Material>();
        readonly List<PlayerHudRow> playerHudRows = new List<PlayerHudRow>();
        readonly Queue<GameEvent> moves = new Queue<GameEvent>();
        readonly List<string> log = new List<string>();
        readonly List<GameObject> markers = new List<GameObject>();

        bool busy;
        bool orderPending;
        bool pendingResults;
        string lastRoll = "";
        string lastOrderRoll = "";

        // Interfaz
        RectTransform modalLayer, actionBar, pathPanel;
        Text banner, playersText, logText;
        Image hudPanel, bannerPanel, logPanel;
        Button rollButton;
        MinigameView minigame;

        // Flujo de robo de filas
        List<KeyValuePair<int, int>> stealQueue;
        int stealIndex;
        Dictionary<int, IDictionary<int, int>> selections;

        void Awake()
        {
            if (content == null) { Debug.LogError("GameController necesita el asset GameContent."); enabled = false; return; }
            // Si se pulsa Play estando en esta escena sin haber elegido personajes en el menu, se vuelve al menu.
            if (!SessionSetup.HasSelection && !allowDirectStart)
            {
                enabled = false;
                SceneManager.LoadScene("Menu");
                return;
            }
            UiKit.Font = content.uiFont;
            board = FindFirstObjectByType<MapBoard>();
            if (board == null) { Debug.LogError("No hay MapBoard en la escena."); enabled = false; return; }
            UiKit.EnsureEventSystem();
            canvas = UiKit.CreateCanvas("GameCanvas", 10);
            BuildHud();
        }

        void Start()
        {
            if (!enabled) return;
            // Los jugadores vienen del menu (seleccion confirmada). Si la escena se abre sola, se usan los primeros personajes.
            var setup = new List<PlayerSetup>(SessionSetup.Players);
            if (setup.Count == 0)
                for (int i = 0; i < content.settings.maxPlayers; i++) setup.Add(new PlayerSetup { number = i + 1, characterIndex = i % content.characters.Count, name = "Jugador " + (i + 1) });
            var config = content.BuildConfig();
            if (!SessionSetup.HasSelection) config.AllowRepeatedCharacters = true;   // solo para pruebas sin menu
            session = new GameSession(config, board.BuildGraph(), content.BuildCatalog(), new SystemRandomSource());
            session.Event += OnEvent;
            for (int i = 0; i < setup.Count; i++)
            {
                string playerName = setup[i].name;
                var character = content.characters[setup[i].characterIndex];
                session.AddPlayer(playerName, setup[i].characterIndex);
                names.Add(playerName);
                colors.Add(character.mainColor);
                characterNames.Add(character.displayName);
                var pawn = Instantiate(character.pawnPrefab).transform;
                pawn.name = "Peon_" + playerName;
                pawn.localScale = Vector3.one * 1.6f;   // legible a la escala del mapa
                ConfigurePawnArtwork(pawn, setup[i].characterIndex);
                pawns[i] = pawn;
            }
            int rows = session.Players.Count;
            BuildPlayerHud();
            UiKit.Place(hudPanel.rectTransform, 0, 1, 0, 1, 8, -(30 + 51 * rows), 398, -8);
            session.StartGame();
            foreach (var p in session.Players) pawns[p.Id].position = PawnPosition(p.Id, p.TileId);
            StartCoroutine(Settle());
        }

        // ---------------- Ordenes del jugador ----------------

        void Act(Action command)
        {
            if (busy) return;
            try { command(); }
            catch (Exception e) { Debug.LogException(e); }
            StartCoroutine(Settle());
        }

        void RollDice() { Act(() => session.RollMoveDie()); }

        void RollOrderDie() { Act(() => session.RollOrderDie()); }

        void ChoosePath(int tileId) { ClearMarkers(); Act(() => session.ChoosePath(tileId)); }

        void OnFinishPressed(int playerId) { Act(() => session.PressFinish(playerId)); }

        void Update()
        {
            if (session == null || busy) return;
            if (autopilot) AutopilotStep();
            if (session.Phase == GamePhase.MinigameRunning)
            {
                session.Tick(Time.deltaTime);
                if (session.Phase != GamePhase.MinigameRunning) StartCoroutine(Settle());
                else if (minigame != null) minigame.Tick();
            }
            else if (session.Phase == GamePhase.AwaitingMoveRoll && !orderPending && rollButton.gameObject.activeSelf
                     && Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
            {
                RollDice();
            }
        }

        void LateUpdate()
        {
            var camera = Camera.main;
            if (camera == null) return;
            foreach (var billboard in billboards)
            {
                if (billboard != null)
                    billboard.rotation = Quaternion.LookRotation(camera.transform.position - billboard.position, Vector3.up);
            }
        }

        void ConfigurePawnArtwork(Transform pawn, int characterIndex)
        {
            var texture = CharacterPortraitLibrary.GetCharacterArtwork(characterIndex);
            var sharedMaterial = CharacterPortraitLibrary.GetArtworkMaterial();
            if (texture == null || sharedMaterial == null) return;

            foreach (var renderer in pawn.GetComponentsInChildren<Renderer>(true)) renderer.enabled = false;

            var visual = GameObject.CreatePrimitive(PrimitiveType.Quad);
            visual.name = "Arte del personaje";
            visual.transform.SetParent(pawn, false);
            visual.transform.localPosition = new Vector3(0f, 0.65f, 0f);
            visual.transform.localScale = new Vector3(1.2f, 1.3f, 1f);
            visual.GetComponent<Collider>().enabled = false;
            var material = new Material(sharedMaterial);
            material.mainTexture = texture;
            var uvRect = CharacterPortraitLibrary.GetArtworkUvRect(texture, 1.2f / 1.3f);
            material.SetVector("_ArtworkUVRect", new Vector4(uvRect.x, uvRect.y, uvRect.width, uvRect.height));
            artworkMaterials.Add(material);
            visual.GetComponent<MeshRenderer>().sharedMaterial = material;
            billboards.Add(visual.transform);
        }

        void OnDestroy()
        {
            foreach (var material in artworkMaterials)
                if (material != null) Destroy(material);
        }

        void AutopilotStep()
        {
            autopilotTimer -= Time.deltaTime;
            if (autopilotTimer > 0f) return;
            autopilotTimer = 0.25f;
            if (orderPending) { orderPending = false; autopilotTimer = 2.5f; Refresh(); return; }
            if (pendingResults) { pendingResults = false; autopilotTimer = 2.5f; Refresh(); return; }
            switch (session.Phase)
            {
                case GamePhase.AwaitingOrderRoll: RollOrderDie(); break;
                case GamePhase.AwaitingMoveRoll: RollDice(); break;
                case GamePhase.AwaitingPathChoice:
                    if (!autopilotLingered) { autopilotLingered = true; autopilotTimer = 2.5f; break; }   // deja ver las opciones
                    autopilotLingered = false;
                    ChoosePath(session.PathOptions[UnityEngine.Random.Range(0, session.PathOptions.Count)]);
                    break;
                case GamePhase.MinigameRunning:
                {
                    if (Time.time - minigameStartedAt < 4f) return;   // deja ver la interfaz unos segundos
                    var round = session.Round;
                    var figure = round.Figure;
                    var ids = new List<int>(round.ParticipantIds);
                    foreach (var pid in ids)
                        for (int y = 0; y < figure.Height; y++)
                            for (int x = 0; x < figure.Width; x++)
                                if (figure.CellAt(x, y) >= 0 && UnityEngine.Random.value < 0.8f) round.TryPaint(pid, x, y, figure.CellAt(x, y));
                    OnFinishPressed(ids[0]);
                    break;
                }
                case GamePhase.AwaitingRowSelection:
                    if (stealQueue != null && stealIndex < stealQueue.Count) { autopilotTimer = 2.5f; PickRow(UnityEngine.Random.Range(0, session.Round.Figure.Height)); }
                    break;
            }
        }

        // ---------------- Eventos del nucleo ----------------

        void OnEvent(GameEvent e)
        {
            if (e.Type == GameEventType.PlayerMoved) { moves.Enqueue(e); return; }
            if (e.Type == GameEventType.OrderDetermined) orderPending = true;
            if (e.Type == GameEventType.OrderRollCompleted)
                lastOrderRoll = names[e.PlayerId] + " sacó un " + e.A + ".";
            if (e.Type == GameEventType.MinigameFinished) pendingResults = true;
            if (e.Type == GameEventType.MoveRolled)
                lastRoll = names[e.PlayerId] + " " + GameText.FaceText(new MoveFace((MoveFaceKind)e.A, e.B)) + ".";
            var text = Describe(e);
            if (text != null) { log.Add(text); if (log.Count > 9) log.RemoveAt(0); }
        }

        string Describe(GameEvent e)
        {
            switch (e.Type)
            {
                case GameEventType.TurnSkipped: return names[e.PlayerId] + " pierde su turno.";
                case GameEventType.MoveRolled: return names[e.PlayerId] + " " + GameText.FaceText(new MoveFace((MoveFaceKind)e.A, e.B)) + ".";
                case GameEventType.TimePenaltyApplied: return names[e.PlayerId] + ": penalización de " + e.A + " s en su próximo minijuego.";
                case GameEventType.MinigameStarted:
                    return "Minijuego de " + GameText.CategoryName((Category)e.A) + ".";
                case GameEventType.MinigameFinished:
                    return e.A == (int)FinishReason.Timeout ? "Se acabó el tiempo." : names[e.PlayerId] + " terminó el desafío.";
                case GameEventType.NoWinnerInMinigame: return "Nadie puntuó: no hay robo.";
                case GameEventType.RewardsApplied: return "Filas robadas: " + e.A + ".";
                case GameEventType.PlayerDisqualified: return names[e.PlayerId] + " queda descalificado.";
                default: return null;
            }
        }

        IEnumerator Settle()
        {
            busy = true;
            while (moves.Count > 0)
            {
                var m = moves.Dequeue();
                yield return MovePawn(m.PlayerId, m.B);
            }
            busy = false;
            Refresh();
        }

        IEnumerator MovePawn(int playerId, int toTile)
        {
            var pawn = pawns[playerId];
            Vector3 a = pawn.position, b = PawnPosition(playerId, toTile);
            const float duration = 0.28f;
            for (float t = 0f; t < duration; t += Time.deltaTime)
            {
                float k = t / duration;
                pawn.position = Vector3.Lerp(a, b, k) + Vector3.up * (Mathf.Sin(k * Mathf.PI) * 0.45f);
                yield return null;
            }
            pawn.position = b;
        }

        Vector3 PawnPosition(int playerId, int tileId)
        {
            var offset = new Vector3((playerId % 2 == 0 ? -0.35f : 0.35f), 0f, (playerId / 2 == 0 ? -0.3f : 0.3f));
            return board.PositionOf(tileId) + offset;
        }

        // ---------------- Pantalla segun la fase ----------------

        void BuildHud()
        {
            var t = canvas.transform;
            hudPanel = MenuWidgets.FramePanel(t, Color.white);
            hudPanel.name = "ListaTripulacion";
            hudPanel.sprite = MenuSprites.Parchment();
            hudPanel.type = Image.Type.Simple;
            UiKit.Place(hudPanel.rectTransform, 0, 1, 0, 1, 8, -240, 398, -8);
            playersText = UiKit.Label(t, "TRIPULACIÓN", 22, TextAnchor.MiddleCenter, UiKit.Ink);
            UiKit.Place(playersText.rectTransform, 0, 1, 0, 1, 16, -39, 390, -10);

            bannerPanel = MenuWidgets.FramePanel(t, Color.white);
            bannerPanel.sprite = MenuSprites.Parchment();
            bannerPanel.type = Image.Type.Simple;
            UiKit.Place(bannerPanel.rectTransform, 0.5f, 1, 0.5f, 1, -300, -76, 300, -6);
            banner = UiKit.Label(t, "", 29, TextAnchor.MiddleCenter, UiKit.Ink);
            UiKit.Place(banner.rectTransform, 0.5f, 1, 0.5f, 1, -276, -65, 276, -12);

            logPanel = MenuWidgets.FramePanel(t, Color.white);
            logPanel.sprite = MenuSprites.Parchment();
            logPanel.type = Image.Type.Simple;
            UiKit.Place(logPanel.rectTransform, 1, 0, 1, 0, -440, 8, -8, 166);
            logText = UiKit.Label(t, "", 17, TextAnchor.LowerLeft, UiKit.Ink);
            UiKit.Place(logText.rectTransform, 1, 0, 1, 0, -415, 20, -28, 144);

            actionBar = UiKit.Rect(t, "Acciones");
            UiKit.Place(actionBar, 0.5f, 0, 0.5f, 0, -230, 18, 230, 150);
            rollButton = MenuWidgets.FrameButton(actionBar, "TIRAR DADO", 26, RollDice, new Color32(137, 83, 42, 255));
            UiKit.Place((RectTransform)rollButton.transform, 0, 0, 1, 0, 0, 0, 0, 64);

            pathPanel = UiKit.Rect(t, "Caminos");
            UiKit.Place(pathPanel, 0.5f, 0, 0.5f, 0, -230, 18, 230, 260);

            modalLayer = UiKit.Rect(t, "Modales");
            UiKit.Stretch(modalLayer);
        }

        void BuildPlayerHud()
        {
            foreach (var player in session.Players)
            {
                var row = new PlayerHudRow();
                var rowRect = UiKit.Rect(hudPanel.transform, "Jugador" + player.Id);
                MenuWidgets.Abs(rowRect, 15, 15 + (session.Players.Count - 1 - player.Id) * 49f, 360, 44);
                row.background = MenuWidgets.FramePanel(rowRect, new Color32(235, 221, 181, 255));
                UiKit.Stretch(row.background.rectTransform);
                row.accent = UiKit.Panel(rowRect, "Acento", colors[player.Id]);
                MenuWidgets.Abs(row.accent.rectTransform, 5, 6, 5, 32);

                var portraitObject = new GameObject("Retrato", typeof(RectTransform), typeof(RawImage));
                portraitObject.transform.SetParent(rowRect, false);
                row.portrait = portraitObject.GetComponent<RawImage>();
                MenuWidgets.Abs(row.portrait.rectTransform, 16, 3, 37, 38);
                var artwork = CharacterPortraitLibrary.GetCharacterArtwork(player.CharacterId);
                if (artwork != null)
                {
                    row.portrait.texture = artwork;
                    row.portrait.material = CharacterPortraitLibrary.GetArtworkMaterial();
                    row.portrait.uvRect = CharacterPortraitLibrary.GetArtworkUvRect(artwork, 0.82f);
                }
                row.portrait.raycastTarget = false;

                row.details = UiKit.Label(rowRect, "", 16, TextAnchor.MiddleLeft, UiKit.Ink);
                MenuWidgets.Abs(row.details.rectTransform, 60, 3, 210, 38);
                var coin = UiKit.Panel(rowRect, "Moneda", UiKit.Gold);
                coin.sprite = MenuSprites.Circle();
                coin.raycastTarget = false;
                MenuWidgets.Abs(coin.rectTransform, 272, 11, 20, 20);
                row.inventory = UiKit.Label(rowRect, "", 18, TextAnchor.MiddleRight, UiKit.Ink);
                MenuWidgets.Abs(row.inventory.rectTransform, 295, 4, 55, 36);
                playerHudRows.Add(row);
            }
        }

        void Refresh()
        {
            if (session == null) return;
            UpdateHud();
            UiKit.Clear(modalLayer);
            UiKit.Clear(pathPanel);
            ClearMarkers();
            rollButton.gameObject.SetActive(false);
            actionBar.gameObject.SetActive(false);

            if (session.Phase != GamePhase.MinigameRunning && minigame != null) { minigame.Close(); minigame = null; }

            if (session.Phase == GamePhase.GameOver) { ShowGameOver(); return; }
            if (session.Phase == GamePhase.AwaitingOrderRoll) { ShowOrderRoll(); return; }
            if (orderPending) { ShowOrder(); return; }
            if (pendingResults) { ShowResults(); return; }

            switch (session.Phase)
            {
                case GamePhase.AwaitingMoveRoll:
                    actionBar.gameObject.SetActive(true);
                    rollButton.gameObject.SetActive(true);
                    UiKit.SetLabel(rollButton, "Tirar dado (Espacio)");
                    break;
                case GamePhase.AwaitingPathChoice:
                    ShowPathChoice();
                    break;
                case GamePhase.MinigameRunning:
                    if (minigame == null)
                    {
                        minigame = MinigameView.Create(canvas.transform, session, content, names, colors, OnFinishPressed);
                        minigameStartedAt = Time.time;
                    }
                    break;
                case GamePhase.AwaitingRowSelection:
                    BeginSteal();
                    break;
            }
        }

        void UpdateHud()
        {
            foreach (var p in session.Players)
            {
                string hex = ColorUtility.ToHtmlStringRGB(colors[p.Id]);
                bool current = session.Phase == GamePhase.AwaitingOrderRoll
                    ? session.CurrentOrderPlayerId == p.Id
                    : session.Phase != GamePhase.MinigameRunning && session.Phase != GamePhase.GameOver &&
                      session.CurrentPlayer != null && session.CurrentPlayer.Id == p.Id;
                var row = playerHudRows[p.Id];
                row.background.color = p.Disqualified
                    ? new Color32(174, 151, 129, 255)
                    : current ? new Color32(255, 241, 184, 255) : new Color32(235, 221, 181, 255);
                string status = p.Disqualified ? " · FUERA" : p.SkipNextTurn ? " · PIERDE TURNO" : p.PendingTimePenalty > 0f ? " · PENALIZACIÓN" : "";
                row.details.text = "<color=#" + hex + "><b>" + p.Name + "</b></color>\n" + characterNames[p.Id] + status;
                row.inventory.text = p.Inventory.Total.ToString();
            }
            logText.text = string.Join("\n", log.ToArray()) + (lastRoll.Length > 0 && session.Phase == GamePhase.AwaitingMoveRoll ? "" : "");
            logPanel.gameObject.SetActive(log.Count > 0);
            if (session.Phase == GamePhase.GameOver) banner.text = "Fin de la partida";
            else if (session.Phase == GamePhase.MinigameRunning) banner.text = "Minijuego simultáneo: todos juegan";
            else if (session.Phase == GamePhase.AwaitingOrderRoll && session.CurrentOrderPlayerId.HasValue)
                banner.text = "Tirada de salida: turno de " + names[session.CurrentOrderPlayerId.Value];
            else if (session.CurrentPlayer != null) banner.text = "Turno de " + session.CurrentPlayer.Name;
        }

        void ClearMarkers()
        {
            foreach (var m in markers) if (m != null) Destroy(m);
            markers.Clear();
        }

        void ShowPathChoice()
        {
            actionBar.gameObject.SetActive(false);
            var parchment = MenuWidgets.FramePanel(pathPanel, UiKit.Parchment);
            UiKit.Stretch(parchment.rectTransform);
            var title = UiKit.Label(pathPanel, "ELIGE TU CAMINO", 24, TextAnchor.MiddleCenter, UiKit.Ink);
            UiKit.Place(title.rectTransform, 0, 1, 1, 1, 0, -40, 0, 0);
            float y = -52f;
            foreach (var opt in session.PathOptions)
            {
                int id = opt;
                var tile = session.Board.Get(id);
                var cat = session.Config.CategoryByColor[tile.Color];
                var btn = MenuWidgets.FrameButton(pathPanel, "Casilla " + GameText.ColorName(tile.Color) + " (" + GameText.CategoryName(cat) + ")", 20, () => ChoosePath(id), new Color32(113, 69, 39, 255));
                UiKit.Place((RectTransform)btn.transform, 0, 1, 1, 1, 0, y - 48, 0, y);
                y -= 56f;
                if (content.markerPrefab != null)
                {
                    var marker = Instantiate(content.markerPrefab, board.PositionOf(id) + Vector3.up * 0.9f, Quaternion.identity);
                    markers.Add(marker);
                }
            }
        }

        // ---------------- Modales ----------------

        RectTransform Modal(string title, float w, float h)
        {
            var dim = UiKit.Panel(modalLayer, "Fondo", new Color(0, 0, 0, 0.55f));
            UiKit.Stretch(dim.rectTransform);
            var box = MenuWidgets.FramePanel(modalLayer, UiKit.Parchment);
            box.name = "Ventana";
            box.sprite = MenuSprites.Parchment();
            box.type = Image.Type.Simple;
            UiKit.Place(box.rectTransform, 0.5f, 0.5f, 0.5f, 0.5f, -w / 2, -h / 2, w / 2, h / 2);
            var head = MenuWidgets.FramePanel(box.transform, UiKit.Wood);
            head.name = "Titulo";
            head.sprite = MenuSprites.WoodWall();
            head.type = Image.Type.Simple;
            UiKit.Place(head.rectTransform, 0, 1, 1, 1, 0, -56, 0, 0);
            var t = UiKit.Label(head.transform, title, 32, TextAnchor.MiddleCenter, UiKit.Parchment);
            UiKit.Stretch(t.rectTransform);
            return box.rectTransform;
        }

        RawImage AddPortrait(Transform parent, int playerId, float x, float y, float w, float h)
        {
            var back = MenuWidgets.FramePanel(parent, Color.Lerp(colors[playerId], UiKit.Parchment, 0.3f));
            MenuWidgets.Abs(back.rectTransform, x, y, w, h);
            var texture = CharacterPortraitLibrary.GetCharacterArtwork(session.GetPlayer(playerId).CharacterId);
            if (texture == null) return null;

            var portraitObject = new GameObject("Retrato" + playerId, typeof(RectTransform), typeof(RawImage));
            portraitObject.transform.SetParent(back.transform, false);
            var portrait = portraitObject.GetComponent<RawImage>();
            UiKit.Place(portrait.rectTransform, 0, 0, 1, 1, 5, 5, -5, -5);
            portrait.texture = texture;
            portrait.material = CharacterPortraitLibrary.GetArtworkMaterial();
            portrait.uvRect = CharacterPortraitLibrary.GetArtworkUvRect(texture, (w - 10f) / (h - 10f));
            portrait.raycastTarget = false;
            return portrait;
        }

        void ShowOrder()
        {
            var box = Modal("Orden de turnos", 900, 640);
            var sb = new StringBuilder();
            var rounds = session.OrderResult.Rounds;
            for (int r = 0; r < rounds.Count; r++)
            {
                sb.AppendLine(r == 0 ? "Tirada inicial:" : "Desempate:");
                foreach (var kv in rounds[r]) sb.AppendLine("   " + names[kv.Key] + ": " + kv.Value);
            }
            var leftPanel = MenuWidgets.FramePanel(box, new Color32(221, 202, 158, 255));
            MenuWidgets.Abs(leftPanel.rectTransform, 22, 88, 392, 472);
            var leftTitle = UiKit.Label(leftPanel.transform, "RESULTADO DE LOS DADOS", 20, TextAnchor.MiddleCenter, UiKit.Wood);
            MenuWidgets.Abs(leftTitle.rectTransform, 10, 418, 372, 40);
            var left = UiKit.Label(leftPanel.transform, sb.ToString(), 22, TextAnchor.UpperLeft, UiKit.Ink);
            MenuWidgets.Abs(left.rectTransform, 18, 30, 356, 376);

            var rightPanel = MenuWidgets.FramePanel(box, new Color32(221, 202, 158, 255));
            MenuWidgets.Abs(rightPanel.rectTransform, 440, 88, 438, 472);
            var rightTitle = UiKit.Label(rightPanel.transform, "ORDEN DE SALIDA", 22, TextAnchor.MiddleCenter, UiKit.Wood);
            MenuWidgets.Abs(rightTitle.rectTransform, 16, 418, 406, 40);
            for (int i = 0; i < session.TurnOrder.Count; i++)
            {
                int id = session.TurnOrder[i];
                float y = 350f - i * 76f;
                var row = MenuWidgets.FramePanel(rightPanel.transform, Color.white);
                MenuWidgets.Abs(row.rectTransform, 16, y, 406, 64);
                var number = UiKit.Label(row.transform, (i + 1).ToString(), 30, TextAnchor.MiddleCenter, UiKit.Gold);
                MenuWidgets.Abs(number.rectTransform, 8, 8, 44, 48);
                var name = UiKit.Label(row.transform, "<color=#" + ColorUtility.ToHtmlStringRGB(colors[id]) + "><b>" + names[id] + "</b></color>\n" + characterNames[id], 19, TextAnchor.MiddleLeft, UiKit.Ink);
                MenuWidgets.Abs(name.rectTransform, 58, 7, 336, 50);
            }
            var go = MenuWidgets.FrameButton(box, "COMENZAR", 26, () => { orderPending = false; Refresh(); }, new Color32(137, 83, 42, 255));
            UiKit.Place((RectTransform)go.transform, 0.5f, 0, 0.5f, 0, -110, 16, 110, 64);
        }

        void ShowOrderRoll()
        {
            var box = Modal("¿Quién comienza?", 900, 640);
            int playerId = session.CurrentOrderPlayerId.Value;
            var prompt = UiKit.Label(box,
                "Turno de tirada: " + names[playerId] + "\n" + characterNames[playerId],
                31, TextAnchor.MiddleCenter, UiKit.Ink);
            UiKit.Place(prompt.rectTransform, 0, 1, 0.64f, 1, 24, -152, -12, -72);

            var currentPlayer = MenuWidgets.FramePanel(box, colors[playerId]);
            UiKit.Place(currentPlayer.rectTransform, 0.68f, 0, 1, 1, 10, 112, -24, -166);
            AddPortrait(currentPlayer.transform, playerId, 20, 100, 190, 250);
            var character = UiKit.Label(currentPlayer.transform, characterNames[playerId], 22, TextAnchor.MiddleCenter, Color.white);
            MenuWidgets.Abs(character.rectTransform, 16, 52, 198, 40);
            var playerName = UiKit.Label(currentPlayer.transform, names[playerId], 25, TextAnchor.MiddleCenter, Color.white);
            MenuWidgets.Abs(playerName.rectTransform, 16, 14, 198, 36);

            var history = new StringBuilder("Tiradas realizadas\n");
            for (int round = 0; round < session.OrderResult.Rounds.Count; round++)
            {
                var rolls = session.OrderResult.Rounds[round];
                if (rolls.Count == 0) continue;
                history.AppendLine(round == 0 ? "Ronda inicial:" : "Desempate:");
                var ids = new List<int>(rolls.Keys);
                ids.Sort();
                foreach (var id in ids) history.AppendLine("  " + names[id] + ": " + rolls[id]);
            }
            history.AppendLine();
            history.AppendLine("Lancen en orden. Si hay empate, repiten solo los empatados.");
            var text = UiKit.Label(box, history.ToString(), 21, TextAnchor.UpperLeft, UiKit.Ink);
            UiKit.Place(text.rectTransform, 0, 0, 0.64f, 1, 28, 100, -12, -170);

            var die = UiKit.Label(box, string.IsNullOrEmpty(lastOrderRoll) ? "?" : lastOrderRoll, 25, TextAnchor.MiddleCenter, UiKit.Wood);
            UiKit.Place(die.rectTransform, 0.68f, 0, 1, 0, 12, 70, -24, 112);
            var roll = MenuWidgets.FrameButton(box, "LANZAR DADO", 28, RollOrderDie, new Color32(137, 83, 42, 255));
            UiKit.Place((RectTransform)roll.transform, 0.5f, 0, 0.5f, 0, -175, 20, 175, 80);
        }

        void ShowResults()
        {
            var box = Modal("Resultados del minijuego", 920, 600);
            var ev = session.LastEvaluation;
            var ids = new List<int>(ev.Keys); ids.Sort();
            for (int i = 0; i < ids.Count; i++)
            {
                int id = ids[i];
                var r = ev[id];
                float y = 392f - i * 74f;
                var row = MenuWidgets.FramePanel(box, new Color32(224, 207, 165, 255));
                MenuWidgets.Abs(row.rectTransform, 28, y, 864, 64);
                var accent = UiKit.Panel(row.transform, "ColorJugador", colors[id]);
                MenuWidgets.Abs(accent.rectTransform, 6, 6, 7, 52);
                AddPortrait(row.transform, id, 18, 6, 48, 52);
                var name = UiKit.Label(row.transform, names[id] + " · " + characterNames[id], 20, TextAnchor.MiddleLeft, UiKit.Ink);
                MenuWidgets.Abs(name.rectTransform, 78, 30, 430, 30);
                var details = UiKit.Label(row.transform, r.Correct + " correctos   ·   " + r.Wrong + " errores   ·   " + r.Missing + " sin pintar", 15, TextAnchor.MiddleLeft, UiKit.Wood);
                MenuWidgets.Abs(details.rectTransform, 78, 3, 500, 22);
                var score = UiKit.Label(row.transform, r.Score.ToString("0.0") + " pts", 27, TextAnchor.MiddleRight, UiKit.Ink);
                MenuWidgets.Abs(score.rectTransform, 620, 9, 220, 46);
            }
            var winnerName = new List<string>();
            foreach (var id in session.RoundWinners) winnerName.Add(names[id]);
            string winnerText = winnerName.Count == 0
                ? "Nadie puntuó en este desafío."
                : (winnerName.Count > 1 ? "EMPATE: " : "GANADOR: ") + string.Join(" · ", winnerName.ToArray());
            var winner = MenuWidgets.FramePanel(box, new Color32(137, 83, 42, 255));
            UiKit.Place(winner.rectTransform, 0, 0, 1, 0, 28, 78, -28, 132);
            var winnerLabel = UiKit.Label(winner.transform, winnerText, 24, TextAnchor.MiddleCenter, UiKit.Parchment);
            UiKit.Stretch(winnerLabel.rectTransform);
            var go = MenuWidgets.FrameButton(box, "CONTINUAR", 26, () => { pendingResults = false; Refresh(); }, new Color32(137, 83, 42, 255));
            UiKit.Place((RectTransform)go.transform, 0.5f, 0, 0.5f, 0, -110, 16, 110, 64);
        }

        void BeginSteal()
        {
            stealQueue = new List<KeyValuePair<int, int>>();
            selections = new Dictionary<int, IDictionary<int, int>>();
            var winners = new List<int>(session.RoundWinners);
            foreach (var w in winners)
            {
                selections[w] = new Dictionary<int, int>();
                foreach (var o in RowRobbery.Opponents(winners, session.Round)) stealQueue.Add(new KeyValuePair<int, int>(w, o));
            }
            stealIndex = 0;
            ShowSteal();
        }

        void ShowSteal()
        {
            UiKit.Clear(modalLayer);
            var pair = stealQueue[stealIndex];
            var grid = session.Round.GridOf(pair.Value);
            var box = Modal(names[pair.Key] + " roba a " + names[pair.Value], 940, 600);
            var hint = UiKit.Label(box, "Elige la fila de " + names[pair.Value] + " que quieres robar. Recuperas los colores de esa fila.", 22, TextAnchor.UpperLeft, UiKit.Ink);
            UiKit.Place(hint.rectTransform, 0, 1, 1, 1, 24, -120, -24, -64);

            var gridFrame = MenuWidgets.FramePanel(box, UiKit.ParchmentDark);
            MenuWidgets.Abs(gridFrame.rectTransform, 24, 24, 380, 420);
            var gridHolder = UiKit.Rect(gridFrame.transform, "Cuadricula");
            UiKit.Place(gridHolder, 0, 0, 1, 1, 10, 10, -10, -10);
            var layout = gridHolder.gameObject.AddComponent<GridLayoutGroup>();
            float cell = Mathf.Floor(Mathf.Min(360f / grid.Width, 400f / grid.Height));
            layout.cellSize = new Vector2(cell - 2f, cell - 2f);
            layout.spacing = new Vector2(1f, 1f);
            layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            layout.constraintCount = grid.Width;
            layout.childAlignment = TextAnchor.UpperLeft;
            for (int y = 0; y < grid.Height; y++)
                for (int x = 0; x < grid.Width; x++)
                {
                    int c = grid.Get(x, y);
                    var img = UiKit.Panel(gridHolder, "c", c >= 0 ? content.palette.entries[c].color : new Color32(0xD8, 0xC9, 0x9A, 255));
                    img.raycastTarget = false;
                }

            var rowsFrame = MenuWidgets.FramePanel(box, UiKit.ParchmentDark);
            MenuWidgets.Abs(rowsFrame.rectTransform, 424, 24, 492, 420);
            var viewport = UiKit.Rect(rowsFrame.transform, "FilasViewport");
            UiKit.Place(viewport, 0, 0, 1, 1, 8, 8, -8, -8);
            var viewportImage = viewport.gameObject.AddComponent<Image>();
            viewportImage.color = Color.white;
            viewportImage.raycastTarget = true;
            var mask = viewport.gameObject.AddComponent<Mask>();
            mask.showMaskGraphic = false;
            var scroll = rowsFrame.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.inertia = false;
            var rowsContent = UiKit.Rect(viewport, "Filas");
            float rowHeight = 38f;
            float rowSpacing = 4f;
            float contentHeight = Mathf.Max(404f, grid.Height * (rowHeight + rowSpacing) + 8f);
            UiKit.Place(rowsContent, 0, 1, 1, 1, 0, -contentHeight, 0, 0);
            scroll.content = rowsContent;

            // Una opcion por fila; el panel se desplaza si la plantilla tiene muchas filas.
            for (int row = 0; row < grid.Height; row++)
            {
                int r = row;
                var counts = grid.RowCounts(row, content.palette.Count);
                var sb = new StringBuilder("Fila " + (row + 1) + ":");
                int total = 0;
                for (int c = 0; c < counts.Length; c++)
                    if (counts[c] > 0) { sb.Append("  " + counts[c] + " " + content.palette.entries[c].name.ToLowerInvariant()); total += counts[c]; }
                if (total == 0) sb.Append("  (vacía)");
                var btn = MenuWidgets.FrameButton(rowsContent, sb.ToString(), 16, () => PickRow(r), new Color32(137, 83, 42, 255));
                float top = contentHeight - 4f - row * (rowHeight + rowSpacing);
                UiKit.Place((RectTransform)btn.transform, 0, 1, 1, 1, 4, top - rowHeight, -4, top);
            }
        }

        void PickRow(int row)
        {
            var pair = stealQueue[stealIndex];
            selections[pair.Key][pair.Value] = row;
            stealIndex++;
            if (stealIndex < stealQueue.Count) { ShowSteal(); return; }
            var chosen = selections;
            Act(() => session.ResolveRewards(chosen));
        }

        void ShowGameOver()
        {
            var box = Modal("Fin de la partida", 780, 460);
            var o = session.Outcome;
            if (o.Kind == OutcomeKind.WinByFinish || o.Kind == OutcomeKind.WinByElimination)
            {
                string winnerMessage = o.Kind == OutcomeKind.WinByFinish
                    ? names[o.WinnerId] + " llegó a la meta."
                    : names[o.WinnerId] + " gana: el resto quedó descalificado.";
                AddPortrait(box, o.WinnerId, 284, 138, 212, 254);
                var name = UiKit.Label(box, names[o.WinnerId] + " · " + characterNames[o.WinnerId], 23, TextAnchor.MiddleCenter, UiKit.Ink);
                MenuWidgets.Abs(name.rectTransform, 140, 96, 500, 35);
                var text = UiKit.Label(box, winnerMessage, 22, TextAnchor.MiddleCenter, UiKit.Wood);
                MenuWidgets.Abs(text.rectTransform, 72, 62, 636, 34);
            }
            else
            {
                var text = UiKit.Label(box, "Todos quedaron descalificados a la vez.\nPartida sin ganador.", 30, TextAnchor.MiddleCenter, UiKit.Ink);
                UiKit.Place(text.rectTransform, 0, 0, 1, 1, 24, 110, -24, -80);
            }
            var again = MenuWidgets.FrameButton(box, "JUGAR DE NUEVO", 24, () => SceneManager.LoadScene(SceneManager.GetActiveScene().name), new Color32(137, 83, 42, 255));
            UiKit.Place((RectTransform)again.transform, 0.5f, 0, 0.5f, 0, -360, 18, -12, 72);
            var menu = MenuWidgets.FrameButton(box, "MENÚ", 24, () => SceneManager.LoadScene("Menu"), new Color32(89, 55, 37, 255));
            UiKit.Place((RectTransform)menu.transform, 0.5f, 0, 0.5f, 0, 12, 18, 360, 72);
        }
    }
}
