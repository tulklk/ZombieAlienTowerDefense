using AlienDefense.Save;
using UnityEngine;

namespace AlienDefense.Core
{
    /// <summary>Flushes any pending debounced save on OnApplicationPause/Focus-lost/Quit. Android can kill the
    /// process without ever calling OnApplicationQuit, so pending progress must already be flushed by the time
    /// OnApplicationPause(true) fires. Lives on ApplicationRoot; no other service embeds lifecycle callbacks.</summary>
    public sealed class ApplicationLifecycleController : MonoBehaviour
    {
        private PlayerProfileService _profileService;

        public void Initialize(PlayerProfileService profileService)
        {
            _profileService = profileService;
        }

        private void OnApplicationPause(bool pauseStatus)
        {
            if (pauseStatus)
            {
                _profileService?.FlushPendingSave();
            }
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus)
            {
                _profileService?.FlushPendingSave();
            }
        }

        private void OnApplicationQuit()
        {
            _profileService?.FlushPendingSave();
        }
    }
}
