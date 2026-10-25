using System.Collections.Generic;
using System.IO;
using AlienDefense.Building;
using AlienDefense.Common;
using AlienDefense.Core;
using AlienDefense.Enemies;
using AlienDefense.UI.Minimap;
using AlienDefense.Waves;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AlienDefense.EditorTools
{
    /// <summary>Builds Level_03 - a coastal town crossed by TWO mirrored enemy routes - as its own scene.
    ///
    /// The minimap reference is the blueprint: two serpentine roads, one from each enemy barn at the top (left and
    /// right), each running down through a U, an arch and four long runs joined by U-turns, both ending at the base
    /// at the bottom centre. World frame is the project's: the camera looks down -X, so minimap up = -X and minimap
    /// right = +Z; the left route lives at -Z, the right route is its mirror at +Z.
    ///
    /// Safety: Level_01 and Level_02 are never written. The first run copies Level_02.unity to Level_03.unity (so
    /// every system, UI, prefab reference and LevelCompositionRoot wiring comes along intact), then strips Level 2's
    /// scenery and re-seats the gameplay objects. Level 3 gets its own TerrainData and terrain layers - Level 2's
    /// assets are only read. Editor only; the built scene runs without this class.
    ///
    /// This is the greybox pass (ground, water, roads, routes, spawns, base, BuildNodes, bounds, minimap); houses and
    /// decoration are later passes that add their own groups under the Level_03 root.</summary>
    public static partial class Level03Builder
    {
        private const string Level02Path = "Assets/_Game/Scenes/Levels/Level_02.unity";
        private const string Level03Path = "Assets/_Game/Scenes/Levels/Level_03.unity";
        private const string SceneName = "Level_03";
        private const string DefinitionPath = "Assets/_Game/Data/Levels/Level_03_Definition.asset";
        private const string CatalogPath = "Assets/_Game/Data/Levels/LevelCatalog.asset";
        private const string TerrainFolder = "Assets/_Game/Data/Terrain/Level03";
        private const string ArtFolder = "Assets/_Game/Art/Level03";
        private const string BarnPrefabPath = "Assets/_Game/Prefabs/Level02/Buildings/PF_EnemyBarn.prefab";
        private const string WaterMaterialPath = ArtFolder + "/MAT_Level03_Water.mat";
        private const string GrassLayerPath = "Assets/_Game/Data/Terrain/Level02/TL_Level02_Grass.terrainlayer";

        // ------------------------------------------------------------------ layout (world X, Z)

        private const float GroundY = 2f;      // same playable height as Level_02, so carried-over objects keep their Y
        private const float SeabedY = 1.1f;
        private const float WaterY = 1.72f;
        private const float RoadWidth = 4.4f;  // visual road: the 3.4 m enemy lane plus a shoulder (Level 2 used 5.2)
        private const float LaneHalfWidth = 1.7f;

        private const float MinX = -44f;       // the top shore (user, 2026-10-11; was -34, the barn doors)
        private const float MaxX = 52f;
        private const float MinZ = -54f;
        private const float MaxZ = 54f;

        // Where the UFO may fly (LevelBounds) since the 2026-10-11 widening: past MaxX onto the sand behind the base and
        // past MinZ / MaxZ out over the sea on both sides. MinX..MaxZ above still shape the terrain, so they stay.
        private const float PlayMinX = -54f;   // out over the top bay
        private const float PlayMaxX = 64f;
        private const float PlayHalfZ = 80f;   // out over both side seas (sea-bed tiles past the terrain, see Level03OuterSea)

        private static readonly Vector3 TerrainOrigin = new Vector3(-62f, 0f, -66f);
        private static readonly Vector3 TerrainSize = new Vector3(128f, 30f, 132f);

        private static readonly Vector2 BaseTargetXZ = new Vector2(35f, 0f);

        /// <summary>Left route (minimap left, -Z) in enemy walking order: inside the left barn -> base. The arcs are
        /// sampled every 45 degrees on true circles so the Catmull-Rom spline stays round. The right route is this
        /// list mirrored in Z.</summary>
        private static readonly Vector2[] LeftRoute =
        {
            new Vector2(-42f, -37.5f), new Vector2(-34.5f, -37.5f), new Vector2(-30f, -37.5f), new Vector2(-25f, -37.5f), // barn -> door -> down
            new Vector2(-19.7f, -35.3f), new Vector2(-17.5f, -30f), new Vector2(-19.7f, -24.7f), new Vector2(-25f, -22.5f), // first U (centre -25,-30; r 7.5)
            new Vector2(-28.5f, -22.5f), new Vector2(-31.5f, -22.5f),
            new Vector2(-36.27f, -20.52f), new Vector2(-38.25f, -15.75f), new Vector2(-36.27f, -10.98f), new Vector2(-31.5f, -9f), // arch (centre -31.5,-15.75; r 6.75)
            new Vector2(-27f, -9f), new Vector2(-22f, -9f), new Vector2(-17f, -9f),                                          // inner leg, down
            new Vector2(-12.05f, -11.05f), new Vector2(-10f, -16f),                                                          // corner (centre -17,-16; r 7)
            new Vector2(-10f, -21f), new Vector2(-10f, -26.5f), new Vector2(-10f, -32f), new Vector2(-10f, -38f),             // run 1, to the left
            new Vector2(-8.1f, -42.6f), new Vector2(-3.5f, -44.5f), new Vector2(1.1f, -42.6f), new Vector2(3f, -38f),         // U-turn (centre -3.5,-38; r 6.5)
            new Vector2(3f, -32f), new Vector2(3f, -25f), new Vector2(3f, -18f), new Vector2(3f, -12f),                       // run 2, to the right
            new Vector2(5.05f, -7.05f), new Vector2(10f, -5f), new Vector2(14.95f, -7.05f), new Vector2(17f, -12f),           // U-turn (centre 10,-12; r 7)
            new Vector2(17f, -18f), new Vector2(17f, -25f), new Vector2(17f, -32f), new Vector2(17f, -38f),                   // run 3, to the left
            new Vector2(18.61f, -41.89f), new Vector2(22.5f, -43.5f), new Vector2(26.39f, -41.89f), new Vector2(28f, -38f),   // U-turn (centre 22.5,-38; r 5.5)
            new Vector2(28f, -31f), new Vector2(28f, -24f), new Vector2(28f, -17f), new Vector2(28f, -10.5f),                 // run 4, to the centre
            new Vector2(29.2f, -6f), new Vector2(31.6f, -2.6f), new Vector2(33.6f, -0.8f), new Vector2(35f, 0f),              // final curve -> base
        };

        /// <summary>The twelve BuildNodes of the minimap reference (the user's normalised list mapped into world
        /// space and snapped to the inside of the bend each dot sits in). Every node is 5.5-9 m from a road centre
        /// line: its 1.7 m ring clears the 5.2 m road, and a tower there reaches the road.</summary>
        private static readonly Vector2[] BuildNodeXZ =
        {
            new Vector2(-31.5f, 15.75f),  // 01 inside the right arch
            new Vector2(-25f, 30f),       // 02 inside the right first U
            new Vector2(-21f, 0f),        // 03 centre, between the two inner legs (covers both routes)
            new Vector2(-16.8f, -15.8f),  // 04 inside the left corner
            new Vector2(-3.5f, -37.6f),   // 05 inside the left U-turn (runs 1-2)
            new Vector2(-3.5f, 37.6f),    // 06 inside the right U-turn (runs 1-2)
            new Vector2(10f, -12.4f),     // 07 inside the left U-turn (runs 2-3)
            new Vector2(10f, 12.4f),      // 08 inside the right U-turn (runs 2-3)
            new Vector2(22.5f, -37.6f),   // 09 inside the left U-turn (runs 3-4)
            new Vector2(22.5f, 37.6f),    // 10 inside the right U-turn (runs 3-4)
            new Vector2(34.6f, -8.6f),    // 11 left of the base
            new Vector2(34.6f, 8.6f),     // 12 right of the base
        };

        private static readonly List<string> Report = new List<string>();
        private static List<Vector2> _leftSamples;
        private static List<Vector2> _rightSamples;

        // ------------------------------------------------------------------ entry

        [MenuItem("Tools/Tower Defense/Level 03/Build Level 03 (greybox)")]
        public static void BuildFromMenu()
        {
            Build();
        }

        public static string Build()
        {
            Report.Clear();
            if (EditorApplication.isPlaying)
            {
                Debug.LogError("[Level03Builder] Stop Play mode first.");
                return "playing";
            }

            _leftSamples = Smooth(LeftRoute, 0.75f);
            _rightSamples = Smooth(Mirror(LeftRoute), 0.75f);
            BuildRoadField();

            Scene scene = OpenOrCreateScene();
            Transform root = PrepareHierarchy(scene);
            Transform environment = Group(root, "Environment");
            Terrain terrain = BuildTerrain(environment);
            BuildStylizedWater(environment);
            PlaceGameplay(scene, root, terrain);
            PlaceBarns(root);
            // Scene-local only (Level 3's RenderSettings): the fog copied from Level 2 starts at 55 m, inside this
            // larger map's normal view distance, and hazes the far half of the screen. Push it out past the map.
            RenderSettings.fogStartDistance = 110f;
            RenderSettings.fogEndDistance = 320f;
            BuildRoadMarkings(environment);
            Level02Builder.BuildLevel03Kit();
            Dress(root);
            Report.Add(OcclusionSetup.SetupScene(scene));
            Validate();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            RegisterScene();
            AssetDatabase.SaveAssets();

            string summary = "[Level03Builder] Done.\n" + string.Join("\n", Report);
            Debug.Log(summary);
            return summary;
        }

        // ------------------------------------------------------------------ scene + hierarchy

        private static Scene OpenOrCreateScene()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(Level03Path) == null)
            {
                Scene active = SceneManager.GetActiveScene();
                if (active.isDirty && !string.IsNullOrEmpty(active.path))
                {
                    EditorSceneManager.SaveScene(active);
                }

                AssetDatabase.CopyAsset(Level02Path, Level03Path);
                Report.Add("Created " + Level03Path + " as a copy of Level_02 (Level_02 itself is not modified).");
            }

            return EditorSceneManager.OpenScene(Level03Path, OpenSceneMode.Single);
        }

        private static Transform PrepareHierarchy(Scene scene)
        {
            GameObject root = FindRoot(scene, SceneName) ?? FindRoot(scene, "Level_02");
            if (root == null)
            {
                throw new System.InvalidOperationException("Level_03 scene has no Level_02/Level_03 root.");
            }

            root.name = SceneName;

            // Level 2's scenery does not belong to this map; Level 3's own generated groups are rebuilt every run.
            foreach (string group in new[] { "Buildings", "Vegetation", "FarmProps", "Animals", "Vehicles", "Optimization" })
            {
                Destroy(root.transform.Find(group));
            }

            Transform environment = Group(root.transform, "Environment");
            foreach (string group in new[] { "Cliffs", "Rocks", "DirtRoads", "AmbientFX", "Water" })
            {
                Destroy(environment.Find(group));
            }

            Transform gameplay = Group(root.transform, "Gameplay");
            Destroy(gameplay.Find("EndArea_Level02"));
            Destroy(gameplay.Find("SpawnAreas"));
            return root.transform;
        }

        private static void Destroy(Transform t)
        {
            if (t != null)
            {
                Object.DestroyImmediate(t.gameObject);
            }
        }

        private static GameObject FindRoot(Scene scene, string name)
        {
            foreach (GameObject go in scene.GetRootGameObjects())
            {
                if (go.name == name)
                {
                    return go;
                }
            }

            return null;
        }

        private static Transform Group(Transform parent, string name)
        {
            Transform t = parent.Find(name);
            if (t == null)
            {
                t = new GameObject(name).transform;
                t.SetParent(parent, false);
            }

            return t;
        }

        // ------------------------------------------------------------------ terrain

        private static Terrain BuildTerrain(Transform environment)
        {
            Transform holder = Group(environment, "Terrain");
            Terrain terrain = holder.GetComponentInChildren<Terrain>(true);
            if (terrain == null)
            {
                throw new System.InvalidOperationException("Level_03 scene has no Terrain under Environment/Terrain.");
            }

            EnsureFolder(TerrainFolder);
            EnsureFolder(ArtFolder);

            string dataPath = TerrainFolder + "/TD_Level03.asset";
            var data = AssetDatabase.LoadAssetAtPath<TerrainData>(dataPath);
            if (data == null)
            {
                data = new TerrainData();
                AssetDatabase.CreateAsset(data, dataPath);
            }

            const int heightRes = 257;
            const int alphaRes = 512;
            data.heightmapResolution = heightRes;
            data.size = TerrainSize;
            data.alphamapResolution = alphaRes;
            data.baseMapResolution = 512;

            // Level 3's own layers: 512 px textures tiled every 7-8 m (~70 px per metre) so the ground stays crisp
            // at gameplay distance. Asphalt carries cracks, sand fine ripples, rock strata for the boundary hills.
            TerrainLayer sand = EnsureLayer("TL_Level03_Sand", new Color(0.95f, 0.80f, 0.50f), new Color(0.86f, 0.69f, 0.40f), 8f, 0);
            TerrainLayer asphalt = EnsureLayer("TL_Level03_Asphalt", new Color(0.50f, 0.47f, 0.44f), new Color(0.38f, 0.36f, 0.35f), 7f, 1);
            TerrainLayer wetSand = EnsureLayer("TL_Level03_WetSand", new Color(0.80f, 0.74f, 0.52f), new Color(0.62f, 0.66f, 0.52f), 8f, 0);
            TerrainLayer grass = EnsureLayer("TL_Level03_Grass", new Color(0.47f, 0.76f, 0.26f), new Color(0.36f, 0.66f, 0.22f), 7f, 2);
            TerrainLayer rock = EnsureLayer("TL_Level03_Rock", new Color(0.80f, 0.68f, 0.50f), new Color(0.66f, 0.54f, 0.40f), 10f, 3);
            data.terrainLayers = new[] { sand, asphalt, wetSand, grass, rock };

            // Heights: flat playable ground, lowered where there is water.
            var heights = new float[heightRes, heightRes];
            for (int iz = 0; iz < heightRes; iz++)
            {
                for (int ix = 0; ix < heightRes; ix++)
                {
                    Vector2 p = WorldAt(ix, iz, heightRes);
                    heights[iz, ix] = HeightAt(p) / TerrainSize.y;
                }
            }

            data.SetHeights(0, 0, heights);

            // Splat: asphalt on the two routes, wet sand under and beside water, grass in patches away from both, sand elsewhere.
            int layerCount = data.terrainLayers.Length;
            var alpha = new float[alphaRes, alphaRes, layerCount];
            for (int iz = 0; iz < alphaRes; iz++)
            {
                for (int ix = 0; ix < alphaRes; ix++)
                {
                    Vector2 p = WorldAt(ix, iz, alphaRes);
                    float roadDistance = RoadDistance(p);
                    float road = Mathf.SmoothStep(1f, 0f, Mathf.InverseLerp(RoadWidth * 0.5f - 0.35f, RoadWidth * 0.5f + 0.35f, roadDistance));
                    float wet = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.02f, 0.45f, WaterMask(p)));
                    // Grass: the authored lawns (yards, farm, parks) plus a little noise-driven scrub, never on the
                    // sandy shoulder right beside a road.
                    float n = Mathf.PerlinNoise(p.x * 0.07f + 31.7f, p.y * 0.07f + 7.3f);
                    float grassPatch = Mathf.Max(LawnMask(p), Step(0.55f, 0.68f, n)) * Step(RoadWidth * 0.5f + 0.9f, RoadWidth * 0.5f + 2.4f, roadDistance);
                    float hill = Step(GroundY + 0.5f, GroundY + 2.2f, HeightAt(p));

                    float wAsphalt = road * (1f - hill);
                    float wRock = hill;
                    float wWet = (1f - road) * (1f - hill) * wet;
                    float wGrass = (1f - road) * (1f - hill) * (1f - wet) * grassPatch;
                    float wSand = Mathf.Max(0f, 1f - wAsphalt - wWet - wGrass - wRock);
                    alpha[iz, ix, 0] = wSand;
                    alpha[iz, ix, 1] = wAsphalt;
                    alpha[iz, ix, 2] = wWet;
                    alpha[iz, ix, 3] = wGrass;
                    alpha[iz, ix, 4] = wRock;
                }
            }

            data.SetAlphamaps(0, 0, alpha);
            EditorUtility.SetDirty(data);

            terrain.name = "Terrain_Level03";
            terrain.terrainData = data;
            terrain.transform.position = TerrainOrigin;
            var collider = terrain.GetComponent<TerrainCollider>();
            if (collider != null)
            {
                collider.terrainData = data;
            }

            terrain.Flush();
            Report.Add("Terrain: " + TerrainSize.x + " x " + TerrainSize.z + " m at " + TerrainOrigin + ", own TerrainData " + dataPath
                + ", " + layerCount + " own layers (sand / asphalt / wet sand / grass / rock), road " + RoadWidth + " m wide.");
            return terrain;
        }

        /// <summary>Ground height: flat playable ground, dipping to the seabed under water and rising into rocky
        /// hills on the land sides outside the playable rectangle (right side above the bay, behind the barns), so
        /// the camera never sees the terrain simply end.</summary>
        private static float HeightAt(Vector2 p)
        {
            float water = WaterMask(p);
            if (water > 0f)
            {
                return Mathf.Lerp(GroundY, SeabedY, water);
            }

            float n = Mathf.PerlinNoise(p.x * 0.11f + 5.2f, p.y * 0.11f + 1.7f);
            float right = Step(MaxZ + 2.5f, MaxZ + 9f, p.y + n * 3f) * Step(-7f, -13f, p.x);            // right side, above the bay
            float behindBarns = Step(-48.5f, -55f, p.x + n * 2.5f) * Step(29f, 34f, Mathf.Abs(p.y));    // top corners
            float hill = Mathf.Max(right, behindBarns);
            return GroundY + hill * (6.5f + n * 4.5f);
        }

        /// <summary>Authored lawns (centre x, centre z, radius x, radius z) - painted grass under yards, pens and parks.</summary>
        private static readonly Vector4[] Lawns =
        {
            new Vector4(22.5f, -24f, 2.6f, 9.5f),   // left farm strip (pens)
            new Vector4(-3.5f, 24f, 2.6f, 9.5f),    // right camp strip (pool, tents)
            new Vector4(-22f, 49.5f, 11f, 4.2f),    // top-right farm
            new Vector4(41f, -30f, 8f, 12f),        // bottom-left residential gardens
            new Vector4(41f, 30f, 8f, 12f),         // bottom-right market park
            new Vector4(24.5f, 0f, 5f, 6f),         // fork lighthouse green
            new Vector4(-24f, -47.5f, 8f, 4f),      // old residential yards
            new Vector4(-25f, 0f, 4.5f, 5f),        // green around the centre node
            new Vector4(22.5f, 24f, 2.6f, 9.5f),    // right market strip
            new Vector4(-31.5f, -15.8f, 3.6f, 3.6f), // inside the left arch
            new Vector4(-25f, 30f, 4f, 4f),         // inside the right first U
            new Vector4(-3.5f, -24f, 2.4f, 9f),     // left market strip
            new Vector4(43f, -8f, 5f, 4f),          // base, left flank
            new Vector4(43f, 8f, 5f, 4f),           // base, right flank
            new Vector4(-22f, -47.5f, 9f, 4.2f),    // residential yards, top-left
        };

        private static float LawnMask(Vector2 p)
        {
            float best = 0f;
            float n = Mathf.PerlinNoise(p.x * 0.2f + 9.1f, p.y * 0.2f + 4.4f) * 0.25f;
            for (int i = 0; i < Lawns.Length; i++)
            {
                float d = new Vector2((p.x - Lawns[i].x) / Lawns[i].z, (p.y - Lawns[i].y) / Lawns[i].w).magnitude + n;
                best = Mathf.Max(best, Step(1.1f, 0.8f, d));
            }

            return best;
        }

        private static Vector2 WorldAt(int ix, int iz, int resolution)
        {
            float u = ix / (resolution - 1f);
            float v = iz / (resolution - 1f);
            return new Vector2(TerrainOrigin.x + u * TerrainSize.x, TerrainOrigin.z + v * TerrainSize.z);
        }

        /// <summary>0 = land, 1 = open water. Generous water shapes (left sea, top bay, lower-right bay, a canal into
        /// each route's gap between runs 2 and 3, a harbour basin between the routes), clipped by a clearance band
        /// around both roads, every BuildNode and the base - so the shoreline follows the gameplay layout instead of
        /// cutting it, and stays irregular (noise).</summary>
        private static float WaterMask(Vector2 p)
        {
            float n = (Mathf.PerlinNoise(p.x * 0.09f + 3.1f, p.y * 0.09f + 11.9f) * 2f - 1f) * 1.25f
                + (Mathf.PerlinNoise(p.x * 0.31f + 7.7f, p.y * 0.31f + 2.3f) * 2f - 1f) * 0.45f;
            float shape = 0f;
            float coast = Mathf.Sin(p.x * 0.13f) * 1.9f + Mathf.Sin(p.x * 0.31f + 1.7f) * 0.8f; // large-scale wobble of both sea coasts
            shape = Mathf.Max(shape, Step(-47.8f, -51.2f, p.y + n * 1.6f + coast));                               // left sea
            shape = Mathf.Max(shape, Step(-43f, -46.5f, p.x + n * 1.4f) * Step(33f, 27f, Mathf.Abs(p.y)));         // top bay, between the barns
            shape = Mathf.Max(shape, Step(47.8f, 51.2f, p.y + n * 1.6f - coast) * Step(-9f, -3f, p.x));           // lower-right bay
            shape = Mathf.Max(shape, Step(1.1f, 0.75f, new Vector2((p.x - 33f) / 6.5f, (p.y + 49.5f) / 3.6f).magnitude)); // left pier cove
            shape = Mathf.Max(shape, Step(1.1f, 0.75f, new Vector2((p.x + 12f) / 5.5f, (p.y + 49.8f) / 3.4f).magnitude)); // left small cove
            shape = Mathf.Max(shape, Step(1.1f, 0.75f, new Vector2((p.x - 31f) / 6.5f, (p.y - 49.5f) / 3.6f).magnitude)); // right pier cove
            var meander = new Vector2(p.x + Mathf.Sin(p.y * 0.21f) * 1.25f, p.y);                                   // the canals wander instead of running ruler-straight
            shape = Mathf.Max(shape, Capsule(meander, new Vector2(10f, -70f), new Vector2(10f, -22f), 3.4f + n * 0.5f)); // left canal
            shape = Mathf.Max(shape, Capsule(meander, new Vector2(10f, 70f), new Vector2(10f, 22f), 3.4f + n * 0.5f));   // right canal
            Vector2 basin = p - new Vector2(-3.5f, 0f);
            shape = Mathf.Max(shape, Step(1.15f, 0.8f, new Vector2(basin.x / 7.5f, basin.y / 7f).magnitude + n * 0.12f)); // harbour basin
            shape = Mathf.Max(shape, Step(1.15f, 0.8f, new Vector2((p.x - 12.5f) / 6.5f, p.y / 9f).magnitude + n * 0.1f));   // fork lagoon, behind the lighthouse

            if (shape <= 0f)
            {
                return 0f;
            }

            float clear = Step(RoadWidth * 0.5f + 1.0f, RoadWidth * 0.5f + 2.6f, RoadDistance(p));
            float nodeDistance = float.MaxValue;
            for (int i = 0; i < BuildNodeXZ.Length; i++)
            {
                nodeDistance = Mathf.Min(nodeDistance, Vector2.Distance(p, BuildNodeXZ[i]));
            }

            clear *= Step(3.6f, 5f, nodeDistance);
            clear *= Step(10f, 13f, Vector2.Distance(p, BaseTargetXZ + new Vector2(6f, 0f)));
            return shape * clear;
        }

        /// <summary>Smooth 0..1 ramp from <paramref name="zero"/> to <paramref name="one"/> (either order).</summary>
        private static float Step(float zero, float one, float value)
        {
            return Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(zero, one, value));
        }

        private static float Capsule(Vector2 p, Vector2 a, Vector2 b, float radius)
        {
            return Step(radius + 1.2f, radius - 0.3f, DistanceToSegment(p, a, b));
        }

        private static float[,] _roadField;

        /// <summary>Distance to the nearest route centre line, read from a 0.5 m field built once per run (the
        /// terrain passes ask for it a few hundred thousand times).</summary>
        private static float RoadDistance(Vector2 p)
        {
            if (_roadField == null)
            {
                return ExactRoadDistance(p);
            }

            float fx = Mathf.Clamp((p.x - TerrainOrigin.x) * 2f, 0f, _roadField.GetLength(0) - 1.001f);
            float fz = Mathf.Clamp((p.y - TerrainOrigin.z) * 2f, 0f, _roadField.GetLength(1) - 1.001f);
            int ix = (int)fx;
            int iz = (int)fz;
            float tx = fx - ix;
            float tz = fz - iz;
            return Mathf.Lerp(Mathf.Lerp(_roadField[ix, iz], _roadField[ix + 1, iz], tx), Mathf.Lerp(_roadField[ix, iz + 1], _roadField[ix + 1, iz + 1], tx), tz);
        }

        private static float ExactRoadDistance(Vector2 p)
        {
            return Mathf.Min(DistanceToPolyline(p, _leftSamples), DistanceToPolyline(p, _rightSamples));
        }

        private static void BuildRoadField()
        {
            _roadField = null;
            int nx = Mathf.CeilToInt(TerrainSize.x * 2f) + 2;
            int nz = Mathf.CeilToInt(TerrainSize.z * 2f) + 2;
            var field = new float[nx, nz];
            for (int ix = 0; ix < nx; ix++)
            {
                for (int iz = 0; iz < nz; iz++)
                {
                    field[ix, iz] = ExactRoadDistance(new Vector2(TerrainOrigin.x + ix * 0.5f, TerrainOrigin.z + iz * 0.5f));
                }
            }

            _roadField = field;
        }

        /// <summary>A Level 3 terrain layer with a generated 512 px tileable texture (kind: 0 sand, 1 asphalt,
        /// 2 grass, 3 rock). Imported with mipmaps, trilinear filtering and anisotropic level 4, so it stays crisp
        /// at the gameplay camera's grazing angle. The layer and its texture are (re)written every run.</summary>
        private static TerrainLayer EnsureLayer(string name, Color a, Color b, float tile, int kind)
        {
            string layerPath = TerrainFolder + "/" + name + ".terrainlayer";
            string texturePath = ArtFolder + "/" + name.Replace("TL_", "T_") + "_512.png";
            if (!File.Exists(texturePath))
            {
                const int size = 512;
                var texture = new Texture2D(size, size, TextureFormat.RGB24, false);
                var pixels = new Color[size * size];
                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        float u = x / (float)size * Mathf.PI * 2f;
                        float v = y / (float)size * Mathf.PI * 2f;
                        float grain = Hash(x, y);
                        float fine = Hash(x / 2, y / 2);
                        float blotch = 0.5f + 0.25f * Mathf.Sin(u * 2f + Mathf.Sin(v * 3f)) + 0.25f * Mathf.Cos(v * 2f + Mathf.Sin(u * 4f));
                        float t;
                        float dark = 0f;
                        switch (kind)
                        {
                            case 1: // asphalt: speckle, soft patches, a few thin cracks
                                t = blotch * 0.45f + grain * 0.35f + fine * 0.2f;
                                float crackA = Mathf.Abs(Mathf.Sin(u * 3f + Mathf.Sin(v * 5f) * 1.3f));
                                float crackB = Mathf.Abs(Mathf.Sin(v * 4f + Mathf.Sin(u * 3f) * 1.6f + 1.3f));
                                dark = Mathf.Max(Mathf.SmoothStep(0.03f, 0f, crackA), Mathf.SmoothStep(0.022f, 0f, crackB)) * 0.42f;
                                break;
                            case 2: // grass: short vertical strokes over soft patches
                                t = blotch * 0.5f + Hash(x, y / 6) * 0.3f + grain * 0.2f;
                                break;
                            case 3: // rock: horizontal strata with broken edges
                                float strata = 0.5f + 0.5f * Mathf.Sin(v * 9f + Mathf.Sin(u * 2f) * 1.4f + fine * 0.8f);
                                t = strata * 0.55f + blotch * 0.25f + grain * 0.2f;
                                dark = Mathf.SmoothStep(0.08f, 0f, Mathf.Abs(strata - 0.5f)) * 0.12f;
                                break;
                            default: // sand: wind ripples plus fine grain
                                float ripple = 0.5f + 0.5f * Mathf.Sin(v * 14f + Mathf.Sin(u * 3f) * 2.2f);
                                t = blotch * 0.4f + ripple * 0.22f + grain * 0.2f + fine * 0.18f;
                                break;
                        }

                        Color c = Color.Lerp(a, b, Mathf.Clamp01(t));
                        pixels[y * size + x] = Color.Lerp(c, new Color(c.r * 0.45f, c.g * 0.45f, c.b * 0.45f), dark);
                    }
                }

                texture.SetPixels(pixels);
                texture.Apply();
                File.WriteAllBytes(texturePath, texture.EncodeToPNG());
                Object.DestroyImmediate(texture);
                AssetDatabase.ImportAsset(texturePath);
                var importer = (TextureImporter)AssetImporter.GetAtPath(texturePath);
                importer.mipmapEnabled = true;
                importer.filterMode = FilterMode.Trilinear;
                importer.anisoLevel = 4;
                importer.wrapMode = TextureWrapMode.Repeat;
                importer.maxTextureSize = 512;
                importer.SaveAndReimport();
            }

            var layer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(layerPath);
            if (layer == null)
            {
                layer = new TerrainLayer();
                AssetDatabase.CreateAsset(layer, layerPath);
            }

            layer.diffuseTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
            layer.tileSize = new Vector2(tile, tile);
            layer.smoothness = 0f;
            layer.metallic = 0f;
            EditorUtility.SetDirty(layer);
            return layer;
        }

        private static float Hash(int x, int y)
        {
            unchecked
            {
                uint h = (uint)(x * 374761393 + y * 668265263);
                h = (h ^ (h >> 13)) * 1274126177u;
                return ((h ^ (h >> 16)) & 0xFFFF) / 65535f;
            }
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        // ------------------------------------------------------------------ water

        private static void BuildWater(Transform environment)
        {
            Transform water = Group(environment, "Water");
            Material material = EnsureWaterMaterial();
            GameObject surface = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Object.DestroyImmediate(surface.GetComponent<Collider>()); // the UFO and enemies use the terrain, never the water
            surface.name = "WaterSurface";
            surface.transform.SetParent(water, false);
            surface.transform.SetPositionAndRotation(
                new Vector3(TerrainOrigin.x + TerrainSize.x * 0.5f, WaterY, TerrainOrigin.z + TerrainSize.z * 0.5f),
                Quaternion.Euler(90f, 0f, 0f));
            surface.transform.localScale = new Vector3(520f, 520f, 1f); // far past the terrain: the sea runs to the horizon
            var renderer = surface.GetComponent<MeshRenderer>();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            if (material != null)
            {
                renderer.sharedMaterial = material;
            }
            else
            {
                Report.Add("WARNING: water material not found at " + WaterMaterialPath + " - the water surface uses the default material.");
            }

            Report.Add("Water: one surface quad at y " + WaterY + " (seabed " + SeabedY + "); visible wherever the terrain dips - left sea, top bay, "
                + "lower-right bay, two canals, harbour basin.");
        }

        /// <summary>Level 3's own water material: plain opaque URP Lit in the reference's turquoise, glossy. Cheap on
        /// mobile (no transparency, no depth/opaque texture) - a later pass may swap in a proper water shader.</summary>
        private static Material EnsureWaterMaterial()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(WaterMaterialPath);
            if (material != null)
            {
                return material;
            }

            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                return null;
            }

            EnsureFolder(ArtFolder);
            material = new Material(shader);
            material.SetColor("_BaseColor", new Color(0.10f, 0.78f, 0.80f, 1f));
            material.SetFloat("_Smoothness", 0.82f);
            material.SetFloat("_Metallic", 0f);
            AssetDatabase.CreateAsset(material, WaterMaterialPath);
            return material;
        }

        // ------------------------------------------------------------------ gameplay

        private static void PlaceGameplay(Scene scene, Transform root, Terrain terrain)
        {
            Transform gameplay = Group(root, "Gameplay");
            Transform objectives = gameplay.Find("Objectives");
            Transform spawners = gameplay.Find("EnemySpawners");
            if (objectives == null || spawners == null)
            {
                throw new System.InvalidOperationException("Gameplay/Objectives or Gameplay/EnemySpawners missing.");
            }

            // Level bounds.
            Transform boundsTransform = gameplay.Find("LevelBounds");
            LevelBounds bounds = boundsTransform != null ? boundsTransform.GetComponent<LevelBounds>() : null;
            if (bounds != null)
            {
                var so = new SerializedObject(bounds);
                so.FindProperty("_center").vector2Value = new Vector2((PlayMinX + PlayMaxX) * 0.5f, 0f);
                so.FindProperty("_extents").vector2Value = new Vector2((PlayMaxX - PlayMinX) * 0.5f, PlayHalfZ);
                so.ApplyModifiedPropertiesWithoutUndo();
                boundsTransform.position = new Vector3(0f, GroundY, 0f);
                Report.Add("LevelBounds: X " + PlayMinX + ".." + PlayMaxX + ", Z -" + PlayHalfZ + ".." + PlayHalfZ + ".");
            }

            // Base, landing pad, player, camera: Level 2's arrangement moved as one block onto the new base target.
            // Ground height is the same as Level 2's, so heights are kept.
            Transform baseTarget = objectives.Find("BaseTarget");
            if (baseTarget != null)
            {
                Vector3 delta = new Vector3(BaseTargetXZ.x - baseTarget.position.x, 0f, BaseTargetXZ.y - baseTarget.position.z);
                if (delta.sqrMagnitude > 0.0001f)
                {
                    foreach (Transform child in objectives)
                    {
                        if (child.name != "Markers")
                        {
                            child.position += delta;
                        }
                    }

                    foreach (string spawnName in new[] { "PlayerSpawn_Level02", "PlayerSpawn_Level03" })
                    {
                        Transform spawn = gameplay.Find(spawnName);
                        if (spawn != null)
                        {
                            spawn.position += delta;
                        }
                    }

                    foreach (string rootName in new[] { "Player", "CameraRig" })
                    {
                        GameObject go = FindRoot(scene, rootName);
                        if (go != null)
                        {
                            go.transform.position += delta;
                        }
                    }

                    Report.Add("Base block (BaseTarget, PlayerBase, UFOLanding, player start, Player, CameraRig) moved by " + delta + " -> base target " + BaseTargetXZ + ".");
                }
            }

            Transform playerSpawn = gameplay.Find("PlayerSpawn_Level02");
            if (playerSpawn != null)
            {
                playerSpawn.name = "PlayerSpawn_Level03";
            }

            Destroy(objectives.Find("Markers"));

            // Enemy routes.
            Transform leftPath = spawners.Find("EnemyPath_Left") ?? spawners.Find("EnemyPath");
            if (leftPath == null)
            {
                throw new System.InvalidOperationException("Gameplay/EnemySpawners/EnemyPath missing.");
            }

            leftPath.name = "EnemyPath_Left";
            Transform rightPath = spawners.Find("EnemyPath_Right");
            if (rightPath == null)
            {
                rightPath = Object.Instantiate(leftPath.gameObject, spawners).transform;
                rightPath.name = "EnemyPath_Right";
            }

            EnemyPath3D left = RebuildPath(leftPath, _leftSamples, terrain, "left");
            EnemyPath3D right = RebuildPath(rightPath, _rightSamples, terrain, "right");

            // Spawn markers (plain transforms for designers; the real spawns are each path's first waypoint).
            Transform spawnAreas = Group(gameplay, "SpawnAreas");
            Marker(spawnAreas, "EnemySpawn_Left", LeftRoute[0], terrain);
            Marker(spawnAreas, "EnemySpawn_Right_BossRoute", new Vector2(LeftRoute[0].x, -LeftRoute[0].y), terrain);

            BuildNode[] nodes = PlaceBuildNodes(gameplay);
            WireSystems(left, right, nodes, bounds);
        }

        private static EnemyPath3D RebuildPath(Transform path, List<Vector2> dense, Terrain terrain, string label)
        {
            path.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            for (int i = path.childCount - 1; i >= 0; i--)
            {
                Object.DestroyImmediate(path.GetChild(i).gameObject);
            }

            // A waypoint every 4 m on straights and whenever the heading has turned 20 degrees, so enemies (who walk
            // straight between waypoints) follow the U-turns instead of cutting them.
            var points = new List<Vector2> { dense[0] };
            float accumulated = 0f;
            Vector2 lastHeading = (dense[1] - dense[0]).normalized;
            for (int i = 1; i < dense.Count; i++)
            {
                accumulated += Vector2.Distance(dense[i - 1], dense[i]);
                Vector2 heading = i + 1 < dense.Count ? (dense[i + 1] - dense[i]).normalized : lastHeading;
                bool turned = Vector2.Angle(lastHeading, heading) >= 20f && accumulated >= 1.5f;
                if (accumulated >= 4f || turned || i == dense.Count - 1)
                {
                    points.Add(dense[i]);
                    accumulated = 0f;
                    lastHeading = heading;
                }
            }

            var waypoints = new Transform[points.Count];
            float length = 0f;
            for (int i = 0; i < points.Count; i++)
            {
                string suffix = i == 0 ? "_EnemySpawn" : i == points.Count - 1 ? "_Base" : "";
                var w = new GameObject("WP_" + i.ToString("00") + suffix).transform;
                w.SetParent(path, false);
                w.position = new Vector3(points[i].x, Ground(terrain, points[i]), points[i].y);
                waypoints[i] = w;
                if (i > 0)
                {
                    length += Vector2.Distance(points[i - 1], points[i]);
                }
            }

            var component = path.GetComponent<EnemyPath3D>();
            var so = new SerializedObject(component);
            SerializedProperty array = so.FindProperty("_waypoints");
            array.arraySize = waypoints.Length;
            for (int i = 0; i < waypoints.Length; i++)
            {
                array.GetArrayElementAtIndex(i).objectReferenceValue = waypoints[i];
            }

            so.FindProperty("_laneHalfWidth").floatValue = LaneHalfWidth;
            so.ApplyModifiedPropertiesWithoutUndo();
            Report.Add("Enemy path (" + label + "): " + waypoints.Length + " waypoints, " + length.ToString("F0") + " m, spawn " + points[0]
                + " -> base " + points[points.Count - 1] + ".");
            return component;
        }

        private static BuildNode[] PlaceBuildNodes(Transform gameplay)
        {
            Transform nodes = gameplay.Find("BuildNodes");
            if (nodes == null)
            {
                throw new System.InvalidOperationException("Gameplay/BuildNodes missing.");
            }

            var list = new List<BuildNode>();
            foreach (Transform child in nodes)
            {
                var node = child.GetComponent<BuildNode>();
                if (node != null)
                {
                    list.Add(node);
                }
            }

            if (list.Count == 0)
            {
                throw new System.InvalidOperationException("No BuildNode instance to duplicate.");
            }

            float y = list[0].transform.position.y; // same ground height as Level 2
            int created = 0;
            while (list.Count < BuildNodeXZ.Length)
            {
                // Same as Ctrl+D in the Hierarchy: keeps the prefab connection, the scene-added ChannelUI child and
                // the overrides pointing at it (a fresh InstantiatePrefab would lose all three).
                Selection.activeGameObject = list[list.Count - 1].gameObject;
                Unsupported.DuplicateGameObjectsUsingPasteboard();
                GameObject copy = Selection.activeGameObject;
                copy.transform.SetParent(nodes, true);
                list.Add(copy.GetComponent<BuildNode>());
                created++;
            }

            Selection.activeGameObject = null;
            for (int i = BuildNodeXZ.Length; i < list.Count; i++)
            {
                list[i].gameObject.SetActive(false);
            }

            var placed = new BuildNode[BuildNodeXZ.Length];
            for (int i = 0; i < placed.Length; i++)
            {
                Transform node = list[i].transform;
                node.name = "BuildNode_" + (i + 1).ToString("00");
                node.SetSiblingIndex(i);
                node.gameObject.SetActive(true);
                node.position = new Vector3(BuildNodeXZ[i].x, y, BuildNodeXZ[i].y);
                placed[i] = list[i];
                Report.Add("BuildNode" + (i + 1).ToString("00") + " = (" + node.position.x.ToString("F1") + ", " + node.position.y.ToString("F2") + ", "
                    + node.position.z.ToString("F1") + ")");
            }

            Report.Add("BuildNodes: " + placed.Length + " (" + created + " duplicated from the existing instances).");
            return placed;
        }

        private static void WireSystems(EnemyPath3D left, EnemyPath3D right, BuildNode[] nodes, LevelBounds bounds)
        {
            void AssignArray(Object target, string property, Object[] values)
            {
                var so = new SerializedObject(target);
                SerializedProperty array = so.FindProperty(property);
                if (array == null)
                {
                    Report.Add("WARNING: " + target.GetType().Name + " has no " + property);
                    return;
                }

                array.arraySize = values.Length;
                for (int i = 0; i < values.Length; i++)
                {
                    array.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
                }

                so.ApplyModifiedPropertiesWithoutUndo();
            }

            foreach (var c in Object.FindObjectsByType<PlayerBuildNodeProximityController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                AssignArray(c, "_nodes", nodes);
            }

            foreach (var c in Object.FindObjectsByType<BuildNodeVisualCoordinator>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                AssignArray(c, "_nodes", nodes);
            }

            // Waves: left route is Path (and the final boss route), right route is the extra one.
            var waves = Object.FindFirstObjectByType<WaveController>(FindObjectsInactive.Include);
            if (waves != null)
            {
                var so = new SerializedObject(waves);
                so.FindProperty("_path").objectReferenceValue = left;
                so.FindProperty("_bossPath").objectReferenceValue = left; // final boss comes down the LEFT route (user, 2026-10-08)
                so.ApplyModifiedPropertiesWithoutUndo();
                AssignArray(waves, "_additionalPaths", new Object[] { right });
                Report.Add("WaveController: Path = left route, Additional Paths = right route, Boss Path = left route.");
            }

            // Minimap: same style as Level 2, plus a second road line and spawn marker for the right route.
            var presenter = Object.FindFirstObjectByType<MinimapPresenter>(FindObjectsInactive.Include);
            var controller = Object.FindFirstObjectByType<MinimapController>(FindObjectsInactive.Include);
            if (presenter != null && controller != null)
            {
                var presenterSo = new SerializedObject(presenter);
                presenterSo.FindProperty("_enemyPath").objectReferenceValue = left;
                presenterSo.FindProperty("_mapBounds").objectReferenceValue = bounds;
                presenterSo.ApplyModifiedPropertiesWithoutUndo();
                AssignArray(presenter, "_additionalEnemyPaths", new Object[] { right });
                AssignArray(presenter, "_buildNodes", nodes);

                var controllerSo = new SerializedObject(controller);
                var pathGraphic = controllerSo.FindProperty("_pathGraphic").objectReferenceValue as MinimapPathGraphic;
                var spawnMarker = controllerSo.FindProperty("_spawnMarker").objectReferenceValue as RectTransform;
                if (pathGraphic != null)
                {
                    Transform parent = pathGraphic.transform.parent;
                    Transform extra = parent.Find("PathGraphic_Right");
                    if (extra == null)
                    {
                        extra = Object.Instantiate(pathGraphic.gameObject, parent).transform;
                        extra.name = "PathGraphic_Right";
                        extra.SetSiblingIndex(pathGraphic.transform.GetSiblingIndex() + 1);
                    }

                    AssignArray(controller, "_extraPathGraphics", new Object[] { extra.GetComponent<MinimapPathGraphic>() });
                }

                if (spawnMarker != null)
                {
                    Transform parent = spawnMarker.parent;
                    Transform extra = parent.Find("SpawnMarker_Right");
                    if (extra == null)
                    {
                        extra = Object.Instantiate(spawnMarker.gameObject, parent).transform;
                        extra.name = "SpawnMarker_Right";
                        extra.SetSiblingIndex(spawnMarker.GetSiblingIndex() + 1);
                    }

                    AssignArray(controller, "_extraSpawnMarkers", new Object[] { extra });
                }

                Report.Add("Minimap: both routes drawn from their EnemyPath3D (left = Path Graphic, right = PathGraphic_Right), 12 BuildNode dots, "
                    + "mapped area = LevelBounds.");
            }

            // Started without Bootstrap (editor shortcut) this scene must play Level 3's data, not Level 1's.
            var compositionRoot = Object.FindFirstObjectByType<LevelCompositionRoot>(FindObjectsInactive.Include);
            var definition = AssetDatabase.LoadAssetAtPath<AlienDefense.Data.LevelDefinition>(DefinitionPath);
            if (compositionRoot != null && definition != null)
            {
                var so = new SerializedObject(compositionRoot);
                SerializedProperty property = so.FindProperty("_developmentLevelDefinition");
                if (property != null)
                {
                    property.objectReferenceValue = definition;
                    so.ApplyModifiedPropertiesWithoutUndo();
                }
            }
        }

        private static void Marker(Transform parent, string name, Vector2 xz, Terrain terrain)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(xz.x, Ground(terrain, xz), xz.y);
        }

        private static float Ground(Terrain terrain, Vector2 xz)
        {
            return terrain.transform.position.y + terrain.SampleHeight(new Vector3(xz.x, 0f, xz.y));
        }

        // ------------------------------------------------------------------ barns

        /// <summary>The two enemy barns (Level 2's PF_EnemyBarn, instantiated - the prefab is not modified), door
        /// toward +X, so enemies spawn inside and walk out through the door onto the road.</summary>
        private static void PlaceBarns(Transform root)
        {
            Transform buildings = Group(root, "Buildings");
            Transform barns = Group(buildings, "Barns");
            for (int i = barns.childCount - 1; i >= 0; i--)
            {
                Object.DestroyImmediate(barns.GetChild(i).gameObject);
            }

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(BarnPrefabPath);
            if (prefab == null)
            {
                Report.Add("WARNING: " + BarnPrefabPath + " not found - no barns placed.");
                return;
            }

            foreach (float side in new[] { -1f, 1f })
            {
                var barn = (GameObject)PrefabUtility.InstantiatePrefab(prefab, barns);
                barn.name = side < 0f ? "EnemyBarn_Left" : "EnemyBarn_Right";
                barn.transform.SetPositionAndRotation(
                    new Vector3(LeftRoute[0].x + 1.5f, GroundY, Mathf.Abs(LeftRoute[0].y) * side),
                    Quaternion.Euler(0f, 90f, 0f));
            }

            Report.Add("Enemy barns: 2 x PF_EnemyBarn at the two spawns (doors toward +X).");
        }

        // ------------------------------------------------------------------ validation + registration

        private static void Validate()
        {
            float ring = 1.7f;
            for (int i = 0; i < BuildNodeXZ.Length; i++)
            {
                Vector2 node = BuildNodeXZ[i];
                float distance = RoadDistance(node);
                float gap = distance - RoadWidth * 0.5f - ring;
                bool inside = node.x >= MinX && node.x <= MaxX && node.y >= MinZ && node.y <= MaxZ;
                float nearest = float.MaxValue;
                for (int j = 0; j < BuildNodeXZ.Length; j++)
                {
                    if (j != i)
                    {
                        nearest = Mathf.Min(nearest, Vector2.Distance(node, BuildNodeXZ[j]));
                    }
                }

                if (gap < 0.4f || distance > 9.5f || !inside || nearest < 8f || WaterMask(node) > 0.01f)
                {
                    Report.Add("WARNING: BuildNode" + (i + 1).ToString("00") + " road distance " + distance.ToString("F1") + " (gap " + gap.ToString("F1")
                        + "), inside bounds " + inside + ", nearest node " + nearest.ToString("F1") + ", water " + WaterMask(node).ToString("F2"));
                }
            }

            // The two routes may only come together at the base.
            float closest = float.MaxValue;
            for (int i = 0; i < _leftSamples.Count; i++)
            {
                if (Vector2.Distance(_leftSamples[i], BaseTargetXZ) < 12f)
                {
                    continue;
                }

                closest = Mathf.Min(closest, DistanceToPolyline(_leftSamples[i], _rightSamples));
            }

            Report.Add("Validation: closest approach of the two routes away from the base = " + closest.ToString("F1") + " m (road width " + RoadWidth + ").");

            int wet = 0;
            foreach (List<Vector2> route in new[] { _leftSamples, _rightSamples })
            {
                foreach (Vector2 p in route)
                {
                    if (WaterMask(p) > 0.01f)
                    {
                        wet++;
                    }
                }
            }

            Report.Add("Validation: route samples under water = " + wet + " (must be 0 - no bridges in this layout).");
        }

        private static void RegisterScene()
        {
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            if (!scenes.Exists(s => s.path == Level03Path))
            {
                int after = scenes.FindIndex(s => s.path == Level02Path);
                scenes.Insert(after >= 0 ? after + 1 : scenes.Count, new EditorBuildSettingsScene(Level03Path, true));
                EditorBuildSettings.scenes = scenes.ToArray();
                Report.Add("Added Level_03 to Build Settings.");
            }

            var catalog = AssetDatabase.LoadAssetAtPath<LevelCatalog>(CatalogPath);
            var definition = AssetDatabase.LoadAssetAtPath<AlienDefense.Data.LevelDefinition>(DefinitionPath);
            if (catalog == null || definition == null)
            {
                Report.Add("WARNING: LevelCatalog or Level_03_Definition not found - catalog not updated.");
                return;
            }

            var so = new SerializedObject(catalog);
            SerializedProperty entries = so.FindProperty("_entries");
            for (int i = 0; i < entries.arraySize; i++)
            {
                SerializedProperty entry = entries.GetArrayElementAtIndex(i);
                if (entry.FindPropertyRelative("_levelDefinition").objectReferenceValue == definition)
                {
                    SerializedProperty sceneName = entry.FindPropertyRelative("_sceneName");
                    if (sceneName.stringValue != SceneName)
                    {
                        Report.Add("LevelCatalog entry " + i + " (Level_03_Definition) now loads scene Level_03 (was " + sceneName.stringValue + ").");
                        sceneName.stringValue = SceneName;
                        so.ApplyModifiedPropertiesWithoutUndo();
                        EditorUtility.SetDirty(catalog);
                    }
                }
            }
        }

        // ------------------------------------------------------------------ geometry helpers

        private static Vector2[] Mirror(Vector2[] points)
        {
            var mirrored = new Vector2[points.Length];
            for (int i = 0; i < points.Length; i++)
            {
                mirrored[i] = new Vector2(points[i].x, -points[i].y);
            }

            return mirrored;
        }

        /// <summary>Catmull-Rom through the control points, sampled every ~<paramref name="spacing"/> m.</summary>
        private static List<Vector2> Smooth(Vector2[] points, float spacing)
        {
            var result = new List<Vector2>();
            for (int i = 0; i < points.Length - 1; i++)
            {
                Vector2 p0 = points[Mathf.Max(0, i - 1)];
                Vector2 p1 = points[i];
                Vector2 p2 = points[i + 1];
                Vector2 p3 = points[Mathf.Min(points.Length - 1, i + 2)];
                int steps = Mathf.Max(2, Mathf.CeilToInt(Vector2.Distance(p1, p2) / spacing));
                for (int s = 0; s < steps; s++)
                {
                    float t = s / (float)steps;
                    float t2 = t * t;
                    float t3 = t2 * t;
                    result.Add(0.5f * (2f * p1 + (-p0 + p2) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 + (-p0 + 3f * p1 - 3f * p2 + p3) * t3));
                }
            }

            result.Add(points[points.Length - 1]);
            return result;
        }

        private static float DistanceToPolyline(Vector2 p, List<Vector2> line)
        {
            float best = float.MaxValue;
            for (int i = 0; i < line.Count - 1; i++)
            {
                float d = DistanceToSegment(p, line[i], line[i + 1]);
                if (d < best)
                {
                    best = d;
                }
            }

            return best;
        }

        private static float DistanceToSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(0.0001f, ab.sqrMagnitude));
            return Vector2.Distance(p, a + ab * t);
        }
    }
}
