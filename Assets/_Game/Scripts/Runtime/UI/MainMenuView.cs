using System;
using UnityEngine;
using UnityEngine.UI;

namespace AlienDefense.UI
{
    /// <summary>Main Menu buttons. Dumb view: forwards clicks as events, never loads a scene itself.</summary>
    public sealed class MainMenuView : MonoBehaviour
    {
        [SerializeField]
        private Button _playButton;

        [SerializeField]
        [Tooltip("Optional placeholder. Disabled: Settings UI is Phase 17.")]
        private Button _settingsButton;

        [SerializeField]
        [Tooltip("Optional. Not meaningful on every platform.")]
        private Button _exitButton;

        public event Action PlayClicked;
        public event Action ExitClicked;

        private void Awake()
        {
            if (_playButton != null)
            {
                _playButton.onClick.AddListener(HandlePlayClicked);
            }

            if (_settingsButton != null)
            {
                _settingsButton.interactable = false;
            }

            if (_exitButton != null)
            {
                _exitButton.onClick.AddListener(HandleExitClicked);
            }
        }

        private void HandlePlayClicked()
        {
            PlayClicked?.Invoke();
        }

        private void HandleExitClicked()
        {
            ExitClicked?.Invoke();
        }

        private void OnDestroy()
        {
            if (_playButton != null)
            {
                _playButton.onClick.RemoveListener(HandlePlayClicked);
            }

            if (_exitButton != null)
            {
                _exitButton.onClick.RemoveListener(HandleExitClicked);
            }
        }
    }
}
