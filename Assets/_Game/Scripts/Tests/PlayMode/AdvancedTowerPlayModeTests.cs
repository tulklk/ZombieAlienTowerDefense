using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using AlienDefense.Combat;
using AlienDefense.Core;
using AlienDefense.Enemies;
using AlienDefense.Towers;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AlienDefense.Tests.PlayMode
{
    public class AdvancedTowerPlayModeTests
    {
        private readonly List<GameObject> _spawnedObjects = new List<GameObject>();
        private readonly List<Object> _scriptableObjects = new List<Object>();
        private ProjectileDefinition _projectileDefinition;

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

        private static IEnumerator WaitForGameSeconds(float seconds)
        {
            float elapsed = 0f;
            while (elapsed < seconds)
            {
                yield return null;
                elapsed += Time.deltaTime;
            }
        }

        private EnemyPath3D CreatePath(params Vector3[] points)
        {
            var pathObject = new GameObject("TestEnemyPath");
            _spawnedObjects.Add(pathObject);
            var path = CreateSilently(() => pathObject.AddComponent<EnemyPath3D>());

            var waypointTransforms = new Transform[points.Length];
            for (int i = 0; i < points.Length; i++)
            {
                var waypoint = new GameObject($"Waypoint_{i}");
                waypoint.transform.position = points[i];
                _spawnedObjects.Add(waypoint);
                waypointTransforms[i] = waypoint.transform;
            }

            SetPrivateField(path, "_waypoints", waypointTransforms);
            return path;
        }

        private EnemyController CreateStationaryEnemy(EnemyPath3D path, EnemyRegistry registry, Vector3 position)
        {
            var go = new GameObject("TestEnemy");
            go.SetActive(false);
            _spawnedObjects.Add(go);
            var health = go.AddComponent<EnemyHealth>();
            var movement = go.AddComponent<EnemyMovement>();
            var statusController = go.AddComponent<EnemyStatusController>();
            SetPrivateField(statusController, "_health", health);
            SetPrivateField(statusController, "_movement", movement);
            var controller = go.AddComponent<EnemyController>();
            SetPrivateField(controller, "_health", health);
            SetPrivateField(controller, "_movement", movement);
            SetPrivateField(controller, "_statusController", statusController);

            var definition = CreateSilently(() => ScriptableObject.CreateInstance<EnemyDefinition>());
            _scriptableObjects.Add(definition);

            go.SetActive(true);
            controller.Initialize(definition, path, null, null, registry, _ => { }, null);
            controller.Movement.StopMovement();
            controller.transform.position = position;
            return controller;
        }

        private ProjectileFactory CreateProjectileFactory()
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
            _projectileDefinition = definition;

            var runtimeParent = new GameObject("ProjectileRuntimeParent");
            _spawnedObjects.Add(runtimeParent);
            var poolRegistry = new ProjectilePoolRegistry(runtimeParent.transform);
            return new ProjectileFactory(poolRegistry);
        }

        private TowerDefinition CreateTowerDefinition(float range, float damage, float attacksPerSecond, TowerAttackBehavior behavior, StatusEffectDefinition statusEffect, float splashRadius)
        {
            var definition = CreateSilently(() => ScriptableObject.CreateInstance<TowerDefinition>());
            _scriptableObjects.Add(definition);

            var level = new TowerLevelData();
            SetPrivateField(level, "_upgradeCost", 0);
            SetPrivateField(level, "_damage", damage);
            SetPrivateField(level, "_range", range);
            SetPrivateField(level, "_attacksPerSecond", attacksPerSecond);
            SetPrivateField(level, "_turretRotationSpeed", 720f);

            SetPrivateField(definition, "_levels", new[] { level });
            SetPrivateField(definition, "_projectileDefinition", _projectileDefinition);
            SetPrivateField(definition, "_defaultTargetingMode", TargetingMode.Closest);
            SetPrivateField(definition, "_attackBehavior", behavior);
            SetPrivateField(definition, "_statusEffectOnHit", statusEffect);
            SetPrivateField(definition, "_splashRadius", splashRadius);
            return definition;
        }

        private StatusEffectDefinition CreateSlow(float magnitude)
        {
            var definition = ScriptableObject.CreateInstance<StatusEffectDefinition>();
            _scriptableObjects.Add(definition);
            SetPrivateField(definition, "_type", StatusEffectType.Slow);
            SetPrivateField(definition, "_duration", 3f);
            SetPrivateField(definition, "_magnitude", magnitude);
            SetPrivateField(definition, "_tickInterval", 1f);
            SetPrivateField(definition, "_stackingRule", StatusStackingRule.RefreshDurationOnly);
            SetPrivateField(definition, "_maxStacks", 1);
            return definition;
        }

        private TowerController CreateTower(Vector3 position)
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
            return controller;
        }

        private static GameFlowController CreatePlayingWaveGameFlow()
        {
            var gameFlow = new GameFlowController();
            gameFlow.BeginPreparingWave();
            gameFlow.BeginPlayingWave();
            return gameFlow;
        }

        [UnityTest]
        public IEnumerator FrostTower_HitsEnemy_AppliesSlowStatus()
        {
            EnemyPath3D path = CreatePath(new Vector3(0f, 0f, 0f), new Vector3(0f, 0f, 50f));
            var registry = new EnemyRegistry();
            EnemyController enemy = CreateStationaryEnemy(path, registry, new Vector3(2f, 0f, 0f));

            ProjectileFactory projectileFactory = CreateProjectileFactory();
            StatusEffectDefinition slow = CreateSlow(0.5f);
            TowerDefinition towerDefinition = CreateTowerDefinition(5f, 6f, 4f, TowerAttackBehavior.Status, slow, 0f);
            TowerController tower = CreateTower(Vector3.zero);

            tower.Initialize(towerDefinition, registry, projectileFactory, null, CreatePlayingWaveGameFlow());

            yield return WaitForGameSeconds(1f);

            EnemyStatusController statusController = enemy.GetComponent<EnemyStatusController>();
            Assert.IsTrue(statusController.HasEffect(StatusEffectType.Slow));

            var movement = enemy.GetComponent<EnemyMovement>();
            FieldInfo multiplierField = typeof(EnemyMovement).GetField("_statusSpeedMultiplier", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.AreEqual(0.5f, (float)multiplierField.GetValue(movement), 0.001f);
        }

        [UnityTest]
        public IEnumerator MortarTower_HitsMultipleEnemies_WithinSplashRadius()
        {
            EnemyPath3D path = CreatePath(new Vector3(0f, 0f, 0f), new Vector3(0f, 0f, 50f));
            var registry = new EnemyRegistry();
            EnemyController primary = CreateStationaryEnemy(path, registry, new Vector3(3f, 0f, 0f));
            EnemyController nearby = CreateStationaryEnemy(path, registry, new Vector3(3.5f, 0f, 0f));
            EnemyController farAway = CreateStationaryEnemy(path, registry, new Vector3(20f, 0f, 0f));

            ProjectileFactory projectileFactory = CreateProjectileFactory();
            TowerDefinition towerDefinition = CreateTowerDefinition(10f, 15f, 2f, TowerAttackBehavior.Splash, null, 2.5f);
            TowerController tower = CreateTower(Vector3.zero);

            var areaDamageResolver = new AreaDamageResolver(new EnemyRegistrySplashProvider(registry));
            tower.Initialize(towerDefinition, registry, projectileFactory, areaDamageResolver, CreatePlayingWaveGameFlow());

            float startHealthPrimary = primary.Health.CurrentHealth;
            float startHealthNearby = nearby.Health.CurrentHealth;
            float startHealthFar = farAway.Health.CurrentHealth;

            yield return WaitForGameSeconds(1f);

            Assert.Less(primary.Health.CurrentHealth, startHealthPrimary);
            Assert.Less(nearby.Health.CurrentHealth, startHealthNearby);
            Assert.AreEqual(startHealthFar, farAway.Health.CurrentHealth, 0.01f, "Enemy outside the splash radius must take no damage.");
        }
    }
}
