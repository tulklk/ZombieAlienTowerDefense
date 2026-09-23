using System;
using System.Collections.Generic;
using System.Collections;
using AlienDefense.UI;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AlienDefense.Core
{
    /// <summary>Loads content scenes in Single mode after Bootstrap boot. Spawns a loading overlay prefab for each
    /// transition and destroys it before the target scene activates.</summary>
    public sealed class SceneTransitionService : MonoBehaviour
    {
        /// <summary>Canvas.sortingOrder maximum, so the fade cover draws over every UI in the scene being left.</summary>
        private const int FadeCoverSortOrder = 32767;

        [SerializeField, Min(0f)]
        [Tooltip("The loading screen stays up at least this long even if the actual scene load finishes sooner " +
            "(e.g. MainMenu loads almost instantly) — so branding/art actually gets seen. 0 = no artificial floor, " +
            "report real AsyncOperation progress only.")]
        private float _minimumLoadDurationSeconds = 6f;

        [SerializeField]
        [Tooltip("Spawned at transition start and destroyed when the transition ends. Assign LoadingOverlay prefab.")]
        private LoadingOverlayView _loadingOverlayPrefab;

        [Header("Quick fade (SceneTransitionStyle.QuickFade)")]
        [SerializeField, Min(0f)]
        [Tooltip("How long the screen fades to black before the target scene is activated.")]
        private float _quickFadeOutSeconds = 0.18f;

        [SerializeField, Min(0f)]
        [Tooltip("How long the black cover fades away once the target scene has painted its first frame.")]
        private float _quickFadeInSeconds = 0.22f;

        private LoadingOverlayView _activeOverlay;
        private readonly List<Canvas> _hiddenSceneCanvases = new List<Canvas>();
        private Camera _activeOverlayCamera;
        private CanvasGroup _fadeCover;

        public SceneTransitionState State { get; private set; } = SceneTransitionState.Idle;
        public bool IsTransitioning => State == SceneTransitionState.Loading;

#if UNITY_INCLUDE_TESTS
        public bool HasActiveLoadingOverlay => _activeOverlay != null;
#endif

        public event Action<string> SceneLoadStarted;
        public event Action<float> SceneLoadProgress;
        public event Action<string> SceneLoadCompleted;
        public event Action<string, string> SceneLoadFailed;

        private void OnDestroy()
        {
            DestroyOverlay();
        }

        /// <summary>Requests a scene load. Returns false without starting anything if a transition is already running.</summary>
        public bool TryLoadScene(string sceneName)
        {
            return TryLoadScene(sceneName, SceneTransitionStyle.BrandedSplash);
        }

        /// <summary>Requests a scene load with an explicit cover style. QuickFade is for returns to a menu, where the
        /// branded splash would read as a detour through the Bootstrap scene.</summary>
        public bool TryLoadScene(string sceneName, SceneTransitionStyle style)
        {
            if (string.IsNullOrWhiteSpace(sceneName))
            {
                Debug.LogError("[SceneTransitionService] Cannot load a scene with an empty name.", this);
                return false;
            }

            if (IsInfrastructureScene(sceneName))
            {
                Debug.LogError($"[SceneTransitionService] '{sceneName}' is an infrastructure scene and cannot be loaded as content.", this);
                return false;
            }

            if (IsTransitioning)
            {
                Debug.LogWarning($"[SceneTransitionService] Ignored LoadScene('{sceneName}') while a transition is already running.", this);
                return false;
            }

            StartCoroutine(LoadSceneRoutine(sceneName, style));
            return true;
        }

        /// <summary>Loads Bootstrap in Single mode so the previous scene unloads immediately, then Bootstrap drives
        /// loading into the target content scene.</summary>
        public bool TryLoadSceneViaBootstrap(string targetSceneName)
        {
            if (string.IsNullOrWhiteSpace(targetSceneName))
            {
                Debug.LogError("[SceneTransitionService] Cannot load Bootstrap with an empty target scene name.", this);
                return false;
            }

            if (IsInfrastructureScene(targetSceneName))
            {
                Debug.LogError($"[SceneTransitionService] '{targetSceneName}' is an infrastructure scene and cannot be loaded as content.", this);
                return false;
            }

            if (IsTransitioning)
            {
                Debug.LogWarning($"[SceneTransitionService] Ignored LoadSceneViaBootstrap('{targetSceneName}') while a transition is already running.", this);
                return false;
            }

            BootstrapLoadContext.RequestLoad(targetSceneName, initializeApplication: false);
            SceneManager.LoadScene(SceneNames.Bootstrap, LoadSceneMode.Single);
            return true;
        }

        private IEnumerator LoadSceneRoutine(string sceneName, SceneTransitionStyle style)
        {
            State = SceneTransitionState.Loading;
            Time.timeScale = 1f;

            if (style == SceneTransitionStyle.QuickFade)
            {
                SpawnFadeCover();

                // Fade to black BEFORE hiding the level's UI, otherwise the HUD and the victory panel would pop out
                // a beat before the screen is covered.
                yield return FadeCover(0f, 1f, _quickFadeOutSeconds);
                HideLeavingSceneUi();
            }
            else
            {
                SpawnOverlay();
            }

            try
            {
                SceneLoadStarted?.Invoke(sceneName);

                AsyncOperation operation;
                try
                {
                    operation = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
                    if (operation != null)
                    {
                        operation.allowSceneActivation = false;
                    }
                }
                catch (Exception exception)
                {
                    operation = null;
                    Debug.LogError($"[SceneTransitionService] LoadSceneAsync('{sceneName}') threw: {exception.Message}", this);
                }

                if (operation == null)
                {
                    RestoreLeavingSceneUi();
                    State = SceneTransitionState.Failed;
                    SceneLoadFailed?.Invoke(sceneName, "Scene could not be loaded. Is it added to Build Settings?");
                    yield break;
                }

                // QuickFade never holds the screen artificially: the menu is ready in a frame or two and the point of
                // this style is that the player goes straight there.
                float minimumDuration = style == SceneTransitionStyle.QuickFade ? 0f : _minimumLoadDurationSeconds;

                float elapsedSeconds = 0f;
                while (elapsedSeconds < minimumDuration || operation.progress < 0.9f)
                {
                    elapsedSeconds += Time.unscaledDeltaTime;

                    float timeProgress = minimumDuration > 0f
                        ? Mathf.Clamp01(elapsedSeconds / minimumDuration)
                        : 1f;
                    float realProgress = Mathf.Clamp01(operation.progress / 0.9f);

                    ReportProgress(Mathf.Min(timeProgress, realProgress));
                    yield return null;
                }

                ReportProgress(1f);

                operation.allowSceneActivation = true;
                while (!operation.isDone)
                {
                    yield return null;
                }

                // Give the new scene's own Awake/Start/OnEnable - and its first rendered frame - a beat before
                // tearing the overlay down. Destroying it earlier (the old order) left a gap between the overlay
                // disappearing and the new scene actually painting anything: the overlay lives under this
                // service's own (persistent) transform, so nothing removes it automatically on scene unload,
                // and its camera keeps clearing to a solid color with no UI on it once the Canvas is gone -
                // that solid color is the "black flash" players see between scenes.
                yield return null;

                if (style == SceneTransitionStyle.QuickFade)
                {
                    yield return FadeCover(1f, 0f, _quickFadeInSeconds);
                }

                DestroyOverlay();

                Debug.Log($"[SceneTransitionService] '{sceneName}' activated. Active scene = {SceneManager.GetActiveScene().name}.", this);

                State = SceneTransitionState.Idle;

                try
                {
                    SceneLoadCompleted?.Invoke(sceneName);
                }
                catch (Exception exception)
                {
                    Debug.LogError($"[SceneTransitionService] A SceneLoadCompleted subscriber threw: {exception}", this);
                }

                Debug.Log($"[SceneTransitionService] LoadSceneRoutine('{sceneName}') finished.", this);
            }
            finally
            {
                DestroyOverlay();
            }
        }

        private void SpawnOverlay()
        {
            DestroyOverlay();

            if (_loadingOverlayPrefab == null)
            {
                return;
            }

            _activeOverlay = Instantiate(_loadingOverlayPrefab, transform);
            _activeOverlayCamera = LoadingOverlayCameraUtility.CreateOverlayCamera(
                transform,
                LoadingOverlayCameraUtility.TransitionCameraName);
            _activeOverlay.BindCamera(_activeOverlayCamera);
            _activeOverlay.Show();
            _activeOverlay.InitializeForTransition();
            HideLeavingSceneUi();
        }

        /// <summary>Builds the plain black cover used by QuickFade. Deliberately not the LoadingOverlay prefab: that
        /// prefab is the boot splash, and players read it as "the game went back through Bootstrap". Screen Space -
        /// Overlay needs no camera, so this also survives the frame where the old scene's camera is already gone and
        /// the new one has not woken up yet.</summary>
        private void SpawnFadeCover()
        {
            DestroyFadeCover();

            var coverObject = new GameObject("SceneFadeCover", typeof(Canvas), typeof(CanvasGroup));
            coverObject.transform.SetParent(transform, false);

            var canvas = coverObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = FadeCoverSortOrder;

            _fadeCover = coverObject.GetComponent<CanvasGroup>();
            _fadeCover.alpha = 0f;
            _fadeCover.interactable = false;
            _fadeCover.blocksRaycasts = true;

            var imageObject = new GameObject("Cover", typeof(UnityEngine.UI.Image));
            imageObject.transform.SetParent(coverObject.transform, false);

            var image = imageObject.GetComponent<UnityEngine.UI.Image>();
            image.color = Color.black;
            image.raycastTarget = true;

            var rect = image.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private IEnumerator FadeCover(float from, float to, float durationSeconds)
        {
            if (_fadeCover == null)
            {
                yield break;
            }

            if (durationSeconds <= 0f)
            {
                _fadeCover.alpha = to;
                yield break;
            }

            float elapsedSeconds = 0f;
            while (elapsedSeconds < durationSeconds)
            {
                elapsedSeconds += Time.unscaledDeltaTime;

                if (_fadeCover == null)
                {
                    yield break;
                }

                _fadeCover.alpha = Mathf.Lerp(from, to, Mathf.Clamp01(elapsedSeconds / durationSeconds));
                yield return null;
            }

            if (_fadeCover != null)
            {
                _fadeCover.alpha = to;
            }
        }

        private void DestroyFadeCover()
        {
            if (_fadeCover != null)
            {
                UnityEngine.Object.DestroyImmediate(_fadeCover.gameObject);
                _fadeCover = null;
            }

            Transform existing = transform.Find("SceneFadeCover");
            if (existing != null)
            {
                UnityEngine.Object.DestroyImmediate(existing.gameObject);
            }
        }

        /// <summary>The loading screen draws through its own camera (Screen Space - Camera), and a Screen Space -
        /// Overlay canvas always composites after every camera - so the level's HUD and its victory panel would sit
        /// on top of the loading screen for the whole transition. Every canvas of the scene being left is switched
        /// off for that reason; they are restored only if the load never happens, since the scene is unloaded
        /// otherwise. The overlay's own canvas (a child of this service) is never touched.</summary>
        private void HideLeavingSceneUi()
        {
            foreach (Canvas canvas in FindObjectsByType<Canvas>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (canvas == null || !canvas.enabled || canvas.transform.IsChildOf(transform))
                {
                    continue;
                }

                canvas.enabled = false;
                _hiddenSceneCanvases.Add(canvas);
            }
        }

        /// <summary>Only used when a transition ends without the scene actually changing (a failed load).</summary>
        private void RestoreLeavingSceneUi()
        {
            for (int i = 0; i < _hiddenSceneCanvases.Count; i++)
            {
                if (_hiddenSceneCanvases[i] != null)
                {
                    _hiddenSceneCanvases[i].enabled = true;
                }
            }

            _hiddenSceneCanvases.Clear();
        }

        private void DestroyOverlay()
        {
            bool destroyedAny = false;

            DestroyFadeCover();

            if (_activeOverlay != null)
            {
                LoadingOverlayCleanup.DestroyInstance(_activeOverlay);
                _activeOverlay = null;
                destroyedAny = true;
            }

            DestroyOverlayCameraChild();
            if (_activeOverlayCamera != null)
            {
                UnityEngine.Object.DestroyImmediate(_activeOverlayCamera.gameObject);
                _activeOverlayCamera = null;
                destroyedAny = true;
            }

            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                Transform child = transform.GetChild(i);
                LoadingOverlayView childOverlay = child.GetComponent<LoadingOverlayView>();
                if (childOverlay != null)
                {
                    LoadingOverlayCleanup.DestroyInstance(childOverlay);
                    destroyedAny = true;
                }
            }

            DestroyOverlayCameraChild();

            if (!Application.isPlaying)
            {
                return;
            }

            int overlayCountBefore = UnityEngine.Object.FindObjectsByType<LoadingOverlayView>(FindObjectsSortMode.None).Length;
            LoadingOverlayCleanup.DestroyAllRuntimeInstances(_loadingOverlayPrefab);
            int overlayCountAfter = UnityEngine.Object.FindObjectsByType<LoadingOverlayView>(FindObjectsSortMode.None).Length;
            if (overlayCountAfter < overlayCountBefore)
            {
                destroyedAny = true;
            }

            if (destroyedAny)
            {
                Debug.Log("[SceneTransitionService] Destroyed loading overlay.", this);
            }
        }

        private void DestroyOverlayCameraChild()
        {
            Transform cameraChild = transform.Find(LoadingOverlayCameraUtility.TransitionCameraName);
            if (cameraChild != null)
            {
                UnityEngine.Object.DestroyImmediate(cameraChild.gameObject);
            }
        }

        private void ReportProgress(float progress01)
        {
            _activeOverlay?.SetProgress(progress01);
            SceneLoadProgress?.Invoke(progress01);
        }

        private static bool IsInfrastructureScene(string sceneName)
        {
            return sceneName == SceneNames.Bootstrap;
        }
    }
}
