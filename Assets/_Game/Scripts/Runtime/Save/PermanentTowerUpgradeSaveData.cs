using System;

namespace AlienDefense.Save
{
    /// <summary>Serializable permanent (meta-progression) upgrade level for one tower. Foundation only; no
    /// gameplay system reads this yet.</summary>
    [Serializable]
    public sealed class PermanentTowerUpgradeSaveData
    {
        public string TowerId;
        public int UpgradeLevel;
    }
}
