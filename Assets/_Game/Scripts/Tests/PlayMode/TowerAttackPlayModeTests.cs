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
    public class TowerAttackPlayModeTests
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

        /// <summary>Waits real elapsed game seconds (accumulating Time.deltaTime) rather than a fixed frame count,
        /// so the test stays correct regardless of the Editor's actual frame rate.</summary>
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
            var controller = go.AddComponent<EnemyController>();
            SetPrivateField(controller, "_health", health);
            SetPrivateField(controller, "_movement", movement);

            var definition = CreateSilently(() => ScriptableObject.CreateInstance<EnemyDefinition>());
            _scriptableObjects.Add(definition);

            go.SetActive(true);
            controller.Initialize(definition, path, null, null, registry, _ => { }, null);
            controller.Movement.StopMovement();
            controller.transform.position = position;
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
            var poolRegistry = new ProjectilePoolRegistry(runtimeParent.transform);
            return new ProjectileFactory(poolRegistry);
        }

        private TowerDefinition CreateTowerDefinition(ProjectileDefinition projectileDefinition, float range, float damage, float attacksPerSecond)
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
            SetPrivateField(definition, "_projectileDefinition", projectileDefinition);
            SetPrivateField(definition, "_defaultTargetingMode", TargetingMode.Closest);
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

        [UnityTest]
        public IEnumerator Tower_DamagesEnemyInRange_WhenPlayingWave()
        {
            EnemyPath3D path = CreatePath(new Vector3(0f, 0f, 0f), new Vector3(0f, 0f, 50f));
            var registry = new EnemyRegistry();
            EnemyController enemy = CreateStationaryEnemy(path, registry, new Vector3(2f, 0f, 0f));

            ProjectileFactory factory = CreateProjectileFactory(out ProjectileDefinition projectileDefinition);
            TowerDefinition towerDefinition = CreateTowerDefinition(projectileDefinition, 5f, 10f, 4f);
            TowerController tower = CreateTower(Vector3.zero);

            var gameFlow = new GameFlowController();
            gameFlow.BeginPreparingWave();
            gameFlow.BeginPlayingWave();

            tower.Initialize(towerDefinition, registry, factory, null, gameFlow);

            float startHealth = enemy.Health.CurrentHealth;

            // First shot fires immediately and travels 2 units at 30 units/sec (~0.07s); wait a generous 2s of
            // game time regardless of the Editor's actual frame rate.
            yield return WaitForGameSeconds(2f);

            Assert.Less(enemy.Health.CurrentHealth, startHealth);
        }

        [UnityTest]
        public IEnumerator Tower_DoesNotAttack_WhenNotPlayingWave()
        {
            EnemyPath3D path = CreatePath(new Vector3(0f, 0f, 0f), new Vector3(0f, 0f, 50f));
            var registry = new EnemyRegistry();
            EnemyController enemy = CreateStationaryEnemy(path, registry, new Vector3(2f, 0f, 0f));

            ProjectileFactory factory = CreateProjectileFactory(out ProjectileDefinition projectileDefinition);
            TowerDefinition towerDefinition = CreateTowerDefinition(projectileDefinition, 5f, 10f, 4f);
            TowerController tower = CreateTower(Vector3.zero);

            var gameFlow = new GameFlowController();
            gameFlow.BeginPreparingWave();

            tower.Initialize(towerDefinition, registry, factory, null, gameFlow);

            float startHealth = enemy.Health.CurrentHealth;

            yield return WaitForGameSeconds(1f);

            Assert.AreEqual(startHealth, enemy.Health.CurrentHealth, 0.01f);
        }

        [UnityTest]
        public IEnumerator Tower_StopsAttacking_WhenGamePaused()
        {
            EnemyPath3D path = CreatePath(new Vector3(0f, 0f, 0f), new Vector3(0f, 0f, 50f));
            var registry = new EnemyRegistry();
            EnemyController enemy = CreateStationaryEnemy(path, registry, new Vector3(2f, 0f, 0f));

            ProjectileFactory factory = CreateProjectileFactory(out ProjectileDefinition projectileDefinition);
            TowerDefinition towerDefinition = CreateTowerDefinition(projectileDefinition, 5f, 1000f, 10f);
            TowerController tower = CreateTower(Vector3.zero);

            var gameFlow = new GameFlowController();
            gameFlow.BeginPreparingWave();
            gameFlow.BeginPlayingWave();

            tower.Initialize(towerDefinition, registry, factory, null, gameFlow);

            // First shot fires immediately and travels 2 units at 30 units/sec (~0.07s); wait a generous 0.5s of
            // game time so it has already landed (and this overkill hit already resolved the enemy at 0 health)
            // before the pause checkpoint, regardless of the Editor's actual frame rate.
            yield return WaitForGameSeconds(0.5f);

            gameFlow.Pause();
            float healthAfterPause = enemy.Health.CurrentHealth;

            yield return WaitForGameSeconds(1f);

            Assert.AreEqual(healthAfterPause, enemy.Health.CurrentHealth, 0.01f);
        }
    }
}
