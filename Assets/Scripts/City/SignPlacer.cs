using UnityEngine;
using UKCity.World;

namespace UKCity.City
{
    /// <summary>Where a sign template goes when dropped beside a road.</summary>
    public struct SignPlacement
    {
        public bool OnRoad;
        public Vector3Int Origin;
        public int Rotation;
    }

    public static class SignPlacer
    {
        /// <summary>
        /// Puts a sign at the edge of the nearest road (within 30 m), facing the traffic that drives on that side,
        /// on the left as in the UK. Gantries span the carriageway out from the central reservation.
        /// </summary>
        public static SignPlacement Place(CityLayer city, BuildingTemplate t, Vector2 point)
        {
            RoadSegment best = null;
            float bestD = 30f, bestT = 0;
            foreach (var s in city.Segments.Values)
            {
                float d = CityLayer.DistToSegment(point, city.Nodes[s.A].Pos, city.Nodes[s.B].Pos, out float st);
                if (d < bestD) { bestD = d; best = s; bestT = st; }
            }
            if (best == null)
                return new SignPlacement { Origin = new Vector3Int(Mathf.FloorToInt(point.x), WorldConst.SurfaceY, Mathf.FloorToInt(point.y)) };

            var a = city.Nodes[best.A].Pos;
            var b = city.Nodes[best.B].Pos;
            var dir = (b - a).normalized;
            var left = new Vector2(-dir.y, dir.x);
            float side = Vector2.Dot(point - a, left) >= 0 ? 1f : -1f;
            var type = best.RoadType;
            float offset = t.SpansCarriageway ? 1f : type.CarriageHalf + (type.HasKerb ? 1f : 0f) + 0.5f;
            var p = Vector2.Lerp(a, b, bestT) + left * (side * offset);
            // Traffic on the left-hand side of A->B travels towards B, so it comes from A.
            var face = side > 0 ? -dir : dir;
            int rot = 0;
            float bestDot = float.MinValue;
            for (int r = 0; r < 4; r++)
            {
                var fd = BuildingTemplate.FrontDirection(r);
                float dot = fd.x * face.x + fd.y * face.y;
                if (dot > bestDot) { bestDot = dot; rot = r; }
            }
            return new SignPlacement
            {
                OnRoad = true,
                Origin = new Vector3Int(Mathf.FloorToInt(p.x), WorldConst.SurfaceY, Mathf.FloorToInt(p.y)),
                Rotation = rot
            };
        }
    }
}
