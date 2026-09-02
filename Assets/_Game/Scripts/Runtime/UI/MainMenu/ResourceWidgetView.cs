using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AlienDefense.UI.MainMenu
{
    /// <summary>One TopHUD resource pill (Coin/Energy/Gem — same layout, different icon/data). Dumb view: shows
    /// whatever amount it's given, already formatted by the caller (CurrencyFormatter), and forwards Add-button
    /// clicks. SetUnavailable puts it in a disabled placeholder state for a resource this project doesn't
    /// actually track yet, per the "don't invent an economy the profile doesn't have" rule — never invents a
    /// fake amount.</summary>
    public sealed class ResourceWidgetView : MonoBehaviour
    {
        [SerializeField]
        private TMP_Text _amountText;

        [SerializeField]
        [Tooltip("Optional.")]
        private Button _addButton;

        [SerializeField]
        [Tooltip("Optional. Dimmed while SetUnavailable is active.")]
        private CanvasGroup _canvasGroup;

        /// <summary>Fires only while the Add button is interactable (see SetAddButtonEnabled) — callers never
        /// need to check state before wiring/unwiring this.</summary>
        public event Action AddButtonClicked;

        private void Awake()
        {
            if (_addButton != null)
            {
                _addButton.onClick.AddListener(HandleAddButtonClicked);
            }
        }

        public void SetAmount(string formattedAmount)
        {
            if (_amountText != null)
            {
                _amountText.text = formattedAmount;
            }

            if (_canvasGroup != null)
            {
                _canvasGroup.alpha = 1f;
            }
        }

        /// <summary>Only call true once there's somewhere real for the click to go (e.g. the Shop screen) —
        /// leave false for a resource with no spend/earn flow yet.</summary>
        public void SetAddButtonEnabled(bool enabled)
        {
            if (_addButton != null)
            {
                _addButton.interactable = enabled;
            }
        }

        /// <summary>This resource doesn't exist in PlayerProfileSaveData yet (e.g. lobby Energy). Shows a
        /// dimmed placeholder slot instead of a fabricated number, so the layout matches the reference composition
        /// without inventing an economy.</summary>
        public void SetUnavailable(string placeholderText = "—")
        {
            if (_amountText != null)
            {
                _amountText.text = placeholderText;
            }

            if (_canvasGroup != null)
            {
                _canvasGroup.alpha = 0.4f;
            }

            SetAddButtonEnabled(false);
        }

        private void HandleAddButtonClicked()
        {
            AddButtonClicked?.Invoke();
        }

        private void OnDestroy()
        {
            if (_addButton != null)
            {
                _addButton.onClick.RemoveListener(HandleAddButtonClicked);
            }
        }
    }
}
