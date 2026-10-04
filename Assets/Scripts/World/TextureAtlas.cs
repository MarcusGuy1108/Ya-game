using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UKCity.World.Art;

namespace UKCity.World
{
    /// <summary>
    /// All block textures live in one Texture2DArray (one 64x64 slice per tile, with its own mip chain, so there is
    /// no bleeding between tiles at a distance). Tiles are painted procedurally at startup; the project ships with
    /// no image assets. A small 2D icon sheet is built alongside for the UI.
    /// </summary>
    public static class TextureAtlas
    {
        public const int TileSize = 64;
        public const int IconSize = 32;
        public const int IconCols = 32;

        private static readonly List<string> names = new List<string>();
        private static readonly Dictionary<string, int> index = new Dictionary<string, int>();
        private static Color32[][] pixels;
        private static Texture2DArray array;
        private static Texture2D icons;
        private static bool pixelArt;

        public static int Count { get { lock (names) return names.Count; } }

        /// <summary>Registers a tile name and returns its slice index (painting happens later, all at once).</summary>
        public static int Register(string name)
        {
            lock (names)
            {
                if (index.TryGetValue(name, out int i)) return i;
                i = names.Count;
                names.Add(name);
                index[name] = i;
                return i;
            }
        }

        public static string NameOf(int tile) { lock (names) return tile >= 0 && tile < names.Count ? names[tile] : "?"; }

        /// <summary>Paints every registered tile (parallel). Safe to call more than once.</summary>
        public static Color32[][] Pixels
        {
            get
            {
                if (pixels != null) return pixels;
                _ = Blocks.All.Count; // make sure every block (and its tiles) is registered
                string[] list;
                lock (names) list = names.ToArray();
                var result = new Color32[list.Length][];
                Parallel.For(0, list.Length, i =>
                {
                    var c = new PixelCanvas(TileSize, TileSize);
                    if (!Signs.PaintTile(list[i], c) && !BlockPainter.Paint(list[i], c)) Missing(c);
                    var px = new Color32[TileSize * TileSize];
                    c.CopyTo(px, 0, 0, TileSize);
                    result[i] = px;
                });
                pixels = result;
                return pixels;
            }
        }

        public static Texture2DArray Array
        {
            get
            {
                if (array == null) Build();
                return array;
            }
        }

        public static Texture2D Icons
        {
            get
            {
                if (icons == null) Build();
                return icons;
            }
        }

        /// <summary>Crisp nearest-neighbour pixels (Minecraft look) instead of smooth filtering.</summary>
        public static bool PixelArt
        {
            get => pixelArt;
            set
            {
                pixelArt = value;
                if (array != null) array.filterMode = value ? FilterMode.Point : FilterMode.Trilinear;
            }
        }

        public static Rect IconRect(int tile)
        {
            int rows = Mathf.CeilToInt(Mathf.Max(1, Count) / (float)IconCols);
            int col = tile % IconCols, row = tile / IconCols;
            return new Rect((float)col / IconCols, 1f - (row + 1f) / rows, 1f / IconCols, 1f / rows);
        }

        private static void Build()
        {
            var px = Pixels;
            int n = px.Length;
            array = new Texture2DArray(TileSize, TileSize, n, TextureFormat.RGBA32, true, false)
            {
                name = "BlockTiles",
                filterMode = pixelArt ? FilterMode.Point : FilterMode.Trilinear,
                anisoLevel = 8,
                wrapMode = TextureWrapMode.Repeat
            };
            int rows = Mathf.CeilToInt(n / (float)IconCols);
            var iconPx = new Color32[IconCols * IconSize * rows * IconSize];
            int iconW = IconCols * IconSize;

            for (int i = 0; i < n; i++)
            {
                var level = px[i];
                int size = TileSize;
                for (int mip = 0; size >= 1; mip++)
                {
                    array.SetPixels32(level, i, mip);
                    if (size == IconSize)
                    {
                        // Icon sheet: row 0 at the top of the texture.
                        int ox = (i % IconCols) * IconSize, oy = (rows - 1 - i / IconCols) * IconSize;
                        for (int y = 0; y < IconSize; y++)
                            for (int x = 0; x < IconSize; x++)
                                iconPx[(oy + y) * iconW + ox + x] = level[y * IconSize + x];
                    }
                    if (size == 1) break;
                    level = Downsample(level, size, mip + 1);
                    size /= 2;
                }
            }
            array.Apply(false, true);

            icons = new Texture2D(iconW, rows * IconSize, TextureFormat.RGBA32, false)
            {
                name = "BlockIcons",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            icons.SetPixels32(iconPx);
            icons.Apply(false, true);
        }

        /// <summary>2x2 box filter weighted by alpha, with a little alpha boost so cut-out leaves don't vanish.</summary>
        private static Color32[] Downsample(Color32[] src, int size, int newLevel)
        {
            int h = size / 2;
            var dst = new Color32[h * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < h; x++)
                {
                    float r = 0, g = 0, b = 0, a = 0;
                    for (int k = 0; k < 4; k++)
                    {
                        var c = src[(y * 2 + (k >> 1)) * size + x * 2 + (k & 1)];
                        float ca = c.a / 255f;
                        r += c.r * ca; g += c.g * ca; b += c.b * ca; a += ca;
                    }
                    float outA = a / 4f;
                    if (a > 0) { r /= a; g /= a; b /= a; }
                    if (outA < 0.999f && outA > 0f) outA = Mathf.Min(1f, outA * (1f + 0.15f * newLevel));
                    dst[y * h + x] = new Color32((byte)r, (byte)g, (byte)b, (byte)(outA * 255f));
                }
            return dst;
        }

        private static void Missing(PixelCanvas c)
        {
            for (int y = 0; y < TileSize; y++)
                for (int x = 0; x < TileSize; x++)
                    c.Set(x, y, ((x / 8 + y / 8) % 2 == 0) ? new Rgba(1, 0, 1) : new Rgba(0, 0, 0));
        }
    }
}
