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

            service.SetLevelCompleted(new LevelCompletedResult("level_01", 2, 15, 75));

            LevelProgressSnapshot progress = service.GetLevelProgress("level_01");
            Assert.IsTrue(progress.IsCompleted);
            Assert.AreEqual(2, progress.BestStars);
            Assert.AreEqual(1, progress.CompletionCount);
            Assert.AreEqual(15, progress.BestRemainingBaseHealth);
            Assert.AreEqual(75, progress.BestRemainingHpPercent);
        }

        [Test]
        public void SetLevelCompleted_WorseResultLater_NeverDecreasesBestStars()
        {
            PlayerProfileService service = CreateService(out _);
            service.SetLevelCompleted(new LevelCompletedResult("level_01", 3, 20, 100));

            service.SetLevelCompleted(new LevelCompletedResult("level_01", 1, 5, 25));

            LevelProgressSnapshot progress = service.GetLevelProgress("level_01");
            Assert.AreEqual(3, progress.BestStars, "Best stars must never decrease.");
            Assert.AreEqual(2, progress.CompletionCount, "Completion count still increments even on a worse replay.");
        }

        [Test]
        public void SetLevelCompleted_StarsOutOfRange_AreClamped()
        {
            PlayerProfileService service = CreateService(out _);

            service.SetLevelCompleted(new LevelCompletedResult("level_01", 99, 10, 50));

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
        public void TrySpendMetaCurrency_SufficientFunds_DeductsAndReturnsTrue()
        {
            PlayerProfileService service = CreateService(out _);
            service.AddMetaCurrency(500);

            bool spent = service.TrySpendMetaCurrency(200);

            Assert.IsTrue(spent);
            Assert.AreEqual(300, service.MetaCurrency);
        }

        [Test]
        public void TrySpendMetaCurrency_InsufficientFunds_RejectsWithoutPartialSpend()
        {
            PlayerProfileService service = CreateService(out _);
            service.AddMetaCurrency(50);

            bool spent = service.TrySpendMetaCurrency(200);

            Assert.IsFalse(spent);
            Assert.AreEqual(50, service.MetaCurrency, "A rejected spend must not partially deduct.");
        }

        [Test]
        public void TrySpendMetaCurrency_NonPositiveAmount_IsRejected()
        {
            PlayerProfileService service = CreateService(out _);
            service.AddMetaCurrency(100);

            Assert.IsFalse(service.TrySpendMetaCurrency(0));
            Assert.IsFalse(service.TrySpendMetaCurrency(-10));
            Assert.AreEqual(100, service.MetaCurrency);
        }

        [Test]
        public void AddMetaCurrency_AccumulatesAcrossCalls()
        {
            PlayerProfileService service = CreateService(out _);

            service.AddMetaCurrency(100);
            service.AddMetaCurrency(50);

            Assert.AreEqual(150, service.MetaCurrency);
        }

        [Test]
        public void TryClaimDailyReward_FirstClaim_GrantsDayOneCoinAndStreak()
        {
            PlayerProfileService service = CreateService(out _);
            var now = new System.DateTime(2026, 1, 1, 8, 0, 0, System.DateTimeKind.Utc);

            bool claimed = service.TryClaimDailyReward(now, out int coin, out int gems);

            Assert.IsTrue(claimed);
            Assert.AreEqual(DailyRewardCalculator.CoinRewardByDay[0], coin);
            Assert.AreEqual(0, gems);
            Assert.AreEqual(1, service.DailyRewardStreakDay);
            Assert.AreEqual(DailyRewardCalculator.CoinRewardByDay[0], service.MetaCurrency);
        }

        [Test]
        public void TryClaimDailyReward_SameDayTwice_SecondFails()
        {
            PlayerProfileService service = CreateService(out _);
            var now = new System.DateTime(2026, 1, 1, 8, 0, 0, System.DateTimeKind.Utc);
            service.TryClaimDailyReward(now, out _, out _);

            bool claimedAgain = service.TryClaimDailyReward(now.AddHours(2), out _, out _);

            Assert.IsFalse(claimedAgain);
            Assert.AreEqual(1, service.DailyRewardStreakDay);
        }

        [Test]
        public void TryClaimDailyReward_NextDay_AdvancesStreak()
        {
            PlayerProfileService service = CreateService(out _);
            var day1 = new System.DateTime(2026, 1, 1, 8, 0, 0, System.DateTimeKind.Utc);
            service.TryClaimDailyReward(day1, out _, out _);

            bool claimed = service.TryClaimDailyReward(day1.AddDays(1), out int coin, out int gems);

            Assert.IsTrue(claimed);
            Assert.AreEqual(2, service.DailyRewardStreakDay);
            Assert.AreEqual(DailyRewardCalculator.CoinRewardByDay[1], coin);
        }

        [Test]
        public void MarkDailyQuestCompleted_ThenClaim_GrantsRewardOnce()
        {
            PlayerProfileService service = CreateService(out _);
            var now = new System.DateTime(2026, 1, 1, 8, 0, 0, System.DateTimeKind.Utc);

            Assert.IsFalse(service.IsDailyQuestCompleted(now));
            service.MarkDailyQuestCompleted(now);
            Assert.IsTrue(service.IsDailyQuestCompleted(now));

            bool claimed = service.TryClaimDailyQuest(now, 30, out int granted);
            Assert.IsTrue(claimed);
            Assert.AreEqual(30, granted);
            Assert.AreEqual(30, service.MetaCurrency);

            bool claimedTwice = service.TryClaimDailyQuest(now, 30, out int grantedAgain);
            Assert.IsFalse(claimedTwice);
            Assert.AreEqual(0, grantedAgain);
            Assert.AreEqual(30, service.MetaCurrency, "Claiming twice must not double-grant.");
        }

        [Test]
        public void TryClaimDailyQuest_NotCompletedYet_Fails()
        {
            PlayerProfileService service = CreateService(out _);
            var now = new System.DateTime(2026, 1, 1, 8, 0, 0, System.DateTimeKind.Utc);

            bool claimed = service.TryClaimDailyQuest(now, 30, out int granted);

            Assert.IsFalse(claimed);
            Assert.AreEqual(0, granted);
        }

        [Test]
        public void DailyQuest_NewDay_ResetsCompletionState()
        {
            PlayerProfileService service = CreateService(out _);
            var day1 = new System.DateTime(2026, 1, 1, 8, 0, 0, System.DateTimeKind.Utc);
            service.MarkDailyQuestCompleted(day1);
            service.TryClaimDailyQuest(day1, 30, out _);

            var day2 = day1.AddDays(1);

            Assert.IsFalse(service.IsDailyQuestCompleted(day2));
            Assert.IsFalse(service.IsDailyQuestClaimed(day2));
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
            Assert.DoesNotThrow(() => service.SetLevelCompleted(new LevelCompletedResult("", 1, 1, 1)));
        }
    }
}
