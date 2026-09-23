using System.Text;
using AlienDefense.Enemies;
using AlienDefense.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace AlienDefense.EditorTools
{
    /// <summary>Turns the cast shadow of the characters - every enemy (boss included) and the player's UFO - on or
    /// off in one pass, across their prefabs and any instance in the open scene.
    ///
    /// Only shadow CASTING changes. The characters still receive shadows from the trees and buildings they walk
    /// under, and every other shadow caster in the level is untouched, so the scene keeps its lighting.
    ///
    /// It is also the cheapest frame win left on a phone: the main light re-renders every caster into the shadow
    /// map, so a screen full of skinned zombies was being submitted twice. Both directions are one menu item, so
    /// this is easy to undo if the flat look is not wanted.</summary>
    internal static class CharacterShadowSetup
    {
        private const string PlayerPrefabPath = "Assets/_Game/Prefabs/Player/UFO_Player.prefab";
        private const string PrefabSearchFolder = "Assets/_Game/Prefabs";

        [MenuItem("AlienDefense/Setup/Visual/Ground Shadows - Enemies + Player OFF")]
        private static void DisableShadows()
        {
            Apply(ShadowCastingMode.Off);
        }

        [MenuItem("AlienDefense/Setup/Visual/Ground Shadows - Enemies + Player ON")]
        private static void EnableShadows()
        {
            Apply(ShadowCastingMode.On);
        }

        private static void Apply(ShadowCastingMode mode)
        {
            var report = new StringBuilder($"[CharacterShadowSetup] cast shadows -> {mode}");
            int prefabsTouched = 0;
            int renderersTouched = 0;

            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { PrefabSearchFolder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null)
                {
                    continue;
                }

                bool isCharacter = prefab.GetComponentInChildren<EnemyController>(true) != null
                    || prefab.GetComponentInChildren<PlayerController>(true) != null
                    || path == PlayerPrefabPath;
                if (!isCharacter)
                {
                    continue;
                }

                GameObject contents = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    int changed = ApplyToHierarchy(contents, mode);
                    if (changed > 0)
                    {
                        PrefabUtility.SaveAsPrefabAsset(contents, path);
                        prefabsTouched++;
                        renderersTouched += changed;
                        report.AppendLine();
                        report.Append($"  {System.IO.Path.GetFileName(path)}: {changed} renderers");
                    }
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(contents);
                }
            }

            // Scene instances can carry their own override, so the open scene is swept too.
            int sceneRenderers = 0;
            foreach (EnemyController enemy in Object.FindObjectsByType<EnemyController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                sceneRenderers += ApplyToHierarchy(enemy.gameObject, mode);
            }

            foreach (PlayerController player in Object.FindObjectsByType<PlayerController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                sceneRenderers += ApplyToHierarchy(player.gameObject, mode);
            }

            if (sceneRenderers > 0)
            {
                EditorSceneManager.MarkAllScenesDirty();
                EditorSceneManager.SaveOpenScenes();
                report.AppendLine();
                report.Append($"  open scene: {sceneRenderers} renderers");
            }

            AssetDatabase.SaveAssets();
            report.AppendLine();
            report.Append($"  {prefabsTouched} prefabs, {renderersTouched + sceneRenderers} renderers in total.");
            Debug.Log(report.ToString());
        }

        /// <summary>Particles and world-space UI (health bars) are skipped - they were never shadow casters and
        /// flipping them would only churn the scene file.
        ///
        /// Switching back ON is deliberately narrower than switching OFF: it only re-enables the character's body
        /// (the skinned meshes, and the UFO's hull under its "ufo" / "Model" child). The tractor beam cone, its
        /// ground ring and the fake blob shadow ship with casting already off on purpose - a beam that casts a
        /// shadow looks wrong - so a restore must not hand them a shadow they never had.</summary>
        private static int ApplyToHierarchy(GameObject root, ShadowCastingMode mode)
        {
            int changed = 0;
            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer is ParticleSystemRenderer || renderer.GetComponentInParent<Canvas>() != null)
                {
                    continue;
                }

                if (renderer.shadowCastingMode == mode)
                {
                    continue;
                }

                if (mode != ShadowCastingMode.Off && !IsCharacterBody(renderer))
                {
                    continue;
                }

                renderer.shadowCastingMode = mode;
                EditorUtility.SetDirty(renderer);
                changed++;
            }

            return changed;
        }

        private static bool IsCharacterBody(Renderer renderer)
        {
            if (renderer is SkinnedMeshRenderer)
            {
                return true;
            }

            for (Transform cursor = renderer.transform; cursor != null; cursor = cursor.parent)
            {
                if (cursor.name == "ufo" || cursor.name == "Model")
                {
                    return true;
                }
            }

            return false;
        }
    }
}
