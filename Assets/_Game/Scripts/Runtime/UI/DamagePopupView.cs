using System;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AlienDefense.UI
{
    /// <summary>One pooled floating damage number ([icon] 466). It only animates itself (pop, rise, fade) with DOTween;
    /// DamagePopupService owns the pool and projects WorldPosition + Rise onto the screen every frame.</summary>
    public sealed class DamagePopupView : MonoBehaviour
    {
        [SerializeField]
        private CanvasGroup _group;

        [SerializeField]
        private RectTransform _content;

        [SerializeField]
        private Image _icon;

        [SerializeField]
        private TMP_Text _text;

        [SerializeField, Min(0f)]
        private float _iconGap = 6f;

        private Sequence _sequence;
        private float _rise;

        /// <summary>World point the number belongs to (above the enemy's head), fixed at spawn.</summary>
        public Vector3 WorldPosition { get; private set; }

        /// <summary>World-space lift added by the animation.</summary>
        public float Rise => _rise;

        public bool IsPlaying { get; private set; }

        public void Play(Vector3 worldPosition, int amount, DamagePopupService.Style style, float riseHeight, float lifetime,
            Action<DamagePopupView> onFinished)
        {
            Kill();
            WorldPosition = worldPosition;
            _rise = 0f;
            IsPlaying = true;
            gameObject.SetActive(true);

            _text.text = amount.ToString();
            _text.fontSize = style.FontSize;
            _text.enableVertexGradient = true;
            _text.colorGradient = new VertexGradient(style.TopColor, style.TopColor, style.BottomColor, style.BottomColor);
            _text.outlineColor = style.OutlineColor;
            _text.outlineWidth = style.OutlineWidth;

            bool hasIcon = style.Icon != null;
            _icon.gameObject.SetActive(hasIcon);
            if (hasIcon)
            {
                _icon.sprite = style.Icon;
            }

            LayoutContent(hasIcon, style.FontSize);

            _group.alpha = 1f;
            _content.localScale = Vector3.one * 0.4f;

            float popIn = 0.09f;
            float settle = 0.07f;
            float fade = Mathf.Min(0.3f, lifetime * 0.4f);

            _sequence = DOTween.Sequence().SetLink(gameObject);
            _sequence.Append(_content.DOScale(1.2f, popIn).SetEase(Ease.OutQuad));
            _sequence.Append(_content.DOScale(1f, settle).SetEase(Ease.InOutQuad));
            _sequence.Insert(0f, DOTween.To(() => _rise, value => _rise = value, riseHeight, lifetime).SetEase(Ease.OutCubic));
            _sequence.Insert(lifetime - fade, DOTween.To(() => _group.alpha, value => _group.alpha = value, 0f, fade));
            _sequence.OnComplete(() =>
            {
                _sequence = null;
                IsPlaying = false;
                onFinished?.Invoke(this);
            });
        }

        public void Stop()
        {
            Kill();
            IsPlaying = false;
            gameObject.SetActive(false);
        }

        /// <summary>Icon then number, centred as one block - one preferred-width query per popup, no layout groups.</summary>
        private void LayoutContent(bool hasIcon, float fontSize)
        {
            var textRect = _text.rectTransform;
            float textWidth = _text.GetPreferredValues(_text.text).x;
            float iconSize = hasIcon ? fontSize * 1.05f : 0f;
            float gap = hasIcon ? _iconGap : 0f;
            float total = iconSize + gap + textWidth;

            textRect.sizeDelta = new Vector2(textWidth, fontSize * 1.3f);
            textRect.anchoredPosition = new Vector2(-total * 0.5f + iconSize + gap + textWidth * 0.5f, 0f);

            if (hasIcon)
            {
                var iconRect = _icon.rectTransform;
                iconRect.sizeDelta = new Vector2(iconSize, iconSize);
                iconRect.anchoredPosition = new Vector2(-total * 0.5f + iconSize * 0.5f, fontSize * 0.04f);
            }
        }

        private void Kill()
        {
            _sequence?.Kill();
            _sequence = null;
        }

        private void OnDestroy()
        {
            Kill();
        }
    }
}
