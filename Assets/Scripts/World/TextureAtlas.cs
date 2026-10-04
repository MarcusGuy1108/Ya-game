using System;
using UnityEngine;

namespace UKCity.World
{
    /// <summary>
    /// Builds the block texture atlas procedurally at startup, so the project ships with no image assets.
    /// Every tile is 16x16 pixel art. Pixel (0,0) is the bottom-left of a tile.
    /// </summary>
    public static class TextureAtlas
    {
        public const int TilePx = 16;
        public const int Cols = 16;
        public const int Rows = 8;
        public const int WidthPx = Cols * TilePx;
        public const int HeightPx = Rows * TilePx;
        private const float Inset = 0.0008f;

        private static Texture2D texture;

        public static Texture2D Texture
        {
            get
            {
                if (texture == null) texture = Build();
                return texture;
            }
        }

        /// <summary>UV for a point (u,v in 0..1) inside a tile. Pure maths, safe on worker threads.</summary>
        public static Vector2 Uv(int tile, float u, float v)
        {
            int col = tile % Cols;
            int row = tile / Cols;
            u = Inset + u * (1f - 2f * Inset);
            v = Inset + v * (1f - 2f * Inset);
            return new Vector2((col + u) / Cols, (row + v) / Rows);
        }

        public static Rect TileRect(int tile)
        {
            int col = tile % Cols;
            int row = tile / Cols;
            return new Rect((float)col / Cols, (float)row / Rows, 1f / Cols, 1f / Rows);
        }

        private static Texture2D Build()
        {
            var tex = new Texture2D(WidthPx, HeightPx, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                name = "BlockAtlas"
            };
            var all = new Color32[WidthPx * HeightPx];
            var tile = new Color32[TilePx * TilePx];
            for (int i = 0; i < Blocks.TileNames.Count; i++)
            {
                Array.Clear(tile, 0, tile.Length);
                Paint(Blocks.TileNames[i], tile);
                int ox = (i % Cols) * TilePx, oy = (i / Cols) * TilePx;
                for (int y = 0; y < TilePx; y++)
                    for (int x = 0; x < TilePx; x++)
                        all[(oy + y) * WidthPx + ox + x] = tile[y * TilePx + x];
            }
            tex.SetPixels32(all);
            tex.Apply(false, false);
            return tex;
        }

        // ------------------------------------------------------------------ painting

        private sealed class P
        {
            public readonly Color32[] Px;
            public readonly System.Random R;
            public P(Color32[] px, string name) { Px = px; R = new System.Random(name.GetHashCode() & 0x7fffffff); }

            public void Set(int x, int y, Color32 c) { if (x >= 0 && y >= 0 && x < 16 && y < 16) Px[y * 16 + x] = c; }
            public Color32 Get(int x, int y) => Px[y * 16 + x];
            public Color32 Vary(Color32 c, int v)
            {
                int d = R.Next(-v, v + 1);
                return new Color32(Clamp(c.r + d), Clamp(c.g + d), Clamp(c.b + d), c.a);
            }
            public void Noise(Color32 c, int v) { for (int y = 0; y < 16; y++) for (int x = 0; x < 16; x++) Set(x, y, Vary(c, v)); }
            public void Rect(int x0, int y0, int x1, int y1, Color32 c) { for (int y = y0; y <= y1; y++) for (int x = x0; x <= x1; x++) Set(x, y, c); }
            public void RectNoise(int x0, int y0, int x1, int y1, Color32 c, int v) { for (int y = y0; y <= y1; y++) for (int x = x0; x <= x1; x++) Set(x, y, Vary(c, v)); }
            public void Border(Color32 c) { for (int i = 0; i < 16; i++) { Set(i, 0, c); Set(i, 15, c); Set(0, i, c); Set(15, i, c); } }
            public void Circle(float cx, float cy, float r, Color32 c)
            {
                for (int y = 0; y < 16; y++)
                    for (int x = 0; x < 16; x++)
                    {
                        float dx = x + 0.5f - cx, dy = y + 0.5f - cy;
                        if (dx * dx + dy * dy <= r * r) Set(x, y, c);
                    }
            }
            public void Ring(float cx, float cy, float r0, float r1, Color32 c)
            {
                for (int y = 0; y < 16; y++)
                    for (int x = 0; x < 16; x++)
                    {
                        float dx = x + 0.5f - cx, dy = y + 0.5f - cy, d2 = dx * dx + dy * dy;
                        if (d2 <= r1 * r1 && d2 >= r0 * r0) Set(x, y, c);
                    }
            }
            /// <summary>Draws a 3x5 glyph; rows given top to bottom.</summary>
            public void Glyph(int x0, int yTop, string[] rows, Color32 c)
            {
                for (int r = 0; r < rows.Length; r++)
                    for (int i = 0; i < rows[r].Length; i++)
                        if (rows[r][i] == '1') Set(x0 + i, yTop - r, c);
            }
            public void FakeText(int x0, int x1, int y0, int y1, Color32 c)
            {
                for (int x = x0; x <= x1; x++)
                {
                    if (R.Next(4) == 0) continue;
                    for (int y = y0; y <= y1; y++) if (R.Next(3) != 0) Set(x, y, c);
                }
            }
        }

        private static byte Clamp(int v) => (byte)(v < 0 ? 0 : v > 255 ? 255 : v);
        private static Color32 C(int r, int g, int b, int a = 255) => new Color32((byte)r, (byte)g, (byte)b, (byte)a);

        private static readonly Color32 White = C(236, 236, 230);
        private static readonly Color32 Black = C(22, 22, 24);
        private static readonly Color32 Yellow = C(232, 190, 40);
        private static readonly Color32 SignRed = C(200, 25, 30);
        private static readonly Color32 AsphaltCol = C(58, 58, 62);

        private static readonly string[] Digit3 = { "111", "001", "111", "001", "111" };
        private static readonly string[] Digit0 = { "111", "101", "101", "101", "111" };

        private static void Paint(string name, Color32[] px)
        {
            var p = new P(px, name);
            switch (name)
            {
                case "grass_top": p.Noise(C(96, 156, 56), 14); break;
                case "dirt": p.Noise(C(128, 92, 64), 14); break;
                case "grass_side":
                    p.Noise(C(128, 92, 64), 14);
                    for (int x = 0; x < 16; x++)
                    {
                        int h = 3 + p.R.Next(0, 2);
                        for (int y = 16 - h; y < 16; y++) p.Set(x, y, p.Vary(C(96, 156, 56), 12));
                    }
                    break;
                case "stone": p.Noise(C(124, 124, 124), 16); break;
                case "bedrock": p.Noise(C(60, 60, 60), 40); break;
                case "sand": p.Noise(C(218, 204, 150), 10); break;
                case "gravel":
                    p.Noise(C(130, 126, 120), 30);
                    break;
                case "log_side":
                    p.Noise(C(98, 74, 44), 10);
                    for (int x = 0; x < 16; x += 3) for (int y = 0; y < 16; y++) if (p.R.Next(3) > 0) p.Set(x, y, C(72, 54, 32));
                    break;
                case "log_top":
                    p.Noise(C(170, 136, 86), 8);
                    p.Ring(8, 8, 2.5f, 3.2f, C(130, 100, 60));
                    p.Ring(8, 8, 5f, 5.7f, C(130, 100, 60));
                    p.Ring(8, 8, 7.2f, 9f, C(98, 74, 44));
                    break;
                case "leaves":
                    p.Noise(C(62, 118, 44), 26);
                    for (int i = 0; i < 40; i++) p.Set(p.R.Next(16), p.R.Next(16), C(0, 0, 0, 0));
                    break;
                case "hedge":
                    p.Noise(C(44, 92, 36), 22);
                    for (int i = 0; i < 10; i++) p.Set(p.R.Next(16), p.R.Next(16), C(0, 0, 0, 0));
                    break;
                case "water": p.Noise(C(48, 92, 170, 170), 8); break;

                case "asphalt":
                    p.Noise(AsphaltCol, 7);
                    for (int i = 0; i < 12; i++) p.Set(p.R.Next(16), p.R.Next(16), C(84, 84, 86));
                    break;
                case "asphalt_line_x": Paint("asphalt", px); p.RectNoise(0, 6, 15, 9, White, 6); break;
                case "asphalt_line_z": Paint("asphalt", px); p.RectNoise(6, 0, 9, 15, White, 6); break;
                case "asphalt_yellow_x": Paint("asphalt", px); p.RectNoise(0, 4, 15, 5, Yellow, 6); p.RectNoise(0, 10, 15, 11, Yellow, 6); break;
                case "asphalt_yellow_z": Paint("asphalt", px); p.RectNoise(4, 0, 5, 15, Yellow, 6); p.RectNoise(10, 0, 11, 15, Yellow, 6); break;
                case "road_paint":
                    p.Noise(White, 6);
                    for (int i = 0; i < 8; i++) p.Set(p.R.Next(16), p.R.Next(16), C(90, 90, 92));
                    break;
                case "kerb":
                    p.Noise(C(172, 170, 164), 7);
                    for (int i = 0; i < 16; i++) { p.Set(i, 0, C(130, 128, 124)); p.Set(0, i, C(140, 138, 132)); }
                    break;
                case "pavement":
                    p.Noise(C(168, 165, 158), 6);
                    for (int i = 0; i < 16; i++)
                    {
                        p.Set(i, 0, C(118, 116, 110)); p.Set(i, 8, C(118, 116, 110));
                        p.Set(0, i, C(118, 116, 110)); p.Set(8, i, C(118, 116, 110));
                    }
                    break;
                case "concrete":
                    p.Noise(C(162, 162, 156), 5);
                    for (int i = 0; i < 16; i++) p.Set(i, 15, C(140, 140, 134));
                    break;

                case "red_brick": Bricks(p, C(150, 62, 46), C(196, 186, 172), 10); break;
                case "yellow_brick": Bricks(p, C(196, 170, 112), C(170, 162, 150), 14); break;
                case "render": p.Noise(C(232, 226, 204), 4); break;
                case "pebbledash":
                    p.Noise(C(204, 198, 182), 10);
                    for (int i = 0; i < 50; i++) p.Set(p.R.Next(16), p.R.Next(16), p.Vary(C(160, 150, 130), 20));
                    break;
                case "slate": Courses(p, C(72, 78, 88), C(48, 52, 60), 6); break;
                case "roof_tile": Courses(p, C(164, 76, 50), C(120, 52, 34), 10); break;
                case "planks":
                    p.Noise(C(162, 122, 72), 8);
                    for (int y = 0; y < 16; y += 4)
                        for (int x = 0; x < 16; x++) p.Set(x, y, C(120, 88, 50));
                    for (int y = 0; y < 16; y += 4) p.Set((y * 5 + 3) % 16, y + 2, C(120, 88, 50));
                    break;
                case "plaster": p.Noise(C(240, 238, 232), 3); break;
                case "floor_tile":
                    for (int y = 0; y < 16; y++)
                        for (int x = 0; x < 16; x++)
                        {
                            bool dark = ((x / 4) + (y / 4)) % 2 == 0;
                            p.Set(x, y, dark ? p.Vary(C(150, 50, 40), 6) : p.Vary(C(225, 220, 205), 4));
                        }
                    break;
                case "carpet": p.Noise(C(128, 32, 42), 6); break;
                case "glass":
                    p.Rect(0, 0, 15, 15, C(190, 220, 235, 70));
                    p.Border(C(210, 230, 240, 150));
                    for (int i = 3; i < 8; i++) p.Set(i, 15 - i, C(255, 255, 255, 140));
                    break;
                case "window":
                    p.Rect(0, 0, 15, 15, C(70, 98, 120, 150));
                    for (int i = 4; i < 9; i++) p.Set(i, i + 4, C(200, 220, 235, 170));
                    p.Rect(0, 0, 15, 1, C(240, 240, 236));
                    p.Rect(0, 14, 15, 15, C(240, 240, 236));
                    p.Rect(0, 0, 1, 15, C(240, 240, 236));
                    p.Rect(14, 0, 15, 15, C(240, 240, 236));
                    p.Rect(0, 7, 15, 8, C(240, 240, 236));
                    break;
                case "door_red": Door(p, C(160, 24, 30)); break;
                case "door_blue": Door(p, C(28, 52, 112)); break;
                case "door_green": Door(p, C(24, 84, 52)); break;
                case "door_black": Door(p, C(26, 26, 28)); break;
                case "shopfront":
                    p.Noise(C(22, 70, 46), 4);
                    p.Rect(1, 1, 14, 1, C(200, 168, 70));
                    p.Rect(1, 14, 14, 14, C(200, 168, 70));
                    break;
                case "shop_sign":
                    p.Noise(C(150, 22, 32), 4);
                    p.Rect(0, 0, 15, 0, C(200, 168, 70));
                    p.Rect(0, 15, 15, 15, C(200, 168, 70));
                    p.FakeText(2, 13, 6, 9, White);
                    break;

                case "phonebox":
                    p.Noise(C(196, 22, 24), 6);
                    for (int r = 0; r < 4; r++)
                        for (int c = 0; c < 3; c++)
                            p.Rect(3 + c * 4, 2 + r * 3, 4 + c * 4, 3 + r * 3, C(150, 180, 190));
                    break;
                case "phonebox_crown":
                    p.Noise(C(196, 22, 24), 6);
                    p.Rect(1, 6, 14, 11, C(240, 240, 236));
                    p.FakeText(2, 13, 8, 9, Black);
                    break;
                case "phonebox_top":
                    p.Noise(C(186, 20, 22), 6);
                    p.Circle(8, 8, 3, C(200, 168, 70));
                    break;
                case "postbox":
                    p.Noise(C(192, 16, 20), 6);
                    p.Rect(0, 0, 15, 2, Black);
                    p.Rect(4, 11, 11, 11, Black);
                    p.Rect(6, 7, 9, 8, C(200, 168, 70));
                    break;
                case "postbox_top":
                    p.Noise(C(176, 14, 18), 6);
                    p.Ring(8, 8, 5, 6.5f, C(140, 10, 14));
                    break;
                case "metal_grey": p.Noise(C(140, 145, 150), 5); break;
                case "metal_black": p.Noise(C(30, 30, 32), 4); break;
                case "pole_grey": p.Noise(C(120, 126, 130), 5); break;
                case "pole_striped":
                    for (int y = 0; y < 16; y++)
                        for (int x = 0; x < 16; x++)
                            p.Set(x, y, (y / 4) % 2 == 0 ? Black : White);
                    break;
                case "lamp":
                    p.Noise(C(255, 238, 180), 6);
                    break;
                case "lamp_side":
                    p.Noise(C(60, 64, 68), 4);
                    p.Rect(1, 0, 14, 4, C(255, 238, 180));
                    break;
                case "lamp_top": p.Noise(C(60, 64, 68), 4); break;
                case "traffic_light":
                    p.Noise(C(26, 26, 28), 3);
                    p.Circle(8, 12.5f, 2.2f, C(220, 30, 30));
                    p.Circle(8, 8f, 2.2f, C(230, 150, 20));
                    p.Circle(8, 3.5f, 2.2f, C(30, 200, 70));
                    break;
                case "belisha":
                    p.Noise(C(255, 168, 30), 8);
                    p.Circle(6, 10, 2, C(255, 220, 150));
                    break;

                case "sign_giveway":
                    p.Noise(C(140, 145, 150), 4);
                    // Inverted red triangle with white centre.
                    for (int y = 0; y < 16; y++)
                        for (int x = 0; x < 16; x++)
                        {
                            float fy = (y - 1.5f) / 13f;           // 0 at the point, 1 at the top edge
                            float half = fy * 7.5f;
                            float dx = Mathf.Abs(x + 0.5f - 8f);
                            if (fy < 0f || fy > 1f || dx > half) continue;
                            bool inner = dx < half - 2.2f && fy < 0.82f && fy > 0.3f;
                            p.Set(x, y, inner ? White : SignRed);
                        }
                    break;
                case "sign_30":
                    p.Noise(C(140, 145, 150), 4);
                    p.Circle(8, 8, 7.6f, SignRed);
                    p.Circle(8, 8, 5.6f, White);
                    p.Glyph(4, 10, Digit3, Black);
                    p.Glyph(9, 10, Digit0, Black);
                    break;
                case "sign_noentry":
                    p.Noise(C(140, 145, 150), 4);
                    p.Circle(8, 8, 7.6f, SignRed);
                    p.Rect(3, 7, 12, 8, White);
                    break;
                case "sign_busstop":
                    p.Rect(0, 0, 15, 15, White);
                    p.Ring(8, 8, 3.5f, 6f, SignRed);
                    p.Rect(1, 7, 14, 8, C(20, 40, 140));
                    break;
                case "sign_street":
                    p.Rect(0, 0, 15, 15, White);
                    p.Border(Black);
                    p.FakeText(2, 13, 6, 9, Black);
                    break;

                default:
                    // Missing texture: magenta checker so it is obvious.
                    for (int y = 0; y < 16; y++)
                        for (int x = 0; x < 16; x++)
                            p.Set(x, y, ((x / 4 + y / 4) % 2 == 0) ? C(255, 0, 255) : Black);
                    break;
            }
        }

        private static void Bricks(P p, Color32 brick, Color32 mortar, int vary)
        {
            p.Noise(mortar, 4);
            for (int row = 0; row < 4; row++)
            {
                int y0 = row * 4 + 1;
                int offset = (row % 2) * 4;
                for (int b = -1; b < 3; b++)
                {
                    int x0 = b * 8 + offset + 1;
                    var c = p.Vary(brick, vary);
                    for (int y = y0; y < y0 + 3; y++)
                        for (int x = x0; x < x0 + 7; x++)
                            if (x >= 0 && x < 16) p.Set(x, y, p.Vary(c, 5));
                }
            }
        }

        private static void Courses(P p, Color32 c, Color32 line, int vary)
        {
            for (int y = 0; y < 16; y++)
                for (int x = 0; x < 16; x++)
                    p.Set(x, y, p.Vary(c, vary / 2));
            for (int row = 0; row < 4; row++)
            {
                int y = row * 4;
                for (int x = 0; x < 16; x++) p.Set(x, y, line);
                int off = (row % 2) * 3;
                for (int x = off; x < 16; x += 6) { p.Set(x, y + 1, line); p.Set(x, y + 2, line); p.Set(x, y + 3, line); }
            }
        }

        private static void Door(P p, Color32 c)
        {
            p.Noise(c, 4);
            var dark = new Color32((byte)(c.r * 0.6f), (byte)(c.g * 0.6f), (byte)(c.b * 0.6f), 255);
            p.Border(C(240, 240, 236));
            // Four raised panels.
            for (int py = 0; py < 2; py++)
                for (int px = 0; px < 2; px++)
                {
                    int x0 = 3 + px * 6, y0 = 2 + py * 7;
                    for (int i = 0; i < 4; i++) { p.Set(x0 + i, y0, dark); p.Set(x0 + i, y0 + 4, dark); }
                    for (int i = 0; i < 5; i++) { p.Set(x0, y0 + i, dark); p.Set(x0 + 3, y0 + i, dark); }
                }
            var brass = C(212, 176, 70);
            p.Set(12, 7, brass);
            p.Rect(6, 8, 9, 8, brass); // letterbox
        }
    }
}
