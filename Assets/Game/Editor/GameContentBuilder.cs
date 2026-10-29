using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using RobaColores.Game.Core;
using RobaColores.Game.Presentation;
using RobaColores.Map.Editor;
using static RobaColores.Map.Editor.MapAtlasGenerator;

namespace RobaColores.Game.EditorTools
{
    // Genera los datos jugables: paleta de dibujo, ajustes, 14 figuras de PRUEBA, personajes provisionales y GameContent.
    // Es idempotente: volver a ejecutarlo actualiza los assets existentes.
    public static class GameContentBuilder
    {
        const string Root = "Assets/Game/Data";

        struct Fig
        {
            public string id, name; public Category cat; public Difficulty diff; public string rows;
            public Fig(string id, string name, Category cat, Difficulty diff, string rows) { this.id = id; this.name = name; this.cat = cat; this.diff = diff; this.rows = rows; }
        }

        // Figuras de prueba: una facil (8x8) y una intermedia por categoria. NO es un catalogo completo.
        static readonly Fig[] Figures =
        {
            new Fig("fruta_manzana", "Manzana", Category.FrutasYVerduras, Difficulty.Facil,
                "...KG...\n..RRRR..\n.RRWRRR.\n.RRRRRR.\n.RRRRRR.\n.RRRRRR.\n..RRRR..\n...RR..."),
            new Fig("fruta_zanahoria", "Zanahoria", Category.FrutasYVerduras, Difficulty.Intermedio,
                "...G.GG...\n....GG....\n...OOOO...\n...OOOO...\n...OWOO...\n....OOO...\n....OOO...\n.....OO...\n.....OO...\n......O..."),
            new Fig("cuerpo_corazon", "Corazon", Category.PartesDelCuerpo, Difficulty.Facil,
                ".RR..RR.\nRRRRRRRR\nRRWRRRRR\nRRRRRRRR\n.RRRRRR.\n..RRRR..\n...RR...\n........"),
            new Fig("cuerpo_ojo", "Ojo", Category.PartesDelCuerpo, Difficulty.Intermedio,
                "..........\n..KKKKKK..\n.KWWWWWWK.\nKWWBBBBWWK\nKWBBKKBBWK\nKWBBKKBBWK\nKWWBBBBWWK\n.KWWWWWWK.\n..KKKKKK..\n.........."),
            new Fig("ropa_sombrero", "Sombrero", Category.AccesoriosDeRopa, Difficulty.Facil,
                "........\n...KK...\n..KKKK..\n..KYYK..\n.KKKKKK.\nKKKKKKKK\n........\n........"),
            new Fig("ropa_gafas", "Gafas", Category.AccesoriosDeRopa, Difficulty.Intermedio,
                "K........K\nKKKK..KKKK\nKBBKKKKBBK\nKBWK..KWBK\nKKKK..KKKK"),
            new Fig("animal_pez", "Pez", Category.Animales, Difficulty.Facil,
                "........\n..GGG..G\n.GGGGGGG\nGGWKGGG.\n.GGGGGGG\n..GGG..G\n........\n........"),
            new Fig("animal_gato", "Gato", Category.Animales, Difficulty.Intermedio,
                "O........O\nOO......OO\nOOOOOOOOOO\nOOWKOOKWOO\nOOOOPPOOOO\nOOOOOOOOOO\n.OOOKKOOO.\n..OOOOOO..\n...OOOO..."),
            new Fig("profesion_cocinero", "Gorro de cocinero", Category.Profesiones, Difficulty.Facil,
                "..WWWW..\n.WWWWWW.\n.WWWWWW.\n..WWWW..\n..WWWW..\n..BBBB..\n..BBBB..\n........"),
            new Fig("profesion_bombero", "Casco de bombero", Category.Profesiones, Difficulty.Intermedio,
                "....RR....\n...RRRR...\n..RRRRRR..\n.RRRYYRRR.\n.RRRRRRRR.\nRRRRRRRRRR\nKKKKKKKKKK"),
            new Fig("deporte_futbol", "Balon de futbol", Category.Deportes, Difficulty.Facil,
                "..WWWW..\n.WWKKWW.\nWWKKKKWW\nWKKWWKKW\nWKKWWKKW\nWWKKKKWW\n.WWKKWW.\n..WWWW.."),
            new Fig("deporte_baloncesto", "Balon de baloncesto", Category.Deportes, Difficulty.Intermedio,
                "..OOOOOO..\n.OOOKKOOO.\nOOOOKKOOOO\nOOOOKKOOOO\nKKKKKKKKKK\nKKKKKKKKKK\nOOOOKKOOOO\nOOOOKKOOOO\n.OOOKKOOO.\n..OOOOOO.."),
            new Fig("astro_estrella", "Estrella", Category.Astronomia, Difficulty.Facil,
                "...YY...\n...YY...\nYYYYYYYY\n.YYYYYY.\n..YYYY..\n..YYYY..\n.YY..YY.\nYY....YY"),
            new Fig("astro_planeta", "Planeta con anillo", Category.Astronomia, Difficulty.Intermedio,
                "...BBBB...\n..BBBBBB..\n.BBBBBBBB.\nWWWWWWWWWW\n.BBBBBBBB.\n..BBBBBB..\n...BBBB..."),
        };

        struct Pawn { public string name; public Color color; public int cell; }

        [MenuItem("Roba Colores/Fase 2 - Generar contenido del juego")]
        public static void Run()
        {
            EnsureFolder("Assets/Game", "Data"); EnsureFolder(Root, "Figuras"); EnsureFolder(Root, "Personajes");
            EnsureFolder("Assets/Game", "Prefabs"); EnsureFolder("Assets/Game", "Meshes");

            var palette = Load<PaletteAsset>(Root + "/Palette.asset");
            palette.entries = new List<PaletteAsset.Entry>
            {
                E("Rojo", 'R', "D8363A"), E("Naranja", 'O', "F08A24"), E("Amarillo", 'Y', "F2D335"), E("Verde", 'G', "3FAE4A"),
                E("Azul", 'B', "3C78D8"), E("Morado", 'P', "8E4FBF"), E("Negro", 'K', "2B2B33"), E("Blanco", 'W', "F4EFE0"),
            };
            EditorUtility.SetDirty(palette);

            var settings = Load<GameSettingsAsset>(Root + "/GameSettings.asset");
            settings.minPlayers = 6; settings.maxPlayers = 6; settings.allowRepeatedCharacters = false;
            settings.moveDieFaces = new List<MoveFaceData>
            {
                new MoveFaceData { kind = MoveFaceKind.Advance, steps = 1 }, new MoveFaceData { kind = MoveFaceKind.Advance, steps = 2 },
                new MoveFaceData { kind = MoveFaceKind.Advance, steps = 3 }, new MoveFaceData { kind = MoveFaceKind.Advance, steps = 4 },
                new MoveFaceData { kind = MoveFaceKind.LoseTurn }, new MoveFaceData { kind = MoveFaceKind.TimePenalty },
            };
            EditorUtility.SetDirty(settings);

            var content = Load<GameContent>(Root + "/GameContent.asset");
            content.settings = settings; content.palette = palette;
            content.uiFont = AssetDatabase.LoadAssetAtPath<Font>("Assets/Map/Fonts/PirataOne-Regular.ttf");
            content.markerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Map/Prefabs/Props/Marcador.prefab");

            content.figures = new List<FigureAsset>();
            foreach (var f in Figures)
            {
                var asset = Load<FigureAsset>(Root + "/Figuras/" + f.id + ".asset");
                asset.figureId = f.id; asset.displayName = f.name; asset.category = f.cat; asset.difficulty = f.diff; asset.rows = f.rows;
                EditorUtility.SetDirty(asset);
                asset.ToDefinition(palette).Validate(palette.Count);   // falla aqui si una fila esta mal escrita
                content.figures.Add(asset);
            }

            var pawns = new[]
            {
                new Pawn { name = "Pirata Rojo", color = Hex("D8363A"), cell = TileRed },
                new Pawn { name = "Pirata Azul", color = Hex("3C78D8"), cell = TileBlue },
                new Pawn { name = "Pirata Verde", color = Hex("3FAE4A"), cell = TileGreen },
                new Pawn { name = "Pirata Morado", color = Hex("8E4FBF"), cell = TilePurple },
                new Pawn { name = "Pirata Naranja", color = Hex("F08A24"), cell = TileOrange },
                new Pawn { name = "Pirata Amarillo", color = Hex("F2D335"), cell = TileYellow },
            };
            content.characters = new List<CharacterAsset>();
            for (int i = 0; i < pawns.Length; i++)
            {
                var p = pawns[i];
                string key = p.name.Replace(" ", "_");
                var c = Load<CharacterAsset>(Root + "/Personajes/" + key + ".asset");
                c.id = key.ToLowerInvariant();
                c.displayName = p.name;
                c.description = "Personaje provisional. Se sustituye por el definitivo en este mismo asset.";
                c.mainColor = p.color;
                c.pawnPrefab = BuildPawn(key, p.cell);
                c.portrait = PortraitPainter.Build(Root + "/Personajes/Retratos", key, p.color, i);
                EditorUtility.SetDirty(c);
                content.characters.Add(c);
            }
            content.menuArt = MenuArtBuilder.Run();

            EditorUtility.SetDirty(content);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        static GameObject BuildPawn(string name, int bodyCell)
        {
            var b = new VoxelMeshBuilder()
                .Add(-0.15f, 0, 0, 0.2f, 0.45f, 0.2f, Iron)
                .Add(0.15f, 0, 0, 0.2f, 0.45f, 0.2f, Iron)
                .Add(0, 0.45f, 0, 0.6f, 0.55f, 0.35f, bodyCell)
                .Add(0, 1.0f, 0, 0.45f, 0.4f, 0.4f, Bone)
                .Add(-0.1f, 1.15f, -0.2f, 0.08f, 0.08f, 0.04f, Outline)
                .Add(0.1f, 1.15f, -0.2f, 0.08f, 0.08f, 0.04f, Outline)
                .Add(0, 1.4f, 0, 0.62f, 0.14f, 0.52f, ClothBlack)
                .Add(0, 1.54f, 0, 0.4f, 0.2f, 0.34f, bodyCell);
            var mesh = b.ToMesh("Pawn_" + name);
            string meshPath = "Assets/Game/Meshes/Pawn_" + name + ".asset";
            AssetDatabase.DeleteAsset(meshPath);
            AssetDatabase.CreateAsset(mesh, meshPath);

            var go = new GameObject("Pawn_" + name);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Map/Materials/MapAtlas.mat");
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, "Assets/Game/Prefabs/Pawn_" + name + ".prefab");
            Object.DestroyImmediate(go);
            return prefab;
        }

        static PaletteAsset.Entry E(string name, char letter, string hex)
        {
            return new PaletteAsset.Entry { name = name, letter = letter, color = Hex(hex) };
        }

        static Color Hex(string hex) { ColorUtility.TryParseHtmlString("#" + hex, out var c); return c; }

        static T Load<T>(string path) where T : ScriptableObject
        {
            var a = AssetDatabase.LoadAssetAtPath<T>(path);
            if (a == null) { a = ScriptableObject.CreateInstance<T>(); AssetDatabase.CreateAsset(a, path); }
            return a;
        }

        static void EnsureFolder(string parent, string name)
        {
            if (!AssetDatabase.IsValidFolder(parent + "/" + name)) AssetDatabase.CreateFolder(parent, name);
        }
    }
}
