namespace AlienDefense.Economy
{
    /// <summary>Gem -> Coin exchange packs. This is the only "real" purchase Shop offers today — buying Gems
    /// with real money needs an actual store integration (Google Play Billing / Apple StoreKit) this project
    /// has no account/SDK for, so that section stays a visible, clearly-labeled, disabled placeholder
    /// (see ShopScreenView) instead of a fake transaction.</summary>
    public readonly struct GemToCoinPack
    {
        public int GemCost { get; }
        public int CoinAmount { get; }

        public GemToCoinPack(int gemCost, int coinAmount)
        {
            GemCost = gemCost;
            CoinAmount = coinAmount;
        }
    }

    public static class ShopExchangeTable
    {
        public static readonly GemToCoinPack[] Packs =
        {
            new GemToCoinPack(10, 100),
            new GemToCoinPack(40, 500),
            new GemToCoinPack(70, 1000),
        };
    }
}
