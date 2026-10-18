using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using static RobaColores.Map.Editor.MapAtlasGenerator;

namespace RobaColores.Map.Editor
{
    // Fase 3: construye la escena del Mapa A (horizontal) a partir de la biblioteca.
    // El layout (nodos y tramos del camino, islas y decoracion) esta definido aqui abajo.
    public static class MapAGenerator
    {
        const int Width = 64, Depth = 48;
        const float GroundTop = 1f, WaterTop = 0.4f, TileSpacing = 2.1f;
        const string ScenePath = "Assets/Map/Scenes/MapaA.unity";
        const string MeshDir = "Assets/Map/Meshes/MapaA";

        enum Kind { Water, Sand, Grass, Rock, Wood, Parchment }

        // ---- Layout del camino (coordenadas de mundo; x a la derecha, z hacia META) ----
        static readonly Dictionary<string, Vector2> Nodes = new Dictionary<string, Vector2>
        {
            { "S", V(32, 3) },   // INICIO
            { "M", V(32, 45) },  // META
            { "N1", V(30, 17) }, { "N2", V(28, 29) }, { "N3", V(37, 24) }, { "N4", V(39, 30) },
            { "N5", V(14, 9) }, { "N6", V(8, 32) }, { "N7", V(57, 27) },
            { "P", V(38, 17) }, { "Q", V(31, 24) }, { "X", V(34, 22) },   // X = cruce
        };

        struct Edge
        {
            public string a, b;
            public Vector2[] via;
            public List<Vector2> pts;
            public float length;
            public int count;
        }

        static Edge E(string a, string b, params Vector2[] via) { return new Edge { a = a, b = b, via = via }; }
        static Vector2 V(float x, float z) { return new Vector2(x, z); }

        static Edge[] BuildEdges()
        {
            return new[]
            {
                E("S", "N5", V(24, 5)),
                E("N5", "N6", V(8, 16), V(6, 24)),
                E("N6", "M", V(12, 38), V(18, 42), V(26, 44)),
                E("S", "N1", V(31, 10)),
                E("N1", "Q"),
                E("Q", "N2"),
                E("N2", "M", V(30, 35), V(30, 41)),
                E("S", "P", V(36, 10)),
                E("P", "N3"),
                E("N3", "N4"),
                E("N4", "M", V(38, 36), V(36, 41)),
                E("S", "N7", V(40, 5), V(48, 7), V(54, 12), V(57, 19)),
                E("N7", "M", V(52, 33), V(46, 37), V(40, 42)),
                E("N1", "N5", V(24, 14), V(19, 11)),
                E("N4", "N7", V(45, 29), V(51, 28)),
                E("N6", "N2", V(14, 31), V(21, 32)),
                E("N1", "X"), E("X", "N3"), E("Q", "X"), E("X", "P"),
            };
        }

        [MenuItem("Roba Colores/Fase 3 - Generar Mapa A")]
        public static void Run()
        {
            MapSignGenerator.Run();
            if (!AssetDatabase.IsValidFolder(MeshDir)) AssetDatabase.CreateFolder("Assets/Map/Meshes", "MapaA");

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var root = new GameObject("MapaA");
            var material = AssetDatabase.LoadAssetAtPath<Material>("Assets/Map/Materials/MapAtlas.mat");

            var edges = BuildEdges();
            var pathPts = new List<Vector2>();
            for (int i = 0; i < edges.Length; i++)
            {
                var e = edges[i];
                var ctrl = new List<Vector2> { Nodes[e.a] };
                ctrl.AddRange(e.via);
                ctrl.Add(Nodes[e.b]);
                e.pts = Spline(ctrl.ToArray());
                e.length = Length(e.pts);
                e.count = Mathf.Max(0, Mathf.RoundToInt(e.length / TileSpacing) - 1);
                edges[i] = e;
                for (int k = 0; k < e.pts.Count; k += 2) pathPts.Add(e.pts[k]);
            }

            var kinds = BuildTerrain(root.transform, material, pathPts);
            BuildPath(root.transform, edges);
            Decorate(root.transform, kinds);
            SetupLightAndCamera(root.transform);

            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
        }

        // ---------------- Camino ----------------

        static void BuildPath(Transform parent, Edge[] edges)
        {
            var camino = new GameObject("Camino").transform;
            camino.SetParent(parent, false);

            // Distancia en casillas desde INICIO (Dijkstra sobre el grafo de nodos).
            var dist = new Dictionary<string, int>();
            foreach (var n in Nodes.Keys) dist[n] = int.MaxValue;
            dist["S"] = 0;
            for (int iter = 0; iter < Nodes.Count; iter++)
                foreach (var e in edges)
                {
                    int w = e.count + 1;
                    if (dist[e.a] != int.MaxValue && dist[e.a] + w < dist[e.b]) dist[e.b] = dist[e.a] + w;
                    if (dist[e.b] != int.MaxValue && dist[e.b] + w < dist[e.a]) dist[e.a] = dist[e.b] + w;
                }

            var rng = new System.Random(11);
            var usage = new int[7];
            var nodeColor = new Dictionary<string, PathTileColor>();
            foreach (var n in Nodes.Keys) nodeColor[n] = PickColor(rng, usage, -1, -1);

            var nodeDone = new HashSet<string>();
            foreach (var e in edges)
            {
                if (nodeDone.Add(e.a)) PlaceTile(camino, Nodes[e.a], Tangent(e.pts, 0), nodeColor[e.a], dist[e.a], e.a);
                if (nodeDone.Add(e.b)) PlaceTile(camino, Nodes[e.b], Tangent(e.pts, e.pts.Count - 1), nodeColor[e.b], dist[e.b], e.b);

                int prev = (int)nodeColor[e.a];
                for (int k = 1; k <= e.count; k++)
                {
                    int next = k == e.count ? (int)nodeColor[e.b] : -1;
                    var color = PickColor(rng, usage, prev, next);
                    prev = (int)color;
                    int idx = Mathf.Min(dist[e.a] + k, dist[e.b] + e.count + 1 - k);
                    float d = k * e.length / (e.count + 1);
                    int pi = IndexAtLength(e.pts, d);
                    PlaceTile(camino, e.pts[pi], Tangent(e.pts, pi), color, idx, null);
                }
            }
        }

        static PathTileColor PickColor(System.Random rng, int[] usage, int avoidA, int avoidB)
        {
            int best = -1, bestUse = int.MaxValue;
            var order = new List<int> { 0, 1, 2, 3, 4, 5, 6 };
            for (int i = order.Count - 1; i > 0; i--) { int j = rng.Next(i + 1); var t = order[i]; order[i] = order[j]; order[j] = t; }
            foreach (int c in order)
            {
                if (c == avoidA || c == avoidB) continue;
                if (usage[c] < bestUse) { best = c; bestUse = usage[c]; }
            }
            usage[best]++;
            return (PathTileColor)best;
        }

        static void PlaceTile(Transform parent, Vector2 p, Vector2 tangent, PathTileColor color, int index, string nodeName)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Map/Prefabs/PathTiles/Casilla_" + color + ".prefab");
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            go.name = "Casilla_" + index.ToString("000") + "_" + color + (nodeName != null ? "_" + nodeName : "");
            float yaw = Mathf.Atan2(-tangent.y, tangent.x) * Mathf.Rad2Deg;
            go.transform.SetPositionAndRotation(new Vector3(p.x, GroundTop, p.y), Quaternion.Euler(0, yaw, 0));
            go.GetComponent<PathTile>().Set(color, index);
        }

        static List<Vector2> Spline(Vector2[] p)
        {
            var res = new List<Vector2>();
            for (int i = 0; i < p.Length - 1; i++)
            {
                var p0 = p[Mathf.Max(i - 1, 0)]; var p1 = p[i]; var p2 = p[i + 1]; var p3 = p[Mathf.Min(i + 2, p.Length - 1)];
                int steps = Mathf.Max(1, Mathf.CeilToInt(Vector2.Distance(p1, p2) / 0.2f));
                for (int s = 0; s < steps; s++)
                {
                    float t = (float)s / steps;
                    res.Add(0.5f * ((2f * p1) + (-p0 + p2) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t * t + (-p0 + 3f * p1 - 3f * p2 + p3) * t * t * t));
                }
            }
            res.Add(p[p.Length - 1]);
            return res;
        }

        static float Length(List<Vector2> pts)
        {
            float l = 0;
            for (int i = 1; i < pts.Count; i++) l += Vector2.Distance(pts[i - 1], pts[i]);
            return l;
        }

        static int IndexAtLength(List<Vector2> pts, float d)
        {
            float l = 0;
            for (int i = 1; i < pts.Count; i++)
            {
                l += Vector2.Distance(pts[i - 1], pts[i]);
                if (l >= d) return i;
            }
            return pts.Count - 1;
        }

        static Vector2 Tangent(List<Vector2> pts, int i)
        {
            int a = Mathf.Max(i - 2, 0), b = Mathf.Min(i + 2, pts.Count - 1);
            var t = pts[b] - pts[a];
            return t.sqrMagnitude < 1e-6f ? Vector2.right : t.normalized;
        }

        // ---------------- Terreno ----------------

        static Kind[,] BuildTerrain(Transform parent, Material material, List<Vector2> pathPts)
        {
            var kinds = new Kind[Width, Depth];
            var chunks = new Dictionary<Vector2Int, VoxelMeshBuilder>();

            for (int x = -1; x <= Width; x++)
                for (int z = -1; z <= Depth; z++)
                {
                    bool frame = x < 0 || x >= Width || z < 0 || z >= Depth;
                    Kind k = frame ? Kind.Wood : Classify(x, z, pathPts);
                    if (!frame) kinds[x, z] = k;

                    var key = new Vector2Int(Mathf.Clamp(x, 0, Width - 1) / 16, Mathf.Clamp(z, 0, Depth - 1) / 16);
                    if (!chunks.TryGetValue(key, out var b)) chunks[key] = b = new VoxelMeshBuilder();
                    float cx = x + 0.5f, cz = z + 0.5f;
                    switch (k)
                    {
                        case Kind.Water: b.Add(cx, -1.5f, cz, 1, 1.9f, 1, Water); break;
                        case Kind.Sand: b.Add(cx, 0, cz, 1, 1, 1, Sand); break;
                        case Kind.Grass: b.Add(cx, 0, cz, 1, 1, 1, GrassTop, GrassSide); break;
                        case Kind.Rock: b.Add(cx, 0, cz, 1, 1, 1, Rock); break;
                        case Kind.Parchment: b.Add(cx, 0, cz, 1, 1, 1, Parchment); break;
                        default: b.Add(cx, 0, cz, 1, 1, 1, Planks); break;
                    }
                }

            var terreno = new GameObject("Terreno").transform;
            terreno.SetParent(parent, false);
            foreach (var kv in chunks)
            {
                string name = "Terreno_" + kv.Key.x + "_" + kv.Key.y;
                var mesh = kv.Value.ToMesh(name);
                string path = MeshDir + "/" + name + ".asset";
                AssetDatabase.DeleteAsset(path);
                AssetDatabase.CreateAsset(mesh, path);
                var go = new GameObject(name);
                go.transform.SetParent(terreno, false);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                go.AddComponent<MeshRenderer>().sharedMaterial = material;
                GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
            }
            return kinds;
        }

        static Kind Classify(int x, int z, List<Vector2> pathPts)
        {
            var c = new Vector2(x + 0.5f, z + 0.5f);
            if (x >= 28 && x < 36 && (z < 7 || z >= 41)) return Kind.Wood;               // muelles de INICIO y META

            float d = float.MaxValue;
            foreach (var p in pathPts) d = Mathf.Min(d, (p - c).sqrMagnitude);
            d = Mathf.Sqrt(d);
            if (d <= 2f) return Kind.Sand;                                                  // cinta del camino

            if (x >= 3 && x <= 8 && z >= 2 && z <= 7) return Kind.Rock;                     // montanas
            if (Vector2.Distance(c, V(21, 9.5f)) <= 3f) return Kind.Rock;                   // cueva
            float di = Vector2.Distance(c, V(55, 6));                                        // isla palmera
            if (di <= 3.8f) return Kind.Grass;
            if (di <= 6f) return Kind.Sand;
            if (Vector2.Distance(c, V(3, 14)) <= 2.5f) return Kind.Sand;                    // X del tesoro
            if (x >= 3 && x <= 7 && z >= 41 && z <= 45) return Kind.Parchment;              // rosa de los vientos
            if (Vector2.Distance(c, V(34, 34)) <= 1.6f) return Kind.Sand;                   // islote del ancla
            if (d <= 3f) return Kind.Grass;
            return Kind.Water;
        }

        // ---------------- Decoracion ----------------

        static Transform decoRoot;
        static Kind[,] groundKinds;

        static void Decorate(Transform parent, Kind[,] kinds)
        {
            groundKinds = kinds;
            decoRoot = new GameObject("Decoracion").transform;
            decoRoot.SetParent(parent, false);

            // Muelles de INICIO y META
            Place("Cartel_Inicio", 28.8f, 6.3f, 45);
            Place("Cartel_Meta", 28.8f, 42.7f, 45);
            Place("BanderaPirata", 35.3f, 5.8f, 0);
            Place("Barril", 35.6f, 1.6f, 0); Place("Caja", 34.6f, 1.4f, 15); Place("Barril", 35.8f, 2.7f, 30);
            Place("Canon", 29f, 1.6f, 90);
            Place("PosteMuelle", 28.3f, 0.5f, 0); Place("PosteMuelle", 35.7f, 0.5f, 0);
            Place("Calavera", 35.2f, 43.5f, 0);
            Place("Cofre", 29.2f, 45.2f, 0);
            Place("PosteMuelle", 28.3f, 47.5f, 0); Place("PosteMuelle", 35.7f, 47.5f, 0);

            // Escenario
            Place("RosaDeLosVientos", 5.5f, 43.5f, 0, GroundTop);
            Animate(Place("Kraken", 17f, 27f, 0, 0.2f), 0.1f, 0.35f, 0f);
            Animate(Place("Remolino", 21f, 21f, 0, 0.43f), 0.02f, 0.4f, 25f);
            Animate(Place("BarcoPirata", 49f, 29f, 30, 0.25f), 0.07f, 0.3f, 0f);
            Place("Ancla", 34f, 34f, 20);
            Place("XTesoro", 3f, 14f, 0, GroundTop);
            Place("Montana", 6f, 5f, 0);
            Place("CuevaArco", 21f, 9.5f, 0);
            Place("Roca", 11.5f, 2.5f, 20, 0.3f); Place("Roca", 2.5f, 20f, 70, 0.3f);
            Place("Roca", 44f, 15f, 40, 0.3f); Place("Roca", 26f, 36f, 10, 0.3f);

            // Isla de la palmera
            Place("Palmera", 56.5f, 4f, 0); Place("Palmera", 58f, 7f, 90); Place("Palmera", 53.5f, 2.5f, 180); Place("Palmera", 51f, 3f, 45);
            Place("Cofre", 55.5f, 6.5f, 30);
        }

        static void Animate(GameObject go, float amplitude, float speed, float spin)
        {
            go.AddComponent<Bobbing>().Configure(amplitude, speed, spin);
            GameObjectUtility.SetStaticEditorFlags(go, 0);   // se mueve: fuera del static batching
        }

        static GameObject Place(string name, float x, float z, float yaw, float? y = null)
        {
            string dir = name.StartsWith("Casilla") ? "PathTiles" : "Props";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Map/Prefabs/" + dir + "/" + name + ".prefab");
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, decoRoot);
            float h = y ?? (groundKinds[Mathf.Clamp((int)x, 0, Width - 1), Mathf.Clamp((int)z, 0, Depth - 1)] == Kind.Water ? WaterTop : GroundTop);
            go.transform.SetPositionAndRotation(new Vector3(x, h, z), Quaternion.Euler(0, yaw, 0));
            return go;
        }

        // ---------------- Luz y camara ----------------

        static void SetupLightAndCamera(Transform parent)
        {
            var lg = new GameObject("Sol");
            lg.transform.SetParent(parent, false);
            var l = lg.AddComponent<Light>();
            l.type = LightType.Directional;
            l.intensity = 1.15f;
            l.shadows = LightShadows.None;   // look plano y menos costo; una sola luz direccional
            l.color = new Color(1f, 0.96f, 0.88f);
            lg.transform.rotation = Quaternion.Euler(50, 30, 0);

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.55f, 0.6f, 0.68f);

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Map/Prefabs/IsoPreviewCamera.prefab");
            var cam = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            cam.GetComponent<Camera>().orthographicSize = 24f;
            cam.transform.position = new Vector3(Width / 2f, 1f, Depth / 2f) - cam.transform.forward * 60f;
        }
    }
}
