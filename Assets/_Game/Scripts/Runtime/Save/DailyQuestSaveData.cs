using System;

namespace AlienDefense.Save
{
    /// <summary>Serializable state for the single daily quest ("Complete 1 level today"). DayResetUtcTicks=0
    /// means no quest day has ever been started; otherwise it marks the UTC day this state belongs to.</summary>
    [Serializable]
    public sealed class DailyQuestSaveData
    {
        public long DayResetUtcTicks;
        public bool CompletedToday;
        public bool ClaimedToday;
    }
}
