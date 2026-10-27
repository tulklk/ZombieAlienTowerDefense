using System;
using UnityEngine;

namespace AlienDefense.Shop
{
    /// <summary>One reward line of a shop product.</summary>
    [Serializable]
    public struct ShopRewardData
    {
        public ShopRewardType Type;

        [Tooltip("Inventory item id for Item rewards (MetaItemIds), ignored otherwise.")]
        public string ItemId;

        [Min(1)]
        public int Amount;

        [Tooltip("Shown on the reward tile. Leave empty to use the catalog's default icon for this type / item.")]
        public Sprite Icon;

        [Tooltip("Optional small tag on top of the tile (e.g. \"1K\", \"Lv2\").")]
        public string Tag;

        [Tooltip("Shown in the reward popup. Leave empty for the type / item name.")]
        public string Label;
    }
}
