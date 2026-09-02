using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AlienDefense.UI.MainMenu
{
    /// <summary>One card on the left/right feature rails (VIP/event/daily/free-offer style slot). Dumb view with
    /// no domain knowledge of what feature it represents — MainMenuPresenter decides content per-slot, and since
    /// no such feature system exists yet in this project, every slot is currently left in its disabled
    /// placeholder state (see SetInteractable(false)) rather than inventing VIP/Event/Daily domains.</summary>
    public sealed class FeatureButtonView : MonoBehaviour
    {
        [SerializeField]
        private Button _button;

        [SerializeField]
        private Image _icon;

        [SerializeField]
        [Tooltip("Optional. e.g. a crown for VIP.")]
        private GameObject _badgeIcon;

        [SerializeField]
        private NotificationBadgeView _notificationBadge;

        [SerializeField]
        [Tooltip("Optional. Countdown text for timed features.")]
        private TMP_Text _countdownText;

        public event Action Clicked;

        private void Awake()
        {
            if (_button != null)
            {
                _button.onClick.AddListener(HandleClicked);
            }
        }

        public void SetIcon(Sprite sprite)
        {
            if (_icon != null)
            {
                _icon.sprite = sprite;
            }
        }

        public void SetBadgeVisible(bool visible)
        {
            if (_badgeIcon != null)
            {
                _badgeIcon.SetActive(visible);
            }
        }

        public void SetNotification(bool hasNotification)
        {
            if (_notificationBadge == null)
            {
                return;
            }

            if (hasNotification)
            {
                _notificationBadge.ShowDot();
            }
            else
            {
                _notificationBadge.Hide();
            }
        }

        public void SetCountdown(string countdownText)
        {
            if (_countdownText == null)
            {
                return;
            }

            bool hasCountdown = !string.IsNullOrEmpty(countdownText);
            _countdownText.gameObject.SetActive(hasCountdown);
            _countdownText.text = countdownText;
        }

        public void SetInteractable(bool interactable)
        {
            if (_button != null)
            {
                _button.interactable = interactable;
            }
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
