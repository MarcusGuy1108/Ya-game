using UnityEngine;
using UKCity.World;

namespace UKCity.City
{
    /// <summary>
    /// A small demo town laid out through the same API the planner uses: an A-road with a roundabout,
    /// a high street with shops, a pub and a zebra crossing, residential streets of terraces and semis,
    /// a church, a tower block and a park.
    /// </summary>
    public static class StarterTown
    {
        private const int S = WorldConst.SurfaceY;

        public static void Build(CityLayer city)
        {
            var w = city.AddNode(new Vector2(-160.5f, 0.5f));
            var r = city.AddNode(new Vector2(0.5f, 0.5f));
            var e = city.AddNode(new Vector2(160.5f, 0.5f));
            var j = city.AddNode(new Vector2(0.5f, 70.5f));
            var n = city.AddNode(new Vector2(0.5f, 150.5f));
            var s = city.AddNode(new Vector2(0.5f, -120.5f));
            var rw = city.AddNode(new Vector2(-110.5f, 70.5f));
            var re = city.AddNode(new Vector2(110.5f, 70.5f));

            int main = RoadTypes.MainRoad.Id, high = RoadTypes.HighStreet.Id, res = RoadTypes.Residential.Id;
            city.AddSegment(w.Id, r.Id, main);
            city.AddSegment(r.Id, e.Id, main);
            var highSouth = city.AddSegment(r.Id, j.Id, high);
            city.AddSegment(j.Id, n.Id, high);
            city.AddSegment(r.Id, s.Id, res);
            city.AddSegment(rw.Id, j.Id, res);
            city.AddSegment(j.Id, re.Id, res);
            city.SetRoundabout(r.Id, 14f);
            city.AddCrossing(highSouth.Id, (40.5f - 0.5f) / 70f);

            // High street, west side (fronts face east).
            Put(city, "corner_shop", -10, 26, 3);
            Put(city, "corner_shop", -10, 34, 3);
            Put(city, "pub", -10, 46, 3);
            Put(city, "corner_shop", -10, 58, 3);
            Put(city, "phone_box", -8, 38, 3);
            Put(city, "pillar_box", -8, 42, 3);
            // High street, east side (fronts face west).
            Put(city, "terrace_row", 10, 32, 1);
            Put(city, "corner_shop", 10, 50, 1);
            Put(city, "bus_shelter", 7, 58, 1);

            // North end.
            Put(city, "church", -10, 100, 3);
            Put(city, "tower_block", 10, 100, 1);
            Put(city, "park", 10, 128, 1);
            Put(city, "terrace_row", -10, 125, 3);

            // Residential street crossing the high street.
            foreach (int c in new[] { -25, -46, -67, -88 }) Put(city, "terrace_row", c, 77, 0);
            foreach (int c in new[] { 26, 47, 68, 89 }) Put(city, "terrace_row", c, 77, 0);
            foreach (int c in new[] { -32, -50, -68, -86, -104 }) Put(city, "semi_pair", c, 63, 2);
            foreach (int c in new[] { 33, 51, 69, 87 }) Put(city, "semi_pair", c, 63, 2);

            // Residential road south of the roundabout.
            foreach (int z in new[] { -35, -57, -79, -101 })
            {
                Put(city, "terrace_row", -7, z, 3);
                Put(city, "terrace_row", 7, z, 1);
            }
        }

        private static void Put(CityLayer city, string id, int x, int z, int rot) =>
            city.AddBuilding(id, new Vector3Int(x, S, z), rot);
    }
}
