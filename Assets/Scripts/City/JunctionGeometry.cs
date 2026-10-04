using System.Collections.Generic;
using UnityEngine;
using UKCity.World;

namespace UKCity.City
{
    /// <summary>
    /// Works out the UK junction furniture around nodes and crossings: give way and stop lines, give way triangles,
    /// lane arrows, rounded kerbs, signal heads, pedestrian crossings and yellow boxes. Runs on the main thread while
    /// building a chunk snapshot; the results are plain data the rasteriser applies.
    /// </summary>
    public sealed class JunctionGeometry
    {
        public readonly List<TransverseData> Transverse = new List<TransverseData>();
        public readonly List<PointMarkData> Points = new List<PointMarkData>();
        public readonly List<CornerData> Corners = new List<CornerData>();
        public readonly List<DiscData> Discs = new List<DiscData>();
        public readonly List<PropData> Props = new List<PropData>();

        private readonly CityLayer city;

        public JunctionGeometry(CityLayer city) { this.city = city; }

        private struct Arm
        {
            public RoadSegment Seg;
            public RoadType Type;
            public Vector2 U;      // unit direction from the node out along the road
            public float Angle;
            public float Length;
        }

        /// <summary>Left of a direction of travel (UK traffic keeps to this side).</summary>
        public static Vector2 Left(Vector2 dir) => new Vector2(-dir.y, dir.x);

        public void Build(RectInt area)
        {
            const float reach = 60f;
            foreach (var n in city.Nodes.Values)
            {
                if (n.Pos.x < area.xMin - reach || n.Pos.x > area.xMax + reach || n.Pos.y < area.yMin - reach || n.Pos.y > area.yMax + reach) continue;
                var arms = ArmsOf(n);
                if (arms.Count == 0) continue;
                if (n.IsRoundabout) Roundabout(n, arms);
                else if (arms.Count >= 3)
                {
                    if (n.Signals) Signalised(n, arms);
                    else Priority(n, arms);
                }
                if (!n.IsRoundabout && arms.Count >= 2) CornersFor(n, arms);
            }

            foreach (var s in city.Segments.Values)
            {
                if (s.Crossings.Count == 0) continue;
                var r = city.SegmentArea(s);
                if (!r.Overlaps(new RectInt(area.xMin - 20, area.yMin - 20, area.width + 40, area.height + 40))) continue;
                CrossingsFor(s);
            }
        }

        private List<Arm> ArmsOf(RoadNode n)
        {
            var arms = new List<Arm>();
            foreach (int sid in n.Segments)
            {
                var s = city.Segments[sid];
                var other = city.Nodes[s.A == n.Id ? s.B : s.A].Pos;
                var d = other - n.Pos;
                float len = d.magnitude;
                if (len < 0.5f) continue;
                var u = d / len;
                arms.Add(new Arm { Seg = s, Type = s.RoadType, U = u, Angle = Mathf.Atan2(u.y, u.x), Length = len });
            }
            arms.Sort((a, b) => a.Angle.CompareTo(b.Angle));
            return arms;
        }

        // ------------------------------------------------------------------ helpers

        private void Line(Vector2 node, Arm a, float dist, float latFrom, float latTo, MarkingKind mark, float halfLen = 0.5f)
        {
            if (dist > a.Length - 1f) return;
            Transverse.Add(new TransverseData
            {
                P = node + a.U * dist, Dir = -a.U, LatFrom = latFrom, LatTo = latTo, HalfLen = halfLen, Mark = mark
            });
        }

        /// <summary>Lateral range of the inbound side of a road (traffic heading into the junction).</summary>
        private static void Inbound(RoadType t, out float from, out float to)
        {
            if (t.CentreLine || t.Dual) { from = 0f; to = t.CarriageHalf; }
            else { from = -t.CarriageHalf; to = t.CarriageHalf; }
        }

        private void Triangle(Vector2 node, Arm a, float dist)
        {
            if (dist > a.Length - 3f) return;
            var dir = -a.U;
            float lat = (a.Type.CentreLine || a.Type.Dual) ? a.Type.CarriageHalf * 0.5f : 0f;
            Points.Add(new PointMarkData { P = node + a.U * dist + Left(dir) * lat, Dir = dir, Mark = MarkingKind.Triangle });
        }

        private void Prop(Vector2 p, PropKind kind, Vector2 faceDir)
        {
            Props.Add(new PropData { X = Mathf.FloorToInt(p.x), Z = Mathf.FloorToInt(p.y), Kind = kind, Facing = BlockState.FacingToward(faceDir) });
        }

        // ------------------------------------------------------------------ junction types

        private void Roundabout(RoadNode n, List<Arm> arms)
        {
            bool mini = n.RoundaboutRadius <= 5f;
            float d = n.RoundaboutRadius + 0.5f;
            foreach (var a in arms)
            {
                Inbound(a.Type, out float from, out float to);
                Line(n.Pos, a, d, from, to, mini ? MarkingKind.GiveWay : MarkingKind.GiveWaySingle);
                Triangle(n.Pos, a, d + 6f);
            }
        }

        private void Priority(RoadNode n, List<Arm> arms)
        {
            // The major road is the best-ranked pair of arms that carry straight on through the junction.
            int bi = 0, bj = 1;
            float best = float.MinValue;
            for (int i = 0; i < arms.Count; i++)
                for (int j = i + 1; j < arms.Count; j++)
                {
                    float straight = -Vector2.Dot(arms[i].U, arms[j].U);
                    float score = (arms[i].Type.Rank + arms[j].Type.Rank) * 2f + straight * 3f;
                    if (score > best) { best = score; bi = i; bj = j; }
                }
            for (int k = 0; k < arms.Count; k++)
            {
                if (k == bi || k == bj) continue;
                var minor = arms[k];
                float dist = 0f;
                foreach (int m in new[] { bi, bj })
                {
                    float sin = Mathf.Abs(arms[m].U.x * minor.U.y - arms[m].U.y * minor.U.x);
                    dist = Mathf.Max(dist, arms[m].Type.CarriageHalf / Mathf.Max(0.35f, sin));
                }
                dist += 0.5f;
                Inbound(minor.Type, out float from, out float to);
                Line(n.Pos, minor, dist, from, to, MarkingKind.GiveWay);
                Triangle(n.Pos, minor, dist + 6f);
            }
        }

        private void Signalised(RoadNode n, List<Arm> arms)
        {
            if (n.YellowBox)
            {
                float r = 0f;
                foreach (var a in arms) r = Mathf.Max(r, a.Type.CarriageHalf);
                Discs.Add(new DiscData { C = n.Pos, R = r + 0.5f, Mark = MarkingKind.Box });
            }
            foreach (var a in arms)
            {
                float m = 0f;
                foreach (var o in arms)
                {
                    if (o.Seg == a.Seg) continue;
                    float sin = Mathf.Abs(o.U.x * a.U.y - o.U.y * a.U.x);
                    m = Mathf.Max(m, o.Type.CarriageHalf / Mathf.Max(0.35f, sin));
                }
                float ch = a.Type.CarriageHalf;
                float crossing = m + 3f;     // centre of the pedestrian crossing
                float stop = m + 6.5f;       // vehicle stop line
                var dir = -a.U;
                var left = Left(dir);
                Inbound(a.Type, out float from, out float to);
                Line(n.Pos, a, stop, from, to, MarkingKind.Stop);
                Line(n.Pos, a, crossing - 1.5f, -ch, ch, MarkingKind.Studs);
                Line(n.Pos, a, crossing + 1.5f, -ch, ch, MarkingKind.Studs);
                if (a.Length > stop + 8f)
                    Points.Add(new PointMarkData { P = n.Pos + a.U * (stop + 6f) + left * (ch * 0.5f), Dir = dir, Mark = MarkingKind.ArrowAhead });
                // Red tactile paving where people wait to cross.
                Transverse.Add(new TransverseData { P = n.Pos + a.U * crossing, Dir = dir, LatFrom = ch + 0.5f, LatTo = ch + 3.5f, HalfLen = 1.5f, Surface = SurfaceKind.Tactile });
                Transverse.Add(new TransverseData { P = n.Pos + a.U * crossing, Dir = dir, LatFrom = -ch - 3.5f, LatTo = -ch - 0.5f, HalfLen = 1.5f, Surface = SurfaceKind.Tactile });

                // Primary signal on the nearside (left) just past the stop line, secondary on the offside.
                var nearKerb = left * (ch + 1.5f);
                Prop(n.Pos + a.U * (stop - 0.5f) + nearKerb, PropKind.SignalHead, a.U);
                Prop(n.Pos + a.U * (stop - 0.5f) - nearKerb, PropKind.SignalHead, a.U);
                // Pedestrian heads at each end of the crossing, looking across the road.
                Prop(n.Pos + a.U * (crossing + 1f) + nearKerb, PropKind.PedHead, -left);
                Prop(n.Pos + a.U * (crossing - 1f) - nearKerb, PropKind.PedHead, left);
            }
        }

        // ------------------------------------------------------------------ corners

        private void CornersFor(RoadNode n, List<Arm> arms)
        {
            for (int i = 0; i < arms.Count; i++)
            {
                var a = arms[i];
                var b = arms[(i + 1) % arms.Count];
                if (a.Seg == b.Seg) continue;
                float gap = b.Angle - a.Angle;
                if (gap <= 0) gap += Mathf.PI * 2f;
                if (gap < 20f * Mathf.Deg2Rad || gap > 165f * Mathf.Deg2Rad) continue;
                // Edge normals pointing into the corner of land between the two roads.
                var na = Left(a.U);
                if (Vector2.Dot(na, b.U) < 0) na = -na;
                var nb = Left(b.U);
                if (Vector2.Dot(nb, a.U) < 0) nb = -nb;
                bool kerb = a.Type.HasKerb && b.Type.HasKerb;
                bool pave = a.Type.HasPavement && b.Type.HasPavement;
                float pw = Mathf.Min(a.Type.OuterWidth, b.Type.OuterWidth);
                float r = pw + (kerb ? 1f : 0f) + 4f;
                Corners.Add(new CornerData
                {
                    Node = n.Pos, Na = na, Nb = nb, Ha = a.Type.CarriageHalf, Hb = b.Type.CarriageHalf, R = r, Pw = pw,
                    KerbKind = kerb ? SurfaceKind.Kerb : SurfaceKind.Verge,
                    OuterKind = pave ? SurfaceKind.Pavement : SurfaceKind.Verge
                });
            }
        }

        // ------------------------------------------------------------------ crossings

        private void CrossingsFor(RoadSegment s)
        {
            var pa = city.Nodes[s.A].Pos;
            var pb = city.Nodes[s.B].Pos;
            float len = Vector2.Distance(pa, pb);
            if (len < 1f) return;
            var dir = (pb - pa) / len;
            var left = Left(dir);
            var t = s.RoadType;
            float ch = t.CarriageHalf;
            foreach (var c in s.Crossings)
            {
                var p = pa + dir * (c.T * len);
                var surf = SurfaceKind.Tactile;
                // Tactile paving both sides.
                Transverse.Add(new TransverseData { P = p, Dir = dir, LatFrom = ch + 0.5f, LatTo = ch + 3.5f, HalfLen = 1.5f, Surface = surf });
                Transverse.Add(new TransverseData { P = p, Dir = dir, LatFrom = -ch - 3.5f, LatTo = -ch - 0.5f, HalfLen = 1.5f, Surface = surf });
                if (c.Kind == CrossingKind.Zebra)
                {
                    // Give way lines a metre before the stripes, for traffic from each direction.
                    Transverse.Add(new TransverseData { P = p - dir * 2.5f, Dir = dir, LatFrom = 0, LatTo = ch, HalfLen = 0.5f, Mark = MarkingKind.GiveWaySingle });
                    Transverse.Add(new TransverseData { P = p + dir * 2.5f, Dir = -dir, LatFrom = 0, LatTo = ch, HalfLen = 0.5f, Mark = MarkingKind.GiveWaySingle });
                    for (int side = -1; side <= 1; side += 2)
                        Props.Add(new PropData
                        {
                            X = Mathf.FloorToInt((p + left * (side * (ch + 1.5f)) + dir * 2f).x),
                            Z = Mathf.FloorToInt((p + left * (side * (ch + 1.5f)) + dir * 2f).y),
                            Kind = PropKind.Belisha
                        });
                }
                else
                {
                    // Puffin crossing: studs either side of the crossing, stop lines, signals and push buttons.
                    Transverse.Add(new TransverseData { P = p - dir * 1.5f, Dir = dir, LatFrom = -ch, LatTo = ch, HalfLen = 0.5f, Mark = MarkingKind.Studs });
                    Transverse.Add(new TransverseData { P = p + dir * 1.5f, Dir = dir, LatFrom = -ch, LatTo = ch, HalfLen = 0.5f, Mark = MarkingKind.Studs });
                    Transverse.Add(new TransverseData { P = p - dir * 3.5f, Dir = dir, LatFrom = 0, LatTo = ch, HalfLen = 0.5f, Mark = MarkingKind.Stop });
                    Transverse.Add(new TransverseData { P = p + dir * 3.5f, Dir = -dir, LatFrom = 0, LatTo = ch, HalfLen = 0.5f, Mark = MarkingKind.Stop });
                    Prop(p - dir * 4f + left * (ch + 1.5f), PropKind.SignalHead, -dir);
                    Prop(p + dir * 4f - left * (ch + 1.5f), PropKind.SignalHead, dir);
                    Prop(p + left * (ch + 1.5f) + dir * 0.5f, PropKind.PedHead, -left);
                    Prop(p - left * (ch + 1.5f) - dir * 0.5f, PropKind.PedHead, left);
                }
            }
        }
    }
}
