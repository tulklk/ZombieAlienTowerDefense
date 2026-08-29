using System.Collections.Generic;
using AlienDefense.Save;
using NUnit.Framework;
using UnityEngine;

namespace AlienDefense.Tests.EditMode
{
    public class SaveSerializationTests
    {
        [Test]
        public void RoundTrip_PreservesAllFields()
        {
            var original = new PlayerProfileSaveData
            {
                SaveVersion = 1,
                ProfileId = "profile-123",
                CreatedUtcTicks = 1000L,
                LastUpdatedUtcTicks = 2000L,
                HighestUnlockedLevelId = "level_02",
                LevelProgress = new List<LevelProgressSaveData>
                {
                    new LevelProgressSaveData { LevelId = "level_01", IsCompleted = true, BestStars = 3, CompletionCount = 2, BestRemainingBaseHealth = 18 }
                },
                UnlockedTowerIds = new List<string> { "tower_blaster", "tower_rapid" },
                TowerUpgrades = new List<PermanentTowerUpgradeSaveData>
                {
                    new PermanentTowerUpgradeSaveData { TowerId = "tower_blaster", UpgradeLevel = 2 }
                },
                MetaCurrency = 150,
                Settings = new SettingsSaveData { MasterVolume = 0.7f, MusicVolume = 0.5f, SfxVolume = 0.9f, HapticsEnabled = false, CameraShakeEnabled = false, QualityLevel = 1, TargetFrameRate = 30 },
                Tutorial = new TutorialProgressSaveData { IsCompleted = true }
            };

            string json = JsonUtility.ToJson(original);
            PlayerProfileSaveData restored = JsonUtility.FromJson<PlayerProfileSaveData>(json);

            Assert.AreEqual(original.SaveVersion, restored.SaveVersion);
            Assert.AreEqual(original.ProfileId, restored.ProfileId);
            Assert.AreEqual(original.CreatedUtcTicks, restored.CreatedUtcTicks);
            Assert.AreEqual(original.HighestUnlockedLevelId, restored.HighestUnlockedLevelId);
            Assert.AreEqual(1, restored.LevelProgress.Count);
            Assert.AreEqual("level_01", restored.LevelProgress[0].LevelId);
            Assert.AreEqual(3, restored.LevelProgress[0].BestStars);
            Assert.AreEqual(2, restored.UnlockedTowerIds.Count);
            Assert.AreEqual(1, restored.TowerUpgrades.Count);
            Assert.AreEqual(2, restored.TowerUpgrades[0].UpgradeLevel);
            Assert.AreEqual(150, restored.MetaCurrency);
            Assert.AreEqual(0.7f, restored.Settings.MasterVolume, 0.001f);
            Assert.IsFalse(restored.Settings.HapticsEnabled);
            Assert.IsTrue(restored.Tutorial.IsCompleted);
        }

        [Test]
        public void RoundTrip_EmptyOptionalLists_StaysEmpty_NotNull()
        {
            var original = new PlayerProfileSaveData
            {
                SaveVersion = 1,
                ProfileId = "profile-empty",
                LevelProgress = new List<LevelProgressSaveData>(),
                UnlockedTowerIds = new List<string>(),
                TowerUpgrades = new List<PermanentTowerUpgradeSaveData>()
            };

            string json = JsonUtility.ToJson(original);
            PlayerProfileSaveData restored = JsonUtility.FromJson<PlayerProfileSaveData>(json);

            Assert.IsNotNull(restored.LevelProgress);
            Assert.AreEqual(0, restored.LevelProgress.Count);
            Assert.IsNotNull(restored.UnlockedTowerIds);
            Assert.AreEqual(0, restored.UnlockedTowerIds.Count);
        }

        [Test]
        public void RoundTrip_UnicodeLevelId_IsPreserved()
        {
            var original = new PlayerProfileSaveData
            {
                SaveVersion = 1,
                ProfileId = "profile-unicode",
                HighestUnlockedLevelId = "level_01"
            };
            original.LevelProgress.Add(new LevelProgressSaveData { LevelId = "level_01", IsCompleted = true });

            string json = JsonUtility.ToJson(original);
            PlayerProfileSaveData restored = JsonUtility.FromJson<PlayerProfileSaveData>(json);

            Assert.AreEqual("level_01", restored.LevelProgress[0].LevelId);
        }

        [Test]
        public void RoundTrip_LargeProfile_HandlesManyEntries()
        {
            var original = new PlayerProfileSaveData { SaveVersion = 1, ProfileId = "profile-large" };
            for (int i = 0; i < 200; i++)
            {
                original.LevelProgress.Add(new LevelProgressSaveData { LevelId = "level_" + i, IsCompleted = true, BestStars = i % 4 });
            }

            string json = JsonUtility.ToJson(original);
            PlayerProfileSaveData restored = JsonUtility.FromJson<PlayerProfileSaveData>(json);

            Assert.AreEqual(200, restored.LevelProgress.Count);
            Assert.AreEqual("level_199", restored.LevelProgress[199].LevelId);
        }

        [Test]
        public void FromJson_MissingSaveVersionField_DefaultsToZero()
        {
            const string jsonWithoutVersion = "{\"ProfileId\":\"legacy\"}";

            PlayerProfileSaveData restored = JsonUtility.FromJson<PlayerProfileSaveData>(jsonWithoutVersion);

            Assert.AreEqual(0, restored.SaveVersion);
            Assert.AreEqual("legacy", restored.ProfileId);
        }
    }
}
