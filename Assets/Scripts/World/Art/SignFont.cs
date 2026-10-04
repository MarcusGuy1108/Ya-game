using System;
using UnityEngine;

namespace UKCity.World.Art
{
    /// <summary>Renders text from the embedded signed-distance-field font (SignFontData) at any size, anti-aliased.</summary>
    public static class SignFont
    {
        private static byte[] data;
        /// <summary>Height of a capital letter in font units (32 units per em).</summary>
        private const float CapUnits = 23.3f;

        private static byte[] Data => data ??= Convert.FromBase64String(SignFontData.Data);

        private static int Glyph(char ch)
        {
            int i = ch - SignFontData.FirstChar;
            return i < 0 || i * 6 >= SignFontData.Metrics.Length ? ('?' - SignFontData.FirstChar) : i;
        }

        public static float Width(string s, float capHeight)
        {
            float scale = capHeight / CapUnits, w = 0;
            foreach (char ch in s) w += SignFontData.Metrics[Glyph(ch) * 6 + 5] * scale;
            return w;
        }

        public static void Draw(PixelCanvas c, string s, float x, float baseline, float capHeight, Rgba col)
        {
            var m = SignFontData.Metrics;
            var d = Data;
            float scale = capHeight / CapUnits;
            // SDF value change per output pixel, used for the anti-aliasing ramp.
            float ramp = 127f / SignFontData.Spread / 255f / scale;
            float pen = x;
            foreach (char ch in s)
            {
                int g = Glyph(ch) * 6;
                int off = (int)m[g], gw = (int)m[g + 1], gh = (int)m[g + 2];
                float ox = m[g + 3], oy = m[g + 4], adv = m[g + 5];
                if (gw > 0)
                {
                    float left = pen + ox * scale, top = baseline + oy * scale;
                    int x0 = Mathf.FloorToInt(left), x1 = Mathf.CeilToInt(left + gw * scale);
                    int y0 = Mathf.FloorToInt(top), y1 = Mathf.CeilToInt(top + gh * scale);
                    for (int py = y0; py < y1; py++)
                        for (int px = x0; px < x1; px++)
                        {
                            float u = (px + 0.5f - left) / scale - 0.5f;
                            float v = (py + 0.5f - top) / scale - 0.5f;
                            float sd = Sample(d, off, gw, gh, u, v);
                            float cov = Mathf.Clamp01((sd - 0.5f) / ramp + 0.5f);
                            if (cov > 0) c.Blend(px, py, col, cov);
                        }
                }
                pen += adv * scale;
            }
        }

        private static float Sample(byte[] d, int off, int w, int h, float u, float v)
        {
            int x0 = Mathf.FloorToInt(u), y0 = Mathf.FloorToInt(v);
            float fx = u - x0, fy = v - y0;
            float a = Px(d, off, w, h, x0, y0), b = Px(d, off, w, h, x0 + 1, y0);
            float c = Px(d, off, w, h, x0, y0 + 1), e = Px(d, off, w, h, x0 + 1, y0 + 1);
            return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, e, fx), fy);
        }

        private static float Px(byte[] d, int off, int w, int h, int x, int y)
        {
            if (x < 0 || y < 0 || x >= w || y >= h) return 0f;
            return d[off + y * w + x] / 255f;
        }
    }
}
