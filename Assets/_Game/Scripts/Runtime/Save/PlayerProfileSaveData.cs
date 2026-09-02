using System;
using System.Collections.Generic;

namespace AlienDefense.Save
{
    /// <summary>Root save document. Plain data only (no ScriptableObject/GameObject/MonoBehaviour references) so it
    /// can round-trip through JsonUtility and survive across app versions via SaveMigrationPipeline.</summary>
    [Serializable]
    public sealed class PlayerProfileSaveData
    {
        public int SaveVersion;
        public string ProfileId;
        public long CreatedUtcTicks;
        public long LastUpdatedUtcTicks;
        public string HighestUnlockedLevelId;
        public List<LevelProgressSaveData> LevelProgress = new List<LevelProgressSaveData>();
        public List<string> UnlockedTowerIds = new List<string>();
        public List<PermanentTowerUpgradeSaveData> TowerUpgrades = new List<PermanentTowerUpgradeSaveData>();
        public int MetaCurrency;
        public int Gems;
        public int VipTier;
        public DailyRewardSaveData DailyReward = new DailyRewardSaveData();
        public DailyQuestSaveData DailyQuest = new DailyQuestSaveData();
        public SettingsSaveData Settings = new SettingsSaveData();
        public TutorialProgressSaveData Tutorial = new TutorialProgressSaveData();
    }
}
