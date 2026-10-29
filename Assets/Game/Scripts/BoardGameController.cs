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
        GUIStyle headingStyle;
        GUIStyle playerHeadingStyle;
        GUIStyle bodyStyle;
        GUIStyle playerBodyStyle;
        GUIStyle smallStyle;
        GUIStyle playerSmallStyle;
        GUIStyle buttonStyle;
        Texture2D backgroundTexture;
        Texture2D panelTexture;
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
            GUI.DrawTexture(new Rect(0, 0, LayoutWidth, LayoutHeight), backgroundTexture);
            DrawRect(new Rect(26, 26, LayoutWidth - 52, 136), new Color32(24, 29, 47, 218));
        }

        void DrawPlayerPanel()
        {
            const float width = 405f;
            const float startY = 190f;
            for (var i = 0; i < players.Count; i++)
            {
                var player = players[i];
                var rect = new Rect(28, startY + i * 158, width, 138);
                DrawRect(rect, i == currentPlayer && phase != TurnPhase.InitialRoll
                    ? new Color32(248, 226, 180, 238)
                    : new Color32(232, 219, 188, 226));
                DrawRect(new Rect(rect.x, rect.y, 12, rect.height), PlayerColors[i]);
                var portraitRect = new Rect(rect.x + 25, rect.y + 25, 88, 88);
                DrawRect(portraitRect, new Color32(56, 39, 34, 255));
                if (player.portrait != null)
                    GUI.DrawTexture(portraitRect, player.portrait, ScaleMode.ScaleToFit);

                var infoX = portraitRect.xMax + 16;
                var infoWidth = rect.xMax - infoX - 12;
                GUI.Label(new Rect(infoX, rect.y + 8, infoWidth, 42),
                    "J" + (i + 1) + "  " + player.setup.characterName, playerHeadingStyle);

                var tileName = player.tile.name;
                GUI.Label(new Rect(infoX, rect.y + 53, infoWidth, 36),
                    tileName.EndsWith("_S", StringComparison.Ordinal) ? "INICIO" :
                    tileName.EndsWith("_M", StringComparison.Ordinal) ? "META" :
                    CategoryFor(player.tile.Color), playerBodyStyle);

                if (phase == TurnPhase.InitialRoll)
                {
                    var result = player.initialRoll.HasValue ? player.initialRoll.Value.ToString() : "—";
                    GUI.Label(new Rect(infoX, rect.y + 92, infoWidth, 32), "Tirada: " + result, playerSmallStyle);
                }
                else
                {
                    var penalty = player.setup.miniGameTimePenaltySeconds;
                    var penaltyText = penalty > 0f ? "  ·  +" + Mathf.CeilToInt(penalty) + " s" : "";
                    GUI.Label(new Rect(infoX, rect.y + 92, infoWidth, 32),
                        "Avance: " + (distanceToFinish[startTile] - distanceToFinish[player.tile]) +
                        " / " + distanceToFinish[startTile] + penaltyText, playerSmallStyle);
                }
            }
        }

        void DrawTurnPanel()
        {
            GUI.Label(new Rect(475, 48, 980, 70), "ROBA COLORES", titleStyle);
            var activeName = phase == TurnPhase.InitialRoll ? "ORDEN DE SALIDA" :
                phase == TurnPhase.GameOver ? "¡TENEMOS GANADOR!" :
                "TURNO DE JUGADOR " + (currentPlayer + 1);
            GUI.Label(new Rect(500, 106, 920, 42), activeName, bodyStyle);

            var panel = new Rect(620, 742, 680, 248);
            GUI.DrawTexture(panel, panelTexture);
            GUI.Label(new Rect(panel.x + 24, panel.y + 12, panel.width - 48, 90), message, headingStyle);

            if (phase == TurnPhase.InitialRoll)
            {
                var next = initialRollQueue.Peek();
                var label = "JUGADOR " + (next + 1) + " · TIRAR";
                if (GUI.Button(new Rect(panel.x + 190, panel.y + 144, 300, 74), label, buttonStyle))
                    RollInitialDie();
            }
            else if (phase == TurnPhase.Playing)
            {
                var label = lastRoll.Length == 0 ? "TIRAR DADO" : "TIRAR DADO";
                if (GUI.Button(new Rect(panel.x + 190, panel.y + 144, 300, 74), label, buttonStyle))
                    RollTurnDie();
            }
            else if (phase == TurnPhase.Moving)
            {
                GUI.Label(new Rect(panel.x + 24, panel.y + 150, panel.width - 48, 52),
                    "Casillas por avanzar: " + movesRemaining, bodyStyle);
            }
            else if (phase == TurnPhase.GameOver && GUI.Button(new Rect(panel.x + 190, panel.y + 144, 300, 74), "VOLVER AL MENÚ", buttonStyle))
            {
                UnityEngine.SceneManagement.SceneManager.LoadScene("SampleScene");
            }

            if (!string.IsNullOrEmpty(lastRoll))
                GUI.Label(new Rect(760, 620, 420, 78), lastRoll, headingStyle);
        }

        void DrawRoutePanel()
        {
            if (phase != TurnPhase.ChoosingRoute)
                return;

            var panel = new Rect(1360, 190, 520, 440);
            GUI.DrawTexture(panel, panelTexture);
            GUI.Label(new Rect(panel.x + 24, panel.y + 18, panel.width - 48, 72), "ELIGE TU CAMINO", headingStyle);
            GUI.Label(new Rect(panel.x + 24, panel.y + 94, panel.width - 48, 52), "Puedes avanzar por cualquiera de estas rutas:", bodyStyle);

            var visibleOptions = Mathf.Min(availableRoutes.Count, 5);
            for (var i = 0; i < visibleOptions; i++)
            {
                var option = availableRoutes[i];
                var label = "Casilla " + CategoryFor(option.Color);
                if (GUI.Button(new Rect(panel.x + 54, panel.y + 160 + i * 56, panel.width - 108, 48), label, buttonStyle))
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
            backgroundTexture = MakeTexture(new Color32(24, 29, 47, 160));
            panelTexture = MakeTexture(new Color32(238, 220, 176, 245));
            buttonTexture = MakeTexture(new Color32(105, 60, 42, 255));
            highlightedButtonTexture = MakeTexture(new Color32(177, 119, 51, 255));

            titleStyle = new GUIStyle(GUI.skin.label)
            {
                font = titleFont,
                fontSize = 68,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color32(248, 225, 174, 255) }
            };
            headingStyle = new GUIStyle(GUI.skin.label)
            {
                font = titleFont,
                fontSize = 38,
                alignment = TextAnchor.MiddleCenter,
                wordWrap = true,
                normal = { textColor = new Color32(56, 39, 34, 255) }
            };
            playerHeadingStyle = new GUIStyle(headingStyle)
            {
                fontSize = 27,
                alignment = TextAnchor.MiddleLeft,
                wordWrap = false
            };
            bodyStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 26,
                alignment = TextAnchor.MiddleCenter,
                wordWrap = true,
                normal = { textColor = new Color32(56, 39, 34, 255) }
            };
            playerBodyStyle = new GUIStyle(bodyStyle)
            {
                fontSize = 21,
                alignment = TextAnchor.MiddleLeft,
                wordWrap = false
            };
            smallStyle = new GUIStyle(bodyStyle) { fontSize = 19 };
            playerSmallStyle = new GUIStyle(smallStyle)
            {
                fontSize = 17,
                alignment = TextAnchor.MiddleLeft,
                wordWrap = false
            };
            buttonStyle = new GUIStyle(GUI.skin.button)
            {
                font = GUI.skin.button.font,
                fontSize = 32,
                alignment = TextAnchor.MiddleCenter,
                normal = { background = buttonTexture, textColor = Color.white },
                hover = { background = highlightedButtonTexture, textColor = Color.white },
                active = { background = highlightedButtonTexture, textColor = Color.white },
                border = new RectOffset(8, 8, 8, 8),
                padding = new RectOffset(10, 10, 6, 6)
            };
        }

        static Texture2D MakeTexture(Color color)
        {
            var texture = new Texture2D(1, 1);
            texture.SetPixel(0, 0, color);
            texture.Apply();
            return texture;
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
            foreach (var material in tokenMaterials)
            {
                if (material != null)
                    Destroy(material);
            }
        }
    }
}
