using UnityEngine;

namespace AlienDefense.Core
{
    /// <summary>Application-scope holder for which level Stable ID Gameplay should load next. Plain C# (no
    /// ScriptableObject, no static Instance) so it can be owned and passed explicitly by ApplicationRuntime.</summary>
    public sealed class LevelLaunchContext
    {
        public string SelectedLevelId { get; private set; }
        public bool HasSelection => !string.IsNullOrEmpty(SelectedLevelId);

        public void SetSelectedLevel(string levelId)
        {
            if (string.IsNullOrWhiteSpace(levelId))
            {
                Debug.LogError("[LevelLaunchContext] Ignored SetSelectedLevel with an empty id.");
                return;
            }

            SelectedLevelId = levelId;
        }

        public void Clear()
        {
            SelectedLevelId = null;
        }
    }
}
