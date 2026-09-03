using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AlienDefense.UI.MainMenu
{
    /// <summary>VIP tier purchase popup. Dumb view: shows 3 fixed tier rows (icon/cost/bonus/Buy) and forwards
    /// which tier was clicked — never computes cost/bonus/ownership itself.</summary>
    public sealed class VipPanelView : MonoBehaviour
    {
        [Serializable]
        public sealed class TierRow
        {
            public TMP_Text CostText;
            public TMP_Text BonusText;
            public Button BuyButton;
            public GameObject OwnedLabel;
        }

        [SerializeField]
        private TMP_Text _currentTierText;

        [SerializeField]
        private TierRow[] _tierRows = Array.Empty<TierRow>();

        [SerializeField]
        private Button _closeButton;

        /// <summary>Tier number (1-based) of whichever row's Buy button was clicked.</summary>
        public event Action<int> TierBuyClicked;

        public event Action CloseClicked;

        private void Awake()
        {
            for (int i = 0; i < _tierRows.Length; i++)
            {
                int tier = i + 1;
                if (_tierRows[i]?.BuyButton != null)
                {
                    _tierRows[i].BuyButton.onClick.AddListener(() => TierBuyClicked?.Invoke(tier));
                }
            }

            if (_closeButton != null)
            {
                _closeButton.onClick.AddListener(HandleCloseClicked);
            }
        }

        public void Show()
        {
            gameObject.SetActive(true);
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }

        public void SetCurrentTier(int currentTier)
        {
            if (_currentTierText != null)
            {
                _currentTierText.text = currentTier > 0 ? $"Current VIP: {currentTier}" : "No VIP";
            }
        }

        /// <param name="tierIndex">0-based row index.</param>
        public void ConfigureRow(int tierIndex, int gemCost, int bonusPercent, bool isOwned, bool canAfford)
        {
            if (tierIndex < 0 || tierIndex >= _tierRows.Length || _tierRows[tierIndex] == null)
            {
                return;
            }

            TierRow row = _tierRows[tierIndex];

            if (row.CostText != null)
            {
                row.CostText.text = gemCost.ToString();
            }

            if (row.BonusText != null)
            {
                row.BonusText.text = $"+{bonusPercent}% Coin";
            }

            if (row.OwnedLabel != null)
            {
                row.OwnedLabel.SetActive(isOwned);
            }

            if (row.BuyButton != null)
            {
                row.BuyButton.gameObject.SetActive(!isOwned);
                row.BuyButton.interactable = canAfford;
            }
        }

        private void HandleCloseClicked()
        {
            CloseClicked?.Invoke();
        }

        private void OnDestroy()
        {
            if (_closeButton != null)
            {
                _closeButton.onClick.RemoveListener(HandleCloseClicked);
            }
        }
    }
}
