using System.Collections.Generic;
using UnityEngine;

namespace RobaColores.Map.Editor
{
    // Construye mallas de cajas con UV hacia celdas del atlas (8x8 celdas de 16 px).
    public class VoxelMeshBuilder
    {
        public const int Cells = 8;

        readonly List<Vector3> verts = new List<Vector3>();
        readonly List<Vector3> normals = new List<Vector3>();
        readonly List<Vector2> uvs = new List<Vector2>();
        readonly List<int> tris = new List<int>();

        // Caja con base en y0 (pivote abajo al centro). Sin cara inferior.
        public VoxelMeshBuilder Add(float cx, float y0, float cz, float sx, float sy, float sz, int top, int side)
        {
            var c = new Vector3(cx, y0 + sy * 0.5f, cz);
            var h = new Vector3(sx, sy, sz) * 0.5f;
            Face(c, h, Vector3.up, Vector3.right, Vector3.forward, top);
            Face(c, h, Vector3.right, Vector3.forward, Vector3.up, side);
            Face(c, h, Vector3.left, Vector3.forward, Vector3.up, side);
            Face(c, h, Vector3.forward, Vector3.right, Vector3.up, side);
            Face(c, h, Vector3.back, Vector3.right, Vector3.up, side);
            return this;
        }

        public VoxelMeshBuilder Add(float cx, float y0, float cz, float sx, float sy, float sz, int cell)
        {
            return Add(cx, y0, cz, sx, sy, sz, cell, cell);
        }

        void Face(Vector3 c, Vector3 h, Vector3 n, Vector3 u, Vector3 v, int cell)
        {
            var ctr = c + Vector3.Scale(n, h);
            var du = Vector3.Scale(u, h);
            var dv = Vector3.Scale(v, h);
            var p0 = ctr - du - dv;
            var p1 = ctr - du + dv;
            var p2 = ctr + du + dv;
            var p3 = ctr + du - dv;

            float cs = 1f / Cells;
            float u0 = (cell % Cells) * cs, v0 = (cell / Cells) * cs;
            int b = verts.Count;
            verts.AddRange(new[] { p0, p1, p2, p3 });
            for (int i = 0; i < 4; i++) normals.Add(n);
            uvs.Add(new Vector2(u0, v0));
            uvs.Add(new Vector2(u0, v0 + cs));
            uvs.Add(new Vector2(u0 + cs, v0 + cs));
            uvs.Add(new Vector2(u0 + cs, v0));

            // El frente en Unity es horario: la normal = Cross(p1-p0, p2-p0).
            bool front = Vector3.Dot(Vector3.Cross(p1 - p0, p2 - p0), n) > 0f;
            if (front) tris.AddRange(new[] { b, b + 1, b + 2, b, b + 2, b + 3 });
            else tris.AddRange(new[] { b, b + 2, b + 1, b, b + 3, b + 2 });
        }

        public Mesh ToMesh(string name)
        {
            var m = new Mesh { name = name };
            m.SetVertices(verts);
            m.SetNormals(normals);
            m.SetUVs(0, uvs);
            m.SetTriangles(tris, 0);
            m.RecalculateBounds();
            return m;
        }
    }
}
