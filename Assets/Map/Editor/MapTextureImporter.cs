using UnityEditor;
using UnityEngine;

namespace RobaColores.Map.Editor
{
    // Fuerza el look pixel art en toda textura bajo Assets/Map/Textures.
    public class MapTextureImporter : AssetPostprocessor
    {
        void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith("Assets/Map/Textures/")) return;
            var t = (TextureImporter)assetImporter;
            t.textureType = TextureImporterType.Default;
            t.filterMode = FilterMode.Point;
            t.textureCompression = TextureImporterCompression.Uncompressed;
            t.mipmapEnabled = false;
            t.spritePixelsPerUnit = 16;
            t.npotScale = TextureImporterNPOTScale.None;
            t.wrapMode = TextureWrapMode.Clamp;
        }
    }
}
