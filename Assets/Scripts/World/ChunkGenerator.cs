using System;
using UnityEngine;
using UKCity.City;

namespace UKCity.World
{
    public struct SurfaceSample
    {
        public SurfaceKind Kind;
        public MarkingKind Marking;
        public bool AxisX;
    }

    /// <summary>
    /// Produces the voxels for one chunk in three layers:
    ///   1. procedural terrain (flat English countryside with oaks),
    ///   2. the city plan (roads, roundabouts, props, building templates),
    ///   3. the player's own block edits.
    /// Pure function of its inputs, so it runs on worker threads.
    /// </summary>
    public static class ChunkGenerator
    {
        private const int S = WorldConst.SurfaceY;
        private const int ClearHeight = 14;

        public static byte[] Generate(ChunkCoord cc, int seed, CitySnapshot city, int[] edits)
        {
            var data = new byte[WorldConst.ChunkVolume];
            int minX = cc.MinX, minZ = cc.MinZ;

            // 1. Terrain.
            for (int z = 0; z < 16; z++)
                for (int x = 0; x < 16; x++)
                {
                    data[WorldConst.Index(x, 0, z)] = BlockIds.Bedrock;
                    for (int y = 1; y < S - 3; y++) data[WorldConst.Index(x, y, z)] = BlockIds.Stone;
                    for (int y = S - 3; y < S; y++) data[WorldConst.Index(x, y, z)] = BlockIds.Dirt;
                    data[WorldConst.Index(x, S, z)] = BlockIds.Grass;
                }
            Trees(data, minX, minZ, seed, city);

            // 2a. Roads.
            if (city.Segments.Length > 0 || city.Roundabouts.Length > 0)
            {
                for (int z = 0; z < 16; z++)
                    for (int x = 0; x < 16; x++)
                    {
                        var s = Sample(minX + x + 0.5f, minZ + z + 0.5f, city);
                        if (s.Kind == SurfaceKind.None) continue;
                        for (int y = S + 1; y <= S + ClearHeight; y++) data[WorldConst.Index(x, y, z)] = BlockIds.Air;
                        data[WorldConst.Index(x, S, z)] = SurfaceBlock(s);
                        if (s.Kind == SurfaceKind.Hedge)
                        {
                            data[WorldConst.Index(x, S + 1, z)] = BlockIds.Hedge;
                            data[WorldConst.Index(x, S + 2, z)] = BlockIds.Hedge;
                        }
                    }
            }

            // 2b. Street furniture generated from roads.
            foreach (var p in city.Props)
            {
                var col = Sample(p.X + 0.5f, p.Z + 0.5f, city).Kind;
                if (col != SurfaceKind.Pavement && col != SurfaceKind.Verge) continue;
                if (p.Kind == PropKind.Lamp)
                {
                    for (int y = S + 1; y <= S + 6; y++) Put(data, minX, minZ, p.X, y, p.Z, BlockIds.PoleGrey);
                    Put(data, minX, minZ, p.X + p.Arm.x, S + 6, p.Z + p.Arm.y, BlockIds.LampHead);
                }
                else
                {
                    Put(data, minX, minZ, p.X, S + 1, p.Z, BlockIds.PoleStriped);
                    Put(data, minX, minZ, p.X, S + 2, p.Z, BlockIds.PoleStriped);
                    Put(data, minX, minZ, p.X, S + 3, p.Z, BlockIds.Belisha);
                }
            }

            // 2c. Buildings, oldest first so newer placements win.
            foreach (var b in city.Buildings) Stamp(data, minX, minZ, b);

            // 3. Player edits.
            if (edits != null)
            {
                foreach (int e in edits) data[e >> 8] = (byte)(e & 0xFF);
            }
            return data;
        }

        private static void Put(byte[] data, int minX, int minZ, int wx, int y, int wz, byte b)
        {
            int lx = wx - minX, lz = wz - minZ;
            if (lx < 0 || lz < 0 || lx >= 16 || lz >= 16 || y < 0 || y >= WorldConst.ChunkHeight) return;
            data[WorldConst.Index(lx, y, lz)] = b;
        }

        private static void Stamp(byte[] data, int minX, int minZ, PlacementData p)
        {
            var t = p.Template;
            var fp = p.Footprint;
            if (fp.xMax <= minX || fp.xMin >= minX + 16 || fp.yMax <= minZ || fp.yMin >= minZ + 16) return;
            for (int lz = 0; lz < t.SizeZ; lz++)
                for (int lx = 0; lx < t.SizeX; lx++)
                {
                    var w = t.LocalToWorldXZ(lx, lz, p.Origin, p.Rotation);
                    int cx = w.x - minX, cz = w.y - minZ;
                    if (cx < 0 || cz < 0 || cx >= 16 || cz >= 16) continue;
                    for (int ly = 0; ly < t.SizeY; ly++)
                    {
                        int y = p.Origin.y + ly;
                        if (y < 1 || y >= WorldConst.ChunkHeight) continue;
                        byte b = t.WorldBlock(t.Get(lx, ly, lz), p.Origin, p.Rotation);
                        if (b == BuildingTemplate.Keep) continue;
                        data[WorldConst.Index(cx, y, cz)] = b;
                    }
                }
        }

        // ------------------------------------------------------------------ trees

        private const int TreeCell = 9;

        private static void Trees(byte[] data, int minX, int minZ, int seed, CitySnapshot city)
        {
            int c0x = WorldConst.FloorDiv(minX - 3, TreeCell), c1x = WorldConst.FloorDiv(minX + 18, TreeCell);
            int c0z = WorldConst.FloorDiv(minZ - 3, TreeCell), c1z = WorldConst.FloorDiv(minZ + 18, TreeCell);
            for (int cz = c0z; cz <= c1z; cz++)
                for (int cx = c0x; cx <= c1x; cx++)
                {
                    uint h = WorldConst.Hash(cx, cz, seed);
                    if (h % 100 >= 22) continue;
                    int tx = cx * TreeCell + 2 + (int)((h >> 8) % (TreeCell - 4));
                    int tz = cz * TreeCell + 2 + (int)((h >> 12) % (TreeCell - 4));
                    int trunk = 4 + (int)((h >> 16) % 3);
                    if (NearCity(tx, tz, city)) continue;
                    StampTree(data, minX, minZ, tx, tz, trunk);
                }
        }

        private static bool NearCity(int tx, int tz, CitySnapshot city)
        {
            const int r = 3;
            for (int i = 0; i < 5; i++)
            {
                int dx = i == 1 ? r : i == 2 ? -r : 0;
                int dz = i == 3 ? r : i == 4 ? -r : 0;
                if (Sample(tx + dx + 0.5f, tz + dz + 0.5f, city).Kind != SurfaceKind.None) return true;
            }
            foreach (var b in city.Buildings)
            {
                var f = b.Footprint;
                if (tx >= f.xMin - r && tx < f.xMax + r && tz >= f.yMin - r && tz < f.yMax + r) return true;
            }
            return false;
        }

        private static void StampTree(byte[] data, int minX, int minZ, int tx, int tz, int trunk)
        {
            for (int y = trunk - 1; y <= trunk + 2; y++)
            {
                int r = y >= trunk + 1 ? 1 : 2;
                for (int dz = -r; dz <= r; dz++)
                    for (int dx = -r; dx <= r; dx++)
                    {
                        if (r == 2 && Math.Abs(dx) == 2 && Math.Abs(dz) == 2) continue;
                        if (y == trunk + 2 && dx != 0 && dz != 0) continue;
                        int lx = tx + dx - minX, lz = tz + dz - minZ;
                        if (lx < 0 || lz < 0 || lx >= 16 || lz >= 16) continue;
                        int idx = WorldConst.Index(lx, S + y, lz);
                        if (data[idx] == BlockIds.Air) data[idx] = BlockIds.Leaves;
                    }
            }
            for (int y = 1; y <= trunk; y++) Put(data, minX, minZ, tx, S + y, tz, BlockIds.Log);
        }

        // ------------------------------------------------------------------ road rasterisation

        public static byte SurfaceBlock(SurfaceSample s)
        {
            switch (s.Kind)
            {
                case SurfaceKind.Asphalt:
                    switch (s.Marking)
                    {
                        case MarkingKind.WhiteDashed:
                        case MarkingKind.WhiteSolid: return s.AxisX ? BlockIds.LineX : BlockIds.LineZ;
                        case MarkingKind.DoubleYellow: return s.AxisX ? BlockIds.YellowX : BlockIds.YellowZ;
                        case MarkingKind.Paint: return BlockIds.RoadPaint;
                        default: return BlockIds.Asphalt;
                    }
                case SurfaceKind.Kerb: return BlockIds.Kerb;
                case SurfaceKind.Pavement: return BlockIds.Pavement;
                case SurfaceKind.Island: return s.Marking == MarkingKind.Paint ? BlockIds.RoadPaint : BlockIds.Grass;
                case SurfaceKind.IslandKerb: return BlockIds.Kerb;
                default: return BlockIds.Grass; // verge, reservation, hedge base
            }
        }

        /// <summary>Works out what the road plan wants at the surface of one column.</summary>
        public static SurfaceSample Sample(float px, float pz, CitySnapshot city)
        {
            var best = new SurfaceSample();
            int bestPrio = 0;
            int asphaltOwners = 0;

            var segs = city.Segments;
            for (int i = 0; i < segs.Length; i++)
            {
                ref readonly var seg = ref segs[i];
                float dx = seg.B.x - seg.A.x, dz = seg.B.y - seg.A.y;
                float len2 = dx * dx + dz * dz;
                if (len2 < 1e-4f) continue;
                float len = Mathf.Sqrt(len2);
                dx /= len; dz /= len;
                float rx = px - seg.A.x, rz = pz - seg.A.y;
                float along = rx * dx + rz * dz;
                float lateral = dx * rz - dz * rx;
                var type = seg.Type;

                bool cap = false, smoothCap = false;
                float dist;
                if (along < 0f)
                {
                    dist = Mathf.Sqrt(rx * rx + rz * rz);
                    cap = true; smoothCap = seg.SmoothA;
                }
                else if (along > len)
                {
                    float bx = px - seg.B.x, bz = pz - seg.B.y;
                    dist = Mathf.Sqrt(bx * bx + bz * bz);
                    cap = true; smoothCap = seg.SmoothB;
                }
                else dist = Mathf.Abs(lateral);

                if (dist >= type.MaxHalf || !type.TryGetBand(dist, out var band)) continue;

                var kind = band.Kind;
                var mark = MarkingKind.None;
                if (!cap)
                {
                    bool zebra = false;
                    if (kind == SurfaceKind.Asphalt && seg.Crossings != null)
                    {
                        foreach (float c in seg.Crossings)
                        {
                            if (Mathf.Abs(along - c) <= 1.5f)
                            {
                                zebra = true;
                                if ((Mathf.FloorToInt(lateral + 0.5f) & 1) == 0) mark = MarkingKind.Paint;
                                break;
                            }
                        }
                    }
                    if (!zebra && band.Marking != MarkingKind.None)
                    {
                        if (band.Marking == MarkingKind.WhiteDashed)
                        {
                            float period = band.Dash + band.Gap;
                            float m = along % period;
                            if (m < band.Dash) mark = MarkingKind.WhiteDashed;
                        }
                        else mark = band.Marking;
                    }
                }
                if (kind == SurfaceKind.Asphalt && !(cap && smoothCap)) asphaltOwners++;

                int prio = (int)kind;
                if (prio > bestPrio || (prio == bestPrio && mark != MarkingKind.None && best.Marking == MarkingKind.None))
                {
                    bestPrio = prio;
                    best.Kind = kind;
                    best.Marking = mark;
                    best.AxisX = Mathf.Abs(dx) >= Mathf.Abs(dz);
                }
            }

            var rbs = city.Roundabouts;
            for (int i = 0; i < rbs.Length; i++)
            {
                float dx = px - rbs[i].Centre.x, dz = pz - rbs[i].Centre.y;
                float d = Mathf.Sqrt(dx * dx + dz * dz);
                float r = rbs[i].Radius;
                SurfaceKind kind;
                var mark = MarkingKind.None;
                if (r <= 5f)
                {
                    // Mini roundabout: a painted white dome.
                    if (d < 1.5f) { kind = SurfaceKind.Island; mark = MarkingKind.Paint; }
                    else if (d < r) kind = SurfaceKind.Asphalt;
                    else if (d < r + 1) kind = SurfaceKind.Kerb;
                    else if (d < r + 3) kind = SurfaceKind.Pavement;
                    else continue;
                }
                else
                {
                    if (d < r - 7) kind = SurfaceKind.Island;
                    else if (d < r - 6) kind = SurfaceKind.IslandKerb;
                    else if (d < r) kind = SurfaceKind.Asphalt;
                    else if (d < r + 1) kind = SurfaceKind.Kerb;
                    else if (d < r + 3) kind = SurfaceKind.Pavement;
                    else continue;
                }
                if (kind == SurfaceKind.Asphalt) asphaltOwners++;
                int prio = (int)kind;
                if (prio > bestPrio)
                {
                    bestPrio = prio;
                    best.Kind = kind;
                    best.Marking = mark;
                }
            }

            // Where two roads overlap (junctions), drop lane markings so the junction box is clean tarmac.
            if (best.Kind == SurfaceKind.Asphalt && asphaltOwners >= 2) best.Marking = MarkingKind.None;
            return best;
        }
    }
}
