using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AlienDefense.UI.MainMenu
{
    /// <summary>Avatar + player level/XP bar, top-left of TopHUD. Avatar is clickable and opens the Profile overlay
    /// (wired by MainMenuPresenter). Shows the player level and XP progress (PlayerLevelCurve over the saved lifetime
    /// XP) and power (PlayerPowerCalculator). SetUnavailable remains for callers that lack services.</summary>
    public sealed class PlayerProfileWidgetView : MonoBehaviour
    {
        [SerializeField]
        private Image _avatarImage;

        [SerializeField]
        private Button _avatarButton;

        [SerializeField]
        [Tooltip("Optional. Extra gear/customize button next to the XP bar — also opens Profile.")]
        private Button _extraButton;

        [SerializeField]
        private TMP_Text _playerLevelText;

        [SerializeField]
        private Image _xpFillImage;

        [SerializeField]
        [Tooltip("Optional. XP numbers drawn on the bar, e.g. '1.2K/3.5K'.")]
        private TMP_Text _xpText;

        [SerializeField]
        [Tooltip("Optional. Secondary progression/currency line under the XP bar.")]
        private TMP_Text _secondaryProgressText;

        [SerializeField]
        private NotificationBadgeView _notificationBadge;

        [SerializeField]
        private CanvasGroup _canvasGroup;

        public event Action AvatarClicked;

        private void Awake()
        {
            EnsureAvatarButton();
            if (_avatarButton != null)
            {
                _avatarButton.onClick.AddListener(HandleAvatarClicked);
            }

            if (_extraButton != null)
            {
                _extraButton.onClick.AddListener(HandleAvatarClicked);
            }
        }

        private void OnDestroy()
        {
            if (_avatarButton != null)
            {
                _avatarButton.onClick.RemoveListener(HandleAvatarClicked);
            }

            if (_extraButton != null)
            {
                _extraButton.onClick.RemoveListener(HandleAvatarClicked);
            }
        }

        public void SetLevel(int level, float xpProgress01, string secondaryText, string xpText = null)
        {
            if (_xpText != null)
            {
                _xpText.text = xpText ?? string.Empty;
            }

            if (_playerLevelText != null)
            {
                _playerLevelText.text = level.ToString();
            }

            if (_xpFillImage != null)
            {
                _xpFillImage.fillAmount = Mathf.Clamp01(xpProgress01);
            }

            if (_secondaryProgressText != null)
            {
                _secondaryProgressText.text = secondaryText ?? string.Empty;
            }

            if (_canvasGroup != null)
            {
                _canvasGroup.alpha = 1f;
            }

            _notificationBadge?.Hide();
        }

        /// <summary>Dims the widget when no profile services are available.</summary>
        public void SetUnavailable()
        {
            if (_playerLevelText != null)
            {
                _playerLevelText.text = "—";
            }

            if (_xpText != null)
            {
                _xpText.text = string.Empty;
            }

            if (_xpFillImage != null)
            {
                _xpFillImage.fillAmount = 0f;
            }

            if (_secondaryProgressText != null)
            {
                _secondaryProgressText.text = string.Empty;
            }

            if (_canvasGroup != null)
            {
                _canvasGroup.alpha = 0.4f;
            }

            _notificationBadge?.Hide();
        }

        private void EnsureAvatarButton()
        {
            if (_avatarButton != null)
            {
                return;
            }

            if (_avatarImage != null)
            {
                _avatarButton = _avatarImage.GetComponent<Button>();
                if (_avatarButton == null)
                {
                    _avatarButton = _avatarImage.gameObject.AddComponent<Button>();
                }

                _avatarImage.raycastTarget = true;
            }
        }

        private void HandleAvatarClicked()
        {
            AvatarClicked?.Invoke();
        }
    }
}
