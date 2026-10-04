using System.Collections.Generic;
using UnityEngine;
using UKCity.City;
using UKCity.Core;
using UKCity.World;

namespace UKCity.Player
{
    /// <summary>
    /// First-person building: break / place blocks, a 9-slot hotbar, placing building templates
    /// with a ghost preview, and capturing a region of the world as a new reusable template.
    /// </summary>
    public sealed class BlockInteractor : MonoBehaviour
    {
        public const float Reach = 8f;

        public GameManager Game;
        public PlayerController Player;

        public readonly ushort[] Hotbar =
        {
            BlockIds.RedBrick, BlockIds.YellowBrick, BlockIds.Window, BlockIds.DoorRed, BlockIds.Slate,
            BlockIds.Planks, BlockIds.Pavement, BlockIds.Asphalt, BlockIds.Glass
        };
        public int Selected;

        /// <summary>Template being placed, or null for normal block mode.</summary>
        public string ActiveTemplate;
        public int TemplateRotOffset;

        public bool HasTarget { get; private set; }
        public VoxelHit Target { get; private set; }
        public Vector3Int? CaptureCorner { get; private set; }

        private float repeatTimer;
        private readonly Dictionary<(string, int), Mesh> ghostMeshes = new Dictionary<(string, int), Mesh>();
        private Material ghostMat;

        public ushort SelectedBlock => Hotbar[Selected];

        private void Start()
        {
            var shader = Shader.Find("UKCity/Ghost");
            ghostMat = new Material(shader != null ? shader : Shader.Find("Unlit/Transparent"));
            ghostMat.SetTexture("_Tiles", TextureAtlas.Array);
            ghostMat.SetColor("_Color", new Color(0.7f, 1f, 0.7f, 0.55f));
        }

        public void BeginTemplate(string id)
        {
            ActiveTemplate = id;
            TemplateRotOffset = 0;
        }

        public void CancelTemplate() => ActiveTemplate = null;

        /// <summary>Called by GameManager each frame while in walk mode with no menu open.</summary>
        public void Tick(bool inputEnabled)
        {
            var cam = Player.Cam.transform;
            HasTarget = VoxelRaycast.Cast(Game.World, cam.position, cam.forward, Reach, out var hit);
            Target = hit;
            if (!inputEnabled) return;

            // Hotbar selection.
            int d = GameInput.DigitDown();
            if (d >= 0) Selected = d;
            float scroll = GameInput.Scroll;
            if (scroll > 0.01f) Selected = (Selected + 8) % 9;
            else if (scroll < -0.01f) Selected = (Selected + 1) % 9;

            if (ActiveTemplate != null) TemplateInput();
            else BlockInput();

            if (GameInput.KeyDown(KeyCode.C)) Capture();
        }

        private void BlockInput()
        {
            repeatTimer -= Time.deltaTime;
            bool breakNow = GameInput.MouseDown(0) || (GameInput.MouseHeld(0) && repeatTimer <= 0);
            bool placeNow = GameInput.MouseDown(1) || (GameInput.MouseHeld(1) && repeatTimer <= 0);

            if (breakNow && HasTarget)
            {
                Game.World.SetBlock(Target.Pos.x, Target.Pos.y, Target.Pos.z, BlockIds.Air);
                repeatTimer = 0.22f;
            }
            else if (placeNow && HasTarget)
            {
                var p = Target.Adjacent;
                ushort id = SelectedBlock;
                ushort existing = Game.World.GetBlock(p.x, p.y, p.z);
                bool replaceable = existing == BlockIds.Air || existing == BlockIds.Water;
                if (replaceable && (!Blocks.Get(id).Solid || !Player.Overlaps(p)))
                    Game.World.SetBlock(p.x, p.y, p.z, OrientForPlayer(id));
                repeatTimer = 0.22f;
            }

            if (GameInput.MouseDown(2) && HasTarget)
            {
                var def = Blocks.Get(Target.Id);
                if (def.Placeable)
                {
                    ushort id = (ushort)def.Id;
                    int existing = System.Array.IndexOf(Hotbar, id);
                    if (existing >= 0) Selected = existing;
                    else Hotbar[Selected] = id;
                }
            }
        }

        /// <summary>Rotatable blocks face the player; road markings point the way the player is looking.</summary>
        private ushort OrientForPlayer(ushort id)
        {
            var def = Blocks.Get(id);
            if (!def.Rotatable) return id;
            var f = Player.Cam.transform.forward;
            var look = new Vector2(f.x, f.z);
            bool marking = def.Category == "Markings";
            return BlockState.Make(def.Id, BlockState.FacingToward(marking ? look : -look));
        }

        // ------------------------------------------------------------------ templates

        public int CurrentTemplateRotation()
        {
            // Front of the building faces back toward the player, then R rotates further.
            var f = Player.Cam.transform.forward;
            var toPlayer = new Vector2(-f.x, -f.z);
            int best = 0;
            float bestDot = float.MinValue;
            for (int r = 0; r < 4; r++)
            {
                var fd = BuildingTemplate.FrontDirection(r);
                float dot = fd.x * toPlayer.x + fd.y * toPlayer.y;
                if (dot > bestDot) { bestDot = dot; best = r; }
            }
            return (best + TemplateRotOffset) & 3;
        }

        public Vector3Int TemplateOrigin => new Vector3Int(Target.Adjacent.x, Target.Adjacent.y - 1, Target.Adjacent.z);

        private void TemplateInput()
        {
            if (GameInput.KeyDown(KeyCode.R)) TemplateRotOffset = (TemplateRotOffset + 1) & 3;
            if (GameInput.MouseDown(1)) { CancelTemplate(); return; }
            if (GameInput.MouseDown(0) && HasTarget)
            {
                Game.PlaceBuilding(ActiveTemplate, TemplateOrigin, CurrentTemplateRotation());
                if (!GameInput.Shift) CancelTemplate();
            }
        }

        public Mesh GhostMesh(BuildingTemplate t, int rot)
        {
            var key = (t.Id, rot);
            if (ghostMeshes.TryGetValue(key, out var m) && m != null) return m;
            var rv = t.GetRotated(rot);
            var src = new BoxSource { Data = rv.Blocks, SX = rv.SizeX, SY = rv.SizeY, SZ = rv.SizeZ };
            var md = new MeshData();
            ChunkMesher.Build(ref src, rv.SizeX, rv.SizeY, rv.SizeZ, md);
            // Ghost draws everything in one pass.
            md.Opaque.AddRange(md.Transparent);
            md.Transparent.Clear();
            m = new Mesh { name = "Ghost " + t.Id };
            md.ApplyTo(m);
            ghostMeshes[key] = m;
            return m;
        }

        public void InvalidateGhost(string templateId)
        {
            for (int r = 0; r < 4; r++)
                if (ghostMeshes.TryGetValue((templateId, r), out var m)) { Destroy(m); ghostMeshes.Remove((templateId, r)); }
        }

        public void DrawGhost(BuildingTemplate t, Vector3Int origin, int rot, Camera cam, bool valid)
        {
            var rv = t.GetRotated(rot);
            var mesh = GhostMesh(t, rot);
            var pos = new Vector3(origin.x + rv.OffsetX, origin.y + 0.02f, origin.z + rv.OffsetZ);
            ghostMat.SetColor("_Color", valid ? new Color(0.7f, 1f, 0.7f, 0.55f) : new Color(1f, 0.5f, 0.5f, 0.55f));
            Graphics.DrawMesh(mesh, Matrix4x4.Translate(pos), ghostMat, 0, cam, 0);
        }

        /// <summary>Draw selection outline, template ghost and capture box for this frame.</summary>
        public void DrawOverlays(LineBatch lines, Camera cam)
        {
            if (ActiveTemplate != null)
            {
                var t = BuildingLibrary.Get(ActiveTemplate);
                if (t != null && HasTarget)
                {
                    int rot = CurrentTemplateRotation();
                    var o = TemplateOrigin;
                    DrawGhost(t, o, rot, cam, true);
                    var fp = t.Footprint(o, rot);
                    lines.WireBox(new Vector3(fp.xMin, o.y, fp.yMin), new Vector3(fp.xMax, o.y + t.SizeY, fp.yMax), new Color(0.6f, 1f, 0.6f, 0.9f));
                    // Arrow showing which way the front faces.
                    var fd = BuildingTemplate.FrontDirection(rot);
                    var c = new Vector3((fp.xMin + fp.xMax) * 0.5f, o.y + 1.05f, (fp.yMin + fp.yMax) * 0.5f);
                    var tip = c + new Vector3(fd.x, 0, fd.y) * (Mathf.Max(fp.width, fp.height) * 0.5f + 2f);
                    lines.Line(c, tip, Color.yellow);
                }
            }
            else if (HasTarget)
            {
                var p = Target.Pos;
                var sb = Blocks.ShapeBounds(Target.Id);
                lines.WireBox(p + sb.min - new Vector3(0.002f, 0.002f, 0.002f), p + sb.max + new Vector3(0.002f, 0.002f, 0.002f), new Color(0, 0, 0, 0.8f));
            }

            if (CaptureCorner.HasValue)
            {
                var a = CaptureCorner.Value;
                var b = HasTarget ? Target.Pos : a;
                var min = Vector3Int.Min(a, b);
                var max = Vector3Int.Max(a, b) + Vector3Int.one;
                lines.WireBox(min, max, new Color(0.3f, 0.8f, 1f, 1f));
            }
        }

        // ------------------------------------------------------------------ capture

        private void Capture()
        {
            if (!HasTarget) return;
            if (!CaptureCorner.HasValue)
            {
                CaptureCorner = Target.Pos;
                Game.Toast("Capture: corner 1 set. Aim at the opposite corner and press C again (Esc to cancel).");
                return;
            }
            var a = CaptureCorner.Value;
            var b = Target.Pos;
            CaptureCorner = null;
            var min = Vector3Int.Min(a, b);
            var max = Vector3Int.Max(a, b);
            var size = max - min + Vector3Int.one;
            if (size.x > 64 || size.y > 64 || size.z > 64)
            {
                Game.Toast("Capture too big (max 64 x 64 x 64).");
                return;
            }

            var tb = new TemplateBuilder(size.x, size.y, size.z);
            for (int y = 0; y < size.y; y++)
                for (int z = 0; z < size.z; z++)
                    for (int x = 0; x < size.x; x++)
                    {
                        ushort id = Game.World.GetBlock(min.x + x, min.y + y, min.z + z);
                        tb.Set(x, y, z, id == BlockIds.Unloaded ? BuildingTemplate.Keep : id);
                    }
            string name = $"Custom {System.DateTime.Now:HHmmss}";
            var t = tb.Build("custom_" + System.Guid.NewGuid().ToString("N").Substring(0, 8), name, "Custom", size.x / 2, 0,
                $"Captured {size.x} x {size.y} x {size.z}. The front is the south (lowest Z) side.");
            t.IsCustom = true;
            BuildingLibrary.Register(t);
            Save.SaveSystem.SaveCustomTemplates();
            Game.Toast($"Saved template '{name}'. Find it under Custom in the building menu (B).");
        }

        public void CancelCapture() => CaptureCorner = null;
    }
}
