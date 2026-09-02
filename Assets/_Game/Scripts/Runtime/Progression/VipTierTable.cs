namespace AlienDefense.Progression
{
    /// <summary>One purchasable VIP tier: a one-time Gem cost for a permanent Coin-reward bonus. Placeholder
    /// numbers — 3 tiers, each strictly better and pricier than the last.</summary>
    public readonly struct VipTierInfo
    {
        public int Tier { get; }
        public int GemCost { get; }
        public float CoinBonusMultiplier { get; }

        public VipTierInfo(int tier, int gemCost, float coinBonusMultiplier)
        {
            Tier = tier;
            GemCost = gemCost;
            CoinBonusMultiplier = coinBonusMultiplier;
        }
    }

    public static class VipTierTable
    {
        public static readonly VipTierInfo[] Tiers =
        {
            new VipTierInfo(1, 100, 0.10f),
            new VipTierInfo(2, 300, 0.25f),
            new VipTierInfo(3, 800, 0.50f),
        };

        public static bool TryGetTier(int tier, out VipTierInfo info)
        {
            for (int i = 0; i < Tiers.Length; i++)
            {
                if (Tiers[i].Tier == tier)
                {
                    info = Tiers[i];
                    return true;
                }
            }

            info = default;
            return false;
        }

        /// <summary>The Coin bonus multiplier for a player's current tier (0 for tier 0 / no VIP).</summary>
        public static float GetMultiplierForTier(int currentTier)
        {
            float multiplier = 0f;
            for (int i = 0; i < Tiers.Length; i++)
            {
                if (Tiers[i].Tier <= currentTier)
                {
                    multiplier = Tiers[i].CoinBonusMultiplier;
                }
            }

            return multiplier;
        }
    }
}
