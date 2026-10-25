using System.Collections.Generic;
using AlienDefense.Common;
using AlienDefense.Environment;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AlienDefense.EditorTools
{
    /// <summary>Level_03's absorbable stock, laid out like a real resort coast (user, 2026-10-11: the earlier random
    /// lattice "lay everywhere"). The authored zones (houses, market, lighthouse, docks...) are left alone; the free
    /// ground and water between them is packed by PlotPlanner into tidy axis-aligned plots with walkways:
    ///   land near the water - sun-lounger rows under parasols, beach-toy corners (sandcastles, buckets, balls),
    ///                         surfboard racks with life buoys;
    ///   land inland         - vegetable gardens (furrowed soil beds with one crop in rows), market crate stacks,
    ///                         produce corners, flowering bush beds;
    ///   open water          - swim lanes (two lines of life rings with beach balls between), floating cargo blocks,
    ///                         beach-ball rafts.
    /// The play area is the widened one (LevelBounds X MinX..PlayMaxX, Z +-PlayHalfZ) with the outer sea around it.
    /// Every prop's XP follows LevelXpBudget's size rule and the total is reported against the budget.</summary>
    public static partial class Level03Builder
    {
        private const float CoastGap = 0.9f;

        private static Transform _gCoast;
        private static int _coastProps;
        private static readonly List<Bounds> CoastBoxes = new List<Bounds>();
        private static readonly List<Vector2> CoastTrunks = new List<Vector2>();

        /// <summary>Adds / refreshes only the pickup layer in the saved Level_03 scene - nothing else in the scene
        /// is rebuilt, so hand edits survive.</summary>
        [MenuItem("Tools/Tower Defense/Level 03/Refresh Pickups (absorbable small props)")]
        public static void RefreshPickupsFromMenu()
        {
            Debug.Log(RefreshPickups());
        }

        public static string RefreshPickups()
        {
            if (EditorApplication.isPlaying)
            {
                return "[Level03Builder] Stop Play mode first.";
            }

            Report.Clear();
            _leftSamples = Smooth(LeftRoute, 0.75f);
            _rightSamples = Smooth(Mirror(LeftRoute), 0.75f);
            BuildRoadField();

            Scene scene = EditorSceneManager.OpenScene(Level03Path, OpenSceneMode.Single);
            Transform root = FindRoot(scene, SceneName).transform;
            _zones = Group(root, "Zones");
            _placed = 0;
            Refused.Clear();
            PrefabCache.Clear();
            OwnedMaterials.Clear();
            Counts.Clear();

            ApplyPlayBounds(root);
            EnsureOuterSea(root);
            DressPickups();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            return "[Level03Builder] Pickups refreshed.\n" + string.Join("\n", Report);
        }

        private static void ApplyPlayBounds(Transform root)
        {
            LevelBounds bounds = root.GetComponentInChildren<LevelBounds>(true);
            if (bounds == null)
            {
                Report.Add("WARNING: no LevelBounds under " + root.name);
                return;
            }

            var so = new SerializedObject(bounds);
            so.FindProperty("_center").vector2Value = new Vector2((PlayMinX + PlayMaxX) * 0.5f, 0f);
            so.FindProperty("_extents").vector2Value = new Vector2((PlayMaxX - PlayMinX) * 0.5f, PlayHalfZ);
            so.ApplyModifiedPropertiesWithoutUndo();
            Report.Add("LevelBounds: X " + PlayMinX + ".." + PlayMaxX + ", Z -" + PlayHalfZ + ".." + PlayHalfZ + ".");
        }

        private static void DressPickups()
        {
            // The old random layers (lattice decor / pickups / floating / top-up) are replaced by the coast plots.
            foreach (string group in new[] { "Pickups", "Decor", "FloatingPickups", "PickupsTopUp", "CoastPlots" })
            {
                Transform old = _zones.Find(group);
                if (old != null)
                {
                    Object.DestroyImmediate(old.gameObject);
                }
            }

            int scatterCleared = ClearScatterGroups();
            int madeAbsorbable = MakeGroundCoverAbsorbable();
            DressCoastPlots();
            Report.Add(FarmSoilFlatten.Flatten(_zones.gameObject.scene));
            Report.Add(scatterCleared + " small random scatter props removed from the verges, lawns and BuildNode surrounds (the trees stay).");

            int target = LevelXpBudget.TargetXp();
            LevelXpBudget.Tally tally = LevelXpBudget.ApplyAndCount(_zones.GetComponentsInChildren<TractorAbsorbableProp>(true));
            Report.Add(madeAbsorbable + " existing grass / flower / plant objects made absorbable.");
            Report.Add("Most repeated props: " + LevelXpBudget.TopKinds(_zones.GetComponentsInChildren<TractorAbsorbableProp>(true), 8));
            Report.Add("XP budget: max all skills = " + LevelXpBudget.RequiredXp() + " XP, target " + target + ". Level now: " + tally + "."
                + (tally.Xp < LevelXpBudget.RequiredXp() ? " WARNING: below what maxing the skills needs." : tally.Xp < target ? " (below the x" + LevelXpBudget.Headroom + " headroom)" : ""));
        }

        // ------------------------------------------------------------------ plots

        private static void DressCoastPlots()
        {
            _gCoast = Group(_zones, "CoastPlots");
            _coastProps = 0;
            CoastBoxes.Clear();
            CoastTrunks.Clear();
            var loose = new List<Transform>();
            CollectBlockers(0.4f, CoastBoxes, CoastTrunks, loose);

            var area = Rect.MinMaxRect(PlayMinX + 1f, -PlayHalfZ + 1f, PlayMaxX - 1f, PlayHalfZ - 1f);
            var land = new PlotPlanner(CoastGap);
            PlotPlanner.FreeTest landFree = LandFree;
            int big = land.Pack(area, new Vector2(9f, 4.4f), 1f, landFree, 0);
            int mid = land.Pack(area, new Vector2(5f, 3.6f), 0.8f, landFree, 1);
            int small = land.Pack(area, new Vector2(3f, 2.2f), 0.6f, landFree, 2);
            small += land.Pack(area, new Vector2(2.2f, 1.6f), 0.5f, landFree, 3); // mini beds in the last gaps
            int cleared = ClearLooseInside(land, loose);
            var landTurns = new int[4];
            foreach (PlotPlanner.Plot plot in land.Plots)
            {
                DressLandPlot(plot, landTurns[Mathf.Min(plot.Tier, 2)]++);
            }

            // Street planting (user, 2026-10-11 evening: "denser, still tidy"): two evenly spaced rows following both
            // sides of each road, and a flower ring round every BuildNode - the ground the plots cannot use.
            int rowProps = DressRoadRows(land);
            int ringProps = DressNodeRings(land);
            Report.Add("Street planting: " + rowProps + " props in rows along the roads, " + ringProps + " in rings round the BuildNodes.");

            var sea = new PlotPlanner(0.75f);
            PlotPlanner.FreeTest seaFree = WaterFree;
            int lanes = sea.Pack(area, new Vector2(10f, 3.2f), 1f, seaFree, 0);
            int cargo = sea.Pack(area, new Vector2(4.4f, 3.2f), 0.8f, seaFree, 1);
            int rafts = sea.Pack(area, new Vector2(2.8f, 2f), 0.6f, seaFree, 2);
            var seaTurns = new int[3];
            foreach (PlotPlanner.Plot plot in sea.Plots)
            {
                DressSeaPlot(plot, seaTurns[plot.Tier]++);
            }

            Report.Add("Coast plots: land " + big + " large / " + mid + " medium / " + small + " small, sea " + lanes + " swim lanes / " + cargo
                + " cargo blocks / " + rafts + " ball rafts; " + _coastProps + " absorbable props on them; " + cleared
                + " loose bits of the old scatter (small rocks, plants, odds and ends) cleared from under the plots.");
        }

        private static readonly string[] RowFlowers =
        {
            "Flower_1_Clump.fbx", "Flower_2_Clump.fbx", "Flower_3_Clump.fbx", "Flower_4_Clump.fbx", "Flower_5_Clump.fbx",
        };

        private static readonly string[] RowShrubs =
        {
            NA + "Bush_Small_Flowers.fbx", NA + "Plant_2.fbx", NA + "Bush_Small.fbx", L3 + "Vegetation/PF3_Cactus_01.prefab", NA + "Plant_Flowers.fbx",
            L3 + "Vegetation/PF3_Cactus_02.prefab",
        };

        private static readonly float[] RowShrubScale = { 0.45f, 1f, 0.45f, 0.5f, 1f, 0.5f };

        private static readonly List<Vector2> StreetPoints = new List<Vector2>();

        /// <summary>Open, dry, unoccupied ground for one street-planting prop.</summary>
        private static bool StreetFree(Vector2 p, PlotPlanner land, float nodeClear)
        {
            if (p.x < MinX + 0.5f || p.x > PlayMaxX - 1f || Mathf.Abs(p.y) > MaxZ + 1f || WaterMask(p) > 0.05f)
            {
                return false;
            }

            for (int i = 0; i < BuildNodeXZ.Length; i++)
            {
                if (Vector2.Distance(p, BuildNodeXZ[i]) < nodeClear)
                {
                    return false;
                }
            }

            if (p.x > BaseTargetXZ.x - 3.5f && p.x < BaseTargetXZ.x + 17.5f && Mathf.Abs(p.y) < 7f)
            {
                return false;
            }

            if (land.IsTaken(p, 0.45f) || IsBlocked(p.x, p.y, 0.3f, CoastBoxes, CoastTrunks))
            {
                return false;
            }

            for (int i = 0; i < StreetPoints.Count; i++)
            {
                if ((StreetPoints[i] - p).sqrMagnitude < 0.75f * 0.75f)
                {
                    return false; // where two runs' rows meet, the first one keeps the spot
                }
            }

            return true;
        }

        private static int DressRoadRows(PlotPlanner land)
        {
            StreetPoints.Clear();
            int before = _placed;
            foreach (List<Vector2> route in new[] { _leftSamples, _rightSamples })
            {
                for (int lane = 0; lane < 2; lane++)
                {
                    float offset = RoadWidth * 0.5f + 0.95f + lane * 1.05f;
                    for (int side = -1; side <= 1; side += 2)
                    {
                        int laneCopy = lane;
                        int sideCopy = side;
                        PlotPlanner.AlongPolyline(route, 1.1f, (point, tangent, index) =>
                        {
                            Vector2 q = point + new Vector2(-tangent.y, tangent.x) * (sideCopy * offset);
                            // Inside a tight bend the offset line folds back towards the road: skip those.
                            if (ExactRoadDistance(q) < offset - 0.2f || !StreetFree(q, land, 4.4f))
                            {
                                return;
                            }

                            // Row by the kerb: flowers, one colour for eight plants, then the next. Second row: a low
                            // flowering hedge.
                            int kind = index / 6 + (sideCopy > 0 ? 2 : 0);
                            string key = laneCopy == 0 ? NA + RowFlowers[kind % RowFlowers.Length] : RowShrubs[kind % RowShrubs.Length];
                            float yaw = Mathf.Atan2(tangent.x, tangent.y) * Mathf.Rad2Deg;
                            if (Put("CoastPlots", key, q.x, q.y, yaw, laneCopy == 0 ? 1f : RowShrubScale[kind % RowShrubs.Length], Where.Land, 0.15f) != null)
                            {
                                StreetPoints.Add(q);
                            }
                        });
                    }
                }
            }

            return _placed - before;
        }

        private static int DressNodeRings(PlotPlanner land)
        {
            int before = _placed;
            for (int n = 0; n < BuildNodeXZ.Length; n++)
            {
                const int count = 14;
                for (int i = 0; i < count; i++)
                {
                    float a = i * Mathf.PI * 2f / count;
                    Vector2 q = BuildNodeXZ[n] + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 3.5f;
                    if (ExactRoadDistance(q) < RoadWidth * 0.5f + 0.6f || !StreetFree(q, land, 0f))
                    {
                        continue;
                    }

                    string key = NA + RowFlowers[(n + i % 2 * 2) % RowFlowers.Length];
                    if (Put("CoastPlots", key, q.x, q.y, a * Mathf.Rad2Deg, 1f, Where.Land, 0.15f) != null)
                    {
                        StreetPoints.Add(q);
                    }
                }
            }

            return _placed - before;
        }

        /// <summary>The verge, lawn and BuildNode-surround groups were a random mix of shrubs, weeds, rocks, cacti,
        /// barrels and flower clumps - the "objects everywhere" look. Their small pieces go; palms and trees stay as
        /// the green frame, and the coast plots fill the space in rows.</summary>
        private static int ClearScatterGroups()
        {
            int removed = 0;
            foreach (string name in new[] { "VergeLeft", "VergeRight", "LawnPlanting", "BuildNodeSurrounds" })
            {
                Transform group = _zones.Find(name);
                if (group == null)
                {
                    continue;
                }

                for (int i = group.childCount - 1; i >= 0; i--)
                {
                    Transform child = group.GetChild(i);
                    if (child.name.Contains("Tree") || child.name.Contains("Palm"))
                    {
                        continue;
                    }

                    Bounds bounds = WorldBounds(child.gameObject);
                    if (bounds.size.y < 2.2f)
                    {
                        Object.DestroyImmediate(child.gameObject);
                        removed++;
                    }
                }
            }

            return removed;
        }

        /// <summary>Small loose scenery standing where a plot now goes is removed - the plot's own rows replace it.</summary>
        private static int ClearLooseInside(PlotPlanner planner, List<Transform> loose)
        {
            int cleared = 0;
            foreach (Transform t in loose)
            {
                if (t == null)
                {
                    continue;
                }

                var p = new Vector2(t.position.x, t.position.z);
                foreach (PlotPlanner.Plot plot in planner.Plots)
                {
                    Vector2 half = (Mathf.Abs(plot.Yaw) < 1f ? plot.Size : new Vector2(plot.Size.y, plot.Size.x)) * 0.5f + new Vector2(0.4f, 0.4f);
                    if (Mathf.Abs(p.x - plot.Center.x) <= half.x && Mathf.Abs(p.y - plot.Center.y) <= half.y)
                    {
                        Object.DestroyImmediate(t.gameObject);
                        cleared++;
                        break;
                    }
                }
            }

            return cleared;
        }

        private static bool LandFree(Vector2 p)
        {
            // Land plots stay on the flat playable ground: past MinX / MaxZ the terrain rises into the boundary hills.
            if (p.x < MinX || Mathf.Abs(p.y) > MaxZ + 1f)
            {
                return false;
            }

            if (WaterMask(p) > 0.05f || ExactRoadDistance(p) < RoadWidth * 0.5f + 0.55f)
            {
                return false;
            }

            for (int i = 0; i < BuildNodeXZ.Length; i++)
            {
                if (Vector2.Distance(p, BuildNodeXZ[i]) < 3.6f)
                {
                    return false;
                }
            }

            if (p.x > BaseTargetXZ.x - 3.5f && p.x < BaseTargetXZ.x + 17.5f && Mathf.Abs(p.y) < 7f)
            {
                return false;
            }

            return !IsBlocked(p.x, p.y, 0.3f, CoastBoxes, CoastTrunks);
        }

        private static bool WaterFree(Vector2 p)
        {
            if (WaterMask(p) < 0.6f)
            {
                return false;
            }

            var point = new Vector3(p.x, GroundY + 1f, p.y);
            for (int i = 0; i < CoastBoxes.Count; i++)
            {
                if (CoastBoxes[i].Contains(point))
                {
                    return false; // a dock, a boat, a stilt house
                }
            }

            return true;
        }

        private static bool NearWater(Vector2 p, float distance)
        {
            for (int i = 0; i < 8; i++)
            {
                float a = i * Mathf.PI * 0.25f;
                if (WaterMask(p + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * distance) > 0.3f)
                {
                    return true;
                }
            }

            return false;
        }

        private static void PutItem(PlotPlanner.Plot plot, PlotPlanner.Item item, Where where, float lift = 0f)
        {
            Vector2 world = PlotPlanner.ToWorld(plot, item.Local);
            float y = where == Where.Water || lift == 0f ? float.NaN : HeightAt(world) + lift;
            GameObject go = Put("CoastPlots", item.Key, world.x, world.y, PlotPlanner.ToWorldYaw(plot, item.Yaw), item.Scale, where, 0.2f, y);
            if (go == null)
            {
                return;
            }

            if (where == Where.Water)
            {
                // Float by draught: a flat life ring sinks a third of its own (small) height, never the 0.28 m a barrel
                // does - otherwise thin things vanish under the opaque water.
                Bounds bounds = WorldBounds(go);
                float bottom = WaterY - Mathf.Min(0.28f, bounds.size.y * 0.35f);
                go.transform.position += Vector3.up * (bottom - bounds.min.y);
                if (lift != 0f)
                {
                    go.transform.position += Vector3.up * lift; // cargo riding on a raft
                }
            }

            if (go.GetComponentInChildren<TractorAbsorbableProp>(true) != null)
            {
                _coastProps++;
            }
        }

        // ------------------------------------------------------------------ shared helpers

        /// <summary>What already stands in the zones: bounding boxes to keep out of, and tree trunks to keep clear of.</summary>
        private static void CollectBlockers(float boxPadding, List<Bounds> boxes, List<Vector2> trunks, List<Transform> loose = null)
        {
            foreach (Transform zone in _zones)
            {
                if (zone.name == "CoastPlots")
                {
                    continue;
                }

                foreach (Transform child in zone)
                {
                    string name = child.name;
                    if (name.StartsWith("Grass") || name.StartsWith("Flower") || name.StartsWith("Plant"))
                    {
                        loose?.Add(child);
                        continue;
                    }

                    if (name.Contains("Tree") || name.Contains("Palm"))
                    {
                        trunks.Add(new Vector2(child.position.x, child.position.z));
                        continue;
                    }

                    Bounds bounds = WorldBounds(child.gameObject);
                    // Small loose things (rocks, bushes, buckets...) do not block a plot - they are cleared from it.
                    if (loose != null && bounds.extents.x < 0.9f && bounds.extents.z < 0.9f && bounds.size.y < 1.6f
                        && child.GetComponentInChildren<TractorAbsorbableProp>(true) != null)
                    {
                        loose.Add(child);
                        continue;
                    }

                    bounds.Expand(new Vector3(boxPadding, 100f, boxPadding));
                    boxes.Add(bounds);
                }
            }
        }

        private static bool IsBlocked(float x, float z, float radius, List<Bounds> boxes, List<Vector2> trunks)
        {
            var point = new Vector3(x, GroundY + 1f, z);
            for (int i = 0; i < boxes.Count; i++)
            {
                if (boxes[i].Contains(point))
                {
                    return true;
                }
            }

            float trunkClearance = 0.9f + radius;
            for (int i = 0; i < trunks.Count; i++)
            {
                if (Vector2.Distance(trunks[i], new Vector2(x, z)) < trunkClearance)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>Ground cover placed before pickups existed has no TractorAbsorbableProp; add it.</summary>
        private static int MakeGroundCoverAbsorbable()
        {
            int count = 0;
            foreach (Transform zone in _zones)
            {
                foreach (Transform child in zone)
                {
                    string name = child.name;
                    bool groundCover = name.StartsWith("Grass") || name.StartsWith("Flower") || name.StartsWith("Plant");
                    if (!groundCover || child.GetComponent<TractorAbsorbableProp>() != null)
                    {
                        continue;
                    }

                    var absorbable = child.gameObject.AddComponent<TractorAbsorbableProp>();
                    var so = new SerializedObject(absorbable);
                    so.FindProperty("_visualRoot").objectReferenceValue = child;
                    so.ApplyModifiedPropertiesWithoutUndo();
                    count++;
                }
            }

            return count;
        }
    }
}
