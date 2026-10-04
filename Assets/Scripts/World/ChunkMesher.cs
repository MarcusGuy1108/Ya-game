using System.Collections.Generic;
using UnityEngine;

namespace UKCity.World
{
    /// <summary>Mesh buffers built off the main thread and uploaded later.</summary>
    public sealed class MeshData
    {
        public readonly List<Vector3> Vertices = new List<Vector3>(8192);
        public readonly List<Vector2> Uvs = new List<Vector2>(8192);
        public readonly List<Color32> Colors = new List<Color32>(8192);
        public readonly List<int> Opaque = new List<int>(12288);
        public readonly List<int> Transparent = new List<int>(1024);

        public void Clear()
        {
            Vertices.Clear(); Uvs.Clear(); Colors.Clear(); Opaque.Clear(); Transparent.Clear();
        }

        public void ApplyTo(Mesh mesh)
        {
            mesh.Clear();
            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.SetVertices(Vertices);
            mesh.SetUVs(0, Uvs);
            mesh.SetColors(Colors);
            mesh.subMeshCount = 2;
            mesh.SetTriangles(Opaque, 0, false);
            mesh.SetTriangles(Transparent, 1, false);
            mesh.RecalculateBounds();
        }
    }

    public interface IVoxelSource
    {
        /// <summary>Block at local coordinates; may be asked for one cell outside the meshed box.</summary>
        byte Get(int x, int y, int z);
    }

    /// <summary>A chunk plus a one-block border copied from its eight neighbours.</summary>
    public struct PaddedChunk : IVoxelSource
    {
        public const int P = WorldConst.ChunkSize + 2;
        public byte[] Data;

        public byte Get(int x, int y, int z)
        {
            if (y < 0) return BlockIds.Bedrock;
            if (y >= WorldConst.ChunkHeight) return BlockIds.Air;
            return Data[(x + 1) + (z + 1) * P + y * P * P];
        }

        /// <summary>neighbours[dx+1 + (dz+1)*3] are the 3x3 chunks around (and including) the centre.</summary>
        public static PaddedChunk Build(byte[][] neighbours, out int maxY)
        {
            var d = new byte[P * P * WorldConst.ChunkHeight];
            maxY = 0;
            for (int pz = -1; pz <= 16; pz++)
            {
                int dz = pz < 0 ? -1 : pz > 15 ? 1 : 0;
                int lz = pz - dz * 16;
                for (int px = -1; px <= 16; px++)
                {
                    int dx = px < 0 ? -1 : px > 15 ? 1 : 0;
                    int lx = px - dx * 16;
                    var src = neighbours[(dx + 1) + (dz + 1) * 3];
                    if (src == null) continue;
                    int dst = (px + 1) + (pz + 1) * P;
                    bool centre = dx == 0 && dz == 0;
                    for (int y = 0; y < WorldConst.ChunkHeight; y++)
                    {
                        byte b = src[WorldConst.Index(lx, y, lz)];
                        d[dst + y * P * P] = b;
                        if (centre && b != 0 && y > maxY) maxY = y;
                    }
                }
            }
            return new PaddedChunk { Data = d };
        }
    }

    /// <summary>A rotated building template, used for ghost previews.</summary>
    public struct BoxSource : IVoxelSource
    {
        public byte[] Data;
        public int SX, SY, SZ;

        public byte Get(int x, int y, int z)
        {
            if (x < 0 || y < 0 || z < 0 || x >= SX || y >= SY || z >= SZ) return BlockIds.Air;
            return Data[x + z * SX + y * SX * SZ];
        }
    }

    /// <summary>
    /// Minecraft-style face-culled mesher with per-vertex ambient occlusion and directional face shading.
    /// Lighting is baked into vertex colours, so the shaders are unlit and work in any render pipeline.
    /// </summary>
    public static class ChunkMesher
    {
        // Face order: +X, -X, +Y, -Y, +Z, -Z
        private static readonly Vector3Int[] Normals =
        {
            new Vector3Int(1, 0, 0), new Vector3Int(-1, 0, 0), new Vector3Int(0, 1, 0),
            new Vector3Int(0, -1, 0), new Vector3Int(0, 0, 1), new Vector3Int(0, 0, -1)
        };

        // Corner positions, wound so Cross(v1-v0, v2-v0) points outward (Unity front face).
        private static readonly Vector3[][] Corners =
        {
            new[] { new Vector3(1, 0, 0), new Vector3(1, 1, 0), new Vector3(1, 1, 1), new Vector3(1, 0, 1) },
            new[] { new Vector3(0, 0, 1), new Vector3(0, 1, 1), new Vector3(0, 1, 0), new Vector3(0, 0, 0) },
            new[] { new Vector3(0, 1, 0), new Vector3(0, 1, 1), new Vector3(1, 1, 1), new Vector3(1, 1, 0) },
            new[] { new Vector3(0, 0, 1), new Vector3(0, 0, 0), new Vector3(1, 0, 0), new Vector3(1, 0, 1) },
            new[] { new Vector3(1, 0, 1), new Vector3(1, 1, 1), new Vector3(0, 1, 1), new Vector3(0, 0, 1) },
            new[] { new Vector3(0, 0, 0), new Vector3(0, 1, 0), new Vector3(1, 1, 0), new Vector3(1, 0, 0) },
        };

        private static readonly float[] Shade = { 0.78f, 0.78f, 1.0f, 0.55f, 0.9f, 0.9f };
        private static readonly float[] AoLevels = { 0.5f, 0.68f, 0.84f, 1.0f };

        public static IReadOnlyList<Vector3Int> FaceNormals => Normals;
        public static Vector3[] FaceCorners(int face) => Corners[face];

        public static void Build<T>(ref T src, int sx, int sy, int sz, MeshData md) where T : struct, IVoxelSource
        {
            md.Clear();
            var defs = Blocks.Defs;
            for (int y = 0; y < sy; y++)
                for (int z = 0; z < sz; z++)
                    for (int x = 0; x < sx; x++)
                    {
                        byte id = src.Get(x, y, z);
                        if (id == 0) continue;
                        var def = defs[id];
                        if (def == null || !def.Visible) continue;
                        if (def.Shape == BlockShape.Pole) { Pole(ref src, x, y, z, def, md); continue; }

                        for (int f = 0; f < 6; f++)
                        {
                            var n = Normals[f];
                            byte nb = src.Get(x + n.x, y + n.y, z + n.z);
                            var nd = defs[nb];
                            if (nd != null && nd.Occludes) continue;
                            if (nb == id && def.Layer != RenderLayer.Opaque) continue;
                            if (nd != null && nd.Layer == RenderLayer.Transparent && def.Layer == RenderLayer.Transparent && nd.Visible) continue;
                            Face(ref src, x, y, z, f, def, md);
                        }
                    }
        }

        private static int TileFor(BlockDef def, int face) => face == 2 ? def.TileTop : face == 3 ? def.TileBottom : def.TileSide;

        /// <summary>UV inside the tile for a point on a face, oriented so textures read left-to-right from outside.</summary>
        private static Vector2 FaceUv(int face, Vector3 c)
        {
            switch (face)
            {
                case 0: return new Vector2(c.z, c.y);
                case 1: return new Vector2(1 - c.z, c.y);
                case 4: return new Vector2(1 - c.x, c.y);
                case 5: return new Vector2(c.x, c.y);
                default: return new Vector2(c.x, c.z);
            }
        }

        private static bool Ao<T>(ref T src, int x, int y, int z) where T : struct, IVoxelSource
        {
            var d = Blocks.Defs[src.Get(x, y, z)];
            return d != null && d.CastsAO;
        }

        private static void Face<T>(ref T src, int x, int y, int z, int f, BlockDef def, MeshData md) where T : struct, IVoxelSource
        {
            var n = Normals[f];
            var corners = Corners[f];
            int tile = TileFor(def, f);
            int baseIndex = md.Vertices.Count;
            System.Span<int> ao = stackalloc int[4];

            // The two in-plane axes for this face (0 = x, 1 = y, 2 = z).
            int ax1, ax2;
            if (n.y != 0) { ax1 = 0; ax2 = 2; }
            else if (n.x != 0) { ax1 = 1; ax2 = 2; }
            else { ax1 = 0; ax2 = 1; }

            for (int i = 0; i < 4; i++)
            {
                var c = corners[i];
                md.Vertices.Add(new Vector3(x + c.x, y + c.y, z + c.z));
                var uv = FaceUv(f, c);
                md.Uvs.Add(TextureAtlas.Uv(tile, uv.x, uv.y));

                int px = x + n.x, py = y + n.y, pz = z + n.z;
                int d1 = c[ax1] > 0.5f ? 1 : -1;
                int d2 = c[ax2] > 0.5f ? 1 : -1;
                var o1 = Axis(ax1, d1);
                var o2 = Axis(ax2, d2);
                bool s1 = Ao(ref src, px + o1.x, py + o1.y, pz + o1.z);
                bool s2 = Ao(ref src, px + o2.x, py + o2.y, pz + o2.z);
                bool cr = Ao(ref src, px + o1.x + o2.x, py + o1.y + o2.y, pz + o1.z + o2.z);
                ao[i] = (s1 && s2) ? 0 : 3 - ((s1 ? 1 : 0) + (s2 ? 1 : 0) + (cr ? 1 : 0));
                float b = Shade[f] * AoLevels[ao[i]];
                byte bb = (byte)(Mathf.Clamp01(b) * 255f);
                md.Colors.Add(new Color32(bb, bb, bb, 255));
            }

            var tris = def.Layer == RenderLayer.Transparent ? md.Transparent : md.Opaque;
            if (ao[0] + ao[2] < ao[1] + ao[3])
            {
                tris.Add(baseIndex + 1); tris.Add(baseIndex + 2); tris.Add(baseIndex + 3);
                tris.Add(baseIndex + 1); tris.Add(baseIndex + 3); tris.Add(baseIndex + 0);
            }
            else
            {
                tris.Add(baseIndex + 0); tris.Add(baseIndex + 1); tris.Add(baseIndex + 2);
                tris.Add(baseIndex + 0); tris.Add(baseIndex + 2); tris.Add(baseIndex + 3);
            }
        }

        private static Vector3Int Axis(int axis, int sign)
        {
            switch (axis)
            {
                case 0: return new Vector3Int(sign, 0, 0);
                case 1: return new Vector3Int(0, sign, 0);
                default: return new Vector3Int(0, 0, sign);
            }
        }

        /// <summary>Thin post: a narrow box. Sides are never culled; ends are culled against solid blocks.</summary>
        private static void Pole<T>(ref T src, int x, int y, int z, BlockDef def, MeshData md) where T : struct, IVoxelSource
        {
            var min = new Vector3(def.MinX, def.MinY, def.MinZ);
            var max = new Vector3(def.MaxX, def.MaxY, def.MaxZ);
            for (int f = 0; f < 6; f++)
            {
                var n = Normals[f];
                if (n.y != 0)
                {
                    byte nb = src.Get(x, y + n.y, z);
                    var nd = Blocks.Defs[nb];
                    if (nb == def.Id || (nd != null && nd.Occludes)) continue;
                }
                int tile = TileFor(def, f);
                int baseIndex = md.Vertices.Count;
                var corners = Corners[f];
                byte bb = (byte)(Shade[f] * 255f);
                for (int i = 0; i < 4; i++)
                {
                    var c = corners[i];
                    var p = new Vector3(Mathf.Lerp(min.x, max.x, c.x), Mathf.Lerp(min.y, max.y, c.y), Mathf.Lerp(min.z, max.z, c.z));
                    md.Vertices.Add(new Vector3(x + p.x, y + p.y, z + p.z));
                    var uv = FaceUv(f, p);
                    md.Uvs.Add(TextureAtlas.Uv(tile, uv.x, uv.y));
                    md.Colors.Add(new Color32(bb, bb, bb, 255));
                }
                var tris = md.Opaque;
                tris.Add(baseIndex + 0); tris.Add(baseIndex + 1); tris.Add(baseIndex + 2);
                tris.Add(baseIndex + 0); tris.Add(baseIndex + 2); tris.Add(baseIndex + 3);
            }
        }
    }
}
