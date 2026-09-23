using System.Collections.Generic;
using UnityEngine;

namespace AlienDefense.Progression
{
    /// <summary>What a level can hand out on a win. The numbers are persisted in LevelDefinition assets, so existing
    /// values never change meaning - new kinds are only ever appended. Each kind needs an entry in
    /// VictoryRewardCatalog (icon + popup text) and a sink in LevelRewardService; the victory panel needs no change.</summary>
    public enum VictoryRewardType
    {
        Coins = 0,
        Gems = 1,

        /// <summary>Player XP, saved on the profile (PlayerProfileService.PlayerExperience).</summary>
        Experience = 2,

        UfoBaseCard = 3,
        BlasterCard = 4,
        MortarCard = 5,
        FrostCard = 6,
        TeslaCard = 7,
        ReactorBlueprint = 8,
        AntiGravityBlueprint = 9,
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

    /// <summary>One line of the match's damage breakdown, frozen when the level is won. Damage is what actually
    /// came off enemy health (overkill already excluded by EnemyHealth).</summary>
    public readonly struct DamageResultEntry
    {
        public readonly string Name;
        public readonly Sprite Icon;
        public readonly float Damage;

        /// <summary>The tower's permanent upgrade tier (1-based) out of <see cref="MaxStars"/>. 0 = this source has
        /// no tier (the UFO, a skill), and the row hides its stars.</summary>
        public readonly int Stars;
        public readonly int MaxStars;

        public DamageResultEntry(string name, Sprite icon, float damage, int stars = 0, int maxStars = 0)
        {
            Name = name;
            Icon = icon;
            Damage = damage;
            MaxStars = Mathf.Max(0, maxStars);
            Stars = Mathf.Clamp(stars, 0, MaxStars);
        }

        /// <summary>0..1 share of <paramref name="total"/>; 0 (never NaN) when nothing was dealt.</summary>
        public static float ShareOf(float damage, float total)
        {
            return total > 0f && damage > 0f ? Mathf.Clamp01(damage / total) : 0f;
        }
    }

    /// <summary>Everything the victory panel shows, built once by LevelCompositionRoot when the level is won: the
    /// rewards that were actually granted (never granted again by the UI), the base-HP result, and this match's
    /// damage breakdown. The panel is a pure reader of this - it never searches the scene for gameplay state.</summary>
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

        /// <summary>Sorted by damage, biggest first.</summary>
        public IReadOnlyList<DamageResultEntry> DamageSources { get; }
        public float TotalDamage { get; }

        /// <summary>True when there is another level to go to.</summary>
        public bool HasNextLevel { get; }

        /// <summary>Remaining base HP as a whole 0-100 percent (100 only when the base is untouched).</summary>
        public int RemainingHpPercent
        {
            get
            {
                if (MaxBaseHealth <= 0)
                {
                    return IsPerfectClear ? 100 : 0;
                }

                if (RemainingBaseHealth >= MaxBaseHealth)
                {
                    return 100;
                }

                // Rounded like the saved objective percent, but capped at 99 so a damaged base never reads "100%".
                return Mathf.Clamp(Mathf.RoundToInt(100f * RemainingBaseHealth / MaxBaseHealth), 0, 99);
            }
        }

        public LevelVictoryResult(
            string levelId,
            string levelDisplayName,
            int stars,
            bool isPerfectClear,
            int remainingBaseHealth,
            int maxBaseHealth,
            IReadOnlyList<VictoryReward> rewards,
            IReadOnlyList<DamageResultEntry> damageSources,
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
            DamageSources = damageSources ?? System.Array.Empty<DamageResultEntry>();
            TotalDamage = Mathf.Max(0f, totalDamage);
            HasNextLevel = hasNextLevel;
        }
    }
}
