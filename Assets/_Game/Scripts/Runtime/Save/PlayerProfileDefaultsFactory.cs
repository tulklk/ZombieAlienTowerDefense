using System;
using System.Collections.Generic;

namespace AlienDefense.Save
{
    /// <summary>Builds a brand-new PlayerProfileSaveData from PlayerProfileDefaults + the caller-resolved first
    /// level id. Never references LevelCatalog/ScriptableObject data beyond PlayerProfileDefaults itself.</summary>
    public static class PlayerProfileDefaultsFactory
    {
        public static PlayerProfileSaveData CreateDefault(PlayerProfileDefaults defaults, string firstLevelId)
        {
            long nowTicks = DateTime.UtcNow.Ticks;

            return new PlayerProfileSaveData
            {
                SaveVersion = SaveConstants.CurrentSaveVersion,
                ProfileId = Guid.NewGuid().ToString("N"),
                CreatedUtcTicks = nowTicks,
                LastUpdatedUtcTicks = nowTicks,
                HighestUnlockedLevelId = firstLevelId,
                LevelProgress = new List<LevelProgressSaveData>(),
                UnlockedTowerIds = defaults != null ? new List<string>(defaults.DefaultUnlockedTowerIds) : new List<string>(),
                TowerUpgrades = new List<PermanentTowerUpgradeSaveData>(),
                MetaCurrency = 0,
                Gems = 0,
                VipTier = 0,
                DisplayName = null,
                AvatarId = 0,
                Statistics = new PlayerStatisticsSaveData(),
                Settings = defaults != null ? defaults.CreateDefaultSettings() : new SettingsSaveData(),
                Tutorial = new TutorialProgressSaveData()
            };
        }
    }
}
