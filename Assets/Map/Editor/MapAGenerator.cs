using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using RobaColores.Game.Core;
using RobaColores.Map;
using static RobaColores.Map.Editor.MapAtlasGenerator;

namespace RobaColores.Map.Editor
{
    // Construye la escena del Mapa A (horizontal) a partir de la biblioteca.
    // El trazado del camino vive en MapALayoutBalanced; aqui estan el terreno y la decoracion.
    public static class MapAGenerator
    {
        const int Width = 64, Depth = 48;
        const float GroundTop = 1f, WaterTop = 0.4f;
        const string ScenePath = "Assets/Map/Scenes/MapaA.unity";
        const string MeshDir = "Assets/Map/Meshes/MapaA";

        enum Kind { Water, Sand, Grass, Rock, Wood, Parchment, Gold }

        static Vector2 V(float x, float z) { return new Vector2(x, z); }

        static readonly string[] Junctions = { "A1", "C1", "A2", "C2", "X" };

        [MenuItem("Roba Colores/Fase 3 - Generar Mapa A")]
        public static void Run()
        {
            MapSignGenerator.Run();
            if (!AssetDatabase.IsValidFolder(MeshDir)) AssetDatabase.CreateFolder("Assets/Map/Meshes", "MapaA");

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var root = new GameObject("MapaA");
            var material = AssetDatabase.LoadAssetAtPath<Material>("Assets/Map/Materials/MapAtlas.mat");

            var layout = MapALayoutBalanced.Build();
            var pathPts = new List<Vector2>();
            foreach (var e in layout.edges)
                for (int k = 0; k < e.pts.Count; k += 2) pathPts.Add(e.pts[k]);

            var kinds = BuildTerrain(root.transform, material, layout, pathPts);
            BuildPath(root.transform, layout);
            Decorate(root.transform, kinds, layout);
            SetupLightAndCamera(root.transform);

            // El mapa tambien se guarda como prefab para reutilizarlo en la escena de partida.
            PrefabUtility.SaveAsPrefabAssetAndConnect(root, "Assets/Map/Prefabs/MapaA.prefab", InteractionMode.AutomatedAction);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
        }

        // ---------------- Camino ----------------

        static void BuildPath(Transform parent, Layout layout)
        {
            var camino = new GameObject("Camino").transform;
            camino.SetParent(parent, false);

            // Distancia en casillas desde INICIO (Dijkstra sobre el grafo de nodos).
            var dist = new Dictionary<string, int>();
            foreach (var n in layout.nodes.Keys) dist[n] = int.MaxValue;
            dist["S"] = 0;
            for (int iter = 0; iter < layout.nodes.Count; iter++)
                foreach (var e in layout.edges)
                {
                    if (dist[e.a] != int.MaxValue && dist[e.a] + e.weight < dist[e.b]) dist[e.b] = dist[e.a] + e.weight;
                    if (dist[e.b] != int.MaxValue && dist[e.b] + e.weight < dist[e.a]) dist[e.a] = dist[e.b] + e.weight;
                }

            var rng = new System.Random(11);
            var usage = new int[7];
            var nodeColor = new Dictionary<string, PathTileColor>();
            foreach (var n in layout.nodes.Keys) nodeColor[n] = PickColor(rng, usage, -1, -1);

            var all = new List<PathTile>();
            var nodeTiles = new Dictionary<string, PathTile>();
            foreach (var e in layout.edges)
            {
                if (!nodeTiles.ContainsKey(e.a)) nodeTiles[e.a] = PlaceTile(camino, all, layout.nodes[e.a], MapAGeometry.Tangent(e.pts, 0), nodeColor[e.a], dist[e.a], e.a);
                if (!nodeTiles.ContainsKey(e.b)) nodeTiles[e.b] = PlaceTile(camino, all, layout.nodes[e.b], MapAGeometry.Tangent(e.pts, e.pts.Count - 1), nodeColor[e.b], dist[e.b], e.b);
                var chain = new List<PathTile> { nodeTiles[e.a] };

                int prev = (int)nodeColor[e.a];
                int interior = e.weight - 1;
                for (int k = 1; k <= interior; k++)
                {
                    int next = k == interior ? (int)nodeColor[e.b] : -1;
                    var color = PickColor(rng, usage, prev, next);
                    prev = (int)color;
                    int idx = Mathf.Min(dist[e.a] + k, dist[e.b] + e.weight - k);
                    float d = k * e.length / e.weight;
                    int pi = MapAGeometry.IndexAtLength(e.pts, d);
                    chain.Add(PlaceTile(camino, all, e.pts[pi], MapAGeometry.Tangent(e.pts, pi), color, idx, null));
                }
                chain.Add(nodeTiles[e.b]);
                for (int i = 0; i < chain.Count - 1; i++) chain[i].AddNext(chain[i + 1]);   // conexiones hacia adelante
            }

            // Datos de tablero: inicio, meta y dificultad creciente con el avance.
            int maxIndex = dist["M"];
            foreach (var t in all)
            {
                float progress = (float)t.Index / maxIndex;
                var difficulty = progress < 0.34f ? Difficulty.Facil : progress < 0.67f ? Difficulty.Intermedio : Difficulty.Dificil;
                t.ConfigureBoard(t.TileId, t.NodeName, t == nodeTiles["S"], t == nodeTiles["M"], difficulty);
            }
            var board = parent.gameObject.AddComponent<MapBoard>();
            board.SetTiles(all);
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

        static PathTile PlaceTile(Transform parent, List<PathTile> all, Vector2 p, Vector2 tangent, PathTileColor color, int index, string nodeName)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Map/Prefabs/PathTiles/Casilla_" + color + ".prefab");
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            go.name = "Casilla_" + index.ToString("000") + "_" + color + (nodeName != null ? "_" + nodeName : "");
            float yaw = Mathf.Atan2(-tangent.y, tangent.x) * Mathf.Rad2Deg;
            go.transform.SetPositionAndRotation(new Vector3(p.x, GroundTop, p.y), Quaternion.Euler(0, yaw, 0));
            var tile = go.GetComponent<PathTile>();
            tile.Set(color, index);
            tile.ConfigureBoard(all.Count, nodeName, false, false, Difficulty.Facil);
            all.Add(tile);
            return tile;
        }

        // ---------------- Terreno ----------------

        static Kind[,] BuildTerrain(Transform parent, Material material, Layout layout, List<Vector2> pathPts)
        {
            var kinds = new Kind[Width, Depth];
            var chunks = new Dictionary<Vector2Int, VoxelMeshBuilder>();
            var plazas = new List<Vector2>();
            foreach (var j in Junctions) plazas.Add(layout.nodes[j]);

            for (int x = -1; x <= Width; x++)
                for (int z = -1; z <= Depth; z++)
                {
                    bool frame = x < 0 || x >= Width || z < 0 || z >= Depth;
                    Kind k = frame ? Kind.Wood : Classify(x, z, pathPts, plazas);
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
                        case Kind.Gold: b.Add(cx, 0, cz, 1, 1, 1, Gold); break;
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

        static Kind Classify(int x, int z, List<Vector2> pathPts, List<Vector2> plazas)
        {
            var c = new Vector2(x + 0.5f, z + 0.5f);
            if (x >= 28 && x < 36 && z >= 41) return Kind.Gold;                             // plataforma de META
            if (x >= 26 && x < 38 && z < 8) return Kind.Wood;                               // muelle de INICIO

            float d = float.MaxValue;
            foreach (var p in pathPts) d = Mathf.Min(d, (p - c).sqrMagnitude);
            d = Mathf.Sqrt(d);
            if (d <= 1.6f) return Kind.Sand;                                                // cinta del camino (3 de ancho)
            foreach (var p in plazas) if (Vector2.Distance(p, c) <= 2.7f) return Kind.Sand; // plazas de interseccion

            if (x >= 3 && x <= 8 && z >= 2 && z <= 7) return Kind.Rock;                     // montanas
            if (Vector2.Distance(c, V(10.5f, 12f)) <= 3f) return Kind.Rock;                   // cueva
            float di = Vector2.Distance(c, V(55, 6));                                        // isla palmera
            if (di <= 3.8f) return Kind.Grass;
            if (di <= 6f) return Kind.Sand;
            float di2 = Vector2.Distance(c, V(57, 39));                                      // islote norte-este
            if (di2 <= 2f) return Kind.Grass;
            if (di2 <= 3.4f) return Kind.Sand;
            if (Vector2.Distance(c, V(3, 14)) <= 2.5f) return Kind.Sand;                    // X del tesoro
            if (x >= 3 && x <= 7 && z >= 41 && z <= 45) return Kind.Parchment;              // rosa de los vientos
            if (Vector2.Distance(c, V(32, 14)) <= 1.6f) return Kind.Sand;                   // islote del ancla
            if (d <= 2.2f) return Kind.Grass;                                               // borde fino del sendero
            return Kind.Water;
        }

        // ---------------- Decoracion ----------------

        static Transform decoRoot;
        static Kind[,] groundKinds;

        static void Decorate(Transform parent, Kind[,] kinds, Layout layout)
        {
            groundKinds = kinds;
            decoRoot = new GameObject("Decoracion").transform;
            decoRoot.SetParent(parent, false);

            // INICIO: muelle de madera, arco rojo con cartel grande y banderas.
            Place("ArcoInicio", 32f, 1.8f, 0, GroundTop);
            Place("BanderaPirata", 27.4f, 4f, 0); Place("BanderaPirata", 36.6f, 4f, 180);
            Place("Barril", 27.2f, 1.2f, 0); Place("Caja", 28.4f, 1.0f, 15); Place("Barril", 36.8f, 1.4f, 30); Place("Caja", 35.6f, 1.0f, 70);
            Place("Canon", 27.6f, 6.6f, 90);
            Place("PosteMuelle", 26.5f, 0.6f, 0); Place("PosteMuelle", 37.5f, 0.6f, 0);

            // META: plataforma dorada, arco alto con cartel dorado y cofre gigante.
            Place("ArcoMeta", 32f, 47.6f, 0, GroundTop);
            Place("TesoroMeta", 32f, 45.6f, 0, GroundTop);

            // Intersecciones: un farol por nodo, en el hueco entre los caminos que llegan.
            foreach (var j in Junctions)
            {
                var p = layout.nodes[j];
                var dirs = IncidentDirections(layout, j);
                Vector2 best = Vector2.zero; float bestScore = -1f;
                for (int a = 0; a < 16; a++)
                {
                    float ang = a * Mathf.PI * 2f / 16f;
                    var dir = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang));
                    float minAng = 180f;
                    foreach (var d in dirs) minAng = Mathf.Min(minAng, Vector2.Angle(dir, d));
                    if (minAng > bestScore) { bestScore = minAng; best = dir; }
                }
                Place("Farol", p.x + best.x * 2.5f, p.y + best.y * 2.5f, 0);
            }

            // Guia direccional: una flecha por tramo, apuntando hacia META (excluye las mitades del cruce).
            foreach (var e in layout.edges)
            {
                if (e.a == "X" || e.b == "X") continue;
                int pi = MapAGeometry.IndexAtLength(e.pts, e.length * 0.5f + 1.05f);
                var t = MapAGeometry.Tangent(e.pts, pi);
                var side = new Vector2(-t.y, t.x);
                var pos = e.pts[pi] + side * 1.25f;
                // Orientacion hacia META: el nodo "b" de cada arista siempre esta mas cerca de META.
                Place("Flecha", pos.x, pos.y, Mathf.Atan2(-t.y, t.x) * Mathf.Rad2Deg, GroundTop + 0.01f);
            }

            // Escenario
            Place("RosaDeLosVientos", 5.5f, 43.5f, 0, GroundTop);
            Animate(Place("Kraken", 20f, 24f, 0, 0.2f), 0.1f, 0.35f, 0f);        // laguna izquierda
            Animate(Place("BarcoPirata", 44f, 24f, 30, 0.25f), 0.07f, 0.3f, 0f); // laguna derecha
            Animate(Place("Remolino", 32f, 34f, 0, 0.43f), 0.02f, 0.4f, 25f);    // laguna norte
            Place("Ancla", 32f, 14f, 20);                                         // islote sur
            Place("XTesoro", 3f, 14f, 0, GroundTop);
            Place("Montana", 6f, 5f, 0);
            Place("CuevaArco", 10.5f, 12f, 0);
            Place("Roca", 11.5f, 2.5f, 20, 0.3f); Place("Roca", 2.5f, 20f, 70, 0.3f);
            Place("Roca", 60f, 15f, 40, 0.3f); Place("Roca", 50f, 44f, 10, 0.3f); Place("Roca", 8f, 31f, 80, 0.3f);

            // Islas de palmeras
            Place("Palmera", 56.5f, 4f, 0); Place("Palmera", 58f, 7f, 90); Place("Palmera", 53.5f, 2.5f, 180); Place("Palmera", 51f, 3f, 45);
            Place("Cofre", 55.5f, 6.5f, 30);
            Place("Palmera", 57f, 39f, 0); Place("Barril", 56f, 40.4f, 20);
        }

        static List<Vector2> IncidentDirections(Layout layout, string node)
        {
            var res = new List<Vector2>();
            foreach (var e in layout.edges)
            {
                if (e.a == node) res.Add((e.pts[Mathf.Min(8, e.pts.Count - 1)] - e.pts[0]).normalized);
                else if (e.b == node) res.Add((e.pts[Mathf.Max(e.pts.Count - 9, 0)] - e.pts[e.pts.Count - 1]).normalized);
            }
            return res;
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
