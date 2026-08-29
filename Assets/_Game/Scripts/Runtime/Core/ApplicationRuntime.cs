using AlienDefense.DebugTools;
using AlienDefense.Save;
using AlienDefense.Settings;
using AlienDefense.UI;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AlienDefense.Core
{
    /// <summary>Static holder for application-scope services. Initialized during Bootstrap loading before the
    /// first content scene loads; SceneTransitionService is rebound per scene via SceneServicesHost.</summary>
    public static class ApplicationRuntime
    {
        private static bool _isInitialized;
        private static LevelLaunchContext _levelLaunchContext;
        private static PlayerProfileService _playerProfileService;
        private static SettingsService _settingsService;
        private static SaveFileRepository _saveRepository;
        private static LevelCatalog _levelCatalog;
        private static SceneTransitionService _sceneTransition;
        private static bool _sceneLoadedHookRegistered;

        public static bool IsInitialized => _isInitialized;

        public static ApplicationServices Services { get; private set; }

        public static void Initialize(LevelCatalog levelCatalog, PlayerProfileDefaults playerProfileDefaults)
        {
            if (_isInitialized)
            {
                return;
            }

            _levelCatalog = levelCatalog;
            _levelLaunchContext = new LevelLaunchContext();

            _saveRepository = new SaveFileRepository();
            var saveService = new SaveService(_saveRepository);
            string firstLevelId = levelCatalog != null && levelCatalog.Count > 0 ? levelCatalog.GetEntry(0).LevelId : null;
            PlayerProfileSaveData profileData = saveService.LoadOrCreateDefault(playerProfileDefaults, firstLevelId);
            _playerProfileService = new PlayerProfileService(saveService, profileData);
            _settingsService = new SettingsService(_playerProfileService);

            UnityEngine.Application.focusChanged += HandleFocusChanged;
            UnityEngine.Application.quitting += HandleQuitting;
            EnsureSceneLoadedCleanupHook();

            _isInitialized = true;
            RebuildServicesBundle();

            Debug.Log("[ApplicationRuntime] Initialized application services.");
        }

        public static void BindSceneTransition(SceneTransitionService sceneTransition)
        {
            _sceneTransition = sceneTransition;
            RebuildServicesBundle();
        }

        public static void InitializeSaveDebugControls(SaveDebugControls saveDebugControls)
        {
            if (saveDebugControls == null || !_isInitialized)
            {
                return;
            }

            saveDebugControls.Initialize(_playerProfileService, _saveRepository);
        }

        public static void Tick(float unscaledDeltaTime)
        {
            _playerProfileService?.Tick(unscaledDeltaTime);
        }

        public static void InjectScene(Scene scene)
        {
            if (!_isInitialized || Services == null || scene.name == SceneNames.Bootstrap)
            {
                return;
            }

            GameObject[] rootObjects = scene.GetRootGameObjects();
            for (int i = 0; i < rootObjects.Length; i++)
            {
                var receiver = rootObjects[i].GetComponentInChildren<IApplicationServicesReceiver>(true);
                if (receiver != null)
                {
                    receiver.ReceiveApplicationServices(Services);
                    return;
                }
            }
        }

        public static void FlushPendingSave()
        {
            _playerProfileService?.FlushPendingSave();
        }

        private static void RebuildServicesBundle()
        {
            if (!_isInitialized)
            {
                return;
            }

            Services = new ApplicationServices(
                _sceneTransition,
                _levelLaunchContext,
                _levelCatalog,
                _playerProfileService,
                _settingsService);
        }

        private static void HandleFocusChanged(bool hasFocus)
        {
            if (!hasFocus)
            {
                FlushPendingSave();
            }
        }

        private static void HandleQuitting()
        {
            if (_sceneLoadedHookRegistered)
            {
                SceneManager.sceneLoaded -= HandleSceneLoaded;
                _sceneLoadedHookRegistered = false;
            }

            FlushPendingSave();
        }

        private static void EnsureSceneLoadedCleanupHook()
        {
            if (_sceneLoadedHookRegistered)
            {
                return;
            }

            SceneManager.sceneLoaded += HandleSceneLoaded;
            _sceneLoadedHookRegistered = true;
        }

        private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name == SceneNames.Bootstrap)
            {
                return;
            }

            LoadingOverlayCleanup.DestroyAllRuntimeInstances();
        }
    }
}
