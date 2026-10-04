using System.Collections.Generic;
using UnityEngine;
using UKCity.City;
using UKCity.Core;
using UKCity.Planner;
using UKCity.World;

namespace UKCity.UI
{
    public enum Panel { None, Blocks, Buildings, Pause, Help }

    /// <summary>
    /// All on-screen UI, drawn with IMGUI so it needs no prefabs, canvases or fonts.
    /// Good enough for a prototype; swap for UI Toolkit later.
    /// </summary>
    public sealed class Hud : MonoBehaviour
    {
        public GameManager Game;
        public Panel Open = Panel.None;

        private float scale = 1f;
        private float W, H;
        private readonly List<Rect> uiRects = new List<Rect>();
        private Vector2 blockScroll, templateScroll, plannerScroll, signScroll;
        private int blockTab;
        private string hotbarTooltip;

        private GUIStyle panel, title, label, small, button, selected, toast, slotNumber, centre;
        private Texture2D white;

        // ------------------------------------------------------------------ helpers

        public bool PointerOverUI()
        {
            if (Open != Panel.None) return true;
            var m = GameInput.MousePosition;
            var v = new Vector2(m.x / scale, (Screen.height - m.y) / scale);
            foreach (var r in uiRects) if (r.Contains(v)) return true;
            return false;
        }

        private void Styles()
        {
            if (panel != null) return;
            white = new Texture2D(1, 1);
            white.SetPixel(0, 0, Color.white);
            white.Apply();

            var bg = new Texture2D(1, 1);
            bg.SetPixel(0, 0, new Color(0.08f, 0.09f, 0.11f, 0.86f));
            bg.Apply();
            panel = new GUIStyle(GUI.skin.box) { padding = new RectOffset(10, 10, 8, 8) };
            panel.normal.background = bg;

            title = new GUIStyle(GUI.skin.label) { fontSize = 17, fontStyle = FontStyle.Bold };
            title.normal.textColor = new Color(1f, 0.85f, 0.3f);
            label = new GUIStyle(GUI.skin.label) { fontSize = 13, wordWrap = true };
            label.normal.textColor = Color.white;
            small = new GUIStyle(label) { fontSize = 11 };
            small.normal.textColor = new Color(0.8f, 0.82f, 0.85f);
            centre = new GUIStyle(label) { alignment = TextAnchor.MiddleCenter };
            button = new GUIStyle(GUI.skin.button) { fontSize = 13, alignment = TextAnchor.MiddleLeft, padding = new RectOffset(8, 6, 4, 4) };
            selected = new GUIStyle(button);
            var selBg = new Texture2D(1, 1);
            selBg.SetPixel(0, 0, new Color(0.85f, 0.65f, 0.1f, 1f));
            selBg.Apply();
            selected.normal.background = selBg;
            selected.normal.textColor = Color.black;
            selected.hover.background = selBg;
            selected.hover.textColor = Color.black;
            toast = new GUIStyle(panel) { fontSize = 14, alignment = TextAnchor.MiddleCenter, wordWrap = true };
            toast.normal.textColor = Color.white;
            slotNumber = new GUIStyle(small) { fontSize = 10, alignment = TextAnchor.UpperLeft };
        }

        private Rect Box(Rect r)
        {
            uiRects.Add(r);
            GUI.Box(r, GUIContent.none, panel);
            return r;
        }

        private void Fill(Rect r, Color c)
        {
            var old = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(r, white);
            GUI.color = old;
        }

        private static void Icon(Rect r, ushort id)
        {
            var def = Blocks.Get(id);
            if (!def.Visible) return;
            TileIcon(r, def.IconTile);
        }

        private static void TileIcon(Rect r, int tile)
        {
            if (tile < 0) return;
            GUI.DrawTextureWithTexCoords(r, TextureAtlas.Icons, TextureAtlas.IconRect(tile));
        }

        private bool Button(Rect r, string text, bool isSelected = false) => GUI.Button(r, text, isSelected ? selected : button);

        // ------------------------------------------------------------------ main

        private void OnGUI()
        {
            Styles();
            if (Event.current.type == EventType.Layout) uiRects.Clear();
            scale = Mathf.Clamp(Screen.height / 900f, 0.75f, 2.5f);
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1));
            W = Screen.width / scale;
            H = Screen.height / scale;

            if (Game.Mode == GameMode.Walk) WalkHud();
            else PlanHud();

            switch (Open)
            {
                case Panel.Blocks: BlocksPanel(); break;
                case Panel.Buildings: BuildingsPanel(); break;
                case Panel.Pause: PausePanel(); break;
                case Panel.Help: HelpPanel(); break;
            }

            if (Game.ToastText != null && Time.unscaledTime < Game.ToastUntil)
            {
                var r = new Rect(W / 2 - 260, 60, 520, 44);
                GUI.Label(r, Game.ToastText, toast);
            }

            if (Game.World.PendingJobs > 0)
                GUI.Label(new Rect(W - 170, H - 24, 160, 20), $"Building world... ({Game.World.PendingJobs})", small);
        }

        // ------------------------------------------------------------------ walk mode

        private void WalkHud()
        {
            var ix = Game.Interactor;

            if (Open == Panel.None)
            {
                Fill(new Rect(W / 2 - 9, H / 2 - 1, 18, 2), new Color(1, 1, 1, 0.85f));
                Fill(new Rect(W / 2 - 1, H / 2 - 9, 2, 18), new Color(1, 1, 1, 0.85f));
            }

            // Info box.
            var p = Game.Player.transform.position;
            var info = Box(new Rect(10, 10, 360, 118));
            GUI.Label(new Rect(info.x + 10, info.y + 6, 340, 22), "FIRST PERSON", title);
            GUI.Label(new Rect(info.x + 10, info.y + 30, 340, 40),
                "Tab: city planner   E: blocks   B: buildings\nF: fly   C: capture template   F1: help", small);
            string target = ix.HasTarget ? Blocks.Get(ix.Target.Id).Name : "-";
            GUI.Label(new Rect(info.x + 10, info.y + 70, 340, 40),
                $"X {p.x:0}  Y {p.y - WorldConst.SurfaceY:0}  Z {p.z:0}   {(Game.Player.Flying ? "FLYING" : "")}\nLooking at: {target}", small);

            if (ix.ActiveTemplate != null)
            {
                var t = BuildingLibrary.Get(ix.ActiveTemplate);
                var r = Box(new Rect(W / 2 - 250, H - 150, 500, 50));
                GUI.Label(new Rect(r.x + 10, r.y + 4, 480, 22), $"Placing: {t?.Name}", title);
                GUI.Label(new Rect(r.x + 10, r.y + 26, 480, 20), "Left click: place   R: rotate   Shift: keep placing   Right click: cancel", small);
            }
            else if (ix.CaptureCorner.HasValue)
            {
                var r = Box(new Rect(W / 2 - 250, H - 150, 500, 40));
                GUI.Label(new Rect(r.x + 10, r.y + 10, 480, 20), "Capturing: aim at the opposite corner and press C. Esc cancels.", small);
            }

            Hotbar(new Rect(W / 2 - 9 * 54 / 2, H - 66, 9 * 54, 58), Open == Panel.Blocks);
        }

        private void Hotbar(Rect r, bool clickable)
        {
            var ix = Game.Interactor;
            Box(r);
            for (int i = 0; i < 9; i++)
            {
                var slot = new Rect(r.x + 4 + i * 54, r.y + 4, 50, 50);
                if (i == ix.Selected) Fill(new Rect(slot.x - 2, slot.y - 2, slot.width + 4, slot.height + 4), new Color(1f, 0.8f, 0.2f));
                Fill(slot, new Color(0.2f, 0.2f, 0.22f));
                Icon(new Rect(slot.x + 5, slot.y + 5, 40, 40), ix.Hotbar[i]);
                GUI.Label(new Rect(slot.x + 2, slot.y, 20, 14), (i + 1).ToString(), slotNumber);
                if (clickable && GUI.Button(slot, GUIContent.none, GUIStyle.none)) ix.Selected = i;
            }
            if (Open == Panel.None)
                GUI.Label(new Rect(r.x, r.y - 22, r.width, 20), Blocks.Get(ix.SelectedBlock).Name, centre);
        }

        private void BlocksPanel()
        {
            var r = Box(new Rect(W / 2 - 400, H / 2 - 280, 800, 500));
            GUI.Label(new Rect(r.x + 12, r.y + 8, 300, 24), "Blocks", title);
            GUI.Label(new Rect(r.x + 12, r.y + 32, 776, 20), "Pick a hotbar slot below, then click a block to put it there. Signs and props face you when placed. E or Esc to close.", small);
            var cats = Blocks.Categories;
            for (int i = 0; i < cats.Length; i++)
                if (Button(new Rect(r.x + 10 + i * 112, r.y + 56, 108, 26), cats[i], blockTab == i)) { blockTab = i; blockScroll = Vector2.zero; }

            hotbarTooltip = null;
            var view = new Rect(r.x + 10, r.y + 90, 780, 400);
            const int cell = 56;
            int perRow = 13, count = 0;
            string cat = cats[blockTab];
            foreach (var d in Blocks.All) if (d.Placeable && d.Category == cat) count++;
            blockScroll = GUI.BeginScrollView(view, blockScroll, new Rect(0, 0, 760, Mathf.Ceil(count / (float)perRow) * cell));
            int k = 0;
            foreach (var d in Blocks.All)
            {
                if (!d.Placeable || d.Category != cat) continue;
                var cr = new Rect((k % perRow) * cell, (k / perRow) * cell, cell - 4, cell - 4);
                Fill(cr, new Color(0.22f, 0.22f, 0.25f));
                Icon(new Rect(cr.x + 3, cr.y + 3, cr.width - 6, cr.height - 6), (ushort)d.Id);
                if (GUI.Button(cr, new GUIContent("", d.Name), GUIStyle.none)) Game.Interactor.Hotbar[Game.Interactor.Selected] = (ushort)d.Id;
                k++;
            }
            GUI.EndScrollView();
            if (!string.IsNullOrEmpty(GUI.tooltip)) hotbarTooltip = GUI.tooltip;
            if (hotbarTooltip != null)
                GUI.Label(new Rect(r.x + 330, r.y + 8, 460, 24), hotbarTooltip, title);
        }

        private void BuildingsPanel()
        {
            var r = Box(new Rect(W / 2 - 300, H / 2 - 260, 600, 520));
            GUI.Label(new Rect(r.x + 12, r.y + 8, 500, 24), "Buildings, Signs & Street Furniture", title);
            GUI.Label(new Rect(r.x + 12, r.y + 32, 576, 20), "Click one, then left click in the world to place it. B or Esc to close.", small);
            string picked = TemplateList(new Rect(r.x + 10, r.y + 56, 580, 454), ref templateScroll, Game.Interactor.ActiveTemplate, t => true);
            if (picked != null)
            {
                Game.Interactor.BeginTemplate(picked);
                Open = Panel.None;
            }
        }

        /// <summary>Scrollable template list grouped by category (and sign group). Returns the id clicked, if any.</summary>
        private string TemplateList(Rect view, ref Vector2 scroll, string current, System.Func<BuildingTemplate, bool> filter)
        {
            string picked = null;
            var rows = new List<(string header, BuildingTemplate t)>();
            foreach (var cat in BuildingLibrary.Categories)
            {
                string lastGroup = null;
                bool header = false;
                foreach (var t in BuildingLibrary.All)
                {
                    if (t.Category != cat || !filter(t)) continue;
                    if (!header) { rows.Add((cat, null)); header = true; }
                    if (!string.IsNullOrEmpty(t.Group) && t.Group != lastGroup && t.Group != cat) { rows.Add(("  " + t.Group, null)); lastGroup = t.Group; }
                    rows.Add((null, t));
                }
            }
            float contentH = 0;
            foreach (var row in rows) contentH += row.t == null ? 24 : 44;
            scroll = GUI.BeginScrollView(view, scroll, new Rect(0, 0, view.width - 20, contentH));
            float y = 0;
            foreach (var (header, t) in rows)
            {
                if (t == null)
                {
                    GUI.Label(new Rect(0, y, 300, 22), header, label);
                    y += 24;
                    continue;
                }
                var br = new Rect(0, y, view.width - 24, 40);
                if (Button(br, "", t.Id == current)) picked = t.Id;
                if (t.IconTile >= 0) TileIcon(new Rect(br.x + 4, br.y + 4, 32, 32), t.IconTile);
                float tx = t.IconTile >= 0 ? 42 : 8;
                var nameStyle = t.Id == current ? new GUIStyle(label) { normal = { textColor = Color.black } } : label;
                GUI.Label(new Rect(br.x + tx, br.y + 2, br.width - 100 - tx, 20), t.Name, nameStyle);
                GUI.Label(new Rect(br.x + tx, br.y + 20, br.width - 100 - tx, 18), t.Description, small);
                GUI.Label(new Rect(br.xMax - 90, br.y + 10, 86, 20), $"{t.SizeX}x{t.SizeZ}x{t.SizeY}", small);
                y += 44;
            }
            GUI.EndScrollView();
            return picked;
        }

        private void PausePanel()
        {
            var r = Box(new Rect(W / 2 - 150, H / 2 - 220, 300, 442));
            GUI.Label(new Rect(r.x + 12, r.y + 10, 276, 26), "Paused", title);
            float y = r.y + 46;
            if (Button(new Rect(r.x + 20, y, 260, 34), "Resume")) Open = Panel.None;
            y += 42;
            if (Button(new Rect(r.x + 20, y, 260, 34), "Save  (F5)")) Game.SaveGame();
            y += 42;
            if (Button(new Rect(r.x + 20, y, 260, 34), "Load  (F9)")) { Game.LoadGame(); Open = Panel.None; }
            y += 42;
            if (Button(new Rect(r.x + 20, y, 260, 34), "New world: starter town")) { Game.NewWorld(true); Open = Panel.None; }
            y += 42;
            if (Button(new Rect(r.x + 20, y, 260, 34), "New world: empty")) { Game.NewWorld(false); Open = Panel.None; }
            y += 42;
            if (Button(new Rect(r.x + 20, y, 260, 34), TextureAtlas.PixelArt ? "Textures: pixel-crisp" : "Textures: smooth")) TextureAtlas.PixelArt = !TextureAtlas.PixelArt;
            y += 42;
            if (Button(new Rect(r.x + 20, y, 260, 34), "Controls  (F1)")) Open = Panel.Help;
            y += 42;
            if (Button(new Rect(r.x + 20, y, 260, 34), "Quit")) Application.Quit();
        }

        private void HelpPanel()
        {
            var r = Box(new Rect(W / 2 - 330, H / 2 - 250, 660, 500));
            GUI.Label(new Rect(r.x + 12, r.y + 10, 600, 26), "Controls", title);
            GUI.Label(new Rect(r.x + 16, r.y + 44, 300, 440),
                "<b>First person</b>\n" +
                "WASD  move     Mouse  look\n" +
                "Space  jump (double-tap: fly)\n" +
                "F  toggle fly   Shift  sprint\n" +
                "Ctrl (flying)  descend\n" +
                "Left click  break block\n" +
                "Right click  place block\n" +
                "Middle click  pick block\n" +
                "1-9 / scroll  hotbar\n" +
                "E  block palette\n" +
                "B  building templates\n" +
                "R  rotate template\n" +
                "C  capture region as template\n" +
                "Tab  switch to city planner\n\n" +
                "<b>General</b>\n" +
                "F5 save   F9 load   Esc menu", new GUIStyle(label) { richText = true });
            GUI.Label(new Rect(r.x + 340, r.y + 44, 300, 440),
                "<b>City planner (2D)</b>\n" +
                "WASD / arrows  pan   Scroll  zoom\n" +
                "Middle / right drag  pan\n" +
                "1 Select  2 Road  3 Roundabout\n" +
                "4 Crossing  5 Signals  6 Sign\n" +
                "7 Building  8 Bulldoze\n\n" +
                "Road: click to start, click to add\n" +
                "points, right click to finish.\n" +
                "Shift snaps to 15 degree angles.\n" +
                "Crossing roads make junctions.\n" +
                "R  cycle road type / rotate building\n" +
                "Delete  remove selection\n" +
                "G  chunk grid   H  hide overlay\n" +
                "P  walk here (drop into 3D)\n" +
                "Tab  back to first person", new GUIStyle(label) { richText = true });
        }

        // ------------------------------------------------------------------ planner

        private void PlanHud()
        {
            var pl = Game.Planner;
            var r = Box(new Rect(10, 10, 270, H - 20));
            GUI.Label(new Rect(r.x + 10, r.y + 6, 250, 24), "CITY PLANNER", title);
            GUI.Label(new Rect(r.x + 10, r.y + 28, 250, 18), "Tab: first person   P: walk here   F1: help", small);

            float y = r.y + 48;
            string[] names = { "1  Select / Move", "2  Draw Road", "3  Roundabout", "4  Crossing", "5  Traffic Signals", "6  Road Sign", "7  Place Building", "8  Bulldoze" };
            for (int i = 0; i < names.Length; i++)
            {
                if (Button(new Rect(r.x + 10, y, 250, 24), names[i], (int)pl.Tool == i)) pl.SetTool((PlannerTool)i);
                y += 26;
            }
            y += 6;
            Fill(new Rect(r.x + 10, y, 250, 1), new Color(1, 1, 1, 0.2f));
            y += 8;

            var opts = new Rect(r.x + 10, y, 250, r.yMax - y - 70);
            switch (pl.Tool)
            {
                case PlannerTool.Road: RoadOptions(opts); break;
                case PlannerTool.Roundabout: RoundaboutOptions(opts); break;
                case PlannerTool.Crossing:
                    if (Button(new Rect(opts.x, opts.y, opts.width, 24), "Zebra (Belisha beacons)", pl.NewCrossingKind == CrossingKind.Zebra)) pl.NewCrossingKind = CrossingKind.Zebra;
                    if (Button(new Rect(opts.x, opts.y + 26, opts.width, 24), "Puffin (signal controlled)", pl.NewCrossingKind == CrossingKind.Signal)) pl.NewCrossingKind = CrossingKind.Signal;
                    GUI.Label(new Rect(opts.x, opts.y + 58, opts.width, 120), "Click a road with pavements to add a crossing, with zig-zags, tactile paving and beacons or signals. Click an existing crossing to switch its type. Bulldoze removes it.", small);
                    break;
                case PlannerTool.Signals:
                    GUI.Label(opts, "Click a junction (3+ roads) to add traffic lights; click again to cycle the hardware: LED heads, older-style heads, LED with left filter arrows and illuminated no-right-turn pods, then off.\n\nYou get stop lines, lane arrows and a pedestrian stage with crossings. Shift+click toggles a yellow box junction.\n\nMore head types (arrow signals, cycle signals, toucan, puffin units) are in the building list under Signals.", label);
                    break;
                case PlannerTool.Sign: SignOptions(opts); break;
                case PlannerTool.Building: BuildingOptions(opts); break;
                case PlannerTool.Select: SelectOptions(opts); break;
                case PlannerTool.Bulldoze:
                    GUI.Label(opts, "Click a junction, road, crossing or building to demolish it.\n\nYour hand-built blocks elsewhere are untouched.", label);
                    break;
            }

            float by = r.yMax - 62;
            pl.ShowGrid = GUI.Toggle(new Rect(r.x + 10, by, 120, 20), pl.ShowGrid, " Grid (G)");
            pl.ShowOverlay = GUI.Toggle(new Rect(r.x + 130, by, 130, 20), pl.ShowOverlay, " Overlay (H)");
            if (Button(new Rect(r.x + 10, by + 24, 250, 28), "Walk here  (P)")) Game.WalkHere(new Vector2(pl.Focus.x, pl.Focus.z));

            // Stats.
            var s = Box(new Rect(W - 230, 10, 220, 64));
            GUI.Label(new Rect(s.x + 10, s.y + 6, 200, 54),
                $"Roads: {Game.City.Segments.Count}   Junctions: {CountJunctions()}\nBuildings: {Game.City.Buildings.Count}\nCursor: {pl.MouseWorld.x:0}, {pl.MouseWorld.y:0}", small);

            // Length label while drawing.
            if (pl.Tool == PlannerTool.Road && pl.IsDrawing)
            {
                var sp = pl.Cam.WorldToScreenPoint(new Vector3(pl.MouseWorld.x, WorldConst.SurfaceY, pl.MouseWorld.y));
                GUI.Label(new Rect(sp.x / scale + 16, (Screen.height - sp.y) / scale + 8, 120, 22), $"{pl.PreviewLength:0} m", title);
            }
        }

        private int CountJunctions()
        {
            int n = 0;
            foreach (var node in Game.City.Nodes.Values) if (node.Segments.Count >= 3 || node.IsRoundabout) n++;
            return n;
        }

        private void RoadOptions(Rect r)
        {
            var pl = Game.Planner;
            GUI.Label(new Rect(r.x, r.y, r.width, 20), "Road type (R cycles)", label);
            float y = r.y + 24;
            foreach (var t in RoadTypes.All)
            {
                if (Button(new Rect(r.x, y, r.width, 24), t.Name, pl.RoadTypeId == t.Id)) pl.RoadTypeId = t.Id;
                y += 26;
            }
            var cur = RoadTypes.Get(pl.RoadTypeId);
            GUI.Label(new Rect(r.x, y + 4, r.width, 60), $"{cur.Description}\nWidth {cur.MaxHalf * 2:0} m.", small);
            GUI.Label(new Rect(r.x, y + 66, r.width, 100),
                "Click to start, click again to lay each section, right click to stop. Shift snaps angles. Drop onto a road to make a junction.", small);
        }

        private void RoundaboutOptions(Rect r)
        {
            var pl = Game.Planner;
            GUI.Label(new Rect(r.x, r.y, r.width, 20), "Size", label);
            float y = r.y + 24;
            (string, float)[] sizes = { ("Mini (painted)", 4f), ("Small", 10f), ("Large", 14f) };
            foreach (var (name, rad) in sizes)
            {
                if (Button(new Rect(r.x, y, r.width, 24), name, Mathf.Approximately(pl.RoundaboutRadius, rad))) pl.RoundaboutRadius = rad;
                y += 26;
            }
            GUI.Label(new Rect(r.x, y + 6, r.width, 100),
                "Click a junction or road to turn it into a roundabout, or empty ground for a standalone one. Click the same size again to remove it. Traffic will go clockwise, as it should.", small);
        }

        private void BuildingOptions(Rect r)
        {
            var pl = Game.Planner;
            pl.AutoOrient = GUI.Toggle(new Rect(r.x, r.y, r.width, 20), pl.AutoOrient, " Face nearest road");
            GUI.Label(new Rect(r.x, r.y + 22, r.width, 20), "R rotates. Click to place.", small);
            string picked = TemplateList(new Rect(r.x - 4, r.y + 46, r.width + 8, r.height - 46), ref plannerScroll, pl.TemplateId, t => !t.IsSign);
            if (picked != null) pl.TemplateId = picked;
        }

        private void SignOptions(Rect r)
        {
            var pl = Game.Planner;
            GUI.Label(new Rect(r.x, r.y, r.width, 40), "Click beside a road: the sign stands at the kerb facing oncoming traffic. R rotates.", small);
            var t = BuildingLibrary.Get(pl.SignTemplateId);
            GUI.Label(new Rect(r.x, r.y + 38, r.width, 20), t?.Name ?? "", label);
            var view = new Rect(r.x - 4, r.y + 62, r.width + 8, r.height - 62);
            const int cell = 46;
            int perRow = 5;
            var signs = new List<BuildingTemplate>();
            foreach (var bt in BuildingLibrary.All) if (bt.IsSign) signs.Add(bt);
            signScroll = GUI.BeginScrollView(view, signScroll, new Rect(0, 0, view.width - 20, Mathf.Ceil(signs.Count / (float)perRow) * cell));
            for (int i = 0; i < signs.Count; i++)
            {
                var cr = new Rect((i % perRow) * cell, (i / perRow) * cell, cell - 4, cell - 4);
                Fill(cr, signs[i].Id == pl.SignTemplateId ? new Color(0.85f, 0.65f, 0.1f) : new Color(0.22f, 0.22f, 0.25f));
                TileIcon(new Rect(cr.x + 3, cr.y + 3, cr.width - 6, cr.height - 6), signs[i].IconTile);
                if (GUI.Button(cr, new GUIContent("", signs[i].Name), GUIStyle.none)) pl.SignTemplateId = signs[i].Id;
            }
            GUI.EndScrollView();
            if (!string.IsNullOrEmpty(GUI.tooltip)) GUI.Label(new Rect(r.x, r.y + 38, r.width, 20), GUI.tooltip, label);
        }

        private void SelectOptions(Rect r)
        {
            var pl = Game.Planner;
            var city = Game.City;
            float y = r.y;
            if (pl.SelectedSegment != 0 && city.Segments.TryGetValue(pl.SelectedSegment, out var seg))
            {
                float len = Vector2.Distance(city.Nodes[seg.A].Pos, city.Nodes[seg.B].Pos);
                GUI.Label(new Rect(r.x, y, r.width, 20), $"Road section, {len:0} m", label);
                y += 24;
                foreach (var t in RoadTypes.All)
                {
                    if (Button(new Rect(r.x, y, r.width, 24), t.Name, seg.Type == t.Id)) city.SetSegmentType(seg.Id, t.Id);
                    y += 26;
                }
                if (Button(new Rect(r.x, y + 6, r.width, 26), "Delete section  (Del)")) pl.DeleteSelection();
            }
            else if (pl.SelectedNode != 0 && city.Nodes.TryGetValue(pl.SelectedNode, out var node))
            {
                GUI.Label(new Rect(r.x, y, r.width, 40), $"Junction with {node.Segments.Count} road(s)\nDrag to move.", label);
                y += 46;
                if (node.IsRoundabout)
                {
                    if (Button(new Rect(r.x, y, r.width, 26), "Remove roundabout")) city.SetRoundabout(node.Id, 0);
                }
                else if (Button(new Rect(r.x, y, r.width, 26), "Make roundabout")) city.SetRoundabout(node.Id, pl.RoundaboutRadius);
                y += 30;
                if (!node.IsRoundabout && node.Segments.Count >= 3)
                {
                    if (Button(new Rect(r.x, y, r.width, 26), node.Signals ? "Remove traffic lights" : "Add traffic lights")) city.SetSignals(node.Id, !node.Signals);
                    y += 30;
                    if (node.Signals && Button(new Rect(r.x, y, r.width, 26), node.YellowBox ? "Remove yellow box" : "Add yellow box")) city.SetYellowBox(node.Id, !node.YellowBox);
                    y += 30;
                    if (node.Signals)
                    {
                        GUI.Label(new Rect(r.x, y, r.width, 20), "Signal heads:", small);
                        y += 20;
                        foreach (SignalStyle st in new[] { SignalStyle.Modern, SignalStyle.Classic, SignalStyle.Filters })
                        {
                            if (Button(new Rect(r.x, y, r.width, 24), PlannerController.StyleName(st), node.Style == st)) city.SetSignalStyle(node.Id, st);
                            y += 26;
                        }
                    }
                }
                if (Button(new Rect(r.x, y, r.width, 26), "Delete junction  (Del)")) pl.DeleteSelection();
            }
            else if (pl.SelectedBuilding != 0 && city.Buildings.TryGetValue(pl.SelectedBuilding, out var b))
            {
                GUI.Label(new Rect(r.x, y, r.width, 20), b.Template?.Name ?? b.TemplateId, label);
                y += 26;
                if (Button(new Rect(r.x, y, r.width, 26), "Rotate")) pl.SelectedBuilding = Game.RotateBuilding(b.Id);
                y += 30;
                if (Button(new Rect(r.x, y, r.width, 26), "Demolish  (Del)")) pl.DeleteSelection();
            }
            else
            {
                GUI.Label(r, "Click a road, junction or building to select it. Drag junctions to reshape roads.", label);
            }
        }
    }
}
