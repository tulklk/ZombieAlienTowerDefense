using System.Collections.Generic;
using AlienDefense.Environment;
using UnityEngine;

namespace AlienDefense.EditorTools
{
    /// <summary>Level_02's absorbable farm stock, laid out like a working farm (user, 2026-10-11: the earlier random
    /// scatter "lay everywhere"). PlotPlanner packs the free ground with axis-aligned plots separated by walkways:
    ///   tier 0 - crop fields: two furrowed soil beds with one crop in straight rows, some with a rail fence;
    ///   tier 1 - yards: hay, crate storage, pumpkin patch, barrel store, tool row, log yard, feed yard, market corner;
    ///   tier 2/3 - beds: flowers in colour rows, hedge squares, garden corner, mushrooms, rock garden, melon and
    ///            cabbage beds, herb bed.
    /// Variety rule (user, 2026-10-12: "not one object over and over"): every tier walks its list of themes in turn
    /// (plot 0 gets theme 0, plot 1 theme 1, ...) and the themes themselves rotate their prop kinds, so no single
    /// prop dominates the map. Lane planting (flower border + hedge row along the road, flower ring round each
    /// BuildNode) rotates its kinds every few plants the same way.
    /// It runs before DensityPass and marks every plot hard-occupied, so the natural grass, flowers and bushes only
    /// grow around the plots, never through them. The soil beds and fences are scenery; everything on them can be
    /// absorbed. Every prop's XP then follows LevelXpBudget's size rule and the total is reported against the budget
    /// (enough to max every skill).</summary>
    public static partial class Level02Builder
    {
        private const float PlotGap = 1.3f;

        private static readonly Vector2 FieldSize = new Vector2(9.2f, 4.6f);
        private static readonly Vector2 YardSize = new Vector2(5f, 3.8f);
        private static readonly Vector2 BedSize = new Vector2(3.2f, 2.2f);
        private static readonly Vector2 MiniBedSize = new Vector2(2.2f, 1.6f);

        private struct Crop
        {
            public string Key;
            public float Along, Across, Scale;

            public Crop(string key, float along, float across, float scale = 1f)
            {
                Key = key;
                Along = along;
                Across = across;
                Scale = scale;
            }
        }

        private static readonly Crop[] Crops =
        {
            new Crop("PF_Cabbage", 0.75f, 0.9f), new Crop("PF_Crop_Tomato", 0.9f, 1.15f), new Crop("PF_Watermelon", 0.8f, 0.9f),
            new Crop("PF_Crop_Wheat", 0.6f, 0.8f), new Crop("PF_Pumpkin", 1.05f, 1.15f), new Crop("PF_Crop_Cabbage", 0.75f, 0.9f),
        };

        private static readonly string[] FlowerRows = { "PF_Flower_Red", "PF_Flower_Daisy", "PF_Flower_Purple", "PF_Flower_Poppy" };
        private static readonly string[] BorderPlants = { "PF_Flower_Red", "PF_Weed_Yellow", "PF_Flower_Daisy", "PF_LeafPlant_01", "PF_Flower_Purple", "PF_Weed_Orange", "PF_Flower_Poppy", "PF_Mushroom_01" };
        private static readonly string[] HedgeKinds = { "PF_Bush_Round_01", "PF_Bush_YellowGreen_01", "PF_Bush_02", "PF_Bush_Autumn_01", "PF_Shrub_01" };
        private static readonly float[] HedgeScale = { 0.6f, 0.6f, 0.5f, 0.6f, 0.5f };
        private static readonly string[] CrateKinds4 = { "PF_Crate", "PF_Crate_Open", "PF_Crate_02", "PF_Crate_Open_02" };
        private static readonly string[] BarrelKinds4 = { "PF_Barrel", "PF_MetalBarrel", "PF_Barrel_02", "PF_Barrel_Open" };

        private static Transform _gPlots;
        private static int _plotProps;

        private static void DressFarmPlots(Transform root)
        {
            _gPlots = Group(Group(root, "FarmProps"), "FarmPlots");
            _plotProps = 0;

            var planner = new PlotPlanner(PlotGap);
            PlotPlanner.FreeTest free = p => Blocker(p, 0.35f, 0.6f, Cat.Prop) == null && !Occupied(p, 0.35f, false);
            var area = Rect.MinMaxRect(MinX + 1f, MinZ + 1f, MaxX - 1f, MaxZ - 1f);
            int fields = planner.Pack(area, FieldSize, 1f, free, 0);
            int yards = planner.Pack(area, YardSize, 0.8f, free, 1);
            int beds = planner.Pack(area, BedSize, 0.6f, free, 2);
            beds += planner.Pack(area, MiniBedSize, 0.5f, free, 3); // mini beds in the last gaps

            float soilTop = KitLocalBounds("PF_FarmPlot_02").max.y;
            int fieldTurn = 0, yardTurn = 0, bedTurn = 0;
            foreach (PlotPlanner.Plot plot in planner.Plots)
            {
                Transform group = Group(_gPlots, (plot.Tier == 0 ? "Field_" : plot.Tier == 1 ? "Yard_" : "Bed_") + plot.Index.ToString("000"));
                if (plot.Tier == 0)
                {
                    DressField(plot, group, fieldTurn++, soilTop);
                }
                else if (plot.Tier == 1)
                {
                    DressYard(plot, group, yardTurn++);
                }
                else
                {
                    DressBed(plot, group, bedTurn++);
                }

                // Keep DensityPass's grass, flowers and bushes out of the plot (walkways stay natural).
                Vector2 half = (Mathf.Abs(plot.Yaw) < 1f ? plot.Size : new Vector2(plot.Size.y, plot.Size.x)) * 0.5f;
                MarkRect(plot.Center, half + new Vector2(0.3f, 0.3f), 0f, 2);
            }

            Report.Add("Farm plots: " + fields + " crop fields, " + yards + " yards, " + beds + " beds; " + _plotProps + " absorbable props on them.");

            // Lane planting: a mixed border and a clipped hedge row following both sides of the road, and a flower ring
            // round every BuildNode. Through Place(), so the usual road / plaza / base / occupancy rules apply and the
            // plots (marked hard above) are left alone. The kind changes every six plants.
            int before = _plotProps;
            Transform lanes = Group(_gPlots, "LanePlanting");
            for (int side = -1; side <= 1; side += 2)
            {
                int sideCopy = side;
                PlotPlanner.AlongPolyline(_mainRoadSamples, 0.9f, (point, tangent, index) =>
                {
                    float offset = MainRoadWidth * 0.5f + 0.65f;
                    Vector2 q = point + new Vector2(-tangent.y, tangent.x) * (sideCopy * offset);
                    if (q.x < MinX + 0.5f || RoadEdge(q) < 0.45f)
                    {
                        return; // off the map, or folded back onto the road inside a tight bend
                    }

                    string kind = BorderPlants[(index / 6 + (sideCopy > 0 ? 3 : 0)) % BorderPlants.Length];
                    CountAbsorbable(Place(kind, lanes, q, Mathf.Atan2(tangent.x, tangent.y) * Mathf.Rad2Deg, 1f, Cat.Flower));
                });
                PlotPlanner.AlongPolyline(_mainRoadSamples, 1.25f, (point, tangent, index) =>
                {
                    float offset = MainRoadWidth * 0.5f + 1.75f;
                    Vector2 q = point + new Vector2(-tangent.y, tangent.x) * (sideCopy * offset);
                    if (q.x < MinX + 0.5f || RoadEdge(q) < 1.5f)
                    {
                        return;
                    }

                    int h = (index / 6 + (sideCopy > 0 ? 2 : 0)) % HedgeKinds.Length;
                    CountAbsorbable(Place(HedgeKinds[h], lanes, q, 0f, HedgeScale[h], Cat.Bush));
                });
            }

            for (int n = 0; n < BuildNodeXZ.Length; n++)
            {
                const int count = 14;
                for (int i = 0; i < count; i++)
                {
                    float a = i * Mathf.PI * 2f / count;
                    Vector2 q = BuildNodeXZ[n] + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 3f;
                    CountAbsorbable(Place(FlowerRows[(n + i % 2 * 2) % FlowerRows.Length], lanes, q, a * Mathf.Rad2Deg, 1f, Cat.Flower));
                }
            }

            Report.Add("Lane planting: " + (_plotProps - before) + " border plants and hedge bushes along the road and round the BuildNodes.");
        }

        private static void CountAbsorbable(GameObject go)
        {
            if (go != null && go.GetComponentInChildren<TractorAbsorbableProp>(true) != null)
            {
                _plotProps++;
            }
        }

        private static void PutOnPlot(PlotPlanner.Plot plot, Transform group, PlotPlanner.Item item, Cat cat, float lift = 0f)
        {
            Vector2 world = PlotPlanner.ToWorld(plot, item.Local);
            GameObject go = Place(item.Key, group, world, PlotPlanner.ToWorldYaw(plot, item.Yaw), item.Scale, cat, true);
            if (go == null)
            {
                return;
            }

            if (lift != 0f)
            {
                go.transform.position += Vector3.up * lift;
            }

            CountAbsorbable(go);
        }

        private static void PutAll(PlotPlanner.Plot plot, Transform group, List<PlotPlanner.Item> items, float lift = 0f)
        {
            foreach (PlotPlanner.Item item in items)
            {
                PutOnPlot(plot, group, item, Cat.Prop, lift);
            }
        }

        /// <summary>A row of the given kinds (cycled) along the plot at local y.</summary>
        private static void Row(List<PlotPlanner.Item> items, string[] kinds, int count, float step, float y, float yaw = 0f, float scale = 1f, int shift = 0)
        {
            for (int i = 0; i < count; i++)
            {
                items.Add(new PlotPlanner.Item(kinds[(i + shift) % kinds.Length], PlotPlanner.RowOffset(i, count, step), y, yaw, scale));
            }
        }

        private static void DressField(PlotPlanner.Plot plot, Transform group, int turn, float soilTop)
        {
            string soil = turn % 2 == 0 ? "PF_FarmPlot_02" : "PF_FarmPlot_03";
            // The soil beds sit flush with the ground (user, 2026-10-12: no raised soil): sunk until only the furrowed
            // top shows, FarmSoilFlatten.SoilTop above the terrain; the crops stand on that.
            float sink = FarmSoilFlatten.SoilTop + 0.02f - soilTop;
            PutOnPlot(plot, group, new PlotPlanner.Item(soil, -2.3f, 0f), Cat.Field, sink);
            PutOnPlot(plot, group, new PlotPlanner.Item(soil, 2.3f, 0f), Cat.Field, sink);

            Crop crop = Crops[turn % Crops.Length];
            var items = new List<PlotPlanner.Item>();
            PlotPlanner.Grid(items, plot, crop.Key, crop.Along, crop.Across, 0.35f, 0f, crop.Scale);
            PutAll(plot, group, items, FarmSoilFlatten.SoilTop + 0.02f);

            // A rail fence along one long side of every other field, out in the walkway; a scarecrow in every fourth.
            if (turn % 2 == 1)
            {
                float side = turn % 4 == 1 ? 1f : -1f;
                for (int i = 0; i < 3; i++)
                {
                    PutOnPlot(plot, group, new PlotPlanner.Item("PF_Fence_Rail", PlotPlanner.RowOffset(i, 3, 3.05f), side * (plot.Size.y * 0.5f + 0.55f)), Cat.Fence);
                }
            }
        }

        private static void DressYard(PlotPlanner.Plot plot, Transform group, int turn)
        {
            var items = new List<PlotPlanner.Item>();
            switch (turn % 8)
            {
                case 0:
                    // Hay yard: two rows of square bales, a round bale at each end, small bales along the front.
                    for (int j = 0; j < 2; j++)
                    {
                        for (int i = 0; i < 2; i++)
                        {
                            items.Add(new PlotPlanner.Item("PF_Hay_Bale", PlotPlanner.RowOffset(i, 2, 1.6f), j == 0 ? -0.15f : 1.3f, 0f, 0.95f));
                        }
                    }

                    items.Add(new PlotPlanner.Item("PF_Hay_Round", -1.95f, 0.55f, 90f, 0.85f));
                    items.Add(new PlotPlanner.Item("PF_Hay_Round", 1.95f, 0.55f, 90f, 0.85f));
                    Row(items, new[] { "PF_Hay_Small" }, 4, 1.15f, -1.45f, 90f, 0.9f);
                    break;

                case 1:
                {
                    // Storage yard: two 2 x 2 crate blocks (each of two crate kinds, one stacked on top), barrels behind.
                    var stacked = new List<PlotPlanner.Item>();
                    for (int b = 0; b < 2; b++)
                    {
                        float cx = b == 0 ? -1.25f : 1.25f;
                        for (int j = 0; j < 2; j++)
                        {
                            for (int i = 0; i < 2; i++)
                            {
                                items.Add(new PlotPlanner.Item(CrateKinds4[(b * 2 + (i + j) % 2) % 4], cx + PlotPlanner.RowOffset(i, 2, 0.95f), -0.55f + PlotPlanner.RowOffset(j, 2, 0.95f)));
                            }
                        }

                        stacked.Add(new PlotPlanner.Item(CrateKinds4[b * 2], cx, -0.55f, 0f, 0.95f));
                    }

                    Row(items, BarrelKinds4, 5, 0.95f, 1.35f, 0f, 0.95f);
                    PutAll(plot, group, stacked, 0.88f);
                    break;
                }

                case 2:
                    // Pumpkin patch.
                    PlotPlanner.Grid(items, plot, "PF_Pumpkin", 1.0f, 1.0f, 0.5f, 0f, 1f);
                    break;

                case 3:
                    // Barrel store: three rows, each a different barrel.
                    for (int j = 0; j < 3; j++)
                    {
                        Row(items, new[] { BarrelKinds4[(j + turn / 8) % 4] }, 4, 1.05f, PlotPlanner.RowOffset(j, 3, 1.1f), 0f, 0.95f);
                    }

                    break;

                case 4:
                    // Tool row: wheelbarrows, tools leaning in a line, watering cans and buckets in front.
                    items.Add(new PlotPlanner.Item("PF_Wheelbarrow", -1.4f, 1.1f, 0f, 1f));
                    items.Add(new PlotPlanner.Item("PF_Wheelbarrow", 1.4f, 1.1f, 0f, 1f));
                    Row(items, new[] { "PF_Tool_Fork", "PF_Tool_Rake", "PF_Tool_Shovel" }, 5, 0.8f, 0f);
                    Row(items, new[] { "PF_WateringCan_01", "PF_Bucket", "PF_WateringCan_02", "PF_Bucket_Metal", "PF_WateringCan_03" }, 5, 0.85f, -1.2f);
                    break;

                case 5:
                    // Log yard: log piles side by side, a row of stumps in front.
                    items.Add(new PlotPlanner.Item("PF_LogPile", -1.25f, 0.75f));
                    items.Add(new PlotPlanner.Item("PF_LogPile_02", 1.25f, 0.75f));
                    Row(items, new[] { "PF_Stump" }, 4, 1.1f, -1.05f);
                    break;

                case 6:
                    // Feed yard: troughs at the back, sacks in the middle, small hay at the front.
                    items.Add(new PlotPlanner.Item("PF_Feeder", -1.1f, 1.25f));
                    items.Add(new PlotPlanner.Item("PF_Feeder_02", 1.1f, 1.25f));
                    Row(items, new[] { "PF_Sack_01", "PF_Sack_02" }, 5, 0.9f, 0.1f);
                    Row(items, new[] { "PF_Hay_Small" }, 4, 1.15f, -1.2f, 90f, 0.9f);
                    break;

                default:
                    // Market corner: open crates at the back, produce in rows in front of them.
                    Row(items, new[] { "PF_Crate_Open", "PF_Crate_Open_02" }, 5, 0.95f, 1.25f);
                    Row(items, new[] { "PF_Watermelon" }, 6, 0.75f, 0.3f);
                    Row(items, new[] { "PF_Cabbage" }, 6, 0.75f, -0.45f);
                    Row(items, new[] { "PF_Pumpkin" }, 5, 0.9f, -1.3f);
                    break;
            }

            PutAll(plot, group, items);
        }

        private static void DressBed(PlotPlanner.Plot plot, Transform group, int turn)
        {
            var items = new List<PlotPlanner.Item>();
            int perRow = Mathf.FloorToInt((plot.Size.x - 0.5f) / 0.62f) + 1;
            int rows = Mathf.FloorToInt((plot.Size.y - 0.5f) / 0.62f) + 1;
            switch (turn % 8)
            {
                case 0:
                    // Flower bed: one colour per row.
                    for (int j = 0; j < rows; j++)
                    {
                        Row(items, new[] { FlowerRows[(j + turn / 8) % FlowerRows.Length] }, perRow, 0.62f, PlotPlanner.RowOffset(j, rows, 0.62f));
                    }

                    break;

                case 1:
                {
                    // Hedge square.
                    int h = (turn / 8) % HedgeKinds.Length;
                    PlotPlanner.Grid(items, plot, HedgeKinds[h], 1.15f, 1.1f, 0.45f, 0f, HedgeScale[h] * 1.35f);
                    break;
                }

                case 2:
                    // Mushroom bed.
                    PlotPlanner.Grid(items, plot, "PF_Mushroom_01", 0.6f, 0.6f, 0.3f, 0f, 1f);
                    break;

                case 3:
                    // Melon bed.
                    PlotPlanner.Grid(items, plot, "PF_Watermelon", 0.7f, 0.7f, 0.35f, 0f, 1f);
                    break;

                case 4:
                    // Rock garden: boulders with flat stones between.
                    for (int j = 0; j < 2; j++)
                    {
                        Row(items, j == 0 ? new[] { "PF_Rock_01", "PF_Rock_03", "PF_Rock_02" } : new[] { "PF_Stones_01", "PF_Stones_02" }, plot.Size.x > 3f ? 3 : 2, 1.0f,
                            PlotPlanner.RowOffset(j, 2, 0.85f), 0f, 0.6f);
                    }

                    break;

                case 5:
                    // Herb bed: leafy plants and weeds in alternating rows.
                    for (int j = 0; j < rows; j++)
                    {
                        Row(items, new[] { j % 2 == 0 ? "PF_LeafPlant_01" : (turn / 8) % 2 == 0 ? "PF_Weed_Yellow" : "PF_Weed_Orange" }, perRow, 0.62f, PlotPlanner.RowOffset(j, rows, 0.62f));
                    }

                    break;

                case 6:
                    // Cabbage bed.
                    PlotPlanner.Grid(items, plot, "PF_Crop_Cabbage", 0.6f, 0.6f, 0.3f, 0f, 1f);
                    break;

                default:
                    // Garden corner: sacks at the back, a bucket and watering cans in front.
                    Row(items, new[] { "PF_Sack_01", "PF_Sack_02" }, plot.Size.x > 3f ? 3 : 2, 0.9f, 0.35f);
                    Row(items, new[] { "PF_Bucket", "PF_WateringCan_01", "PF_Bucket_Metal" }, plot.Size.x > 3f ? 3 : 2, 0.9f, -0.4f);
                    break;
            }

            PutAll(plot, group, items);
        }

        /// <summary>Applies LevelXpBudget's size rule to every absorbable prop and reports the total, plus the five
        /// most repeated props (the variety check).</summary>
        private static void ReportXpBudget(Transform root)
        {
            int target = LevelXpBudget.TargetXp();
            TractorAbsorbableProp[] props = root.GetComponentsInChildren<TractorAbsorbableProp>(true);
            LevelXpBudget.Tally tally = LevelXpBudget.ApplyAndCount(props);
            Report.Add("XP budget: max all skills = " + LevelXpBudget.RequiredXp() + " XP, target " + target + ". Level now: " + tally + "."
                + (tally.Xp < LevelXpBudget.RequiredXp() ? " WARNING: below what maxing the skills needs." : tally.Xp < target ? " (below the x" + LevelXpBudget.Headroom + " headroom)" : ""));
            Report.Add("Most repeated props: " + LevelXpBudget.TopKinds(props, 8));
        }
    }
}
