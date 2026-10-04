using System.Collections.Generic;
using UKCity.World;

namespace UKCity.City
{
    /// <summary>All placeable building templates: built-in UK set plus any captured by the player.</summary>
    public static class BuildingLibrary
    {
        public static readonly List<BuildingTemplate> All = new List<BuildingTemplate>();
        private static readonly Dictionary<string, BuildingTemplate> byId = new Dictionary<string, BuildingTemplate>();

        public static readonly string[] Categories = { "Houses", "Shops & Pubs", "Civic", "Street", "Nature", "Custom" };

        static BuildingLibrary()
        {
            Register(TerracedHouse());
            Register(TerraceRow());
            Register(SemiDetachedPair());
            Register(CornerShop());
            Register(Pub());
            Register(TowerBlock());
            Register(Church());
            Register(BusShelter());
            Register(PhoneBox());
            Register(PillarBox());
            Register(StreetLamp());
            Register(TrafficLights());
            Register(BelishaBeacon());
            Register(PostSign("sign_giveway", "Give Way Sign", BlockIds.SignGiveWay));
            Register(PostSign("sign_30", "30 mph Sign", BlockIds.Sign30));
            Register(PostSign("sign_noentry", "No Entry Sign", BlockIds.SignNoEntry));
            Register(StreetNamePlate());
            Register(OakTree());
            Register(Park());
        }

        public static void Register(BuildingTemplate t)
        {
            if (byId.TryGetValue(t.Id, out var existing)) All.Remove(existing);
            byId[t.Id] = t;
            All.Add(t);
        }

        public static BuildingTemplate Get(string id) => id != null && byId.TryGetValue(id, out var t) ? t : null;

        // ------------------------------------------------------------------ houses

        /// <summary>Victorian two-up two-down. 5 wide, small front yard, back garden. Party walls on both sides.</summary>
        private static void TerraceHouseInto(TemplateBuilder b, int ox, bool mirror)
        {
            // Local helper so the door can be on either side.
            int X(int x) => ox + (mirror ? 4 - x : x);

            // Ground: tiled front yard, house floor, back garden.
            for (int x = 0; x < 5; x++)
            {
                b.Fill(X(x), 0, 0, X(x), 0, 1, BlockIds.Pavement);
                b.Fill(X(x), 0, 2, X(x), 0, 9, BlockIds.Planks);
                b.Fill(X(x), 0, 10, X(x), 0, 15, BlockIds.Grass);
                b.Fill(X(x), 1, 0, X(x), 11, 15, BlockIds.Air);
            }
            b.Fill(X(1), 0, 3, X(1), 0, 9, BlockIds.FloorTile); // hallway

            // Low front wall with a gate gap in line with the door.
            for (int x = 0; x < 5; x++) if (x != 1) b.Set(X(x), 1, 0, BlockIds.RedBrick);

            // Shell.
            for (int x = 0; x < 5; x++)
                for (int z = 2; z <= 9; z++)
                    if (x == 0 || x == 4 || z == 2 || z == 9)
                        b.Fill(X(x), 1, z, X(x), 6, z, BlockIds.RedBrick);
            // First floor and loft floor.
            for (int x = 1; x <= 3; x++)
            {
                b.Fill(X(x), 3, 3, X(x), 3, 8, BlockIds.Carpet);
                b.Fill(X(x), 6, 3, X(x), 6, 8, BlockIds.Plaster);
            }
            // Stairs up the side of the hall: two steps and a hole in the floor.
            b.Set(X(3), 1, 6, BlockIds.Planks);
            b.Set(X(3), 2, 7, BlockIds.Planks);
            b.Fill(X(3), 3, 5, X(3), 3, 7, BlockIds.Air);

            // Front: door + bay window downstairs, two sash windows upstairs.
            b.Fill(X(1), 1, 2, X(1), 2, 2, BlockIds.DoorRed);
            b.Fill(X(3), 1, 2, X(3), 2, 2, BlockIds.Window);
            b.Set(X(1), 4, 2, BlockIds.Window); b.Set(X(1), 5, 2, BlockIds.Window);
            b.Set(X(3), 4, 2, BlockIds.Window); b.Set(X(3), 5, 2, BlockIds.Window);
            // Back.
            b.Fill(X(1), 1, 9, X(1), 2, 9, BlockIds.DoorBlack);
            b.Fill(X(3), 1, 9, X(3), 2, 9, BlockIds.Window);
            b.Fill(X(2), 4, 9, X(2), 5, 9, BlockIds.Window);

            // Slate roof, ridge parallel to the street.
            for (int z = 2; z <= 9; z++)
            {
                int step = System.Math.Min(z - 2, 9 - z);
                int y = 6 + step;
                for (int x = 0; x < 5; x++) b.Set(X(x), y, z, BlockIds.Slate);
                for (int yy = 7; yy < y; yy++)
                {
                    b.Set(X(0), yy, z, BlockIds.RedBrick);
                    b.Set(X(4), yy, z, BlockIds.RedBrick);
                }
            }
            // Chimney stack on the party wall.
            b.Fill(X(0), 10, 5, X(0), 11, 6, BlockIds.RedBrick);

            // Back garden fence.
            b.Fill(X(0), 1, 10, X(0), 1, 15, BlockIds.Planks);
            b.Fill(X(4), 1, 10, X(4), 1, 15, BlockIds.Planks);
            b.Fill(X(0), 1, 15, X(4), 1, 15, BlockIds.Planks);
        }

        private static BuildingTemplate TerracedHouse()
        {
            var b = new TemplateBuilder(5, 12, 16);
            TerraceHouseInto(b, 0, false);
            return b.Build("terrace_house", "Terraced House", "Houses", 2, 0,
                "Victorian red-brick two-up two-down with a slate roof.", varyDoors: true);
        }

        private static BuildingTemplate TerraceRow()
        {
            var b = new TemplateBuilder(20, 12, 16);
            for (int i = 0; i < 4; i++) TerraceHouseInto(b, i * 5, i % 2 == 1);
            return b.Build("terrace_row", "Terrace (row of 4)", "Houses", 10, 0,
                "Four terraced houses, doors paired in the classic mirrored layout.", varyDoors: true);
        }

        private static BuildingTemplate SemiDetachedPair()
        {
            var b = new TemplateBuilder(14, 12, 16);
            b.Fill(0, 1, 0, 13, 11, 15, BlockIds.Air);
            b.Fill(0, 0, 0, 13, 0, 3, BlockIds.Grass);
            b.Fill(0, 0, 4, 13, 0, 11, BlockIds.Carpet);
            b.Fill(0, 0, 12, 13, 0, 15, BlockIds.Grass);
            // Front hedge with gaps for the drives.
            b.Fill(0, 1, 0, 13, 1, 0, BlockIds.Hedge);
            for (int half = 0; half < 2; half++)
            {
                int ox = half * 7;
                bool m = half == 1;
                int X(int x) => ox + (m ? 6 - x : x);
                // Gravel drive and path.
                b.Fill(X(0), 0, 0, X(1), 0, 3, BlockIds.Gravel);
                b.Fill(X(0), 1, 0, X(1), 1, 0, BlockIds.Air);
                b.Fill(X(4), 0, 0, X(4), 0, 3, BlockIds.Pavement);
                b.Set(X(4), 1, 0, BlockIds.Air);
                // Shell: brick downstairs, pebbledash upstairs.
                for (int x = 0; x < 7; x++)
                    for (int z = 4; z <= 11; z++)
                    {
                        if (x != 0 && x != 6 && z != 4 && z != 11) continue;
                        b.Fill(X(x), 1, z, X(x), 3, z, BlockIds.RedBrick);
                        b.Fill(X(x), 4, z, X(x), 6, z, BlockIds.Pebbledash);
                    }
                b.Fill(X(1), 3, 5, X(5), 3, 10, BlockIds.Planks);
                b.Fill(X(1), 6, 5, X(5), 6, 10, BlockIds.Plaster);
                // Stairs.
                b.Set(X(5), 1, 7, BlockIds.Planks);
                b.Set(X(5), 2, 8, BlockIds.Planks);
                b.Fill(X(5), 3, 6, X(5), 3, 8, BlockIds.Air);
                // Door, bay window, upstairs windows.
                b.Fill(X(4), 1, 4, X(4), 2, 4, BlockIds.DoorRed);
                b.Fill(X(1), 1, 4, X(2), 2, 4, BlockIds.Window);
                b.Fill(X(1), 4, 4, X(2), 5, 4, BlockIds.Window);
                b.Fill(X(4), 4, 4, X(4), 5, 4, BlockIds.Window);
                b.Fill(X(2), 1, 11, X(3), 2, 11, BlockIds.Window);
                b.Fill(X(5), 1, 11, X(5), 2, 11, BlockIds.DoorBlack);
                b.Fill(X(2), 4, 11, X(3), 5, 11, BlockIds.Window);
                // Garden fence.
                b.Fill(X(0), 1, 12, X(0), 1, 15, BlockIds.Planks);
                b.Fill(X(0), 1, 15, X(6), 1, 15, BlockIds.Planks);
            }
            b.Fill(6, 1, 12, 7, 1, 15, BlockIds.Planks);
            b.GableRoofX(0, 13, 4, 11, 6, BlockIds.RoofTile, BlockIds.Pebbledash);
            b.Fill(6, 10, 7, 7, 11, 8, BlockIds.RedBrick);
            return b.Build("semi_pair", "Semi-detached Pair", "Houses", 7, 0,
                "1930s semis: brick and pebbledash, bay windows, clay tile roof.", varyDoors: true);
        }

        // ------------------------------------------------------------------ shops

        private static BuildingTemplate CornerShop()
        {
            var b = new TemplateBuilder(7, 10, 10);
            b.Fill(0, 1, 0, 6, 9, 9, BlockIds.Air);
            b.Fill(0, 0, 0, 6, 0, 9, BlockIds.FloorTile);
            b.Walls(0, 1, 0, 6, 7, 9, BlockIds.YellowBrick);
            // Shopfront.
            b.Fill(0, 1, 0, 6, 2, 0, BlockIds.Shopfront);
            b.Fill(1, 1, 0, 4, 2, 0, BlockIds.Glass);
            b.Fill(5, 1, 0, 5, 2, 0, BlockIds.DoorGreen);
            b.Fill(0, 3, 0, 6, 3, 0, BlockIds.ShopSign);
            // Counter and shelves.
            b.Fill(1, 1, 6, 3, 1, 6, BlockIds.Planks);
            b.Fill(1, 1, 8, 5, 2, 8, BlockIds.Planks);
            // Flat above.
            b.Fill(1, 4, 1, 5, 4, 8, BlockIds.Carpet);
            b.Set(1, 5, 0, BlockIds.Window); b.Set(1, 6, 0, BlockIds.Window);
            b.Set(3, 5, 0, BlockIds.Window); b.Set(3, 6, 0, BlockIds.Window);
            b.Set(5, 5, 0, BlockIds.Window); b.Set(5, 6, 0, BlockIds.Window);
            b.Fill(0, 5, 3, 0, 6, 4, BlockIds.Window);
            b.Fill(6, 5, 3, 6, 6, 4, BlockIds.Window);
            // Flat roof with parapet.
            b.Fill(0, 8, 0, 6, 8, 9, BlockIds.Concrete);
            b.Walls(0, 9, 0, 6, 9, 9, BlockIds.YellowBrick);
            return b.Build("corner_shop", "Corner Shop", "Shops & Pubs", 3, 0,
                "Newsagent with a flat upstairs. London stock brick.");
        }

        private static BuildingTemplate Pub()
        {
            var b = new TemplateBuilder(11, 15, 13);
            b.Fill(0, 1, 0, 10, 14, 12, BlockIds.Air);
            b.Fill(0, 0, 0, 10, 0, 0, BlockIds.Pavement);
            b.Fill(0, 0, 1, 10, 0, 12, BlockIds.Planks);
            b.Fill(1, 0, 2, 4, 0, 7, BlockIds.Carpet);
            // Ground floor: green tiled pub front.
            b.Walls(0, 1, 1, 10, 3, 12, BlockIds.Shopfront);
            b.Fill(1, 1, 1, 3, 2, 1, BlockIds.Window);
            b.Fill(7, 1, 1, 9, 2, 1, BlockIds.Window);
            b.Fill(5, 1, 1, 5, 2, 1, BlockIds.DoorBlack);
            b.Fill(0, 3, 1, 10, 3, 1, BlockIds.ShopSign);
            // Upstairs: cream render.
            b.Fill(1, 4, 2, 9, 4, 11, BlockIds.Planks);
            b.Walls(0, 4, 1, 10, 7, 12, BlockIds.Render);
            for (int x = 1; x <= 9; x += 2) { b.Set(x, 5, 1, BlockIds.Window); b.Set(x, 6, 1, BlockIds.Window); }
            // Bar.
            b.Fill(2, 1, 9, 8, 1, 9, BlockIds.Planks);
            b.Fill(2, 1, 11, 8, 2, 11, BlockIds.Planks);
            // Hanging sign on a bracket.
            b.Set(10, 5, 0, BlockIds.MetalBlack);
            b.Set(10, 4, 0, BlockIds.ShopSign);
            b.GableRoofX(0, 10, 1, 12, 8, BlockIds.Slate, BlockIds.Render);
            b.Fill(1, 13, 6, 1, 14, 7, BlockIds.RedBrick);
            return b.Build("pub", "The Red Lion (Pub)", "Shops & Pubs", 5, 0,
                "Proper local: green tiled frontage, render above, slate roof.");
        }

        // ------------------------------------------------------------------ civic

        private static BuildingTemplate TowerBlock()
        {
            const int storeys = 12;
            int h = storeys * 3 + 3;
            var b = new TemplateBuilder(13, h, 13);
            b.Fill(0, 1, 0, 12, h - 1, 12, BlockIds.Air);
            b.Fill(0, 0, 0, 12, 0, 12, BlockIds.Concrete);
            for (int s = 0; s < storeys; s++)
            {
                int y0 = s * 3;
                b.Fill(0, y0, 0, 12, y0, 12, BlockIds.Concrete);
                b.Walls(0, y0 + 1, 0, 12, y0 + 2, 12, BlockIds.Concrete);
                for (int i = 1; i < 12; i += 2)
                {
                    b.Fill(i, y0 + 1, 0, i, y0 + 2, 0, BlockIds.Window);
                    b.Fill(i, y0 + 1, 12, i, y0 + 2, 12, BlockIds.Window);
                    b.Fill(0, y0 + 1, i, 0, y0 + 2, i, BlockIds.Window);
                    b.Fill(12, y0 + 1, i, 12, y0 + 2, i, BlockIds.Window);
                }
                if (s > 0) b.Fill(5, y0, 5, 7, y0, 7, BlockIds.Air); // stairwell void (fly up)
            }
            int top = storeys * 3;
            b.Fill(0, top, 0, 12, top, 12, BlockIds.Concrete);
            b.Walls(0, top + 1, 0, 12, top + 1, 12, BlockIds.Concrete);
            b.Fill(4, top + 1, 4, 8, top + 2, 8, BlockIds.Concrete); // lift motor room
            // Entrance.
            b.Fill(5, 1, 0, 7, 2, 0, BlockIds.Glass);
            b.Fill(6, 1, 0, 6, 2, 0, BlockIds.DoorBlack);
            return b.Build("tower_block", "Council Tower Block", "Civic", 6, 0,
                "Twelve storeys of 1960s concrete. Fly up the stairwell.");
        }

        private static BuildingTemplate Church()
        {
            var b = new TemplateBuilder(11, 26, 22);
            b.Fill(0, 1, 0, 10, 25, 21, BlockIds.Air);
            b.Fill(0, 0, 0, 10, 0, 21, BlockIds.Grass);
            b.Fill(5, 0, 0, 5, 0, 2, BlockIds.Gravel);
            // Nave.
            b.Fill(1, 0, 7, 9, 0, 21, BlockIds.FloorTile);
            b.Walls(1, 1, 7, 9, 6, 21, BlockIds.Stone);
            for (int z = 9; z <= 19; z += 3)
            {
                b.Fill(1, 2, z, 1, 5, z, BlockIds.Glass);
                b.Fill(9, 2, z, 9, 5, z, BlockIds.Glass);
            }
            b.Fill(4, 3, 21, 6, 6, 21, BlockIds.Glass);
            // Pews.
            for (int z = 10; z <= 18; z += 2)
            {
                b.Fill(2, 1, z, 4, 1, z, BlockIds.Planks);
                b.Fill(6, 1, z, 8, 1, z, BlockIds.Planks);
            }
            // Roof along the nave (ridge along Z): step inwards from both side walls.
            for (int x = 1; x <= 9; x++)
            {
                int step = System.Math.Min(x - 1, 9 - x);
                int y = 7 + step;
                b.Fill(x, y, 7, x, y, 21, BlockIds.Slate);
                for (int yy = 7; yy < y; yy++) { b.Set(x, yy, 7, BlockIds.Stone); b.Set(x, yy, 21, BlockIds.Stone); }
            }
            // Tower with spire.
            b.Walls(3, 1, 2, 7, 14, 6, BlockIds.Stone);
            b.Fill(5, 1, 2, 5, 3, 2, BlockIds.DoorBlack);
            b.Fill(5, 1, 6, 5, 3, 7, BlockIds.Air);
            b.Fill(5, 10, 2, 5, 12, 2, BlockIds.Window);
            b.Fill(3, 10, 4, 3, 12, 4, BlockIds.Window);
            b.Fill(7, 10, 4, 7, 12, 4, BlockIds.Window);
            for (int i = 0; i < 3; i++)
                b.Fill(3 + i, 15 + i * 3, 2 + i, 7 - i, 17 + i * 3, 6 - i, BlockIds.Slate);
            b.Fill(5, 24, 4, 5, 25, 4, BlockIds.MetalBlack);
            return b.Build("church", "Parish Church", "Civic", 5, 0, "Stone church with a slate spire.");
        }

        private static BuildingTemplate Park()
        {
            var b = new TemplateBuilder(12, 9, 12);
            b.Fill(0, 1, 0, 11, 8, 11, BlockIds.Air);
            b.Fill(0, 0, 0, 11, 0, 11, BlockIds.Grass);
            b.Walls(0, 1, 0, 11, 1, 11, BlockIds.Hedge);
            b.Fill(5, 0, 0, 6, 0, 11, BlockIds.Gravel);
            b.Fill(0, 0, 5, 11, 0, 6, BlockIds.Gravel);
            b.Fill(5, 1, 0, 6, 1, 0, BlockIds.Air);
            b.Fill(5, 1, 11, 6, 1, 11, BlockIds.Air);
            b.Fill(0, 1, 5, 0, 1, 6, BlockIds.Air);
            b.Fill(11, 1, 5, 11, 1, 6, BlockIds.Air);
            b.Set(2, 1, 4, BlockIds.Planks); b.Set(3, 1, 4, BlockIds.Planks);
            b.Set(8, 1, 7, BlockIds.Planks); b.Set(9, 1, 7, BlockIds.Planks);
            TreeInto(b, 2, 2, 5);
            TreeInto(b, 9, 9, 5);
            TreeInto(b, 9, 2, 4);
            return b.Build("park", "Pocket Park", "Nature", 6, 0, "Hedged green with paths, benches and oaks.");
        }

        // ------------------------------------------------------------------ street furniture

        private static BuildingTemplate BusShelter()
        {
            var b = new TemplateBuilder(5, 4, 3);
            b.Fill(0, 1, 0, 4, 3, 2, BlockIds.Air);
            b.Fill(0, 1, 2, 3, 2, 2, BlockIds.Glass);
            b.Fill(0, 1, 1, 0, 2, 1, BlockIds.Glass);
            b.Fill(3, 1, 1, 3, 2, 1, BlockIds.Glass);
            b.Fill(0, 3, 0, 3, 3, 2, BlockIds.MetalGrey);
            b.Fill(4, 1, 0, 4, 2, 0, BlockIds.PoleGrey);
            b.Set(4, 3, 0, BlockIds.SignBusStop);
            return b.Build("bus_shelter", "Bus Shelter + Stop", "Street", 2, 0, "Glass shelter with a bus stop flag.");
        }

        private static BuildingTemplate PhoneBox()
        {
            var b = new TemplateBuilder(1, 4, 1);
            b.Fill(0, 1, 0, 0, 2, 0, BlockIds.PhoneBox);
            b.Set(0, 3, 0, BlockIds.PhoneBoxTop);
            return b.Build("phone_box", "Red Phone Box", "Street", 0, 0, "K6 telephone kiosk.");
        }

        private static BuildingTemplate PillarBox()
        {
            var b = new TemplateBuilder(1, 2, 1);
            b.Set(0, 1, 0, BlockIds.PostBox);
            return b.Build("pillar_box", "Pillar Box", "Street", 0, 0, "Royal Mail post box.");
        }

        private static BuildingTemplate StreetLamp()
        {
            var b = new TemplateBuilder(1, 7, 2);
            b.Fill(0, 1, 1, 0, 6, 1, BlockIds.PoleGrey);
            b.Set(0, 6, 0, BlockIds.LampHead);
            return b.Build("street_lamp", "Street Lamp", "Street", 0, 1, "Lamp column; the head overhangs the front.");
        }

        private static BuildingTemplate TrafficLights()
        {
            var b = new TemplateBuilder(1, 4, 1);
            b.Fill(0, 1, 0, 0, 2, 0, BlockIds.PoleBlack);
            b.Set(0, 3, 0, BlockIds.TrafficLight);
            return b.Build("traffic_lights", "Traffic Lights", "Street", 0, 0, "Static for now; they will cycle once traffic arrives.");
        }

        private static BuildingTemplate BelishaBeacon()
        {
            var b = new TemplateBuilder(1, 4, 1);
            b.Fill(0, 1, 0, 0, 2, 0, BlockIds.PoleStriped);
            b.Set(0, 3, 0, BlockIds.Belisha);
            return b.Build("belisha", "Belisha Beacon", "Street", 0, 0, "The flashing amber globe at a zebra crossing.");
        }

        private static BuildingTemplate PostSign(string id, string name, byte sign)
        {
            var b = new TemplateBuilder(1, 4, 1);
            b.Fill(0, 1, 0, 0, 2, 0, BlockIds.PoleGrey);
            b.Set(0, 3, 0, sign);
            return b.Build(id, name, "Street", 0, 0, "Sign on a post.");
        }

        private static BuildingTemplate StreetNamePlate()
        {
            var b = new TemplateBuilder(3, 3, 1);
            b.Set(0, 1, 0, BlockIds.PoleBlack);
            b.Set(2, 1, 0, BlockIds.PoleBlack);
            b.Fill(0, 2, 0, 2, 2, 0, BlockIds.SignStreet);
            return b.Build("street_name", "Street Name Plate", "Street", 1, 0, "Black-and-white street name sign on legs.");
        }

        // ------------------------------------------------------------------ nature

        public static void TreeInto(TemplateBuilder b, int cx, int cz, int trunk)
        {
            for (int y = trunk - 1; y <= trunk + 2; y++)
            {
                int r = y >= trunk + 1 ? 1 : 2;
                for (int dz = -r; dz <= r; dz++)
                    for (int dx = -r; dx <= r; dx++)
                    {
                        if (r == 2 && System.Math.Abs(dx) == 2 && System.Math.Abs(dz) == 2) continue;
                        if (y == trunk + 2 && dx != 0 && dz != 0) continue;
                        b.Set(cx + dx, y, cz + dz, BlockIds.Leaves);
                    }
            }
            b.Fill(cx, 1, cz, cx, trunk, cz, BlockIds.Log);
        }

        private static BuildingTemplate OakTree()
        {
            var b = new TemplateBuilder(5, 8, 5);
            TreeInto(b, 2, 2, 5);
            return b.Build("oak_tree", "Oak Tree", "Nature", 2, 2, "A proper English oak (well, a blocky one).");
        }
    }
}
