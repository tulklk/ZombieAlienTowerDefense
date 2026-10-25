using System;
using AlienDefense.UI.MainMenu;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AlienDefense.UI.Defense
{
    /// <summary>One upgrade card of the inventory grid: the card art and how many the player owns. Dumb view.</summary>
    public sealed class UpgradeCardView : MonoBehaviour
    {
        [SerializeField] private Image _icon;
        [SerializeField] private TMP_Text _quantity;

        public void Configure(Sprite icon, int amount)
        {
            if (_icon != null)
            {
                _icon.sprite = icon;
                _icon.enabled = icon != null;
            }

            if (_quantity != null)
            {
                _quantity.text = CurrencyFormatter.Format(amount);
            }
        }
    }
}
