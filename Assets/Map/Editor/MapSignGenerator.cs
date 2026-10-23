using System.IO;
using UnityEditor;
using UnityEngine;

namespace RobaColores.Map.Editor
{
    // Rasteriza "INICIO" y "META" con Pirata One a una textura pixelada (80x64) y crea los prefabs de cartel.
    public static class MapSignGenerator
    {
        const string FontPath = "Assets/Map/Fonts/PirataOne-Regular.ttf";
        const string TexPath = "Assets/Map/Textures/MapSigns.png";
        const string MatPath = "Assets/Map/Materials/MapSigns.mat";
        const int W = 80, H = 32, FontSize = 26;

        static readonly Color Wood = new Color32(0x5E, 0x3A, 0x1C, 255);
        static readonly Color WoodLight = new Color32(0x7A, 0x4E, 0x2A, 255);
        static readonly Color Ink = new Color32(0x1A, 0x14, 0x23, 255);
        static readonly Color Gold = new Color32(0xF2, 0xC2, 0x30, 255);

        public static void Run()
        {
            var tex = new Texture2D(W, H * 2, TextureFormat.RGBA32, false);
            DrawLabel(tex, "INICIO", H);
            DrawLabel(tex, "META", 0);
            File.WriteAllBytes(TexPath, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(TexPath, ImportAssetOptions.ForceUpdate);

            var m = AssetDatabase.LoadAssetAtPath<Material>(MatPath);
            if (m == null)
            {
                m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(m, MatPath);
            }
            m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(TexPath));
            m.SetFloat("_Smoothness", 0f);
            m.SetFloat("_SpecularHighlights", 0f);
            m.SetFloat("_EnvironmentReflections", 0f);
            m.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
            m.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
            EditorUtility.SetDirty(m);

            BuildSign("Cartel_Inicio", 0.5f, m);
            BuildSign("Cartel_Meta", 0f, m);
            BuildGate("ArcoInicio", 0.5f, MapAtlasGenerator.TileRed, m, 0f);
            BuildGate("ArcoMeta", 0f, MapAtlasGenerator.Gold, m, 2.4f);
            AssetDatabase.SaveAssets();
        }

        // Arco de madera con el cartel grande y banderas de color: marca INICIO (rojo) y META (dorado).
        static void BuildGate(string name, float v0, int flagCell, Material signMat, float lift)
        {
            int planks = MapAtlasGenerator.Planks, trunk = MapAtlasGenerator.Trunk;
            var b = new VoxelMeshBuilder()
                .Add(-3.2f, 0, 0, 0.7f, 4f + lift, 0.7f, planks)
                .Add(3.2f, 0, 0, 0.7f, 4f + lift, 0.7f, planks)
                .Add(0, 4f + lift, 0, 7.4f, 0.5f, 0.7f, planks)
                .Add(-3.2f, 4.5f + lift, 0, 0.2f, 1.4f, 0.2f, trunk).Add(-2.6f, 5.3f + lift, 0, 1f, 0.6f, 0.05f, flagCell)
                .Add(3.2f, 4.5f + lift, 0, 0.2f, 1.4f, 0.2f, trunk).Add(2.6f, 5.3f + lift, 0, 1f, 0.6f, 0.05f, flagCell);
            var mesh = b.ToMesh(name);
            string meshPath = "Assets/Map/Meshes/" + name + ".asset";
            AssetDatabase.DeleteAsset(meshPath);
            AssetDatabase.CreateAsset(mesh, meshPath);

            var root = new GameObject(name);
            root.AddComponent<MeshFilter>().sharedMesh = mesh;
            root.AddComponent<MeshRenderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Map/Materials/MapAtlas.mat");
            GameObjectUtility.SetStaticEditorFlags(root, StaticEditorFlags.BatchingStatic);

            var q = new Mesh { name = name + "_Texto" };
            float hx = 2.8f, y0 = 1.6f + lift, y1 = 3.84f + lift, z = -0.37f;
            q.vertices = new[] { new Vector3(-hx, y0, z), new Vector3(-hx, y1, z), new Vector3(hx, y1, z), new Vector3(hx, y0, z) };
            q.normals = new[] { Vector3.back, Vector3.back, Vector3.back, Vector3.back };
            q.uv = new[] { new Vector2(0, v0), new Vector2(0, v0 + 0.5f), new Vector2(1, v0 + 0.5f), new Vector2(1, v0) };
            q.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            if (Vector3.Dot(Vector3.Cross(q.vertices[1] - q.vertices[0], q.vertices[2] - q.vertices[0]), Vector3.back) < 0)
                q.triangles = new[] { 0, 2, 1, 0, 3, 2 };
            q.RecalculateBounds();
            string qPath = "Assets/Map/Meshes/" + name + "_Texto.asset";
            AssetDatabase.DeleteAsset(qPath);
            AssetDatabase.CreateAsset(q, qPath);

            var sign = new GameObject("Texto");
            sign.transform.SetParent(root.transform, false);
            sign.AddComponent<MeshFilter>().sharedMesh = q;
            sign.AddComponent<MeshRenderer>().sharedMaterial = signMat;
            GameObjectUtility.SetStaticEditorFlags(sign, StaticEditorFlags.BatchingStatic);

            PrefabUtility.SaveAsPrefabAsset(root, "Assets/Map/Prefabs/Props/" + name + ".prefab");
            Object.DestroyImmediate(root);
        }

        static void BuildSign(string name, float v0, Material mat)
        {
            var board = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Map/Prefabs/Props/Cartel.prefab");
            var root = (GameObject)PrefabUtility.InstantiatePrefab(board);
            root.name = name;

            var mesh = new Mesh { name = name + "_Texto" };
            float hx = 0.85f, y0 = 0.74f, y1 = 1.36f, z = -0.065f;
            mesh.vertices = new[] { new Vector3(-hx, y0, z), new Vector3(-hx, y1, z), new Vector3(hx, y1, z), new Vector3(hx, y0, z) };
            mesh.normals = new[] { Vector3.back, Vector3.back, Vector3.back, Vector3.back };
            mesh.uv = new[] { new Vector2(0, v0), new Vector2(0, v0 + 0.5f), new Vector2(1, v0 + 0.5f), new Vector2(1, v0) };
            // Visto desde -Z (mirando hacia +Z) la cara frontal es horaria.
            mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            if (Vector3.Dot(Vector3.Cross(mesh.vertices[1] - mesh.vertices[0], mesh.vertices[2] - mesh.vertices[0]), Vector3.back) < 0)
                mesh.triangles = new[] { 0, 2, 1, 0, 3, 2 };
            mesh.RecalculateBounds();
            string meshPath = "Assets/Map/Meshes/" + name + "_Texto.asset";
            AssetDatabase.DeleteAsset(meshPath);
            AssetDatabase.CreateAsset(mesh, meshPath);

            var quad = new GameObject("Texto");
            quad.transform.SetParent(root.transform, false);
            quad.AddComponent<MeshFilter>().sharedMesh = mesh;
            quad.AddComponent<MeshRenderer>().sharedMaterial = mat;
            GameObjectUtility.SetStaticEditorFlags(quad, StaticEditorFlags.BatchingStatic);

            PrefabUtility.SaveAsPrefabAsset(root, "Assets/Map/Prefabs/Props/" + name + ".prefab");
            Object.DestroyImmediate(root);
        }

        static void DrawLabel(Texture2D tex, string text, int oy)
        {
            // Fondo de madera con borde.
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    bool edge = x == 0 || y == 0 || x == W - 1 || y == H - 1;
                    bool inner = x == 1 || y == 1 || x == W - 2 || y == H - 2;
                    tex.SetPixel(x, oy + y, edge ? Ink : inner ? WoodLight : Wood);
                }

            var font = AssetDatabase.LoadAssetAtPath<Font>(FontPath);
            font.RequestCharactersInTexture(text, FontSize, FontStyle.Normal);
            var atlas = ReadAtlas(font);
            bool useAlpha = false;
            foreach (var c in atlas.GetPixels32()) if (c.a < 255) { useAlpha = true; break; }

            var infos = new CharacterInfo[text.Length];
            float total = 0; int minY = int.MaxValue, maxY = int.MinValue;
            for (int i = 0; i < text.Length; i++)
            {
                font.GetCharacterInfo(text[i], out infos[i], FontSize, FontStyle.Normal);
                total += infos[i].advance;
                minY = Mathf.Min(minY, infos[i].minY);
                maxY = Mathf.Max(maxY, infos[i].maxY);
            }
            int baseline = (H - (maxY - minY)) / 2 - minY;
            float pen = (W - total) / 2f;

            var mask = new bool[W, H];
            for (int i = 0; i < text.Length; i++)
            {
                var ci = infos[i];
                int gw = ci.maxX - ci.minX, gh = ci.maxY - ci.minY;
                int left = Mathf.RoundToInt(pen) + ci.minX, bottom = baseline + ci.minY;
                for (int gy = 0; gy < gh; gy++)
                    for (int gx = 0; gx < gw; gx++)
                    {
                        float s = (gx + 0.5f) / gw, t = (gy + 0.5f) / gh;
                        Vector2 uv = ci.uvBottomLeft + s * (ci.uvBottomRight - ci.uvBottomLeft) + t * (ci.uvTopLeft - ci.uvBottomLeft);
                        var c = atlas.GetPixel(Mathf.Clamp((int)(uv.x * atlas.width), 0, atlas.width - 1), Mathf.Clamp((int)(uv.y * atlas.height), 0, atlas.height - 1));
                        float cov = useAlpha ? c.a : c.r;
                        int dx = left + gx, dy = bottom + gy;
                        if (cov > 0.5f && dx >= 0 && dx < W && dy >= 0 && dy < H) mask[dx, dy] = true;
                    }
                pen += ci.advance;
            }
            Object.DestroyImmediate(atlas);

            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    if (mask[x, y]) { tex.SetPixel(x, oy + y, Gold); continue; }
                    bool near = (x > 0 && mask[x - 1, y]) || (x < W - 1 && mask[x + 1, y]) || (y > 0 && mask[x, y - 1]) || (y < H - 1 && mask[x, y + 1]);
                    if (near) tex.SetPixel(x, oy + y, Ink);
                }
        }

        static Texture2D ReadAtlas(Font font)
        {
            var src = font.material.mainTexture;
            var rt = RenderTexture.GetTemporary(src.width, src.height, 0, RenderTextureFormat.ARGB32);
            Graphics.Blit(src, rt);
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var t = new Texture2D(src.width, src.height, TextureFormat.RGBA32, false);
            t.ReadPixels(new Rect(0, 0, src.width, src.height), 0, 0);
            t.Apply();
            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(rt);
            return t;
        }
    }
}
