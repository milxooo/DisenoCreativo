using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using RobaColores.Game.Core;

namespace RobaColores.Game.Presentation
{
    // Menu principal segun el boceto (JUGAR / OPCIONES / PERSONALIZAR / SALIDA) y seleccion de personajes.
    // La interfaz se construye por codigo con la paleta de pergamino y madera del juego.
    public sealed class MenuController : MonoBehaviour
    {
        [SerializeField] GameContent content;
        [Tooltip("Solo pruebas: pantalla con la que arranca (0 principal, 1 seleccion, 2 opciones, 3 personajes).")]
        [SerializeField] int debugStartScreen;

        RectTransform screen;
        int playerCount = 2;
        readonly int[] chosen = { 0, 1, 2, 3 };

        void Awake()
        {
            if (content == null) { Debug.LogError("MenuController necesita el asset GameContent."); enabled = false; return; }
            UiKit.Font = content.uiFont;
            UiKit.EnsureEventSystem();
            var canvas = UiKit.CreateCanvas("MenuCanvas", 0);
            var frame = UiKit.Panel(canvas.transform, "Marco", UiKit.Wood);
            UiKit.Stretch(frame.rectTransform);
            var paper = UiKit.Panel(frame.transform, "Pergamino", UiKit.Parchment);
            UiKit.Place(paper.rectTransform, 0, 0, 1, 1, 16, 16, -16, -16);
            screen = UiKit.Rect(paper.transform, "Pantalla");
            UiKit.Stretch(screen);
            switch (debugStartScreen)
            {
                case 1: ShowSetup(); break;
                case 2: ShowOptions(); break;
                case 3: ShowGallery(); break;
                default: ShowMain(); break;
            }
        }

        void ClearScreen() { UiKit.Clear(screen); }

        // ---------------- Principal ----------------

        void ShowMain()
        {
            ClearScreen();
            var title = UiKit.Label(screen, "ROBA\nCOLORES", 150, TextAnchor.UpperLeft, UiKit.Ink);
            UiKit.Place(title.rectTransform, 0, 0, 0, 0, 60, 270, 760, 680);
            var sub = UiKit.Label(screen, "Pixel Art Isométrico", 38, TextAnchor.UpperLeft, UiKit.Wood);
            UiKit.Place(sub.rectTransform, 0, 0, 0, 0, 70, 210, 700, 270);

            // Franja con los siete colores de las casillas (a modo de adorno).
            string[] hex = { "D8363A", "F08A24", "F2D335", "3FAE4A", "3C78D8", "8E4FBF", "2B2B33" };
            for (int i = 0; i < hex.Length; i++)
            {
                ColorUtility.TryParseHtmlString("#" + hex[i], out var c);
                var dot = UiKit.Panel(screen, "Color", c);
                UiKit.Place(dot.rectTransform, 0, 0, 0, 0, 80 + i * 70, 130, 130 + i * 70, 168);
            }

            AddMenuButton("JUGAR", 470, ShowSetup);
            AddMenuButton("OPCIONES", 360, ShowOptions);
            AddMenuButton("PERSONALIZAR", 250, ShowGallery);

            var exit = UiKit.MakeButton(screen, "X   SALIDA", UiKit.Wood, 34, Quit);
            UiKit.Place((RectTransform)exit.transform, 0, 0, 0, 0, 60, 40, 330, 100);
        }

        void AddMenuButton(string text, float y, System.Action onClick)
        {
            var b = UiKit.MakeButton(screen, text, UiKit.WoodLight, 46, onClick);
            UiKit.Place((RectTransform)b.transform, 1, 0, 1, 0, -480, y, -80, y + 84);
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

        void ShowSetup()
        {
            ClearScreen();
            Header("Selección de personajes");
            var lbl = UiKit.Label(screen, "Jugadores:", 36, TextAnchor.MiddleLeft, UiKit.Ink);
            UiKit.Place(lbl.rectTransform, 0, 1, 0, 1, 80, -190, 330, -130);
            for (int n = 2; n <= content.settings.maxPlayers; n++)
            {
                int count = n;
                var b = UiKit.MakeButton(screen, n.ToString(), count == playerCount ? UiKit.Gold : UiKit.Wood, 40, () => { playerCount = count; ShowSetup(); });
                if (count == playerCount) b.GetComponentInChildren<Text>().color = UiKit.Ink;
                UiKit.Place((RectTransform)b.transform, 0, 1, 0, 1, 340 + (n - 2) * 90, -190, 420 + (n - 2) * 90, -130);
            }

            for (int slot = 0; slot < playerCount; slot++)
            {
                int s = slot;
                float top = -230 - slot * 92;
                var row = UiKit.Panel(screen, "Fila", UiKit.ParchmentDark);
                UiKit.Place(row.rectTransform, 0, 1, 1, 1, 80, top - 80, -80, top);
                var name = UiKit.Label(row.transform, "Jugador " + (slot + 1), 34, TextAnchor.MiddleLeft, UiKit.Ink);
                UiKit.Place(name.rectTransform, 0, 0, 0, 1, 24, 0, 300, 0);

                var prev = UiKit.MakeButton(row.transform, "<", UiKit.Wood, 44, () => Cycle(s, -1));
                UiKit.Place((RectTransform)prev.transform, 0, 0.5f, 0, 0.5f, 330, -30, 390, 30);
                var ch = content.characters[chosen[slot]];
                var swatch = UiKit.Panel(row.transform, "Color", ch.mainColor);
                UiKit.Place(swatch.rectTransform, 0, 0.5f, 0, 0.5f, 404, -30, 464, 30);
                var cname = UiKit.Label(row.transform, ch.displayName, 32, TextAnchor.MiddleLeft, UiKit.Ink);
                UiKit.Place(cname.rectTransform, 0, 0, 0, 1, 480, 0, 760, 0);
                var next = UiKit.MakeButton(row.transform, ">", UiKit.Wood, 44, () => Cycle(s, 1));
                UiKit.Place((RectTransform)next.transform, 0, 0.5f, 0, 0.5f, 770, -30, 830, 30);
            }

            var back = UiKit.MakeButton(screen, "VOLVER", UiKit.Wood, 34, ShowMain);
            UiKit.Place((RectTransform)back.transform, 0, 0, 0, 0, 60, 40, 330, 100);
            var go = UiKit.MakeButton(screen, "COMENZAR", UiKit.WoodLight, 40, StartGame);
            UiKit.Place((RectTransform)go.transform, 1, 0, 1, 0, -400, 36, -60, 106);
        }

        // Pasa al siguiente personaje libre: cada jugador conserva uno distinto.
        void Cycle(int slot, int dir)
        {
            int count = content.characters.Count;
            for (int step = 1; step <= count; step++)
            {
                int candidate = ((chosen[slot] + dir * step) % count + count) % count;
                bool taken = false;
                for (int i = 0; i < playerCount; i++) if (i != slot && chosen[i] == candidate) taken = true;
                if (!taken) { chosen[slot] = candidate; break; }
            }
            ShowSetup();
        }

        void StartGame()
        {
            var characters = new List<int>();
            var names = new List<string>();
            for (int i = 0; i < playerCount; i++) { characters.Add(chosen[i]); names.Add("Jugador " + (i + 1)); }
            SessionSetup.Set(characters, names);
            SceneManager.LoadScene("Partida");
        }

        // ---------------- Opciones y galeria ----------------

        void ShowOptions()
        {
            ClearScreen();
            Header("Opciones");
            var s = content.settings;
            var sb = new StringBuilder();
            sb.AppendLine("Duración del minijuego: " + s.minigameSeconds + " s");
            sb.AppendLine("Unidades iniciales por color: " + s.initialUnitsPerColor);
            sb.AppendLine("Penalización de tiempo (cara 6): " + s.timePenaltySeconds + " s   [valor provisional]");
            sb.AppendLine("Jugadores: " + s.minPlayers + " a " + s.maxPlayers);
            sb.AppendLine();
            sb.AppendLine("Dado de movimiento: 1, 2, 3, 4 casillas, perder turno, penalización de tiempo.");
            sb.AppendLine();
            sb.AppendLine("Estos valores se cambian en Assets/Game/Data/GameSettings.asset.");
            var text = UiKit.Label(screen, sb.ToString(), 32, TextAnchor.UpperLeft, UiKit.Ink);
            UiKit.Place(text.rectTransform, 0, 0, 1, 1, 90, 140, -90, -170);
            BackButton();
        }

        void ShowGallery()
        {
            ClearScreen();
            Header("Personajes");
            for (int i = 0; i < content.characters.Count; i++)
            {
                var ch = content.characters[i];
                var card = UiKit.Panel(screen, "Tarjeta", UiKit.ParchmentDark);
                UiKit.Place(card.rectTransform, 0, 0, 0, 0, 80 + i * 285, 220, 345 + i * 285, 540);
                var swatch = UiKit.Panel(card.transform, "Color", ch.mainColor);
                UiKit.Place(swatch.rectTransform, 0, 0, 1, 1, 30, 90, -30, -30);
                var name = UiKit.Label(card.transform, ch.displayName, 32, TextAnchor.MiddleCenter, UiKit.Ink);
                UiKit.Place(name.rectTransform, 0, 0, 1, 0, 0, 20, 0, 80);
            }
            var note = UiKit.Label(screen, "Personajes provisionales: se pueden reemplazar en Assets/Game/Data/Personajes.", 26, TextAnchor.MiddleLeft, UiKit.Wood);
            UiKit.Place(note.rectTransform, 0, 0, 1, 0, 80, 130, -80, 190);
            BackButton();
        }

        void Header(string text)
        {
            var t = UiKit.Label(screen, text, 76, TextAnchor.MiddleLeft, UiKit.Ink);
            UiKit.Place(t.rectTransform, 0, 1, 1, 1, 80, -140, -80, -30);
        }

        void BackButton()
        {
            var back = UiKit.MakeButton(screen, "VOLVER", UiKit.Wood, 34, ShowMain);
            UiKit.Place((RectTransform)back.transform, 0, 0, 0, 0, 60, 40, 330, 100);
        }
    }
}
