using System;
using UnityEngine;

namespace UKCity.World.Art
{
    /// <summary>Straight (non-premultiplied) float colour used by the painters.</summary>
    public struct Rgba
    {
        public float r, g, b, a;

        public Rgba(float r, float g, float b, float a = 1f) { this.r = r; this.g = g; this.b = b; this.a = a; }

        public static Rgba Hex(string hex, float a = 1f)
        {
            if (hex[0] == '#') hex = hex.Substring(1);
            int v = Convert.ToInt32(hex, 16);
            return new Rgba(((v >> 16) & 255) / 255f, ((v >> 8) & 255) / 255f, (v & 255) / 255f, a);
        }

        public static Rgba Lerp(Rgba x, Rgba y, float t) =>
            new Rgba(x.r + (y.r - x.r) * t, x.g + (y.g - x.g) * t, x.b + (y.b - x.b) * t, x.a + (y.a - x.a) * t);

        public Rgba Scale(float k) => new Rgba(r * k, g * k, b * k, a);
        public Rgba WithAlpha(float na) => new Rgba(r, g, b, na);
        public Rgba Add(float d) => new Rgba(r + d, g + d, b + d, a);

        public static readonly Rgba Clear = new Rgba(0, 0, 0, 0);
        public static readonly Rgba White = Hex("f4f4f0");
        public static readonly Rgba Black = Hex("141416");
    }

    /// <summary>
    /// Small anti-aliased 2D rasteriser used to paint block textures and road signs.
    /// Coordinates are in pixels with y pointing DOWN (like an image); the atlas flips it on upload.
    /// </summary>
    public sealed class PixelCanvas
    {
        public readonly int W, H;
        public readonly Rgba[] P;

        public PixelCanvas(int w, int h)
        {
            W = w; H = h;
            P = new Rgba[w * h];
        }

        public Rgba Get(int x, int y)
        {
            x = ((x % W) + W) % W;
            y = ((y % H) + H) % H;
            return P[y * W + x];
        }

        public void Set(int x, int y, Rgba c)
        {
            if (x < 0 || y < 0 || x >= W || y >= H) return;
            P[y * W + x] = c;
        }

        /// <summary>Alpha-blends c over the pixel with the given coverage (0..1).</summary>
        public void Blend(int x, int y, Rgba c, float coverage)
        {
            if (x < 0 || y < 0 || x >= W || y >= H || coverage <= 0f) return;
            float a = Mathf.Clamp01(c.a * coverage);
            ref var d = ref P[y * W + x];
            float outA = a + d.a * (1 - a);
            if (outA <= 1e-5f) { d = Rgba.Clear; return; }
            d.r = (c.r * a + d.r * d.a * (1 - a)) / outA;
            d.g = (c.g * a + d.g * d.a * (1 - a)) / outA;
            d.b = (c.b * a + d.b * d.a * (1 - a)) / outA;
            d.a = outA;
        }

        public void Fill(Rgba c)
        {
            for (int i = 0; i < P.Length; i++) P[i] = c;
        }

        /// <summary>Applies a per-pixel function (for noise-based textures).</summary>
        public void Each(Func<int, int, Rgba, Rgba> f)
        {
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                    P[y * W + x] = f(x, y, P[y * W + x]);
        }

        // ------------------------------------------------------------------ shapes (analytic AA)

        private static float Cov(float signedDistInside) => Mathf.Clamp01(signedDistInside + 0.5f);

        public void Rect(float x, float y, float w, float h, Rgba c)
        {
            int x0 = Mathf.FloorToInt(x), y0 = Mathf.FloorToInt(y);
            int x1 = Mathf.CeilToInt(x + w), y1 = Mathf.CeilToInt(y + h);
            for (int py = y0; py < y1; py++)
                for (int px = x0; px < x1; px++)
                {
                    float cx = Mathf.Clamp01(Mathf.Min(px + 1, x + w) - Mathf.Max(px, x));
                    float cy = Mathf.Clamp01(Mathf.Min(py + 1, y + h) - Mathf.Max(py, y));
                    Blend(px, py, c, cx * cy);
                }
        }

        public void RoundRect(float x, float y, float w, float h, float r, Rgba c)
        {
            r = Mathf.Min(r, Mathf.Min(w, h) * 0.5f);
            int x0 = Mathf.FloorToInt(x), y0 = Mathf.FloorToInt(y);
            int x1 = Mathf.CeilToInt(x + w), y1 = Mathf.CeilToInt(y + h);
            float hx = w * 0.5f, hy = h * 0.5f, cxm = x + hx, cym = y + hy;
            for (int py = y0; py < y1; py++)
                for (int px = x0; px < x1; px++)
                {
                    float qx = Mathf.Abs(px + 0.5f - cxm) - (hx - r);
                    float qy = Mathf.Abs(py + 0.5f - cym) - (hy - r);
                    float outside = Mathf.Sqrt(Mathf.Max(qx, 0) * Mathf.Max(qx, 0) + Mathf.Max(qy, 0) * Mathf.Max(qy, 0)) + Mathf.Min(Mathf.Max(qx, qy), 0) - r;
                    Blend(px, py, c, Cov(-outside));
                }
        }

        public void Circle(float cx, float cy, float r, Rgba c)
        {
            int x0 = Mathf.FloorToInt(cx - r - 1), x1 = Mathf.CeilToInt(cx + r + 1);
            int y0 = Mathf.FloorToInt(cy - r - 1), y1 = Mathf.CeilToInt(cy + r + 1);
            for (int py = y0; py < y1; py++)
                for (int px = x0; px < x1; px++)
                {
                    float dx = px + 0.5f - cx, dy = py + 0.5f - cy;
                    Blend(px, py, c, Cov(r - Mathf.Sqrt(dx * dx + dy * dy)));
                }
        }

        public void Ring(float cx, float cy, float r0, float r1, Rgba c)
        {
            int x0 = Mathf.FloorToInt(cx - r1 - 1), x1 = Mathf.CeilToInt(cx + r1 + 1);
            int y0 = Mathf.FloorToInt(cy - r1 - 1), y1 = Mathf.CeilToInt(cy + r1 + 1);
            for (int py = y0; py < y1; py++)
                for (int px = x0; px < x1; px++)
                {
                    float dx = px + 0.5f - cx, dy = py + 0.5f - cy;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    Blend(px, py, c, Mathf.Min(Cov(r1 - d), Cov(d - r0)));
                }
        }

        /// <summary>A thick line with round caps.</summary>
        public void Line(float ax, float ay, float bx, float by, float width, Rgba c)
        {
            float hw = width * 0.5f;
            int x0 = Mathf.FloorToInt(Mathf.Min(ax, bx) - hw - 1), x1 = Mathf.CeilToInt(Mathf.Max(ax, bx) + hw + 1);
            int y0 = Mathf.FloorToInt(Mathf.Min(ay, by) - hw - 1), y1 = Mathf.CeilToInt(Mathf.Max(ay, by) + hw + 1);
            float vx = bx - ax, vy = by - ay, l2 = vx * vx + vy * vy;
            for (int py = y0; py < y1; py++)
                for (int px = x0; px < x1; px++)
                {
                    float qx = px + 0.5f - ax, qy = py + 0.5f - ay;
                    float t = l2 > 0 ? Mathf.Clamp01((qx * vx + qy * vy) / l2) : 0;
                    float dx = qx - vx * t, dy = qy - vy * t;
                    Blend(px, py, c, Cov(hw - Mathf.Sqrt(dx * dx + dy * dy)));
                }
        }

        /// <summary>A thick line with square (butt) ends.</summary>
        public void Bar(float ax, float ay, float bx, float by, float width, Rgba c)
        {
            float len = Mathf.Sqrt((bx - ax) * (bx - ax) + (by - ay) * (by - ay));
            if (len < 1e-4f) return;
            float nx = -(by - ay) / len * width * 0.5f, ny = (bx - ax) / len * width * 0.5f;
            Polygon(c, ax + nx, ay + ny, bx + nx, by + ny, bx - nx, by - ny, ax - nx, ay - ny);
        }

        /// <summary>Filled polygon (any shape, even-odd), 4x4 supersampled. Points as x0,y0,x1,y1...</summary>
        public void Polygon(Rgba c, params float[] pts)
        {
            int n = pts.Length / 2;
            float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;
            for (int i = 0; i < n; i++)
            {
                minX = Mathf.Min(minX, pts[i * 2]); maxX = Mathf.Max(maxX, pts[i * 2]);
                minY = Mathf.Min(minY, pts[i * 2 + 1]); maxY = Mathf.Max(maxY, pts[i * 2 + 1]);
            }
            int x0 = Mathf.FloorToInt(minX), x1 = Mathf.CeilToInt(maxX), y0 = Mathf.FloorToInt(minY), y1 = Mathf.CeilToInt(maxY);
            for (int py = y0; py < y1; py++)
                for (int px = x0; px < x1; px++)
                {
                    int hits = 0;
                    for (int sy = 0; sy < 4; sy++)
                        for (int sx = 0; sx < 4; sx++)
                            if (Inside(pts, n, px + (sx + 0.5f) * 0.25f, py + (sy + 0.5f) * 0.25f)) hits++;
                    if (hits > 0) Blend(px, py, c, hits / 16f);
                }
        }

        private static bool Inside(float[] p, int n, float x, float y)
        {
            bool inside = false;
            for (int i = 0, j = n - 1; i < n; j = i++)
            {
                float xi = p[i * 2], yi = p[i * 2 + 1], xj = p[j * 2], yj = p[j * 2 + 1];
                if (((yi > y) != (yj > y)) && (x < (xj - xi) * (y - yi) / (yj - yi) + xi)) inside = !inside;
            }
            return inside;
        }

        /// <summary>Regular polygon (e.g. 8 for the STOP octagon) with a flat top when rotation is half a step.</summary>
        public void RegularPolygon(float cx, float cy, float r, int sides, float rotation, Rgba c)
        {
            var pts = new float[sides * 2];
            for (int i = 0; i < sides; i++)
            {
                float a = rotation + i * Mathf.PI * 2f / sides;
                pts[i * 2] = cx + Mathf.Cos(a) * r;
                pts[i * 2 + 1] = cy + Mathf.Sin(a) * r;
            }
            Polygon(c, pts);
        }

        /// <summary>Triangle with rounded look (UK warning sign shape), pointing up or down.</summary>
        public void Triangle(float cx, float cy, float size, bool pointUp, Rgba c)
        {
            float h = size * 0.866f;
            if (pointUp) Polygon(c, cx, cy - h * 0.62f, cx + size * 0.5f, cy + h * 0.38f, cx - size * 0.5f, cy + h * 0.38f);
            else Polygon(c, cx, cy + h * 0.62f, cx - size * 0.5f, cy - h * 0.38f, cx + size * 0.5f, cy - h * 0.38f);
        }

        // ------------------------------------------------------------------ text

        public float TextWidth(string s, float capHeight) => SignFont.Width(s, capHeight);

        /// <summary>Draws text. align: 0 = left, 0.5 = centre, 1 = right. y is the baseline.</summary>
        public void Text(string s, float x, float baseline, float capHeight, Rgba c, float align = 0f)
        {
            float w = SignFont.Width(s, capHeight);
            SignFont.Draw(this, s, x - w * align, baseline, capHeight, c);
        }

        /// <summary>Text centred in a box, shrunk to fit its width.</summary>
        public void TextFit(string s, float cx, float baseline, float capHeight, float maxWidth, Rgba c)
        {
            float w = SignFont.Width(s, capHeight);
            if (w > maxWidth) capHeight *= maxWidth / w;
            Text(s, cx, baseline, capHeight, c, 0.5f);
        }

        // ------------------------------------------------------------------ output

        /// <summary>Copies a sub-rectangle into a Color32 tile, flipping so canvas top = texture top.</summary>
        public void CopyTo(Color32[] dst, int sx, int sy, int size)
        {
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    var c = P[(sy + y) * W + sx + x];
                    dst[(size - 1 - y) * size + x] = new Color32(B(c.r), B(c.g), B(c.b), B(c.a));
                }
        }

        private static byte B(float v) => (byte)Mathf.Clamp(Mathf.Round(v * 255f), 0, 255);
    }
}
