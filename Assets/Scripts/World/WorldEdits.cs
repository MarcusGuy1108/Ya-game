using System.Collections.Generic;
using UnityEngine;

namespace UKCity.World
{
    /// <summary>
    /// Every block the player has placed or broken by hand, stored as a sparse per-chunk delta.
    /// Applied on top of terrain + city plan whenever a chunk generates, so edits survive
    /// chunk unloading, road changes and save/load.
    /// </summary>
    public sealed class WorldEdits
    {
        private readonly Dictionary<ChunkCoord, Dictionary<int, byte>> map = new Dictionary<ChunkCoord, Dictionary<int, byte>>();

        public IEnumerable<KeyValuePair<ChunkCoord, Dictionary<int, byte>>> All => map;

        public void Set(int x, int y, int z, byte id)
        {
            var cc = ChunkCoord.FromBlock(x, z);
            if (!map.TryGetValue(cc, out var d)) map[cc] = d = new Dictionary<int, byte>();
            d[WorldConst.Index(x - cc.MinX, y, z - cc.MinZ)] = id;
        }

        /// <summary>Packed (index &lt;&lt; 8 | block) copy for a worker thread, or null if no edits.</summary>
        public int[] GetPacked(ChunkCoord cc)
        {
            if (!map.TryGetValue(cc, out var d) || d.Count == 0) return null;
            var arr = new int[d.Count];
            int i = 0;
            foreach (var kv in d) arr[i++] = (kv.Key << 8) | kv.Value;
            return arr;
        }

        public void SetPacked(ChunkCoord cc, int[] packed)
        {
            var d = new Dictionary<int, byte>(packed.Length);
            foreach (int e in packed) d[e >> 8] = (byte)(e & 0xFF);
            map[cc] = d;
        }

        /// <summary>Removes edits inside an inclusive box. Returns the chunks that changed.</summary>
        public List<ChunkCoord> ClearBox(Vector3Int min, Vector3Int max)
        {
            var changed = new List<ChunkCoord>();
            var c0 = ChunkCoord.FromBlock(min.x, min.z);
            var c1 = ChunkCoord.FromBlock(max.x, max.z);
            for (int cz = c0.Z; cz <= c1.Z; cz++)
                for (int cx = c0.X; cx <= c1.X; cx++)
                {
                    var cc = new ChunkCoord(cx, cz);
                    if (!map.TryGetValue(cc, out var d)) continue;
                    var remove = new List<int>();
                    foreach (var key in d.Keys)
                    {
                        int lx = key & 15, lz = (key >> 4) & 15, y = key >> 8;
                        int wx = cc.MinX + lx, wz = cc.MinZ + lz;
                        if (wx >= min.x && wx <= max.x && wz >= min.z && wz <= max.z && y >= min.y && y <= max.y) remove.Add(key);
                    }
                    if (remove.Count == 0) continue;
                    foreach (int k in remove) d.Remove(k);
                    changed.Add(cc);
                }
            return changed;
        }

        public void Clear() => map.Clear();
    }
}
