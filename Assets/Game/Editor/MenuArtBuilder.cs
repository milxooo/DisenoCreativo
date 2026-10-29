using System.IO;
using UnityEditor;
using UnityEngine;
using RobaColores.Game.Presentation;

namespace RobaColores.Game.EditorTools
{
    // Recorta del boceto (Assets/Game/Art/Menu/Mockup_Menu.png) las imagenes PROVISIONALES del menu: titulo con kraken y
    // pirata, e iconos de los botones. El blanco del papel pasa a transparente y queda solo el trazo de lapiz.
    // Las coordenadas estan en pixeles del boceto original (605 x 469, origen arriba a la izquierda).
    public static class MenuArtBuilder
    {
        const string Dir = "Assets/Game/Art/Menu";
        const string MockupPath = Dir + "/Mockup_Menu.png";
        static readonly Color32 Ink = new Color32(0x2E, 0x28, 0x20, 255);

        public static MenuArt Run()
        {
            var src = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            src.LoadImage(File.ReadAllBytes(MockupPath));
            float k = src.width / 605f;

            var art = Crop(src, k, 22, 10, 424, 462, "Menu_Arte", true);
            var sword = Crop(src, k, 426, 124, 460, 160, "Icono_Espada", false);
            var compass = Crop(src, k, 428, 166, 464, 202, "Icono_Brujula", false);
            var chest = Crop(src, k, 422, 206, 462, 246, "Icono_Cofre", false);
            Object.DestroyImmediate(src);

            string assetPath = Dir + "/MenuArt.asset";
            var menuArt = AssetDatabase.LoadAssetAtPath<MenuArt>(assetPath);
            if (menuArt == null) { menuArt = ScriptableObject.CreateInstance<MenuArt>(); AssetDatabase.CreateAsset(menuArt, assetPath); }
            menuArt.titleArt = art; menuArt.swordIcon = sword; menuArt.compassIcon = compass; menuArt.chestIcon = chest;
            EditorUtility.SetDirty(menuArt);
            AssetDatabase.SaveAssets();
            return menuArt;
        }

        static Sprite Crop(Texture2D src, float k, int x0, int y0, int x1, int y1, string name, bool eraseExitCorner)
        {
            int px0 = Mathf.RoundToInt(x0 * k), px1 = Mathf.RoundToInt(x1 * k);
            int py0 = src.height - Mathf.RoundToInt(y1 * k), py1 = src.height - Mathf.RoundToInt(y0 * k);
            int w = px1 - px0, h = py1 - py0;
            var pixels = src.GetPixels(px0, py0, w, h);
            var outPx = new Color32[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    var c = pixels[y * w + x];
                    float lum = c.r * 0.299f + c.g * 0.587f + c.b * 0.114f;
                    float a = Mathf.Clamp01((0.9f - lum) / 0.5f);
                    a = Mathf.Pow(a, 0.85f);
                    // La esquina inferior izquierda del boceto (X y texto SALIDA) se redibuja como boton real.
                    if (eraseExitCorner)
                    {
                        float mx = (px0 + x) / k, my = (src.height - (py0 + y)) / k;
                        if (mx < 135f && my > 366f) a = 0f;
                    }
                    outPx[y * w + x] = new Color32(Ink.r, Ink.g, Ink.b, (byte)Mathf.RoundToInt(a * 255f));
                }
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.SetPixels32(outPx);
            string path = Dir + "/" + name + ".png";
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 100;
            importer.filterMode = FilterMode.Bilinear;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.alphaIsTransparency = true;
            importer.maxTextureSize = 2048;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }
    }
}
