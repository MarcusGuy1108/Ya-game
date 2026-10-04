using UnityEngine;

namespace UKCity.World
{
    /// <summary>
    /// A voxel is a ushort: the low 12 bits are the block id (up to 4096 types) and bits 12-13 the facing.
    /// Facing follows building templates: 0 = front faces south (-Z), 1 = west (-X), 2 = north (+Z), 3 = east (+X),
    /// i.e. each step rotates 90 degrees clockwise seen from above.
    /// </summary>
    public static class BlockState
    {
        public const int IdMask = 0x0FFF;
        public const int FacingShift = 12;

        public static int Id(ushort v) => v & IdMask;
        public static int Facing(ushort v) => (v >> FacingShift) & 3;
        public static ushort Make(int id, int facing = 0) => (ushort)((id & IdMask) | ((facing & 3) << FacingShift));

        /// <summary>Rotates a stored block by rot quarter turns (used when stamping rotated templates).</summary>
        public static ushort Rotate(ushort v, int rot)
        {
            rot &= 3;
            if (rot == 0) return v;
            var def = Blocks.Get(v);
            if (def.Rotatable) return Make(def.Id, Facing(v) + rot);
            if ((rot & 1) == 1 && def.Rotated90 != def.Id) return Make(def.Rotated90, Facing(v));
            return v;
        }

        /// <summary>World direction the front of a block faces.</summary>
        public static Vector2Int FrontDirection(int facing)
        {
            switch (facing & 3)
            {
                case 1: return new Vector2Int(-1, 0);
                case 2: return new Vector2Int(0, 1);
                case 3: return new Vector2Int(1, 0);
                default: return new Vector2Int(0, -1);
            }
        }

        /// <summary>Facing whose front points closest to the given XZ direction.</summary>
        public static int FacingToward(Vector2 dir)
        {
            if (Mathf.Abs(dir.x) > Mathf.Abs(dir.y)) return dir.x > 0 ? 3 : 1;
            return dir.y > 0 ? 2 : 0;
        }

        // Face indices used by the mesher: 0 +X, 1 -X, 2 +Y, 3 -Y, 4 +Z, 5 -Z.
        private static readonly int[,] rotFace = BuildRotFace();

        private static int[,] BuildRotFace()
        {
            var t = new int[4, 6];
            int[] one = { 5, 4, 2, 3, 0, 1 }; // +X->-Z, -X->+Z, +Z->+X, -Z->-X
            for (int f = 0; f < 6; f++) t[0, f] = f;
            for (int r = 1; r < 4; r++)
                for (int f = 0; f < 6; f++) t[r, f] = one[t[r - 1, f]];
            return t;
        }

        /// <summary>Which world face a local face ends up on after rotating by r.</summary>
        public static int RotFace(int face, int r) => rotFace[r & 3, face];
        /// <summary>Which local face is shown on a world face for a block rotated by r.</summary>
        public static int InvRotFace(int face, int r) => rotFace[(4 - (r & 3)) & 3, face];

        /// <summary>Rotates a point inside the unit cell about the cell centre.</summary>
        public static Vector3 RotatePoint(Vector3 p, int r)
        {
            for (int i = 0; i < (r & 3); i++) p = new Vector3(p.z, p.y, 1f - p.x);
            return p;
        }

        public static Vector3 InvRotatePoint(Vector3 p, int r) => RotatePoint(p, (4 - (r & 3)) & 3);
    }
}
