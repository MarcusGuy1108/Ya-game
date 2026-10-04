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
            city.SetSignals(j.Id, true);
            city.SetYellowBox(j.Id, true);
            var mainEast = city.FindSegmentBetween(r.Id, e.Id);
            city.AddCrossing(mainEast.Id, (60f - 0.5f) / 160f, CrossingKind.Signal);

            // A stretch of motorway to the east with gantries.
            var m1 = city.AddNode(new Vector2(240.5f, -220.5f));
            var m2 = city.AddNode(new Vector2(240.5f, 320.5f));
            city.AddSegment(m1.Id, m2.Id, RoadTypes.Motorway.Id);

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

            // Signs and street furniture, each turned to face the traffic it is for.
            Sign(city, "sign_welcome", -125, 12);
            Sign(city, "sign_limit_30", -112, 10);
            Sign(city, "sign_nsl", -112, -10);
            Sign(city, "sign_speed_camera", -95, 10);
            Sign(city, "speed_camera", -80, 10);
            Sign(city, "sign_roundabout_ahead", -55, 10);
            Sign(city, "sign_limit_30", 118, -10);
            Sign(city, "sign_nsl", 118, 10);
            Sign(city, "vas", 95, -10);
            Sign(city, "sign_roundabout_ahead", 55, -10);
            Sign(city, "sign_signals_ahead", -7.5f, 29);
            Sign(city, "school_warning", 8, -32);
            Sign(city, "sign_children", -8, -95);
            Sign(city, "sign_parking", -8, 49);
            Sign(city, "cctv", 8, 45);
            Sign(city, "sign_dir_town_centre", 40, 63);
            Sign(city, "sign_street_high", -8, 18);
            Sign(city, "sign_street_station", 30, 78);
            Put(city, "litter_bin", -8, 30, 0);
            Put(city, "bench", -8, 52, 3);
            Put(city, "litter_bin", 7, 47, 0);
            Put(city, "grit_bin", -9, 66, 0);
            Put(city, "cabinet", 9, 76, 1);
            Put(city, "cycle_stand", -8, 22, 3);

            // Motorway furniture.
            Sign(city, "gantry_smart", 245, 40);
            Sign(city, "gantry_smart", 236, 60);
            Sign(city, "gantry_direction", 245, 150);
            Sign(city, "gantry_direction", 236, 170);
            Sign(city, "sign_motorway_advance", 250, -40);
            Sign(city, "sign_countdown_3", 250, 75);
            Sign(city, "sign_countdown_2", 250, 95);
            Sign(city, "sign_countdown_1", 250, 115);
            Sign(city, "sign_emergency_area", 250, 10);
            Sign(city, "sos_phone", 250, 12);

            // Residential road south of the roundabout.
            foreach (int z in new[] { -35, -57, -79, -101 })
            {
                Put(city, "terrace_row", -7, z, 3);
                Put(city, "terrace_row", 7, z, 1);
            }
        }

        private static void Put(CityLayer city, string id, int x, int z, int rot) =>
            city.AddBuilding(id, new Vector3Int(x, S, z), rot);

        private static void Sign(CityLayer city, string id, float x, float z)
        {
            var t = BuildingLibrary.Get(id);
            if (t == null) { UnityEngine.Debug.LogWarning("Starter town: missing template " + id); return; }
            var sp = SignPlacer.Place(city, t, new Vector2(x, z));
            city.AddBuilding(id, sp.Origin, sp.Rotation);
        }
    }
}
