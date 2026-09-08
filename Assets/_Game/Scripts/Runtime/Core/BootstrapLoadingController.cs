using System.Collections;
using AlienDefense.Save;
using AlienDefense.Towers;
using AlienDefense.UI;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AlienDefense.Core
{
    /// <summary>Bootstrap entry point. Drives the embedded LoadingOverlay progress bar, optionally initializes
    /// ApplicationCompositionRoot on cold boot, then loads the target scene from BootstrapLoadContext.</summary>
    [DisallowMultipleComponent]
    public sealed class BootstrapLoadingController : MonoBehaviour
    {
        private const string LevelCatalogAssetPath = "Assets/_Game/Data/Levels/LevelCatalog.asset";
        private const string PlayerProfileDefaultsAssetPath = "Assets/_Game/Data/Save/PlayerProfileDefaults.asset";
        private const string TowerCatalogAssetPath = "Assets/_Game/Data/Towers/TowerCatalog.asset";

        [SerializeField, Min(0f)]
        private float _minimumLoadDurationSeconds = 6f;

        [SerializeField]
        private LoadingOverlayView _loadingOverlay;

        [SerializeField]
        private LevelCatalog _levelCatalog;

        [SerializeField]
        private PlayerProfileDefaults _playerProfileDefaults;

        [SerializeField]
        [Tooltip("Optional. Needed only for screens that enumerate every tower (e.g. the Upgrade screen).")]
        private TowerCatalog _towerCatalog;

        [SerializeField]
        private Camera _bootstrapUICamera;

        private void Awake()
        {
            if (_loadingOverlay == null)
            {
                _loadingOverlay = FindFirstObjectByType<LoadingOverlayView>(FindObjectsInactive.Include);
            }

            if (_bootstrapUICamera == null)
            {
                _bootstrapUICamera = ResolveBootstrapUICamera();
            }

            ResolveBootstrapAssetsIfNeeded();
        }

        private static Camera ResolveBootstrapUICamera()
        {
            GameObject cameraObject = GameObject.Find(LoadingOverlayCameraUtility.BootstrapUICameraName);
            if (cameraObject != null)
            {
                Camera existingCamera = cameraObject.GetComponent<Camera>();
                if (existingCamera != null)
                {
                    return existingCamera;
                }
            }

            return LoadingOverlayCameraUtility.CreateOverlayCamera(null, LoadingOverlayCameraUtility.BootstrapUICameraName);
        }

        private IEnumerator Start()
        {
            if (_loadingOverlay == null)
            {
                Debug.LogError("[BootstrapLoadingController] No LoadingOverlayView assigned.", this);
                yield break;
            }

            if (SceneManager.GetActiveScene().name != SceneNames.Bootstrap)
            {
                Debug.LogWarning("[BootstrapLoadingController] Ignored outside Bootstrap scene.", this);
                yield break;
            }

            Time.timeScale = 1f;
            if (_bootstrapUICamera == null)
            {
                _bootstrapUICamera = ResolveBootstrapUICamera();
            }

            _loadingOverlay.BindCamera(_bootstrapUICamera);
            _loadingOverlay.Show();
            _loadingOverlay.InitializeForTransition();

            string targetSceneName = BootstrapLoadContext.TargetSceneName;
            if (BootstrapLoadContext.ShouldInitializeApplication)
            {
                ApplicationCompositionRoot.EnsureInitialized(_levelCatalog, _playerProfileDefaults, _towerCatalog);
            }

            AsyncOperation operation;
            try
            {
                operation = SceneManager.LoadSceneAsync(targetSceneName, LoadSceneMode.Single);
                if (operation != null)
                {
                    operation.allowSceneActivation = false;
                }
            }
            catch (System.Exception exception)
            {
                Debug.LogError($"[BootstrapLoadingController] LoadSceneAsync('{targetSceneName}') threw: {exception.Message}", this);
                yield break;
            }

            if (operation == null)
            {
                Debug.LogError($"[BootstrapLoadingController] Failed to start loading '{targetSceneName}'. Is it in Build Settings?", this);
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

                _loadingOverlay.SetProgress(Mathf.Min(timeProgress, realProgress));
                yield return null;
            }

            _loadingOverlay.SetProgress(1f);
            operation.allowSceneActivation = true;

            while (!operation.isDone)
            {
                yield return null;
            }

            // Give the new scene's own Awake/Start/OnEnable - and its first rendered frame - a beat before
            // tearing the overlay down. Destroying it earlier (the old order) left a gap between the overlay
            // disappearing and the new scene actually painting anything, which read as a black flash: the
            // overlay's own camera clears to a solid color and stays alive/rendering until Bootstrap's objects
            // are actually unloaded, so removing only the overlay's Canvas first just uncovers that solid
            // background instead of the new scene.
            yield return null;

            // Bootstrap's own objects (this controller included) can already be mid-unload by now since the new
            // scene just activated under LoadSceneMode.Single - guard against the overlay having gone with it.
            if (_loadingOverlay != null)
            {
                _loadingOverlay.Dismiss();
                LoadingOverlayCleanup.DestroyInstance(_loadingOverlay);
                _loadingOverlay = null;
            }

            LoadingOverlayCleanup.DestroyAllRuntimeInstances();

            Debug.Log($"[BootstrapLoadingController] '{targetSceneName}' loaded; Bootstrap scene will unload.", this);
            BootstrapLoadContext.ResetToColdBootDefaults();
        }

        private void ResolveBootstrapAssetsIfNeeded()
        {
#if UNITY_EDITOR
            if (_levelCatalog == null)
            {
                _levelCatalog = UnityEditor.AssetDatabase.LoadAssetAtPath<LevelCatalog>(LevelCatalogAssetPath);
            }

            if (_playerProfileDefaults == null)
            {
                _playerProfileDefaults = UnityEditor.AssetDatabase.LoadAssetAtPath<PlayerProfileDefaults>(PlayerProfileDefaultsAssetPath);
            }

            if (_towerCatalog == null)
            {
                _towerCatalog = UnityEditor.AssetDatabase.LoadAssetAtPath<TowerCatalog>(TowerCatalogAssetPath);
            }
#endif

            if (_levelCatalog == null)
            {
                Debug.LogWarning("[BootstrapLoadingController] LevelCatalog is not assigned. Run AlienDefense/Setup/12. Build Bootstrap Scene.", this);
            }

            if (_playerProfileDefaults == null)
            {
                Debug.LogWarning("[BootstrapLoadingController] PlayerProfileDefaults is not assigned. Run AlienDefense/Setup/12. Build Bootstrap Scene.", this);
            }
        }
    }
}
