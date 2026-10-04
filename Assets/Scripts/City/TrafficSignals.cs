using System.Collections.Generic;
using UnityEngine;
using UKCity.World;

namespace UKCity.City
{
    /// <summary>The four UK vehicle signal aspects, in the order of the signal head's animation frames.</summary>
    public enum Aspect { Red = 0, RedAmber = 1, Green = 2, Amber = 3 }

    /// <summary>
    /// Runs every set of traffic lights. Signal heads are just blocks; each one works out which controller it
    /// belongs to from where it stands and which way it faces:
    ///  - near a signalised junction: the approach it faces down, in a staged cycle with an all-red pedestrian stage,
    ///  - near a signal-controlled (puffin) crossing: that crossing's cycle,
    ///  - otherwise: a simple two-phase cycle shared by nearby heads, split by north-south vs east-west facing.
    /// The UK sequence is red, red+amber, green, amber, red.
    /// </summary>
    public sealed class TrafficSignals
    {
        public const float Green = 12f, AmberTime = 3f, AllRed = 2f, RedAmberTime = 2f, PedGreen = 7f, PedClear = 3f;

        private enum Ctl : byte { Standalone, Junction, Crossing }

        private struct Binding
        {
            public Ctl Kind;
            public int Node;        // junction node id
            public int Group;       // stage group for this approach (-1 = pedestrian head)
            public int Groups;      // number of vehicle stages at the junction
            public float Offset;    // per-controller time offset
        }

        private readonly CityLayer city;
        private readonly Dictionary<long, Binding> cache = new Dictionary<long, Binding>();

        public float Time;

        public TrafficSignals(CityLayer city)
        {
            this.city = city;
            city.Changed += _ => cache.Clear();
        }

        public void Invalidate() => cache.Clear();

        private static long Key(int x, int y, int z) => ((long)(x & 0xFFFFF) << 40) | ((long)(z & 0xFFFFF) << 20) | (uint)(y & 0xFFFFF);

        // ------------------------------------------------------------------ public queries

        public Aspect VehicleAspect(int x, int y, int z, int facing)
        {
            var b = Bind(x, y, z, facing, false);
            switch (b.Kind)
            {
                case Ctl.Junction: return JunctionAspect(b.Group, b.Groups, Time + b.Offset);
                case Ctl.Crossing: return CrossingAspect(Time + b.Offset);
                default: return JunctionAspect(b.Group, 2, Time + b.Offset);
            }
        }

        public bool PedestrianGreen(int x, int y, int z, int facing)
        {
            var b = Bind(x, y, z, facing, true);
            switch (b.Kind)
            {
                case Ctl.Junction: return PedPhase(b.Groups, Time + b.Offset);
                case Ctl.Crossing: return CrossingPedGreen(Time + b.Offset);
                default: return PedPhase(2, Time + b.Offset);
            }
        }

        /// <summary>Everything a signal head needs to light its lamps.</summary>
        public struct HeadState
        {
            public Aspect Vehicle;
            public bool PedGreen;
            /// <summary>Another vehicle stage is running (used for filter arrows).</summary>
            public bool OtherStageGreen;
            /// <summary>Seconds until the green man (for the WAIT lamp); 0 while it shows.</summary>
            public float UntilPed;
        }

        public HeadState State(int x, int y, int z, int facing, bool pedestrian)
        {
            var b = Bind(x, y, z, facing, pedestrian);
            float t = Time + b.Offset;
            var st = new HeadState();
            switch (b.Kind)
            {
                case Ctl.Crossing:
                    st.Vehicle = CrossingAspect(t);
                    st.PedGreen = CrossingPedGreen(t);
                    st.UntilPed = Mod(CrossGreen + AmberTime + 1f - Mod(t, CrossCycle), CrossCycle);
                    break;
                default:
                {
                    int groups = b.Kind == Ctl.Junction ? b.Groups : 2;
                    int group = Mathf.Max(0, b.Group);
                    st.Vehicle = JunctionAspect(group, groups, t);
                    st.PedGreen = PedPhase(groups, t);
                    for (int g = 0; g < groups; g++)
                        if (g != group && JunctionAspect(g, groups, t) == Aspect.Green) st.OtherStageGreen = true;
                    float cycle = Cycle(groups);
                    st.UntilPed = Mod(groups * StageLength - Mod(t, cycle), cycle);
                    break;
                }
            }
            if (st.PedGreen) st.UntilPed = 0;
            return st;
        }

        // ------------------------------------------------------------------ timing

        private static float StageLength => Green + AmberTime + AllRed;
        private static float Cycle(int groups) => groups * StageLength + PedGreen + PedClear;

        /// <summary>Stage g gets green at g * StageLength; red+amber shows during the all-red before it.</summary>
        public static Aspect JunctionAspect(int group, int groups, float t)
        {
            float cycle = Cycle(groups);
            t = Mod(t, cycle);
            float start = group * StageLength;
            float rel = Mod(t - start, cycle);
            if (rel < Green) return Aspect.Green;
            if (rel < Green + AmberTime) return Aspect.Amber;
            if (rel >= cycle - RedAmberTime) return Aspect.RedAmber;
            return Aspect.Red;
        }

        public static bool PedPhase(int groups, float t)
        {
            float cycle = Cycle(groups);
            t = Mod(t, cycle);
            float start = groups * StageLength;
            return t >= start && t < start + PedGreen;
        }

        // Puffin crossing: long vehicle green, then pedestrians.
        private const float CrossGreen = 24f, CrossCycle = CrossGreen + AmberTime + 1f + PedGreen + PedClear + RedAmberTime;

        public static Aspect CrossingAspect(float t)
        {
            t = Mod(t, CrossCycle);
            if (t < CrossGreen) return Aspect.Green;
            if (t < CrossGreen + AmberTime) return Aspect.Amber;
            if (t >= CrossCycle - RedAmberTime) return Aspect.RedAmber;
            return Aspect.Red;
        }

        public static bool CrossingPedGreen(float t)
        {
            t = Mod(t, CrossCycle);
            float start = CrossGreen + AmberTime + 1f;
            return t >= start && t < start + PedGreen;
        }

        private static float Mod(float a, float m) => a - m * Mathf.Floor(a / m);

        // ------------------------------------------------------------------ binding heads to controllers

        private Binding Bind(int x, int y, int z, int facing, bool pedestrian)
        {
            long key = Key(x, y, z);
            if (cache.TryGetValue(key, out var b)) return b;
            b = Resolve(x, z, facing, pedestrian);
            cache[key] = b;
            return b;
        }

        private Binding Resolve(int x, int z, int facing, bool pedestrian)
        {
            var p = new Vector2(x + 0.5f, z + 0.5f);
            var front = BlockState.FrontDirection(facing);
            var faceDir = new Vector2(front.x, front.y);

            // Signal-controlled crossing within reach?
            foreach (var s in city.Segments.Values)
            {
                foreach (var c in s.Crossings)
                {
                    if (c.Kind != CrossingKind.Signal) continue;
                    var cp = city.CrossingPos(s, c.T);
                    if (Vector2.Distance(cp, p) <= s.RoadType.CarriageHalf + 7f)
                        return new Binding { Kind = Ctl.Crossing, Offset = Hash01(Mathf.FloorToInt(cp.x), Mathf.FloorToInt(cp.y)) * CrossCycle };
                }
            }

            // Nearest signalised junction.
            RoadNode best = null;
            float bestD = 45f;
            foreach (var n in city.Nodes.Values)
            {
                if (!n.Signals || n.Segments.Count < 3) continue;
                float d = Vector2.Distance(n.Pos, p);
                if (d < bestD) { bestD = d; best = n; }
            }
            if (best != null)
            {
                var groups = Stages(best, out var armGroup, out var armDirs);
                int group = -1;
                if (!pedestrian)
                {
                    // The head faces the drivers it controls, i.e. outward along its approach.
                    float bestDot = -2f;
                    for (int i = 0; i < armDirs.Count; i++)
                    {
                        float dot = Vector2.Dot(armDirs[i], faceDir);
                        if (dot > bestDot) { bestDot = dot; group = armGroup[i]; }
                    }
                }
                return new Binding { Kind = Ctl.Junction, Node = best.Id, Group = group, Groups = groups, Offset = Hash01(best.Id, 3) * 30f };
            }

            // Standalone: north-south heads in stage 0, east-west in stage 1, shared across a 48 m area.
            int cx = Mathf.FloorToInt(x / 48f), cz = Mathf.FloorToInt(z / 48f);
            return new Binding { Kind = Ctl.Standalone, Group = (facing & 1) == 0 ? 0 : 1, Groups = 2, Offset = Hash01(cx, cz) * 40f };
        }

        /// <summary>Groups a junction's arms into stages: roughly opposite arms run together.</summary>
        private int Stages(RoadNode n, out List<int> armGroup, out List<Vector2> armDirs)
        {
            armGroup = new List<int>();
            armDirs = new List<Vector2>();
            foreach (int sid in n.Segments)
            {
                var s = city.Segments[sid];
                var other = city.Nodes[s.A == n.Id ? s.B : s.A].Pos;
                armDirs.Add((other - n.Pos).normalized);
                armGroup.Add(-1);
            }
            int groups = 0;
            for (int i = 0; i < armDirs.Count; i++)
            {
                if (armGroup[i] >= 0) continue;
                armGroup[i] = groups;
                for (int j = i + 1; j < armDirs.Count; j++)
                    if (armGroup[j] < 0 && Vector2.Dot(armDirs[i], armDirs[j]) < -0.8f) { armGroup[j] = groups; break; }
                groups++;
            }
            return Mathf.Max(groups, 1);
        }

        private static float Hash01(int a, int b) => (WorldConst.Hash(a, b, 991) & 0xFFFF) / 65535f;
    }
}
