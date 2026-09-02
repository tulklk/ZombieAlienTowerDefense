using System;

namespace AlienDefense.Save
{
    /// <summary>Serializable daily-login-streak state. LastClaimUtcTicks=0 means never claimed.</summary>
    [Serializable]
    public sealed class DailyRewardSaveData
    {
        public long LastClaimUtcTicks;
        public int StreakDay;
    }
}
