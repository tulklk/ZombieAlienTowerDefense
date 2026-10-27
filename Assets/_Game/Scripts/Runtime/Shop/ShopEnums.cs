namespace AlienDefense.Shop
{
    /// <summary>The Shop's bottom category bar, in display order.</summary>
    public enum ShopCategory
    {
        ConstructionQueue = 0,
        CampaignPacks = 1,
        Vip = 2,
        WeeklyMonthlyCards = 3,
        ResourcePacks = 4,
        Crystals = 5,
    }

    /// <summary>What one reward line grants. Items cover every inventory stack the game has (tower cards, the UFO
    /// Base card, wood, Volt Drip, Evolution Coins, microcircuits...).</summary>
    public enum ShopRewardType
    {
        Gems = 0,
        Coins = 1,
        Item = 2,
        /// <summary>+Amount permanent construction queues (the Base can build that many more at once).</summary>
        ConstructionQueue = 3,
        /// <summary>Amount days of VIP (extends a running VIP).</summary>
        VipDays = 4,
        /// <summary>Amount days of this product's own privilege card (e.g. the Combat Monthly Card).</summary>
        PrivilegeDays = 5,
    }

    /// <summary>Which card prefab shows a product.</summary>
    public enum ShopCardLayout
    {
        LargeFeature = 0,
        Campaign = 1,
        Vip = 2,
        Privilege = 3,
        Resource = 4,
        Crystal = 5,
    }

    public enum ShopItemState
    {
        Available = 0,
        /// <summary>Purchase limit reached (one-time packs, limited offers this cycle).</summary>
        SoldOut = 1,
        /// <summary>Needs campaign progress first (or the campaign does not exist yet).</summary>
        Locked = 2,
        /// <summary>A limited offer whose countdown ran out; refreshed with the next offer cycle.</summary>
        Expired = 3,
        /// <summary>A privilege card that is running (cannot be bought again until it ends).</summary>
        Active = 4,
    }
}
