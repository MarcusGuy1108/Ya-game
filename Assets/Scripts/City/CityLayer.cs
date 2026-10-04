using System;
using System.Collections.Generic;
using UnityEngine;
using UKCity.World;

namespace UKCity.City
{
    public sealed class RoadNode
    {
        public int Id;
        public Vector2 Pos;
        public bool IsRoundabout;
        public float RoundaboutRadius;
        public readonly List<int> Segments = new List<int>();
    }

    public sealed class RoadSegment
    {
        public int Id;
        public int A, B;
        public int Type;
        /// <summary>Zebra crossings, stored as a fraction (0..1) along the segment from A to B.</summary>
        public readonly List<float> Crossings = new List<float>();
        public RoadType RoadType => RoadTypes.Get(Type);
    }

    public sealed class BuildingPlacement
    {
        public int Id;
        public string TemplateId;
        public Vector3Int Origin;
        public int Rotation;
        public BuildingTemplate Template => BuildingLibrary.Get(TemplateId);
    }

    /// <summary>
    /// The "plan" of the city: a road graph plus placed building templates.
    /// This is the editable layer the 2D planner works on. It is rasterised into voxels when chunks generate,
    /// underneath the player's hand-made block edits. Later, AI traffic will path-find over the same graph.
    /// </summary>
    public sealed class CityLayer
    {
        public readonly Dictionary<int, RoadNode> Nodes = new Dictionary<int, RoadNode>();
        public readonly Dictionary<int, RoadSegment> Segments = new Dictionary<int, RoadSegment>();
        public readonly Dictionary<int, BuildingPlacement> Buildings = new Dictionary<int, BuildingPlacement>();

        /// <summary>Raised with the world-space XZ area whose voxels need regenerating.</summary>
        public event Action<RectInt> Changed;

        public int NextId = 1;

        // ------------------------------------------------------------------ editing: roads

        public RoadNode AddNode(Vector2 pos)
        {
            var n = new RoadNode { Id = NextId++, Pos = pos };
            Nodes[n.Id] = n;
            return n;
        }

        public RoadSegment FindSegmentBetween(int a, int b)
        {
            if (!Nodes.TryGetValue(a, out var na)) return null;
            foreach (int sid in na.Segments)
            {
                var s = Segments[sid];
                if ((s.A == a && s.B == b) || (s.A == b && s.B == a)) return s;
            }
            return null;
        }

        public RoadSegment AddSegment(int a, int b, int type)
        {
            if (a == b || !Nodes.ContainsKey(a) || !Nodes.ContainsKey(b)) return null;
            var existing = FindSegmentBetween(a, b);
            if (existing != null) return existing;
            var s = new RoadSegment { Id = NextId++, A = a, B = b, Type = type };
            Segments[s.Id] = s;
            Nodes[a].Segments.Add(s.Id);
            Nodes[b].Segments.Add(s.Id);
            RaiseAround(a, b);
            return s;
        }

        public void RemoveSegment(int id)
        {
            if (!Segments.TryGetValue(id, out var s)) return;
            var area = NodeArea(s.A, s.B);
            Segments.Remove(id);
            Nodes[s.A].Segments.Remove(id);
            Nodes[s.B].Segments.Remove(id);
            PruneNode(s.A);
            PruneNode(s.B);
            Raise(area);
        }

        public void RemoveNode(int id)
        {
            if (!Nodes.TryGetValue(id, out var n)) return;
            var area = NodeArea(id);
            foreach (int sid in n.Segments.ToArray())
            {
                var s = Segments[sid];
                int other = s.A == id ? s.B : s.A;
                Segments.Remove(sid);
                Nodes[other].Segments.Remove(sid);
                PruneNode(other);
            }
            Nodes.Remove(id);
            Raise(area);
        }

        public void MoveNode(int id, Vector2 pos)
        {
            if (!Nodes.TryGetValue(id, out var n)) return;
            var before = NodeArea(id);
            n.Pos = pos;
            var after = NodeArea(id);
            Raise(Union(before, after));
        }

        /// <summary>Inserts a node into a segment at the given point, returning the new node.</summary>
        public RoadNode SplitSegment(int segId, Vector2 pos)
        {
            var s = Segments[segId];
            var a = Nodes[s.A];
            var b = Nodes[s.B];
            float len = Vector2.Distance(a.Pos, b.Pos);
            float t = len > 0.001f ? Mathf.Clamp01(Vector2.Dot(pos - a.Pos, b.Pos - a.Pos) / (len * len)) : 0.5f;
            var n = AddNode(pos);

            // Keep crossings on whichever half they fell on.
            var crossings = s.Crossings.ToArray();
            Segments.Remove(segId);
            a.Segments.Remove(segId);
            b.Segments.Remove(segId);
            var s1 = new RoadSegment { Id = NextId++, A = s.A, B = n.Id, Type = s.Type };
            var s2 = new RoadSegment { Id = NextId++, A = n.Id, B = s.B, Type = s.Type };
            foreach (float c in crossings)
            {
                if (c < t) s1.Crossings.Add(c / Mathf.Max(t, 0.0001f));
                else s2.Crossings.Add((c - t) / Mathf.Max(1 - t, 0.0001f));
            }
            foreach (var ns in new[] { s1, s2 })
            {
                Segments[ns.Id] = ns;
                Nodes[ns.A].Segments.Add(ns.Id);
                Nodes[ns.B].Segments.Add(ns.Id);
            }
            RaiseAround(s.A, s.B);
            return n;
        }

        public void SetRoundabout(int nodeId, float radius)
        {
            if (!Nodes.TryGetValue(nodeId, out var n)) return;
            var before = NodeArea(nodeId);
            n.IsRoundabout = radius > 0;
            n.RoundaboutRadius = radius;
            Raise(Union(before, NodeArea(nodeId)));
        }

        public void SetSegmentType(int segId, int type)
        {
            if (!Segments.TryGetValue(segId, out var s)) return;
            var before = NodeArea(s.A, s.B);
            s.Type = type;
            Raise(Union(before, NodeArea(s.A, s.B)));
        }

        public void AddCrossing(int segId, float frac)
        {
            if (!Segments.TryGetValue(segId, out var s)) return;
            s.Crossings.Add(Mathf.Clamp01(frac));
            Raise(SegmentArea(s));
        }

        public bool RemoveCrossingNear(Vector2 p, float radius)
        {
            foreach (var s in Segments.Values)
            {
                for (int i = 0; i < s.Crossings.Count; i++)
                {
                    if (Vector2.Distance(CrossingPos(s, s.Crossings[i]), p) <= radius)
                    {
                        s.Crossings.RemoveAt(i);
                        Raise(SegmentArea(s));
                        return true;
                    }
                }
            }
            return false;
        }

        public Vector2 CrossingPos(RoadSegment s, float frac) => Vector2.Lerp(Nodes[s.A].Pos, Nodes[s.B].Pos, frac);

        /// <summary>Removes a node that no longer carries any road (unless it is a standalone roundabout).</summary>
        private void PruneNode(int id)
        {
            if (Nodes.TryGetValue(id, out var n) && n.Segments.Count == 0 && !n.IsRoundabout) Nodes.Remove(id);
        }

        // ------------------------------------------------------------------ editing: buildings

        public BuildingPlacement AddBuilding(string templateId, Vector3Int origin, int rot)
        {
            var t = BuildingLibrary.Get(templateId);
            if (t == null) return null;
            var p = new BuildingPlacement { Id = NextId++, TemplateId = templateId, Origin = origin, Rotation = rot & 3 };
            Buildings[p.Id] = p;
            Raise(Expand(t.Footprint(origin, rot), 1));
            return p;
        }

        public void RemoveBuilding(int id)
        {
            if (!Buildings.TryGetValue(id, out var p)) return;
            Buildings.Remove(id);
            var t = p.Template;
            if (t != null) Raise(Expand(t.Footprint(p.Origin, p.Rotation), 1));
        }

        public BuildingPlacement FindBuildingAt(int x, int z)
        {
            BuildingPlacement best = null;
            foreach (var p in Buildings.Values)
            {
                var t = p.Template;
                if (t == null) continue;
                var fp = t.Footprint(p.Origin, p.Rotation);
                if (x >= fp.xMin && x < fp.xMax && z >= fp.yMin && z < fp.yMax)
                {
                    // Prefer the most recently placed building when they overlap.
                    if (best == null || p.Id > best.Id) best = p;
                }
            }
            return best;
        }

        // ------------------------------------------------------------------ queries

        public RoadNode FindNode(Vector2 p, float radius)
        {
            RoadNode best = null;
            float bestD = radius;
            foreach (var n in Nodes.Values)
            {
                float d = Vector2.Distance(n.Pos, p);
                if (d <= bestD) { bestD = d; best = n; }
            }
            return best;
        }

        /// <summary>Nearest segment whose carriageway (plus extra) contains p.</summary>
        public RoadSegment FindSegment(Vector2 p, float extra, out float t)
        {
            RoadSegment best = null;
            float bestD = float.MaxValue;
            t = 0;
            foreach (var s in Segments.Values)
            {
                var a = Nodes[s.A].Pos;
                var b = Nodes[s.B].Pos;
                float d = DistToSegment(p, a, b, out float st);
                if (d <= s.RoadType.CarriageHalf + extra && d < bestD)
                {
                    bestD = d; best = s; t = st;
                }
            }
            return best;
        }

        public static float DistToSegment(Vector2 p, Vector2 a, Vector2 b, out float t)
        {
            var ab = b - a;
            float l2 = ab.sqrMagnitude;
            t = l2 < 1e-6f ? 0f : Mathf.Clamp01(Vector2.Dot(p - a, ab) / l2);
            return Vector2.Distance(p, a + ab * t);
        }

        public static bool SegmentIntersection(Vector2 p1, Vector2 p2, Vector2 q1, Vector2 q2, out float t, out float u)
        {
            var r = p2 - p1;
            var s = q2 - q1;
            float denom = r.x * s.y - r.y * s.x;
            t = u = 0;
            if (Mathf.Abs(denom) < 1e-6f) return false;
            var qp = q1 - p1;
            t = (qp.x * s.y - qp.y * s.x) / denom;
            u = (qp.x * r.y - qp.y * r.x) / denom;
            return t > 0.0001f && t < 0.9999f && u > 0.0001f && u < 0.9999f;
        }

        /// <summary>Existing segments crossed by the line a-b (ignoring ones touching the given end nodes).</summary>
        public List<(int segId, float tNew, Vector2 point)> FindIntersections(Vector2 a, Vector2 b, int nodeA, int nodeB)
        {
            var result = new List<(int, float, Vector2)>();
            foreach (var s in Segments.Values)
            {
                if (s.A == nodeA || s.B == nodeA || s.A == nodeB || s.B == nodeB) continue;
                var p = Nodes[s.A].Pos;
                var q = Nodes[s.B].Pos;
                if (SegmentIntersection(a, b, p, q, out float t, out _))
                    result.Add((s.Id, t, a + (b - a) * t));
            }
            result.Sort((x, y) => x.Item2.CompareTo(y.Item2));
            return result;
        }

        /// <summary>A segment end is "smooth" if the road just continues there (a bend, not a junction).</summary>
        public bool IsSmoothEnd(int nodeId)
        {
            var n = Nodes[nodeId];
            return !n.IsRoundabout && n.Segments.Count == 2;
        }

        // ------------------------------------------------------------------ change areas

        private static RectInt Expand(RectInt r, int by) => new RectInt(r.xMin - by, r.yMin - by, r.width + by * 2, r.height + by * 2);

        private static RectInt Union(RectInt a, RectInt b)
        {
            if (a.width <= 0) return b;
            if (b.width <= 0) return a;
            int x0 = Math.Min(a.xMin, b.xMin), z0 = Math.Min(a.yMin, b.yMin);
            int x1 = Math.Max(a.xMax, b.xMax), z1 = Math.Max(a.yMax, b.yMax);
            return new RectInt(x0, z0, x1 - x0, z1 - z0);
        }

        private RectInt SegmentArea(RoadSegment s)
        {
            var a = Nodes[s.A].Pos;
            var b = Nodes[s.B].Pos;
            float m = s.RoadType.MaxHalf + 3;
            int x0 = Mathf.FloorToInt(Mathf.Min(a.x, b.x) - m), z0 = Mathf.FloorToInt(Mathf.Min(a.y, b.y) - m);
            int x1 = Mathf.CeilToInt(Mathf.Max(a.x, b.x) + m), z1 = Mathf.CeilToInt(Mathf.Max(a.y, b.y) + m);
            return new RectInt(x0, z0, x1 - x0, z1 - z0);
        }

        private RectInt NodeOnlyArea(RoadNode n)
        {
            float r = (n.IsRoundabout ? n.RoundaboutRadius : 0) + 5;
            return new RectInt(Mathf.FloorToInt(n.Pos.x - r), Mathf.FloorToInt(n.Pos.y - r), Mathf.CeilToInt(r * 2) + 1, Mathf.CeilToInt(r * 2) + 1);
        }

        /// <summary>Area covered by the given nodes and every segment touching them.</summary>
        private RectInt NodeArea(params int[] nodeIds)
        {
            var area = new RectInt(0, 0, 0, 0);
            foreach (int id in nodeIds)
            {
                if (!Nodes.TryGetValue(id, out var n)) continue;
                area = Union(area, NodeOnlyArea(n));
                foreach (int sid in n.Segments) area = Union(area, SegmentArea(Segments[sid]));
            }
            return area;
        }

        private void RaiseAround(params int[] nodeIds) => Raise(NodeArea(nodeIds));

        private void Raise(RectInt area)
        {
            if (area.width > 0 && area.height > 0) Changed?.Invoke(area);
        }

        /// <summary>Clears everything (used when loading a save).</summary>
        public void Clear()
        {
            Nodes.Clear();
            Segments.Clear();
            Buildings.Clear();
            NextId = 1;
        }

        // ------------------------------------------------------------------ snapshot for worker threads

        public CitySnapshot BuildSnapshot(RectInt area)
        {
            var snap = new CitySnapshot();
            var segs = new List<SegmentData>();
            var props = new List<PropData>();
            foreach (var s in Segments.Values)
            {
                var r = SegmentArea(s);
                if (!r.Overlaps(area)) continue;
                var na = Nodes[s.A];
                var nb = Nodes[s.B];
                var type = s.RoadType;
                float len = Vector2.Distance(na.Pos, nb.Pos);
                var crossings = new float[s.Crossings.Count];
                for (int i = 0; i < crossings.Length; i++) crossings[i] = s.Crossings[i] * len;
                segs.Add(new SegmentData
                {
                    A = na.Pos, B = nb.Pos, Type = type,
                    SmoothA = IsSmoothEnd(s.A), SmoothB = IsSmoothEnd(s.B),
                    Crossings = crossings
                });
                AddProps(s, na, nb, type, len, crossings, props);
            }
            snap.Segments = segs.ToArray();

            var rbs = new List<RoundaboutData>();
            foreach (var n in Nodes.Values)
                if (n.IsRoundabout && NodeOnlyArea(n).Overlaps(area))
                    rbs.Add(new RoundaboutData { Centre = n.Pos, Radius = n.RoundaboutRadius });
            snap.Roundabouts = rbs.ToArray();

            var props2 = new List<PropData>();
            foreach (var p in props)
                if (p.X >= area.xMin - 2 && p.X < area.xMax + 2 && p.Z >= area.yMin - 2 && p.Z < area.yMax + 2)
                    props2.Add(p);
            snap.Props = props2.ToArray();

            var bl = new List<PlacementData>();
            foreach (var p in Buildings.Values)
            {
                var t = p.Template;
                if (t == null) continue;
                var fp = t.Footprint(p.Origin, p.Rotation);
                if (!fp.Overlaps(area)) continue;
                bl.Add(new PlacementData { Template = t, Origin = p.Origin, Rotation = p.Rotation, Footprint = fp, Id = p.Id });
            }
            bl.Sort((x, y) => x.Id.CompareTo(y.Id));
            snap.Buildings = bl.ToArray();
            return snap;
        }

        private void AddProps(RoadSegment s, RoadNode na, RoadNode nb, RoadType type, float len, float[] crossings, List<PropData> props)
        {
            if (len < 1f) return;
            var dir = (nb.Pos - na.Pos) / len;
            var perp = new Vector2(-dir.y, dir.x);

            // Street lamps, staggered on alternate sides, kept clear of junctions.
            if (type.LampOffset > 0)
            {
                float startMargin = EndMargin(na), endMargin = EndMargin(nb);
                int i = 0;
                for (float along = startMargin; along <= len - endMargin; along += type.LampSpacing * 0.5f, i++)
                {
                    float side = (i % 2 == 0) ? 1f : -1f;
                    var pos = na.Pos + dir * along + perp * (side * type.LampOffset);
                    var toRoad = -perp * side;
                    var arm = Mathf.Abs(toRoad.x) >= Mathf.Abs(toRoad.y)
                        ? new Vector2Int(toRoad.x > 0 ? 1 : -1, 0)
                        : new Vector2Int(0, toRoad.y > 0 ? 1 : -1);
                    props.Add(new PropData { X = Mathf.FloorToInt(pos.x), Z = Mathf.FloorToInt(pos.y), Kind = PropKind.Lamp, Arm = arm });
                }
            }

            // Belisha beacons either side of every zebra crossing.
            if (type.HasPavement)
            {
                foreach (float c in crossings)
                {
                    for (int side = -1; side <= 1; side += 2)
                    {
                        var pos = na.Pos + dir * c + perp * (side * (type.CarriageHalf + 1.5f)) + dir * 2f;
                        props.Add(new PropData { X = Mathf.FloorToInt(pos.x), Z = Mathf.FloorToInt(pos.y), Kind = PropKind.Belisha });
                    }
                }
            }
        }

        private float EndMargin(RoadNode n)
        {
            if (n.IsRoundabout) return n.RoundaboutRadius + 6;
            if (n.Segments.Count >= 3) return 12;
            return 5;
        }
    }

    // ------------------------------------------------------------------ immutable snapshot types

    public enum PropKind : byte { Lamp, Belisha }

    public struct SegmentData
    {
        public Vector2 A, B;
        public RoadType Type;
        public bool SmoothA, SmoothB;
        /// <summary>Distances along the segment from A.</summary>
        public float[] Crossings;
    }

    public struct RoundaboutData
    {
        public Vector2 Centre;
        public float Radius;
    }

    public struct PlacementData
    {
        public int Id;
        public BuildingTemplate Template;
        public Vector3Int Origin;
        public int Rotation;
        public RectInt Footprint;
    }

    public struct PropData
    {
        public int X, Z;
        public PropKind Kind;
        public Vector2Int Arm;
    }

    /// <summary>A read-only copy of the city near one chunk, handed to a worker thread.</summary>
    public sealed class CitySnapshot
    {
        public SegmentData[] Segments = Array.Empty<SegmentData>();
        public RoundaboutData[] Roundabouts = Array.Empty<RoundaboutData>();
        public PlacementData[] Buildings = Array.Empty<PlacementData>();
        public PropData[] Props = Array.Empty<PropData>();
    }
}
