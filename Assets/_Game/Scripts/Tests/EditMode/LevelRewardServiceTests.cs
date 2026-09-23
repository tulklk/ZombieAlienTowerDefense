using System.Collections.Generic;
using System.IO;
using AlienDefense.Meta;
using AlienDefense.Progression;
using AlienDefense.Save;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AlienDefense.Tests.EditMode
{
    /// <summary>The victory payout: every configured line lands in the saved profile, a run is paid once no matter
    /// how often victory is reported, and first-clear lines are paid once per level.</summary>
    public class LevelRewardServiceTests
    {
        private string _testDirectory;

        [SetUp]
        public void SetUp()
        {
            _testDirectory = Path.Combine(Application.temporaryCachePath, "LevelRewardServiceTests_" + System.Guid.NewGuid().ToString("N"));
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_testDirectory))
            {
                Directory.Delete(_testDirectory, recursive: true);
            }
        }

        private static readonly LevelRewardEntry[] Level1 =
        {
            new LevelRewardEntry(VictoryRewardType.Coins, 5800),
            new LevelRewardEntry(VictoryRewardType.Experience, 3000),
            new LevelRewardEntry(VictoryRewardType.UfoBaseCard, 5),
            new LevelRewardEntry(VictoryRewardType.BlasterCard, 10),
            new LevelRewardEntry(VictoryRewardType.FrostCard, 20),
            new LevelRewardEntry(VictoryRewardType.MortarCard, 10),
            new LevelRewardEntry(VictoryRewardType.TeslaCard, 5),
            new LevelRewardEntry(VictoryRewardType.ReactorBlueprint, 1),
            new LevelRewardEntry(VictoryRewardType.AntiGravityBlueprint, 3),
        };

        [Test]
        public void Grant_PaysEveryLineIntoTheSavedProfile()
        {
            PlayerProfileService profile = CreateProfile();
            var granted = new List<VictoryReward>();

            bool paid = new LevelRewardService(profile).TryGrantVictoryRewards(Context("run1", stars: 2, hp: 70), Level1, granted);

            Assert.IsTrue(paid);
            Assert.AreEqual(5800, profile.MetaCurrency);
            Assert.AreEqual(3000, profile.PlayerExperience);
            Assert.AreEqual(5, profile.GetItemAmount(MetaItemIds.CardUfo));
            Assert.AreEqual(10, profile.GetItemAmount(MetaItemIds.CardBlaster));
            Assert.AreEqual(20, profile.GetItemAmount(MetaItemIds.CardFrost));
            Assert.AreEqual(10, profile.GetItemAmount(MetaItemIds.CardMortar));
            Assert.AreEqual(5, profile.GetItemAmount(MetaItemIds.CardTesla));
            Assert.AreEqual(1, profile.GetItemAmount(MetaItemIds.ReactorBlueprint));
            Assert.AreEqual(3, profile.GetItemAmount(MetaItemIds.AntiGravityBlueprint));
            Assert.AreEqual(9, granted.Count, "The panel is handed exactly what was paid.");
            Assert.AreEqual(VictoryRewardType.Coins, granted[0].Type);
            Assert.AreEqual(5800, granted[0].Amount);
        }

        [Test]
        public void Grant_SameRunTwice_PaysOnce()
        {
            PlayerProfileService profile = CreateProfile();
            var service = new LevelRewardService(profile);
            var first = new List<VictoryReward>();
            var second = new List<VictoryReward>();

            service.TryGrantVictoryRewards(Context("run1"), Level1, first);
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("already paid"));
            bool paidAgain = service.TryGrantVictoryRewards(Context("run1"), Level1, second);

            Assert.IsFalse(paidAgain, "A duplicated victory callback must not pay the run again.");
            Assert.AreEqual(0, second.Count);
            Assert.AreEqual(5800, profile.MetaCurrency);
            Assert.AreEqual(10, profile.GetItemAmount(MetaItemIds.CardBlaster));
        }

        [Test]
        public void Grant_NewRun_PaysEveryClearLinesAgain_ButNotFirstClearOnes()
        {
            PlayerProfileService profile = CreateProfile();
            var service = new LevelRewardService(profile);
            var config = new[]
            {
                new LevelRewardEntry(VictoryRewardType.Coins, 100),
                new LevelRewardEntry(VictoryRewardType.ReactorBlueprint, 1, LevelRewardGrantRule.FirstClearOnly),
            };

            service.TryGrantVictoryRewards(Context("run1"), config, new List<VictoryReward>());
            var replay = new List<VictoryReward>();
            service.TryGrantVictoryRewards(Context("run2"), config, replay);

            Assert.AreEqual(200, profile.MetaCurrency);
            Assert.AreEqual(1, profile.GetItemAmount(MetaItemIds.ReactorBlueprint), "First-clear reward is paid once.");
            Assert.AreEqual(1, replay.Count);
            Assert.AreEqual(VictoryRewardType.Coins, replay[0].Type);
        }

        [Test]
        public void Grant_RequirementNotMet_SkipsTheLine()
        {
            PlayerProfileService profile = CreateProfile();
            var config = new[]
            {
                new LevelRewardEntry(VictoryRewardType.Coins, 100),
                new LevelRewardEntry(VictoryRewardType.TeslaCard, 4, LevelRewardGrantRule.EveryClear, LevelRewardRequirement.PerfectClear),
            };
            var granted = new List<VictoryReward>();

            new LevelRewardService(profile).TryGrantVictoryRewards(Context("run1", hp: 99), config, granted);

            Assert.AreEqual(0, profile.GetItemAmount(MetaItemIds.CardTesla));
            Assert.AreEqual(1, granted.Count);
        }

        [Test]
        public void Grant_UnconfiguredLevel_KeepsTheStarFormula()
        {
            PlayerProfileService profile = CreateProfile();
            var granted = new List<VictoryReward>();

            new LevelRewardService(profile).TryGrantVictoryRewards(Context("run1", stars: 3, hp: 100, first: true), null, granted);

            Assert.AreEqual(50, profile.MetaCurrency, "20 + 10 per star.");
            Assert.AreEqual(5, profile.Gems, "First-time three-star gem bonus.");
            Assert.AreEqual(2, granted.Count);
        }

        [Test]
        public void GrantedRewards_AndTheTransaction_SurviveASaveReload()
        {
            PlayerProfileService profile = CreateProfile();
            new LevelRewardService(profile).TryGrantVictoryRewards(Context("run1"), Level1, new List<VictoryReward>());

            PlayerProfileService reloaded = CreateProfile();

            Assert.AreEqual(5800, reloaded.MetaCurrency);
            Assert.AreEqual(3000, reloaded.PlayerExperience);
            Assert.AreEqual(3, reloaded.GetItemAmount(MetaItemIds.AntiGravityBlueprint));
            Assert.IsTrue(reloaded.HasRewardTransaction(Context("run1").TransactionId));
        }

        [Test]
        public void EveryCardAndBlueprintHasStorage()
        {
            foreach (VictoryRewardType type in System.Enum.GetValues(typeof(VictoryRewardType)))
            {
                bool isCurrency = type == VictoryRewardType.Coins || type == VictoryRewardType.Gems
                    || type == VictoryRewardType.Experience;
                Assert.AreEqual(!isCurrency, !string.IsNullOrEmpty(LevelRewardService.GetItemId(type)), type.ToString());
            }
        }

        private static LevelRewardContext Context(string runId, int stars = 1, int hp = 40, bool first = false)
        {
            return new LevelRewardContext("level_01", runId, stars, hp, first);
        }

        private PlayerProfileService CreateProfile()
        {
            var repository = new SaveFileRepository(_testDirectory);
            var saveService = new SaveService(repository);
            PlayerProfileSaveData data = saveService.LoadOrCreateDefault(null, "level_01");
            return new PlayerProfileService(saveService, data);
        }
    }
}
