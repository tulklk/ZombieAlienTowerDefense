using AlienDefense.Building;
using AlienDefense.Core;
using AlienDefense.Enemies;
using AlienDefense.Environment;
using AlienDefense.Player;
using AlienDefense.Towers;
using UnityEditor;
using UnityEngine;

namespace AlienDefense.EditorTools
{
    /// <summary>Bulk add/remove TractorAbsorbableProp on the current Selection — the only supported way to mark
    /// Environment Props absorbable. Never scans the whole Scene, never infers by name/tag: the user actively
    /// selects what should be absorbable (see class doc on TractorAbsorbableProp for why opt-in-by-component is
    /// the whole point). Warns (does not silently skip past) obviously-unsafe selections — Terrain, Tower,
    /// BuildNode, Enemy, Player, Camera, CompositionRoot, EnemyPath/Waypoints — all of which are detectable by
    /// component. Bare-Transform markers with no distinguishing component (e.g. BaseTarget, a tower spawn point)
    /// cannot be detected this way; avoiding those is on the person running this tool.</summary>
    internal static class TractorAbsorbablePropEditorTool
    {
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

            if (go.GetComponent<PlayerController>() != null)
            {
                reason = "is the Player";
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

            reason = null;
            return false;
        }
    }
}
