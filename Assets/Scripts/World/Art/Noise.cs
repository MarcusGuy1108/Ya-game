using UnityEngine;

namespace UKCity.World.Art
{
    /// <summary>Tileable value noise so block textures repeat seamlessly.</summary>
    public static class Noise
    {
        public static float Hash(int x, int y, int seed)
        {
            unchecked
            {
                uint h = (uint)(x * 374761393 + y * 668265263 + seed * 1442695041);
                h = (h ^ (h >> 13)) * 1274126177u;
                h ^= h >> 16;
                return (h & 0xFFFFFF) / 16777215f;
            }
        }

        private static float Smooth(float t) => t * t * (3f - 2f * t);

        /// <summary>Value noise at (x,y) in lattice units, wrapping every `period` cells.</summary>
        public static float Value(float x, float y, int period, int seed)
        {
            int x0 = Mathf.FloorToInt(x), y0 = Mathf.FloorToInt(y);
            float fx = Smooth(x - x0), fy = Smooth(y - y0);
            int ax = Wrap(x0, period), bx = Wrap(x0 + 1, period), ay = Wrap(y0, period), by = Wrap(y0 + 1, period);
            float a = Hash(ax, ay, seed), b = Hash(bx, ay, seed), c = Hash(ax, by, seed), d = Hash(bx, by, seed);
            return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fy);
        }

        private static int Wrap(int v, int p) => ((v % p) + p) % p;

        /// <summary>
        /// Fractal noise for a pixel of a tile of `size` pixels. `cells` is how many lattice cells span the tile
        /// at the first octave. Result roughly 0..1.
        /// </summary>
        public static float Fbm(float px, float py, int size, int cells, int octaves, int seed, float persistence = 0.5f)
        {
            float sum = 0, amp = 1, norm = 0;
            for (int o = 0; o < octaves; o++)
            {
                float s = (float)cells / size;
                sum += Value(px * s, py * s, cells, seed + o * 31) * amp;
                norm += amp;
                amp *= persistence;
                cells *= 2;
            }
            return sum / norm;
        }

        /// <summary>Per-pixel white noise (for grain and speckles).</summary>
        public static float White(int x, int y, int seed) => Hash(x, y, seed * 7 + 3);
    }
}
