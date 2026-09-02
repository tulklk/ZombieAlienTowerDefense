using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AlienDefense.UI.MainMenu
{
    /// <summary>Avatar + player level/XP bar, top-left of TopHUD. PlayerProfileSaveData has no persistent player
    /// level/XP field yet (only per-level campaign progress + MetaCurrency) — SetUnavailable puts this widget in
    /// a dimmed placeholder state rather than inventing a meta-level system, matching ResourceWidgetView's rule.
    /// Kept ready for a real PlayerLevelProgressionService-backed profile field later (SetLevel/SetExperience).</summary>
    public sealed class PlayerProfileWidgetView : MonoBehaviour
    {
        [SerializeField]
        private Image _avatarImage;

        [SerializeField]
        private TMP_Text _playerLevelText;

        [SerializeField]
        private Image _xpFillImage;

        [SerializeField]
        [Tooltip("Optional. Secondary progression/currency line under the XP bar.")]
        private TMP_Text _secondaryProgressText;

        [SerializeField]
        private NotificationBadgeView _notificationBadge;

        [SerializeField]
        private CanvasGroup _canvasGroup;

        public void SetLevel(int level, float xpProgress01, string secondaryText)
        {
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

        /// <summary>No persistent player-level system exists yet — dims the widget instead of showing a fake level 1.</summary>
        public void SetUnavailable()
        {
            if (_playerLevelText != null)
            {
                _playerLevelText.text = "—";
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
    }
}
