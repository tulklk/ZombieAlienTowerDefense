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

        public LevelProgressSnapshot(string levelId, bool isCompleted, int bestStars, int completionCount, int bestRemainingBaseHealth)
        {
            LevelId = levelId;
            IsCompleted = isCompleted;
            BestStars = bestStars;
            CompletionCount = completionCount;
            BestRemainingBaseHealth = bestRemainingBaseHealth;
        }

        public static LevelProgressSnapshot NotStarted(string levelId)
        {
            return new LevelProgressSnapshot(levelId, false, 0, 0, 0);
        }

        public static LevelProgressSnapshot From(LevelProgressSaveData data)
        {
            return new LevelProgressSnapshot(data.LevelId, data.IsCompleted, data.BestStars, data.CompletionCount, data.BestRemainingBaseHealth);
        }
    }
}
