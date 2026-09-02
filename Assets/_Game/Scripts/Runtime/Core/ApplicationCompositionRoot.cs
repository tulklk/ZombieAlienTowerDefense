using AlienDefense.DebugTools;
using AlienDefense.Save;
using AlienDefense.Settings;
using AlienDefense.Towers;
using AlienDefense.UI;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AlienDefense.Core
{
    /// <summary>Application-scope composition root. Exactly one instance ever exists — created on cold boot by
    /// BootstrapLoadingController and kept alive via DontDestroyOnLoad for the rest of the process, surviving
    /// every later Bootstrap reload (SceneTransitionService.TryLoadSceneViaBootstrap routes menu-to-menu
    /// transitions back through Bootstrap for its loading screen; EnsureInitialized reuses the live instance
    /// instead of creating a second one). Owns every application-scope service and pushes them into each
    /// newly-loaded scene's IApplicationServicesReceiver.
    ///
    /// Replaces the previous static ApplicationRuntime holder: same responsibilities, but as a real instance so
    /// there is no static mutable business state or static service-locator API anywhere in the app-lifecycle
    /// path. The one static field below is a duplicate-detection pointer only — the standard Unity
    /// DontDestroyOnLoad singleton idiom — never a public static accessor for services or business logic; every
    /// consumer still only ever receives services explicitly, either as the return value here or pushed through
    /// IApplicationServicesReceiver.</summary>
    public sealed class ApplicationCompositionRoot : MonoBehaviour
    {
        private static ApplicationCompositionRoot _instance;

        private LevelLaunchContext _levelLaunchContext;
        private PlayerProfileService _playerProfileService;
        private SettingsService _settingsService;
        private SaveFileRepository _saveRepository;
        private LevelCatalog _levelCatalog;
        private TowerCatalog _towerCatalog;
        private SceneTransitionService _sceneTransition;

        public ApplicationServices Services { get; private set; }

        /// <summary>Creates and initializes the one application root if it doesn't exist yet. Safe to call on
        /// every Bootstrap pass — BootstrapLoadContext.ShouldInitializeApplication already gates this to cold
        /// boot only, and this stays idempotent regardless so a live instance is always reused as-is.</summary>
        public static ApplicationCompositionRoot EnsureInitialized(LevelCatalog levelCatalog, PlayerProfileDefaults playerProfileDefaults, TowerCatalog towerCatalog = null)
        {
            if (_instance != null)
            {
                return _instance;
            }

            var rootObject = new GameObject(nameof(ApplicationCompositionRoot));
            var root = rootObject.AddComponent<ApplicationCompositionRoot>();
            root.Initialize(levelCatalog, playerProfileDefaults, towerCatalog);
            return root;
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Debug.LogWarning("[ApplicationCompositionRoot] Duplicate instance detected; destroying the new one.", this);
                Destroy(gameObject);
                return;
            }

            _instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void Initialize(LevelCatalog levelCatalog, PlayerProfileDefaults playerProfileDefaults, TowerCatalog towerCatalog)
        {
            Time.timeScale = 1f;

            _levelCatalog = levelCatalog;
            _towerCatalog = towerCatalog;
            _levelLaunchContext = new LevelLaunchContext();

            _saveRepository = new SaveFileRepository();
            var saveService = new SaveService(_saveRepository);
            string firstLevelId = levelCatalog != null && levelCatalog.Count > 0 ? levelCatalog.GetEntry(0).LevelId : null;
            PlayerProfileSaveData profileData = saveService.LoadOrCreateDefault(playerProfileDefaults, firstLevelId);
            _playerProfileService = new PlayerProfileService(saveService, profileData);
            _settingsService = new SettingsService(_playerProfileService);

            Application.focusChanged += HandleFocusChanged;
            Application.quitting += HandleQuitting;
            SceneManager.sceneLoaded += HandleSceneLoaded;

            RebuildServicesBundle();

            Debug.Log("[ApplicationCompositionRoot] Initialized application services.", this);
        }

        private void Update()
        {
            _playerProfileService?.Tick(Time.unscaledDeltaTime);
        }

        /// <summary>Called by SceneServicesHost.BindApplicationRoot right after each scene loads — rebinds
        /// Services to that scene's own SceneTransitionService instance (SceneTransitionService is scene-scoped,
        /// rebuilt per scene, not itself DontDestroyOnLoad).</summary>
        public void BindSceneTransition(SceneTransitionService sceneTransition)
        {
            _sceneTransition = sceneTransition;
            RebuildServicesBundle();
        }

        public void InitializeSaveDebugControls(SaveDebugControls saveDebugControls)
        {
            if (saveDebugControls == null)
            {
                return;
            }

            saveDebugControls.Initialize(_playerProfileService, _saveRepository);
        }

        public void FlushPendingSave()
        {
            _playerProfileService?.FlushPendingSave();
        }

        private void RebuildServicesBundle()
        {
            Services = new ApplicationServices(_sceneTransition, _levelLaunchContext, _levelCatalog, _playerProfileService, _settingsService, _towerCatalog);
        }

        private void HandleFocusChanged(bool hasFocus)
        {
            if (!hasFocus)
            {
                FlushPendingSave();
            }
        }

        private void HandleQuitting()
        {
            FlushPendingSave();
        }

        /// <summary>Pushes this app root down into a freshly loaded scene: finds SceneServicesHost and
        /// IApplicationServicesReceiver within that scene's own object graph only (GetComponentInChildren scoped
        /// to scene.GetRootGameObjects(), never a global Find/FindObjectOfType) and hands each what it needs.</summary>
        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name == SceneNames.Bootstrap)
            {
                return;
            }

            LoadingOverlayCleanup.DestroyAllRuntimeInstances();

            GameObject[] rootObjects = scene.GetRootGameObjects();

            SceneServicesHost host = null;
            IApplicationServicesReceiver receiver = null;

            for (int i = 0; i < rootObjects.Length && (host == null || receiver == null); i++)
            {
                if (host == null)
                {
                    host = rootObjects[i].GetComponentInChildren<SceneServicesHost>(true);
                }

                if (receiver == null)
                {
                    receiver = rootObjects[i].GetComponentInChildren<IApplicationServicesReceiver>(true);
                }
            }

            host?.BindApplicationRoot(this);
            receiver?.ReceiveApplicationServices(Services);
        }

        private void OnDestroy()
        {
            if (_instance != this)
            {
                return;
            }

            SceneManager.sceneLoaded -= HandleSceneLoaded;
            Application.focusChanged -= HandleFocusChanged;
            Application.quitting -= HandleQuitting;
            _playerProfileService?.FlushPendingSave();
            _instance = null;
        }
    }
}
