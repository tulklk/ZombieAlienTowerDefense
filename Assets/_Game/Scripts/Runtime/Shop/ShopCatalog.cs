using System.Collections.Generic;
using UnityEngine;

namespace AlienDefense.Shop
{
    /// <summary>Every Shop product plus the shop-wide rules (offer cycle, VIP daily reward) and the default reward
    /// icons. Data only; ShopService applies it.</summary>
    [CreateAssetMenu(menuName = "AlienDefense/Shop/Shop Catalog", fileName = "ShopCatalog")]
    public sealed class ShopCatalog : ScriptableObject
    {
        [SerializeField]
        private ShopItemData[] _items = new ShopItemData[0];

        [Header("Rules")]
        [SerializeField, Min(1), Tooltip("Length of the local limited-offer cycle. Resource Packs count down to its end, " +
            "then refresh (their purchase limits reset).")]
        private int _offerCycleHours = 168;

        [SerializeField, Min(0), Tooltip("Gems a VIP player can claim once per (UTC) day.")]
        private int _vipDailyGems = 100;

        [Header("Default reward icons")]
        [SerializeField]
        private Sprite _gemIcon;

        [SerializeField]
        private Sprite _coinIcon;

        [SerializeField]
        private Sprite _constructionQueueIcon;

        [SerializeField]
        private Sprite _vipIcon;

        [SerializeField]
        private Sprite _privilegeIcon;

        [SerializeField]
        private ItemIcon[] _itemIcons = new ItemIcon[0];

        [System.Serializable]
        public struct ItemIcon
        {
            public string ItemId;
            public Sprite Icon;
            public string Name;
        }

        public int Count => _items != null ? _items.Length : 0;
        public ShopItemData Get(int index) => _items[index];
        public int OfferCycleHours => Mathf.Max(1, _offerCycleHours);
        public int VipDailyGems => Mathf.Max(0, _vipDailyGems);
        public Sprite GemIcon => _gemIcon;
        public Sprite CoinIcon => _coinIcon;

        public void GetItems(ShopCategory category, List<ShopItemData> into)
        {
            into.Clear();
            for (int i = 0; i < Count; i++)
            {
                if (_items[i] != null && _items[i].Category == category)
                {
                    into.Add(_items[i]);
                }
            }
        }

        public ShopItemData Find(string productId)
        {
            for (int i = 0; i < Count; i++)
            {
                if (_items[i] != null && _items[i].ProductId == productId)
                {
                    return _items[i];
                }
            }

            return null;
        }

        public Sprite GetIcon(in ShopRewardData reward)
        {
            if (reward.Icon != null)
            {
                return reward.Icon;
            }

            switch (reward.Type)
            {
                case ShopRewardType.Gems: return _gemIcon;
                case ShopRewardType.Coins: return _coinIcon;
                case ShopRewardType.ConstructionQueue: return _constructionQueueIcon;
                case ShopRewardType.VipDays: return _vipIcon;
                case ShopRewardType.PrivilegeDays: return _privilegeIcon;
            }

            for (int i = 0; i < _itemIcons.Length; i++)
            {
                if (_itemIcons[i].ItemId == reward.ItemId)
                {
                    return _itemIcons[i].Icon;
                }
            }

            return null;
        }

        public string GetName(in ShopRewardData reward)
        {
            if (!string.IsNullOrEmpty(reward.Label))
            {
                return reward.Label;
            }

            switch (reward.Type)
            {
                case ShopRewardType.Gems: return "Crystals";
                case ShopRewardType.Coins: return "Coins";
                case ShopRewardType.ConstructionQueue: return "Construction Queue";
                case ShopRewardType.VipDays: return "VIP days";
                case ShopRewardType.PrivilegeDays: return "Privilege days";
            }

            for (int i = 0; i < _itemIcons.Length; i++)
            {
                if (_itemIcons[i].ItemId == reward.ItemId && !string.IsNullOrEmpty(_itemIcons[i].Name))
                {
                    return _itemIcons[i].Name;
                }
            }

            return reward.ItemId;
        }
    }
}
