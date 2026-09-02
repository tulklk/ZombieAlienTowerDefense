using AlienDefense.Save;
using UnityEngine;

namespace AlienDefense.Towers
{
    /// <summary>Spends MetaCurrency to permanently raise a tower's saved upgrade level (PlayerProfileService's
    /// PermanentTowerUpgradeSaveData) — the meta-progression counterpart to TowerUpgradeService, which spends
    /// in-level EconomyService gold on a placed tower's in-match level instead. Both read the exact same
    /// TowerDefinition._levels curve: a tower's permanent level is simply the index it now starts a fresh build
    /// at (see TowerFactory/TowerController), so no separate stat curve had to be invented.</summary>
    public sealed class TowerMetaUpgradeService
    {
        private readonly PlayerProfileService _profileService;

        public TowerMetaUpgradeService(PlayerProfileService profileService)
        {
            _profileService = profileService;
        }

        public int GetCurrentLevelIndex(TowerDefinition definition)
        {
            if (definition == null || definition.LevelCount <= 0)
            {
                return 0;
            }

            int saved = _profileService.GetTowerUpgradeLevel(definition.Id);
            return Mathf.Clamp(saved, 0, definition.LevelCount - 1);
        }

        public bool IsMaxLevel(TowerDefinition definition)
        {
            return definition != null && GetCurrentLevelIndex(definition) >= definition.LevelCount - 1;
        }

        public bool TryGetNextLevelCost(TowerDefinition definition, out int cost)
        {
            if (definition == null)
            {
                cost = 0;
                return false;
            }

            int nextIndex = GetCurrentLevelIndex(definition) + 1;
            if (nextIndex >= definition.LevelCount)
            {
                cost = 0;
                return false;
            }

            cost = definition.GetLevel(nextIndex).UpgradeCost;
            return true;
        }

        public bool CanAffordNextLevel(TowerDefinition definition)
        {
            return TryGetNextLevelCost(definition, out int cost) && _profileService.MetaCurrency >= cost;
        }

        /// <summary>Validates unlock + affordability, spends MetaCurrency, and saves the new level — all-or-nothing.</summary>
        public bool TryUpgrade(TowerDefinition definition)
        {
            if (definition == null || !_profileService.IsTowerUnlocked(definition.Id))
            {
                return false;
            }

            if (!TryGetNextLevelCost(definition, out int cost))
            {
                return false;
            }

            if (!_profileService.TrySpendMetaCurrency(cost))
            {
                return false;
            }

            int nextLevel = GetCurrentLevelIndex(definition) + 1;
            _profileService.SetTowerUpgradeLevel(definition.Id, nextLevel);
            return true;
        }
    }
}
