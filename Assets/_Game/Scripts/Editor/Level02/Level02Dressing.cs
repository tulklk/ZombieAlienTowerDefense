using System.Collections.Generic;
using AlienDefense.Environment;
using UnityEditor;
using UnityEngine;

namespace AlienDefense.EditorTools
{
    /// <summary>Level_02 dressing: fixed landmarks, then storytelling clusters around each of them (house yard:
    /// barrel + crate + mailbox + flowers; barn: hay + logs + wheel + wheelbarrow; pen: cows + hay + buckets...),
    /// road-edge clusters, tree clusters of 2-5 mixed species, the cow pasture, animals, vehicles, the emergency
    /// corner, then a density pass that tops up any empty patch of grass, and finally the cliff ledges. Visual density
    /// rises toward the north (enemy) end, the start clearing stays calm and open.</summary>
    public static partial class Level02Builder
    {
        private static readonly string[] GreenTrees = { "PF_Tree_Broadleaf_01", "PF_Tree_Broadleaf_02", "PF_Tree_Broadleaf_03", "PF_Tree_YellowGreen_01", "PF_Tree_YellowGreen_02", "PF_Tree_Fruit_01" };
        private static readonly string[] AutumnTrees = { "PF_Tree_Autumn_01", "PF_Tree_Autumn_02", "PF_Tree_Golden_01", "PF_Tree_Golden_02" };
        private static readonly string[] Pines = { "PF_Tree_Pine_01", "PF_Tree_Pine_02" };
        private static readonly string[] BushKinds = { "PF_Bush_Round_01", "PF_Bush_02", "PF_Shrub_01", "PF_Bush_YellowGreen_01" };
        private static readonly string[] FlowerKinds = { "PF_Flower_Red", "PF_Flower_Purple", "PF_Flower_Poppy", "PF_Flower_Daisy" };
        private static readonly string[] WeedKinds = { "PF_Weed_Orange", "PF_Weed_Yellow", "PF_LeafPlant_01" };
        private static readonly string[] GrassKinds = { "PF_Grass_01", "PF_Grass_02", "PF_Grass_03" };
        private static readonly string[] RockKinds = { "PF_Rock_01", "PF_Rock_02", "PF_Rock_03" };
        private static readonly string[] BarrelKinds = { "PF_Barrel", "PF_Barrel_02", "PF_Barrel_Open" };
        private static readonly string[] CrateKinds = { "PF_Crate", "PF_Crate_02", "PF_Crate_Open", "PF_Crate_Open_02" };

        private static Transform _gHouses, _gBarns, _gChurch, _gWaterTower, _gWindmill, _gMarket, _gWells, _gOther;
        private static Transform _gTrees, _gCliffTrees, _gBushes, _gFlowers, _gGrass;
        private static Transform _gBarrels, _gCrates, _gLogs, _gPumpkins, _gTools, _gFences, _gFields, _gMisc;
        private static Transform _gRocks, _gRoadEdge, _gCows, _gOtherAnimals, _gVehicles, _gFx;

        static partial void DressLevel(Transform root, Terrain terrain, List<LedgeSpot> ledges)
        {
            InitPlacement(terrain);
            Rejections.Clear();

            Transform buildings = Group(root, "Buildings");
            _gHouses = Group(buildings, "Houses");
            _gBarns = Group(buildings, "Barns");
            _gChurch = Group(buildings, "Church");
            _gWaterTower = Group(buildings, "WaterTower");
            _gWindmill = Group(buildings, "Windmill");
            _gMarket = Group(buildings, "Market");
            _gWells = Group(buildings, "Wells");
            _gOther = Group(buildings, "Other");
            Transform vegetation = Group(root, "Vegetation");
            _gTrees = Group(vegetation, "Trees");
            _gCliffTrees = Group(vegetation, "CliffTrees");
            _gBushes = Group(vegetation, "Bushes");
            _gFlowers = Group(vegetation, "Flowers");
            _gGrass = Group(vegetation, "GrassProps");
            Transform props = Group(root, "FarmProps");
            _gBarrels = Group(props, "Barrels");
            _gCrates = Group(props, "Crates");
            _gLogs = Group(props, "Logs");
            _gPumpkins = Group(props, "Pumpkins");
            _gTools = Group(props, "Tools");
            _gFences = Group(props, "Fences");
            _gFields = Group(props, "Fields");
            _gMisc = Group(props, "Misc");
            Transform environment = Group(root, "Environment");
            _gRocks = Group(environment, "Rocks");
            _gRoadEdge = Group(environment, "DirtRoads");
            Transform animals = Group(root, "Animals");
            _gCows = Group(animals, "Cows");
            _gOtherAnimals = Group(animals, "OtherAmbientAnimals");
            _gVehicles = Group(root, "Vehicles");
            _gFx = Group(environment, "AmbientFX");

            PlaceLandmarks();
            PlaceVehicles();
            DressHouses();
            DressMarketAndPlaza();
            DressWaterTower();
            DressWindmillAndFields();
            DressCowFarm();
            DressRedBarnAndChurch();
            DressEmergencyCorner();
            DressEnemyBarn();
            DressStartArea();
            DressClearingRims();
            DressRoadEdges();
            PlaceRoadsideTrees();
            PlaceTreeBelts();
            DressFarmPlots(root); // tidy themed plots first, so the natural cover below only fills the walkways around them
            DensityPass();
            DressCliffs(ledges);
            ReportXpBudget(root);

            int trees = Count(Cat.Tree) + Count(Cat.CliffDressing);
            int propCount = Count(Cat.Prop) + Count(Cat.Rock) + Count(Cat.Fence) + Count(Cat.Field);
            Report.Add("Placed: buildings " + Count(Cat.Building) + ", trees " + Count(Cat.Tree) + " (+" + Count(Cat.CliffDressing) + " on cliff ledges), bushes " + Count(Cat.Bush)
                + ", props " + Count(Cat.Prop) + ", rocks " + Count(Cat.Rock) + ", fences " + Count(Cat.Fence) + ", field plots " + Count(Cat.Field)
                + ", flowers " + Count(Cat.Flower) + ", grass " + Count(Cat.Grass) + ", animals " + Count(Cat.Animal) + ", vehicles " + Count(Cat.Vehicle) + ".");
            var reasons = new List<string>();
            foreach (KeyValuePair<string, int> kv in Rejections)
            {
                reasons.Add(kv.Key + "=" + kv.Value);
            }

            Report.Add("Rejected placements: " + string.Join(", ", reasons));
            _lastTreeTotal = trees;
            _lastPropTotal = propCount;
        }

        private static int _lastTreeTotal;
        private static int _lastPropTotal;

        private static int Count(Cat c) => PlacedCount.TryGetValue(c, out int n) ? n : 0;

        // ------------------------------------------------------------------ landmarks

        private static string BuildingPrefab(BuildingSpot b)
        {
            switch (b.Kind)
            {
                case Landmark.FarmHouseBlue: return "PF_FarmHouse_Blue";
                case Landmark.BarnWhite: return "PF_Barn_White";
                case Landmark.SiloRed: return "PF_Silo";
                case Landmark.ChickenCoop: return "PF_ChickenCoop";
                case Landmark.StoreYellow: return "PF_Store";
                case Landmark.House: return "PF_FarmHouse_0" + (b.Variant % 6 + 1);
                case Landmark.Church: return "PF_Church";
                case Landmark.WaterTower: return "PF_WaterTower";
                case Landmark.Windmill: return "PF_Windmill";
                case Landmark.WindPump: return "PF_WindPump";
                case Landmark.MarketStall: return "PF_MarketStall_0" + (b.Variant % 3 + 1);
                case Landmark.RedBarn: return "PF_Barn_Red";
                case Landmark.EnemyBarn: return "PF_EnemyBarn";
                case Landmark.OldShed: return "PF_OldShed";
                case Landmark.Well: return "PF_Well";
                default: return "PF_Well_Stone";
            }
        }

        private static Transform BuildingGroup(Landmark kind)
        {
            switch (kind)
            {
                case Landmark.RedBarn:
                case Landmark.BarnWhite:
                case Landmark.EnemyBarn:
                case Landmark.SiloRed: return _gBarns;
                case Landmark.Church: return _gChurch;
                case Landmark.WaterTower: return _gWaterTower;
                case Landmark.Windmill:
                case Landmark.WindPump: return _gWindmill;
                case Landmark.MarketStall: return _gMarket;
                case Landmark.Well:
                case Landmark.WellStone: return _gWells;
                case Landmark.House:
                case Landmark.FarmHouseBlue:
                case Landmark.StoreYellow: return _gHouses;
                default: return _gOther;
            }
        }

        private static void PlaceLandmarks()
        {
            foreach (BuildingSpot b in Buildings)
            {
                GameObject go = Place(BuildingPrefab(b), BuildingGroup(b.Kind), b.Position, b.Yaw, b.Scale, Cat.Building, true);
                if (go == null)
                {
                    Report.Add("WARNING: landmark " + b.Kind + " not placed.");
                    continue;
                }

                if (!string.IsNullOrEmpty(b.Name))
                {
                    go.name = b.Name;
                }

                float edge = RoadEdge(b.Position);
                foreach (Vector2 node in BuildNodeXZ)
                {
                    if (Vector2.Distance(node, b.Position) < 5f)
                    {
                        Report.Add("WARNING: " + go.name + " is " + Vector2.Distance(node, b.Position).ToString("F1") + " m from a BuildNode.");
                    }
                }
            }
        }

        // ------------------------------------------------------------------ clusters

        private static void FlowerPatch(Vector2 center, int count, float radius, string[] kinds = null)
        {
            kinds = kinds ?? FlowerKinds;
            string kind = Pick(kinds);
            for (int i = 0; i < count; i++)
            {
                if (Chance(0.3f))
                {
                    kind = Pick(kinds);
                }

                PlaceNear(kind, _gFlowers, center, radius, Cat.Flower, 0.85f, 1.2f, 3);
            }
        }

        private static void GrassPatch(Vector2 center, int count, float radius)
        {
            for (int i = 0; i < count; i++)
            {
                PlaceNear(Pick(GrassKinds), _gGrass, center, radius, Cat.Grass, 0.8f, 1.25f, 3);
            }
        }

        /// <summary>2-5 trees of mixed species with a bush, a rock and flowers at their feet.</summary>
        private static void TreeCluster(Vector2 center, int trees, float autumnShare, bool pines = false)
        {
            for (int i = 0; i < trees; i++)
            {
                string kind = pines && Chance(0.6f) ? Pick(Pines) : Chance(autumnShare) ? Pick(AutumnTrees) : Pick(GreenTrees);
                PlaceNear(kind, _gTrees, center, 2.2f + trees * 0.5f, Cat.Tree, 0.88f, 1.18f, 8);
            }

            PlaceNear(Pick(BushKinds), _gBushes, center, 3f, Cat.Bush, 0.85f, 1.15f, 6);
            if (Chance(0.6f))
            {
                PlaceNear(Pick(BushKinds), _gBushes, center, 3.4f, Cat.Bush, 0.8f, 1.1f, 6);
            }

            if (Chance(0.55f))
            {
                PlaceNear(Pick(RockKinds), _gRocks, center, 3f, Cat.Rock, 0.7f, 1.1f, 6);
            }

            FlowerPatch(center + Random2(2.5f), 2 + _rng.Next(3), 1.2f);
            GrassPatch(center, 2, 3f);
        }

        /// <summary>House yard: mailbox by the road side, barrels + crate at a front corner, flower bed along the
        /// front, a tree or two behind, maybe a clothes line or vegetable plot, a short picket fence.</summary>
        private static void HouseYard(Transform house, int variant)
        {
            GameObject source = PrefabUtility.GetCorrespondingObjectFromSource(house.gameObject);
            Bounds lb = source != null ? KitLocalBounds(source.name) : new Bounds(Vector3.zero, new Vector3(5.2f, 4f, 4.6f));
            float yaw = house.eulerAngles.y;
            float s = house.localScale.x;
            Vector2 c = new Vector2(house.position.x, house.position.z) + Rotate(new Vector2(lb.center.x, lb.center.z) * s, -yaw);
            Vector2 Local(float x, float z) => c + Rotate(new Vector2(x, z), -yaw);
            float hw = lb.extents.x * s + 0.35f;
            float hd = lb.extents.z * s + 0.35f;

            Place(Chance(0.5f) ? "PF_Mailbox_01" : "PF_Mailbox_02", _gMisc, Local(hw + 0.9f, hd + 1.6f), yaw + Rand(-10f, 10f), 1f, Cat.Prop);
            PlaceNear(Pick(BarrelKinds), _gBarrels, Local(-hw - 0.7f, hd - 0.4f), 0.3f, Cat.Prop, 0.9f, 1.05f);
            PlaceNear(Pick(BarrelKinds), _gBarrels, Local(-hw - 0.8f, hd - 1.5f), 0.3f, Cat.Prop, 0.85f, 1f);
            PlaceNear(Pick(CrateKinds), _gCrates, Local(-hw - 0.9f, hd + 0.8f), 0.3f, Cat.Prop, 0.85f, 1.05f);
            FlowerPatch(Local(-hw * 0.5f, hd + 0.9f), 3, 0.5f);
            FlowerPatch(Local(hw * 0.55f, hd + 0.9f), 3, 0.5f);
            PlaceNear(Pick(BushKinds), _gBushes, Local(hw + 0.8f, -hd + 0.6f), 0.5f, Cat.Bush, 0.8f, 1f);
            PlaceNear(Chance(0.35f) ? Pick(AutumnTrees) : Pick(GreenTrees), _gTrees, Local(-hw - 1.8f, -hd - 1.6f), 1f, Cat.Tree, 0.9f, 1.1f);
            PlaceNear(Pick(GreenTrees), _gTrees, Local(hw + 1.6f, -hd - 2.4f), 1.2f, Cat.Tree, 0.85f, 1.05f);

            switch (variant % 3)
            {
                case 0:
                    Place("PF_ClothesLine", _gMisc, Local(0f, -hd - 2.4f), yaw + Rand(-8f, 8f), 1f, Cat.Prop);
                    break;
                case 1:
                    Place("PF_FarmPlot_02", _gFields, Local(hw + 3.2f, 0.4f), yaw + 90f, 0.7f, Cat.Field);
                    for (int i = 0; i < 4; i++)
                    {
                        PlaceNear("PF_Crop_Cabbage", _gFields, Local(hw + 3.2f, -0.9f + i * 0.9f), 0.2f, Cat.Grass, 0.9f, 1.1f);
                    }

                    PlaceNear(Pick("PF_WateringCan_01", "PF_WateringCan_02", "PF_WateringCan_03"), _gTools, Local(hw + 1.6f, 1.9f), 0.3f, Cat.Prop);
                    break;
                default:
                    PlaceNear("PF_LogPile", _gLogs, Local(hw + 1.2f, -0.8f), 0.4f, Cat.Prop, 0.9f, 1f, 4, yaw + 90f);
                    PlaceNear("PF_Bench", _gMisc, Local(hw * 0.2f, hd + 1.6f), 0.2f, Cat.Prop, 1f, 1f, 3, yaw + 180f);
                    break;
            }

            FenceRun(_gFences, "PF_Fence_Picket", Local(-hw - 2.2f, hd + 2.3f), Local(-hw - 2.2f, -hd - 1f), 0.15f);
            FenceRun(_gFences, "PF_Fence_Picket", Local(-hw - 2.2f, hd + 2.3f), Local(-hw * 0.25f, hd + 2.3f), 0.2f);
        }

        private static void DressHouses()
        {
            int variant = 0;
            foreach (Transform house in _gHouses)
            {
                if (house.name.StartsWith("House_") || house.name.StartsWith("FarmHouse") || house.name.StartsWith("Store"))
                {
                    HouseYard(house, variant++);
                }
            }
        }

        private static void DressMarketAndPlaza()
        {
            // Around each stall: crates of produce, pumpkins, a barrel, sacks, a bucket.
            foreach (Transform stall in _gMarket)
            {
                Vector2 c = new Vector2(stall.position.x, stall.position.z);
                float yaw = stall.eulerAngles.y;
                Vector2 Local(float x, float z) => c + Rotate(new Vector2(x, z), -yaw);
                PlaceNear("PF_Crate_Open", _gCrates, Local(-1.9f, 0.6f), 0.3f, Cat.Prop, 0.9f, 1f);
                PlaceNear("PF_Pumpkin", _gPumpkins, Local(-1.8f, 1.5f), 0.3f, Cat.Prop, 0.9f, 1.2f);
                PlaceNear("PF_Pumpkin", _gPumpkins, Local(-1.3f, 1.8f), 0.3f, Cat.Prop, 0.8f, 1f);
                PlaceNear(Pick(BarrelKinds), _gBarrels, Local(1.95f, 0.1f), 0.2f, Cat.Prop, 0.85f, 1f);
                PlaceNear(Pick("PF_Sack_01", "PF_Sack_02"), _gCrates, Local(1.9f, 1.2f), 0.3f, Cat.Prop, 0.9f, 1f);
                PlaceNear("PF_Bucket", _gTools, Local(2.2f, -0.8f), 0.3f, Cat.Prop);
                PlaceNear("PF_Watermelon", _gPumpkins, Local(-2.3f, -0.5f), 0.3f, Cat.Prop);
            }

            // Market square edges and the lane from the start stem.
            Vector2 market = MarketSquareXZ;
            Place("PF_Bench", _gMisc, market + new Vector2(-5.8f, 2.6f), 60f, 1f, Cat.Prop);
            Place("PF_Bench", _gMisc, market + new Vector2(5.2f, -3.4f), -110f, 1f, Cat.Prop);
            Place("PF_Signpost", _gMisc, new Vector2(15.4f, -8.2f), 225f, 1f, Cat.Prop);
            FlowerPatch(market + new Vector2(-5.4f, -1.8f), 4, 0.8f);
            FlowerPatch(new Vector2(18.2f, -6.4f), 4, 0.8f);
            PlaceNear(Pick(BarrelKinds), _gBarrels, market + new Vector2(6.2f, 1.6f), 0.4f, Cat.Prop);
            PlaceNear("PF_Bucket", _gTools, market + new Vector2(6.8f, 0.6f), 0.3f, Cat.Prop);
            TreeCluster(new Vector2(1.5f, -28f), 3, 0.5f);
            TreeCluster(new Vector2(10.5f, -27.5f), 2, 0.7f);
            TreeCluster(new Vector2(23f, -28.5f), 2, 0.6f);
        }

        private static void DressWaterTower()
        {
            Vector2 tower = WaterTowerXZ;
            PlaceNear("PF_Barrel", _gBarrels, tower + new Vector2(-3.2f, 1.6f), 0.3f, Cat.Prop);
            PlaceNear("PF_Barrel_02", _gBarrels, tower + new Vector2(-3.4f, 0.5f), 0.3f, Cat.Prop);
            PlaceNear("PF_MetalBarrel", _gBarrels, tower + new Vector2(-2.6f, 2.4f), 0.3f, Cat.Prop);
            PlaceNear("PF_LogPile", _gLogs, tower + new Vector2(3.3f, -2.2f), 0.4f, Cat.Prop, 0.9f, 1f, 4, 80f);
            PlaceNear("PF_Rock_02", _gRocks, tower + new Vector2(-3.4f, -3f), 0.4f, Cat.Rock, 1.1f, 1.4f);
            PlaceNear("PF_Rock_03", _gRocks, tower + new Vector2(-2.4f, -3.8f), 0.4f, Cat.Rock, 0.7f, 0.9f);
            PlaceNear("PF_Bucket_Metal", _gTools, tower + new Vector2(2.6f, 1.6f), 0.3f, Cat.Prop);
            PlaceNear("PF_WagonWheel", _gMisc, tower + new Vector2(2.9f, 2.4f), 0.2f, Cat.Prop, 1f, 1f, 3, 75f);
            FlowerPatch(tower + new Vector2(-4.2f, -1.2f), 3, 0.6f);
            GrassPatch(tower + new Vector2(3.8f, -3.8f), 3, 1f);
            TreeCluster(tower + new Vector2(-6.5f, 4f), 3, 0.35f);
            TreeCluster(tower + new Vector2(5f, 5f), 2, 0.3f);
            TreeCluster(new Vector2(-15f, 28.5f), 3, 0.4f);
            TreeCluster(new Vector2(-37.5f, 27f), 2, 0.2f);
        }

        private static void DressWindmillAndFields()
        {
            // Windmill: small fields, hay, sacks, a wheelbarrow.
            Vector2 mill = WindmillXZ;
            Place("PF_FarmPlot_01", _gFields, mill + new Vector2(-5.2f, 1.2f), 0f, 0.95f, Cat.Field);
            Place("PF_FarmPlot_03", _gFields, mill + new Vector2(5.4f, 1.6f), 90f, 0.95f, Cat.Field);
            for (int i = 0; i < 6; i++)
            {
                PlaceNear("PF_Crop_Wheat", _gFields, mill + new Vector2(-6.6f + (i % 3) * 1.2f, 0.2f + (i / 3) * 1.6f), 0.15f, Cat.Grass, 0.9f, 1.1f, 2);
            }

            PlaceNear("PF_Hay_Round", _gMisc, mill + new Vector2(2.6f, -3.1f), 0.3f, Cat.Prop);
            PlaceNear("PF_Hay_Bale", _gMisc, mill + new Vector2(3.6f, -2.4f), 0.3f, Cat.Prop);
            PlaceNear("PF_Sack_01", _gCrates, mill + new Vector2(-2.4f, -3f), 0.3f, Cat.Prop);
            PlaceNear("PF_Sack_02", _gCrates, mill + new Vector2(-1.8f, -3.4f), 0.3f, Cat.Prop);
            PlaceNear("PF_Wheelbarrow", _gTools, mill + new Vector2(-3.6f, -2.2f), 0.3f, Cat.Prop, 1f, 1f, 4, 40f);

            // Fields around the windmill (bottom-left corner of the map), with a scarecrow.
            Vector2[] rightPlots = { new Vector2(22.5f, -18f), new Vector2(35.2f, -16.2f), new Vector2(24f, -28f) };
            foreach (Vector2 p in rightPlots)
            {
                Place(Pick("PF_FarmPlot_01", "PF_FarmPlot_02", "PF_FarmPlot_03"), _gFields, p, Pick(0f, 90f), 0.95f, Cat.Field);
                for (int i = 0; i < 4; i++)
                {
                    PlaceNear(Pick("PF_Crop_Tomato", "PF_Crop_Cabbage", "PF_Crop_Wheat"), _gFields, p + new Vector2(-1.3f + (i % 2) * 2.6f, -1.2f + (i / 2) * 2.4f), 0.2f, Cat.Grass, 0.9f, 1.1f, 2);
                }
            }

            Place("PF_Scarecrow", _gMisc, new Vector2(35.8f, -20.2f), 200f, 1f, Cat.Prop);
            Vector2[] leftPlots = { new Vector2(36f, -27f), new Vector2(-2f, 28.2f) };
            foreach (Vector2 p in leftPlots)
            {
                Place(Pick("PF_FarmPlot_01", "PF_FarmPlot_02"), _gFields, p, 0f, 0.95f, Cat.Field);
                for (int i = 0; i < 4; i++)
                {
                    PlaceNear("PF_Crop_Wheat", _gFields, p + new Vector2(-1.2f + (i % 2) * 2.4f, -1.1f + (i / 2) * 2.2f), 0.2f, Cat.Grass, 0.9f, 1.1f, 2);
                }
            }

            FenceRun(_gFences, "PF_Fence_Rail", new Vector2(19.8f, -15.2f), new Vector2(25.6f, -15.2f), 0.2f);
            FenceRun(_gFences, "PF_Fence_Rail", new Vector2(32.4f, -13.6f), new Vector2(37.8f, -13.6f), 0.25f);

            // Storage yard in front of the white barn.
            Vector2 yard = StorageYardXZ;
            PlaceNear("PF_Hay_Bale", _gMisc, yard + new Vector2(-3.4f, 0.8f), 0.3f, Cat.Prop);
            PlaceNear("PF_Hay_Bale", _gMisc, yard + new Vector2(-3.6f, -0.6f), 0.3f, Cat.Prop);
            PlaceNear("PF_Hay_Round", _gMisc, yard + new Vector2(-2.3f, 2f), 0.3f, Cat.Prop);
            PlaceNear("PF_Pallet", _gCrates, yard + new Vector2(3.4f, 1.6f), 0.3f, Cat.Prop);
            PlaceNear("PF_Crate_Long", _gCrates, yard + new Vector2(3.2f, 2.6f), 0.3f, Cat.Prop);
            PlaceNear("PF_LogPile_02", _gLogs, yard + new Vector2(4.4f, -1.4f), 0.4f, Cat.Prop, 0.9f, 1f, 4, 10f);
            PlaceNear("PF_Tool_Fork", _gTools, yard + new Vector2(-1.5f, 3f), 0.2f, Cat.Prop);

            // Chicken coop and the farmhouse yard.
            Vector2 coop = CoopXZ;
            PlaceNear("PF_Feeder", _gTools, coop + new Vector2(2.4f, -1.8f), 0.3f, Cat.Prop);
            PlaceNear("PF_Hay_Small", _gMisc, coop + new Vector2(-2.2f, -1.6f), 0.3f, Cat.Prop);
            PlaceNear("PF_Bucket", _gTools, coop + new Vector2(1.6f, -2.6f), 0.3f, Cat.Prop);
            TreeCluster(new Vector2(28f, 29f), 3, 0.3f);
            TreeCluster(new Vector2(10.5f, 28.5f), 2, 0.2f);
        }

        private static void DressCowFarm()
        {
            // Pasture between the two long runs; the gate faces the lower run (+X, toward the camera).
            Rect r = CowPasture;
            float gateHalf = 1.5f;
            FenceRun(_gFences, "PF_Fence_Rail", new Vector2(r.xMax, r.yMin), new Vector2(r.xMax, CowPastureGateZ - gateHalf), 0f, true);
            FenceRun(_gFences, "PF_Fence_Rail", new Vector2(r.xMax, CowPastureGateZ + gateHalf), new Vector2(r.xMax, r.yMax), 0f, true);
            FenceRun(_gFences, "PF_Fence_Rail", new Vector2(r.xMax, r.yMax), new Vector2(r.xMin, r.yMax), 0f, true);
            FenceRun(_gFences, "PF_Fence_Rail", new Vector2(r.xMin, r.yMax), new Vector2(r.xMin, r.yMin), 0f, true);
            FenceRun(_gFences, "PF_Fence_Rail", new Vector2(r.xMin, r.yMin), new Vector2(r.xMax, r.yMin), 0f, true);

            // Cows: scattered, never in rows, each with its own idle phase and a small wander radius.
            var spots = new List<Vector2>();
            for (int attempt = 0; attempt < 300 && spots.Count < 5; attempt++)
            {
                var p = new Vector2(Rand(r.xMin + 1.5f, r.xMax - 1.5f), Rand(r.yMin + 1.8f, r.yMax - 1.8f));
                bool clear = true;
                foreach (Vector2 s in spots)
                {
                    if (Vector2.Distance(s, p) < 2.5f)
                    {
                        clear = false;
                    }
                }

                if (clear)
                {
                    spots.Add(p);
                }
            }

            // Two more cows loose in front of the red barn.
            spots.Add(RedBarnYardXZ + new Vector2(-0.6f, -2.6f));
            spots.Add(RedBarnYardXZ + new Vector2(0.4f, 2.2f));
            for (int i = 0; i < spots.Count; i++)
            {
                GameObject cow = Place("PF_Cow", _gCows, spots[i], Rand(0f, 360f), Rand(0.92f, 1.08f), Cat.Animal, true);
                if (cow != null)
                {
                    var wander = cow.GetComponentInChildren<Animator>().gameObject.AddComponent<AnimalWanderer>();
                    wander.Configure(i < spots.Count - 2 ? 1.4f : 1f, 0.7f);
                }
            }

            PlaceNear("PF_Hay_Pile", _gMisc, new Vector2(r.xMin + 1.3f, r.yMin + 1.4f), 0.2f, Cat.Prop, 1f, 1.2f);
            PlaceNear("PF_Hay_Bale", _gMisc, new Vector2(r.xMin + 1.2f, r.yMax - 1.3f), 0.2f, Cat.Prop);
            PlaceNear("PF_Feeder", _gTools, new Vector2(r.xMin + 1f, r.center.y), 0.2f, Cat.Prop, 1f, 1f, 3, 90f);
            PlaceNear("PF_Bucket", _gTools, new Vector2(r.xMax - 1f, CowPastureGateZ + 2.6f), 0.3f, Cat.Prop);
            PlaceNear("PF_Bucket_Metal", _gTools, new Vector2(r.xMin + 1.3f, r.center.y + 1.6f), 0.3f, Cat.Prop);
            PlaceNear("PF_Barrel_Open", _gBarrels, new Vector2(r.xMax - 1f, r.yMin + 1f), 0.3f, Cat.Prop);
        }

        private static void DressRedBarnAndChurch()
        {
            // Red barn (doors toward +X, the U-turn): hay, barrels, logs, wagon wheel, wheelbarrow, crates, tools.
            Vector2 barn = RedBarnXZ;
            Vector2 yard = RedBarnYardXZ;
            PlaceNear("PF_Hay_Bale", _gMisc, yard + new Vector2(-0.4f, -4.6f), 0.3f, Cat.Prop);
            PlaceNear("PF_Hay_Bale", _gMisc, yard + new Vector2(0.8f, -5.2f), 0.3f, Cat.Prop);
            PlaceNear("PF_Hay_Round", _gMisc, yard + new Vector2(1.6f, -3.8f), 0.3f, Cat.Prop);
            PlaceNear("PF_Barrel", _gBarrels, yard + new Vector2(-0.4f, 4.6f), 0.3f, Cat.Prop);
            PlaceNear("PF_Barrel_02", _gBarrels, yard + new Vector2(0.6f, 5.1f), 0.3f, Cat.Prop);
            PlaceNear("PF_WagonWheel", _gMisc, yard + new Vector2(-1.2f, 5.6f), 0.2f, Cat.Prop, 1f, 1f, 3, 100f);
            PlaceNear("PF_Wheelbarrow", _gTools, yard + new Vector2(2f, 3.8f), 0.3f, Cat.Prop, 1f, 1f, 3, 200f);
            PlaceNear("PF_LogPile", _gLogs, barn + new Vector2(-2f, 6.6f), 0.4f, Cat.Prop, 1f, 1f, 4, 0f);
            PlaceNear("PF_Crate_Open", _gCrates, barn + new Vector2(3.5f, -6.2f), 0.3f, Cat.Prop);
            PlaceNear("PF_Tool_Shovel", _gTools, barn + new Vector2(4.6f, -5.8f), 0.2f, Cat.Prop, 1f, 1f, 3, 90f);
            TreeCluster(new Vector2(-35.5f, -28.5f), 3, 0.45f);
            TreeCluster(new Vector2(-21.5f, -28.8f), 2, 0.4f);

            // Church (front toward +X, the camera): benches, flowers, picket fence either side of the entrance.
            Vector2 church = ChurchXZ;
            Place("PF_Bench", _gMisc, church + new Vector2(8.9f, -4.4f), 90f, 1f, Cat.Prop);
            Place("PF_Bench", _gMisc, church + new Vector2(8.9f, 4.4f), 90f, 1f, Cat.Prop);
            FlowerPatch(church + new Vector2(9.6f, -2.6f), 4, 0.7f, new[] { "PF_Flower_Red", "PF_Flower_Purple" });
            FlowerPatch(church + new Vector2(9.6f, 2.6f), 4, 0.7f, new[] { "PF_Flower_Daisy", "PF_Flower_Purple" });
            FenceRun(_gFences, "PF_Fence_Picket", church + new Vector2(9.9f, 6.2f), church + new Vector2(9.9f, 1.6f), 0f);
            FenceRun(_gFences, "PF_Fence_Picket", church + new Vector2(9.9f, -1.6f), church + new Vector2(9.9f, -5.2f), 0f);
            Place("PF_Signpost", _gMisc, new Vector2(-17.6f, 3.2f), 110f, 1f, Cat.Prop);
            TreeCluster(church + new Vector2(-1.5f, -7.2f), 2, 0.8f);
            TreeCluster(church + new Vector2(-1f, 7.8f), 2, 0.7f);

            // A few sheep grazing the meadow right of the right hook.
            for (int i = 0; i < 3; i++)
            {
                GameObject sheep = PlaceNear("PF_Sheep", _gOtherAnimals, new Vector2(-4f, 26f), 2.2f, Cat.Animal, 0.95f, 1.05f, 8);
                if (sheep != null)
                {
                    sheep.GetComponentInChildren<Animator>().gameObject.AddComponent<AnimalWanderer>().Configure(1.8f, 0.5f);
                }
            }
        }

        private static void DressEmergencyCorner()
        {
            Vector2 corner = EmergencyCornerXZ;
            Place("PF_FireTruck", _gVehicles, corner + new Vector2(-2.6f, -1.4f), 95f, 1f, Cat.Vehicle, true);
            Place("PF_FireTruck", _gVehicles, corner + new Vector2(2.4f, 1.6f), 62f, 1f, Cat.Vehicle, true);
            GameObject hay = Place("PF_Hay_Pile", _gMisc, corner + new Vector2(4.6f, -2.1f), 30f, 1.4f, Cat.Prop, true);
            if (hay != null && Kit.TryGetValue("PF_Fire_Small", out GameObject fire))
            {
                var fx = (GameObject)PrefabUtility.InstantiatePrefab(fire, _gFx);
                fx.transform.position = hay.transform.position + Vector3.up * 0.4f;
                fx.name = "Fire_Haystack";
            }

            PlaceNear("PF_Barrel", _gBarrels, corner + new Vector2(-3.4f, 2.4f), 0.3f, Cat.Prop);
            PlaceNear("PF_MetalBarrel", _gBarrels, corner + new Vector2(-2.6f, 3f), 0.3f, Cat.Prop);
            PlaceNear("PF_Bucket_Metal", _gTools, corner + new Vector2(3.6f, -0.6f), 0.3f, Cat.Prop);
            PlaceNear("PF_Bucket_Metal", _gTools, corner + new Vector2(5.6f, -0.2f), 0.3f, Cat.Prop);
            PlaceNear("PF_Crate", _gCrates, corner + new Vector2(0.2f, -2.8f), 0.3f, Cat.Prop);
        }

        private static void DressEnemyBarn()
        {
            // Old, darker ground: dead trees, dead shrubs, mushroom rings, rocks, broken weathered fences.
            Vector2[] deadGroves = { new Vector2(-40.5f, -9f), new Vector2(-45f, -17f), new Vector2(-39.5f, -24.5f), new Vector2(-44.5f, 19.5f), new Vector2(-40f, 28f), new Vector2(-47.5f, -4f) };
            foreach (Vector2 g in deadGroves)
            {
                PlaceNear(Pick("PF_Tree_Dead_01", "PF_Tree_Dead_02"), _gTrees, g, 1.5f, Cat.Tree, 0.9f, 1.15f, 8);
                PlaceNear(Pick(Pines), _gTrees, g + new Vector2(1.5f, 2f), 1.5f, Cat.Tree, 0.9f, 1.1f, 8);
                PlaceNear("PF_Shrub_Dead_01", _gBushes, g + new Vector2(-1.5f, 1f), 1f, Cat.Bush, 0.8f, 1.1f, 6);
                PlaceNear(Pick(RockKinds), _gRocks, g + new Vector2(0.5f, -1.8f), 1f, Cat.Rock, 0.8f, 1.3f, 6);
                for (int i = 0; i < 4; i++)
                {
                    PlaceNear("PF_Mushroom_01", _gFlowers, g + new Vector2(-0.8f, -0.6f), 1.2f, Cat.Flower, 0.8f, 1.3f, 3);
                }

                PlaceNear("PF_Weed_Orange", _gFlowers, g, 2.5f, Cat.Flower, 0.9f, 1.2f, 3);
            }

            FenceRun(_gFences, "PF_Fence_Weathered", new Vector2(-44.5f, -2.6f), new Vector2(-37f, -3.2f), 0.35f);
            FenceRun(_gFences, "PF_Fence_Weathered", new Vector2(-44.5f, 16.6f), new Vector2(-37f, 17.2f), 0.35f);
            PlaceNear("PF_Barrel_Open", _gBarrels, new Vector2(-38.5f, -1.2f), 0.4f, Cat.Prop);
            PlaceNear("PF_Barrel", _gBarrels, new Vector2(-37.6f, -1.9f), 0.4f, Cat.Prop);
            PlaceNear("PF_Crate_Open_02", _gCrates, new Vector2(-38.8f, 15.4f), 0.4f, Cat.Prop);
            PlaceNear("PF_LogPile_02", _gLogs, new Vector2(-34.2f, -1.8f), 0.4f, Cat.Prop, 1f, 1f, 4, 20f);
            PlaceNear("PF_Stump", _gLogs, new Vector2(-33f, 15.6f), 0.4f, Cat.Prop);
            PlaceNear("PF_Rock_01", _gRocks, new Vector2(-35.5f, -4f), 0.5f, Cat.Rock, 1.2f, 1.5f);
            PlaceNear("PF_Rock_Tall_01", _gRocks, new Vector2(-40f, -2.4f), 0.5f, Cat.Rock, 0.9f, 1.1f);
            PlaceNear("PF_Rock_Tall_01", _gRocks, new Vector2(-40.4f, 16.2f), 0.5f, Cat.Rock, 0.8f, 1f);
        }

        private static void DressStartArea()
        {
            // Calm and open around the base: the landmark tree at the first bend, a few rocks, barrels/crates, tools.
            Place("PF_Tree_Broadleaf_02", _gTrees, BendTreeXZ, 30f, 1.55f, Cat.Tree, true);
            PlaceNear("PF_Bush_Round_01", _gBushes, BendTreeXZ + new Vector2(-1.2f, -2f), 0.4f, Cat.Bush);
            FlowerPatch(BendTreeXZ + new Vector2(1.6f, -1.8f), 4, 0.9f);
            PlaceNear("PF_Rock_01", _gRocks, new Vector2(37.2f, -4.6f), 0.4f, Cat.Rock, 1.5f, 1.8f);
            PlaceNear("PF_Rock_02", _gRocks, new Vector2(36.8f, -2.8f), 0.4f, Cat.Rock, 1.1f, 1.3f);
            PlaceNear("PF_Stones_01", _gRocks, new Vector2(35.6f, -4.2f), 0.4f, Cat.Rock);
            GrassPatch(new Vector2(36.6f, -3.8f), 4, 2f);
            PlaceNear("PF_Barrel", _gBarrels, new Vector2(27.2f, 12.4f), 0.3f, Cat.Prop);
            PlaceNear("PF_Barrel_02", _gBarrels, new Vector2(28f, 12.9f), 0.3f, Cat.Prop);
            PlaceNear("PF_Crate", _gCrates, new Vector2(26.4f, 12.9f), 0.3f, Cat.Prop);
            PlaceNear("PF_WateringCan_02", _gTools, new Vector2(24.6f, -3.9f), 0.3f, Cat.Prop);
            PlaceNear("PF_Wheelbarrow", _gTools, new Vector2(25.6f, -4.6f), 0.3f, Cat.Prop, 1f, 1f, 4, 150f);
            PlaceNear("PF_Hay_Bale", _gMisc, new Vector2(18.6f, -11.8f), 0.3f, Cat.Prop);
            FlowerPatch(new Vector2(23.8f, 11.8f), 3, 0.8f);
            FlowerPatch(new Vector2(35.4f, 7.6f), 3, 0.8f);
        }

        private static void PlaceVehicles()
        {
            Place("PF_Vehicle_PickupBlue", _gVehicles, new Vector2(17.8f, 18.2f), 160f, 1f, Cat.Vehicle, true);
            Place("PF_Vehicle_Van", _gVehicles, new Vector2(15.8f, -10.8f), 225f, 1f, Cat.Vehicle, true);
            Place("PF_Vehicle_PickupOld", _gVehicles, new Vector2(-38.8f, -6.8f), 35f, 1f, Cat.Vehicle, true);
            Place("PF_Vehicle_TowTruck", _gVehicles, new Vector2(25.4f, 15.6f), 170f, 1f, Cat.Vehicle, true);

            // A scooter driving the farm-village loop road now and then (not absorbable: it moves).
            if (Kit.TryGetValue("PF_Vehicle_Scooter", out GameObject scooter))
            {
                var go = (GameObject)PrefabUtility.InstantiatePrefab(scooter, _gVehicles);
                go.name = "Scooter_Ambient";
                List<Vector2> loop = _sideRoadSamples[0];
                var route = new List<Vector3>();
                for (int i = 0; i < loop.Count; i += 4)
                {
                    route.Add(new Vector3(loop[i].x, 0f, loop[i].y));
                }

                int end = route.Count - 1;
                for (int i = loop.Count - 1; i >= 0; i -= 4)
                {
                    route.Add(new Vector3(loop[i].x, 0f, loop[i].y));
                }

                var mover = go.AddComponent<AmbientRouteMover>();
                mover.Configure(route.ToArray(), 3.2f, new[] { 0, end }, 0f);
                go.transform.position = new Vector3(route[0].x, TerrainY(new Vector2(route[0].x, route[0].z)), route[0].z);
                foreach (Renderer r in go.GetComponentsInChildren<Renderer>())
                {
                    r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                }

                PlacedCount[Cat.Vehicle] = Count(Cat.Vehicle) + 1;
            }
        }

        /// <summary>The rims of the open plazas/yards get small clusters (flowers + grass, a barrel or crate, a bush)
        /// so the clearing reads as a village square rather than a bare patch - the inside stays clear.</summary>
        private static void DressClearingRims()
        {
            foreach (Clearing c in Clearings)
            {
                if (c.Pad || (!c.Movement && c.Kind == GroundKind.DryGrass))
                {
                    continue;
                }

                int spots = Mathf.RoundToInt((c.Radii.x + c.Radii.y) * 1.1f);
                for (int i = 0; i < spots; i++)
                {
                    float a = (i + Rand(-0.3f, 0.3f)) / spots * Mathf.PI * 2f;
                    var local = new Vector2(Mathf.Cos(a) * c.Radii.x, Mathf.Sin(a) * c.Radii.y) * Rand(1.08f, 1.25f);
                    Vector2 p = c.Center + Rotate(local, c.Yaw);
                    float roll = (float)_rng.NextDouble();
                    if (roll < 0.55f)
                    {
                        FlowerPatch(p, 2 + _rng.Next(2), 0.6f);
                        GrassPatch(p, 2, 0.8f);
                    }
                    else if (roll < 0.8f)
                    {
                        PlaceNear(Pick("PF_Barrel", "PF_Crate", "PF_Crate_Open", "PF_Sack_01", "PF_Bucket", "PF_Pumpkin", "PF_Hay_Small"), _gMisc, p, 0.5f, Cat.Prop, 0.9f, 1.1f, 4);
                        GrassPatch(p, 1, 0.8f);
                    }
                    else
                    {
                        PlaceNear(Pick(BushKinds), _gBushes, p, 0.6f, Cat.Bush, 0.8f, 1.05f, 4);
                    }
                }
            }
        }

        /// <summary>Weed + rock + flower clusters along every road edge, alternating sides, irregular spacing;
        /// plus tiny stones on the dirt shoulders.</summary>
        private static void DressRoadEdges()
        {
            var roads = new List<List<Vector2>> { _mainRoadSamples };
            roads.AddRange(_sideRoadSamples);
            for (int r = 0; r < roads.Count; r++)
            {
                List<Vector2> line = roads[r];
                float half = (r == 0 ? MainRoadWidth : SideRoadWidth) * 0.5f;
                float next = Rand(1.5f, 3.5f);
                float travelled = 0f;
                float side = Chance(0.5f) ? 1f : -1f;
                for (int i = 1; i < line.Count; i++)
                {
                    travelled += Vector2.Distance(line[i - 1], line[i]);
                    if (travelled < next)
                    {
                        continue;
                    }

                    travelled = 0f;
                    next = Rand(2.8f, 5.5f);
                    side = Chance(0.7f) ? -side : side;
                    Vector2 tangent = (line[i] - line[i - 1]).normalized;
                    Vector2 normal = new Vector2(-tangent.y, tangent.x) * side;
                    Vector2 edge = line[i] + normal * (half + Rand(0.3f, 0.9f));
                    PlaceNear(Pick(WeedKinds), _gFlowers, edge, 0.4f, Cat.Flower, 0.9f, 1.2f, 3);
                    PlaceNear(Pick(GrassKinds), _gGrass, edge + normal * 0.5f, 0.5f, Cat.Grass, 0.9f, 1.2f, 3);
                    if (Chance(0.5f))
                    {
                        PlaceNear(Pick("PF_Stones_01", "PF_Stones_02", "PF_Rock_03"), _gRoadEdge, edge + normal * 0.7f + tangent * 0.6f, 0.3f, Cat.Rock, 0.6f, 0.9f, 3);
                    }

                    if (Chance(0.55f))
                    {
                        FlowerPatch(edge + normal * 0.9f - tangent * 0.7f, 1 + _rng.Next(3), 0.5f);
                    }
                }
            }
        }

        /// <summary>Single trees and pairs a few metres off the roads, alternating sides at irregular intervals, so a
        /// view down any road has trees framing it (the occlusion rule keeps them off the camera side of the road).</summary>
        private static void PlaceRoadsideTrees()
        {
            var roads = new List<List<Vector2>> { _mainRoadSamples };
            roads.AddRange(_sideRoadSamples);
            for (int r = 0; r < roads.Count; r++)
            {
                List<Vector2> line = roads[r];
                float half = (r == 0 ? MainRoadWidth : SideRoadWidth) * 0.5f;
                float next = Rand(3f, 6f);
                float travelled = 0f;
                for (int i = 1; i < line.Count; i++)
                {
                    travelled += Vector2.Distance(line[i - 1], line[i]);
                    if (travelled < next)
                    {
                        continue;
                    }

                    travelled = 0f;
                    next = Rand(5f, 9f);
                    Vector2 tangent = (line[i] - line[i - 1]).normalized;
                    Vector2 normal = new Vector2(-tangent.y, tangent.x) * (Chance(0.5f) ? 1f : -1f);
                    float north = Mathf.InverseLerp(MaxX, MinX, line[i].x);
                    string kind = north > 0.8f && Chance(0.5f) ? Pick(Pines) : Chance(0.35f) ? Pick(AutumnTrees) : Pick(GreenTrees);
                    Vector2 spot = line[i] + normal * (half + Rand(2.4f, 4.2f));
                    if (PlaceNear(kind, _gTrees, spot, 0.8f, Cat.Tree, 0.85f, 1.15f, 5) != null)
                    {
                        PlaceNear(Pick(BushKinds), _gBushes, spot + normal * 1.4f + tangent * Rand(-1f, 1f), 0.5f, Cat.Bush, 0.8f, 1.05f, 4);
                        FlowerPatch(spot - normal * 1.2f, 2, 0.5f);
                    }
                }
            }
        }

        /// <summary>Tree clusters along the long map edges (the foot of the cliffs) and between zones.</summary>
        private static void PlaceTreeBelts()
        {
            for (float x = MinX + 3f; x < MaxX - 2f; x += Rand(6f, 9f))
            {
                foreach (float side in new[] { -1f, 1f })
                {
                    float z = side * Rand(MaxZ - 3.5f, MaxZ - 0.8f);
                    bool north = x < MinX + 6f;
                    TreeCluster(new Vector2(x, z), 2 + _rng.Next(3), north ? 0.15f : x < 0f ? 0.4f : 0.25f, north || Chance(0.3f));
                }
            }

            // Trees standing just outside the playable edge, at the cliff foot (not absorbable, static).
            for (float x = RimMinX + 4f; x < RimMaxX - 3f; x += Rand(3.5f, 6f))
            {
                foreach (float side in new[] { -1f, 1f })
                {
                    var p = new Vector2(x, side * Rand(RimMaxZ - 2.2f, RimMaxZ - 1f));
                    string kind = x < MinX - 4f ? Pick(Pines) : Chance(0.3f) ? Pick(AutumnTrees) : Chance(0.4f) ? Pick(Pines) : Pick(GreenTrees);
                    Place(kind, _gTrees, p, Rand(0f, 360f), Rand(0.9f, 1.25f), Cat.Tree);
                    if (Chance(0.6f))
                    {
                        Place(Pick(BushKinds), _gBushes, p + new Vector2(Rand(-1.5f, 1.5f), -side * Rand(0.8f, 1.6f)), Rand(0f, 360f), Rand(0.9f, 1.2f), Cat.Bush);
                    }
                }
            }

            for (float z = MinZ + 4f; z <= MaxZ - 4f; z += Rand(5f, 7f))
            {
                // The barn courtyard and the base clearing are movement clearings: the placement rules keep these
                // rows out of them, so no explicit gap is needed here.
                TreeCluster(new Vector2(MaxX - 1.5f, z), 2, 0.2f);
                TreeCluster(new Vector2(MinX + 1.5f, z), 2, 0.1f, true);
            }
        }

        /// <summary>Tops up every 5 m cell of grass that ended up empty: a flower/grass tuft cluster everywhere,
        /// plus a bush, rock or tree where there is room. Density grows toward the north end.</summary>
        private static void DensityPass()
        {
            const float cell = 4f;
            for (float x = MinX + cell * 0.5f; x < MaxX; x += cell)
            {
                for (float z = MinZ + cell * 0.5f; z < MaxZ; z += cell)
                {
                    var c = new Vector2(x + Rand(-1.5f, 1.5f), z + Rand(-1.5f, 1.5f));
                    if (RoadEdge(c) < 1f)
                    {
                        continue;
                    }

                    float north = Mathf.InverseLerp(MaxX, MinX, x); // 0 at the start, 1 at the enemy barn
                    GrassPatch(c, 2 + _rng.Next(2), 1.6f);
                    if (Chance(0.55f + north * 0.2f))
                    {
                        FlowerPatch(c + Random2(1.5f), 1 + _rng.Next(3), 0.6f);
                    }

                    if (Chance(0.3f + north * 0.25f))
                    {
                        PlaceNear(Chance(0.6f) ? Pick(BushKinds) : Pick(RockKinds), Chance(0.6f) ? _gBushes : _gRocks, c, 1.5f,
                            Chance(0.6f) ? Cat.Bush : Cat.Rock, 0.7f, 1.05f, 4);
                    }

                    if (Chance(0.22f + north * 0.2f) && RoadEdge(c) > 3f)
                    {
                        PlaceNear(Chance(0.3f) ? Pick(AutumnTrees) : Pick(GreenTrees), _gTrees, c, 1.5f, Cat.Tree, 0.85f, 1.15f, 5);
                    }
                }
            }
        }

        private static void DressCliffs(List<LedgeSpot> ledges)
        {
            foreach (LedgeSpot s in ledges)
            {
                Vector2 p = new Vector2(s.Position.x, s.Position.z);
                float roll = (float)_rng.NextDouble();
                string kind;
                if (s.Tier == 1)
                {
                    kind = roll < 0.35f ? Pick(BushKinds) : roll < 0.55f ? Pick(RockKinds) : roll < 0.75f ? Pick(GreenTrees) : null;
                }
                else if (s.Tier == 2)
                {
                    kind = roll < 0.45f ? (Chance(0.5f) ? Pick(Pines) : Chance(0.4f) ? Pick(AutumnTrees) : Pick(GreenTrees)) : roll < 0.7f ? Pick(BushKinds) : null;
                }
                else
                {
                    kind = roll < 0.72f ? (Chance(0.55f) ? Pick(Pines) : Chance(0.35f) ? Pick(AutumnTrees) : Pick(GreenTrees)) : roll < 0.85f ? Pick(BushKinds) : null;
                }

                if (kind == null || !Kit.TryGetValue(kind, out GameObject prefab))
                {
                    continue;
                }

                var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, _gCliffTrees);
                go.transform.SetPositionAndRotation(s.Position + Vector3.down * 0.15f, Quaternion.Euler(0f, Rand(0f, 360f), 0f));
                go.transform.localScale = Vector3.one * Rand(0.9f, 1.3f);
                foreach (Renderer r in go.GetComponentsInChildren<Renderer>())
                {
                    r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    r.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
                    r.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
                }

                foreach (TractorAbsorbableProp prop in go.GetComponentsInChildren<TractorAbsorbableProp>(true))
                {
                    Object.DestroyImmediate(prop); // out of reach on the cliffs: static scenery
                }

                foreach (Transform t in go.GetComponentsInChildren<Transform>(true))
                {
                    GameObjectUtility.SetStaticEditorFlags(t.gameObject, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic);
                }

                PlacedCount[Cat.CliffDressing] = Count(Cat.CliffDressing) + 1;
            }
        }
    }
}
