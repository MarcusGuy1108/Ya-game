using System.Collections.Generic;
using UnityEngine;

namespace UKCity.City
{
    /// <summary>What a strip of a road's cross-section is made of, in increasing priority order.</summary>
    public enum SurfaceKind : byte
    {
        None = 0,
        Hedge = 10,
        Verge = 20,
        ArmcoVerge = 21,
        Pavement = 30,
        Tactile = 35,
        Kerb = 40,
        Barrier = 44,
        Reservation = 45,
        Asphalt = 50,
        Island = 100,
        IslandKerb = 101,
    }

    public enum MarkingKind : byte
    {
        None, WhiteDashed, WhiteSolid, WhiteThick, DoubleYellow, SingleYellow, Paint,
        GiveWay, GiveWaySingle, Stop, Zigzag, Studs, Box, Triangle, ArrowAhead, Hatch
    }

    /// <summary>A symmetric band of a road cross-section, measured outward from the centre line.</summary>
    public readonly struct Band
    {
        public readonly float From, To;
        public readonly SurfaceKind Kind;
        public readonly MarkingKind Marking;
        public readonly float Dash, Gap;

        public Band(float from, float to, SurfaceKind kind, MarkingKind marking = MarkingKind.None, float dash = 0, float gap = 0)
        {
            From = from; To = to; Kind = kind; Marking = marking; Dash = dash; Gap = gap;
        }
    }

    public sealed class RoadType
    {
        public int Id;
        public string Name;
        public string Description;
        public Band[] Bands;
        public Color MapColour;
        /// <summary>Distance from centre to the outer edge of the carriageway (tarmac).</summary>
        public float CarriageHalf;
        /// <summary>Distance from centre to the outer edge of everything.</summary>
        public float MaxHalf;
        /// <summary>Width of the footway/verge beyond the kerb (used for rounded junction corners).</summary>
        public float OuterWidth;
        public bool HasKerb;
        /// <summary>Lateral offset for automatic street lamps (0 = none).</summary>
        public float LampOffset;
        public float LampSpacing = 26f;
        public bool HasPavement;
        /// <summary>Has a dashed centre line that turns into a hazard line near junctions.</summary>
        public bool CentreLine;
        /// <summary>Junction priority: the highest ranked road through a junction gets right of way.</summary>
        public int Rank;
        /// <summary>Number of lanes in each direction (for future traffic).</summary>
        public int LanesEachWay = 1;
        public bool Dual;

        public bool TryGetBand(float dist, out Band band)
        {
            for (int i = 0; i < Bands.Length; i++)
            {
                if (dist >= Bands[i].From && dist < Bands[i].To) { band = Bands[i]; return true; }
            }
            band = default;
            return false;
        }
    }

    /// <summary>
    /// UK road types. Widths are odd so the centre line sits on a whole block.
    /// Markings follow UK practice and everything is laid out for left-hand traffic.
    /// </summary>
    public static class RoadTypes
    {
        public static readonly List<RoadType> All = new List<RoadType>();

        public static readonly RoadType Residential;
        public static readonly RoadType MainRoad;
        public static readonly RoadType HighStreet;
        public static readonly RoadType DualCarriageway;
        public static readonly RoadType CountryLane;
        public static readonly RoadType RuralA;
        public static readonly RoadType Motorway;

        static RoadTypes()
        {
            Residential = Add("Residential Street", "Two lanes, dashed centre line, pavements both sides.", 1,
                new Color(0.95f, 0.95f, 0.95f),
                new Band(0f, 0.5f, SurfaceKind.Asphalt, MarkingKind.WhiteDashed, 2, 4),
                new Band(0.5f, 3.5f, SurfaceKind.Asphalt),
                new Band(3.5f, 4.5f, SurfaceKind.Kerb),
                new Band(4.5f, 6.5f, SurfaceKind.Pavement));
            Residential.LampOffset = 6f;

            MainRoad = Add("A-Road (urban)", "Wide two-lane road with edge lines and wide pavements.", 3,
                new Color(1f, 0.55f, 0.45f),
                new Band(0f, 0.5f, SurfaceKind.Asphalt, MarkingKind.WhiteDashed, 3, 6),
                new Band(0.5f, 3.5f, SurfaceKind.Asphalt),
                new Band(3.5f, 4.5f, SurfaceKind.Asphalt, MarkingKind.WhiteSolid),
                new Band(4.5f, 5.5f, SurfaceKind.Kerb),
                new Band(5.5f, 8.5f, SurfaceKind.Pavement));
            MainRoad.LampOffset = 7.5f;

            HighStreet = Add("High Street", "Double yellow lines (no parking) and broad pavements for shops.", 2,
                new Color(1f, 0.85f, 0.35f),
                new Band(0f, 0.5f, SurfaceKind.Asphalt, MarkingKind.WhiteDashed, 2, 4),
                new Band(0.5f, 3.5f, SurfaceKind.Asphalt),
                new Band(3.5f, 4.5f, SurfaceKind.Asphalt, MarkingKind.DoubleYellow),
                new Band(4.5f, 5.5f, SurfaceKind.Kerb),
                new Band(5.5f, 9.5f, SurfaceKind.Pavement));
            HighStreet.LampOffset = 8.5f;
            HighStreet.LampSpacing = 20f;

            DualCarriageway = Add("Dual Carriageway", "Two lanes each way split by a grass central reservation.", 4,
                new Color(0.45f, 0.75f, 1f),
                new Band(0f, 0.5f, SurfaceKind.Reservation),
                new Band(0.5f, 1.5f, SurfaceKind.Kerb),
                new Band(1.5f, 2.5f, SurfaceKind.Asphalt, MarkingKind.WhiteSolid),
                new Band(2.5f, 4.5f, SurfaceKind.Asphalt),
                new Band(4.5f, 5.5f, SurfaceKind.Asphalt, MarkingKind.WhiteDashed, 3, 6),
                new Band(5.5f, 8.5f, SurfaceKind.Asphalt),
                new Band(8.5f, 9.5f, SurfaceKind.Asphalt, MarkingKind.WhiteSolid),
                new Band(9.5f, 10.5f, SurfaceKind.Kerb),
                new Band(10.5f, 12.5f, SurfaceKind.Verge));
            DualCarriageway.LanesEachWay = 2;
            DualCarriageway.Dual = true;
            DualCarriageway.LampOffset = 11.5f;
            DualCarriageway.LampSpacing = 36f;

            CountryLane = Add("Country Lane", "Narrow single-track lane with grass verges and hedgerows.", 0,
                new Color(0.6f, 0.9f, 0.5f),
                new Band(0f, 2.5f, SurfaceKind.Asphalt),
                new Band(2.5f, 3.5f, SurfaceKind.Verge),
                new Band(3.5f, 4.5f, SurfaceKind.Hedge));

            RuralA = Add("A-Road (rural)", "Single carriageway with edge lines and grass verges, unlit.", 3,
                new Color(0.4f, 0.9f, 0.6f),
                new Band(0f, 0.5f, SurfaceKind.Asphalt, MarkingKind.WhiteDashed, 3, 6),
                new Band(0.5f, 3.5f, SurfaceKind.Asphalt),
                new Band(3.5f, 4.5f, SurfaceKind.Asphalt, MarkingKind.WhiteSolid),
                new Band(4.5f, 7.5f, SurfaceKind.Verge));

            Motorway = Add("Motorway", "Three lanes each way, hard shoulders, central concrete barrier.", 5,
                new Color(0.3f, 0.55f, 1f),
                new Band(0f, 0.5f, SurfaceKind.Barrier),
                new Band(0.5f, 1.5f, SurfaceKind.Asphalt),
                new Band(1.5f, 2.5f, SurfaceKind.Asphalt, MarkingKind.WhiteSolid),
                new Band(2.5f, 5.5f, SurfaceKind.Asphalt),
                new Band(5.5f, 6.5f, SurfaceKind.Asphalt, MarkingKind.WhiteDashed, 3, 6),
                new Band(6.5f, 9.5f, SurfaceKind.Asphalt),
                new Band(9.5f, 10.5f, SurfaceKind.Asphalt, MarkingKind.WhiteDashed, 3, 6),
                new Band(10.5f, 13.5f, SurfaceKind.Asphalt),
                new Band(13.5f, 14.5f, SurfaceKind.Asphalt, MarkingKind.WhiteThick),
                new Band(14.5f, 17.5f, SurfaceKind.Asphalt),
                new Band(17.5f, 18.5f, SurfaceKind.Verge),
                new Band(18.5f, 19.5f, SurfaceKind.ArmcoVerge),
                new Band(19.5f, 22.5f, SurfaceKind.Verge));
            Motorway.LanesEachWay = 3;
            Motorway.Dual = true;
        }

        private static RoadType Add(string name, string desc, int rank, Color col, params Band[] bands)
        {
            var t = new RoadType { Id = All.Count, Name = name, Description = desc, Bands = bands, MapColour = col, Rank = rank };
            foreach (var b in bands)
            {
                if (b.Kind == SurfaceKind.Asphalt) t.CarriageHalf = Mathf.Max(t.CarriageHalf, b.To);
                if (b.Kind == SurfaceKind.Pavement) t.HasPavement = true;
                if (b.From == 0f && b.Marking == MarkingKind.WhiteDashed) t.CentreLine = true;
                t.MaxHalf = Mathf.Max(t.MaxHalf, b.To);
            }
            foreach (var b in bands)
            {
                if (b.From < t.CarriageHalf) continue;
                if (b.Kind == SurfaceKind.Kerb) t.HasKerb = true;
                else if (b.Kind == SurfaceKind.Pavement || b.Kind == SurfaceKind.Verge) t.OuterWidth += b.To - b.From;
            }
            All.Add(t);
            return t;
        }

        public static RoadType Get(int id) => id >= 0 && id < All.Count ? All[id] : Residential;

        public static float MaxHalfWidth
        {
            get
            {
                float m = 0;
                foreach (var t in All) m = Mathf.Max(m, t.MaxHalf);
                return m;
            }
        }
    }
}
