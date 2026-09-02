using AlienDefense.Save;

namespace AlienDefense.Economy
{
    /// <summary>Spends Gems for a Coin pack. Never mutates save data itself, only orchestrates
    /// PlayerProfileService's own validated mutators (TrySpendGems, AddMetaCurrency).</summary>
    public sealed class ShopExchangeService
    {
        private readonly PlayerProfileService _profileService;

        public ShopExchangeService(PlayerProfileService profileService)
        {
            _profileService = profileService;
        }

        public bool CanAfford(int packIndex)
        {
            return packIndex >= 0 && packIndex < ShopExchangeTable.Packs.Length
                && _profileService.Gems >= ShopExchangeTable.Packs[packIndex].GemCost;
        }

        public bool TryExchange(int packIndex)
        {
            if (packIndex < 0 || packIndex >= ShopExchangeTable.Packs.Length)
            {
                return false;
            }

            GemToCoinPack pack = ShopExchangeTable.Packs[packIndex];
            if (!_profileService.TrySpendGems(pack.GemCost))
            {
                return false;
            }

            _profileService.AddMetaCurrency(pack.CoinAmount);
            return true;
        }
    }
}
