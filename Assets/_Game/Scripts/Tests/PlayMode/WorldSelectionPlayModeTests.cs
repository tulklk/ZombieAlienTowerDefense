using System.Collections.Generic;
using System.Reflection;
using AlienDefense.Building;
using AlienDefense.Core;
using AlienDefense.Economy;
using AlienDefense.Enemies;
using AlienDefense.Towers;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AlienDefense.Tests.PlayMode
{
    public class WorldSelectionPlayModeTests
    {
        private const string BuildNodeLayerName = "BuildNode";

        private readonly List<GameObject> _spawnedObjects = new List<GameObject>();
        private readonly List<Object> _scriptableObjects = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            // DestroyImmediate (not Destroy) so leftover colliders from this synchronous [Test] don't
            // linger into the next one, since Destroy() is deferred and these tests never yield a frame.
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

        private Camera CreateCamera()
        {
            var go = new GameObject("TestCamera");
            _spawnedObjects.Add(go);
            go.transform.position = new Vector3(0f, 10f, -10f);
            go.transform.LookAt(Vector3.zero);
            return go.AddComponent<Camera>();
        }

        private BuildNode CreateBuildNode(Vector3 position, int layer)
        {
            var go = new GameObject("TestBuildNode");
            go.layer = layer;
            _spawnedObjects.Add(go);
            go.transform.position = position;
            go.AddComponent<BoxCollider>();

            var buildPointObject = new GameObject("BuildPoint");
            buildPointObject.transform.SetParent(go.transform);
            _spawnedObjects.Add(buildPointObject);

            var node = go.AddComponent<BuildNode>();
            SetPrivateField(node, "_buildPoint", buildPointObject.transform);
            return node;
        }

        private TowerController CreateTowerPrefabTemplate()
        {
            var go = new GameObject("TestTowerPrefab");
            go.SetActive(false);
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

        private TowerDefinition CreateTowerDefinition(TowerController prefab)
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
            SetPrivateField(definition, "_buildCost", 50);
            SetPrivateField(definition, "_defaultTargetingMode", TargetingMode.Closest);
            return definition;
        }

        private BuildService CreateBuildService()
        {
            var selection = new BuildSelectionService();
            selection.SelectTower(CreateTowerDefinition(CreateTowerPrefabTemplate()));

            var economy = new EconomyService(1000);

            var runtimeParent = new GameObject("TowerRuntimeParent");
            _spawnedObjects.Add(runtimeParent);
            var factory = new TowerFactory(runtimeParent.transform, new EnemyRegistry(), null, null);

            var gameFlow = new GameFlowController();
            gameFlow.BeginPreparingWave();

            return new BuildService(selection, economy, factory, gameFlow);
        }

        private WorldSelectionController CreateController(Camera camera, int layerMaskValue)
        {
            var go = new GameObject("TestWorldSelectionController");
            _spawnedObjects.Add(go);
            var controller = go.AddComponent<WorldSelectionController>();
            SetPrivateField(controller, "_worldCamera", camera);
            SetPrivateField(controller, "_interactableLayerMask", (LayerMask)layerMaskValue);
            return controller;
        }

        [Test]
        public void HandlePointerDown_HitsBuildNode_BuildsSuccessfully()
        {
            int buildNodeLayer = LayerMask.NameToLayer(BuildNodeLayerName);
            Assume.That(buildNodeLayer, Is.GreaterThanOrEqualTo(0), "BuildNode layer must exist in this project.");

            Camera camera = CreateCamera();
            BuildNode node = CreateBuildNode(Vector3.zero, buildNodeLayer);
            WorldSelectionController controller = CreateController(camera, 1 << buildNodeLayer);
            controller.Initialize(CreateBuildService(), new TowerSelectionService());

            Vector2 screenPosition = camera.WorldToScreenPoint(Vector3.zero);
            bool handled = controller.HandlePointerDown(screenPosition, isPointerOverUI: false);

            Assert.IsTrue(handled);
            Assert.AreEqual(BuildNodeState.Occupied, node.State);
        }

        [Test]
        public void HandlePointerDown_PointerOverUI_DoesNotBuild()
        {
            int buildNodeLayer = LayerMask.NameToLayer(BuildNodeLayerName);
            Assume.That(buildNodeLayer, Is.GreaterThanOrEqualTo(0), "BuildNode layer must exist in this project.");

            Camera camera = CreateCamera();
            BuildNode node = CreateBuildNode(Vector3.zero, buildNodeLayer);
            WorldSelectionController controller = CreateController(camera, 1 << buildNodeLayer);
            controller.Initialize(CreateBuildService(), new TowerSelectionService());

            Vector2 screenPosition = camera.WorldToScreenPoint(Vector3.zero);
            bool handled = controller.HandlePointerDown(screenPosition, isPointerOverUI: true);

            Assert.IsFalse(handled);
            Assert.AreEqual(BuildNodeState.Available, node.State);
        }

        [Test]
        public void HandlePointerDown_NodeOnWrongLayer_IsIgnored()
        {
            int buildNodeLayer = LayerMask.NameToLayer(BuildNodeLayerName);
            Assume.That(buildNodeLayer, Is.GreaterThanOrEqualTo(0), "BuildNode layer must exist in this project.");

            Camera camera = CreateCamera();
            BuildNode node = CreateBuildNode(Vector3.zero, 0);
            WorldSelectionController controller = CreateController(camera, 1 << buildNodeLayer);
            controller.Initialize(CreateBuildService(), new TowerSelectionService());

            Vector2 screenPosition = camera.WorldToScreenPoint(Vector3.zero);
            bool handled = controller.HandlePointerDown(screenPosition, isPointerOverUI: false);

            Assert.IsFalse(handled);
            Assert.AreEqual(BuildNodeState.Available, node.State);
        }

        [Test]
        public void HandlePointerDown_NullCamera_FailsFastWithoutThrowing()
        {
            var go = new GameObject("TestWorldSelectionController");
            _spawnedObjects.Add(go);
            var controller = go.AddComponent<WorldSelectionController>();
            controller.Initialize(CreateBuildService(), new TowerSelectionService());

            bool handled = controller.HandlePointerDown(Vector2.zero, isPointerOverUI: false);

            Assert.IsFalse(handled);
        }

        [Test]
        public void HandlePointerDown_InputDisabled_IsIgnored()
        {
            int buildNodeLayer = LayerMask.NameToLayer(BuildNodeLayerName);
            Assume.That(buildNodeLayer, Is.GreaterThanOrEqualTo(0), "BuildNode layer must exist in this project.");

            Camera camera = CreateCamera();
            BuildNode node = CreateBuildNode(Vector3.zero, buildNodeLayer);
            WorldSelectionController controller = CreateController(camera, 1 << buildNodeLayer);
            controller.Initialize(CreateBuildService(), new TowerSelectionService());
            controller.SetInputEnabled(false);

            Vector2 screenPosition = camera.WorldToScreenPoint(Vector3.zero);
            bool handled = controller.HandlePointerDown(screenPosition, isPointerOverUI: false);

            Assert.IsFalse(handled);
            Assert.AreEqual(BuildNodeState.Available, node.State);
        }
    }
}
