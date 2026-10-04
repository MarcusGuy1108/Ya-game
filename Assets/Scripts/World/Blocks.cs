using System.Collections.Generic;

namespace UKCity.World
{
    public enum BlockShape : byte { None, Cube, Pole }

    /// <summary>Which material/submesh the block renders with.</summary>
    public enum RenderLayer : byte { Opaque, Cutout, Transparent }

    public static class BlockIds
    {
        public const byte Air = 0;
        // Nature
        public const byte Grass = 1, Dirt = 2, Stone = 3, Bedrock = 4;
        // Road surfaces
        public const byte Asphalt = 5, LineX = 6, LineZ = 7, YellowX = 8, YellowZ = 9;
        public const byte Kerb = 10, Pavement = 11, RoadPaint = 12, Gravel = 13;
        // Building materials
        public const byte RedBrick = 14, YellowBrick = 15, Render = 16, Pebbledash = 17, Slate = 18, RoofTile = 19;
        public const byte Concrete = 20, Planks = 21, Plaster = 22, FloorTile = 23, Carpet = 24, Glass = 25, Window = 26;
        public const byte DoorRed = 27, DoorBlue = 28, DoorGreen = 29, DoorBlack = 30, Shopfront = 31, ShopSign = 32;
        // Street furniture
        public const byte PhoneBox = 33, PhoneBoxTop = 34, PostBox = 35, MetalGrey = 36, MetalBlack = 37;
        public const byte PoleGrey = 38, PoleBlack = 39, PoleStriped = 40, LampHead = 41, TrafficLight = 42, Belisha = 43;
        public const byte SignGiveWay = 44, Sign30 = 45, SignNoEntry = 46, SignBusStop = 47, SignStreet = 48;
        // More nature
        public const byte Hedge = 50, Leaves = 51, Log = 52, Water = 53, Sand = 54;

        /// <summary>Returned by world queries for chunks that are not loaded yet. Treated as solid.</summary>
        public const byte Unloaded = 254;
    }

    public sealed class BlockDef
    {
        public byte Id;
        public string Name;
        public string Category;
        public BlockShape Shape = BlockShape.Cube;
        public RenderLayer Layer = RenderLayer.Opaque;
        /// <summary>Blocks movement. Doors are deliberately passable until they can open.</summary>
        public bool Solid = true;
        /// <summary>Shown in the block palette.</summary>
        public bool Placeable = true;
        public int TileTop, TileBottom, TileSide;
        /// <summary>The block to use when a structure is rotated by 90 degrees (road markings swap axis).</summary>
        public byte Rotated90;

        /// <summary>True if this block completely hides the faces of neighbours touching it.</summary>
        public bool Occludes => Shape == BlockShape.Cube && Layer == RenderLayer.Opaque;
        /// <summary>True if this block darkens neighbouring corners (ambient occlusion).</summary>
        public bool CastsAO => Shape == BlockShape.Cube && Layer != RenderLayer.Transparent;
        public bool Visible => Shape != BlockShape.None;
        /// <summary>Can be targeted by the crosshair.</summary>
        public bool Selectable => Shape != BlockShape.None && Id != BlockIds.Water;

        // Collision box inside the unit cube.
        public float MinX, MinY, MinZ, MaxX = 1, MaxY = 1, MaxZ = 1;
    }

    /// <summary>Static registry of every block in the game. Read-only after static init, so safe from worker threads.</summary>
    public static class Blocks
    {
        public static readonly BlockDef[] Defs = new BlockDef[256];
        public static readonly List<string> TileNames = new List<string>();
        public static readonly List<BlockDef> All = new List<BlockDef>();
        private static readonly Dictionary<string, int> tileIndex = new Dictionary<string, int>();

        public static readonly string[] Categories = { "Nature", "Road", "Building", "Street" };

        static Blocks()
        {
            var air = new BlockDef { Id = 0, Name = "Air", Category = "", Shape = BlockShape.None, Solid = false, Placeable = false };
            Defs[0] = air;

            // Nature
            Add(BlockIds.Grass, "Grass", "Nature", "grass_top", "dirt", "grass_side");
            Add(BlockIds.Dirt, "Dirt", "Nature", "dirt");
            Add(BlockIds.Stone, "Stone", "Nature", "stone");
            Add(BlockIds.Bedrock, "Bedrock", "Nature", "bedrock").Placeable = false;
            Add(BlockIds.Sand, "Sand", "Nature", "sand");
            Add(BlockIds.Gravel, "Gravel", "Nature", "gravel");
            Add(BlockIds.Log, "Oak Log", "Nature", "log_top", "log_top", "log_side");
            Add(BlockIds.Leaves, "Oak Leaves", "Nature", "leaves").Layer = RenderLayer.Cutout;
            Add(BlockIds.Hedge, "Privet Hedge", "Nature", "hedge").Layer = RenderLayer.Cutout;
            var water = Add(BlockIds.Water, "Water", "Nature", "water");
            water.Layer = RenderLayer.Transparent;
            water.Solid = false;

            // Road
            Add(BlockIds.Asphalt, "Tarmac", "Road", "asphalt");
            Add(BlockIds.LineX, "White Line (E-W)", "Road", "asphalt_line_x", "asphalt", "asphalt");
            Add(BlockIds.LineZ, "White Line (N-S)", "Road", "asphalt_line_z", "asphalt", "asphalt");
            Add(BlockIds.YellowX, "Double Yellow (E-W)", "Road", "asphalt_yellow_x", "asphalt", "asphalt");
            Add(BlockIds.YellowZ, "Double Yellow (N-S)", "Road", "asphalt_yellow_z", "asphalt", "asphalt");
            Add(BlockIds.RoadPaint, "Road Paint", "Road", "road_paint", "asphalt", "asphalt");
            Add(BlockIds.Kerb, "Kerb", "Road", "kerb");
            Add(BlockIds.Pavement, "Paving Slabs", "Road", "pavement", "concrete", "kerb");
            Pair(BlockIds.LineX, BlockIds.LineZ);
            Pair(BlockIds.YellowX, BlockIds.YellowZ);

            // Building
            Add(BlockIds.RedBrick, "Red Brick", "Building", "red_brick");
            Add(BlockIds.YellowBrick, "London Stock Brick", "Building", "yellow_brick");
            Add(BlockIds.Render, "Cream Render", "Building", "render");
            Add(BlockIds.Pebbledash, "Pebbledash", "Building", "pebbledash");
            Add(BlockIds.Slate, "Slate Roof", "Building", "slate");
            Add(BlockIds.RoofTile, "Clay Roof Tiles", "Building", "roof_tile");
            Add(BlockIds.Concrete, "Concrete", "Building", "concrete");
            Add(BlockIds.Planks, "Floorboards", "Building", "planks");
            Add(BlockIds.Plaster, "Plaster", "Building", "plaster");
            Add(BlockIds.FloorTile, "Hallway Tiles", "Building", "floor_tile");
            Add(BlockIds.Carpet, "Carpet", "Building", "carpet");
            Add(BlockIds.Glass, "Glass", "Building", "glass").Layer = RenderLayer.Transparent;
            Add(BlockIds.Window, "Sash Window", "Building", "window").Layer = RenderLayer.Transparent;
            Door(BlockIds.DoorRed, "Front Door (Red)", "door_red");
            Door(BlockIds.DoorBlue, "Front Door (Blue)", "door_blue");
            Door(BlockIds.DoorGreen, "Front Door (Green)", "door_green");
            Door(BlockIds.DoorBlack, "Front Door (Black)", "door_black");
            Add(BlockIds.Shopfront, "Shopfront Panel", "Building", "shopfront");
            Add(BlockIds.ShopSign, "Shop Sign", "Building", "shop_sign");

            // Street furniture
            Add(BlockIds.PhoneBox, "Phone Box", "Street", "phonebox_top", "phonebox_top", "phonebox");
            Add(BlockIds.PhoneBoxTop, "Phone Box Crown", "Street", "phonebox_top", "phonebox_top", "phonebox_crown");
            Add(BlockIds.PostBox, "Pillar Box", "Street", "postbox_top", "postbox_top", "postbox");
            Add(BlockIds.MetalGrey, "Grey Metal", "Street", "metal_grey");
            Add(BlockIds.MetalBlack, "Black Metal", "Street", "metal_black");
            Pole(BlockIds.PoleGrey, "Grey Pole", "pole_grey");
            Pole(BlockIds.PoleBlack, "Black Pole", "metal_black");
            Pole(BlockIds.PoleStriped, "Belisha Pole", "pole_striped");
            Add(BlockIds.LampHead, "Street Lamp", "Street", "lamp_top", "lamp", "lamp_side");
            Add(BlockIds.TrafficLight, "Traffic Light", "Street", "metal_black", "metal_black", "traffic_light");
            Add(BlockIds.Belisha, "Belisha Beacon", "Street", "belisha");
            Add(BlockIds.SignGiveWay, "Give Way Sign", "Street", "metal_grey", "metal_grey", "sign_giveway");
            Add(BlockIds.Sign30, "30 Limit Sign", "Street", "metal_grey", "metal_grey", "sign_30");
            Add(BlockIds.SignNoEntry, "No Entry Sign", "Street", "metal_grey", "metal_grey", "sign_noentry");
            Add(BlockIds.SignBusStop, "Bus Stop Flag", "Street", "metal_grey", "metal_grey", "sign_busstop");
            Add(BlockIds.SignStreet, "Street Name Plate", "Street", "metal_black", "metal_black", "sign_street");

            for (int i = 0; i < 256; i++)
            {
                if (Defs[i] == null) continue;
                if (Defs[i].Rotated90 == 0) Defs[i].Rotated90 = (byte)i;
                if (Defs[i].Visible) All.Add(Defs[i]);
            }
        }

        public static BlockDef Get(byte id) => Defs[id] ?? Defs[0];

        public static int Tile(string name)
        {
            if (!tileIndex.TryGetValue(name, out int idx))
            {
                idx = TileNames.Count;
                TileNames.Add(name);
                tileIndex[name] = idx;
            }
            return idx;
        }

        private static BlockDef Add(byte id, string name, string cat, string all) => Add(id, name, cat, all, all, all);

        private static BlockDef Add(byte id, string name, string cat, string top, string bottom, string side)
        {
            var d = new BlockDef
            {
                Id = id, Name = name, Category = cat,
                TileTop = Tile(top), TileBottom = Tile(bottom), TileSide = Tile(side)
            };
            Defs[id] = d;
            return d;
        }

        private static void Door(byte id, string name, string tile)
        {
            var d = Add(id, name, "Building", "planks", "planks", tile);
            d.Solid = false;
        }

        private static void Pole(byte id, string name, string tile)
        {
            var d = Add(id, name, "Street", tile);
            d.Shape = BlockShape.Pole;
            d.MinX = d.MinZ = 0.375f;
            d.MaxX = d.MaxZ = 0.625f;
        }

        private static void Pair(byte a, byte b)
        {
            Defs[a].Rotated90 = b;
            Defs[b].Rotated90 = a;
        }

        public static bool IsSolidForPhysics(byte id)
        {
            if (id == BlockIds.Unloaded) return true;
            var d = Defs[id];
            return d != null && d.Solid && d.Shape != BlockShape.None;
        }
    }
}
