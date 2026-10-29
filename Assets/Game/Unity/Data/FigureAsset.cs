using System;
using System.Collections.Generic;
using UnityEngine;
using RobaColores.Game.Core;

namespace RobaColores.Game.Presentation
{
    // Figura de referencia editable como texto: una fila por linea, '.' = vacio, el resto letras de la paleta.
    [CreateAssetMenu(menuName = "Roba Colores/Figura", fileName = "Figura")]
    public class FigureAsset : ScriptableObject
    {
        public string figureId;
        public string displayName;
        public Category category;
        public Difficulty difficulty;
        [TextArea(4, 16)] public string rows;

        public FigureDefinition ToDefinition(PaletteAsset palette)
        {
            var lines = new List<string>();
            foreach (var raw in rows.Replace("\r", "").Split('\n'))
            {
                var line = raw.Trim();
                if (line.Length > 0) lines.Add(line);
            }
            if (lines.Count == 0) throw new InvalidOperationException("Figura sin filas: " + name);
            int w = lines[0].Length;
            var cells = new int[w * lines.Count];
            for (int y = 0; y < lines.Count; y++)
            {
                if (lines[y].Length != w) throw new InvalidOperationException("Fila " + y + " de " + name + " mide " + lines[y].Length + " y deberia medir " + w);
                for (int x = 0; x < w; x++)
                {
                    char c = lines[y][x];
                    int idx = c == '.' ? PixelGrid.Empty : palette.IndexOfLetter(c);
                    if (c != '.' && idx < 0) throw new InvalidOperationException("Letra desconocida '" + c + "' en " + name);
                    cells[y * w + x] = idx;
                }
            }
            return new FigureDefinition { Id = figureId, Name = displayName, Category = category, Difficulty = difficulty, Width = w, Height = lines.Count, Cells = cells };
        }
    }
}
