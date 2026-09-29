using AlienDefense.UI.MainMenu;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AlienDefense.UI.Inventory
{
    /// <summary>One "owned / required" requirement slot under the craft comparison - a duplicate card or the
    /// crafting currency. Turns red the moment the player is short, which is the same signal the Craft button
    /// uses to stay disabled.</summary>
    public sealed class CraftRequirementView : MonoBehaviour
    {
        [SerializeField]
        private Image _icon;

        [SerializeField]
        private TMP_Text _amountText;

        [SerializeField]
        private Color _satisfiedColor = Color.white;

        [SerializeField]
        private Color _missingColor = new Color(1f, 0.35f, 0.35f, 1f);

        public void Bind(Sprite icon, int owned, int required)
        {
            if (_icon != null)
            {
                _icon.sprite = icon;
                _icon.enabled = icon != null;
            }

            if (_amountText != null)
            {
                _amountText.text = $"{CurrencyFormatter.Format(owned)}/{CurrencyFormatter.Format(required)}";
                _amountText.color = owned >= required ? _satisfiedColor : _missingColor;
            }
        }
    }
}
