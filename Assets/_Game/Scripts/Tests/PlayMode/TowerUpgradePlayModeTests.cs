using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using AlienDefense.Combat;
using AlienDefense.Core;
using AlienDefense.Economy;
using AlienDefense.Enemies;
using AlienDefense.Towers;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AlienDefense.Tests.PlayMode
{
    public class TowerUpgradePlayModeTests
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
                    Object.Destroy(go);
                }
            }

            _spawnedObjects.Clear();

            foreach (Object asset in _scriptableObjects)
            {
                if (asset != null)
                {
                    Object.Destroy(asset);
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

        private static T CreateSilently<T>(System.Func<T> factory)
        {
            bool previousLogEnabled = Debug.unityLogger.logEnabled;
            Debug.unityLogger.logEnabled = false;
            T result = factory();
            Debug.unityLogger.logEnabled = previousLogEnabled;
            return result;
        }

        private TowerController CreateActiveTower(TowerDefinition definition, Vector3 position)
        {
            var go = new GameObject("TestTower");
            go.SetActive(false);
            _spawnedObjects.Add(go);
            go.transform.position = position;

            var targeting = go.AddComponent<TowerTargeting>();
            var attack = go.AddComponent<TowerAttackController>();
            var visual = go.AddComponent<TowerVisual>();
            var controller = go.AddComponent<TowerController>();

            var firePointObject = new GameObject("FirePoint");
            firePointObject.transform.SetParent(go.transform, false);
            _spawnedObjects.Add(firePointObject);

            SetPrivateField(attack, "_firePoint", firePointObject.transform);
            SetPrivateField(controller, "_targeting", targeting);
            SetPrivateField(controller, "_attack", attack);
            SetPrivateField(controller, "_visual", visual);

            go.SetActive(true);

            var gameFlow = new GameFlowController();
            gameFlow.BeginPreparingWave();
            gameFlow.BeginPlayingWave();
            controller.Initialize(definition, new EnemyRegistry(), CreateProjectileFactory(out _), gameFlow);

            return controller;
        }

        private ProjectileFactory CreateProjectileFactory(out ProjectileDefinition projectileDefinition)
        {
            var template = new GameObject("TestProjectileTemplate");
            template.SetActive(false);
            _spawnedObjects.Add(template);
            template.AddComponent<ProjectileController>();

            var definition = CreateSilently(() => ScriptableObject.CreateInstance<ProjectileDefinition>());
            _scriptableObjects.Add(definition);
            SetPrivateField(definition, "_prefab", template.GetComponent<ProjectileController>());
            SetPrivateField(definition, "_speed", 30f);
            SetPrivateField(definition, "_maximumLifetime", 5f);
            SetPrivateField(definition, "_hitDistance", 0.3f);
            SetPrivateField(definition, "_poolPrewarmCount", 0);
            SetPrivateField(definition, "_poolDefaultCapacity", 10);
            SetPrivateField(definition, "_poolMaximumSize", 10);
            projectileDefinition = definition;

            var runtimeParent = new GameObject("ProjectileRuntimeParent");
            _spawnedObjects.Add(runtimeParent);
            return new ProjectileFactory(new ProjectilePoolRegistry(runtimeParent.transform));
        }

        private TowerDefinition CreateTowerDefinition()
        {
            var definition = CreateSilently(() => ScriptableObject.CreateInstance<TowerDefinition>());
            _scriptableObjects.Add(definition);

            var level1 = new TowerLevelData();
            SetPrivateField(level1, "_upgradeCost", 0);
            SetPrivateField(level1, "_damage", 1000f);
            SetPrivateField(level1, "_range", 5f);
            SetPrivateField(level1, "_attacksPerSecond", 10f);
            SetPrivateField(level1, "_turretRotationSpeed", 720f);

            var level2 = new TowerLevelData();
            SetPrivateField(level2, "_upgradeCost", 90);
            SetPrivateField(level2, "_damage", 2000f);
            SetPrivateField(level2, "_range", 6f);
            SetPrivateField(level2, "_attacksPerSecond", 12f);
            SetPrivateField(level2, "_turretRotationSpeed", 720f);

            SetPrivateField(definition, "_levels", new[] { level1, level2 });
            SetPrivateField(definition, "_defaultTargetingMode", TargetingMode.Closest);
            return definition;
        }

        [UnityTest]
        public IEnumerator Upgrade_TowerContinuesAttacking_WithNewStats()
        {
            TowerDefinition definition = CreateTowerDefinition();
            TowerController tower = CreateActiveTower(definition, Vector3.zero);

            var economy = new EconomyService(200);
            var gameFlow = new GameFlowController();
            gameFlow.BeginPreparingWave();
            var upgradeService = new TowerUpgradeService(economy, gameFlow);

            UpgradeOperationResult result = upgradeService.TryUpgrade(tower);

            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual(6f, tower.CurrentStats.Range, 0.001f);
            Assert.AreEqual(110, economy.CurrentResource);

            yield return null;
            yield return null;

            Assert.AreEqual(1, tower.CurrentLevelIndex);
        }
    }
}
