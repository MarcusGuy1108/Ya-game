using System.Collections.Generic;
using UnityEngine;

namespace UKCity.World
{
    /// <summary>
    /// 3D traffic signal models built from boxes: housing, hooded lenses, white-bordered backing board, pole.
    /// Every lens is its own lamp, lit independently by the signal controller (see DynamicFaces).
    /// Local space: front (towards drivers) is -Z; the pole runs up the middle behind the board.
    /// </summary>
    public static class SignalModels
    {
        private enum Style { Led, Classic }

        private sealed class Builder
        {
            public readonly List<Box> Boxes = new List<Box>();
            public readonly List<Lamp> Lamps = new List<Lamp>();

            public int Add(Box b) { Boxes.Add(b); return Boxes.Count - 1; }

            public void Lens(float x0, float y0, float x1, float y1, float z, string offTile, string onTile, LampRole role, string glow, float glowScale = 2.4f)
            {
                int box = Add(new Box(new Vector3(x0, y0, z - 0.006f), new Vector3(x1, y1, z), -1).Front(Blocks.Tile(offTile)).Full().NoCollide());
                Lamps.Add(new Lamp
                {
                    Box = box, Face = 5, Role = role, LitTile = Blocks.Tile(onTile),
                    GlowTile = glow != null ? Blocks.Tile(glow) : -1, GlowScale = glowScale
                });
            }
        }

        private static Vector3 V(float x, float y, float z) => new Vector3(x, y, z);
        private static int T(string n) => Blocks.Tile(n);

        public static void Register()
        {
            Vehicle(BlockIds.TrafficLight, "Signal Head (LED)", Style.Led, "led", null);
            Vehicle(BlockIds.SignalClassic, "Signal Head (older style)", Style.Classic, "classic", null);
            Vehicle(BlockIds.SignalArrowLeft, "Arrow Signal (left)", Style.Led, "arrow_left", null);
            Vehicle(BlockIds.SignalArrowRight, "Arrow Signal (right)", Style.Led, "arrow_right", null);
            Vehicle(BlockIds.SignalArrowAhead, "Arrow Signal (ahead)", Style.Led, "arrow_ahead", null);
            Vehicle(BlockIds.SignalFilterLeft, "Signal Head + Left Filter", Style.Led, "led", "filter_left");
            Vehicle(BlockIds.SignalFilterRight, "Signal Head + Right Filter", Style.Led, "led", "filter_right");
            Vehicle(BlockIds.SignalClassicFilterLeft, "Older Signal + Left Filter", Style.Classic, "classic", "filter_left");
            Vehicle(BlockIds.SignalNoRightPod, "Signal Head + No Right Turn Pod", Style.Led, "led", "pod_noright");
            Vehicle(BlockIds.SignalNoLeftPod, "Signal Head + No Left Turn Pod", Style.Led, "led", "pod_noleft");
            Cycle();
            Pedestrian(BlockIds.PedSignal, "Pedestrian Signal (far side)", false);
            Pedestrian(BlockIds.ToucanSignal, "Toucan Signal (walk + cycle)", true);
            PushButton();
            PuffinUnit();
            Belisha();
            WigWag();
        }

        // ------------------------------------------------------------------ vehicle heads

        /// <summary>A three-aspect head, optionally with a filter arrow or an illuminated pod beside it.</summary>
        private static void Vehicle(int id, string name, Style style, string lens, string extra)
        {
            var b = new Builder();
            int grey = T("pole_grey"), black = T("sig_housing"), hood = T("sig_hood");
            bool left = extra == "filter_left" || extra == "pod_noleft";
            bool right = extra == "filter_right" || extra == "pod_noright";

            b.Add(Blocks.PostBehind(grey));
            // Backing board (widened to carry the filter/pod).
            float bx0 = left ? 0.0f : 0.18f, bx1 = right ? 1.0f : 0.82f;
            b.Add(new Box(V(bx0, 0.0f, 0.40f), V(bx1, 1.0f, 0.435f), black).Front(T("signal_board")).Full());
            // Main housing.
            b.Add(new Box(V(0.33f, 0.03f, 0.19f), V(0.67f, 0.97f, 0.40f), black).Front(T(style == Style.Led ? "sig_housing_led" : "sig_housing")).Full());
            // Brackets to the pole.
            b.Add(new Box(V(0.46f, 0.12f, 0.435f), V(0.54f, 0.18f, 0.46f), black));
            b.Add(new Box(V(0.46f, 0.82f, 0.435f), V(0.54f, 0.88f, 0.46f), black));

            string[] colours = { "red", "amber", "green" };
            LampRole[] roles = { LampRole.Red, LampRole.Amber, LampRole.Green };
            float[] cy = { 0.81f, 0.5f, 0.19f };
            for (int i = 0; i < 3; i++)
            {
                Hood(b, style, 0.345f, 0.655f, cy[i], hood);
                b.Lens(0.37f, cy[i] - 0.13f, 0.63f, cy[i] + 0.13f, 0.19f,
                    $"lens_{lens}_{colours[i]}_off", $"lens_{lens}_{colours[i]}_on", roles[i], "glow_" + colours[i]);
            }

            if (extra == "filter_left" || extra == "filter_right")
            {
                // Fourth aspect beside the green: a green filter arrow.
                float x0 = left ? 0.02f : 0.67f, x1 = left ? 0.33f : 0.98f;
                b.Add(new Box(V(x0, 0.03f, 0.19f), V(x1, 0.36f, 0.40f), black).Front(T("sig_housing")).Full());
                Hood(b, style, x0 + 0.015f, x1 - 0.015f, 0.19f, hood);
                b.Lens(x0 + 0.025f, 0.06f, x1 - 0.025f, 0.32f, 0.19f, $"lens_{extra}_off", $"lens_{extra}_on",
                    left ? LampRole.FilterLeft : LampRole.FilterRight, "glow_green");
            }
            else if (extra != null)
            {
                // Illuminated regulatory pod ("no right turn" / "no left turn").
                float x0 = left ? 0.02f : 0.68f, x1 = left ? 0.32f : 0.98f;
                b.Add(new Box(V(x0, 0.55f, 0.22f), V(x1, 0.92f, 0.40f), black).Front(T("sig_housing")).Full());
                b.Lens(x0 + 0.02f, 0.57f, x1 - 0.02f, 0.90f, 0.22f, extra + "_off", extra + "_on",
                    left ? LampRole.NoLeftTurn : LampRole.NoRightTurn, "glow_white", 1.8f);
            }

            Finish(id, name, b, $"lens_{lens}_green_on");
        }

        private static void Hood(Builder b, Style style, float x0, float x1, float cy, int tile)
        {
            float depth = style == Style.Classic ? 0.19f : 0.07f;
            float z1 = 0.19f, z0 = z1 - depth;
            // Top visor, then shorter cheeks down each side.
            b.Add(new Box(V(x0, cy + 0.125f, z0), V(x1, cy + 0.145f, z1), tile).NoCollide());
            float sideDrop = style == Style.Classic ? -0.06f : 0.02f;
            b.Add(new Box(V(x0, cy + sideDrop, z0 + depth * 0.3f), V(x0 + 0.015f, cy + 0.145f, z1), tile).NoCollide());
            b.Add(new Box(V(x1 - 0.015f, cy + sideDrop, z0 + depth * 0.3f), V(x1, cy + 0.145f, z1), tile).NoCollide());
        }

        private static void Cycle()
        {
            var b = new Builder();
            int black = T("sig_housing"), hood = T("sig_hood");
            b.Add(Blocks.PostBehind(T("pole_grey")));
            b.Add(new Box(V(0.38f, 0.18f, 0.28f), V(0.62f, 0.86f, 0.44f), black).Front(T("sig_housing")).Full());
            string[] colours = { "red", "amber", "green" };
            LampRole[] roles = { LampRole.Red, LampRole.Amber, LampRole.Green };
            float[] cy = { 0.74f, 0.52f, 0.30f };
            for (int i = 0; i < 3; i++)
            {
                b.Add(new Box(V(0.39f, cy[i] + 0.09f, 0.22f), V(0.61f, cy[i] + 0.1f, 0.28f), hood).NoCollide());
                b.Lens(0.41f, cy[i] - 0.09f, 0.59f, cy[i] + 0.09f, 0.28f, $"lens_cycle_{colours[i]}_off", $"lens_cycle_{colours[i]}_on", roles[i], "glow_" + colours[i], 2f);
            }
            Finish(BlockIds.CycleSignal, "Low-level Cycle Signal", b, "lens_cycle_green_on");
        }

        // ------------------------------------------------------------------ pedestrians

        private static void Pedestrian(int id, string name, bool toucan)
        {
            var b = new Builder();
            int black = T("sig_housing"), hood = T("sig_hood");
            b.Add(Blocks.PostBehind(T("pole_grey")));
            b.Add(new Box(V(0.27f, 0.1f, 0.24f), V(0.73f, 0.97f, 0.44f), black).Front(T("sig_housing")).Full());
            b.Add(new Box(V(0.28f, 0.92f, 0.12f), V(0.72f, 0.94f, 0.24f), hood).NoCollide());
            b.Add(new Box(V(0.28f, 0.52f, 0.14f), V(0.72f, 0.54f, 0.24f), hood).NoCollide());
            b.Lens(0.3f, 0.57f, 0.7f, 0.91f, 0.24f, "lens_redman_off", "lens_redman_on", LampRole.RedMan, "glow_red", 2f);
            string g = toucan ? "lens_toucan" : "lens_greenman";
            b.Lens(0.3f, 0.15f, 0.7f, 0.49f, 0.24f, g + "_off", g + "_on", LampRole.GreenMan, "glow_green", 2f);
            Finish(id, name, b, g + "_on").PedestrianLamps = true;
        }

        private static void PushButton()
        {
            var b = new Builder();
            int yellow = T("yellow_box");
            b.Add(Blocks.PostBehind(T("pole_grey")));
            b.Add(new Box(V(0.3f, 0.28f, 0.28f), V(0.7f, 0.76f, 0.44f), yellow).Front(T("push_button")).Full());
            b.Add(new Box(V(0.44f, 0.24f, 0.32f), V(0.56f, 0.28f, 0.4f), T("metal_dark")).NoCollide()); // tactile cone underneath
            b.Lens(0.35f, 0.62f, 0.65f, 0.72f, 0.28f, "wait_off", "wait_on", LampRole.Wait, null);
            Finish(BlockIds.PushButton, "Crossing Push Button", b, "wait_on").PedestrianLamps = true;
        }

        /// <summary>Puffin near-side unit: push button with the red/green man display on top, facing the kerb.</summary>
        private static void PuffinUnit()
        {
            var b = new Builder();
            int yellow = T("yellow_box"), black = T("sig_housing");
            b.Add(Blocks.PostBehind(T("pole_grey")));
            b.Add(new Box(V(0.27f, 0.18f, 0.26f), V(0.73f, 0.58f, 0.44f), yellow).Front(T("push_button")).Full());
            b.Add(new Box(V(0.27f, 0.58f, 0.22f), V(0.73f, 0.84f, 0.44f), black).Front(T("sig_housing")).Full());
            b.Add(new Box(V(0.27f, 0.84f, 0.16f), V(0.73f, 0.86f, 0.3f), T("sig_hood")).NoCollide());
            b.Lens(0.29f, 0.61f, 0.49f, 0.82f, 0.22f, "lens_redman_off", "lens_redman_on", LampRole.RedMan, "glow_red", 1.8f);
            b.Lens(0.51f, 0.61f, 0.71f, 0.82f, 0.22f, "lens_greenman_off", "lens_greenman_on", LampRole.GreenMan, "glow_green", 1.8f);
            b.Lens(0.34f, 0.44f, 0.66f, 0.54f, 0.26f, "wait_off", "wait_on", LampRole.Wait, null);
            Finish(BlockIds.PuffinUnit, "Puffin Near-side Unit", b, "lens_greenman_on").PedestrianLamps = true;
        }

        // ------------------------------------------------------------------ beacons

        private static void Belisha()
        {
            var b = new Builder();
            int off = T("belisha_off");
            int core = b.Add(new Box(V(0.22f, 0.07f, 0.22f), V(0.78f, 0.49f, 0.78f), off).Full());
            int top = b.Add(new Box(V(0.3f, 0.49f, 0.3f), V(0.7f, 0.56f, 0.7f), off).Full());
            b.Add(new Box(V(0.3f, 0f, 0.3f), V(0.7f, 0.07f, 0.7f), off).Full());
            int on = T("belisha_on"), glow = T("glow_amber");
            foreach (int f in new[] { 0, 1, 4, 5 })
                b.Lamps.Add(new Lamp { Box = core, Face = f, Role = LampRole.Beacon, LitTile = on, GlowTile = glow, GlowScale = 1.9f });
            b.Lamps.Add(new Lamp { Box = top, Face = 2, Role = LampRole.Beacon, LitTile = on, GlowTile = -1 });
            var d = Finish(BlockIds.Belisha, "Belisha Beacon", b, "belisha_on");
            d.Rotatable = false;
        }

        private static void WigWag()
        {
            var b = new Builder();
            int black = T("sig_housing"), hood = T("sig_hood");
            b.Add(Blocks.PostBehind(T("pole_grey")));
            b.Add(new Box(V(0.04f, 0.25f, 0.3f), V(0.96f, 0.75f, 0.42f), black).Front(T("sig_housing")).Full());
            for (int i = 0; i < 2; i++)
            {
                float x0 = i == 0 ? 0.08f : 0.56f, x1 = x0 + 0.36f;
                b.Add(new Box(V(x0, 0.68f, 0.18f), V(x1, 0.7f, 0.3f), hood).NoCollide());
                b.Lens(x0 + 0.02f, 0.32f, x1 - 0.02f, 0.68f, 0.3f, "lens_wigwag_off", "lens_wigwag_on", i == 0 ? LampRole.WigLeft : LampRole.WigRight, "glow_amber", 2f);
            }
            Finish(BlockIds.WigWag, "School Flashing Lights", b, "lens_wigwag_on");
        }

        private static BlockDef Finish(int id, string name, Builder b, string icon)
        {
            var d = Blocks.ModelBlock(id, name, "Signals", T(icon), b.Boxes.ToArray());
            d.Anim = AnimKind.Lamps;
            d.Lamps = b.Lamps.ToArray();
            return d;
        }
    }
}
