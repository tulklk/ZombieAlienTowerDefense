using UnityEngine;
using UnityEngine.SceneManagement;

namespace AlienDefense.Core
{
    /// <summary>Resets global time scale and reloads the current level scene in Single mode.</summary>
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
