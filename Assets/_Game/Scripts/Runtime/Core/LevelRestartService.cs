using UnityEngine;
using UnityEngine.SceneManagement;

namespace AlienDefense.Core
{
    /// <summary>Resets global time scale and reloads the current level scene. Relies on Unity's own scene
    /// teardown (Single load mode) to dispose all non-persistent objects; no manual cleanup needed since
    /// nothing in this codebase uses DontDestroyOnLoad or static gameplay state.</summary>
    public sealed class LevelRestartService
    {
        private readonly string _sceneName;

        public LevelRestartService(string sceneName)
        {
            _sceneName = sceneName;
        }

        public void Restart()
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(_sceneName, LoadSceneMode.Single);
        }
    }
}
