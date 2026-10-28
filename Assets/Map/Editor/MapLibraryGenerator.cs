using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using static RobaColores.Map.Editor.MapAtlasGenerator;

namespace RobaColores.Map.Editor
{
    // Fase 2: genera atlas, material, mallas y prefabs de bloques, casillas y props.
    public static class MapLibraryGenerator
    {
        const string PalettePath = "Assets/Map/ScriptableObjects/MapPalette.asset";
        const string MaterialPath = "Assets/Map/Materials/MapAtlas.mat";
        const string MeshDir = "Assets/Map/Meshes";
        const string BlockDir = "Assets/Map/Prefabs/Blocks";
        const string TileDir = "Assets/Map/Prefabs/PathTiles";
        const string PropDir = "Assets/Map/Prefabs/Props";

        static Material material;

        [MenuItem("Roba Colores/Fase 2 - Generar biblioteca")]
        public static void Run()
        {
            var palette = AssetDatabase.LoadAssetAtPath<MapPalette>(PalettePath);
            Generate(palette);
            material = BuildMaterial();

            Block("Bloque_Pasto", GrassTop, GrassSide, 1f);
            Block("Bloque_Tierra", Dirt, Dirt, 1f);
            Block("Bloque_Arena", Sand, Sand, 1f);
            Block("Bloque_Madera", Planks, Planks, 1f);
            Block("Bloque_Roca", Rock, Rock, 1f);
            Block("Bloque_Agua", Water, Water, 0.9f);
            Block("Bloque_Pergamino", Parchment, Parchment, 1f);

            var names = (PathTileColor[])Enum.GetValues(typeof(PathTileColor));
            for (int i = 0; i < names.Length; i++) Tile(names[i], i);

            Props();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        static Material BuildMaterial()
        {
            var atlas = AssetDatabase.LoadAssetAtPath<Texture2D>(AtlasPath);
            var m = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (m == null)
            {
                m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(m, MaterialPath);
            }
            m.SetTexture("_BaseMap", atlas);
            m.SetColor("_BaseColor", Color.white);
            m.SetFloat("_Smoothness", 0f);
            m.SetFloat("_Metallic", 0f);
            m.SetFloat("_SpecularHighlights", 0f);
            m.SetFloat("_EnvironmentReflections", 0f);
            m.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
            m.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
            EditorUtility.SetDirty(m);
            return m;
        }

        static GameObject Make(string dir, string name, VoxelMeshBuilder b)
        {
            var mesh = b.ToMesh(name);
            string meshPath = MeshDir + "/" + name + ".asset";
            AssetDatabase.DeleteAsset(meshPath);
            AssetDatabase.CreateAsset(mesh, meshPath);

            var go = new GameObject(name);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = material;
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
            return go;
        }

        static void Save(GameObject go, string dir)
        {
            PrefabUtility.SaveAsPrefabAsset(go, dir + "/" + go.name + ".prefab");
            UnityEngine.Object.DestroyImmediate(go);
        }

        static void Block(string name, int top, int side, float height)
        {
            var b = new VoxelMeshBuilder().Add(0, 0, 0, 1, height, 1, top, side);
            Save(Make(BlockDir, name, b), BlockDir);
        }

        // Casilla ovalada de 9x7 celdas de 0.2: borde oscuro, relieve de color y glifo propio.
        static void Tile(PathTileColor color, int index)
        {
            const float u = 0.2f;
            int[] outer = { 3, 5, 7, 9, 7, 5, 3 };
            int[] inner = { 3, 5, 7, 5, 3 };
            int cell = TileRed + (int)color;
            var b = new VoxelMeshBuilder();
            for (int r = 0; r < outer.Length; r++)
                b.Add(0, 0, (r - 3) * u, outer[r] * u, 0.12f, u, Outline);
            for (int r = 0; r < inner.Length; r++)
                b.Add(0, 0.12f, (r - 2) * u, inner[r] * u, 0.08f, u, cell);

            string[] glyph = Glyph(color);
            int glyphCell = color == PathTileColor.Negro ? Iron : Outline;
            for (int r = 0; r < 3; r++)
                for (int c = 0; c < 3; c++)
                    if (glyph[r][c] == 'X')
                        b.Add((c - 1) * u, 0.2f, (r - 1) * u, u, 0.05f, u, glyphCell);

            var go = Make(TileDir, "Casilla_" + color, b);
            go.AddComponent<PathTile>().Set(color, 0);
            Save(go, TileDir);
        }

        static string[] Glyph(PathTileColor c)
        {
            switch (c)
            {
                case PathTileColor.Rojo: return new[] { "XXX", "X.X", "XXX" };
                case PathTileColor.Naranja: return new[] { ".X.", "XXX", ".X." };
                case PathTileColor.Amarillo: return new[] { "X.X", ".X.", "X.X" };
                case PathTileColor.Verde: return new[] { ".X.", "X.X", "XXX" };
                case PathTileColor.Azul: return new[] { "XXX", "...", "XXX" };
                case PathTileColor.Morado: return new[] { "X.X", "X.X", "X.X" };
                default: return new[] { "XXX", "XXX", "XXX" };
            }
        }

        static void Prop(string name, Action<VoxelMeshBuilder> build)
        {
            var b = new VoxelMeshBuilder();
            build(b);
            Save(Make(PropDir, name, b), PropDir);
        }

        static void Props()
        {
            Prop("Barril", b => b
                .Add(0, 0, 0, 0.8f, 1f, 0.8f, Planks)
                .Add(0, 0.15f, 0, 0.86f, 0.1f, 0.86f, PlankDark)
                .Add(0, 0.75f, 0, 0.86f, 0.1f, 0.86f, PlankDark));

            Prop("Caja", b =>
            {
                b.Add(0, 0, 0, 1f, 1f, 1f, Planks);
                foreach (var sx in new[] { -0.45f, 0.45f })
                    foreach (var sz in new[] { -0.45f, 0.45f })
                        b.Add(sx, 0, sz, 0.12f, 1.02f, 0.12f, PlankDark);
            });

            Prop("Cofre", b => b
                .Add(0, 0, 0, 1f, 0.5f, 0.7f, PlankDark)
                .Add(0, 0.5f, 0, 1f, 0.3f, 0.7f, Planks)
                .Add(-0.3f, 0, 0, 0.08f, 0.8f, 0.74f, Gold)
                .Add(0.3f, 0, 0, 0.08f, 0.8f, 0.74f, Gold)
                .Add(0, 0.4f, 0.36f, 0.2f, 0.25f, 0.04f, Gold));

            Prop("Palmera", b => b
                .Add(0, 0, 0, 0.35f, 1f, 0.35f, Trunk)
                .Add(0.1f, 1f, 0, 0.3f, 1f, 0.3f, Trunk)
                .Add(0.2f, 2f, 0.05f, 0.28f, 1f, 0.28f, Trunk)
                .Add(0.2f, 3f, 0.05f, 1.6f, 0.2f, 0.5f, Leaves)
                .Add(0.2f, 3f, 0.05f, 0.5f, 0.2f, 1.6f, Leaves)
                .Add(0.2f, 3.2f, 0.05f, 0.9f, 0.2f, 0.9f, Leaves)
                .Add(1.15f, 2.8f, 0.05f, 0.4f, 0.2f, 0.5f, Leaves)
                .Add(-0.75f, 2.8f, 0.05f, 0.4f, 0.2f, 0.5f, Leaves)
                .Add(0.2f, 2.8f, 1f, 0.5f, 0.2f, 0.4f, Leaves)
                .Add(0.2f, 2.8f, -0.9f, 0.5f, 0.2f, 0.4f, Leaves));

            Prop("BanderaPirata", b => b
                .Add(0, 0, 0, 0.15f, 3.2f, 0.15f, Trunk)
                .Add(0.65f, 2.2f, 0, 1.2f, 0.8f, 0.05f, ClothBlack)
                .Add(0.65f, 2.5f, 0.04f, 0.3f, 0.25f, 0.04f, Bone)
                .Add(0.65f, 2.35f, 0.04f, 0.55f, 0.08f, 0.04f, Bone));

            Prop("Ancla", b => b
                .Add(0, 0, 0, 0.18f, 1.4f, 0.18f, Iron)
                .Add(0, 1f, 0, 0.8f, 0.14f, 0.14f, Iron)
                .Add(0, 1.4f, 0, 0.3f, 0.12f, 0.12f, Iron)
                .Add(0, 0, 0, 0.9f, 0.15f, 0.15f, Iron)
                .Add(-0.45f, 0.15f, 0, 0.15f, 0.25f, 0.15f, Iron)
                .Add(0.45f, 0.15f, 0, 0.15f, 0.25f, 0.15f, Iron));

            Prop("Canon", b => b
                .Add(-0.3f, 0, 0, 0.12f, 0.5f, 0.5f, Planks)
                .Add(0.3f, 0, 0, 0.12f, 0.5f, 0.5f, Planks)
                .Add(0, 0.15f, 0, 0.45f, 0.4f, 1.2f, Iron)
                .Add(0, 0.1f, 0.6f, 0.55f, 0.5f, 0.2f, Iron));

            Prop("Calavera", b => b
                .Add(0, 0, 0, 1f, 1f, 1f, Bone)
                .Add(-0.22f, 0.5f, 0.5f, 0.24f, 0.24f, 0.04f, ClothBlack)
                .Add(0.22f, 0.5f, 0.5f, 0.24f, 0.24f, 0.04f, ClothBlack)
                .Add(0, 0.3f, 0.5f, 0.1f, 0.15f, 0.04f, ClothBlack)
                .Add(0, 0.05f, 0.5f, 0.5f, 0.1f, 0.04f, ClothBlack));

            Prop("Cartel", b => b
                .Add(-0.7f, 0, 0, 0.15f, 1.2f, 0.15f, PlankDark)
                .Add(0.7f, 0, 0, 0.15f, 1.2f, 0.15f, PlankDark)
                .Add(0, 0.7f, 0, 1.8f, 0.7f, 0.12f, Planks));

            Prop("PosteMuelle", b => b
                .Add(0, 0, 0, 0.3f, 1.6f, 0.3f, Trunk)
                .Add(0, 1.2f, 0, 0.4f, 0.1f, 0.4f, Rope));

            Prop("RosaDeLosVientos", b =>
            {
                const float u = 0.25f;
                for (int x = -4; x <= 4; x++)
                    for (int z = -4; z <= 4; z++)
                    {
                        int cell = -1;
                        if (x == 0 && z == 0) cell = TileRed;
                        else if (x == 0 || z == 0) cell = Outline;
                        else if (Mathf.Abs(x) == Mathf.Abs(z) && Mathf.Abs(x) <= 2) cell = Iron;
                        if (cell >= 0) b.Add(x * u, 0, z * u, u, 0.06f, u, cell);
                    }
            });

            Prop("XTesoro", b =>
            {
                for (int i = -2; i <= 2; i++)
                {
                    b.Add(i * 0.3f, 0, i * 0.3f, 0.3f, 0.06f, 0.3f, TileRed);
                    b.Add(i * 0.3f, 0, -i * 0.3f, 0.3f, 0.06f, 0.3f, TileRed);
                }
            });

            Prop("Kraken", b =>
            {
                b.Add(0, 0, 0, 2.4f, 1.2f, 2.4f, KrakenDark)
                 .Add(0, 1.2f, 0, 1.8f, 0.6f, 1.8f, KrakenDark)
                 .Add(0, 1.8f, 0, 1f, 0.3f, 1f, KrakenDark)
                 .Add(-0.45f, 0.8f, 1.21f, 0.3f, 0.3f, 0.05f, KrakenLight)
                 .Add(0.45f, 0.8f, 1.21f, 0.3f, 0.3f, 0.05f, KrakenLight)
                 .Add(-0.45f, 0.85f, 1.24f, 0.12f, 0.2f, 0.03f, Outline)
                 .Add(0.45f, 0.85f, 1.24f, 0.12f, 0.2f, 0.03f, Outline);
                foreach (var d in new[] { new Vector2(1, 0), new Vector2(-1, 0), new Vector2(0, 1), new Vector2(0, -1) })
                {
                    b.Add(d.x * 1.6f, 0, d.y * 1.6f, 0.5f, 1f, 0.5f, KrakenDark)
                     .Add(d.x * 2.1f, 0, d.y * 2.1f, 0.45f, 0.6f, 0.45f, KrakenDark)
                     .Add(d.x * 2.5f, 0, d.y * 2.5f, 0.4f, 0.35f, 0.4f, KrakenDark)
                     .Add(d.x * 1.6f, 1f, d.y * 1.6f, 0.4f, 0.8f, 0.4f, KrakenDark)
                     .Add(d.x * 1.6f, 1.8f, d.y * 1.6f, 0.2f, 0.05f, 0.2f, KrakenLight);
                }
            });

            Prop("BarcoPirata", b => b
                .Add(0, 0, 0, 1.6f, 0.4f, 3.4f, PlankDark)
                .Add(0, 0.4f, 0, 2f, 0.5f, 3.8f, Planks, PlankDark)
                .Add(0, 0.4f, 2f, 1f, 0.5f, 0.4f, Planks, PlankDark)
                .Add(0, 0.9f, -1.4f, 1.8f, 0.5f, 0.9f, Planks, PlankDark)
                .Add(0, 0.9f, 0.3f, 0.18f, 3.2f, 0.18f, Trunk)
                .Add(0, 1.8f, 0.3f, 1.8f, 1.6f, 0.08f, Sail)
                .Add(0, 4.1f, 0.3f, 0.5f, 0.3f, 0.05f, ClothBlack));

            Prop("Remolino", b =>
            {
                const float u = 0.4f;
                for (int k = 1; k <= 3; k++)
                    for (int x = -k; x <= k; x++)
                        for (int z = -k; z <= k; z++)
                        {
                            if (Mathf.Max(Mathf.Abs(x), Mathf.Abs(z)) != k) continue;
                            if (k == 2 && x == k && z < 0) continue;
                            if (k == 3 && x == -k && z > 0) continue;
                            b.Add(x * u, 0, z * u, u, 0.04f, u, WaterLight);
                        }
                b.Add(0, 0, 0, u, 0.04f, u, Outline);
            });

            Prop("CuevaArco", b => b
                .Add(-1.2f, 0, 0, 1f, 2.5f, 1.2f, Rock)
                .Add(1.2f, 0, 0, 1f, 2.5f, 1.2f, Rock)
                .Add(0, 2.5f, 0, 3.4f, 1f, 1.4f, Rock)
                .Add(0, 3.5f, 0, 2.4f, 0.7f, 1f, Rock)
                .Add(0, 0, 0.35f, 1.4f, 2.5f, 0.1f, Outline));

            Prop("Montana", b => b
                .Add(0, 0, 0, 6f, 1f, 6f, Rock)
                .Add(0, 1f, 0, 4.5f, 1f, 4.5f, Rock)
                .Add(0, 2f, 0, 3f, 1f, 3f, Rock)
                .Add(0, 3f, 0, 1.6f, 1f, 1.6f, Rock)
                .Add(0, 4f, 0, 0.8f, 0.6f, 0.8f, Sail));

            Prop("Roca", b => b
                .Add(0, 0, 0, 1.2f, 0.8f, 1f, Rock)
                .Add(0.2f, 0.8f, 0, 0.7f, 0.4f, 0.6f, Rock));

            // Marcador dorado (flecha hacia abajo) para senalar las opciones de camino en las bifurcaciones.
            Prop("Marcador", b => b
                .Add(0, 1.2f, 0, 0.8f, 0.3f, 0.8f, Gold)
                .Add(0, 0.9f, 0, 0.6f, 0.3f, 0.6f, Gold)
                .Add(0, 0.6f, 0, 0.4f, 0.3f, 0.4f, Gold)
                .Add(0, 0.3f, 0, 0.2f, 0.3f, 0.2f, Gold));

            // Guia direccional: flecha plana que apunta hacia +X local (hacia META cuando se orienta con el camino).
            Prop("Flecha", b => b
                .Add(-0.15f, 0, 0, 0.6f, 0.08f, 0.2f, TileRed)
                .Add(0.30f, 0, 0, 0.2f, 0.08f, 0.7f, TileRed)
                .Add(0.45f, 0, 0, 0.15f, 0.08f, 0.46f, TileRed)
                .Add(0.58f, 0, 0, 0.12f, 0.08f, 0.22f, TileRed));

            // Farol de interseccion: marca los nodos donde se puede elegir ruta.
            Prop("Farol", b => b
                .Add(0, 0, 0, 0.25f, 1.6f, 0.25f, Trunk)
                .Add(0, 1.6f, 0, 0.5f, 0.4f, 0.5f, Gold)
                .Add(0, 2.0f, 0, 0.64f, 0.1f, 0.64f, Outline));

            // Meta: plataforma con cofre gigante, montones de oro y banderas.
            Prop("TesoroMeta", b => b
                .Add(0, 0, 0, 7f, 0.5f, 3.2f, Planks)
                .Add(0, 0, -1.9f, 4.5f, 0.25f, 0.6f, Planks)
                .Add(0, 0.5f, 0.2f, 2.6f, 1f, 1.6f, PlankDark)
                .Add(0, 1.5f, 0.2f, 2.6f, 0.6f, 1.6f, Planks)
                .Add(-0.8f, 0.5f, 0.2f, 0.2f, 1.6f, 1.7f, Gold)
                .Add(0.8f, 0.5f, 0.2f, 0.2f, 1.6f, 1.7f, Gold)
                .Add(0, 0.9f, 1.03f, 0.5f, 0.6f, 0.08f, Gold)
                .Add(-2.6f, 0.5f, -0.6f, 0.9f, 0.5f, 0.9f, Gold)
                .Add(2.6f, 0.5f, -0.6f, 0.9f, 0.7f, 0.9f, Gold)
                .Add(-2.9f, 0.5f, 0.8f, 0.6f, 0.4f, 0.6f, Gold)
                .Add(2.9f, 0.5f, 0.8f, 0.6f, 0.3f, 0.6f, Gold)
                .Add(-3.2f, 0.5f, 0.2f, 0.15f, 3f, 0.15f, Trunk)
                .Add(-2.7f, 2.9f, 0.2f, 1f, 0.6f, 0.05f, ClothBlack)
                .Add(3.2f, 0.5f, 0.2f, 0.15f, 3f, 0.15f, Trunk)
                .Add(2.7f, 2.9f, 0.2f, 1f, 0.6f, 0.05f, ClothBlack));
        }
    }
}
