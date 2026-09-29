using UnityEngine;

namespace AlienDefense.Settings
{
    /// <summary>Maps the saved frame-rate preference to an actual target: 30 is the power-saving choice, anything
    /// else means "as smooth as this screen allows" (60 Hz panel -> 60 FPS, 90 Hz -> 90, 120 Hz -> 120), further
    /// limited by what AdaptiveFrameRateGovernor has measured this device can sustain.</summary>
    public static class DisplayFrameRate
    {
        public const int PowerSaving = 30;
        public const int BaselineSmooth = 60;
        private const int MaxSmooth = 120;

        private static int _cachedMaxRefreshRate;
        private static int _sustainableCap = int.MaxValue;

        public static int NativeSmoothRate => Mathf.Clamp(GetMaxRefreshRate(), BaselineSmooth, MaxSmooth);

        public static bool IsLimited => _sustainableCap < NativeSmoothRate;

        public static int Resolve(int savedTargetFrameRate)
        {
            if (savedTargetFrameRate == PowerSaving)
            {
                return PowerSaving;
            }

            return Mathf.Min(NativeSmoothRate, _sustainableCap);
        }

        public static void LimitTo(int frameRate)
        {
            _sustainableCap = Mathf.Max(BaselineSmooth, frameRate);
            Reapply();
        }

        public static void RemoveLimit()
        {
            _sustainableCap = int.MaxValue;
            Reapply();
        }

        private static void Reapply()
        {
            if (Application.targetFrameRate != PowerSaving)
            {
                Application.targetFrameRate = Resolve(BaselineSmooth);
            }
        }

        public static int GetMaxRefreshRate()
        {
            if (_cachedMaxRefreshRate > 0)
            {
                return _cachedMaxRefreshRate;
            }

            double best = Screen.currentResolution.refreshRateRatio.value;
            foreach (Resolution resolution in Screen.resolutions)
            {
                if (resolution.refreshRateRatio.value > best)
                {
                    best = resolution.refreshRateRatio.value;
                }
            }

            best = System.Math.Max(best, QueryAndroidSupportedModes());
            _cachedMaxRefreshRate = Mathf.RoundToInt((float)best);
            return _cachedMaxRefreshRate;
        }

        // MIUI/adaptive-refresh phones can report the idle 60 Hz mode through Screen APIs even when the panel
        // supports 120 Hz, so ask Android for every supported display mode directly.
        private static double QueryAndroidSupportedModes()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using (var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (AndroidJavaObject activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
                using (AndroidJavaObject windowManager = activity.Call<AndroidJavaObject>("getWindowManager"))
                using (AndroidJavaObject display = windowManager.Call<AndroidJavaObject>("getDefaultDisplay"))
                {
                    AndroidJavaObject[] modes = display.Call<AndroidJavaObject[]>("getSupportedModes");
                    double best = 0;
                    foreach (AndroidJavaObject mode in modes)
                    {
                        best = System.Math.Max(best, mode.Call<float>("getRefreshRate"));
                        mode.Dispose();
                    }

                    return best;
                }
            }
            catch (System.Exception exception)
            {
                Debug.LogWarning("[DisplayFrameRate] Could not query display modes: " + exception.Message);
            }
#endif
            return 0;
        }
    }
}
