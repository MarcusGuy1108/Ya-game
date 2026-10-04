using UnityEngine;

namespace UKCity.World
{
    public struct VoxelHit
    {
        public Vector3Int Pos;
        public Vector3Int Normal;
        public byte Id;
        public float Distance;
        public Vector3Int Adjacent => Pos + Normal;
    }

    public static class VoxelRaycast
    {
        /// <summary>Amanatides &amp; Woo grid traversal. Stops at the first selectable block.</summary>
        public static bool Cast(VoxelWorld world, Vector3 origin, Vector3 dir, float maxDist, out VoxelHit hit)
        {
            hit = default;
            if (dir.sqrMagnitude < 1e-8f) return false;
            dir.Normalize();

            int x = Mathf.FloorToInt(origin.x), y = Mathf.FloorToInt(origin.y), z = Mathf.FloorToInt(origin.z);
            int sx = dir.x > 0 ? 1 : -1, sy = dir.y > 0 ? 1 : -1, sz = dir.z > 0 ? 1 : -1;
            float tdx = dir.x != 0 ? Mathf.Abs(1f / dir.x) : float.MaxValue;
            float tdy = dir.y != 0 ? Mathf.Abs(1f / dir.y) : float.MaxValue;
            float tdz = dir.z != 0 ? Mathf.Abs(1f / dir.z) : float.MaxValue;
            float tmx = dir.x != 0 ? ((sx > 0 ? (x + 1 - origin.x) : (origin.x - x)) * tdx) : float.MaxValue;
            float tmy = dir.y != 0 ? ((sy > 0 ? (y + 1 - origin.y) : (origin.y - y)) * tdy) : float.MaxValue;
            float tmz = dir.z != 0 ? ((sz > 0 ? (z + 1 - origin.z) : (origin.z - z)) * tdz) : float.MaxValue;

            var normal = Vector3Int.zero;
            float t = 0;
            for (int i = 0; i < 512 && t <= maxDist; i++)
            {
                byte id = world.GetBlock(x, y, z);
                if (id == BlockIds.Unloaded) return false;
                if (i > 0 || id != 0)
                {
                    var def = Blocks.Get(id);
                    if (def.Selectable)
                    {
                        hit = new VoxelHit { Pos = new Vector3Int(x, y, z), Normal = normal, Id = id, Distance = t };
                        return true;
                    }
                }
                if (tmx < tmy && tmx < tmz) { x += sx; t = tmx; tmx += tdx; normal = new Vector3Int(-sx, 0, 0); }
                else if (tmy < tmz) { y += sy; t = tmy; tmy += tdy; normal = new Vector3Int(0, -sy, 0); }
                else { z += sz; t = tmz; tmz += tdz; normal = new Vector3Int(0, 0, -sz); }
            }
            return false;
        }
    }
}
