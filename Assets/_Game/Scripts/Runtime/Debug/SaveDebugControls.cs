using AlienDefense.Save;
using UnityEngine;

namespace AlienDefense.DebugTools
{
    /// <summary>Manual save testing controls (right-click the component in the Inspector). Every action re-checks
    /// Debug.isDebugBuild at call time so it stays inert even if something other than the Editor context menu
    /// ever invokes it in a release build.</summary>
    public sealed class SaveDebugControls : MonoBehaviour
    {
        private PlayerProfileService _profileService;
        private SaveFileRepository _repository;

        public void Initialize(PlayerProfileService profileService, SaveFileRepository repository)
        {
            _profileService = profileService;
            _repository = repository;
        }

#if UNITY_EDITOR
        [ContextMenu("Force Save Now")]
#endif
        public void ForceSaveNow()
        {
            if (!ValidateDebugBuild())
            {
                return;
            }

            _profileService?.FlushPendingSave();
            Debug.Log("[SaveDebugControls] Forced a save flush.");
        }

#if UNITY_EDITOR
        [ContextMenu("Log Save File Path")]
#endif
        public void LogSaveFilePath()
        {
            Debug.Log("[SaveDebugControls] persistentDataPath = " + Application.persistentDataPath);
        }

#if UNITY_EDITOR
        [ContextMenu("Corrupt Main Save File (Test Recovery)")]
#endif
        public void CorruptMainSaveFile()
        {
            if (!ValidateDebugBuild())
            {
                return;
            }

            _repository?.DebugCorruptMainFile();
            Debug.LogWarning("[SaveDebugControls] Main save file corrupted for testing. Restart the app to see backup recovery.");
        }

#if UNITY_EDITOR
        [ContextMenu("Reset Profile (Delete All Save Files)")]
#endif
        public void ResetProfile()
        {
            if (!ValidateDebugBuild())
            {
                return;
            }

            _repository?.DebugDeleteAllFiles();
            Debug.LogWarning("[SaveDebugControls] Save files deleted. Restart the app to get a fresh default profile.");
        }

        private static bool ValidateDebugBuild()
        {
            if (Debug.isDebugBuild)
            {
                return true;
            }

            Debug.LogWarning("[SaveDebugControls] Ignored: not a development/debug build.");
            return false;
        }
    }
}
