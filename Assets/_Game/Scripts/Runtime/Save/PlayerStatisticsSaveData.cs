using System;

namespace AlienDefense.Save
{
    /// <summary>Career statistics that persist across sessions. Updated in memory during a match; flushed with the
    /// normal save debounce / level-end / app-pause path — never written to disk per hit.</summary>
    [Serializable]
    public sealed class PlayerStatisticsSaveData
    {
        public long TotalTowerDamage;
        public int ZombiesKilled;
        public int BossesKilled;
    }
}
