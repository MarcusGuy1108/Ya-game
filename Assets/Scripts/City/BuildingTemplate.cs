using System;
using UnityEngine;
using UKCity.World;

namespace UKCity.City
{
    /// <summary>
    /// A block structure that can be stamped into the world, from either the 2D planner or first person.
    /// Local space: x = width, y = up (y 0 is the ground layer), z = depth with the FRONT at z = 0.
    /// </summary>
    public sealed class BuildingTemplate
    {
        /// <summary>Template cells with this value leave the world untouched.</summary>
        public const ushort Keep = 0xFFFF;

        public string Id;
        public string Name;
        public string Category;
        public string Description = "";
        public bool IsCustom;
        /// <summary>Sub-group shown in menus (e.g. the sign category).</summary>
        public string Group = "";
        /// <summary>Texture tile used as the menu icon (-1 = none).</summary>
        public int IconTile = -1;
        /// <summary>Placed by the planner's sign tool: faces oncoming traffic on the nearest road.</summary>
        public bool IsSign;
        /// <summary>Spans a motorway carriageway (gantries); the anchor sits by the central reservation.</summary>
        public bool SpansCarriageway;
        /// <summary>Swap red doors for a colour picked from the placement position, so terraces look varied.</summary>
        public bool VaryDoors;

        public int SizeX, SizeY, SizeZ;
        /// <summary>The cell (on the ground layer) that sits under the cursor when placing.</summary>
        public int AnchorX, AnchorZ;
        public ushort[] Blocks;

        private readonly RotatedVoxels[] rotatedCache = new RotatedVoxels[4];

        public ushort Get(int x, int y, int z) => Blocks[x + z * SizeX + y * SizeX * SizeZ];

        /// <summary>Rotates an offset clockwise (seen from above) by rot * 90 degrees.</summary>
        public static Vector2Int Rotate(int dx, int dz, int rot)
        {
            switch (rot & 3)
            {
                case 1: return new Vector2Int(dz, -dx);
                case 2: return new Vector2Int(-dx, -dz);
                case 3: return new Vector2Int(-dz, dx);
                default: return new Vector2Int(dx, dz);
            }
        }

        /// <summary>World direction the front of the building faces for a rotation.</summary>
        public static Vector2Int FrontDirection(int rot) => Rotate(0, -1, rot);

        public Vector2Int LocalToWorldXZ(int lx, int lz, Vector3Int origin, int rot)
        {
            var r = Rotate(lx - AnchorX, lz - AnchorZ, rot);
            return new Vector2Int(origin.x + r.x, origin.z + r.y);
        }

        /// <summary>World-space XZ footprint (inclusive min, exclusive max as RectInt).</summary>
        public RectInt Footprint(Vector3Int origin, int rot)
        {
            var a = LocalToWorldXZ(0, 0, origin, rot);
            var b = LocalToWorldXZ(SizeX - 1, SizeZ - 1, origin, rot);
            int minX = Math.Min(a.x, b.x), minZ = Math.Min(a.y, b.y);
            int maxX = Math.Max(a.x, b.x), maxZ = Math.Max(a.y, b.y);
            return new RectInt(minX, minZ, maxX - minX + 1, maxZ - minZ + 1);
        }

        private static readonly ushort[] doorBottoms = { BlockIds.DoorRed, BlockIds.DoorBlue, BlockIds.DoorGreen, BlockIds.DoorBlack };
        private static readonly ushort[] doorTops = { BlockIds.DoorRedTop, BlockIds.DoorBlueTop, BlockIds.DoorGreenTop, BlockIds.DoorBlackTop };

        /// <summary>Block to write into the world for a template cell, after rotation and door variation.</summary>
        public ushort WorldBlock(ushort b, Vector3Int origin, int rot)
        {
            if (b == Keep) return Keep;
            if (VaryDoors && (b == BlockIds.DoorRed || b == BlockIds.DoorRedTop))
            {
                int pick = (int)(WorldConst.Hash(origin.x, origin.z, 77) % 4);
                b = b == BlockIds.DoorRed ? doorBottoms[pick] : doorTops[pick];
            }
            return BlockState.Rotate(b, rot);
        }

        /// <summary>The template rotated into a world-aligned box, for preview meshes.</summary>
        public RotatedVoxels GetRotated(int rot)
        {
            rot &= 3;
            if (rotatedCache[rot] != null) return rotatedCache[rot];
            var fp = Footprint(Vector3Int.zero, rot);
            var rv = new RotatedVoxels
            {
                SizeX = fp.width, SizeY = SizeY, SizeZ = fp.height,
                OffsetX = fp.xMin, OffsetZ = fp.yMin,
                Blocks = new ushort[fp.width * SizeY * fp.height]
            };
            for (int y = 0; y < SizeY; y++)
                for (int z = 0; z < SizeZ; z++)
                    for (int x = 0; x < SizeX; x++)
                    {
                        ushort b = Get(x, y, z);
                        if (b == Keep) continue;
                        b = BlockState.Rotate(b, rot);
                        var w = LocalToWorldXZ(x, z, Vector3Int.zero, rot);
                        int rx = w.x - fp.xMin, rz = w.y - fp.yMin;
                        rv.Blocks[rx + rz * rv.SizeX + y * rv.SizeX * rv.SizeZ] = b;
                    }
            rotatedCache[rot] = rv;
            return rv;
        }
    }

    public sealed class RotatedVoxels
    {
        public int SizeX, SizeY, SizeZ;
        /// <summary>Offset of the box minimum relative to the placement origin.</summary>
        public int OffsetX, OffsetZ;
        public ushort[] Blocks;
        public ushort Get(int x, int y, int z) => Blocks[x + z * SizeX + y * SizeX * SizeZ];
    }

    /// <summary>Small helper for authoring templates in code.</summary>
    public sealed class TemplateBuilder
    {
        public readonly int SX, SY, SZ;
        public readonly ushort[] B;

        public TemplateBuilder(int sx, int sy, int sz)
        {
            SX = sx; SY = sy; SZ = sz;
            B = new ushort[sx * sy * sz];
            for (int i = 0; i < B.Length; i++) B[i] = BuildingTemplate.Keep;
        }

        public void Set(int x, int y, int z, ushort b)
        {
            if (x < 0 || y < 0 || z < 0 || x >= SX || y >= SY || z >= SZ) return;
            B[x + z * SX + y * SX * SZ] = b;
        }

        public ushort Get(int x, int y, int z) => B[x + z * SX + y * SX * SZ];

        public void Fill(int x0, int y0, int z0, int x1, int y1, int z1, ushort b)
        {
            if (x0 > x1) (x0, x1) = (x1, x0);
            if (y0 > y1) (y0, y1) = (y1, y0);
            if (z0 > z1) (z0, z1) = (z1, z0);
            for (int y = y0; y <= y1; y++)
                for (int z = z0; z <= z1; z++)
                    for (int x = x0; x <= x1; x++)
                        Set(x, y, z, b);
        }

        /// <summary>Fills only the vertical walls (perimeter) of a box.</summary>
        public void Walls(int x0, int y0, int z0, int x1, int y1, int z1, ushort b)
        {
            Fill(x0, y0, z0, x1, y1, z0, b);
            Fill(x0, y0, z1, x1, y1, z1, b);
            Fill(x0, y0, z0, x0, y1, z1, b);
            Fill(x1, y0, z0, x1, y1, z1, b);
        }

        /// <summary>Replaces cells of one block type inside a box.</summary>
        public void Replace(int x0, int y0, int z0, int x1, int y1, int z1, ushort from, ushort to)
        {
            for (int y = y0; y <= y1; y++)
                for (int z = z0; z <= z1; z++)
                    for (int x = x0; x <= x1; x++)
                        if (x >= 0 && y >= 0 && z >= 0 && x < SX && y < SY && z < SZ && Get(x, y, z) == from) Set(x, y, z, to);
        }

        /// <summary>
        /// Stepped pitched roof with the ridge running along X, covering z0..z1, starting at baseY.
        /// Fills the gable ends at x0 and x1 with gableBlock.
        /// </summary>
        public void GableRoofX(int x0, int x1, int z0, int z1, int baseY, ushort roof, ushort gableBlock, int overhang = 0)
        {
            for (int z = z0; z <= z1; z++)
            {
                int step = Math.Min(z - z0, z1 - z);
                int y = baseY + step;
                Fill(x0 - overhang, y, z, x1 + overhang, y, z, roof);
                // Clear anything under the roof inside the building, and fill gables.
                for (int yy = baseY; yy < y; yy++)
                {
                    Fill(x0 + 1, yy, z, x1 - 1, yy, z, BlockIds.Air);
                    Set(x0, yy, z, gableBlock);
                    Set(x1, yy, z, gableBlock);
                }
            }
        }

        /// <summary>A two-block front door (bottom + top half).</summary>
        public void Door(int x, int y, int z, ushort bottom)
        {
            Set(x, y, z, bottom);
            Set(x, y + 1, z, (ushort)(bottom - BlockIds.DoorRed + BlockIds.DoorRedTop));
        }

        public BuildingTemplate Build(string id, string name, string category, int anchorX, int anchorZ, string desc = "", bool varyDoors = false)
        {
            return new BuildingTemplate
            {
                Id = id, Name = name, Category = category, Description = desc,
                SizeX = SX, SizeY = SY, SizeZ = SZ, AnchorX = anchorX, AnchorZ = anchorZ,
                Blocks = (ushort[])B.Clone(), VaryDoors = varyDoors
            };
        }
    }
}
