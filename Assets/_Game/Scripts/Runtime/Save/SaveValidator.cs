using System;
using System.Collections.Generic;
using UnityEngine;

namespace AlienDefense.Save
{
    /// <summary>Repairs a deserialized save in place (null lists, clamped ranges, duplicate entries) and reports
    /// whether the result is usable. Unknown Stable IDs are kept, never deleted, and only logged — content the
    /// catalog no longer recognizes today might exist again later, and destroying earned progress is worse than
    /// carrying an orphan entry.</summary>
    public static class SaveValidator
    {
        public static bool ValidateAndRepair(PlayerProfileSaveData data)
        {
            if (data == null)
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(data.ProfileId))
            {
                data.ProfileId = Guid.NewGuid().ToString("N");
            }

            if (data.CreatedUtcTicks <= 0)
            {
                data.CreatedUtcTicks = DateTime.UtcNow.Ticks;
            }

            if (data.LastUpdatedUtcTicks <= 0)
            {
                data.LastUpdatedUtcTicks = data.CreatedUtcTicks;
            }

            if (data.Settings == null)
            {
                data.Settings = new SettingsSaveData();
            }

            if (data.Tutorial == null)
            {
                data.Tutorial = new TutorialProgressSaveData();
            }

            if (data.DailyReward == null)
            {
                data.DailyReward = new DailyRewardSaveData();
            }

            if (data.DailyQuest == null)
            {
                data.DailyQuest = new DailyQuestSaveData();
            }

            if (data.Statistics == null)
            {
                data.Statistics = new PlayerStatisticsSaveData();
            }

            ProfileIdentityUtility.EnsureDisplayIdentity(data);

            RepairLevelProgress(data);
            RepairUnlockedTowers(data);
            RepairTowerUpgrades(data);
            RepairInventory(data);
            RepairSettings(data.Settings);

            if (data.Statistics.TotalTowerDamage < 0)
            {
                data.Statistics.TotalTowerDamage = 0;
            }

            if (data.Statistics.ZombiesKilled < 0)
            {
                data.Statistics.ZombiesKilled = 0;
            }

            if (data.Statistics.BossesKilled < 0)
            {
                data.Statistics.BossesKilled = 0;
            }

            if (data.MetaCurrency < 0)
            {
                data.MetaCurrency = 0;
            }

            if (data.Gems < 0)
            {
                data.Gems = 0;
            }

            if (data.VipTier < 0)
            {
                data.VipTier = 0;
            }

            if (data.DailyReward.StreakDay < 0 || data.DailyReward.StreakDay > 7)
            {
                data.DailyReward.StreakDay = 0;
            }

            return true;
        }

        private static void RepairLevelProgress(PlayerProfileSaveData data)
        {
            if (data.LevelProgress == null)
            {
                data.LevelProgress = new List<LevelProgressSaveData>();
                return;
            }

            var repaired = new List<LevelProgressSaveData>();
            var seenIds = new HashSet<string>();
            for (int i = 0; i < data.LevelProgress.Count; i++)
            {
                LevelProgressSaveData entry = data.LevelProgress[i];
                if (entry == null || string.IsNullOrWhiteSpace(entry.LevelId))
                {
                    Debug.LogWarning("[SaveValidator] Dropped a level progress entry with an empty LevelId.");
                    continue;
                }

                if (!seenIds.Add(entry.LevelId))
                {
                    Debug.LogWarning($"[SaveValidator] Dropped a duplicate level progress entry for '{entry.LevelId}'.");
                    continue;
                }

                entry.BestStars = Mathf.Clamp(entry.BestStars, 0, 3);
                if (entry.CompletionCount < 0)
                {
                    entry.CompletionCount = 0;
                }

                if (entry.BestRemainingBaseHealth < 0)
                {
                    entry.BestRemainingBaseHealth = 0;
                }

                entry.BestRemainingHpPercent = Mathf.Clamp(entry.BestRemainingHpPercent, 0, 100);

                repaired.Add(entry);
            }

            data.LevelProgress = repaired;
        }

        private static void RepairUnlockedTowers(PlayerProfileSaveData data)
        {
            if (data.UnlockedTowerIds == null)
            {
                data.UnlockedTowerIds = new List<string>();
                return;
            }

            var repaired = new List<string>();
            var seenIds = new HashSet<string>();
            for (int i = 0; i < data.UnlockedTowerIds.Count; i++)
            {
                string id = data.UnlockedTowerIds[i];
                if (string.IsNullOrWhiteSpace(id) || !seenIds.Add(id))
                {
                    continue;
                }

                repaired.Add(id);
            }

            data.UnlockedTowerIds = repaired;
        }

        private static void RepairTowerUpgrades(PlayerProfileSaveData data)
        {
            if (data.TowerUpgrades == null)
            {
                data.TowerUpgrades = new List<PermanentTowerUpgradeSaveData>();
                return;
            }

            var repaired = new List<PermanentTowerUpgradeSaveData>();
            var seenIds = new HashSet<string>();
            for (int i = 0; i < data.TowerUpgrades.Count; i++)
            {
                PermanentTowerUpgradeSaveData entry = data.TowerUpgrades[i];
                if (entry == null || string.IsNullOrWhiteSpace(entry.TowerId) || !seenIds.Add(entry.TowerId))
                {
                    continue;
                }

                if (entry.UpgradeLevel < 0)
                {
                    entry.UpgradeLevel = 0;
                }

                repaired.Add(entry);
            }

            data.TowerUpgrades = repaired;
        }

        private static void RepairInventory(PlayerProfileSaveData data)
        {
            if (data.Inventory == null)
            {
                data.Inventory = new List<MetaItemStackSaveData>();
                return;
            }

            var repaired = new List<MetaItemStackSaveData>();
            var seenIds = new HashSet<string>();
            for (int i = 0; i < data.Inventory.Count; i++)
            {
                MetaItemStackSaveData entry = data.Inventory[i];
                if (entry == null || string.IsNullOrWhiteSpace(entry.ItemId))
                {
                    continue;
                }

                if (entry.Amount < 0)
                {
                    entry.Amount = 0;
                }

                if (!seenIds.Add(entry.ItemId))
                {
                    // Merge duplicate stacks into the first occurrence.
                    for (int j = 0; j < repaired.Count; j++)
                    {
                        if (repaired[j].ItemId == entry.ItemId)
                        {
                            repaired[j].Amount += entry.Amount;
                            break;
                        }
                    }

                    continue;
                }

                repaired.Add(entry);
            }

            data.Inventory = repaired;
        }

        private static void RepairSettings(SettingsSaveData settings)
        {
            settings.MasterVolume = Mathf.Clamp01(settings.MasterVolume);
            settings.MusicVolume = Mathf.Clamp01(settings.MusicVolume);
            settings.SfxVolume = Mathf.Clamp01(settings.SfxVolume);

            if (settings.QualityLevel < 0)
            {
                settings.QualityLevel = 0;
            }

            if (settings.TargetFrameRate != 30 && settings.TargetFrameRate != 60)
            {
                settings.TargetFrameRate = 60;
            }
        }
    }
}
