using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using AlienDefense.Base;
using AlienDefense.Economy;
using AlienDefense.Enemies;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AlienDefense.Tests.PlayMode
{
    public class EnemyLifecyclePlayModeTests
    {
        private readonly List<GameObject> _spawnedObjects = new List<GameObject>();

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
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(field, $"Field '{fieldName}' not found on {target.GetType().Name}.");
            field.SetValue(target, value);
        }

        private EnemyPath3D CreatePath(params Vector3[] points)
        {
            var pathObject = new GameObject("TestEnemyPath");
            _spawnedObjects.Add(pathObject);
            LogAssert.Expect(LogType.Error, "[EnemyPath3D] 'TestEnemyPath' needs at least two waypoints.");
            var path = pathObject.AddComponent<EnemyPath3D>();

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

        private GameObject CreateEnemyTemplate()
        {
            var go = new GameObject("TestEnemyTemplate");
            go.SetActive(false);
            _spawnedObjects.Add(go);

            var health = go.AddComponent<EnemyHealth>();
            var movement = go.AddComponent<EnemyMovement>();
            var controller = go.AddComponent<EnemyController>();
            SetPrivateField(controller, "_health", health);
            SetPrivateField(controller, "_movement", movement);

            return go;
        }

        [UnityTest]
        public IEnumerator EnemyController_Defeated_AddsRewardExactlyOnce_AndDoesNotDamageBase()
        {
            var definition = ScriptableObject.CreateInstance<EnemyDefinition>();
            EnemyPath3D path = CreatePath(new Vector3(0f, 0f, 0f), new Vector3(0f, 0f, 50f));
            GameObject enemyObject = CreateEnemyTemplate();
            var controller = enemyObject.GetComponent<EnemyController>();
            enemyObject.SetActive(true);

            var registry = new EnemyRegistry();
            var economy = new EconomyService(0);
            var baseHealth = new BaseHealthService(20);
            bool released = false;

            controller.Initialize(definition, path, economy, baseHealth, registry, _ => released = true, null);
            yield return null;

            controller.ApplyDebugDamage(100000f);
            yield return null;

            Assert.AreEqual(definition.RewardResource, economy.CurrentResource);
            Assert.AreEqual(20, baseHealth.CurrentHealth);
            Assert.AreEqual(0, registry.Count);
            Assert.IsTrue(released);

            controller.ApplyDebugDamage(10f);
            yield return null;
            Assert.AreEqual(definition.RewardResource, economy.CurrentResource);
        }

        [UnityTest]
        public IEnumerator EnemyController_ReachedBase_DamagesBaseExactlyOnce_AndDoesNotAddReward()
        {
            var definition = ScriptableObject.CreateInstance<EnemyDefinition>();
            EnemyPath3D path = CreatePath(new Vector3(0f, 0f, 0f), new Vector3(0f, 0f, 1f));
            GameObject enemyObject = CreateEnemyTemplate();
            var controller = enemyObject.GetComponent<EnemyController>();
            enemyObject.SetActive(true);

            var registry = new EnemyRegistry();
            var economy = new EconomyService(0);
            var baseHealth = new BaseHealthService(20);

            controller.Initialize(definition, path, economy, baseHealth, registry, _ => { }, null);

            for (int i = 0; i < 60; i++)
            {
                yield return null;
            }

            Assert.AreEqual(20 - definition.BaseDamage, baseHealth.CurrentHealth);
            Assert.AreEqual(0, economy.CurrentResource);
            Assert.AreEqual(0, registry.Count);
        }

        [UnityTest]
        public IEnumerator EnemyController_ForceResolveRemoved_HasNoRewardOrDamage()
        {
            var definition = ScriptableObject.CreateInstance<EnemyDefinition>();
            EnemyPath3D path = CreatePath(new Vector3(0f, 0f, 0f), new Vector3(0f, 0f, 50f));
            GameObject enemyObject = CreateEnemyTemplate();
            var controller = enemyObject.GetComponent<EnemyController>();
            enemyObject.SetActive(true);

            var registry = new EnemyRegistry();
            var economy = new EconomyService(0);
            var baseHealth = new BaseHealthService(20);
            bool released = false;

            controller.Initialize(definition, path, economy, baseHealth, registry, _ => released = true, null);
            yield return null;

            controller.ForceResolve(EnemyResolveReason.Removed);

            Assert.AreEqual(0, economy.CurrentResource);
            Assert.AreEqual(20, baseHealth.CurrentHealth);
            Assert.AreEqual(0, registry.Count);
            Assert.IsTrue(released);
        }

        [UnityTest]
        public IEnumerator EnemyFactory_ReusesPooledInstance_AndResetsHealthOnRespawn()
        {
            var definition = ScriptableObject.CreateInstance<EnemyDefinition>();
            GameObject template = CreateEnemyTemplate();
            var templateController = template.GetComponent<EnemyController>();
            SetPrivateField(definition, "_prefab", templateController);
            SetPrivateField(definition, "_poolPrewarmCount", 0);
            SetPrivateField(definition, "_poolDefaultCapacity", 2);
            SetPrivateField(definition, "_poolMaximumSize", 2);

            EnemyPath3D path = CreatePath(new Vector3(0f, 0f, 0f), new Vector3(0f, 0f, 50f));

            var registry = new EnemyRegistry();
            var economy = new EconomyService(0);
            var baseHealth = new BaseHealthService(20);
            var runtimeParent = new GameObject("RuntimeParent");
            _spawnedObjects.Add(runtimeParent);

            var poolRegistry = new EnemyPoolRegistry(runtimeParent.transform);
            var factory = new EnemyFactory(poolRegistry, registry, economy, baseHealth, null);

            EnemyController first = factory.Spawn(definition, path, Vector3.zero, Quaternion.identity);
            yield return null;
            Assert.IsNotNull(first);
            Assert.AreEqual(1, registry.Count);

            first.ApplyDebugDamage(100000f);
            yield return null;

            Assert.AreEqual(0, registry.Count);
            Assert.AreEqual(definition.RewardResource, economy.CurrentResource);

            EnemyController second = factory.Spawn(definition, path, new Vector3(5f, 0f, 5f), Quaternion.identity);
            yield return null;

            Assert.AreSame(first, second);
            Assert.AreEqual(definition.MaxHealth, second.Health.CurrentHealth);
            Assert.AreEqual(1, registry.Count);
        }
    }
}
