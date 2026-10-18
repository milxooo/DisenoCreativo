using UnityEngine;

namespace RobaColores.Map
{
    [CreateAssetMenu(menuName = "Roba Colores/Map Palette", fileName = "MapPalette")]
    public class MapPalette : ScriptableObject
    {
        [Header("Entorno")]
        public Color outline = Hex("1A1423");
        public Color sand = Hex("E3CE8C");
        public Color wood = Hex("8B5A2B");
        public Color water = Hex("3FC1C9");
        public Color rock = Hex("6B6478");
        public Color grass = Hex("5E9E3C");
        public Color parchment = Hex("EBDDB5");

        [Header("Casillas")]
        public Color tileRed = Hex("D8363A");
        public Color tileOrange = Hex("F08A24");
        public Color tileYellow = Hex("F2D335");
        public Color tileGreen = Hex("3FAE4A");
        public Color tileBlue = Hex("3C78D8");
        public Color tilePurple = Hex("8E4FBF");
        public Color tileBlack = Hex("2B2B33");

        public Color GetTileColor(PathTileColor c)
        {
            switch (c)
            {
                case PathTileColor.Rojo: return tileRed;
                case PathTileColor.Naranja: return tileOrange;
                case PathTileColor.Amarillo: return tileYellow;
                case PathTileColor.Verde: return tileGreen;
                case PathTileColor.Azul: return tileBlue;
                case PathTileColor.Morado: return tilePurple;
                default: return tileBlack;
            }
        }

        static Color Hex(string hex)
        {
            ColorUtility.TryParseHtmlString("#" + hex, out var c);
            return c;
        }
    }
}
