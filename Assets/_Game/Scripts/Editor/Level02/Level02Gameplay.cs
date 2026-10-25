using System.Collections.Generic;
using AlienDefense.Building;
using AlienDefense.Common;
using AlienDefense.UI.Minimap;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace AlienDefense.EditorTools
{
    /// <summary>Level_02 gameplay wiring that goes beyond moving Level_01's anchors: the eight BuildNodes of the
    /// minimap reference (the existing BuildNode system, duplicated like Ctrl+D so every instance keeps its prefab
    /// link and scene-added ChannelUI), the three scene lists that hold them, the Level_02-only minimap style, and a
    /// validation pass that checks every node against the road, the buildings, the props and the gameplay camera.</summary>
    public static partial class Level02Builder
    {
        private const float BuildNodeRingRadius = 1.7f; // BuildNode visual ring is 3.4 m across

        // ------------------------------------------------------------------ build nodes

        /// <summary>Moves the BuildNodes container to Gameplay/BuildNodes, makes sure it holds exactly one node per
        /// BuildNodeXZ entry (duplicating an existing node when more are needed), names them BuildNode_01.. in route
        /// order, seats them on the terrain and re-points every scene list that references them.</summary>
        private static void PlaceBuildNodes(Transform gameplay, Anchor nodesAnchor, Terrain terrain)
        {
            Transform nodes = nodesAnchor.Transform;
            if (nodes.parent != gameplay)
            {
                Transform oldParent = nodes.parent;
                nodes.SetParent(gameplay, true);
                if (oldParent != null && oldParent.name == "BuildPoints" && oldParent.childCount == 0)
                {
                    Object.DestroyImmediate(oldParent.gameObject);
                }
            }

            nodes.name = "BuildNodes";
            nodes.SetPositionAndRotation(Vector3.zero, Quaternion.identity);

            var list = new List<BuildNode>();
            foreach (Transform child in nodes)
            {
                var node = child.GetComponent<BuildNode>();
                if (node != null)
                {
                    list.Add(node);
                }
            }

            int created = 0;
            while (list.Count < BuildNodeXZ.Length && list.Count > 0)
            {
                // Same as Ctrl+D in the Hierarchy: keeps the prefab connection, the added ChannelUI child and the
                // overrides that point at it (a fresh InstantiatePrefab would lose all three).
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
                list[i].gameObject.SetActive(false); // never expected; kept inactive rather than deleted
            }

            int count = Mathf.Min(list.Count, BuildNodeXZ.Length);
            var placed = new BuildNode[count];
            for (int i = 0; i < count; i++)
            {
                Transform node = list[i].transform;
                node.name = "BuildNode_" + (i + 1).ToString("00");
                node.SetSiblingIndex(i);
                Vector2 xz = BuildNodeXZ[i];
                node.position = new Vector3(xz.x, Ground(terrain, xz) + nodesAnchor.HeightAboveGround, xz.y);
                placed[i] = list[i];
            }

            WireBuildNodeLists(placed);
            Report.Add("BuildNodes: " + count + " (" + created + " duplicated from the existing prefab instances), under Gameplay/BuildNodes.");
        }

        /// <summary>Every scene list of BuildNodes (proximity/build controller, visual coordinator, minimap) gets the
        /// same array, in route order.</summary>
        private static void WireBuildNodeLists(BuildNode[] nodes)
        {
            void Assign(Object target, string property)
            {
                var so = new SerializedObject(target);
                SerializedProperty array = so.FindProperty(property);
                if (array == null)
                {
                    Report.Add("WARNING: " + target.GetType().Name + " has no " + property);
                    return;
                }

                array.arraySize = nodes.Length;
                for (int i = 0; i < nodes.Length; i++)
                {
                    array.GetArrayElementAtIndex(i).objectReferenceValue = nodes[i];
                }

                so.ApplyModifiedPropertiesWithoutUndo();
            }

            int lists = 0;
            foreach (var c in Object.FindObjectsByType<PlayerBuildNodeProximityController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                Assign(c, "_nodes");
                lists++;
            }

            foreach (var c in Object.FindObjectsByType<BuildNodeVisualCoordinator>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                Assign(c, "_nodes");
                lists++;
            }

            foreach (var c in Object.FindObjectsByType<MinimapPresenter>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                Assign(c, "_buildNodes");
                lists++;
            }

            Report.Add("BuildNode lists re-wired: " + lists + " (PlayerBuildNodeProximityController, BuildNodeVisualCoordinator, MinimapPresenter).");
        }

        // ------------------------------------------------------------------ minimap

        // Level_01's minimap palette (read from Level_01.unity): green panel, dark-green road, light-blue node dots.
        private static readonly Color MinimapTimerPanelColor = Hex("#22B14C");
        private static readonly Color MinimapFrameColor = Hex("#0B6E2B");
        private static readonly Color MinimapBackgroundColor = Hex("#22B14C");
        private static readonly Color MinimapHighlightColor = new Color(0.369f, 0.910f, 0.541f, 0.349f); // #5EE88A59
        private static readonly Color MinimapRoadColor = Hex("#146B2E");
        private static readonly Color MinimapNodeColor = Hex("#4FA8E8");
        private static readonly Color MinimapNodeOccupiedColor = Hex("#E6F8FF"); // reads on the green panel

        [MenuItem("Tools/Tower Defense/Level 02/Apply Minimap Style")]
        public static void ApplyMinimapStyleFromMenu()
        {
            if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name != Level02SceneName)
            {
                Debug.LogError("[Level02Builder] Apply Minimap Style only runs on the Level_02 scene (the open scene is '"
                    + UnityEngine.SceneManagement.SceneManager.GetActiveScene().name + "').");
                return;
            }

            var bounds = Object.FindFirstObjectByType<LevelBounds>(FindObjectsInactive.Include);
            Report.Clear();
            ConfigureMinimap(bounds);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            Debug.Log(string.Join("\n", Report));
        }

        /// <summary>Level_02 minimap setup, all scene-serialized (Level_01's HUD is untouched): the whole LevelBounds
        /// rectangle is the mapped area, Level_01's colours, BuildNode dots that turn near-white once a tower stands on
        /// them, one dot per living zombie (no orange active-route line), and no corner-waypoint dots.</summary>
        private static void ConfigureMinimap(LevelBounds bounds)
        {
            var presenter = Object.FindFirstObjectByType<MinimapPresenter>(FindObjectsInactive.Include);
            var controller = Object.FindFirstObjectByType<MinimapController>(FindObjectsInactive.Include);
            if (presenter == null || controller == null)
            {
                Report.Add("WARNING: no minimap in the scene.");
                return;
            }

            var presenterSo = new SerializedObject(presenter);
            presenterSo.FindProperty("_mapBounds").objectReferenceValue = bounds;
            presenterSo.ApplyModifiedPropertiesWithoutUndo();

            var so = new SerializedObject(controller);
            var pathGraphic = (MinimapPathGraphic)so.FindProperty("_pathGraphic").objectReferenceValue;
            if (pathGraphic != null)
            {
                Undo.RecordObject(pathGraphic, "Level02 minimap");
                pathGraphic.color = MinimapRoadColor;
                EditorUtility.SetDirty(pathGraphic);

                Transform existing = pathGraphic.transform.parent.Find("ActivePathGraphic");
                if (existing != null)
                {
                    existing.gameObject.SetActive(false);
                }
            }

            so.FindProperty("_activePathGraphic").objectReferenceValue = null;

            so.FindProperty("_waypointMarkersParent").objectReferenceValue = null;
            so.FindProperty("_buildNodeMarkerColor").colorValue = MinimapNodeColor;
            so.FindProperty("_buildNodeMarkerSize").floatValue = 11f;
            so.FindProperty("_tintOccupiedBuildNodes").boolValue = true;
            so.FindProperty("_buildNodeOccupiedMarkerColor").colorValue = MinimapNodeOccupiedColor;
            so.FindProperty("_pathWidthPx").floatValue = 13f;
            so.FindProperty("_highlightBossEnemies").boolValue = true; // mini bosses + final boss get a bigger orange dot
            so.FindProperty("_boundsPaddingFactor").floatValue = 1.04f;
            so.ApplyModifiedPropertiesWithoutUndo();

            // Same panel colours as Level_01.
            Transform minimapRoot = pathGraphic != null ? pathGraphic.transform : controller.transform;
            while (minimapRoot.parent != null && minimapRoot.name != "MinimapRoot")
            {
                minimapRoot = minimapRoot.parent;
            }

            void Tint(string path, Color color)
            {
                Transform t = minimapRoot.Find(path);
                Graphic g = t != null ? t.GetComponent<Graphic>() : null;
                if (g == null)
                {
                    Report.Add("WARNING: minimap graphic " + path + " not found.");
                    return;
                }

                Undo.RecordObject(g, "Level02 minimap");
                g.color = color;
                EditorUtility.SetDirty(g);
            }

            Tint("TimerPanel", MinimapTimerPanelColor);
            Tint("MinimapPanel", MinimapFrameColor);
            Tint("MinimapPanel/Background", MinimapBackgroundColor);
            Tint("MinimapPanel/Background/InnerHighlight", MinimapHighlightColor);
            Report.Add("Minimap: maps LevelBounds X " + MinX + ".." + MaxX + ", Z " + MinZ + ".." + MaxZ + " (+ path), Level_01 colours (panel #22B14C, road #146B2E, BuildNode dots #4FA8E8), one dot per living zombie.");
        }

        // ------------------------------------------------------------------ validation

        /// <summary>Checks every BuildNode: clearance to the main road edge, to the other nodes, to building footprints
        /// and to any prop/tree/animal/vehicle, and whether anything tall stands between it and the gameplay camera.
        /// Findings go to the build report (nothing is moved automatically).</summary>
        private static void ValidateLayout(Transform root)
        {
            var controller = Object.FindFirstObjectByType<AlienDefense.CameraSystem.TopDownCameraController>();
            Vector3 cameraOffset = controller != null ? new SerializedObject(controller).FindProperty("_positionOffset").vector3Value : new Vector3(9f, 14f, 0f);

            var renderers = new List<Renderer>();
            foreach (string group in new[] { "Buildings", "Vegetation", "FarmProps", "Animals", "Vehicles", "Environment/Rocks" })
            {
                Transform t = root.Find(group);
                if (t != null)
                {
                    renderers.AddRange(t.GetComponentsInChildren<Renderer>());
                }
            }

            int problems = 0;
            for (int i = 0; i < BuildNodeXZ.Length; i++)
            {
                Vector2 node = BuildNodeXZ[i];
                string name = "BuildNode_" + (i + 1).ToString("00");
                float roadGap = DistanceToMainRoad(node) - MainRoadWidth * 0.5f - BuildNodeRingRadius;
                float nearest = float.MaxValue;
                for (int j = 0; j < BuildNodeXZ.Length; j++)
                {
                    if (j != i)
                    {
                        nearest = Mathf.Min(nearest, Vector2.Distance(node, BuildNodeXZ[j]));
                    }
                }

                var inside = new List<string>();
                var blockers = new List<string>();
                Vector3 target = new Vector3(node.x, Level02PreviewTools.GroundHeight(node) + 0.6f, node.y);
                Vector3 eye = target + cameraOffset;
                var ray = new Ray(eye, (target - eye).normalized);
                float rayLength = Vector3.Distance(eye, target) - 0.5f;
                foreach (Renderer r in renderers)
                {
                    if (r == null || r is ParticleSystemRenderer)
                    {
                        continue;
                    }

                    Bounds b = r.bounds;
                    float dx = Mathf.Max(b.min.x - node.x, 0f, node.x - b.max.x);
                    float dz = Mathf.Max(b.min.z - node.y, 0f, node.y - b.max.z);
                    float gap = Mathf.Sqrt(dx * dx + dz * dz);
                    if (r.GetComponentInParent<LODGroup>() != null || r.transform.IsChildOf(root.Find("Vegetation")))
                    {
                        // Canopies and bushes are round: the AABB corner would report a false overlap.
                        gap = Vector2.Distance(new Vector2(b.center.x, b.center.z), node) - Mathf.Max(b.extents.x, b.extents.z);
                    }

                    bool tiny = b.size.y < 0.6f && b.size.x < 1.2f && b.size.z < 1.2f; // flowers, grass tufts, stones
                    if (!tiny && gap < BuildNodeRingRadius + 0.4f)
                    {
                        inside.Add(TopName(r.transform, root));
                    }

                    if (b.size.y > 1.2f && b.IntersectRay(ray, out float hit) && hit < rayLength)
                    {
                        blockers.Add(TopName(r.transform, root));
                    }
                }

                string line = name + " at (" + node.x.ToString("F1") + ", " + node.y.ToString("F1") + "): road-edge gap " + roadGap.ToString("F2")
                    + " m, nearest node " + nearest.ToString("F1") + " m";
                bool bad = roadGap < 0.3f || nearest < 7f || inside.Count > 0 || blockers.Count > 0;
                if (inside.Count > 0)
                {
                    line += ", OVERLAPS " + string.Join("/", Distinct(inside));
                }

                if (blockers.Count > 0)
                {
                    line += ", CAMERA BLOCKED BY " + string.Join("/", Distinct(blockers));
                }

                Report.Add((bad ? "NODE CHECK FAIL: " : "Node check ok: ") + line);
                problems += bad ? 1 : 0;
            }

            Report.Add("Node validation: " + (BuildNodeXZ.Length - problems) + "/" + BuildNodeXZ.Length + " clean.");
        }

        private static string TopName(Transform t, Transform root)
        {
            // The placed instance (root/Group/SubGroup/instance) is the meaningful name.
            Transform current = t;
            while (current.parent != null && current.parent.parent != null && current.parent.parent.parent != null && current.parent.parent.parent != root)
            {
                current = current.parent;
            }

            return current.name;
        }

        private static List<string> Distinct(List<string> items)
        {
            var result = new List<string>();
            foreach (string s in items)
            {
                if (!result.Contains(s))
                {
                    result.Add(s);
                }
            }

            return result;
        }
    }
}
