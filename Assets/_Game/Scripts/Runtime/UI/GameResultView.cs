using System;
using UnityEngine;
using UnityEngine.UI;

namespace AlienDefense.UI
{
    /// <summary>Reused for both the Victory and Defeat panels: static title text baked in at build time, plus
    /// Restart/Level Selection buttons. Next Level is optional and only wired on the Victory instance.</summary>
    public sealed class GameResultView : MonoBehaviour
    {
        [SerializeField]
        private Button _restartButton;

        [SerializeField]
        private Button _levelSelectionButton;

        [SerializeField]
        [Tooltip("Optional. Victory only.")]
        private Button _nextLevelButton;

        public event Action RestartClicked;
        public event Action LevelSelectionClicked;
        public event Action NextLevelClicked;

        private void Awake()
        {
            if (_restartButton != null)
            {
                _restartButton.onClick.AddListener(HandleRestartClicked);
            }

            if (_levelSelectionButton != null)
            {
                _levelSelectionButton.onClick.AddListener(HandleLevelSelectionClicked);
            }

            if (_nextLevelButton != null)
            {
                _nextLevelButton.onClick.AddListener(HandleNextLevelClicked);
            }
        }

        private void HandleRestartClicked()
        {
            RestartClicked?.Invoke();
        }

        private void HandleLevelSelectionClicked()
        {
            LevelSelectionClicked?.Invoke();
        }

        private void HandleNextLevelClicked()
        {
            NextLevelClicked?.Invoke();
        }

        private void OnDestroy()
        {
            if (_restartButton != null)
            {
                _restartButton.onClick.RemoveListener(HandleRestartClicked);
            }

            if (_levelSelectionButton != null)
            {
                _levelSelectionButton.onClick.RemoveListener(HandleLevelSelectionClicked);
            }

            if (_nextLevelButton != null)
            {
                _nextLevelButton.onClick.RemoveListener(HandleNextLevelClicked);
            }
        }
    }
}
