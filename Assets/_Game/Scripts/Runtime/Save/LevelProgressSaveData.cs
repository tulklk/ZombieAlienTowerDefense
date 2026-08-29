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
    }
}
