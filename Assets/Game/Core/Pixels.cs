using System;
using System.Collections.Generic;

namespace RobaColores.Game.Core
{
    // Cuadricula de pixel art. Cada celda guarda un indice de paleta o Empty (-1).
    public sealed class PixelGrid
    {
        public const int Empty = -1;

        readonly int[] cells;
        public int Width { get; private set; }
        public int Height { get; private set; }

        public PixelGrid(int width, int height)
        {
            if (width <= 0 || height <= 0) throw new ArgumentException("Dimensiones invalidas.");
            Width = width; Height = height;
            cells = new int[width * height];
            for (int i = 0; i < cells.Length; i++) cells[i] = Empty;
        }

        public bool InBounds(int x, int y) { return x >= 0 && y >= 0 && x < Width && y < Height; }

        public int Get(int x, int y)
        {
            if (!InBounds(x, y)) throw new ArgumentOutOfRangeException();
            return cells[y * Width + x];
        }

        // Uso interno: la escritura real pasa por MinigameRound para respetar el inventario y el bloqueo.
        internal void SetInternal(int x, int y, int color) { cells[y * Width + x] = color; }

        public int[] Snapshot() { return (int[])cells.Clone(); }

        public int[] CountsByColor(int paletteSize)
        {
            var counts = new int[paletteSize];
            foreach (var c in cells) if (c >= 0) counts[c]++;
            return counts;
        }

        public int[] RowCounts(int row, int paletteSize)
        {
            if (row < 0 || row >= Height) throw new ArgumentOutOfRangeException("row");
            var counts = new int[paletteSize];
            for (int x = 0; x < Width; x++)
            {
                int c = cells[row * Width + x];
                if (c >= 0) counts[c]++;
            }
            return counts;
        }

        internal void ClearRow(int row)
        {
            for (int x = 0; x < Width; x++) cells[row * Width + x] = Empty;
        }
    }

    // Figura de referencia. Cells en orden de filas (y=0 arriba), -1 = sin pintar.
    public sealed class FigureDefinition
    {
        public string Id;
        public string Name;
        public Category Category;
        public Difficulty Difficulty;
        public int Width;
        public int Height;
        public int[] Cells;

        public int ColoredPixels
        {
            get { int n = 0; foreach (var c in Cells) if (c >= 0) n++; return n; }
        }

        public int CellAt(int x, int y) { return Cells[y * Width + x]; }

        public int[] RequiredUnits(int paletteSize)
        {
            var req = new int[paletteSize];
            foreach (var c in Cells) if (c >= 0) req[c]++;
            return req;
        }

        public void Validate(int paletteSize)
        {
            if (string.IsNullOrEmpty(Id)) throw new InvalidOperationException("Figura sin Id.");
            if (Width <= 0 || Height <= 0 || Cells == null || Cells.Length != Width * Height)
                throw new InvalidOperationException("Figura con dimensiones inconsistentes: " + Id);
            foreach (var c in Cells)
                if (c < -1 || c >= paletteSize) throw new InvalidOperationException("Indice de paleta invalido en " + Id);
            if (ColoredPixels == 0) throw new InvalidOperationException("Figura vacia: " + Id);
        }
    }

    // Catalogo de figuras. Anadir figuras no requiere tocar el resto del juego.
    public sealed class FigureCatalog
    {
        readonly List<FigureDefinition> figures = new List<FigureDefinition>();
        public IList<FigureDefinition> All { get { return figures; } }

        public void Add(FigureDefinition f)
        {
            foreach (var other in figures) if (other.Id == f.Id) throw new ArgumentException("Id de figura repetido: " + f.Id);
            figures.Add(f);
        }

        public List<FigureDefinition> Find(Category category, Difficulty? difficulty)
        {
            var res = new List<FigureDefinition>();
            foreach (var f in figures)
                if (f.Category == category && (!difficulty.HasValue || f.Difficulty == difficulty.Value)) res.Add(f);
            return res;
        }

        // Figura de la categoria pedida. Prefiere la dificultad indicada; si no hay, usa otra de la misma categoria.
        public FigureDefinition Pick(Category category, Difficulty difficulty, IRandom rng)
        {
            var list = Find(category, difficulty);
            if (list.Count == 0) list = Find(category, null);
            if (list.Count == 0) throw new InvalidOperationException("No hay figuras de la categoria " + category);
            return list[rng.Next(0, list.Count)];
        }
    }

    public sealed class EvaluationResult
    {
        public int Correct;       // pixeles con el color correcto
        public int Wrong;         // pintados con color equivocado o sobre una celda vacia de la referencia
        public int Missing;       // celdas de la referencia aun sin pintar
        public int ReferencePixels;
        public double Score;      // 0..100
        public double Similarity; // 0..1: correctos / (pixeles de la referencia + pixeles de mas)
    }

    // Evaluacion pura y reproducible: mismas entradas, misma puntuacion.
    public static class FigureEvaluator
    {
        public static EvaluationResult Evaluate(FigureDefinition reference, PixelGrid grid, ScoreWeights w)
        {
            if (grid.Width != reference.Width || grid.Height != reference.Height)
                throw new ArgumentException("La cuadricula no coincide con las dimensiones de la figura.");

            var r = new EvaluationResult { ReferencePixels = reference.ColoredPixels };
            int extra = 0;
            for (int y = 0; y < reference.Height; y++)
                for (int x = 0; x < reference.Width; x++)
                {
                    int want = reference.CellAt(x, y), got = grid.Get(x, y);
                    if (want >= 0)
                    {
                        if (got == want) r.Correct++;
                        else if (got == PixelGrid.Empty) r.Missing++;
                        else r.Wrong++;
                    }
                    else if (got >= 0) { r.Wrong++; extra++; }
                }

            double raw = (r.Correct * w.Correct - r.Wrong * w.Wrong - r.Missing * w.Missing) / r.ReferencePixels;
            r.Score = Math.Max(0.0, raw) * 100.0;
            r.Similarity = (double)r.Correct / (r.ReferencePixels + extra);
            return r;
        }
    }
}
