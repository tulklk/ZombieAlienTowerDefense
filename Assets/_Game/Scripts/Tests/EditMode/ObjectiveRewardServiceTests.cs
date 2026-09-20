using System.Collections.Generic;
using System.IO;
using AlienDefense.Core;
using AlienDefense.Meta;
using AlienDefense.Save;
using NUnit.Framework;
using UnityEngine;

namespace AlienDefense.Tests.EditMode
{
    public class ObjectiveRewardServiceTests
    {
        private string _testDirectory;

        [SetUp]
        public void SetUp()
        {
            _testDirectory = Path.Combine(Application.temporaryCachePath, "ObjectiveRewardServiceTests_" + System.Guid.NewGuid().ToString("N"));
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_testDirectory))
            {
                Directory.Delete(_testDirectory, recursive: true);
            }
        }

        [Test]
        public void GetUiState_FreshLevel_AllLocked()
        {
            PlayerProfileService profile = CreateProfile();
            var service = new ObjectiveRewardService(profile, null);

            Assert.AreEqual(ObjectiveRewardUiState.Locked, service.GetUiState("L2", LevelObjectiveKind.Clear));
            Assert.AreEqual(ObjectiveRewardUiState.Locked, service.GetUiState("L2", LevelObjectiveKind.Hp50));
            Assert.AreEqual(ObjectiveRewardUiState.Locked, service.GetUiState("L2", LevelObjectiveKind.Perfect));
        }

        [Test]
        public void GetUiState_After30PercentWin_OnlyClearClaimable()
        {
            PlayerProfileService profile = CreateProfile();
            profile.SetLevelCompleted(new LevelCompletedResult("L2", 1, 30, 30));
            var service = new ObjectiveRewardService(profile, null);

            Assert.AreEqual(ObjectiveRewardUiState.Claimable, service.GetUiState("L2", LevelObjectiveKind.Clear));
            Assert.AreEqual(ObjectiveRewardUiState.Locked, service.GetUiState("L2", LevelObjectiveKind.Hp50));
            Assert.AreEqual(ObjectiveRewardUiState.Locked, service.GetUiState("L2", LevelObjectiveKind.Perfect));
        }

        [Test]
        public void GetUiState_After75PercentWin_ClearAndHp50Claimable()
        {
            PlayerProfileService profile = CreateProfile();
            profile.SetLevelCompleted(new LevelCompletedResult("L2", 2, 75, 75));
            var service = new ObjectiveRewardService(profile, null);

            Assert.AreEqual(ObjectiveRewardUiState.Claimable, service.GetUiState("L2", LevelObjectiveKind.Clear));
            Assert.AreEqual(ObjectiveRewardUiState.Claimable, service.GetUiState("L2", LevelObjectiveKind.Hp50));
            Assert.AreEqual(ObjectiveRewardUiState.Locked, service.GetUiState("L2", LevelObjectiveKind.Perfect));
        }

        [Test]
        public void GetUiState_PerfectWin_AllClaimable()
        {
            PlayerProfileService profile = CreateProfile();
            profile.SetLevelCompleted(new LevelCompletedResult("L2", 3, 100, 100));
            var service = new ObjectiveRewardService(profile, null);

            Assert.AreEqual(ObjectiveRewardUiState.Claimable, service.GetUiState("L2", LevelObjectiveKind.Clear));
            Assert.AreEqual(ObjectiveRewardUiState.Claimable, service.GetUiState("L2", LevelObjectiveKind.Hp50));
            Assert.AreEqual(ObjectiveRewardUiState.Claimable, service.GetUiState("L2", LevelObjectiveKind.Perfect));
        }

        [Test]
        public void TryClaim_GrantsOnce_AndBlocksDuplicate()
        {
            PlayerProfileService profile = CreateProfile();
            profile.SetLevelCompleted(new LevelCompletedResult("L2", 1, 40, 40));

            LevelCatalog catalog = ScriptableObject.CreateInstance<LevelCatalog>();
            var service = new ObjectiveRewardService(profile, catalog);

            Assert.IsTrue(service.TryClaim("L2", LevelObjectiveKind.Clear, out List<GrantedObjectiveReward> granted));
            Assert.AreEqual(ObjectiveRewardUiState.Claimed, service.GetUiState("L2", LevelObjectiveKind.Clear));
            Assert.IsFalse(service.TryClaim("L2", LevelObjectiveKind.Clear, out _));
            Assert.IsNotNull(granted);
        }

        [Test]
        public void AddItem_PersistsStackAmounts()
        {
            PlayerProfileService profile = CreateProfile();
            profile.AddItem(MetaItemIds.Microcircuit, 75);
            profile.AddItem(MetaItemIds.Microcircuit, 25);
            Assert.AreEqual(100, profile.GetItemAmount(MetaItemIds.Microcircuit));
        }

        [Test]
        public void Migrate_V2ToV3_CreatesInventory()
        {
            var data = new PlayerProfileSaveData
            {
                SaveVersion = 2,
                ProfileId = "p",
                Inventory = null,
            };

            (SaveMigrationPipeline.MigrationOutcome outcome, PlayerProfileSaveData result) = SaveMigrationPipeline.Migrate(data);
            Assert.AreEqual(SaveMigrationPipeline.MigrationOutcome.Migrated, outcome);
            Assert.AreEqual(SaveConstants.CurrentSaveVersion, result.SaveVersion);
            Assert.IsNotNull(result.Inventory);
        }

        private PlayerProfileService CreateProfile()
        {
            var repository = new SaveFileRepository(_testDirectory);
            var saveService = new SaveService(repository);
            PlayerProfileSaveData data = saveService.LoadOrCreateDefault(null, "L1");
            return new PlayerProfileService(saveService, data);
        }
    }
}
