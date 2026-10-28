using System;
using System.Collections.Generic;
using UnityEngine;

namespace RobaColores.Game.Presentation
{
    // Paleta de dibujo (distinta de los colores de las casillas). Indice = id de color usado por el nucleo.
    [CreateAssetMenu(menuName = "Roba Colores/Paleta de dibujo", fileName = "Palette")]
    public class PaletteAsset : ScriptableObject
    {
        [Serializable]
        public struct Entry
        {
            public string name;
            public char letter;      // letra usada al escribir figuras ('.' esta reservada para "vacio")
            public Color color;
        }

        public List<Entry> entries = new List<Entry>();

        public int Count { get { return entries.Count; } }

        public int IndexOfLetter(char letter)
        {
            for (int i = 0; i < entries.Count; i++) if (entries[i].letter == letter) return i;
            return -1;
        }
    }
}
