using System;
using System.Collections;
using System.Collections.Generic;
using RobaColores.Map;
using UnityEngine;

namespace RobaColores.Game
{
    public sealed class BoardGameController : MonoBehaviour
    {
        const int LayoutWidth = 1920;
        const int LayoutHeight = 1080;
        const float DiceTimePenaltySeconds = 15f;
        const float TileNeighborDistance = 2.7f;

        enum TurnPhase
        {
            InitialRoll,
            Playing,
            ChoosingRoute,
            Moving,
            MiniGame,
            GameOver
        }

        static readonly Color[] PlayerColors =
        {
            new Color32(221, 78, 73, 255),
            new Color32(55, 145, 224, 255),
            new Color32(67, 174, 93, 255),
            new Color32(239, 172, 48, 255)
        };

        static readonly string[] CategoryNames =
        {
            "Partes del cuerpo",
            "Frutas y verduras",
            "Accesorios",
            "Animales",
            "Profesiones",
            "Deportes",
            "Astronomía"
        };

        sealed class Player
        {
            public PlayerSetup setup;
            public Texture2D portrait;
            public PathTile tile;
            public Transform token;
            public int? initialRoll;
        }

        readonly List<Player> players = new List<Player>();
        readonly Dictionary<PathTile, List<PathTile>> connections = new Dictionary<PathTile, List<PathTile>>();
        readonly Dictionary<PathTile, int> distanceToFinish = new Dictionary<PathTile, int>();
        readonly List<PathTile> availableRoutes = new List<PathTile>();
        readonly Queue<int> initialRollQueue = new Queue<int>();
        readonly List<int> initialRollParticipants = new List<int>();
        readonly List<Material> tokenMaterials = new List<Material>();

        PathTile startTile;
        PathTile finishTile;
        TurnPhase phase;
        int currentPlayer;
        int movesRemaining;
        int? chosenRoute;
        string message;
        string lastRoll;
        Font titleFont;
        GUIStyle titleStyle;
        GUIStyle eyebrowStyle;
        GUIStyle panelTitleStyle;
        GUIStyle headingStyle;
        GUIStyle playerHeadingStyle;
        GUIStyle bodyStyle;
        GUIStyle playerBodyStyle;
        GUIStyle smallStyle;
        GUIStyle playerSmallStyle;
        GUIStyle buttonStyle;
        Texture2D buttonTexture;
        Texture2D highlightedButtonTexture;

        void Start()
        {
            titleFont = Resources.Load<Font>("Fonts/PirataOne-Regular");
            if (!LoadBoard())
            {
                enabled = false;
                return;
            }

            var setups = LocalGameSetup.Players;
            for (var i = 0; i < setups.Length; i++)
            {
                var player = new Player
                {
                    setup = setups[i],
                    portrait = CharacterPortraitLibrary.GetPortrait(setups[i].characterIndex),
                    tile = startTile
                };
                player.token = CreateToken(i, setups.Length, player.setup.characterIndex, startTile.transform.position);
                players.Add(player);
            }

            phase = TurnPhase.InitialRoll;
            for (var i = 0; i < players.Count; i++)
                initialRollQueue.Enqueue(i);
            message = "Todos tiran el dado. El resultado más alto decide quién empieza.";
            CenterCameraOnStart();
        }

        bool LoadBoard()
        {
            var tiles = FindObjectsByType<PathTile>(FindObjectsSortMode.None);
            foreach (var tile in tiles)
            {
                if (tile.name.EndsWith("_S", StringComparison.Ordinal))
                    startTile = tile;
                else if (tile.name.EndsWith("_M", StringComparison.Ordinal))
                    finishTile = tile;
            }

            if (startTile == null || finishTile == null)
            {
                Debug.LogError("El mapa necesita una casilla INICIO (_S) y META (_M) para iniciar la partida.", this);
                message = "El mapa no tiene las casillas de inicio y meta.";
                return false;
            }

            foreach (var tile in tiles)
            {
                var neighbors = new List<PathTile>();
                foreach (var candidate in tiles)
                {
                    if (candidate == tile)
                        continue;

                    if (Vector3.Distance(tile.transform.position, candidate.transform.position) <= TileNeighborDistance)
                        neighbors.Add(candidate);
                }

                connections.Add(tile, neighbors);
            }

            CalculateDistanceToFinish();
            if (!distanceToFinish.ContainsKey(startTile))
            {
                Debug.LogError("No existe un camino conectado entre INICIO y META.", this);
                message = "No se encontró una ruta entre el inicio y la meta.";
                return false;
            }

            return true;
        }

        void CalculateDistanceToFinish()
        {
            var queue = new Queue<PathTile>();
            distanceToFinish[finishTile] = 0;
            queue.Enqueue(finishTile);

            while (queue.Count > 0)
            {
                var tile = queue.Dequeue();
                foreach (var neighbor in connections[tile])
                {
                    if (distanceToFinish.ContainsKey(neighbor))
                        continue;

                    distanceToFinish[neighbor] = distanceToFinish[tile] + 1;
                    queue.Enqueue(neighbor);
                }
            }
        }

        void CenterCameraOnStart()
        {
            var camera = Camera.main;
            if (camera == null)
                camera = FindFirstObjectByType<Camera>();
            if (camera == null)
            {
                Debug.LogError("No se encontró la cámara principal de MapaA.", this);
                return;
            }

            var boardCenter = new Vector3(32f, 0f, 24f);
            var direction = (startTile.transform.position - boardCenter).normalized;
            camera.transform.position += direction * 4f;
        }

        Transform CreateToken(int playerIndex, int playerCount, int characterIndex, Vector3 tilePosition)
        {
            var root = new GameObject("Ficha Jugador " + (playerIndex + 1));
            root.transform.position = GetTokenPosition(playerIndex, playerCount, tilePosition);

            var baseToken = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            baseToken.name = "Base";
            baseToken.transform.SetParent(root.transform, false);
            baseToken.transform.localPosition = new Vector3(0f, 0.13f, 0f);
            baseToken.transform.localScale = new Vector3(0.48f, 0.13f, 0.48f);
            baseToken.GetComponent<Collider>().enabled = false;
            SetTokenColor(baseToken, PlayerColors[playerIndex]);

            var figure = GameObject.CreatePrimitive(GetCharacterTokenShape(characterIndex));
            figure.name = "Personaje";
            figure.transform.SetParent(root.transform, false);
            figure.transform.localPosition = new Vector3(0f, 0.55f, 0f);
            figure.transform.localScale = Vector3.one * 0.43f;
            figure.GetComponent<Collider>().enabled = false;
            SetTokenColor(figure, PlayerColors[playerIndex]);
            return root.transform;
        }

        Vector3 GetTokenPosition(int playerIndex, int playerCount, Vector3 tilePosition)
        {
            var angle = Mathf.PI * 2f * playerIndex / playerCount;
            var offset = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * 0.36f;
            return tilePosition + Vector3.up * 1.08f + offset;
        }

        static PrimitiveType GetCharacterTokenShape(int characterIndex)
        {
            switch (characterIndex % 4)
            {
                case 0: return PrimitiveType.Capsule;
                case 1: return PrimitiveType.Cube;
                case 2: return PrimitiveType.Sphere;
                default: return PrimitiveType.Cylinder;
            }
        }

        void SetTokenColor(GameObject tokenPart, Color color)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
                shader = Shader.Find("Standard");

            var material = new Material(shader);
            if (material.HasProperty("_BaseColor"))
                material.SetColor("_BaseColor", color);
            else
                material.color = color;
            tokenMaterials.Add(material);
            tokenPart.GetComponent<Renderer>().material = material;
        }

        void OnGUI()
        {
            if (titleStyle == null)
                BuildStyles();

            var previousMatrix = GUI.matrix;
            var scale = Mathf.Min((float)Screen.width / LayoutWidth, (float)Screen.height / LayoutHeight);
            var offsetX = (Screen.width - LayoutWidth * scale) * 0.5f;
            var offsetY = (Screen.height - LayoutHeight * scale) * 0.5f;
            GUI.matrix = Matrix4x4.TRS(new Vector3(offsetX, offsetY, 0f), Quaternion.identity, Vector3.one * scale);

            DrawBackground();
            if (phase == TurnPhase.MiniGame)
            {
                GUI.matrix = previousMatrix;
                return;
            }

            DrawPlayerPanel();
            DrawTurnPanel();
            DrawRoutePanel();
            GUI.matrix = previousMatrix;
        }

        void DrawBackground()
        {
            var frame = new Color32(25, 39, 49, 205);
            var trim = new Color32(199, 150, 65, 220);
            DrawRect(new Rect(0, 0, LayoutWidth, 14), frame);
            DrawRect(new Rect(0, LayoutHeight - 14, LayoutWidth, 14), frame);
            DrawRect(new Rect(0, 14, 14, LayoutHeight - 28), frame);
            DrawRect(new Rect(LayoutWidth - 14, 14, 14, LayoutHeight - 28), frame);
            DrawRect(new Rect(14, 14, 180, 3), trim);
            DrawRect(new Rect(LayoutWidth - 194, 14, 180, 3), trim);
            DrawRect(new Rect(14, LayoutHeight - 17, 180, 3), trim);
            DrawRect(new Rect(LayoutWidth - 194, LayoutHeight - 17, 180, 3), trim);
        }

        void DrawPlayerPanel()
        {
            const float x = 26f;
            const float y = 26f;
            const float width = 520f;
            const float headerHeight = 48f;
            const float rowHeight = 82f;
            var panel = new Rect(x, y, width, headerHeight + players.Count * rowHeight + 14f);
            DrawFrame(panel, new Color32(226, 207, 165, 250));
            GUI.Label(new Rect(x + 18, y + 7, width - 36, 34), "TRIPULACIÓN", panelTitleStyle);
            DrawRect(new Rect(x + 18, y + headerHeight - 1, width - 36, 2), new Color32(177, 125, 53, 230));

            for (var i = 0; i < players.Count; i++)
            {
                var player = players[i];
                var rect = new Rect(x + 12, y + headerHeight + i * rowHeight, width - 24, rowHeight - 6);
                bool active = i == currentPlayer && phase != TurnPhase.InitialRoll;
                DrawFrame(rect, active
                    ? new Color32(255, 237, 185, 255)
                    : new Color32(238, 224, 193, 250));
                DrawRect(new Rect(rect.x + 8, rect.y + 8, 6, rect.height - 16), PlayerColors[i]);
                var portraitRect = new Rect(rect.x + 23, rect.y + 10, 58, 58);
                DrawRect(new Rect(portraitRect.x - 3, portraitRect.y - 3, portraitRect.width + 6, portraitRect.height + 6),
                    new Color32(70, 43, 26, 255));
                if (player.portrait != null)
                    GUI.DrawTexture(portraitRect, player.portrait, ScaleMode.ScaleToFit);

                var infoX = portraitRect.xMax + 15;
                var infoWidth = rect.xMax - infoX - 100;
                GUI.Label(new Rect(infoX, rect.y + 7, infoWidth, 30),
                    "J" + player.setup.playerNumber + "  ·  " + player.setup.characterName, playerHeadingStyle);
                var tileName = player.tile.name;
                var location = tileName.EndsWith("_S", StringComparison.Ordinal) ? "INICIO" :
                    tileName.EndsWith("_M", StringComparison.Ordinal) ? "META" :
                    CategoryFor(player.tile.Color);
                var progress = distanceToFinish[startTile] - distanceToFinish[player.tile];
                GUI.Label(new Rect(infoX, rect.y + 38, infoWidth, 22),
                    location + "  ·  Avance " + progress + "/" + distanceToFinish[startTile], playerBodyStyle);
                var barRect = new Rect(infoX, rect.y + 65, infoWidth, 5);
                DrawRect(barRect, new Color32(106, 76, 47, 150));
                var fraction = distanceToFinish[startTile] == 0 ? 0f :
                    (float)progress / distanceToFinish[startTile];
                DrawRect(new Rect(barRect.x, barRect.y, barRect.width * Mathf.Clamp01(fraction), barRect.height),
                    PlayerColors[i]);

                int inventoryTotal = 0;
                if (player.setup.pixelInventory != null)
                    foreach (var count in player.setup.pixelInventory)
                        inventoryTotal += count;
                GUI.Label(new Rect(rect.xMax - 94, rect.y + 26, 82, 28), inventoryTotal + " u", playerSmallStyle);
                if (phase == TurnPhase.InitialRoll)
                {
                    var result = player.initialRoll.HasValue ? player.initialRoll.Value.ToString() : "—";
                    GUI.Label(new Rect(rect.xMax - 94, rect.y + 48, 82, 22), "Dado: " + result, playerSmallStyle);
                }
                else if (player.setup.miniGameTimePenaltySeconds > 0f)
                {
                    GUI.Label(new Rect(rect.xMax - 94, rect.y + 48, 82, 22),
                        "+" + Mathf.CeilToInt(player.setup.miniGameTimePenaltySeconds) + " s", playerSmallStyle);
                }
            }
        }

        void DrawTurnPanel()
        {
            var header = new Rect(650, 26, 620, 104);
            DrawFrame(header, new Color32(91, 54, 32, 255));
            GUI.Label(new Rect(header.x + 20, header.y + 9, header.width - 40, 26), "ROBA COLORES", eyebrowStyle);
            var activeName = phase == TurnPhase.InitialRoll ? "ORDEN DE SALIDA" :
                phase == TurnPhase.GameOver ? "¡TENEMOS GANADOR!" :
                "TURNO DE " + players[currentPlayer].setup.characterName.ToUpperInvariant();
            GUI.Label(new Rect(header.x + 18, header.y + 35, header.width - 36, 55), activeName, titleStyle);

            var logPanel = new Rect(1380, 900, 510, 150);
            DrawFrame(logPanel, new Color32(231, 215, 179, 250));
            GUI.Label(new Rect(logPanel.x + 20, logPanel.y + 12, logPanel.width - 40, 28), "BITÁCORA", panelTitleStyle);
            DrawRect(new Rect(logPanel.x + 20, logPanel.y + 42, logPanel.width - 40, 2),
                new Color32(177, 125, 53, 230));
            GUI.Label(new Rect(logPanel.x + 22, logPanel.y + 52, logPanel.width - 44, 82),
                message, bodyStyle);

            if (phase == TurnPhase.InitialRoll)
            {
                var next = initialRollQueue.Peek();
                var label = "TIRAR DADO · JUGADOR " + players[next].setup.playerNumber;
                if (GUI.Button(new Rect(690, 954, 540, 98), label, buttonStyle))
                    RollInitialDie();
            }
            else if (phase == TurnPhase.Playing)
            {
                if (GUI.Button(new Rect(690, 954, 540, 98), "TIRAR DADO  ·  ESPACIO", buttonStyle))
                    RollTurnDie();
            }
            else if (phase == TurnPhase.GameOver && GUI.Button(new Rect(690, 954, 540, 98), "VOLVER AL MENÚ", buttonStyle))
            {
                UnityEngine.SceneManagement.SceneManager.LoadScene("Menu");
            }
        }

        void DrawRoutePanel()
        {
            if (phase != TurnPhase.ChoosingRoute)
                return;

            var panel = new Rect(1390, 206, 500, 430);
            DrawFrame(panel, new Color32(231, 215, 179, 250));
            GUI.Label(new Rect(panel.x + 22, panel.y + 18, panel.width - 44, 48), "ELIGE TU CAMINO", panelTitleStyle);
            GUI.Label(new Rect(panel.x + 28, panel.y + 68, panel.width - 56, 56),
                "Selecciona la casilla por la que quieres avanzar.", bodyStyle);

            var visibleOptions = Mathf.Min(availableRoutes.Count, 5);
            for (var i = 0; i < visibleOptions; i++)
            {
                var option = availableRoutes[i];
                var label = "Casilla " + CategoryFor(option.Color);
                if (GUI.Button(new Rect(panel.x + 30, panel.y + 132 + i * 58, panel.width - 60, 50), label, buttonStyle))
                {
                    chosenRoute = i;
                    phase = TurnPhase.Moving;
                }
            }
        }

        void RollInitialDie()
        {
            var playerIndex = initialRollQueue.Dequeue();
            initialRollParticipants.Add(playerIndex);
            var result = UnityEngine.Random.Range(1, 7);
            players[playerIndex].initialRoll = result;
            message = players[playerIndex].setup.characterName + " sacó " + result + ".";
            lastRoll = result.ToString();
            if (initialRollQueue.Count == 0)
                ResolveInitialRolls();
        }

        void ResolveInitialRolls()
        {
            var highest = -1;
            var winners = new List<int>();
            foreach (var i in initialRollParticipants)
            {
                var roll = players[i].initialRoll.GetValueOrDefault();
                if (roll > highest)
                {
                    highest = roll;
                    winners.Clear();
                    winners.Add(i);
                }
                else if (roll == highest)
                {
                    winners.Add(i);
                }
            }
            initialRollParticipants.Clear();

            if (winners.Count == 1)
            {
                currentPlayer = winners[0];
                BeginFirstTurn();
                return;
            }

            foreach (var playerIndex in winners)
            {
                players[playerIndex].initialRoll = null;
                initialRollQueue.Enqueue(playerIndex);
            }

            message = "Empate entre " + winners.Count + " jugadores. Solo ellos vuelven a tirar.";
            lastRoll = string.Empty;
        }

        void BeginFirstTurn()
        {
            phase = TurnPhase.Playing;
            message = "Empieza " + players[currentPlayer].setup.characterName + ". Tira el dado.";
            lastRoll = string.Empty;
        }

        void RollTurnDie()
        {
            var face = UnityEngine.Random.Range(0, 6);

            switch (face)
            {
                case 0:
                case 1:
                case 2:
                case 3:
                    movesRemaining = face + 1;
                    lastRoll = "AVANZAS " + movesRemaining;
                    message = players[currentPlayer].setup.characterName + " avanza " + movesRemaining + " casillas.";
                    phase = TurnPhase.Moving;
                    StartCoroutine(MovePlayer());
                    break;
                case 4:
                    lastRoll = "PIERDES TURNO";
                    message = players[currentPlayer].setup.characterName + " pierde su turno.";
                    StartCoroutine(EndTurnAfterNotice());
                    break;
                default:
                    LocalGameSetup.AddMiniGameTimePenalty(currentPlayer, DiceTimePenaltySeconds);
                    lastRoll = "PIERDES TIEMPO";
                    message = "Se suman " + DiceTimePenaltySeconds + " segundos a la próxima partida de pixel art.";
                    StartCoroutine(EndTurnAfterNotice());
                    break;
            }
        }

        IEnumerator EndTurnAfterNotice()
        {
            phase = TurnPhase.Moving;
            yield return new WaitForSeconds(1.5f);
            var resultMessage = message;
            AdvanceTurn(resultMessage);
        }

        IEnumerator MovePlayer()
        {
            var player = players[currentPlayer];
            while (movesRemaining > 0)
            {
                var nextTiles = GetForwardRoutes(player.tile);
                if (nextTiles.Count == 0)
                {
                    FinishGame(currentPlayer);
                    yield break;
                }

                PathTile nextTile;
                if (nextTiles.Count > 1)
                {
                    availableRoutes.Clear();
                    availableRoutes.AddRange(nextTiles);
                    chosenRoute = null;
                    phase = TurnPhase.ChoosingRoute;
                    message = "Tu camino se bifurca. Elige por dónde avanzar.";
                    yield return new WaitUntil(() => chosenRoute.HasValue || phase == TurnPhase.GameOver);
                    if (phase == TurnPhase.GameOver)
                        yield break;

                    nextTile = availableRoutes[chosenRoute.Value];
                    phase = TurnPhase.Moving;
                    availableRoutes.Clear();
                }
                else
                {
                    nextTile = nextTiles[0];
                }

                var destination = GetTokenPosition(currentPlayer, players.Count, nextTile.transform.position);
                var start = player.token.position;
                const float duration = 0.32f;
                var elapsed = 0f;
                while (elapsed < duration)
                {
                    elapsed += Time.deltaTime;
                    player.token.position = Vector3.Lerp(start, destination, Mathf.Clamp01(elapsed / duration));
                    yield return null;
                }

                player.token.position = destination;
                player.tile = nextTile;
                movesRemaining--;
                if (player.tile == finishTile)
                {
                    FinishGame(currentPlayer);
                    yield break;
                }
            }

            ArriveAtTile(player);
        }

        List<PathTile> GetForwardRoutes(PathTile currentTile)
        {
            var result = new List<PathTile>();
            if (!connections.TryGetValue(currentTile, out var neighbors) ||
                !distanceToFinish.TryGetValue(currentTile, out var currentDistance))
                return result;

            foreach (var neighbor in neighbors)
            {
                if (distanceToFinish.TryGetValue(neighbor, out var neighborDistance) &&
                    neighborDistance == currentDistance - 1)
                    result.Add(neighbor);
            }

            result.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
            return result;
        }

        void ArriveAtTile(Player player)
        {
            phase = TurnPhase.MiniGame;
            message = "Todos juegan el mismo pixel art de " + CategoryFor(player.tile.Color) + ".";
            var minigame = gameObject.AddComponent<PixelArtMinigame>();
            minigame.Begin(player.tile.Color, LocalGameSetup.Players, OnMiniGameCompleted);
        }

        void OnMiniGameCompleted(int[] winners, bool eliminationVictory)
        {
            var updatedSetups = LocalGameSetup.Players;
            for (var i = 0; i < players.Count; i++)
                players[i].setup = updatedSetups[i];

            if (winners.Length == 0)
            {
                Debug.LogError("El minijuego terminó sin seleccionar ningún ganador.", this);
                phase = TurnPhase.Playing;
                message = "No se pudo determinar un ganador. Tira el dado para continuar.";
                return;
            }

            if (eliminationVictory)
            {
                FinishGame(winners[0]);
                return;
            }

            if (winners.Length == 1)
            {
                message = players[winners[0]].setup.characterName + " ganó el reto y robó una fila de píxeles.";
            }
            else
            {
                var winnerNames = new List<string>();
                foreach (var winner in winners)
                    winnerNames.Add(players[winner].setup.characterName);
                message = "Empate: " + string.Join(", ", winnerNames) + " comparten las filas robadas.";
            }

            AdvanceTurn();
        }

        string CategoryFor(PathTileColor color)
        {
            var index = Mathf.Clamp((int)color, 0, CategoryNames.Length - 1);
            return CategoryNames[index];
        }

        void AdvanceTurn(string resultMessage = null)
        {
            currentPlayer = (currentPlayer + 1) % players.Count;
            lastRoll = string.Empty;
            phase = TurnPhase.Playing;
            var resultPrefix = string.IsNullOrEmpty(resultMessage) ? string.Empty : resultMessage + " ";
            message = resultPrefix + "Turno de " + players[currentPlayer].setup.characterName + ". Tira el dado.";
        }

        void FinishGame(int winnerIndex)
        {
            StopAllCoroutines();
            currentPlayer = winnerIndex;
            phase = TurnPhase.GameOver;
            lastRoll = "¡TESORO!";
            message = players[winnerIndex].setup.characterName + " llegó a la meta y ganó la partida.";
        }

        void BuildStyles()
        {
            buttonTexture = MakeWoodTexture(new Color32(112, 65, 36, 255));
            highlightedButtonTexture = MakeWoodTexture(new Color32(157, 101, 48, 255));

            titleStyle = new GUIStyle(GUI.skin.label)
            {
                font = titleFont,
                fontSize = 38,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color32(248, 225, 174, 255) }
            };
            eyebrowStyle = new GUIStyle(GUI.skin.label)
            {
                font = titleFont,
                fontSize = 21,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color32(241, 199, 110, 255) }
            };
            panelTitleStyle = new GUIStyle(GUI.skin.label)
            {
                font = titleFont,
                fontSize = 27,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color32(86, 51, 28, 255) }
            };
            headingStyle = new GUIStyle(GUI.skin.label)
            {
                font = titleFont,
                fontSize = 32,
                alignment = TextAnchor.MiddleCenter,
                wordWrap = true,
                normal = { textColor = new Color32(56, 39, 34, 255) }
            };
            playerHeadingStyle = new GUIStyle(headingStyle)
            {
                fontSize = 23,
                alignment = TextAnchor.MiddleLeft,
                wordWrap = false
            };
            bodyStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 21,
                alignment = TextAnchor.MiddleLeft,
                wordWrap = true,
                normal = { textColor = new Color32(56, 39, 34, 255) }
            };
            playerBodyStyle = new GUIStyle(bodyStyle)
            {
                fontSize = 17,
                wordWrap = false
            };
            smallStyle = new GUIStyle(bodyStyle) { fontSize = 18 };
            playerSmallStyle = new GUIStyle(smallStyle)
            {
                fontSize = 16,
                alignment = TextAnchor.MiddleCenter,
                wordWrap = false
            };
            buttonStyle = new GUIStyle(GUI.skin.button)
            {
                font = titleFont != null ? titleFont : GUI.skin.button.font,
                fontSize = 31,
                alignment = TextAnchor.MiddleCenter,
                normal = { background = buttonTexture, textColor = new Color32(255, 238, 196, 255) },
                hover = { background = highlightedButtonTexture, textColor = Color.white },
                active = { background = highlightedButtonTexture, textColor = Color.white },
                border = new RectOffset(8, 8, 8, 8),
                padding = new RectOffset(18, 18, 8, 8),
                wordWrap = true
            };
        }

        static Texture2D MakeWoodTexture(Color32 baseColor)
        {
            const int size = 64;
            var pixels = new Color32[size * size];
            for (var y = 0; y < size; y++)
                for (var x = 0; x < size; x++)
                {
                    var grain = (Mathf.PerlinNoise(x * 0.075f, y * 0.22f) - 0.5f) * 28f;
                    var plank = y % 16;
                    var seam = plank < 2 || plank == 15;
                    var edge = x < 2 || x >= size - 2;
                    var shade = seam ? -35f : edge ? 20f : grain;
                    pixels[y * size + x] = new Color32(
                        (byte)Mathf.Clamp(baseColor.r + shade, 0, 255),
                        (byte)Mathf.Clamp(baseColor.g + shade * 0.7f, 0, 255),
                        (byte)Mathf.Clamp(baseColor.b + shade * 0.45f, 0, 255),
                        255);
                }
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            texture.SetPixels32(pixels);
            texture.Apply();
            return texture;
        }

        static void DrawFrame(Rect rect, Color fill)
        {
            DrawRect(new Rect(rect.x + 7, rect.y + 8, rect.width, rect.height), new Color32(12, 20, 27, 180));
            DrawRect(rect, new Color32(53, 32, 19, 255));
            DrawRect(new Rect(rect.x + 4, rect.y + 4, rect.width - 8, rect.height - 8),
                new Color32(202, 151, 67, 255));
            DrawRect(new Rect(rect.x + 8, rect.y + 8, rect.width - 16, rect.height - 16), fill);
            DrawRect(new Rect(rect.x + 9, rect.y + 9, rect.width - 18, 2),
                new Color(1f, 0.92f, 0.72f, 0.28f));
        }

        static void DrawRect(Rect rect, Color color)
        {
            var previousColor = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = previousColor;
        }

        void OnDestroy()
        {
            if (buttonTexture != null)
                Destroy(buttonTexture);
            if (highlightedButtonTexture != null)
                Destroy(highlightedButtonTexture);
            foreach (var material in tokenMaterials)
            {
                if (material != null)
                    Destroy(material);
            }
        }
    }
}
