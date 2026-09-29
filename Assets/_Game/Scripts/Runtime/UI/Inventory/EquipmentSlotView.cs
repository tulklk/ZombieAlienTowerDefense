using AlienDefense.Meta;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AlienDefense.UI.Inventory
{
    /// <summary>One of the six sockets around the UFO preview.
    ///
    /// The slot it represents is authored in the Inspector, not derived from its position, so re-arranging the
    /// layout never silently re-assigns which category a socket shows.</summary>
    public sealed class EquipmentSlotView : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Which category this socket shows. Set per instance in the Inspector.")]
        private EquipmentSlotType _slot = EquipmentSlotType.Controls;

        [SerializeField]
        [Tooltip("The dimmed silhouette shown when nothing is equipped here.")]
        private Image _emptyIcon;

        [SerializeField]
        private Image _itemIcon;

        [SerializeField]
        private Image _rarityFrame;

        [SerializeField]
        private TMP_Text _levelText;

        [SerializeField]
        [Tooltip("Green arrow badge - on when the equipped piece can be crafted up right now.")]
        private GameObject _upgradeIndicator;

        [SerializeField]
        private Button _button;

        [SerializeField, Range(0f, 1f)]
        [Tooltip("Opacity of the empty silhouette. The design calls for a faint placeholder, not a blank hole.")]
        private float _emptyIconAlpha = 0.4f;

        private string _equipmentId;
        private System.Action<string> _clicked;

        public EquipmentSlotType Slot => _slot;

        private void Awake()
        {
            if (_button != null)
            {
                _button.onClick.AddListener(HandleClicked);
            }
        }

        private void OnDestroy()
        {
            if (_button != null)
            {
                _button.onClick.RemoveListener(HandleClicked);
            }
        }

        public void Bind(EquipmentEntry entry, RarityPalette palette, System.Action<string> clicked)
        {
            _clicked = clicked;
            bool filled = entry.Exists;
            _equipmentId = filled ? entry.Definition.Id : null;

            if (_emptyIcon != null)
            {
                _emptyIcon.gameObject.SetActive(!filled);
                Color c = _emptyIcon.color;
                c.a = _emptyIconAlpha;
                _emptyIcon.color = c;
            }

            if (_itemIcon != null)
            {
                _itemIcon.gameObject.SetActive(filled);
                if (filled)
                {
                    _itemIcon.sprite = entry.Definition.Icon;
                    _itemIcon.enabled = _itemIcon.sprite != null;
                }
            }

            if (_rarityFrame != null)
            {
                _rarityFrame.gameObject.SetActive(filled);
                if (filled)
                {
                    InventoryItemView.ApplyRarity(_rarityFrame, palette, entry.Rarity);
                }
            }

            if (_levelText != null)
            {
                // An empty socket shows no level at all rather than "Lvl. 0".
                _levelText.gameObject.SetActive(filled);
                if (filled)
                {
                    _levelText.text = $"Lvl. {entry.Level}";
                }
            }

            if (_upgradeIndicator != null)
            {
                _upgradeIndicator.SetActive(filled && entry.CanUpgrade);
            }

            if (_button != null)
            {
                _button.interactable = filled;
            }
        }

        private void HandleClicked()
        {
            if (!string.IsNullOrEmpty(_equipmentId))
            {
                _clicked?.Invoke(_equipmentId);
            }
        }
    }
}
