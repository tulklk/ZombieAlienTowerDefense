using DG.Tweening;
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

        [SerializeField]
        [Tooltip("Optional. Where a reward flying in from the level result should land, and what pulses when it " +
            "arrives. Falls back to this widget's own RectTransform.")]
        private RectTransform _flyTarget;

        [SerializeField]
        [Tooltip("Optional. Small line under the pill, e.g. the energy refill countdown. Hidden while empty.")]
        private TMP_Text _subText;

        private Tween _punchTween;
        private Tween _countTween;

        /// <summary>Where an incoming reward icon should fly to. Never null for an assigned widget.</summary>
        public RectTransform FlyTarget => _flyTarget != null ? _flyTarget : (RectTransform)transform;

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

        /// <summary>Shows a short line under the pill (the energy countdown); empty or null hides it.</summary>
        public void SetSubText(string text)
        {
            if (_subText == null)
            {
                return;
            }

            bool show = !string.IsNullOrEmpty(text);
            if (_subText.gameObject.activeSelf != show)
            {
                _subText.gameObject.SetActive(show);
            }

            if (show)
            {
                _subText.text = text;
            }
        }

        /// <summary>A reward just landed here: a short scale pulse on the target. Purely cosmetic.</summary>
        public void PlayArrivalPulse(float scale = 1.18f, float duration = 0.15f)
        {
            RectTransform target = FlyTarget;
            if (target == null)
            {
                return;
            }

            _punchTween?.Kill();
            target.localScale = Vector3.one;
            _punchTween = target.DOPunchScale(Vector3.one * (scale - 1f), duration, 1, 0.4f)
                .SetLink(gameObject);
        }

        /// <summary>Counts the displayed number up to the value the profile already holds. Visual only - this
        /// widget never owns or changes the real amount, it is told what to show.</summary>
        public void CountTo(int from, int to, float duration)
        {
            if (_amountText == null)
            {
                return;
            }

            _countTween?.Kill();
            int shown = from;
            _amountText.text = CurrencyFormatter.Format(from);
            _countTween = DOTween.To(() => shown, value =>
                {
                    shown = value;
                    _amountText.text = CurrencyFormatter.Format(shown);
                }, to, duration)
                .SetEase(Ease.OutCubic)
                .SetLink(gameObject);
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
            _punchTween?.Kill();
            _countTween?.Kill();

            if (_addButton != null)
            {
                _addButton.onClick.RemoveListener(HandleAddButtonClicked);
            }
        }
    }
}
