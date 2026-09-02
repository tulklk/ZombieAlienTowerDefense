using System.IO;
using System.Reflection;
using AlienDefense.Save;
using AlienDefense.Towers;
using NUnit.Framework;
using UnityEngine;

namespace AlienDefense.Tests.EditMode
{
    public class TowerMetaUpgradeServiceTests
    {
        private string _testDirectory;

        [SetUp]
        public void SetUp()
        {
            _testDirectory = Path.Combine(Application.temporaryCachePath, "TowerMetaUpgradeServiceTests_" + System.Guid.NewGuid().ToString("N"));
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_testDirectory))
            {
                Directory.Delete(_testDirectory, recursive: true);
            }
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(field, $"Field '{fieldName}' not found on {target.GetType().Name}.");
            field.SetValue(target, value);
        }

        private static T CreateSilently<T>(System.Func<T> factory)
        {
            bool previousLogEnabled = Debug.unityLogger.logEnabled;
            Debug.unityLogger.logEnabled = false;
            T result = factory();
            Debug.unityLogger.logEnabled = previousLogEnabled;
            return result;
        }

        private static TowerLevelData CreateLevel(int upgradeCost)
        {
            var level = new TowerLevelData();
            SetPrivateField(level, "_upgradeCost", upgradeCost);
            SetPrivateField(level, "_damage", 10f);
            SetPrivateField(level, "_range", 4f);
            SetPrivateField(level, "_attacksPerSecond", 1f);
            SetPrivateField(level, "_turretRotationSpeed", 360f);
            return level;
        }

        /// <summary>3 levels: index 0 costs 0 (build cost, not an upgrade cost), index 1 costs 100, index 2 costs 250.</summary>
        private static TowerDefinition CreateThreeLevelTower(string id)
        {
            var definition = CreateSilently(() => ScriptableObject.CreateInstance<TowerDefinition>());
            SetPrivateField(definition, "_id", id);
            SetPrivateField(definition, "_levels", new[] { CreateLevel(0), CreateLevel(100), CreateLevel(250) });
            return definition;
        }

        private PlayerProfileService CreateProfileService(int startingMetaCurrency, params string[] unlockedTowerIds)
        {
            var repository = new SaveFileRepository(_testDirectory);
            var saveService = new SaveService(repository);
            PlayerProfileSaveData data = saveService.LoadOrCreateDefault(null, "level_01");
            data.MetaCurrency = startingMetaCurrency;
            data.UnlockedTowerIds.AddRange(unlockedTowerIds);
            return new PlayerProfileService(saveService, data);
        }

        [Test]
        public void GetCurrentLevelIndex_NoSavedUpgrade_ReturnsZero()
        {
            TowerDefinition tower = CreateThreeLevelTower("tower_blaster");
            var service = new TowerMetaUpgradeService(CreateProfileService(0, "tower_blaster"));

            Assert.AreEqual(0, service.GetCurrentLevelIndex(tower));
        }

        [Test]
        public void TryGetNextLevelCost_FromLevelZero_ReturnsLevelOneCost()
        {
            TowerDefinition tower = CreateThreeLevelTower("tower_blaster");
            var service = new TowerMetaUpgradeService(CreateProfileService(0, "tower_blaster"));

            bool found = service.TryGetNextLevelCost(tower, out int cost);

            Assert.IsTrue(found);
            Assert.AreEqual(100, cost);
        }

        [Test]
        public void TryUpgrade_NotUnlocked_Fails_NoCurrencySpent()
        {
            TowerDefinition tower = CreateThreeLevelTower("tower_blaster");
            PlayerProfileService profile = CreateProfileService(500); // not unlocked

            bool upgraded = new TowerMetaUpgradeService(profile).TryUpgrade(tower);

            Assert.IsFalse(upgraded);
            Assert.AreEqual(500, profile.MetaCurrency);
            Assert.AreEqual(0, profile.GetTowerUpgradeLevel("tower_blaster"));
        }

        [Test]
        public void TryUpgrade_InsufficientCurrency_Fails_NoStateChange()
        {
            TowerDefinition tower = CreateThreeLevelTower("tower_blaster");
            PlayerProfileService profile = CreateProfileService(50, "tower_blaster"); // needs 100

            bool upgraded = new TowerMetaUpgradeService(profile).TryUpgrade(tower);

            Assert.IsFalse(upgraded);
            Assert.AreEqual(50, profile.MetaCurrency);
            Assert.AreEqual(0, profile.GetTowerUpgradeLevel("tower_blaster"));
        }

        [Test]
        public void TryUpgrade_Success_SpendsExactCost_AndIncrementsSavedLevel()
        {
            TowerDefinition tower = CreateThreeLevelTower("tower_blaster");
            PlayerProfileService profile = CreateProfileService(150, "tower_blaster");
            var service = new TowerMetaUpgradeService(profile);

            bool upgraded = service.TryUpgrade(tower);

            Assert.IsTrue(upgraded);
            Assert.AreEqual(50, profile.MetaCurrency, "Only the level's UpgradeCost (100) should be spent.");
            Assert.AreEqual(1, profile.GetTowerUpgradeLevel("tower_blaster"));
            Assert.AreEqual(1, service.GetCurrentLevelIndex(tower));
        }

        [Test]
        public void TryUpgrade_AtMaxLevel_Fails()
        {
            TowerDefinition tower = CreateThreeLevelTower("tower_blaster");
            PlayerProfileService profile = CreateProfileService(10_000, "tower_blaster");
            var service = new TowerMetaUpgradeService(profile);

            Assert.IsTrue(service.TryUpgrade(tower)); // -> level index 1
            Assert.IsTrue(service.TryUpgrade(tower)); // -> level index 2 (max)
            Assert.IsTrue(service.IsMaxLevel(tower));

            bool upgradedPastMax = service.TryUpgrade(tower);

            Assert.IsFalse(upgradedPastMax);
        }

        [Test]
        public void CanAffordNextLevel_ReflectsCurrentMetaCurrency()
        {
            TowerDefinition tower = CreateThreeLevelTower("tower_blaster");
            var poor = new TowerMetaUpgradeService(CreateProfileService(10, "tower_blaster"));
            var rich = new TowerMetaUpgradeService(CreateProfileService(1000, "tower_blaster"));

            Assert.IsFalse(poor.CanAffordNextLevel(tower));
            Assert.IsTrue(rich.CanAffordNextLevel(tower));
        }
    }
}
