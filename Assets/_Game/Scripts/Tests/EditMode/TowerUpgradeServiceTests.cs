using System.Collections.Generic;
using System.Reflection;
using AlienDefense.Core;
using AlienDefense.Economy;
using AlienDefense.Towers;
using NUnit.Framework;
using UnityEngine;

namespace AlienDefense.Tests.EditMode
{
    public class TowerUpgradeServiceTests
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

        private static T CreateSilently<T>(System.Func<T> factory)
        {
            bool previousLogEnabled = Debug.unityLogger.logEnabled;
            Debug.unityLogger.logEnabled = false;
            T result = factory();
            Debug.unityLogger.logEnabled = previousLogEnabled;
            return result;
        }

        private TowerController CreateInitializedTower(params (int upgradeCost, float damage, float range, float aps, float rotationSpeed)[] levels)
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

            controller.Initialize(definition, null, null, null, CreatePreparingWaveGameFlow());
            return controller;
        }

        private static GameFlowController CreatePreparingWaveGameFlow()
        {
            var gameFlow = new GameFlowController();
            gameFlow.BeginPreparingWave();
            return gameFlow;
        }

        [Test]
        public void TryUpgrade_Success_DeductsCorrectCost_AndAppliesNextLevel()
        {
            TowerController tower = CreateInitializedTower(
                (0, 20f, 4f, 1f, 720f),
                (90, 32f, 4.4f, 1.2f, 720f));
            var economy = new EconomyService(200);
            var service = new TowerUpgradeService(economy, CreatePreparingWaveGameFlow());

            UpgradeOperationResult result = service.TryUpgrade(tower);

            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual(110, economy.CurrentResource);
            Assert.AreEqual(1, tower.CurrentLevelIndex);
            Assert.AreEqual(32f, tower.CurrentStats.Damage, 0.001f);
        }

        [Test]
        public void TryUpgrade_Success_IncreasesInvestedResource()
        {
            TowerController tower = CreateInitializedTower(
                (0, 20f, 4f, 1f, 720f),
                (90, 32f, 4.4f, 1.2f, 720f));
            var economy = new EconomyService(200);
            var service = new TowerUpgradeService(economy, CreatePreparingWaveGameFlow());

            service.TryUpgrade(tower);

            Assert.AreEqual(90, tower.TotalInvestedResource);
        }

        [Test]
        public void TryUpgrade_AlreadyMaxLevel_ReturnsAlreadyMaxLevel_AndDoesNotCharge()
        {
            TowerController tower = CreateInitializedTower((0, 20f, 4f, 1f, 720f));
            var economy = new EconomyService(200);
            var service = new TowerUpgradeService(economy, CreatePreparingWaveGameFlow());

            UpgradeOperationResult result = service.TryUpgrade(tower);

            Assert.AreEqual(UpgradeResult.AlreadyMaxLevel, result.Status);
            Assert.AreEqual(200, economy.CurrentResource);
        }

        [Test]
        public void TryUpgrade_NotEnoughResource_ReturnsNotEnoughResource()
        {
            TowerController tower = CreateInitializedTower(
                (0, 20f, 4f, 1f, 720f),
                (90, 32f, 4.4f, 1.2f, 720f));
            var economy = new EconomyService(10);
            var service = new TowerUpgradeService(economy, CreatePreparingWaveGameFlow());

            UpgradeOperationResult result = service.TryUpgrade(tower);

            Assert.AreEqual(UpgradeResult.NotEnoughResource, result.Status);
            Assert.AreEqual(0, tower.CurrentLevelIndex);
            Assert.AreEqual(10, economy.CurrentResource);
        }

        [Test]
        public void TryUpgrade_GameStateInvalid_ReturnsGameNotPlaying()
        {
            TowerController tower = CreateInitializedTower(
                (0, 20f, 4f, 1f, 720f),
                (90, 32f, 4.4f, 1.2f, 720f));
            var economy = new EconomyService(200);
            var gameFlow = new GameFlowController();
            var service = new TowerUpgradeService(economy, gameFlow);

            UpgradeOperationResult result = service.TryUpgrade(tower);

            Assert.AreEqual(UpgradeResult.GameNotPlaying, result.Status);
            Assert.AreEqual(200, economy.CurrentResource);
        }

        [Test]
        public void TryUpgrade_InvalidTower_ReturnsInvalidTower()
        {
            var economy = new EconomyService(200);
            var service = new TowerUpgradeService(economy, CreatePreparingWaveGameFlow());

            UpgradeOperationResult result = service.TryUpgrade(null);

            Assert.AreEqual(UpgradeResult.InvalidTower, result.Status);
        }

        [Test]
        public void TryUpgrade_SoldTower_ReturnsInvalidTower()
        {
            TowerController tower = CreateInitializedTower(
                (0, 20f, 4f, 1f, 720f),
                (90, 32f, 4.4f, 1.2f, 720f));
            tower.MarkSold();
            var economy = new EconomyService(200);
            var service = new TowerUpgradeService(economy, CreatePreparingWaveGameFlow());

            UpgradeOperationResult result = service.TryUpgrade(tower);

            Assert.AreEqual(UpgradeResult.InvalidTower, result.Status);
        }

        [Test]
        public void TryUpgrade_FiresTowerUpgradedEvent_ExactlyOnce()
        {
            TowerController tower = CreateInitializedTower(
                (0, 20f, 4f, 1f, 720f),
                (90, 32f, 4.4f, 1.2f, 720f));
            var economy = new EconomyService(200);
            var service = new TowerUpgradeService(economy, CreatePreparingWaveGameFlow());

            int fireCount = 0;
            service.TowerUpgraded += _ => fireCount++;

            service.TryUpgrade(tower);

            Assert.AreEqual(1, fireCount);
        }

        [Test]
        public void TryUpgrade_RefreshesRangeIndicatorScale_ViaCurrentStats()
        {
            TowerController tower = CreateInitializedTower(
                (0, 20f, 4f, 1f, 720f),
                (90, 32f, 4.8f, 1.2f, 720f));
            var economy = new EconomyService(200);
            var service = new TowerUpgradeService(economy, CreatePreparingWaveGameFlow());

            service.TryUpgrade(tower);

            Assert.AreEqual(4.8f, tower.CurrentStats.Range, 0.001f);
        }
    }
}
