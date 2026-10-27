using System.Collections.Generic;
using AlienDefense.Base.Ambient;
using AlienDefense.Base.DayNight;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace AlienDefense.EditorTools
{
    /// <summary>Environment upgrade pass for the Base (2026-10-27): turns the tidy-but-empty town into a lived-in
    /// alien base without moving anything gameplay owns (plots, buildings, roads, lamps, NPC routes, camera, UI).
    ///
    ///  - Alien tech decor (antenna, dishes, crystals, terminals, glow tubes, signs, beacons...) is generated once as
    ///    project prefabs from primitives + a few shared materials (Prefabs/Base/Decor) - the packs have none.
    ///  - Every plot gets an identity zone in its yard band (between the building / construction footprint and the
    ///    road): Command Center, Science, Workshop, Patrol, Sawmill; locked expansion plots get a construction kit.
    ///  - Street furniture at the junctions, crosswalks and a little road grime, more nature (clusters, flower
    ///    beds, roadside tufts), a forest on the plateau behind the cliffs, a dressed river bank with a dock,
    ///    one major landmark (Energy Core) and small ones (UFO statue, telescope, crystal outcrop).
    ///  - Night/day extras: tech glows harder at night, halos and fireflies at night, pollen by day (registered on
    ///    BaseWorldDayNightController by BaseWorldDayNightBuilder, which this pass re-runs at the end).
    ///
    /// Keeps clear of roads, plot footprints, street lamps and every AmbientWaypoint link (the residents' paths).
    /// Static decor lives under Environment/UpgradeDecor (static-batched by BaseWorldPresenter); the one moving part
    /// (the Energy Core ring) under BaseWorld/UpgradeAnimated; particles under BaseWorld/FX/UpgradeAmbient.
    /// Deterministic (fixed seed). Re-runnable: it only removes and rebuilds its own roots. If "Rebuild Environment
    /// Dressing" is run again later, run this again after it.</summary>
    public static partial class BaseSceneSetupTool
    {
        private const string UpgRootName = "UpgradeDecor";
        private const string UpgAnimatedName = "UpgradeAnimated";
        private const string UpgAmbientName = "UpgradeAmbient";
        private const string UpgDockLampName = "StreetLight_Dock";
        private const string UpgDecorFolder = "Assets/_Game/Prefabs/Base/Decor";
        private const string UpgMaterialFolder = "Assets/_Game/Art/Base/Decor/Materials";
        private const string UpgArtFolder = "Assets/_Game/Art/Base/Decor";
        private const string UpgConstructionFolder = "Assets/Low Poly Construction/Low Poly Construction/";
        private const float UpgKitScale = 1.7f;
        private const float UpgPlotInner = 6.1f;   // construction site +-5.8 m (bigger than every building) + margin
        private const float UpgPlotOuter = 8.45f;  // road edge is 8.6 m from a plot centre
        private static readonly Quaternion UpgCameraRotation = Quaternion.Euler(50f, 45f, 0f);

        private static readonly Dictionary<string, int> UpgCounts = new Dictionary<string, int>();
        private static readonly List<(Vector3 a, Vector3 b)> UpgPaths = new List<(Vector3, Vector3)>();
        private static readonly List<Vector3> UpgLamps = new List<Vector3>();
        private static readonly Dictionary<string, GameObject> UpgTech = new Dictionary<string, GameObject>();
        private static readonly Dictionary<string, Material> UpgMaterials = new Dictionary<string, Material>();

        [MenuItem("AlienDefense/Setup/34. Build Base Environment Upgrade")]
        public static void BuildEnvironmentUpgradeFromMenu()
        {
            Debug.Log(BuildEnvironmentUpgrade());
        }

        public static string BuildEnvironmentUpgrade()
        {
            if (EditorApplication.isPlaying)
            {
                return "[BaseEnvironmentUpgrade] Stop Play mode first.";
            }

            UpgCounts.Clear();
            UpgPaths.Clear();
            UpgLamps.Clear();
            UpgTech.Clear();
            UpgMaterials.Clear();
            Missing.Clear();

            UpgBuildMaterials();
            UpgBuildTechPrefabs();

            Scene scene = EditorSceneManager.OpenScene(MainMenuPath, OpenSceneMode.Single);
            GameObject world = FindRoot(scene, WorldRootName);
            Transform environment = world != null ? world.transform.Find("Environment") : null;
            if (environment == null)
            {
                return "[BaseEnvironmentUpgrade] No BaseWorld/Environment in MainMenu.";
            }

            UpgRemove(environment, UpgRootName);
            UpgRemove(world.transform, UpgAnimatedName);
            Transform fx = world.transform.Find("FX");
            if (fx != null)
            {
                UpgRemove(fx, UpgAmbientName);
            }

            Transform lampRoot = environment.Find("StreetLightsRoot");
            if (lampRoot != null)
            {
                UpgRemove(lampRoot, UpgDockLampName);
            }

            // What must stay clear: lamps, the residents' paths, Alien 3's work spot.
            if (lampRoot != null)
            {
                foreach (Transform lamp in lampRoot)
                {
                    UpgLamps.Add(lamp.position);
                }
            }

            foreach (AmbientWaypoint waypoint in world.GetComponentsInChildren<AmbientWaypoint>(true))
            {
                foreach (AmbientWaypoint neighbor in waypoint.Neighbors)
                {
                    if (neighbor != null)
                    {
                        UpgPaths.Add((waypoint.Position, neighbor.Position));
                    }
                }
            }

            foreach (Transform t in world.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == "Alien3_WorkAnchor")
                {
                    UpgPaths.Add((t.position, t.position + Vector3.forward * 0.01f));
                }
            }

            // Existing scenery counts as occupied, so nothing new lands inside an old tree or rock.
            var placed = new List<(Vector3 position, float radius)>();
            foreach (Renderer renderer in environment.GetComponentsInChildren<Renderer>(true))
            {
                Bounds b = renderer.bounds;
                if (b.size.x > 12f || b.size.z > 12f || renderer.name.StartsWith("Road") || renderer is ParticleSystemRenderer)
                {
                    continue;
                }

                placed.Add((new Vector3(b.center.x, 0f, b.center.z), Mathf.Max(b.extents.x, b.extents.z) * 1.0f));
            }

            var random = new System.Random(27102026);
            var root = new GameObject(UpgRootName).transform;
            root.SetParent(environment, false);
            var animated = new GameObject(UpgAnimatedName).transform;
            animated.SetParent(world.transform, false);

            UpgLandmarks(root, animated, environment, placed, random);
            UpgPlotZones(root, placed, random);
            UpgExpansionPlots(root, placed, random);
            UpgJunctions(root, placed, random);
            UpgRoadDetails(root, random);
            UpgParking(root, placed, random);
            UpgRiver(root, lampRoot, placed, random);
            UpgNature(root, placed, random);
            UpgPlateau(root, random);
            UpgAmbientParticles(fx != null ? fx : world.transform);

            SetLayerRecursively(root.gameObject, BaseLayer);
            SetLayerRecursively(animated.gameObject, BaseLayer);
            MarkEnvironmentStatic(root.gameObject);
            DisableDecorColliders(root.gameObject);
            DisableDecorColliders(animated.gameObject);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            string dayNight = BaseWorldDayNightBuilder.Build(); // registers the new lamp, tech glows, halos, particles

            var report = new System.Text.StringBuilder("[BaseEnvironmentUpgrade] ");
            int total = 0;
            foreach (KeyValuePair<string, int> count in UpgCounts)
            {
                report.Append(count.Key).Append('=').Append(count.Value).Append(", ");
                total += count.Value;
            }

            report.Append("TOTAL=").Append(total).Append(". ").Append(dayNight);
            if (Missing.Count > 0)
            {
                report.Append(" Missing: ").Append(string.Join("; ", Missing));
            }

            return report.ToString();
        }

        // ================================================================== placement helpers

        private static void UpgRemove(Transform parent, string name)
        {
            Transform old = parent.Find(name);
            while (old != null)
            {
                Object.DestroyImmediate(old.gameObject);
                old = parent.Find(name);
            }
        }

        private static void UpgCount(string category)
        {
            UpgCounts[category] = UpgCounts.TryGetValue(category, out int n) ? n + 1 : 1;
        }

        /// <summary>A pack prefab by file name, a Low Poly Construction model by name, or one of our tech prefabs.</summary>
        private static GameObject UpgAsset(string name)
        {
            if (UpgTech.TryGetValue(name, out GameObject tech))
            {
                return tech;
            }

            GameObject prefab = FindPrefab(name);
            if (prefab != null)
            {
                return prefab;
            }

            var model = AssetDatabase.LoadAssetAtPath<GameObject>(UpgConstructionFolder + name + ".fbx");
            if (model == null)
            {
                Missing.Add(name);
            }

            return model;
        }

        private static GameObject UpgSpawn(Transform parent, string asset, Vector3 position, float yaw, float scale, string category)
        {
            GameObject source = UpgAsset(asset);
            if (source == null)
            {
                return null;
            }

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(source, parent);
            instance.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
            instance.transform.localScale = Vector3.one * scale;
            foreach (Transform t in instance.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == "NightHalo")
                {
                    t.rotation = UpgCameraRotation; // halos face the fixed base camera
                }
            }

            // Level kit prefabs carry level gameplay (tractor-beam absorb, occlusion markers): decor here, so strip them
            // on the instance (a removed-component override; the prefab asset is untouched).
            foreach (MonoBehaviour behaviour in instance.GetComponentsInChildren<MonoBehaviour>(true))
            {
                Object.DestroyImmediate(behaviour);
            }

            foreach (Renderer renderer in instance.GetComponentsInChildren<Renderer>(true))
            {
                renderer.lightProbeUsage = LightProbeUsage.Off;
                renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
                if (category != "landmarks" && category != "trees" && category != "vehicles" && renderer.bounds.size.y < 1.6f)
                {
                    renderer.shadowCastingMode = ShadowCastingMode.Off; // small props: no shadow casting
                }
            }

            UpgCount(category);
            return instance;
        }

        private static float UpgSegmentDistance(Vector3 p, Vector3 a, Vector3 b)
        {
            var q = new Vector2(p.x, p.z);
            var a2 = new Vector2(a.x, a.z);
            var b2 = new Vector2(b.x, b.z);
            Vector2 ab = b2 - a2;
            float t = Mathf.Clamp01(Vector2.Dot(q - a2, ab) / Mathf.Max(0.0001f, ab.sqrMagnitude));
            return Vector2.Distance(q, a2 + ab * t);
        }

        /// <summary>Clear of lamps and of every resident's path.</summary>
        private static bool UpgClearOfLife(Vector3 p, float radius)
        {
            foreach (Vector3 lamp in UpgLamps)
            {
                if ((new Vector2(lamp.x - p.x, lamp.z - p.z)).sqrMagnitude < (1.3f + radius) * (1.3f + radius))
                {
                    return false;
                }
            }

            foreach ((Vector3 a, Vector3 b) in UpgPaths)
            {
                if (UpgSegmentDistance(p, a, b) < 1.25f + radius)
                {
                    return false;
                }
            }

            return true;
        }

        private static float UpgRoadDistance(Vector3 p)
        {
            var q = new Vector2(p.x, p.z);
            float best = float.MaxValue;
            foreach ((Vector3 center, Vector2 size) in Roads)
            {
                best = Mathf.Min(best, RectDistance(q, new Vector2(center.x, center.z), size * 0.5f));
            }

            return best;
        }

        /// <summary>Inside a plot's yard band: off the road, outside the building / construction footprint.</summary>
        private static bool UpgPlotSpotOk(Vector3 p, float radius, Vector3 plot, float inner, List<(Vector3, float)> placed)
        {
            float ax = Mathf.Abs(p.x - plot.x), az = Mathf.Abs(p.z - plot.z);
            if (ax > UpgPlotOuter - radius || az > UpgPlotOuter - radius)
            {
                return false;
            }

            if (ax < inner + radius && az < inner + radius)
            {
                return false;
            }

            return UpgClearOfLife(p, radius) && !Overlaps(p, radius, placed);
        }

        /// <summary>Candidate spots round a plot's yard band, shuffled deterministically; corners first when asked.</summary>
        private static List<Vector3> UpgPlotCandidates(Vector3 plot, float inner, System.Random random, bool cornersFirst)
        {
            var spots = new List<Vector3>();
            float[] rings = { inner + 0.9f, (inner + UpgPlotOuter) * 0.5f + 0.35f, UpgPlotOuter - 0.6f };
            foreach (float d in rings)
            {
                for (float t = -UpgPlotOuter + 0.6f; t <= UpgPlotOuter - 0.6f; t += 0.9f)
                {
                    spots.Add(plot + new Vector3(t, 0f, -d));
                    spots.Add(plot + new Vector3(t, 0f, d));
                    spots.Add(plot + new Vector3(-d, 0f, t));
                    spots.Add(plot + new Vector3(d, 0f, t));
                }
            }

            for (int i = spots.Count - 1; i > 0; i--)
            {
                int j = random.Next(i + 1);
                (spots[i], spots[j]) = (spots[j], spots[i]);
            }

            if (cornersFirst)
            {
                spots.Sort((a, b) => Corner(b, plot).CompareTo(Corner(a, plot)));
            }

            return spots;

            float Corner(Vector3 s, Vector3 c) => Mathf.Min(Mathf.Abs(s.x - c.x), Mathf.Abs(s.z - c.z));
        }

        private static float UpgYawFacing(Vector3 from, Vector3 to, System.Random random, float jitter)
        {
            Vector3 d = to - from;
            float yaw = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
            return yaw + Range(random, -jitter, jitter);
        }

        /// <summary>Places one themed item in a plot's yard band, then (optionally) a few small satellites round it.</summary>
        private static GameObject UpgPlotItem(Transform parent, Vector3 plot, string asset, float scale, float radius,
            List<Vector3> candidates, List<(Vector3, float)> placed, System.Random random, string category, bool faceIn,
            string[] satellites = null, int satelliteCount = 0)
        {
            if (asset.StartsWith("BP_Tech_"))
            {
                // Our tech pieces read too small from the base camera at 1:1 - a third bigger.
                scale *= 1.35f;
                radius *= 1.2f;
            }

            foreach (Vector3 spot in candidates)
            {
                Vector3 p = spot + new Vector3(Range(random, -0.25f, 0.25f), 0f, Range(random, -0.25f, 0.25f));
                if (!UpgPlotSpotOk(p, radius, plot, UpgPlotInner, placed))
                {
                    continue;
                }

                float yaw = faceIn ? UpgYawFacing(p, plot, random, 12f) : Range(random, 0f, 360f);
                if (asset == "BP_Tech_Dish" || asset == "BP_Tech_Terminal")
                {
                    yaw = 225f + Range(random, -25f, 25f); // bowl / screen toward the base camera (it looks along +x+z)
                }
                GameObject item = UpgSpawn(parent, asset, p, yaw, scale * Range(random, 0.95f, 1.05f), category);
                if (item == null)
                {
                    return null;
                }

                placed.Add((p, radius));
                for (int s = 0; s < satelliteCount && satellites != null; s++)
                {
                    for (int attempt = 0; attempt < 5; attempt++)
                    {
                        float angle = Range(random, 0f, Mathf.PI * 2f);
                        Vector3 q = p + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * Range(random, radius + 0.4f, radius + 1.3f);
                        if (!UpgPlotSpotOk(q, 0.35f, plot, UpgPlotInner, placed))
                        {
                            continue;
                        }

                        UpgSpawn(parent, satellites[random.Next(satellites.Length)], q, Range(random, 0f, 360f), UpgKitScale * Range(random, 0.8f, 1.15f), "small details");
                        placed.Add((q, 0.35f));
                        break;
                    }
                }

                return item;
            }

            return null;
        }

        // ================================================================== plot identity zones

        private static readonly string[] UpgGrassSmall = { "PF_Grass_01", "PF_Grass_03", "PT_Grass_02_v1", "PF_LeafPlant_01", "PF_Weed_Yellow" };
        private static readonly string[] UpgFlowers = { "PF_Flower_Daisy", "PF_Flower_Purple", "PF_Flower_Red", "PF_Flower_Poppy", "PF_Weed_Orange" };

        private static void UpgPlotZones(Transform root, List<(Vector3, float)> placed, System.Random random)
        {
            foreach ((string id, Vector3 plot) in Plots)
            {
                var zone = new GameObject("Zone_" + id).transform;
                zone.SetParent(root, false);
                List<Vector3> corners = UpgPlotCandidates(plot, UpgPlotInner, random, cornersFirst: true);
                List<Vector3> any = UpgPlotCandidates(plot, UpgPlotInner, random, cornersFirst: false);

                switch (id)
                {
                    case "central_building":
                        // Alien Command Center: clean, organised, tech-forward.
                        // Alien 4's stroll ring runs through the middle of this yard (~6.4-7.2 m out), so the hero
                        // pieces go in the four yard corners with tight footprints (still >= 1.25 m off the ring).
                        // Hero beacon mast in a back corner: behind the building from the base camera (which looks
                        // toward +x+z), so it frames the Command Center instead of standing in front of it.
                        var backCorners = new List<Vector3> { plot + new Vector3(7.5f, 0f, 7.5f), plot + new Vector3(-7.5f, 0f, 7.5f), plot + new Vector3(7.5f, 0f, -7.5f) };
                        UpgPlotItem(zone, plot, "BP_Tech_Antenna", 1.3f, 0.55f, backCorners, placed, random, "landmarks", false);
                        UpgPlotItem(zone, plot, "BP_Tech_Terminal", 1f, 0.45f, corners, placed, random, "plot props", true);
                        UpgPlotItem(zone, plot, "BP_Tech_HoloMap", 1f, 0.5f, corners, placed, random, "plot props", true);
                        UpgPlotItem(zone, plot, "BP_Tech_Dish", 0.75f, 0.6f, corners, placed, random, "plot props", true);
                        for (int i = 0; i < 2; i++) UpgPlotItem(zone, plot, "BP_Tech_Flag", 1f, 0.6f, any, placed, random, "plot props", true);
                        for (int i = 0; i < 2; i++) UpgPlotItem(zone, plot, "PF_Bench", UpgKitScale, 1.2f, any, placed, random, "plot props", true);
                        UpgPlotItem(zone, plot, "BP_Tech_UtilityBox", 1f, 0.6f, any, placed, random, "plot props", true);
                        for (int i = 0; i < 6; i++) UpgPlotItem(zone, plot, "BP_Tech_Bollard", 1f, 0.3f, any, placed, random, "plot props", false);
                        for (int i = 0; i < 2; i++) UpgPlotItem(zone, plot, "PF_Crate", UpgKitScale, 0.8f, any, placed, random, "plot props", false, UpgGrassSmall, 1);
                        UpgPlotItem(zone, plot, "BP_Tech_AlienSign", 1f, 0.5f, any, placed, random, "plot props", false);
                        break;

                    case "research_center":
                        // Science Zone: cyan / purple, experimental.
                        for (int i = 0; i < 2; i++) UpgPlotItem(zone, plot, "BP_Tech_Crystal", 1f, 1.1f, corners, placed, random, "plot props", false, UpgGrassSmall, 2);
                        UpgPlotItem(zone, plot, "BP_Tech_Dish", 0.85f, 1.1f, corners, placed, random, "plot props", true);
                        for (int i = 0; i < 3; i++) UpgPlotItem(zone, plot, "BP_Tech_GlowTube", 1f, 0.55f, any, placed, random, "plot props", false);
                        for (int i = 0; i < 2; i++) UpgPlotItem(zone, plot, "BP_Tech_Specimen", 1f, 0.55f, any, placed, random, "plot props", false);
                        UpgPlotItem(zone, plot, "BP_Tech_Terminal", 1f, 0.7f, any, placed, random, "plot props", true);
                        UpgPlotItem(zone, plot, "BP_Tech_Antenna", 0.75f, 0.9f, any, placed, random, "plot props", false);
                        UpgPlotItem(zone, plot, "PF_Bench", UpgKitScale, 1.2f, any, placed, random, "plot props", true);
                        UpgPlotItem(zone, plot, "BP_Tech_AlienSign", 1f, 0.7f, any, placed, random, "plot props", false);
                        for (int i = 0; i < 2; i++) UpgPlotItem(zone, plot, "PF_Crate_Open", UpgKitScale, 0.7f, any, placed, random, "plot props", false);
                        break;

                    case "weapon_workshop":
                        // Workshop Zone: orange / yellow, mechanical, messier.
                        UpgPlotItem(zone, plot, "Spool A", UpgKitScale * 0.75f, 1.1f, any, placed, random, "plot props", false);
                        for (int i = 0; i < 3; i++) UpgPlotItem(zone, plot, i == 0 ? "PF_Crate_Long" : "PF_Crate", UpgKitScale, 0.9f, any, placed, random, "plot props", false, new[] { "Toolbox", "Gas Tank A", "Bolts", "PF_Crate_Open" }, 2);
                        for (int i = 0; i < 3; i++) UpgPlotItem(zone, plot, i == 1 ? "PF_MetalBarrel" : "PF_Barrel", UpgKitScale, 0.6f, any, placed, random, "plot props", false);
                        UpgPlotItem(zone, plot, "BP_Tech_HazardSign", 1f, 0.6f, any, placed, random, "plot props", true);
                        UpgPlotItem(zone, plot, "Work Light A", UpgKitScale, 0.6f, any, placed, random, "plot props", true);
                        UpgPlotItem(zone, plot, "Pipe A", UpgKitScale * 0.6f, 1.4f, any, placed, random, "plot props", false);
                        UpgPlotItem(zone, plot, "PF_Pallet", UpgKitScale, 1.1f, any, placed, random, "plot props", false);
                        UpgPlotItem(zone, plot, "BP_Tech_UtilityBox", 1f, 0.6f, any, placed, random, "plot props", true);
                        break;

                    case "patrol_post":
                        // Patrol Zone: blue, security, organised.
                        UpgPlotItem(zone, plot, "BP_Tech_Radar", 1f, 1.2f, corners, placed, random, "plot props", true);
                        UpgPlotItem(zone, plot, "BP_Tech_SecurityCam", 1f, 0.5f, corners, placed, random, "plot props", true);
                        for (int i = 0; i < 2; i++) UpgPlotItem(zone, plot, "Barrier A", UpgKitScale * 0.9f, 1.1f, any, placed, random, "plot props", true);
                        for (int i = 0; i < 4; i++) UpgPlotItem(zone, plot, "Cone A", UpgKitScale, 0.35f, any, placed, random, "plot props", false);
                        UpgPlotItem(zone, plot, "BP_Tech_HoloMap", 1f, 0.8f, any, placed, random, "plot props", true);
                        UpgPlotItem(zone, plot, "Work Light A", UpgKitScale, 0.6f, any, placed, random, "plot props", true);
                        for (int i = 0; i < 2; i++) UpgPlotItem(zone, plot, "PF_Crate", UpgKitScale, 0.8f, any, placed, random, "plot props", false);
                        UpgPlotItem(zone, plot, "BP_Tech_AlienSign", 1f, 0.7f, any, placed, random, "plot props", false);
                        for (int i = 0; i < 3; i++) UpgPlotItem(zone, plot, "BP_Tech_Bollard", 1f, 0.3f, any, placed, random, "plot props", false);
                        break;

                    case "sawmill":
                        // Sawmill: logs, wood piles, a light alien touch.
                        for (int i = 0; i < 2; i++) UpgPlotItem(zone, plot, "PF_LogPile", UpgKitScale, 1.4f, corners, placed, random, "plot props", false);
                        UpgPlotItem(zone, plot, "PT_Pine_Tree_03_logs", 1.4f, 1.6f, any, placed, random, "plot props", false);
                        for (int i = 0; i < 3; i++) UpgPlotItem(zone, plot, "PF_Stump", UpgKitScale * 1.4f, 0.5f, any, placed, random, "plot props", false, UpgGrassSmall, 1);
                        UpgPlotItem(zone, plot, "PF_Wheelbarrow", UpgKitScale, 0.9f, any, placed, random, "plot props", false);
                        UpgPlotItem(zone, plot, "PF_WagonWheel", UpgKitScale, 0.6f, any, placed, random, "plot props", false);
                        for (int i = 0; i < 2; i++) UpgPlotItem(zone, plot, "PF_Crate", UpgKitScale, 0.8f, any, placed, random, "plot props", false);
                        UpgPlotItem(zone, plot, "PF_Tool_Rake", UpgKitScale, 0.6f, any, placed, random, "plot props", false);
                        UpgPlotItem(zone, plot, "BP_Tech_Antenna", 0.6f, 0.8f, any, placed, random, "plot props", false);
                        break;
                }

                // Every yard also gets a little green: tufts and the odd flower.
                for (int i = 0; i < 16; i++)
                {
                    string plant = i % 4 == 0 ? UpgFlowers[random.Next(UpgFlowers.Length)] : UpgGrassSmall[random.Next(UpgGrassSmall.Length)];
                    UpgPlotItem(zone, plot, plant, UpgKitScale * Range(random, 1.3f, 1.9f), 0.4f, any, placed, random, i % 4 == 0 ? "flowers" : "grass", false);
                }

                // Two bushes softening the yard corners.
                for (int i = 0; i < 2; i++)
                {
                    UpgPlotItem(zone, plot, UpgBushes[random.Next(UpgBushes.Length)], UpgKitScale * Range(random, 1f, 1.3f), 0.8f, corners, placed, random, "bushes", false);
                }
            }
        }

        private static void UpgExpansionPlots(Transform root, List<(Vector3, float)> placed, System.Random random)
        {
            var zone = new GameObject("Zone_ExpansionPlots").transform;
            zone.SetParent(root, false);
            string[] kit = { "Cone A", "Cone B", "Barrier C", "Plank A", "Pallet", "Toolbox", "BP_Tech_BlueprintTable", "Pipe B", "Cement Bag A", "Wheelbarrow" };
            foreach (Vector3 plot in ExpansionPlots)
            {
                List<Vector3> any = UpgPlotCandidates(plot, 6.9f, random, cornersFirst: false);
                for (int i = 0; i < kit.Length; i++)
                {
                    string asset = kit[i];
                    float scale = asset.StartsWith("BP_") ? 1f : UpgKitScale * (asset.StartsWith("Plank") || asset.StartsWith("Pipe") ? 0.6f : 1f);
                    float radius = asset.StartsWith("Plank") || asset.StartsWith("Pipe") ? 1.3f : 0.6f;
                    foreach (Vector3 spot in any)
                    {
                        float ax = Mathf.Abs(spot.x - plot.x), az = Mathf.Abs(spot.z - plot.z);
                        if (ax > UpgPlotOuter - radius || az > UpgPlotOuter - radius || (ax < 6.9f + radius && az < 6.9f + radius)
                            || !UpgClearOfLife(spot, radius) || Overlaps(spot, radius, placed))
                        {
                            continue;
                        }

                        UpgSpawn(zone, asset, spot, Range(random, 0f, 360f), scale, "plot props");
                        placed.Add((spot, radius));
                        break;
                    }
                }
            }
        }

        // ================================================================== junctions + road

        private static void UpgJunctions(Transform root, List<(Vector3, float)> placed, System.Random random)
        {
            var zone = new GameObject("Roadside").transform;
            zone.SetParent(root, false);
            int set = 0;
            foreach (float x in RoadGridX)
            {
                foreach (float z in RoadGridZ)
                {
                    if (Mathf.Abs(x) > 40f && Mathf.Abs(z) > 20f)
                    {
                        continue; // curved corners
                    }

                    var node = new Vector3(x, 0f, z);
                    int placedHere = 0;
                    int start = random.Next(4);
                    for (int q = 0; q < 4 && placedHere < 2; q++)
                    {
                        int quadrant = (start + q) % 4;
                        float sx = quadrant % 2 == 0 ? -1f : 1f, sz = quadrant < 2 ? -1f : 1f;
                        bool flip = random.NextDouble() < 0.5;
                        Vector3 a = node + (flip ? new Vector3(sx * 4.4f, 0f, sz * 6.2f) : new Vector3(sx * 6.2f, 0f, sz * 4.4f));
                        Vector3 b = node + (flip ? new Vector3(sx * 6.0f, 0f, sz * 5.3f) : new Vector3(sx * 5.3f, 0f, sz * 6.0f));
                        if (!UpgJunctionOk(a, 0.7f, placed) || !UpgJunctionOk(b, 0.6f, placed))
                        {
                            continue;
                        }

                        Vector3 roadSide = node + new Vector3(sx * 2.4f, 0f, sz * 2.4f);
                        switch (set++ % 5)
                        {
                            case 0: // bench facing the junction, bin beside it
                                UpgSpawn(zone, "PF_Bench", a, UpgYawFacing(a, new Vector3(a.x, 0f, node.z), random, 4f), UpgKitScale, "road props");
                                UpgSpawn(zone, "PF3_TrashBin", b, Range(random, 0f, 360f), UpgKitScale, "road props");
                                break;
                            case 1: // alien sign + planter
                                UpgSpawn(zone, "BP_Tech_AlienSign", a, UpgYawFacing(a, roadSide, random, 8f), 1f, "road props");
                                UpgSpawn(zone, "PF_Bush_Round_01", b, Range(random, 0f, 360f), UpgKitScale * 1.1f, "bushes");
                                UpgSpawn(zone, UpgFlowers[random.Next(UpgFlowers.Length)], b + new Vector3(sx * 0.9f, 0f, 0f), Range(random, 0f, 360f), UpgKitScale, "flowers");
                                break;
                            case 2: // sci-fi hydrant + mailbox
                                UpgSpawn(zone, "BP_Tech_Hydrant", a, Range(random, 0f, 360f), 1f, "road props");
                                UpgSpawn(zone, "PF_Mailbox_01", b, UpgYawFacing(b, roadSide, random, 6f), UpgKitScale, "road props");
                                break;
                            case 3: // signpost + small rock and tufts
                                UpgSpawn(zone, "PF_Signpost", a, UpgYawFacing(a, roadSide, random, 10f), UpgKitScale * 0.85f, "road props");
                                UpgSpawn(zone, "PF_Rock_01", b, Range(random, 0f, 360f), UpgKitScale * 0.6f, "rocks");
                                break;
                            default: // utility box + bin
                                UpgSpawn(zone, "BP_Tech_UtilityBox", a, UpgYawFacing(a, roadSide, random, 5f), 1f, "road props");
                                UpgSpawn(zone, "PF3_TrashBin", b, Range(random, 0f, 360f), UpgKitScale, "road props");
                                break;
                        }

                        placed.Add((a, 0.7f));
                        placed.Add((b, 0.6f));
                        placedHere++;
                    }
                }
            }
        }

        /// <summary>Two small parking bays on the outer road shoulders: north of the Command Center (by the Energy Core
        /// meadow) and south of the Workshop. Static vehicles parked parallel to the road.</summary>
        private static void UpgParking(Transform root, List<(Vector3, float)> placed, System.Random random)
        {
            var zone = new GameObject("Parking").transform;
            zone.SetParent(root, false);
            (float z, float fromX, float toX, string[] cars)[] bays =
            {
                (29.2f, 20f, 40f, new[] { "PF_Vehicle_Van", "PF_Vehicle_PickupBlue" }),
                (-29.2f, 4f, 30f, new[] { "PF_Vehicle_PickupBlue", "PF_Vehicle_Van" }),
            };
            foreach ((float z, float fromX, float toX, string[] cars) in bays)
            {
                int parked = 0;
                for (float x = fromX; x <= toX && parked < cars.Length; x += 0.5f)
                {
                    var p = new Vector3(x, 0f, z);
                    if (!UpgClearOfLife(p, 2.4f) || Overlaps(p, 2.4f, placed) || UpgRoadDistance(p) < 1.6f
                        || RiverBankDistance(new Vector2(p.x, p.z)) < 3f)
                    {
                        continue;
                    }

                    UpgSpawn(zone, cars[parked], p, 90f + (random.NextDouble() < 0.5 ? 0f : 180f), UpgKitScale * 0.95f, "vehicles");
                    placed.Add((p, 2.4f));
                    parked++;
                    x += 4f;
                }
            }
        }

        private static bool UpgJunctionOk(Vector3 p, float radius, List<(Vector3, float)> placed)
        {
            if (UpgRoadDistance(p) < 0.6f + radius || !UpgClearOfLife(p, radius) || Overlaps(p, radius, placed))
            {
                return false;
            }

            // Never inside a building / construction footprint.
            foreach ((string _, Vector3 plot) in Plots)
            {
                if (Mathf.Abs(p.x - plot.x) < UpgPlotInner + radius && Mathf.Abs(p.z - plot.z) < UpgPlotInner + radius)
                {
                    return false;
                }
            }

            foreach (Vector3 plot in ExpansionPlots)
            {
                if (Mathf.Abs(p.x - plot.x) < 6.9f + radius && Mathf.Abs(p.z - plot.z) < 6.9f + radius)
                {
                    return false;
                }
            }

            return Mathf.Abs(p.x) < GroundHalfSize - 6f && RiverBankDistance(new Vector2(p.x, p.z)) > 1.5f;
        }

        /// <summary>Crosswalks at the busiest junctions, a little grime and a few drain covers. All flat (the
        /// residents walk over them), lifted a hair above the pavers so nothing z-fights.</summary>
        private static void UpgRoadDetails(Transform root, System.Random random)
        {
            var zone = new GameObject("RoadDetails").transform;
            zone.SetParent(root, false);
            Material marking = UpgMaterials["MAT_Road_Marking"];
            Material grime = UpgMaterials["MAT_Road_Grime"];
            Material grate = UpgMaterials["MAT_Tech_Dark"];
            Vector3[] junctions = { new Vector3(0f, 0f, 0f), new Vector3(0f, 0f, 24f), new Vector3(24f, 0f, 0f), new Vector3(-24f, 0f, 0f), new Vector3(0f, 0f, -24f) };
            Vector3[] arms = { Vector3.forward, Vector3.back, Vector3.left, Vector3.right };
            foreach (Vector3 node in junctions)
            {
                foreach (Vector3 arm in arms)
                {
                    Vector3 probe = node + arm * 6f;
                    if (UpgRoadDistance(probe) > 0.1f)
                    {
                        continue; // no road that way
                    }

                    Vector3 across = Vector3.Cross(Vector3.up, arm);
                    for (int s = 0; s < 5; s++)
                    {
                        Vector3 p = node + arm * 4.3f + across * (-1.8f + s * 0.9f) + Vector3.up * 0.172f;
                        GameObject stripe = UpgQuad(zone, "Crosswalk", p, Quaternion.LookRotation(Vector3.down, arm), new Vector3(0.5f, 1.4f, 1f), marking);
                        stripe.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
                    }

                    UpgCount("road details");
                }
            }

            // Grime / wear patches on the straights (not at junctions), and drain covers by the kerb.
            int grimeCount = 0, drains = 0;
            for (int attempt = 0; attempt < 400 && (grimeCount < 30 || drains < 10); attempt++)
            {
                (Vector3 center, Vector2 size) = Roads[random.Next(Roads.Length)];
                bool alongX = size.x > size.y;
                float length = alongX ? size.x : size.y;
                float along = Range(random, -length * 0.5f + 3f, length * 0.5f - 3f);
                Vector3 p = center + (alongX ? new Vector3(along, 0f, 0f) : new Vector3(0f, 0f, along));
                if (Mathf.Abs(Mathf.Repeat(alongX ? p.x : p.z, 24f) - 12f) > 7.5f)
                {
                    continue; // too close to a junction
                }

                if (grimeCount < 30)
                {
                    float side = Range(random, -1.4f, 1.4f);
                    Vector3 g = p + (alongX ? new Vector3(0f, 0f, side) : new Vector3(side, 0f, 0f)) + Vector3.up * (0.162f + grimeCount * 0.0004f);
                    float s = Range(random, 1.4f, 2.8f);
                    UpgQuad(zone, "RoadGrime", g, Quaternion.Euler(90f, Range(random, 0f, 360f), 0f), new Vector3(s, s * Range(random, 0.5f, 0.9f), 1f), grime);
                    grimeCount++;
                    UpgCount("road details");
                }
                else
                {
                    float side = random.NextDouble() < 0.5 ? -1.95f : 1.95f;
                    Vector3 g = p + (alongX ? new Vector3(0f, 0.16f, side) : new Vector3(side, 0.16f, 0f));
                    GameObject cover = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    cover.name = "DrainCover";
                    Object.DestroyImmediate(cover.GetComponent<Collider>());
                    cover.transform.SetParent(zone, false);
                    cover.transform.position = g;
                    cover.transform.localScale = new Vector3(0.7f, 0.01f, 0.7f);
                    cover.GetComponent<Renderer>().sharedMaterial = grate;
                    drains++;
                    UpgCount("road details");
                }
            }
        }

        private static GameObject UpgQuad(Transform parent, string name, Vector3 position, Quaternion rotation, Vector3 scale, Material material)
        {
            GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = name;
            Object.DestroyImmediate(quad.GetComponent<Collider>());
            quad.transform.SetParent(parent, false);
            quad.transform.SetPositionAndRotation(position, rotation);
            quad.transform.localScale = scale;
            var renderer = quad.GetComponent<Renderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            return quad;
        }

        // ================================================================== landmarks

        private static void UpgLandmarks(Transform root, Transform animated, Transform environment, List<(Vector3, float)> placed, System.Random random)
        {
            var zone = new GameObject("Landmarks").transform;
            zone.SetParent(root, false);

            // Major: the Energy Core in the north meadow (seen at the top of the default view).
            var core = new Vector3(13f, 0f, 35f);
            UpgClearArea(environment, core, 6.5f);
            GameObject coreObject = UpgSpawn(zone, "BP_Tech_EnergyCore", core, 20f, 1f, "landmarks");
            placed.Add((core, 8f)); // plinth + breathing room
            if (coreObject != null)
            {
                // The floating ring turns: it lives outside the static-batched root.
                GameObject ring = UpgSpawn(animated, "BP_Tech_EnergyRing", core + Vector3.up * 4.2f, 0f, 1f, "landmarks");
                if (ring != null)
                {
                    ring.AddComponent<AmbientSpinner>().Configure(18f, 0.35f);
                }
            }

            // Small: UFO statue at the west entrance, telescope by the river, crystal outcrop by the east bank.
            (string asset, Vector3 position, float clear)[] small =
            {
                ("BP_Tech_UfoStatue", new Vector3(-54.5f, 0f, -6f), 3.2f),
                ("BP_Tech_Telescope", new Vector3(-30f, 0f, -29.5f), 1.6f),
                ("BP_Tech_Crystal", new Vector3(31f, 0f, -29.5f), 1.6f),
                ("BP_Tech_Crystal", new Vector3(-56f, 0f, 30f), 1.6f),
            };
            foreach ((string asset, Vector3 position, float clear) in small)
            {
                if (RiverBankDistance(new Vector2(position.x, position.z)) < clear || UpgRoadDistance(position) < clear)
                {
                    Missing.Add("landmark " + asset + " at " + position + " skipped (river/road)");
                    continue;
                }

                UpgClearArea(environment, position, clear + 0.4f);
                UpgSpawn(zone, asset, position, Range(random, 0f, 360f), asset == "BP_Tech_Crystal" ? 1.25f : 1.3f, "landmarks");
                placed.Add((position, clear));
                for (int i = 0; i < 4; i++)
                {
                    float a = i * 1.57f + Range(random, -0.4f, 0.4f);
                    Vector3 p = position + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * (clear + Range(random, 0.3f, 1.2f));
                    if (UpgRoadDistance(p) < 0.8f || RiverBankDistance(new Vector2(p.x, p.z)) < 0.8f) continue;
                    UpgSpawn(zone, i % 2 == 0 ? UpgFlowers[random.Next(UpgFlowers.Length)] : UpgGrassSmall[random.Next(UpgGrassSmall.Length)], p, Range(random, 0f, 360f), UpgKitScale, i % 2 == 0 ? "flowers" : "grass");
                }
            }
        }

        /// <summary>Removes old scenery (nature clusters, roadside tufts, plot dressing) whose root stands inside a
        /// landmark's footprint, so the landmark is not planted into a tree.</summary>
        private static void UpgClearArea(Transform environment, Vector3 center, float radius)
        {
            foreach (string group in new[] { "NatureClusters", "RoadsideGrass", "PlotDressing" })
            {
                Transform t = environment.Find(group);
                if (t == null) continue;
                var doomed = new List<GameObject>();
                foreach (Transform child in t.GetComponentsInChildren<Transform>(true))
                {
                    if (!PrefabUtility.IsOutermostPrefabInstanceRoot(child.gameObject)) continue;
                    var d = new Vector2(child.position.x - center.x, child.position.z - center.z);
                    if (d.sqrMagnitude < radius * radius) doomed.Add(child.gameObject);
                }

                foreach (GameObject go in doomed) Object.DestroyImmediate(go);
            }
        }

        // ================================================================== river

        private static void UpgRiver(Transform root, Transform lampRoot, List<(Vector3, float)> placed, System.Random random)
        {
            var zone = new GameObject("RiverBank").transform;
            zone.SetParent(root, false);
            List<(Vector3 point, float halfWidth)> samples = RiverSamples();
            string[] reeds = { "PF_Grass_03", "PT_High_Grass_02_v1", "PF_Grass_02", "PF_Weed_Yellow" };

            for (int i = 1; i < samples.Count - 1; i += 2)
            {
                Vector3 p = samples[i].point;
                Vector3 dir = samples[i + 1].point - samples[i - 1].point;
                dir.y = 0f;
                Vector3 side = Vector3.Cross(Vector3.up, dir.normalized);
                if (Mathf.Abs(p.x) < 7f || p.x > 56f || Mathf.Abs(p.x) > GroundHalfSize - 8f)
                {
                    continue; // bridge, cave, map edge
                }

                for (int s = -1; s <= 1; s += 2)
                {
                    if (random.NextDouble() > 0.55) continue;
                    Vector3 bank = p + side * s * (samples[i].halfWidth + Range(random, -0.2f, 1.1f));
                    bank.y = 0f;
                    if (UpgRoadDistance(bank) < 1f || Overlaps(bank, 0.4f, placed)) continue;
                    double roll = random.NextDouble();
                    if (roll < 0.62)
                    {
                        UpgSpawn(zone, reeds[random.Next(reeds.Length)], bank, Range(random, 0f, 360f), UpgKitScale * Range(random, 1.1f, 1.5f), "grass");
                    }
                    else if (roll < 0.85)
                    {
                        UpgSpawn(zone, "PT_River_Rock_Pile_02", bank, Range(random, 0f, 360f), UpgKitScale * Range(random, 1f, 1.6f), "rocks");
                    }
                    else
                    {
                        UpgSpawn(zone, "BP_Boulder_" + (char)('A' + random.Next(3)), bank, Range(random, 0f, 360f), Range(random, 0.35f, 0.6f), "rocks");
                    }

                    placed.Add((bank, 0.4f));
                }
            }

            // A small dock on the north bank west of the bridge, with buoys and a lamp.
            int best = -1;
            float bestScore = float.MaxValue;
            for (int i = 1; i < samples.Count - 1; i++)
            {
                float score = Mathf.Abs(samples[i].point.x + 17f);
                if (score < bestScore)
                {
                    bestScore = score;
                    best = i;
                }
            }

            if (best > 0)
            {
                Vector3 p = samples[best].point;
                Vector3 dir = samples[best + 1].point - samples[best - 1].point;
                dir.y = 0f;
                Vector3 side = Vector3.Cross(Vector3.up, dir.normalized);
                if (side.z < 0f) side = -side; // toward the north bank
                // The jetty sticks out from the north bank about a third of the way across - measured from its
                // real bounds so it never spans the river like a bridge.
                Vector3 bank = p + side * samples[best].halfWidth;
                float yaw = Mathf.Atan2(side.x, side.z) * Mathf.Rad2Deg + 90f;
                GameObject dock = UpgSpawn(zone, "PF3_Dock", new Vector3(bank.x, 0.35f, bank.z), yaw, 1.1f, "river props");
                Vector3 dockPos = bank;
                if (dock != null)
                {
                    Bounds bounds = default;
                    bool first = true;
                    foreach (Renderer renderer in dock.GetComponentsInChildren<Renderer>())
                    {
                        if (first) { bounds = renderer.bounds; first = false; }
                        else bounds.Encapsulate(renderer.bounds);
                    }

                    float along = Mathf.Abs(bounds.extents.x * side.x) + Mathf.Abs(bounds.extents.z * side.z);
                    along = Mathf.Min(along, samples[best].halfWidth * 0.7f);
                    Vector3 wanted = bank + side * (0.8f - along);
                    Vector3 shift = wanted - bounds.center;
                    shift.y = 0f;
                    dock.transform.position += shift;
                    dockPos = wanted;
                    dockPos.y = 0.35f;
                }

                UpgSpawn(zone, "PF3_Lifebuoy", dockPos + side * 0.6f + Vector3.up * 0.25f, Range(random, 0f, 360f), 1.3f, "river props");
                for (int b = 0; b < 3; b++)
                {
                    Vector3 buoy = p - side * Range(random, -1.5f, 2.5f) + dir.normalized * (b * 5f - 5f);
                    buoy.y = 0.05f;
                    UpgSpawn(zone, "PF3_Buoy", buoy, Range(random, 0f, 360f), 1.2f, "river props");
                }

                if (lampRoot != null && UpgAsset("BP_StreetLight") != null)
                {
                    Vector3 lampPos = p + side * (samples[best].halfWidth + 1.6f) + dir.normalized * 2.2f;
                    lampPos.y = 0f;
                    var lamp = (GameObject)PrefabUtility.InstantiatePrefab(UpgAsset("BP_StreetLight"), lampRoot);
                    lamp.name = UpgDockLampName;
                    lamp.transform.position = lampPos;
                    UpgCount("lamps");
                    UpgLamps.Add(lampPos);
                }
            }
        }

        // ================================================================== nature

        private static readonly string[] UpgTrees =
            { "PF_Tree_Broadleaf_01", "PF_Tree_Broadleaf_02", "PF_Tree_Broadleaf_03", "PF_Tree_YellowGreen_01", "PF_Tree_YellowGreen_02", "PF_Tree_Fruit_01", "PF_Tree_Autumn_01", "Env_Tree_02", "Env_Tree_04" };
        private static readonly string[] UpgPines = { "PF_Tree_Pine_01", "PF_Tree_Pine_02", "PT_Pine_Tree_03_green", "PF_Tree_Golden_01" };
        private static readonly string[] UpgBushes = { "PF_Bush_Round_01", "PF_Bush_02", "PF_Bush_YellowGreen_01", "PF_Shrub_01", "Env_Bush_01", "PT_Generic_Shrub_01_green" };

        private static float UpgNatureScale(string asset, System.Random random)
        {
            if (asset.StartsWith("PT_Pine")) return Range(random, 0.65f, 0.9f);
            if (asset.StartsWith("PT_")) return Range(random, 1.1f, 1.5f);
            if (asset.StartsWith("Env_")) return Range(random, 1.15f, 1.5f);
            return UpgKitScale * Range(random, 0.8f, 1.2f);
        }

        /// <summary>A second, denser layer of clusters with more variety, weighted to what the camera sees; plus
        /// flower beds and roadside tufts. Clusters, never an even scatter.</summary>
        private static void UpgNature(Transform root, List<(Vector3, float)> placed, System.Random random)
        {
            var zone = new GameObject("Nature").transform;
            zone.SetParent(root, false);
            var centers = new List<Vector3>();
            int clusters = 0;
            for (int attempt = 0; attempt < 12000 && clusters < 150; attempt++)
            {
                var c = new Vector3(Range(random, -70f, 75f), 0f, Range(random, -66f, CliffNorth - 1f));
                if (Blocked(c, 1.2f) || Overlaps(c, 1.2f, placed) || !UpgClearOfLife(c, 1.2f))
                {
                    continue;
                }

                bool tooClose = false;
                foreach (Vector3 other in centers)
                {
                    if ((other - c).sqrMagnitude < 6.5f * 6.5f)
                    {
                        tooClose = true;
                        break;
                    }
                }

                if (tooClose) continue;
                centers.Add(c);
                clusters++;
                var group = new GameObject("Cluster_" + clusters).transform;
                group.SetParent(zone, false);
                double kind = random.NextDouble();
                if (kind < 0.35)
                {
                    UpgNatureItem(group, UpgTrees, c, 0f, 0.8f, 1.6f, placed, random, "trees");
                    for (int i = 0; i < 2 + random.Next(2); i++) UpgNatureItem(group, UpgBushes, c, 1.6f, 3.2f, 0.8f, placed, random, "bushes");
                    for (int i = 0; i < 3 + random.Next(4); i++) UpgNatureItem(group, UpgGrassSmall, c, 1.4f, 3.6f, 0.35f, placed, random, "grass");
                    if (random.NextDouble() < 0.6) UpgNatureItem(group, UpgFlowers, c, 1.4f, 3.4f, 0.3f, placed, random, "flowers");
                    UpgNatureItem(group, new[] { "PF_Rock_01", "PF_Stones_01", "BP_Boulder_A", "BP_Boulder_B" }, c, 1.5f, 3.2f, 0.5f, placed, random, "rocks");
                }
                else if (kind < 0.6)
                {
                    // Flower bed: colour accents.
                    for (int i = 0; i < 4 + random.Next(4); i++) UpgNatureItem(group, UpgFlowers, c, 0f, 2.2f, 0.3f, placed, random, "flowers");
                    for (int i = 0; i < 2 + random.Next(2); i++) UpgNatureItem(group, UpgGrassSmall, c, 0.8f, 2.6f, 0.35f, placed, random, "grass");
                    if (random.NextDouble() < 0.5) UpgNatureItem(group, UpgBushes, c, 1.2f, 2.6f, 0.8f, placed, random, "bushes");
                }
                else if (kind < 0.8)
                {
                    // Pine pocket.
                    UpgNatureItem(group, UpgPines, c, 0f, 1f, 1.3f, placed, random, "trees");
                    if (random.NextDouble() < 0.5) UpgNatureItem(group, UpgPines, c, 1.8f, 3.2f, 1.3f, placed, random, "trees");
                    for (int i = 0; i < 2; i++) UpgNatureItem(group, UpgBushes, c, 1.4f, 3f, 0.8f, placed, random, "bushes");
                    for (int i = 0; i < 3; i++) UpgNatureItem(group, UpgGrassSmall, c, 1.2f, 3.2f, 0.35f, placed, random, "grass");
                    if (random.NextDouble() < 0.4) UpgNatureItem(group, new[] { "PF_Mushroom_01", "PT_Caesars_Mushroom_01" }, c, 1f, 2.4f, 0.3f, placed, random, "small details");
                }
                else
                {
                    // Rock garden.
                    UpgNatureItem(group, new[] { "BP_Boulder_A", "BP_Boulder_B", "BP_Boulder_C", "PF_Rock_Tall_01" }, c, 0f, 0.6f, 1.1f, placed, random, "rocks");
                    for (int i = 0; i < 2; i++) UpgNatureItem(group, new[] { "PF_Rock_01", "PF_Stones_01", "PF_Stones_02" }, c, 1.2f, 2.6f, 0.5f, placed, random, "rocks");
                    for (int i = 0; i < 3; i++) UpgNatureItem(group, UpgGrassSmall, c, 1f, 2.8f, 0.35f, placed, random, "grass");
                    if (random.NextDouble() < 0.5) UpgNatureItem(group, UpgFlowers, c, 1f, 2.6f, 0.3f, placed, random, "flowers");
                }
            }

            // Tufts and the odd flower along the outer road shoulders.
            foreach ((Vector3 center, Vector2 size) in Roads)
            {
                bool alongX = size.x > size.y;
                float length = alongX ? size.x : size.y;
                int tufts = Mathf.RoundToInt(length / 3.2f);
                for (int i = 0; i < tufts; i++)
                {
                    float along = Range(random, -length * 0.5f, length * 0.5f);
                    float side = (random.NextDouble() < 0.5 ? -1f : 1f) * (RoadWidth * 0.5f + Range(random, 0.7f, 1.5f));
                    Vector3 p = center + (alongX ? new Vector3(along, 0f, side) : new Vector3(side, 0f, along));
                    if (BlockedExceptRoads(p, 0.3f) || Overlaps(p, 0.35f, placed) || !UpgClearOfLife(p, 0.3f) || UpgRoadDistance(p) < 0.5f)
                    {
                        continue;
                    }

                    bool flower = random.NextDouble() < 0.18;
                    string[] pool = flower ? UpgFlowers : UpgGrassSmall;
                    UpgSpawn(zone, pool[random.Next(pool.Length)], p, Range(random, 0f, 360f), UpgKitScale * Range(random, 0.8f, 1.15f), flower ? "flowers" : "grass");
                    placed.Add((p, 0.35f));
                }
            }
        }

        private static void UpgNatureItem(Transform parent, string[] pool, Vector3 center, float minRing, float maxRing, float radius,
            List<(Vector3, float)> placed, System.Random random, string category)
        {
            for (int attempt = 0; attempt < 6; attempt++)
            {
                float angle = Range(random, 0f, Mathf.PI * 2f);
                Vector3 p = center + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * Range(random, minRing, maxRing);
                if (Blocked(p, radius) || Overlaps(p, radius, placed) || !UpgClearOfLife(p, radius))
                {
                    continue;
                }

                string asset = pool[random.Next(pool.Length)];
                if (UpgSpawn(parent, asset, p, Range(random, 0f, 360f), UpgNatureScale(asset, random), category) != null)
                {
                    placed.Add((p, radius));
                }

                return;
            }
        }

        /// <summary>A forest on the plateau behind the cliffs: depth for the top of the screen (tree line, pockets of
        /// bushes and big boulders), slightly sparser further back.</summary>
        private static void UpgPlateau(Transform root, System.Random random)
        {
            var zone = new GameObject("PlateauForest").transform;
            zone.SetParent(root, false);
            List<Vector3> rim = BuildRimPath();
            float top = CliffTiers * TierHeight;
            var plateauPlaced = new List<(Vector3, float)>();
            for (int i = 0; i < rim.Count; i++)
            {
                Vector3 normal = RimNormal(rim, i);
                for (int row = 0; row < 3; row++)
                {
                    if (random.NextDouble() < 0.25 + row * 0.15) continue;
                    float depth = CliffTiers * TierDepth + 2.5f + row * 6f + Range(random, -1.5f, 2.5f);
                    Vector3 p = rim[i] + normal * depth + Vector3.Cross(normal, Vector3.up) * Range(random, -1.4f, 1.4f);
                    p.y = top;
                    if (Mathf.Abs(p.x) > GroundHalfSize - 2f || Mathf.Abs(p.z) > GroundHalfSize - 2f || Overlaps(p, 1.5f, plateauPlaced))
                    {
                        continue;
                    }

                    double roll = random.NextDouble();
                    string[] pool = roll < 0.45 ? UpgPines : roll < 0.75 ? UpgTrees : roll < 0.9 ? UpgBushes : new[] { "BP_Boulder_A", "BP_Boulder_B", "BP_Boulder_C" };
                    string asset = pool[random.Next(pool.Length)];
                    float scale = asset.StartsWith("BP_Boulder") ? Range(random, 0.9f, 1.5f) : UpgNatureScale(asset, random) * 1.1f;
                    string category = pool == UpgBushes ? "bushes" : asset.StartsWith("BP_Boulder") ? "rocks" : "trees";
                    UpgSpawn(zone, asset, p, Range(random, 0f, 360f), scale, category);
                    plateauPlaced.Add((p, 1.5f));
                }
            }
        }

        // ================================================================== ambient particles

        private static void UpgAmbientParticles(Transform fx)
        {
            var root = new GameObject(UpgAmbientName).transform;
            root.SetParent(fx, false);
            SetLayerRecursively(root.gameObject, BaseLayer);
            Material firefly = UpgMaterials["MAT_Firefly"];
            Material pollen = UpgMaterials["MAT_Pollen"];
            UpgParticles(root, "Fireflies_Meadow", "NightFX", new Vector3(8f, 1.6f, 36f), new Vector3(34f, 2.5f, 14f), firefly, 16, 0.55f, new Color(0.85f, 1f, 0.45f), 0.25f);
            UpgParticles(root, "Fireflies_River", "NightFX", new Vector3(-12f, 1.4f, -31f), new Vector3(40f, 2f, 6f), firefly, 14, 0.5f, new Color(0.7f, 1f, 0.6f), 0.25f);
            UpgParticles(root, "Pollen_Town", "DayFX", new Vector3(2f, 3f, 6f), new Vector3(60f, 4f, 50f), pollen, 18, 0.16f, new Color(1f, 0.98f, 0.85f), 0.12f);
            UpgParticles(root, "Fireflies_Town", "NightFX", new Vector3(-14f, 1.5f, 12f), new Vector3(16f, 2f, 22f), firefly, 8, 0.45f, new Color(0.8f, 1f, 0.5f), 0.2f);
            for (int i = 0; i < 4; i++)
            {
                UpgCount("particle systems");
            }
        }

        private static void UpgParticles(Transform parent, string name, string toggleName, Vector3 position, Vector3 box,
            Material material, int max, float size, Color color, float speed)
        {
            var holder = new GameObject(name).transform;
            holder.SetParent(parent, false);
            holder.position = position;
            holder.gameObject.layer = BaseLayer;
            var go = new GameObject(toggleName);
            go.layer = BaseLayer;
            go.transform.SetParent(holder, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ParticleSystem.MainModule main = ps.main;
            main.loop = true;
            main.prewarm = true;
            main.duration = 6f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(4f, 7f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0f, speed);
            main.startSize = new ParticleSystem.MinMaxCurve(size * 0.7f, size * 1.2f);
            main.startColor = color;
            main.maxParticles = max;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            ParticleSystem.EmissionModule emission = ps.emission;
            emission.rateOverTime = max / 5f;
            ParticleSystem.ShapeModule shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = box;
            ParticleSystem.NoiseModule noise = ps.noise;
            noise.enabled = true;
            noise.strength = 0.35f;
            noise.frequency = 0.25f;
            noise.quality = ParticleSystemNoiseQuality.Low;
            ParticleSystem.ColorOverLifetimeModule col = ps.colorOverLifetime;
            col.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.25f), new GradientAlphaKey(1f, 0.7f), new GradientAlphaKey(0f, 1f) });
            col.color = gradient;
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        // ================================================================== materials + tech prefabs

        private static void UpgBuildMaterials()
        {
            EnsureFolder(UpgMaterialFolder);
            UpgLit("MAT_Tech_Metal", Hex("#D9DFE7"), 0.25f, 0.55f);
            UpgLit("MAT_Tech_Dark", Hex("#3A4357"), 0.3f, 0.45f);
            UpgLit("MAT_Tech_Teal", Hex("#2FA6B8"), 0.2f, 0.5f);
            UpgLit("MAT_Tech_Yellow", Hex("#F4C22E"), 0f, 0.35f);
            UpgLit("MAT_Tech_Black", Hex("#23262D"), 0f, 0.3f);
            UpgLit("MAT_Tech_Banner", Hex("#6A4BD8"), 0f, 0.25f);
            UpgLit("MAT_Tech_Stone", Hex("#C8BEAE"), 0f, 0.15f);
            UpgLit("MAT_Tech_Blueprint", Hex("#2F6FD6"), 0f, 0.4f);
            UpgLit("MAT_Tech_Red", Hex("#E2463C"), 0f, 0.4f);
            UpgLit("MAT_Road_Marking", Hex("#EEEDE4"), 0f, 0.2f);
            UpgEmissive("MAT_Tech_Cyan", Hex("#5AE6FF"), Hex("#22D3FF") * 1.4f);
            UpgEmissive("MAT_Tech_Purple", Hex("#B07BFF"), Hex("#8F52FF") * 1.3f);
            UpgEmissive("MAT_Tech_Warm", Hex("#FFC45E"), Hex("#FF9A2E") * 1.2f);

            // Road grime: lit, transparent, soft-edged blotch (so it darkens with the night like the road).
            Texture2D grimeTexture = UpgPaintGrime();
            Material grime = UpgLit("MAT_Road_Grime", new Color(0.33f, 0.27f, 0.2f, 0.55f), 0f, 0.1f);
            grime.SetTexture("_BaseMap", grimeTexture);
            grime.SetFloat("_Surface", 1f);
            grime.SetFloat("_Blend", 0f);
            grime.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            grime.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            grime.SetFloat("_ZWrite", 0f);
            grime.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            grime.SetOverrideTag("RenderType", "Transparent");
            grime.renderQueue = (int)RenderQueue.Transparent - 10;

            Texture2D glow = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/_Game/Art/Base/DayNight/T_LampGlow.png");
            UpgGlow("MAT_Firefly", glow, new Color(1f, 1f, 1f, 1f));
            UpgGlow("MAT_Pollen", glow, new Color(1f, 1f, 1f, 0.55f));
            AssetDatabase.SaveAssets();
        }

        private static Material UpgLit(string name, Color color, float metallic, float smoothness)
        {
            string path = UpgMaterialFolder + "/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(material, path);
            }

            material.SetColor("_BaseColor", color);
            material.SetFloat("_Metallic", metallic);
            material.SetFloat("_Smoothness", smoothness);
            material.enableInstancing = true;
            EditorUtility.SetDirty(material);
            UpgMaterials[name] = material;
            return material;
        }

        private static void UpgEmissive(string name, Color color, Color emission)
        {
            Material material = UpgLit(name, color, 0f, 0.6f);
            material.SetColor("_EmissionColor", emission);
            material.EnableKeyword("_EMISSION");
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        }

        private static void UpgGlow(string name, Texture2D texture, Color color)
        {
            string path = UpgMaterialFolder + "/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("AlienDefense/WorldGlowAdditive"));
                AssetDatabase.CreateAsset(material, path);
            }

            material.shader = Shader.Find("AlienDefense/WorldGlowAdditive");
            material.SetTexture("_BaseMap", texture);
            material.SetColor("_BaseColor", color);
            material.renderQueue = 3000;
            EditorUtility.SetDirty(material);
            UpgMaterials[name] = material;
        }

        private static Texture2D UpgPaintGrime()
        {
            EnsureFolder(UpgArtFolder);
            string path = UpgArtFolder + "/T_RoadGrime.png";
            const int size = 128;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float u = (x + 0.5f) / size * 2f - 1f, v = (y + 0.5f) / size * 2f - 1f;
                    float r = Mathf.Sqrt(u * u + v * v);
                    float noise = Mathf.PerlinNoise(x * 0.09f + 7f, y * 0.09f + 3f);
                    float a = Mathf.Clamp01((1f - r) * 1.6f) * Mathf.Clamp01((noise - 0.3f) * 2.2f);
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
                }
            }

            texture.SetPixels32(pixels);
            System.IO.File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        private static Mesh UpgCrystalMesh()
        {
            EnsureFolder(UpgArtFolder + "/Meshes");
            string path = UpgArtFolder + "/Meshes/SM_Tech_Crystal.asset";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (mesh != null)
            {
                return mesh;
            }

            // Hexagonal prism with pointed ends, flat shaded: 0..1 high, radius 0.22.
            var ring = new Vector3[6];
            for (int i = 0; i < 6; i++)
            {
                float a = i * Mathf.PI / 3f;
                ring[i] = new Vector3(Mathf.Cos(a) * 0.22f, 0f, Mathf.Sin(a) * 0.22f);
            }

            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            void Tri(Vector3 a, Vector3 b, Vector3 c)
            {
                triangles.Add(vertices.Count); vertices.Add(a);
                triangles.Add(vertices.Count); vertices.Add(b);
                triangles.Add(vertices.Count); vertices.Add(c);
            }

            Vector3 bottom = Vector3.zero, top = Vector3.up;
            for (int i = 0; i < 6; i++)
            {
                int j = (i + 1) % 6;
                Vector3 lowI = ring[i] + Vector3.up * 0.15f, lowJ = ring[j] + Vector3.up * 0.15f;
                Vector3 highI = ring[i] + Vector3.up * 0.75f, highJ = ring[j] + Vector3.up * 0.75f;
                Tri(bottom, lowJ, lowI);
                Tri(lowI, lowJ, highJ);
                Tri(lowI, highJ, highI);
                Tri(highI, highJ, top);
            }

            mesh = new Mesh { name = "SM_Tech_Crystal" };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            AssetDatabase.CreateAsset(mesh, path);
            return mesh;
        }

        private static GameObject UpgPart(Transform parent, PrimitiveType type, Vector3 position, Vector3 scale, Vector3 euler, string material)
        {
            GameObject part = GameObject.CreatePrimitive(type);
            Object.DestroyImmediate(part.GetComponent<Collider>());
            part.transform.SetParent(parent, false);
            part.transform.localPosition = position;
            part.transform.localEulerAngles = euler;
            part.transform.localScale = scale;
            part.GetComponent<Renderer>().sharedMaterial = UpgMaterials[material];
            return part;
        }

        private static GameObject UpgCrystal(Transform parent, Vector3 position, Vector3 scale, Vector3 euler, string material)
        {
            var go = new GameObject("Crystal");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localEulerAngles = euler;
            go.transform.localScale = scale;
            go.AddComponent<MeshFilter>().sharedMesh = UpgCrystalMesh();
            go.AddComponent<MeshRenderer>().sharedMaterial = UpgMaterials[material];
            return go;
        }

        private static void UpgHalo(Transform parent, Vector3 position, float size)
        {
            Material glow = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Game/Art/Base/DayNight/MAT_LampGlowBulb.mat");
            var halo = new GameObject("NightHalo");
            halo.transform.SetParent(parent, false);
            halo.transform.localPosition = position;
            GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = "BulbGlow";
            Object.DestroyImmediate(quad.GetComponent<Collider>());
            quad.transform.SetParent(halo.transform, false);
            quad.transform.localScale = new Vector3(size, size, 1f);
            var renderer = quad.GetComponent<Renderer>();
            renderer.sharedMaterial = glow;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            halo.SetActive(false);
        }

        private static void UpgSaveTech(GameObject root)
        {
            EnsureFolder(UpgDecorFolder);
            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                renderer.lightProbeUsage = LightProbeUsage.Off;
                renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            }

            string path = UpgDecorFolder + "/" + root.name + ".prefab";
            UpgTech[root.name] = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
        }

        /// <summary>Stylised low-poly alien tech, built from primitives in a handful of shared materials (static-batch
        /// friendly). Sizes are in this world's metres (aliens ~2.3 m, street lamps 4.9 m, doors 2.35 m).</summary>
        private static void UpgBuildTechPrefabs()
        {
            const PrimitiveType Cyl = PrimitiveType.Cylinder, Box = PrimitiveType.Cube, Ball = PrimitiveType.Sphere;
            GameObject r;
            Transform t;

            r = new GameObject("BP_Tech_Antenna"); t = r.transform;
            UpgPart(t, Box, new Vector3(0f, 0.2f, 0f), new Vector3(1.2f, 0.4f, 1.2f), Vector3.zero, "MAT_Tech_Dark");
            UpgPart(t, Cyl, new Vector3(0f, 3.2f, 0f), new Vector3(0.22f, 3f, 0.22f), Vector3.zero, "MAT_Tech_Metal");
            for (int i = 0; i < 3; i++) UpgPart(t, Box, new Vector3(0f, 2.2f + i * 1.3f, 0f), new Vector3(1.4f - i * 0.35f, 0.08f, 0.08f), new Vector3(0f, i * 60f, 0f), "MAT_Tech_Metal");
            UpgPart(t, Ball, new Vector3(0f, 6.35f, 0f), Vector3.one * 0.5f, Vector3.zero, "MAT_Tech_Cyan");
            UpgPart(t, Ball, new Vector3(0f, 3.6f, 0f), Vector3.one * 0.26f, Vector3.zero, "MAT_Tech_Red");
            UpgHalo(t, new Vector3(0f, 6.35f, 0f), 2.2f);
            UpgSaveTech(r);

            r = new GameObject("BP_Tech_Dish"); t = r.transform;
            UpgPart(t, Cyl, new Vector3(0f, 0.3f, 0f), new Vector3(1.1f, 0.3f, 1.1f), Vector3.zero, "MAT_Tech_Dark");
            UpgPart(t, Cyl, new Vector3(0f, 1.1f, 0f), new Vector3(0.3f, 0.6f, 0.3f), Vector3.zero, "MAT_Tech_Metal");
            // Bowl faces +z and up (38 deg); a dark inner disc reads as the dish's hollow, the feed sits on its axis.
            UpgPart(t, Ball, new Vector3(0f, 2f, 0.15f), new Vector3(2.5f, 0.45f, 2.5f), new Vector3(38f, 0f, 0f), "MAT_Tech_Metal");
            UpgPart(t, Cyl, new Vector3(0f, 2.18f, 0.29f), new Vector3(2.05f, 0.01f, 2.05f), new Vector3(38f, 0f, 0f), "MAT_Tech_Dark");
            UpgPart(t, Cyl, new Vector3(0f, 2.5f, 0.54f), new Vector3(0.07f, 0.4f, 0.07f), new Vector3(38f, 0f, 0f), "MAT_Tech_Metal");
            UpgPart(t, Ball, new Vector3(0f, 2.8f, 0.78f), Vector3.one * 0.22f, Vector3.zero, "MAT_Tech_Cyan");
            UpgSaveTech(r);

            r = new GameObject("BP_Tech_Crystal"); t = r.transform;
            UpgPart(t, Cyl, new Vector3(0f, 0.12f, 0f), new Vector3(1.6f, 0.12f, 1.5f), Vector3.zero, "MAT_Tech_Stone");
            UpgCrystal(t, new Vector3(0f, 0.1f, 0f), new Vector3(1.6f, 2.6f, 1.6f), new Vector3(4f, 0f, 3f), "MAT_Tech_Cyan");
            UpgCrystal(t, new Vector3(0.45f, 0.05f, 0.2f), new Vector3(1.1f, 1.6f, 1.1f), new Vector3(0f, 20f, -22f), "MAT_Tech_Cyan");
            UpgCrystal(t, new Vector3(-0.4f, 0.05f, -0.25f), new Vector3(1f, 1.4f, 1f), new Vector3(18f, 40f, 14f), "MAT_Tech_Purple");
            UpgCrystal(t, new Vector3(-0.1f, 0.05f, 0.5f), new Vector3(0.8f, 1f, 0.8f), new Vector3(-25f, 0f, 6f), "MAT_Tech_Cyan");
            UpgHalo(t, new Vector3(0f, 1.4f, 0f), 3f);
            UpgSaveTech(r);

            r = new GameObject("BP_Tech_Terminal"); t = r.transform;
            UpgPart(t, Box, new Vector3(0f, 0.55f, 0f), new Vector3(0.7f, 1.1f, 0.45f), Vector3.zero, "MAT_Tech_Dark");
            UpgPart(t, Box, new Vector3(0f, 1.25f, 0.05f), new Vector3(1f, 0.62f, 0.08f), new Vector3(-20f, 0f, 0f), "MAT_Tech_Cyan");
            UpgPart(t, Cyl, new Vector3(0f, 1.85f, 0f), new Vector3(0.6f, 0.02f, 0.6f), Vector3.zero, "MAT_Tech_Cyan");
            UpgHalo(t, new Vector3(0f, 1.4f, 0f), 1.8f);
            UpgSaveTech(r);

            r = new GameObject("BP_Tech_GlowTube"); t = r.transform;
            UpgPart(t, Cyl, new Vector3(0f, 0.18f, 0f), new Vector3(0.9f, 0.18f, 0.9f), Vector3.zero, "MAT_Tech_Dark");
            UpgPart(t, Cyl, new Vector3(0f, 1.15f, 0f), new Vector3(0.55f, 0.8f, 0.55f), Vector3.zero, "MAT_Tech_Cyan");
            UpgPart(t, Cyl, new Vector3(0f, 2f, 0f), new Vector3(0.75f, 0.08f, 0.75f), Vector3.zero, "MAT_Tech_Metal");
            for (int i = 0; i < 3; i++) UpgPart(t, Cyl, new Vector3(Mathf.Cos(i * 2.09f) * 0.36f, 1.15f, Mathf.Sin(i * 2.09f) * 0.36f), new Vector3(0.06f, 0.85f, 0.06f), Vector3.zero, "MAT_Tech_Metal");
            UpgSaveTech(r);

            r = new GameObject("BP_Tech_Specimen"); t = r.transform;
            UpgPart(t, Box, new Vector3(0f, 0.2f, 0f), new Vector3(0.9f, 0.4f, 0.9f), Vector3.zero, "MAT_Tech_Metal");
            UpgPart(t, PrimitiveType.Capsule, new Vector3(0f, 0.95f, 0f), new Vector3(0.6f, 0.55f, 0.6f), Vector3.zero, "MAT_Tech_Purple");
            UpgPart(t, Cyl, new Vector3(0f, 1.55f, 0f), new Vector3(0.7f, 0.06f, 0.7f), Vector3.zero, "MAT_Tech_Dark");
            UpgSaveTech(r);

            r = new GameObject("BP_Tech_Bollard"); t = r.transform;
            UpgPart(t, Cyl, new Vector3(0f, 0.45f, 0f), new Vector3(0.28f, 0.45f, 0.28f), Vector3.zero, "MAT_Tech_Metal");
            UpgPart(t, Cyl, new Vector3(0f, 0.75f, 0f), new Vector3(0.3f, 0.05f, 0.3f), Vector3.zero, "MAT_Tech_Cyan");
            UpgSaveTech(r);

            r = new GameObject("BP_Tech_UtilityBox"); t = r.transform;
            UpgPart(t, Box, new Vector3(0f, 0.65f, 0f), new Vector3(1f, 1.3f, 0.55f), Vector3.zero, "MAT_Tech_Teal");
            UpgPart(t, Box, new Vector3(0f, 0.95f, 0.29f), new Vector3(0.7f, 0.12f, 0.02f), Vector3.zero, "MAT_Tech_Cyan");
            UpgPart(t, Box, new Vector3(0f, 1.33f, 0f), new Vector3(1.08f, 0.06f, 0.62f), Vector3.zero, "MAT_Tech_Dark");
            UpgSaveTech(r);

            r = new GameObject("BP_Tech_Flag"); t = r.transform;
            UpgPart(t, Cyl, new Vector3(0f, 2.3f, 0f), new Vector3(0.12f, 2.3f, 0.12f), Vector3.zero, "MAT_Tech_Metal");
            UpgPart(t, Box, new Vector3(0.62f, 3.6f, 0f), new Vector3(1.1f, 1.7f, 0.04f), Vector3.zero, "MAT_Tech_Banner");
            UpgPart(t, Cyl, new Vector3(0.62f, 3.75f, -0.03f), new Vector3(0.6f, 0.01f, 0.6f), new Vector3(90f, 0f, 0f), "MAT_Tech_Cyan");
            UpgPart(t, Ball, new Vector3(0f, 4.65f, 0f), Vector3.one * 0.22f, Vector3.zero, "MAT_Tech_Cyan");
            UpgSaveTech(r);

            r = new GameObject("BP_Tech_HazardSign"); t = r.transform;
            UpgPart(t, Cyl, new Vector3(0f, 0.9f, 0f), new Vector3(0.1f, 0.9f, 0.1f), Vector3.zero, "MAT_Tech_Dark");
            UpgPart(t, Box, new Vector3(0f, 1.75f, 0.06f), new Vector3(0.9f, 0.9f, 0.05f), new Vector3(0f, 0f, 45f), "MAT_Tech_Yellow");
            for (int i = 0; i < 3; i++) UpgPart(t, Box, new Vector3(-0.22f + i * 0.22f, 1.75f, 0.09f), new Vector3(0.09f, 0.7f, 0.02f), new Vector3(0f, 0f, 45f), "MAT_Tech_Black");
            UpgSaveTech(r);

            r = new GameObject("BP_Tech_AlienSign"); t = r.transform;
            UpgPart(t, Cyl, new Vector3(-0.55f, 1.1f, 0f), new Vector3(0.09f, 1.1f, 0.09f), Vector3.zero, "MAT_Tech_Metal");
            UpgPart(t, Cyl, new Vector3(0.55f, 1.1f, 0f), new Vector3(0.09f, 1.1f, 0.09f), Vector3.zero, "MAT_Tech_Metal");
            UpgPart(t, Box, new Vector3(0f, 1.85f, 0f), new Vector3(1.5f, 0.9f, 0.1f), Vector3.zero, "MAT_Tech_Dark");
            UpgPart(t, Cyl, new Vector3(-0.35f, 1.85f, 0.06f), new Vector3(0.55f, 0.01f, 0.55f), new Vector3(90f, 0f, 0f), "MAT_Tech_Cyan");
            UpgPart(t, Ball, new Vector3(-0.35f, 1.85f, 0.08f), new Vector3(0.45f, 0.14f, 0.05f), Vector3.zero, "MAT_Tech_Metal");
            UpgPart(t, Box, new Vector3(0.3f, 1.85f, 0.06f), new Vector3(0.55f, 0.1f, 0.02f), Vector3.zero, "MAT_Tech_Cyan");
            UpgPart(t, Box, new Vector3(0.48f, 1.92f, 0.06f), new Vector3(0.18f, 0.06f, 0.02f), new Vector3(0f, 0f, 40f), "MAT_Tech_Cyan");
            UpgSaveTech(r);

            r = new GameObject("BP_Tech_Hydrant"); t = r.transform;
            UpgPart(t, Cyl, new Vector3(0f, 0.4f, 0f), new Vector3(0.42f, 0.4f, 0.42f), Vector3.zero, "MAT_Tech_Teal");
            UpgPart(t, Ball, new Vector3(0f, 0.85f, 0f), new Vector3(0.44f, 0.3f, 0.44f), Vector3.zero, "MAT_Tech_Metal");
            UpgPart(t, Cyl, new Vector3(0.26f, 0.5f, 0f), new Vector3(0.14f, 0.1f, 0.14f), new Vector3(0f, 0f, 90f), "MAT_Tech_Metal");
            UpgPart(t, Ball, new Vector3(0f, 1.02f, 0f), Vector3.one * 0.12f, Vector3.zero, "MAT_Tech_Cyan");
            UpgSaveTech(r);

            r = new GameObject("BP_Tech_SecurityCam"); t = r.transform;
            UpgPart(t, Cyl, new Vector3(0f, 1.5f, 0f), new Vector3(0.14f, 1.5f, 0.14f), Vector3.zero, "MAT_Tech_Metal");
            UpgPart(t, Box, new Vector3(0f, 3.05f, 0.25f), new Vector3(0.35f, 0.3f, 0.7f), new Vector3(18f, 0f, 0f), "MAT_Tech_Dark");
            UpgPart(t, Ball, new Vector3(0f, 2.95f, 0.62f), Vector3.one * 0.16f, Vector3.zero, "MAT_Tech_Cyan");
            UpgSaveTech(r);

            r = new GameObject("BP_Tech_Radar"); t = r.transform;
            UpgPart(t, Box, new Vector3(0f, 0.35f, 0f), new Vector3(1.6f, 0.7f, 1.6f), Vector3.zero, "MAT_Tech_Dark");
            for (int i = 0; i < 4; i++) UpgPart(t, Cyl, new Vector3((i % 2 == 0 ? -0.4f : 0.4f), 1.4f, (i < 2 ? -0.4f : 0.4f)), new Vector3(0.08f, 0.75f, 0.08f), new Vector3((i < 2 ? -8f : 8f), 0f, (i % 2 == 0 ? 8f : -8f)), "MAT_Tech_Metal");
            UpgPart(t, Ball, new Vector3(0f, 2.55f, 0f), new Vector3(2.8f, 0.35f, 1.2f), new Vector3(-20f, 0f, 0f), "MAT_Tech_Metal");
            UpgPart(t, Ball, new Vector3(0f, 2.7f, 0.4f), Vector3.one * 0.2f, Vector3.zero, "MAT_Tech_Cyan");
            UpgSaveTech(r);

            r = new GameObject("BP_Tech_HoloMap"); t = r.transform;
            UpgPart(t, Cyl, new Vector3(0f, 0.45f, 0f), new Vector3(0.5f, 0.45f, 0.5f), Vector3.zero, "MAT_Tech_Dark");
            UpgPart(t, Cyl, new Vector3(0f, 0.93f, 0f), new Vector3(1.4f, 0.04f, 1.4f), Vector3.zero, "MAT_Tech_Metal");
            UpgPart(t, Cyl, new Vector3(0f, 0.97f, 0f), new Vector3(1.15f, 0.01f, 1.15f), Vector3.zero, "MAT_Tech_Cyan");
            UpgPart(t, Ball, new Vector3(0.2f, 1.35f, -0.1f), new Vector3(0.45f, 0.45f, 0.45f), Vector3.zero, "MAT_Tech_Cyan");
            UpgHalo(t, new Vector3(0f, 1.3f, 0f), 2.4f);
            UpgSaveTech(r);

            r = new GameObject("BP_Tech_Telescope"); t = r.transform;
            for (int i = 0; i < 3; i++) UpgPart(t, Cyl, new Vector3(Mathf.Cos(i * 2.09f) * 0.35f, 0.75f, Mathf.Sin(i * 2.09f) * 0.35f), new Vector3(0.07f, 0.8f, 0.07f), new Vector3(Mathf.Sin(i * 2.09f) * 14f, 0f, -Mathf.Cos(i * 2.09f) * 14f), "MAT_Tech_Dark");
            UpgPart(t, Cyl, new Vector3(0f, 1.75f, 0.3f), new Vector3(0.38f, 1f, 0.38f), new Vector3(55f, 0f, 0f), "MAT_Tech_Metal");
            UpgPart(t, Cyl, new Vector3(0f, 2.3f, 1.1f), new Vector3(0.44f, 0.04f, 0.44f), new Vector3(55f, 0f, 0f), "MAT_Tech_Cyan");
            UpgSaveTech(r);

            r = new GameObject("BP_Tech_UfoStatue"); t = r.transform;
            UpgPart(t, Cyl, new Vector3(0f, 0.4f, 0f), new Vector3(2.4f, 0.4f, 2.4f), Vector3.zero, "MAT_Tech_Stone");
            UpgPart(t, Cyl, new Vector3(0f, 1.8f, 0f), new Vector3(0.3f, 1.1f, 0.3f), Vector3.zero, "MAT_Tech_Metal");
            UpgPart(t, Ball, new Vector3(0f, 3.1f, 0f), new Vector3(3.6f, 0.75f, 3.6f), new Vector3(0f, 0f, 12f), "MAT_Tech_Metal");
            UpgPart(t, Ball, new Vector3(-0.08f, 3.5f, 0f), new Vector3(1.4f, 1f, 1.4f), new Vector3(0f, 0f, 12f), "MAT_Tech_Cyan");
            for (int i = 0; i < 6; i++) UpgPart(t, Ball, new Vector3(Mathf.Cos(i * 1.047f) * 1.65f, 3.05f + Mathf.Cos(i * 1.047f) * -0.35f, Mathf.Sin(i * 1.047f) * 1.65f), Vector3.one * 0.18f, Vector3.zero, "MAT_Tech_Warm");
            UpgHalo(t, new Vector3(0f, 3.3f, 0f), 4.5f);
            UpgSaveTech(r);

            r = new GameObject("BP_Tech_Beacon"); t = r.transform;
            UpgPart(t, Ball, Vector3.zero, Vector3.one * 0.55f, Vector3.zero, "MAT_Tech_Cyan");
            UpgPart(t, Cyl, new Vector3(0f, -0.45f, 0f), new Vector3(0.12f, 0.35f, 0.12f), Vector3.zero, "MAT_Tech_Metal");
            UpgHalo(t, Vector3.zero, 3.2f);
            UpgSaveTech(r);

            r = new GameObject("BP_Tech_BlueprintTable"); t = r.transform;
            for (int i = 0; i < 4; i++) UpgPart(t, Box, new Vector3((i % 2 == 0 ? -0.6f : 0.6f), 0.45f, (i < 2 ? -0.35f : 0.35f)), new Vector3(0.08f, 0.9f, 0.08f), Vector3.zero, "MAT_Tech_Dark");
            UpgPart(t, Box, new Vector3(0f, 0.92f, 0f), new Vector3(1.4f, 0.06f, 0.9f), new Vector3(-8f, 0f, 0f), "MAT_Tech_Blueprint");
            UpgSaveTech(r);

            r = new GameObject("BP_Tech_EnergyCore"); t = r.transform;
            UpgPart(t, Cyl, new Vector3(0f, 0.25f, 0f), new Vector3(8.4f, 0.25f, 8.4f), Vector3.zero, "MAT_Tech_Dark");
            UpgPart(t, Cyl, new Vector3(0f, 0.55f, 0f), new Vector3(5.2f, 0.12f, 5.2f), Vector3.zero, "MAT_Tech_Metal");
            UpgPart(t, Cyl, new Vector3(0f, 0.68f, 0f), new Vector3(4.2f, 0.02f, 4.2f), Vector3.zero, "MAT_Tech_Cyan");
            for (int i = 0; i < 4; i++)
            {
                float a = i * Mathf.PI * 0.5f + Mathf.PI * 0.25f;
                Vector3 pylon = new Vector3(Mathf.Cos(a) * 3.3f, 0f, Mathf.Sin(a) * 3.3f);
                UpgPart(t, Box, pylon + Vector3.up * 1.8f, new Vector3(0.55f, 3.2f, 0.55f), new Vector3(0f, -a * Mathf.Rad2Deg, 0f), "MAT_Tech_Metal");
                UpgPart(t, Ball, pylon + Vector3.up * 3.55f, Vector3.one * 0.5f, Vector3.zero, "MAT_Tech_Cyan");
            }

            UpgCrystal(t, new Vector3(0f, 0.5f, 0f), new Vector3(4.4f, 7.2f, 4.4f), new Vector3(0f, 15f, 0f), "MAT_Tech_Cyan");
            UpgCrystal(t, new Vector3(1.1f, 0.5f, 0.4f), new Vector3(2f, 3.2f, 2f), new Vector3(0f, 0f, -18f), "MAT_Tech_Purple");
            UpgCrystal(t, new Vector3(-1f, 0.5f, -0.6f), new Vector3(1.8f, 2.6f, 1.8f), new Vector3(14f, 30f, 16f), "MAT_Tech_Cyan");
            UpgHalo(t, new Vector3(0f, 4f, 0f), 9f);
            UpgSaveTech(r);

            r = new GameObject("BP_Tech_EnergyRing"); t = r.transform;
            for (int i = 0; i < 10; i++)
            {
                float a = i * Mathf.PI * 0.2f;
                UpgPart(t, Box, new Vector3(Mathf.Cos(a) * 2.6f, 0f, Mathf.Sin(a) * 2.6f), new Vector3(0.18f, 0.18f, 1.5f), new Vector3(0f, -a * Mathf.Rad2Deg, 0f), i % 2 == 0 ? "MAT_Tech_Cyan" : "MAT_Tech_Metal");
            }

            UpgSaveTech(r);
        }
    }
}
