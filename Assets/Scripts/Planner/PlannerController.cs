using System.Collections.Generic;
using UnityEngine;
using UKCity.City;
using UKCity.Core;
using UKCity.World;

namespace UKCity.Planner
{
    public enum PlannerTool { Select, Road, Roundabout, Crossing, Signals, Sign, Building, Bulldoze }

    /// <summary>
    /// The top-down 2D city planner. An orthographic camera looks straight down at the same voxel world
    /// you walk around in; the tools edit the city plan (road graph + building placements), which is
    /// re-rasterised into blocks underneath.
    /// </summary>
    public sealed class PlannerController : MonoBehaviour
    {
        public GameManager Game;
        public Camera Cam;

        public PlannerTool Tool = PlannerTool.Road;
        public int RoadTypeId;
        public float RoundaboutRadius = 10f;
        public string TemplateId = "terrace_row";
        public string SignTemplateId = "sign_limit_30";
        public CrossingKind NewCrossingKind = CrossingKind.Zebra;
        public int TemplateRotOffset;
        public bool AutoOrient = true;
        public bool ShowGrid;
        public bool ShowOverlay = true;

        public float OrthoSize = 40f;
        public const float MinZoom = 8f, MaxZoom = 110f;

        // Selection & hover.
        public int SelectedNode, SelectedSegment, SelectedBuilding;
        private int hoverNode, hoverSegment, hoverBuilding;
        private bool hoverCrossing;

        // Road drawing.
        private bool drawing;
        private Anchor start;

        // Dragging a node with the select tool.
        private int dragNode;
        private Vector2 dragFrom;
        private bool dragMoved;

        // Panning with the mouse.
        private Vector3 panGrab;
        private bool panning;

        public Vector2 MouseWorld { get; private set; }
        public Vector2 SnappedMouse => Snap(MouseWorld);
        public bool IsDrawing => drawing;
        public Vector2 DrawStart => start.Pos;
        public float PreviewLength { get; private set; }

        private struct Anchor
        {
            public int NodeId;
            public int SegId;
            public Vector2 Pos;
        }

        private const float OverlayY = WorldConst.SurfaceY + 1.2f;

        public static Vector2 Snap(Vector2 p) => new Vector2(Mathf.Floor(p.x) + 0.5f, Mathf.Floor(p.y) + 0.5f);

        public void CenterOn(Vector3 worldPos)
        {
            Cam.transform.position = new Vector3(worldPos.x, WorldConst.SurfaceY + 160f, worldPos.z);
        }

        public Vector3 Focus => new Vector3(Cam.transform.position.x, 0, Cam.transform.position.z);

        public int LoadRadius => Mathf.Clamp(Mathf.CeilToInt(OrthoSize * Mathf.Max(1f, Cam.aspect) / 16f) + 1, 3, VoxelWorld.MaxRadius);

        public void CancelAll()
        {
            drawing = false;
            dragNode = 0;
            panning = false;
        }

        public void SetTool(PlannerTool t)
        {
            Tool = t;
            CancelAll();
        }

        // ------------------------------------------------------------------ per-frame

        public void Tick(bool pointerOverUi)
        {
            CameraControls(pointerOverUi);

            var ray = Cam.ScreenPointToRay(GameInput.MousePosition);
            MouseWorld = new Vector2(ray.origin.x, ray.origin.z);
            UpdateHover();

            int digit = GameInput.DigitDown();
            if (digit >= 0 && digit < 8) SetTool((PlannerTool)digit);
            if (GameInput.KeyDown(KeyCode.G)) ShowGrid = !ShowGrid;
            if (GameInput.KeyDown(KeyCode.H)) ShowOverlay = !ShowOverlay;
            if (GameInput.KeyDown(KeyCode.R))
            {
                if (Tool == PlannerTool.Building || Tool == PlannerTool.Sign) TemplateRotOffset = (TemplateRotOffset + 1) & 3;
                else if (Tool == PlannerTool.Road) RoadTypeId = (RoadTypeId + 1) % RoadTypes.All.Count;
            }
            if (GameInput.KeyDown(KeyCode.Delete) || GameInput.KeyDown(KeyCode.Backspace)) DeleteSelection();
            if (GameInput.KeyDown(KeyCode.P) && !pointerOverUi) Game.WalkHere(MouseWorld);

            if (pointerOverUi && !drawing && dragNode == 0) return;

            switch (Tool)
            {
                case PlannerTool.Select: SelectTool(); break;
                case PlannerTool.Road: RoadTool(); break;
                case PlannerTool.Roundabout: RoundaboutTool(); break;
                case PlannerTool.Crossing: CrossingTool(); break;
                case PlannerTool.Signals: SignalsTool(); break;
                case PlannerTool.Sign: SignTool(); break;
                case PlannerTool.Building: BuildingTool(); break;
                case PlannerTool.Bulldoze: BulldozeTool(); break;
            }
        }

        private void CameraControls(bool pointerOverUi)
        {
            float pan = OrthoSize * 1.4f * Time.unscaledDeltaTime * (GameInput.Shift ? 2.5f : 1f);
            var move = Vector3.zero;
            if (GameInput.KeyHeld(KeyCode.W) || GameInput.KeyHeld(KeyCode.UpArrow)) move.z += 1;
            if (GameInput.KeyHeld(KeyCode.S) || GameInput.KeyHeld(KeyCode.DownArrow)) move.z -= 1;
            if (GameInput.KeyHeld(KeyCode.D) || GameInput.KeyHeld(KeyCode.RightArrow)) move.x += 1;
            if (GameInput.KeyHeld(KeyCode.A) || GameInput.KeyHeld(KeyCode.LeftArrow)) move.x -= 1;
            Cam.transform.position += move * pan;

            if (!pointerOverUi)
            {
                float scroll = GameInput.Scroll;
                if (Mathf.Abs(scroll) > 0.01f)
                {
                    // Zoom towards the cursor.
                    var before = Cam.ScreenPointToRay(GameInput.MousePosition).origin;
                    OrthoSize = Mathf.Clamp(OrthoSize * Mathf.Pow(0.88f, scroll), MinZoom, MaxZoom);
                    Cam.orthographicSize = OrthoSize;
                    var after = Cam.ScreenPointToRay(GameInput.MousePosition).origin;
                    Cam.transform.position += new Vector3(before.x - after.x, 0, before.z - after.z);
                }
            }
            Cam.orthographicSize = OrthoSize;

            // Middle-drag pans. Right-drag pans when not drawing a road (right-click ends a road instead).
            bool panButton = GameInput.MouseHeld(2) || (GameInput.MouseHeld(1) && !drawing && !GameInput.MouseDown(1) && Tool != PlannerTool.Road);
            var mouse = Cam.ScreenPointToRay(GameInput.MousePosition).origin;
            if (panButton && !panning && !pointerOverUi) { panning = true; panGrab = mouse; }
            if (!panButton) panning = false;
            if (panning)
            {
                var delta = panGrab - mouse;
                Cam.transform.position += new Vector3(delta.x, 0, delta.z);
            }
        }

        private void UpdateHover()
        {
            float r = HoverRadius;
            var city = Game.City;
            var n = city.FindNode(MouseWorld, r);
            hoverNode = n != null ? n.Id : 0;
            var s = city.FindSegment(MouseWorld, 0.5f, out _);
            hoverSegment = s != null ? s.Id : 0;
            var b = city.FindBuildingAt(Mathf.FloorToInt(MouseWorld.x), Mathf.FloorToInt(MouseWorld.y));
            hoverBuilding = b != null ? b.Id : 0;
            hoverCrossing = false;
            if (s != null)
            {
                foreach (var c in s.Crossings)
                    if (Vector2.Distance(city.CrossingPos(s, c.T), MouseWorld) < 2.5f) hoverCrossing = true;
            }
        }

        private float HoverRadius => Mathf.Max(2.0f, OrthoSize * 0.035f);

        // ------------------------------------------------------------------ anchors

        private Anchor ResolveAnchor(Vector2 p)
        {
            var city = Game.City;
            var n = city.FindNode(p, HoverRadius);
            if (n != null) return new Anchor { NodeId = n.Id, Pos = n.Pos };
            var s = city.FindSegment(p, 0.5f, out float t);
            if (s != null)
            {
                var a = city.Nodes[s.A].Pos;
                var b = city.Nodes[s.B].Pos;
                return new Anchor { SegId = s.Id, Pos = Snap(Vector2.Lerp(a, b, t)) };
            }
            return new Anchor { Pos = Snap(p) };
        }

        /// <summary>Turns an anchor into a real node, creating or splitting as needed.</summary>
        private int CommitAnchor(Anchor a)
        {
            var city = Game.City;
            if (a.NodeId != 0 && city.Nodes.ContainsKey(a.NodeId)) return a.NodeId;
            var near = city.FindNode(a.Pos, 0.75f);
            if (near != null) return near.Id;
            if (a.SegId != 0 && city.Segments.ContainsKey(a.SegId)) return city.SplitSegment(a.SegId, a.Pos).Id;
            var s = city.FindSegment(a.Pos, 0.25f, out _);
            if (a.SegId != 0 && s != null) return city.SplitSegment(s.Id, a.Pos).Id;
            return city.AddNode(a.Pos).Id;
        }

        private Vector2 AngleSnapped(Vector2 from, Vector2 to)
        {
            if (!GameInput.Shift) return to;
            var d = to - from;
            float len = d.magnitude;
            if (len < 0.01f) return to;
            float ang = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
            ang = Mathf.Round(ang / 15f) * 15f * Mathf.Deg2Rad;
            return Snap(from + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * len);
        }

        public Vector2 PreviewEnd()
        {
            var end = ResolveAnchor(MouseWorld);
            return end.NodeId != 0 || end.SegId != 0 ? end.Pos : AngleSnapped(start.Pos, end.Pos);
        }

        // ------------------------------------------------------------------ tools

        private void RoadTool()
        {
            if (drawing)
            {
                PreviewLength = Vector2.Distance(start.Pos, PreviewEnd());
                if (GameInput.MouseDown(1)) { drawing = false; return; }
            }

            if (!GameInput.MouseDown(0)) return;
            if (!drawing)
            {
                start = ResolveAnchor(MouseWorld);
                drawing = true;
                return;
            }

            var endAnchor = ResolveAnchor(MouseWorld);
            if (endAnchor.NodeId == 0 && endAnchor.SegId == 0) endAnchor.Pos = AngleSnapped(start.Pos, endAnchor.Pos);
            if (Vector2.Distance(start.Pos, endAnchor.Pos) < 3f) return;

            var city = Game.City;
            int a = CommitAnchor(start);
            int b = CommitAnchor(endAnchor);
            if (a == b) return;

            // Insert junctions wherever the new road crosses an existing one.
            var pa = city.Nodes[a].Pos;
            var pb = city.Nodes[b].Pos;
            var chain = new List<int> { a };
            foreach (var hit in city.FindIntersections(pa, pb, a, b))
            {
                var p = Snap(hit.point);
                var near = city.FindNode(p, 1.5f);
                int id;
                if (near != null) id = near.Id;
                else if (city.Segments.ContainsKey(hit.segId)) id = city.SplitSegment(hit.segId, p).Id;
                else continue;
                if (chain[chain.Count - 1] != id) chain.Add(id);
            }
            if (chain[chain.Count - 1] != b) chain.Add(b);
            for (int i = 0; i + 1 < chain.Count; i++) city.AddSegment(chain[i], chain[i + 1], RoadTypeId);

            // Keep drawing from the end point.
            start = new Anchor { NodeId = b, Pos = city.Nodes[b].Pos };
        }

        private void RoundaboutTool()
        {
            if (!GameInput.MouseDown(0)) return;
            var a = ResolveAnchor(MouseWorld);
            var city = Game.City;
            int id = CommitAnchor(a);
            var n = city.Nodes[id];
            if (n.IsRoundabout && Mathf.Approximately(n.RoundaboutRadius, RoundaboutRadius))
                city.SetRoundabout(id, 0);
            else
                city.SetRoundabout(id, RoundaboutRadius);
        }

        private void CrossingTool()
        {
            if (!GameInput.MouseDown(0)) return;
            var city = Game.City;
            if (city.FindCrossing(MouseWorld, 2.5f, out int sid, out int idx)) { city.ToggleCrossingKind(sid, idx); return; }
            var s = city.FindSegment(MouseWorld, 0.5f, out float t);
            if (s == null) return;
            if (!s.RoadType.HasPavement)
            {
                Game.Toast("Crossings need a road with pavements.");
                return;
            }
            city.AddCrossing(s.Id, t, NewCrossingKind);
        }

        private void SignalsTool()
        {
            if (!GameInput.MouseDown(0)) return;
            var city = Game.City;
            var n = city.FindNode(MouseWorld, HoverRadius * 2f);
            if (n == null || n.IsRoundabout || n.Segments.Count < 3)
            {
                Game.Toast("Click a junction where three or more roads meet.");
                return;
            }
            if (GameInput.Shift)
            {
                if (!n.Signals) city.SetSignals(n.Id, true);
                city.SetYellowBox(n.Id, !n.YellowBox);
            }
            else city.SetSignals(n.Id, !n.Signals);
        }

        // ------------------------------------------------------------------ signs

        public SignPlacement PlaceSign(BuildingTemplate t)
        {
            var sp = SignPlacer.Place(Game.City, t, MouseWorld);
            sp.Rotation = (sp.Rotation + TemplateRotOffset) & 3;
            return sp;
        }

        private void SignTool()
        {
            if (!GameInput.MouseDown(0)) return;
            var t = BuildingLibrary.Get(SignTemplateId);
            if (t == null) return;
            var sp = PlaceSign(t);
            Game.PlaceBuilding(SignTemplateId, sp.Origin, sp.Rotation);
        }

        public int BuildingRotation(BuildingTemplate t, Vector3Int origin)
        {
            int baseRot = 0;
            if (AutoOrient)
            {
                // Face the nearest road.
                var p = new Vector2(origin.x + 0.5f, origin.z + 0.5f);
                float bestD = 40f;
                Vector2? toRoad = null;
                foreach (var s in Game.City.Segments.Values)
                {
                    var a = Game.City.Nodes[s.A].Pos;
                    var b = Game.City.Nodes[s.B].Pos;
                    float d = CityLayer.DistToSegment(p, a, b, out float st);
                    if (d < bestD) { bestD = d; toRoad = Vector2.Lerp(a, b, st) - p; }
                }
                if (toRoad.HasValue)
                {
                    float best = float.MinValue;
                    for (int r = 0; r < 4; r++)
                    {
                        var fd = BuildingTemplate.FrontDirection(r);
                        float dot = fd.x * toRoad.Value.x + fd.y * toRoad.Value.y;
                        if (dot > best) { best = dot; baseRot = r; }
                    }
                }
            }
            return (baseRot + TemplateRotOffset) & 3;
        }

        public Vector3Int BuildingOrigin => new Vector3Int(Mathf.FloorToInt(MouseWorld.x), WorldConst.SurfaceY, Mathf.FloorToInt(MouseWorld.y));

        private void BuildingTool()
        {
            if (!GameInput.MouseDown(0)) return;
            var t = BuildingLibrary.Get(TemplateId);
            if (t == null) return;
            var o = BuildingOrigin;
            Game.PlaceBuilding(TemplateId, o, BuildingRotation(t, o));
        }

        private void BulldozeTool()
        {
            if (!GameInput.MouseDown(0)) return;
            var city = Game.City;
            if (hoverCrossing && city.RemoveCrossingNear(MouseWorld, 2.5f)) return;
            if (hoverNode != 0) { city.RemoveNode(hoverNode); return; }
            if (hoverSegment != 0) { city.RemoveSegment(hoverSegment); return; }
            if (hoverBuilding != 0) Game.RemoveBuilding(hoverBuilding);
        }

        private void SelectTool()
        {
            var city = Game.City;
            if (GameInput.MouseDown(0))
            {
                SelectedNode = SelectedSegment = SelectedBuilding = 0;
                if (hoverNode != 0)
                {
                    SelectedNode = hoverNode;
                    dragNode = hoverNode;
                    dragFrom = city.Nodes[hoverNode].Pos;
                    dragMoved = false;
                }
                else if (hoverSegment != 0) SelectedSegment = hoverSegment;
                else if (hoverBuilding != 0) SelectedBuilding = hoverBuilding;
            }
            if (dragNode != 0)
            {
                if (Vector2.Distance(SnappedMouse, dragFrom) > 0.5f) dragMoved = true;
                if (GameInput.MouseUp(0))
                {
                    if (dragMoved && city.Nodes.ContainsKey(dragNode)) city.MoveNode(dragNode, SnappedMouse);
                    dragNode = 0;
                }
            }
        }

        public void DeleteSelection()
        {
            var city = Game.City;
            if (SelectedNode != 0) city.RemoveNode(SelectedNode);
            else if (SelectedSegment != 0) city.RemoveSegment(SelectedSegment);
            else if (SelectedBuilding != 0) Game.RemoveBuilding(SelectedBuilding);
            SelectedNode = SelectedSegment = SelectedBuilding = 0;
        }

        // ------------------------------------------------------------------ drawing

        public void DrawOverlays(LineBatch lines)
        {
            var city = Game.City;
            float y = OverlayY;
            float w = Mathf.Max(0.35f, OrthoSize * 0.006f);

            if (ShowGrid) DrawGrid(lines, y);

            if (ShowOverlay)
            {
                foreach (var s in city.Segments.Values)
                {
                    var a = city.Nodes[s.A].Pos;
                    var b = city.Nodes[s.B].Pos;
                    var col = s.RoadType.MapColour;
                    bool hl = s.Id == SelectedSegment || (Tool == PlannerTool.Bulldoze && s.Id == hoverSegment && hoverNode == 0);
                    if (hl) col = Tool == PlannerTool.Bulldoze ? new Color(1, 0.2f, 0.2f) : Color.yellow;
                    col.a = hl ? 0.9f : 0.55f;
                    lines.Strip(a, b, hl ? w * 2.5f : w, y, col);
                    foreach (var c in s.Crossings)
                        lines.Disc(city.CrossingPos(s, c.T), w * 3f, y, c.Kind == CrossingKind.Zebra ? new Color(1, 1, 1, 0.8f) : new Color(1, 0.5f, 0.3f, 0.9f), 8);
                }
                foreach (var n in city.Nodes.Values)
                {
                    bool hl = n.Id == SelectedNode || n.Id == hoverNode;
                    var col = n.Segments.Count >= 3 ? new Color(1, 1, 1, 0.9f) : new Color(0.9f, 0.9f, 0.9f, 0.6f);
                    if (hl) col = Tool == PlannerTool.Bulldoze ? new Color(1, 0.2f, 0.2f) : Color.yellow;
                    lines.Disc(n.Pos, (hl ? 2.2f : 1.4f) * w, y, col, 10);
                    if (n.IsRoundabout) lines.Circle(n.Pos, n.RoundaboutRadius, y, new Color(1, 1, 1, 0.6f));
                    if (n.Signals) lines.Circle(n.Pos, 5f, y, new Color(1f, 0.35f, 0.3f, 0.8f));
                }
            }

            // Live preview while dragging a junction.
            if (dragNode != 0 && dragMoved && city.Nodes.TryGetValue(dragNode, out var dn))
            {
                foreach (int sid in dn.Segments)
                {
                    var s = city.Segments[sid];
                    var other = city.Nodes[s.A == dragNode ? s.B : s.A].Pos;
                    var col = s.RoadType.MapColour;
                    col.a = 0.35f;
                    lines.Strip(SnappedMouse, other, s.RoadType.CarriageHalf * 2f, y, col);
                    lines.Strip(SnappedMouse, other, w, y, Color.yellow);
                }
                lines.Disc(SnappedMouse, w * 2.5f, y, Color.yellow, 10);
            }

            // Hovered / selected building outline.
            int bid = Tool == PlannerTool.Select ? SelectedBuilding : 0;
            if (Tool == PlannerTool.Bulldoze || Tool == PlannerTool.Select) { if (hoverBuilding != 0 && hoverNode == 0 && hoverSegment == 0) DrawBuildingOutline(lines, hoverBuilding, y, Tool == PlannerTool.Bulldoze ? Color.red : new Color(1, 1, 0.4f, 0.8f)); }
            if (bid != 0) DrawBuildingOutline(lines, bid, y, Color.yellow);

            switch (Tool)
            {
                case PlannerTool.Road:
                {
                    var type = RoadTypes.Get(RoadTypeId);
                    var col = type.MapColour;
                    if (drawing)
                    {
                        var end = PreviewEnd();
                        col.a = 0.3f;
                        lines.Strip(start.Pos, end, type.MaxHalf * 2f, y, col);
                        col.a = 0.5f;
                        lines.Strip(start.Pos, end, type.CarriageHalf * 2f, y, col);
                        lines.Strip(start.Pos, end, w, y, Color.white);
                        lines.Disc(start.Pos, w * 2, y, Color.white, 10);
                    }
                    var cursor = ResolveAnchor(MouseWorld);
                    lines.Disc(cursor.Pos, w * 2, y, cursor.NodeId != 0 || cursor.SegId != 0 ? Color.cyan : Color.white, 10);
                    break;
                }
                case PlannerTool.Roundabout:
                {
                    var a = ResolveAnchor(MouseWorld);
                    lines.Circle(a.Pos, RoundaboutRadius, y, Color.cyan);
                    lines.Circle(a.Pos, RoundaboutRadius + 3, y, new Color(0, 1, 1, 0.4f));
                    break;
                }
                case PlannerTool.Crossing:
                {
                    var s = city.FindSegment(MouseWorld, 0.5f, out float t);
                    if (s != null)
                    {
                        var p = Vector2.Lerp(city.Nodes[s.A].Pos, city.Nodes[s.B].Pos, t);
                        lines.Disc(p, 1.5f, y, hoverCrossing ? new Color(1, 0.3f, 0.3f, 0.7f) : new Color(1, 1, 1, 0.7f), 12);
                    }
                    break;
                }
                case PlannerTool.Sign:
                {
                    var t = BuildingLibrary.Get(SignTemplateId);
                    if (t == null) break;
                    var sp = PlaceSign(t);
                    var fp = t.Footprint(sp.Origin, sp.Rotation);
                    lines.Rect(fp, y, Color.cyan);
                    var fd = BuildingTemplate.FrontDirection(sp.Rotation);
                    var c = new Vector2((fp.xMin + fp.xMax) * 0.5f, (fp.yMin + fp.yMax) * 0.5f);
                    lines.Strip(c, c + new Vector2(fd.x, fd.y) * 4f, w, y, Color.yellow);
                    Game.Interactor.DrawGhost(t, sp.Origin, sp.Rotation, Cam, true);
                    break;
                }
                case PlannerTool.Signals:
                {
                    var n = city.FindNode(MouseWorld, HoverRadius * 2f);
                    if (n != null && n.Segments.Count >= 3 && !n.IsRoundabout)
                        lines.Circle(n.Pos, 6f, y, n.Signals ? new Color(1, 0.3f, 0.3f) : new Color(0.3f, 1, 0.4f));
                    break;
                }
                case PlannerTool.Building:
                {
                    var t = BuildingLibrary.Get(TemplateId);
                    if (t == null) break;
                    var o = BuildingOrigin;
                    int rot = BuildingRotation(t, o);
                    var fp = t.Footprint(o, rot);
                    lines.Rect(fp, y, Color.green);
                    var fd = BuildingTemplate.FrontDirection(rot);
                    var c = new Vector2((fp.xMin + fp.xMax) * 0.5f, (fp.yMin + fp.yMax) * 0.5f);
                    lines.Strip(c, c + new Vector2(fd.x, fd.y) * (Mathf.Max(fp.width, fp.height) * 0.5f + 2f), w, y, Color.yellow);
                    Game.Interactor.DrawGhost(t, o, rot, Cam, true);
                    break;
                }
            }
        }

        private void DrawBuildingOutline(LineBatch lines, int id, float y, Color c)
        {
            if (!Game.City.Buildings.TryGetValue(id, out var p) || p.Template == null) return;
            lines.Rect(p.Template.Footprint(p.Origin, p.Rotation), y, c);
        }

        private void DrawGrid(LineBatch lines, float y)
        {
            var pos = Cam.transform.position;
            float half = OrthoSize * Mathf.Max(1f, Cam.aspect) + 16f;
            int x0 = Mathf.FloorToInt((pos.x - half) / 16f) * 16, x1 = Mathf.CeilToInt((pos.x + half) / 16f) * 16;
            int z0 = Mathf.FloorToInt((pos.z - OrthoSize - 16f) / 16f) * 16, z1 = Mathf.CeilToInt((pos.z + OrthoSize + 16f) / 16f) * 16;
            var col = new Color(1, 1, 1, 0.12f);
            for (int x = x0; x <= x1; x += 16) lines.Line(new Vector3(x, y, z0), new Vector3(x, y, z1), col);
            for (int z = z0; z <= z1; z += 16) lines.Line(new Vector3(x0, y, z), new Vector3(x1, y, z), col);
        }
    }
}
