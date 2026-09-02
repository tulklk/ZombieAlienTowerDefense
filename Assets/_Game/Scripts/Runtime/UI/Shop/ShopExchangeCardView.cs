using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AlienDefense.UI.Shop
{
    /// <summary>One Gem-to-Coin exchange pack card. Dumb view: forwards Buy clicks — never computes cost itself.</summary>
    public sealed class ShopExchangeCardView : MonoBehaviour
    {
        [SerializeField]
        private TMP_Text _coinAmountText;

        [SerializeField]
        private TMP_Text _gemCostText;

        [SerializeField]
        private Button _buyButton;

        public event Action Clicked;

        private void Awake()
        {
            if (_buyButton != null)
            {
                _buyButton.onClick.AddListener(HandleClicked);
            }
        }

        public void Configure(int coinAmount, int gemCost, bool canAfford)
        {
            if (_coinAmountText != null)
            {
                _coinAmountText.text = $"{coinAmount} Coin";
            }

            if (_gemCostText != null)
            {
                _gemCostText.text = gemCost.ToString();
            }

            if (_buyButton != null)
            {
                _buyButton.interactable = canAfford;
            }
        }

        private void HandleClicked()
        {
            Clicked?.Invoke();
        }

        private void OnDestroy()
        {
            if (_buyButton != null)
            {
                _buyButton.onClick.RemoveListener(HandleClicked);
            }
        }
    }
}
