using System.IO;
using UnityEditor;
using UnityEngine;

namespace RobaColores.Map.Editor
{
    // Genera por código el atlas pixel art (128x128, celdas de 16x16).
    public static class MapAtlasGenerator
    {
        public const string AtlasPath = "Assets/Map/Textures/MapAtlas.png";

        // Índices de celda del atlas.
        public const int GrassTop = 0, GrassSide = 1, Dirt = 2, Sand = 3, Planks = 4, Rock = 5, Water = 6, Parchment = 7;
        public const int Trunk = 8, Leaves = 9, Bone = 10, Iron = 11, ClothBlack = 12, Gold = 13, PlankDark = 14, Rope = 15;
        public const int TileRed = 16, TileOrange = 17, TileYellow = 18, TileGreen = 19, TileBlue = 20, TilePurple = 21, TileBlack = 22, Outline = 23;
        public const int KrakenDark = 24, KrakenLight = 25, Sail = 26, WaterLight = 27;

        const int Size = 128, Cell = 16;
        static Color32[] px;

        public static void Generate(MapPalette p)
        {
            px = new Color32[Size * Size];

            Solid(GrassTop, p.grass); Speckle(GrassTop, Mul(p.grass, 1.2f), 0.12f); Speckle(GrassTop, Mul(p.grass, 0.8f), 0.12f); Edge(GrassTop, 0.78f);

            Solid(GrassSide, Hex("8A5A3A")); Speckle(GrassSide, Hex("6F4529"), 0.15f);
            for (int x = 0; x < Cell; x++)
            {
                int h = 4 + (Hash(x, 7) % 3 == 0 ? 1 : 0);
                for (int y = Cell - h; y < Cell; y++) Set(GrassSide, x, y, y == Cell - h ? Mul(p.grass, 0.8f) : p.grass);
            }
            Edge(GrassSide, 0.78f);

            Solid(Dirt, Hex("8A5A3A")); Speckle(Dirt, Hex("6F4529"), 0.18f); Speckle(Dirt, Hex("A06C48"), 0.1f); Edge(Dirt, 0.78f);
            Solid(Sand, p.sand); Speckle(Sand, Mul(p.sand, 0.88f), 0.14f); Speckle(Sand, Mul(p.sand, 1.08f), 0.1f); Edge(Sand, 0.85f);

            Solid(Planks, p.wood);
            for (int y = 3; y < Cell; y += 4) for (int x = 0; x < Cell; x++) Set(Planks, x, y, Mul(p.wood, 0.65f));
            for (int r = 0; r < 4; r++) { int x = (r * 7 + 3) % Cell; for (int y = r * 4; y < r * 4 + 3; y++) Set(Planks, x, y, Mul(p.wood, 0.75f)); }
            Speckle(Planks, Mul(p.wood, 1.15f), 0.07f); Edge(Planks, 0.7f);

            Solid(Rock, p.rock); Speckle(Rock, Mul(p.rock, 1.25f), 0.14f); Speckle(Rock, Mul(p.rock, 0.7f), 0.1f);
            for (int i = 0; i < 6; i++) Set(Rock, 3 + i, 9 - i / 2, Mul(p.rock, 0.55f));
            Edge(Rock, 0.7f);

            Solid(Water, p.water);
            for (int y = 2; y < Cell; y += 5) for (int x = 0; x < 6; x++) Set(Water, (x + y * 3) % Cell, y, Mul(p.water, 1.25f));
            Speckle(Water, Mul(p.water, 0.85f), 0.08f);

            Solid(Parchment, p.parchment); Speckle(Parchment, Mul(p.parchment, 0.9f), 0.15f); Edge(Parchment, 0.8f);

            Solid(Trunk, Hex("7A4E2A")); for (int x = 1; x < Cell; x += 4) for (int y = 0; y < Cell; y++) Set(Trunk, x, y, Hex("5E3A1C")); Edge(Trunk, 0.75f);
            Solid(Leaves, Hex("3F8F3A")); Speckle(Leaves, Hex("2C6B2C"), 0.25f); Speckle(Leaves, Hex("63B04F"), 0.18f); Edge(Leaves, 0.75f);
            Solid(Bone, Hex("F2ECD8")); Speckle(Bone, Hex("D8D0B8"), 0.15f); Edge(Bone, 0.8f);
            Solid(Iron, Hex("4A4A55")); Speckle(Iron, Hex("63636F"), 0.15f); Edge(Iron, 0.7f);
            Solid(ClothBlack, Hex("24202C")); Speckle(ClothBlack, Hex("34303E"), 0.1f);
            Solid(Gold, Hex("F2C230")); Speckle(Gold, Hex("D4A020"), 0.15f); Edge(Gold, 0.75f);
            Solid(PlankDark, Hex("5E3A1C"));
            for (int y = 3; y < Cell; y += 4) for (int x = 0; x < Cell; x++) Set(PlankDark, x, y, Hex("3F2610"));
            Edge(PlankDark, 0.7f);
            Solid(Rope, Hex("C9A66B"));
            for (int i = 0; i < Cell; i++) { Set(Rope, i, i, Hex("9C7C48")); Set(Rope, (i + 8) % Cell, i, Hex("9C7C48")); }

            Solid(TileRed, p.tileRed); Solid(TileOrange, p.tileOrange); Solid(TileYellow, p.tileYellow); Solid(TileGreen, p.tileGreen);
            Solid(TileBlue, p.tileBlue); Solid(TilePurple, p.tilePurple); Solid(TileBlack, p.tileBlack);
            Solid(Outline, p.outline);

            Solid(KrakenDark, Hex("7A3FA0")); Speckle(KrakenDark, Hex("5E2D80"), 0.2f); Edge(KrakenDark, 0.75f);
            Solid(KrakenLight, Hex("C9A0DC")); Speckle(KrakenLight, Hex("B088C8"), 0.15f);
            Solid(Sail, Hex("F4EFE0")); for (int y = 4; y < Cell; y += 6) for (int x = 0; x < Cell; x++) Set(Sail, x, y, Hex("DCD4BC")); Edge(Sail, 0.85f);
            Solid(WaterLight, Hex("9CEBEE")); Speckle(WaterLight, Hex("C4F6F7"), 0.15f);

            var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
            tex.SetPixels32(px);
            File.WriteAllBytes(AtlasPath, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(AtlasPath, ImportAssetOptions.ForceUpdate);
        }

        static void Set(int cell, int x, int y, Color c)
        {
            int ox = (cell % VoxelMeshBuilder.Cells) * Cell, oy = (cell / VoxelMeshBuilder.Cells) * Cell;
            px[(oy + y) * Size + ox + x] = c;
        }

        static Color32 Get(int cell, int x, int y)
        {
            int ox = (cell % VoxelMeshBuilder.Cells) * Cell, oy = (cell / VoxelMeshBuilder.Cells) * Cell;
            return px[(oy + y) * Size + ox + x];
        }

        static void Solid(int cell, Color c) { for (int y = 0; y < Cell; y++) for (int x = 0; x < Cell; x++) Set(cell, x, y, c); }

        static void Speckle(int cell, Color c, float density)
        {
            int s = cell * 977 + 13 + (int)(density * 1000);
            for (int y = 0; y < Cell; y++) for (int x = 0; x < Cell; x++)
                if (Hash(x * 31 + y * 17, s) % 1000 < density * 1000) Set(cell, x, y, c);
        }

        static void Edge(int cell, float factor)
        {
            for (int i = 0; i < Cell; i++)
            {
                Dim(cell, i, 0, factor); Dim(cell, i, Cell - 1, factor);
                Dim(cell, 0, i, factor); Dim(cell, Cell - 1, i, factor);
            }
        }

        static void Dim(int cell, int x, int y, float f) { Set(cell, x, y, Mul(Get(cell, x, y), f)); }

        static Color Mul(Color c, float f) { return new Color(Mathf.Clamp01(c.r * f), Mathf.Clamp01(c.g * f), Mathf.Clamp01(c.b * f), 1f); }

        static Color Hex(string h) { ColorUtility.TryParseHtmlString("#" + h, out var c); return c; }

        static int Hash(int a, int b)
        {
            unchecked
            {
                uint h = (uint)(a * 73856093) ^ (uint)(b * 19349663);
                h ^= h >> 13; h *= 0x5bd1e995; h ^= h >> 15;
                return (int)(h & 0x7fffffff);
            }
        }
    }
}
