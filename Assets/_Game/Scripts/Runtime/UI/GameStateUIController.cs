using AlienDefense.Base;
using AlienDefense.Combat;
using AlienDefense.Core;
using AlienDefense.Economy;
using AlienDefense.Progression;
using AlienDefense.Settings;
using UnityEngine;

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
                _victoryPanelView.NextClicked += HandleNextLevelClicked;
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
            _applicationServices?.LevelLaunchContext.Clear();
            _applicationServices?.SceneTransition.TryLoadSceneViaBootstrap(SceneNames.MainMenu);
        }

        private void HandleLevelSelectionClicked()
        {
            _applicationServices?.LevelLaunchContext.Clear();
            _applicationServices?.SceneTransition.TryLoadSceneViaBootstrap(SceneNames.LevelSelection);
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
                _victoryPanelView.NextClicked -= HandleNextLevelClicked;
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
