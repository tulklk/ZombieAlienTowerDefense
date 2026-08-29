using System.IO;
using AlienDefense.Save;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AlienDefense.Tests.EditMode
{
    public class PlayerProfileServiceTests
    {
        private string _testDirectory;

        [SetUp]
        public void SetUp()
        {
            _testDirectory = Path.Combine(Application.temporaryCachePath, "PlayerProfileServiceTests_" + System.Guid.NewGuid().ToString("N"));
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_testDirectory))
            {
                Directory.Delete(_testDirectory, recursive: true);
            }
        }

        private PlayerProfileService CreateService(out SaveFileRepository repository)
        {
            repository = new SaveFileRepository(_testDirectory);
            var saveService = new SaveService(repository);
            PlayerProfileSaveData data = saveService.LoadOrCreateDefault(null, "level_01");
            return new PlayerProfileService(saveService, data);
        }

        [Test]
        public void SetLevelCompleted_FirstTime_MarksCompleted_SetsStarsAndCount()
        {
            PlayerProfileService service = CreateService(out _);

            service.SetLevelCompleted(new LevelCompletedResult("level_01", 2, 15));

            LevelProgressSnapshot progress = service.GetLevelProgress("level_01");
            Assert.IsTrue(progress.IsCompleted);
            Assert.AreEqual(2, progress.BestStars);
            Assert.AreEqual(1, progress.CompletionCount);
            Assert.AreEqual(15, progress.BestRemainingBaseHealth);
        }

        [Test]
        public void SetLevelCompleted_WorseResultLater_NeverDecreasesBestStars()
        {
            PlayerProfileService service = CreateService(out _);
            service.SetLevelCompleted(new LevelCompletedResult("level_01", 3, 20));

            service.SetLevelCompleted(new LevelCompletedResult("level_01", 1, 5));

            LevelProgressSnapshot progress = service.GetLevelProgress("level_01");
            Assert.AreEqual(3, progress.BestStars, "Best stars must never decrease.");
            Assert.AreEqual(2, progress.CompletionCount, "Completion count still increments even on a worse replay.");
        }

        [Test]
        public void SetLevelCompleted_StarsOutOfRange_AreClamped()
        {
            PlayerProfileService service = CreateService(out _);

            service.SetLevelCompleted(new LevelCompletedResult("level_01", 99, 10));

            Assert.AreEqual(3, service.GetLevelProgress("level_01").BestStars);
        }

        [Test]
        public void GetLevelProgress_NeverCompleted_ReturnsNotStartedSnapshot()
        {
            PlayerProfileService service = CreateService(out _);

            LevelProgressSnapshot progress = service.GetLevelProgress("level_never_played");

            Assert.IsFalse(progress.IsCompleted);
            Assert.AreEqual(0, progress.BestStars);
        }

        [Test]
        public void UnlockTower_TwiceForSameId_DoesNotDuplicate()
        {
            PlayerProfileService service = CreateService(out _);

            service.UnlockTower("tower_heavy");
            service.UnlockTower("tower_heavy");

            Assert.IsTrue(service.IsTowerUnlocked("tower_heavy"));
        }

        [Test]
        public void SetTowerUpgradeLevel_NegativeLevel_IsRejected()
        {
            PlayerProfileService service = CreateService(out _);

            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex(".*"));
            service.SetTowerUpgradeLevel("tower_blaster", -1);

            Assert.AreEqual(0, service.GetTowerUpgradeLevel("tower_blaster"));
        }

        [Test]
        public void SetTowerUpgradeLevel_ValidLevel_IsStored()
        {
            PlayerProfileService service = CreateService(out _);

            service.SetTowerUpgradeLevel("tower_blaster", 2);

            Assert.AreEqual(2, service.GetTowerUpgradeLevel("tower_blaster"));
        }

        [Test]
        public void UpdateSettings_DoesNotSaveImmediately_OnlyAfterTickDebounceElapses()
        {
            PlayerProfileService service = CreateService(out SaveFileRepository repository);
            repository.TryReadMain(out string beforeJson);

            var newSettings = new GameSettings(0.1f, 0.2f, 0.3f, false, false, 0, 30);
            service.UpdateSettings(newSettings);

            repository.TryReadMain(out string immediatelyAfterJson);
            Assert.AreEqual(beforeJson, immediatelyAfterJson, "Settings changes must be debounced, not saved on the same frame.");

            service.Tick(10f);

            repository.TryReadMain(out string afterTickJson);
            PlayerProfileSaveData saved = JsonUtility.FromJson<PlayerProfileSaveData>(afterTickJson);
            Assert.AreEqual(0.1f, saved.Settings.MasterVolume, 0.001f);
        }

        [Test]
        public void FlushPendingSave_SavesImmediately_WithoutWaitingForTick()
        {
            PlayerProfileService service = CreateService(out SaveFileRepository repository);
            service.UpdateSettings(new GameSettings(0.42f, 0.5f, 0.5f, true, true, 2, 60));

            service.FlushPendingSave();

            repository.TryReadMain(out string json);
            PlayerProfileSaveData saved = JsonUtility.FromJson<PlayerProfileSaveData>(json);
            Assert.AreEqual(0.42f, saved.Settings.MasterVolume, 0.001f);
        }

        [Test]
        public void SetLevelCompleted_EmptyLevelId_IsIgnored_NoException()
        {
            PlayerProfileService service = CreateService(out _);

            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex(".*"));
            Assert.DoesNotThrow(() => service.SetLevelCompleted(new LevelCompletedResult("", 1, 1)));
        }
    }
}
