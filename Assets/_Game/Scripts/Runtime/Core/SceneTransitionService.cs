using System;
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
        [SerializeField, Min(0f)]
        [Tooltip("The loading screen stays up at least this long even if the actual scene load finishes sooner " +
            "(e.g. MainMenu loads almost instantly) — so branding/art actually gets seen. 0 = no artificial floor, " +
            "report real AsyncOperation progress only.")]
        private float _minimumLoadDurationSeconds = 6f;

        [SerializeField]
        [Tooltip("Spawned at transition start and destroyed when the transition ends. Assign LoadingOverlay prefab.")]
        private LoadingOverlayView _loadingOverlayPrefab;

        private LoadingOverlayView _activeOverlay;
        private Camera _activeOverlayCamera;

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

            StartCoroutine(LoadSceneRoutine(sceneName));
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

        private IEnumerator LoadSceneRoutine(string sceneName)
        {
            State = SceneTransitionState.Loading;
            Time.timeScale = 1f;
            SpawnOverlay();

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
                    State = SceneTransitionState.Failed;
                    SceneLoadFailed?.Invoke(sceneName, "Scene could not be loaded. Is it added to Build Settings?");
                    yield break;
                }

                float elapsedSeconds = 0f;
                while (elapsedSeconds < _minimumLoadDurationSeconds || operation.progress < 0.9f)
                {
                    elapsedSeconds += Time.unscaledDeltaTime;

                    float timeProgress = _minimumLoadDurationSeconds > 0f
                        ? Mathf.Clamp01(elapsedSeconds / _minimumLoadDurationSeconds)
                        : 1f;
                    float realProgress = Mathf.Clamp01(operation.progress / 0.9f);

                    ReportProgress(Mathf.Min(timeProgress, realProgress));
                    yield return null;
                }

                ReportProgress(1f);
                DestroyOverlay();

                yield return null;

                operation.allowSceneActivation = true;
                while (!operation.isDone)
                {
                    yield return null;
                }

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
        }

        private void DestroyOverlay()
        {
            bool destroyedAny = false;

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
