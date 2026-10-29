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
        Image hudPanel;
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
            UiKit.Place(hudPanel.rectTransform, 0, 1, 0, 1, 8, -(24 + 32 * rows), 528, -8);
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
            material.SetVector("_ArtworkUVRect", new Vector4(0.15f, 0f, 0.7f, 1f));
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
            playersText = UiKit.Label(t, "", 22, TextAnchor.UpperLeft, UiKit.Parchment);
            UiKit.Place(playersText.rectTransform, 0, 1, 0, 1, 16, -190, 520, -12);
            hudPanel = UiKit.Panel(t, "FondoJugadores", new Color(0, 0, 0, 0.45f));
            UiKit.Place(hudPanel.rectTransform, 0, 1, 0, 1, 8, -196, 528, -8);
            hudPanel.transform.SetAsFirstSibling();

            banner = UiKit.Label(t, "", 34, TextAnchor.MiddleCenter, UiKit.Gold);
            UiKit.Place(banner.rectTransform, 0.5f, 1, 0.5f, 1, -300, -64, 300, -10);

            logText = UiKit.Label(t, "", 17, TextAnchor.LowerRight, UiKit.Parchment);
            UiKit.Place(logText.rectTransform, 1, 0, 1, 0, -480, 12, -12, 190);

            actionBar = UiKit.Rect(t, "Acciones");
            UiKit.Place(actionBar, 0.5f, 0, 0.5f, 0, -230, 18, 230, 150);
            rollButton = UiKit.MakeButton(actionBar, "Tirar dado de movimiento", UiKit.WoodLight, 28, RollDice);
            UiKit.Place((RectTransform)rollButton.transform, 0, 0, 1, 0, 0, 0, 0, 64);

            pathPanel = UiKit.Rect(t, "Caminos");
            UiKit.Place(pathPanel, 0.5f, 0, 0.5f, 0, -230, 18, 230, 260);

            modalLayer = UiKit.Rect(t, "Modales");
            UiKit.Stretch(modalLayer);
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
            var sb = new StringBuilder();
            foreach (var p in session.Players)
            {
                string hex = ColorUtility.ToHtmlStringRGB(colors[p.Id]);
                bool current = session.Phase == GamePhase.AwaitingOrderRoll
                    ? session.CurrentOrderPlayerId == p.Id
                    : session.Phase != GamePhase.MinigameRunning && session.Phase != GamePhase.GameOver &&
                      session.CurrentPlayer != null && session.CurrentPlayer.Id == p.Id;
                string status = p.Disqualified ? " DESCALIFICADO" : p.SkipNextTurn ? " (pierde turno)" : p.PendingTimePenalty > 0f ? " (penalización)" : "";
                sb.AppendLine((current ? "> " : "   ") + "<color=#" + hex + ">" + p.Name + "</color> " + characterNames[p.Id] + "  " + p.Inventory.Total + " u." + status);
            }
            playersText.text = sb.ToString();
            logText.text = string.Join("\n", log.ToArray()) + (lastRoll.Length > 0 && session.Phase == GamePhase.AwaitingMoveRoll ? "" : "");
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
            var title = UiKit.Label(pathPanel, "Elige tu camino", 26, TextAnchor.MiddleCenter, UiKit.Gold);
            UiKit.Place(title.rectTransform, 0, 1, 1, 1, 0, -40, 0, 0);
            float y = -52f;
            foreach (var opt in session.PathOptions)
            {
                int id = opt;
                var tile = session.Board.Get(id);
                var cat = session.Config.CategoryByColor[tile.Color];
                var btn = UiKit.MakeButton(pathPanel, "Casilla " + GameText.ColorName(tile.Color) + " (" + GameText.CategoryName(cat) + ")", UiKit.WoodLight, 22, () => ChoosePath(id));
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
            var box = UiKit.Panel(modalLayer, "Ventana", UiKit.Parchment);
            UiKit.Place(box.rectTransform, 0.5f, 0.5f, 0.5f, 0.5f, -w / 2, -h / 2, w / 2, h / 2);
            var head = UiKit.Panel(box.transform, "Titulo", UiKit.Wood);
            UiKit.Place(head.rectTransform, 0, 1, 1, 1, 0, -56, 0, 0);
            var t = UiKit.Label(head.transform, title, 34, TextAnchor.MiddleCenter, UiKit.Parchment);
            UiKit.Stretch(t.rectTransform);
            return box.rectTransform;
        }

        void ShowOrder()
        {
            var box = Modal("Orden de turnos", 900, 640);
            // Izquierda: las tiradas del dado de orden (con los desempates); derecha: el orden final.
            var sb = new StringBuilder();
            var rounds = session.OrderResult.Rounds;
            for (int r = 0; r < rounds.Count; r++)
            {
                sb.AppendLine(r == 0 ? "Tirada inicial:" : "Desempate:");
                foreach (var kv in rounds[r]) sb.AppendLine("   " + names[kv.Key] + ": " + kv.Value);
            }
            var left = UiKit.Label(box, sb.ToString(), 22, TextAnchor.UpperLeft, UiKit.Ink);
            UiKit.Place(left.rectTransform, 0, 0, 0.5f, 1, 28, 90, -10, -70);
            var order = new StringBuilder("Orden de juego:\n");
            for (int i = 0; i < session.TurnOrder.Count; i++) order.AppendLine("   " + (i + 1) + ". " + names[session.TurnOrder[i]] + " (" + characterNames[session.TurnOrder[i]] + ")");
            var text = UiKit.Label(box, order.ToString(), 24, TextAnchor.UpperLeft, UiKit.Ink);
            UiKit.Place(text.rectTransform, 0.5f, 0, 1, 1, 10, 90, -24, -70);
            var go = UiKit.MakeButton(box, "COMENZAR", UiKit.WoodLight, 28, () => { orderPending = false; Refresh(); });
            UiKit.Place((RectTransform)go.transform, 0.5f, 0, 0.5f, 0, -110, 16, 110, 64);
        }

        void ShowOrderRoll()
        {
            var box = Modal("¿Quién comienza?", 900, 640);
            int playerId = session.CurrentOrderPlayerId.Value;
            var prompt = UiKit.Label(box,
                "Turno de tirada: " + names[playerId] + "\n" + characterNames[playerId],
                34, TextAnchor.MiddleCenter, colors[playerId]);
            UiKit.Place(prompt.rectTransform, 0, 1, 1, 1, 24, -152, -24, -72);

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
            var text = UiKit.Label(box, history.ToString(), 22, TextAnchor.UpperLeft, UiKit.Ink);
            UiKit.Place(text.rectTransform, 0, 0, 0.62f, 1, 28, 100, -12, -170);

            var die = UiKit.Label(box, string.IsNullOrEmpty(lastOrderRoll) ? "?" : lastOrderRoll, 34, TextAnchor.MiddleCenter, UiKit.Wood);
            UiKit.Place(die.rectTransform, 0.64f, 0, 1, 1, 12, 118, -24, -180);
            var roll = UiKit.MakeButton(box, "LANZAR DADO", UiKit.WoodLight, 30, RollOrderDie);
            UiKit.Place((RectTransform)roll.transform, 0.5f, 0, 0.5f, 0, -175, 20, 175, 80);
        }

        void ShowResults()
        {
            var box = Modal("Resultados del minijuego", 860, 520);
            var sb = new StringBuilder();
            var ev = session.LastEvaluation;
            var ids = new List<int>(ev.Keys); ids.Sort();
            foreach (var id in ids)
            {
                var r = ev[id];
                sb.AppendLine(names[id] + ":  " + r.Score.ToString("0.0") + " pts   (" + r.Correct + " correctos, " + r.Wrong + " errores, " + r.Missing + " sin pintar)");
            }
            sb.AppendLine();
            if (session.RoundWinners.Count == 0) sb.AppendLine("Nadie puntuó: no hay ganador ni robo.");
            else
            {
                var w = new List<string>();
                foreach (var id in session.RoundWinners) w.Add(names[id]);
                sb.AppendLine((w.Count > 1 ? "Empate. Ganadores: " : "Ganador: ") + string.Join(", ", w.ToArray()));
            }
            var text = UiKit.Label(box, sb.ToString(), 24, TextAnchor.UpperLeft, UiKit.Ink);
            UiKit.Place(text.rectTransform, 0, 0, 1, 1, 24, 80, -24, -70);
            var go = UiKit.MakeButton(box, "CONTINUAR", UiKit.WoodLight, 28, () => { pendingResults = false; Refresh(); });
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

            // Cuadricula final del oponente (solo lectura).
            var gridHolder = UiKit.Rect(box, "Cuadricula");
            UiKit.Place(gridHolder, 0, 0, 0, 0, 24, 24, 404, 444);
            var layout = gridHolder.gameObject.AddComponent<GridLayoutGroup>();
            float cell = Mathf.Floor(Mathf.Min(380f / grid.Width, 420f / grid.Height));
            layout.cellSize = new Vector2(cell - 1f, cell - 1f);
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

            // Un boton por fila, con el resumen de colores que aporta.
            float by = 444f;
            for (int row = 0; row < grid.Height; row++)
            {
                int r = row;
                var counts = grid.RowCounts(row, content.palette.Count);
                var sb = new StringBuilder("Fila " + (row + 1) + ":");
                int total = 0;
                for (int c = 0; c < counts.Length; c++)
                    if (counts[c] > 0) { sb.Append("  " + counts[c] + " " + content.palette.entries[c].name.ToLowerInvariant()); total += counts[c]; }
                if (total == 0) sb.Append("  (vacía)");
                var btn = UiKit.MakeButton(box, sb.ToString(), UiKit.WoodLight, 18, () => PickRow(r));
                float height = Mathf.Min(40f, 410f / grid.Height - 4f);
                UiKit.Place((RectTransform)btn.transform, 0, 0, 1, 0, 424, by - height, -24, by);
                by -= height + 4f;
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
            var box = Modal("Fin de la partida", 700, 380);
            string message;
            var o = session.Outcome;
            if (o.Kind == OutcomeKind.WinByFinish) message = names[o.WinnerId] + " llegó a la meta y gana la partida.";
            else if (o.Kind == OutcomeKind.WinByElimination) message = names[o.WinnerId] + " gana: todos los demás quedaron descalificados.";
            else message = "Todos quedaron descalificados a la vez.\nPartida sin ganador.";
            var text = UiKit.Label(box, message, 30, TextAnchor.MiddleCenter, UiKit.Ink);
            UiKit.Place(text.rectTransform, 0, 0, 1, 1, 24, 90, -24, -70);
            var again = UiKit.MakeButton(box, "JUGAR DE NUEVO", UiKit.WoodLight, 26, () => SceneManager.LoadScene(SceneManager.GetActiveScene().name));
            UiKit.Place((RectTransform)again.transform, 0.5f, 0, 0.5f, 0, -330, 20, -10, 70);
            var menu = UiKit.MakeButton(box, "MENÚ", UiKit.Wood, 26, () => SceneManager.LoadScene("Menu"));
            UiKit.Place((RectTransform)menu.transform, 0.5f, 0, 0.5f, 0, 10, 20, 330, 70);
        }
    }
}
