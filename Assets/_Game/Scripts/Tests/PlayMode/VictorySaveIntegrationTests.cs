using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using AlienDefense.Base;
using AlienDefense.Core;
using AlienDefense.Save;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AlienDefense.Tests.PlayMode
{
    /// <summary>Covers LevelCompositionRoot.BuildLevelCompletedResult() (the star placeholder formula, invoked via
    /// reflection since it is private) and that feeding its result into a real PlayerProfileService persists
    /// correctly. Does not drive the full HandleGameStateChanged -> Victory dispatch: that method assumes
    /// BuildLevel() already wired GameSpeed/WaveController/etc., which is out of scope to fully stub here.</summary>
    public class VictorySaveIntegrationTests
    {
        private readonly List<GameObject> _spawnedObjects = new List<GameObject>();
        private string _testDirectory;

        [SetUp]
        public void SetUp()
        {
            _testDirectory = Path.Combine(Application.temporaryCachePath, "VictorySaveTests_" + System.Guid.NewGuid().ToString("N"));
        }

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject go in _spawnedObjects)
            {
                if (go != null)
                {
                    Object.Destroy(go);
                }
            }

            _spawnedObjects.Clear();

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

        /// <summary>Sets an auto-property with a private setter via its compiler-generated backing field —
        /// the same reflection technique this project already uses for private fields, applied to
        /// "{ get; private set; }" properties that have no public setter to call directly.</summary>
        private static void SetAutoPropertyBackingField(object target, string propertyName, object value)
        {
            FieldInfo field = target.GetType().GetField($"<{propertyName}>k__BackingField", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(field, $"Backing field for property '{propertyName}' not found on {target.GetType().Name}.");
            field.SetValue(target, value);
        }

        private static object InvokePrivateMethodWithResult(object target, string methodName)
        {
            MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(method, $"Method '{methodName}' not found on {target.GetType().Name}.");
            return method.Invoke(target, null);
        }

        private LevelCompositionRoot CreateRootWithBaseHealth(int maxHealth, int currentHealth, string levelId)
        {
            var go = new GameObject("TestLevelCompositionRoot");
            go.SetActive(false);
            _spawnedObjects.Add(go);
            var root = go.AddComponent<LevelCompositionRoot>();

            var baseHealth = new BaseHealthService(maxHealth);
            if (currentHealth < maxHealth)
            {
                baseHealth.TakeDamage(maxHealth - currentHealth);
            }

            SetAutoPropertyBackingField(root, "BaseHealth", baseHealth);
            SetPrivateField(root, "_resolvedLevelId", levelId);

            return root;
        }

        [Test]
        public void BuildLevelCompletedResult_NoDamageTaken_Gives3Stars()
        {
            LevelCompositionRoot root = CreateRootWithBaseHealth(20, 20, "level_01");

            var result = (LevelCompletedResult)InvokePrivateMethodWithResult(root, "BuildLevelCompletedResult");

            Assert.AreEqual(3, result.Stars);
            Assert.AreEqual(20, result.RemainingBaseHealth);
            Assert.AreEqual("level_01", result.LevelId);
        }

        [Test]
        public void BuildLevelCompletedResult_HalfHealthRemaining_Gives2Stars()
        {
            LevelCompositionRoot root = CreateRootWithBaseHealth(20, 10, "level_01");

            var result = (LevelCompletedResult)InvokePrivateMethodWithResult(root, "BuildLevelCompletedResult");

            Assert.AreEqual(2, result.Stars);
        }

        [Test]
        public void BuildLevelCompletedResult_LowHealthRemaining_Gives1Star()
        {
            LevelCompositionRoot root = CreateRootWithBaseHealth(20, 1, "level_01");

            var result = (LevelCompletedResult)InvokePrivateMethodWithResult(root, "BuildLevelCompletedResult");

            Assert.AreEqual(1, result.Stars);
        }

        [UnityTest]
        public IEnumerator FeedingResultIntoPlayerProfileService_PersistsCompletionAndStars()
        {
            LevelCompositionRoot root = CreateRootWithBaseHealth(20, 20, "level_01");
            var result = (LevelCompletedResult)InvokePrivateMethodWithResult(root, "BuildLevelCompletedResult");

            var repository = new SaveFileRepository(_testDirectory);
            var saveService = new SaveService(repository);
            PlayerProfileSaveData data = saveService.LoadOrCreateDefault(null, "level_01");
            var profileService = new PlayerProfileService(saveService, data);

            profileService.SetLevelCompleted(result);

            LevelProgressSnapshot progress = profileService.GetLevelProgress("level_01");
            Assert.IsTrue(progress.IsCompleted);
            Assert.AreEqual(3, progress.BestStars);
            yield return null;
        }
    }
}
