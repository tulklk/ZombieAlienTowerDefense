using AlienDefense.Occlusion;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AlienDefense.EditorTools
{
    /// <summary>Gives Level 2 and Level 3 the same cheap rendering as Level 1 (user request, 2026-10-09):
    /// - NO real-time shadows on the sun (Level 1 renders none - the biggest single GPU saving on mobile: no
    ///   shadow-map pass);
    /// - static batching for scenery that never moves (anything with a gameplay/animation script on it or above
    ///   it is left dynamic: absorbable props, spinners, shaking props, animals...), as Level 1 does.
    /// Until 2026-10-11 it also copied Level 1's sun colour, ambient, fog and post profile; each level now has its
    /// own sky and look (LevelSkySetup), so this tool no longer touches them. Safe to run again.</summary>
    public static class LevelLookLikeLevel01
    {
        private static readonly string[] Targets =
        {
            "Assets/_Game/Scenes/Levels/Level_02.unity",
            "Assets/_Game/Scenes/Levels/Level_03.unity",
        };

        [MenuItem("Tools/Tower Defense/Levels/Apply Level 1 Optimisation (no shadows, static batching) To Level 2 and 3")]
        public static void ApplyFromMenu()
        {
            Debug.Log(Apply());
        }

        public static string Apply()
        {
            if (EditorApplication.isPlaying)
            {
                return "[LevelLookLikeLevel01] Stop Play mode first.";
            }

            var report = new System.Collections.Generic.List<string>();
            foreach (string path in Targets)
            {
                Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                report.Add(scene.name + ": " + DisableSunShadows() + "; " + MarkStatic(scene));
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }

            return "[LevelLookLikeLevel01] Done.\n" + string.Join("\n", report);
        }

        private static string DisableSunShadows()
        {
            foreach (Light light in Object.FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (light.type == LightType.Directional)
                {
                    light.shadows = LightShadows.None;
                    EditorUtility.SetDirty(light);
                    return "sun shadows off (like Level 1)";
                }
            }

            return "no sun found";
        }

        /// <summary>Batching-static for every mesh under the level root's scenery groups that nothing will ever move.</summary>
        private static string MarkStatic(Scene scene)
        {
            int marked = 0, skipped = 0;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (!root.name.StartsWith("Level_"))
                {
                    continue; // gameplay, UI, camera, player live outside the level root
                }

                foreach (MeshRenderer renderer in root.GetComponentsInChildren<MeshRenderer>(true))
                {
                    if (CanMove(renderer.transform, root.transform))
                    {
                        skipped++;
                        continue;
                    }

                    GameObject go = renderer.gameObject;
                    StaticEditorFlags flags = GameObjectUtility.GetStaticEditorFlags(go);
                    if ((flags & StaticEditorFlags.BatchingStatic) == 0)
                    {
                        GameObjectUtility.SetStaticEditorFlags(go, flags | StaticEditorFlags.BatchingStatic);
                        marked++;
                    }
                }
            }

            return marked + " more scenery meshes batching-static (" + skipped + " left dynamic: they move, animate or belong to gameplay)";
        }

        /// <summary>True when the object or anything above it (up to the level root) carries a behaviour that may
        /// move or animate it, or is gameplay (BuildNodes, paths, barns...). Occlusion markers are data only.</summary>
        private static bool CanMove(Transform t, Transform levelRoot)
        {
            for (Transform current = t; current != null && current != levelRoot; current = current.parent)
            {
                if (current.name == "Gameplay")
                {
                    return true;
                }

                if (current.GetComponent<Animator>() != null || current.GetComponent<ParticleSystem>() != null)
                {
                    return true;
                }

                foreach (MonoBehaviour behaviour in current.GetComponents<MonoBehaviour>())
                {
                    if (behaviour != null && !(behaviour is OcclusionObject))
                    {
                        return true;
                    }
                }
            }

            return false;
        }
    }
}
