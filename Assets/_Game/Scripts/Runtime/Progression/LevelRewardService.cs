using System.Collections.Generic;
using AlienDefense.Economy;
using AlienDefense.Meta;
using AlienDefense.Save;
using UnityEngine;

namespace AlienDefense.Progression
{
    /// <summary>The finished run a victory payout is computed for.</summary>
    public readonly struct LevelRewardContext
    {
        public readonly string LevelId;

        /// <summary>Unique per play-through of a level (a restart is a new run). Together with the level id it is
        /// the payout's transaction id.</summary>
        public readonly string RunId;

        public readonly int Stars;
        public readonly int RemainingHpPercent;

        /// <summary>Whether the level had never been completed before this run (read before the run is saved).</summary>
        public readonly bool IsFirstCompletion;

        public LevelRewardContext(string levelId, string runId, int stars, int remainingHpPercent, bool isFirstCompletion)
        {
            LevelId = levelId;
            RunId = runId;
            Stars = stars;
            RemainingHpPercent = remainingHpPercent;
            IsFirstCompletion = isFirstCompletion;
        }

        public string TransactionId => LevelId + "#" + RunId;
    }

    /// <summary>Pays a won level's rewards into the player's saved profile - coins to the wallet, XP to the
    /// profile's XP, cards and blueprints to the meta inventory - and returns exactly what was paid, which is what
    /// the victory panel then shows. It is the only place victory rewards are granted.
    ///
    /// Idempotent per run: the payout is recorded as a transaction (level id + run id) in the same save, so a
    /// victory callback that fires twice pays once. First-clear-only lines are additionally guarded by a
    /// per-level flag in the save, so they are never paid on a replay.
    ///
    /// What a level pays comes from its LevelDefinition (Victory Rewards). A level with no configured rewards keeps
    /// the original star-based formula (LevelRewardCalculator), so unconfigured levels are unchanged.</summary>
    public sealed class LevelRewardService
    {
        private readonly PlayerProfileService _profile;

        public LevelRewardService(PlayerProfileService profile)
        {
            _profile = profile;
        }

        /// <summary>Grants the run's rewards and appends them (merged per type, in configuration order) to
        /// <paramref name="granted"/>. Returns false, granting nothing, when there is no profile or this run was
        /// already paid.</summary>
        public bool TryGrantVictoryRewards(in LevelRewardContext context, IReadOnlyList<LevelRewardEntry> configured,
            List<VictoryReward> granted)
        {
            if (_profile == null || granted == null || string.IsNullOrEmpty(context.LevelId))
            {
                return false;
            }

            if (!_profile.TryRegisterRewardTransaction(context.TransactionId))
            {
                Debug.LogWarning($"[LevelRewardService] Run '{context.TransactionId}' was already paid; ignoring the repeat.");
                return false;
            }

            float vipCoinBonus = VipTierTable.GetMultiplierForTier(_profile.VipTier);
            bool configuresGems = false;

            _profile.BeginBatch();
            try
            {
                if (configured != null && configured.Count > 0)
                {
                    bool firstClearAvailable = !_profile.IsFirstClearRewardClaimed(context.LevelId);
                    for (int i = 0; i < configured.Count; i++)
                    {
                        LevelRewardEntry entry = configured[i];
                        configuresGems |= entry.Type == VictoryRewardType.Gems;
                        if (entry.Amount <= 0 || !entry.IsMetBy(context.RemainingHpPercent))
                        {
                            continue;
                        }

                        if (entry.Grant == LevelRewardGrantRule.FirstClearOnly && !firstClearAvailable)
                        {
                            continue;
                        }

                        int amount = entry.Type == VictoryRewardType.Coins
                            ? Mathf.RoundToInt(entry.Amount * (1f + Mathf.Max(0f, vipCoinBonus)))
                            : entry.Amount;
                        if (Grant(entry.Type, amount))
                        {
                            Merge(granted, entry.Type, amount);
                        }
                    }

                    // The first winning run spends the first-clear lines whether or not it met their HP requirement,
                    // exactly like a one-off "first clear" chest.
                    if (firstClearAvailable)
                    {
                        _profile.TryMarkFirstClearRewardClaimed(context.LevelId);
                    }
                }
                else
                {
                    int coins = LevelRewardCalculator.CalculateCoinReward(context.Stars, vipCoinBonus);
                    if (Grant(VictoryRewardType.Coins, coins))
                    {
                        Merge(granted, VictoryRewardType.Coins, coins);
                    }
                }

                // The rare first-time three-star gem bonus stays on top unless the level configures gems itself.
                if (!configuresGems)
                {
                    int gems = LevelRewardCalculator.CalculateGemReward(context.Stars, context.IsFirstCompletion);
                    if (gems > 0 && Grant(VictoryRewardType.Gems, gems))
                    {
                        Merge(granted, VictoryRewardType.Gems, gems);
                    }
                }
            }
            finally
            {
                _profile.EndBatch(); // one save for the transaction id and every line it paid
            }

            return true;
        }

        /// <summary>The meta inventory item a card/blueprint reward is stored as; null for currencies and XP.</summary>
        public static string GetItemId(VictoryRewardType type)
        {
            switch (type)
            {
                case VictoryRewardType.UfoBaseCard:
                    return MetaItemIds.CardUfo;
                case VictoryRewardType.BlasterCard:
                    return MetaItemIds.CardBlaster;
                case VictoryRewardType.MortarCard:
                    return MetaItemIds.CardMortar;
                case VictoryRewardType.FrostCard:
                    return MetaItemIds.CardFrost;
                case VictoryRewardType.TeslaCard:
                    return MetaItemIds.CardTesla;
                case VictoryRewardType.ReactorBlueprint:
                    return MetaItemIds.ReactorBlueprint;
                case VictoryRewardType.AntiGravityBlueprint:
                    return MetaItemIds.AntiGravityBlueprint;
                default:
                    return null;
            }
        }

        private bool Grant(VictoryRewardType type, int amount)
        {
            if (amount <= 0)
            {
                return false;
            }

            switch (type)
            {
                case VictoryRewardType.Coins:
                    _profile.AddMetaCurrency(amount);
                    return true;
                case VictoryRewardType.Gems:
                    _profile.AddGems(amount);
                    return true;
                case VictoryRewardType.Experience:
                    _profile.AddPlayerExperience(amount);
                    return true;
            }

            string itemId = GetItemId(type);
            if (string.IsNullOrEmpty(itemId))
            {
                Debug.LogError($"[LevelRewardService] Reward type {type} has no storage; it was not granted.");
                return false;
            }

            _profile.AddItem(itemId, amount);
            return true;
        }

        private static void Merge(List<VictoryReward> granted, VictoryRewardType type, int amount)
        {
            for (int i = 0; i < granted.Count; i++)
            {
                if (granted[i].Type == type)
                {
                    granted[i] = new VictoryReward(type, granted[i].Amount + amount);
                    return;
                }
            }

            granted.Add(new VictoryReward(type, amount));
        }
    }
}
