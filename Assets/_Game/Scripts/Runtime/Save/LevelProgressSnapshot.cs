namespace AlienDefense.Save
{
    /// <summary>Read-only view of one level's saved progress. PlayerProfileService returns this instead of the
    /// mutable internal LevelProgressSaveData, so callers can never mutate save state directly.</summary>
    public readonly struct LevelProgressSnapshot
    {
        public string LevelId { get; }
        public bool IsCompleted { get; }
        public int BestStars { get; }
        public int CompletionCount { get; }
        public int BestRemainingBaseHealth { get; }
        public int BestRemainingHpPercent { get; }
        public bool ClearRewardClaimed { get; }
        public bool Hp50RewardClaimed { get; }
        public bool PerfectRewardClaimed { get; }

        public LevelProgressSnapshot(
            string levelId,
            bool isCompleted,
            int bestStars,
            int completionCount,
            int bestRemainingBaseHealth,
            int bestRemainingHpPercent = 0,
            bool clearRewardClaimed = false,
            bool hp50RewardClaimed = false,
            bool perfectRewardClaimed = false)
        {
            LevelId = levelId;
            IsCompleted = isCompleted;
            BestStars = bestStars;
            CompletionCount = completionCount;
            BestRemainingBaseHealth = bestRemainingBaseHealth;
            BestRemainingHpPercent = bestRemainingHpPercent;
            ClearRewardClaimed = clearRewardClaimed;
            Hp50RewardClaimed = hp50RewardClaimed;
            PerfectRewardClaimed = perfectRewardClaimed;
        }

        public static LevelProgressSnapshot NotStarted(string levelId)
        {
            return new LevelProgressSnapshot(levelId, false, 0, 0, 0, 0);
        }

        public static LevelProgressSnapshot From(LevelProgressSaveData data)
        {
            return new LevelProgressSnapshot(
                data.LevelId,
                data.IsCompleted,
                data.BestStars,
                data.CompletionCount,
                data.BestRemainingBaseHealth,
                data.BestRemainingHpPercent,
                data.ClearRewardClaimed,
                data.Hp50RewardClaimed,
                data.PerfectRewardClaimed);
        }

        public bool IsObjectiveRewardClaimed(AlienDefense.Meta.LevelObjectiveKind kind)
        {
            switch (kind)
            {
                case AlienDefense.Meta.LevelObjectiveKind.Clear:
                    return ClearRewardClaimed;
                case AlienDefense.Meta.LevelObjectiveKind.Hp50:
                    return Hp50RewardClaimed;
                case AlienDefense.Meta.LevelObjectiveKind.Perfect:
                    return PerfectRewardClaimed;
                default:
                    return false;
            }
        }
    }
}
