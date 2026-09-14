using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AlienDefense.EditorTools
{
    /// <summary>Chooses which scene pressing Play starts from, via the menu toggle
    /// AlienDefense/Play Mode/Start From Bootstrap (saved per machine in EditorPrefs):
    /// - Off (default): Play runs the scene currently open in the Editor, for testing a single scene directly.
    /// - On: Play always runs the real app-start flow (Bootstrap LoadingOverlay -> MainMenu), whichever scene is open.
    ///
    /// EditorSceneManager.playModeStartScene is an in-memory-only value that silently resets to None on every
    /// domain reload (every script recompile), so [InitializeOnLoad] re-applies the chosen mode after each one.</summary>
    [InitializeOnLoad]
    internal static class PlayModeStartSceneEnforcer
    {
        private const string BootstrapScenePath = "Assets/_Game/Scenes/Bootstrap/Bootstrap.unity";
        private const string MenuPath = "AlienDefense/Play Mode/Start From Bootstrap";
        private const string PrefKey = "AlienDefense.PlayModeStartFromBootstrap";

        static PlayModeStartSceneEnforcer()
        {
            // Deferred: menu check marks can't be set from a static constructor.
            EditorApplication.delayCall += Apply;
            Apply();
        }

        private static bool StartFromBootstrap
        {
            get => EditorPrefs.GetBool(PrefKey, false);
            set => EditorPrefs.SetBool(PrefKey, value);
        }

        [MenuItem(MenuPath, priority = 0)]
        private static void ToggleStartFromBootstrap()
        {
            StartFromBootstrap = !StartFromBootstrap;
            Apply();
            Debug.Log(StartFromBootstrap
                ? "[PlayModeStartSceneEnforcer] Play now starts from Bootstrap.unity (MainMenu flow)."
                : "[PlayModeStartSceneEnforcer] Play now starts from the scene currently open in the Editor.");
        }

        [MenuItem(MenuPath, validate = true)]
        private static bool ToggleStartFromBootstrapValidate()
        {
            Menu.SetChecked(MenuPath, StartFromBootstrap);
            return !EditorApplication.isPlayingOrWillChangePlaymode;
        }

        private static void Apply()
        {
            Menu.SetChecked(MenuPath, StartFromBootstrap);

            if (!StartFromBootstrap)
            {
                if (EditorSceneManager.playModeStartScene != null)
                {
                    EditorSceneManager.playModeStartScene = null;
                }

                return;
            }

            var bootstrapScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(BootstrapScenePath);
            if (bootstrapScene == null)
            {
                // Bootstrap scene not built yet (e.g. fresh project checkout before running the setup tools) — nothing to pin yet.
                return;
            }

            if (EditorSceneManager.playModeStartScene != bootstrapScene)
            {
                EditorSceneManager.playModeStartScene = bootstrapScene;
            }
        }
    }
}
