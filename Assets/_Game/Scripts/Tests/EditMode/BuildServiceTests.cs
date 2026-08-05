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
    public class BuildServiceTests
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

        private TowerController CreateTowerPrefabTemplate()
        {
            var go = new GameObject("TestTowerPrefab");
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

        private TowerDefinition CreateTowerDefinition(TowerController prefab, int buildCost)
        {
            var definition = CreateSilently(() => ScriptableObject.CreateInstance<TowerDefinition>());
            _scriptableObjects.Add(definition);

            var level = new TowerLevelData();
            SetPrivateField(level, "_upgradeCost", 0);
            SetPrivateField(level, "_damage", 10f);
            SetPrivateField(level, "_range", 4f);
            SetPrivateField(level, "_attacksPerSecond", 1f);
            SetPrivateField(level, "_turretRotationSpeed", 360f);

            SetPrivateField(definition, "_levels", new[] { level });
            SetPrivateField(definition, "_prefab", prefab);
            SetPrivateField(definition, "_buildCost", buildCost);
            SetPrivateField(definition, "_defaultTargetingMode", TargetingMode.Closest);
            return definition;
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
            return new TowerFactory(runtimeParent.transform, new EnemyRegistry(), null, null);
        }

        private static GameFlowController CreatePreparingWaveGameFlow()
        {
            var gameFlow = new GameFlowController();
            gameFlow.BeginPreparingWave();
            return gameFlow;
        }

        [Test]
        public void TryBuild_NoSelection_ReturnsNoTowerSelected()
        {
            var selection = new BuildSelectionService();
            var economy = new EconomyService(1000);
            var service = new BuildService(selection, economy, CreateFactory(), CreatePreparingWaveGameFlow());

            BuildOperationResult result = service.TryBuild(CreateBuildNode());

            Assert.AreEqual(BuildResult.NoTowerSelected, result.Status);
            Assert.IsFalse(result.IsSuccess);
        }

        [Test]
        public void TryBuild_NullNode_ReturnsInvalidNode()
        {
            var selection = new BuildSelectionService();
            selection.SelectTower(CreateTowerDefinition(CreateTowerPrefabTemplate(), 50));
            var economy = new EconomyService(1000);
            var service = new BuildService(selection, economy, CreateFactory(), CreatePreparingWaveGameFlow());

            BuildOperationResult result = service.TryBuild(null);

            Assert.AreEqual(BuildResult.InvalidNode, result.Status);
        }

        [Test]
        public void TryBuild_OccupiedNode_ReturnsNodeUnavailable()
        {
            var selection = new BuildSelectionService();
            TowerDefinition definition = CreateTowerDefinition(CreateTowerPrefabTemplate(), 50);
            selection.SelectTower(definition);
            var economy = new EconomyService(1000);
            TowerFactory factory = CreateFactory();
            var service = new BuildService(selection, economy, factory, CreatePreparingWaveGameFlow());
            BuildNode node = CreateBuildNode();

            BuildOperationResult first = service.TryBuild(node);
            BuildOperationResult second = service.TryBuild(node);

            Assert.IsTrue(first.IsSuccess);
            Assert.AreEqual(BuildResult.NodeUnavailable, second.Status);
        }

        [Test]
        public void TryBuild_NotEnoughResource_ReturnsNotEnoughResource()
        {
            var selection = new BuildSelectionService();
            selection.SelectTower(CreateTowerDefinition(CreateTowerPrefabTemplate(), 50));
            var economy = new EconomyService(10);
            var service = new BuildService(selection, economy, CreateFactory(), CreatePreparingWaveGameFlow());

            BuildOperationResult result = service.TryBuild(CreateBuildNode());

            Assert.AreEqual(BuildResult.NotEnoughResource, result.Status);
            Assert.AreEqual(10, economy.CurrentResource);
        }

        [Test]
        public void TryBuild_GameStateInvalid_ReturnsGameNotPlaying()
        {
            var selection = new BuildSelectionService();
            selection.SelectTower(CreateTowerDefinition(CreateTowerPrefabTemplate(), 50));
            var economy = new EconomyService(1000);
            var gameFlow = new GameFlowController();
            var service = new BuildService(selection, economy, CreateFactory(), gameFlow);

            BuildOperationResult result = service.TryBuild(CreateBuildNode());

            Assert.AreEqual(BuildResult.GameNotPlaying, result.Status);
        }

        [Test]
        public void TryBuild_FactoryUnavailable_ReturnsFactoryUnavailable()
        {
            var selection = new BuildSelectionService();
            selection.SelectTower(CreateTowerDefinition(CreateTowerPrefabTemplate(), 50));
            var economy = new EconomyService(1000);
            var service = new BuildService(selection, economy, null, CreatePreparingWaveGameFlow());

            BuildOperationResult result = service.TryBuild(CreateBuildNode());

            Assert.AreEqual(BuildResult.FactoryUnavailable, result.Status);
        }

        [Test]
        public void TryBuild_SpawnFails_ReturnsSpawnFailed_AndDoesNotChargeResource()
        {
            var selection = new BuildSelectionService();
            TowerDefinition definition = CreateTowerDefinition(null, 50);
            selection.SelectTower(definition);
            var economy = new EconomyService(1000);
            var service = new BuildService(selection, economy, CreateFactory(), CreatePreparingWaveGameFlow());
            BuildNode node = CreateBuildNode();

            BuildOperationResult result = CreateSilently(() => service.TryBuild(node));

            Assert.AreEqual(BuildResult.SpawnFailed, result.Status);
            Assert.AreEqual(1000, economy.CurrentResource);
        }

        [Test]
        public void TryBuild_PaymentFails_RollsBackTower_AndNodeStaysAvailable()
        {
            var selection = new BuildSelectionService();
            TowerDefinition definition = CreateTowerDefinition(CreateTowerPrefabTemplate(), 0);
            selection.SelectTower(definition);
            var economy = new EconomyService(1000);
            var service = new BuildService(selection, economy, CreateFactory(), CreatePreparingWaveGameFlow());
            BuildNode node = CreateBuildNode();

            BuildOperationResult result = service.TryBuild(node);

            Assert.AreEqual(BuildResult.PaymentFailed, result.Status);
            Assert.AreEqual(BuildNodeState.Available, node.State);
            Assert.IsNull(node.CurrentTower);
        }

        [Test]
        public void TryBuild_Success_DeductsCorrectCost()
        {
            var selection = new BuildSelectionService();
            selection.SelectTower(CreateTowerDefinition(CreateTowerPrefabTemplate(), 75));
            var economy = new EconomyService(200);
            var service = new BuildService(selection, economy, CreateFactory(), CreatePreparingWaveGameFlow());

            BuildOperationResult result = service.TryBuild(CreateBuildNode());

            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual(125, economy.CurrentResource);
            Assert.AreEqual(125, result.RemainingResource);
        }

        [Test]
        public void TryBuild_Success_AssignsTowerToNode()
        {
            var selection = new BuildSelectionService();
            selection.SelectTower(CreateTowerDefinition(CreateTowerPrefabTemplate(), 75));
            var economy = new EconomyService(200);
            var service = new BuildService(selection, economy, CreateFactory(), CreatePreparingWaveGameFlow());
            BuildNode node = CreateBuildNode();

            BuildOperationResult result = service.TryBuild(node);

            Assert.AreEqual(BuildNodeState.Occupied, node.State);
            Assert.AreEqual(result.SpawnedTower, node.CurrentTower);
            Assert.AreEqual(node, result.SpawnedTower.BuildNodeOwner);
        }

        [Test]
        public void TryBuild_Success_DoesNotChargeOrSpawnTwice()
        {
            var selection = new BuildSelectionService();
            selection.SelectTower(CreateTowerDefinition(CreateTowerPrefabTemplate(), 75));
            var economy = new EconomyService(200);
            var service = new BuildService(selection, economy, CreateFactory(), CreatePreparingWaveGameFlow());
            BuildNode node = CreateBuildNode();

            service.TryBuild(node);
            BuildOperationResult second = service.TryBuild(node);

            Assert.AreEqual(BuildResult.NodeUnavailable, second.Status);
            Assert.AreEqual(125, economy.CurrentResource);
        }
    }
}
