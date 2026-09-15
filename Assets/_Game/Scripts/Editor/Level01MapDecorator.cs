using System;
using System.Collections.Generic;
using AlienDefense.Building;
using AlienDefense.Enemies;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;
using Random = System.Random;

namespace AlienDefense.EditorTools
{
    /// <summary>Dresses Level_01 with designed prop clusters (farm yards, crop fields, storage yards, orchards,
    /// rock/shrub corners, flower beds, roadside dressing and dense map-edge belts) built only from prefabs already
    /// in the project. Every placement goes through clearance masks - the enemy road (path polyline plus the dirt
    /// terrain layer), the river, BuildNodes, the player base, the UFO landing pad, the enemy spawn - an occupancy
    /// grid seeded from everything already in the level, a slope check, and for tall props a camera-occlusion check
    /// against the road/BuildNodes (the gameplay camera looks down the -X axis, so a tall prop hides what lies at
    /// lower X behind it). Decorative instances lose their colliders (nothing may block the UFO, the tractor beam's
    /// ground probes or enemies), tiny props stop casting shadows, and structural props are batching-static.
    ///
    /// Output lives under Maps/Environment/Decoration, one child per zone; re-running rebuilds it from the same
    /// seed, so the layout is stable. Prefab source assets are never modified.</summary>
    public static class Level01MapDecorator
    {
        /// <summary>Debug: builds the masks (no decoration root changes) and tries one placement under a temporary
        /// parent, returning "ok" or the rejection reason. Size: 0 tiny, 1 small, 2 medium, 3 large, 4 tree.</summary>
        public static string DebugProbe(string prefab, float x, float z, int size)
        {
            _rng = new Random(Seed);
            RejectReasons.Clear();
            if (!BuildMasks())
            {
                return "no masks";
            }

            var temp = new GameObject("TmpProbe").transform;
            GameObject go = Place(temp, prefab, new Vector2(x, z), 0f, 1f, (Size)size);
            string reason = go != null ? "ok" : string.Join(",", RejectReasons.Keys);
            Object.DestroyImmediate(temp.gameObject);
            _placed = 0;
            return reason;
        }

        private const string ScenePath = "Assets/_Game/Scenes/Levels/Level_01.unity";
        private const int Seed = 20260915;

        private const string PZ = "Assets/Pandazole_Ultimate_Pack/Pandazole Farm Ranch Pack/Prefabs/";
        private const string PE = "Assets/Polytope Studio/Lowpoly_Environments/Prefabs/";
        private const string PV = "Assets/Polytope Studio/Lowpoly_Village/Prefabs/Modular/";
        private const string GR = "Assets/Gridness Studios/Lite Farm Pack/Prefabs/";
        private const string LF = "Assets/LowPolyFarmLite/Prefabs/";

        // ------------------------------------------------------------------ prefab palettes

        private static readonly string[] Houses = { PZ + "Bld_FarmerHouse" };
        private static readonly string[] Barns = { PZ + "Bld_Barn_01", PZ + "Bld_Barn_02" };
        private static readonly string[] Stores = { PZ + "Bld_StoreBuilding_01" };
        private static readonly string[] Silos = { PZ + "Bld_Silo_01", PZ + "Bld_Silo_02" };
        private static readonly string[] Mills = { PZ + "Bld_FarmMill_01", PZ + "Bld_FarmMill_02" };
        private static readonly string[] Coops = { PZ + "Bld_ChickenCoop", PZ + "Bld_Outhouses" };

        private static readonly string[] FruitTrees = { PE + "Trees/PT_Fruit_Tree_01_apples", PE + "Trees/PT_Fruit_Tree_01_pears", PE + "Trees/PT_Fruit_Tree_01_plums", PE + "Trees/PT_Fruit_Tree_01_green" };
        private static readonly string[] Pines = { PE + "Trees/PT_Pine_Tree_03_green" };
        private static readonly string[] ToonTrees = { PZ + "Env_Tree_01", PZ + "Env_Tree_02", PZ + "Env_Tree_03", PZ + "Env_Tree_04", PZ + "Env_Tree_05", PZ + "Env_Tree_06", PZ + "Env_Tree_07", LF + "Tree_04" };
        private static readonly string[] Bushes = { PZ + "Env_Bush_01", PZ + "Env_Bush_02", PE + "Shrubs/PT_Generic_Shrub_01_green" };
        private static readonly string[] BigRocks = { PE + "Rocks/PT_Menhir_Rock_02", LF + "Rock_04" };
        private static readonly string[] SmallRocks = { PE + "Rocks/PT_River_Rock_Pile_02", PE + "Rocks/PT_Generic_Rock_01" };
        private static readonly string[] Logs = { PE + "Trees/PT_Fruit_Tree_01_logs", PE + "Trees/PT_Pine_Tree_03_logs", PE + "Trees/PT_Fruit_Tree_01_stump" };

        private static readonly string[] Tulips = { PZ + "Env_GrassPlant_02" };
        private static readonly string[] Poppies = { PE + "Flowers/PT_Poppy_02" };
        private static readonly string[] Daisies = { GR + "Flower" };
        private static readonly string[] Grass = { PE + "Plants/PT_Grass_02", GR + "Grass", PZ + "Env_GrassPlant_05", PZ + "Env_GrassPlant_07", PZ + "Env_GrassPlant_06", PZ + "Env_GrassPlant_01" };
        private static readonly string[] Mushrooms = { PE + "Mushrooms/PT_Caesars_Mushroom_01" };

        private static readonly string[] Barrels = { PZ + "Prop_Berrel_01.001", PZ + "Prop_Berrel_02_A", PZ + "Prop_Berrel_03", PZ + "Prop_Berrel_04", PZ + "Prop_Berrel_05_A" };
        private static readonly string[] MetalBarrels = { PZ + "Prop_MetalBerrel_01" };
        private static readonly string[] Crates = { PZ + "Prop_WoodenBox_01", PZ + "Prop_WoodenBox_02", PZ + "Prop_WoodenBox_03", PZ + "Prop_WoodenCrates_01", PZ + "Prop_WoodenCrates_02", GR + "Crate" };
        private static readonly string[] LongCrates = { PZ + "Prop_WoodenBox_04", PZ + "Prop_WoodenBox_05", PZ + "Prop_WoodenChest_01", PZ + "Prop_WoodenChest_02" };
        private static readonly string[] Sacks = { PZ + "Prop_FoodSack_01", PZ + "Prop_FoodSack_02", PZ + "Prop_FoodSack_03", PZ + "Prop_FoodSack_04" };
        private static readonly string[] Hay = { PZ + "Prop_Haystack_01", PZ + "Prop_Haystack_02", PZ + "Prop_Haystack_04", PZ + "Prop_Haystack_05" };
        private static readonly string[] Pallets = { PZ + "Prop_Pallete_01", PZ + "Prop_Pallete_02" };
        private static readonly string[] Buckets = { PZ + "Prop_Bucket_01", PZ + "Prop_Bucket_03", PZ + "Prop_Bucket_04" };
        private static readonly string[] WateringCans = { PZ + "Prop_WateringCan_01", PZ + "Prop_WateringCan_02", PZ + "Prop_WateringCan_03", GR + "WaterCan" };
        private static readonly string[] Tools = { PZ + "Prop_BigFarmingTool_01", PZ + "Prop_BigFarmingTool_02", PZ + "Prop_BigFarmingTool_03", PZ + "Prop_BigFarmingTool_04", GR + "Harrow" };
        private static readonly string[] Produce = { PZ + "food_Pumpkin", PZ + "food_Watermelon", LF + "Cabbage_01", GR + "Tomato_Crate" };
        private static readonly string[] Wells = { PZ + "Env_Well_01", PZ + "Env_Well_02" };
        private static readonly string[] Feeders = { PZ + "Prop_AnimalFeeder_01", PZ + "Prop_AnimalFeeder_02" };
        private static readonly string[] Wheelbarrows = { PZ + "Prop_Wheelbarrow" };
        private static readonly string[] Plots = { PZ + "Env_FarmLand_03", PZ + "Env_FarmLand_04", PZ + "Env_FarmLand_05", PZ + "Env_FarmLand_05_Watered", PZ + "Env_FarmLand_07", PZ + "Env_FarmLand_09", PZ + "Env_FarmLand_10", PZ + "Env_FarmLand_03_Watered" };
        private static readonly string[] Crops = { GR + "Plant_Tomato_Medium", GR + "Plant_Tomato_Large", GR + "Plant_Medium", LF + "Cabbage_01", PZ + "Env_Wheat" };
        private static readonly string[] WoodFences = { PZ + "Env_WoodFence_02", PZ + "Env_WoodFence_03", PZ + "Env_WoodFence_04" };
        private static readonly string[] PicketFences = { PV + "Fence/PT_Modular_Fence_Wood_01", PV + "Fence/PT_Modular_Fence_Wood_02" };

        // ------------------------------------------------------------------ placement classes

        private enum Size { Tiny, Small, Medium, Large, Tree }

        // Source art scale vs this level: the Polytope fruit/pine trees are authored far bigger than the farm props
        // and the enemies (a 7-9 m tree would fill the whole portrait view).
        private static float BaseScale(string prefab)
        {
            if (prefab.Contains("PT_Fruit_Tree_01_logs") || prefab.Contains("stump"))
            {
                return 0.8f;
            }

            if (prefab.Contains("PT_Fruit_Tree_01"))
            {
                return 0.6f;
            }

            return prefab.Contains("PT_Pine_Tree_03") ? 0.6f : 1f;
        }

        private static bool IsTree(string prefab) => prefab.Contains("Tree") && !prefab.Contains("_logs") && !prefab.Contains("stump");

        private sealed class Rules
        {
            public float Road;       // metres from the path centre line
            public float Node;       // metres from a BuildNode
            public float Base;       // metres from the player base
            public float Landing;    // metres from the UFO landing pad
            public float Spawn;      // metres from the enemy spawn point
            public float Padding;    // occupancy padding around the footprint
            public float MaxSlope;   // max ground height difference across the footprint
            public bool CastShadows;
            public bool Static;
        }

        private static readonly Dictionary<Size, Rules> RuleSet = new Dictionary<Size, Rules>
        {
            [Size.Tiny] = new Rules { Road = 4.6f, Node = 3.2f, Base = 7f, Landing = 5.5f, Spawn = 7f, Padding = 0f, MaxSlope = 1.2f, CastShadows = false, Static = false },
            [Size.Small] = new Rules { Road = 5.0f, Node = 4.5f, Base = 8f, Landing = 6.5f, Spawn = 8f, Padding = 0.05f, MaxSlope = 0.9f, CastShadows = false, Static = true },
            [Size.Medium] = new Rules { Road = 5.6f, Node = 5.5f, Base = 9f, Landing = 8f, Spawn = 9f, Padding = 0.1f, MaxSlope = 0.8f, CastShadows = true, Static = true },
            [Size.Large] = new Rules { Road = 9f, Node = 8f, Base = 13f, Landing = 11f, Spawn = 12f, Padding = 0.4f, MaxSlope = 1.2f, CastShadows = true, Static = true },
            [Size.Tree] = new Rules { Road = 7.5f, Node = 6f, Base = 10f, Landing = 9f, Spawn = 9f, Padding = 0.2f, MaxSlope = 2f, CastShadows = true, Static = true },
        };

        // ------------------------------------------------------------------ state

        private static Random _rng;
        private static readonly Dictionary<string, GameObject> PrefabCache = new Dictionary<string, GameObject>();
        private static readonly List<Vector2> PathXZ = new List<Vector2>();
        private static readonly List<Vector2> NodeXZ = new List<Vector2>();
        private static Vector2 _baseXZ, _landingXZ, _spawnXZ;
        private static Terrain[] _terrains;
        private static readonly Dictionary<Terrain, float[,,]> Alphas = new Dictionary<Terrain, float[,,]>();

        private const float GridMinX = -85f, GridMinZ = 18f, Cell = 0.5f;
        private const int GridW = 360, GridH = 210;
        private static bool[] _occupied;
        private static bool[] _water;

        private static int _placed;
        private static int _rejected;
        private static readonly Dictionary<string, int> RejectReasons = new Dictionary<string, int>();

        private static void Reject(string reason)
        {
            _rejected++;
            RejectReasons[reason] = RejectReasons.TryGetValue(reason, out int n) ? n + 1 : 1;
        }
        private static readonly Dictionary<Size, int> PlacedBySize = new Dictionary<Size, int>();

        // ------------------------------------------------------------------ entry point

        [MenuItem("AlienDefense/Setup/Level 01/Decorate Map")]
        public static void Run()
        {
            if (EditorSceneManager.GetActiveScene().path != ScenePath)
            {
                Debug.LogError("[Level01MapDecorator] Open Level_01 first.");
                return;
            }

            GameObject environment = GameObject.Find("Maps/Environment");
            if (environment == null)
            {
                Debug.LogError("[Level01MapDecorator] Maps/Environment not found.");
                return;
            }

            Transform old = environment.transform.Find("Decoration");
            if (old != null)
            {
                Object.DestroyImmediate(old.gameObject);
            }

            _rng = new Random(Seed);
            _placed = 0;
            _rejected = 0;
            RejectReasons.Clear();
            PlacedBySize.Clear();
            PrefabCache.Clear();

            if (!BuildMasks())
            {
                return;
            }

            var root = new GameObject("Decoration").transform;
            root.SetParent(environment.transform, false);

            DecorateZones(root);

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            string bySize = string.Join(", ", PlacedBySize);
            Debug.Log($"[Level01MapDecorator] Placed {_placed} props ({bySize}); {_rejected} candidate spots rejected ({string.Join(", ", RejectReasons)}).");
        }

        // ------------------------------------------------------------------ zone layout (designed, not scattered)

        private static void DecorateZones(Transform root)
        {
            // Terrain reading (Level_01): the centre (X -26..26) is a hill, the rim is mountain, and the flat ground is
            // the north end around the spawn (X -58..-34) and the south end around the base (X 38..62, Z 42..78).
            // Farms and buildings go on the flats; the hill and the rim get orchards, tree groups and rock outcrops.

            // Zone A - west hillside (left of the road across the central hill).
            Transform zoneA = Zone(root, "Zone_A_WestHill");
            Orchard(zoneA, new Vector2(8f, 47f), 5);
            TreeGroup(zoneA, new Vector2(-12f, 49f));
            TreeGroup(zoneA, new Vector2(21f, 44f));
            RockCorner(zoneA, new Vector2(0f, 41f));
            RockCorner(zoneA, new Vector2(-21f, 43f));
            FlowerBed(zoneA, new Vector2(-6f, 55f), 12, 3.2f);
            FlowerBed(zoneA, new Vector2(14f, 56f), 10, 3f);
            HayCorner(zoneA, new Vector2(-26f, 55f));

            // Zone B - east hillside and village yard (right of the road, north of the dark barn).
            Transform zoneB = Zone(root, "Zone_B_EastHill");
            FarmYardIn(zoneB, new Rect(-32f, 86f, 16f, 20f), new Vector2(-24f, 100f));
            AnimalPen(zoneB, new Vector2(-27f, 90f));
            Orchard(zoneB, new Vector2(-6f, 96f), 5);
            TreeGroup(zoneB, new Vector2(-20f, 82f));
            TreeGroup(zoneB, new Vector2(10f, 101f));
            RockCorner(zoneB, new Vector2(4f, 82f));
            FlowerBed(zoneB, new Vector2(-12f, 78f), 14, 3.5f);
            FlowerBed(zoneB, new Vector2(12f, 80f), 10, 3f);

            // Zone C - orchard fringe between the dark barn and the river.
            Transform zoneC = Zone(root, "Zone_C_Orchard");
            Orchard(zoneC, new Vector2(20f, 90f), 4);
            RockCorner(zoneC, new Vector2(24f, 101f));
            FlowerBed(zoneC, new Vector2(16f, 78f), 10, 3f);

            // Zone D - south flats beyond the river (farm field + barn yard), mountain behind.
            Transform zoneD = Zone(root, "Zone_D_SouthFarm");
            StorageYardIn(zoneD, new Rect(38f, 64f, 28f, 18f), new Vector2(48f, 78f), Barns);
            FieldIn(zoneD, new Rect(38f, 64f, 28f, 18f), new Vector2(60f, 75f), 2, 2, 0f);
            SiloIn(zoneD, new Rect(36f, 34f, 30f, 26f), new Vector2(62f, 45f));
            MillIn(zoneD, new Rect(36f, 34f, 30f, 26f), new Vector2(40f, 44f));
            TreeGroup(zoneD, new Vector2(50f, 88f));
            RockCorner(zoneD, new Vector2(66f, 84f));

            // Zone E - base outskirts (props only; the base itself stays readable).
            Transform zoneE = Zone(root, "Zone_E_BaseOutskirts");
            PropCluster(zoneE, new Vector2(58f, 53f));
            PropCluster(zoneE, new Vector2(40f, 58f));
            FlowerBed(zoneE, new Vector2(52f, 74f), 10, 2.8f);
            FlowerBed(zoneE, new Vector2(66f, 54f), 8, 2.5f);
            HayCorner(zoneE, new Vector2(42f, 77f));

            // Zone F - spawn outskirts (north end of the road).
            Transform zoneF = Zone(root, "Zone_F_SpawnFlats");
            FieldIn(zoneF, new Rect(-62f, 84f, 24f, 22f), new Vector2(-44f, 96f), 2, 2, 90f);
            StorageYardIn(zoneF, new Rect(-62f, 34f, 30f, 32f), new Vector2(-36f, 40f), Stores);
            FarmYardIn(zoneF, new Rect(-62f, 34f, 30f, 32f), new Vector2(-50f, 40f));
            MillIn(zoneF, new Rect(-62f, 84f, 24f, 22f), new Vector2(-56f, 100f));
            FieldIn(zoneF, new Rect(-62f, 34f, 30f, 32f), new Vector2(-40f, 56f), 1, 2, 0f);
            RockCorner(zoneF, new Vector2(-58f, 72f));
            TreeGroup(zoneF, new Vector2(-38f, 88f));
            FlowerBed(zoneF, new Vector2(-32f, 58f), 12, 3f);
            HayCorner(zoneF, new Vector2(-54f, 104f));
            PropCluster(zoneF, new Vector2(-38f, 62f));

            // Zone G - river banks (away from the bridge).
            Transform zoneG = Zone(root, "Zone_G_Riverbanks");
            RockCorner(zoneG, new Vector2(25f, 46f));
            RockCorner(zoneG, new Vector2(42f, 88f));
            FlowerBed(zoneG, new Vector2(24f, 58f), 10, 2.8f);
            FlowerBed(zoneG, new Vector2(43f, 97f), 10, 2.8f);

            // Roadside dressing along the whole road, both sides.
            Roadside(Zone(root, "Zone_Roadside"));

            // Dense natural boundary on every map edge.
            Transform edges = Zone(root, "Zone_MapEdge");
            ForestBelt(edges, new Vector2(-66f, 31f), new Vector2(26f, 31f), 5f);
            ForestBelt(edges, new Vector2(40f, 31f), new Vector2(72f, 31f), 5f);
            ForestBelt(edges, new Vector2(-66f, 108f), new Vector2(26f, 108f), 5f);
            ForestBelt(edges, new Vector2(42f, 108f), new Vector2(74f, 108f), 5f);
            ForestBelt(edges, new Vector2(-70f, 36f), new Vector2(-70f, 104f), 5f);
            ForestBelt(edges, new Vector2(72f, 68f), new Vector2(72f, 104f), 4f);

            // Last: sweep the map in 9 m cells and give every still-bare cell a small cluster suited to its ground.
            FillPass(Zone(root, "Zone_Fill"));
        }

        /// <summary>Controlled fill (not blind scatter): a jittered 9 m lattice over the playable bounds. A cell is
        /// dressed only if it is still visually bare (little occupied ground within 4.5 m), outside every gameplay
        /// mask, and gets a recipe chosen from what is there - hillside or rim: trees, bushes, rocks; flats far from
        /// the road: small farm vignettes; flats near the road: low props and flowers only.</summary>
        private static void FillPass(Transform zone)
        {
            int index = 0;
            for (float x = -68f; x <= 70f; x += 9f)
            {
                for (float z = 33f; z <= 107f; z += 9f, index++)
                {
                    Vector2 p = new Vector2(x + Jit(2.5f), z + Jit(2.5f));
                    float road = DistanceToPath(p);
                    if (road < 8f || float.IsNaN(GroundHeight(p)) || OccupiedFraction(p, 4.5f) > 0.22f || WaterNear(p, 3f))
                    {
                        continue;
                    }

                    bool rim = p.x < -60f || p.x > 64f || p.y < 38f || p.y > 102f;
                    bool hill = LocalRelief(p, 3f) > 1.1f;
                    Transform c = Cluster(zone, "Fill");
                    if (rim || hill)
                    {
                        int trees = rim ? 2 : 1;
                        for (int i = 0; i < trees; i++)
                        {
                            PlaceNear(c, Pick(i == 0 && rim ? Pines : _rng.NextDouble() < 0.5 ? FruitTrees : ToonTrees), p + Circle(2.5f), Yaw(), Scale(), Size.Large, 2f);
                        }

                        PlaceNear(c, Pick(Bushes), p + Circle(3f), Yaw(), Scale(), Size.Small, 1.5f);
                        PlaceNear(c, Pick(Bushes), p + Circle(3f), Yaw(), Scale(), Size.Small, 1.5f);
                        PlaceNear(c, Pick(_rng.NextDouble() < 0.6 ? BigRocks : Logs), p + Circle(3.5f), Yaw(), Scale(), Size.Medium, 1.5f);
                        Patch(c, p + Circle(3f), 5, 1.6f, _rng.NextDouble() < 0.5 ? Poppies : Tulips);
                        Patch(c, p + Circle(3f), 4, 1.6f, Grass);
                        if (_rng.NextDouble() < 0.35)
                        {
                            Patch(c, p + Circle(2f), 3, 0.8f, Mushrooms);
                        }
                    }
                    else if (road < 16f)
                    {
                        switch (index % 3)
                        {
                            case 0: FlowerBed(c, p, 10, 2.6f); break;
                            case 1: RockCorner(c, p); break;
                            default: PropCluster(c, p); break;
                        }
                    }
                    else
                    {
                        switch (index % 5)
                        {
                            case 0: HayCorner(c, p); FlowerBed(c, p + new Vector2(0f, 4f), 8, 2f); break;
                            case 1: Field(c, p, 1, 2, 90f * _rng.Next(2)); break;
                            case 2: TreeGroup(c, p); break;
                            case 3:
                                PlaceNear(c, Pick(Coops), p, Yaw(), 1f, Size.Large, 2f);
                                FenceRun(c, p + new Vector2(-4f, -3.5f), p + new Vector2(4f, -3.5f), WoodFences, 0.2f);
                                PropStack(c, p + new Vector2(3f, 2f));
                                Patch(c, p + new Vector2(-2.5f, 2.5f), 6, 1.5f, Daisies);
                                break;
                            default: PropCluster(c, p); RockCorner(c, p + new Vector2(4f, -3f)); break;
                        }
                    }
                }
            }
        }

        private static float OccupiedFraction(Vector2 p, float radius)
        {
            int total = 0, occupied = 0;
            int r = Mathf.CeilToInt(radius / Cell);
            int cx = Mathf.FloorToInt((p.x - GridMinX) / Cell), cz = Mathf.FloorToInt((p.y - GridMinZ) / Cell);
            for (int dz = -r; dz <= r; dz++)
            {
                for (int dx = -r; dx <= r; dx++)
                {
                    int x = cx + dx, z = cz + dz;
                    if (x < 0 || z < 0 || x >= GridW || z >= GridH || dx * dx + dz * dz > r * r)
                    {
                        continue;
                    }

                    total++;
                    if (_occupied[z * GridW + x])
                    {
                        occupied++;
                    }
                }
            }

            return total > 0 ? (float)occupied / total : 1f;
        }

        private static bool WaterNear(Vector2 p, float radius)
        {
            int r = Mathf.CeilToInt(radius / Cell);
            int cx = Mathf.FloorToInt((p.x - GridMinX) / Cell), cz = Mathf.FloorToInt((p.y - GridMinZ) / Cell);
            for (int dz = -r; dz <= r; dz += 2)
            {
                for (int dx = -r; dx <= r; dx += 2)
                {
                    int x = cx + dx, z = cz + dz;
                    if (x >= 0 && z >= 0 && x < GridW && z < GridH && _water[z * GridW + x])
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static float LocalRelief(Vector2 p, float half)
        {
            float min = float.MaxValue, max = float.MinValue;
            for (float dx = -half; dx <= half; dx += half)
            {
                for (float dz = -half; dz <= half; dz += half)
                {
                    float h = GroundHeight(p + new Vector2(dx, dz));
                    if (float.IsNaN(h))
                    {
                        continue;
                    }

                    min = Mathf.Min(min, h);
                    max = Mathf.Max(max, h);
                }
            }

            return max - min;
        }

        // ------------------------------------------------------------------ cluster recipes

        private static void FarmYard(Transform zone, Vector2 centre)
        {
            Transform c = Cluster(zone, "FarmYard");
            float face = FaceRoadYaw(centre);
            GameObject house = PlaceNear(c, Pick(Houses), centre, face, 1f, Size.Large, 3f);
            Vector2 core = house != null ? XZ(house.transform.position) : centre;

            // L-shaped fence round the yard, with a gap for the gate.
            Vector2 fwd = Dir(face), right = new Vector2(fwd.y, -fwd.x);
            Vector2 a = core - fwd * 6.5f - right * 7f, b = core + fwd * 5.5f - right * 7f, d = core + fwd * 5.5f + right * 7f;
            FenceRun(c, a, b, WoodFences, 0.15f);
            FenceRun(c, b, d, WoodFences, 0.25f);

            PlaceNear(c, Pick(Wells), core + right * 5f - fwd * 3.5f, Yaw(), 1f, Size.Medium, 1.5f);
            PropStack(c, core - right * 5.5f + fwd * 3f);
            PropStack(c, core + right * 5f + fwd * 3.5f);
            PlaceNear(c, Pick(ToonTrees), core - fwd * 8f + right * 6f, Yaw(), Scale(), Size.Large, 2.5f);
            PlaceNear(c, Pick(ToonTrees), core - fwd * 8.5f - right * 5f, Yaw(), Scale(), Size.Large, 2.5f);
            for (int i = 0; i < 3; i++)
            {
                PlaceNear(c, Pick(Bushes), core + Circle(7.5f), Yaw(), Scale(), Size.Small, 1.5f);
            }

            Patch(c, core + fwd * 4.5f - right * 2f, 7, 1.6f, Tulips);
            Patch(c, core - fwd * 5f + right * 3f, 6, 1.4f, Daisies);
            Patch(c, core + Circle(6f), 6, 2.2f, Grass);
        }

        private static void Field(Transform zone, Vector2 centre, int rows, int cols, float yaw)
        {
            Transform c = Cluster(zone, "FarmField");
            const float plot = 5f, walk = 1.1f;
            Vector2 fwd = Dir(yaw), right = new Vector2(fwd.y, -fwd.x);
            float width = cols * plot + (cols - 1) * walk, depth = rows * plot + (rows - 1) * walk;

            for (int r = 0; r < rows; r++)
            {
                for (int k = 0; k < cols; k++)
                {
                    Vector2 p = centre + right * (-width * 0.5f + plot * 0.5f + k * (plot + walk))
                                       + fwd * (-depth * 0.5f + plot * 0.5f + r * (plot + walk));
                    GameObject land = Place(c, Pick(Plots), p, yaw + 90f * _rng.Next(4), 1f, Size.Medium, sink: 0.4f);
                    if (land != null && _rng.NextDouble() < 0.6)
                    {
                        // a few crops standing in the soil
                        string crop = Pick(Crops);
                        for (int i = 0; i < 4; i++)
                        {
                            Place(c, crop, p + new Vector2(Jit(1.6f), Jit(1.6f)), Yaw(), Scale(), Size.Tiny, ignoreOccupancy: true);
                        }
                    }
                }
            }

            // walkway / border storytelling: tools, water, harvest
            Vector2 edge = centre - fwd * (depth * 0.5f + 1.6f);
            PlaceNear(c, Pick(Wheelbarrows), edge + right * (width * 0.25f), yaw + 90f, 1f, Size.Medium, 1.2f);
            PlaceNear(c, Pick(WateringCans), edge - right * (width * 0.2f), Yaw(), 1f, Size.Tiny, 0.8f);
            PlaceNear(c, Pick(Buckets), edge - right * (width * 0.2f) + right * 0.9f, Yaw(), 1f, Size.Tiny, 0.8f);
            PlaceNear(c, Pick(Hay), centre + fwd * (depth * 0.5f + 1.8f) - right * (width * 0.3f), Yaw(), Scale(), Size.Medium, 1.2f);
            PlaceNear(c, Pick(Hay), centre + fwd * (depth * 0.5f + 1.6f) - right * (width * 0.3f - 1.4f), Yaw(), Scale(), Size.Medium, 1.2f);
            PlaceNear(c, Pick(Tools), centre + right * (width * 0.5f + 1.2f), yaw, 1f, Size.Tiny, 1f);
            PlaceNear(c, Pick(Produce), centre + right * (width * 0.5f + 1.3f) + fwd * 1.5f, Yaw(), 1f, Size.Tiny, 1f);
            PlaceNear(c, Pick(Sacks), centre + right * (width * 0.5f + 1.4f) - fwd * 1.3f, Yaw(), 1f, Size.Small, 1f);

            // partial fence on the far side, flowers along the near border
            FenceRun(c, centre + fwd * (depth * 0.5f + 0.8f) - right * (width * 0.5f + 0.5f), centre + fwd * (depth * 0.5f + 0.8f) + right * (width * 0.5f + 0.5f), PicketFences, 0.2f);
            Patch(c, edge - right * (width * 0.35f), 5, 1.2f, Poppies);
            Patch(c, edge + right * (width * 0.05f), 4, 1.2f, Tulips);
        }

        private static void StorageYard(Transform zone, Vector2 centre, string[] buildings)
        {
            Transform c = Cluster(zone, "StorageYard");
            float face = FaceRoadYaw(centre);
            GameObject building = PlaceNear(c, Pick(buildings), centre, face, 1f, Size.Large, 3f);
            Vector2 core = building != null ? XZ(building.transform.position) : centre;
            Vector2 fwd = Dir(face), right = new Vector2(fwd.y, -fwd.x);

            GameObject pallet = PlaceNear(c, Pick(Pallets), core + fwd * 6f + right * 3f, face, 1f, Size.Small, 1.2f);
            if (pallet != null)
            {
                StackOn(c, pallet, Pick(Crates));
            }

            PropStack(c, core + fwd * 5.5f - right * 3.5f);
            PropStack(c, core - right * 7f);
            PlaceNear(c, Pick(MetalBarrels), core + right * 7f, Yaw(), 1f, Size.Small, 1.2f);
            PlaceNear(c, Pick(MetalBarrels), core + right * 7.8f + fwd * 0.9f, Yaw(), 1f, Size.Small, 1.2f);
            PlaceNear(c, Pick(LongCrates), core + right * 6.5f - fwd * 2.5f, face + 90f, 1f, Size.Small, 1.2f);
            PlaceNear(c, Pick(Hay), core - fwd * 6f + right * 4f, Yaw(), Scale(), Size.Medium, 1.5f);
            PlaceNear(c, Pick(Hay), core - fwd * 6.5f + right * 2.5f, Yaw(), Scale(), Size.Medium, 1.5f);
            PlaceNear(c, Pick(Bushes), core - fwd * 6f - right * 5f, Yaw(), Scale(), Size.Small, 1.5f);
            Patch(c, core + fwd * 7f, 5, 1.5f, Grass);
            Patch(c, core - right * 8.5f + fwd * 2f, 5, 1.3f, Poppies);
        }

        private static void Orchard(Transform zone, Vector2 centre, int trees)
        {
            Transform c = Cluster(zone, "Orchard");
            const float spacing = 7.5f;
            int placed = 0;
            for (int i = 0; i < trees * 2 && placed < trees; i++)
            {
                Vector2 p = centre + new Vector2((i % 3 - 1) * spacing + Jit(1.2f), ((i / 3) - 0.5f) * spacing + Jit(1.2f));
                if (PlaceNear(c, Pick(FruitTrees), p, Yaw(), Scale(), Size.Large, 1.5f) != null)
                {
                    placed++;
                    Patch(c, p + Circle(3f), 3, 1f, Grass);
                }
            }

            PlaceNear(c, Pick(Logs), centre + Circle(5f), Yaw(), 1f, Size.Small, 2f);
            PlaceNear(c, Pick(Crates), centre + Circle(4f), Yaw(), 1f, Size.Small, 2f);
            PlaceNear(c, Pick(Produce), centre + Circle(4f), Yaw(), 1f, Size.Tiny, 2f);
            PlaceNear(c, Pick(Buckets), centre + Circle(4f), Yaw(), 1f, Size.Tiny, 2f);
            for (int i = 0; i < 3; i++)
            {
                PlaceNear(c, Pick(Bushes), centre + Circle(9f), Yaw(), Scale(), Size.Small, 2f);
            }

            Patch(c, centre + Circle(8f), 8, 2.2f, Daisies);
            Patch(c, centre + Circle(8f), 8, 2.2f, Poppies);
            Patch(c, centre + Circle(6f), 4, 1.2f, Mushrooms);
        }

        private static void TreeGroup(Transform zone, Vector2 centre)
        {
            Transform c = Cluster(zone, "TreeGroup");
            PlaceNear(c, Pick(FruitTrees), centre, Yaw(), Scale(), Size.Large, 2.5f);
            for (int i = 0; i < 3; i++)
            {
                PlaceNear(c, Pick(ToonTrees), centre + Circle(5f), Yaw(), Scale(), Size.Large, 2f);
            }

            for (int i = 0; i < 3; i++)
            {
                PlaceNear(c, Pick(Bushes), centre + Circle(4f), Yaw(), Scale(), Size.Small, 1.5f);
            }

            PlaceNear(c, Pick(BigRocks), centre + Circle(4.5f), Yaw(), Scale(), Size.Medium, 1.5f);
            Patch(c, centre + Circle(4f), 7, 2f, Tulips);
            Patch(c, centre + Circle(5f), 6, 2f, Grass);
        }

        private static void RockCorner(Transform zone, Vector2 centre)
        {
            Transform c = Cluster(zone, "RockCorner");
            GameObject big = PlaceNear(c, Pick(BigRocks), centre, Yaw(), Scale() * 1.15f, Size.Medium, 2f);
            Vector2 core = big != null ? XZ(big.transform.position) : centre;
            for (int i = 0; i < 2; i++)
            {
                PlaceNear(c, Pick(SmallRocks), core + Circle(1.8f), Yaw(), Scale(), Size.Tiny, 1f);
            }

            for (int i = 0; i < 2; i++)
            {
                PlaceNear(c, Pick(Bushes), core + Circle(2.4f), Yaw(), Scale(), Size.Small, 1f);
            }

            PlaceNear(c, Pick(Logs), core + Circle(3f), Yaw(), 1f, Size.Small, 1.5f);
            Patch(c, core + Circle(2.5f), 4, 1.2f, Grass);
            Patch(c, core + Circle(2.5f), 3, 0.8f, Mushrooms);
            Patch(c, core + Circle(3.5f), 5, 1.4f, Poppies);
        }

        private static void FlowerBed(Transform zone, Vector2 centre, int count, float radius)
        {
            Transform c = Cluster(zone, "FlowerBed");
            string[] main = _rng.NextDouble() < 0.5 ? Tulips : Poppies;
            Patch(c, centre, count, radius, main);
            Patch(c, centre + Circle(radius * 0.6f), count / 3, radius * 0.5f, Daisies);
            Patch(c, centre + Circle(radius), 4, radius * 0.6f, Grass);
            PlaceNear(c, Pick(SmallRocks), centre + Circle(radius), Yaw(), 1f, Size.Tiny, 1f);
        }

        private static void PropCluster(Transform zone, Vector2 centre)
        {
            Transform c = Cluster(zone, "PropCluster");
            PropStack(c, centre);
            PlaceNear(c, Pick(Sacks), centre + Circle(1.8f), Yaw(), 1f, Size.Small, 1f);
            PlaceNear(c, Pick(Hay), centre + Circle(2.2f), Yaw(), Scale(), Size.Medium, 1f);
            PlaceNear(c, Pick(Buckets), centre + Circle(1.6f), Yaw(), 1f, Size.Tiny, 1f);
            Patch(c, centre + Circle(2.5f), 5, 1.3f, Grass);
            Patch(c, centre + Circle(2.8f), 4, 1f, Tulips);
        }

        private static void HayCorner(Transform zone, Vector2 centre)
        {
            Transform c = Cluster(zone, "HayCorner");
            for (int i = 0; i < 3; i++)
            {
                PlaceNear(c, Pick(Hay), centre + new Vector2(i * 1.4f, Jit(0.4f)), Yaw(), Scale(), Size.Medium, 1f);
            }

            PlaceNear(c, Pick(Wheelbarrows), centre + new Vector2(0.5f, 2.4f), Yaw(), 1f, Size.Medium, 1.2f);
            PlaceNear(c, Pick(Tools), centre + new Vector2(-1.3f, 0.8f), Yaw(), 1f, Size.Tiny, 1f);
            PlaceNear(c, Pick(Feeders), centre + new Vector2(2f, -2.3f), Yaw(), 1f, Size.Small, 1.2f);
            Patch(c, centre + Circle(3f), 5, 1.5f, Grass);
        }

        private static void MillLandmark(Transform zone, Vector2 centre)
        {
            Transform c = Cluster(zone, "MillLandmark");
            GameObject mill = PlaceNear(c, Pick(Mills), centre, FaceRoadYaw(centre), 1f, Size.Large, 3f);
            Vector2 core = mill != null ? XZ(mill.transform.position) : centre;
            PlaceNear(c, Pick(Sacks), core + new Vector2(3.5f, 1f), Yaw(), 1f, Size.Small, 1f);
            PlaceNear(c, Pick(Sacks), core + new Vector2(3.8f, 2f), Yaw(), 1f, Size.Small, 1f);
            PlaceNear(c, Pick(Hay), core + new Vector2(-3.5f, 2f), Yaw(), Scale(), Size.Medium, 1f);
            PlaceNear(c, Pick(Hay), core + new Vector2(-4f, 3.3f), Yaw(), Scale(), Size.Medium, 1f);
            PlaceNear(c, Pick(Crates), core + new Vector2(3f, -2.5f), Yaw(), 1f, Size.Small, 1f);
            Patch(c, core + new Vector2(0f, 5f), 8, 2f, Poppies);
            Patch(c, core + new Vector2(0f, -5f), 6, 2f, Grass);
        }

        private static void SiloGroup(Transform zone, Vector2 centre)
        {
            Transform c = Cluster(zone, "SiloGroup");
            PlaceNear(c, Silos[0], centre, Yaw(), 1f, Size.Large, 2f);
            PlaceNear(c, Silos[1], centre + new Vector2(3.6f, 1f), Yaw(), 1f, Size.Large, 2f);
            PlaceNear(c, Pick(MetalBarrels), centre + new Vector2(-2.6f, 2f), Yaw(), 1f, Size.Small, 1f);
            PlaceNear(c, Pick(MetalBarrels), centre + new Vector2(-2.2f, 3.1f), Yaw(), 1f, Size.Small, 1f);
            PlaceNear(c, Pick(Pallets), centre + new Vector2(2f, -3f), Yaw(), 1f, Size.Small, 1f);
            FenceRun(c, centre + new Vector2(-5f, -5f), centre + new Vector2(6f, -5f), WoodFences, 0.2f);
            Patch(c, centre + new Vector2(0f, 5f), 6, 2f, Grass);
        }

        private static void AnimalPen(Transform zone, Vector2 centre)
        {
            Transform c = Cluster(zone, "AnimalPen");
            Vector2 a = centre + new Vector2(-4f, -3.5f), b = centre + new Vector2(4f, -3.5f), d = centre + new Vector2(4f, 3.5f), e = centre + new Vector2(-4f, 3.5f);
            FenceRun(c, a, b, WoodFences, 0.15f);
            FenceRun(c, b, d, WoodFences, 0.15f);
            FenceRun(c, d, e, WoodFences, 0.15f);
            FenceRun(c, e, a, WoodFences, 0.35f); // open side
            PlaceNear(c, Pick(Feeders), centre + new Vector2(0f, -1.5f), 0f, 1f, Size.Small, 1f);
            PlaceNear(c, Pick(Hay), centre + new Vector2(2f, 1.5f), Yaw(), Scale(), Size.Medium, 1f);
            PlaceNear(c, Pick(Buckets), centre + new Vector2(-2f, 1.2f), Yaw(), 1f, Size.Tiny, 1f);
            PlaceNear(c, Pick(Coops), centre + new Vector2(-1.5f, 6.5f), Yaw(), 1f, Size.Large, 1.5f);
            Patch(c, centre + new Vector2(6f, 0f), 6, 1.5f, Grass);
        }

        /// <summary>Walks the road and dresses both verges with small story clusters, alternating sides and themes,
        /// plus a sparse second row of trees further out.</summary>
        private static void Roadside(Transform zone)
        {
            float total = PathLength();
            int index = 0;
            for (float s = 2.5f; s < total - 3f; s += 4.6f)
            {
                SamplePath(s, out Vector2 p, out Vector2 tangent);
                Vector2 normal = new Vector2(-tangent.y, tangent.x);
                foreach (float side in new[] { 1f, -1f })
                {
                    Vector2 spot = p + tangent * Jit(1f) + normal * side * (5.4f + (float)_rng.NextDouble() * 1.2f);
                    VergeCluster(zone, spot, tangent, normal, side, index++);

                    // second row: vegetation mass a little further out, alternating trees and bush/rock clumps
                    Vector2 back = p + tangent * Jit(1.5f) + normal * side * (8.2f + (float)_rng.NextDouble() * 2.2f);
                    if (index % 3 == 0)
                    {
                        PlaceNear(zone, Pick(ToonTrees), back + normal * side * 1.5f, Yaw(), Scale(), Size.Large, 1.5f);
                    }
                    else if (index % 3 == 1)
                    {
                        PlaceNear(zone, Pick(Bushes), back, Yaw(), Scale(), Size.Small, 1f);
                        PlaceNear(zone, Pick(Bushes), back + tangent * 1.4f, Yaw(), Scale(), Size.Small, 1f);
                        Patch(zone, back - tangent * 1.3f, 4, 1f, Grass);
                    }
                    else
                    {
                        Patch(zone, back, 6, 1.6f, _rng.NextDouble() < 0.5 ? Poppies : Tulips);
                        PlaceNear(zone, Pick(SmallRocks), back + tangent, Yaw(), 1f, Size.Tiny, 0.6f);
                    }
                }
            }
        }

        private static void VergeCluster(Transform zone, Vector2 spot, Vector2 tangent, Vector2 normal, float side, int index)
        {
            {
                Transform c = Cluster(zone, "Verge");
                switch (index % 5)
                {
                    case 0:
                        FenceRun(c, spot - tangent * 2.8f, spot + tangent * 2.8f, index % 10 == 0 ? PicketFences : WoodFences, 0.1f);
                        Patch(c, spot + normal * side * 0.9f, 5, 1.8f, Tulips);
                        break;
                    case 1:
                        PropStack(c, spot);
                        Patch(c, spot + tangent * 1.8f, 3, 0.8f, Grass);
                        break;
                    case 2:
                        PlaceNear(c, Pick(BigRocks), spot, Yaw(), Scale(), Size.Medium, 1f);
                        PlaceNear(c, Pick(Bushes), spot + tangent * 1.6f, Yaw(), Scale(), Size.Small, 0.8f);
                        Patch(c, spot - tangent * 1.6f, 5, 1.2f, Poppies);
                        break;
                    case 3:
                        PlaceNear(c, Pick(Hay), spot, Yaw(), Scale(), Size.Medium, 1f);
                        PlaceNear(c, Pick(Tools), spot + tangent * 1.2f, Yaw(), 1f, Size.Tiny, 0.6f);
                        PlaceNear(c, Pick(Buckets), spot - tangent * 1.1f, Yaw(), 1f, Size.Tiny, 0.6f);
                        Patch(c, spot + normal * side * 1.2f, 4, 1f, Daisies);
                        break;
                    default:
                        Patch(c, spot, 9, 2.2f, _rng.NextDouble() < 0.5 ? Tulips : Poppies);
                        Patch(c, spot, 4, 2f, Grass);
                        PlaceNear(c, Pick(SmallRocks), spot + tangent * 2f, Yaw(), 1f, Size.Tiny, 0.6f);
                        break;
                }
            }
        }

        private static void ForestBelt(Transform zone, Vector2 a, Vector2 b, float depth)
        {
            Transform c = Cluster(zone, "ForestBelt");
            Vector2 along = (b - a).normalized, across = new Vector2(-along.y, along.x);
            float length = Vector2.Distance(a, b);
            int row = 0;
            for (float s = 0f; s < length; s += 4.2f, row++)
            {
                Vector2 basePoint = a + along * s + across * Jit(depth * 0.5f);
                string[] kind = row % 3 == 0 ? Pines : row % 3 == 1 ? FruitTrees : ToonTrees;
                PlaceNear(c, Pick(kind), basePoint, Yaw(), Scale(), Size.Large, 1.5f);
                if (row % 2 == 0)
                {
                    PlaceNear(c, Pick(Bushes), basePoint + across * Jit(depth) + along * 2f, Yaw(), Scale(), Size.Small, 1f);
                }

                if (row % 3 == 1)
                {
                    PlaceNear(c, Pick(_rng.NextDouble() < 0.5 ? BigRocks : Logs), basePoint + along * 2.1f + across * Jit(depth * 0.6f), Yaw(), Scale(), Size.Medium, 1f);
                }

                if (row % 4 == 2)
                {
                    Patch(c, basePoint + across * Jit(depth), 4, 1.3f, _rng.NextDouble() < 0.5 ? Poppies : Mushrooms);
                }
            }
        }

        private static void PropStack(Transform parent, Vector2 centre)
        {
            switch (_rng.Next(4))
            {
                case 0: // barrel pair + crate
                    PlaceNear(parent, Pick(Barrels), centre, Yaw(), 1f, Size.Small, 0.8f);
                    PlaceNear(parent, Pick(Barrels), centre + new Vector2(1.15f, 0.3f), Yaw(), 1f, Size.Small, 0.8f);
                    PlaceNear(parent, Pick(Crates), centre + new Vector2(0.4f, -1.3f), Yaw(), 1f, Size.Small, 0.8f);
                    break;
                case 1: // crate stack + sack
                    GameObject crate = PlaceNear(parent, Pick(Crates), centre, Yaw(), 1f, Size.Small, 0.8f);
                    if (crate != null && _rng.NextDouble() < 0.7)
                    {
                        StackOn(parent, crate, Pick(Crates));
                    }

                    PlaceNear(parent, Pick(Sacks), centre + new Vector2(1.3f, 0.2f), Yaw(), 1f, Size.Small, 0.8f);
                    break;
                case 2: // long crate + barrel + sacks
                    PlaceNear(parent, Pick(LongCrates), centre, Yaw(), 1f, Size.Small, 0.8f);
                    PlaceNear(parent, Pick(Barrels), centre + new Vector2(-1.6f, 0.4f), Yaw(), 1f, Size.Small, 0.8f);
                    PlaceNear(parent, Pick(Sacks), centre + new Vector2(0.6f, 1.2f), Yaw(), 1f, Size.Small, 0.8f);
                    break;
                default: // single barrel + watering can + produce
                    PlaceNear(parent, Pick(Barrels), centre, Yaw(), 1f, Size.Small, 0.8f);
                    PlaceNear(parent, Pick(WateringCans), centre + new Vector2(0.9f, 0.5f), Yaw(), 1f, Size.Tiny, 0.6f);
                    PlaceNear(parent, Pick(Produce), centre + new Vector2(-0.8f, 0.7f), Yaw(), 1f, Size.Tiny, 0.6f);
                    break;
            }
        }

        private static void FenceRun(Transform parent, Vector2 from, Vector2 to, string[] palette, float gapChance)
        {
            Vector2 dir = to - from;
            float length = dir.magnitude;
            if (length < 0.5f)
            {
                return;
            }

            dir /= length;
            float yaw = Mathf.Atan2(dir.x, dir.y) * Mathf.Rad2Deg - 90f;
            string prefab = Pick(palette);
            float segment = SegmentLength(prefab);
            for (float s = segment * 0.5f; s <= length - segment * 0.4f; s += segment)
            {
                if (_rng.NextDouble() < gapChance)
                {
                    continue; // broken / open segment
                }

                Place(parent, prefab, from + dir * s, yaw + Jit(3f), 1f, Size.Small);
            }
        }

        private static void Patch(Transform parent, Vector2 centre, int count, float radius, string[] palette)
        {
            for (int i = 0; i < count; i++)
            {
                // square-root radius gives an even-ish but irregular spread, never a grid
                double angle = _rng.NextDouble() * Math.PI * 2.0;
                float r = radius * Mathf.Sqrt((float)_rng.NextDouble());
                var offset = new Vector2(Mathf.Cos((float)angle) * r, Mathf.Sin((float)angle) * r);
                Place(parent, Pick(palette), centre + offset, Yaw(), 0.85f + (float)_rng.NextDouble() * 0.35f, Size.Tiny);
            }
        }

        private static void StackOn(Transform parent, GameObject below, string prefab)
        {
            Bounds b = WorldBounds(below);
            GameObject go = Instantiate(parent, prefab);
            if (go == null)
            {
                return;
            }

            go.transform.rotation = Quaternion.Euler(0f, Yaw(), 0f);
            go.transform.position = new Vector3(b.center.x + Jit(0.1f), 0f, b.center.z + Jit(0.1f));
            Bounds own = WorldBounds(go);
            go.transform.position += Vector3.up * (b.max.y - own.min.y - 0.02f);
            Bounds stack = b;
            stack.Encapsulate(WorldBounds(go));
            if (OccludesGameplay(stack, stack.size.y))
            {
                Object.DestroyImmediate(go);
                Reject("occlusion");
                return;
            }

            Finish(go, Size.Small);
        }

        // ------------------------------------------------------------------ placement core

        private static GameObject PlaceNear(Transform parent, string prefab, Vector2 target, float yaw, float scale, Size size, float searchRadius)
        {
            bool big = size == Size.Large || size == Size.Tree;
            int attempts = big ? 18 : 10;
            for (int attempt = 0; attempt < attempts; attempt++)
            {
                Vector2 p = attempt == 0 ? target : target + Circle(searchRadius * (0.35f + attempt * (big ? 0.2f : 0.08f)));
                GameObject go = Place(parent, prefab, p, yaw, scale, size);
                if (go != null)
                {
                    return go;
                }
            }

            return null;
        }

        /// <summary>Scans an area (coarse lattice, nearest-to-preferred first) for a spot where the prefab passes every
        /// mask. Used for focal pieces that need a big clear footprint - a fixed anchor would rarely fit.</summary>
        private static void FarmYardIn(Transform zone, Rect area, Vector2 preferred)
        {
            if (FindSpot(Houses[0], Size.Large, area, preferred, FaceRoadYaw, 1.35f, out Vector2 spot))
            {
                FarmYard(zone, spot);
            }
        }

        private static void StorageYardIn(Transform zone, Rect area, Vector2 preferred, string[] buildings)
        {
            if (FindSpot(buildings[0], Size.Large, area, preferred, FaceRoadYaw, 1.2f, out Vector2 spot))
            {
                StorageYard(zone, spot, buildings);
            }
        }

        private static void MillIn(Transform zone, Rect area, Vector2 preferred)
        {
            if (FindSpot(Mills[0], Size.Large, area, preferred, FaceRoadYaw, 1.3f, out Vector2 spot))
            {
                MillLandmark(zone, spot);
            }
        }

        private static void SiloIn(Transform zone, Rect area, Vector2 preferred)
        {
            if (FindSpot(Silos[0], Size.Large, area, preferred, _ => 0f, 2.2f, out Vector2 spot))
            {
                SiloGroup(zone, spot);
            }
        }

        private static void FieldIn(Transform zone, Rect area, Vector2 preferred, int rows, int cols, float yaw)
        {
            // proxy: one plot scaled to the whole field's footprint
            float span = Mathf.Max(rows, cols) * 6.1f / 5f;
            if (FindSpot(Plots[0], Size.Medium, area, preferred, _ => yaw, span, out Vector2 spot))
            {
                Field(zone, spot, rows, cols, yaw);
            }
        }

        private static bool FindSpot(string prefab, Size size, Rect area, Vector2 preferred, Func<Vector2, float> yaw, float scale, out Vector2 spot)
        {
            var candidates = new List<Vector2>();
            for (float x = area.xMin; x <= area.xMax; x += 2f)
            {
                for (float z = area.yMin; z <= area.yMax; z += 2f)
                {
                    candidates.Add(new Vector2(x, z));
                }
            }

            candidates.Sort((a, b) => Vector2.SqrMagnitude(a - preferred).CompareTo(Vector2.SqrMagnitude(b - preferred)));
            foreach (Vector2 c in candidates)
            {
                if (Place(null, prefab, c, yaw(c), scale, size, dryRun: true) != null)
                {
                    spot = c;
                    return true;
                }
            }

            spot = preferred;
            return false;
        }

        private static GameObject Place(Transform parent, string prefab, Vector2 xz, float yaw, float scale, Size size, float sink = -1f, bool ignoreOccupancy = false, bool dryRun = false)
        {
            if (size == Size.Large && IsTree(prefab))
            {
                size = Size.Tree;
            }

            scale *= BaseScale(prefab);
            Rules rules = RuleSet[size];
            string gameplay = GameplayBlocker(xz, rules);
            if (gameplay != null)
            {
                Reject(gameplay);
                return null;
            }

            GameObject go = Instantiate(parent, prefab);
            if (go == null)
            {
                return null;
            }

            go.transform.SetPositionAndRotation(new Vector3(xz.x, 0f, xz.y), Quaternion.Euler(0f, yaw, 0f));
            go.transform.localScale = go.transform.localScale * scale;
            Bounds bounds = WorldBounds(go);
            if (bounds.size == Vector3.zero)
            {
                Object.DestroyImmediate(go);
                return null;
            }

            // Camera occlusion: anything taller than a flower leans over what lies behind it on screen, so a crate
            // stack just camera-side of a BuildNode already hides part of the node's ring.
            float height = bounds.size.y;
            if (height > 0.8f && OccludesGameplay(bounds, height))
            {
                Object.DestroyImmediate(go);
                Reject("occlusion");
                return null;
            }

            // Tiny vegetation may brush against each other (a flower bed), so it neither marks nor respects other
            // tiny props - only real props and the level's existing objects, via its core footprint.
            bool tiny = size == Size.Tiny;
            bool vegetation = IsVegetation(prefab);
            Bounds footprintBounds = bounds;
            if (tiny || (vegetation && height > 2.5f))
            {
                footprintBounds.Expand(new Vector3(-bounds.size.x * 0.5f, 0f, -bounds.size.z * 0.5f)); // trunk / core only
            }
            else if (size == Size.Small || size == Size.Medium)
            {
                // props may brush each other a little - natural clutter, not a grid of isolated items
                footprintBounds.Expand(new Vector3(-bounds.size.x * 0.3f, 0f, -bounds.size.z * 0.3f));
            }

            // Whole footprint (not just the pivot) must respect the road and BuildNode clearance - a 2 m fence
            // pivoted outside a node's ring can still reach into it.
            if (FootprintNear(bounds, NodeXZ) < rules.Node - 1f)
            {
                Object.DestroyImmediate(go);
                Reject("buildnode-footprint");
                return null;
            }

            if (FootprintDistanceToPath(bounds) < Mathf.Min(rules.Road, 6f) - 0.9f)
            {
                Object.DestroyImmediate(go);
                Reject("road-footprint");
                return null;
            }

            float pad = rules.Padding;
            string footprint = FootprintBlocker(footprintBounds, pad, ignoreOccupancy);
            if (footprint != null)
            {
                Object.DestroyImmediate(go);
                Reject(footprint);
                return null;
            }

            // Buildings and field plots want flat ground; props tolerate a gentle slope; vegetation and rocks grow on
            // hillsides (the centre of Level_01 is a hill and its rim is mountain).
            float diagonal = new Vector2(footprintBounds.size.x, footprintBounds.size.z).magnitude;
            float slopeRatio = prefab.Contains("/Bld_") ? 0.22f : prefab.Contains("Env_FarmLand") ? 0.3f : vegetation ? 0.9f : 0.5f;
            if (!GroundOk(footprintBounds, diagonal * slopeRatio + 0.25f, out float groundMin, out float groundMax))
            {
                Object.DestroyImmediate(go);
                Reject("slope/offterrain");
                return null;
            }

            if (dryRun)
            {
                Object.DestroyImmediate(go);
                return DryRunMarker;
            }

            // Bottom on the lowest ground under the footprint so nothing floats on the downhill side.
            float s = sink >= 0f ? sink : size == Size.Large || size == Size.Tree ? 0.15f : 0.04f;
            float targetBottom = groundMin - s - (vegetation ? (groundMax - groundMin) * 0.1f : 0f);
            go.transform.position += Vector3.up * (targetBottom - bounds.min.y);

            if (!ignoreOccupancy && !tiny)
            {
                MarkOccupied(footprintBounds, pad);
            }

            Finish(go, size);
            return go;
        }

        private static GameObject _dryRunMarker;

        private static GameObject DryRunMarker
        {
            get
            {
                if (_dryRunMarker == null)
                {
                    _dryRunMarker = new GameObject("DryRunMarker") { hideFlags = HideFlags.HideAndDontSave };
                }

                return _dryRunMarker;
            }
        }

        private static GameObject Instantiate(Transform parent, string prefabPath)
        {
            if (!PrefabCache.TryGetValue(prefabPath, out GameObject prefab))
            {
                prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath + ".prefab");
                PrefabCache[prefabPath] = prefab;
                if (prefab == null)
                {
                    Debug.LogWarning("[Level01MapDecorator] Missing prefab " + prefabPath);
                }
            }

            if (prefab == null)
            {
                return null;
            }

            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            go.transform.localPosition = Vector3.zero; // some source prefabs carry an offset root position
            return go;
        }

        /// <summary>Decoration only: no colliders (never block the UFO, beam probes or enemies), tiny props without
        /// shadows, structural ones batching-static like the level's existing environment.</summary>
        private static void Finish(GameObject go, Size size)
        {
            Rules rules = RuleSet[size];
            foreach (Collider collider in go.GetComponentsInChildren<Collider>(true))
            {
                Object.DestroyImmediate(collider);
            }

            foreach (Renderer renderer in go.GetComponentsInChildren<Renderer>(true))
            {
                renderer.shadowCastingMode = rules.CastShadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
                if (!rules.CastShadows)
                {
                    renderer.receiveShadows = size != Size.Tiny;
                }
            }

            if (rules.Static)
            {
                foreach (Transform t in go.GetComponentsInChildren<Transform>(true))
                {
                    GameObjectUtility.SetStaticEditorFlags(t.gameObject, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic);
                }
            }

            _placed++;
            PlacedBySize[size] = PlacedBySize.TryGetValue(size, out int n) ? n + 1 : 1;
        }

        // ------------------------------------------------------------------ masks

        private static bool BuildMasks()
        {
            var path = Object.FindFirstObjectByType<EnemyPath3D>();
            if (path == null || path.Count < 2)
            {
                Debug.LogError("[Level01MapDecorator] EnemyPath3D missing.");
                return false;
            }

            PathXZ.Clear();
            for (int i = 0; i < path.Count; i++)
            {
                PathXZ.Add(XZ(path.GetPoint(i)));
            }

            _spawnXZ = PathXZ[0];
            NodeXZ.Clear();
            foreach (BuildNode node in Object.FindObjectsByType<BuildNode>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                NodeXZ.Add(XZ(node.BuildPoint.position));
            }

            GameObject playerBase = GameObject.Find("Maps/ZombieRoad/PlayerBase");
            _baseXZ = playerBase != null ? XZ(playerBase.transform.position) : PathXZ[PathXZ.Count - 1];
            GameObject landing = GameObject.Find("Maps/UFOLanding");
            _landingXZ = landing != null ? XZ(landing.transform.position) : _baseXZ;

            _terrains = Terrain.activeTerrains;
            Alphas.Clear();
            foreach (Terrain terrain in _terrains)
            {
                TerrainData data = terrain.terrainData;
                Alphas[terrain] = data.GetAlphamaps(0, 0, data.alphamapResolution, data.alphamapResolution);
            }

            _occupied = new bool[GridW * GridH];
            _water = new bool[GridW * GridH];

            // Everything already in the level occupies its footprint.
            foreach (Renderer renderer in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (renderer is ParticleSystemRenderer || renderer.GetComponentInParent<Canvas>() != null || IsUnderDecoration(renderer.transform))
                {
                    continue;
                }

                string n = renderer.name;
                if (n == "Plane" || n.StartsWith("Cube") || n.StartsWith("RiverSegment") || n.StartsWith("RiverFlow") || n.StartsWith("Waterfall"))
                {
                    continue;
                }

                Bounds b = renderer.bounds;
                if (b.size.x > 30f || b.size.z > 30f || b.max.y < -50f)
                {
                    continue;
                }

                // canopies overhang their trunk - count most of the silhouette but not all of it
                if (b.size.y > 3f)
                {
                    b.Expand(new Vector3(-b.size.x * 0.25f, 0f, -b.size.z * 0.25f));
                }

                // Tiny existing bits (scattered produce, stumps) only claim their own spot; tractor-absorbable props
                // (gameplay pickups for XP) keep a clear ring so new decoration never hides or crowds them.
                bool absorbable = renderer.GetComponentInParent<AlienDefense.Environment.TractorAbsorbableProp>() != null;
                if (Mathf.Max(b.size.x, b.size.z) < 0.8f && !absorbable)
                {
                    continue; // scattered produce, sprouts: invisible from the gameplay camera, fine to cover
                }

                _markOwner = renderer.name;
                MarkOccupied(b, absorbable ? 0.6f : 0.1f);
                _markOwner = null;
            }

            // River surface (plus banks) from the water meshes' triangles.
            GameObject river = GameObject.Find("Maps/Environment/RiverSystem");
            if (river != null)
            {
                foreach (MeshFilter filter in river.GetComponentsInChildren<MeshFilter>())
                {
                    string n = filter.name;
                    if (!(n.StartsWith("RiverSegment") || n.StartsWith("WaterfallPlungePool")) || filter.sharedMesh == null)
                    {
                        continue;
                    }

                    RasteriseWater(filter);
                }

                DilateWater(4);
            }

            return true;
        }

        /// <summary>Null when the spot is clear of every gameplay area, else the name of what blocks it.</summary>
        private static string GameplayBlocker(Vector2 p, Rules rules)
        {
            if (DistanceToPath(p) < rules.Road)
            {
                return "road";
            }

            foreach (Vector2 node in NodeXZ)
            {
                if (Vector2.Distance(p, node) < rules.Node)
                {
                    return "buildnode";
                }
            }

            if (Vector2.Distance(p, _baseXZ) < rules.Base || Vector2.Distance(p, _landingXZ) < rules.Landing)
            {
                return "base/landing";
            }

            if (Vector2.Distance(p, _spawnXZ) < rules.Spawn)
            {
                return "spawn";
            }

            return DirtWeight(p) >= 0.3f ? "dirt" : null;
        }

        /// <summary>The gameplay camera sits at +X looking toward -X, pitched 50 deg: a prop of height h hides the ground
        /// for about 0.85 h behind it (toward -X). Reject tall props whose shadow-in-view would cover the road,
        /// a BuildNode or the base.</summary>
        private static bool OccludesGameplay(Bounds b, float height)
        {
            float behind = height * 0.85f + 1f;
            float minX = b.min.x - behind, maxX = b.max.x, minZ = b.min.z - 1.8f, maxZ = b.max.z + 1.8f;

            bool Inside(Vector2 q) => q.x >= minX && q.x <= maxX && q.y >= minZ && q.y <= maxZ;

            float total = PathLength();
            for (float s = 0f; s <= total; s += 1.5f)
            {
                SamplePath(s, out Vector2 q, out Vector2 _);
                if (Inside(q))
                {
                    return true;
                }
            }

            foreach (Vector2 node in NodeXZ)
            {
                if (Inside(node))
                {
                    return true;
                }
            }

            return Inside(_baseXZ) || Inside(_landingXZ);
        }

        private static string FootprintBlocker(Bounds b, float pad, bool ignoreOccupancy)
        {
            GridRange(b, pad, out int x0, out int z0, out int x1, out int z1);
            if (x0 < 0 || z0 < 0 || x1 >= GridW || z1 >= GridH)
            {
                return "outside";
            }

            for (int z = z0; z <= z1; z++)
            {
                for (int x = x0; x <= x1; x++)
                {
                    int i = z * GridW + x;
                    if (_water[i])
                    {
                        return "water";
                    }

                    if (!ignoreOccupancy && _occupied[i])
                    {
                        return "occupied";
                    }
                }
            }

            // the corners must also be off the road
            bool offRoad = DirtWeight(new Vector2(b.min.x, b.min.z)) < 0.45f && DirtWeight(new Vector2(b.max.x, b.max.z)) < 0.45f
                && DirtWeight(new Vector2(b.min.x, b.max.z)) < 0.45f && DirtWeight(new Vector2(b.max.x, b.min.z)) < 0.45f;
            return offRoad ? null : "dirt-corner";
        }

        [MenuItem("AlienDefense/Setup/Level 01/Export Decoration Masks (debug)")]
        private static void ExportMasks()
        {
            _rng = new Random(Seed);
            if (!BuildMasks())
            {
                return;
            }

            var tex = new Texture2D(GridW, GridH, TextureFormat.RGB24, false);
            for (int z = 0; z < GridH; z++)
            {
                for (int x = 0; x < GridW; x++)
                {
                    var p = new Vector2(GridMinX + (x + 0.5f) * Cell, GridMinZ + (z + 0.5f) * Cell);
                    int i = z * GridW + x;
                    Color c = new Color(0.2f, 0.45f, 0.2f);
                    if (DirtWeight(p) >= 0.3f) c = new Color(0.55f, 0.45f, 0.3f);
                    if (_occupied[i]) c = new Color(0.9f, 0.2f, 0.2f);
                    if (_water[i]) c = new Color(0.2f, 0.4f, 0.95f);
                    if (DistanceToPath(p) < 5f) c = Color.Lerp(c, Color.yellow, 0.5f);
                    if (float.IsNaN(GroundHeight(p))) c = Color.black;
                    tex.SetPixel(x, z, c);
                }
            }

            tex.Apply();
            string file = System.IO.Path.Combine(Application.dataPath, "../Temp/Level01DecorationMasks.png");
            System.IO.File.WriteAllBytes(file, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            Debug.Log("[Level01MapDecorator] Masks written to " + System.IO.Path.GetFullPath(file) + " (x = world X from " + GridMinX + ", y = world Z from " + GridMinZ + ", 0.5 m per pixel)");
        }

        private static bool GroundOk(Bounds b, float maxSlope, out float min, out float max)
        {
            min = float.MaxValue;
            max = float.MinValue;
            var samples = new[]
            {
                new Vector2(b.center.x, b.center.z), new Vector2(b.min.x, b.min.z), new Vector2(b.max.x, b.max.z),
                new Vector2(b.min.x, b.max.z), new Vector2(b.max.x, b.min.z)
            };

            foreach (Vector2 s in samples)
            {
                float h = GroundHeight(s);
                if (float.IsNaN(h))
                {
                    return false;
                }

                min = Mathf.Min(min, h);
                max = Mathf.Max(max, h);
            }

            return max - min <= maxSlope;
        }

        private static string _markOwner;
        private static string[] _owners;

        private static void MarkOccupied(Bounds b, float pad)
        {
            GridRange(b, pad, out int x0, out int z0, out int x1, out int z1);
            for (int z = Mathf.Max(0, z0); z <= Mathf.Min(GridH - 1, z1); z++)
            {
                for (int x = Mathf.Max(0, x0); x <= Mathf.Min(GridW - 1, x1); x++)
                {
                    _occupied[z * GridW + x] = true;
                    if (_markOwner != null && _owners != null)
                    {
                        _owners[z * GridW + x] = _markOwner;
                    }
                }
            }
        }

        /// <summary>Debug: what claimed the grid cells around a point.</summary>
        public static string DebugOwners(float px, float pz, float radius)
        {
            _owners = new string[GridW * GridH];
            BuildMasks();
            var names = new HashSet<string>();
            int occupiedCells = 0;
            for (float x = px - radius; x <= px + radius; x += Cell)
            {
                for (float z = pz - radius; z <= pz + radius; z += Cell)
                {
                    int ix = Mathf.FloorToInt((x - GridMinX) / Cell), iz = Mathf.FloorToInt((z - GridMinZ) / Cell);
                    if (ix < 0 || iz < 0 || ix >= GridW || iz >= GridH || !_occupied[iz * GridW + ix])
                    {
                        continue;
                    }

                    occupiedCells++;
                    names.Add(_owners[iz * GridW + ix] ?? "(unnamed)");
                }
            }

            _owners = null;
            return occupiedCells + " cells: " + string.Join(", ", names);
        }

        private static void GridRange(Bounds b, float pad, out int x0, out int z0, out int x1, out int z1)
        {
            x0 = Mathf.FloorToInt((b.min.x - pad - GridMinX) / Cell);
            z0 = Mathf.FloorToInt((b.min.z - pad - GridMinZ) / Cell);
            x1 = Mathf.FloorToInt((b.max.x + pad - GridMinX) / Cell);
            z1 = Mathf.FloorToInt((b.max.z + pad - GridMinZ) / Cell);
        }

        private static void RasteriseWater(MeshFilter filter)
        {
            Vector3[] vertices = filter.sharedMesh.vertices;
            int[] triangles = filter.sharedMesh.triangles;
            Matrix4x4 m = filter.transform.localToWorldMatrix;
            for (int t = 0; t < triangles.Length; t += 3)
            {
                Vector2 a = XZ(m.MultiplyPoint3x4(vertices[triangles[t]]));
                Vector2 b = XZ(m.MultiplyPoint3x4(vertices[triangles[t + 1]]));
                Vector2 c = XZ(m.MultiplyPoint3x4(vertices[triangles[t + 2]]));
                int x0 = Mathf.FloorToInt((Mathf.Min(a.x, b.x, c.x) - GridMinX) / Cell), x1 = Mathf.CeilToInt((Mathf.Max(a.x, b.x, c.x) - GridMinX) / Cell);
                int z0 = Mathf.FloorToInt((Mathf.Min(a.y, b.y, c.y) - GridMinZ) / Cell), z1 = Mathf.CeilToInt((Mathf.Max(a.y, b.y, c.y) - GridMinZ) / Cell);
                for (int z = Mathf.Max(0, z0); z <= Mathf.Min(GridH - 1, z1); z++)
                {
                    for (int x = Mathf.Max(0, x0); x <= Mathf.Min(GridW - 1, x1); x++)
                    {
                        var p = new Vector2(GridMinX + (x + 0.5f) * Cell, GridMinZ + (z + 0.5f) * Cell);
                        if (InTriangle(p, a, b, c))
                        {
                            _water[z * GridW + x] = true;
                        }
                    }
                }
            }
        }

        private static void DilateWater(int cells)
        {
            var copy = (bool[])_water.Clone();
            for (int z = 0; z < GridH; z++)
            {
                for (int x = 0; x < GridW; x++)
                {
                    if (!copy[z * GridW + x])
                    {
                        continue;
                    }

                    for (int dz = -cells; dz <= cells; dz++)
                    {
                        for (int dx = -cells; dx <= cells; dx++)
                        {
                            int xx = x + dx, zz = z + dz;
                            if (xx >= 0 && zz >= 0 && xx < GridW && zz < GridH)
                            {
                                _water[zz * GridW + xx] = true;
                            }
                        }
                    }
                }
            }
        }

        // ------------------------------------------------------------------ geometry helpers

        private static float GroundHeight(Vector2 p)
        {
            foreach (Terrain terrain in _terrains)
            {
                Vector3 o = terrain.transform.position;
                Vector3 size = terrain.terrainData.size;
                if (p.x >= o.x && p.x <= o.x + size.x && p.y >= o.z && p.y <= o.z + size.z)
                {
                    return o.y + terrain.SampleHeight(new Vector3(p.x, 0f, p.y));
                }
            }

            return float.NaN;
        }

        private static float DirtWeight(Vector2 p)
        {
            foreach (Terrain terrain in _terrains)
            {
                Vector3 o = terrain.transform.position;
                TerrainData data = terrain.terrainData;
                if (p.x < o.x || p.x > o.x + data.size.x || p.y < o.z || p.y > o.z + data.size.z)
                {
                    continue;
                }

                float[,,] alpha = Alphas[terrain];
                int res = data.alphamapResolution;
                int ax = Mathf.Clamp((int)((p.x - o.x) / data.size.x * res), 0, res - 1);
                int az = Mathf.Clamp((int)((p.y - o.z) / data.size.z * res), 0, res - 1);
                return alpha.GetLength(2) > 1 ? alpha[az, ax, 1] : 0f;
            }

            return 1f; // off the terrain: treat as unusable
        }

        private static float DistanceToPath(Vector2 p)
        {
            float best = float.MaxValue;
            for (int i = 0; i < PathXZ.Count - 1; i++)
            {
                Vector2 a = PathXZ[i], b = PathXZ[i + 1], ab = b - a;
                float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(0.0001f, ab.sqrMagnitude));
                best = Mathf.Min(best, Vector2.Distance(p, a + ab * t));
            }

            return best;
        }

        private static float FootprintNear(Bounds b, List<Vector2> points)
        {
            float best = float.MaxValue;
            foreach (Vector2 p in points)
            {
                float dx = Mathf.Max(b.min.x - p.x, 0f, p.x - b.max.x);
                float dz = Mathf.Max(b.min.z - p.y, 0f, p.y - b.max.z);
                best = Mathf.Min(best, Mathf.Sqrt(dx * dx + dz * dz));
            }

            return best;
        }

        private static float FootprintDistanceToPath(Bounds b)
        {
            return Mathf.Min(
                DistanceToPath(new Vector2(b.center.x, b.center.z)),
                Mathf.Min(DistanceToPath(new Vector2(b.min.x, b.min.z)), DistanceToPath(new Vector2(b.max.x, b.max.z))),
                Mathf.Min(DistanceToPath(new Vector2(b.min.x, b.max.z)), DistanceToPath(new Vector2(b.max.x, b.min.z))));
        }

        private static float PathLength()
        {
            float total = 0f;
            for (int i = 0; i < PathXZ.Count - 1; i++)
            {
                total += Vector2.Distance(PathXZ[i], PathXZ[i + 1]);
            }

            return total;
        }

        private static void SamplePath(float distance, out Vector2 point, out Vector2 tangent)
        {
            for (int i = 0; i < PathXZ.Count - 1; i++)
            {
                float len = Vector2.Distance(PathXZ[i], PathXZ[i + 1]);
                if (distance <= len || i == PathXZ.Count - 2)
                {
                    tangent = (PathXZ[i + 1] - PathXZ[i]).normalized;
                    point = PathXZ[i] + tangent * Mathf.Min(distance, len);
                    return;
                }

                distance -= len;
            }

            point = PathXZ[PathXZ.Count - 1];
            tangent = Vector2.right;
        }

        private static float FaceRoadYaw(Vector2 p)
        {
            float best = float.MaxValue;
            Vector2 nearest = p;
            for (int i = 0; i < PathXZ.Count - 1; i++)
            {
                Vector2 a = PathXZ[i], ab = PathXZ[i + 1] - a;
                float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(0.0001f, ab.sqrMagnitude));
                Vector2 q = a + ab * t;
                float d = Vector2.Distance(p, q);
                if (d < best)
                {
                    best = d;
                    nearest = q;
                }
            }

            Vector2 dir = nearest - p;
            float yaw = Mathf.Atan2(dir.x, dir.y) * Mathf.Rad2Deg;
            return Mathf.Round(yaw / 15f) * 15f;
        }

        private static float SegmentLength(string prefab)
        {
            GameObject go = Instantiate(null, prefab);
            if (go == null)
            {
                return 2f;
            }

            go.transform.position = new Vector3(0f, -3000f, 0f);
            float length = Mathf.Max(1f, WorldBounds(go).size.x * 0.98f);
            Object.DestroyImmediate(go);
            return length;
        }

        private static Bounds WorldBounds(GameObject go)
        {
            Renderer[] renderers = go.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0)
            {
                return new Bounds(go.transform.position, Vector3.zero);
            }

            Bounds b = renderers[0].bounds;
            foreach (Renderer r in renderers)
            {
                if (!(r is ParticleSystemRenderer))
                {
                    b.Encapsulate(r.bounds);
                }
            }

            return b;
        }

        private static bool InTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
        {
            float d1 = Cross(p, a, b), d2 = Cross(p, b, c), d3 = Cross(p, c, a);
            bool neg = d1 < 0 || d2 < 0 || d3 < 0, pos = d1 > 0 || d2 > 0 || d3 > 0;
            return !(neg && pos);
        }

        private static float Cross(Vector2 p, Vector2 a, Vector2 b) => (p.x - b.x) * (a.y - b.y) - (a.x - b.x) * (p.y - b.y);

        private static Transform Zone(Transform root, string name)
        {
            var t = new GameObject(name).transform;
            t.SetParent(root, false);
            return t;
        }

        private static Transform Cluster(Transform zone, string name)
        {
            var t = new GameObject(name).transform;
            t.SetParent(zone, false);
            return t;
        }

        private static bool IsVegetation(string prefab)
        {
            string p = prefab.ToLowerInvariant();
            return p.Contains("tree") || p.Contains("shrub") || p.Contains("bush") || p.Contains("rock") || p.Contains("grass")
                || p.Contains("flower") || p.Contains("poppy") || p.Contains("mushroom") || p.Contains("_logs") || p.Contains("stump")
                || p.Contains("wheat") || p.Contains("plant");
        }

        private static bool IsUnderDecoration(Transform t)
        {
            for (; t != null; t = t.parent)
            {
                if (t.name == "Decoration" && t.parent != null && t.parent.name == "Environment")
                {
                    return true;
                }
            }

            return false;
        }

        private static Vector2 XZ(Vector3 v) => new Vector2(v.x, v.z);
        private static Vector2 Dir(float yaw) => new Vector2(Mathf.Sin(yaw * Mathf.Deg2Rad), Mathf.Cos(yaw * Mathf.Deg2Rad));
        private static string Pick(string[] palette) => palette[_rng.Next(palette.Length)];
        private static float Yaw() => (float)_rng.NextDouble() * 360f;
        private static float Scale() => 0.9f + (float)_rng.NextDouble() * 0.2f;
        private static float Jit(float amount) => ((float)_rng.NextDouble() * 2f - 1f) * amount;

        private static Vector2 Circle(float radius)
        {
            double angle = _rng.NextDouble() * Math.PI * 2.0;
            float r = radius * (0.5f + 0.5f * (float)_rng.NextDouble());
            return new Vector2(Mathf.Cos((float)angle) * r, Mathf.Sin((float)angle) * r);
        }
    }
}
