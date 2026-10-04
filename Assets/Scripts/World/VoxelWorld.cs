using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UKCity.City;

namespace UKCity.World
{
    public sealed class Chunk
    {
        public ChunkCoord Coord;
        /// <summary>Null until the first generation finishes.</summary>
        public ushort[] Blocks;
        public bool NeedsRegen = true;
        public int GenRequest;
        public int EditVersion;
        public bool NeedsMesh;
        public int MeshRequest;
        public GameObject Go;
        public Mesh Mesh;
    }

    /// <summary>
    /// Infinite, chunk-streamed voxel world. Generation and meshing run on worker threads;
    /// results are applied on the main thread. Every result carries a request id so stale work is dropped.
    /// </summary>
    public sealed class VoxelWorld : MonoBehaviour
    {
        public const int MaxRadius = 16;

        public int Seed = 1066;
        public CityLayer City { get; private set; }
        public readonly WorldEdits Edits = new WorldEdits();
        /// <summary>Receives each chunk's animated blocks (signals, message signs...).</summary>
        public DynamicFaces Animated;

        private readonly Dictionary<ChunkCoord, Chunk> chunks = new Dictionary<ChunkCoord, Chunk>();
        private readonly ConcurrentQueue<GenResult> genResults = new ConcurrentQueue<GenResult>();
        private readonly ConcurrentQueue<MeshResult> meshResults = new ConcurrentQueue<MeshResult>();
        private readonly ConcurrentBag<MeshData> meshPool = new ConcurrentBag<MeshData>();
        private readonly MeshData mainThreadMesh = new MeshData();
        private static List<Vector2Int> sortedOffsets;

        private Material opaqueMat, transparentMat;
        private ChunkCoord focusChunk;
        private int radius = 8;
        private int running;
        private int maxJobs;
        private float nextUnload;

        public int LoadedChunks => chunks.Count;
        public int PendingJobs => running;
        public Material OpaqueMaterial => opaqueMat;

        private struct GenResult
        {
            public Chunk Chunk;
            public int Request;
            public int EditVersion;
            public ushort[] Data;
            public Exception Error;
        }

        private struct MeshResult
        {
            public Chunk Chunk;
            public int Request;
            public MeshData Mesh;
            public Exception Error;
        }

        public void Init(CityLayer city, int seed)
        {
            City = city;
            Seed = seed;
            City.Changed += OnCityChanged;
            maxJobs = Mathf.Max(1, SystemInfo.processorCount - 1);

            opaqueMat = MakeMaterial("UKCity/Voxel");
            transparentMat = MakeMaterial("UKCity/VoxelTransparent");
            if (Animated != null) Animated.Material = opaqueMat;

            if (sortedOffsets == null)
            {
                sortedOffsets = new List<Vector2Int>();
                for (int z = -MaxRadius - 1; z <= MaxRadius + 1; z++)
                    for (int x = -MaxRadius - 1; x <= MaxRadius + 1; x++)
                        sortedOffsets.Add(new Vector2Int(x, z));
                sortedOffsets.Sort((a, b) => a.sqrMagnitude.CompareTo(b.sqrMagnitude));
            }
        }

        public static Material MakeMaterial(string shaderName)
        {
            var shader = Shader.Find(shaderName);
            if (shader == null)
            {
                Debug.LogError($"Shader '{shaderName}' not found. Check Assets/Resources/Shaders.");
                shader = Shader.Find("Unlit/Texture");
            }
            var m = new Material(shader);
            m.SetTexture("_Tiles", TextureAtlas.Array);
            return m;
        }

        private void OnDestroy()
        {
            if (City != null) City.Changed -= OnCityChanged;
        }

        // ------------------------------------------------------------------ public API

        public void SetFocus(Vector3 worldPos, int chunkRadius)
        {
            focusChunk = ChunkCoord.FromBlock(Mathf.FloorToInt(worldPos.x), Mathf.FloorToInt(worldPos.z));
            radius = Mathf.Clamp(chunkRadius, 2, MaxRadius);
        }

        public ushort GetBlock(int x, int y, int z)
        {
            if (y < 0) return BlockIds.Bedrock;
            if (y >= WorldConst.ChunkHeight) return BlockIds.Air;
            var cc = ChunkCoord.FromBlock(x, z);
            if (!chunks.TryGetValue(cc, out var c) || c.Blocks == null) return BlockIds.Unloaded;
            return c.Blocks[WorldConst.Index(x - cc.MinX, y, z - cc.MinZ)];
        }

        public bool IsLoaded(int x, int z)
        {
            var cc = ChunkCoord.FromBlock(x, z);
            return chunks.TryGetValue(cc, out var c) && c.Blocks != null;
        }

        /// <summary>Highest non-air block in a column, or -1 if the chunk is not loaded.</summary>
        public int TopBlockY(int x, int z)
        {
            if (!IsLoaded(x, z)) return -1;
            for (int y = WorldConst.ChunkHeight - 1; y >= 0; y--)
            {
                ushort b = GetBlock(x, y, z);
                if (b != BlockIds.Air && Blocks.IsSolidForPhysics(b)) return y;
            }
            return 0;
        }

        /// <summary>Player edit: changes a block, records it and remeshes immediately.</summary>
        public bool SetBlock(int x, int y, int z, ushort id)
        {
            if (y < 1 || y >= WorldConst.ChunkHeight) return false;
            var cc = ChunkCoord.FromBlock(x, z);
            if (!chunks.TryGetValue(cc, out var c) || c.Blocks == null) return false;
            int lx = x - cc.MinX, lz = z - cc.MinZ;
            int idx = WorldConst.Index(lx, y, lz);
            if (c.Blocks[idx] == id) return false;
            c.Blocks[idx] = id;
            c.EditVersion++;
            Edits.Set(x, y, z, id);

            RemeshNow(c);
            int nx = lx == 0 ? -1 : lx == 15 ? 1 : 0;
            int nz = lz == 0 ? -1 : lz == 15 ? 1 : 0;
            if (nx != 0) RemeshNow(cc.X + nx, cc.Z);
            if (nz != 0) RemeshNow(cc.X, cc.Z + nz);
            if (nx != 0 && nz != 0) RemeshNow(cc.X + nx, cc.Z + nz);
            return true;
        }

        /// <summary>Forget hand edits in a box (used when a building is placed or bulldozed there).</summary>
        public void ClearEdits(Vector3Int min, Vector3Int max)
        {
            foreach (var cc in Edits.ClearBox(min, max))
                if (chunks.TryGetValue(cc, out var c)) Invalidate(c);
        }

        /// <summary>Regenerate every loaded chunk (after loading a save).</summary>
        public void RegenerateAll()
        {
            foreach (var c in chunks.Values) Invalidate(c);
        }

        /// <summary>Drop everything and start streaming again (new world / load).</summary>
        public void ResetAll()
        {
            foreach (var c in chunks.Values) DestroyChunk(c);
            chunks.Clear();
            Animated?.Clear();
        }

        private void Invalidate(Chunk c)
        {
            c.NeedsRegen = true;
            c.GenRequest++;
        }

        private void OnCityChanged(RectInt area)
        {
            var c0 = ChunkCoord.FromBlock(area.xMin, area.yMin);
            var c1 = ChunkCoord.FromBlock(area.xMax, area.yMax);
            for (int z = c0.Z; z <= c1.Z; z++)
                for (int x = c0.X; x <= c1.X; x++)
                    if (chunks.TryGetValue(new ChunkCoord(x, z), out var c)) Invalidate(c);
        }

        // ------------------------------------------------------------------ frame loop

        private void Update()
        {
            DrainGenResults();
            ScheduleWork();
            DrainMeshResults(8);
            if (Time.unscaledTime >= nextUnload)
            {
                nextUnload = Time.unscaledTime + 0.5f;
                UnloadFar();
            }
        }

        private void ScheduleWork()
        {
            int genRadius = radius + 1;
            int maxOffsets = sortedOffsets.Count;
            float genR2 = (genRadius + 0.5f) * (genRadius + 0.5f);
            float meshR2 = (radius + 0.5f) * (radius + 0.5f);

            for (int i = 0; i < maxOffsets && running < maxJobs; i++)
            {
                var o = sortedOffsets[i];
                if (o.sqrMagnitude > genR2) break;
                var cc = new ChunkCoord(focusChunk.X + o.x, focusChunk.Z + o.y);
                if (!chunks.TryGetValue(cc, out var c))
                {
                    c = new Chunk { Coord = cc };
                    chunks[cc] = c;
                }
                if (c.NeedsRegen) ScheduleGen(c);
                else if (c.NeedsMesh && o.sqrMagnitude <= meshR2 && NeighboursReady(cc)) ScheduleMesh(c);
            }
        }

        private bool NeighboursReady(ChunkCoord cc)
        {
            for (int dz = -1; dz <= 1; dz++)
                for (int dx = -1; dx <= 1; dx++)
                    if (!chunks.TryGetValue(new ChunkCoord(cc.X + dx, cc.Z + dz), out var n) || n.Blocks == null) return false;
            return true;
        }

        private ushort[][] GatherNeighbours(ChunkCoord cc)
        {
            var arr = new ushort[9][];
            for (int dz = -1; dz <= 1; dz++)
                for (int dx = -1; dx <= 1; dx++)
                    arr[(dx + 1) + (dz + 1) * 3] = chunks[new ChunkCoord(cc.X + dx, cc.Z + dz)].Blocks;
            return arr;
        }

        private void ScheduleGen(Chunk c)
        {
            c.NeedsRegen = false;
            int req = ++c.GenRequest;
            int editVersion = c.EditVersion;
            var cc = c.Coord;
            var area = new RectInt(cc.MinX - 8, cc.MinZ - 8, 32, 32);
            var snap = City.BuildSnapshot(area);
            var edits = Edits.GetPacked(cc);
            int seed = Seed;
            Interlocked.Increment(ref running);
            Task.Run(() =>
            {
                var r = new GenResult { Chunk = c, Request = req, EditVersion = editVersion };
                try { r.Data = ChunkGenerator.Generate(cc, seed, snap, edits); }
                catch (Exception e) { r.Error = e; }
                genResults.Enqueue(r);
                Interlocked.Decrement(ref running);
            });
        }

        private void ScheduleMesh(Chunk c)
        {
            c.NeedsMesh = false;
            int req = ++c.MeshRequest;
            var neighbours = GatherNeighbours(c.Coord);
            if (!meshPool.TryTake(out var md)) md = new MeshData();
            Interlocked.Increment(ref running);
            Task.Run(() =>
            {
                var r = new MeshResult { Chunk = c, Request = req, Mesh = md };
                try
                {
                    var padded = PaddedChunk.Build(neighbours, out int maxY);
                    ChunkMesher.Build(ref padded, 16, Mathf.Min(maxY + 2, WorldConst.ChunkHeight), 16, md, c.Coord.MinX, 0, c.Coord.MinZ);
                }
                catch (Exception e) { r.Error = e; }
                meshResults.Enqueue(r);
                Interlocked.Decrement(ref running);
            });
        }

        private void DrainGenResults()
        {
            while (genResults.TryDequeue(out var r))
            {
                if (r.Error != null) { Debug.LogException(r.Error); continue; }
                var c = r.Chunk;
                if (!chunks.TryGetValue(c.Coord, out var live) || live != c) continue;
                if (r.Request != c.GenRequest) continue;
                if (r.EditVersion != c.EditVersion) { c.NeedsRegen = true; continue; }
                c.Blocks = r.Data;
                for (int dz = -1; dz <= 1; dz++)
                    for (int dx = -1; dx <= 1; dx++)
                        if (chunks.TryGetValue(new ChunkCoord(c.Coord.X + dx, c.Coord.Z + dz), out var n) && n.Blocks != null)
                            n.NeedsMesh = true;
            }
        }

        private void DrainMeshResults(int budget)
        {
            while (budget > 0 && meshResults.TryDequeue(out var r))
            {
                if (r.Error != null) { Debug.LogException(r.Error); meshPool.Add(r.Mesh); continue; }
                var c = r.Chunk;
                if (chunks.TryGetValue(c.Coord, out var live) && live == c && r.Request == c.MeshRequest)
                {
                    Upload(c, r.Mesh);
                    budget--;
                }
                meshPool.Add(r.Mesh);
            }
        }

        private void RemeshNow(int cx, int cz)
        {
            if (chunks.TryGetValue(new ChunkCoord(cx, cz), out var c)) RemeshNow(c);
        }

        private void RemeshNow(Chunk c)
        {
            if (c.Blocks == null || !NeighboursReady(c.Coord)) return;
            c.MeshRequest++;
            c.NeedsMesh = false;
            var padded = PaddedChunk.Build(GatherNeighbours(c.Coord), out int maxY);
            ChunkMesher.Build(ref padded, 16, Mathf.Min(maxY + 2, WorldConst.ChunkHeight), 16, mainThreadMesh, c.Coord.MinX, 0, c.Coord.MinZ);
            Upload(c, mainThreadMesh);
        }

        private void Upload(Chunk c, MeshData md)
        {
            if (c.Go == null)
            {
                c.Go = new GameObject($"Chunk {c.Coord}");
                c.Go.transform.SetParent(transform, false);
                c.Go.transform.position = new Vector3(c.Coord.MinX, 0, c.Coord.MinZ);
                c.Mesh = new Mesh { name = c.Go.name };
                c.Mesh.MarkDynamic();
                c.Go.AddComponent<MeshFilter>().sharedMesh = c.Mesh;
                var mr = c.Go.AddComponent<MeshRenderer>();
                mr.sharedMaterials = new[] { opaqueMat, transparentMat };
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                mr.receiveShadows = false;
            }
            md.ApplyTo(c.Mesh);
            Animated?.SetChunk(c.Coord, md.Animated);
        }

        private void UnloadFar()
        {
            int keep = radius + 3;
            List<Chunk> remove = null;
            foreach (var c in chunks.Values)
            {
                if (Math.Abs(c.Coord.X - focusChunk.X) > keep || Math.Abs(c.Coord.Z - focusChunk.Z) > keep)
                    (remove ??= new List<Chunk>()).Add(c);
            }
            if (remove == null) return;
            foreach (var c in remove)
            {
                DestroyChunk(c);
                chunks.Remove(c.Coord);
                Animated?.Remove(c.Coord);
            }
        }

        private static void DestroyChunk(Chunk c)
        {
            if (c.Go != null) Destroy(c.Go);
            if (c.Mesh != null) Destroy(c.Mesh);
            c.Go = null;
            c.Mesh = null;
        }
    }
}
