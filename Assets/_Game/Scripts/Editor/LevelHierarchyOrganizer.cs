using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AlienDefense.EditorTools
{
    /// <summary>One-shot Hierarchy cleanup for Level_01: keeps gameplay roots at the scene root, moves
    /// hand-placed décor under Maps/Environment/HandPlaced, and buckets LowPolyFarmDemo children by prefix.
    /// Never touches GeneratedFarmDecoration (FarmMapDecorator owns that folder).</summary>
    public static class LevelHierarchyOrganizer
    {
        private const string SceneNameHint = "Level_01";
        private const string ScenePath = "Assets/_Game/Scenes/Levels/Level_01.unity";
        private const string AutoRunRequestPath = "Assets/_Game/EditorReports/OrganizeLevel01.request";

        private static readonly string[] RootOrder =
        {
            "CompositionRoot",
            "Player",
            "CameraRig",
            "BuildNodes",
            "Systems",
            "Runtime",
            "Canvas",
            "EventSystem",
            "AudioService",
            "SceneServicesHost",
            "Maps",
        };

        [InitializeOnLoadMethod]
        private static void AutoRunIfRequested()
        {
            if (!File.Exists(AutoRunRequestPath))
            {
                return;
            }

            EditorApplication.delayCall += () =>
            {
                if (!File.Exists(AutoRunRequestPath))
                {
                    return;
                }

                try
                {
                    File.Delete(AutoRunRequestPath);
                    string metaPath = AutoRunRequestPath + ".meta";
                    if (File.Exists(metaPath))
                    {
                        File.Delete(metaPath);
                    }
                }
                catch
                {
                    // Ignore delete races; organize still runs once this domain reload.
                }

                Scene active = SceneManager.GetActiveScene();
                string activePath = active.IsValid() ? active.path.Replace('\\', '/') : string.Empty;
                if (!activePath.EndsWith("Level_01.unity"))
                {
                    Scene opened = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
                    if (!opened.IsValid())
                    {
                        Debug.LogError("[LevelHierarchyOrganizer] Auto-run failed to open Level_01.");
                        return;
                    }
                }

                OrganizeActiveScene(saveIfDirty: true);
                AssetDatabase.Refresh();
            };
        }

        [MenuItem("AlienDefense/Setup/Organize Level_01 Hierarchy")]
        public static void OrganizeLevel01Hierarchy()
        {
            OrganizeActiveScene(saveIfDirty: false);
        }

        /// <summary>Batch-mode entry: opens Level_01, organizes, and saves.</summary>
        public static void OrganizeAndSaveLevel01()
        {
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            if (!scene.IsValid())
            {
                Debug.LogError("[LevelHierarchyOrganizer] Failed to open " + ScenePath);
                return;
            }

            OrganizeActiveScene(saveIfDirty: true);
        }

        private static void OrganizeActiveScene(bool saveIfDirty)
        {
            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.isLoaded)
            {
                Debug.LogError("[LevelHierarchyOrganizer] No loaded active scene.");
                return;
            }

            GameObject mapsGo = GameObject.Find("Maps");
            GameObject demoSceneGo = FindSceneObjectByName("DemoScene");
            GameObject lowPolyDemoGo = FindSceneObjectByName("LowPolyFarmDemo");

            bool looksLikeLevel01 = scene.name.Contains(SceneNameHint)
                || mapsGo != null
                || demoSceneGo != null
                || lowPolyDemoGo != null;

            if (!looksLikeLevel01)
            {
                Debug.LogError("[LevelHierarchyOrganizer] Active scene does not look like Level_01 (need Maps and/or DemoScene). Aborting.");
                return;
            }

            if (mapsGo == null)
            {
                Debug.LogError("[LevelHierarchyOrganizer] Missing Maps root. Aborting.");
                return;
            }

            Transform maps = mapsGo.transform;
            Transform environment = maps.Find("Environment");
            if (environment == null)
            {
                Debug.LogError("[LevelHierarchyOrganizer] Missing Maps/Environment. Aborting so FarmMapDecorator path stays intact.");
                return;
            }

            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Organize Level_01 Hierarchy");

            Transform handPlaced = EnsureChild(environment, "HandPlaced");
            Transform animals = EnsureChild(handPlaced, "Animals");
            Transform unused = EnsureChild(handPlaced, "_Unused");

            int moved = 0;

            Transform demoRoot = null;
            if (demoSceneGo != null)
            {
                demoRoot = demoSceneGo.transform;
                Undo.RecordObject(demoRoot.gameObject, "Rename DemoScene");
                demoRoot.gameObject.name = "LowPolyFarmDemo";
            }
            else if (lowPolyDemoGo != null)
            {
                demoRoot = lowPolyDemoGo.transform;
            }

            if (demoRoot != null)
            {
                if (demoRoot.parent != handPlaced)
                {
                    Reparent(demoRoot, handPlaced);
                    moved++;
                }

                moved += BucketLowPolyFarmDemo(demoRoot);
            }

            moved += MoveIfExists("AbsorbableTrees", handPlaced);
            moved += MoveIfExists("SheepPen", animals);
            moved += MoveIfExists("HorsePen", animals);
            moved += MoveIfExists("Cows", animals);

            GameObject zombiesLeftover = FindSceneObjectByName("Zombies stylized concatenated");
            if (zombiesLeftover != null)
            {
                if (zombiesLeftover.transform.parent != unused)
                {
                    Reparent(zombiesLeftover.transform, unused);
                    moved++;
                }

                if (zombiesLeftover.activeSelf)
                {
                    Undo.RecordObject(zombiesLeftover, "Deactivate unused zombie leftover");
                    zombiesLeftover.SetActive(false);
                }
            }

            // Orphan Pandazole / animal prefabs left at scene root (not under GeneratedFarmDecoration).
            moved += SweepRootPandazoleDecor(scene, handPlaced, animals);

            OrderSceneRoots(scene);

            Undo.CollapseUndoOperations(undoGroup);
            EditorSceneManager.MarkSceneDirty(scene);

            if (saveIfDirty)
            {
                EditorSceneManager.SaveScene(scene);
            }

            Debug.Log(
                $"[LevelHierarchyOrganizer] Organized '{scene.name}'. " +
                $"Moves/buckets applied: {moved}. " +
                "Décor lives under Maps/Environment/HandPlaced. GeneratedFarmDecoration untouched.");
        }

        private static int BucketLowPolyFarmDemo(Transform demoRoot)
        {
            Transform crops = EnsureChild(demoRoot, "Crops");
            Transform fences = EnsureChild(demoRoot, "Fences");
            Transform props = EnsureChild(demoRoot, "Props");
            Transform trees = EnsureChild(demoRoot, "Trees");
            Transform other = EnsureChild(demoRoot, "Other");

            var children = new List<Transform>();
            for (int i = 0; i < demoRoot.childCount; i++)
            {
                Transform child = demoRoot.GetChild(i);
                string n = child.name;
                if (n == "Crops" || n == "Fences" || n == "Props" || n == "Trees" || n == "Other")
                {
                    continue;
                }

                children.Add(child);
            }

            int moved = 0;
            for (int i = 0; i < children.Count; i++)
            {
                Transform child = children[i];
                Transform bucket = ResolveBucket(child.name, crops, fences, props, trees, other);
                if (child.parent == bucket)
                {
                    continue;
                }

                Reparent(child, bucket);
                moved++;
            }

            return moved;
        }

        private static Transform ResolveBucket(
            string objectName,
            Transform crops,
            Transform fences,
            Transform props,
            Transform trees,
            Transform other)
        {
            if (StartsWith(objectName, "Cabbage_")
                || StartsWith(objectName, "TomatoPlant_")
                || StartsWith(objectName, "Tomato_")
                || StartsWith(objectName, "Mud_"))
            {
                return crops;
            }

            if (StartsWith(objectName, "Fence_"))
            {
                return fences;
            }

            if (StartsWith(objectName, "Tree_"))
            {
                return trees;
            }

            if (StartsWith(objectName, "Box_")
                || StartsWith(objectName, "WateringCan_")
                || StartsWith(objectName, "Rock_")
                || StartsWith(objectName, "Grass_"))
            {
                return props;
            }

            return other;
        }

        private static bool StartsWith(string name, string prefix)
        {
            return name != null && name.StartsWith(prefix);
        }

        private static int MoveIfExists(string rootName, Transform newParent)
        {
            GameObject go = FindSceneObjectByName(rootName);
            if (go == null || go.transform.parent == newParent)
            {
                return 0;
            }

            Reparent(go.transform, newParent);
            return 1;
        }

        /// <summary>Moves root-level Bld_/Prop_/Env_ instances under HandPlaced/PandazoleLoose,
        /// and loose animal prefabs under Animals. Never touches GeneratedFarmDecoration.</summary>
        private static int SweepRootPandazoleDecor(Scene scene, Transform handPlaced, Transform animals)
        {
            Transform pandazoleBucket = EnsureChild(handPlaced, "PandazoleLoose");
            int moved = 0;
            GameObject[] roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                GameObject go = roots[i];
                if (IsProtectedGameplayRoot(go.name))
                {
                    continue;
                }

                if (IsLooseAnimalName(go.name))
                {
                    Reparent(go.transform, animals);
                    moved++;
                    continue;
                }

                if (!IsPandazoleDecorName(go.name))
                {
                    continue;
                }

                Reparent(go.transform, pandazoleBucket);
                moved++;
            }

            return moved;
        }

        private static bool IsProtectedGameplayRoot(string name)
        {
            for (int i = 0; i < RootOrder.Length; i++)
            {
                if (RootOrder[i] == name)
                {
                    return true;
                }
            }

            return name == "DemoScene"
                || name == "LowPolyFarmDemo"
                || name == "AbsorbableTrees"
                || name == "SheepPen"
                || name == "HorsePen"
                || name == "Cows"
                || name == "Zombies stylized concatenated";
        }

        private static bool IsPandazoleDecorName(string name)
        {
            return StartsWith(name, "Bld_")
                || StartsWith(name, "Prop_")
                || StartsWith(name, "Env_");
        }

        private static bool IsLooseAnimalName(string name)
        {
            return name == "Horse"
                || name == "Sheep"
                || name == "Cow"
                || StartsWith(name, "Horse")
                || StartsWith(name, "Sheep")
                || StartsWith(name, "Cow");
        }

        private static GameObject FindSceneObjectByName(string name)
        {
            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid())
            {
                return null;
            }

            GameObject[] roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                GameObject match = FindInHierarchy(roots[i].transform, name);
                if (match != null)
                {
                    return match;
                }
            }

            return null;
        }

        private static GameObject FindInHierarchy(Transform root, string name)
        {
            if (root.name == name)
            {
                return root.gameObject;
            }

            for (int i = 0; i < root.childCount; i++)
            {
                GameObject match = FindInHierarchy(root.GetChild(i), name);
                if (match != null)
                {
                    return match;
                }
            }

            return null;
        }

        private static Transform EnsureChild(Transform parent, string childName)
        {
            Transform existing = parent.Find(childName);
            if (existing != null)
            {
                return existing;
            }

            var go = new GameObject(childName);
            Undo.RegisterCreatedObjectUndo(go, "Create " + childName);
            Undo.SetTransformParent(go.transform, parent, "Parent " + childName);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;
            return go.transform;
        }

        private static void Reparent(Transform child, Transform newParent)
        {
            Undo.SetTransformParent(child, newParent, "Reparent " + child.name);
        }

        private static void OrderSceneRoots(Scene scene)
        {
            for (int i = 0; i < RootOrder.Length; i++)
            {
                GameObject root = FindRootByName(scene, RootOrder[i]);
                if (root != null)
                {
                    root.transform.SetAsLastSibling();
                }
            }
        }

        private static GameObject FindRootByName(Scene scene, string name)
        {
            GameObject[] roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                if (roots[i].name == name)
                {
                    return roots[i];
                }
            }

            return null;
        }
    }
}
