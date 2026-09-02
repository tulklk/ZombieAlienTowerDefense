using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AlienDefense.UI.MainMenu
{
    /// <summary>Horizontal banner above BottomNavigation. This project has no Quest/Task system yet, so
    /// MainMenuPresenter currently only ever calls SetUnavailable — the view stays ready for a real
    /// MainMenuQuestDisplayModel-backed quest system to opt into via SetQuest/Clicked later.</summary>
    public sealed class QuestBannerView : MonoBehaviour
    {
        [SerializeField]
        private Button _button;

        [SerializeField]
        private Image _questIcon;

        [SerializeField]
        private TMP_Text _questText;

        [SerializeField]
        private TMP_Text _questProgressText;

        [SerializeField]
        [Tooltip("Optional.")]
        private Image _progressBarFill;

        [SerializeField]
        private NotificationBadgeView _notificationBadge;

        public event Action Clicked;

        private void Awake()
        {
            if (_button != null)
            {
                _button.onClick.AddListener(HandleClicked);
            }
        }

        public void SetQuest(string questText, string progressText, float progress01)
        {
            gameObject.SetActive(true);

            if (_questText != null)
            {
                _questText.text = questText;
            }

            if (_questProgressText != null)
            {
                _questProgressText.text = progressText;
            }

            if (_progressBarFill != null)
            {
                _progressBarFill.fillAmount = Mathf.Clamp01(progress01);
            }

            if (_button != null)
            {
                _button.interactable = true;
            }
        }

        /// <summary>No Quest/Task system exists yet — hides the banner instead of showing fabricated content.</summary>
        public void SetUnavailable()
        {
            gameObject.SetActive(false);
            _notificationBadge?.Hide();
        }

        private void HandleClicked()
        {
            Clicked?.Invoke();
        }

        private void OnDestroy()
        {
            if (_button != null)
            {
                _button.onClick.RemoveListener(HandleClicked);
            }
        }
    }
}
