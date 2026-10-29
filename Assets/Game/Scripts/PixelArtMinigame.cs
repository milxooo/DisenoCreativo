using System;
using System.Collections.Generic;
using RobaColores.Map;
using UnityEngine;

namespace RobaColores.Game
{
    public sealed class PixelArtMinigame : MonoBehaviour
    {
        const int GridSize = 25;
        const float RoundDurationSeconds = 90f;
        const int LayoutWidth = 1920;
        const int LayoutHeight = 1080;

        enum RoundPhase
        {
            Drawing,
            Results
        }

        static readonly string[] ColorNames =
        {
            "ROJO",
            "NARANJA",
            "AMARILLO",
            "VERDE",
            "AZUL",
            "MORADO",
            "NEGRO"
        };

        static readonly Color[] PixelColors =
        {
            new Color32(216, 54, 58, 255),
            new Color32(240, 138, 36, 255),
            new Color32(242, 211, 53, 255),
            new Color32(63, 174, 74, 255),
            new Color32(60, 120, 216, 255),
            new Color32(142, 79, 191, 255),
            new Color32(43, 43, 51, 255)
        };

        static readonly string[] CategoryNames =
        {
            "partes del cuerpo",
            "frutas y verduras",
            "accesorios",
            "animales",
            "profesiones",
            "deportes",
            "astronomía"
        };

        sealed class Participant
        {
            public PlayerSetup setup;
            public int[,] pixels;
            public int[] inventory;
            public Texture2D canvasTexture;
            public float timeRemaining;
            public float score;
            public bool submitted;
            public bool eligible;
        }

        readonly List<Participant> participants = new List<Participant>();
        readonly List<int> roundWinners = new List<int>();
        readonly int[] target = new int[GridSize * GridSize];
        readonly int[] requiredPixels = new int[7];

        Action<int[], bool> onComplete;
        RoundPhase phase;
        PathTileColor category;
        int activeColor;
        int targetPixelCount;
        float remainingRoundTime;
        bool eliminationVictory;
        string resultMessage;
        GUIStyle titleStyle;
        GUIStyle headingStyle;
        GUIStyle bodyStyle;
        GUIStyle smallStyle;
        GUIStyle buttonStyle;
        GUIStyle colorLabelStyle;
        Texture2D backgroundTexture;
        Texture2D cardTexture;
        Texture2D buttonTexture;
        Texture2D selectedTexture;
        Texture2D targetTexture;

        public void Begin(PathTileColor roundCategory, PlayerSetup[] playerSetups, Action<int[], bool> completed)
        {
            if (playerSetups == null || playerSetups.Length < 2 || playerSetups.Length > 4)
                throw new ArgumentException("El minijuego requiere de dos a cuatro jugadores.", nameof(playerSetups));
            if (completed == null)
                throw new ArgumentNullException(nameof(completed));

            category = roundCategory;
            onComplete = completed;
            BuildTarget();
            for (var i = 0; i < playerSetups.Length; i++)
            {
                var inventory = playerSetups[i].pixelInventory;
                if (inventory == null || inventory.Length != PixelColors.Length)
                    throw new InvalidOperationException("El inventario de colores del jugador no está inicializado.");

                var participant = new Participant
                {
                    setup = playerSetups[i],
                    inventory = (int[])inventory.Clone(),
                    pixels = new int[GridSize, GridSize],
                    timeRemaining = Mathf.Max(0f, RoundDurationSeconds - LocalGameSetup.ConsumeMiniGameTimePenalty(i)),
                    eligible = HasRequiredPixels(inventory),
                    submitted = !HasRequiredPixels(inventory)
                };
                for (var y = 0; y < GridSize; y++)
                {
                    for (var x = 0; x < GridSize; x++)
                        participant.pixels[x, y] = -1;
                }

                participant.canvasTexture = CreateGridTexture(participant.pixels);
                participants.Add(participant);
            }

            phase = RoundPhase.Drawing;
            remainingRoundTime = RoundDurationSeconds;
            if (CompleteFromAvailablePixels())
                return;

            foreach (var participant in participants)
            {
                if (participant.timeRemaining <= 0f)
                    participant.submitted = true;
            }

            if (AllSubmitted())
                FinishRound();
        }

        void Update()
        {
            if (phase != RoundPhase.Drawing)
                return;

            var delta = Time.deltaTime;
            remainingRoundTime = Mathf.Max(0f, remainingRoundTime - delta);
            for (var i = 0; i < participants.Count; i++)
            {
                var participant = participants[i];
                if (participant.submitted || !participant.eligible)
                    continue;

                participant.timeRemaining = Mathf.Max(0f, participant.timeRemaining - delta);
                if (participant.timeRemaining <= 0f || remainingRoundTime <= 0f)
                    participant.submitted = true;
            }

            if (AllSubmitted())
                FinishRound();
        }

        void OnGUI()
        {
            if (backgroundTexture == null)
                BuildStyles();

            var previousMatrix = GUI.matrix;
            var scale = Mathf.Min((float)Screen.width / LayoutWidth, (float)Screen.height / LayoutHeight);
            var offsetX = (Screen.width - LayoutWidth * scale) * 0.5f;
            var offsetY = (Screen.height - LayoutHeight * scale) * 0.5f;
            GUI.matrix = Matrix4x4.TRS(new Vector3(offsetX, offsetY, 0f), Quaternion.identity, Vector3.one * scale);

            DrawRect(new Rect(0, 0, LayoutWidth, LayoutHeight), new Color32(24, 29, 47, 255));
            if (phase == RoundPhase.Drawing)
                DrawDrawingRound();
            else
                DrawResults();

            GUI.matrix = previousMatrix;
        }

        void DrawDrawingRound()
        {
            GUI.Label(new Rect(240, 14, 1440, 66), "¡COMPLETA EL PIXEL ART!", titleStyle);
            GUI.Label(new Rect(540, 77, 840, 48),
                "Categoría: " + CategoryNames[Mathf.Clamp((int)category, 0, CategoryNames.Length - 1)] +
                " · Dificultad: " + DifficultyFor(category),
                bodyStyle);

            var colorButtonWidth = 148f;
            var paletteWidth = colorButtonWidth * (PixelColors.Length + 1) + 16f * PixelColors.Length;
            var paletteX = (LayoutWidth - paletteWidth) * 0.5f;
            for (var i = 0; i < PixelColors.Length; i++)
            {
                var rect = new Rect(paletteX + i * (colorButtonWidth + 16f), 132, colorButtonWidth, 50);
                if (activeColor == i)
                    DrawOutline(rect, Color.white, 4f);
                DrawRect(rect, PixelColors[i]);
                if (GUI.Button(rect, GUIContent.none, GUIStyle.none))
                    activeColor = i;
                GUI.Label(rect, ColorNames[i], colorLabelStyle);
            }

            var eraseRect = new Rect(paletteX + PixelColors.Length * (colorButtonWidth + 16f), 132, colorButtonWidth, 50);
            if (activeColor == -1)
                DrawOutline(eraseRect, Color.white, 4f);
            DrawRect(eraseRect, new Color32(105, 60, 42, 255));
            if (GUI.Button(eraseRect, GUIContent.none, GUIStyle.none))
                activeColor = -1;
            GUI.Label(eraseRect, "BORRAR", colorLabelStyle);

            var columns = 2;
            var rows = (participants.Count + columns - 1) / columns;
            var cardWidth = (LayoutWidth - 84f) / columns;
            var cardHeight = (LayoutHeight - 222f) / rows;
            var gridSize = Mathf.Min(520f, cardWidth - 335f, cardHeight - 76f);
            var startY = 202f;

            for (var i = 0; i < participants.Count; i++)
            {
                var column = i % columns;
                var row = i / columns;
                var rect = new Rect(24f + column * (cardWidth + 36f), startY + row * (cardHeight + 18f), cardWidth, cardHeight - 12f);
                DrawParticipantCard(i, rect, gridSize);
            }

        }

        void DrawParticipantCard(int index, Rect card, float gridSize)
        {
            var participant = participants[index];
            DrawRect(card, new Color32(238, 220, 176, 255));
            GUI.Label(new Rect(card.x + 12, card.y + 6, card.width - 24, 40),
                "JUGADOR " + (index + 1) + " · " + participant.setup.characterName,
                headingStyle);

            var canvasRect = new Rect(card.x + 18, card.y + 52, gridSize, gridSize);
            HandleCanvasInput(index, canvasRect);
            GUI.DrawTexture(canvasRect, participant.canvasTexture, ScaleMode.StretchToFill, false);
            DrawGridLines(canvasRect);

            var referenceSize = Mathf.Min(245f, card.width - gridSize - 84f, card.height - 244f);
            var referenceRect = new Rect(canvasRect.xMax + 38, card.y + 92, referenceSize, referenceSize);
            GUI.Label(new Rect(referenceRect.x - 4, card.y + 52, referenceRect.width + 8, 36), "REFERENCIA", smallStyle);
            GUI.DrawTexture(referenceRect, targetTexture, ScaleMode.ScaleToFit, false);
            GUI.Label(new Rect(referenceRect.x - 4, referenceRect.yMax + 8, referenceRect.width + 8, 40),
                participant.submitted ? "Terminado" : "Tiempo: " + Mathf.CeilToInt(participant.timeRemaining) + " s",
                smallStyle);
            GUI.Label(new Rect(referenceRect.x - 4, referenceRect.yMax + 46, referenceRect.width + 8, 42),
                participant.submitted
                    ? "Inventario total: " + TotalInventory(participant.inventory)
                    : "Color activo: " + (activeColor < 0
                        ? "borrador"
                        : ColorNames[activeColor] + " (" + participant.inventory[activeColor] + ")") +
                      " · Total: " + TotalInventory(participant.inventory),
                smallStyle);

            var submitRect = new Rect(referenceRect.x - 4, referenceRect.yMax + 94, referenceRect.width + 8, 58);
            if (!participant.submitted && GUI.Button(submitRect, "TERMINAR", buttonStyle))
                Submit(index);
            else if (!participant.eligible)
                GUI.Label(submitRect, "Sin píxeles suficientes", smallStyle);
            else if (participant.submitted)
                GUI.Label(submitRect, "ENTREGADO", smallStyle);
        }

        void DrawGridLines(Rect rect)
        {
            var previousColor = GUI.color;
            GUI.color = new Color(0.1f, 0.1f, 0.1f, 0.35f);
            for (var i = 0; i <= GridSize; i++)
            {
                var x = rect.x + rect.width * i / GridSize;
                var y = rect.y + rect.height * i / GridSize;
                GUI.DrawTexture(new Rect(x, rect.y, 1f, rect.height), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(rect.x, y, rect.width, 1f), Texture2D.whiteTexture);
            }
            GUI.color = previousColor;
        }

        void HandleCanvasInput(int playerIndex, Rect canvas)
        {
            var currentEvent = Event.current;
            if (phase != RoundPhase.Drawing || participants[playerIndex].submitted ||
                currentEvent.button != 0 || !canvas.Contains(currentEvent.mousePosition))
                return;

            if (currentEvent.type != EventType.MouseDown && currentEvent.type != EventType.MouseDrag)
                return;

            var cellSize = canvas.width / GridSize;
            var x = Mathf.Clamp(Mathf.FloorToInt((currentEvent.mousePosition.x - canvas.x) / cellSize), 0, GridSize - 1);
            var y = GridSize - 1 - Mathf.Clamp(Mathf.FloorToInt((currentEvent.mousePosition.y - canvas.y) / cellSize), 0, GridSize - 1);
            PaintPixel(playerIndex, x, y, activeColor);
            currentEvent.Use();
        }

        void PaintPixel(int playerIndex, int x, int y, int color)
        {
            var participant = participants[playerIndex];
            var previousColor = participant.pixels[x, y];
            if (previousColor == color)
                return;

            if (color >= 0 && participant.inventory[color] == 0)
                return;

            if (previousColor >= 0)
                participant.inventory[previousColor]++;
            if (color >= 0)
                participant.inventory[color]--;

            participant.pixels[x, y] = color;
            participant.canvasTexture.SetPixel(x, y, TextureColor(color));
            participant.canvasTexture.Apply(false, false);
            LocalGameSetup.SetPixelInventory(playerIndex, participant.inventory);
        }

        void DrawResults()
        {
            GUI.DrawTexture(new Rect(190, 110, 1540, 850), cardTexture);
            GUI.Label(new Rect(340, 155, 1240, 100), eliminationVictory ? "¡FIN DE LA PARTIDA!" : "¡RETO TERMINADO!", titleStyle);
            GUI.Label(new Rect(340, 258, 1240, 96), resultMessage, headingStyle);

            for (var i = 0; i < participants.Count; i++)
            {
                var participant = participants[i];
                var rect = new Rect(300, 390 + i * 96, 1320, 80);
                var winner = roundWinners.Contains(i);
                DrawRect(rect, winner ? new Color32(177, 119, 51, 255) : new Color32(218, 202, 166, 255));
                GUI.Label(new Rect(rect.x + 24, rect.y + 8, 790, 62),
                    "JUGADOR " + (i + 1) + " · " + participant.setup.characterName,
                    headingStyle);
                GUI.Label(new Rect(rect.x + 820, rect.y + 8, 470, 62),
                    participant.eligible ? "Puntuación: " + Mathf.RoundToInt(participant.score) + "%" : "No alcanzó los píxeles para jugar",
                    bodyStyle);
            }

            if (GUI.Button(new Rect(710, 865, 500, 74), eliminationVictory ? "VER GANADOR" : "VOLVER AL TABLERO", buttonStyle))
            {
                var completedWinners = roundWinners.ToArray();
                var completedEliminationVictory = eliminationVictory;
                onComplete(completedWinners, completedEliminationVictory);
                Destroy(this);
            }
        }

        void Submit(int playerIndex)
        {
            participants[playerIndex].submitted = true;
            if (AllSubmitted())
                FinishRound();
        }

        bool AllSubmitted()
        {
            foreach (var participant in participants)
            {
                if (participant.eligible && !participant.submitted)
                    return false;
            }
            return true;
        }

        void FinishRound()
        {
            if (phase == RoundPhase.Results)
                return;

            var bestScore = -1f;
            for (var i = 0; i < participants.Count; i++)
            {
                var participant = participants[i];
                participant.score = participant.eligible ? Score(participant.pixels) : 0f;
                if (!participant.eligible)
                    continue;

                if (participant.score > bestScore)
                {
                    bestScore = participant.score;
                    roundWinners.Clear();
                    roundWinners.Add(i);
                }
                else if (Mathf.Approximately(participant.score, bestScore))
                {
                    roundWinners.Add(i);
                }
            }

            if (roundWinners.Count == 0)
            {
                var highestInventory = -1;
                for (var i = 0; i < participants.Count; i++)
                {
                    var inventory = TotalInventory(participants[i].inventory);
                    if (inventory > highestInventory)
                    {
                        highestInventory = inventory;
                        roundWinners.Clear();
                        roundWinners.Add(i);
                    }
                    else if (inventory == highestInventory)
                    {
                        roundWinners.Add(i);
                    }
                }
                eliminationVictory = true;
                resultMessage = "La tripulación con más píxeles es la última que puede seguir jugando.";
            }
            else
            {
                DistributeStolenRows();
                var ablePlayers = GetPlayersAbleToContinue();
                if (ablePlayers.Count == 1)
                {
                    roundWinners.Clear();
                    roundWinners.Add(ablePlayers[0]);
                    eliminationVictory = true;
                    resultMessage = participants[ablePlayers[0]].setup.characterName +
                        " es la última persona con píxeles suficientes para continuar.";
                }
                else if (ablePlayers.Count == 0)
                {
                    ChooseFinalWinnerByInventory();
                    eliminationVictory = true;
                    resultMessage = "Se acabaron los píxeles. Gana quien conserve más colores.";
                }
                else if (roundWinners.Count == 1)
                {
                    var winner = roundWinners[0];
                    resultMessage = participants[winner].setup.characterName +
                        " ganó con " + Mathf.RoundToInt(participants[winner].score) +
                        "% de precisión y recibió las filas robadas.";
                }
                else
                {
                    resultMessage = "Empate con " + Mathf.RoundToInt(bestScore) +
                        "% de precisión. Las filas robadas se repartieron por turnos entre las personas empatadas.";
                }
            }

            phase = RoundPhase.Results;
        }

        void DistributeStolenRows()
        {
            var recipient = 0;
            for (var i = 0; i < participants.Count; i++)
            {
                if (roundWinners.Contains(i))
                    continue;

                var row = FindBestRow(participants[i]);
                if (row < 0)
                    continue;

                var winnerIndex = roundWinners[recipient % roundWinners.Count];
                var winner = participants[winnerIndex];
                var loser = participants[i];
                for (var x = 0; x < GridSize; x++)
                {
                    var color = loser.pixels[x, row];
                    if (color < 0)
                        continue;

                    winner.inventory[color]++;
                    loser.pixels[x, row] = -1;
                    loser.canvasTexture.SetPixel(x, row, Color.white);
                    winner.canvasTexture.Apply(false, false);
                    loser.canvasTexture.Apply(false, false);
                }

                LocalGameSetup.SetPixelInventory(winnerIndex, winner.inventory);
                LocalGameSetup.SetPixelInventory(i, loser.inventory);
                recipient++;
            }
        }

        int FindBestRow(Participant participant)
        {
            var bestRow = -1;
            var mostPixels = 0;
            for (var y = 0; y < GridSize; y++)
            {
                var count = 0;
                for (var x = 0; x < GridSize; x++)
                {
                    if (participant.pixels[x, y] >= 0)
                        count++;
                }

                if (count > mostPixels)
                {
                    bestRow = y;
                    mostPixels = count;
                }
            }

            return bestRow;
        }

        List<int> GetPlayersAbleToContinue()
        {
            var able = new List<int>();
            for (var i = 0; i < participants.Count; i++)
            {
                if (HasRequiredPixels(participants[i].inventory))
                    able.Add(i);
            }

            return able;
        }

        void ChooseFinalWinnerByInventory()
        {
            var maximum = -1;
            roundWinners.Clear();
            for (var i = 0; i < participants.Count; i++)
            {
                var count = TotalInventory(participants[i].inventory);
                if (count > maximum)
                {
                    maximum = count;
                    roundWinners.Clear();
                    roundWinners.Add(i);
                }
                else if (count == maximum)
                {
                    roundWinners.Add(i);
                }
            }
        }

        bool CompleteFromAvailablePixels()
        {
            var able = GetPlayersAbleToContinue();
            if (able.Count > 1)
                return false;

            if (able.Count == 1)
            {
                roundWinners.Add(able[0]);
                eliminationVictory = true;
                resultMessage = participants[able[0]].setup.characterName +
                    " es la única persona con píxeles suficientes para jugar este reto.";
            }
            else
            {
                ChooseFinalWinnerByInventory();
                eliminationVictory = true;
                resultMessage = "La tripulación agotó sus píxeles. Gana quien conserve más colores.";
            }

            phase = RoundPhase.Results;
            return true;
        }

        bool HasRequiredPixels(int[] inventory)
        {
            for (var i = 0; i < requiredPixels.Length; i++)
            {
                if (inventory[i] < requiredPixels[i])
                    return false;
            }

            return true;
        }

        float Score(int[,] pixels)
        {
            var correct = 0;
            var incorrect = 0;
            for (var x = 0; x < GridSize; x++)
            {
                for (var y = 0; y < GridSize; y++)
                {
                    var targetColor = target[y * GridSize + x];
                    if (targetColor < 0)
                    {
                        if (pixels[x, y] >= 0)
                            incorrect++;
                        continue;
                    }

                    if (pixels[x, y] == targetColor)
                        correct++;
                    else if (pixels[x, y] >= 0)
                        incorrect++;
                }
            }

            return Mathf.Clamp((correct - incorrect) * 100f / targetPixelCount, 0f, 100f);
        }

        static int TotalInventory(int[] inventory)
        {
            var total = 0;
            foreach (var count in inventory)
                total += count;
            return total;
        }

        void BuildTarget()
        {
            for (var i = 0; i < target.Length; i++)
                target[i] = -1;
            Array.Clear(requiredPixels, 0, requiredPixels.Length);

            switch (category)
            {
                case PathTileColor.Rojo:
                    BuildHeart();
                    break;
                case PathTileColor.Naranja:
                    BuildFruit();
                    break;
                case PathTileColor.Amarillo:
                    BuildHat();
                    break;
                case PathTileColor.Verde:
                    BuildCat();
                    break;
                case PathTileColor.Azul:
                    BuildSailor();
                    break;
                case PathTileColor.Morado:
                    BuildBall();
                    break;
                default:
                    BuildRocket();
                    break;
            }

            for (var y = 0; y < GridSize; y++)
            {
                for (var x = 0; x < GridSize; x++)
                {
                    var color = target[y * GridSize + x];
                    if (color >= 0)
                        requiredPixels[color]++;
                }
            }

            targetPixelCount = 0;
            foreach (var count in requiredPixels)
                targetPixelCount += count;

            targetTexture = CreateTargetTexture();
        }

        static string DifficultyFor(PathTileColor tileColor)
        {
            switch (tileColor)
            {
                case PathTileColor.Rojo:
                case PathTileColor.Naranja:
                    return "Fácil";
                case PathTileColor.Amarillo:
                case PathTileColor.Verde:
                case PathTileColor.Azul:
                    return "Media";
                default:
                    return "Difícil";
            }
        }

        void BuildHeart()
        {
            FillEllipse(7, 17, 4, 4, 0);
            FillEllipse(17, 17, 4, 4, 0);
            FillTriangle(3, 15, 21, 15, 12, 5, 0);
            FillEllipse(8, 17, 2, 2, 5);
            FillEllipse(16, 17, 2, 2, 5);
        }

        void BuildFruit()
        {
            FillEllipse(12, 11, 7, 8, 0);
            FillEllipse(8, 12, 3, 5, 1);
            FillEllipse(16, 12, 3, 5, 1);
            FillRect(11, 19, 13, 22, 6);
            FillEllipse(16, 19, 5, 2, 3);
            FillEllipse(7, 12, 1, 3, 2);
        }

        void BuildHat()
        {
            FillRect(3, 9, 21, 11, 6);
            FillTriangle(6, 11, 18, 11, 12, 21, 6);
            FillRect(7, 10, 17, 11, 0);
            FillRect(10, 14, 14, 17, 2);
        }

        void BuildCat()
        {
            FillEllipse(12, 13, 7, 6, 3);
            FillTriangle(5, 14, 10, 14, 6, 23, 3);
            FillTriangle(14, 14, 19, 14, 18, 23, 3);
            FillEllipse(8, 13, 2, 3, 6);
            FillEllipse(15, 13, 2, 3, 6);
            FillEllipse(9, 14, 1, 1, 2);
            FillEllipse(14, 14, 1, 1, 2);
            FillTriangle(11, 10, 13, 10, 12, 8, 0);
            FillEllipse(12, 5, 6, 3, 3);
        }

        void BuildSailor()
        {
            FillTriangle(6, 17, 18, 17, 12, 23, 4);
            FillRect(7, 12, 17, 17, 2);
            FillEllipse(9, 13, 3, 3, 4);
            FillRect(8, 3, 16, 9, 4);
            FillTriangle(6, 8, 18, 8, 12, 11, 4);
            FillRect(9, 6, 15, 7, 5);
        }

        void BuildBall()
        {
            FillEllipse(12, 12, 8, 8, 2);
            FillEllipse(12, 12, 2, 2, 0);
            FillTriangle(4, 14, 10, 18, 10, 10, 0);
            FillTriangle(14, 18, 20, 14, 14, 10, 0);
            FillTriangle(9, 4, 13, 10, 16, 5, 0);
        }

        void BuildRocket()
        {
            FillEllipse(12, 12, 6, 8, 4);
            FillTriangle(7, 18, 17, 18, 12, 24, 6);
            FillTriangle(6, 8, 10, 8, 5, 3, 0);
            FillTriangle(14, 8, 18, 8, 19, 3, 0);
            FillEllipse(11, 13, 3, 3, 1);
            FillEllipse(12, 14, 1, 1, 6);
            FillEllipse(11, 2, 2, 2, 0);
            FillEllipse(13, 2, 2, 2, 0);
        }

        void FillEllipse(int minX, int minY, int radiusX, int radiusY, int color)
        {
            for (var y = minY - radiusY; y <= minY + radiusY; y++)
            {
                for (var x = minX - radiusX; x <= minX + radiusX; x++)
                {
                    if (x < 0 || x >= GridSize || y < 0 || y >= GridSize)
                        continue;

                    var dx = (float)(x - minX) / Mathf.Max(1, radiusX);
                    var dy = (float)(y - minY) / Mathf.Max(1, radiusY);
                    if (dx * dx + dy * dy <= 1f)
                        SetTargetPixel(x, y, color);
                }
            }
        }

        void FillRect(int minX, int minY, int maxX, int maxY, int color)
        {
            for (var y = minY; y <= maxY; y++)
            {
                for (var x = minX; x <= maxX; x++)
                    SetTargetPixel(x, y, color);
            }
        }

        void FillTriangle(int x1, int y1, int x2, int y2, int x3, int y3, int color)
        {
            var minY = Mathf.Min(y1, Mathf.Min(y2, y3));
            var maxY = Mathf.Max(y1, Mathf.Max(y2, y3));
            for (var y = minY; y <= maxY; y++)
            {
                var intersections = new List<float>(3);
                AddTriangleEdgeIntersection(x1, y1, x2, y2, y, intersections);
                AddTriangleEdgeIntersection(x2, y2, x3, y3, y, intersections);
                AddTriangleEdgeIntersection(x3, y3, x1, y1, y, intersections);
                if (intersections.Count < 2)
                    continue;

                intersections.Sort();
                for (var x = Mathf.RoundToInt(intersections[0]); x <= Mathf.RoundToInt(intersections[intersections.Count - 1]); x++)
                    SetTargetPixel(x, y, color);
            }
        }

        static void AddTriangleEdgeIntersection(int x1, int y1, int x2, int y2, int y, List<float> intersections)
        {
            if (y1 == y2 || y < Mathf.Min(y1, y2) || y > Mathf.Max(y1, y2))
                return;

            intersections.Add(x1 + (float)(y - y1) * (x2 - x1) / (y2 - y1));
        }

        void SetTargetPixel(int x, int y, int color)
        {
            if (x >= 0 && x < GridSize && y >= 0 && y < GridSize)
                target[y * GridSize + x] = color;
        }

        Texture2D CreateTargetTexture()
        {
            var texture = new Texture2D(GridSize, GridSize, TextureFormat.RGBA32, false);
            texture.filterMode = FilterMode.Point;
            texture.wrapMode = TextureWrapMode.Clamp;
            for (var y = 0; y < GridSize; y++)
            {
                for (var x = 0; x < GridSize; x++)
                    texture.SetPixel(x, y, TextureColor(target[y * GridSize + x]));
            }
            texture.Apply(false, false);
            return texture;
        }

        Texture2D CreateGridTexture(int[,] pixels)
        {
            var texture = new Texture2D(GridSize, GridSize, TextureFormat.RGBA32, false);
            texture.filterMode = FilterMode.Point;
            texture.wrapMode = TextureWrapMode.Clamp;
            for (var y = 0; y < GridSize; y++)
            {
                for (var x = 0; x < GridSize; x++)
                    texture.SetPixel(x, y, TextureColor(pixels[x, y]));
            }
            texture.Apply(false, false);
            return texture;
        }

        static Color TextureColor(int index)
        {
            return index >= 0 && index < PixelColors.Length ? PixelColors[index] : Color.white;
        }

        void BuildStyles()
        {
            backgroundTexture = MakeTexture(new Color32(24, 29, 47, 255));
            cardTexture = MakeTexture(new Color32(238, 220, 176, 255));
            buttonTexture = MakeTexture(new Color32(105, 60, 42, 255));
            selectedTexture = MakeTexture(new Color32(177, 119, 51, 255));

            titleStyle = new GUIStyle(GUI.skin.label)
            {
                font = Resources.Load<Font>("Fonts/PirataOne-Regular"),
                fontSize = 54,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color32(248, 225, 174, 255) }
            };
            headingStyle = new GUIStyle(GUI.skin.label)
            {
                font = GUI.skin.label.font,
                fontSize = 28,
                alignment = TextAnchor.MiddleCenter,
                wordWrap = true,
                normal = { textColor = new Color32(56, 39, 34, 255) }
            };
            bodyStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 26,
                alignment = TextAnchor.MiddleCenter,
                wordWrap = true,
                normal = { textColor = new Color32(56, 39, 34, 255) }
            };
            smallStyle = new GUIStyle(bodyStyle) { fontSize = 22 };
            buttonStyle = new GUIStyle(GUI.skin.button)
            {
                font = GUI.skin.button.font,
                fontSize = 28,
                alignment = TextAnchor.MiddleCenter,
                normal = { background = buttonTexture, textColor = Color.white },
                hover = { background = selectedTexture, textColor = Color.white },
                active = { background = selectedTexture, textColor = Color.white },
                border = new RectOffset(6, 6, 6, 6),
                padding = new RectOffset(4, 4, 4, 4)
            };
            colorLabelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 23,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color32(25, 25, 30, 255) }
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

        static void DrawOutline(Rect rect, Color color, float thickness)
        {
            DrawRect(new Rect(rect.x - thickness, rect.y - thickness, rect.width + thickness * 2f, thickness), color);
            DrawRect(new Rect(rect.x - thickness, rect.yMax, rect.width + thickness * 2f, thickness), color);
            DrawRect(new Rect(rect.x - thickness, rect.y, thickness, rect.height), color);
            DrawRect(new Rect(rect.xMax, rect.y, thickness, rect.height), color);
        }

        void OnDestroy()
        {
            foreach (var participant in participants)
            {
                if (participant.canvasTexture != null)
                    Destroy(participant.canvasTexture);
            }

            if (targetTexture != null)
                Destroy(targetTexture);
            if (backgroundTexture != null)
                Destroy(backgroundTexture);
            if (cardTexture != null)
                Destroy(cardTexture);
            if (buttonTexture != null)
                Destroy(buttonTexture);
            if (selectedTexture != null)
                Destroy(selectedTexture);
        }
    }
}
