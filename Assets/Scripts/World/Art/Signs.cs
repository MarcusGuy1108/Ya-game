using System;
using System.Collections.Generic;
using UnityEngine;

namespace UKCity.World.Art
{
    public enum SignMount : byte { Post, Gantry, Legs }

    /// <summary>One road sign design. Multi-block signs are split into W x H panel blocks.</summary>
    public sealed class SignDef
    {
        public string Id;
        public string Name;
        public string Category;
        public int W = 1, H = 1;
        public SignMount Mount = SignMount.Post;
        public Action<PixelCanvas> Paint;
        /// <summary>Electronic message sign: each entry is one message (lines of text).</summary>
        public string[][] Messages;
        /// <summary>Block id for each part [x, y from bottom].</summary>
        public int[,] Parts;
        public int LegacyId;

        public int PostHeight => Mount == SignMount.Gantry ? 0 : Mount == SignMount.Legs ? 1 : (H >= 2 ? 1 : 2);
    }

    /// <summary>
    /// The UK sign catalogue: speed limits, zones, regulatory, warning, school, information, direction, motorway and
    /// electronic message signs. Each is painted from vector shapes and the SDF font, then sliced into 64px tiles.
    /// </summary>
    public static class Signs
    {
        public const int Px = 64;
        public static readonly List<SignDef> All = new List<SignDef>();
        private static readonly Dictionary<string, SignDef> byId = new Dictionary<string, SignDef>();
        private static readonly Dictionary<string, PixelCanvas> painted = new Dictionary<string, PixelCanvas>();

        public static readonly string[] Categories =
            { "Speed limits", "Regulatory", "Warning", "School", "Information", "Direction", "Motorway" };

        // UK sign colours.
        private static readonly Rgba Red = Rgba.Hex("c8102e");
        private static readonly Rgba Blue = Rgba.Hex("0b52a1");
        private static readonly Rgba Green = Rgba.Hex("00703c");
        private static readonly Rgba Brown = Rgba.Hex("6e3a1e");
        private static readonly Rgba Yellow = Rgba.Hex("ffd100");
        private static readonly Rgba Orange = Rgba.Hex("f07d00");
        private static readonly Rgba W = Rgba.Hex("f6f6f2");
        private static readonly Rgba K = Rgba.Hex("111214");
        private static readonly Rgba Amber = Rgba.Hex("ffb020");

        public static SignDef Get(string id) => byId.TryGetValue(id, out var s) ? s : null;

        static Signs()
        {
            // ---------------------------------------------------------- speed limits
            foreach (var n in new[] { "20", "30", "40", "50", "60", "70" })
                Add($"limit_{n}", $"{n} mph Limit", "Speed limits", c => Roundel(c, n), n == "30" ? BlockIds.Sign30 : 0);
            Add("nsl", "National Speed Limit", "Speed limits", c =>
            {
                c.Circle(32, 32, 30, K.Scale(0.6f));
                c.Circle(32, 32, 29, W);
                c.Bar(50, 10, 14, 54, 12, K);
            });
            Add("zone_20", "20 Zone", "Speed limits", c =>
            {
                c.RoundRect(6, 1, 52, 62, 4, W);
                Frame(c, 6, 1, 52, 62, 1.5f, K);
                c.Ring(32, 23, 14, 19, Red);
                c.Circle(32, 23, 14, W);
                c.TextFit("20", 32, 29.5f, 13, 20, K);
                c.TextFit("ZONE", 32, 56, 11, 44, K);
            });
            Add("end_20_zone", "End of 20 Zone", "Speed limits", c =>
            {
                c.RoundRect(6, 1, 52, 62, 4, W);
                Frame(c, 6, 1, 52, 62, 1.5f, K);
                c.Ring(32, 23, 14, 19, K.Scale(1.4f));
                c.TextFit("20", 32, 29.5f, 13, 20, K.Scale(1.6f));
                c.Bar(46, 8, 18, 38, 3, K);
                c.TextFit("ZONE", 32, 56, 11, 44, K);
            });

            // ---------------------------------------------------------- regulatory
            Add("stop", "Stop", "Regulatory", c =>
            {
                c.RegularPolygon(32, 32, 31, 8, Mathf.PI / 8, W);
                c.RegularPolygon(32, 32, 28.5f, 8, Mathf.PI / 8, Red);
                c.TextFit("STOP", 32, 39, 15, 44, W);
            });
            Add("give_way", "Give Way", "Regulatory", c =>
            {
                c.Triangle(32, 30, 62, false, Red);
                c.Triangle(32, 28, 42, false, W);
                c.TextFit("GIVE", 32, 21, 7, 26, K);
                c.TextFit("WAY", 32, 31, 7, 20, K);
            }, BlockIds.SignGiveWay);
            Add("no_entry", "No Entry", "Regulatory", c =>
            {
                c.Circle(32, 32, 30, Red);
                c.Rect(10, 27, 44, 10, W);
            }, BlockIds.SignNoEntry);
            Add("no_right", "No Right Turn", "Regulatory", c => Prohibit(c, p => TurnArrow(p, true), true));
            Add("no_left", "No Left Turn", "Regulatory", c => Prohibit(c, p => TurnArrow(p, false), true));
            Add("no_uturn", "No U-turn", "Regulatory", c => Prohibit(c, UTurn, true));
            Add("no_overtaking", "No Overtaking", "Regulatory", c => Prohibit(c, p =>
            {
                CarSide(p, 3, 9, Red);
                CarSide(p, 14, 9, K);
            }, false));
            Add("no_vehicles", "No Vehicles", "Regulatory", c => Prohibit(c, p => { }, false));
            Add("no_motor_vehicles", "No Motor Vehicles", "Regulatory", c => Prohibit(c, p =>
            {
                CarSide(p, 6, 4, K);
                Bike(p, 12, 17, 0.7f, K);
            }, false));
            Add("no_cycling", "No Cycling", "Regulatory", c => Prohibit(c, p => Bike(p, 12, 12, 1f, K), false));
            Add("no_pedestrians", "No Pedestrians", "Regulatory", c => Prohibit(c, p => Person(p, 12, 12, 1.1f, K, true), false));
            Add("weight_limit", "7.5 Tonne Limit", "Regulatory", c =>
            {
                c.Circle(32, 32, 30, Red);
                c.Circle(32, 32, 23, W);
                c.TextFit("7.5T", 32, 39, 14, 36, K);
            });
            Add("no_waiting", "No Waiting", "Regulatory", c =>
            {
                c.Circle(32, 32, 30, Red);
                c.Circle(32, 32, 24, Blue);
                c.Bar(15, 15, 49, 49, 6, Red);
            });
            Add("clearway", "No Stopping (Clearway)", "Regulatory", c =>
            {
                c.Circle(32, 32, 30, Red);
                c.Circle(32, 32, 24, Blue);
                c.Bar(15, 15, 49, 49, 6, Red);
                c.Bar(49, 15, 15, 49, 6, Red);
            });
            Add("ahead_only", "Ahead Only", "Regulatory", c => Mandatory(c, p => Arrow(p, 12, 21, 12, 3, 4.5f, W)));
            Add("turn_left", "Turn Left Ahead", "Regulatory", c => Mandatory(c, p =>
            {
                p.Line(14, 21, 14, 11, 4.5f, W);
                Arrow(p, 15, 11, 4, 11, 4.5f, W);
            }));
            Add("turn_right", "Turn Right Ahead", "Regulatory", c => Mandatory(c, p =>
            {
                p.Line(10, 21, 10, 11, 4.5f, W);
                Arrow(p, 9, 11, 20, 11, 4.5f, W);
            }));
            Add("keep_left", "Keep Left", "Regulatory", c => Mandatory(c, p => Arrow(p, 18, 6, 6, 18, 4.5f, W)));
            Add("keep_right", "Keep Right", "Regulatory", c => Mandatory(c, p => Arrow(p, 6, 6, 18, 18, 4.5f, W)));
            Add("mini_roundabout", "Mini Roundabout", "Regulatory", c => Mandatory(c, p => RoundaboutArrows(p, W)));
            Add("cycle_route", "Route for Cycles", "Regulatory", c => Mandatory(c, p => Bike(p, 12, 13, 1f, W)));
            Add("one_way_left", "One Way (left)", "Regulatory", c =>
            {
                c.Rect(2, 14, 60, 36, Blue);
                Frame(c, 2, 14, 60, 36, 1.5f, W);
                var p = new Pen(c, 32, 32, 2.2f);
                Arrow(p, 22, 12, 2, 12, 5.5f, W);
            });
            Add("one_way_right", "One Way (right)", "Regulatory", c =>
            {
                c.Rect(2, 14, 60, 36, Blue);
                Frame(c, 2, 14, 60, 36, 1.5f, W);
                var p = new Pen(c, 32, 32, 2.2f);
                Arrow(p, 2, 12, 22, 12, 5.5f, W);
            });
            Add("bus_lane", "Bus Lane", "Regulatory", c =>
            {
                c.Rect(2, 2, 60, 60, Blue);
                Frame(c, 2, 2, 60, 60, 1.5f, W);
                var p = new Pen(c, 24, 28, 1.6f);
                BusSide(p, W);
                c.Bar(50, 54, 50, 10, 4, W);
                c.Polygon(W, 44, 16, 56, 16, 50, 6);
                c.TextFit("Mon-Fri 7-10am", 32, 58, 5, 56, W);
            });

            // ---------------------------------------------------------- warning triangles
            Warn("crossroads", "Crossroads", p => { p.Bar(12, 3, 12, 21, 4, K); p.Bar(3, 12, 21, 12, 4, K); });
            Warn("t_junction", "T-junction", p => { p.Bar(3, 7, 21, 7, 4, K); p.Bar(12, 7, 12, 21, 4, K); });
            Warn("side_road", "Side Road", p => { p.Bar(12, 3, 12, 21, 4, K); p.Bar(12, 12, 4, 12, 3.5f, K); });
            Warn("staggered", "Staggered Junction", p =>
            {
                p.Bar(12, 3, 12, 21, 4, K); p.Bar(12, 8, 4, 8, 3.5f, K); p.Bar(12, 16, 20, 16, 3.5f, K);
            });
            Warn("roundabout_ahead", "Roundabout Ahead", p => RoundaboutArrows(p, K));
            Warn("bend_left", "Bend to Left", p => Curve(p, false));
            Warn("bend_right", "Bend to Right", p => Curve(p, true));
            Warn("double_bend", "Double Bend", p =>
            {
                p.Line(14, 21, 14, 16, 3.5f, K); p.Line(14, 16, 8, 12, 3.5f, K);
                p.Line(8, 12, 15, 7, 3.5f, K); Arrow(p, 15, 8, 11, 2, 3.5f, K);
            });
            Warn("road_narrows", "Road Narrows", p =>
            {
                p.Line(6, 21, 9, 12, 3, K); p.Line(9, 12, 9, 3, 3, K);
                p.Line(18, 21, 15, 12, 3, K); p.Line(15, 12, 15, 3, 3, K);
            });
            Warn("signals_ahead", "Traffic Signals Ahead", p =>
            {
                p.RoundRect(7.5f, 1, 9, 22, 2, K);
                p.Circle(12, 5.5f, 2.6f, Red);
                p.Circle(12, 12, 2.6f, Amber);
                p.Circle(12, 18.5f, 2.6f, Rgba.Hex("00a650"));
            });
            Warn("ped_crossing", "Pedestrian Crossing", p =>
            {
                Person(p, 12, 10, 1f, K, true);
                for (int i = 0; i < 4; i++) p.Rect(3 + i * 5, 20, 3, 3, K);
            });
            Warn("children", "Children (School)", p =>
            {
                Person(p, 8, 12, 1.05f, K, true);
                Person(p, 16, 14, 0.8f, K, true);
            });
            Warn("cycles", "Cycles", p => Bike(p, 12, 13, 1f, K));
            Warn("slippery", "Slippery Road", p =>
            {
                CarBack(p, 12, 7, K);
                p.Line(7, 14, 10, 17, 1.6f, K); p.Line(10, 17, 6, 20, 1.6f, K);
                p.Line(17, 14, 14, 17, 1.6f, K); p.Line(14, 17, 18, 20, 1.6f, K);
            });
            Warn("humps", "Road Humps", p =>
            {
                p.Rect(2, 17, 20, 2.5f, K);
                p.Polygon(K, 6, 17, 9, 12, 15, 12, 18, 17);
            });
            Warn("roadworks", "Road Works", p =>
            {
                Person(p, 9, 10, 1f, K, false);
                p.Line(12, 9, 19, 17, 2f, K);
                p.Polygon(K, 14, 22, 23, 22, 20, 17);
            });
            Warn("deer", "Wild Animals", p =>
            {
                p.Polygon(K, 4, 12, 15, 9, 18, 4, 21, 5, 19, 10, 17, 14, 19, 21, 17, 21, 14, 15, 8, 15, 6, 21, 4, 21, 5, 15);
                p.Line(18, 4, 16, 1, 1.2f, K); p.Line(19, 4, 22, 1, 1.2f, K);
            });
            Warn("cattle", "Cattle", p =>
            {
                p.RoundRect(4, 8, 14, 8, 2, K);
                p.Polygon(K, 17, 8, 22, 7, 22, 12, 18, 13);
                p.Rect(5, 15, 2, 6, K); p.Rect(15, 15, 2, 6, K);
            });
            Warn("two_way", "Two-way Traffic", p =>
            {
                Arrow(p, 8, 20, 8, 3, 3.2f, K);
                Arrow(p, 16, 4, 16, 21, 3.2f, K);
            });
            Warn("queues", "Queues Likely", p =>
            {
                for (int i = 0; i < 3; i++) CarBack(p, 12, 4 + i * 7, K, 0.75f);
            });
            Warn("uneven", "Uneven Road", p =>
            {
                p.Line(2, 17, 6, 13, 2.5f, K); p.Line(6, 13, 10, 17, 2.5f, K);
                p.Line(10, 17, 14, 13, 2.5f, K); p.Line(14, 13, 18, 17, 2.5f, K); p.Line(18, 17, 22, 13, 2.5f, K);
            });
            Warn("low_bridge", "Low Bridge", p =>
            {
                p.Rect(2, 6, 20, 4, K);
                p.TextFit("14'6\"", 12, 20, 6, 20, K);
            });
            Warn("falling_rocks", "Falling Rocks", p =>
            {
                p.Polygon(K, 3, 3, 10, 3, 10, 21, 3, 21);
                p.Circle(15, 10, 2.2f, K); p.Circle(18, 15, 1.6f, K); p.Circle(14, 19, 2.6f, K);
            });

            // ---------------------------------------------------------- school
            Add("school_patrol", "School Crossing Patrol", "School", c =>
            {
                c.Circle(32, 32, 30, Red);
                c.Circle(32, 32, 25, W);
                c.TextFit("STOP", 32, 30, 11, 34, K);
                var p = new Pen(c, 32, 46, 0.62f);
                Person(p, 8, 12, 1.05f, K, true);
                Person(p, 16, 14, 0.8f, K, true);
            });
            Plate("plate_school", "School (plate)", "School", "School");
            Plate("plate_patrol", "Patrol (plate)", "School", "Patrol");
            Add("school_keep_clear", "School Keep Clear", "School", c =>
            {
                c.Rect(4, 8, 56, 48, W);
                Frame(c, 4, 8, 56, 48, 1.5f, K);
                c.TextFit("SCHOOL", 32, 26, 8, 48, K);
                c.TextFit("KEEP", 32, 38, 8, 48, K);
                c.TextFit("CLEAR", 32, 50, 8, 48, K);
            });
            Add("school_20_when_lights", "20 When Lights Flash", "School", c =>
            {
                c.Rect(4, 2, 56, 60, W);
                Frame(c, 4, 2, 56, 60, 1.5f, K);
                c.Ring(32, 22, 12, 16, Red);
                c.TextFit("20", 32, 27.5f, 11, 16, K);
                c.TextFit("when lights", 32, 48, 6.5f, 50, K);
                c.TextFit("show", 32, 57, 6.5f, 50, K);
            });

            // ---------------------------------------------------------- plates
            Plate("plate_except_access", "Except for Access (plate)", "Regulatory", "Except for access");
            Plate("plate_ahead", "Ahead (plate)", "Warning", "Ahead");
            Plate("plate_humps", "Humps for 400 yds (plate)", "Warning", "Humps for 400 yds");
            Plate("plate_end", "End (plate)", "Regulatory", "End");

            // ---------------------------------------------------------- information
            Add("parking", "Parking", "Information", c =>
            {
                c.RoundRect(3, 3, 58, 58, 4, Blue);
                Frame(c, 3, 3, 58, 58, 1.5f, W);
                c.TextFit("P", 32, 48, 34, 40, W);
            });
            Add("hospital", "Hospital", "Information", c =>
            {
                c.RoundRect(3, 3, 58, 58, 4, Blue);
                Frame(c, 3, 3, 58, 58, 1.5f, W);
                c.TextFit("H", 32, 48, 34, 40, W);
            });
            Add("speed_camera", "Speed Camera", "Information", c =>
            {
                c.Rect(3, 10, 58, 44, W);
                Frame(c, 3, 10, 58, 44, 1.5f, K);
                c.RoundRect(14, 22, 30, 20, 3, K);
                c.Circle(29, 32, 7, W);
                c.Circle(29, 32, 4.5f, K);
                c.Rect(20, 17, 10, 6, K);
                c.Rect(44, 26, 6, 12, K);
            });
            Add("bus_stop", "Bus Stop Flag", "Information", c =>
            {
                c.Rect(4, 2, 56, 60, W);
                Frame(c, 4, 2, 56, 60, 1.5f, K);
                c.Ring(32, 22, 10, 15, Red);
                c.Rect(13, 19, 38, 6, Blue);
                c.TextFit("BUS STOP", 32, 50, 7, 48, K);
            }, BlockIds.SignBusStop);
            Add("ped_zone", "Pedestrian Zone", "Information", c =>
            {
                c.Rect(4, 2, 56, 60, W);
                Frame(c, 4, 2, 56, 60, 1.5f, K);
                c.Circle(32, 24, 17, Red);
                c.Circle(32, 24, 13, W);
                var p = new Pen(c, 32, 24, 0.9f);
                Person(p, 9, 12, 1f, K, true);
                Person(p, 16, 13, 0.8f, K, true);
                c.TextFit("ZONE", 32, 55, 8, 48, K);
            });
            Add("welcome", "Welcome to Brickton", "Information", c =>
            {
                c.RoundRect(3, 3, 186, 122, 6, W);
                Frame(c, 3, 3, 186, 122, 2f, K);
                c.TextFit("Welcome to", 96, 30, 11, 170, K);
                c.TextFit("BRICKTON", 96, 66, 24, 170, K);
                c.TextFit("Please drive carefully", 96, 102, 9, 170, K);
            }, 0, 3, 2);
            StreetName("street_high", "High Street", 3);
            StreetName("street_church", "Church Lane", 3);
            StreetName("street_station", "Station Road", 3);
            StreetName("street_victoria", "Victoria Road", 3);
            StreetName("street_mill", "Mill Lane", 2, BlockIds.SignStreet);

            // ---------------------------------------------------------- direction
            Add("dir_town_centre", "Town Centre (local)", "Direction", c => Local(c, "Town Centre", true), 0, 3, 1);
            Add("dir_station", "Station (local)", "Direction", c => Local(c, "Station", false), 0, 3, 1);
            Add("dir_church", "Parish Church (tourist)", "Direction", c =>
            {
                c.RoundRect(2, 6, 188, 52, 4, Brown);
                Frame(c, 2, 6, 188, 52, 1.5f, W);
                c.TextFit("Parish Church", 104, 41, 14, 140, W);
                var p = new Pen(c, 22, 32, 1.4f);
                Arrow(p, 18, 12, 4, 12, 4f, W);
            }, 0, 3, 1);
            Add("primary_route", "Primary Route (A1)", "Direction", c =>
            {
                c.RoundRect(2, 2, 188, 124, 6, Green);
                Frame(c, 2, 2, 188, 124, 2f, W);
                c.Text("A1", 16, 34, 14, Yellow);
                c.Text("(M)", 50, 34, 10, Yellow);
                c.Text("The NORTH", 16, 62, 13, W);
                c.Text("Brickton", 16, 92, 13, W);
                c.Text("12", 176, 92, 13, W, 1f);
                c.Text("Newtown", 16, 118, 13, W);
                c.Text("25", 176, 118, 13, W, 1f);
            }, 0, 3, 2);

            // ---------------------------------------------------------- motorway
            Add("motorway_start", "Start of Motorway", "Motorway", c =>
            {
                c.RoundRect(3, 3, 58, 58, 4, Blue);
                Frame(c, 3, 3, 58, 58, 1.5f, W);
                MotorwaySymbol(c);
            });
            Add("motorway_end", "End of Motorway", "Motorway", c =>
            {
                c.RoundRect(3, 3, 58, 58, 4, Blue);
                Frame(c, 3, 3, 58, 58, 1.5f, W);
                MotorwaySymbol(c);
                c.Bar(54, 10, 10, 54, 5, Red);
            });
            for (int n = 3; n >= 1; n--)
            {
                int bars = n;
                Add($"countdown_{n}", $"Countdown Marker ({n * 100} yds)", "Motorway", c =>
                {
                    c.Rect(12, 1, 40, 62, Blue);
                    Frame(c, 12, 1, 40, 62, 1.2f, W);
                    for (int b = 0; b < bars; b++) c.Bar(18, 14 + b * 16, 46, 4 + b * 16, 6, W);
                });
            }
            Add("emergency_area", "Emergency Area", "Motorway", c =>
            {
                c.RoundRect(3, 3, 58, 58, 4, Orange);
                c.Circle(32, 25, 14, W);
                c.TextFit("SOS", 32, 30, 10, 22, Orange);
                c.TextFit("Emergency", 32, 52, 7, 52, K);
            });
            Add("motorway_route", "M1 Route Shield", "Motorway", c =>
            {
                c.RoundRect(3, 12, 58, 40, 4, Blue);
                Frame(c, 3, 12, 58, 40, 1.5f, W);
                c.TextFit("M1", 32, 42, 20, 46, W);
            });
            Add("motorway_advance", "Motorway Advance Direction", "Motorway", c =>
            {
                c.RoundRect(2, 2, 252, 124, 6, Blue);
                Frame(c, 2, 2, 252, 124, 2f, W);
                c.RoundRect(14, 14, 34, 22, 3, K);
                c.TextFit("12", 31, 31, 12, 30, W);
                c.Text("M1", 62, 34, 16, W);
                c.Text("The NORTH", 18, 70, 15, W);
                c.Text("Leeds", 18, 100, 15, W);
                c.Text("Sheffield", 130, 100, 15, W);
                c.Text("1 mile", 238, 34, 11, W, 1f);
            }, 0, 4, 2);
            for (int lane = 0; lane < 3; lane++)
            {
                string[] dest = { "Leeds", "Sheffield", "Brickton" };
                string[] route = { "M1", "M1", "A1" };
                int li = lane;
                var def = Add($"gantry_lane_{lane + 1}", $"Gantry Lane Sign ({dest[lane]})", "Motorway", c =>
                {
                    bool primary = route[li][0] == 'A';
                    c.Rect(1, 1, 190, 126, primary ? Green : Blue);
                    Frame(c, 1, 1, 190, 126, 2f, W);
                    c.TextFit(route[li], 96, 36, 16, 120, primary ? Yellow : W);
                    c.TextFit(dest[li], 96, 72, 16, 170, W);
                    var p = new Pen(c, 96, 104, 1.1f);
                    Arrow(p, 12, 2, 12, 22, 5f, W);
                }, 0, 3, 2);
                def.Mount = SignMount.Gantry;
            }
            var vms = Vms("vms", "Motorway Message Sign", new[]
            {
                new[] { "QUEUE AHEAD", "SLOW DOWN" },
                new[] { "CONGESTION", "J12 - J14" },
                new[] { "THINK!", "TIRED DRIVERS DIE" },
                new[] { "REPORT DEBRIS", "0300 123 5000" },
                new[] { "DON'T DRINK", "AND DRIVE" },
            });
            vms.Mount = SignMount.Gantry;
        }

        // ------------------------------------------------------------------ catalogue helpers

        private static SignDef Add(string id, string name, string cat, Action<PixelCanvas> paint, int legacy = 0, int w = 1, int h = 1)
        {
            var s = new SignDef { Id = id, Name = name, Category = cat, Paint = paint, LegacyId = legacy, W = w, H = h };
            All.Add(s);
            byId[id] = s;
            return s;
        }

        private static void Warn(string id, string name, Action<Pen> picto)
        {
            Add(id, name, id == "children" ? "School" : "Warning", c =>
            {
                c.Triangle(32, 34, 63, true, Red);
                c.Triangle(32, 35.5f, 44, true, W);
                picto(new Pen(c, 32, 38, 0.95f));
            });
        }

        private static void Plate(string id, string name, string cat, string text)
        {
            Add(id, name, cat, c =>
            {
                float w = Mathf.Clamp(c.TextWidth(text, 9) + 10, 30, 62);
                c.Rect(32 - w / 2, 18, w, 26, W);
                Frame(c, 32 - w / 2, 18, w, 26, 1.2f, K);
                c.TextFit(text, 32, 36, 9, w - 6, K);
            });
        }

        private static void StreetName(string id, string name, int w, int legacy = 0)
        {
            var s = Add(id, name, "Information", c =>
            {
                float pw = w * Px;
                c.Rect(2, 10, pw - 4, 44, W);
                Frame(c, 2, 10, pw - 4, 44, 2.5f, K);
                c.TextFit(name, pw / 2, 41, 17, pw - 24, K);
            }, legacy, w, 1);
            s.Mount = SignMount.Legs;
        }

        private static void Local(PixelCanvas c, string text, bool arrowLeft)
        {
            c.Rect(2, 8, 188, 48, W);
            Frame(c, 2, 8, 188, 48, 2f, K);
            c.TextFit(text, arrowLeft ? 108 : 84, 41, 15, 130, K);
            var p = new Pen(c, arrowLeft ? 22 : 170, 32, 1.4f);
            if (arrowLeft) Arrow(p, 20, 12, 4, 12, 4.5f, K);
            else Arrow(p, 4, 12, 20, 12, 4.5f, K);
        }

        private static SignDef Vms(string id, string name, string[][] messages)
        {
            var s = Add(id, name, "Motorway", c => VmsFace(c, null), 0, 3, 1);
            s.Messages = messages;
            return s;
        }

        /// <summary>Electronic message panel: amber LED text on a black dot matrix.</summary>
        private static void VmsFace(PixelCanvas c, string[] lines)
        {
            c.Fill(Rgba.Hex("17181a"));
            c.Rect(3, 3, c.W - 6, c.H - 6, Rgba.Hex("0b0b0c"));
            for (int y = 5; y < c.H - 4; y += 3)
                for (int x = 5; x < c.W - 4; x += 3)
                    c.Set(x, y, Rgba.Hex("1c1c1f"));
            if (lines == null) return;
            var text = new PixelCanvas(c.W, c.H);
            for (int i = 0; i < lines.Length; i++)
                text.TextFit(lines[i], c.W / 2f, 26 + i * 22, 13, c.W - 16, Amber);
            // Quantise onto the LED grid.
            for (int y = 5; y < c.H - 4; y += 3)
                for (int x = 5; x < c.W - 4; x += 3)
                {
                    float cov = Mathf.Max(Mathf.Max(text.Get(x, y).a, text.Get(x + 1, y).a), Mathf.Max(text.Get(x, y + 1).a, text.Get(x + 1, y + 1).a));
                    if (cov > 0.4f) { c.Set(x, y, Amber); c.Set(x + 1, y, Amber.Scale(0.7f)); c.Set(x, y + 1, Amber.Scale(0.7f)); }
                }
        }

        // ------------------------------------------------------------------ sign shapes

        private static void Frame(PixelCanvas c, float x, float y, float w, float h, float t, Rgba col)
        {
            c.Rect(x, y, w, t, col);
            c.Rect(x, y + h - t, w, t, col);
            c.Rect(x, y, t, h, col);
            c.Rect(x + w - t, y, t, h, col);
        }

        private static void Roundel(PixelCanvas c, string n)
        {
            c.Circle(32, 32, 30.5f, Red);
            c.Circle(32, 32, 23.5f, W);
            c.TextFit(n, 32, 41.5f, 19, 34, K);
        }

        private static void Prohibit(PixelCanvas c, Action<Pen> picto, bool bar)
        {
            c.Circle(32, 32, 30.5f, Red);
            c.Circle(32, 32, 24f, W);
            picto(new Pen(c, 32, 32, 1.45f));
            if (bar) c.Bar(15, 15, 49, 49, 5.5f, Red);
        }

        private static void Mandatory(PixelCanvas c, Action<Pen> picto)
        {
            c.Circle(32, 32, 30.5f, W);
            c.Circle(32, 32, 29f, Blue);
            picto(new Pen(c, 32, 32, 1.75f));
        }

        private static void MotorwaySymbol(PixelCanvas c)
        {
            c.Polygon(W, 8, 56, 26, 22, 30, 22, 22, 56);
            c.Polygon(W, 56, 56, 38, 22, 34, 22, 42, 56);
            c.Bar(31, 52, 31, 40, 3, W);
            c.Bar(31, 34, 31, 26, 3, W);
            c.Rect(10, 14, 44, 6, W);
        }

        // ------------------------------------------------------------------ pictograms (24 x 24 unit box)

        /// <summary>Draws pictograms in a 24x24 unit box centred on (cx, cy), scaled by k pixels per unit.</summary>
        public sealed class Pen
        {
            private readonly PixelCanvas c;
            private readonly float cx, cy, k;

            public Pen(PixelCanvas c, float cx, float cy, float k) { this.c = c; this.cx = cx; this.cy = cy; this.k = k; }

            private float X(float x) => cx + (x - 12f) * k;
            private float Y(float y) => cy + (y - 12f) * k;

            public void Line(float x0, float y0, float x1, float y1, float w, Rgba col) => c.Line(X(x0), Y(y0), X(x1), Y(y1), w * k, col);
            public void Bar(float x0, float y0, float x1, float y1, float w, Rgba col) => c.Bar(X(x0), Y(y0), X(x1), Y(y1), w * k, col);
            public void Circle(float x, float y, float r, Rgba col) => c.Circle(X(x), Y(y), r * k, col);
            public void Ring(float x, float y, float r0, float r1, Rgba col) => c.Ring(X(x), Y(y), r0 * k, r1 * k, col);
            public void Rect(float x, float y, float w, float h, Rgba col) => c.Rect(X(x), Y(y), w * k, h * k, col);
            public void RoundRect(float x, float y, float w, float h, float r, Rgba col) => c.RoundRect(X(x), Y(y), w * k, h * k, r * k, col);
            public void TextFit(string s, float x, float baseline, float cap, float maxW, Rgba col) => c.TextFit(s, X(x), Y(baseline), cap * k, maxW * k, col);

            public void Arc(float x, float y, float r, float a0, float a1, float w, Rgba col, int steps = 10)
            {
                for (int i = 0; i < steps; i++)
                {
                    float t0 = Mathf.Lerp(a0, a1, i / (float)steps), t1 = Mathf.Lerp(a0, a1, (i + 1) / (float)steps);
                    Line(x + Mathf.Cos(t0) * r, y + Mathf.Sin(t0) * r, x + Mathf.Cos(t1) * r, y + Mathf.Sin(t1) * r, w, col);
                }
            }

            public void Polygon(Rgba col, params float[] pts)
            {
                var t = new float[pts.Length];
                for (int i = 0; i < pts.Length; i += 2) { t[i] = X(pts[i]); t[i + 1] = Y(pts[i + 1]); }
                c.Polygon(col, t);
            }
        }

        private static void Arrow(Pen p, float x0, float y0, float x1, float y1, float w, Rgba col)
        {
            float dx = x1 - x0, dy = y1 - y0, len = Mathf.Sqrt(dx * dx + dy * dy);
            if (len < 1e-3f) return;
            dx /= len; dy /= len;
            float head = w * 1.9f;
            float bx = x1 - dx * head, by = y1 - dy * head;
            p.Bar(x0, y0, bx + dx * 0.5f, by + dy * 0.5f, w, col);
            float nx = -dy * head * 0.9f, ny = dx * head * 0.9f;
            p.Polygon(col, x1, y1, bx + nx, by + ny, bx - nx, by - ny);
        }

        private static void TurnArrow(Pen p, bool right)
        {
            float s = right ? 1 : -1;
            p.Bar(12 - 3 * s, 21, 12 - 3 * s, 10, 3.5f, K);
            Arrow(p, 12 - 4.5f * s, 10, 12 + 8 * s, 10, 3.5f, K);
        }

        private static void UTurn(Pen p)
        {
            p.Bar(15, 21, 15, 9, 3.5f, K);
            p.Arc(11, 9, 4f, 0f, -Mathf.PI, 3.5f, K);
            Arrow(p, 7, 8.5f, 7, 18, 3.5f, K);
        }

        private static void Curve(Pen p, bool right)
        {
            float s = right ? 1 : -1;
            p.Line(12 - 3 * s, 21, 12 - 3 * s, 14, 3.5f, K);
            p.Line(12 - 3 * s, 14, 12 - 1 * s, 9, 3.5f, K);
            Arrow(p, 12 - 1.5f * s, 9.5f, 12 + 7 * s, 4, 3.5f, K);
        }

        private static void RoundaboutArrows(Pen p, Rgba col)
        {
            // Three arrows chasing clockwise round a circle (UK roundabouts run clockwise).
            for (int i = 0; i < 3; i++)
            {
                float a0 = i * Mathf.PI * 2f / 3f - Mathf.PI / 2f;
                float a1 = a0 + 1.35f;
                const float r = 6.5f;
                int steps = 6;
                for (int s = 0; s < steps; s++)
                {
                    float t0 = Mathf.Lerp(a0, a1, s / (float)steps), t1 = Mathf.Lerp(a0, a1, (s + 1) / (float)steps);
                    p.Line(12 + Mathf.Cos(t0) * r, 12 + Mathf.Sin(t0) * r, 12 + Mathf.Cos(t1) * r, 12 + Mathf.Sin(t1) * r, 2.6f, col);
                }
                float ax = 12 + Mathf.Cos(a1) * r, ay = 12 + Mathf.Sin(a1) * r;
                float tx = -Mathf.Sin(a1), ty = Mathf.Cos(a1);
                float nx = Mathf.Cos(a1), ny = Mathf.Sin(a1);
                p.Polygon(col, ax + tx * 4f, ay + ty * 4f, ax + nx * 3.2f, ay + ny * 3.2f, ax - nx * 3.2f, ay - ny * 3.2f);
            }
        }

        private static void Person(Pen p, float x, float y, float s, Rgba col, bool walking)
        {
            p.Circle(x, y - 7.5f * s, 2.1f * s, col);
            p.Line(x, y - 4.5f * s, x - 0.6f * s, y + 2f * s, 2.8f * s, col);
            if (walking)
            {
                p.Line(x - 0.6f * s, y + 2f * s, x - 4f * s, y + 8.5f * s, 2.3f * s, col);
                p.Line(x - 0.6f * s, y + 2f * s, x + 3.2f * s, y + 8.5f * s, 2.3f * s, col);
                p.Line(x, y - 3.5f * s, x - 4f * s, y + 1f * s, 1.8f * s, col);
                p.Line(x, y - 3.5f * s, x + 3.8f * s, y + 0.5f * s, 1.8f * s, col);
            }
            else
            {
                p.Line(x - 0.6f * s, y + 2f * s, x - 2f * s, y + 8.5f * s, 2.3f * s, col);
                p.Line(x - 0.6f * s, y + 2f * s, x + 1.5f * s, y + 8.5f * s, 2.3f * s, col);
                p.Line(x, y - 3.5f * s, x + 3f * s, y - 1f * s, 1.8f * s, col);
                p.Line(x, y - 3.5f * s, x - 3f * s, y + 0.5f * s, 1.8f * s, col);
            }
        }

        private static void Bike(Pen p, float x, float y, float s, Rgba col)
        {
            p.Ring(x - 5.5f * s, y + 3f * s, 2.4f * s, 3.6f * s, col);
            p.Ring(x + 5.5f * s, y + 3f * s, 2.4f * s, 3.6f * s, col);
            p.Line(x - 5.5f * s, y + 3f * s, x - 1f * s, y - 2f * s, 1.4f * s, col);
            p.Line(x - 1f * s, y - 2f * s, x + 4f * s, y - 2f * s, 1.4f * s, col);
            p.Line(x + 4f * s, y - 2f * s, x + 5.5f * s, y + 3f * s, 1.4f * s, col);
            p.Line(x - 1f * s, y - 2f * s, x + 0.5f * s, y + 3f * s, 1.4f * s, col);
            p.Line(x + 0.5f * s, y + 3f * s, x - 5.5f * s, y + 3f * s, 1.4f * s, col);
            p.Line(x + 3.5f * s, y - 4.5f * s, x + 4f * s, y - 2f * s, 1.4f * s, col);
            p.Line(x - 2f * s, y - 3.5f * s, x + 0.5f * s, y - 3.5f * s, 1.4f * s, col);
        }

        private static void CarSide(Pen p, float x, float y, Rgba col)
        {
            p.RoundRect(x, y + 2, 8, 3.5f, 1, col);
            p.Polygon(col, x + 1.5f, y + 2.2f, x + 2.5f, y, x + 5.5f, y, x + 6.8f, y + 2.2f);
            p.Circle(x + 2, y + 5.5f, 1.2f, col);
            p.Circle(x + 6, y + 5.5f, 1.2f, col);
        }

        private static void CarBack(Pen p, float x, float y, Rgba col, float s = 1f)
        {
            p.RoundRect(x - 5 * s, y + 2 * s, 10 * s, 4 * s, 1 * s, col);
            p.Polygon(col, x - 3.8f * s, y + 2.2f * s, x - 2.6f * s, y - 0.5f * s, x + 2.6f * s, y - 0.5f * s, x + 3.8f * s, y + 2.2f * s);
            p.Rect(x - 4.5f * s, y + 6 * s, 2 * s, 1.5f * s, col);
            p.Rect(x + 2.5f * s, y + 6 * s, 2 * s, 1.5f * s, col);
        }

        private static void BusSide(Pen p, Rgba col)
        {
            p.RoundRect(2, 6, 20, 11, 1.5f, col);
            for (int i = 0; i < 4; i++) p.Rect(4 + i * 4.5f, 8, 3.4f, 3.4f, Blue);
            p.Circle(6, 17.5f, 1.8f, col);
            p.Circle(18, 17.5f, 1.8f, col);
        }

        // ------------------------------------------------------------------ blocks & tiles

        /// <summary>Registers a panel block for every part of every sign.</summary>
        public static void RegisterBlocks()
        {
            int next = BlockIds.FirstSign;
            int grey = Blocks.Tile("metal_grey");
            foreach (var s in All)
            {
                s.Parts = new int[s.W, s.H];
                for (int j = 0; j < s.H; j++)
                    for (int i = 0; i < s.W; i++)
                    {
                        int id = (i == 0 && j == 0 && s.LegacyId != 0) ? s.LegacyId : next++;
                        s.Parts[i, j] = id;
                        string part = s.W * s.H == 1 ? "" : $" [{i + 1},{j + 1}]";
                        int front = Blocks.Tile($"sign:{s.Id}:{i}:{j}");
                        int back = Blocks.Tile($"signback:{s.Id}:{i}:{j}");
                        var boxes = new List<Box>
                        {
                            new Box(new Vector3(0, 0, 0.34f), new Vector3(1, 1, 0.38f), -1).Front(front).Back(back).Full()
                        };
                        bool postColumn = s.Mount != SignMount.Gantry && (i == 0 || i == s.W - 1);
                        if (postColumn) boxes.Add(Blocks.PostBehind(grey));
                        if (s.Mount == SignMount.Gantry) boxes.Add(new Box(new Vector3(0.45f, 0.9f, 0.38f), new Vector3(0.55f, 1f, 0.5f), grey).NoCollide());
                        var d = Blocks.ModelBlock(id, s.Name + part, "Signs", front, boxes.ToArray());
                        d.Placeable = s.W * s.H == 1;
                        if (s.Messages != null)
                        {
                            d.Anim = AnimKind.Vms;
                            d.AnimBox = 0;
                            d.Frames = new int[s.Messages.Length];
                            for (int m = 0; m < s.Messages.Length; m++) d.Frames[m] = Blocks.Tile($"vms:{s.Id}:{m}:{i}:{j}");
                        }
                    }
            }
        }

        /// <summary>Paints a sign-related tile ("sign:", "signback:" or "vms:"). Thread-safe.</summary>
        public static bool PaintTile(string name, PixelCanvas tile)
        {
            var parts = name.Split(':');
            if (parts.Length < 4) return false;
            bool back = parts[0] == "signback";
            bool isVms = parts[0] == "vms";
            if (parts[0] != "sign" && !back && !isVms) return false;
            var def = Get(parts[1]);
            if (def == null) return false;
            int msg = isVms ? int.Parse(parts[2]) : -1;
            int i = int.Parse(parts[isVms ? 3 : 2]), j = int.Parse(parts[isVms ? 4 : 3]);
            var full = Full(def, msg);
            int sx = i * Px, sy = (def.H - 1 - j) * Px;
            for (int y = 0; y < Px; y++)
                for (int x = 0; x < Px; x++)
                {
                    if (!back)
                    {
                        tile.P[y * Px + x] = full.P[(sy + y) * full.W + sx + x];
                        continue;
                    }
                    // Back of the plate: grey, same silhouette, mirrored because it is seen from behind.
                    int mx = full.W - 1 - (sx + x);
                    float a = full.P[(sy + y) * full.W + mx].a;
                    float n = Noise.White(x, y, 5) * 0.04f;
                    tile.P[y * Px + x] = new Rgba(0.55f + n, 0.57f + n, 0.58f + n, a > 0.5f ? 1f : 0f);
                }
            return true;
        }

        private static PixelCanvas Full(SignDef def, int message)
        {
            string key = def.Id + "#" + message;
            lock (painted)
            {
                if (painted.TryGetValue(key, out var c)) return c;
                c = new PixelCanvas(def.W * Px, def.H * Px);
                if (message >= 0) VmsFace(c, def.Messages[message]);
                else def.Paint(c);
                // Hard alpha edge: signs are drawn as alpha-cutout plates.
                for (int k = 0; k < c.P.Length; k++)
                {
                    var p = c.P[k];
                    c.P[k] = p.a >= 0.5f ? new Rgba(p.r, p.g, p.b, 1f) : new Rgba(p.r, p.g, p.b, 0f);
                }
                painted[key] = c;
                return c;
            }
        }
    }
}
