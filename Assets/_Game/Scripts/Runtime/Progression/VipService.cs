using AlienDefense.Save;

namespace AlienDefense.Progression
{
    /// <summary>Spends Gems to permanently raise VipTier. Never mutates save data itself — only orchestrates
    /// PlayerProfileService's own validated mutators (TrySpendGems, SetVipTier), same shape as
    /// TowerMetaUpgradeService.</summary>
    public sealed class VipService
    {
        private readonly PlayerProfileService _profileService;

        public VipService(PlayerProfileService profileService)
        {
            _profileService = profileService;
        }

        public int CurrentTier => _profileService.VipTier;
        public float CurrentCoinBonusMultiplier => VipTierTable.GetMultiplierForTier(CurrentTier);

        public bool TryPurchase(int tier)
        {
            if (tier <= CurrentTier || !VipTierTable.TryGetTier(tier, out VipTierInfo info))
            {
                return false;
            }

            if (!_profileService.TrySpendGems(info.GemCost))
            {
                return false;
            }

            _profileService.SetVipTier(tier);
            return true;
        }
    }
}
