using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using AlienDefense.Combat;
using AlienDefense.Enemies;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AlienDefense.Tests.PlayMode
{
    public class ProjectileLifecyclePlayModeTests
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

        private ProjectileDefinition CreateProjectileDefinition(ProjectileController template, float speed, float maximumLifetime, float hitDistance)
        {
            var definition = CreateSilently(() => ScriptableObject.CreateInstance<ProjectileDefinition>());
            _scriptableObjects.Add(definition);

            SetPrivateField(definition, "_prefab", template);
            SetPrivateField(definition, "_speed", speed);
            SetPrivateField(definition, "_maximumLifetime", maximumLifetime);
            SetPrivateField(definition, "_hitDistance", hitDistance);
            SetPrivateField(definition, "_poolPrewarmCount", 0);
            SetPrivateField(definition, "_poolDefaultCapacity", 2);
            SetPrivateField(definition, "_poolMaximumSize", 2);
            return definition;
        }

        private ProjectileFactory CreateFactory()
        {
            var runtimeParent = new GameObject("ProjectileRuntimeParent");
            _spawnedObjects.Add(runtimeParent);
            var poolRegistry = new ProjectilePoolRegistry(runtimeParent.transform);
            return new ProjectileFactory(poolRegistry);
        }

        [UnityTest]
        public IEnumerator Projectile_HitsTarget_DamagesExactlyOnce()
        {
            EnemyPath3D path = CreatePath(new Vector3(0f, 0f, 0f), new Vector3(0f, 0f, 50f));
            var registry = new EnemyRegistry();
            EnemyController target = CreateStationaryEnemy(path, registry, new Vector3(0f, 0f, 5f));

            GameObject template = CreateProjectileTemplate();
            ProjectileDefinition definition = CreateProjectileDefinition(template.GetComponent<ProjectileController>(), 10f, 5f, 0.3f);
            ProjectileFactory factory = CreateFactory();

            var handle = new CombatTargetHandle(target);
            var damage = new DamageInfo(25f, null, target.AimPoint.position);
            var request = new ProjectileSpawnRequest(Vector3.zero, Quaternion.identity, handle, damage);

            ProjectileController projectile = factory.Spawn(definition, request);
            Assert.IsNotNull(projectile);

            float startHealth = target.Health.CurrentHealth;

            for (int i = 0; i < 60; i++)
            {
                yield return null;
            }

            Assert.AreEqual(startHealth - 25f, target.Health.CurrentHealth, 0.01f);
            Assert.IsFalse(projectile.gameObject.activeSelf);

            float healthAfterHit = target.Health.CurrentHealth;
            for (int i = 0; i < 10; i++)
            {
                yield return null;
            }

            Assert.AreEqual(healthAfterHit, target.Health.CurrentHealth, 0.01f);
        }

        [UnityTest]
        public IEnumerator Projectile_Despawns_WhenTargetBecomesInvalidBeforeHit()
        {
            EnemyPath3D path = CreatePath(new Vector3(0f, 0f, 0f), new Vector3(0f, 0f, 50f));
            var registry = new EnemyRegistry();
            EnemyController target = CreateStationaryEnemy(path, registry, new Vector3(0f, 0f, 100f));

            GameObject template = CreateProjectileTemplate();
            ProjectileDefinition definition = CreateProjectileDefinition(template.GetComponent<ProjectileController>(), 5f, 5f, 0.3f);
            ProjectileFactory factory = CreateFactory();

            var handle = new CombatTargetHandle(target);
            var damage = new DamageInfo(10f, null, target.AimPoint.position);
            var request = new ProjectileSpawnRequest(Vector3.zero, Quaternion.identity, handle, damage);

            ProjectileController projectile = factory.Spawn(definition, request);
            Assert.IsNotNull(projectile);
            Assert.IsTrue(projectile.gameObject.activeSelf);

            yield return null;

            target.ForceResolve(EnemyResolveReason.Removed);

            for (int i = 0; i < 5; i++)
            {
                yield return null;
            }

            Assert.IsFalse(projectile.gameObject.activeSelf);
        }

        [UnityTest]
        public IEnumerator Projectile_Despawns_AfterMaximumLifetime()
        {
            EnemyPath3D path = CreatePath(new Vector3(0f, 0f, 0f), new Vector3(0f, 0f, 50f));
            var registry = new EnemyRegistry();
            EnemyController target = CreateStationaryEnemy(path, registry, new Vector3(0f, 0f, 1000f));

            GameObject template = CreateProjectileTemplate();
            ProjectileDefinition definition = CreateProjectileDefinition(template.GetComponent<ProjectileController>(), 5f, 0.1f, 0.3f);
            ProjectileFactory factory = CreateFactory();

            var handle = new CombatTargetHandle(target);
            var damage = new DamageInfo(10f, null, target.AimPoint.position);
            var request = new ProjectileSpawnRequest(Vector3.zero, Quaternion.identity, handle, damage);

            ProjectileController projectile = factory.Spawn(definition, request);
            Assert.IsNotNull(projectile);

            for (int i = 0; i < 30; i++)
            {
                yield return null;
            }

            Assert.IsFalse(projectile.gameObject.activeSelf);
        }

        [UnityTest]
        public IEnumerator Projectile_DoesNotDamageReusedTarget_AfterOriginalResolvedAndRespawned()
        {
            EnemyPath3D path = CreatePath(new Vector3(0f, 0f, 0f), new Vector3(0f, 0f, 50f));
            var registry = new EnemyRegistry();
            EnemyController target = CreateStationaryEnemy(path, registry, new Vector3(0f, 0f, 10f));

            GameObject template = CreateProjectileTemplate();
            ProjectileDefinition definition = CreateProjectileDefinition(template.GetComponent<ProjectileController>(), 3f, 5f, 0.3f);
            ProjectileFactory factory = CreateFactory();

            var handle = new CombatTargetHandle(target);
            var damage = new DamageInfo(999f, null, target.AimPoint.position);
            var request = new ProjectileSpawnRequest(Vector3.zero, Quaternion.identity, handle, damage);

            ProjectileController projectile = factory.Spawn(definition, request);
            Assert.IsNotNull(projectile);

            yield return null;

            target.ForceResolve(EnemyResolveReason.Removed);

            var newDefinition = CreateSilently(() => ScriptableObject.CreateInstance<EnemyDefinition>());
            _scriptableObjects.Add(newDefinition);
            target.Initialize(newDefinition, path, null, null, registry, _ => { }, null);
            target.Movement.StopMovement();
            target.transform.position = new Vector3(0f, 0f, 10f);

            float reusedHealth = target.Health.CurrentHealth;

            for (int i = 0; i < 60; i++)
            {
                yield return null;
            }

            Assert.AreEqual(reusedHealth, target.Health.CurrentHealth, 0.01f);
            Assert.IsFalse(projectile.gameObject.activeSelf);
        }
    }
}
