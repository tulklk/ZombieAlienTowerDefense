using AlienDefense.Save;
using UnityEngine;

namespace AlienDefense.Towers
{
    /// <summary>Why a permanent level-up did or did not go through.</summary>
    public enum MetaUpgradeResult
    {
        Success = 0,
        Invalid = 1,
        Locked = 2,
        MaxLevel = 3,
        InsufficientCoins = 4,
        InsufficientCards = 5,
    }

    /// <summary>The permanent ("Defense tab") level of a tower type: spends Coins plus the tower's upgrade cards to
    /// raise a saved level (PlayerProfileService's PermanentTowerUpgradeSaveData) whose only effect is a shot damage
    /// multiplier (DefenseMetaConfig.GetTowerDamageMultiplier) on every tower of that type.
    ///
    /// It is deliberately independent of the in-level upgrade (TowerUpgradeService / TowerDefinition._levels): in a
    /// level a tower is always bought at its lowest level and upgraded there with in-level resources (user,
    /// 2026-10-12 - until then the permanent level was the in-level level a tower started at). Its own curve - how
    /// many levels, what each costs - lives in DefenseMetaConfig; without a config there is nothing to upgrade.</summary>
    public sealed class TowerMetaUpgradeService
    {
        private readonly PlayerProfileService _profileService;
        private readonly DefenseMetaConfig _config;

        public TowerMetaUpgradeService(PlayerProfileService profileService, DefenseMetaConfig config = null)
        {
            _profileService = profileService;
            _config = config;
        }

        private int MaxLevelIndex => _config != null ? _config.TowerMaxLevelIndex : 0;

        /// <summary>Shot damage multiplier this tower type currently gets in every level (1 = none).</summary>
        public float GetDamageMultiplier(TowerDefinition definition)
        {
            return _config != null ? _config.GetTowerDamageMultiplier(GetCurrentLevelIndex(definition)) : 1f;
        }

        public int GetCurrentLevelIndex(TowerDefinition definition)
        {
            if (definition == null)
            {
                return 0;
            }

            int saved = _profileService.GetTowerUpgradeLevel(definition.Id);
            return Mathf.Clamp(saved, 0, MaxLevelIndex);
        }

        public bool IsMaxLevel(TowerDefinition definition)
        {
            return definition != null && GetCurrentLevelIndex(definition) >= MaxLevelIndex;
        }

        public bool TryGetNextLevelCost(TowerDefinition definition, out int cost)
        {
            return TryGetNextLevelCost(definition, out cost, out _);
        }

        /// <summary>Coins and upgrade cards the next permanent level costs; false at max level.</summary>
        public bool TryGetNextLevelCost(TowerDefinition definition, out int coins, out int cards)
        {
            coins = 0;
            cards = 0;
            if (definition == null)
            {
                return false;
            }

            if (_config == null || !_config.TryGetTowerLevelCost(GetCurrentLevelIndex(definition), out DefenseMetaConfig.BaseLevelCost cost))
            {
                return false;
            }

            coins = cost.Coins;
            cards = string.IsNullOrEmpty(definition.UpgradeCardItemId) ? 0 : cost.Cards;
            return true;
        }

        /// <summary>Upgrade cards of this tower's kind the player owns.</summary>
        public int GetOwnedCards(TowerDefinition definition)
        {
            return definition == null || string.IsNullOrEmpty(definition.UpgradeCardItemId)
                ? 0
                : _profileService.GetItemAmount(definition.UpgradeCardItemId);
        }

        public bool CanAffordNextLevel(TowerDefinition definition)
        {
            return CheckUpgrade(definition) == MetaUpgradeResult.Success;
        }

        /// <summary>What TryUpgrade would answer right now, without spending anything.</summary>
        public MetaUpgradeResult CheckUpgrade(TowerDefinition definition)
        {
            if (definition == null)
            {
                return MetaUpgradeResult.Invalid;
            }

            if (!_profileService.IsTowerUnlocked(definition.Id))
            {
                return MetaUpgradeResult.Locked;
            }

            if (!TryGetNextLevelCost(definition, out int coins, out int cards))
            {
                return MetaUpgradeResult.MaxLevel;
            }

            if (_profileService.MetaCurrency < coins)
            {
                return MetaUpgradeResult.InsufficientCoins;
            }

            return cards > 0 && GetOwnedCards(definition) < cards ? MetaUpgradeResult.InsufficientCards : MetaUpgradeResult.Success;
        }

        /// <summary>Validates unlock + affordability, spends MetaCurrency, and saves the new level — all-or-nothing.</summary>
        public bool TryUpgrade(TowerDefinition definition)
        {
            return TryUpgradeWithResult(definition) == MetaUpgradeResult.Success;
        }

        /// <summary>All-or-nothing: checks everything first, then spends Coins and cards and saves the new level in
        /// one batched write.</summary>
        public MetaUpgradeResult TryUpgradeWithResult(TowerDefinition definition)
        {
            MetaUpgradeResult check = CheckUpgrade(definition);
            if (check != MetaUpgradeResult.Success)
            {
                return check;
            }

            TryGetNextLevelCost(definition, out int coins, out int cards);
            int nextLevel = GetCurrentLevelIndex(definition) + 1;

            _profileService.BeginBatch();
            try
            {
                // A 0 cost is "free": TrySpendMetaCurrency / TrySpendItem reject spending exactly 0.
                if (coins > 0 && !_profileService.TrySpendMetaCurrency(coins))
                {
                    return MetaUpgradeResult.InsufficientCoins;
                }

                if (cards > 0 && !_profileService.TrySpendItem(definition.UpgradeCardItemId, cards))
                {
                    if (coins > 0)
                    {
                        _profileService.AddMetaCurrency(coins); // cannot happen after CheckUpgrade; never half-charge
                    }

                    return MetaUpgradeResult.InsufficientCards;
                }

                _profileService.SetTowerUpgradeLevel(definition.Id, nextLevel);
            }
            finally
            {
                _profileService.EndBatch();
            }

            return MetaUpgradeResult.Success;
        }

        /// <summary>Permanent upgrade levels owned across the catalog (level index summed) - what the collection's
        /// crit damage bonus scales with.</summary>
        public int GetTotalUpgradeLevels(TowerCatalog catalog)
        {
            if (catalog == null)
            {
                return 0;
            }

            int total = 0;
            for (int i = 0; i < catalog.Count; i++)
            {
                TowerDefinition tower = catalog.GetTower(i);
                if (tower != null && _profileService.IsTowerUnlocked(tower.Id))
                {
                    total += GetCurrentLevelIndex(tower);
                }
            }

            return total;
        }
    }
}
