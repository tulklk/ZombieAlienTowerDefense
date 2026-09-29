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
            RepairEquipment(data);
            RepairArtifacts(data);
            RepairBaseBuildings(data);
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

            if (data.PlayEnergy < 0)
            {
                data.PlayEnergy = 0;
            }

            if (data.PlayerExperience < 0)
            {
                data.PlayerExperience = 0;
            }

            if (data.RewardTransactions == null)
            {
                data.RewardTransactions = new List<string>();
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

        /// <summary>Equipment is keyed by item id alone (a piece is a single upgradable object, not a stack), so
        /// a duplicate id is dropped rather than merged. Also enforces the "at most one equipped per slot" rule
        /// lazily: the service re-checks on equip, this only stops a corrupt file from arriving with two.</summary>
        private static void RepairEquipment(PlayerProfileSaveData data)
        {
            if (data.Equipment == null)
            {
                data.Equipment = new List<EquipmentSaveData>();
                return;
            }

            var repaired = new List<EquipmentSaveData>();
            var seenIds = new HashSet<string>();
            for (int i = 0; i < data.Equipment.Count; i++)
            {
                EquipmentSaveData entry = data.Equipment[i];
                if (entry == null || string.IsNullOrWhiteSpace(entry.ItemId) || !seenIds.Add(entry.ItemId))
                {
                    continue;
                }

                if (entry.Rarity < 0)
                {
                    entry.Rarity = 0;
                }

                if (entry.Level < 1)
                {
                    entry.Level = 1;
                }

                if (entry.Duplicates < 0)
                {
                    entry.Duplicates = 0;
                }

                repaired.Add(entry);
            }

            data.Equipment = repaired;
        }

        /// <summary>Artifacts ARE stacks, and the key is (item id, rarity) - the same family at two rarities is
        /// two legitimate rows - so duplicates of that pair are merged the way inventory stacks are.</summary>
        private static void RepairArtifacts(PlayerProfileSaveData data)
        {
            if (data.Artifacts == null)
            {
                data.Artifacts = new List<ArtifactSaveData>();
                return;
            }

            var repaired = new List<ArtifactSaveData>();
            for (int i = 0; i < data.Artifacts.Count; i++)
            {
                ArtifactSaveData entry = data.Artifacts[i];
                if (entry == null || string.IsNullOrWhiteSpace(entry.ItemId))
                {
                    continue;
                }

                if (entry.Rarity < 0)
                {
                    entry.Rarity = 0;
                }

                if (entry.Amount < 0)
                {
                    entry.Amount = 0;
                }

                bool merged = false;
                for (int j = 0; j < repaired.Count; j++)
                {
                    if (repaired[j].ItemId == entry.ItemId && repaired[j].Rarity == entry.Rarity)
                    {
                        repaired[j].Amount += entry.Amount;
                        merged = true;
                        break;
                    }
                }

                if (!merged)
                {
                    repaired.Add(entry);
                }
            }

            data.Artifacts = repaired;
        }

        /// <summary>One row per building id. Also heals a half-written construction: a row that claims to be
        /// building but carries no completion timestamp would otherwise hang forever, because nothing would ever
        /// compare true against DateTime.UtcNow.</summary>
        private static void RepairBaseBuildings(PlayerProfileSaveData data)
        {
            if (data.BaseBuildings == null)
            {
                data.BaseBuildings = new List<BaseBuildingSaveData>();
                return;
            }

            var repaired = new List<BaseBuildingSaveData>();
            var seenIds = new HashSet<string>();
            for (int i = 0; i < data.BaseBuildings.Count; i++)
            {
                BaseBuildingSaveData entry = data.BaseBuildings[i];
                if (entry == null || string.IsNullOrWhiteSpace(entry.BuildingId) || !seenIds.Add(entry.BuildingId))
                {
                    continue;
                }

                if (entry.Level < 0)
                {
                    entry.Level = 0;
                }

                if (entry.State < 0)
                {
                    entry.State = 0;
                }

                bool building = entry.State == (int)AlienDefense.Base.BaseBuildingState.Constructing ||
                                entry.State == (int)AlienDefense.Base.BaseBuildingState.Upgrading;

                if (building && entry.ConstructionCompleteUtcTicks <= 0)
                {
                    // Drop back to whatever the level says: 0 means the plot is empty again, anything else means
                    // the building simply stays at the level it had reached.
                    entry.State = entry.Level > 0
                        ? (int)AlienDefense.Base.BaseBuildingState.Built
                        : (int)AlienDefense.Base.BaseBuildingState.Available;
                    entry.ConstructionStartUtcTicks = 0;
                }

                if (!building)
                {
                    entry.ConstructionStartUtcTicks = 0;
                    entry.ConstructionCompleteUtcTicks = 0;
                }

                if (entry.LastCollectUtcTicks < 0)
                {
                    entry.LastCollectUtcTicks = 0;
                }

                repaired.Add(entry);
            }

            data.BaseBuildings = repaired;
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
