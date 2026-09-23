using AlienDefense.Base;
using AlienDefense.Combat;
using AlienDefense.Core;
using AlienDefense.Economy;
using AlienDefense.Progression;
using AlienDefense.Settings;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AlienDefense.UI
{
    /// <summary>Shows/hides Pause, Victory, and Defeat panels based on GameFlow state, and routes their buttons
    /// (Resume/Restart/Main Menu/Level Selection/Next Level) and the Android back button. Never sets GameState
    /// directly except via GameFlow/GameSpeed, and never contains gameplay/scene-routing rules beyond navigation.</summary>
    public sealed class GameStateUIController : MonoBehaviour
    {
        [SerializeField]
        private GameObject _pausePanel;

        [SerializeField]
        [Tooltip("Optional.")]
        private PausePanelView _pausePanelView;

        [SerializeField]
        private GameObject _victoryPanel;

        [SerializeField]
        [Tooltip("Optional.")]
        private GameResultView _victoryResultView;

        [SerializeField]
        [Tooltip("Optional. The full victory screen (rewards, damage leaders); GameResultView stays for levels that " +
            "still use the plain result panel.")]
        private VictoryPanelView _victoryPanelView;

        [SerializeField]
        private GameObject _defeatPanel;

        [SerializeField]
        [Tooltip("Optional.")]
        private GameResultView _defeatResultView;

        [SerializeField]
        [Tooltip("Optional. Hidden while the pause panel is open, so the pause screen is not competing with the HUD.")]
        private CanvasGroup[] _hiddenWhilePaused = new CanvasGroup[0];

        [SerializeField]
        [Tooltip("Optional. Disabled (not hidden) while the game is not PreparingWave/PlayingWave, so Build/TowerDetails buttons can't be clicked mid-pause.")]
        private CanvasGroup _gameplayInteractionGroup;

        [SerializeField]
        [Tooltip("Optional.")]
        private BackNavigationController _backNavigation;

        private GameFlowController _gameFlow;
        private GameSpeedController _gameSpeed;
        private LevelRestartService _restartService;
        private ApplicationServices _applicationServices;
        private string _currentLevelId;
        private BaseHealthService _baseHealth;

        public void Initialize(
            GameFlowController gameFlow,
            GameSpeedController gameSpeed,
            LevelRestartService restartService,
            ApplicationServices applicationServices,
            string currentLevelId)
        {
            Unsubscribe();

            _gameFlow = gameFlow;
            _gameSpeed = gameSpeed;
            _restartService = restartService;
            _applicationServices = applicationServices;
            _currentLevelId = currentLevelId;

            if (_gameFlow != null)
            {
                _gameFlow.GameStateChanged += HandleGameStateChanged;
                Refresh(_gameFlow.CurrentState);
            }

            if (_pausePanelView != null)
            {
                _pausePanelView.ResumeClicked += HandleResumeClicked;
                _pausePanelView.RestartClicked += HandleRestartClicked;
                _pausePanelView.MainMenuClicked += HandleMainMenuClicked;
            }

            if (_victoryPanelView != null)
            {
                _victoryPanelView.NextClicked += HandleVictoryNextClicked;
            }

            if (_victoryResultView != null)
            {
                _victoryResultView.RestartClicked += HandleRestartClicked;
                _victoryResultView.LevelSelectionClicked += HandleLevelSelectionClicked;
                _victoryResultView.NextLevelClicked += HandleNextLevelClicked;
            }

            if (_defeatResultView != null)
            {
                _defeatResultView.RestartClicked += HandleRestartClicked;
                _defeatResultView.LevelSelectionClicked += HandleLevelSelectionClicked;
            }

            _backNavigation?.SetHandler(HandleBackPressed);
        }

        /// <summary>Gives the pause panel the level services it shows: settings toggles, picked skills, and this
        /// match's damage totals. Separate from Initialize so a bare panel (and the existing tests) still work.</summary>
        public void BindPauseServices(SettingsService settings, PlayerSkillService skills, CombatStatsService stats, BaseHealthService baseHealth)
        {
            _baseHealth = baseHealth;
            _pausePanelView?.Bind(settings, skills, stats);
        }

        /// <summary>Called by LevelCompositionRoot right after the win is banked (rewards granted, progress saved),
        /// so the panel only ever displays a finished result.</summary>
        public void ShowVictoryResult(LevelVictoryResult result)
        {
            if (_victoryPanelView == null || result == null)
            {
                return;
            }

            SetActiveIfAssigned(_victoryPanel, true);
            _victoryPanelView.Show(result);
        }

        private void HandleGameStateChanged(GameState previous, GameState current)
        {
            Refresh(current);
        }

        private void Refresh(GameState state)
        {
            bool paused = state == GameState.Paused;
            if (paused && _pausePanelView != null)
            {
                // The mockup's badge: only while the base has not been touched yet.
                bool perfect = _baseHealth == null || _baseHealth.CurrentHealth >= _baseHealth.MaxHealth;
                _pausePanelView.SetSubtitle(perfect ? "Perfect Clear" : string.Empty);
            }

            SetActiveIfAssigned(_pausePanel, paused);

            foreach (CanvasGroup group in _hiddenWhilePaused)
            {
                if (group == null)
                {
                    continue;
                }

                group.alpha = paused ? 0f : 1f;
                group.blocksRaycasts = !paused;
            }
            SetActiveIfAssigned(_victoryPanel, state == GameState.Victory);
            SetActiveIfAssigned(_defeatPanel, state == GameState.Defeat);

            if (_gameplayInteractionGroup != null)
            {
                _gameplayInteractionGroup.interactable = state == GameState.PreparingWave || state == GameState.PlayingWave;
            }
        }

        private void HandleResumeClicked()
        {
            if (_gameFlow == null || _gameFlow.CurrentState != GameState.Paused)
            {
                return;
            }

            _gameFlow.Resume();
            _gameSpeed?.Resume();
        }

        private void HandleRestartClicked()
        {
            _restartService?.Restart();
        }

        private void HandleMainMenuClicked()
        {
            LeaveLevelFor(SceneNames.MainMenu);
        }

        private void HandleLevelSelectionClicked()
        {
            LeaveLevelFor(SceneNames.LevelSelection);
        }

        /// <summary>Leaves the level straight for <paramref name="sceneName"/> through this scene's
        /// SceneTransitionService (its own loading overlay, then the menu activates) - no detour through the
        /// Bootstrap scene, which is only needed to create the application on a cold start. The application root
        /// is DontDestroyOnLoad, so the menu still receives the same services (and the queued victory rewards).
        ///
        /// When the application is not running - the gameplay scene was entered directly, which is what pressing
        /// Play on Level_01 in the editor does - there are no ApplicationServices. In the editor the missing half of
        /// the application is created on the spot (same assets Bootstrap uses) so the button still goes straight to
        /// the menu; only if even that fails does it fall back to cold-booting the Bootstrap scene.</summary>
        private void LeaveLevelFor(string sceneName)
        {
            _applicationServices?.LevelLaunchContext.Clear();

            if (_applicationServices?.SceneTransition != null)
            {
                _applicationServices.SceneTransition.TryLoadScene(sceneName, SceneTransitionStyle.QuickFade);
                return;
            }

#if UNITY_EDITOR
            if (TryCreateEditorApplicationServices(out ApplicationServices created)
                && created?.SceneTransition != null)
            {
                Debug.Log($"[GameStateUIController] The level was played directly (no Bootstrap), so the application " +
                    $"services were created here; loading '{sceneName}' straight away.", this);
                _applicationServices = created;
                created.LevelLaunchContext.Clear();
                created.SceneTransition.TryLoadScene(sceneName, SceneTransitionStyle.QuickFade);
                return;
            }
#endif

            Debug.LogWarning($"[GameStateUIController] No ApplicationServices in this session (the level was started " +
                $"directly instead of through Bootstrap); cold-booting Bootstrap to reach '{sceneName}'.", this);

            BootstrapLoadContext.RequestLoad(sceneName, initializeApplication: true);
            SceneManager.LoadScene(SceneNames.Bootstrap, LoadSceneMode.Single);
        }

#if UNITY_EDITOR
        /// <summary>Editor-only convenience for "press Play on the gameplay scene": builds the application root from
        /// the very assets the Bootstrap scene holds and binds this scene's own SceneServicesHost to it, so leaving
        /// the level behaves exactly like a normal session instead of restarting at Bootstrap. A real build always
        /// starts at Bootstrap, so this path never exists there.</summary>
        private bool TryCreateEditorApplicationServices(out ApplicationServices services)
        {
            services = null;

            var levelCatalog = UnityEditor.AssetDatabase.LoadAssetAtPath<LevelCatalog>(
                "Assets/_Game/Data/Levels/LevelCatalog.asset");
            var profileDefaults = UnityEditor.AssetDatabase.LoadAssetAtPath<AlienDefense.Save.PlayerProfileDefaults>(
                "Assets/_Game/Data/Save/PlayerProfileDefaults.asset");
            var towerCatalog = UnityEditor.AssetDatabase.LoadAssetAtPath<AlienDefense.Towers.TowerCatalog>(
                "Assets/_Game/Data/Towers/TowerCatalog.asset");
            if (levelCatalog == null || profileDefaults == null)
            {
                return false;
            }

            ApplicationCompositionRoot root = ApplicationCompositionRoot.EnsureInitialized(
                levelCatalog, profileDefaults, towerCatalog);
            if (root == null)
            {
                return false;
            }

            // The root binds a scene's SceneTransitionService when that scene loads; this one loaded before the root
            // existed, so it is bound by hand here.
            var host = FindFirstObjectByType<SceneServicesHost>(FindObjectsInactive.Include);
            host?.BindApplicationRoot(root);

            services = root.Services;
            return true;
        }
#endif

        /// <summary>The victory screen's Next returns to MainMenu, where the rewards this level granted fly into
        /// the HUD (ApplicationServices.PendingRewards). VictoryPanelView disables its own button on the first tap,
        /// so a double tap cannot start two transitions - and the transition service ignores a second request while
        /// one is already loading either way.</summary>
        private void HandleVictoryNextClicked()
        {
            HandleMainMenuClicked();
        }

        private void HandleNextLevelClicked()
        {
            if (_applicationServices?.LevelCatalog != null
                && _applicationServices.LevelCatalog.TryGetNext(_currentLevelId, out LevelCatalogEntry nextEntry))
            {
                _applicationServices.LevelLaunchContext.SetSelectedLevel(nextEntry.LevelId);
                _applicationServices.SceneTransition.TryLoadSceneViaBootstrap(nextEntry.SceneName);
                return;
            }

            // No next level in the catalog yet: fall back to Level Selection rather than a Campaign Complete
            // placeholder, keeping Phase 12 scope minimal.
            HandleLevelSelectionClicked();
        }

        private void HandleBackPressed()
        {
            if (_gameFlow == null)
            {
                return;
            }

            switch (_gameFlow.CurrentState)
            {
                case GameState.Paused:
                    HandleResumeClicked();
                    break;
                case GameState.PreparingWave:
                case GameState.PlayingWave:
                    _gameFlow.Pause();
                    _gameSpeed?.Pause();
                    break;
                case GameState.Victory:
                case GameState.Defeat:
                    HandleLevelSelectionClicked();
                    break;
            }
        }

        private static void SetActiveIfAssigned(GameObject target, bool active)
        {
            if (target != null)
            {
                target.SetActive(active);
            }
        }

        private void Unsubscribe()
        {
            if (_gameFlow != null)
            {
                _gameFlow.GameStateChanged -= HandleGameStateChanged;
            }

            if (_pausePanelView != null)
            {
                _pausePanelView.ResumeClicked -= HandleResumeClicked;
                _pausePanelView.RestartClicked -= HandleRestartClicked;
                _pausePanelView.MainMenuClicked -= HandleMainMenuClicked;
            }

            if (_victoryPanelView != null)
            {
                _victoryPanelView.NextClicked -= HandleVictoryNextClicked;
            }

            if (_victoryResultView != null)
            {
                _victoryResultView.RestartClicked -= HandleRestartClicked;
                _victoryResultView.LevelSelectionClicked -= HandleLevelSelectionClicked;
                _victoryResultView.NextLevelClicked -= HandleNextLevelClicked;
            }

            if (_defeatResultView != null)
            {
                _defeatResultView.RestartClicked -= HandleRestartClicked;
                _defeatResultView.LevelSelectionClicked -= HandleLevelSelectionClicked;
            }

            _backNavigation?.SetHandler(null);
        }

        private void OnDestroy()
        {
            Unsubscribe();
        }
    }
}
