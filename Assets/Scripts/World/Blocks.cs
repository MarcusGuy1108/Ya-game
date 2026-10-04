using System.Collections.Generic;
using UnityEngine;
using UKCity.World.Art;

namespace UKCity.World
{
    public enum BlockShape : byte { None, Cube, Model }

    /// <summary>Which material/submesh the block renders with.</summary>
    public enum RenderLayer : byte { Opaque, Cutout, Transparent }

    /// <summary>Faces whose texture changes over time (drawn by DynamicFaces on top of the static mesh).</summary>
    public enum AnimKind : byte { None, VehicleSignal, PedSignal, Belisha, WigWag, Vms, LaneSignal, Vas }

    public static class BlockIds
    {
        public const ushort Air = 0;
        // Nature
        public const ushort Grass = 1, Dirt = 2, Stone = 3, Bedrock = 4;
        // Road surfaces (6-9 are legacy axis-specific markings, kept for old saves)
        public const ushort Asphalt = 5, LineX = 6, LineZ = 7, YellowX = 8, YellowZ = 9;
        public const ushort Kerb = 10, Pavement = 11, RoadPaint = 12, Gravel = 13;
        // Building materials
        public const ushort RedBrick = 14, YellowBrick = 15, Render = 16, Pebbledash = 17, Slate = 18, RoofTile = 19;
        public const ushort Concrete = 20, Planks = 21, Plaster = 22, FloorTile = 23, Carpet = 24, Glass = 25, Window = 26;
        public const ushort DoorRed = 27, DoorBlue = 28, DoorGreen = 29, DoorBlack = 30, Shopfront = 31, ShopSign = 32;
        // Street furniture
        public const ushort PhoneBox = 33, PhoneBoxTop = 34, PostBox = 35, MetalGrey = 36, MetalBlack = 37;
        public const ushort PoleGrey = 38, PoleBlack = 39, PoleStriped = 40, LampHead = 41, TrafficLight = 42, Belisha = 43;
        public const ushort SignGiveWay = 44, Sign30 = 45, SignNoEntry = 46, SignBusStop = 47, SignStreet = 48;
        // More nature
        public const ushort Hedge = 50, Leaves = 51, Log = 52, Water = 53, Sand = 54;

        // Surfaces
        public const ushort FootwayTarmac = 60, TactileRed = 61, TactileBuff = 62, RedTarmac = 63, Setts = 64, BlockPaving = 66;
        // Rotatable road markings (texture drawn along the road for facing 0 = road running north-south)
        public const ushort MarkLine = 70, MarkDoubleYellow = 71, MarkSingleYellow = 72, MarkGiveWay = 73, MarkGiveWaySingle = 74;
        public const ushort MarkStop = 75, MarkZigzag = 76, MarkZigzagYellow = 77, MarkStuds = 78, MarkBox = 79, MarkTriangle = 80;
        public const ushort MarkArrowAhead = 81, MarkArrowLeft = 82, MarkArrowRight = 83, MarkHatch = 84, MarkThickLine = 85;
        // Building extras
        public const ushort DoorRedTop = 90, DoorBlueTop = 91, DoorGreenTop = 92, DoorBlackTop = 93;
        public const ushort Sandstone = 94, BlueBrick = 95, FeltRoof = 96, OfficeGlass = 97, WhiteTiles = 98;
        public const ushort Corrugated = 99, TimberCladding = 100, Stucco = 101, Wallpaper = 102, GreyRender = 103, PubFascia = 104, PubBoard = 105;
        // Props
        public const ushort PedSignal = 110, PushButton = 111, SpeedCamera = 112, AvgSpeedCamera = 113, Cctv = 114;
        public const ushort Bollard = 115, KeepLeftBollard = 116, GuardRail = 117, Armco = 118, ConcreteBarrier = 119;
        public const ushort LitterBin = 120, GritBin = 121, Cabinet = 122, TrafficCone = 123, RoadworksBarrier = 124;
        public const ushort Bench = 125, CycleStand = 126, SosPhone = 127, MarkerPost = 128, EvCharger = 129;
        public const ushort GantryTruss = 130, GantryLeg = 131, LaneSignal = 132, WigWag = 133, Vas = 134, Railings = 135, LampLed = 136;

        /// <summary>First id used by the generated sign catalogue.</summary>
        public const int FirstSign = 300;

        /// <summary>Returned by world queries for chunks that are not loaded yet. Treated as solid.</summary>
        public const ushort Unloaded = 4094;
    }

    /// <summary>An axis-aligned box of a block model, in block-local space for facing 0 (front = -Z).</summary>
    public sealed class Box
    {
        public Vector3 Min, Max;
        /// <summary>Tile per local face (+X, -X, +Y, -Y, +Z, -Z); -1 = no face.</summary>
        public int[] Tiles = new int[6];
        /// <summary>Stretch the whole texture over each face instead of using the face's position in the cell.</summary>
        public bool FullUv;
        public bool Collide = true;

        public Box(Vector3 min, Vector3 max, int tile)
        {
            Min = min; Max = max;
            for (int i = 0; i < 6; i++) Tiles[i] = tile;
        }

        public Box Face(int face, int tile) { Tiles[face] = tile; return this; }
        public Box Front(int tile) => Face(5, tile);
        public Box Back(int tile) => Face(4, tile);
        public Box Top(int tile) => Face(2, tile);
        public Box Bottom(int tile) => Face(3, tile);
        public Box Sides(int tile) { Tiles[0] = Tiles[1] = Tiles[4] = Tiles[5] = tile; return this; }
        public Box Full() { FullUv = true; return this; }
        public Box NoCollide() { Collide = false; return this; }
    }

    public sealed class BlockDef
    {
        public int Id;
        public string Name;
        public string Category;
        public BlockShape Shape = BlockShape.Cube;
        public RenderLayer Layer = RenderLayer.Opaque;
        /// <summary>Blocks movement. Doors are deliberately passable until they can open.</summary>
        public bool Solid = true;
        /// <summary>Shown in the block palette.</summary>
        public bool Placeable = true;
        /// <summary>Has a facing (models, road markings). Placed facing the player.</summary>
        public bool Rotatable;
        /// <summary>Cube tiles per local face (+X, -X, +Y, -Y, +Z, -Z).</summary>
        public int[] Tiles = new int[6];
        public Box[] Model;
        public int IconTile;
        /// <summary>Legacy axis-swap partner for 90 degree rotation (LineX &lt;-&gt; LineZ).</summary>
        public int Rotated90;

        public AnimKind Anim;
        public int AnimBox;
        public int[] AnimFaces = { 5 };
        /// <summary>Animation frames (tiles); meaning depends on Anim.</summary>
        public int[] Frames;

        public bool Occludes => Shape == BlockShape.Cube && Layer == RenderLayer.Opaque;
        public bool CastsAO => Shape == BlockShape.Cube && Layer != RenderLayer.Transparent;
        public bool Visible => Shape != BlockShape.None;
        public bool Selectable => Shape != BlockShape.None && Id != BlockIds.Water;

        public int TileTop => Tiles[2];
        public int TileSide => Tiles[5];
    }

    /// <summary>Static registry of every block in the game. Read-only after static init, so safe from worker threads.</summary>
    public static class Blocks
    {
        public static readonly BlockDef[] Defs = new BlockDef[4096];
        public static readonly List<BlockDef> All = new List<BlockDef>();

        public static readonly string[] Categories = { "Nature", "Road", "Markings", "Building", "Street", "Signals", "Signs" };

        static Blocks()
        {
            Defs[0] = new BlockDef { Id = 0, Name = "Air", Category = "", Shape = BlockShape.None, Solid = false, Placeable = false };
            RegisterNature();
            RegisterRoad();
            RegisterBuilding();
            RegisterStreet();
            Signs.RegisterBlocks();

            for (int i = 0; i < Defs.Length; i++)
            {
                var d = Defs[i];
                if (d == null) continue;
                if (d.Rotated90 == 0) d.Rotated90 = i;
                if (d.Visible) All.Add(d);
            }
        }

        public static BlockDef Get(ushort state) => Defs[state & BlockState.IdMask] ?? Defs[0];
        public static BlockDef Get(int id) => Defs[id & BlockState.IdMask] ?? Defs[0];

        public static int Tile(string name) => TextureAtlas.Register(name);

        public static bool IsSolidForPhysics(ushort state)
        {
            if (state == BlockIds.Unloaded) return true;
            var d = Defs[state & BlockState.IdMask];
            return d != null && d.Solid && d.Shape != BlockShape.None;
        }

        /// <summary>Collision boxes in world space for a block at (x,y,z).</summary>
        public static void CollisionBoxes(ushort state, int x, int y, int z, List<Bounds> into)
        {
            var origin = new Vector3(x, y, z);
            if (state == BlockIds.Unloaded) { into.Add(new Bounds(origin + Vector3.one * 0.5f, Vector3.one)); return; }
            var d = Get(state);
            if (d.Shape == BlockShape.Cube) { into.Add(new Bounds(origin + Vector3.one * 0.5f, Vector3.one)); return; }
            if (d.Model == null) return;
            int f = BlockState.Facing(state);
            foreach (var b in d.Model)
            {
                if (!b.Collide) continue;
                var a = BlockState.RotatePoint(b.Min, f);
                var c = BlockState.RotatePoint(b.Max, f);
                var mn = Vector3.Min(a, c);
                var mx = Vector3.Max(a, c);
                // Keep collision inside the cell so overhanging arms don't trap the player.
                mn = Vector3.Max(mn, Vector3.zero);
                mx = Vector3.Min(mx, Vector3.one);
                if (mx.x <= mn.x || mx.y <= mn.y || mx.z <= mn.z) continue;
                var bounds = new Bounds();
                bounds.SetMinMax(origin + mn, origin + mx);
                into.Add(bounds);
            }
        }

        /// <summary>Bounding box of a block's visible shape (for the selection outline).</summary>
        public static Bounds ShapeBounds(ushort state)
        {
            var d = Get(state);
            if (d.Shape != BlockShape.Model || d.Model == null) return new Bounds(Vector3.one * 0.5f, Vector3.one);
            int f = BlockState.Facing(state);
            var mn = Vector3.one * 9f;
            var mx = Vector3.one * -9f;
            foreach (var b in d.Model)
            {
                var a = BlockState.RotatePoint(b.Min, f);
                var c = BlockState.RotatePoint(b.Max, f);
                mn = Vector3.Min(mn, Vector3.Min(a, c));
                mx = Vector3.Max(mx, Vector3.Max(a, c));
            }
            var bb = new Bounds();
            bb.SetMinMax(Vector3.Max(mn, Vector3.zero), Vector3.Min(mx, Vector3.one));
            return bb;
        }

        // ------------------------------------------------------------------ registration helpers

        public static BlockDef Cube(int id, string name, string cat, string top, string bottom, string side)
        {
            var d = new BlockDef { Id = id, Name = name, Category = cat };
            int t = Tile(top), b = Tile(bottom), s = Tile(side);
            d.Tiles = new[] { s, s, t, b, s, s };
            d.IconTile = s;
            Defs[id] = d;
            return d;
        }

        public static BlockDef Cube(int id, string name, string cat, string all) => Cube(id, name, cat, all, all, all);

        public static BlockDef ModelBlock(int id, string name, string cat, int icon, params Box[] boxes)
        {
            var d = new BlockDef
            {
                Id = id, Name = name, Category = cat, Shape = BlockShape.Model, Model = boxes,
                Rotatable = true, IconTile = icon, Layer = RenderLayer.Cutout
            };
            Defs[id] = d;
            return d;
        }

        private static Vector3 V(float x, float y, float z) => new Vector3(x, y, z);

        /// <summary>A thin post behind the cell centre, used under signs and signal heads.</summary>
        public static Box PostBehind(int tile, float z = 0.44f) => new Box(V(0.44f, 0, z), V(0.56f, 1, z + 0.12f), tile);

        private static void Pair(int a, int b)
        {
            Defs[a].Rotated90 = b;
            Defs[b].Rotated90 = a;
        }

        // ------------------------------------------------------------------ blocks

        private static void RegisterNature()
        {
            Cube(BlockIds.Grass, "Grass", "Nature", "grass_top", "dirt", "grass_side");
            Cube(BlockIds.Dirt, "Dirt", "Nature", "dirt");
            Cube(BlockIds.Stone, "Stone", "Nature", "stone");
            Cube(BlockIds.Bedrock, "Bedrock", "Nature", "bedrock").Placeable = false;
            Cube(BlockIds.Sand, "Sand", "Nature", "sand");
            Cube(BlockIds.Gravel, "Gravel", "Nature", "gravel");
            Cube(BlockIds.Log, "Oak Log", "Nature", "log_top", "log_top", "log_side");
            Cube(BlockIds.Leaves, "Oak Leaves", "Nature", "leaves").Layer = RenderLayer.Cutout;
            Cube(BlockIds.Hedge, "Privet Hedge", "Nature", "hedge").Layer = RenderLayer.Cutout;
            var water = Cube(BlockIds.Water, "Water", "Nature", "water");
            water.Layer = RenderLayer.Transparent;
            water.Solid = false;
        }

        private static void RegisterRoad()
        {
            Cube(BlockIds.Asphalt, "Tarmac", "Road", "asphalt");
            Cube(BlockIds.LineX, "White Line (E-W)", "Road", "mark_line_x", "asphalt", "asphalt").Placeable = false;
            Cube(BlockIds.LineZ, "White Line (N-S)", "Road", "mark_line", "asphalt", "asphalt").Placeable = false;
            Cube(BlockIds.YellowX, "Double Yellow (E-W)", "Road", "mark_double_yellow_x", "asphalt", "asphalt").Placeable = false;
            Cube(BlockIds.YellowZ, "Double Yellow (N-S)", "Road", "mark_double_yellow", "asphalt", "asphalt").Placeable = false;
            Pair(BlockIds.LineX, BlockIds.LineZ);
            Pair(BlockIds.YellowX, BlockIds.YellowZ);
            Cube(BlockIds.RoadPaint, "White Road Paint", "Markings", "road_paint", "asphalt", "asphalt");
            Cube(BlockIds.Kerb, "Kerb", "Road", "kerb_top", "concrete", "kerb_side");
            Cube(BlockIds.Pavement, "Paving Slabs", "Road", "pavement", "concrete", "kerb_side");
            Cube(BlockIds.FootwayTarmac, "Footway Tarmac", "Road", "footway", "asphalt", "asphalt");
            Cube(BlockIds.TactileRed, "Tactile Paving (Red)", "Road", "tactile_red", "concrete", "kerb_side");
            Cube(BlockIds.TactileBuff, "Tactile Paving (Buff)", "Road", "tactile_buff", "concrete", "kerb_side");
            Cube(BlockIds.RedTarmac, "Red Tarmac (Bus Lane)", "Road", "red_tarmac", "asphalt", "asphalt");
            Cube(BlockIds.Setts, "Granite Setts", "Road", "setts");
            Cube(BlockIds.BlockPaving, "Block Paving", "Road", "block_paving");

            Mark(BlockIds.MarkLine, "Line", "mark_line");
            Mark(BlockIds.MarkThickLine, "Thick Line", "mark_thick_line");
            Mark(BlockIds.MarkDoubleYellow, "Double Yellow Lines", "mark_double_yellow");
            Mark(BlockIds.MarkSingleYellow, "Single Yellow Line", "mark_single_yellow");
            Mark(BlockIds.MarkGiveWay, "Give Way Line (double)", "mark_giveway");
            Mark(BlockIds.MarkGiveWaySingle, "Give Way Line (roundabout)", "mark_giveway_single");
            Mark(BlockIds.MarkStop, "Stop Line", "mark_stop");
            Mark(BlockIds.MarkZigzag, "Zig-zag (white)", "mark_zigzag");
            Mark(BlockIds.MarkZigzagYellow, "Zig-zag (School Keep Clear)", "mark_zigzag_yellow");
            Mark(BlockIds.MarkStuds, "Crossing Studs", "mark_studs");
            Mark(BlockIds.MarkBox, "Yellow Box", "mark_box");
            Mark(BlockIds.MarkTriangle, "Give Way Triangle", "mark_triangle");
            Mark(BlockIds.MarkArrowAhead, "Arrow Ahead", "mark_arrow_ahead");
            Mark(BlockIds.MarkArrowLeft, "Arrow Left", "mark_arrow_left");
            Mark(BlockIds.MarkArrowRight, "Arrow Right", "mark_arrow_right");
            Mark(BlockIds.MarkHatch, "Hatching", "mark_hatch");
        }

        private static void Mark(int id, string name, string tile)
        {
            var d = Cube(id, name, "Markings", tile, "asphalt", "asphalt");
            d.Rotatable = true;
            d.IconTile = d.Tiles[2];
        }

        private static void RegisterBuilding()
        {
            Cube(BlockIds.RedBrick, "Red Brick", "Building", "red_brick");
            Cube(BlockIds.YellowBrick, "London Stock Brick", "Building", "yellow_brick");
            Cube(BlockIds.BlueBrick, "Engineering Brick", "Building", "blue_brick");
            Cube(BlockIds.Sandstone, "Sandstone", "Building", "sandstone");
            Cube(BlockIds.Render, "Cream Render", "Building", "render");
            Cube(BlockIds.GreyRender, "Grey Render", "Building", "grey_render");
            Cube(BlockIds.Stucco, "White Stucco", "Building", "stucco");
            Cube(BlockIds.Pebbledash, "Pebbledash", "Building", "pebbledash");
            Cube(BlockIds.TimberCladding, "Timber Cladding", "Building", "timber_cladding");
            Cube(BlockIds.Corrugated, "Corrugated Metal", "Building", "corrugated");
            Cube(BlockIds.Slate, "Slate Roof", "Building", "slate");
            Cube(BlockIds.RoofTile, "Clay Roof Tiles", "Building", "roof_tile");
            Cube(BlockIds.FeltRoof, "Felt Flat Roof", "Building", "felt_roof", "concrete", "concrete");
            Cube(BlockIds.Concrete, "Concrete", "Building", "concrete");
            Cube(BlockIds.Planks, "Floorboards", "Building", "planks");
            Cube(BlockIds.Plaster, "Plaster", "Building", "plaster");
            Cube(BlockIds.Wallpaper, "Wallpaper", "Building", "wallpaper");
            Cube(BlockIds.FloorTile, "Hallway Tiles", "Building", "floor_tile");
            Cube(BlockIds.WhiteTiles, "White Tiles", "Building", "white_tiles");
            Cube(BlockIds.Carpet, "Carpet", "Building", "carpet");
            Cube(BlockIds.Glass, "Glass", "Building", "glass").Layer = RenderLayer.Transparent;
            Cube(BlockIds.Window, "Sash Window", "Building", "window").Layer = RenderLayer.Transparent;
            Cube(BlockIds.OfficeGlass, "Office Glazing", "Building", "office_glass").Layer = RenderLayer.Transparent;
            Door(BlockIds.DoorRed, BlockIds.DoorRedTop, "Red", "door_red");
            Door(BlockIds.DoorBlue, BlockIds.DoorBlueTop, "Blue", "door_blue");
            Door(BlockIds.DoorGreen, BlockIds.DoorGreenTop, "Green", "door_green");
            Door(BlockIds.DoorBlack, BlockIds.DoorBlackTop, "Black", "door_black");
            Cube(BlockIds.Shopfront, "Shopfront Panel", "Building", "shopfront");
            Cube(BlockIds.ShopSign, "Shop Fascia", "Building", "shop_sign");
            Cube(BlockIds.PubFascia, "Pub Fascia", "Building", "pub_fascia");
            Cube(BlockIds.PubBoard, "Pub Sign Board", "Building", "metal_black", "metal_black", "pub_board");
        }

        private static void Door(int bottom, int top, string colour, string tile)
        {
            var b = Cube(bottom, $"Front Door ({colour})", "Building", "planks", "planks", tile + "_bottom");
            b.Solid = false;
            var t = Cube(top, $"Front Door Top ({colour})", "Building", "planks", "planks", tile + "_top");
            t.Solid = false;
            t.Placeable = false;
        }

        private static void RegisterStreet()
        {
            Cube(BlockIds.PhoneBox, "Phone Box", "Street", "phonebox_top", "phonebox_top", "phonebox");
            Cube(BlockIds.PhoneBoxTop, "Phone Box Crown", "Street", "phonebox_top", "phonebox_top", "phonebox_crown");
            int post = Tile("postbox"), postTop = Tile("postbox_top");
            ModelBlock(BlockIds.PostBox, "Pillar Box", "Street", post,
                new Box(V(0.22f, 0, 0.22f), V(0.78f, 0.86f, 0.78f), post).Top(postTop).Full(),
                new Box(V(0.18f, 0.86f, 0.18f), V(0.82f, 1f, 0.82f), postTop));
            Cube(BlockIds.MetalGrey, "Grey Metal", "Street", "metal_grey");
            Cube(BlockIds.MetalBlack, "Black Metal", "Street", "metal_black");

            Pole(BlockIds.PoleGrey, "Grey Pole", "pole_grey");
            Pole(BlockIds.PoleBlack, "Black Pole", "metal_black");
            Pole(BlockIds.PoleStriped, "Belisha Pole", "pole_striped");

            int grey = Tile("metal_grey"), black = Tile("metal_black"), dark = Tile("metal_dark");

            // Street lamp: lantern with an arm reaching back into the column cell behind it.
            int lens = Tile("lamp_lens");
            ModelBlock(BlockIds.LampHead, "Street Lamp Head", "Street", lens,
                new Box(V(0.2f, 0.62f, 0.05f), V(0.8f, 0.8f, 0.7f), grey).Bottom(lens),
                new Box(V(0.45f, 0.72f, 0.6f), V(0.55f, 0.8f, 1.5f), grey).NoCollide());
            int led = Tile("led_lens");
            ModelBlock(BlockIds.LampLed, "LED Street Lamp Head", "Street", led,
                new Box(V(0.15f, 0.76f, 0.0f), V(0.85f, 0.84f, 0.75f), dark).Bottom(led),
                new Box(V(0.45f, 0.76f, 0.7f), V(0.55f, 0.83f, 1.5f), grey).NoCollide());

            // Vehicle signal head on a backing board with the UK white border.
            int board = Tile("signal_board");
            var tl = ModelBlock(BlockIds.TrafficLight, "Traffic Signal Head", "Signals", Tile("signal_green"),
                PostBehind(grey),
                new Box(V(0.2f, 0.02f, 0.56f), V(0.8f, 0.98f, 0.6f), black).Front(board).Full(),
                new Box(V(0.32f, 0.06f, 0.32f), V(0.68f, 0.94f, 0.56f), black).Front(Tile("signal_off")).Full());
            Animate(tl, AnimKind.VehicleSignal, 2, "signal_red", "signal_redamber", "signal_green", "signal_amber");

            var ped = ModelBlock(BlockIds.PedSignal, "Pedestrian Signal", "Signals", Tile("ped_green"),
                PostBehind(grey),
                new Box(V(0.28f, 0.22f, 0.36f), V(0.72f, 0.88f, 0.62f), black).Front(Tile("ped_off")).Full());
            Animate(ped, AnimKind.PedSignal, 1, "ped_red", "ped_green");

            ModelBlock(BlockIds.PushButton, "Crossing Push Button", "Signals", Tile("push_button"),
                PostBehind(grey),
                new Box(V(0.3f, 0.3f, 0.36f), V(0.7f, 0.72f, 0.56f), Tile("yellow_box")).Front(Tile("push_button")).Full());

            var bel = ModelBlock(BlockIds.Belisha, "Belisha Beacon", "Signals", Tile("belisha_on"),
                new Box(V(0.22f, 0.0f, 0.22f), V(0.78f, 0.56f, 0.78f), Tile("belisha_off")).Full());
            Animate(bel, AnimKind.Belisha, 0, "belisha_on");
            bel.AnimFaces = new[] { 0, 1, 2, 4, 5 };
            bel.Rotatable = false;

            var wig = ModelBlock(BlockIds.WigWag, "School Flashing Lights", "Signals", Tile("wigwag_l"),
                PostBehind(grey),
                new Box(V(0.06f, 0.25f, 0.3f), V(0.94f, 0.75f, 0.42f), black).Front(Tile("wigwag_off")).Full());
            Animate(wig, AnimKind.WigWag, 1, "wigwag_l", "wigwag_r");

            var vas = ModelBlock(BlockIds.Vas, "Vehicle Activated Sign (30)", "Signals", Tile("vas_30"),
                PostBehind(grey),
                new Box(V(0.04f, 0.04f, 0.3f), V(0.96f, 0.96f, 0.42f), black).Front(Tile("vas_off")).Full());
            Animate(vas, AnimKind.Vas, 1, "vas_30", "vas_slow");

            var lane = ModelBlock(BlockIds.LaneSignal, "Motorway Lane Signal", "Signals", Tile("lanesig_60"),
                new Box(V(0.45f, 0.86f, 0.45f), V(0.55f, 1f, 0.55f), grey).NoCollide(),
                new Box(V(0.1f, 0.08f, 0.42f), V(0.9f, 0.88f, 0.58f), black).Front(Tile("lanesig_off")).Full());
            Animate(lane, AnimKind.LaneSignal, 1, "lanesig_off", "lanesig_60", "lanesig_50", "lanesig_40", "lanesig_x", "lanesig_arrow");

            int yellow = Tile("yellow_box");
            ModelBlock(BlockIds.SpeedCamera, "Speed Camera", "Street", Tile("gatso_front"),
                new Box(V(0.18f, 0.08f, 0.12f), V(0.82f, 0.92f, 0.88f), yellow).Front(Tile("gatso_front")).Back(Tile("gatso_back")).Full());
            ModelBlock(BlockIds.AvgSpeedCamera, "Average Speed Camera", "Street", Tile("specs_front"),
                new Box(V(0.3f, 0.34f, 0.05f), V(0.7f, 0.66f, 0.7f), yellow).Front(Tile("specs_front")).Full(),
                new Box(V(0.45f, 0.42f, 0.65f), V(0.55f, 0.52f, 1.5f), grey).NoCollide());
            ModelBlock(BlockIds.Cctv, "CCTV Camera", "Street", Tile("cctv_front"),
                new Box(V(0.36f, 0.38f, 0.0f), V(0.64f, 0.6f, 0.62f), Tile("cctv_body")).Front(Tile("cctv_front")).Full(),
                new Box(V(0.45f, 0.6f, 0.4f), V(0.55f, 0.68f, 1.5f), grey).NoCollide());

            ModelBlock(BlockIds.Bollard, "Bollard", "Street", Tile("bollard"),
                new Box(V(0.36f, 0, 0.36f), V(0.64f, 0.9f, 0.64f), Tile("bollard")).Top(black).Full()).Rotatable = false;
            ModelBlock(BlockIds.KeepLeftBollard, "Keep Left Bollard", "Street", Tile("keepleft_bollard"),
                new Box(V(0.26f, 0, 0.3f), V(0.74f, 0.95f, 0.7f), Tile("bollard_white")).Front(Tile("keepleft_bollard")).Full());
            ModelBlock(BlockIds.GuardRail, "Pedestrian Guard Rail", "Street", Tile("guardrail"),
                new Box(V(0, 0.02f, 0.48f), V(1, 1, 0.52f), -1).Front(Tile("guardrail")).Back(Tile("guardrail")).Full());
            ModelBlock(BlockIds.Railings, "Iron Railings", "Street", Tile("railings"),
                new Box(V(0, 0, 0.48f), V(1, 1, 0.52f), -1).Front(Tile("railings")).Back(Tile("railings")).Full());
            int armco = Tile("armco");
            ModelBlock(BlockIds.Armco, "Crash Barrier (Armco)", "Street", armco,
                new Box(V(0, 0.42f, 0.32f), V(1, 0.78f, 0.42f), armco).Full(),
                new Box(V(0.44f, 0, 0.42f), V(0.56f, 0.72f, 0.54f), grey));
            int cb = Tile("concrete_barrier");
            ModelBlock(BlockIds.ConcreteBarrier, "Concrete Barrier", "Street", cb,
                new Box(V(0, 0, 0.2f), V(1, 0.35f, 0.8f), cb),
                new Box(V(0, 0.35f, 0.32f), V(1, 0.95f, 0.68f), cb));
            ModelBlock(BlockIds.LitterBin, "Litter Bin", "Street", Tile("bin_side"),
                new Box(V(0.24f, 0, 0.24f), V(0.76f, 0.92f, 0.76f), Tile("bin_side")).Top(Tile("bin_top")).Full());
            ModelBlock(BlockIds.GritBin, "Grit Bin", "Street", Tile("gritbin_side"),
                new Box(V(0.12f, 0, 0.2f), V(0.88f, 0.72f, 0.8f), Tile("gritbin_side")).Top(Tile("gritbin_top")).Full());
            ModelBlock(BlockIds.Cabinet, "Street Cabinet", "Street", Tile("cabinet_front"),
                new Box(V(0.06f, 0, 0.3f), V(0.94f, 0.96f, 0.7f), Tile("cabinet_side")).Front(Tile("cabinet_front")).Full());
            int cone = Tile("cone");
            ModelBlock(BlockIds.TrafficCone, "Traffic Cone", "Street", cone,
                new Box(V(0.18f, 0, 0.18f), V(0.82f, 0.06f, 0.82f), black),
                new Box(V(0.32f, 0.06f, 0.32f), V(0.68f, 0.42f, 0.68f), cone).Full(),
                new Box(V(0.4f, 0.42f, 0.4f), V(0.6f, 0.78f, 0.6f), cone).Full()).Rotatable = false;
            int rw = Tile("barrier_redwhite");
            ModelBlock(BlockIds.RoadworksBarrier, "Roadworks Barrier", "Street", rw,
                new Box(V(0, 0.4f, 0.47f), V(1, 0.78f, 0.53f), rw).Full(),
                new Box(V(0.05f, 0, 0.3f), V(0.15f, 0.06f, 0.7f), black),
                new Box(V(0.85f, 0, 0.3f), V(0.95f, 0.06f, 0.7f), black),
                new Box(V(0.08f, 0.06f, 0.47f), V(0.12f, 0.4f, 0.53f), grey),
                new Box(V(0.88f, 0.06f, 0.47f), V(0.92f, 0.4f, 0.53f), grey));
            int planks = Tile("bench_wood");
            ModelBlock(BlockIds.Bench, "Bench", "Street", planks,
                new Box(V(0.04f, 0.4f, 0.28f), V(0.96f, 0.47f, 0.72f), planks),
                new Box(V(0.04f, 0.47f, 0.66f), V(0.96f, 0.92f, 0.72f), planks),
                new Box(V(0.1f, 0, 0.32f), V(0.18f, 0.4f, 0.68f), black),
                new Box(V(0.82f, 0, 0.32f), V(0.9f, 0.4f, 0.68f), black));
            ModelBlock(BlockIds.CycleStand, "Cycle Stand", "Street", grey,
                new Box(V(0.14f, 0, 0.47f), V(0.22f, 0.82f, 0.53f), grey),
                new Box(V(0.78f, 0, 0.47f), V(0.86f, 0.82f, 0.53f), grey),
                new Box(V(0.14f, 0.74f, 0.47f), V(0.86f, 0.82f, 0.53f), grey));
            ModelBlock(BlockIds.SosPhone, "Emergency Phone", "Street", Tile("sos_front"),
                new Box(V(0.24f, 0, 0.3f), V(0.76f, 1f, 0.7f), Tile("sos_side")).Front(Tile("sos_front")).Full());
            ModelBlock(BlockIds.MarkerPost, "Marker Post", "Street", Tile("marker_post"),
                new Box(V(0.42f, 0, 0.44f), V(0.58f, 0.95f, 0.56f), Tile("marker_post")).Full());
            ModelBlock(BlockIds.EvCharger, "EV Charger", "Street", Tile("ev_front"),
                new Box(V(0.26f, 0, 0.34f), V(0.74f, 0.96f, 0.66f), Tile("ev_side")).Front(Tile("ev_front")).Full());

            var truss = Cube(BlockIds.GantryTruss, "Gantry Truss", "Signals", "gantry_truss");
            truss.Layer = RenderLayer.Cutout;
            ModelBlock(BlockIds.GantryLeg, "Gantry Leg", "Signals", Tile("gantry_leg"),
                new Box(V(0.28f, 0, 0.28f), V(0.72f, 1, 0.72f), Tile("gantry_leg"))).Rotatable = false;
        }

        private static void Pole(int id, string name, string tile)
        {
            int t = Tile(tile);
            var d = ModelBlock(id, name, "Street", t, new Box(V(0.42f, 0, 0.42f), V(0.58f, 1, 0.58f), t));
            d.Rotatable = false;
            d.Layer = RenderLayer.Opaque;
        }

        public static void Animate(BlockDef d, AnimKind kind, int box, params string[] frames)
        {
            d.Anim = kind;
            d.AnimBox = box;
            d.Frames = new int[frames.Length];
            for (int i = 0; i < frames.Length; i++) d.Frames[i] = Tile(frames[i]);
        }
    }
}
