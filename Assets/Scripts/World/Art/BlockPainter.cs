using UnityEngine;

namespace UKCity.World.Art
{
    /// <summary>
    /// Paints every non-sign block texture procedurally at 64x64. Coordinates are y-down; for side faces the top of
    /// the canvas is the top of the block, for top faces the top of the canvas is north (+Z).
    /// Road markings are drawn for a road running north-south (facing 0); the "forward" direction of travel is
    /// down the canvas (south), and blocks are rotated to suit.
    /// </summary>
    public static class BlockPainter
    {
        public const int S = 64;

        private static Rgba H(string hex, float a = 1f) => Rgba.Hex(hex, a);

        private static readonly Rgba White = H("eeeeea");
        private static readonly Rgba Yellow = H("f2c21b");
        private static readonly Rgba Black = H("17181a");

        public static bool Paint(string name, PixelCanvas c)
        {
            int seed = name.GetHashCode() & 0xFFFF;
            switch (name)
            {
                // ---------------------------------------------------------- nature
                case "grass_top": Grass(c, seed); return true;
                case "grass_side":
                    Dirt(c, seed);
                    for (int x = 0; x < S; x++)
                    {
                        int h = 9 + (int)(Noise.Fbm(x, 0, S, 8, 2, seed) * 8f) + (Noise.White(x, 1, seed) > 0.7f ? 3 : 0);
                        for (int y = 0; y < h; y++)
                        {
                            float n = Noise.Fbm(x, y, S, 16, 2, seed + 1);
                            c.Set(x, y, Rgba.Lerp(H("4f8a2c"), H("74ad42"), n).Scale(y > h - 3 ? 0.85f : 1f));
                        }
                    }
                    return true;
                case "dirt": Dirt(c, seed); return true;
                case "stone": Stone(c, seed, H("8a8a88"), 0.18f); return true;
                case "bedrock": Stone(c, seed, H("3a3a3c"), 0.6f); return true;
                case "sand": Grainy(c, seed, H("d9c690"), H("c4ad74"), 0.5f); return true;
                case "gravel": Gravel(c, seed, H("8e8a84")); return true;
                case "log_side":
                    c.Each((x, y, _) =>
                    {
                        float stripe = Noise.Fbm(x * 0.5f, y * 4f, S, 8, 3, seed);
                        float n = Noise.Fbm(x, y, S, 16, 2, seed + 4);
                        return Rgba.Lerp(H("3e2e1c"), H("6b5236"), stripe * 0.7f + n * 0.3f);
                    });
                    return true;
                case "log_top":
                    c.Each((x, y, _) =>
                    {
                        float dx = x + 0.5f - 32, dy = y + 0.5f - 32;
                        float d = Mathf.Sqrt(dx * dx + dy * dy);
                        float ring = 0.5f + 0.5f * Mathf.Sin(d * 1.1f + Noise.Fbm(x, y, S, 4, 2, seed) * 4f);
                        var wood = Rgba.Lerp(H("b08a58"), H("c9a571"), ring);
                        return d > 28 ? H("4a3722") : wood;
                    });
                    return true;
                case "leaves": Leaves(c, seed, H("2f5e22"), H("5c9238"), 0.22f); return true;
                case "hedge": Leaves(c, seed, H("2a4f1c"), H("4d7c2c"), 0.06f); return true;
                case "water":
                    c.Each((x, y, _) => Rgba.Lerp(H("2c5f8f", 0.72f), H("4f86b8", 0.72f), Noise.Fbm(x, y, S, 4, 3, seed)));
                    return true;

                // ---------------------------------------------------------- road surfaces
                case "asphalt": Asphalt(c, seed); return true;
                case "footway": Asphalt(c, seed, 0.06f); return true;
                case "red_tarmac":
                    Asphalt(c, seed);
                    c.Each((x, y, p) => new Rgba(p.r * 1.9f + 0.12f, p.g * 0.8f, p.b * 0.75f));
                    return true;
                case "road_paint": Asphalt(c, seed); Paint(c, 0, 0, S, S, White, seed); return true;
                case "kerb_top":
                    Stone(c, seed, H("a9a8a2"), 0.1f);
                    c.Rect(0, 0, S, 2, H("7c7b77"));
                    c.Rect(0, S - 2, S, 2, H("7c7b77"));
                    c.Rect(0, 0, 2, S, H("8c8b87"));
                    return true;
                case "kerb_side":
                    Stone(c, seed, H("a3a29c"), 0.1f);
                    c.Rect(0, 0, S, 3, H("c2c1bb"));
                    return true;
                case "concrete": Concrete(c, seed, H("a8a7a1")); return true;
                case "pavement": Slabs(c, seed, H("b3b0a8"), 2); return true;
                case "tactile_red": Tactile(c, seed, H("a44a3c")); return true;
                case "tactile_buff": Tactile(c, seed, H("c9b07a")); return true;
                case "setts": Setts(c, seed); return true;
                case "block_paving": BlockPaving(c, seed); return true;

                // ---------------------------------------------------------- road markings (road runs N-S)
                case "mark_line": Asphalt(c, seed); Paint(c, 28, 0, 8, S, White, seed); return true;
                case "mark_line_x": Asphalt(c, seed); Paint(c, 0, 28, S, 8, White, seed); return true;
                case "mark_thick_line": Asphalt(c, seed); Paint(c, 25, 0, 14, S, White, seed); return true;
                case "mark_double_yellow":
                    Asphalt(c, seed); Paint(c, 22, 0, 6, S, Yellow, seed); Paint(c, 36, 0, 6, S, Yellow, seed); return true;
                case "mark_double_yellow_x":
                    Asphalt(c, seed); Paint(c, 0, 22, S, 6, Yellow, seed); Paint(c, 0, 36, S, 6, Yellow, seed); return true;
                case "mark_single_yellow": Asphalt(c, seed); Paint(c, 29, 0, 6, S, Yellow, seed); return true;
                case "mark_giveway":
                    Asphalt(c, seed);
                    Paint(c, 4, 14, 38, 12, White, seed); Paint(c, 4, 38, 38, 12, White, seed);
                    return true;
                case "mark_giveway_single":
                    Asphalt(c, seed); Paint(c, 4, 25, 38, 14, White, seed); return true;
                case "mark_stop": Asphalt(c, seed); Paint(c, 0, 22, S, 20, White, seed); return true;
                case "mark_zigzag":
                case "mark_zigzag_yellow":
                {
                    Asphalt(c, seed);
                    var col = name == "mark_zigzag" ? White : Yellow;
                    c.Line(23, -4, 41, 32, 7, col);
                    c.Line(41, 32, 23, 68, 7, col);
                    Wear(c, seed);
                    return true;
                }
                case "mark_studs":
                    Asphalt(c, seed);
                    for (int i = 0; i < 2; i++) c.RoundRect(6 + i * 32, 24, 18, 16, 3, H("e8e8e2"));
                    return true;
                case "mark_box":
                    Asphalt(c, seed);
                    c.Line(-4, -4, 68, 68, 6, Yellow);
                    c.Line(-4, 68, 68, -4, 6, Yellow);
                    Wear(c, seed);
                    return true;
                case "mark_triangle":
                    Asphalt(c, seed);
                    c.Polygon(White, 6, 4, 58, 4, 32, 62);
                    c.Polygon(H("37373a"), 16, 10, 48, 10, 32, 46);
                    Asphalt(c, seed, 0, 18, 12, 46, 44);
                    Wear(c, seed);
                    return true;
                case "mark_arrow_ahead":
                    Asphalt(c, seed);
                    c.Rect(28, 0, 8, 40, White);
                    c.Polygon(White, 16, 36, 48, 36, 32, 62);
                    Wear(c, seed);
                    return true;
                case "mark_arrow_left":   // traveller heads south (down), so their left is east (right of canvas)
                    Asphalt(c, seed);
                    c.Rect(28, 0, 8, 30, White);
                    c.Line(32, 26, 48, 44, 8, White);
                    c.Polygon(White, 40, 50, 60, 56, 54, 36);
                    Wear(c, seed);
                    return true;
                case "mark_arrow_right":
                    Asphalt(c, seed);
                    c.Rect(28, 0, 8, 30, White);
                    c.Line(32, 26, 16, 44, 8, White);
                    c.Polygon(White, 24, 50, 4, 56, 10, 36);
                    Wear(c, seed);
                    return true;
                case "mark_hatch":
                    Asphalt(c, seed);
                    for (int k = -64; k < 128; k += 32) c.Bar(k, 64, k + 64, 0, 8, White);
                    Wear(c, seed);
                    return true;

                // ---------------------------------------------------------- walls & roofs
                case "red_brick": Bricks(c, seed, H("9a3f2c"), H("b3523a"), H("c9bfb0")); return true;
                case "yellow_brick": Bricks(c, seed, H("c3a36a"), H("d8bd85"), H("b8ae9d")); return true;
                case "blue_brick": Bricks(c, seed, H("33343c"), H("484a55"), H("8d8c88")); return true;
                case "sandstone":
                    Stone(c, seed, H("cbb183"), 0.12f);
                    for (int r = 0; r < 4; r++)
                    {
                        c.Rect(0, r * 16, S, 1.5f, H("9c8662"));
                        int off = (r % 2) * 24;
                        c.Rect(off + 10, r * 16, 1.5f, 16, H("9c8662"));
                        c.Rect(off + 42, r * 16, 1.5f, 16, H("9c8662"));
                    }
                    return true;
                case "render": Grainy(c, seed, H("e6dcc0"), H("d8cdaf"), 0.25f); return true;
                case "grey_render": Grainy(c, seed, H("b9b8b2"), H("a7a6a0"), 0.25f); return true;
                case "stucco": Grainy(c, seed, H("f1efe8"), H("e4e1d8"), 0.2f); c.Rect(0, 30, S, 1, H("d6d2c7")); return true;
                case "pebbledash": Pebbledash(c, seed); return true;
                case "timber_cladding":
                    for (int r = 0; r < 4; r++)
                        for (int y = 0; y < 16; y++)
                            for (int x = 0; x < S; x++)
                            {
                                float g = Noise.Fbm(x * 0.3f, (r * 16 + y) * 3f, S, 8, 3, seed + r);
                                var col = Rgba.Lerp(H("6d5a44"), H("8c7558"), g).Scale(y < 2 ? 0.7f : y > 13 ? 1.08f : 1f);
                                c.Set(x, r * 16 + y, col);
                            }
                    return true;
                case "corrugated":
                    c.Each((x, y, _) =>
                    {
                        float s = 0.5f + 0.5f * Mathf.Sin(x / 64f * Mathf.PI * 2f * 8f);
                        return Rgba.Lerp(H("7d8487"), H("b4babd"), s).Add((Noise.Fbm(x, y, S, 8, 2, seed) - 0.5f) * 0.1f);
                    });
                    return true;
                case "slate": Slates(c, seed, H("3d424b"), H("555b66"), 0.9f); return true;
                case "roof_tile": Slates(c, seed, H("8f3f28"), H("ad5634"), 0.8f, true); return true;
                case "felt_roof": Gravel(c, seed, H("4a4a4c")); return true;

                // ---------------------------------------------------------- interiors
                case "planks": Planks(c, seed, H("8e6a42"), H("a98153")); return true;
                case "bench_wood": Planks(c, seed, H("7a5532"), H("99704a")); return true;
                case "plaster": Grainy(c, seed, H("efede6"), H("e6e3da"), 0.15f); return true;
                case "wallpaper":
                    c.Fill(H("c9d3c0"));
                    for (int x = 0; x < S; x += 16) c.Rect(x + 6, 0, 3, S, H("b4c0a9"));
                    for (int y = 0; y < S; y += 16) for (int x = 0; x < S; x += 16) c.Circle(x + 7.5f, y + 8, 2.4f, H("a26b6b"));
                    return true;
                case "floor_tile":
                    for (int y = 0; y < S; y++)
                        for (int x = 0; x < S; x++)
                        {
                            bool dark = ((x / 16) + (y / 16)) % 2 == 0;
                            var col = dark ? H("8f3a2c") : H("e3dccb");
                            col = col.Add((Noise.White(x, y, seed) - 0.5f) * 0.05f);
                            if (x % 16 == 0 || y % 16 == 0) col = H("6c665c");
                            c.Set(x, y, col);
                        }
                    return true;
                case "white_tiles":
                    for (int y = 0; y < S; y++)
                        for (int x = 0; x < S; x++)
                            c.Set(x, y, (x % 16 == 0 || y % 16 == 0) ? H("c9c9c4") : H("f4f4f1").Add((Noise.White(x, y, seed) - 0.5f) * 0.03f));
                    return true;
                case "carpet":
                    c.Each((x, y, _) => Rgba.Lerp(H("6f2430"), H("85303c"), Noise.Fbm(x, y, S, 32, 2, seed) * 0.6f + Noise.White(x, y, seed) * 0.4f));
                    return true;

                // ---------------------------------------------------------- glazing & doors
                case "glass": Glass(c, seed, H("c7dde6", 0.28f)); c.Rect(0, 0, S, 2, H("dde8ec", 0.6f)); return true;
                case "window": Window(c, seed); return true;
                case "office_glass":
                    Glass(c, seed, H("3f6378", 0.62f));
                    c.Rect(0, 0, S, 3, H("8a9399"));
                    c.Rect(0, 0, 3, S, H("8a9399"));
                    c.Rect(0, 40, S, 2, H("8a9399"));
                    return true;
                case "door_red_bottom": Door(c, seed, H("8e1a22"), false); return true;
                case "door_red_top": Door(c, seed, H("8e1a22"), true); return true;
                case "door_blue_bottom": Door(c, seed, H("1d3264"), false); return true;
                case "door_blue_top": Door(c, seed, H("1d3264"), true); return true;
                case "door_green_bottom": Door(c, seed, H("1f4a32"), false); return true;
                case "door_green_top": Door(c, seed, H("1f4a32"), true); return true;
                case "door_black_bottom": Door(c, seed, H("1a1a1c"), false); return true;
                case "door_black_top": Door(c, seed, H("1a1a1c"), true); return true;
                case "shopfront":
                    Painted(c, seed, H("1d4a35"));
                    c.Rect(3, 3, 58, 2, H("c9a64a"));
                    c.Rect(3, 59, 58, 2, H("c9a64a"));
                    c.Rect(8, 12, 48, 40, H("173d2b"));
                    c.Rect(8, 12, 48, 1, H("2d6448"));
                    return true;
                case "shop_sign":
                    Painted(c, seed, H("7c1420"));
                    c.Rect(0, 2, S, 2, H("c9a64a"));
                    c.Rect(0, 60, S, 2, H("c9a64a"));
                    c.TextFit("NEWS", 32, 42, 22, 58, H("f2e6c4"));
                    return true;

                case "pub_fascia":
                    Painted(c, seed, H("173d2b"));
                    c.Rect(0, 3, S, 2, H("c9a64a"));
                    c.Rect(0, 59, S, 2, H("c9a64a"));
                    c.TextFit("RED LION", 32, 39, 14, 58, H("e7c95a"));
                    return true;
                case "pub_board":
                    Painted(c, seed, H("1b1b1d"));
                    c.Rect(4, 4, 56, 56, H("c9a64a"));
                    c.Rect(7, 7, 50, 50, H("7c1420"));
                    // A rampant (if rather blocky) lion.
                    c.Polygon(H("e7c95a"), 18, 50, 22, 30, 18, 22, 26, 14, 34, 16, 38, 24, 46, 22, 44, 30, 38, 34, 42, 50, 36, 50, 32, 38, 26, 40, 24, 50);
                    c.TextFit("THE RED LION", 32, 56, 5, 46, H("e7c95a"));
                    return true;

                // ---------------------------------------------------------- street furniture
                case "phonebox":
                    Painted(c, seed, H("b3191b"));
                    for (int r = 0; r < 6; r++)
                        for (int col = 0; col < 3; col++)
                            c.Rect(10 + col * 15, 4 + r * 10, 13, 8, H("8fb0b8", 1f));
                    c.Rect(8, 0, 1.5f, S, H("8c1214"));
                    c.Rect(55, 0, 1.5f, S, H("8c1214"));
                    return true;
                case "phonebox_crown":
                    Painted(c, seed, H("b3191b"));
                    c.Rect(4, 16, 56, 18, H("f1ede0"));
                    c.TextFit("TELEPHONE", 32, 30, 11, 52, Black);
                    c.Circle(32, 50, 6, H("c9a64a"));
                    return true;
                case "phonebox_top": Painted(c, seed, H("a8171a")); c.Circle(32, 32, 10, H("c9a64a")); return true;
                case "postbox":
                    Painted(c, seed, H("b8141a"));
                    c.Rect(0, 56, S, 8, Black);
                    c.Rect(14, 10, 36, 5, Black);
                    c.RoundRect(18, 22, 28, 22, 3, H("1b1b1d"));
                    c.TextFit("ROYAL MAIL", 32, 36, 6, 26, H("e7c95a"));
                    return true;
                case "postbox_top": Painted(c, seed, H("a81218")); c.Ring(32, 32, 18, 24, H("8c0f14")); return true;
                case "metal_grey": Metal(c, seed, H("8f9599")); return true;
                case "metal_black": Metal(c, seed, H("222326")); return true;
                case "metal_dark": Metal(c, seed, H("3b3f44")); return true;
                case "pole_grey": Metal(c, seed, H("7f878c")); return true;
                case "pole_striped":
                    for (int y = 0; y < S; y++)
                        for (int x = 0; x < S; x++)
                            c.Set(x, y, ((y / 16) % 2 == 0 ? H("161618") : H("f0f0ec")).Add((Noise.White(x, y, seed) - 0.5f) * 0.04f));
                    return true;
                case "yellow_box": Painted(c, seed, H("f0c419")); return true;
                case "lamp_lens": c.Fill(H("fff1c4")); c.Circle(32, 32, 22, H("fffbe8")); return true;
                case "led_lens":
                    c.Fill(H("3b3f44"));
                    for (int y = 0; y < 3; y++) for (int x = 0; x < 6; x++) c.Circle(10 + x * 9, 20 + y * 12, 3.4f, H("fdfdf5"));
                    return true;

                // ---------------------------------------------------------- signals (faces stretched over their boxes)
                case "signal_board":
                    c.Fill(Black);
                    c.Rect(0, 0, S, 3, White); c.Rect(0, S - 3, S, 3, White);
                    c.Rect(0, 0, 5, S, White); c.Rect(S - 5, 0, 5, S, White);
                    return true;
                case "signal_off": SignalHead(c, -1); return true;
                case "signal_red": SignalHead(c, 0); return true;
                case "signal_redamber": SignalHead(c, 3); return true;
                case "signal_green": SignalHead(c, 2); return true;
                case "signal_amber": SignalHead(c, 1); return true;
                case "ped_off": PedHead(c, -1); return true;
                case "ped_red": PedHead(c, 0); return true;
                case "ped_green": PedHead(c, 1); return true;
                case "push_button":
                    Painted(c, seed, H("f0c419"));
                    c.RoundRect(10, 8, 44, 20, 4, Black);
                    c.TextFit("WAIT", 32, 23, 10, 36, H("ff8a2a"));
                    c.Circle(32, 46, 8, H("4a4a4c"));
                    return true;
                case "belisha_off": Globe(c, H("c8781c"), false); return true;
                case "belisha_on": Globe(c, H("ffb32c"), true); return true;
                case "wigwag_off": WigWag(c, -1); return true;
                case "wigwag_l": WigWag(c, 0); return true;
                case "wigwag_r": WigWag(c, 1); return true;
                case "vas_off": c.Fill(H("101012")); Dots(c, H("26262a")); return true;
                case "vas_30":
                    c.Fill(H("101012")); Dots(c, H("26262a"));
                    c.Ring(32, 32, 22, 28, H("ff3a2a"));
                    c.TextFit("30", 32, 42, 20, 34, H("fff6e0"));
                    return true;
                case "vas_slow":
                    c.Fill(H("101012")); Dots(c, H("26262a"));
                    c.TextFit("SLOW", 32, 28, 15, 56, H("ffb02a"));
                    c.TextFit("DOWN", 32, 50, 15, 56, H("ffb02a"));
                    return true;
                case "lanesig_off": c.Fill(H("0e0e10")); Dots(c, H("1e1e22")); return true;
                case "lanesig_60": LaneSig(c, "60"); return true;
                case "lanesig_50": LaneSig(c, "50"); return true;
                case "lanesig_40": LaneSig(c, "40"); return true;
                case "lanesig_x":
                    c.Fill(H("0e0e10")); Dots(c, H("1e1e22"));
                    c.Line(16, 16, 48, 48, 7, H("ff2a22"));
                    c.Line(48, 16, 16, 48, 7, H("ff2a22"));
                    return true;
                case "lanesig_arrow":
                    c.Fill(H("0e0e10")); Dots(c, H("1e1e22"));
                    c.Line(44, 18, 22, 40, 6, H("fff3d6"));
                    c.Polygon(H("fff3d6"), 14, 48, 14, 30, 32, 48);
                    return true;
                case "gantry_truss":
                    c.Fill(Rgba.Clear);
                    c.Rect(0, 0, S, 6, H("8a9095")); c.Rect(0, S - 6, S, 6, H("8a9095"));
                    c.Bar(0, 6, 32, 58, 5, H("7d8388")); c.Bar(32, 58, 64, 6, 5, H("7d8388"));
                    return true;
                case "gantry_leg": Metal(c, seed, H("8a9095")); return true;

                // ---------------------------------------------------------- props
                case "gatso_front":
                    Painted(c, seed, H("f0c419"));
                    c.RoundRect(10, 10, 44, 20, 3, H("1d1f22"));
                    c.Circle(22, 20, 7, H("3b5361"));
                    c.Rect(36, 14, 14, 12, H("dfe6ea"));
                    c.Rect(10, 40, 44, 14, H("1d1f22"));
                    return true;
                case "gatso_back":
                    Painted(c, seed, H("f0c419"));
                    for (int k = -64; k < 128; k += 16) c.Bar(k, 64, k + 64, 0, 6, H("1d1f22"));
                    return true;
                case "specs_front":
                    Painted(c, seed, H("f0c419"));
                    c.Circle(32, 32, 14, H("1d1f22"));
                    c.Circle(32, 32, 8, H("3b5361"));
                    return true;
                case "cctv_front": c.Fill(H("dcdcd8")); c.Circle(32, 32, 16, H("1d1f22")); c.Circle(28, 28, 5, H("516a78")); return true;
                case "cctv_body": Painted(c, seed, H("dcdcd8")); return true;
                case "bollard":
                    Painted(c, seed, H("1c1c1e"));
                    c.Rect(0, 8, S, 6, H("e8e8e2"));
                    c.Rect(0, 0, S, 3, H("c9a64a"));
                    return true;
                case "bollard_white": Painted(c, seed, H("ecece6")); return true;
                case "keepleft_bollard":
                    Painted(c, seed, H("ecece6"));
                    c.Circle(32, 24, 18, H("1f4fa8"));
                    c.Line(40, 14, 24, 32, 5, White);
                    c.Polygon(White, 18, 38, 18, 24, 30, 38);
                    c.Rect(0, 52, S, 12, H("1f4fa8"));
                    return true;
                case "guardrail":
                    c.Fill(Rgba.Clear);
                    c.Rect(0, 2, S, 6, H("d8dcdc")); c.Rect(0, 54, S, 6, H("d8dcdc"));
                    for (int x = 2; x < S; x += 8) c.Rect(x, 8, 3, 46, H("d8dcdc"));
                    c.Rect(0, 0, 4, S, H("c4c9c9"));
                    return true;
                case "railings":
                    c.Fill(Rgba.Clear);
                    c.Rect(0, 6, S, 4, H("18181a")); c.Rect(0, 54, S, 4, H("18181a"));
                    for (int x = 3; x < S; x += 10)
                    {
                        c.Rect(x, 6, 3, 58, H("18181a"));
                        c.Polygon(H("18181a"), x - 2, 6, x + 5, 6, x + 1.5f, 0);
                    }
                    return true;
                case "armco":
                    c.Each((x, y, _) =>
                    {
                        float s = 0.5f + 0.5f * Mathf.Cos(y / 64f * Mathf.PI * 2f * 2f);
                        return Rgba.Lerp(H("8c9294"), H("c9cfd1"), s);
                    });
                    for (int x = 0; x < S; x += 32) c.Circle(x + 8, 32, 2.5f, H("5b6164"));
                    return true;
                case "concrete_barrier": Concrete(c, seed, H("b3b1aa")); return true;
                case "bin_side":
                    Painted(c, seed, H("1c1c1e"));
                    c.Rect(0, 6, S, 4, H("c9a64a"));
                    c.Rect(12, 16, 40, 8, H("101012"));
                    c.TextFit("LITTER", 32, 44, 8, 40, H("c9a64a"));
                    return true;
                case "bin_top": Painted(c, seed, H("1c1c1e")); c.Circle(32, 32, 14, H("0b0b0c")); return true;
                case "gritbin_side":
                    Painted(c, seed, H("e5b916"));
                    c.TextFit("GRIT", 32, 40, 14, 44, Black);
                    return true;
                case "gritbin_top": Painted(c, seed, H("e5b916")); c.Rect(0, 30, S, 3, H("b8920f")); return true;
                case "cabinet_front":
                    Painted(c, seed, H("2c4a37"));
                    c.Rect(31, 6, 2, 52, H("1f3528"));
                    c.Rect(26, 30, 3, 8, H("9a9a9a")); c.Rect(35, 30, 3, 8, H("9a9a9a"));
                    return true;
                case "cabinet_side": Painted(c, seed, H("2c4a37")); return true;
                case "cone":
                    c.Fill(H("f05a1a"));
                    c.Rect(0, 20, S, 16, H("f2f2ee"));
                    return true;
                case "barrier_redwhite":
                    for (int y = 0; y < S; y++)
                        for (int x = 0; x < S; x++)
                            c.Set(x, y, ((x + y) / 16) % 2 == 0 ? H("d42a24") : H("f2f2ee"));
                    return true;
                case "sos_front":
                    Painted(c, seed, H("e8701c"));
                    c.RoundRect(14, 8, 36, 14, 3, H("f4f4ee"));
                    c.TextFit("SOS", 32, 20, 10, 30, H("d42a24"));
                    c.RoundRect(18, 30, 28, 26, 3, H("3a3a3c"));
                    return true;
                case "sos_side": Painted(c, seed, H("e8701c")); return true;
                case "marker_post":
                    Painted(c, seed, H("f2f2ee"));
                    c.Rect(0, 8, S, 10, H("d42a24"));
                    return true;
                case "ev_front":
                    Painted(c, seed, H("f2f2ee"));
                    c.Rect(0, 0, S, 10, H("1f8f4a"));
                    c.RoundRect(16, 18, 32, 18, 3, H("1d1f22"));
                    c.Circle(32, 48, 7, H("3a3a3c"));
                    return true;
                case "ev_side": Painted(c, seed, H("e6e6e0")); return true;
            }
            return false;
        }

        // ------------------------------------------------------------------ material helpers

        private static void Grass(PixelCanvas c, int seed)
        {
            c.Each((x, y, _) =>
            {
                float n = Noise.Fbm(x, y, S, 4, 4, seed);
                float blade = Noise.White(x, y, seed);
                var col = Rgba.Lerp(H("3f7a26"), H("6aa23c"), n);
                if (blade > 0.86f) col = col.Scale(1.18f);
                else if (blade < 0.1f) col = col.Scale(0.78f);
                return col;
            });
        }

        private static void Dirt(PixelCanvas c, int seed)
        {
            c.Each((x, y, _) =>
            {
                float n = Noise.Fbm(x, y, S, 8, 4, seed);
                var col = Rgba.Lerp(H("5e4330"), H("85623f"), n);
                float w = Noise.White(x, y, seed + 9);
                if (w > 0.93f) col = H("9a8b7a");
                return col;
            });
        }

        private static void Stone(PixelCanvas c, int seed, Rgba baseCol, float var)
        {
            c.Each((x, y, _) =>
            {
                float n = Noise.Fbm(x, y, S, 4, 5, seed);
                float w = Noise.White(x, y, seed);
                return baseCol.Add((n - 0.5f) * var * 1.4f + (w - 0.5f) * 0.06f);
            });
        }

        private static void Grainy(PixelCanvas c, int seed, Rgba a, Rgba b, float strength)
        {
            c.Each((x, y, _) =>
            {
                float n = Noise.Fbm(x, y, S, 8, 4, seed);
                var col = Rgba.Lerp(a, b, n);
                return col.Add((Noise.White(x, y, seed) - 0.5f) * strength * 0.25f);
            });
        }

        private static void Gravel(PixelCanvas c, int seed, Rgba baseCol)
        {
            c.Each((x, y, _) => baseCol.Add((Noise.Fbm(x, y, S, 16, 2, seed) - 0.5f) * 0.25f + (Noise.White(x, y, seed) - 0.5f) * 0.18f));
            var r = new System.Random(seed);
            for (int i = 0; i < 70; i++)
            {
                float x = r.Next(S), y = r.Next(S), s = 1.5f + (float)r.NextDouble() * 2.5f;
                c.Circle(x, y, s, baseCol.Add(((float)r.NextDouble() - 0.4f) * 0.3f));
            }
        }

        private static void Leaves(PixelCanvas c, int seed, Rgba dark, Rgba light, float holes)
        {
            var r = new System.Random(seed);
            c.Each((x, y, _) => Rgba.Lerp(dark, light, Noise.Fbm(x, y, S, 8, 3, seed)).Scale(0.8f));
            for (int i = 0; i < 160; i++)
            {
                float x = r.Next(S), y = r.Next(S);
                var col = Rgba.Lerp(dark, light, (float)r.NextDouble());
                c.Circle(x, y, 2f + (float)r.NextDouble() * 2.2f, col);
            }
            // Punch holes straight through (blending zero alpha would not clear them).
            var r2 = new System.Random(seed + 1);
            for (int i = 0; i < (int)(holes * 220); i++)
            {
                int hx = r2.Next(S), hy = r2.Next(S), rad = 1 + r2.Next(2);
                for (int dy = -rad; dy <= rad; dy++)
                    for (int dx = -rad; dx <= rad; dx++)
                        if (dx * dx + dy * dy <= rad * rad) c.Set(((hx + dx) % S + S) % S, ((hy + dy) % S + S) % S, Rgba.Clear);
            }
        }

        public static void Asphalt(PixelCanvas c, int seed, float lift = 0f)
        {
            c.Each((x, y, _) =>
            {
                float n = Noise.Fbm(x, y, S, 4, 4, seed);
                float w = Noise.White(x, y, seed);
                var col = H("38393c").Add((n - 0.5f) * 0.08f + lift);
                if (w > 0.9f) col = col.Add(0.12f + (w - 0.9f) * 1.2f);
                else if (w < 0.08f) col = col.Add(-0.05f);
                return col;
            });
        }

        /// <summary>Re-fills a rectangle with asphalt (used to cut the inside out of outlined markings).</summary>
        private static void Asphalt(PixelCanvas c, int seed, int lift, int x0, int y0, int x1, int y1)
        {
            for (int y = y0; y < y1; y++)
                for (int x = x0; x < x1; x++)
                {
                    float px = x + 0.5f, py = y + 0.5f;
                    // Only inside the inner triangle of the give way marking.
                    float t = (py - 10f) / 36f;
                    if (t < 0 || t > 1) continue;
                    float half = 16f * (1 - t);
                    if (Mathf.Abs(px - 32f) > half - 5f) continue;
                    float n = Noise.Fbm(x, y, S, 4, 4, seed);
                    c.Set(x, y, H("38393c").Add((n - 0.5f) * 0.08f + lift));
                }
        }

        /// <summary>Road paint: slightly worn white or yellow with aggregate showing through.</summary>
        private static void Paint(PixelCanvas c, float x, float y, float w, float h, Rgba col, int seed)
        {
            c.Rect(x, y, w, h, col);
            int x0 = Mathf.FloorToInt(x), y0 = Mathf.FloorToInt(y);
            for (int py = y0; py < y0 + h; py++)
                for (int px = x0; px < x0 + w; px++)
                {
                    if (px < 0 || py < 0 || px >= S || py >= S) continue;
                    float wear = Noise.Fbm(px, py, S, 8, 3, seed + 77);
                    float g = Noise.White(px, py, seed + 5);
                    if (wear > 0.68f || g > 0.94f) c.Set(px, py, Rgba.Lerp(c.Get(px, py), H("5a5a5c"), 0.6f));
                }
        }

        private static void Wear(PixelCanvas c, int seed)
        {
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    var p = c.Get(x, y);
                    if (p.r + p.g < 0.8f) continue;
                    if (Noise.White(x, y, seed + 5) > 0.95f || Noise.Fbm(x, y, S, 8, 3, seed + 77) > 0.7f)
                        c.Set(x, y, Rgba.Lerp(p, H("5a5a5c"), 0.6f));
                }
        }

        private static void Concrete(PixelCanvas c, int seed, Rgba baseCol)
        {
            c.Each((x, y, _) =>
            {
                float n = Noise.Fbm(x, y, S, 4, 4, seed);
                float w = Noise.White(x, y, seed);
                var col = baseCol.Add((n - 0.5f) * 0.1f + (w - 0.5f) * 0.05f);
                if (w > 0.985f) col = col.Add(-0.15f);
                return col;
            });
        }

        private static void Slabs(PixelCanvas c, int seed, Rgba baseCol, int perSide)
        {
            int size = S / perSide;
            c.Each((x, y, _) =>
            {
                int sx = x / size, sy = y / size;
                float tone = (Noise.Hash(sx, sy, seed) - 0.5f) * 0.08f;
                float n = Noise.Fbm(x, y, S, 8, 3, seed) - 0.5f;
                var col = baseCol.Add(tone + n * 0.06f + (Noise.White(x, y, seed) - 0.5f) * 0.05f);
                int lx = x % size, ly = y % size;
                if (lx == 0 || ly == 0) col = baseCol.Scale(0.62f);
                else if (lx == 1 || ly == 1) col = col.Scale(1.06f);
                else if (lx == size - 1 || ly == size - 1) col = col.Scale(0.9f);
                return col;
            });
        }

        private static void Tactile(PixelCanvas c, int seed, Rgba baseCol)
        {
            Slabs(c, seed, baseCol, 2);
            for (int y = 0; y < 8; y++)
                for (int x = 0; x < 8; x++)
                {
                    float cx = 4 + x * 8, cy = 4 + y * 8;
                    c.Circle(cx + 0.6f, cy + 0.6f, 2.6f, baseCol.Scale(0.7f));
                    c.Circle(cx, cy, 2.4f, baseCol.Scale(1.15f));
                }
        }

        private static void Setts(PixelCanvas c, int seed)
        {
            c.Fill(H("4b4a48"));
            for (int r = 0; r < 6; r++)
            {
                int off = (r % 2) * 8;
                for (int i = -1; i < 4; i++)
                {
                    float x = off + i * 16, y = r * 10.67f;
                    var tone = H("858582").Add((Noise.Hash(i + 10, r, seed) - 0.5f) * 0.18f);
                    c.RoundRect(x + 1, y + 1, 14, 8.6f, 2.5f, tone);
                }
            }
            Speckle(c, seed, 0.05f);
        }

        private static void BlockPaving(PixelCanvas c, int seed)
        {
            c.Fill(H("6e5a4a"));
            for (int by = 0; by < 4; by++)
                for (int bx = 0; bx < 4; bx++)
                {
                    float x = bx * 16, y = by * 16;
                    bool horiz = (bx + by) % 2 == 0;
                    var tone = H("a2826a").Add((Noise.Hash(bx, by, seed) - 0.5f) * 0.12f);
                    var tone2 = H("a2826a").Add((Noise.Hash(bx + 7, by, seed) - 0.5f) * 0.12f);
                    if (horiz) { c.Rect(x + 0.7f, y + 0.7f, 14.6f, 6.6f, tone); c.Rect(x + 0.7f, y + 8.7f, 14.6f, 6.6f, tone2); }
                    else { c.Rect(x + 0.7f, y + 0.7f, 6.6f, 14.6f, tone); c.Rect(x + 8.7f, y + 0.7f, 6.6f, 14.6f, tone2); }
                }
            Speckle(c, seed, 0.05f);
        }

        private static void Speckle(PixelCanvas c, int seed, float amount)
        {
            c.Each((x, y, p) => p.WithAlpha(1f).Add((Noise.White(x, y, seed + 3) - 0.5f) * amount));
        }

        private static void Bricks(PixelCanvas c, int seed, Rgba dark, Rgba light, Rgba mortar)
        {
            const int courseH = 8, brickW = 16;
            c.Each((x, y, _) => mortar.Add((Noise.White(x, y, seed) - 0.5f) * 0.08f - 0.04f));
            for (int row = 0; row < S / courseH; row++)
            {
                int off = (row % 2) * (brickW / 2);
                for (int b = -1; b <= S / brickW; b++)
                {
                    float t = Noise.Hash(b + 50, row, seed);
                    var col = Rgba.Lerp(dark, light, t);
                    if (Noise.Hash(b, row + 90, seed) > 0.9f) col = col.Scale(0.78f);
                    int x0 = b * brickW + off, y0 = row * courseH;
                    for (int y = y0; y < y0 + courseH - 2; y++)
                        for (int x = x0; x < x0 + brickW - 2; x++)
                        {
                            if (x < 0 || x >= S) continue;
                            float n = Noise.Fbm(x, y, S, 16, 2, seed + 11) - 0.5f;
                            float g = Noise.White(x, y, seed + 2) - 0.5f;
                            var p = col.Add(n * 0.12f + g * 0.08f);
                            if (y == y0) p = p.Scale(1.1f);
                            else if (y == y0 + courseH - 3) p = p.Scale(0.88f);
                            c.Set(x, y, p);
                        }
                }
            }
        }

        private static void Pebbledash(PixelCanvas c, int seed)
        {
            Grainy(c, seed, H("cfc8b6"), H("bdb5a1"), 0.3f);
            var r = new System.Random(seed);
            for (int i = 0; i < 420; i++)
            {
                float x = r.Next(S), y = r.Next(S);
                var tone = H("b0a68e").Add(((float)r.NextDouble() - 0.5f) * 0.25f);
                c.Circle(x + 0.5f, y + 0.5f, 0.9f + (float)r.NextDouble() * 0.9f, tone);
            }
        }

        private static void Slates(PixelCanvas c, int seed, Rgba dark, Rgba light, float roughness, bool rounded = false)
        {
            const int rowH = 16, w = 16;
            c.Fill(dark.Scale(0.6f));
            for (int row = 0; row < S / rowH; row++)
            {
                int off = (row % 2) * (w / 2);
                for (int i = -1; i <= S / w; i++)
                {
                    var col = Rgba.Lerp(dark, light, Noise.Hash(i + 20, row, seed));
                    int x0 = i * w + off, y0 = row * rowH;
                    for (int y = y0; y < y0 + rowH; y++)
                        for (int x = x0; x < x0 + w - 1; x++)
                        {
                            if (x < 0 || x >= S) continue;
                            float ly = (y - y0) / (float)rowH;
                            float n = Noise.Fbm(x, y, S, 16, 2, seed + 5) - 0.5f;
                            // Lower edge of each slate catches light; top is shadowed by the row above.
                            var p = col.Add(n * 0.1f * roughness).Scale(0.78f + ly * 0.3f);
                            if (rounded && y > y0 + rowH - 4)
                            {
                                float lx = (x - x0 + 0.5f) / (w - 1) * 2 - 1;
                                if (Mathf.Abs(lx) > 0.8f) p = p.Scale(0.7f);
                            }
                            c.Set(x, y, p);
                        }
                    for (int y = y0; y < y0 + 2; y++)
                        for (int x = x0; x < x0 + w; x++)
                            if (x >= 0 && x < S) c.Set(x, y, c.Get(x, y).Scale(0.7f));
                }
            }
        }

        private static void Planks(PixelCanvas c, int seed, Rgba dark, Rgba light)
        {
            for (int row = 0; row < 4; row++)
            {
                var tone = Rgba.Lerp(dark, light, Noise.Hash(row, 3, seed));
                int seam = (int)(Noise.Hash(row, 7, seed) * S);
                for (int y = row * 16; y < row * 16 + 16; y++)
                    for (int x = 0; x < S; x++)
                    {
                        float grain = Noise.Fbm(x * 0.25f, y * 3f, S, 8, 3, seed + row);
                        var p = Rgba.Lerp(tone.Scale(0.85f), tone.Scale(1.1f), grain);
                        if (y == row * 16 || x == seam) p = tone.Scale(0.55f);
                        c.Set(x, y, p);
                    }
            }
        }

        private static void Painted(PixelCanvas c, int seed, Rgba col)
        {
            c.Each((x, y, _) => col.Add((Noise.Fbm(x, y, S, 8, 3, seed) - 0.5f) * 0.05f + (Noise.White(x, y, seed) - 0.5f) * 0.025f));
        }

        private static void Metal(PixelCanvas c, int seed, Rgba col)
        {
            c.Each((x, y, _) => col.Add((Noise.Fbm(x * 0.2f, y, S, 16, 2, seed) - 0.5f) * 0.08f + (Noise.White(x, y, seed) - 0.5f) * 0.03f));
        }

        private static void Glass(PixelCanvas c, int seed, Rgba tint)
        {
            c.Each((x, y, _) =>
            {
                float refl = Mathf.Clamp01(1f - Mathf.Abs((x - y) / 64f - 0.15f) * 4f);
                return Rgba.Lerp(tint, H("ffffff", tint.a + 0.25f), refl * 0.35f);
            });
        }

        private static void Window(PixelCanvas c, int seed)
        {
            Glass(c, seed, H("3c5566", 0.55f));
            var frame = H("f2f2ee");
            c.Rect(0, 0, S, 5, frame); c.Rect(0, S - 6, S, 6, frame);
            c.Rect(0, 0, 5, S, frame); c.Rect(S - 5, 0, 5, S, frame);
            c.Rect(0, 30, S, 5, frame);            // meeting rail of the sash
            c.Rect(30, 0, 3, S, frame.Scale(0.95f)); // glazing bar
            c.Rect(0, S - 6, S, 2, H("c8c8c2"));
        }

        private static void Door(PixelCanvas c, int seed, Rgba col, bool top)
        {
            Painted(c, seed, col);
            var frame = H("efefe9");
            c.Rect(0, 0, 4, S, frame); c.Rect(S - 4, 0, 4, S, frame);
            var shadow = col.Scale(0.6f);
            var hi = col.Scale(1.25f);
            if (top)
            {
                c.Rect(0, 0, S, 4, frame);
                // Fanlight then two upper panels.
                c.Rect(8, 8, 48, 12, H("3c5566"));
                c.Rect(8, 8, 48, 2, frame);
                for (int i = 0; i < 2; i++)
                {
                    c.Rect(10 + i * 24, 26, 20, 34, shadow);
                    c.Rect(12 + i * 24, 28, 16, 30, hi.Scale(0.85f));
                }
            }
            else
            {
                for (int i = 0; i < 2; i++)
                {
                    c.Rect(10 + i * 24, 30, 20, 28, shadow);
                    c.Rect(12 + i * 24, 32, 16, 24, hi.Scale(0.85f));
                }
                var brass = H("d4b04a");
                c.Rect(20, 12, 24, 6, brass);         // letterbox
                c.Circle(50, 22, 3, brass);           // knob
                c.Rect(4, 60, 56, 4, H("2a2a2a"));    // threshold shadow
            }
        }

        private static void Globe(PixelCanvas c, Rgba col, bool lit)
        {
            c.Fill(col);
            c.Circle(24, 22, lit ? 16 : 10, col.Scale(lit ? 1.25f : 1.12f).WithAlpha(1));
            if (lit) c.Circle(22, 20, 7, H("fff2c8"));
        }

        /// <summary>Signal head face (box is 0.36 wide x 0.88 tall, so the lenses are drawn as ellipses).</summary>
        private static void SignalHead(PixelCanvas c, int lit)
        {
            c.Fill(H("17181a"));
            const float rx = 22f, ry = 9f;
            var off = H("2b2a26");
            Rgba[] on = { H("ff2a1e"), H("ffab1a"), H("2aff7a") };
            for (int i = 0; i < 3; i++)
            {
                float cy = 11f + i * 21f;
                bool isLit = lit == i || (lit == 3 && i <= 1);
                Ellipse(c, 32, cy + 1.2f, rx + 3, ry + 1.5f, H("0b0b0c"));
                Ellipse(c, 32, cy, rx, ry, isLit ? on[i] : Rgba.Lerp(off, on[i], 0.18f));
                if (isLit) Ellipse(c, 28, cy - 2, rx * 0.4f, ry * 0.35f, H("ffffff", 0.55f));
            }
        }

        /// <summary>Pedestrian signal face (0.44 x 0.66): red man above, green man below.</summary>
        private static void PedHead(PixelCanvas c, int lit)
        {
            c.Fill(H("17181a"));
            c.Rect(6, 4, 52, 26, H("0b0b0c"));
            c.Rect(6, 34, 52, 26, H("0b0b0c"));
            var red = lit == 0 ? H("ff2a1e") : H("4a1a16");
            var green = lit == 1 ? H("2aff7a") : H("173a22");
            Man(c, 32, 17, red, false);
            Man(c, 32, 47, green, true);
        }

        /// <summary>Little pictogram person (standing or walking), stretched for a 0.44 x 0.66 face.</summary>
        private static void Man(PixelCanvas c, float cx, float cy, Rgba col, bool walking)
        {
            Ellipse(c, cx, cy - 8, 4.2f, 2.8f, col);
            c.Rect(cx - 3.5f, cy - 5, 7, 7, col);
            if (walking)
            {
                c.Line(cx - 1, cy + 2, cx - 8, cy + 9, 3, col);
                c.Line(cx + 1, cy + 2, cx + 8, cy + 9, 3, col);
                c.Line(cx - 3, cy - 4, cx - 10, cy + 1, 2.5f, col);
                c.Line(cx + 3, cy - 4, cx + 10, cy - 1, 2.5f, col);
            }
            else
            {
                c.Rect(cx - 3, cy + 2, 2.5f, 8, col);
                c.Rect(cx + 0.5f, cy + 2, 2.5f, 8, col);
                c.Rect(cx - 6, cy - 5, 2.5f, 7, col);
                c.Rect(cx + 3.5f, cy - 5, 2.5f, 7, col);
            }
        }

        private static void WigWag(PixelCanvas c, int lit)
        {
            c.Fill(H("17181a"));
            for (int i = 0; i < 2; i++)
            {
                float cx = 16 + i * 32;
                c.Circle(cx, 32, 13, H("0b0b0c"));
                c.Circle(cx, 32, 10, lit == i ? H("ffb21a") : H("4a3510"));
            }
        }

        private static void LaneSig(PixelCanvas c, string speed)
        {
            c.Fill(H("0e0e10"));
            Dots(c, H("1e1e22"));
            c.Ring(32, 32, 21, 27, H("ff2a22"));
            c.TextFit(speed, 32, 41, 18, 30, H("fff3d6"));
        }

        private static void Dots(PixelCanvas c, Rgba col)
        {
            for (int y = 2; y < S; y += 4)
                for (int x = 2; x < S; x += 4)
                    c.Set(x, y, col);
        }

        public static void Ellipse(PixelCanvas c, float cx, float cy, float rx, float ry, Rgba col)
        {
            int x0 = Mathf.FloorToInt(cx - rx - 1), x1 = Mathf.CeilToInt(cx + rx + 1);
            int y0 = Mathf.FloorToInt(cy - ry - 1), y1 = Mathf.CeilToInt(cy + ry + 1);
            float m = Mathf.Min(rx, ry);
            for (int y = y0; y < y1; y++)
                for (int x = x0; x < x1; x++)
                {
                    float dx = (x + 0.5f - cx) / rx, dy = (y + 0.5f - cy) / ry;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    c.Blend(x, y, col, Mathf.Clamp01((1f - d) * m + 0.5f));
                }
        }
    }
}
