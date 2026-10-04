using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UKCity.City;
using UKCity.World;

namespace UKCity.Save
{
    [Serializable] public class NodeSave { public int id; public float x, z; public bool roundabout; public float radius; }
    [Serializable] public class SegmentSave { public int id, a, b, type; public float[] crossings; }
    [Serializable] public class BuildingSave { public int id; public string template; public int x, y, z, rot; }
    [Serializable] public class EditSave { public int cx, cz; public int[] packed; }

    [Serializable]
    public class TemplateSave
    {
        public string id, name, category, description;
        public int sx, sy, sz, ax, az;
        public string blocks;

        public static TemplateSave From(BuildingTemplate t) => new TemplateSave
        {
            id = t.Id, name = t.Name, category = t.Category, description = t.Description,
            sx = t.SizeX, sy = t.SizeY, sz = t.SizeZ, ax = t.AnchorX, az = t.AnchorZ,
            blocks = Convert.ToBase64String(t.Blocks)
        };

        public BuildingTemplate ToTemplate() => new BuildingTemplate
        {
            Id = id, Name = name, Category = string.IsNullOrEmpty(category) ? "Custom" : category, Description = description ?? "",
            SizeX = sx, SizeY = sy, SizeZ = sz, AnchorX = ax, AnchorZ = az,
            Blocks = Convert.FromBase64String(blocks), IsCustom = true
        };
    }

    [Serializable]
    public class TemplateLibrarySave { public List<TemplateSave> templates = new List<TemplateSave>(); }

    [Serializable]
    public class WorldSave
    {
        public int version = 1;
        public int seed;
        public int nextId;
        public float px, py, pz, yaw, pitch;
        public bool flying;
        public float planX, planZ, planZoom;
        public int[] hotbar;
        public List<NodeSave> nodes = new List<NodeSave>();
        public List<SegmentSave> segments = new List<SegmentSave>();
        public List<BuildingSave> buildings = new List<BuildingSave>();
        public List<EditSave> edits = new List<EditSave>();
        public List<TemplateSave> templates = new List<TemplateSave>();
    }

    /// <summary>JSON saves in Application.persistentDataPath. One world slot for now, plus a shared custom template library.</summary>
    public static class SaveSystem
    {
        public static string Folder => Path.Combine(Application.persistentDataPath, "saves");
        public static string WorldPath(string slot) => Path.Combine(Folder, slot + ".json");
        public static string TemplatesPath => Path.Combine(Application.persistentDataPath, "custom_templates.json");

        public static bool Exists(string slot) => File.Exists(WorldPath(slot));

        public static void Save(GameManager g, string slot)
        {
            var s = new WorldSave
            {
                seed = g.World.Seed,
                nextId = g.City.NextId,
                px = g.Player.transform.position.x, py = g.Player.transform.position.y, pz = g.Player.transform.position.z,
                yaw = g.Player.Yaw, pitch = g.Player.Pitch, flying = g.Player.Flying,
                planX = g.Planner.Focus.x, planZ = g.Planner.Focus.z, planZoom = g.Planner.OrthoSize,
                hotbar = Array.ConvertAll(g.Interactor.Hotbar, b => (int)b)
            };
            foreach (var n in g.City.Nodes.Values)
                s.nodes.Add(new NodeSave { id = n.Id, x = n.Pos.x, z = n.Pos.y, roundabout = n.IsRoundabout, radius = n.RoundaboutRadius });
            foreach (var seg in g.City.Segments.Values)
                s.segments.Add(new SegmentSave { id = seg.Id, a = seg.A, b = seg.B, type = seg.Type, crossings = seg.Crossings.ToArray() });
            var usedCustom = new HashSet<string>();
            foreach (var b in g.City.Buildings.Values)
            {
                s.buildings.Add(new BuildingSave { id = b.Id, template = b.TemplateId, x = b.Origin.x, y = b.Origin.y, z = b.Origin.z, rot = b.Rotation });
                var t = b.Template;
                if (t != null && t.IsCustom && usedCustom.Add(t.Id)) s.templates.Add(TemplateSave.From(t));
            }
            foreach (var kv in g.World.Edits.All)
            {
                if (kv.Value.Count == 0) continue;
                s.edits.Add(new EditSave { cx = kv.Key.X, cz = kv.Key.Z, packed = g.World.Edits.GetPacked(kv.Key) });
            }

            Directory.CreateDirectory(Folder);
            string path = WorldPath(slot);
            string tmp = path + ".tmp";
            File.WriteAllText(tmp, JsonUtility.ToJson(s));
            if (File.Exists(path)) File.Delete(path);
            File.Move(tmp, path);
        }

        public static bool Load(GameManager g, string slot)
        {
            string path = WorldPath(slot);
            if (!File.Exists(path)) return false;
            var s = JsonUtility.FromJson<WorldSave>(File.ReadAllText(path));
            if (s == null) return false;

            foreach (var ts in s.templates)
                if (BuildingLibrary.Get(ts.id) == null) BuildingLibrary.Register(ts.ToTemplate());

            var city = g.City;
            city.Clear();
            foreach (var n in s.nodes)
                city.Nodes[n.id] = new RoadNode { Id = n.id, Pos = new Vector2(n.x, n.z), IsRoundabout = n.roundabout, RoundaboutRadius = n.radius };
            foreach (var seg in s.segments)
            {
                if (!city.Nodes.ContainsKey(seg.a) || !city.Nodes.ContainsKey(seg.b)) continue;
                var rs = new RoadSegment { Id = seg.id, A = seg.a, B = seg.b, Type = seg.type };
                if (seg.crossings != null) rs.Crossings.AddRange(seg.crossings);
                city.Segments[rs.Id] = rs;
                city.Nodes[seg.a].Segments.Add(rs.Id);
                city.Nodes[seg.b].Segments.Add(rs.Id);
            }
            foreach (var b in s.buildings)
                city.Buildings[b.id] = new BuildingPlacement { Id = b.id, TemplateId = b.template, Origin = new Vector3Int(b.x, b.y, b.z), Rotation = b.rot };
            city.NextId = Math.Max(s.nextId, 1);

            g.World.Edits.Clear();
            foreach (var e in s.edits) g.World.Edits.SetPacked(new ChunkCoord(e.cx, e.cz), e.packed);
            g.World.Seed = s.seed;
            g.World.ResetAll();

            if (s.hotbar != null && s.hotbar.Length == g.Interactor.Hotbar.Length)
                for (int i = 0; i < s.hotbar.Length; i++) g.Interactor.Hotbar[i] = (byte)s.hotbar[i];

            g.Player.Teleport(new Vector3(s.px, s.py, s.pz));
            g.Player.Yaw = s.yaw;
            g.Player.Pitch = s.pitch;
            g.Player.Flying = s.flying;
            g.Planner.CenterOn(new Vector3(s.planX, 0, s.planZ));
            if (s.planZoom > 0) g.Planner.OrthoSize = s.planZoom;
            return true;
        }

        public static void SaveCustomTemplates()
        {
            var lib = new TemplateLibrarySave();
            foreach (var t in BuildingLibrary.All)
                if (t.IsCustom) lib.templates.Add(TemplateSave.From(t));
            try
            {
                File.WriteAllText(TemplatesPath, JsonUtility.ToJson(lib));
            }
            catch (Exception e)
            {
                Debug.LogWarning("Could not save custom templates: " + e.Message);
            }
        }

        public static void LoadCustomTemplates()
        {
            try
            {
                if (!File.Exists(TemplatesPath)) return;
                var lib = JsonUtility.FromJson<TemplateLibrarySave>(File.ReadAllText(TemplatesPath));
                if (lib?.templates == null) return;
                foreach (var ts in lib.templates) BuildingLibrary.Register(ts.ToTemplate());
            }
            catch (Exception e)
            {
                Debug.LogWarning("Could not load custom templates: " + e.Message);
            }
        }
    }
}
