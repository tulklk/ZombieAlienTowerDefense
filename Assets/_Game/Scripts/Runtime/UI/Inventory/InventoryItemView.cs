using AlienDefense.Meta;
using AlienDefense.UI.MainMenu;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AlienDefense.UI.Inventory
{
    /// <summary>One square in the Materials or Containers grid: rarity frame, icon, and the amount held.
    ///
    /// Shared by both sections rather than duplicated, because a container differs from a material only by which
    /// list it came from - the visual is identical. The quantity runs through the project's existing
    /// CurrencyFormatter so 1300 reads as "1.3K" here exactly as it does in the HUD.</summary>
    public sealed class InventoryItemView : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Tinted by rarity, or swapped for the rarity's own sprite when the palette provides one.")]
        private Image _rarityFrame;

        [SerializeField]
        private Image _icon;

        [SerializeField]
        private TMP_Text _quantityText;

        [SerializeField]
        [Tooltip("Optional. Raised on tap so the panel can open a detail popup.")]
        private Button _button;

        private string _itemId;
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

        public void Bind(MaterialEntry entry, RarityPalette palette, System.Action<string> clicked)
        {
            _clicked = clicked;
            _itemId = entry.Definition != null ? entry.Definition.Id : null;

            if (_icon != null)
            {
                _icon.sprite = entry.Definition != null ? entry.Definition.Icon : null;
                _icon.enabled = _icon.sprite != null;
            }

            if (_quantityText != null)
            {
                _quantityText.text = CurrencyFormatter.Format(entry.Amount);
            }

            ApplyRarity(_rarityFrame, palette, entry.Definition != null ? entry.Definition.Rarity : MetaItemRarity.Common);
        }

        /// <summary>Frame styling shared by every inventory card. A palette entry with no sprite keeps whatever
        /// frame art the prefab already has and only tints it, so one palette can drive several card shapes.</summary>
        internal static void ApplyRarity(Image frame, RarityPalette palette, MetaItemRarity rarity)
        {
            if (frame == null)
            {
                return;
            }

            if (palette == null)
            {
                return;
            }

            Sprite sprite = palette.GetFrameSprite(rarity);
            if (sprite != null)
            {
                frame.sprite = sprite;
            }

            frame.color = palette.GetFrameColor(rarity);
        }

        private void HandleClicked()
        {
            _clicked?.Invoke(_itemId);
        }
    }
}
