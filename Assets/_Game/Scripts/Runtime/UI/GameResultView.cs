using System;
using UnityEngine;
using UnityEngine.UI;

namespace AlienDefense.UI
{
    /// <summary>Reused for both the Victory and Defeat panels: static title text baked in at build time, plus a Restart button.</summary>
    public sealed class GameResultView : MonoBehaviour
    {
        [SerializeField]
        private Button _restartButton;

        public event Action RestartClicked;

        private void Awake()
        {
            if (_restartButton != null)
            {
                _restartButton.onClick.AddListener(HandleRestartClicked);
            }
        }

        private void HandleRestartClicked()
        {
            RestartClicked?.Invoke();
        }

        private void OnDestroy()
        {
            if (_restartButton != null)
            {
                _restartButton.onClick.RemoveListener(HandleRestartClicked);
            }
        }
    }
}
