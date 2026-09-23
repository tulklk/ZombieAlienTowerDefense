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
        public List<MetaItemStackSaveData> Inventory = new List<MetaItemStackSaveData>();
        public int MetaCurrency;
        public int Gems;

        /// <summary>Lifetime player XP earned from level rewards.</summary>
        public int PlayerExperience;

        /// <summary>Lobby energy spent to start a level, as of <see cref="PlayEnergyUpdatedUtcTicks"/>. Regeneration since
        /// then is computed from the clock (PlayEnergyService), never ticked into the save.</summary>
        public int PlayEnergy;

        /// <summary>0 = never initialised (an older save or a new profile): the player starts with a full bar.</summary>
        public long PlayEnergyUpdatedUtcTicks;

        /// <summary>Most recent victory reward transactions (level id + run id). A run whose id is already here
        /// has been paid, so a repeated victory callback can never pay it twice. Capped, oldest dropped.</summary>
        public List<string> RewardTransactions = new List<string>();
        public int VipTier;
        public string DisplayName;
        public int AvatarId;
        public PlayerStatisticsSaveData Statistics = new PlayerStatisticsSaveData();
        public DailyRewardSaveData DailyReward = new DailyRewardSaveData();
        public DailyQuestSaveData DailyQuest = new DailyQuestSaveData();
        public SettingsSaveData Settings = new SettingsSaveData();
        public TutorialProgressSaveData Tutorial = new TutorialProgressSaveData();
    }
}
