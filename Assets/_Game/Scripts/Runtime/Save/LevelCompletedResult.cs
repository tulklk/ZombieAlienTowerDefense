namespace AlienDefense.Save
{
    /// <summary>Outcome of a finished level, computed entirely by the gameplay domain (LevelCompositionRoot).
    /// PlayerProfileService only validates, clamps, and stores this — it never computes stars itself.</summary>
    public readonly struct LevelCompletedResult
    {
        public string LevelId { get; }
        public int Stars { get; }
        public int RemainingBaseHealth { get; }

        public LevelCompletedResult(string levelId, int stars, int remainingBaseHealth)
        {
            LevelId = levelId;
            Stars = stars;
            RemainingBaseHealth = remainingBaseHealth;
        }
    }
}
