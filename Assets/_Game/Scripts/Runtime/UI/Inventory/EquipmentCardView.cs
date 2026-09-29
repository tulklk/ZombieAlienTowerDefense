using AlienDefense.Meta;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AlienDefense.UI.Inventory
{
    /// <summary>One owned piece in the equipment grid below the category bar.</summary>
    public sealed class EquipmentCardView : MonoBehaviour
    {
        [SerializeField]
        private Image _rarityFrame;

        [SerializeField]
        private Image _icon;

        [SerializeField]
        private TMP_Text _levelText;

        [SerializeField]
        [Tooltip("Green tick shown on the piece currently worn in its slot.")]
        private GameObject _equippedBadge;

        [SerializeField]
        private TMP_Text _equippedText;

        [SerializeField]
        private GameObject _upgradeBadge;

        [SerializeField]
        private Button _button;

        [SerializeField]
        private string _equippedLabel = "equipped";

        private string _equipmentId;
        private System.Action<string> _clicked;

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
            _equipmentId = entry.Definition != null ? entry.Definition.Id : null;

            if (_icon != null)
            {
                _icon.sprite = entry.Definition != null ? entry.Definition.Icon : null;
                _icon.enabled = _icon.sprite != null;
            }

            if (_levelText != null)
            {
                _levelText.text = $"Lvl. {entry.Level}";
            }

            if (_equippedBadge != null)
            {
                _equippedBadge.SetActive(entry.Equipped);
            }

            if (_equippedText != null)
            {
                _equippedText.gameObject.SetActive(entry.Equipped);
                _equippedText.text = _equippedLabel;
            }

            if (_upgradeBadge != null)
            {
                _upgradeBadge.SetActive(entry.CanUpgrade);
            }

            InventoryItemView.ApplyRarity(_rarityFrame, palette, entry.Rarity);
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
