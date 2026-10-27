using System;
using AlienDefense.Shop;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AlienDefense.UI.Shop
{
    /// <summary>The VIP Account panel: status (Deactivated / VIP Active + time left), the daily gift (claimable once a
    /// day while VIP runs), the perk list and the purchase plans (one slot per VIP product).</summary>
    public sealed class ShopVipView : MonoBehaviour
    {
        [Serializable]
        public struct PlanSlot
        {
            public Button Button;
            public TMP_Text Price;
            public TMP_Text Time;
            public TMP_Text Gems;
            public TMP_Text Label;
        }

        [SerializeField]
        private TMP_Text _statusLabel;

        [SerializeField]
        private TMP_Text _statusTimer;

        [SerializeField]
        private Image _avatar;

        [SerializeField]
        private GameObject _sleepMark;

        [SerializeField]
        private Button _giftButton;

        [SerializeField]
        private GameObject _giftBadge;

        [SerializeField]
        private TMP_Text _dailyGems;

        [SerializeField]
        private PlanSlot[] _plans = new PlanSlot[0];

        private ShopService _service;
        private ShopItemData[] _planItems = new ShopItemData[0];
        private Action<ShopItemData> _onBuy;
        private Action _onClaim;
        private bool _lastActive;
        private bool _lastClaimable;

        private void Awake()
        {
            if (_giftButton != null)
            {
                _giftButton.onClick.AddListener(() => _onClaim?.Invoke());
            }

            for (int i = 0; i < _plans.Length; i++)
            {
                int index = i;
                if (_plans[i].Button != null)
                {
                    _plans[i].Button.onClick.AddListener(() =>
                    {
                        if (index < _planItems.Length && _planItems[index] != null)
                        {
                            _onBuy?.Invoke(_planItems[index]);
                        }
                    });
                }
            }
        }

        public void Bind(ShopService service, ShopItemData[] plans, Action<ShopItemData> onBuy, Action onClaim)
        {
            _service = service;
            _planItems = plans ?? new ShopItemData[0];
            _onBuy = onBuy;
            _onClaim = onClaim;
            if (_dailyGems != null)
            {
                _dailyGems.text = service.Catalog.VipDailyGems.ToString();
            }

            for (int i = 0; i < _plans.Length; i++)
            {
                bool has = i < _planItems.Length && _planItems[i] != null;
                if (_plans[i].Button != null)
                {
                    _plans[i].Button.transform.parent.gameObject.SetActive(has);
                }

                if (!has)
                {
                    continue;
                }

                ShopItemData plan = _planItems[i];
                if (_plans[i].Gems != null)
                {
                    _plans[i].Gems.text = plan.GemAmount.ToString();
                }

                if (_plans[i].Label != null)
                {
                    _plans[i].Label.text = "Reward\nfor " + plan.DurationDays + " days";
                }

                if (_plans[i].Time != null)
                {
                    _plans[i].Time.text = "Time: " + plan.DurationDays + "d 0h";
                }
            }

            Refresh(DateTime.UtcNow);
        }

        public void Refresh(DateTime nowUtc)
        {
            if (_service == null)
            {
                return;
            }

            bool active = _service.IsVipActive(nowUtc);
            bool claimable = _service.CanClaimVipDaily(nowUtc);
            _lastActive = active;
            _lastClaimable = claimable;
            if (_statusLabel != null)
            {
                _statusLabel.text = active ? "VIP Active" : "Deactivated";
            }

            if (_sleepMark != null)
            {
                _sleepMark.SetActive(!active);
            }

            if (_avatar != null)
            {
                _avatar.color = active ? Color.white : new Color(0.75f, 0.75f, 0.8f, 1f);
            }

            if (_giftBadge != null)
            {
                _giftBadge.SetActive(claimable);
            }

            if (_giftButton != null)
            {
                _giftButton.interactable = claimable;
            }

            for (int i = 0; i < _plans.Length && i < _planItems.Length; i++)
            {
                if (_planItems[i] == null)
                {
                    continue;
                }

                bool available = _service.GetState(_planItems[i], nowUtc) == ShopItemState.Available;
                if (_plans[i].Button != null)
                {
                    _plans[i].Button.interactable = available;
                }

                if (_plans[i].Price != null)
                {
                    _plans[i].Price.text = _service.GetPriceLabel(_planItems[i]);
                }
            }

            Tick(nowUtc);
        }

        public void Tick(DateTime nowUtc)
        {
            if (_service == null)
            {
                return;
            }

            if (_service.IsVipActive(nowUtc) != _lastActive || _service.CanClaimVipDaily(nowUtc) != _lastClaimable)
            {
                Refresh(nowUtc);
                return;
            }

            if (_statusTimer != null)
            {
                _statusTimer.gameObject.SetActive(_lastActive);
                if (_lastActive)
                {
                    _statusTimer.text = "Remaining: " + ShopFormat.Remaining(_service.GetVipTimeLeft(nowUtc));
                }
            }
        }
    }
}
