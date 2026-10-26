using UnityEngine;
using UnityEngine.SceneManagement;

namespace RobaColores.Game
{
    public sealed class MainMenuController : MonoBehaviour
    {
        const int LayoutWidth = 1920;
        const int LayoutHeight = 1080;

        enum MenuPage
        {
            Main,
            PlayerSetup,
            Options
        }

        static readonly string[] CharacterNames =
        {
            "Capitán Brújula",
            "Arquera del Mar",
            "Pulpo Bucanero",
            "Pirata Pixel",
            "Oso Marinero",
            "Capitán Tentáculos",
            "Grumete"
        };

        static readonly string[] CharacterArt =
        {
            "CharacterArt/PirateCaptain",
            "CharacterArt/SeaArcher",
            "CharacterArt/OctopusPirate",
            "CharacterArt/PixelPirate",
            "CharacterArt/PirateBear",
            "CharacterArt/OctopusCaptain",
            "CharacterArt/PixelGrumete"
        };

        readonly int[] selectedCharacters = { 0, 1, 2, 3 };
        readonly Texture2D[] portraits = new Texture2D[CharacterArt.Length];

        MenuPage page;
        bool customizing;
        int playerCount = 2;
        int activePlayer;
        float musicVolume = 0.7f;
        bool fullscreen = true;
        string notice;
        Font titleFont;
        GUIStyle titleStyle;
        GUIStyle headingStyle;
        GUIStyle bodyStyle;
        GUIStyle buttonStyle;
        GUIStyle selectedButtonStyle;
        GUIStyle cardStyle;
        GUIStyle selectedCardStyle;
        GUIStyle paperStyle;
        Texture2D darkTexture;
        Texture2D paperTexture;
        Texture2D buttonTexture;
        Texture2D selectedTexture;

        void Awake()
        {
            titleFont = Resources.Load<Font>("Fonts/PirataOne-Regular");
            for (var i = 0; i < CharacterArt.Length; i++)
                portraits[i] = Resources.Load<Texture2D>(CharacterArt[i]);

            if (LocalGameSetup.Players.Length > 0)
            {
                var savedPlayers = LocalGameSetup.Players;
                playerCount = savedPlayers.Length;
                for (var i = 0; i < playerCount; i++)
                    selectedCharacters[i] = Mathf.Clamp(savedPlayers[i].characterIndex, 0, CharacterNames.Length - 1);
            }

            musicVolume = AudioListener.volume;
            fullscreen = Screen.fullScreen;
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
            switch (page)
            {
                case MenuPage.Main:
                    DrawMainMenu();
                    break;
                case MenuPage.PlayerSetup:
                    DrawPlayerSetup();
                    break;
                case MenuPage.Options:
                    DrawOptions();
                    break;
            }

            GUI.matrix = previousMatrix;
        }

        void BuildStyles()
        {
            darkTexture = MakeTexture(new Color32(24, 29, 47, 255));
            paperTexture = MakeTexture(new Color32(238, 220, 176, 255));
            buttonTexture = MakeTexture(new Color32(105, 60, 42, 255));
            selectedTexture = MakeTexture(new Color32(177, 119, 51, 255));

            titleStyle = new GUIStyle(GUI.skin.label)
            {
                font = titleFont,
                fontSize = 104,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color32(248, 225, 174, 255) },
                wordWrap = true
            };
            headingStyle = new GUIStyle(GUI.skin.label)
            {
                font = titleFont,
                fontSize = 58,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color32(56, 39, 34, 255) }
            };
            bodyStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 28,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color32(56, 39, 34, 255) },
                wordWrap = true
            };
            buttonStyle = new GUIStyle(GUI.skin.button)
            {
                font = titleFont,
                fontSize = 38,
                alignment = TextAnchor.MiddleCenter,
                normal = { background = buttonTexture, textColor = Color.white },
                hover = { background = selectedTexture, textColor = Color.white },
                active = { background = selectedTexture, textColor = Color.white },
                border = new RectOffset(8, 8, 8, 8),
                margin = new RectOffset(0, 0, 0, 0),
                padding = new RectOffset(12, 12, 8, 8)
            };
            selectedButtonStyle = new GUIStyle(buttonStyle)
            {
                normal = { background = selectedTexture }
            };
            cardStyle = new GUIStyle(GUI.skin.box)
            {
                normal = { background = paperTexture },
                border = new RectOffset(10, 10, 10, 10),
                padding = new RectOffset(8, 8, 8, 8)
            };
            selectedCardStyle = new GUIStyle(cardStyle)
            {
                normal = { background = selectedTexture }
            };
            paperStyle = new GUIStyle(GUI.skin.box)
            {
                normal = { background = paperTexture },
                border = new RectOffset(16, 16, 16, 16)
            };
        }

        void DrawBackground()
        {
            GUI.color = Color.white;
            GUI.DrawTexture(new Rect(0, 0, LayoutWidth, LayoutHeight), darkTexture);
            DrawRect(new Rect(34, 34, LayoutWidth - 68, LayoutHeight - 68), new Color32(43, 50, 69, 255));
            DrawRect(new Rect(48, 48, LayoutWidth - 96, LayoutHeight - 96), new Color32(24, 29, 47, 255));
        }

        void DrawMainMenu()
        {
            GUI.Box(new Rect(92, 92, 1736, 896), GUIContent.none, paperStyle);
            GUI.Label(new Rect(150, 130, 1010, 144), "ROBA COLORES", titleStyle);
            GUI.Label(new Rect(220, 252, 870, 60), "Una aventura de piratas y pixel art", bodyStyle);
            DrawHero(new Rect(190, 325, 880, 570));

            var buttonX = 1210f;
            var buttonY = 290f;
            var buttonWidth = 470f;
            var buttonHeight = 82f;
            var gap = 24f;

            if (GUI.Button(new Rect(buttonX, buttonY, buttonWidth, buttonHeight), "JUGAR", buttonStyle))
                OpenPlayerSetup(false);
            if (GUI.Button(new Rect(buttonX, buttonY + buttonHeight + gap, buttonWidth, buttonHeight), "OPCIONES", buttonStyle))
                page = MenuPage.Options;
            if (GUI.Button(new Rect(buttonX, buttonY + (buttonHeight + gap) * 2, buttonWidth, buttonHeight), "PERSONALIZAR", buttonStyle))
                OpenPlayerSetup(true);
            if (GUI.Button(new Rect(buttonX, buttonY + (buttonHeight + gap) * 3, buttonWidth, buttonHeight), "SALIR", buttonStyle))
                Application.Quit();
            if (!string.IsNullOrEmpty(notice))
                GUI.Label(new Rect(buttonX, 750, buttonWidth, 70), notice, bodyStyle);
        }

        void DrawHero(Rect rect)
        {
            var hero = portraits[0];
            if (hero == null)
            {
                GUI.Box(rect, "¡Prepárate para zarpar!", bodyStyle);
                return;
            }

            DrawRect(rect, new Color32(212, 190, 148, 255));
            GUI.DrawTexture(new Rect(rect.x + 12, rect.y + 12, rect.width - 24, rect.height - 24), hero, ScaleMode.ScaleToFit, true);
        }

        void DrawPlayerSetup()
        {
            GUI.Box(new Rect(92, 70, 1736, 940), GUIContent.none, paperStyle);
            GUI.Label(new Rect(180, 95, 1560, 80), customizing ? "PERSONALIZA TU TRIPULACIÓN" : "ELIGE A TU TRIPULACIÓN", headingStyle);
            GUI.Label(new Rect(140, 180, 390, 58), "Jugadores", bodyStyle);

            if (GUI.Button(new Rect(535, 188, 72, 52), "−", buttonStyle))
                ChangePlayerCount(-1);
            GUI.Label(new Rect(615, 183, 100, 58), playerCount.ToString(), headingStyle);
            if (GUI.Button(new Rect(724, 188, 72, 52), "+", buttonStyle))
                ChangePlayerCount(1);

            for (var i = 0; i < playerCount; i++)
            {
                var tab = new Rect(850 + i * 225, 183, 210, 58);
                var label = "JUGADOR " + (i + 1);
                if (GUI.Button(tab, label, i == activePlayer ? selectedButtonStyle : buttonStyle))
                    activePlayer = i;
            }

            GUI.Label(new Rect(130, 254, 1660, 54), "Selecciona un personaje para el Jugador " + (activePlayer + 1), bodyStyle);
            DrawCharacterCards();

            if (!string.IsNullOrEmpty(notice))
                GUI.Label(new Rect(260, 896, 1400, 46), notice, bodyStyle);

            if (GUI.Button(new Rect(180, 930, 310, 72), "VOLVER", buttonStyle))
            {
                notice = string.Empty;
                page = MenuPage.Main;
            }

            var finishLabel = customizing ? "GUARDAR SELECCIÓN" : "COMENZAR";
            if (GUI.Button(new Rect(1190, 930, 550, 72), finishLabel, buttonStyle))
                FinishPlayerSetup();
        }

        void DrawCharacterCards()
        {
            const float cardWidth = 350f;
            const float cardHeight = 286f;
            const float gapX = 46f;
            const float gapY = 28f;
            const float startX = 154f;
            const float startY = 330f;

            for (var i = 0; i < CharacterNames.Length; i++)
            {
                var row = i / 4;
                var column = i % 4;
                var rect = new Rect(startX + column * (cardWidth + gapX), startY + row * (cardHeight + gapY), cardWidth, cardHeight);
                var isSelected = selectedCharacters[activePlayer] == i;
                GUI.Box(rect, GUIContent.none, isSelected ? selectedCardStyle : cardStyle);

                var imageRect = new Rect(rect.x + 30, rect.y + 12, rect.width - 60, 192);
                var image = portraits[i];
                if (image != null)
                    GUI.DrawTexture(imageRect, image, ScaleMode.ScaleToFit, true);
                else
                    GUI.Label(imageRect, "Imagen pendiente", bodyStyle);

                GUI.Label(new Rect(rect.x + 12, rect.y + 202, rect.width - 24, 43), CharacterNames[i], bodyStyle);
                var owner = GetCharacterOwner(i);
                var caption = owner >= 0 && owner != activePlayer ? "Elegido por Jugador " + (owner + 1) : isSelected ? "SELECCIONADO" : "ELEGIR";
                GUI.Label(new Rect(rect.x + 12, rect.y + 244, rect.width - 24, 32), caption, bodyStyle);

                if (GUI.Button(rect, GUIContent.none, GUIStyle.none) && (owner < 0 || owner == activePlayer))
                {
                    selectedCharacters[activePlayer] = i;
                    notice = string.Empty;
                }
            }
        }

        void DrawOptions()
        {
            GUI.Box(new Rect(250, 150, 1420, 780), GUIContent.none, paperStyle);
            GUI.Label(new Rect(400, 220, 1120, 100), "OPCIONES", headingStyle);
            GUI.Label(new Rect(480, 390, 430, 60), "Volumen general", bodyStyle);
            musicVolume = GUI.HorizontalSlider(new Rect(920, 412, 430, 30), musicVolume, 0f, 1f);
            AudioListener.volume = musicVolume;
            GUI.Label(new Rect(1360, 390, 130, 60), Mathf.RoundToInt(musicVolume * 100f) + "%", bodyStyle);

            var fullscreenLabel = fullscreen ? "Pantalla completa: sí" : "Pantalla completa: no";
            if (GUI.Button(new Rect(600, 520, 720, 76), fullscreenLabel, buttonStyle))
            {
                fullscreen = !fullscreen;
                Screen.fullScreen = fullscreen;
            }

            if (GUI.Button(new Rect(770, 760, 380, 76), "VOLVER", buttonStyle))
                page = MenuPage.Main;
        }

        void OpenPlayerSetup(bool customizeOnly)
        {
            customizing = customizeOnly;
            activePlayer = 0;
            notice = string.Empty;
            page = MenuPage.PlayerSetup;
        }

        void ChangePlayerCount(int amount)
        {
            var newCount = Mathf.Clamp(playerCount + amount, 2, 4);
            if (newCount > playerCount)
            {
                for (var player = playerCount; player < newCount; player++)
                {
                    selectedCharacters[player] = FindAvailableCharacter(player);
                }
            }

            playerCount = newCount;
            activePlayer = Mathf.Clamp(activePlayer, 0, playerCount - 1);
            notice = string.Empty;
        }

        int FindAvailableCharacter(int playerIndex)
        {
            for (var character = 0; character < CharacterNames.Length; character++)
            {
                var available = true;
                for (var player = 0; player < playerIndex; player++)
                {
                    if (selectedCharacters[player] == character)
                    {
                        available = false;
                        break;
                    }
                }

                if (available)
                    return character;
            }

            return 0;
        }

        int GetCharacterOwner(int characterIndex)
        {
            for (var player = 0; player < playerCount; player++)
            {
                if (selectedCharacters[player] == characterIndex)
                    return player;
            }

            return -1;
        }

        void FinishPlayerSetup()
        {
            var names = new string[playerCount];
            var indices = new int[playerCount];
            for (var i = 0; i < playerCount; i++)
            {
                indices[i] = selectedCharacters[i];
                names[i] = CharacterNames[indices[i]];
            }

            LocalGameSetup.SetPlayers(indices, names);
            if (customizing)
            {
                page = MenuPage.Main;
                notice = "Tripulación guardada.";
                return;
            }

            if (!Application.CanStreamedLevelBeLoaded("MapaA"))
            {
                notice = "No se encontró la escena MapaA en la lista de escenas.";
                return;
            }

            SceneManager.LoadScene("MapaA");
        }

        void DrawRect(Rect rect, Color color)
        {
            var previousColor = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = previousColor;
        }

        static Texture2D MakeTexture(Color color)
        {
            var texture = new Texture2D(1, 1);
            texture.SetPixel(0, 0, color);
            texture.Apply();
            return texture;
        }
    }
}
