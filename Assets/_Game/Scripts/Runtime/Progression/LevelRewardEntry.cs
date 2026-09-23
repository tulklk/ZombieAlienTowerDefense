using System;
using UnityEngine;

namespace AlienDefense.Progression
{
    /// <summary>How often a configured reward can be earned.</summary>
    public enum LevelRewardGrantRule
    {
        /// <summary>Granted on every winning run (once per run).</summary>
        EveryClear = 0,

        /// <summary>Granted only the first time the level is cleared; saved on the profile.</summary>
        FirstClearOnly = 1,
    }

    /// <summary>The base-HP result a run must reach for a reward - the same Clear / 50% / Perfect tiers as the
    /// MainMenu level objectives.</summary>
    public enum LevelRewardRequirement
    {
        AnyClear = 0,
        RemainingHp50 = 1,
        PerfectClear = 2,
    }

    /// <summary>One victory reward a LevelDefinition hands out. Amounts are real item counts; the panel only
    /// formats them (5800 -> 5.8K).</summary>
    [Serializable]
    public struct LevelRewardEntry
    {
        public VictoryRewardType Type;

        [Min(0)]
        public int Amount;

        public LevelRewardGrantRule Grant;

        public LevelRewardRequirement Requirement;

        public LevelRewardEntry(VictoryRewardType type, int amount,
            LevelRewardGrantRule grant = LevelRewardGrantRule.EveryClear,
            LevelRewardRequirement requirement = LevelRewardRequirement.AnyClear)
        {
            Type = type;
            Amount = amount;
            Grant = grant;
            Requirement = requirement;
        }

        public bool IsMetBy(int remainingHpPercent)
        {
            switch (Requirement)
            {
                case LevelRewardRequirement.RemainingHp50:
                    return remainingHpPercent >= 50;
                case LevelRewardRequirement.PerfectClear:
                    return remainingHpPercent >= 100;
                default:
                    return true;
            }
        }
    }
}
