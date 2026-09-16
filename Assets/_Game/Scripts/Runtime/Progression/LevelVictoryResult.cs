using System.Collections.Generic;
using AlienDefense.Combat;

namespace AlienDefense.Progression
{
    /// <summary>What a level handed out on a win. New reward kinds (upgrade cards, blueprints, ...) are added here
    /// and given an entry in VictoryRewardCatalog; the victory panel itself needs no change.</summary>
    public enum VictoryRewardType
    {
        Coins = 0,
        Gems = 1,
        Experience = 2,
    }

    public readonly struct VictoryReward
    {
        public readonly VictoryRewardType Type;
        public readonly int Amount;

        public VictoryReward(VictoryRewardType type, int amount)
        {
            Type = type;
            Amount = amount;
        }
    }

    /// <summary>Everything the victory panel shows, built once by LevelCompositionRoot when the level is won: the
    /// rewards that were actually granted (never granted again by the UI), the star result, and this match's damage
    /// breakdown. The panel is a pure reader of this - it never searches the scene for gameplay state.</summary>
    public sealed class LevelVictoryResult
    {
        public string LevelId { get; }

        /// <summary>Ready to print, e.g. "CAMPAIGN LEVEL 2".</summary>
        public string LevelDisplayName { get; }

        public int Stars { get; }
        public bool IsPerfectClear { get; }
        public int RemainingBaseHealth { get; }
        public int MaxBaseHealth { get; }
        public IReadOnlyList<VictoryReward> Rewards { get; }
        public IReadOnlyList<CombatStatsService.Contributor> DamageSources { get; }
        public float TotalDamage { get; }

        /// <summary>True when there is another level to go to; the panel's Next button says so either way, but the
        /// navigation falls back to level selection when this is false.</summary>
        public bool HasNextLevel { get; }

        public LevelVictoryResult(
            string levelId,
            string levelDisplayName,
            int stars,
            bool isPerfectClear,
            int remainingBaseHealth,
            int maxBaseHealth,
            IReadOnlyList<VictoryReward> rewards,
            IReadOnlyList<CombatStatsService.Contributor> damageSources,
            float totalDamage,
            bool hasNextLevel)
        {
            LevelId = levelId;
            LevelDisplayName = levelDisplayName;
            Stars = stars;
            IsPerfectClear = isPerfectClear;
            RemainingBaseHealth = remainingBaseHealth;
            MaxBaseHealth = maxBaseHealth;
            Rewards = rewards ?? System.Array.Empty<VictoryReward>();
            DamageSources = damageSources ?? System.Array.Empty<CombatStatsService.Contributor>();
            TotalDamage = totalDamage;
            HasNextLevel = hasNextLevel;
        }
    }
}
