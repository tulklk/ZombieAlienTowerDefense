using System;
using UnityEngine;
using UnityEngine.UI;

namespace AlienDefense.UI
{
    /// <summary>Pause panel buttons: Resume, Restart, and Main Menu.</summary>
    public sealed class PausePanelView : MonoBehaviour
    {
        [SerializeField]
        private Button _resumeButton;

        [SerializeField]
        private Button _restartButton;

        [SerializeField]
        [Tooltip("Optional.")]
        private Button _mainMenuButton;

        public event Action ResumeClicked;
        public event Action RestartClicked;
        public event Action MainMenuClicked;

        private void Awake()
        {
            if (_resumeButton != null)
            {
                _resumeButton.onClick.AddListener(HandleResumeClicked);
            }

            if (_restartButton != null)
            {
                _restartButton.onClick.AddListener(HandleRestartClicked);
            }

            if (_mainMenuButton != null)
            {
                _mainMenuButton.onClick.AddListener(HandleMainMenuClicked);
            }
        }

        private void HandleResumeClicked()
        {
            ResumeClicked?.Invoke();
        }

        private void HandleRestartClicked()
        {
            RestartClicked?.Invoke();
        }

        private void HandleMainMenuClicked()
        {
            MainMenuClicked?.Invoke();
        }

        private void OnDestroy()
        {
            if (_resumeButton != null)
            {
                _resumeButton.onClick.RemoveListener(HandleResumeClicked);
            }

            if (_restartButton != null)
            {
                _restartButton.onClick.RemoveListener(HandleRestartClicked);
            }

            if (_mainMenuButton != null)
            {
                _mainMenuButton.onClick.RemoveListener(HandleMainMenuClicked);
            }
        }
    }
}
