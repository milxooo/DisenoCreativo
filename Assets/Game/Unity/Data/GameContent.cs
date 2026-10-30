using System.Collections.Generic;
using UnityEngine;
using RobaColores.Game.Core;

namespace RobaColores.Game.Presentation
{
    // Punto unico de acceso a los datos del juego (se referencia desde las escenas).
    [CreateAssetMenu(menuName = "Roba Colores/Contenido del juego", fileName = "GameContent")]
    public class GameContent : ScriptableObject
    {
        public GameSettingsAsset settings;
        public PaletteAsset palette;
        public List<FigureAsset> figures = new List<FigureAsset>();
        public List<CharacterAsset> characters = new List<CharacterAsset>();
        public Font uiFont;
        public GameObject markerPrefab;   // marcador dorado para las opciones de camino
        public MenuArt menuArt;           // imagenes y fondo del menu principal

        public FigureCatalog BuildCatalog()
        {
            var catalog = new FigureCatalog();
            foreach (var f in figures) catalog.Add(f.ToDefinition(palette));
            return catalog;
        }

        public GameConfig BuildConfig() { return settings.ToConfig(palette.Count); }
    }
}
