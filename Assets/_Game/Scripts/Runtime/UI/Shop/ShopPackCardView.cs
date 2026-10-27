using System;
using System.Collections.Generic;
using AlienDefense.Shop;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AlienDefense.UI.Shop
{
    /// <summary>Shared view of a pack card (Construction Queue feature pack, Campaign pack, Resource pack, privilege
    /// card). Every field is optional, so the four card prefabs differ only in layout. Bind once (builds the reward
    /// row), Refresh on shop changes, Tick once per second for the countdown / privilege timer. Nothing here knows a
    /// pack by name: all text and art come from ShopItemData.</summary>
    public sealed class ShopPackCardView : MonoBehaviour
    {
        [Header("Text")]
        [SerializeField]
        private TMP_Text _title;

        [SerializeField]
        private TMP_Text _subtitle;

        [SerializeField]
        private TMP_Text _description;

        [SerializeField]
        private TMP_Text _benefits;

        [Header("Look")]
        [SerializeField, Tooltip("Tinted with ShopItemData.Theme.")]
        private Image _themeImage;

        [SerializeField, Tooltip("Tinted with ShopItemData.ThemeDark.")]
        private Image _themeDarkImage;

        [SerializeField]
        private Image _art;

        [SerializeField]
        private Image _secondaryArt;

        [Header("Rewards")]
        [SerializeField, Tooltip("Optional big gem strip (feature / resource packs). Gems are then left out of the row.")]
        private TMP_Text _gemValue;

        [SerializeField]
        private Transform _rewardRow;

        [SerializeField]
        private ShopRewardIconView _rewardTemplate;

        [SerializeField]
        private bool _gemsInRow = true;

        [SerializeField]
        private bool _hideDurationRewards = true;

        [Header("Price")]
        [SerializeField]
        private GameObject _ribbon;

        [SerializeField]
        private TMP_Text _ribbonText;

        [SerializeField]
        private TMP_Text _originalPrice;

        [SerializeField]
        private Button _buyButton;

        [SerializeField]
        private Image _buyImage;

        [SerializeField]
        private TMP_Text _buyLabel;

        [SerializeField]
        private TMP_Text _limitText;

        [SerializeField, Tooltip("\"Purchase Limit: {0}\" or \"Remaining: {0}\".")]
        private string _limitFormat = "Remaining: {0}";

        [SerializeField]
        private TMP_Text _footnote;

        [Header("State")]
        [SerializeField]
        private GameObject _timerRoot;

        [SerializeField]
        private TMP_Text _timerText;

        [SerializeField]
        private GameObject _statusRoot;

        [SerializeField]
        private TMP_Text _statusText;

        [SerializeField]
        private GameObject _lockOverlay;

        [SerializeField]
        private TMP_Text _lockText;

        private readonly List<ShopRewardIconView> _rewardViews = new List<ShopRewardIconView>();
        private ShopItemData _item;
        private ShopService _service;
        private Action<ShopItemData> _onBuy;
        private ShopItemState _shownState = (ShopItemState)(-1);

        public ShopItemData Item => _item;

        private Color _buyBaseColor = Color.white;
        private bool _initialized;

        /// <summary>Once, from Bind: cards are created under inactive category containers, so Awake would run
        /// only later - after the first Refresh had already tinted the buy button.</summary>
        private void Initialize()
        {
            if (_initialized)
            {
                return;
            }

            _initialized = true;
            if (_buyButton != null)
            {
                _buyButton.onClick.AddListener(HandleBuy);
            }

            if (_buyImage != null)
            {
                _buyBaseColor = _buyImage.color; // the campaign price bar is a dark tint, the green buttons white
            }
        }

        public void Bind(ShopItemData item, ShopService service, Action<ShopItemData> onBuy)
        {
            Initialize();
            _item = item;
            _service = service;
            _onBuy = onBuy;
            name = "Card_" + item.ProductId;

            SetText(_title, item.DisplayName);
            SetText(_subtitle, item.Subtitle);
            SetText(_description, item.Description);
            SetText(_benefits, item.Benefits);
            if (_themeImage != null)
            {
                _themeImage.color = item.Theme;
            }

            if (_themeDarkImage != null)
            {
                _themeDarkImage.color = item.ThemeDark;
            }

            SetSprite(_art, item.Art);
            SetSprite(_secondaryArt, item.SecondaryArt);

            if (_ribbon != null)
            {
                _ribbon.SetActive(item.DiscountPercent > 0);
            }

            SetText(_ribbonText, item.DiscountPercent > 0 ? item.DiscountPercent + "%" : null);
            SetText(_originalPrice, item.OriginalPriceVnd > 0 ? "<s>" + ShopFormat.Vnd(item.OriginalPriceVnd) + "</s>" : null);
            BuildRewards();
            Refresh(DateTime.UtcNow);
        }

        private void BuildRewards()
        {
            if (_rewardRow == null || _rewardTemplate == null)
            {
                return;
            }

            foreach (ShopRewardIconView view in _rewardViews)
            {
                Destroy(view.gameObject);
            }

            _rewardViews.Clear();
            ShopCatalog catalog = _service.Catalog;
            for (int i = 0; i < _item.RewardCount; i++)
            {
                ShopRewardData reward = _item.GetReward(i);
                if (reward.Type == ShopRewardType.Gems && !_gemsInRow)
                {
                    continue;
                }

                if (_hideDurationRewards && (reward.Type == ShopRewardType.VipDays || reward.Type == ShopRewardType.PrivilegeDays))
                {
                    continue;
                }

                ShopRewardIconView view = Instantiate(_rewardTemplate, _rewardRow);
                view.gameObject.SetActive(true);
                view.Bind(catalog.GetIcon(reward), FormatAmount(reward), reward.Tag, TileColor(reward.Type));
                _rewardViews.Add(view);
            }
        }

        public void Refresh(DateTime nowUtc)
        {
            if (_item == null || _service == null)
            {
                return;
            }

            if (_gemValue != null)
            {
                _gemValue.text = ShopFormat.Compact(_service.GetGemPayout(_item));
            }

            ShopItemState state = _service.GetState(_item, nowUtc);
            _shownState = state;
            bool available = state == ShopItemState.Available;
            if (_buyButton != null)
            {
                _buyButton.gameObject.SetActive(state != ShopItemState.Active);
                _buyButton.interactable = available;
            }

            if (_buyImage != null)
            {
                _buyImage.color = available ? _buyBaseColor : _buyBaseColor * new Color(0.55f, 0.58f, 0.62f, 1f);
            }

            switch (state)
            {
                case ShopItemState.SoldOut:
                    SetText(_buyLabel, "Purchased");
                    break;
                case ShopItemState.Expired:
                    SetText(_buyLabel, "Expired");
                    break;
                case ShopItemState.Locked:
                    SetText(_buyLabel, "Locked");
                    break;
                default:
                    SetText(_buyLabel, _service.GetPriceLabel(_item));
                    break;
            }

            int remaining = _service.GetRemaining(_item);
            if (_limitText != null)
            {
                _limitText.gameObject.SetActive(remaining >= 0 && state != ShopItemState.Locked && state != ShopItemState.Active);
                _limitText.text = string.Format(_limitFormat, _item.LimitedTime ? remaining : _item.EffectiveLimit);
            }

            if (_subtitle != null && _item.OneTime)
            {
                _subtitle.text = state == ShopItemState.SoldOut ? "Purchased" : _item.Subtitle;
            }

            if (_lockOverlay != null)
            {
                _lockOverlay.SetActive(state == ShopItemState.Locked);
            }

            SetText(_lockText, string.IsNullOrEmpty(_item.LockedLabel) ? "Locked" : _item.LockedLabel);

            if (_footnote != null && _item.DurationDays > 0)
            {
                _footnote.text = _item.DurationDays + "-day privilege";
            }

            Tick(nowUtc);
        }

        /// <summary>Once per second (ShopTimer): the offer countdown and the privilege time left. Re-runs Refresh
        /// only when the state flips (an offer ran out, a privilege ended).</summary>
        public void Tick(DateTime nowUtc)
        {
            if (_item == null || _service == null)
            {
                return;
            }

            ShopItemState state = _service.GetState(_item, nowUtc);
            if (state != _shownState)
            {
                Refresh(nowUtc);
                return;
            }

            if (_timerRoot != null)
            {
                _timerRoot.SetActive(_item.LimitedTime);
            }

            if (_timerText != null && _item.LimitedTime)
            {
                _timerText.text = ShopFormat.Countdown(_service.GetOfferTimeLeft(nowUtc));
            }

            bool active = state == ShopItemState.Active;
            if (_statusRoot != null)
            {
                _statusRoot.SetActive(active);
            }

            if (_statusText != null && active)
            {
                _statusText.text = "ACTIVE  -  Remaining: " + ShopFormat.Remaining(_service.GetPrivilegeTimeLeft(_item.ProductId, nowUtc));
            }
        }

        private void HandleBuy()
        {
            if (_item != null)
            {
                _onBuy?.Invoke(_item);
            }
        }

        public static string FormatAmount(in ShopRewardData reward)
        {
            switch (reward.Type)
            {
                case ShopRewardType.VipDays:
                case ShopRewardType.PrivilegeDays:
                    return reward.Amount + "d";
            }

            return ShopFormat.Compact(reward.Amount);
        }

        public static Color TileColor(ShopRewardType type)
        {
            switch (type)
            {
                case ShopRewardType.Gems: return new Color(0.16f, 0.55f, 0.93f);
                case ShopRewardType.Coins: return new Color(0.23f, 0.70f, 0.30f);
                case ShopRewardType.ConstructionQueue: return new Color(0.98f, 0.73f, 0.16f);
                case ShopRewardType.VipDays: return new Color(0.62f, 0.32f, 0.92f);
                case ShopRewardType.PrivilegeDays: return new Color(0.62f, 0.32f, 0.92f);
                default: return new Color(0.23f, 0.70f, 0.30f);
            }
        }

        private static void SetText(TMP_Text text, string value)
        {
            if (text == null)
            {
                return;
            }

            text.gameObject.SetActive(!string.IsNullOrEmpty(value));
            text.text = value;
        }

        private static void SetSprite(Image image, Sprite sprite)
        {
            if (image == null)
            {
                return;
            }

            image.sprite = sprite;
            image.enabled = sprite != null;
        }
    }
}
