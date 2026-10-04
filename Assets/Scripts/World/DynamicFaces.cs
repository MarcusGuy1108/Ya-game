using System.Collections.Generic;
using UnityEngine;
using UKCity.City;

namespace UKCity.World
{
    /// <summary>
    /// Draws the faces that change over time (lit traffic signal aspects, green/red man, flashing Belisha beacons,
    /// school wig-wags, motorway message signs and lane signals) as a small overlay mesh rebuilt each frame,
    /// so chunks never need remeshing for animation.
    /// </summary>
    public sealed class DynamicFaces
    {
        private readonly Dictionary<ChunkCoord, AnimInstance[]> chunks = new Dictionary<ChunkCoord, AnimInstance[]>();
        private readonly List<Vector3> verts = new List<Vector3>();
        private readonly List<Vector3> uvs = new List<Vector3>();
        private readonly List<Color32> cols = new List<Color32>();
        private readonly List<int> tris = new List<int>();
        private readonly Vector3[] pos = new Vector3[4];
        private readonly Vector3[] uvw = new Vector3[4];
        private readonly Mesh mesh;

        public TrafficSignals Signals;
        public Material Material;
        public float Time;

        public DynamicFaces()
        {
            mesh = new Mesh { name = "Animated faces", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            mesh.MarkDynamic();
        }

        public int Count
        {
            get
            {
                int n = 0;
                foreach (var a in chunks.Values) n += a.Length;
                return n;
            }
        }

        public void SetChunk(ChunkCoord c, List<AnimInstance> list)
        {
            if (list.Count == 0) chunks.Remove(c);
            else chunks[c] = list.ToArray();
        }

        public void Remove(ChunkCoord c) => chunks.Remove(c);
        public void Clear() => chunks.Clear();

        /// <summary>Animation frame for one block right now, or -1 for "unlit" (no overlay).</summary>
        public int Frame(BlockDef d, AnimInstance a)
        {
            int facing = BlockState.Facing(a.State);
            float t = Time;
            switch (d.Anim)
            {
                case AnimKind.VehicleSignal:
                    return Signals != null ? (int)Signals.VehicleAspect(a.X, a.Y, a.Z, facing) : (int)TrafficSignals.JunctionAspect(facing & 1, 2, t);
                case AnimKind.PedSignal:
                    return Signals != null && Signals.PedestrianGreen(a.X, a.Y, a.Z, facing) ? 1 : 0;
                case AnimKind.Belisha:
                    return Mod(t + (a.X + a.Z) * 0.01f, 1.1f) < 0.55f ? 0 : -1;
                case AnimKind.WigWag:
                    return (int)(t * 1.6f) % 2;
                case AnimKind.Vms:
                    return (int)(t / 4f) % d.Frames.Length;
                case AnimKind.LaneSignal:
                {
                    float c = Mod(t, 70f);
                    if (c < 25f) return 0;
                    if (c < 35f) return 1;
                    if (c < 45f) return 2;
                    if (c < 55f)
                    {
                        uint h = WorldConst.Hash(a.X, a.Z, 4) % 3;
                        return h == 0 ? 4 : h == 1 ? 5 : 3;
                    }
                    return c < 62f ? 2 : 1;
                }
                case AnimKind.Vas:
                {
                    float c = Mod(t, 2.4f);
                    return c < 1.0f ? 0 : c < 2.0f ? 1 : -1;
                }
            }
            return -1;
        }

        private static float Mod(float a, float m) => a - m * Mathf.Floor(a / m);

        public void Draw(Camera cam, Vector3 eye, float maxDist)
        {
            if (Material == null || chunks.Count == 0) return;
            verts.Clear(); uvs.Clear(); cols.Clear(); tris.Clear();
            float max2 = maxDist * maxDist;
            var white = new Color32(255, 255, 255, 255);
            foreach (var arr in chunks.Values)
            {
                foreach (var a in arr)
                {
                    float dx = a.X + 0.5f - eye.x, dy = a.Y + 0.5f - eye.y, dz = a.Z + 0.5f - eye.z;
                    if (dx * dx + dy * dy + dz * dz > max2) continue;
                    var d = Blocks.Get(a.State);
                    if (d.Anim == AnimKind.None || d.Model == null) continue;
                    int frame = Frame(d, a);
                    if (frame < 0 || frame >= d.Frames.Length) continue;
                    int facing = d.Rotatable ? BlockState.Facing(a.State) : 0;
                    var box = d.Model[d.AnimBox];
                    foreach (int lf in d.AnimFaces)
                    {
                        int f = BlockState.RotFace(lf, facing);
                        ChunkMesher.BoxFace(box, facing, f, pos, uvw, out _);
                        var n = ChunkMesher.FaceNormals[f];
                        var off = new Vector3(n.x, n.y, n.z) * 0.004f;
                        int b = verts.Count;
                        for (int i = 0; i < 4; i++)
                        {
                            verts.Add(new Vector3(a.X, a.Y, a.Z) + pos[i] + off);
                            uvs.Add(new Vector3(uvw[i].x, uvw[i].y, d.Frames[frame]));
                            cols.Add(white);
                        }
                        tris.Add(b); tris.Add(b + 1); tris.Add(b + 2);
                        tris.Add(b); tris.Add(b + 2); tris.Add(b + 3);
                    }
                }
            }
            mesh.Clear();
            if (verts.Count == 0) return;
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.SetColors(cols);
            mesh.SetTriangles(tris, 0, false);
            mesh.bounds = new Bounds(eye, Vector3.one * (maxDist * 2f + 4f));
            Graphics.DrawMesh(mesh, Matrix4x4.identity, Material, 0, cam);
        }
    }
}
