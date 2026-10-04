using UnityEngine;

namespace UKCity.World.Art
{
    /// <summary>
    /// Lens, glow and housing textures for traffic signals. Lenses are round (alpha cut-out) and come in pairs:
    /// "_off" is baked into the model, "_on" is overlaid by DynamicFaces when the lamp is lit.
    /// </summary>
    public static class SignalPainter
    {
        private const int S = 64;
        private static Rgba H(string hex, float a = 1f) => Rgba.Hex(hex, a);

        private static Rgba Colour(string c)
        {
            switch (c)
            {
                case "red": return H("ff2a1a");
                case "amber": return H("ffa514");
                default: return H("2bff8c");
            }
        }

        public static bool Paint(string name, PixelCanvas c)
        {
            switch (name)
            {
                case "sig_housing": Plastic(c, H("1a1b1d")); return true;
                case "sig_housing_led": Plastic(c, H("202124")); return true;
                case "sig_hood": Plastic(c, H("141516")); return true;
                case "push_button":
                    Plastic(c, H("f2c419"));
                    c.Circle(32, 42, 9, H("2a2a2c"));
                    c.Circle(32, 42, 6, H("b8b8b8"));
                    c.TextFit("PUSH BUTTON", 32, 58, 4.5f, 52, H("1a1a1a"));
                    return true;
                case "wait_off": Wait(c, false); return true;
                case "wait_on": Wait(c, true); return true;
                case "glow_red": Glow(c, H("ff3a20")); return true;
                case "glow_amber": Glow(c, H("ffaa22")); return true;
                case "glow_green": Glow(c, H("3dffa0")); return true;
                case "glow_white": Glow(c, H("fff6e8")); return true;
                case "lens_wigwag_off": Led(c, H("ffa514"), false); return true;
                case "lens_wigwag_on": Led(c, H("ffa514"), true); return true;
                case "pod_noright_off": Pod(c, true, false); return true;
                case "pod_noright_on": Pod(c, true, true); return true;
                case "pod_noleft_off": Pod(c, false, false); return true;
                case "pod_noleft_on": Pod(c, false, true); return true;
                case "lens_redman_off": Man(c, false, false, false); return true;
                case "lens_redman_on": Man(c, false, true, false); return true;
                case "lens_greenman_off": Man(c, true, false, false); return true;
                case "lens_greenman_on": Man(c, true, true, false); return true;
                case "lens_toucan_off": Man(c, true, false, true); return true;
                case "lens_toucan_on": Man(c, true, true, true); return true;
            }

            if (!name.StartsWith("lens_")) return false;
            // lens_<style>_<colour>_<on|off>, where style may itself contain an underscore (arrow_left, filter_left).
            bool on = name.EndsWith("_on");
            string body = name.Substring(5, name.LastIndexOf('_') - 5);
            if (body.StartsWith("filter_"))
            {
                Arrow(c, Colour("green"), on, body == "filter_left" ? -1 : 1);
                return true;
            }
            int us = body.LastIndexOf('_');
            if (us < 0) return false;
            string style = body.Substring(0, us), colour = body.Substring(us + 1);
            var col = Colour(colour);
            switch (style)
            {
                case "led": Led(c, col, on); return true;
                case "classic": Classic(c, col, on); return true;
                case "arrow_left": Arrow(c, col, on, -1); return true;
                case "arrow_right": Arrow(c, col, on, 1); return true;
                case "arrow_ahead": Arrow(c, col, on, 0); return true;
                case "cycle": CycleLens(c, col, on); return true;
            }
            return false;
        }

        // ------------------------------------------------------------------ helpers

        private static void Plastic(PixelCanvas c, Rgba col)
        {
            c.Each((x, y, _) => col.Add((Noise.Fbm(x, y, S, 8, 3, 7) - 0.5f) * 0.04f + (Noise.White(x, y, 3) - 0.5f) * 0.02f));
            // Soft bevel so flat faces read as moulded plastic.
            for (int i = 0; i < 3; i++)
            {
                c.Rect(i, i, S - 2 * i, 1, col.Add(0.06f - i * 0.02f));
                c.Rect(i, S - 1 - i, S - 2 * i, 1, col.Add(-0.04f + i * 0.01f));
            }
        }

        /// <summary>Round lens mask: everything outside radius r is transparent.</summary>
        private static void Disc(PixelCanvas c, float r = 30.5f)
        {
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    float dx = x + 0.5f - 32, dy = y + 0.5f - 32;
                    if (dx * dx + dy * dy > r * r) c.Set(x, y, Rgba.Clear);
                }
        }

        private static Rgba Dim(Rgba col, float k) => new Rgba(col.r * k + 0.04f, col.g * k + 0.04f, col.b * k + 0.04f);

        /// <summary>Modern LED aspect: hexagonal cluster of LEDs behind a clear lens.</summary>
        private static void Led(PixelCanvas c, Rgba col, bool on)
        {
            c.Fill(H("0c0c0d"));
            c.Circle(32, 32, 30.5f, on ? Dim(col, 0.55f) : Dim(col, 0.12f));
            for (int row = -6; row <= 6; row++)
                for (int k = -7; k <= 7; k++)
                {
                    float x = 32 + k * 4.4f + (row & 1) * 2.2f, y = 32 + row * 3.9f;
                    float d = Mathf.Sqrt((x - 32) * (x - 32) + (y - 32) * (y - 32));
                    if (d > 26) continue;
                    var led = on ? Rgba.Lerp(col, H("ffffff"), Mathf.Clamp01(0.55f - d / 60f)) : Dim(col, 0.22f);
                    c.Circle(x, y, 1.55f, led);
                }
            if (!on) c.Circle(24, 22, 6, H("ffffff", 0.08f));
            Disc(c);
        }

        /// <summary>Older incandescent aspect: Fresnel lens with concentric ribs.</summary>
        private static void Classic(PixelCanvas c, Rgba col, bool on)
        {
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    float dx = x + 0.5f - 32, dy = y + 0.5f - 32, d = Mathf.Sqrt(dx * dx + dy * dy);
                    float rib = 0.5f + 0.5f * Mathf.Sin(d * 1.4f);
                    Rgba p;
                    if (on)
                    {
                        float core = Mathf.Clamp01(1f - d / 30f);
                        p = Rgba.Lerp(Dim(col, 0.7f), Rgba.Lerp(col, H("fff4d8"), 0.55f), core * core + rib * 0.15f);
                    }
                    else p = Dim(col, 0.13f + rib * 0.07f);
                    c.Set(x, y, p);
                }
            c.Ring(32, 32, 28.5f, 30.5f, H("0a0a0a"));
            if (!on) c.Circle(23, 21, 7, H("ffffff", 0.07f));
            Disc(c);
        }

        /// <summary>Arrow aspect: black mask with an arrow cut out. dir -1 left, 0 ahead, 1 right.</summary>
        private static void Arrow(PixelCanvas c, Rgba col, bool on, int dir)
        {
            c.Fill(H("0b0b0c"));
            c.Circle(32, 32, 30.5f, H("101011"));
            var a = on ? Rgba.Lerp(col, H("ffffff"), 0.15f) : Dim(col, 0.16f);
            if (dir == 0)
            {
                c.Rect(28, 26, 8, 26, a);
                c.Polygon(a, 32, 8, 46, 28, 18, 28);
            }
            else
            {
                float s = dir;
                c.Rect(s > 0 ? 12 : 26, 28, 26, 8, a);
                c.Polygon(a, 32 + 24 * s, 32, 32 + 4 * s, 18, 32 + 4 * s, 46);
            }
            if (on)
                for (int y = 0; y < S; y += 3)
                    for (int x = 0; x < S; x += 3)
                    {
                        var p = c.Get(x, y);
                        if (p.r + p.g + p.b > 1f) c.Set(x, y, Rgba.Lerp(p, H("ffffff"), 0.35f));
                    }
            Disc(c);
        }

        private static void CycleLens(PixelCanvas c, Rgba col, bool on)
        {
            c.Fill(H("0b0b0c"));
            c.Circle(32, 32, 30.5f, H("111112"));
            var a = on ? col : Dim(col, 0.16f);
            c.Ring(18, 38, 6, 9.5f, a);
            c.Ring(46, 38, 6, 9.5f, a);
            c.Line(18, 38, 28, 24, 3.5f, a);
            c.Line(28, 24, 40, 24, 3.5f, a);
            c.Line(40, 24, 46, 38, 3.5f, a);
            c.Line(28, 24, 32, 38, 3.5f, a);
            c.Line(32, 38, 18, 38, 3.5f, a);
            Disc(c);
        }

        private static void Man(PixelCanvas c, bool green, bool on, bool bike)
        {
            c.Fill(H("0b0b0c"));
            c.RoundRect(3, 3, 58, 58, 6, H("111112"));
            var col = green ? H("2bff8c") : H("ff2a1a");
            var a = on ? col : Dim(col, 0.14f);
            float cx = bike ? 22 : 32;
            c.Circle(cx, 13, 5.5f, a);
            if (green)
            {
                c.Line(cx, 20, cx - 2, 36, 7, a);
                c.Line(cx - 2, 36, cx - 10, 54, 5.5f, a);
                c.Line(cx - 2, 36, cx + 8, 54, 5.5f, a);
                c.Line(cx, 22, cx - 11, 32, 4.5f, a);
                c.Line(cx, 22, cx + 10, 30, 4.5f, a);
            }
            else
            {
                c.Rect(cx - 6, 20, 12, 18, a);
                c.Rect(cx - 6, 38, 5, 18, a);
                c.Rect(cx + 1, 38, 5, 18, a);
                c.Rect(cx - 10, 20, 4, 16, a);
                c.Rect(cx + 6, 20, 4, 16, a);
            }
            if (bike)
            {
                c.Ring(42, 46, 3.5f, 6.5f, a);
                c.Ring(56, 46, 3.5f, 6.5f, a);
                c.Line(42, 46, 48, 36, 3, a);
                c.Line(48, 36, 56, 46, 3, a);
                c.Line(48, 36, 52, 34, 3, a);
            }
            if (on)
                for (int y = 1; y < S; y += 3)
                    for (int x = 1; x < S; x += 3)
                    {
                        var p = c.Get(x, y);
                        if (p.r + p.g > 0.9f) c.Set(x, y, Rgba.Lerp(p, H("ffffff"), 0.4f));
                    }
        }

        /// <summary>Illuminated "no right/left turn" pod (internally lit regulatory sign).</summary>
        private static void Pod(PixelCanvas c, bool right, bool on)
        {
            c.Fill(H("0d0d0e"));
            var white = on ? H("fbfbf6") : H("2c2c2e");
            var red = on ? H("e8141e") : H("3a1214");
            var black = on ? H("111111") : H("161617");
            c.Circle(32, 32, 27, red);
            c.Circle(32, 32, 21, white);
            float s = right ? 1 : -1;
            c.Bar(32 - 7 * s, 46, 32 - 7 * s, 26, 6, black);
            c.Bar(32 - 7 * s, 26, 32 + 6 * s, 26, 6, black);
            c.Polygon(black, 32 + 14 * s, 26, 32 + 5 * s, 17, 32 + 5 * s, 35);
            c.Bar(18, 18, 46, 46, 5, red);
        }

        private static void Wait(PixelCanvas c, bool on)
        {
            c.Fill(H("0d0d0e"));
            c.TextFit("WAIT", 32, 44, 22, 56, on ? H("ff9a1e") : H("3a2610"));
        }

        /// <summary>Soft radial glow drawn around a lit lens (alpha falls off to zero at the edge).</summary>
        private static void Glow(PixelCanvas c, Rgba col)
        {
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    float dx = x + 0.5f - 32, dy = y + 0.5f - 32;
                    float d = Mathf.Sqrt(dx * dx + dy * dy) / 32f;
                    float a = Mathf.Clamp01(1f - d);
                    c.Set(x, y, col.WithAlpha(a * a * 0.55f));
                }
        }
    }
}
