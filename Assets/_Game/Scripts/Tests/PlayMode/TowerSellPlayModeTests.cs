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
    public class TowerSellPlayModeTests
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

        private TowerDefinition CreateTowerDefinition(TowerController prefab, ProjectileDefinition projectileDefinition)
        {
            var definition = CreateSilently(() => ScriptableObject.CreateInstance<TowerDefinition>());
            _scriptableObjects.Add(definition);

            var level = new TowerLevelData();
            SetPrivateField(level, "_upgradeCost", 0);
            SetPrivateField(level, "_damage", 5f);
            SetPrivateField(level, "_range", 5f);
            SetPrivateField(level, "_attacksPerSecond", 8f);
            SetPrivateField(level, "_turretRotationSpeed", 720f);

            SetPrivateField(definition, "_levels", new[] { level });
            SetPrivateField(definition, "_prefab", prefab);
            SetPrivateField(definition, "_projectileDefinition", projectileDefinition);
            SetPrivateField(definition, "_buildCost", 100);
            SetPrivateField(definition, "_sellPercentage", 0.5f);
            SetPrivateField(definition, "_defaultTargetingMode", TargetingMode.Closest);
            return definition;
        }

        private EnemyController CreateStationaryEnemy(Vector3 position)
        {
            var pathObject = new GameObject("TestEnemyPath");
            _spawnedObjects.Add(pathObject);
            var waypointA = new GameObject("A");
            waypointA.transform.position = Vector3.zero;
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

        [UnityTest]
        public IEnumerator Sell_StopsAttack_ReleasesNode_AndDestroysTower()
        {
            ProjectileFactory projectileFactory = CreateProjectileFactory(out ProjectileDefinition projectileDefinition);
            TowerController prefab = CreateActiveTowerPrefabTemplate();
            TowerDefinition towerDefinition = CreateTowerDefinition(prefab, projectileDefinition);

            var registry = new EnemyRegistry();
            EnemyController enemy = CreateStationaryEnemy(new Vector3(1f, 0f, 0f));
            registry.Register(enemy);

            var runtimeParent = new GameObject("TowerRuntimeParent");
            _spawnedObjects.Add(runtimeParent);
            var towerFactory = new TowerFactory(runtimeParent.transform, registry, projectileFactory, null);

            var gameFlow = new GameFlowController();
            gameFlow.BeginPreparingWave();
            gameFlow.BeginPlayingWave();

            BuildNode node = CreateBuildNode(Vector3.zero);
            TowerController tower = towerFactory.Create(towerDefinition, node.BuildPoint.position, node.BuildPoint.rotation);
            node.AssignTower(tower);
            tower.SetBuildNodeOwner(node);
            tower.RegisterInvestment(towerDefinition.BuildCost);

            for (int i = 0; i < 30; i++)
            {
                yield return null;
            }

            float healthBeforeSell = enemy.Health.CurrentHealth;
            Assert.Less(healthBeforeSell, 100f, "Tower should have damaged the enemy before being sold.");

            var economy = new EconomyService(0);
            var selection = new TowerSelectionService();
            selection.Select(tower);
            var sellService = new TowerSellService(economy, gameFlow, towerFactory, selection);

            SellOperationResult result = sellService.TrySell(tower);

            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual(50, result.SellValue);
            Assert.AreEqual(50, economy.CurrentResource);
            Assert.AreEqual(BuildNodeState.Available, node.State);
            Assert.IsNull(selection.SelectedTower);

            float healthAfterSellCommand = enemy.Health.CurrentHealth;

            for (int i = 0; i < 10; i++)
            {
                yield return null;
            }

            Assert.AreEqual(healthAfterSellCommand, enemy.Health.CurrentHealth, 0.01f, "Enemy should take no further damage after the tower is sold.");
            Assert.IsTrue(tower == null || !tower.gameObject.activeInHierarchy, "Sold tower's GameObject should be destroyed or inactive.");
        }
    }
}
