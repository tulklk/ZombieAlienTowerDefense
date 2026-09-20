using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using AlienDefense.Meta;

namespace AlienDefense.UI.MainMenu
{
    /// <summary>Reward icon + amount used in YOUR REWARDS overlay and objective preview bubbles.</summary>
    public sealed class ObjectiveRewardItemView : MonoBehaviour
    {
        [SerializeField]
        private Image _frame;

        [SerializeField]
        private Image _icon;

        [SerializeField]
        private TMP_Text _amountText;

        [SerializeField]
        private Button _button;

        private GrantedObjectiveReward _granted;
        private Action<GrantedObjectiveReward> _onGrantedClicked;
        private string _previewItemId;
        private int _previewAmount;
        private bool _previewMystery;
        private Action<string, int, bool> _onPreviewClicked;

        private void Awake()
        {
            EnsureButtonWired();
            if (_icon != null)
            {
                _icon.preserveAspect = true;
            }
        }

        private void OnDestroy()
        {
            if (_button != null)
            {
                _button.onClick.RemoveListener(HandleClick);
            }
        }

        private void EnsureButtonWired()
        {
            if (_button == null)
            {
                _button = GetComponent<Button>();
            }

            if (_button == null)
            {
                return;
            }

            _button.onClick.RemoveListener(HandleClick);
            _button.onClick.AddListener(HandleClick);
        }

        public void Bind(
            GrantedObjectiveReward data,
            Sprite icon,
            string amountLabel,
            Color frameColor,
            Action<GrantedObjectiveReward> onClicked)
        {
            EnsureButtonWired();
            _granted = data;
            _onGrantedClicked = onClicked;
            _onPreviewClicked = null;
            _previewItemId = null;
            _previewMystery = false;

            ApplyVisual(icon, amountLabel, frameColor);
            if (_button != null)
            {
                _button.interactable = onClicked != null;
            }
        }

        /// <summary>Bubble preview bind: amount is grant quantity, not inventory owned.</summary>
        public void BindPreview(Sprite icon, int amount, string itemId, bool isMystery, Action<string, int, bool> onClicked)
        {
            EnsureButtonWired();
            _granted = default;
            _onGrantedClicked = null;
            _previewItemId = itemId;
            _previewAmount = amount;
            _previewMystery = isMystery;
            _onPreviewClicked = onClicked;

            ApplyVisual(icon, CurrencyFormatter.Format(amount), Color.clear);
            if (_frame != null)
            {
                _frame.enabled = false;
            }

            if (_button != null)
            {
                _button.interactable = onClicked != null;
            }
        }

        private void ApplyVisual(Sprite icon, string amountLabel, Color frameColor)
        {
            if (_icon != null)
            {
                _icon.sprite = icon;
                _icon.enabled = icon != null;
                _icon.preserveAspect = true;
                _icon.color = Color.white;
            }

            if (_amountText != null)
            {
                _amountText.text = amountLabel ?? string.Empty;
                _amountText.color = Color.white;
                _amountText.fontStyle = FontStyles.Bold;
            }

            if (_frame != null && frameColor.a > 0.01f)
            {
                _frame.enabled = true;
                _frame.color = frameColor;
            }
        }

        private void HandleClick()
        {
            if (_onPreviewClicked != null)
            {
                _onPreviewClicked.Invoke(_previewItemId, _previewAmount, _previewMystery);
                return;
            }

            _onGrantedClicked?.Invoke(_granted);
        }
    }
}
