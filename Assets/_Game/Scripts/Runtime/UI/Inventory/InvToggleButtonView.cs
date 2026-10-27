using System;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace AlienDefense.UI.Inventory
{
    /// <summary>A selectable button of the inventory: the Equipment / Artifacts / Materials sub-tabs (sprite swap blue
    /// -> green) and the part filter icons (icon tint light blue -> green). Red "!" badge optional.</summary>
    public sealed class InvToggleButtonView : MonoBehaviour
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
        private Image _icon;

        [SerializeField]
        private Color _iconNormal = new Color(0.78f, 0.9f, 1f, 1f);

        [SerializeField]
        private Color _iconSelected = new Color(0.36f, 0.9f, 0.35f, 1f);

        [SerializeField]
        private GameObject _badge;

        private bool _selected;
        private bool _wired;

        public event Action Clicked;

        private void Awake()
        {
            Wire();
        }

        private void Wire()
        {
            if (_wired || _button == null)
            {
                return;
            }

            _wired = true;
            _button.onClick.AddListener(() => Clicked?.Invoke());
        }

        public void SetSelected(bool selected, bool animate)
        {
            Wire();
            bool changed = selected != _selected;
            _selected = selected;
            if (_background != null && _selectedSprite != null)
            {
                _background.sprite = selected ? _selectedSprite : _normalSprite;
            }

            if (_icon != null)
            {
                _icon.color = selected ? _iconSelected : _iconNormal;
            }

            if (selected && changed && animate)
            {
                transform.DOKill();
                transform.localScale = Vector3.one;
                transform.DOPunchScale(Vector3.one * 0.1f, 0.22f, 6, 0.6f).SetUpdate(true).SetLink(gameObject);
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
