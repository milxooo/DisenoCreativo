using System.Collections.Generic;
using UnityEngine;

namespace RobaColores.Game
{
    public static class CharacterPortraitLibrary
    {
        public const int CharacterCount = 7;

        static readonly string[] ResourcePaths =
        {
            "CharacterArt/PirateCaptain",
            "CharacterArt/SeaArcher",
            "CharacterArt/OctopusPirate",
            "CharacterArt/PixelPirate",
            "CharacterArt/PirateBear",
            "CharacterArt/OctopusCaptain",
            "CharacterArt/PixelGrumete"
        };

        static readonly string[] GameArtworkPaths =
        {
            "CharacterArt/PixelGrumete",
            "CharacterArt/PirateBear",
            "CharacterArt/OctopusPirate",
            "CharacterArt/PirateCaptain",
            "CharacterArt/PixelPirate",
            "CharacterArt/SeaArcher"
        };

        static readonly Color32[] BackgroundColors =
        {
            new Color32(36, 101, 130, 255),
            new Color32(154, 86, 76, 255),
            new Color32(101, 82, 148, 255),
            new Color32(54, 123, 103, 255),
            new Color32(155, 117, 50, 255),
            new Color32(74, 103, 155, 255),
            new Color32(164, 85, 67, 255)
        };

        static readonly Texture2D[] portraits = new Texture2D[CharacterCount];
        static readonly Texture2D[] gameArtwork = new Texture2D[GameArtworkPaths.Length];
        static readonly bool[] gameArtworkLoaded = new bool[GameArtworkPaths.Length];
        static readonly List<string> missingResources = new List<string>();
        static Material artworkMaterial;
        static bool initialized;

        public static Texture2D GetCharacterArtwork(int characterIndex)
        {
            if (characterIndex < 0 || characterIndex >= GameArtworkPaths.Length)
                throw new System.ArgumentOutOfRangeException(nameof(characterIndex));
            if (!gameArtworkLoaded[characterIndex])
            {
                gameArtwork[characterIndex] = Resources.Load<Texture2D>(GameArtworkPaths[characterIndex]);
                gameArtworkLoaded[characterIndex] = true;
                if (gameArtwork[characterIndex] == null)
                    Debug.LogError("Falta el arte del personaje en Assets/Resources/" + GameArtworkPaths[characterIndex] + ".png.");
            }
            return gameArtwork[characterIndex];
        }

        public static Rect GetArtworkUvRect(Texture2D texture, float aspect)
        {
            if (texture == null || aspect <= 0f)
                return new Rect(0f, 0f, 1f, 1f);

            float width = Mathf.Min(1f, aspect * texture.height / texture.width);
            return new Rect((1f - width) * 0.5f, 0f, width, 1f);
        }

        public static Material GetArtworkMaterial()
        {
            if (artworkMaterial != null) return artworkMaterial;
            var shader = Shader.Find("RobaColores/CharacterCutout");
            if (shader == null)
            {
                Debug.LogError("No se encontró el shader RobaColores/CharacterCutout para mostrar el arte de los personajes.");
                return null;
            }
            artworkMaterial = new Material(shader)
            {
                name = "Material de personajes ilustrados",
                hideFlags = HideFlags.DontSave
            };
            return artworkMaterial;
        }

        public static Texture2D GetPortrait(int characterIndex)
        {
            var index = Mathf.Clamp(characterIndex, 0, CharacterCount - 1);
            Initialize();
            return portraits[index];
        }

        static void Initialize()
        {
            if (initialized)
                return;

            for (var i = 0; i < CharacterCount; i++)
            {
                portraits[i] = Resources.Load<Texture2D>(ResourcePaths[i]);
                if (portraits[i] != null)
                    continue;

                missingResources.Add(ResourcePaths[i]);
                portraits[i] = CreateFallbackPortrait(i);
            }

            initialized = true;
            if (missingResources.Count > 0)
            {
                Debug.LogWarning(
                    "Faltan retratos en Assets/Resources. Se muestran iconos provisionales para: " +
                    string.Join(", ", missingResources) +
                    ". Revisa que esos archivos existan y que Git LFS haya descargado los PNG.");
            }
        }

        static Texture2D CreateFallbackPortrait(int index)
        {
            const int width = 64;
            const int height = 80;
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                name = "Retrato provisional " + (index + 1),
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.DontSave
            };
            var background = BackgroundColors[index];
            var dark = new Color32(36, 36, 47, 255);
            var skin = new Color32(242, 196, 143, 255);
            var shirt = new Color32(245, 225, 185, 255);
            var red = new Color32(184, 65, 58, 255);

            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    var stripe = ((x / 8 + y / 8 + index) % 2 == 0) ? 0 : 10;
                    var color = new Color32(
                        (byte)Mathf.Min(255, background.r + stripe),
                        (byte)Mathf.Min(255, background.g + stripe),
                        (byte)Mathf.Min(255, background.b + stripe),
                        255);

                    var headX = (x - 32f) / 15f;
                    var headY = (y - 39f) / 19f;
                    var insideHead = headX * headX + headY * headY <= 1f;
                    var insideHeadOutline = headX * headX + headY * headY <= 1.2f;
                    var insideShoulders = y < 28 && y > 7 && Mathf.Abs(x - 32) < 11 + (27 - y) / 2;

                    if (insideShoulders)
                        color = shirt;
                    if (insideHeadOutline)
                        color = dark;
                    if (insideHead)
                        color = skin;
                    if (y >= 56 && y <= 64 && Mathf.Abs(x - 32) <= 20 - Mathf.Abs(y - 60))
                        color = dark;
                    if (y >= 61 && y <= 72 && Mathf.Abs(x - 32) <= 13 - Mathf.Abs(y - 66) / 2)
                        color = dark;
                    if (y >= 58 && y <= 60 && x >= 18 && x <= 46)
                        color = red;
                    if (y >= 39 && y <= 41 && (x == 27 || x == 28 || x == 36 || x == 37))
                        color = dark;

                    texture.SetPixel(x, y, color);
                }
            }

            texture.Apply(false, true);
            return texture;
        }
    }
}
