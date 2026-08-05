using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using AlienDefense.Combat;
using AlienDefense.Data;
using AlienDefense.Enemies;
using AlienDefense.Player;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AlienDefense.Tests.PlayMode
{
    public class PlayerAutoAttackPlayModeTests
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

        private GameObject CreateProjectileTemplate()
        {
            var go = new GameObject("TestProjectileTemplate");
            go.SetActive(false);
            _spawnedObjects.Add(go);
            go.AddComponent<ProjectileController>();
            return go;
        }

        private ProjectileDefinition CreateProjectileDefinition(ProjectileController template)
        {
            var definition = CreateSilently(() => ScriptableObject.CreateInstance<ProjectileDefinition>());
            _scriptableObjects.Add(definition);

            SetPrivateField(definition, "_prefab", template);
            SetPrivateField(definition, "_speed", 20f);
            SetPrivateField(definition, "_maximumLifetime", 5f);
            SetPrivateField(definition, "_hitDistance", 0.3f);
            SetPrivateField(definition, "_poolPrewarmCount", 0);
            SetPrivateField(definition, "_poolDefaultCapacity", 10);
            SetPrivateField(definition, "_poolMaximumSize", 10);
            return definition;
        }

        private ProjectileFactory CreateFactory()
        {
            var runtimeParent = new GameObject("ProjectileRuntimeParent");
            _spawnedObjects.Add(runtimeParent);
            var poolRegistry = new ProjectilePoolRegistry(runtimeParent.transform);
            return new ProjectileFactory(poolRegistry);
        }

        private PlayerDefinition CreatePlayerDefinition(ProjectileDefinition projectileDefinition, float range, int damage, float attacksPerSecond)
        {
            var definition = CreateSilently(() => ScriptableObject.CreateInstance<PlayerDefinition>());
            _scriptableObjects.Add(definition);

            SetPrivateField(definition, "_attackRange", range);
            SetPrivateField(definition, "_attackDamage", damage);
            SetPrivateField(definition, "_attacksPerSecond", attacksPerSecond);
            SetPrivateField(definition, "_projectileDefinition", projectileDefinition);
            return definition;
        }

        private PlayerAutoAttack CreateAutoAttack(Vector3 position)
        {
            var go = new GameObject("TestPlayerAutoAttack", typeof(PlayerAutoAttack));
            _spawnedObjects.Add(go);
            go.transform.position = position;

            var firePointObject = new GameObject("FirePoint");
            firePointObject.transform.SetParent(go.transform, false);
            _spawnedObjects.Add(firePointObject);

            var autoAttack = go.GetComponent<PlayerAutoAttack>();
            SetPrivateField(autoAttack, "_firePoint", firePointObject.transform);
            return autoAttack;
        }

        [UnityTest]
        public IEnumerator PlayerAutoAttack_TargetsClosestEnemyInRange_AndFires()
        {
            EnemyPath3D path = CreatePath(new Vector3(0f, 0f, 0f), new Vector3(0f, 0f, 50f));
            var registry = new EnemyRegistry();
            EnemyController near = CreateStationaryEnemy(path, registry, new Vector3(2f, 0f, 0f));
            EnemyController far = CreateStationaryEnemy(path, registry, new Vector3(4.5f, 0f, 0f));
            CreateStationaryEnemy(path, registry, new Vector3(20f, 0f, 0f));

            GameObject template = CreateProjectileTemplate();
            ProjectileDefinition projectileDefinition = CreateProjectileDefinition(template.GetComponent<ProjectileController>());
            ProjectileFactory factory = CreateFactory();

            PlayerDefinition playerDefinition = CreatePlayerDefinition(projectileDefinition, 5f, 10, 2f);
            PlayerAutoAttack autoAttack = CreateAutoAttack(Vector3.zero);
            autoAttack.Initialize(playerDefinition, registry, factory);

            float nearHealthStart = near.Health.CurrentHealth;
            float farHealthStart = far.Health.CurrentHealth;

            for (int i = 0; i < 90; i++)
            {
                yield return null;
            }

            Assert.Less(near.Health.CurrentHealth, nearHealthStart);
            Assert.AreEqual(farHealthStart, far.Health.CurrentHealth, 0.01f);
        }

        [UnityTest]
        public IEnumerator PlayerAutoAttack_DoesNotFire_WhenAttackDisabled()
        {
            EnemyPath3D path = CreatePath(new Vector3(0f, 0f, 0f), new Vector3(0f, 0f, 50f));
            var registry = new EnemyRegistry();
            EnemyController enemy = CreateStationaryEnemy(path, registry, new Vector3(2f, 0f, 0f));

            GameObject template = CreateProjectileTemplate();
            ProjectileDefinition projectileDefinition = CreateProjectileDefinition(template.GetComponent<ProjectileController>());
            ProjectileFactory factory = CreateFactory();

            PlayerDefinition playerDefinition = CreatePlayerDefinition(projectileDefinition, 5f, 10, 2f);
            PlayerAutoAttack autoAttack = CreateAutoAttack(Vector3.zero);
            autoAttack.Initialize(playerDefinition, registry, factory);
            autoAttack.SetAttackEnabled(false);

            float startHealth = enemy.Health.CurrentHealth;

            for (int i = 0; i < 60; i++)
            {
                yield return null;
            }

            Assert.AreEqual(startHealth, enemy.Health.CurrentHealth, 0.01f);
        }

        [UnityTest]
        public IEnumerator PlayerAutoAttack_FiresImmediately_OnFirstValidTarget()
        {
            EnemyPath3D path = CreatePath(new Vector3(0f, 0f, 0f), new Vector3(0f, 0f, 50f));
            var registry = new EnemyRegistry();
            EnemyController enemy = CreateStationaryEnemy(path, registry, new Vector3(2f, 0f, 0f));

            GameObject template = CreateProjectileTemplate();
            ProjectileDefinition projectileDefinition = CreateProjectileDefinition(template.GetComponent<ProjectileController>());
            ProjectileFactory factory = CreateFactory();

            PlayerDefinition playerDefinition = CreatePlayerDefinition(projectileDefinition, 5f, 10, 0.5f);
            PlayerAutoAttack autoAttack = CreateAutoAttack(Vector3.zero);
            autoAttack.Initialize(playerDefinition, registry, factory);

            float startHealth = enemy.Health.CurrentHealth;

            for (int i = 0; i < 60; i++)
            {
                yield return null;
            }

            Assert.Less(enemy.Health.CurrentHealth, startHealth);
        }
    }
}
