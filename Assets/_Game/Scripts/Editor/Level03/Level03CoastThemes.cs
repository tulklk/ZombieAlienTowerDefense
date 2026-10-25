using System.Collections.Generic;
using UnityEngine;

namespace AlienDefense.EditorTools
{
    /// <summary>What goes on Level_03's coast plots. Variety rule (user, 2026-10-12: "too many life rings - not one
    /// object over and over"): every plot size walks its own list of themes in turn (plot 0 gets theme 0, plot 1
    /// theme 1, ...), and the themes rotate their prop kinds, so the sea reads as a working bay - a swim lane here,
    /// then a surfboard line, moored rowing boats, a barrel line, fruit rafts, a speedboat course, a crate line,
    /// floating loungers - instead of life rings everywhere. Same idea on land.</summary>
    public static partial class Level03Builder
    {
        private static readonly string[] SeaBalls = { AQ + "BeachBall1.prefab", AQ + "BeachBall2.prefab", AQ + "BeachBall3.prefab" };
        private static readonly string[] SeaBarrels = { Prop + "PF_Barrel.prefab", Prop + "PF_MetalBarrel.prefab", Prop + "PF_Barrel_02.prefab", Prop + "PF_Barrel_Open.prefab" };
        private static readonly string[] SeaCrates = { Prop + "PF_Crate.prefab", Prop + "PF_Crate_Open.prefab", Prop + "PF_Crate_02.prefab", Prop + "PF_Crate_Open_02.prefab" };
        private static readonly string[] SeaBoards = { Prop3 + "PF3_Surfboard_Blue.prefab", AQ + "SurfBoard.prefab", Prop3 + "PF3_Surfboard_Yellow.prefab", AQ + "SurfBoard (1).prefab" };
        private static readonly string[] RowBoats = { L3 + "Vehicles/PF3_RowBoat_Blue.prefab", L3 + "Vehicles/PF3_RowBoat_Wood.prefab", L3 + "Vehicles/PF3_RowBoat_Red.prefab" };
        private static readonly string[] SpeedBoats = { KW + "boat-speed-a.fbx", KW + "boat-speed-e.fbx", KW + "boat-speed-c.fbx", KW + "boat-speed-h.fbx" };
        private static readonly string[] Containers = { KW + "cargo-container-a.fbx", KW + "cargo-container-b.fbx", KW + "cargo-container-c.fbx" };
        private static readonly string[] Fruit = { Prop + "PF_Watermelon.prefab", Prop + "PF_Pumpkin.prefab", Prop + "PF_Cabbage.prefab" };
        private static readonly string[] GardenCrops =
        {
            Prop + "PF_Cabbage.prefab", Prop + "PF_Crop_Tomato.prefab", Prop + "PF_Watermelon.prefab", Prop + "PF_Crop_Wheat.prefab", Prop + "PF_Pumpkin.prefab",
            Prop + "PF_Crop_Cabbage.prefab",
        };

        private static readonly string[] Parasols = { Prop3 + "PF3_Umbrella_Beach.prefab", Prop3 + "PF3_Umbrella_Red.prefab", AQ + "Umbrella1.prefab", Prop3 + "PF3_Umbrella_Market.prefab" };

        private static void SeaRow(List<PlotPlanner.Item> items, string[] kinds, int count, float step, float y, float yaw = 0f, float scale = 1f, int shift = 0)
        {
            for (int i = 0; i < count; i++)
            {
                items.Add(new PlotPlanner.Item(kinds[(i + shift) % kinds.Length], PlotPlanner.RowOffset(i, count, step), y, yaw, scale));
            }
        }

        private static void PutAll(PlotPlanner.Plot plot, List<PlotPlanner.Item> items, Where where, float lift = 0f)
        {
            foreach (PlotPlanner.Item item in items)
            {
                PutItem(plot, item, where, lift);
            }
        }

        // ------------------------------------------------------------------ sea

        private static void DressSeaPlot(PlotPlanner.Plot plot, int turn)
        {
            var items = new List<PlotPlanner.Item>();
            int round = turn / 8;
            if (plot.Tier == 0) // 10 x 3.2
            {
                switch (turn % 8)
                {
                    case 0:
                        // Swim lane: two lines of life rings, beach balls down the middle.
                        SeaRow(items, new[] { AQ + "LifeSaver.prefab" }, 8, 1.25f, 1.05f);
                        SeaRow(items, new[] { AQ + "LifeSaver.prefab" }, 8, 1.25f, -1.05f);
                        SeaRow(items, SeaBalls, 8, 1.25f, 0f, 0f, 1f, round);
                        break;
                    case 1:
                        // Surfboard line: boards floating side by side, nose out.
                        SeaRow(items, SeaBoards, 11, 0.85f, 0.35f, 0f, 1f, round);
                        SeaRow(items, SeaBalls, 11, 0.85f, -1.3f, 0f, 1f, round);
                        break;
                    case 2:
                        // Mooring: two rowing boats with marker buoys between and at the ends.
                        items.Add(new PlotPlanner.Item(RowBoats[round % 3], -2.4f, 0.3f, 45f, 0.8f));
                        items.Add(new PlotPlanner.Item(RowBoats[(round + 1) % 3], 2.4f, 0.3f, 45f, 0.8f));
                        SeaRow(items, new[] { Prop3 + "PF3_Buoy.prefab" }, 3, 4.6f, 0.3f, 0f, 0.7f);
                        SeaRow(items, SeaCrates, 9, 1.05f, -1.3f, 0f, 0.8f, round); // the catch, crated along the mooring line
                        break;
                    case 3:
                        // Barrel line: two rows, four kinds of barrel in turn.
                        SeaRow(items, SeaBarrels, 9, 1.05f, 1.05f, 0f, 0.95f, round);
                        SeaRow(items, SeaBarrels, 9, 1.05f, 0f, 0f, 0.95f, round + 1);
                        SeaRow(items, SeaBarrels, 9, 1.05f, -1.05f, 0f, 0.95f, round + 2);
                        break;
                    case 4:
                    {
                        // Fruit rafts: four pallets, each carrying a row of one fruit.
                        var cargo = new List<PlotPlanner.Item>();
                        for (int r = 0; r < 4; r++)
                        {
                            float x = PlotPlanner.RowOffset(r, 4, 2.4f);
                            items.Add(new PlotPlanner.Item(SeaBarrels[(r + round) % 4], x, 1.2f, 0f, 0.85f));
                            items.Add(new PlotPlanner.Item(SeaBarrels[(r + round + 1) % 4], x, -1.2f, 0f, 0.85f));
                            items.Add(new PlotPlanner.Item(Prop + "PF_Pallet.prefab", x, 0f));
                            for (int i = 0; i < 3; i++)
                            {
                                cargo.Add(new PlotPlanner.Item(Fruit[(r + round) % 3], x + PlotPlanner.RowOffset(i, 3, 0.6f), 0f));
                            }
                        }

                        PutAll(plot, cargo, Where.Water, 0.22f);
                        break;
                    }
                    case 5:
                        // Speedboat course: two boats nose to tail, marker buoys at the corners, a flag buoy mid-course.
                        items.Add(new PlotPlanner.Item(SpeedBoats[round % 4], -2.3f, 0f, 90f));
                        items.Add(new PlotPlanner.Item(SpeedBoats[(round + 2) % 4], 2.3f, 0f, 90f));
                        items.Add(new PlotPlanner.Item(KW + "buoy-flag.fbx", 0f, 0f, 0f, 0.8f));
                        SeaRow(items, new[] { KW + "buoy.fbx" }, 7, 1.5f, 1.3f, 0f, 0.7f);   // course markers down both sides
                        SeaRow(items, new[] { KW + "buoy.fbx" }, 7, 1.5f, -1.3f, 0f, 0.7f);

                        break;
                    case 6:
                        // Crate line: two rows, four kinds of crate in turn.
                        SeaRow(items, SeaCrates, 9, 1.05f, 1.05f, 0f, 0.95f, round);
                        SeaRow(items, SeaCrates, 9, 1.05f, 0f, 0f, 0.95f, round + 1);
                        SeaRow(items, SeaCrates, 9, 1.05f, -1.05f, 0f, 0.95f, round + 2);
                        break;
                    default:
                        // Floating loungers with a beach ball beside each.
                        SeaRow(items, new[] { Prop3 + "PF3_Lounger.prefab" }, 6, 1.6f, 0.3f);
                        SeaRow(items, SeaBalls, 6, 1.6f, -1.15f, 0f, 1f, round);
                        SeaRow(items, new[] { AQ + "Bucket.prefab", Prop3 + "PF3_Lifebuoy.prefab" }, 5, 1.6f, -1.15f);
                        break;
                }
            }
            else if (plot.Tier == 1) // 4.4 x 3.2
            {
                switch (turn % 6)
                {
                    case 0:
                        // Floating cargo block: barrels and crates chequered.
                        for (int j = 0; j < 3; j++)
                        {
                            for (int i = 0; i < 4; i++)
                            {
                                string key = (i + j) % 2 == 0 ? SeaBarrels[(j + round) % 4] : SeaCrates[(i + round) % 4];
                                items.Add(new PlotPlanner.Item(key, PlotPlanner.RowOffset(i, 4, 1.05f), PlotPlanner.RowOffset(j, 3, 1.0f), 0f, 0.95f));
                            }
                        }

                        break;
                    case 1:
                        // Three shipping containers side by side.
                        for (int i = 0; i < 3; i++)
                        {
                            items.Add(new PlotPlanner.Item(Containers[(i + round) % 3], PlotPlanner.RowOffset(i, 3, 1.45f), 0.25f, 0f, 0.8f));
                        }

                        SeaRow(items, SeaBarrels, 4, 1.05f, -1.25f, 0f, 0.85f, round);

                        break;
                    case 2:
                        // A small fishing boat with a buoy at each corner.
                        items.Add(new PlotPlanner.Item(KW + (round % 2 == 0 ? "boat-fishing-small.fbx" : "boat-fan.fbx"), 0f, 0f, 90f, 0.9f));
                        foreach (float x in new[] { -1.9f, 1.9f })
                        {
                            items.Add(new PlotPlanner.Item(Prop3 + "PF3_Buoy.prefab", x, 1.25f, 0f, 0.65f));
                            items.Add(new PlotPlanner.Item(Prop3 + "PF3_Buoy.prefab", x, -1.25f, 0f, 0.65f));
                        }

                        break;
                    case 3:
                        // Life-buoy cluster.
                        PlotPlanner.Grid(items, plot, Prop3 + "PF3_Lifebuoy.prefab", 1.25f, 1.2f, 0.5f, 0f, 1f);
                        break;
                    case 4:
                        // Watermelons bobbing in a block.
                        PlotPlanner.Grid(items, plot, Fruit[round % 3], 0.8f, 0.8f, 0.45f, 0f, 1f);
                        break;
                    default:
                        // A rowing boat with a ring of beach balls.
                        items.Add(new PlotPlanner.Item(RowBoats[(round + 2) % 3], 0f, 0f, 45f, 0.9f));
                        SeaRow(items, SeaBalls, 4, 1.2f, 1.3f, 0f, 1f, round);
                        SeaRow(items, SeaBalls, 4, 1.2f, -1.3f, 0f, 1f, round + 1);
                        break;
                }
            }
            else // 2.8 x 2
            {
                switch (turn % 6)
                {
                    case 0:
                        // Beach-ball raft, three colours.
                        for (int j = 0; j < 2; j++)
                        {
                            SeaRow(items, SeaBalls, 3, 0.85f, PlotPlanner.RowOffset(j, 2, 0.85f), 0f, 1f, j + round);
                        }

                        break;
                    case 1:
                        PlotPlanner.Grid(items, plot, AQ + "Bucket.prefab", 0.85f, 0.85f, 0.4f, 0f, 1f);
                        break;
                    case 2:
                        items.Add(new PlotPlanner.Item(KW + (round % 2 == 0 ? "boat-row-small.fbx" : "boat-row-large.fbx"), 0f, 0f, 90f, 0.85f));
                        break;
                    case 3:
                        // A marker buoy between two life rings.
                        items.Add(new PlotPlanner.Item(Prop3 + "PF3_Buoy.prefab", 0f, 0f, 0f, 0.7f));
                        items.Add(new PlotPlanner.Item(AQ + "LifeSaver.prefab", -0.95f, 0f));
                        items.Add(new PlotPlanner.Item(AQ + "LifeSaver.prefab", 0.95f, 0f));
                        break;
                    case 4:
                        PlotPlanner.Grid(items, plot, Fruit[(round + 1) % 3], 0.8f, 0.8f, 0.4f, 0f, 1f);
                        break;
                    default:
                        // Two surfboards adrift.
                        items.Add(new PlotPlanner.Item(SeaBoards[round % 4], -0.5f, 0f, 90f));
                        items.Add(new PlotPlanner.Item(SeaBoards[(round + 2) % 4], 0.5f, 0f, 90f));
                        break;
                }
            }

            PutAll(plot, items, Where.Water);
        }

        // ------------------------------------------------------------------ land

        private static void DressLandPlot(PlotPlanner.Plot plot, int turn)
        {
            var items = new List<PlotPlanner.Item>();
            bool beach = NearWater(plot.Center, 7f);
            if (plot.Tier == 0) // 9 x 4.4
            {
                int round = turn / 4;
                int theme = beach ? (turn % 2 == 0 ? 0 : 3) : turn % 4;
                switch (theme)
                {
                    case 0:
                    {
                        // Sun-lounger row: three parasols, two loungers under each, a ball and a bucket in front.
                        string parasol = Parasols[round % Parasols.Length];
                        for (int i = 0; i < 3; i++)
                        {
                            float x = PlotPlanner.RowOffset(i, 3, 3f);
                            items.Add(new PlotPlanner.Item(parasol, x, 0.7f, 0f, 0.85f));
                            items.Add(new PlotPlanner.Item(Prop3 + "PF3_Lounger.prefab", x - 0.55f, -0.35f));
                            items.Add(new PlotPlanner.Item(Prop3 + "PF3_Lounger.prefab", x + 0.55f, -0.35f));
                            items.Add(new PlotPlanner.Item(SeaBalls[(i + round) % 3], x - 0.7f, -1.75f));
                            items.Add(new PlotPlanner.Item(AQ + (i % 2 == 0 ? "Bucket.prefab" : "SandCastle.prefab"), x + 0.7f, -1.75f));
                        }

                        break;
                    }
                    case 2:
                    {
                        // Market row: three stalls (table under a market parasol), a row of open crates in front.
                        for (int i = 0; i < 3; i++)
                        {
                            float x = PlotPlanner.RowOffset(i, 3, 3f);
                            items.Add(new PlotPlanner.Item(Prop3 + (i % 2 == 0 ? "PF3_MarketTable_01.prefab" : "PF3_MarketTable_02.prefab"), x, 0.9f));
                        }

                        SeaRow(items, new[] { Prop + "PF_Crate_Open.prefab", Prop + "PF_Sack_01.prefab", Prop + "PF_Crate_Open_02.prefab", Prop + "PF_Sack_02.prefab" }, 8, 1.05f, -0.4f);
                        SeaRow(items, Fruit, 9, 0.9f, -1.5f, 0f, 1f, round);
                        break;
                    }
                    case 3:
                    {
                        // Camp row: tents with a picnic table between them, a bin at the end.
                        items.Add(new PlotPlanner.Item(Prop3 + "PF3_Tent_Blue.prefab", -3f, 0.4f));
                        items.Add(new PlotPlanner.Item(Prop3 + "PF3_PicnicTable.prefab", -0.2f, 0.5f));
                        items.Add(new PlotPlanner.Item(Prop3 + "PF3_Tent_Orange.prefab", 2.6f, 0.4f));
                        items.Add(new PlotPlanner.Item(Prop3 + "PF3_TrashBin.prefab", 4.1f, 1.5f));
                        SeaRow(items, new[] { Prop + "PF_Stump.prefab", Prop + "PF_Bucket.prefab", Prop + "PF_Stump.prefab", AQ + "BeachBall2.prefab" }, 8, 1.05f, -1.6f);
                        break;
                    }
                    default:
                    {
                        // Vegetable garden: two furrowed soil beds, one crop in rows (six crops in turn).
                        string soil = Prop + (turn % 2 == 0 ? "PF_FarmPlot_02.prefab" : "PF_FarmPlot_03.prefab");
                        // Beds flush with the ground (user, 2026-10-12: no raised soil) - FarmSoilFlatten.Flatten seats them
                        // and the crops after the plots are placed (see DressCoastPlots).
                        PutItem(plot, new PlotPlanner.Item(soil, -2.25f, 0f), Where.Land);
                        PutItem(plot, new PlotPlanner.Item(soil, 2.25f, 0f), Where.Land);
                        string crop = GardenCrops[round % GardenCrops.Length];
                        bool wide = crop.Contains("Pumpkin") || crop.Contains("Tomato");
                        float along = crop.Contains("Wheat") ? 0.6f : wide ? 0.9f : 0.62f;
                        PlotPlanner.Grid(items, plot, crop, along, wide ? 0.95f : 0.72f, 0.3f, 0f, 1f);
                        PutAll(plot, items, Where.Land, 0.3f);
                        return;
                    }
                }
            }
            else if (plot.Tier == 1) // 5 x 3.6
            {
                int round = turn / 7;
                switch (turn % 7)
                {
                    case 0:
                        // Beach-toy corner: a row each of sandcastles, buckets and balls.
                        SeaRow(items, new[] { AQ + "SandCastle.prefab" }, 4, 1.1f, 1.05f);
                        SeaRow(items, new[] { AQ + "Bucket.prefab" }, 4, 1.1f, 0f);
                        SeaRow(items, SeaBalls, 4, 1.1f, -1.05f, 0f, 1f, round);
                        break;
                    case 1:
                        // Surf rack: boards side by side, two life buoys in front.
                        SeaRow(items, SeaBoards, 6, 0.72f, 0.35f, 0f, 1f, round);
                        items.Add(new PlotPlanner.Item(Prop3 + "PF3_Lifebuoy.prefab", -1.1f, -1.25f));
                        items.Add(new PlotPlanner.Item(Prop3 + "PF3_Lifebuoy.prefab", 1.1f, -1.25f));
                        break;
                    case 2:
                        // Market stack: two 2 x 2 crate blocks of different crates and a mixed barrel row.
                        for (int b = 0; b < 2; b++)
                        {
                            float cx = b == 0 ? -1.25f : 1.25f;
                            for (int j = 0; j < 2; j++)
                            {
                                for (int i = 0; i < 2; i++)
                                {
                                    items.Add(new PlotPlanner.Item(SeaCrates[(b * 2 + (i + j) % 2 + round) % 4], cx + PlotPlanner.RowOffset(i, 2, 0.95f), -0.5f + PlotPlanner.RowOffset(j, 2, 0.95f)));
                                }
                            }
                        }

                        SeaRow(items, SeaBarrels, 5, 0.95f, 1.3f, 0f, 0.95f, round);
                        break;
                    case 3:
                        // Picnic spot: two tables, a bench row, a bin.
                        items.Add(new PlotPlanner.Item(Prop3 + "PF3_PicnicTable.prefab", -1.2f, 0.5f));
                        items.Add(new PlotPlanner.Item(Prop3 + "PF3_PicnicTable.prefab", 1.2f, 0.5f));
                        items.Add(new PlotPlanner.Item(Prop + "PF_Bench.prefab", -1.2f, -1.2f));
                        items.Add(new PlotPlanner.Item(Prop + "PF_Bench.prefab", 0.6f, -1.2f));
                        items.Add(new PlotPlanner.Item(Prop3 + "PF3_TrashBin.prefab", 2.1f, -1.2f));
                        break;
                    case 4:
                        // Cactus garden: two kinds in alternating rows, rocks at the corners.
                        SeaRow(items, new[] { L3 + "Vegetation/PF3_Cactus_01.prefab" }, 4, 1.2f, 0.9f, 0f, 0.6f);
                        SeaRow(items, new[] { L3 + "Vegetation/PF3_Cactus_02.prefab" }, 4, 1.2f, -0.2f, 0f, 0.6f);
                        SeaRow(items, new[] { NA + "Rock_1.fbx", NA + "Rock_3.fbx" }, 4, 1.2f, -1.25f, 0f, 0.5f);
                        break;
                    case 5:
                        // Barrel store: three rows, each a different barrel.
                        for (int j = 0; j < 3; j++)
                        {
                            SeaRow(items, new[] { SeaBarrels[(j + round) % 4] }, 4, 1.05f, PlotPlanner.RowOffset(j, 3, 1.1f), 0f, 0.95f);
                        }

                        break;
                    default:
                        // Fruit stand: three rows, one fruit each.
                        for (int j = 0; j < 3; j++)
                        {
                            SeaRow(items, new[] { Fruit[(j + round) % 3] }, 5, 0.85f, PlotPlanner.RowOffset(j, 3, 0.95f));
                        }

                        break;
                }
            }
            else // small beds, 3 x 2.2 and 2.2 x 1.6
            {
                int round = turn / 9;
                bool mini = plot.Size.x < 2.6f;
                switch (turn % 9)
                {
                    case 0:
                        PlotPlanner.Grid(items, plot, Fruit[round % 3], 0.72f, 0.72f, 0.35f, 0f, 1f);
                        break;
                    case 1:
                        PlotPlanner.Grid(items, plot, NA + "Bush_Small_Flowers.fbx", 1.25f, 1.2f, 0.5f, 0f, 0.55f);
                        break;
                    case 2:
                        PlotPlanner.Grid(items, plot, NA + RowFlowers[round % RowFlowers.Length], 0.75f, 0.75f, 0.35f, 0f, 1f);
                        break;
                    case 3:
                        // Rock garden.
                        SeaRow(items, new[] { NA + "Rock_1.fbx", NA + "Rock_3.fbx", NA + "Rock_5.fbx" }, mini ? 2 : 3, 0.95f, 0.3f, 0f, 0.45f, round);
                        SeaRow(items, new[] { NA + "Plant_2.fbx" }, mini ? 2 : 3, 0.95f, -0.45f);
                        break;
                    case 4:
                        // Sandcastles with a bucket row.
                        SeaRow(items, new[] { AQ + "SandCastle.prefab" }, mini ? 2 : 3, 0.95f, 0.4f);
                        SeaRow(items, new[] { AQ + "Bucket.prefab" }, mini ? 2 : 3, 0.95f, -0.45f);
                        break;
                    case 5:
                        // Beach balls, three colours.
                        for (int j = 0; j < 2; j++)
                        {
                            SeaRow(items, SeaBalls, mini ? 2 : 3, 0.85f, PlotPlanner.RowOffset(j, 2, 0.8f), 0f, 1f, j + round);
                        }

                        break;
                    case 6:
                        // Two cacti and a plant between.
                        items.Add(new PlotPlanner.Item(L3 + "Vegetation/PF3_Cactus_01.prefab", -0.7f, 0f, 0f, 0.6f));
                        items.Add(new PlotPlanner.Item(NA + "Plant_Flowers.fbx", 0f, 0f));
                        items.Add(new PlotPlanner.Item(L3 + "Vegetation/PF3_Cactus_02.prefab", 0.7f, 0f, 0f, 0.6f));
                        break;
                    case 7:
                        // Crates and sacks.
                        SeaRow(items, SeaCrates, mini ? 2 : 3, 0.95f, 0.4f, 0f, 0.9f, round);
                        SeaRow(items, new[] { Prop + "PF_Sack_01.prefab", Prop + "PF_Sack_02.prefab" }, mini ? 2 : 3, 0.9f, -0.5f);
                        break;
                    default:
                        // Life buoys on the sand.
                        SeaRow(items, new[] { Prop3 + "PF3_Lifebuoy.prefab" }, mini ? 1 : 2, 1.25f, 0f);
                        SeaRow(items, new[] { Prop + "PF_Bucket.prefab" }, mini ? 2 : 3, 0.8f, -0.7f);
                        break;
                }
            }

            PutAll(plot, items, Where.Land);
        }
    }
}
