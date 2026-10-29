using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using RobaColores.Game.Core;

namespace RobaColores.Game.Presentation
{
    // Interfaz del minijuego de Pixel Art: referencia, temporizador global y, por cada participante,
    // su cuadricula propia, su paleta con el inventario y su boton de terminar.
    // El layout usa anclas relativas, asi que se adapta a cualquier relacion de aspecto.
    public sealed class MinigameView : MonoBehaviour
    {
        sealed class PlayerPanel
        {
            public int playerId;
            public int width, height;
            public Image[] cells;
            public Text[] counts;
            public GameObject[] frames;
            public Text notice;
            public float noticeUntil;
            public Button finish;
            public int selected;          // indice de paleta, o -1 = borrar
            public RectTransform gridArea;
            public GridLayoutGroup gridLayout;
        }

        GameSession session;
        GameContent content;
        Action<int> onFinishPressed;
        RectTransform root;
        Text timerText;
        readonly List<PlayerPanel> panels = new List<PlayerPanel>();
        RectTransform refArea;
        GridLayoutGroup refLayout;
        bool fitted;

        public static MinigameView Create(Transform canvas, GameSession s, GameContent c, IList<string> names, IList<Color> colors, Action<int> onFinish)
        {
            var go = new GameObject("MinigameView", typeof(RectTransform));
            go.transform.SetParent(canvas, false);
            var view = go.AddComponent<MinigameView>();
            view.Build(s, c, names, colors, onFinish);
            return view;
        }

        void Build(GameSession s, GameContent c, IList<string> names, IList<Color> colors, Action<int> onFinish)
        {
            session = s; content = c; onFinishPressed = onFinish;
            var round = s.Round;
            root = (RectTransform)transform;
            UiKit.Stretch(root);
            var bg = UiKit.Panel(root, "Fondo", new Color(0.025f, 0.06f, 0.11f, 0.68f));
            UiKit.Stretch(bg.rectTransform);

            var header = MenuWidgets.FramePanel(root, new Color32(65, 42, 31, 246));
            UiKit.Place(header.rectTransform, 0, 1, 1, 1, 0, -105, 0, 0);

            var refBox = MenuWidgets.FramePanel(root, UiKit.Parchment);
            UiKit.Place(refBox.rectTransform, 0, 1, 0, 1, 18, -324, 326, -16);
            var refTitle = UiKit.Label(refBox.transform, "MAPA DEL DESAFÍO", 18, TextAnchor.MiddleCenter, UiKit.Ink);
            UiKit.Place(refTitle.rectTransform, 0, 1, 1, 1, 4, -30, -4, -4);
            var refGrid = UiKit.Rect(refBox.transform, "Cuadricula");
            UiKit.Place(refGrid, 0, 0, 1, 1, 12, 12, -12, -38);
            refArea = refGrid;
            var refCells = BuildGrid(refGrid, round.Figure.Width, round.Figure.Height, null, out refLayout);
            for (int y = 0; y < round.Figure.Height; y++)
                for (int x = 0; x < round.Figure.Width; x++)
                    refCells[y * round.Figure.Width + x].color = CellColor(round.Figure.CellAt(x, y));

            var eyebrow = UiKit.Label(root, "DESAFÍO DE PIXEL ART", 18, TextAnchor.MiddleLeft, new Color32(232, 192, 111, 255));
            UiKit.Place(eyebrow.rectTransform, 0, 1, 0, 1, 354, -34, 520, -8);
            var cat = UiKit.Label(root, GameText.CategoryName(round.Category), 34, TextAnchor.MiddleLeft, UiKit.Parchment);
            UiKit.Place(cat.rectTransform, 0, 1, 0, 1, 354, -82, 560, -34);
            timerText = UiKit.Label(root, "", 52, TextAnchor.MiddleCenter, UiKit.Parchment);
            UiKit.Place(timerText.rectTransform, 0.72f, 1, 0.86f, 1, 0, -92, 0, -16);
            var simultaneous = UiKit.Label(root, "TODOS JUEGAN A LA VEZ", 20, TextAnchor.MiddleCenter, UiKit.Gold);
            UiKit.Place(simultaneous.rectTransform, 0.85f, 1, 1, 1, 0, -72, -18, -24);
            var hint = UiKit.Label(root, "Clic izquierdo: pintar · clic derecho o X: borrar\nCualquier jugador puede terminar el desafío.", 16, TextAnchor.MiddleLeft, UiKit.Parchment);
            UiKit.Place(hint.rectTransform, 0.52f, 1, 0.86f, 1, 0, -156, 0, -108);

            // Un panel por participante, repartidos en el ancho.
            var ids = new List<int>(round.ParticipantIds);
            ids.Sort();
            int n = ids.Count;
            for (int i = 0; i < n; i++)
                BuildPanel(ids[i], (float)i / n, (float)(i + 1) / n, names[ids[i]], colors[ids[i]], n >= 4);
        }

        void BuildPanel(int playerId, float ax0, float ax1, string playerName, Color playerColor, bool compact)
        {
            var round = session.Round;
            var p = new PlayerPanel { playerId = playerId, width = round.Figure.Width, height = round.Figure.Height, selected = 0 };
            var panel = MenuWidgets.FramePanel(root, new Color32(58, 39, 31, 250));
            panel.name = "Jugador" + playerId;
            UiKit.Place(panel.rectTransform, ax0, 0.015f, ax1, 0.54f, 8, 0, -8, 0);

            var header = MenuWidgets.FramePanel(panel.transform, playerColor);
            UiKit.Place(header.rectTransform, 0, 1, 1, 1, 10, -49, -10, -8);
            var title = UiKit.Label(header.transform, playerName, compact ? 19 : 26, TextAnchor.MiddleCenter, Color.white);
            UiKit.Stretch(title.rectTransform);

            var gridArea = MenuWidgets.FramePanel(panel.transform, UiKit.ParchmentDark);
            UiKit.Place(gridArea.rectTransform, 0, 0, 1, 1, 14, 116, -14, -58);
            var grid = UiKit.Rect(gridArea.transform, "Cuadricula");
            UiKit.Place(grid, 0, 0, 1, 1, 10, 10, -10, -10);
            p.gridArea = grid;
            p.cells = BuildGrid(grid, p.width, p.height, (cx, cy, paint) => OnCell(p, cx, cy, paint), out p.gridLayout);

            p.notice = UiKit.Label(gridArea.transform, "", 22, TextAnchor.MiddleCenter, UiKit.Gold);
            UiKit.Stretch(p.notice.rectTransform);

            // Paleta (con unidades disponibles del inventario) + goma.
            int colorCount = content.palette.Count;
            var paletteRow = UiKit.Rect(panel.transform, "Paleta");
            UiKit.Place(paletteRow, 0, 0, 1, 0, 12, 62, -12, 112);
            p.counts = new Text[colorCount];
            p.frames = new GameObject[colorCount + 1];
            for (int c = 0; c <= colorCount; c++)
            {
                int index = c < colorCount ? c : -1;
                float f0 = (float)c / (colorCount + 1), f1 = (float)(c + 1) / (colorCount + 1);
                var frame = MenuWidgets.FramePanel(paletteRow, UiKit.Gold);
                UiKit.Place(frame.rectTransform, f0, 0, f1, 1, 0, 0, 0, 0);
                frame.gameObject.SetActive(index == p.selected);
                p.frames[c] = frame.gameObject;
                var color = index >= 0 ? content.palette.entries[index].color : UiKit.ParchmentDark;
                var btn = MenuWidgets.FrameButton(paletteRow, index >= 0 ? "" : "X", compact ? 18 : 22, () => Select(p, index), color);
                UiKit.Place((RectTransform)btn.transform, f0, 0, f1, 1, 3, 3, -3, -3);
                if (index >= 0)
                {
                    var count = UiKit.Label(btn.transform, "", compact ? 10 : 13, TextAnchor.LowerCenter, ContrastText(color));
                    UiKit.Stretch(count.rectTransform);
                    p.counts[index] = count;
                }
                else btn.GetComponentInChildren<Text>().color = UiKit.Ink;
            }

            p.finish = MenuWidgets.FrameButton(panel.transform, "TERMINAR", compact ? 18 : 24, () => onFinishPressed(playerId), new Color32(0x9C, 0x2B, 0x2B, 255));
            UiKit.Place((RectTransform)p.finish.transform, 0, 0, 1, 0, 14, 10, -14, 52);

            panels.Add(p);
            RefreshCounts(p);
        }

        Image[] BuildGrid(RectTransform parent, int w, int h, Action<int, int, bool> onCell, out GridLayoutGroup layout)
        {
            layout = parent.gameObject.AddComponent<GridLayoutGroup>();
            layout.cellSize = new Vector2(20, 20);
            layout.spacing = new Vector2(2f, 2f);
            layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            layout.constraintCount = w;
            layout.childAlignment = TextAnchor.MiddleCenter;
            var cells = new Image[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    var img = UiKit.Panel(parent, "c" + x + "_" + y, CellColor(PixelGrid.Empty));
                    cells[y * w + x] = img;
                    if (onCell != null)
                    {
                        int cx = x, cy = y;
                        img.gameObject.AddComponent<CellHandler>().onAction = paint => onCell(cx, cy, paint);
                    }
                    else img.raycastTarget = false;
                }
            return cells;
        }

        // El tamano de celda depende del rectangulo real disponible: se ajusta en cuanto el layout existe.
        void Fit(GridLayoutGroup layout, RectTransform area, int w, int h)
        {
            var r = area.rect;
            float cell = Mathf.Floor(Mathf.Min(
                (r.width - (w - 1) * layout.spacing.x) / w,
                (r.height - (h - 1) * layout.spacing.y) / h));
            if (cell < 4f) cell = 4f;
            layout.cellSize = new Vector2(cell, cell);
        }

        Color CellColor(int paletteIndex)
        {
            return paletteIndex >= 0 ? content.palette.entries[paletteIndex].color : new Color32(0xE6, 0xD9, 0xAE, 255);
        }

        static Color ContrastText(Color c)
        {
            return (c.r * 0.3f + c.g * 0.6f + c.b * 0.1f) > 0.55f ? UiKit.Ink : Color.white;
        }

        void Select(PlayerPanel p, int index)
        {
            p.selected = index;
            int colorCount = content.palette.Count;
            for (int c = 0; c <= colorCount; c++) p.frames[c].SetActive((c < colorCount ? c : -1) == index);
        }

        void OnCell(PlayerPanel p, int x, int y, bool paint)
        {
            if (session.Phase != GamePhase.MinigameRunning) return;
            var round = session.Round;
            PaintResult res = (paint && p.selected >= 0) ? round.TryPaint(p.playerId, x, y, p.selected) : round.TryErase(p.playerId, x, y);
            if (res == PaintResult.Ok)
            {
                p.cells[y * p.width + x].color = CellColor(round.GridOf(p.playerId).Get(x, y));
                RefreshCounts(p);
            }
            else if (res == PaintResult.NoUnits) Notice(p, "Sin unidades\nde ese color");
        }

        void Notice(PlayerPanel p, string text)
        {
            p.notice.text = text;
            p.noticeUntil = Time.unscaledTime + 1.5f;
        }

        void RefreshCounts(PlayerPanel p)
        {
            var inv = session.GetPlayer(p.playerId).Inventory;
            for (int c = 0; c < p.counts.Length; c++) p.counts[c].text = inv.Get(c).ToString();
        }

        // Llamado cada frame por el controlador mientras el minijuego esta en curso.
        public void Tick()
        {
            var round = session.Round;
            if (round == null) return;
            if (!fitted && refArea.rect.width > 8f)
            {
                Fit(refLayout, refArea, round.Figure.Width, round.Figure.Height);
                foreach (var p in panels) Fit(p.gridLayout, p.gridArea, p.width, p.height);
                fitted = true;
            }

            int secs = Mathf.CeilToInt(round.TimeRemaining);
            timerText.text = (secs / 60).ToString("00") + ":" + (secs % 60).ToString("00");
            timerText.color = secs <= 10 ? new Color32(0xFF, 0x6A, 0x5A, 255) : UiKit.Parchment;
            foreach (var p in panels)
            {
                float lockLeft = round.LockRemaining(p.playerId);
                if (lockLeft > 0f) { p.notice.text = "PENALIZACIÓN\n" + Mathf.CeilToInt(lockLeft) + " s"; p.noticeUntil = 0f; }
                else if (p.noticeUntil == 0f && p.notice.text.StartsWith("PENALIZACI")) p.notice.text = "";
                else if (p.noticeUntil > 0f && Time.unscaledTime > p.noticeUntil) { p.notice.text = ""; p.noticeUntil = 0f; }
                p.finish.interactable = !round.Finished;
            }
        }

        public void Close() { Destroy(gameObject); }
    }
}
