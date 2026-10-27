using System;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AlienDefense.UI.Shop
{
    /// <summary>One tab of the Shop's bottom category bar: blue when idle, orange with a soft glow when selected,
    /// a red "!" badge for news.</summary>
    public sealed class ShopCategoryButtonView : MonoBehaviour
    {
        [SerializeField]
        private Button _button;

        [SerializeField]
        private Image _background;

        [SerializeField]
        private Sprite _normalSprite;

        [SerializeField]
        private Sprite _selectedSprite;

        [SerializeField]
        private GameObject _glow;

        [SerializeField]
        private RectTransform _iconRoot;

        [SerializeField]
        private TMP_Text _label;

        [SerializeField]
        private GameObject _badge;

        private bool _selected;

        public event Action Clicked;

        public RectTransform Rect => (RectTransform)transform;

        private void Awake()
        {
            if (_button != null)
            {
                _button.onClick.AddListener(() => Clicked?.Invoke());
            }
        }

        public void SetSelected(bool selected, bool animate)
        {
            bool changed = selected != _selected;
            _selected = selected;
            if (_background != null)
            {
                _background.sprite = selected ? _selectedSprite : _normalSprite;
            }

            if (_glow != null)
            {
                _glow.SetActive(selected);
            }

            if (selected && changed && animate && _iconRoot != null)
            {
                _iconRoot.DOKill();
                _iconRoot.localScale = Vector3.one;
                _iconRoot.DOPunchScale(Vector3.one * 0.14f, 0.25f, 6, 0.6f).SetUpdate(true).SetLink(gameObject);
            }
        }

        public void SetBadge(bool visible)
        {
            if (_badge != null && _badge.activeSelf != visible)
            {
                _badge.SetActive(visible);
            }
        }
    }
}
