using System.Collections.Generic;
using System.Reflection;
using AlienDefense.Towers;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace AlienDefense.Tests.EditMode
{
    public class TowerControllerTests
    {
        private readonly List<GameObject> _spawnedObjects = new List<GameObject>();
        private readonly List<Object> _scriptableObjects = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject go in _spawnedObjects)
            {
                if (go != null)
                {
                    Object.DestroyImmediate(go);
                }
            }

            _spawnedObjects.Clear();

            foreach (Object asset in _scriptableObjects)
            {
                if (asset != null)
                {
                    Object.DestroyImmediate(asset);
                }
            }

            _scriptableObjects.Clear();
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(field, $"Field '{fieldName}' not found on {target.GetType().Name}.");
            field.SetValue(target, value);
        }

        private TowerController CreateTower()
        {
            var go = new GameObject("TestTower");
            _spawnedObjects.Add(go);

            var targeting = go.AddComponent<TowerTargeting>();
            var attack = go.AddComponent<TowerAttackController>();
            var visual = go.AddComponent<TowerVisual>();
            var controller = go.AddComponent<TowerController>();

            var firePointObject = new GameObject("FirePoint");
            firePointObject.transform.SetParent(go.transform);
            _spawnedObjects.Add(firePointObject);

            SetPrivateField(attack, "_firePoint", firePointObject.transform);
            SetPrivateField(controller, "_targeting", targeting);
            SetPrivateField(controller, "_attack", attack);
            SetPrivateField(controller, "_visual", visual);

            return controller;
        }

        private static T CreateSilently<T>(System.Func<T> factory)
        {
            bool previousLogEnabled = Debug.unityLogger.logEnabled;
            Debug.unityLogger.logEnabled = false;
            T result = factory();
            Debug.unityLogger.logEnabled = previousLogEnabled;
            return result;
        }

        private TowerDefinition CreateDefinition(params (int upgradeCost, float damage, float range, float aps, float rotationSpeed)[] levels)
        {
            var definition = CreateSilently(() => ScriptableObject.CreateInstance<TowerDefinition>());
            _scriptableObjects.Add(definition);

            var levelDataArray = new TowerLevelData[levels.Length];
            for (int i = 0; i < levels.Length; i++)
            {
                var levelData = new TowerLevelData();
                SetPrivateField(levelData, "_upgradeCost", levels[i].upgradeCost);
                SetPrivateField(levelData, "_damage", levels[i].damage);
                SetPrivateField(levelData, "_range", levels[i].range);
                SetPrivateField(levelData, "_attacksPerSecond", levels[i].aps);
                SetPrivateField(levelData, "_turretRotationSpeed", levels[i].rotationSpeed);
                levelDataArray[i] = levelData;
            }

            SetPrivateField(definition, "_levels", levelDataArray);
            return definition;
        }

        [Test]
        public void Initialize_AppliesLevelOneStats()
        {
            TowerController tower = CreateTower();
            TowerDefinition definition = CreateDefinition((0, 20f, 4f, 1f, 720f), (90, 32f, 4.4f, 1.2f, 720f));

            tower.Initialize(definition, null, null, null, null);

            Assert.AreEqual(0, tower.CurrentLevelIndex);
            Assert.AreEqual(20f, tower.CurrentStats.Damage, 0.001f);
            Assert.AreEqual(4f, tower.CurrentStats.Range, 0.001f);
        }

        [Test]
        public void ApplyLevel_UpdatesCurrentStats()
        {
            TowerController tower = CreateTower();
            TowerDefinition definition = CreateDefinition((0, 20f, 4f, 1f, 720f), (90, 32f, 4.4f, 1.2f, 720f));
            tower.Initialize(definition, null, null, null, null);

            bool result = tower.ApplyLevel(1);

            Assert.IsTrue(result);
            Assert.AreEqual(1, tower.CurrentLevelIndex);
            Assert.AreEqual(32f, tower.CurrentStats.Damage, 0.001f);
            Assert.AreEqual(4.4f, tower.CurrentStats.Range, 0.001f);
        }

        [Test]
        public void ApplyLevel_RejectsOutOfRangeIndex()
        {
            TowerController tower = CreateTower();
            TowerDefinition definition = CreateDefinition((0, 20f, 4f, 1f, 720f));
            tower.Initialize(definition, null, null, null, null);

            LogAssert.Expect(LogType.Warning, "[TowerController] Invalid level index 5.");
            bool result = tower.ApplyLevel(5);

            Assert.IsFalse(result);
            Assert.AreEqual(0, tower.CurrentLevelIndex);
        }

        [Test]
        public void ApplyLevel_BeforeInitialize_IsRejected()
        {
            TowerController tower = CreateTower();

            LogAssert.Expect(LogType.Warning, "[TowerController] ApplyLevel called before Initialize.");
            bool result = tower.ApplyLevel(0);

            Assert.IsFalse(result);
        }
    }
}
