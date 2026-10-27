using UnityEngine;

namespace AlienDefense.Shop
{
    /// <summary>One product of the Shop. Pure data: the cards bind to it (ShopPackCardView.Bind), ShopService reads
    /// its rules (limits, offer timer, first-purchase bonus, campaign unlock) and grants its rewards. Prices are the
    /// display price in VND until a real store returns a localized price for <see cref="ProductId"/>.</summary>
    [CreateAssetMenu(menuName = "AlienDefense/Shop/Shop Item", fileName = "ShopItem")]
    public sealed class ShopItemData : ScriptableObject
    {
        [Header("Identity")]
        [SerializeField, Tooltip("Store product id (Google Play / App Store). Also the key of its purchase state in the save.")]
        private string _productId = "alien.product";

        [SerializeField]
        private ShopCategory _category;

        [SerializeField]
        private ShopCardLayout _layout;

        [SerializeField]
        private string _displayName = "Pack";

        [SerializeField, Tooltip("Small line under the title (\"One-time only\", \"Purchase and get:\").")]
        private string _subtitle;

        [SerializeField, TextArea(2, 4)]
        private string _description;

        [SerializeField, TextArea(1, 4), Tooltip("Benefit lines (one per line) for privilege cards / VIP.")]
        private string _benefits;

        [Header("Art")]
        [SerializeField, Tooltip("Hero / product picture.")]
        private Sprite _art;

        [SerializeField, Tooltip("Optional second picture (a character beside the product).")]
        private Sprite _secondaryArt;

        [SerializeField]
        private Color _theme = new Color(0.2f, 0.45f, 0.95f);

        [SerializeField]
        private Color _themeDark = new Color(0.1f, 0.2f, 0.6f);

        [Header("Price")]
        [SerializeField, Min(0)]
        private int _priceVnd = 99000;

        [SerializeField, Min(0), Tooltip("Crossed-out price. 0 hides it.")]
        private int _originalPriceVnd;

        [SerializeField, Min(0), Tooltip("Value badge on the corner ribbon (\"800%\"). 0 hides the ribbon.")]
        private int _discountPercent;

        [Header("Rules")]
        [SerializeField, Min(0), Tooltip("0 = unlimited. Limited-time offers count per offer cycle.")]
        private int _purchaseLimit;

        [SerializeField, Tooltip("Shows \"One-time only\" and becomes \"Purchased\" afterwards.")]
        private bool _oneTime;

        [SerializeField, Tooltip("Part of the rotating offer cycle (ShopCatalog.OfferCycleHours): countdown, refreshes with the cycle.")]
        private bool _limitedTime;

        [SerializeField, Tooltip("Gem rewards are doubled on the very first purchase of this product.")]
        private bool _firstPurchaseDouble;

        [SerializeField, Tooltip("Campaign level that must be unlocked before this can be bought. Empty = always.")]
        private string _unlockLevelId;

        [SerializeField, Tooltip("Shown on locked cards (\"Unlocks with Campaign 2\").")]
        private string _lockedLabel;

        [Header("Rewards")]
        [SerializeField]
        private ShopRewardData[] _rewards = new ShopRewardData[0];

        public string ProductId => _productId;
        public ShopCategory Category => _category;
        public ShopCardLayout Layout => _layout;
        public string DisplayName => _displayName;
        public string Subtitle => _subtitle;
        public string Description => _description;
        public string Benefits => _benefits;
        public Sprite Art => _art;
        public Sprite SecondaryArt => _secondaryArt;
        public Color Theme => _theme;
        public Color ThemeDark => _themeDark;
        public int PriceVnd => _priceVnd;
        public int OriginalPriceVnd => _originalPriceVnd;
        public int DiscountPercent => _discountPercent;
        public int PurchaseLimit => _purchaseLimit;
        public bool OneTime => _oneTime;
        public bool LimitedTime => _limitedTime;
        public bool FirstPurchaseDouble => _firstPurchaseDouble;
        public string UnlockLevelId => _unlockLevelId;
        public string LockedLabel => _lockedLabel;
        public int RewardCount => _rewards != null ? _rewards.Length : 0;
        public ShopRewardData GetReward(int index) => _rewards[index];

        /// <summary>Sum of the plain Gem rewards (before any first-purchase bonus).</summary>
        public int GemAmount
        {
            get
            {
                int total = 0;
                for (int i = 0; i < RewardCount; i++)
                {
                    if (_rewards[i].Type == ShopRewardType.Gems)
                    {
                        total += _rewards[i].Amount;
                    }
                }

                return total;
            }
        }

        /// <summary>Days of the product's own privilege (VIP days / privilege-card days), 0 when none.</summary>
        public int DurationDays
        {
            get
            {
                for (int i = 0; i < RewardCount; i++)
                {
                    if (_rewards[i].Type == ShopRewardType.VipDays || _rewards[i].Type == ShopRewardType.PrivilegeDays)
                    {
                        return _rewards[i].Amount;
                    }
                }

                return 0;
            }
        }

        /// <summary>Effective per-product limit: one-time packs are limited to one even if the field says 0.</summary>
        public int EffectiveLimit => _oneTime ? 1 : _purchaseLimit;
    }
}
