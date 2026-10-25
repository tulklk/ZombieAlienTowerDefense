using AlienDefense.Save;
using UnityEngine;

namespace AlienDefense.Towers
{
    /// <summary>The permanent UFO Base level (Defense tab, user-approved 2026-10-12): each level raises every level's
    /// BaseMaxHealth by DefenseMetaConfig.BaseHealthBonusPerLevel and costs Coins plus UFO cards. Same shape and
    /// same all-or-nothing rule as TowerMetaUpgradeService; the level itself lives in PlayerProfileSaveData.</summary>
    public sealed class BaseDefenseUpgradeService
    {
        private readonly PlayerProfileService _profile;
        private readonly DefenseMetaConfig _config;

        public BaseDefenseUpgradeService(PlayerProfileService profile, DefenseMetaConfig config)
        {
            _profile = profile;
            _config = config;
        }

        public bool IsAvailable => _profile != null && _config != null;

        public int CurrentLevelIndex => IsAvailable ? Mathf.Clamp(_profile.BaseDefenseLevel, 0, _config.BaseMaxLevelIndex) : 0;

        public bool IsMaxLevel => !IsAvailable || CurrentLevelIndex >= _config.BaseMaxLevelIndex;

        public int OwnedCards => IsAvailable ? _profile.GetItemAmount(_config.BaseCardItemId) : 0;

        /// <summary>BaseMaxHealth of a level after the permanent Base level is applied.</summary>
        public int ApplyTo(int levelBaseMaxHealth)
        {
            return ApplyAtLevel(levelBaseMaxHealth, CurrentLevelIndex);
        }

        public int ApplyAtLevel(int levelBaseMaxHealth, int levelIndex)
        {
            return _config == null
                ? levelBaseMaxHealth
                : Mathf.Max(1, Mathf.RoundToInt(levelBaseMaxHealth * _config.GetBaseHealthMultiplier(levelIndex)));
        }

        public bool TryGetNextLevelCost(out int coins, out int cards)
        {
            coins = 0;
            cards = 0;
            if (!IsAvailable || !_config.TryGetBaseLevelCost(CurrentLevelIndex, out DefenseMetaConfig.BaseLevelCost cost))
            {
                return false;
            }

            coins = cost.Coins;
            cards = cost.Cards;
            return true;
        }

        public MetaUpgradeResult CheckUpgrade()
        {
            if (!IsAvailable)
            {
                return MetaUpgradeResult.Invalid;
            }

            if (!TryGetNextLevelCost(out int coins, out int cards))
            {
                return MetaUpgradeResult.MaxLevel;
            }

            if (_profile.MetaCurrency < coins)
            {
                return MetaUpgradeResult.InsufficientCoins;
            }

            return cards > 0 && OwnedCards < cards ? MetaUpgradeResult.InsufficientCards : MetaUpgradeResult.Success;
        }

        public MetaUpgradeResult TryUpgrade()
        {
            MetaUpgradeResult check = CheckUpgrade();
            if (check != MetaUpgradeResult.Success)
            {
                return check;
            }

            TryGetNextLevelCost(out int coins, out int cards);
            int next = CurrentLevelIndex + 1;

            _profile.BeginBatch();
            try
            {
                if (coins > 0 && !_profile.TrySpendMetaCurrency(coins))
                {
                    return MetaUpgradeResult.InsufficientCoins;
                }

                if (cards > 0 && !_profile.TrySpendItem(_config.BaseCardItemId, cards))
                {
                    if (coins > 0)
                    {
                        _profile.AddMetaCurrency(coins);
                    }

                    return MetaUpgradeResult.InsufficientCards;
                }

                _profile.SetBaseDefenseLevel(next);
            }
            finally
            {
                _profile.EndBatch();
            }

            return MetaUpgradeResult.Success;
        }
    }
}
