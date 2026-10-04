using System.Collections.Generic;
using UnityEngine;

namespace UKCity.Core
{
    /// <summary>
    /// Immediate-mode overlay drawing: accumulate lines and flat quads during a frame, then Flush() draws them.
    /// Works in any render pipeline (uses Graphics.DrawMesh with a tiny unlit shader).
    /// </summary>
    public sealed class LineBatch
    {
        private readonly List<Vector3> verts = new List<Vector3>();
        private readonly List<Color32> cols = new List<Color32>();
        private readonly List<int> lines = new List<int>();
        private readonly List<int> tris = new List<int>();
        private readonly Mesh mesh;
        private readonly Material material;

        public LineBatch(bool alwaysOnTop)
        {
            mesh = new Mesh { name = "Overlay" };
            mesh.MarkDynamic();
            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            var shader = Shader.Find("UKCity/Lines");
            if (shader == null) shader = Shader.Find("Hidden/Internal-Colored");
            material = new Material(shader);
            material.SetFloat("_ZTest", alwaysOnTop ? (float)UnityEngine.Rendering.CompareFunction.Always : (float)UnityEngine.Rendering.CompareFunction.LessEqual);
        }

        public void Line(Vector3 a, Vector3 b, Color c)
        {
            int i = verts.Count;
            verts.Add(a); verts.Add(b);
            cols.Add(c); cols.Add(c);
            lines.Add(i); lines.Add(i + 1);
        }

        /// <summary>A flat quad strip in the XZ plane at height y, from a to b with the given width.</summary>
        public void Strip(Vector2 a, Vector2 b, float width, float y, Color c)
        {
            var d = b - a;
            if (d.sqrMagnitude < 1e-6f) return;
            var perp = new Vector2(-d.y, d.x).normalized * (width * 0.5f);
            int i = verts.Count;
            verts.Add(new Vector3(a.x + perp.x, y, a.y + perp.y));
            verts.Add(new Vector3(b.x + perp.x, y, b.y + perp.y));
            verts.Add(new Vector3(b.x - perp.x, y, b.y - perp.y));
            verts.Add(new Vector3(a.x - perp.x, y, a.y - perp.y));
            for (int k = 0; k < 4; k++) cols.Add(c);
            tris.Add(i); tris.Add(i + 1); tris.Add(i + 2);
            tris.Add(i); tris.Add(i + 2); tris.Add(i + 3);
        }

        /// <summary>Filled flat disc (approximated) in the XZ plane.</summary>
        public void Disc(Vector2 centre, float radius, float y, Color c, int segments = 16)
        {
            int i0 = verts.Count;
            verts.Add(new Vector3(centre.x, y, centre.y));
            cols.Add(c);
            for (int s = 0; s <= segments; s++)
            {
                float a = s * Mathf.PI * 2f / segments;
                verts.Add(new Vector3(centre.x + Mathf.Cos(a) * radius, y, centre.y + Mathf.Sin(a) * radius));
                cols.Add(c);
                if (s > 0) { tris.Add(i0); tris.Add(i0 + s + 1); tris.Add(i0 + s); }
            }
        }

        public void Circle(Vector2 centre, float radius, float y, Color c, int segments = 32)
        {
            var prev = new Vector3(centre.x + radius, y, centre.y);
            for (int s = 1; s <= segments; s++)
            {
                float a = s * Mathf.PI * 2f / segments;
                var p = new Vector3(centre.x + Mathf.Cos(a) * radius, y, centre.y + Mathf.Sin(a) * radius);
                Line(prev, p, c);
                prev = p;
            }
        }

        public void Rect(RectInt r, float y, Color c)
        {
            var a = new Vector3(r.xMin, y, r.yMin);
            var b = new Vector3(r.xMax, y, r.yMin);
            var d = new Vector3(r.xMax, y, r.yMax);
            var e = new Vector3(r.xMin, y, r.yMax);
            Line(a, b, c); Line(b, d, c); Line(d, e, c); Line(e, a, c);
        }

        public void WireBox(Vector3 min, Vector3 max, Color c)
        {
            var p = new[]
            {
                new Vector3(min.x, min.y, min.z), new Vector3(max.x, min.y, min.z), new Vector3(max.x, min.y, max.z), new Vector3(min.x, min.y, max.z),
                new Vector3(min.x, max.y, min.z), new Vector3(max.x, max.y, min.z), new Vector3(max.x, max.y, max.z), new Vector3(min.x, max.y, max.z)
            };
            for (int i = 0; i < 4; i++)
            {
                Line(p[i], p[(i + 1) % 4], c);
                Line(p[i + 4], p[(i + 1) % 4 + 4], c);
                Line(p[i], p[i + 4], c);
            }
        }

        public void Flush(Camera cam)
        {
            if (verts.Count == 0) return;
            mesh.Clear();
            mesh.SetVertices(verts);
            mesh.SetColors(cols);
            mesh.subMeshCount = 2;
            mesh.SetIndices(tris, MeshTopology.Triangles, 0, false);
            mesh.SetIndices(lines, MeshTopology.Lines, 1, false);
            mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 100000f);
            Graphics.DrawMesh(mesh, Matrix4x4.identity, material, 0, cam, 0);
            Graphics.DrawMesh(mesh, Matrix4x4.identity, material, 0, cam, 1);
            verts.Clear(); cols.Clear(); lines.Clear(); tris.Clear();
        }
    }
}
