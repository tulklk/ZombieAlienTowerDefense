using System.Collections.Generic;
using AlienDefense.Save;
using NUnit.Framework;

namespace AlienDefense.Tests.EditMode
{
    public class ProfileValidationTests
    {
        [Test]
        public void ValidateAndRepair_NullData_ReturnsFalse()
        {
            bool isValid = SaveValidator.ValidateAndRepair(null);

            Assert.IsFalse(isValid);
        }

        [Test]
        public void ValidateAndRepair_NullLists_AreReplacedWithEmptyLists()
        {
            var data = new PlayerProfileSaveData { ProfileId = "p1", LevelProgress = null, UnlockedTowerIds = null, TowerUpgrades = null, Settings = null, Tutorial = null };

            bool isValid = SaveValidator.ValidateAndRepair(data);

            Assert.IsTrue(isValid);
            Assert.IsNotNull(data.LevelProgress);
            Assert.IsNotNull(data.UnlockedTowerIds);
            Assert.IsNotNull(data.TowerUpgrades);
            Assert.IsNotNull(data.Settings);
            Assert.IsNotNull(data.Tutorial);
        }

        [Test]
        public void ValidateAndRepair_EmptyProfileId_GetsGenerated()
        {
            var data = new PlayerProfileSaveData { ProfileId = "" };

            SaveValidator.ValidateAndRepair(data);

            Assert.IsNotEmpty(data.ProfileId);
        }

        [Test]
        public void ValidateAndRepair_DuplicateLevelProgress_KeepsFirstOnly()
        {
            var data = new PlayerProfileSaveData
            {
                ProfileId = "p1",
                LevelProgress = new List<LevelProgressSaveData>
                {
                    new LevelProgressSaveData { LevelId = "level_01", BestStars = 2 },
                    new LevelProgressSaveData { LevelId = "level_01", BestStars = 3 }
                }
            };

            SaveValidator.ValidateAndRepair(data);

            Assert.AreEqual(1, data.LevelProgress.Count);
            Assert.AreEqual(2, data.LevelProgress[0].BestStars);
        }

        [Test]
        public void ValidateAndRepair_StarsOutOfRange_AreClamped()
        {
            var data = new PlayerProfileSaveData
            {
                ProfileId = "p1",
                LevelProgress = new List<LevelProgressSaveData>
                {
                    new LevelProgressSaveData { LevelId = "level_01", BestStars = 99 },
                    new LevelProgressSaveData { LevelId = "level_02", BestStars = -5 }
                }
            };

            SaveValidator.ValidateAndRepair(data);

            Assert.AreEqual(3, data.LevelProgress[0].BestStars);
            Assert.AreEqual(0, data.LevelProgress[1].BestStars);
        }

        [Test]
        public void ValidateAndRepair_NegativeCompletionCount_ClampsToZero()
        {
            var data = new PlayerProfileSaveData
            {
                ProfileId = "p1",
                LevelProgress = new List<LevelProgressSaveData> { new LevelProgressSaveData { LevelId = "level_01", CompletionCount = -3 } }
            };

            SaveValidator.ValidateAndRepair(data);

            Assert.AreEqual(0, data.LevelProgress[0].CompletionCount);
        }

        [Test]
        public void ValidateAndRepair_DuplicateUnlockedTowers_Deduplicated()
        {
            var data = new PlayerProfileSaveData { ProfileId = "p1", UnlockedTowerIds = new List<string> { "tower_blaster", "tower_blaster", "tower_rapid" } };

            SaveValidator.ValidateAndRepair(data);

            Assert.AreEqual(2, data.UnlockedTowerIds.Count);
        }

        [Test]
        public void ValidateAndRepair_DuplicateTowerUpgrades_KeepsFirstOnly()
        {
            var data = new PlayerProfileSaveData
            {
                ProfileId = "p1",
                TowerUpgrades = new List<PermanentTowerUpgradeSaveData>
                {
                    new PermanentTowerUpgradeSaveData { TowerId = "tower_blaster", UpgradeLevel = 1 },
                    new PermanentTowerUpgradeSaveData { TowerId = "tower_blaster", UpgradeLevel = 5 }
                }
            };

            SaveValidator.ValidateAndRepair(data);

            Assert.AreEqual(1, data.TowerUpgrades.Count);
            Assert.AreEqual(1, data.TowerUpgrades[0].UpgradeLevel);
        }

        [Test]
        public void ValidateAndRepair_NegativeUpgradeLevel_ClampsToZero()
        {
            var data = new PlayerProfileSaveData
            {
                ProfileId = "p1",
                TowerUpgrades = new List<PermanentTowerUpgradeSaveData> { new PermanentTowerUpgradeSaveData { TowerId = "tower_blaster", UpgradeLevel = -2 } }
            };

            SaveValidator.ValidateAndRepair(data);

            Assert.AreEqual(0, data.TowerUpgrades[0].UpgradeLevel);
        }

        [Test]
        public void ValidateAndRepair_SettingsOutOfRange_AreClamped()
        {
            var data = new PlayerProfileSaveData
            {
                ProfileId = "p1",
                Settings = new SettingsSaveData { MasterVolume = 5f, MusicVolume = -2f, SfxVolume = 1.5f, QualityLevel = -1, TargetFrameRate = 144 }
            };

            SaveValidator.ValidateAndRepair(data);

            Assert.AreEqual(1f, data.Settings.MasterVolume);
            Assert.AreEqual(0f, data.Settings.MusicVolume);
            Assert.AreEqual(1f, data.Settings.SfxVolume);
            Assert.AreEqual(0, data.Settings.QualityLevel);
            Assert.AreEqual(60, data.Settings.TargetFrameRate, "Invalid frame rate must fall back to 60, not stay at an unsupported value.");
        }

        [Test]
        public void ValidateAndRepair_NegativeMetaCurrency_ClampsToZero()
        {
            var data = new PlayerProfileSaveData { ProfileId = "p1", MetaCurrency = -50 };

            SaveValidator.ValidateAndRepair(data);

            Assert.AreEqual(0, data.MetaCurrency);
        }

        [Test]
        public void ValidateAndRepair_InvalidTimestamps_AreReplacedWithNow()
        {
            var data = new PlayerProfileSaveData { ProfileId = "p1", CreatedUtcTicks = -1, LastUpdatedUtcTicks = 0 };

            SaveValidator.ValidateAndRepair(data);

            Assert.Greater(data.CreatedUtcTicks, 0);
            Assert.Greater(data.LastUpdatedUtcTicks, 0);
        }

        [Test]
        public void ValidateAndRepair_UnknownLevelIdNotInAnyCatalog_IsKeptNotDeleted()
        {
            // SaveValidator has no LevelCatalog dependency by design (Save must not depend on Core); an id the
            // current catalog no longer recognizes is simply out of this validator's concern and must survive.
            var data = new PlayerProfileSaveData
            {
                ProfileId = "p1",
                LevelProgress = new List<LevelProgressSaveData> { new LevelProgressSaveData { LevelId = "level_removed_from_catalog", IsCompleted = true, BestStars = 3 } }
            };

            SaveValidator.ValidateAndRepair(data);

            Assert.AreEqual(1, data.LevelProgress.Count);
            Assert.AreEqual("level_removed_from_catalog", data.LevelProgress[0].LevelId);
        }
    }
}
