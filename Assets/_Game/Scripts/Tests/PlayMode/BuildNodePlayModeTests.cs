using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using AlienDefense.Building;
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
    public class BuildNodePlayModeTests
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

        private BuildNode CreateBuildNode(Vector3 position)
        {
            var go = new GameObject("TestBuildNode");
            _spawnedObjects.Add(go);
            go.transform.position = position;

            var buildPointObject = new GameObject("BuildPoint");
            buildPointObject.transform.SetParent(go.transform);
            buildPointObject.transform.position = position;
            _spawnedObjects.Add(buildPointObject);

            var node = go.AddComponent<BuildNode>();
            SetPrivateField(node, "_buildPoint", buildPointObject.transform);
            return node;
        }

        /// <summary>Builds a genuinely active tower prefab template, matching how real saved Tower prefabs behave.</summary>
        private TowerController CreateActiveTowerPrefabTemplate()
        {
            var go = new GameObject("TestTowerPrefab");
            go.SetActive(false);
            _spawnedObjects.Add(go);

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

        private TowerDefinition CreateTowerDefinition(TowerController prefab, ProjectileDefinition projectileDefinition)
        {
            var definition = CreateSilently(() => ScriptableObject.CreateInstance<TowerDefinition>());
            _scriptableObjects.Add(definition);

            var level = new TowerLevelData();
            SetPrivateField(level, "_upgradeCost", 0);
            SetPrivateField(level, "_damage", 10f);
            SetPrivateField(level, "_range", 5f);
            SetPrivateField(level, "_attacksPerSecond", 4f);
            SetPrivateField(level, "_turretRotationSpeed", 720f);

            SetPrivateField(definition, "_levels", new[] { level });
            SetPrivateField(definition, "_prefab", prefab);
            SetPrivateField(definition, "_projectileDefinition", projectileDefinition);
            SetPrivateField(definition, "_buildCost", 50);
            SetPrivateField(definition, "_defaultTargetingMode", TargetingMode.Closest);
            return definition;
        }

        private EnemyController CreateStationaryEnemy(Vector3 position)
        {
            var pathObject = new GameObject("TestEnemyPath");
            _spawnedObjects.Add(pathObject);
            var waypointA = new GameObject("A");
            waypointA.transform.position = new Vector3(0f, 0f, 0f);
            _spawnedObjects.Add(waypointA);
            var waypointB = new GameObject("B");
            waypointB.transform.position = new Vector3(0f, 0f, 50f);
            _spawnedObjects.Add(waypointB);
            EnemyPath3D path = CreateSilently(() => pathObject.AddComponent<EnemyPath3D>());
            SetPrivateField(path, "_waypoints", new[] { waypointA.transform, waypointB.transform });

            var go = new GameObject("TestEnemy");
            go.SetActive(false);
            _spawnedObjects.Add(go);
            var health = go.AddComponent<EnemyHealth>();
            var movement = go.AddComponent<EnemyMovement>();
            var controller = go.AddComponent<EnemyController>();
            SetPrivateField(controller, "_health", health);
            SetPrivateField(controller, "_movement", movement);

            var definition = CreateSilently(() => ScriptableObject.CreateInstance<EnemyDefinition>());
            _scriptableObjects.Add(definition);

            go.SetActive(true);
            controller.Initialize(definition, path, null, null, new EnemyRegistry(), _ => { }, null);
            controller.Movement.StopMovement();
            controller.transform.position = position;
            return controller;
        }

        [UnityTest]
        public IEnumerator Build_ThenTower_ActuallyDamagesEnemyInRange()
        {
            ProjectileFactory projectileFactory = CreateProjectileFactory(out ProjectileDefinition projectileDefinition);
            TowerController prefab = CreateActiveTowerPrefabTemplate();
            TowerDefinition towerDefinition = CreateTowerDefinition(prefab, projectileDefinition);

            var registry = new EnemyRegistry();
            EnemyController enemy = CreateStationaryEnemy(new Vector3(1f, 0f, 0f));
            registry.Register(enemy);

            var selection = new BuildSelectionService();
            selection.SelectTower(towerDefinition);

            var economy = new EconomyService(1000);

            var runtimeParent = new GameObject("TowerRuntimeParent");
            _spawnedObjects.Add(runtimeParent);
            var factory = new TowerFactory(runtimeParent.transform, registry, projectileFactory, null);

            var gameFlow = new GameFlowController();
            gameFlow.BeginPreparingWave();
            gameFlow.BeginPlayingWave();

            var service = new BuildService(selection, economy, factory, gameFlow);
            BuildNode node = CreateBuildNode(Vector3.zero);

            BuildOperationResult result = service.TryBuild(node);
            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual(BuildNodeState.Occupied, node.State);

            float startHealth = enemy.Health.CurrentHealth;

            for (int i = 0; i < 60; i++)
            {
                yield return null;
            }

            Assert.Less(enemy.Health.CurrentHealth, startHealth);
        }

        [UnityTest]
        public IEnumerator Build_OnOccupiedNode_SecondAttemptFails()
        {
            ProjectileFactory projectileFactory = CreateProjectileFactory(out ProjectileDefinition projectileDefinition);
            TowerController prefab = CreateActiveTowerPrefabTemplate();
            TowerDefinition towerDefinition = CreateTowerDefinition(prefab, projectileDefinition);

            var selection = new BuildSelectionService();
            selection.SelectTower(towerDefinition);

            var economy = new EconomyService(1000);

            var runtimeParent = new GameObject("TowerRuntimeParent");
            _spawnedObjects.Add(runtimeParent);
            var factory = new TowerFactory(runtimeParent.transform, new EnemyRegistry(), projectileFactory, null);

            var gameFlow = new GameFlowController();
            gameFlow.BeginPreparingWave();

            var service = new BuildService(selection, economy, factory, gameFlow);
            BuildNode node = CreateBuildNode(Vector3.zero);

            BuildOperationResult first = service.TryBuild(node);
            yield return null;
            BuildOperationResult second = service.TryBuild(node);

            Assert.IsTrue(first.IsSuccess);
            Assert.AreEqual(BuildResult.NodeUnavailable, second.Status);
            Assert.AreEqual(950, economy.CurrentResource);
        }
    }
}
