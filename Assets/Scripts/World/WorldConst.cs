using System;

namespace UKCity.World
{
    /// <summary>Fixed dimensions of the voxel world. Horizontally infinite, vertically capped.</summary>
    public static class WorldConst
    {
        public const int ChunkSize = 16;
        public const int ChunkHeight = 128;
        public const int ChunkArea = ChunkSize * ChunkSize;
        public const int ChunkVolume = ChunkArea * ChunkHeight;

        /// <summary>Y of the top grass layer. Roads are painted into this layer; buildings sit on it.</summary>
        public const int SurfaceY = 40;

        public static int Index(int x, int y, int z) => x + (z << 4) + (y << 8);

        public static int FloorDiv(int a, int b)
        {
            int q = a / b;
            if ((a % b != 0) && ((a < 0) != (b < 0))) q--;
            return q;
        }

        public static int Mod(int a, int b)
        {
            int m = a % b;
            return m < 0 ? m + b : m;
        }

        public static int FloorToInt(float f) => (int)Math.Floor(f);

        /// <summary>Deterministic integer hash (used for trees, door colours etc).</summary>
        public static uint Hash(int x, int z, int seed)
        {
            unchecked
            {
                uint h = (uint)seed * 0x9E3779B1u;
                h ^= (uint)x * 0x85EBCA6Bu;
                h = (h << 13) | (h >> 19);
                h ^= (uint)z * 0xC2B2AE35u;
                h *= 0x27D4EB2Fu;
                h ^= h >> 15;
                h *= 0x165667B1u;
                h ^= h >> 13;
                return h;
            }
        }
    }

    public readonly struct ChunkCoord : IEquatable<ChunkCoord>
    {
        public readonly int X;
        public readonly int Z;

        public ChunkCoord(int x, int z) { X = x; Z = z; }

        public static ChunkCoord FromBlock(int bx, int bz) =>
            new ChunkCoord(WorldConst.FloorDiv(bx, WorldConst.ChunkSize), WorldConst.FloorDiv(bz, WorldConst.ChunkSize));

        public int MinX => X * WorldConst.ChunkSize;
        public int MinZ => Z * WorldConst.ChunkSize;

        public bool Equals(ChunkCoord o) => X == o.X && Z == o.Z;
        public override bool Equals(object obj) => obj is ChunkCoord o && Equals(o);
        public override int GetHashCode() => unchecked((X * 73856093) ^ (Z * 19349663));
        public override string ToString() => $"({X},{Z})";
        public static bool operator ==(ChunkCoord a, ChunkCoord b) => a.Equals(b);
        public static bool operator !=(ChunkCoord a, ChunkCoord b) => !a.Equals(b);
    }
}
