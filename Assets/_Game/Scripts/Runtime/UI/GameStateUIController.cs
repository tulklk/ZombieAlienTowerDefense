using AlienDefense.Core;
using UnityEngine;

namespace AlienDefense.UI
{
    /// <summary>Shows/hides Pause, Victory, and Defeat panels based on GameFlow state, and routes their
    /// Resume/Restart button clicks to GameFlow/GameSpeed/LevelRestartService. Never sets GameState directly.</summary>
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
        private GameObject _defeatPanel;

        [SerializeField]
        [Tooltip("Optional.")]
        private GameResultView _defeatResultView;

        [SerializeField]
        [Tooltip("Optional. Disabled (not hidden) while the game is not PreparingWave/PlayingWave, so Build/TowerDetails buttons can't be clicked mid-pause.")]
        private CanvasGroup _gameplayInteractionGroup;

        private GameFlowController _gameFlow;
        private GameSpeedController _gameSpeed;
        private LevelRestartService _restartService;

        public void Initialize(GameFlowController gameFlow, GameSpeedController gameSpeed, LevelRestartService restartService)
        {
            Unsubscribe();

            _gameFlow = gameFlow;
            _gameSpeed = gameSpeed;
            _restartService = restartService;

            if (_gameFlow != null)
            {
                _gameFlow.GameStateChanged += HandleGameStateChanged;
                Refresh(_gameFlow.CurrentState);
            }

            if (_pausePanelView != null)
            {
                _pausePanelView.ResumeClicked += HandleResumeClicked;
                _pausePanelView.RestartClicked += HandleRestartClicked;
            }

            if (_victoryResultView != null)
            {
                _victoryResultView.RestartClicked += HandleRestartClicked;
            }

            if (_defeatResultView != null)
            {
                _defeatResultView.RestartClicked += HandleRestartClicked;
            }
        }

        private void HandleGameStateChanged(GameState previous, GameState current)
        {
            Refresh(current);
        }

        private void Refresh(GameState state)
        {
            SetActiveIfAssigned(_pausePanel, state == GameState.Paused);
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
            }

            if (_victoryResultView != null)
            {
                _victoryResultView.RestartClicked -= HandleRestartClicked;
            }

            if (_defeatResultView != null)
            {
                _defeatResultView.RestartClicked -= HandleRestartClicked;
            }
        }

        private void OnDestroy()
        {
            Unsubscribe();
        }
    }
}
