using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace RobaColores.Map.Editor
{
    public class LayoutEdge
    {
        public string a, b;
        public List<Vector2> pts;
        public float length;
        public int weight;          // pasos (casillas) entre a y b: casillas intermedias + 1
        public float amplitude;     // desvio lateral usado para igualar la longitud
        public float minRadius = float.PositiveInfinity;
    }

    public class Layout
    {
        public Dictionary<string, Vector2> nodes = new Dictionary<string, Vector2>();
        public List<LayoutEdge> edges = new List<LayoutEdge>();
    }

    public static class MapAGeometry
    {
        public const float TileSpacing = 2.1f;

        public static List<Vector2> Spline(Vector2[] p)
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

        public static float Length(List<Vector2> pts)
        {
            float l = 0;
            for (int i = 1; i < pts.Count; i++) l += Vector2.Distance(pts[i - 1], pts[i]);
            return l;
        }

        public static int IndexAtLength(List<Vector2> pts, float d)
        {
            float l = 0;
            for (int i = 1; i < pts.Count; i++)
            {
                l += Vector2.Distance(pts[i - 1], pts[i]);
                if (l >= d) return i;
            }
            return pts.Count - 1;
        }

        public static Vector2 Tangent(List<Vector2> pts, int i)
        {
            int a = Mathf.Max(i - 2, 0), b = Mathf.Min(i + 2, pts.Count - 1);
            var t = pts[b] - pts[a];
            return t.sqrMagnitude < 1e-6f ? Vector2.right : t.normalized;
        }

        // Curva de P a Q con desvio lateral sin(pi t) (arco) o sin(2 pi t) (S), ajustada por biseccion
        // para que su longitud sea exactamente "target". Con target <= baseline queda recta.
        public static List<Vector2> Meander(Vector2 p, Vector2 q, float target, bool bulge, float sign, out float amplitude, out float minRadius)
        {
            amplitude = 0f;
            var dir = q - p;
            var normal = new Vector2(-dir.y, dir.x).normalized * sign;
            Func<float, List<Vector2>> build = amp =>
            {
                var l = new List<Vector2>();
                const int n = 120;
                for (int i = 0; i <= n; i++)
                {
                    float t = (float)i / n;
                    float shape = bulge ? Mathf.Sin(Mathf.PI * t) : Mathf.Sin(2f * Mathf.PI * t);
                    l.Add(p + dir * t + normal * (amp * shape));
                }
                return l;
            };

            var pts = build(0f);
            if (target > Length(pts) + 0.005f)
            {
                float lo = 0f, hi = 20f;
                for (int it = 0; it < 60; it++)
                {
                    float mid = (lo + hi) * 0.5f;
                    if (Length(build(mid)) < target) lo = mid; else hi = mid;
                }
                amplitude = (lo + hi) * 0.5f;
                pts = build(amplitude);
            }
            minRadius = MinRadius(pts);
            return pts;
        }

        public static float MinRadius(List<Vector2> pts)
        {
            float min = float.PositiveInfinity;
            for (int i = 2; i < pts.Count; i += 2)
            {
                Vector2 a = pts[i - 2], b = pts[i - 1], c = pts[i];
                float area = Mathf.Abs((b.x - a.x) * (c.y - a.y) - (c.x - a.x) * (b.y - a.y)) * 0.5f;
                if (area < 1e-5f) continue;
                float r = Vector2.Distance(a, b) * Vector2.Distance(b, c) * Vector2.Distance(a, c) / (4f * area);
                min = Mathf.Min(min, r);
            }
            return min;
        }
    }

    // Layout original (antes del rediseno). Se conserva solo para medir "antes".
    public static class MapALayoutLegacy
    {
        static Vector2 V(float x, float z) { return new Vector2(x, z); }

        static void E(Layout l, string a, string b, params Vector2[] via)
        {
            var ctrl = new List<Vector2> { l.nodes[a] };
            ctrl.AddRange(via);
            ctrl.Add(l.nodes[b]);
            var e = new LayoutEdge { a = a, b = b, pts = MapAGeometry.Spline(ctrl.ToArray()) };
            e.length = MapAGeometry.Length(e.pts);
            e.weight = Mathf.Max(0, Mathf.RoundToInt(e.length / MapAGeometry.TileSpacing) - 1) + 1;
            l.edges.Add(e);
        }

        public static Layout Build()
        {
            var l = new Layout();
            l.nodes["S"] = V(32, 3); l.nodes["M"] = V(32, 45);
            l.nodes["N1"] = V(30, 17); l.nodes["N2"] = V(28, 29); l.nodes["N3"] = V(37, 24); l.nodes["N4"] = V(39, 30);
            l.nodes["N5"] = V(14, 9); l.nodes["N6"] = V(8, 32); l.nodes["N7"] = V(57, 27);
            l.nodes["P"] = V(38, 17); l.nodes["Q"] = V(31, 24); l.nodes["X"] = V(34, 22);
            E(l, "S", "N5", V(24, 5));
            E(l, "N5", "N6", V(8, 16), V(6, 24));
            E(l, "N6", "M", V(12, 38), V(18, 42), V(26, 44));
            E(l, "S", "N1", V(31, 10));
            E(l, "N1", "Q");
            E(l, "Q", "N2");
            E(l, "N2", "M", V(30, 35), V(30, 41));
            E(l, "S", "P", V(36, 10));
            E(l, "P", "N3");
            E(l, "N3", "N4");
            E(l, "N4", "M", V(38, 36), V(36, 41));
            E(l, "S", "N7", V(40, 5), V(48, 7), V(54, 12), V(57, 19));
            E(l, "N7", "M", V(52, 33), V(46, 37), V(40, 42));
            E(l, "N1", "N5", V(24, 14), V(19, 11));
            E(l, "N4", "N7", V(45, 29), V(51, 28));
            E(l, "N6", "N2", V(14, 31), V(21, 32));
            E(l, "N1", "X"); E(l, "X", "N3"); E(l, "Q", "X"); E(l, "X", "P");
            return l;
        }
    }

    // Layout equilibrado: dos carriles laterales (arcos hacia afuera) y un cruce central en X.
    // Tres etapas (salida, tramo central, llegada); en cada etapa todas las alternativas tienen la misma
    // longitud, asi que toda ruta INICIO -> META mide lo mismo.
    public static class MapALayoutBalanced
    {
        public static float LaneSpacing = 14f, Row1 = 13f;
        public const float StartZ = 5f, FinishZ = 43f, CenterX = 32f;

        static Vector2 V(float x, float z) { return new Vector2(x, z); }

        public static Layout Build()
        {
            var l = new Layout();
            float row2 = StartZ + FinishZ - Row1;
            var n = l.nodes;
            n["S"] = V(CenterX, StartZ); n["M"] = V(CenterX, FinishZ);
            n["A1"] = V(CenterX - LaneSpacing, Row1); n["C1"] = V(CenterX + LaneSpacing, Row1);
            n["A2"] = V(CenterX - LaneSpacing, row2); n["C2"] = V(CenterX + LaneSpacing, row2);
            n["X"] = (n["A1"] + n["C2"]) * 0.5f;   // A1-C2 y C1-A2 se cortan en su punto medio comun

            float t0 = Vector2.Distance(n["S"], n["A1"]);
            int w0 = Mathf.Max(2, Mathf.RoundToInt(t0 / MapAGeometry.TileSpacing));
            float cross = Vector2.Distance(n["A1"], n["C2"]);
            float t1 = Mathf.Max(cross, Vector2.Distance(n["A1"], n["A2"]));
            int w1 = 2 * Mathf.Max(1, Mathf.RoundToInt(t1 / (2f * MapAGeometry.TileSpacing)));

            // Etapa 0 (salida) y etapa 2 (llegada): diagonales rectas de igual largo.
            Edge(l, "S", "A1", t0, w0, true, 1f);
            Edge(l, "S", "C1", t0, w0, true, -1f);
            Edge(l, "A2", "M", t0, w0, true, 1f);
            Edge(l, "C2", "M", t0, w0, true, -1f);

            // Etapa 1: los carriles laterales se arquean hacia afuera hasta igualar la longitud del cruce.
            Edge(l, "A1", "A2", t1, w1, true, 1f);
            Edge(l, "C1", "C2", t1, w1, true, -1f);
            Half(l, "A1", "X", w1 / 2); Half(l, "X", "C2", w1 / 2);
            Half(l, "C1", "X", w1 / 2); Half(l, "X", "A2", w1 / 2);
            return l;
        }

        static void Edge(Layout l, string a, string b, float target, int weight, bool bulge, float sign)
        {
            var e = new LayoutEdge { a = a, b = b, weight = weight };
            e.pts = MapAGeometry.Meander(l.nodes[a], l.nodes[b], target, bulge, sign, out e.amplitude, out e.minRadius);
            e.length = MapAGeometry.Length(e.pts);
            l.edges.Add(e);
        }

        static void Half(Layout l, string a, string b, int weight)
        {
            var e = new LayoutEdge { a = a, b = b, weight = weight };
            e.pts = MapAGeometry.Meander(l.nodes[a], l.nodes[b], 0f, true, 1f, out e.amplitude, out e.minRadius);
            e.length = MapAGeometry.Length(e.pts);
            l.edges.Add(e);
        }
    }

    public static class MapARouteAnalyzer
    {
        public struct Route { public string path; public float length; public int steps; }

        // Rutas "hacia adelante": solo se avanza por aristas que acercan (por longitud) a META.
        public static List<Route> Routes(Layout l)
        {
            var distM = new Dictionary<string, float>();
            foreach (var k in l.nodes.Keys) distM[k] = float.PositiveInfinity;
            distM["M"] = 0;
            for (int i = 0; i < l.nodes.Count; i++)
                foreach (var e in l.edges)
                {
                    if (distM[e.b] + e.length < distM[e.a]) distM[e.a] = distM[e.b] + e.length;
                    if (distM[e.a] + e.length < distM[e.b]) distM[e.b] = distM[e.a] + e.length;
                }

            var res = new List<Route>();
            void Dfs(string u, string path, float len, int steps)
            {
                if (u == "M") { res.Add(new Route { path = path, length = len, steps = steps }); return; }
                foreach (var e in l.edges)
                {
                    string v = e.a == u ? e.b : e.b == u ? e.a : null;
                    if (v == null || distM[v] >= distM[u] - 0.01f) continue;
                    Dfs(v, path + ">" + v, len + e.length, steps + e.weight);
                }
            }
            Dfs("S", "S", 0, 0);
            return res;
        }

        public static string Report(Layout l, string title)
        {
            var routes = Routes(l);
            var sb = new StringBuilder();
            sb.AppendLine("== " + title + " ==");
            float min = float.MaxValue, max = 0; int smin = int.MaxValue, smax = 0;
            routes.Sort((x, y) => x.length.CompareTo(y.length));
            foreach (var r in routes)
            {
                sb.AppendLine(string.Format("{0,-34} {1,6:F2} u  {2,3} pasos", r.path, r.length, r.steps));
                min = Mathf.Min(min, r.length); max = Mathf.Max(max, r.length);
                smin = Mathf.Min(smin, r.steps); smax = Mathf.Max(smax, r.steps);
            }
            sb.AppendLine(string.Format("rutas: {0} | longitud min {1:F2} max {2:F2} | diferencia {3:F2}% | pasos {4}-{5}", routes.Count, min, max, (max - min) / min * 100f, smin, smax));
            float r0 = float.PositiveInfinity;
            foreach (var e in l.edges) r0 = Mathf.Min(r0, e.minRadius);
            sb.AppendLine("radio minimo de curva: " + (float.IsInfinity(r0) ? "recto" : r0.ToString("F2")));
            return sb.ToString();
        }

        [MenuItem("Roba Colores/Analizar rutas Mapa A")]
        public static void Run()
        {
            Debug.Log(Report(MapALayoutLegacy.Build(), "ANTES (layout original)"));
            Debug.Log(Report(MapALayoutBalanced.Build(), "DESPUES (layout equilibrado)"));
        }
    }
}
