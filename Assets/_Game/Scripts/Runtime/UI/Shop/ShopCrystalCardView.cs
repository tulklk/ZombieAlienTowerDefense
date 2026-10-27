using System;
using AlienDefense.Shop;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AlienDefense.UI.Shop
{
    /// <summary>A Crystals grid card: amount (green and doubled while the first-purchase bonus is unused, with the
    /// normal amount crossed out), product picture, name, price and the "x2 1st time" badge.</summary>
    public sealed class ShopCrystalCardView : MonoBehaviour
    {
        private static readonly Color BonusGreen = new Color(0.55f, 1f, 0.35f);

        [SerializeField]
        private Button _button;

        [SerializeField]
        private TMP_Text _amount;

        [SerializeField]
        private TMP_Text _oldAmount;

        [SerializeField]
        private Image _art;

        [SerializeField]
        private TMP_Text _name;

        [SerializeField]
        private TMP_Text _price;

        [SerializeField]
        private GameObject _bonusBadge;

        private ShopItemData _item;
        private ShopService _service;
        private Action<ShopItemData> _onBuy;

        private bool _initialized;

        public void Bind(ShopItemData item, ShopService service, Action<ShopItemData> onBuy)
        {
            if (!_initialized && _button != null)
            {
                _initialized = true; // here, not Awake: cards are created under inactive containers
                _button.onClick.AddListener(() =>
                {
                    if (_item != null)
                    {
                        _onBuy?.Invoke(_item);
                    }
                });
            }

            _item = item;
            _service = service;
            _onBuy = onBuy;
            name = "Card_" + item.ProductId;
            if (_art != null)
            {
                _art.sprite = item.Art;
                _art.enabled = item.Art != null;
            }

            if (_name != null)
            {
                _name.text = item.DisplayName;
            }

            Refresh();
        }

        public void Refresh()
        {
            if (_item == null)
            {
                return;
            }

            bool bonus = _service.IsFirstPurchaseBonusAvailable(_item);
            int payout = _service.GetGemPayout(_item);
            if (_amount != null)
            {
                _amount.text = payout.ToString();
                _amount.color = bonus ? BonusGreen : Color.white;
            }

            if (_oldAmount != null)
            {
                _oldAmount.gameObject.SetActive(bonus);
                _oldAmount.text = "<s>" + _item.GemAmount + "</s>";
            }

            if (_bonusBadge != null)
            {
                _bonusBadge.SetActive(bonus);
            }

            if (_price != null)
            {
                _price.text = _service.GetPriceLabel(_item);
            }

            if (_button != null)
            {
                _button.interactable = _service.GetState(_item, DateTime.UtcNow) == ShopItemState.Available;
            }
        }
    }
}
