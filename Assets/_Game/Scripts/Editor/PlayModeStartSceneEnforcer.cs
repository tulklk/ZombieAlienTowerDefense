using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AlienDefense.EditorTools
{
    /// <summary>Keeps EditorSceneManager.playModeStartScene pinned to Bootstrap.unity so pressing Play always
    /// runs the real app-start flow (Bootstrap LoadingOverlay -> MainMenu), no matter
    /// which scene happens to be open in the Editor at the time.
    ///
    /// This exists because EditorSceneManager.playModeStartScene is an in-memory-only EditorWindow-session value —
    /// it is NOT persisted anywhere on disk and silently resets to None on every domain reload (i.e. every script
    /// recompile). Setting it once via a one-off script/menu item only holds until the next compile. [InitializeOnLoadMethod]
    /// re-applies it after every domain reload (including entering/exiting Play Mode and every recompile while
    /// iterating), so it never silently reverts back to "use whatever scene is currently open".</summary>
    [InitializeOnLoad]
    internal static class PlayModeStartSceneEnforcer
    {
        private const string BootstrapScenePath = "Assets/_Game/Scenes/Bootstrap/Bootstrap.unity";

        static PlayModeStartSceneEnforcer()
        {
            Apply();
        }

        private static void Apply()
        {
            var bootstrapScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(BootstrapScenePath);
            if (bootstrapScene == null)
            {
                // Bootstrap scene not built yet (e.g. fresh project checkout before running the setup tools) — nothing to pin yet.
                return;
            }

            if (EditorSceneManager.playModeStartScene == bootstrapScene)
            {
                return;
            }

            EditorSceneManager.playModeStartScene = bootstrapScene;
            Debug.Log("[PlayModeStartSceneEnforcer] Pinned Play Mode start scene to Bootstrap.unity.");
        }
    }
}
