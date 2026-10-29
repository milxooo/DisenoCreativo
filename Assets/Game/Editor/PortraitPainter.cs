using System.IO;
using UnityEditor;
using UnityEngine;

namespace RobaColores.Game.EditorTools
{
    // Retratos PROVISIONALES de 32x32 px (busto pirata) para la seleccion de personajes.
    // Se sustituyen asignando una imagen real en CharacterAsset.portrait.
    public static class PortraitPainter
    {
        const int S = 32;
        static readonly Color32 Skin = new Color32(0xF0, 0xC8, 0xA0, 255);
        static readonly Color32 Ink = new Color32(0x1A, 0x14, 0x23, 255);
        static readonly Color32 Brown = new Color32(0x6B, 0x44, 0x23, 255);
        static readonly Color32 Gold = new Color32(0xF2, 0xC2, 0x30, 255);
        static readonly Color32 Cloth = new Color32(0xF4, 0xEF, 0xE0, 255);
        static readonly Color32 Clear = new Color32(0, 0, 0, 0);

        public static Sprite Build(string folder, string key, Color main, int variant)
        {
            if (!AssetDatabase.IsValidFolder(folder))
            {
                string parent = Path.GetDirectoryName(folder).Replace('\\', '/');
                AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
            }
            var px = new Color32[S * S];
            for (int i = 0; i < px.Length; i++) px[i] = Clear;
            Color32 main32 = main;
            Color32 dark = new Color32((byte)(main32.r * 0.65f), (byte)(main32.g * 0.65f), (byte)(main32.b * 0.65f), 255);

            // Casaca
            for (int y = 0; y <= 9; y++) for (int x = 6; x < 26; x++) Set(px, x, y, (x < 8 || x > 23) && y > 6 ? Clear : main32);
            for (int x = 6; x < 26; x++) Set(px, x, 0, dark);
            for (int y = 8; y <= 10; y++) for (int x = 13; x <= 18; x++) Set(px, x, y, Cloth);       // cuello
            for (int y = 8; y <= 9; y++) { Set(px, 15, y, dark); Set(px, 16, y, dark); }
            // Cabeza
            for (int y = 11; y <= 21; y++)
                for (int x = 10; x <= 21; x++)
                {
                    bool corner = (x == 10 || x == 21) && (y == 11 || y == 21);
                    if (!corner) Set(px, x, y, Skin);
                }
            Set(px, 13, 16, Ink); Set(px, 13, 15, Ink); Set(px, 18, 16, Ink); Set(px, 18, 15, Ink);   // ojos
            for (int x = 14; x <= 17; x++) Set(px, x, 13, new Color32(0xA8, 0x45, 0x3A, 255));        // boca

            // Variantes para distinguirlos
            switch (variant % 6)
            {
                case 1: for (int y = 14; y <= 17; y++) for (int x = 17; x <= 19; x++) Set(px, x, y, Ink); for (int x = 10; x <= 21; x++) Set(px, x, 19, Ink); break;   // parche
                case 2: for (int y = 10; y <= 13; y++) for (int x = 11; x <= 20; x++) if (!(y == 13 && x >= 14 && x <= 17)) Set(px, x, y, Brown); break;                 // barba
                case 3: Set(px, 9, 14, Gold); Set(px, 9, 13, Gold); Set(px, 22, 14, Gold); break;                                                                       // aretes
                case 4: for (int x = 12; x <= 19; x++) Set(px, x, 14, Brown); break;                                                                                     // bigote
                case 5: for (int y = 22; y <= 26; y++) for (int x = 9; x <= 22; x++) Set(px, x, y, main32); Set(px, 23, 22, main32); Set(px, 24, 21, main32); break;     // panuelo
            }

            // Sombrero tricornio (salvo la variante con panuelo)
            if (variant % 6 != 5)
            {
                for (int x = 5; x <= 26; x++) { Set(px, x, 22, Ink); if (x >= 7 && x <= 24) Set(px, x, 23, Ink); }
                for (int y = 24; y <= 28; y++) for (int x = 9; x <= 22; x++) Set(px, x, y, Ink);
                for (int x = 9; x <= 22; x++) { Set(px, x, 24, main32); Set(px, x, 25, main32); }
                Set(px, 15, 27, Cloth); Set(px, 16, 27, Cloth); Set(px, 15, 26, Cloth); Set(px, 16, 26, Cloth);
            }

            // Contorno oscuro
            var outlined = (Color32[])px.Clone();
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    if (px[y * S + x].a != 0) continue;
                    bool near = (x > 0 && px[y * S + x - 1].a != 0) || (x < S - 1 && px[y * S + x + 1].a != 0) ||
                                (y > 0 && px[(y - 1) * S + x].a != 0) || (y < S - 1 && px[(y + 1) * S + x].a != 0);
                    if (near) outlined[y * S + x] = Ink;
                }

            var tex = new Texture2D(S, S, TextureFormat.RGBA32, false);
            tex.SetPixels32(outlined);
            string path = folder + "/Retrato_" + key + ".png";
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 16;
            importer.filterMode = FilterMode.Point;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.alphaIsTransparency = true;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        static void Set(Color32[] px, int x, int y, Color32 c)
        {
            if (x >= 0 && y >= 0 && x < S && y < S) px[y * S + x] = c;
        }
    }
}
