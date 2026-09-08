using System;
using System.Collections.Generic;
using System.Linq;
using AlienDefense.Building;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AlienDefense.EditorTools
{
    /// <summary>Editor-only tool that decorates Level_01 (or any level with the same BuildNode/EnemyPath/Terrain
    /// conventions) into a big, readable low-poly farm using the project's existing farm asset packs (Gridness
    /// Lite Farm Pack, LowPolyFarmLite, Pandazole Farm Ranch Pack). Never touches gameplay objects (Enemy/Tower/
    /// Wave/Build/Path/Camera/Joystick) - it only reads their transforms to compute safe placement, and only ever
    /// creates/destroys objects under Environment/GeneratedFarmDecoration. Re-runnable: same seed -> same layout.</summary>
    public sealed class FarmMapDecoratorWindow : EditorWindow
    {
        private const string GeneratedRootName = "GeneratedFarmDecoration";

        [SerializeField] private Transform _environmentRoot;
        [SerializeField] private Transform _roadRoot;
        [SerializeField] private Transform _buildNodesRoot;
        [SerializeField] private int _seed = 1001;
        [SerializeField] private float _decorationDensity = 1f;
        [SerializeField] private float _treeDensity = 1f;
        [SerializeField] private float _propDensity = 1f;

        [MenuItem("AlienDefense/Level Tools/Farm Map Decorator")]
        public static void Open()
        {
            GetWindow<FarmMapDecoratorWindow>("Farm Map Decorator");
        }

        private void OnEnable()
        {
            AutoWireDefaults();
        }

        private void AutoWireDefaults()
        {
            if (_environmentRoot == null)
            {
                GameObject maps = GameObject.Find("Maps");
                Transform env = maps != null ? maps.transform.Find("Environment") : null;
                if (env != null)
                {
                    _environmentRoot = env;
                }
            }

            if (_roadRoot == null)
            {
                GameObject road = GameObject.Find("EnemyPath");
                if (road != null)
                {
                    _roadRoot = road.transform;
                }
            }

            if (_buildNodesRoot == null)
            {
                GameObject bn = GameObject.Find("BuildNodes");
                if (bn != null)
                {
                    _buildNodesRoot = bn.transform;
                }
            }
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Farm Map Decorator", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Decorates Level_01 into a big low-poly farm using existing asset packs only. " +
                "Only ever creates/removes objects under Environment/GeneratedFarmDecoration.",
                MessageType.Info);

            EditorGUILayout.Space();
            _environmentRoot = (Transform)EditorGUILayout.ObjectField("Environment Root", _environmentRoot, typeof(Transform), true);
            _roadRoot = (Transform)EditorGUILayout.ObjectField("Road / Path Root", _roadRoot, typeof(Transform), true);
            _buildNodesRoot = (Transform)EditorGUILayout.ObjectField("BuildNodes Root", _buildNodesRoot, typeof(Transform), true);

            EditorGUILayout.Space();
            _seed = EditorGUILayout.IntField(new GUIContent("Random Seed", "Same seed = same layout. Try 1001 / 2001 / 3001."), _seed);
            _decorationDensity = EditorGUILayout.Slider(new GUIContent("Decoration Density", "Scales roadside clusters and field size."), _decorationDensity, 0.2f, 1.5f);
            _treeDensity = EditorGUILayout.Slider(new GUIContent("Tree Density", "Scales orchard + background tree count."), _treeDensity, 0.2f, 1.5f);
            _propDensity = EditorGUILayout.Slider(new GUIContent("Prop Density", "Scales crate/barrel/tool clusters."), _propDensity, 0.2f, 1.5f);

            EditorGUILayout.Space();
            if (GUILayout.Button("Analyze Level", GUILayout.Height(24)))
            {
                AutoWireDefaults();
                FarmMapDecoratorCore.Analyze(_environmentRoot, _roadRoot, _buildNodesRoot);
            }

            if (GUILayout.Button("Preview Layout (log only, creates nothing)", GUILayout.Height(24)))
            {
                RunDecoration(dryRun: true);
            }

            GUI.backgroundColor = new Color(0.6f, 1f, 0.6f);
            if (GUILayout.Button("Decorate Level", GUILayout.Height(28)))
            {
                RunDecoration(dryRun: false);
            }

            GUI.backgroundColor = new Color(1f, 0.6f, 0.6f);
            if (GUILayout.Button("Clear Generated Decoration", GUILayout.Height(24)))
            {
                ClearGenerated();
            }

            GUI.backgroundColor = Color.white;
        }

        private void RunDecoration(bool dryRun)
        {
            AutoWireDefaults();
            if (_environmentRoot == null || _roadRoot == null || _buildNodesRoot == null)
            {
                Debug.LogError("[FarmMapDecorator] Missing Environment/Road/BuildNodes root. Click Analyze Level first, or assign them by hand.");
                return;
            }

            FarmMapDecoratorCore.Generate(_environmentRoot, _roadRoot, _buildNodesRoot, _seed, _decorationDensity, _treeDensity, _propDensity, dryRun);

            if (!dryRun)
            {
                EditorSceneManager.MarkSceneDirty(_environmentRoot.gameObject.scene);
            }
        }

        private void ClearGenerated()
        {
            AutoWireDefaults();
            if (_environmentRoot == null)
            {
                Debug.LogError("[FarmMapDecorator] No Environment Root assigned/found.");
                return;
            }

            Transform existing = _environmentRoot.Find(GeneratedRootName);
            if (existing == null)
            {
                Debug.Log("[FarmMapDecorator] Nothing to clear - GeneratedFarmDecoration does not exist.");
                return;
            }

            Undo.DestroyObjectImmediate(existing.gameObject);
            EditorSceneManager.MarkSceneDirty(_environmentRoot.gameObject.scene);
            Debug.Log("[FarmMapDecorator] Cleared GeneratedFarmDecoration. Hand-placed Environment decoration was left untouched.");
        }
    }

    /// <summary>Pure logic behind the window: safe-placement checks, zone composition, prefab catalog. Kept
    /// separate from the EditorWindow so both the UI buttons and (during development) direct script calls can
    /// drive it identically.</summary>
    public static class FarmMapDecoratorCore
    {
        private const string GeneratedRootName = "GeneratedFarmDecoration";

        // Flat, non-mountain playable valley (see LevelBounds / terrain sculpt: flat zone was X:[-60,65] Z:[40,100]).
        // Inset by a couple of units so nothing is generated right at the base of the mountain ramp.
        private const float FlatMinX = -58f, FlatMaxX = 63f, FlatMinZ = 42f, FlatMaxZ = 98f;

        private const float RoadSafeSmall = 0.8f, RoadSafeMedium = 1.8f, RoadSafeLarge = 3.2f;
        private const float NodeSafeRadius = 2.2f; // extra margin beyond each BuildNode's own collider bounds

        private static System.Random _rng;

        // ---------------------------------------------------------------- Prefab catalog ----------------------
        private static string Pandazole(string n) => $"Assets/Pandazole_Ultimate_Pack/Pandazole Farm Ranch Pack/Prefabs/{n}.prefab";
        private static string Gridness(string n) => $"Assets/Gridness Studios/Lite Farm Pack/Prefabs/{n}.prefab";
        private static string LowPoly(string n) => $"Assets/LowPolyFarmLite/Prefabs/{n}.prefab";

        private static GameObject Load(string path)
        {
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (go == null)
            {
                Debug.LogWarning($"[FarmMapDecorator] Prefab not found, skipped: {path}");
            }
            return go;
        }

        // ---------------------------------------------------------------- Footprint measurement -----------------
        // Real prefab sizes vary wildly (a "small" farmland tile is actually 5x5 units, a greenhouse is 12 units
        // across) - hand-guessed placement radii caused tiles to overlap and let props spawn on top of buildings.
        // Measuring the actual combined-renderer XZ bounds once per prefab and caching it fixes both at the root.
        private static readonly Dictionary<string, Vector2> _footprintCache = new Dictionary<string, Vector2>();

        /// <summary>XZ size (width, depth) of a prefab's combined renderer bounds, cached. (0,0) if unmeasurable.</summary>
        private static Vector2 GetFootprintSize(string path)
        {
            if (_footprintCache.TryGetValue(path, out Vector2 cached)) return cached;

            Vector2 size = Vector2.zero;
            GameObject prefab = Load(path);
            if (prefab != null)
            {
                var renderers = prefab.GetComponentsInChildren<Renderer>();
                if (renderers.Length > 0)
                {
                    Bounds b = renderers[0].bounds;
                    foreach (var r in renderers) b.Encapsulate(r.bounds);
                    size = new Vector2(b.size.x, b.size.z);
                }
            }

            _footprintCache[path] = size;
            return size;
        }

        /// <summary>Bounding-circle radius (half diagonal of the XZ footprint) - safe worst-case radius regardless
        /// of Y rotation, used for every "keep clear of this" distance check (roads, BuildNodes, other props).</summary>
        private static float GetFootprintRadius(string path)
        {
            Vector2 s = GetFootprintSize(path);
            if (s == Vector2.zero) return 0.5f; // sane fallback for an unmeasurable/missing prefab
            return 0.5f * Mathf.Sqrt(s.x * s.x + s.y * s.y);
        }

        private static float GetMaxFootprintRadius(string[] pool)
        {
            float max = 0.4f;
            foreach (var p in pool) max = Mathf.Max(max, GetFootprintRadius(p));
            return max;
        }

        // ---------------------------------------------------------------- Analyze ------------------------------
        public static void Analyze(Transform environmentRoot, Transform roadRoot, Transform buildNodesRoot)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("[FarmMapDecorator] === Analyze Level ===");

            if (roadRoot != null)
            {
                var pts = RoadPoints(roadRoot);
                float len = 0f;
                for (int i = 1; i < pts.Count; i++) len += Vector3.Distance(pts[i - 1], pts[i]);
                sb.AppendLine($"Road '{roadRoot.name}': {pts.Count} waypoints, ~{len:0.0} units long.");
            }
            else sb.AppendLine("Road root NOT set.");

            if (buildNodesRoot != null)
            {
                sb.AppendLine($"BuildNodes: {buildNodesRoot.childCount}.");
                foreach (Transform bn in buildNodesRoot)
                {
                    sb.AppendLine($"  - {bn.name} @ {bn.position}");
                }
            }
            else sb.AppendLine("BuildNodes root NOT set.");

            var terrains = Terrain.activeTerrains;
            sb.AppendLine($"Terrain tiles: {terrains.Length}");
            foreach (var t in terrains)
            {
                sb.AppendLine($"  - {t.name} origin={t.transform.position} size={t.terrainData.size}");
            }

            sb.AppendLine($"Flat/farmable zone (no mountains): X[{FlatMinX},{FlatMaxX}] Z[{FlatMinZ},{FlatMaxZ}]");

            if (environmentRoot != null)
            {
                Transform gen = environmentRoot.Find(GeneratedRootName);
                sb.AppendLine(gen != null
                    ? $"Existing GeneratedFarmDecoration found ({gen.childCount} category groups) - Decorate will replace it, Clear will remove it."
                    : "No GeneratedFarmDecoration yet - Decorate Level will create a fresh one.");
            }

            Debug.Log(sb.ToString());
        }

        // ---------------------------------------------------------------- Generate -----------------------------
        public static void Generate(Transform environmentRoot, Transform roadRoot, Transform buildNodesRoot,
            int seed, float decorationDensity, float treeDensity, float propDensity, bool dryRun)
        {
            _rng = new System.Random(seed);

            List<Vector3> road = RoadPoints(roadRoot);
            List<Bounds> nodeBounds = BuildNodeBounds(buildNodesRoot);
            List<Bounds> obstacles = FindObstacleBounds();

            var placed = new List<(Vector3 pos, float radius)>();
            int dryLarge = 0, dryMedium = 0, drySmall = 0, drySkipped = 0;

            Transform root = null;
            Transform gBuildings = null, gFields = null, gOrchards = null, gAnimal = null, gStorage = null,
                gFences = null, gRoadside = null, gTrees = null, gBushes = null, gFlowers = null, gRocks = null,
                gSmallProps = null, gBackground = null;

            if (!dryRun)
            {
                Transform existing = environmentRoot.Find(GeneratedRootName);
                if (existing != null)
                {
                    Undo.DestroyObjectImmediate(existing.gameObject);
                }

                root = NewChild(environmentRoot, GeneratedRootName);
                gBuildings = NewChild(root, "FarmBuildings");
                gFields = NewChild(root, "CropFields");
                gOrchards = NewChild(root, "Orchards");
                gAnimal = NewChild(root, "AnimalArea");
                gStorage = NewChild(root, "StorageArea");
                gFences = NewChild(root, "Fences");
                gRoadside = NewChild(root, "RoadsideProps");
                gTrees = NewChild(root, "Trees");
                gBushes = NewChild(root, "Bushes");
                gFlowers = NewChild(root, "Flowers");
                gRocks = NewChild(root, "Rocks");
                gSmallProps = NewChild(root, "SmallProps");
                gBackground = NewChild(root, "BackgroundDecoration");
            }

            int placedMedium = 0, placedSmall = 0, skipped = 0;
            int buildingCount = 0, treeCount = 0, propCount = 0, fenceCount = 0;

            // ---------- STEP 1: LARGE LANDMARKS (buildings) ----------
            // Zone A - Farmhouse, background NW, well clear of the road's west entrance.
            PlaceLandmark(Pandazole("Bld_FarmerHouse"), new Vector3(-50f, 0f, 90f), gBuildings, "Farmhouse",
                road, nodeBounds, obstacles, placed, ref buildingCount, dryRun, ref dryLarge, ref drySkipped);
            PlaceLandmark(Pandazole("Bld_Outhouses"), new Vector3(-42f, 0f, 86f), gBuildings, "Outhouse",
                road, nodeBounds, obstacles, placed, ref buildingCount, dryRun, ref dryLarge, ref drySkipped);
            PlaceLandmark(Pandazole("Env_Well_01"), new Vector3(-47f, 0f, 95f), gBuildings, "Well",
                road, nodeBounds, obstacles, placed, ref propCount, dryRun, ref dryLarge, ref drySkipped);

            // Zone D - Barn + Silo + Storage shed, south-east, clear of PlayerBase.
            PlaceLandmark(Pandazole("Bld_Barn_01"), new Vector3(44f, 0f, 50f), gStorage, "Barn",
                road, nodeBounds, obstacles, placed, ref buildingCount, dryRun, ref dryLarge, ref drySkipped);
            PlaceLandmark(Pandazole("Bld_Silo_01"), new Vector3(36f, 0f, 45f), gStorage, "Silo",
                road, nodeBounds, obstacles, placed, ref buildingCount, dryRun, ref dryLarge, ref drySkipped);
            PlaceLandmark(Pandazole("Bld_StoreBuilding_01"), new Vector3(56f, 0f, 50f), gStorage, "StorageShed",
                road, nodeBounds, obstacles, placed, ref buildingCount, dryRun, ref dryLarge, ref drySkipped);

            // Zone C secondary landmark - greenhouse next to the crop field.
            PlaceLandmark(Pandazole("Bld_GreenMouse"), new Vector3(26f, 0f, 53f), gFields, "Greenhouse",
                road, nodeBounds, obstacles, placed, ref buildingCount, dryRun, ref dryLarge, ref drySkipped);

            // Zone E secondary landmark - farm mill, gives the ranch corner its own silhouette.
            PlaceLandmark(Pandazole("Bld_FarmMill_01"), new Vector3(58f, 0f, 90f), gAnimal, "FarmMill",
                road, nodeBounds, obstacles, placed, ref buildingCount, dryRun, ref dryLarge, ref drySkipped);

            // ---------- STEP 2: CROP FIELDS (Zone B - two fields, big soil rows + a small tilled garden) ----------
            BuildCropField(new Vector3(0f, 0f, 50f), 46f, 16f, road, nodeBounds, obstacles, placed,
                gFields, gFences, dryRun, decorationDensity, ref propCount, ref fenceCount, ref placedSmall, ref placedMedium, ref skipped,
                ref dryLarge, ref dryMedium, ref drySmall, ref drySkipped);
            BuildTilledGarden(new Vector3(28f, 0f, 46f), 11f, 7f, road, nodeBounds, obstacles, placed,
                gFields, dryRun, decorationDensity, ref propCount, ref placedSmall, ref skipped, ref drySmall, ref drySkipped);

            // ---------- STEP 2b: FENCED TOMATO PATCHES (matches the LowPolyFarmLite reference: dense rows,
            // full fence ring, rocks/trees just outside) - two of them, so the farm has several distinct
            // "growing areas" rather than one big field.
            BuildFencedTomatoPatch(new Vector3(-33f, 0f, 48f), 10f, 9f, road, nodeBounds, obstacles, placed,
                gFields, gFences, gRocks, gTrees, dryRun, decorationDensity,
                ref propCount, ref fenceCount, ref treeCount, ref placedSmall, ref placedMedium, ref skipped,
                ref dryMedium, ref drySmall, ref drySkipped);
            BuildFencedTomatoPatch(new Vector3(8f, 0f, 88f), 10f, 9f, road, nodeBounds, obstacles, placed,
                gFields, gFences, gRocks, gTrees, dryRun, decorationDensity,
                ref propCount, ref fenceCount, ref treeCount, ref placedSmall, ref placedMedium, ref skipped,
                ref dryMedium, ref drySmall, ref drySkipped);

            // ---------- STEP 3: ORCHARD (Zone C) ----------
            BuildOrchard(new Vector3(25f, 0f, 92f), road, nodeBounds, obstacles, placed, gOrchards, gBushes,
                dryRun, treeDensity, ref treeCount, ref propCount, ref placedMedium, ref placedSmall, ref skipped,
                ref dryMedium, ref drySmall, ref drySkipped);

            // ---------- STEP 4: RANCH / ANIMAL AREA (Zone E) ----------
            BuildRanch(new Vector3(55f, 0f, 78f), road, nodeBounds, obstacles, placed, gAnimal, gFences,
                dryRun, ref propCount, ref fenceCount, ref placedMedium, ref placedSmall, ref skipped,
                ref dryMedium, ref drySmall, ref drySkipped);

            // ---------- STEP 5: FARMHOUSE GARDEN PROPS (medium/small) ----------
            string[] gardenProps = { Pandazole("Prop_WateringCan_01"), Pandazole("Prop_WateringCan_03"), Pandazole("Prop_Bucket_01"),
                Pandazole("Prop_Bucket_02"), Pandazole("Prop_WoodenBox_02"), Pandazole("Prop_FoodSack_01"), Pandazole("Prop_SmallFarmingTool_01"),
                Pandazole("Prop_SmallFarmingTool_03"), LowPoly("WateringCan_01"), LowPoly("Box_01") };
            ScatterCluster(new Vector3(-51f, 0f, 85f), 5.5f, 7, gardenProps, gSmallProps, road, nodeBounds, obstacles, placed,
                dryRun, ref propCount, ref placedSmall, ref skipped, ref drySmall, ref drySkipped);
            string[] gardenFlowers = { Pandazole("Env_GrassPlant_02"), Pandazole("Env_GrassPlant_03"), Pandazole("Env_GrassPlant_04"),
                Pandazole("Env_GrassPlant_05"), Gridness("Flower"), Gridness("Grass") };
            ScatterCluster(new Vector3(-53f, 0f, 89f), 3.5f, 8, gardenFlowers, gFlowers, road, nodeBounds, obstacles, placed,
                dryRun, ref propCount, ref placedSmall, ref skipped, ref drySmall, ref drySkipped);
            string[] gardenBushes = { Pandazole("Env_Bush_01"), Pandazole("Env_Bush_02") };
            ScatterCluster(new Vector3(-46f, 0f, 83f), 3f, 4, gardenBushes, gBushes, road, nodeBounds, obstacles, placed,
                dryRun, ref propCount, ref placedSmall, ref skipped, ref drySmall, ref drySkipped);

            // ---------- STEP 6: STORAGE CLUTTER (medium/small) ----------
            string[] storageProps = { Pandazole("Prop_WoodenCrates_02"), Pandazole("Prop_WoodenBox_02"), Pandazole("Prop_WoodenBox_03"),
                Pandazole("Prop_WoodenBox_04"), Pandazole("Prop_WoodenBox_05"), Pandazole("Prop_WoodenChest_01"), Pandazole("Prop_WoodenChest_02"),
                Pandazole("Prop_Berrel_04"), Pandazole("Prop_Berrel_05_B"), Pandazole("Prop_Berrel_02_B"), Pandazole("Prop_Pallete_01"),
                Pandazole("Prop_Pallete_02"), Pandazole("Prop_FoodSack_01"), Pandazole("Prop_FoodSack_04"), Pandazole("Prop_Haystack_02"),
                Pandazole("Prop_Haystack_03"), Pandazole("Prop_Wheelbarrow"), Pandazole("Prop_BigFarmingTool_01"), Pandazole("Prop_BigFarmingTool_02"),
                Pandazole("Prop_BigFarmingTool_04"), Pandazole("Prop_BigFarmingTool_05"), Gridness("Crate") };
            int storageClusters = Mathf.RoundToInt(7 * propDensity);
            for (int i = 0; i < storageClusters; i++)
            {
                Vector3 c = new Vector3(46f + (float)(_rng.NextDouble() * 14 - 7), 0f, 44f + (float)(_rng.NextDouble() * 8 - 4));
                ScatterCluster(c, 2.8f, 4, storageProps, gStorage, road, nodeBounds, obstacles, placed,
                    dryRun, ref propCount, ref placedMedium, ref skipped, ref dryMedium, ref drySkipped);
            }

            // ---------- STEP 6b: PRODUCE / HARVEST CLUTTER near the fields and storage ----------
            string[] produce = { Pandazole("food_Apple"), Pandazole("food_Carrot"), Pandazole("food_Potato"), Pandazole("food_Pumpkin"),
                Pandazole("food_Cucumber"), Pandazole("food_RedChili"), Pandazole("food_Grape"), Pandazole("food_Broccoli"),
                Pandazole("food_Garlic"), Pandazole("food_Watermelon"), Pandazole("food_Tangerine"), Pandazole("food_Pear") };
            for (int i = 0; i < Mathf.RoundToInt(5 * propDensity); i++)
            {
                Vector3 c = new Vector3(4f + (float)(_rng.NextDouble() * 34 - 17), 0f, 47f + (float)(_rng.NextDouble() * 10 - 5));
                ScatterCluster(c, 2f, 3, produce, gSmallProps, road, nodeBounds, obstacles, placed,
                    dryRun, ref propCount, ref placedSmall, ref skipped, ref drySmall, ref drySkipped);
            }

            // ---------- STEP 7: ROADSIDE DECORATION (both sides of the whole path) ----------
            BuildRoadside(road, nodeBounds, obstacles, placed, gRoadside, dryRun, decorationDensity,
                ref propCount, ref placedSmall, ref skipped, ref drySmall, ref drySkipped);

            // ---------- STEP 8: BACKGROUND TREES (map edges, silhouette only) ----------
            BuildBackgroundTrees(road, nodeBounds, obstacles, placed, gTrees, gBackground, dryRun, treeDensity,
                ref treeCount, ref placedMedium, ref skipped, ref dryMedium, ref drySkipped);

            // ---------- STEP 9: OPEN-MEADOW FILLER (flowers/bushes/rocks scattered across the remaining grass) ----------
            BuildMeadowFiller(road, nodeBounds, obstacles, placed, gFlowers, gBushes, gRocks, dryRun, decorationDensity,
                ref propCount, ref placedSmall, ref skipped, ref drySmall, ref drySkipped);

            int totalSkipped = skipped + drySkipped;
            if (dryRun)
            {
                Debug.Log($"[FarmMapDecorator] PREVIEW (nothing created) - seed={seed} would place: " +
                    $"large={dryLarge} medium={dryMedium} small={drySmall} skipped(unsafe)={totalSkipped}");
            }
            else
            {
                Debug.Log($"[FarmMapDecorator] DECORATED - seed={seed} buildings={buildingCount} trees={treeCount} " +
                    $"props={propCount} fences={fenceCount} skipped(unsafe)={totalSkipped}");
            }
        }

        // ================================================================ Zone builders ==========================

        private static void BuildCropField(Vector3 center, float sizeX, float sizeZ,
            List<Vector3> road, List<Bounds> nodeBounds, List<Bounds> obstacles, List<(Vector3, float)> placed,
            Transform fieldsParent, Transform fencesParent, bool dryRun, float density,
            ref int propCount, ref int fenceCount, ref int placedSmall, ref int placedMedium, ref int skipped,
            ref int dryLarge, ref int dryMedium, ref int drySmall, ref int drySkipped)
        {
            string[] soilTiles = { Pandazole("Env_FarmLand_01"), Pandazole("Env_FarmLand_02"), Pandazole("Env_FarmLand_03"),
                Pandazole("Env_FarmLand_04"), Pandazole("Env_FarmLand_05"), Pandazole("Env_FarmLand_06"), Pandazole("Env_FarmLand_07"),
                Pandazole("Env_FarmLand_08"), Pandazole("Env_FarmLand_09_Watered"), Pandazole("Env_FarmLand_10_Watered") };
            string[] cropPlants = { Gridness("Plant_Tomato_Small"), Gridness("Plant_Tomato_Medium"), Gridness("Plant_Tomato_Large"),
                Gridness("Tomato_Bunch"), Gridness("Tomato_Bunch_Immature"), Gridness("Plant_Small"), Gridness("Plant_Medium"),
                LowPoly("TomatoPlant_01"), LowPoly("Cabbage_01"), LowPoly("Tomato_03") };

            // Tiles are square and meant to butt edge-to-edge like a quilt - measure the real size instead of
            // guessing, and keep rotation fixed so seams line up (random rotation is what caused the pileup bug).
            Vector2 tileSize = GetFootprintSize(soilTiles[0]);
            float spacing = Mathf.Max(tileSize.x, tileSize.y);
            float tileRadius = 0.5f * Mathf.Sqrt(tileSize.x * tileSize.x + tileSize.y * tileSize.y);
            int cols = Mathf.Max(3, Mathf.FloorToInt(sizeX / spacing));
            int rows = Mathf.Max(2, Mathf.FloorToInt(sizeZ / spacing));
            float startX = center.x - (cols - 1) * spacing * 0.5f;
            float startZ = center.z - (rows - 1) * spacing * 0.5f;

            // Neighbouring tiles are meant to sit flush against each other (that is not "overlap"), so the
            // tile-vs-tile check uses a frozen snapshot of what was already placed BEFORE this field started -
            // buildings/other zones already in there are still respected with their real (measured) radius.
            var obstacleSnapshot = new List<(Vector3, float)>(placed);

            for (int c = 0; c < cols; c++)
            {
                for (int r = 0; r < rows; r++)
                {
                    if (_rng.NextDouble() > density * 0.92) continue; // small deliberate gaps, not a 100% rigid grid

                    Vector3 p = new Vector3(startX + c * spacing, 0f, startZ + r * spacing);

                    if (!TrySnapToGround(p, out Vector3 snapped)) { skipped++; continue; }
                    if (!IsSafe(snapped, tileRadius, RoadSafeMedium, road, nodeBounds, obstacles, obstacleSnapshot)) { skipped++; continue; }

                    if (dryRun) { drySmall++; placed.Add((snapped, tileRadius * 0.5f)); continue; }

                    string soil = soilTiles[_rng.Next(soilTiles.Length)];
                    GameObject tile = Spawn(soil, snapped, 0f, Vector3.one, fieldsParent);
                    if (tile != null)
                    {
                        placedSmall++;
                        // Half-radius only, so this doesn't block the immediate neighbour tile that shares an edge.
                        placed.Add((snapped, tileRadius * 0.5f));

                        if (_rng.NextDouble() < 0.65)
                        {
                            string crop = cropPlants[_rng.Next(cropPlants.Length)];
                            Spawn(crop, snapped, RandomYRotation(), UniformScale(0.9f, 1.1f), fieldsParent);
                        }
                    }
                }
            }

            // Crates / hay bales / tools at the field corner, like a farmer left them there.
            string[] fieldAccents = { Pandazole("Prop_WoodenBox_03"), Pandazole("Prop_Haystack_03"), Pandazole("Prop_WateringCan_03"),
                Pandazole("Prop_SmallFarmingTool_02"), Pandazole("Prop_SmallFarmingTool_04"), Gridness("Harrow"), Gridness("Seed") };
            ScatterCluster(new Vector3(startX - spacing, 0f, startZ - spacing * 0.5f), 2.2f, 4, fieldAccents,
                fieldsParent, road, nodeBounds, obstacles, placed, dryRun, ref propCount, ref placedMedium, ref skipped, ref dryMedium, ref drySkipped);

            // Fence along two edges only (west + south), deliberately leaving north/east open - not boxed in.
            float fenceLen = GetFootprintRadius(Pandazole("Env_WoodFence_01")) * 2f;
            BuildFenceLine(new Vector3(startX - spacing * 0.5f, 0f, startZ - spacing * 0.5f - fenceLen * 0.5f),
                new Vector3(1f, 0f, 0f), cols + 1, fenceLen, fencesParent, road, nodeBounds, obstacles, placed,
                dryRun, ref fenceCount, ref placedMedium, ref skipped, ref dryMedium, ref drySkipped);
            BuildFenceLine(new Vector3(startX - spacing * 0.5f - fenceLen * 0.5f, 0f, startZ - spacing * 0.5f),
                new Vector3(0f, 0f, 1f), rows + 1, fenceLen, fencesParent, road, nodeBounds, obstacles, placed,
                dryRun, ref fenceCount, ref placedMedium, ref skipped, ref dryMedium, ref drySkipped);
        }

        /// <summary>Second, smaller field - a tight tilled-row vegetable garden using the much smaller Gridness
        /// tillage tiles, so the farm reads as having more than one kind of crop area (per spec Zone B: "one or
        /// two fields") without just re-using the big Pandazole soil tiles again.</summary>
        private static void BuildTilledGarden(Vector3 center, float sizeX, float sizeZ,
            List<Vector3> road, List<Bounds> nodeBounds, List<Bounds> obstacles, List<(Vector3, float)> placed,
            Transform fieldsParent, bool dryRun, float density,
            ref int propCount, ref int placedSmall, ref int skipped, ref int drySmall, ref int drySkipped)
        {
            // 2x1/4x1 tillage tiles are literally 2x/4x longer than 1x1 - mixing them into one uniformly-spaced
            // grid is exactly the "different real sizes than assumed" mistake this tool now measures around, so
            // this small garden sticks to the 1x1 module only (the big field already provides tile-size variety).
            string[] tillage = { Gridness("Tillage_1x1") };
            string[] tomatoes = { Gridness("Tomato"), Gridness("Tomato_A"), Gridness("Tomato_B"), Gridness("Tomato_C"),
                Gridness("Tomato_Fallen"), LowPoly("Tomato_03") };

            Vector2 tileSize = GetFootprintSize(Gridness("Tillage_1x1"));
            float spacing = Mathf.Max(tileSize.x, tileSize.y) * 1.02f;
            float tileRadius = 0.5f * Mathf.Sqrt(tileSize.x * tileSize.x + tileSize.y * tileSize.y);
            int cols = Mathf.Max(3, Mathf.FloorToInt(sizeX / spacing));
            int rows = Mathf.Max(3, Mathf.FloorToInt(sizeZ / spacing));
            float startX = center.x - (cols - 1) * spacing * 0.5f;
            float startZ = center.z - (rows - 1) * spacing * 0.5f;

            var obstacleSnapshot = new List<(Vector3, float)>(placed);

            for (int c = 0; c < cols; c++)
            {
                for (int r = 0; r < rows; r++)
                {
                    if (_rng.NextDouble() > density * 0.9) continue;

                    Vector3 p = new Vector3(startX + c * spacing, 0f, startZ + r * spacing);
                    if (!TrySnapToGround(p, out Vector3 snapped)) { skipped++; continue; }
                    if (!IsSafe(snapped, tileRadius, RoadSafeMedium, road, nodeBounds, obstacles, obstacleSnapshot)) { skipped++; continue; }

                    if (dryRun) { drySmall++; placed.Add((snapped, tileRadius * 0.5f)); continue; }

                    string soil = tillage[_rng.Next(tillage.Length)];
                    GameObject tile = Spawn(soil, snapped, 0f, Vector3.one, fieldsParent);
                    if (tile != null)
                    {
                        placedSmall++;
                        placed.Add((snapped, tileRadius * 0.5f));
                        if (_rng.NextDouble() < 0.8)
                        {
                            Spawn(tomatoes[_rng.Next(tomatoes.Length)], snapped, RandomYRotation(), UniformScale(0.9f, 1.1f), fieldsParent);
                        }
                    }
                }
            }
        }

        /// <summary>A dense, fully-fenced tomato patch matching the LowPolyFarmLite pack's own demo composition
        /// (mud ground + tight tomato rows + a full fence ring + rocks/trees just outside) - a third, visually
        /// distinct "growing area" alongside the big soil field and the small tillage garden. Because TomatoPlant_01
        /// is tiny (~0.4-0.6 units), a dense planting here adds a large number of small objects without any of
        /// the earlier overlap bug, since spacing is still measured from the real prefab footprint.</summary>
        private static void BuildFencedTomatoPatch(Vector3 center, float sizeX, float sizeZ,
            List<Vector3> road, List<Bounds> nodeBounds, List<Bounds> obstacles, List<(Vector3, float)> placed,
            Transform fieldsParent, Transform fencesParent, Transform rocksParent, Transform treesParent,
            bool dryRun, float density,
            ref int propCount, ref int fenceCount, ref int treeCount, ref int placedSmall, ref int placedMedium, ref int skipped,
            ref int dryMedium, ref int drySmall, ref int drySkipped)
        {
            // --- Mud ground base (matches the reference: a solid mud rectangle under the tomato rows) ---
            Vector2 mudSize = GetFootprintSize(LowPoly("Mud_01"));
            float mudSpacing = Mathf.Max(mudSize.x, mudSize.y) * 0.98f;
            float mudRadius = 0.5f * Mathf.Sqrt(mudSize.x * mudSize.x + mudSize.y * mudSize.y);
            int mudCols = Mathf.Max(1, Mathf.CeilToInt(sizeX / mudSpacing));
            int mudRows = Mathf.Max(1, Mathf.CeilToInt(sizeZ / mudSpacing));
            float mudStartX = center.x - (mudCols - 1) * mudSpacing * 0.5f;
            float mudStartZ = center.z - (mudRows - 1) * mudSpacing * 0.5f;

            var landmarkSnapshot = new List<(Vector3, float)>(placed);

            // Only actually build this patch if its footprint clears roads/BuildNodes/existing landmarks -
            // check the four corners plus the center before committing to mud+fence+tomatoes here.
            Vector3[] corners = {
                new Vector3(mudStartX - mudSpacing * 0.5f, 0f, mudStartZ - mudSpacing * 0.5f),
                new Vector3(mudStartX + (mudCols - 0.5f) * mudSpacing, 0f, mudStartZ - mudSpacing * 0.5f),
                new Vector3(mudStartX - mudSpacing * 0.5f, 0f, mudStartZ + (mudRows - 0.5f) * mudSpacing),
                new Vector3(mudStartX + (mudCols - 0.5f) * mudSpacing, 0f, mudStartZ + (mudRows - 0.5f) * mudSpacing),
                center
            };
            foreach (var c in corners)
            {
                if (!IsSafe(c, 0.5f, RoadSafeLarge, road, nodeBounds, obstacles, landmarkSnapshot))
                {
                    Debug.LogWarning($"[FarmMapDecorator] Fenced tomato patch skipped - no safe spot found near {center}.");
                    drySkipped++;
                    return;
                }
            }

            // Terrain grass detail (the wind-swaying PT_Grass_02/High_Grass/Poppy layer painted on the whole
            // meadow) renders on top of/through the tomato rows and fence otherwise - clear it under the whole
            // patch (fence ring + a little buffer) so the tilled/mud ground reads clearly, like the reference.
            if (!dryRun)
            {
                ClearTerrainGrassInArea(center, sizeX + 4f, sizeZ + 4f);
            }

            for (int c = 0; c < mudCols; c++)
            {
                for (int r = 0; r < mudRows; r++)
                {
                    Vector3 p = new Vector3(mudStartX + c * mudSpacing, 0f, mudStartZ + r * mudSpacing);
                    if (!TrySnapToGround(p, out Vector3 snapped)) { skipped++; continue; }

                    if (dryRun) { drySmall++; placed.Add((snapped, mudRadius * 0.5f)); continue; }

                    GameObject tile = Spawn(LowPoly("Mud_01"), snapped, 0f, Vector3.one, fieldsParent);
                    if (tile != null) { placedSmall++; placed.Add((snapped, mudRadius * 0.5f)); }
                }
            }

            // --- Dense tomato rows on top of the mud (TomatoPlant_01 is tiny - pack them in) ---
            float plantSpacing = 0.85f;
            int plantCols = Mathf.FloorToInt(sizeX / plantSpacing);
            int plantRows = Mathf.FloorToInt(sizeZ / plantSpacing);
            float plantStartX = center.x - (plantCols - 1) * plantSpacing * 0.5f;
            float plantStartZ = center.z - (plantRows - 1) * plantSpacing * 0.5f;

            for (int c = 0; c < plantCols; c++)
            {
                for (int r = 0; r < plantRows; r++)
                {
                    if (_rng.NextDouble() > density * 0.85) continue; // a few gaps so rows read as planted, not a solid block

                    float jx = (float)(_rng.NextDouble() * 0.3 - 0.15);
                    float jz = (float)(_rng.NextDouble() * 0.3 - 0.15);
                    Vector3 p = new Vector3(plantStartX + c * plantSpacing + jx, 0f, plantStartZ + r * plantSpacing + jz);
                    if (!TrySnapToGround(p, out Vector3 snapped)) { skipped++; continue; }

                    if (dryRun) { drySmall++; continue; }

                    GameObject go = Spawn(LowPoly("TomatoPlant_01"), snapped, RandomYRotation(), UniformScale(0.9f, 1.15f), fieldsParent);
                    if (go != null) { placedSmall++; }
                }
            }

            // --- Full fence ring around the patch, one gate gap on the near side ---
            float halfX = sizeX * 0.5f + 0.6f, halfZ = sizeZ * 0.5f + 0.6f;
            float panelLen = GetFootprintRadius(Gridness("Fence_Middle")) * 2f;
            Vector3 sw = center - new Vector3(halfX, 0f, halfZ);
            int segX = Mathf.Max(2, Mathf.RoundToInt((halfX * 2f) / panelLen));
            int segZ = Mathf.Max(2, Mathf.RoundToInt((halfZ * 2f) / panelLen));

            BuildGridnessFenceLine(sw, new Vector3(1f, 0f, 0f), segX, panelLen, fencesParent, road, nodeBounds, obstacles, placed,
                dryRun, true, ref fenceCount, ref placedMedium, ref skipped, ref dryMedium, ref drySkipped); // south, gate here
            BuildGridnessFenceLine(sw, new Vector3(0f, 0f, 1f), segZ, panelLen, fencesParent, road, nodeBounds, obstacles, placed,
                dryRun, false, ref fenceCount, ref placedMedium, ref skipped, ref dryMedium, ref drySkipped); // west
            BuildGridnessFenceLine(sw + new Vector3(0f, 0f, halfZ * 2f), new Vector3(1f, 0f, 0f), segX, panelLen, fencesParent, road, nodeBounds, obstacles, placed,
                dryRun, false, ref fenceCount, ref placedMedium, ref skipped, ref dryMedium, ref drySkipped); // north
            BuildGridnessFenceLine(sw + new Vector3(halfX * 2f, 0f, 0f), new Vector3(0f, 0f, 1f), segZ, panelLen, fencesParent, road, nodeBounds, obstacles, placed,
                dryRun, false, ref fenceCount, ref placedMedium, ref skipped, ref dryMedium, ref drySkipped); // east

            // --- Rocks + trees ring just outside the fence, like the reference screenshot ---
            string[] ringRocks = { LowPoly("Rock_04") };
            string[] ringTrees = { LowPoly("Tree_04"), Gridness("Tree") };
            int ringCount = Mathf.RoundToInt(6 * Mathf.Clamp(density, 0.5f, 1.5f));
            for (int i = 0; i < ringCount; i++)
            {
                float angle = (float)i / ringCount * Mathf.PI * 2f;
                float ringDist = Mathf.Max(halfX, halfZ) + 2f + (float)_rng.NextDouble() * 2f;
                Vector3 p = center + new Vector3(Mathf.Cos(angle) * ringDist, 0f, Mathf.Sin(angle) * ringDist);
                if (!TrySnapToGround(p, out Vector3 snapped)) { skipped++; continue; }
                if (!IsSafe(snapped, 1.3f, RoadSafeMedium, road, nodeBounds, obstacles, placed)) { skipped++; continue; }

                if (dryRun) { dryMedium++; placed.Add((snapped, 1.3f)); continue; }

                bool rock = i % 2 == 0;
                string prefab = rock ? ringRocks[0] : ringTrees[_rng.Next(ringTrees.Length)];
                GameObject go = Spawn(prefab, snapped, RandomYRotation(), UniformScale(0.85f, 1.15f), rock ? rocksParent : treesParent);
                if (go != null)
                {
                    placedMedium++;
                    placed.Add((snapped, 1.3f));
                    if (!rock) treeCount++; else propCount++;
                }
            }
        }

        /// <summary>Same idea as BuildFenceLine but for the Gridness fence module (Fence_Middle/FenceEnd/FenceGate_A),
        /// used specifically for the fenced tomato patch to match the reference screenshot's fence style.</summary>
        private static void BuildGridnessFenceLine(Vector3 start, Vector3 dir, int segments, float segmentLength,
            Transform parent, List<Vector3> road, List<Bounds> nodeBounds, List<Bounds> obstacles, List<(Vector3, float)> placed,
            bool dryRun, bool includeGate, ref int fenceCount, ref int placedMedium, ref int skipped, ref int dryMedium, ref int drySkipped)
        {
            float safeRadius = segmentLength * 0.5f;
            float yaw = dir.x != 0f ? 0f : 90f;

            var obstacleSnapshot = new List<(Vector3, float)>(placed);

            for (int i = 0; i < segments; i++)
            {
                bool isGateSlot = includeGate && i == segments / 2;

                Vector3 p = start + dir * (i * segmentLength);
                if (!TrySnapToGround(p, out Vector3 snapped)) { skipped++; continue; }
                if (!IsSafe(snapped, safeRadius, RoadSafeMedium, road, nodeBounds, obstacles, obstacleSnapshot)) { skipped++; continue; }

                if (dryRun) { dryMedium++; placed.Add((snapped, safeRadius * 0.5f)); continue; }

                string prefab = isGateSlot ? Gridness("FenceGate_A") : Gridness("Fence_Middle");
                GameObject go = Spawn(prefab, snapped, yaw, Vector3.one, parent);
                if (go != null) { fenceCount++; placedMedium++; placed.Add((snapped, safeRadius * 0.5f)); }
            }
        }

        /// <summary>Zeroes every painted Terrain grass/detail layer (PT_Grass_02, High_Grass, Poppy...) inside a
        /// world-space rectangle. Used so the tilled-ground fenced patches read as clean dirt/mud instead of the
        /// meadow's grass detail mesh rendering through the tomato rows and fence. Edits both Terrain tiles'
        /// detail maps in place (whichever one(s) the rectangle actually overlaps) - never touches the terrain
        /// heightmap/alphamap, only the detail (grass) layer.</summary>
        private static void ClearTerrainGrassInArea(Vector3 center, float sizeX, float sizeZ)
        {
            float minX = center.x - sizeX * 0.5f;
            float maxX = center.x + sizeX * 0.5f;
            float minZ = center.z - sizeZ * 0.5f;
            float maxZ = center.z + sizeZ * 0.5f;

            foreach (var t in Terrain.activeTerrains)
            {
                TerrainData td = t.terrainData;
                Vector3 origin = t.transform.position;

                float locMinX = Mathf.Clamp(minX - origin.x, 0f, td.size.x);
                float locMaxX = Mathf.Clamp(maxX - origin.x, 0f, td.size.x);
                float locMinZ = Mathf.Clamp(minZ - origin.z, 0f, td.size.z);
                float locMaxZ = Mathf.Clamp(maxZ - origin.z, 0f, td.size.z);
                if (locMaxX <= locMinX || locMaxZ <= locMinZ) continue; // rectangle doesn't touch this tile

                int dw = td.detailWidth, dh = td.detailHeight;
                int px0 = Mathf.Clamp(Mathf.FloorToInt(locMinX / td.size.x * dw), 0, dw);
                int px1 = Mathf.Clamp(Mathf.CeilToInt(locMaxX / td.size.x * dw), 0, dw);
                int pz0 = Mathf.Clamp(Mathf.FloorToInt(locMinZ / td.size.z * dh), 0, dh);
                int pz1 = Mathf.Clamp(Mathf.CeilToInt(locMaxZ / td.size.z * dh), 0, dh);
                int w = px1 - px0, h = pz1 - pz0;
                if (w <= 0 || h <= 0) continue;

                int layerCount = td.detailPrototypes.Length;
                for (int layer = 0; layer < layerCount; layer++)
                {
                    int[,] map = td.GetDetailLayer(px0, pz0, w, h, layer);
                    for (int a = 0; a < map.GetLength(0); a++)
                    {
                        for (int b = 0; b < map.GetLength(1); b++)
                        {
                            map[a, b] = 0;
                        }
                    }
                    td.SetDetailLayer(px0, pz0, layer, map);
                }
            }
        }

        private static void BuildOrchard(Vector3 center, List<Vector3> road, List<Bounds> nodeBounds, List<Bounds> obstacles,
            List<(Vector3, float)> placed, Transform orchardParent, Transform bushParent, bool dryRun, float treeDensity,
            ref int treeCount, ref int propCount, ref int placedMedium, ref int placedSmall, ref int skipped,
            ref int dryMedium, ref int drySmall, ref int drySkipped)
        {
            string[] fruitTrees = { Pandazole("Env_Tree_01"), Pandazole("Env_Tree_02"), Pandazole("Env_Tree_03"),
                Pandazole("Env_Tree_04"), Pandazole("Env_Tree_05"), Pandazole("Env_Tree_06"), Pandazole("Env_Tree_07") };
            string[] bushes = { Pandazole("Env_Bush_01"), Pandazole("Env_Bush_02") };

            float treeRadius = GetMaxFootprintRadius(fruitTrees);
            float spacingX = Mathf.Max(5f, treeRadius * 1.7f), spacingZ = Mathf.Max(5f, treeRadius * 1.7f);
            int targetCount = Mathf.RoundToInt(28 * Mathf.Clamp(treeDensity, 0.5f, 1.5f));
            int planted = 0;
            int rowsN = 5, colsN = 7;
            for (int r = 0; r < rowsN; r++)
            {
                for (int c = 0; c < colsN; c++)
                {
                    if (planted >= targetCount) break;
                    float stagger = (r % 2 == 0) ? 0f : spacingX * 0.5f;
                    float jx = (float)(_rng.NextDouble() * 1.0 - 0.5);
                    float jz = (float)(_rng.NextDouble() * 1.0 - 0.5);
                    Vector3 p = new Vector3(center.x - (colsN * spacingX * 0.5f) + c * spacingX + stagger + jx,
                        0f, center.z - (rowsN * spacingZ * 0.5f) + r * spacingZ + jz);

                    if (!TrySnapToGround(p, out Vector3 snapped)) { skipped++; continue; }
                    if (!IsSafe(snapped, treeRadius, RoadSafeMedium, road, nodeBounds, obstacles, placed)) { skipped++; continue; }

                    if (dryRun) { dryMedium++; placed.Add((snapped, treeRadius)); planted++; continue; }

                    string tree = fruitTrees[_rng.Next(fruitTrees.Length)];
                    GameObject go = Spawn(tree, snapped, RandomYRotation(), UniformScale(0.9f, 1.15f), orchardParent);
                    if (go != null) { treeCount++; placedMedium++; placed.Add((snapped, treeRadius)); planted++; }
                }
            }

            ScatterCluster(center + new Vector3(-16f, 0f, 0f), 3f, 5, bushes, bushParent, road, nodeBounds, obstacles, placed,
                dryRun, ref propCount, ref placedSmall, ref skipped, ref drySmall, ref drySkipped);
            string[] orchardCrates = { Pandazole("Prop_WoodenChest_01"), Pandazole("Prop_Berrel_04"), Pandazole("Prop_Wheelbarrow") };
            ScatterCluster(center + new Vector3(16f, 0f, 4f), 3f, 3, orchardCrates, orchardParent, road, nodeBounds, obstacles, placed,
                dryRun, ref propCount, ref placedMedium, ref skipped, ref dryMedium, ref drySkipped);
        }

        private static void BuildRanch(Vector3 center, List<Vector3> road, List<Bounds> nodeBounds, List<Bounds> obstacles,
            List<(Vector3, float)> placed, Transform animalParent, Transform fencesParent, bool dryRun,
            ref int propCount, ref int fenceCount, ref int placedMedium, ref int placedSmall, ref int skipped,
            ref int dryMedium, ref int drySmall, ref int drySkipped)
        {
            float fenceLen = GetFootprintRadius(Pandazole("Env_WoodFence_01")) * 2f;
            float half = fenceLen * 2f;
            Vector3 corner = center - new Vector3(half, 0f, half);

            BuildFenceLine(corner, new Vector3(1f, 0f, 0f), 5, fenceLen, fencesParent, road, nodeBounds, obstacles, placed,
                dryRun, ref fenceCount, ref placedMedium, ref skipped, ref dryMedium, ref drySkipped); // south edge, gate gap left open
            BuildFenceLine(corner, new Vector3(0f, 0f, 1f), 5, fenceLen, fencesParent, road, nodeBounds, obstacles, placed,
                dryRun, ref fenceCount, ref placedMedium, ref skipped, ref dryMedium, ref drySkipped); // west edge
            BuildFenceLine(corner + new Vector3(0f, 0f, half * 2f), new Vector3(1f, 0f, 0f), 5, fenceLen, fencesParent, road, nodeBounds, obstacles, placed,
                dryRun, ref fenceCount, ref placedMedium, ref skipped, ref dryMedium, ref drySkipped); // north edge
            BuildFenceLine(corner + new Vector3(half * 2f, 0f, 0f), new Vector3(0f, 0f, 1f), 5, fenceLen, fencesParent, road, nodeBounds, obstacles, placed,
                dryRun, ref fenceCount, ref placedMedium, ref skipped, ref dryMedium, ref drySkipped); // east edge

            string[] ranchProps = { Pandazole("Prop_Haystack_04"), Pandazole("Prop_Haystack_02"), Pandazole("Prop_AnimalFeeder_01"),
                Pandazole("Prop_AnimalFeeder_02"), Pandazole("Prop_AnimalFeeder_03"), Pandazole("Prop_Bucket_02"), Pandazole("Prop_Bucket_04") };
            ScatterCluster(center, 3.5f, 6, ranchProps, animalParent, road, nodeBounds, obstacles, placed,
                dryRun, ref propCount, ref placedMedium, ref skipped, ref dryMedium, ref drySkipped);
        }

        private static void BuildFenceLine(Vector3 start, Vector3 dir, int segments, float segmentLength,
            Transform parent, List<Vector3> road, List<Bounds> nodeBounds, List<Bounds> obstacles, List<(Vector3, float)> placed,
            bool dryRun, ref int fenceCount, ref int placedMedium, ref int skipped, ref int dryMedium, ref int drySkipped)
        {
            string[] fenceVariants = { Pandazole("Env_WoodFence_01"), Pandazole("Env_WoodFence_02"), Pandazole("Env_WoodFence_03") };
            float safeRadius = segmentLength * 0.5f;
            float yaw = dir.x != 0f ? 90f : 0f;

            var obstacleSnapshot = new List<(Vector3, float)>(placed);

            for (int i = 0; i < segments; i++)
            {
                // Leave a deliberate opening (gate gap) roughly in the middle - fences are never fully sealed.
                if (i == segments / 2 && segments > 2) continue;

                Vector3 p = start + dir * (i * segmentLength);
                if (!TrySnapToGround(p, out Vector3 snapped)) { skipped++; continue; }
                if (!IsSafe(snapped, safeRadius, RoadSafeMedium, road, nodeBounds, obstacles, obstacleSnapshot)) { skipped++; continue; }

                if (dryRun) { dryMedium++; placed.Add((snapped, safeRadius * 0.5f)); continue; }

                GameObject go = Spawn(fenceVariants[_rng.Next(fenceVariants.Length)], snapped, yaw, Vector3.one, parent);
                if (go != null) { fenceCount++; placedMedium++; placed.Add((snapped, safeRadius * 0.5f)); }
            }
        }

        private static void BuildRoadside(List<Vector3> road, List<Bounds> nodeBounds, List<Bounds> obstacles,
            List<(Vector3, float)> placed, Transform parent, bool dryRun, float density,
            ref int propCount, ref int placedSmall, ref int skipped, ref int drySmall, ref int drySkipped)
        {
            string[] roadsideProps = { Gridness("Flower"), Gridness("Grass"), Gridness("GrassGround"), LowPoly("Rock_04"),
                LowPoly("Grass_02"), Pandazole("Env_GrassPlant_02"), Pandazole("Env_GrassPlant_03"), Pandazole("Env_GrassPlant_04"),
                Pandazole("Env_GrassPlant_05"), Pandazole("Env_Bush_01"), Pandazole("Env_Bush_02"), Pandazole("Prop_WoodenBox_04"),
                Pandazole("Prop_FoodSack_01"), Pandazole("Prop_SmallFarmingTool_05"), Pandazole("Prop_SmallFarmingTool_06") };
            float footprintRadius = GetMaxFootprintRadius(roadsideProps);

            float step = Mathf.Max(1.1f, 2.6f / Mathf.Clamp(density, 0.3f, 1.5f));
            for (int i = 1; i < road.Count; i++)
            {
                Vector3 a = road[i - 1], b = road[i];
                float segLen = Vector3.Distance(a, b);
                int samples = Mathf.Max(1, Mathf.RoundToInt(segLen / step));
                Vector3 segDir = (b - a).normalized;
                Vector3 perp = new Vector3(-segDir.z, 0f, segDir.x);

                for (int s = 0; s < samples; s++)
                {
                    float t = (s + 0.5f) / samples;
                    Vector3 basePoint = Vector3.Lerp(a, b, t);
                    float side = (_rng.Next(0, 2) == 0) ? 1f : -1f;
                    float lateral = RoadSafeSmall + footprintRadius + (float)_rng.NextDouble() * 2.5f;
                    Vector3 p = basePoint + perp * side * lateral;

                    if (!TrySnapToGround(p, out Vector3 snapped)) { skipped++; continue; }
                    if (!IsSafe(snapped, footprintRadius, RoadSafeSmall, road, nodeBounds, obstacles, placed)) { skipped++; continue; }

                    if (dryRun) { drySmall++; placed.Add((snapped, footprintRadius)); continue; }

                    string prefab = roadsideProps[_rng.Next(roadsideProps.Length)];
                    GameObject go = Spawn(prefab, snapped, RandomYRotation(), UniformScale(0.85f, 1.15f), parent);
                    if (go != null) { propCount++; placedSmall++; placed.Add((snapped, footprintRadius)); }
                }
            }
        }

        private static void BuildBackgroundTrees(List<Vector3> road, List<Bounds> nodeBounds, List<Bounds> obstacles,
            List<(Vector3, float)> placed, Transform treesParent, Transform backgroundParent, bool dryRun, float treeDensity,
            ref int treeCount, ref int placedMedium, ref int skipped, ref int dryMedium, ref int drySkipped)
        {
            string[] edgeTrees = { Gridness("Tree"), LowPoly("Tree_04"), Pandazole("Env_Tree_01"), Pandazole("Env_Tree_02"),
                Pandazole("Env_Tree_03"), Pandazole("Env_Tree_05"), Pandazole("Env_Tree_07") };
            float footprintRadius = GetMaxFootprintRadius(edgeTrees);
            int count = Mathf.RoundToInt(46 * Mathf.Clamp(treeDensity, 0.4f, 1.5f));

            for (int i = 0; i < count; i++)
            {
                // Bias samples toward the map's X/Z edges (the flat-zone border, just before the mountain ramp)
                // so trees read as a silhouette framing the farm rather than clutter in the middle of gameplay.
                bool onXEdge = _rng.Next(0, 2) == 0;
                float x, z;
                if (onXEdge)
                {
                    x = _rng.Next(0, 2) == 0 ? FlatMinX + 2f : FlatMaxX - 2f;
                    z = FlatMinZ + (float)_rng.NextDouble() * (FlatMaxZ - FlatMinZ);
                }
                else
                {
                    x = FlatMinX + (float)_rng.NextDouble() * (FlatMaxX - FlatMinX);
                    z = _rng.Next(0, 2) == 0 ? FlatMinZ + 2f : FlatMaxZ - 2f;
                }

                Vector3 p = new Vector3(x, 0f, z);
                if (!TrySnapToGround(p, out Vector3 snapped)) { skipped++; continue; }
                if (!IsSafe(snapped, footprintRadius, RoadSafeMedium, road, nodeBounds, obstacles, placed)) { skipped++; continue; }

                if (dryRun) { dryMedium++; placed.Add((snapped, footprintRadius)); continue; }

                string prefab = edgeTrees[_rng.Next(edgeTrees.Length)];
                GameObject go = Spawn(prefab, snapped, RandomYRotation(), UniformScale(0.9f, 1.15f), backgroundParent);
                if (go != null) { treeCount++; placedMedium++; placed.Add((snapped, footprintRadius)); }
            }
        }

        /// <summary>Fills the remaining open grass (away from road/nodes/zones) with loose flower/bush/rock
        /// clusters so the map doesn't read as "empty green" between the composed zones - per spec Section 18/19,
        /// kept as light accents (small clusters, wide spacing) rather than a dense scatter.</summary>
        private static void BuildMeadowFiller(List<Vector3> road, List<Bounds> nodeBounds, List<Bounds> obstacles,
            List<(Vector3, float)> placed, Transform flowersParent, Transform bushesParent, Transform rocksParent,
            bool dryRun, float density, ref int propCount, ref int placedSmall, ref int skipped, ref int drySmall, ref int drySkipped)
        {
            string[] flowerPool = { Gridness("Flower"), Pandazole("Env_GrassPlant_02"), Pandazole("Env_GrassPlant_03") };
            string[] bushPool = { Pandazole("Env_Bush_01"), Pandazole("Env_Bush_02") };
            string[] rockPool = { LowPoly("Rock_04") };

            int clusterAttempts = Mathf.RoundToInt(24 * Mathf.Clamp(density, 0.3f, 1.5f));
            for (int i = 0; i < clusterAttempts; i++)
            {
                float x = FlatMinX + (float)_rng.NextDouble() * (FlatMaxX - FlatMinX);
                float z = FlatMinZ + (float)_rng.NextDouble() * (FlatMaxZ - FlatMinZ);
                Vector3 center = new Vector3(x, 0f, z);

                int roll = _rng.Next(3);
                string[] pool = roll == 0 ? flowerPool : roll == 1 ? bushPool : rockPool;
                Transform parent = roll == 0 ? flowersParent : roll == 1 ? bushesParent : rocksParent;
                int count = roll == 2 ? 2 : 4;

                ScatterCluster(center, 2.2f, count, pool, parent, road, nodeBounds, obstacles, placed,
                    dryRun, ref propCount, ref placedSmall, ref skipped, ref drySmall, ref drySkipped);
            }
        }

        // ================================================================ Placement helpers ======================

        private static void PlaceLandmark(string prefabPath, Vector3 desired, Transform parent, string label,
            List<Vector3> road, List<Bounds> nodeBounds, List<Bounds> obstacles, List<(Vector3, float)> placed,
            ref int counter, bool dryRun, ref int dryLarge, ref int drySkipped)
        {
            float footprintRadius = GetFootprintRadius(prefabPath);
            Vector3 p = desired;
            bool found = false;
            for (int attempt = 0; attempt < 12 && !found; attempt++)
            {
                Vector3 candidate = attempt == 0 ? desired : desired + RandomOffset(attempt * 1.5f);
                if (!TrySnapToGround(candidate, out Vector3 snapped)) continue;
                if (!IsSafe(snapped, footprintRadius, RoadSafeLarge, road, nodeBounds, obstacles, placed)) continue;
                p = snapped;
                found = true;
            }

            if (!found)
            {
                Debug.LogWarning($"[FarmMapDecorator] Landmark '{label}' skipped - no safe spot found near {desired}.");
                drySkipped++;
                return;
            }

            if (dryRun)
            {
                dryLarge++;
                placed.Add((p, footprintRadius));
                Debug.Log($"[FarmMapDecorator] PREVIEW landmark '{label}' -> {p}");
                return;
            }

            GameObject go = Spawn(prefabPath, p, DeliberateFacing(p, road), Vector3.one, parent);
            if (go != null)
            {
                counter++;
                placed.Add((p, footprintRadius));
            }
        }

        private static void ScatterCluster(Vector3 center, float radius, int count, string[] pool, Transform parent,
            List<Vector3> road, List<Bounds> nodeBounds, List<Bounds> obstacles, List<(Vector3, float)> placed,
            bool dryRun, ref int counter, ref int placedBucket, ref int skipped, ref int dryBucket, ref int drySkipped)
        {
            if (pool.Length == 0) return;
            float footprintRadius = GetMaxFootprintRadius(pool);
            for (int i = 0; i < count; i++)
            {
                Vector3 p = center + RandomOffset(radius);
                if (!TrySnapToGround(p, out Vector3 snapped)) { skipped++; continue; }
                if (!IsSafe(snapped, footprintRadius, RoadSafeSmall, road, nodeBounds, obstacles, placed)) { skipped++; continue; }

                if (dryRun) { dryBucket++; placed.Add((snapped, footprintRadius)); continue; }

                string prefab = pool[_rng.Next(pool.Length)];
                GameObject go = Spawn(prefab, snapped, RandomYRotation(), UniformScale(0.9f, 1.1f), parent);
                if (go != null) { counter++; placedBucket++; placed.Add((snapped, footprintRadius)); }
            }
        }

        private static bool IsSafe(Vector3 p, float footprintRadius, float roadSafeBase,
            List<Vector3> road, List<Bounds> nodeBounds, List<Bounds> obstacles, List<(Vector3 pos, float radius)> placed)
        {
            if (p.x < FlatMinX || p.x > FlatMaxX || p.z < FlatMinZ || p.z > FlatMaxZ) return false;

            if (road != null && road.Count > 1)
            {
                float dRoad = DistanceToPolylineXZ(p, road);
                if (dRoad < roadSafeBase + footprintRadius) return false;
            }

            if (nodeBounds != null)
            {
                foreach (var b in nodeBounds)
                {
                    if (DistanceXZToBounds(p, b) < NodeSafeRadius + footprintRadius) return false;
                }
            }

            if (obstacles != null)
            {
                foreach (var b in obstacles)
                {
                    if (DistanceXZToBounds(p, b) < 1.5f + footprintRadius) return false;
                }
            }

            if (placed != null)
            {
                foreach (var (pos, r) in placed)
                {
                    float minDist = (r + footprintRadius) * 0.85f;
                    if ((pos - p).sqrMagnitude < minDist * minDist) return false;
                }
            }

            return true;
        }

        private static bool TrySnapToGround(Vector3 xzOnly, out Vector3 result)
        {
            foreach (var t in Terrain.activeTerrains)
            {
                Vector3 origin = t.transform.position;
                Vector3 size = t.terrainData.size;
                if (xzOnly.x >= origin.x && xzOnly.x <= origin.x + size.x &&
                    xzOnly.z >= origin.z && xzOnly.z <= origin.z + size.z)
                {
                    float y = t.SampleHeight(new Vector3(xzOnly.x, 0f, xzOnly.z)) + origin.y;
                    result = new Vector3(xzOnly.x, y, xzOnly.z);
                    return true;
                }
            }

            result = xzOnly;
            return false;
        }

        private static List<Vector3> RoadPoints(Transform roadRoot)
        {
            var pts = new List<Vector3>();
            if (roadRoot == null) return pts;
            foreach (Transform child in roadRoot)
            {
                pts.Add(child.position);
            }
            return pts;
        }

        private static List<Bounds> BuildNodeBounds(Transform buildNodesRoot)
        {
            var list = new List<Bounds>();
            if (buildNodesRoot == null) return list;
            foreach (Transform child in buildNodesRoot)
            {
                var bn = child.GetComponent<BuildNode>();
                if (bn == null) continue;
                var col = child.GetComponent<Collider>();
                list.Add(col != null ? col.bounds : new Bounds(child.position, new Vector3(2.8f, 1f, 2.8f)));
            }
            return list;
        }

        private static List<Bounds> FindObstacleBounds()
        {
            var list = new List<Bounds>();
            GameObject baseObj = GameObject.Find("PlayerBase");
            if (baseObj != null)
            {
                var r = baseObj.GetComponentInChildren<Renderer>();
                list.Add(r != null ? r.bounds : new Bounds(baseObj.transform.position, new Vector3(10f, 3f, 10f)));
            }

            GameObject bridge = GameObject.Find("PT_Wooden_Bridge_02");
            if (bridge != null)
            {
                var r = bridge.GetComponentInChildren<Renderer>();
                list.Add(r != null ? r.bounds : new Bounds(bridge.transform.position, new Vector3(4f, 2f, 2f)));
            }

            return list;
        }

        private static float DistanceToPolylineXZ(Vector3 p, List<Vector3> pts)
        {
            float best = float.MaxValue;
            for (int i = 1; i < pts.Count; i++)
            {
                float d = DistanceToSegmentXZ(p, pts[i - 1], pts[i]);
                if (d < best) best = d;
            }
            return best;
        }

        private static float DistanceToSegmentXZ(Vector3 p, Vector3 a, Vector3 b)
        {
            Vector2 p2 = new Vector2(p.x, p.z);
            Vector2 a2 = new Vector2(a.x, a.z);
            Vector2 b2 = new Vector2(b.x, b.z);
            Vector2 ab = b2 - a2;
            float t = ab.sqrMagnitude > 0.0001f ? Mathf.Clamp01(Vector2.Dot(p2 - a2, ab) / ab.sqrMagnitude) : 0f;
            Vector2 closest = a2 + ab * t;
            return Vector2.Distance(p2, closest);
        }

        private static float DistanceXZToBounds(Vector3 p, Bounds b)
        {
            float dx = Mathf.Max(0f, Mathf.Max(b.min.x - p.x, p.x - b.max.x));
            float dz = Mathf.Max(0f, Mathf.Max(b.min.z - p.z, p.z - b.max.z));
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        private static Vector3 RandomOffset(float radius)
        {
            double angle = _rng.NextDouble() * Math.PI * 2.0;
            double dist = _rng.NextDouble() * radius;
            return new Vector3((float)(Math.Cos(angle) * dist), 0f, (float)(Math.Sin(angle) * dist));
        }

        private static float RandomYRotation() => (float)(_rng.NextDouble() * 360.0);

        private static Vector3 UniformScale(float min, float max)
        {
            float s = min + (float)_rng.NextDouble() * (max - min);
            return new Vector3(s, s, s);
        }

        /// <summary>Buildings get a deliberate facing (toward the nearest road point) instead of random rotation.</summary>
        private static float DeliberateFacing(Vector3 p, List<Vector3> road)
        {
            if (road == null || road.Count == 0) return 0f;
            Vector3 nearest = road[0];
            float best = float.MaxValue;
            foreach (var r in road)
            {
                float d = (r - p).sqrMagnitude;
                if (d < best) { best = d; nearest = r; }
            }
            Vector3 dir = nearest - p;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.01f) return 0f;
            return Quaternion.LookRotation(dir.normalized, Vector3.up).eulerAngles.y;
        }

        private static Transform NewChild(Transform parent, string name)
        {
            var go = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(go, "Farm Map Decorator");
            go.transform.SetParent(parent, false);
            return go.transform;
        }

        private static GameObject Spawn(string prefabPath, Vector3 position, float yRotation, Vector3 scale, Transform parent)
        {
            GameObject prefab = Load(prefabPath);
            if (prefab == null) return null;

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            Undo.RegisterCreatedObjectUndo(instance, "Farm Map Decorator");
            instance.transform.position = position;
            instance.transform.rotation = Quaternion.Euler(0f, yRotation, 0f);
            instance.transform.localScale = Vector3.Scale(instance.transform.localScale, scale);

            ApplyPerformanceSettings(instance);
            return instance;
        }

        /// <summary>Mobile-friendly defaults: mark static, disable shadows on small/leaf-level renderers so we are
        /// not casting realtime shadows for every flower/crop/rock. Never touches the source prefab asset.</summary>
        private static void ApplyPerformanceSettings(GameObject instance)
        {
            GameObjectUtility.SetStaticEditorFlags(instance, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic);

            var renderers = instance.GetComponentsInChildren<Renderer>(true);
            foreach (var r in renderers)
            {
                var bounds = r.bounds;
                float footprint = Mathf.Max(bounds.size.x, bounds.size.z);
                r.shadowCastingMode = footprint < 1.2f
                    ? UnityEngine.Rendering.ShadowCastingMode.Off
                    : UnityEngine.Rendering.ShadowCastingMode.On;
            }
        }
    }
}
