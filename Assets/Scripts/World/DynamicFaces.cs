using System.Collections.Generic;
using UnityEngine;
using UKCity.City;

namespace UKCity.World
{
    /// <summary>Quads produced for one frame of animated faces.</summary>
    public sealed class FaceBuffers
    {
        public readonly List<Vector3> Verts = new List<Vector3>();
        public readonly List<Vector3> Uvs = new List<Vector3>();
        public readonly List<Color32> Cols = new List<Color32>();
        public readonly List<int> Tris = new List<int>();

        public void Clear() { Verts.Clear(); Uvs.Clear(); Cols.Clear(); Tris.Clear(); }

        public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 ua, Vector3 ub, Vector3 uc, Vector3 ud)
        {
            int i = Verts.Count;
            Verts.Add(a); Verts.Add(b); Verts.Add(c); Verts.Add(d);
            Uvs.Add(ua); Uvs.Add(ub); Uvs.Add(uc); Uvs.Add(ud);
            var w = new Color32(255, 255, 255, 255);
            Cols.Add(w); Cols.Add(w); Cols.Add(w); Cols.Add(w);
            Tris.Add(i); Tris.Add(i + 1); Tris.Add(i + 2);
            Tris.Add(i); Tris.Add(i + 2); Tris.Add(i + 3);
        }
    }

    /// <summary>
    /// Draws the faces that change over time as small overlay meshes rebuilt each frame, so chunks never need
    /// remeshing for animation:
    ///  - signal lamps (each lens lit on its own, with a soft glow): vehicle aspects, arrows, filter arrows,
    ///    no-turn pods, red/green man, WAIT, Belisha beacons, school wig-wags,
    ///  - frame-swapped faces: motorway message signs, lane signals, vehicle activated signs.
    /// </summary>
    public sealed class DynamicFaces
    {
        private readonly Dictionary<ChunkCoord, AnimInstance[]> chunks = new Dictionary<ChunkCoord, AnimInstance[]>();
        private readonly FaceBuffers lamps = new FaceBuffers();
        private readonly FaceBuffers glows = new FaceBuffers();
        private readonly Vector3[] pos = new Vector3[4];
        private readonly Vector3[] uvw = new Vector3[4];
        private readonly Mesh lampMesh, glowMesh;

        public TrafficSignals Signals;
        public Material Material;
        public Material GlowMaterial;
        public float Time;

        public DynamicFaces()
        {
            lampMesh = new Mesh { name = "Lit lamps", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            lampMesh.MarkDynamic();
            glowMesh = new Mesh { name = "Lamp glow", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            glowMesh.MarkDynamic();
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

        private static float Mod(float a, float m) => a - m * Mathf.Floor(a / m);

        // ------------------------------------------------------------------ what is lit

        /// <summary>Whether a signal lamp is lit right now.</summary>
        public bool Lit(LampRole role, in TrafficSignals.HeadState st, AnimInstance a)
        {
            float t = Time;
            switch (role)
            {
                case LampRole.Red: return st.Vehicle == Aspect.Red || st.Vehicle == Aspect.RedAmber;
                case LampRole.Amber: return st.Vehicle == Aspect.Amber || st.Vehicle == Aspect.RedAmber;
                case LampRole.Green: return st.Vehicle == Aspect.Green;
                // Filter arrows run while the main aspect is red but another stage is moving.
                case LampRole.FilterLeft:
                case LampRole.FilterRight: return st.Vehicle == Aspect.Red && st.OtherStageGreen;
                // The turn is banned while this approach has right of way.
                case LampRole.NoRightTurn:
                case LampRole.NoLeftTurn: return st.Vehicle == Aspect.Green || st.Vehicle == Aspect.Amber;
                case LampRole.RedMan: return !st.PedGreen;
                case LampRole.GreenMan: return st.PedGreen;
                // WAIT lights once someone has "pressed the button" (the last part of the wait).
                case LampRole.Wait: return !st.PedGreen && st.UntilPed < 20f;
                case LampRole.Beacon: return Mod(t + (a.X + a.Z) * 0.01f, 1.1f) < 0.55f;
                case LampRole.WigLeft: return Mod(t, 1.2f) < 0.6f;
                case LampRole.WigRight: return Mod(t, 1.2f) >= 0.6f;
            }
            return false;
        }

        /// <summary>Animation frame for frame-swapped faces right now, or -1 for none.</summary>
        public int Frame(BlockDef d, AnimInstance a)
        {
            float t = Time;
            switch (d.Anim)
            {
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

        // ------------------------------------------------------------------ geometry

        /// <summary>Adds the overlay quads for one animated block (lit lenses into lampOut, halos into glowOut).</summary>
        public void Emit(AnimInstance a, FaceBuffers lampOut, FaceBuffers glowOut, Vector3 eye)
        {
            var d = Blocks.Get(a.State);
            if (d.Anim == AnimKind.None || d.Model == null) return;
            int facing = d.Rotatable ? BlockState.Facing(a.State) : 0;
            var origin = new Vector3(a.X, a.Y, a.Z);

            if (d.Anim == AnimKind.Lamps)
            {
                var st = Signals != null
                    ? Signals.State(a.X, a.Y, a.Z, facing, d.PedestrianLamps)
                    : new TrafficSignals.HeadState { Vehicle = TrafficSignals.JunctionAspect(facing & 1, 2, Time), PedGreen = TrafficSignals.PedPhase(2, Time) };
                foreach (var lamp in d.Lamps)
                {
                    if (!Lit(lamp.Role, st, a)) continue;
                    int f = BlockState.RotFace(lamp.Face, facing);
                    ChunkMesher.BoxFace(d.Model[lamp.Box], facing, f, pos, uvw, out _);
                    var n = ChunkMesher.FaceNormals[f];
                    var nv = new Vector3(n.x, n.y, n.z);
                    var off = origin + nv * 0.004f;
                    var t = new Vector3(0, 0, lamp.LitTile);
                    lampOut.Quad(pos[0] + off, pos[1] + off, pos[2] + off, pos[3] + off,
                        new Vector3(uvw[0].x, uvw[0].y, t.z), new Vector3(uvw[1].x, uvw[1].y, t.z),
                        new Vector3(uvw[2].x, uvw[2].y, t.z), new Vector3(uvw[3].x, uvw[3].y, t.z));
                    if (lamp.GlowTile >= 0 && glowOut != null) Glow(pos, origin + nv * 0.01f, lamp, glowOut);
                }
                return;
            }

            int frame = Frame(d, a);
            if (frame < 0 || frame >= d.Frames.Length) return;
            var box = d.Model[d.AnimBox];
            foreach (int lf in d.AnimFaces)
            {
                int f = BlockState.RotFace(lf, facing);
                ChunkMesher.BoxFace(box, facing, f, pos, uvw, out _);
                var n = ChunkMesher.FaceNormals[f];
                var off = origin + new Vector3(n.x, n.y, n.z) * 0.004f;
                float tile = d.Frames[frame];
                lampOut.Quad(pos[0] + off, pos[1] + off, pos[2] + off, pos[3] + off,
                    new Vector3(uvw[0].x, uvw[0].y, tile), new Vector3(uvw[1].x, uvw[1].y, tile),
                    new Vector3(uvw[2].x, uvw[2].y, tile), new Vector3(uvw[3].x, uvw[3].y, tile));
            }
        }

        /// <summary>A halo quad centred on the lens, scaled up, in the same plane.</summary>
        private static void Glow(Vector3[] p, Vector3 off, Lamp lamp, FaceBuffers outBuf)
        {
            var centre = (p[0] + p[1] + p[2] + p[3]) * 0.25f;
            float k = lamp.GlowScale;
            Vector3 S(Vector3 v) => centre + (v - centre) * k + off;
            float tile = lamp.GlowTile;
            outBuf.Quad(S(p[0]), S(p[1]), S(p[2]), S(p[3]),
                new Vector3(0, 0, tile), new Vector3(0, 1, tile), new Vector3(1, 1, tile), new Vector3(1, 0, tile));
        }

        public void Draw(Camera cam, Vector3 eye, float maxDist)
        {
            if (Material == null || chunks.Count == 0) return;
            lamps.Clear();
            glows.Clear();
            float max2 = maxDist * maxDist;
            foreach (var arr in chunks.Values)
                foreach (var a in arr)
                {
                    float dx = a.X + 0.5f - eye.x, dy = a.Y + 0.5f - eye.y, dz = a.Z + 0.5f - eye.z;
                    if (dx * dx + dy * dy + dz * dz > max2) continue;
                    Emit(a, lamps, GlowMaterial != null ? glows : null, eye);
                }
            Upload(lampMesh, lamps, eye, maxDist);
            if (lamps.Verts.Count > 0) Graphics.DrawMesh(lampMesh, Matrix4x4.identity, Material, 0, cam);
            if (GlowMaterial != null)
            {
                Upload(glowMesh, glows, eye, maxDist);
                if (glows.Verts.Count > 0) Graphics.DrawMesh(glowMesh, Matrix4x4.identity, GlowMaterial, 0, cam);
            }
        }

        private static void Upload(Mesh m, FaceBuffers b, Vector3 eye, float maxDist)
        {
            m.Clear();
            if (b.Verts.Count == 0) return;
            m.SetVertices(b.Verts);
            m.SetUVs(0, b.Uvs);
            m.SetColors(b.Cols);
            m.SetTriangles(b.Tris, 0, false);
            m.bounds = new Bounds(eye, Vector3.one * (maxDist * 2f + 4f));
        }
    }
}
