using System;
using UnityEngine;
using UnityEngine.UI;

namespace AlienDefense.UI
{
    /// <summary>Pause panel buttons: Resume and Restart. Quit/Main Menu is a disabled placeholder (out of scope this phase).</summary>
    public sealed class PausePanelView : MonoBehaviour
    {
        [SerializeField]
        private Button _resumeButton;

        [SerializeField]
        private Button _restartButton;

        [SerializeField]
        [Tooltip("Optional placeholder. Disabled: no Main Menu exists yet.")]
        private Button _quitButton;

        public event Action ResumeClicked;
        public event Action RestartClicked;

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

            if (_quitButton != null)
            {
                _quitButton.interactable = false;
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
        }
    }
}
