using UnityEngine;
using UnityEngine.SceneManagement;

namespace AlienDefense.Settings
{
    /// <summary>Keeps frame pacing even on high-refresh phones: a device that cannot hold its 90/120 FPS target
    /// drops to a steady 60 (an uneven 70-110 feels worse than a locked 60), and climbs back once CPU and GPU both
    /// show enough headroom. Creates itself on mobile; no scene wiring needed.</summary>
    public sealed class AdaptiveFrameRateGovernor : MonoBehaviour
    {
        private const float WindowSeconds = 2f;
        private const float WarmupSeconds = 3f;
        private const float RequiredShareOfTarget = 0.9f;
        private const int MissedWindowsBeforeDowngrade = 2;
        private const float UpgradeBudgetShare = 0.8f;
        private const float BaseProbeSeconds = 5f;
        private const int MaxFailedUpgrades = 3;

        private readonly FrameTiming[] _timings = new FrameTiming[1];
        private float _warmupRemaining = WarmupSeconds;
        private float _windowElapsed;
        private int _windowFrames;
        private int _missedWindows;
        private float _headroomElapsed;
        private int _failedUpgrades;
        private bool _upgradedRecently;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Create()
        {
            if (!Application.isMobilePlatform)
            {
                return;
            }

            var host = new GameObject(nameof(AdaptiveFrameRateGovernor)) { hideFlags = HideFlags.HideInHierarchy };
            DontDestroyOnLoad(host);
            host.AddComponent<AdaptiveFrameRateGovernor>();
        }

        private void OnEnable()
        {
            SceneManager.sceneLoaded += HandleSceneLoaded;
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
        }

        // Scene loads stall for a moment; don't let that count as the device being too slow.
        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            _warmupRemaining = WarmupSeconds;
            ResetWindow();
            _headroomElapsed = 0f;
        }

        private void Update()
        {
            float deltaTime = Time.unscaledDeltaTime;
            if (_warmupRemaining > 0f)
            {
                _warmupRemaining -= deltaTime;
                return;
            }

            int target = Application.targetFrameRate;
            if (target > DisplayFrameRate.BaselineSmooth)
            {
                TrackHighTarget(deltaTime, target);
            }
            else if (target == DisplayFrameRate.BaselineSmooth && DisplayFrameRate.IsLimited && _failedUpgrades < MaxFailedUpgrades)
            {
                ProbeForHeadroom(deltaTime);
            }
        }

        private void TrackHighTarget(float deltaTime, int target)
        {
            _windowElapsed += deltaTime;
            _windowFrames++;
            if (_windowElapsed < WindowSeconds)
            {
                return;
            }

            float achieved = _windowFrames / _windowElapsed;
            _missedWindows = achieved < target * RequiredShareOfTarget ? _missedWindows + 1 : 0;
            ResetWindow();

            if (_missedWindows < MissedWindowsBeforeDowngrade)
            {
                return;
            }

            if (_upgradedRecently)
            {
                _failedUpgrades++;
            }

            _upgradedRecently = false;
            _missedWindows = 0;
            _headroomElapsed = 0f;
            DisplayFrameRate.LimitTo(DisplayFrameRate.BaselineSmooth);
        }

        private void ProbeForHeadroom(float deltaTime)
        {
            FrameTimingManager.CaptureFrameTimings();
            if (FrameTimingManager.GetLatestTimings(1, _timings) == 0 || _timings[0].gpuFrameTime <= 0)
            {
                return;
            }

            float budgetMs = 1000f / DisplayFrameRate.NativeSmoothRate * UpgradeBudgetShare;
            bool fits = _timings[0].cpuMainThreadFrameTime < budgetMs
                && _timings[0].cpuRenderThreadFrameTime < budgetMs
                && _timings[0].gpuFrameTime < budgetMs;
            _headroomElapsed = fits ? _headroomElapsed + deltaTime : 0f;

            // Each failed attempt doubles the proof required before trying the high rate again.
            if (_headroomElapsed >= BaseProbeSeconds * (1 << _failedUpgrades))
            {
                _headroomElapsed = 0f;
                _upgradedRecently = true;
                ResetWindow();
                DisplayFrameRate.RemoveLimit();
            }
        }

        private void ResetWindow()
        {
            _windowElapsed = 0f;
            _windowFrames = 0;
        }
    }
}
