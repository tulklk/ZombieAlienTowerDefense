using System;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AlienDefense.UI.Inventory
{
    /// <summary>The one square card of the inventory: equipment, artifacts, materials, containers, the six sockets
    /// around the UFO, merge slots and reward tiles. Coloured background, icon, small part badge in the corner, level
    /// on top, amount bottom-right, green upgrade arrow, red "!" / "NEW" for unseen entries, a faded part glyph when
    /// empty. Pure display: the presenter binds data and gets the click back with <see cref="Key"/>.</summary>
    public sealed class InvItemCardView : MonoBehaviour
    {
        [SerializeField]
        private Button _button;

        [SerializeField]
        private Image _background;

        [SerializeField]
        private Image _icon;

        [SerializeField]
        private Image _emptyGlyph;

        [SerializeField]
        private GameObject _partBadge;

        [SerializeField]
        private Image _partIcon;

        [SerializeField]
        private TMP_Text _level;

        [SerializeField]
        private TMP_Text _amount;

        [SerializeField]
        private GameObject _upgradeArrow;

        [SerializeField]
        private GameObject _newBadge;

        [SerializeField]
        private GameObject _newTag;

        [SerializeField]
        private GameObject _equippedMark;

        [SerializeField]
        private TMP_Text _label;

        /// <summary>Set by the presenter on every bind (pooled cards are re-bound, never re-subscribed).</summary>
        public Action<InvItemCardView> ClickHandler { get; set; }

        /// <summary>What this card shows (an equipment / artifact / item id), set by the presenter.</summary>
        public string Key { get; set; }

        public int Index { get; set; }

        private bool _wired;

        private void Wire()
        {
            if (_wired || _button == null)
            {
                return;
            }

            _wired = true;
            _button.onClick.AddListener(() => ClickHandler?.Invoke(this));
        }

        /// <summary>Empty socket / slot: dark card with the faded part glyph.</summary>
        public void ShowEmpty(Color background, Sprite glyph)
        {
            Wire();
            SetImage(_icon, null);
            if (_background != null) _background.color = background;
            if (_emptyGlyph != null)
            {
                _emptyGlyph.sprite = glyph;
                _emptyGlyph.enabled = glyph != null;
            }

            SetActive(_partBadge, false);
            SetText(_level, null);
            SetText(_amount, null);
            SetActive(_upgradeArrow, false);
            SetActive(_newBadge, false);
            SetActive(_newTag, false);
            SetActive(_equippedMark, false);
            SetText(_label, null);
        }

        public void Show(Sprite icon, Color background, Sprite partIcon, string level, string amount,
            bool upgrade = false, bool isNew = false, bool newTag = false, bool equipped = false, string label = null)
        {
            Wire();
            SetImage(_icon, icon);
            if (_background != null) _background.color = background;
            if (_emptyGlyph != null) _emptyGlyph.enabled = false;
            SetActive(_partBadge, partIcon != null);
            if (_partIcon != null) _partIcon.sprite = partIcon;
            SetText(_level, level);
            SetText(_amount, amount);
            SetActive(_upgradeArrow, upgrade);
            SetActive(_newBadge, isNew && !newTag);
            SetActive(_newTag, newTag);
            SetActive(_equippedMark, equipped);
            SetText(_label, label);
        }

        public void SetInteractable(bool interactable)
        {
            if (_button != null) _button.interactable = interactable;
        }

        /// <summary>Small pop (0.9 -> 1) when a card appears / is updated.</summary>
        public void Pop(float delay = 0f, float from = 0.9f)
        {
            transform.DOKill();
            transform.localScale = Vector3.one * from;
            transform.DOScale(1f, 0.22f).SetDelay(delay).SetEase(Ease.OutBack).SetUpdate(true).SetLink(gameObject);
        }

        /// <summary>Equip / upgrade feedback.</summary>
        public void Pulse()
        {
            transform.DOKill();
            transform.localScale = Vector3.one;
            transform.DOPunchScale(Vector3.one * 0.15f, 0.35f, 6, 0.6f).SetUpdate(true).SetLink(gameObject);
        }

        private static void SetImage(Image image, Sprite sprite)
        {
            if (image == null) return;
            image.sprite = sprite;
            image.enabled = sprite != null;
        }

        private static void SetText(TMP_Text text, string value)
        {
            if (text == null) return;
            bool show = !string.IsNullOrEmpty(value);
            if (text.gameObject.activeSelf != show) text.gameObject.SetActive(show);
            if (show) text.text = value;
        }

        private static void SetActive(GameObject go, bool active)
        {
            if (go != null && go.activeSelf != active) go.SetActive(active);
        }
    }
}
