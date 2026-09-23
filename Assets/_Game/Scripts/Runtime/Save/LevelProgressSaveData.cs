using System;

namespace AlienDefense.Save
{
    /// <summary>Serializable progress record for one level. Plain data only, no Unity Object references.</summary>
    [Serializable]
    public sealed class LevelProgressSaveData
    {
        public string LevelId;
        public bool IsCompleted;
        public int BestStars;
        public int CompletionCount;
        public int BestRemainingBaseHealth;
        /// <summary>Best remaining base HP as 0–100 percent. Monotonic; never downgraded on worse replays.</summary>
        public int BestRemainingHpPercent;
        public bool ClearRewardClaimed;
        public bool Hp50RewardClaimed;
        public bool PerfectRewardClaimed;

        /// <summary>The LevelDefinition's first-clear-only victory rewards have been paid out.</summary>
        public bool FirstClearRewardClaimed;
    }
}
