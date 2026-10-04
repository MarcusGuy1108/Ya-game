using System.Collections.Generic;
using UnityEngine;

namespace UKCity.World
{
    /// <summary>A block with animated faces found while meshing (traffic lights, message signs...).</summary>
    public struct AnimInstance
    {
        public int X, Y, Z;     // world coordinates
        public ushort State;
    }

    /// <summary>Mesh buffers built off the main thread and uploaded later.</summary>
    public sealed class MeshData
    {
        public readonly List<Vector3> Vertices = new List<Vector3>(8192);
        /// <summary>u, v and the texture-array slice.</summary>
        public readonly List<Vector3> Uvs = new List<Vector3>(8192);
        public readonly List<Color32> Colors = new List<Color32>(8192);
        public readonly List<int> Opaque = new List<int>(12288);
        public readonly List<int> Transparent = new List<int>(1024);
        public readonly List<AnimInstance> Animated = new List<AnimInstance>();

        public void Clear()
        {
            Vertices.Clear(); Uvs.Clear(); Colors.Clear(); Opaque.Clear(); Transparent.Clear(); Animated.Clear();
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
        ushort Get(int x, int y, int z);
    }

    /// <summary>A chunk plus a one-block border copied from its eight neighbours.</summary>
    public struct PaddedChunk : IVoxelSource
    {
        public const int P = WorldConst.ChunkSize + 2;
        public ushort[] Data;

        public ushort Get(int x, int y, int z)
        {
            if (y < 0) return BlockIds.Bedrock;
            if (y >= WorldConst.ChunkHeight) return BlockIds.Air;
            return Data[(x + 1) + (z + 1) * P + y * P * P];
        }

        /// <summary>neighbours[dx+1 + (dz+1)*3] are the 3x3 chunks around (and including) the centre.</summary>
        public static PaddedChunk Build(ushort[][] neighbours, out int maxY)
        {
            var d = new ushort[P * P * WorldConst.ChunkHeight];
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
                        ushort b = src[WorldConst.Index(lx, y, lz)];
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
        public ushort[] Data;
        public int SX, SY, SZ;

        public ushort Get(int x, int y, int z)
        {
            if (x < 0 || y < 0 || z < 0 || x >= SX || y >= SY || z >= SZ) return BlockIds.Air;
            return Data[x + z * SX + y * SX * SZ];
        }
    }

    /// <summary>
    /// Minecraft-style mesher: face-culled cubes with per-vertex ambient occlusion, plus box models (signs, signal
    /// heads, props), all honouring each block's facing. Lighting is baked into vertex colours so the shaders are
    /// unlit and work in any render pipeline.
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

        public static readonly float[] Shade = { 0.8f, 0.8f, 1.0f, 0.58f, 0.9f, 0.9f };
        private static readonly float[] AoLevels = { 0.52f, 0.7f, 0.86f, 1.0f };

        public static IReadOnlyList<Vector3Int> FaceNormals => Normals;
        public static Vector3[] FaceCorners(int face) => Corners[face];

        /// <summary>Meshes a box of voxels. (ox, oy, oz) is added to recorded animated-block positions.</summary>
        public static void Build<T>(ref T src, int sx, int sy, int sz, MeshData md, int ox = 0, int oy = 0, int oz = 0) where T : struct, IVoxelSource
        {
            md.Clear();
            var defs = Blocks.Defs;
            for (int y = 0; y < sy; y++)
                for (int z = 0; z < sz; z++)
                    for (int x = 0; x < sx; x++)
                    {
                        ushort state = src.Get(x, y, z);
                        if (state == 0) continue;
                        var def = defs[state & BlockState.IdMask];
                        if (def == null || !def.Visible) continue;
                        int facing = def.Rotatable ? BlockState.Facing(state) : 0;
                        if (def.Anim != AnimKind.None) md.Animated.Add(new AnimInstance { X = x + ox, Y = y + oy, Z = z + oz, State = state });

                        if (def.Shape == BlockShape.Model) { Model(ref src, x, y, z, def, facing, md); continue; }

                        for (int f = 0; f < 6; f++)
                        {
                            var n = Normals[f];
                            ushort nb = src.Get(x + n.x, y + n.y, z + n.z);
                            var nd = defs[nb & BlockState.IdMask];
                            if (nd != null && nd.Occludes) continue;
                            if ((nb & BlockState.IdMask) == def.Id && def.Layer != RenderLayer.Opaque) continue;
                            if (nd != null && nd.Visible && nd.Layer == RenderLayer.Transparent && def.Layer == RenderLayer.Transparent && nd.Shape == BlockShape.Cube) continue;
                            CubeFace(ref src, x, y, z, f, facing, def, md);
                        }
                    }
        }

        /// <summary>UV inside the tile for a point on a local face, oriented so textures read correctly from outside.</summary>
        public static Vector2 FaceUv(int face, Vector3 c)
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
            var d = Blocks.Defs[src.Get(x, y, z) & BlockState.IdMask];
            return d != null && d.CastsAO;
        }

        private static void CubeFace<T>(ref T src, int x, int y, int z, int f, int facing, BlockDef def, MeshData md) where T : struct, IVoxelSource
        {
            var n = Normals[f];
            var corners = Corners[f];
            int localFace = BlockState.InvRotFace(f, facing);
            int tile = def.Tiles[localFace];
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
                var uv = FaceUv(localFace, BlockState.InvRotatePoint(c, facing));
                md.Uvs.Add(new Vector3(uv.x, uv.y, tile));

                int px = x + n.x, py = y + n.y, pz = z + n.z;
                int d1 = c[ax1] > 0.5f ? 1 : -1;
                int d2 = c[ax2] > 0.5f ? 1 : -1;
                var o1 = Axis(ax1, d1);
                var o2 = Axis(ax2, d2);
                bool s1 = Ao(ref src, px + o1.x, py + o1.y, pz + o1.z);
                bool s2 = Ao(ref src, px + o2.x, py + o2.y, pz + o2.z);
                bool cr = Ao(ref src, px + o1.x + o2.x, py + o1.y + o2.y, pz + o1.z + o2.z);
                ao[i] = (s1 && s2) ? 0 : 3 - ((s1 ? 1 : 0) + (s2 ? 1 : 0) + (cr ? 1 : 0));
                byte bb = (byte)(Mathf.Clamp01(Shade[f] * AoLevels[ao[i]]) * 255f);
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

        /// <summary>Rotated world-space box (cell-local) for a model box.</summary>
        public static void RotatedBox(Box b, int facing, out Vector3 min, out Vector3 max)
        {
            var a = BlockState.RotatePoint(b.Min, facing);
            var c = BlockState.RotatePoint(b.Max, facing);
            min = Vector3.Min(a, c);
            max = Vector3.Max(a, c);
        }

        /// <summary>Corner positions (cell-local) and UVs for one world face of a model box.</summary>
        public static void BoxFace(Box b, int facing, int f, Vector3[] pos, Vector3[] uvw, out int tile)
        {
            RotatedBox(b, facing, out var min, out var max);
            int lf = BlockState.InvRotFace(f, facing);
            tile = b.Tiles[lf];
            var size = b.Max - b.Min;
            var corners = Corners[f];
            for (int i = 0; i < 4; i++)
            {
                var c = corners[i];
                var p = new Vector3(Mathf.Lerp(min.x, max.x, c.x), Mathf.Lerp(min.y, max.y, c.y), Mathf.Lerp(min.z, max.z, c.z));
                pos[i] = p;
                var local = BlockState.InvRotatePoint(p, facing);
                if (b.FullUv)
                    local = new Vector3(
                        size.x > 0 ? (local.x - b.Min.x) / size.x : 0,
                        size.y > 0 ? (local.y - b.Min.y) / size.y : 0,
                        size.z > 0 ? (local.z - b.Min.z) / size.z : 0);
                var uv = FaceUv(lf, local);
                uvw[i] = new Vector3(uv.x, uv.y, tile);
            }
        }

        [System.ThreadStatic] private static Vector3[] tmpPos, tmpUv;

        private static void Model<T>(ref T src, int x, int y, int z, BlockDef def, int facing, MeshData md) where T : struct, IVoxelSource
        {
            tmpPos ??= new Vector3[4];
            tmpUv ??= new Vector3[4];
            var defs = Blocks.Defs;
            var tris = def.Layer == RenderLayer.Transparent ? md.Transparent : md.Opaque;
            foreach (var b in def.Model)
            {
                RotatedBox(b, facing, out var min, out var max);
                for (int f = 0; f < 6; f++)
                {
                    int lf = BlockState.InvRotFace(f, facing);
                    if (b.Tiles[lf] < 0) continue;
                    // Faces flush with the cell boundary are hidden by solid neighbours.
                    var n = Normals[f];
                    bool onBoundary =
                        (f == 0 && max.x >= 0.999f) || (f == 1 && min.x <= 0.001f) ||
                        (f == 2 && max.y >= 0.999f) || (f == 3 && min.y <= 0.001f) ||
                        (f == 4 && max.z >= 0.999f) || (f == 5 && min.z <= 0.001f);
                    if (onBoundary)
                    {
                        var nd = defs[src.Get(x + n.x, y + n.y, z + n.z) & BlockState.IdMask];
                        if (nd != null && nd.Occludes) continue;
                    }
                    BoxFace(b, facing, f, tmpPos, tmpUv, out _);
                    byte bb = (byte)(Shade[f] * 255f);
                    int baseIndex = md.Vertices.Count;
                    for (int i = 0; i < 4; i++)
                    {
                        md.Vertices.Add(new Vector3(x + tmpPos[i].x, y + tmpPos[i].y, z + tmpPos[i].z));
                        md.Uvs.Add(tmpUv[i]);
                        md.Colors.Add(new Color32(bb, bb, bb, 255));
                    }
                    tris.Add(baseIndex + 0); tris.Add(baseIndex + 1); tris.Add(baseIndex + 2);
                    tris.Add(baseIndex + 0); tris.Add(baseIndex + 2); tris.Add(baseIndex + 3);
                }
            }
        }
    }
}
