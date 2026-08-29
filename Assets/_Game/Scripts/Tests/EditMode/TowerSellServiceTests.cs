using System.Collections.Generic;
using System.Reflection;
using AlienDefense.Building;
using AlienDefense.Core;
using AlienDefense.Economy;
using AlienDefense.Enemies;
using AlienDefense.Towers;
using NUnit.Framework;
using UnityEngine;

namespace AlienDefense.Tests.EditMode
{
    public class TowerSellServiceTests
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

        private BuildNode CreateBuildNode()
        {
            var go = new GameObject("TestBuildNode");
            _spawnedObjects.Add(go);
            var buildPointObject = new GameObject("BuildPoint");
            buildPointObject.transform.SetParent(go.transform);
            _spawnedObjects.Add(buildPointObject);

            var node = go.AddComponent<BuildNode>();
            SetPrivateField(node, "_buildPoint", buildPointObject.transform);
            return node;
        }

        private TowerFactory CreateFactory()
        {
            var runtimeParent = new GameObject("TowerRuntimeParent");
            _spawnedObjects.Add(runtimeParent);
            return new TowerFactory(runtimeParent.transform, new EnemyRegistry(), null, null, null);
        }

        /// <summary>Builds a tower and assigns it to a node the same way BuildService would, including investment.</summary>
        private (TowerController tower, BuildNode node) CreateBuiltTower(int buildCost, float sellPercentage)
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

            var level = new TowerLevelData();
            SetPrivateField(level, "_upgradeCost", 0);
            SetPrivateField(level, "_damage", 10f);
            SetPrivateField(level, "_range", 4f);
            SetPrivateField(level, "_attacksPerSecond", 1f);
            SetPrivateField(level, "_turretRotationSpeed", 360f);

            SetPrivateField(definition, "_levels", new[] { level });
            SetPrivateField(definition, "_sellPercentage", sellPercentage);

            controller.Initialize(definition, null, null, null, CreatePreparingWaveGameFlow());
            controller.RegisterInvestment(buildCost);

            BuildNode node = CreateBuildNode();
            node.AssignTower(controller);
            controller.SetBuildNodeOwner(node);

            return (controller, node);
        }

        private static GameFlowController CreatePreparingWaveGameFlow()
        {
            var gameFlow = new GameFlowController();
            gameFlow.BeginPreparingWave();
            return gameFlow;
        }

        [Test]
        public void TrySell_Success_PaysCorrectSellValue()
        {
            (TowerController tower, BuildNode node) = CreateBuiltTower(100, 0.5f);
            var economy = new EconomyService(0);
            var service = new TowerSellService(economy, CreatePreparingWaveGameFlow(), CreateFactory(), new TowerSelectionService());

            SellOperationResult result = service.TrySell(tower);

            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual(50, result.SellValue);
            Assert.AreEqual(50, economy.CurrentResource);
        }

        [Test]
        public void TrySell_Success_ReleasesNode()
        {
            (TowerController tower, BuildNode node) = CreateBuiltTower(100, 0.5f);
            var economy = new EconomyService(0);
            var service = new TowerSellService(economy, CreatePreparingWaveGameFlow(), CreateFactory(), new TowerSelectionService());

            service.TrySell(tower);

            Assert.AreEqual(BuildNodeState.Available, node.State);
            Assert.IsNull(node.CurrentTower);
        }

        [Test]
        public void TrySell_Success_ClearsSelectionIfTowerWasSelected()
        {
            (TowerController tower, BuildNode node) = CreateBuiltTower(100, 0.5f);
            var economy = new EconomyService(0);
            var selection = new TowerSelectionService();
            selection.Select(tower);
            var service = new TowerSellService(economy, CreatePreparingWaveGameFlow(), CreateFactory(), selection);

            service.TrySell(tower);

            Assert.IsNull(selection.SelectedTower);
        }

        [Test]
        public void TrySell_Success_MarksTowerSold()
        {
            (TowerController tower, BuildNode node) = CreateBuiltTower(100, 0.5f);
            var economy = new EconomyService(0);
            var service = new TowerSellService(economy, CreatePreparingWaveGameFlow(), CreateFactory(), new TowerSelectionService());

            service.TrySell(tower);

            Assert.IsTrue(tower.IsSold);
        }

        [Test]
        public void TrySell_SecondAttempt_IsRejected_AndDoesNotPayTwice()
        {
            (TowerController tower, BuildNode node) = CreateBuiltTower(100, 0.5f);
            var economy = new EconomyService(0);
            var service = new TowerSellService(economy, CreatePreparingWaveGameFlow(), CreateFactory(), new TowerSelectionService());

            service.TrySell(tower);
            SellOperationResult second = service.TrySell(tower);

            Assert.AreEqual(SellResult.InvalidTower, second.Status);
            Assert.AreEqual(50, economy.CurrentResource);
        }

        [Test]
        public void TrySell_NodeCanBeRebuiltAfterSell()
        {
            (TowerController tower, BuildNode node) = CreateBuiltTower(100, 0.5f);
            var economy = new EconomyService(0);
            var service = new TowerSellService(economy, CreatePreparingWaveGameFlow(), CreateFactory(), new TowerSelectionService());

            service.TrySell(tower);

            TowerController secondTower = new GameObject("SecondTower").AddComponent<TowerController>();
            _spawnedObjects.Add(secondTower.gameObject);
            bool assigned = node.AssignTower(secondTower);

            Assert.IsTrue(assigned);
            Assert.AreEqual(BuildNodeState.Occupied, node.State);
        }

        [Test]
        public void TrySell_InvalidTower_ReturnsInvalidTower()
        {
            var economy = new EconomyService(0);
            var service = new TowerSellService(economy, CreatePreparingWaveGameFlow(), CreateFactory(), new TowerSelectionService());

            SellOperationResult result = service.TrySell(null);

            Assert.AreEqual(SellResult.InvalidTower, result.Status);
        }

        [Test]
        public void TrySell_GameStateInvalid_ReturnsGameNotPlaying()
        {
            (TowerController tower, BuildNode node) = CreateBuiltTower(100, 0.5f);
            var economy = new EconomyService(0);
            var service = new TowerSellService(economy, new GameFlowController(), CreateFactory(), new TowerSelectionService());

            SellOperationResult result = service.TrySell(tower);

            Assert.AreEqual(SellResult.GameNotPlaying, result.Status);
            Assert.AreEqual(BuildNodeState.Occupied, node.State);
        }

        [Test]
        public void TrySell_NoOwnerNode_ReturnsInvalidNode()
        {
            var go = new GameObject("TestTowerNoNode");
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
            var level = new TowerLevelData();
            SetPrivateField(level, "_upgradeCost", 0);
            SetPrivateField(level, "_damage", 10f);
            SetPrivateField(level, "_range", 4f);
            SetPrivateField(level, "_attacksPerSecond", 1f);
            SetPrivateField(level, "_turretRotationSpeed", 360f);
            SetPrivateField(definition, "_levels", new[] { level });
            controller.Initialize(definition, null, null, null, CreatePreparingWaveGameFlow());
            // Deliberately not calling SetBuildNodeOwner.

            var economy = new EconomyService(0);
            var service = new TowerSellService(economy, CreatePreparingWaveGameFlow(), CreateFactory(), new TowerSelectionService());

            SellOperationResult result = service.TrySell(controller);

            Assert.AreEqual(SellResult.InvalidNode, result.Status);
        }
    }
}
