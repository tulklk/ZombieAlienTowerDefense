using System.IO;
using System.Reflection;
using AlienDefense.Save;
using AlienDefense.Towers;
using NUnit.Framework;
using UnityEngine;

namespace AlienDefense.Tests.EditMode
{
    public class TowerUnlockServiceTests
    {
        private string _testDirectory;

        [SetUp]
        public void SetUp()
        {
            _testDirectory = Path.Combine(Application.temporaryCachePath, "TowerUnlockServiceTests_" + System.Guid.NewGuid().ToString("N"));
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

        private static TowerDefinition CreateTower(string id, int unlockCost)
        {
            bool previousLogEnabled = Debug.unityLogger.logEnabled;
            Debug.unityLogger.logEnabled = false;
            var definition = ScriptableObject.CreateInstance<TowerDefinition>();
            Debug.unityLogger.logEnabled = previousLogEnabled;

            SetPrivateField(definition, "_id", id);
            SetPrivateField(definition, "_unlockCost", unlockCost);
            return definition;
        }

        private PlayerProfileService CreateProfileService(int startingMetaCurrency)
        {
            var repository = new SaveFileRepository(_testDirectory);
            var saveService = new SaveService(repository);
            PlayerProfileSaveData data = saveService.LoadOrCreateDefault(null, "level_01");
            data.MetaCurrency = startingMetaCurrency;
            return new PlayerProfileService(saveService, data);
        }

        [Test]
        public void TryUnlock_SufficientCoin_SpendsAndUnlocks()
        {
            TowerDefinition tower = CreateTower("tower_frost", 200);
            PlayerProfileService profile = CreateProfileService(500);
            var service = new TowerUnlockService(profile);

            bool unlocked = service.TryUnlock(tower);

            Assert.IsTrue(unlocked);
            Assert.IsTrue(profile.IsTowerUnlocked("tower_frost"));
            Assert.AreEqual(300, profile.MetaCurrency);
        }

        [Test]
        public void TryUnlock_InsufficientCoin_Fails_NoStateChange()
        {
            TowerDefinition tower = CreateTower("tower_frost", 200);
            PlayerProfileService profile = CreateProfileService(50);
            var service = new TowerUnlockService(profile);

            bool unlocked = service.TryUnlock(tower);

            Assert.IsFalse(unlocked);
            Assert.IsFalse(profile.IsTowerUnlocked("tower_frost"));
            Assert.AreEqual(50, profile.MetaCurrency);
        }

        [Test]
        public void TryUnlock_AlreadyUnlocked_Fails()
        {
            TowerDefinition tower = CreateTower("tower_frost", 200);
            PlayerProfileService profile = CreateProfileService(1000);
            var service = new TowerUnlockService(profile);
            Assert.IsTrue(service.TryUnlock(tower));

            bool unlockedAgain = service.TryUnlock(tower);

            Assert.IsFalse(unlockedAgain);
            Assert.AreEqual(800, profile.MetaCurrency, "Second unlock attempt must not spend again.");
        }

        [Test]
        public void IsUnlocked_ReflectsProfileState()
        {
            TowerDefinition tower = CreateTower("tower_frost", 0);
            PlayerProfileService profile = CreateProfileService(0);
            var service = new TowerUnlockService(profile);

            Assert.IsFalse(service.IsUnlocked(tower));

            service.TryUnlock(tower);

            Assert.IsTrue(service.IsUnlocked(tower));
        }
    }
}
