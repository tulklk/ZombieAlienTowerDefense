using System.Collections.Generic;
using AlienDefense.Building;
using AlienDefense.Core;
using AlienDefense.Enemies;
using AlienDefense.Environment;
using AlienDefense.Player;
using AlienDefense.Towers;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AlienDefense.EditorTools
{
    /// <summary>Bulk add/remove TractorAbsorbableProp — either on the current Selection (the original, precise
    /// way), or via "Scan &amp; Mark" which walks the whole active Scene and figures out for itself what looks
    /// like absorbable decoration vs. what must never be touched. Both paths share the same safety checks
    /// (Terrain, Tower, BuildNode, Enemy, Player, Camera, CompositionRoot, EnemyPath/Waypoints, live decorative
    /// animals) so "Scan &amp; Mark" is never more permissive than hand-selecting would be — it is meant to be
    /// re-run after adding new decoration (by hand or by another tool) instead of re-selecting everything.</summary>
    internal static class TractorAbsorbablePropEditorTool
    {
        // Ground-level or structural decoration that must stay exactly where it's placed - absorbing it would
        // either leave a visible hole in the ground (farmland/tillage/mud tiles) or break an enclosure's shape
        // (fences, buildings, wells). Matched against the start of the GameObject's name (prefab instances keep
        // the source prefab's name, e.g. "Prop_Haystack_02 (3)"), case-insensitive.
        private static readonly string[] ExcludedNamePrefixes =
        {
            "Bld_", "Env_Well_", "Env_WoodFence_", "Fence_Middle", "FenceGate_", "FenceEnd", "Fence_01",
            "Env_FarmLand_", "Tillage_", "Mud_01", "Env_MetalFence_",
        };

        // Root GameObjects that "Scan And Mark" never even walks into - pure infrastructure (systems, UI, the
        // Player/camera rig, unused leftover asset imports) that happens to contain Renderers/model instances
        // too (e.g. the UFO's own hull, zombie reference models nobody spawns), which is exactly what made the
        // very first version of this scan unsafe. Only "Maps" (the level content) and any top-level decoration
        // root the user adds by hand (animal pens, DemoScene imports, etc.) are meant to be walked - so this is
        // a short deny-list of names we KNOW are never decoration, not an allow-list of where decoration lives.
        private static readonly string[] SkippedRootNames =
        {
            "CompositionRoot", "Player", "CameraRig", "Runtime", "AudioService", "BuildNodes", "Systems",
            "Canvas", "EventSystem", "SceneServicesHost", "Zombies stylized concatenated",
        };

        // Specific individual objects that must never become absorbable even though nothing about their
        // component makeup singles them out (see class doc on TractorAbsorbableProp / IsUnsafeToMark: bare
        // structural markers can't be caught by component alone). Exact name match, not a prefix - these are
        // singletons, not a family of prefab instances.
        private static readonly string[] CriticalExactNames =
        {
            "Plane", "PT_Wooden_Bridge_02", "PlayerBase", "BaseVisual", "LevelBounds",
            "TowerSpawnPoint_Debug", "Directional Light", "colliders",
        };


        [MenuItem("Tools/Alien Defense/Tractor Beam/Mark Selected As Absorbable")]
        public static void MarkSelectedAsAbsorbable()
        {
            GameObject[] selection = Selection.gameObjects;
            if (selection.Length == 0)
            {
                Debug.LogWarning("[AlienDefense] Mark Selected As Absorbable: nothing selected.");
                return;
            }

            int added = 0;
            int alreadyMarked = 0;
            int unsafeSkipped = 0;
            int visualRootFixed = 0;

            for (int i = 0; i < selection.Length; i++)
            {
                GameObject go = selection[i];

                if (IsUnsafeToMark(go, out string reason))
                {
                    Debug.LogWarning($"[AlienDefense] Skipped '{go.name}': {reason}. Not marking as absorbable.", go);
                    unsafeSkipped++;
                    continue;
                }

                TractorAbsorbableProp prop = go.GetComponent<TractorAbsorbableProp>();
                if (prop != null)
                {
                    alreadyMarked++;
                }
                else
                {
                    prop = Undo.AddComponent<TractorAbsorbableProp>(go);
                    added++;
                }

                // Re-running this on an already-marked selection (e.g. after seeing the "no Visual Root
                // assigned" warning) retroactively fixes it too — no separate menu item needed.
                if (AutoAssignVisualRootIfMissing(prop))
                {
                    visualRootFixed++;
                }
            }

            Debug.Log($"[AlienDefense] Mark Selected As Absorbable: {added} added, {alreadyMarked} already marked, " +
                $"{visualRootFixed} Visual Root auto-assigned, {unsafeSkipped} skipped as unsafe.");
        }

        // The UFO's own body sits roughly this many units above the ground while hovering (measured empirically
        // in Play Mode) - anything taller than this gets scaled down so it visually fits under the ship instead
        // of poking through it during Lift. Kept a bit under the measured clearance as a safety margin.
        private const float MaxAbsorbableHeight = 4.3f;

        /// <summary>Re-runnable, no-selection-needed version of "Mark Selected As Absorbable": walks the whole
        /// active Scene, finds every distinct Prefab/model instance that isn't already absorbable, and marks
        /// the ones that look like decoration. Deliberately conservative in three layers: it never even walks
        /// into <see cref="SkippedRootNames"/> (Player/cameras/UI/systems/unused asset leftovers - this is what
        /// an earlier, unsafe version of this scan got wrong, marking the UFO's own hull, the ground Plane, the
        /// bridge, the invisible collision boundary and PlayerBase absorbable); it exact-name-skips
        /// <see cref="CriticalExactNames"/> for singleton objects a component check can't catch; and within
        /// whatever's left, it applies the same component/name safety checks as the Selection-based tool. Only
        /// considers Prefab/model instance ROOTS (never loose non-prefab meshes, never a prefab's own internal
        /// bones/sub-meshes) - a genuinely loose hand-authored mesh still needs "Mark Selected As Absorbable".
        /// Also fixes up two things that are easy to forget when adding decoration by hand: it un-sets Static (a
        /// statically-batched object silently fails to visually move when the tractor beam pulls it) and caps
        /// render height to <see cref="MaxAbsorbableHeight"/> (so nothing new pokes up through the UFO's hull).
        /// Re-run this any time after adding new decoration - already-marked props are left alone (only
        /// re-checked for the Static/height fixups), so it is always safe to run again.</summary>
        [MenuItem("Tools/Alien Defense/Tractor Beam/Scan And Mark New Absorbable Props (Whole Scene)")]
        public static void ScanAndMarkWholeScene()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (!scene.isLoaded)
            {
                Debug.LogWarning("[AlienDefense] Scan & Mark: no active scene loaded.");
                return;
            }

            // Every distinct Prefab/model instance root under an allowed scene root (see SkippedRootNames) -
            // never a loose non-prefab mesh, never an instance's own internal bones/sub-meshes, so the whole
            // tree/crate/building/animal gets marked once, not once per part.
            var candidates = new HashSet<GameObject>();
            GameObject[] roots = scene.GetRootGameObjects();
            for (int r = 0; r < roots.Length; r++)
            {
                if (System.Array.IndexOf(SkippedRootNames, roots[r].name) >= 0)
                {
                    continue;
                }

                Transform[] all = roots[r].GetComponentsInChildren<Transform>(true);
                for (int i = 0; i < all.Length; i++)
                {
                    GameObject prefabRoot = PrefabUtility.GetNearestPrefabInstanceRoot(all[i].gameObject);
                    if (prefabRoot != null)
                    {
                        candidates.Add(prefabRoot);
                    }
                }
            }

            int added = 0, alreadyMarked = 0, unsafeSkipped = 0, excludedByName = 0, visualRootFixed = 0,
                unstaticked = 0, heightCapped = 0;

            foreach (GameObject go in candidates)
            {
                if (go == null) continue;

                bool isAlreadyMarked = go.GetComponent<TractorAbsorbableProp>() != null;

                if (!isAlreadyMarked)
                {
                    if (System.Array.IndexOf(CriticalExactNames, go.name) >= 0 || IsUnderCriticalContainer(go.transform))
                    {
                        excludedByName++;
                        continue;
                    }

                    if (IsExcludedByName(go.name))
                    {
                        excludedByName++;
                        continue;
                    }

                    if (IsUnsafeToMark(go, out string reason))
                    {
                        unsafeSkipped++;
                        continue;
                    }
                }

                TractorAbsorbableProp prop = go.GetComponent<TractorAbsorbableProp>();
                if (prop == null)
                {
                    prop = Undo.AddComponent<TractorAbsorbableProp>(go);
                    added++;
                }
                else
                {
                    alreadyMarked++;
                }

                if (AutoAssignVisualRootIfMissing(prop))
                {
                    visualRootFixed++;
                }

                if (UnStaticIfNeeded(go))
                {
                    unstaticked++;
                }

                if (CapHeightIfNeeded(go, MaxAbsorbableHeight))
                {
                    heightCapped++;
                }
            }

            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log($"[AlienDefense] Scan & Mark ('{scene.name}'): {added} newly marked, {alreadyMarked} already marked, " +
                $"{visualRootFixed} Visual Root auto-assigned, {unstaticked} un-staticked, {heightCapped} height-capped, " +
                $"{excludedByName} excluded by name (building/fence/ground tile), {unsafeSkipped} skipped as unsafe.");
        }

        /// <summary>Static objects render via a combined batch - moving their Transform at runtime (Pull/Lift)
        /// silently does nothing visually. Returns true if it changed anything.</summary>
        private static bool UnStaticIfNeeded(GameObject go)
        {
            if (!go.isStatic && GameObjectUtility.GetStaticEditorFlags(go) == 0)
            {
                return false;
            }

            Undo.RegisterCompleteObjectUndo(go, "Un-static absorbable prop");
            GameObjectUtility.SetStaticEditorFlags(go, 0);
            go.isStatic = false;
            return true;
        }

        /// <summary>Uniformly scales the prop down (around its current position) if its combined renderer bounds
        /// are taller than <paramref name="maxHeight"/>. Returns true if it changed anything.</summary>
        private static bool CapHeightIfNeeded(GameObject go, float maxHeight)
        {
            Renderer[] renderers = go.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0)
            {
                return false;
            }

            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
            {
                bounds.Encapsulate(renderers[i].bounds);
            }

            float height = bounds.size.y;
            if (height <= maxHeight || height <= 0f)
            {
                return false;
            }

            float scaleFactor = maxHeight / height;
            Vector3 position = go.transform.position;
            Undo.RecordObject(go.transform, "Cap absorbable prop height");
            go.transform.localScale *= scaleFactor;
            go.transform.position = position;
            return true;
        }

        /// <summary>Fills in an empty Visual Root with whatever Renderer actually represents this prop: a child
        /// "Model"-style mesh if one exists, otherwise the prop's own Transform (safe — Pull/Lift only ever move
        /// position, so scaling/spinning that same Transform for the Lift shrink never conflicts). Returns true
        /// if it changed anything.</summary>
        private static bool AutoAssignVisualRootIfMissing(TractorAbsorbableProp prop)
        {
            var serialized = new SerializedObject(prop);
            SerializedProperty visualRootProperty = serialized.FindProperty("_visualRoot");
            if (visualRootProperty.objectReferenceValue != null)
            {
                return false;
            }

            Renderer renderer = prop.GetComponentInChildren<Renderer>();
            Transform visualRoot = renderer != null ? renderer.transform : prop.transform;

            visualRootProperty.objectReferenceValue = visualRoot;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return true;
        }

        [MenuItem("Tools/Alien Defense/Tractor Beam/Remove Absorbable From Selected")]
        public static void RemoveAbsorbableFromSelected()
        {
            GameObject[] selection = Selection.gameObjects;
            if (selection.Length == 0)
            {
                Debug.LogWarning("[AlienDefense] Remove Absorbable From Selected: nothing selected.");
                return;
            }

            int removed = 0;
            for (int i = 0; i < selection.Length; i++)
            {
                var component = selection[i].GetComponent<TractorAbsorbableProp>();
                if (component != null)
                {
                    Undo.DestroyObjectImmediate(component);
                    removed++;
                }
            }

            Debug.Log($"[AlienDefense] Removed TractorAbsorbableProp from {removed} object(s).");
        }

        private static bool IsUnsafeToMark(GameObject go, out string reason)
        {
            if (go.GetComponent<Terrain>() != null || go.GetComponent<TerrainCollider>() != null)
            {
                reason = "is Terrain";
                return true;
            }

            if (go.GetComponent<TowerController>() != null)
            {
                reason = "is a Tower";
                return true;
            }

            if (go.GetComponent<BuildNode>() != null)
            {
                reason = "is a BuildNode";
                return true;
            }

            if (go.GetComponent<EnemyController>() != null)
            {
                reason = "is an Enemy";
                return true;
            }

            if (go.GetComponentInParent<PlayerController>() != null)
            {
                reason = "is the Player or one of its child parts (e.g. the UFO hull mesh)";
                return true;
            }

            if (go.GetComponent<Camera>() != null)
            {
                reason = "has a Camera component";
                return true;
            }

            if (go.GetComponent<LevelCompositionRoot>() != null)
            {
                reason = "is the CompositionRoot";
                return true;
            }

            if (go.GetComponentInParent<EnemyPath3D>() != null)
            {
                reason = "is the EnemyPath or one of its Waypoints";
                return true;
            }

            if (go.GetComponent<AnimalWanderer>() != null || go.GetComponent<Animator>() != null)
            {
                // Every decorative farm animal (Cow/Horse/Sheep) carries an Animator so its Idle/Walk clips can
                // play - none of this project's other decoration does, so this alone safely singles animals out
                // even for the ones (e.g. Sheep, which has no Walk clip) that don't also have AnimalWanderer.
                reason = "is a live decorative animal (has an Animator) - absorbing it would delete it, not just its clutter";
                return true;
            }

            if (go.GetComponentInParent<Canvas>() != null)
            {
                reason = "is under a UI Canvas";
                return true;
            }

            reason = null;
            return false;
        }

        /// <summary>True if any ancestor (up to the scene root) is named "colliders" - the invisible collision-
        /// boundary container found under Maps/ZombieRoad, whose child Cube meshes have Renderers but must never
        /// be absorbable.</summary>
        private static bool IsUnderCriticalContainer(Transform t)
        {
            for (Transform current = t.parent; current != null; current = current.parent)
            {
                if (current.name == "colliders")
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>True if the GameObject's name starts with one of <see cref="ExcludedNamePrefixes"/> - ground
        /// tiles, fences and buildings that "Scan And Mark" must never touch even though nothing about their
        /// components makes them unsafe. Prefab instance names keep the source prefab's name (e.g.
        /// "Env_FarmLand_04_Watered (12)"), so a StartsWith match against the un-suffixed prefab name is enough.</summary>
        private static bool IsExcludedByName(string goName)
        {
            for (int i = 0; i < ExcludedNamePrefixes.Length; i++)
            {
                if (goName.StartsWith(ExcludedNamePrefixes[i], System.StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
