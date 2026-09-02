using AlienDefense.Save;

namespace AlienDefense.Towers
{
    /// <summary>Spends Coin (MetaCurrency) to permanently unlock a new tower type — the Defense screen's
    /// counterpart to TowerMetaUpgradeService (which levels up a tower already unlocked). Never mutates save
    /// data itself, only orchestrates PlayerProfileService's own validated mutators.</summary>
    public sealed class TowerUnlockService
    {
        private readonly PlayerProfileService _profileService;

        public TowerUnlockService(PlayerProfileService profileService)
        {
            _profileService = profileService;
        }

        public bool IsUnlocked(TowerDefinition definition)
        {
            return definition != null && _profileService.IsTowerUnlocked(definition.Id);
        }

        public bool TryUnlock(TowerDefinition definition)
        {
            if (definition == null || IsUnlocked(definition))
            {
                return false;
            }

            // A 0 UnlockCost means "free" — TrySpendMetaCurrency rejects spending exactly 0 (same all-or-nothing
            // guard as EconomyService.TrySpend), so a free unlock must skip the spend call entirely rather than
            // always failing.
            if (definition.UnlockCost > 0 && !_profileService.TrySpendMetaCurrency(definition.UnlockCost))
            {
                return false;
            }

            _profileService.UnlockTower(definition.Id);
            return true;
        }
    }
}
