using System;
using UnityEngine;
using UKCity.City;

namespace UKCity.World
{
    public struct SurfaceSample
    {
        public SurfaceKind Kind;
        public MarkingKind Marking;
        /// <summary>Facing for the marking block (0 = road runs north-south).</summary>
        public int Facing;
        /// <summary>Block standing on the surface (central barrier, crash barrier), 0 = none.</summary>
        public ushort Above;
    }

    /// <summary>
    /// Produces the voxels for one chunk in three layers:
    ///   1. procedural terrain (flat English countryside with oaks),
    ///   2. the city plan (roads, junction markings, street furniture, building templates),
    ///   3. the player's own block edits.
    /// Pure function of its inputs, so it runs on worker threads.
    /// </summary>
    public static class ChunkGenerator
    {
        private const int S = WorldConst.SurfaceY;
        private const int ClearHeight = 14;

        public static ushort[] Generate(ChunkCoord cc, int seed, CitySnapshot city, int[] edits)
        {
            var data = new ushort[WorldConst.ChunkVolume];
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
                        else if (s.Above != 0) data[WorldConst.Index(x, S + 1, z)] = s.Above;
                    }
            }

            // 2b. Street furniture generated from roads and junctions.
            foreach (var p in city.Props)
            {
                var col = Sample(p.X + 0.5f, p.Z + 0.5f, city).Kind;
                if (col != SurfaceKind.Pavement && col != SurfaceKind.Verge && col != SurfaceKind.Tactile) continue;
                switch (p.Kind)
                {
                    case PropKind.Lamp:
                        for (int y = S + 1; y <= S + 6; y++) Put(data, minX, minZ, p.X, y, p.Z, BlockIds.PoleGrey);
                        Put(data, minX, minZ, p.X + p.Arm.x, S + 6, p.Z + p.Arm.y, BlockState.Make(BlockIds.LampHead, p.Facing));
                        break;
                    case PropKind.Belisha:
                        Put(data, minX, minZ, p.X, S + 1, p.Z, BlockIds.PoleStriped);
                        Put(data, minX, minZ, p.X, S + 2, p.Z, BlockIds.PoleStriped);
                        Put(data, minX, minZ, p.X, S + 3, p.Z, BlockIds.Belisha);
                        break;
                    case PropKind.SignalHead:
                        Put(data, minX, minZ, p.X, S + 1, p.Z, BlockIds.PoleGrey);
                        Put(data, minX, minZ, p.X, S + 2, p.Z, BlockIds.PoleGrey);
                        Put(data, minX, minZ, p.X, S + 3, p.Z, BlockState.Make(BlockIds.TrafficLight, p.Facing));
                        break;
                    case PropKind.PedHead:
                        Put(data, minX, minZ, p.X, S + 1, p.Z, BlockState.Make(BlockIds.PushButton, p.Facing));
                        Put(data, minX, minZ, p.X, S + 2, p.Z, BlockState.Make(BlockIds.PedSignal, p.Facing));
                        break;
                }
            }

            // 2c. Buildings, oldest first so newer placements win.
            foreach (var b in city.Buildings) Stamp(data, minX, minZ, b);

            // 3. Player edits.
            if (edits != null)
            {
                foreach (int e in edits) data[e >> 16] = (ushort)(e & 0xFFFF);
            }
            return data;
        }

        private static void Put(ushort[] data, int minX, int minZ, int wx, int y, int wz, ushort b)
        {
            int lx = wx - minX, lz = wz - minZ;
            if (lx < 0 || lz < 0 || lx >= 16 || lz >= 16 || y < 0 || y >= WorldConst.ChunkHeight) return;
            data[WorldConst.Index(lx, y, lz)] = b;
        }

        private static void Stamp(ushort[] data, int minX, int minZ, PlacementData p)
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
                        ushort b = t.WorldBlock(t.Get(lx, ly, lz), p.Origin, p.Rotation);
                        if (b == BuildingTemplate.Keep) continue;
                        data[WorldConst.Index(cx, y, cz)] = b;
                    }
                }
        }

        // ------------------------------------------------------------------ trees

        private const int TreeCell = 9;

        private static void Trees(ushort[] data, int minX, int minZ, int seed, CitySnapshot city)
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

        private static void StampTree(ushort[] data, int minX, int minZ, int tx, int tz, int trunk)
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

        public static ushort SurfaceBlock(SurfaceSample s)
        {
            switch (s.Kind)
            {
                case SurfaceKind.Asphalt:
                    int id;
                    switch (s.Marking)
                    {
                        case MarkingKind.WhiteDashed:
                        case MarkingKind.WhiteSolid: id = BlockIds.MarkLine; break;
                        case MarkingKind.WhiteThick: id = BlockIds.MarkThickLine; break;
                        case MarkingKind.DoubleYellow: id = BlockIds.MarkDoubleYellow; break;
                        case MarkingKind.SingleYellow: id = BlockIds.MarkSingleYellow; break;
                        case MarkingKind.Paint: return BlockIds.RoadPaint;
                        case MarkingKind.GiveWay: id = BlockIds.MarkGiveWay; break;
                        case MarkingKind.GiveWaySingle: id = BlockIds.MarkGiveWaySingle; break;
                        case MarkingKind.Stop: id = BlockIds.MarkStop; break;
                        case MarkingKind.Zigzag: id = BlockIds.MarkZigzag; break;
                        case MarkingKind.Studs: id = BlockIds.MarkStuds; break;
                        case MarkingKind.Box: id = BlockIds.MarkBox; break;
                        case MarkingKind.Triangle: id = BlockIds.MarkTriangle; break;
                        case MarkingKind.ArrowAhead: id = BlockIds.MarkArrowAhead; break;
                        case MarkingKind.Hatch: id = BlockIds.MarkHatch; break;
                        default: return BlockIds.Asphalt;
                    }
                    return BlockState.Make(id, s.Facing);
                case SurfaceKind.Kerb:
                case SurfaceKind.IslandKerb: return BlockIds.Kerb;
                case SurfaceKind.Pavement: return BlockIds.Pavement;
                case SurfaceKind.Tactile: return BlockIds.TactileRed;
                case SurfaceKind.Barrier: return BlockIds.Concrete;
                case SurfaceKind.Island: return s.Marking == MarkingKind.Paint ? BlockIds.RoadPaint : BlockIds.Grass;
                default: return BlockIds.Grass; // verge, reservation, hedge base
            }
        }

        private static int AlongFacing(float dx, float dz) => Mathf.Abs(dx) > Mathf.Abs(dz) ? 1 : 0;

        /// <summary>Works out what the road plan wants at the surface of one column.</summary>
        public static SurfaceSample Sample(float px, float pz, CitySnapshot city)
        {
            var best = new SurfaceSample();
            int bestPrio = 0;
            int asphaltOwners = 0;
            bool forced = false;

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
                ushort above = 0;
                int facing = AlongFacing(dx, dz);
                if (kind == SurfaceKind.Barrier) above = BlockState.Make(BlockIds.ConcreteBarrier, facing == 1 ? 0 : 1);
                else if (kind == SurfaceKind.ArmcoVerge) above = BlockState.Make(BlockIds.Armco, lateral > 0 ? (facing == 1 ? 2 : 3) : (facing == 1 ? 0 : 1));

                if (!cap && kind == SurfaceKind.Asphalt)
                {
                    bool special = false;
                    if (seg.Crossings != null)
                    {
                        for (int c = 0; c < seg.Crossings.Length; c++)
                        {
                            float d = Mathf.Abs(along - seg.Crossings[c]);
                            bool zebra = seg.CrossingKinds[c] == CrossingKind.Zebra;
                            if (zebra && d <= 1.5f)
                            {
                                special = true;
                                if ((Mathf.FloorToInt(lateral + 0.5f) & 1) == 0) mark = MarkingKind.Paint;
                                break;
                            }
                            if (!zebra && d <= 1.0f) { special = true; break; }
                            float z0 = zebra ? 3.5f : 4.5f;
                            if (d >= z0 && d <= z0 + 9f)
                            {
                                float edge = Mathf.Abs(lateral);
                                if (edge >= type.CarriageHalf - 1f || (type.CentreLine && edge < 0.5f))
                                {
                                    special = true;
                                    mark = MarkingKind.Zigzag;
                                    break;
                                }
                            }
                        }
                    }
                    if (!special && band.Marking != MarkingKind.None)
                    {
                        if (band.Marking == MarkingKind.WhiteDashed)
                        {
                            float dash = band.Dash, gap = band.Gap;
                            // Hazard warning line: longer dashes approaching a junction.
                            bool centre = band.From == 0f;
                            if (centre && ((!seg.SmoothA && along < 30f) || (!seg.SmoothB && len - along < 30f))) { dash = 4f; gap = 2f; }
                            float m = along % (dash + gap);
                            if (m < dash) mark = MarkingKind.WhiteDashed;
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
                    best.Facing = facing;
                    best.Above = above;
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
                    best.Above = 0;
                }
            }

            // Rounded kerbs at junction corners.
            var corners = city.Corners;
            for (int i = 0; i < corners.Length; i++)
            {
                ref readonly var c = ref corners[i];
                float rx = px - c.Node.x, rz = pz - c.Node.y;
                float sa = rx * c.Na.x + rz * c.Na.y - c.Ha;
                float sb = rx * c.Nb.x + rz * c.Nb.y - c.Hb;
                if (sa < 0 || sb < 0 || sa > c.R || sb > c.R) continue;
                if (bestPrio >= (int)SurfaceKind.Asphalt) continue;
                // Centre of the kerb arc: R in from both carriageway edges.
                float a1 = c.Na.x, b1 = c.Na.y, c1 = c.Ha + c.R;
                float a2 = c.Nb.x, b2 = c.Nb.y, c2 = c.Hb + c.R;
                float det = a1 * b2 - a2 * b1;
                if (Mathf.Abs(det) < 1e-4f) continue;
                float cx = (c1 * b2 - c2 * b1) / det, cz = (a1 * c2 - a2 * c1) / det;
                float ddx = rx - cx, ddz = rz - cz;
                float d = Mathf.Sqrt(ddx * ddx + ddz * ddz);
                if (d > c.R)
                {
                    best.Kind = SurfaceKind.Asphalt;
                    best.Marking = MarkingKind.None;
                    best.Above = 0;
                    bestPrio = (int)SurfaceKind.Asphalt;
                    asphaltOwners = Math.Max(asphaltOwners, 2);
                }
                else if (d > c.R - 1f && c.KerbKind == SurfaceKind.Kerb) { best.Kind = SurfaceKind.Kerb; best.Above = 0; bestPrio = (int)SurfaceKind.Kerb; }
                else if (d > c.R - 1f - c.Pw - (c.KerbKind == SurfaceKind.Kerb ? 0 : 1)) { best.Kind = c.OuterKind; best.Above = 0; bestPrio = (int)c.OuterKind; }
            }

            // Where two roads overlap (junctions), drop lane markings so the junction box is clean tarmac.
            if (best.Kind == SurfaceKind.Asphalt && asphaltOwners >= 2) best.Marking = MarkingKind.None;

            // Lines across the road: give way, stop lines, studs; and tactile paving.
            var tr = city.Transverse;
            for (int i = 0; i < tr.Length; i++)
            {
                ref readonly var t = ref tr[i];
                float rx = px - t.P.x, rz = pz - t.P.y;
                float al = rx * t.Dir.x + rz * t.Dir.y;
                if (al <= -t.HalfLen || al > t.HalfLen) continue;
                float lat = -t.Dir.y * rx + t.Dir.x * rz;
                if (lat < t.LatFrom || lat >= t.LatTo) continue;
                if (t.Surface != SurfaceKind.None)
                {
                    if (best.Kind == SurfaceKind.Pavement) best.Kind = SurfaceKind.Tactile;
                }
                else if (best.Kind == SurfaceKind.Asphalt)
                {
                    best.Marking = t.Mark;
                    best.Facing = AlongFacing(t.Dir.x, t.Dir.y);
                    forced = true;
                }
            }

            var pts = city.Points;
            int cellX = Mathf.FloorToInt(px), cellZ = Mathf.FloorToInt(pz);
            for (int i = 0; i < pts.Length; i++)
            {
                if (Mathf.FloorToInt(pts[i].P.x) != cellX || Mathf.FloorToInt(pts[i].P.y) != cellZ) continue;
                if (best.Kind != SurfaceKind.Asphalt) continue;
                best.Marking = pts[i].Mark;
                best.Facing = BlockState.FacingToward(pts[i].Dir);
                forced = true;
            }

            var discs = city.Discs;
            if (!forced && best.Kind == SurfaceKind.Asphalt)
                for (int i = 0; i < discs.Length; i++)
                {
                    float dx = px - discs[i].C.x, dz = pz - discs[i].C.y;
                    if (dx * dx + dz * dz < discs[i].R * discs[i].R) { best.Marking = discs[i].Mark; best.Facing = 0; }
                }
            return best;
        }
    }
}
