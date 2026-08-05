using System.Collections.Generic;
using System.Reflection;
using AlienDefense.Enemies;
using AlienDefense.Towers;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AlienDefense.Tests.PlayMode
{
    public class TowerTargetingPlayModeTests
    {
        private readonly List<GameObject> _spawnedObjects = new List<GameObject>();
        private readonly List<Object> _scriptableObjects = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            // DestroyImmediate (not Destroy) so leftover objects from this synchronous [Test] don't
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

        private TowerTargeting CreateTargeting(Vector3 position)
        {
            var go = new GameObject("TestTargeting");
            _spawnedObjects.Add(go);
            go.transform.position = position;
            return go.AddComponent<TowerTargeting>();
        }

        [Test]
        public void Tick_AcquiresClosestEnemyInRange_AfterRefreshInterval()
        {
            EnemyPath3D path = CreatePath(new Vector3(0f, 0f, 0f), new Vector3(0f, 0f, 50f));
            var registry = new EnemyRegistry();
            EnemyController near = CreateStationaryEnemy(path, registry, new Vector3(1f, 0f, 0f));
            CreateStationaryEnemy(path, registry, new Vector3(3f, 0f, 0f));

            TowerTargeting targeting = CreateTargeting(Vector3.zero);
            targeting.Initialize(registry, new ClosestTargetStrategy(), 5f);

            targeting.Tick(0.25f);

            Assert.AreEqual(near, targeting.CurrentTarget);
        }

        [Test]
        public void Tick_IgnoresEnemiesOutOfRange()
        {
            EnemyPath3D path = CreatePath(new Vector3(0f, 0f, 0f), new Vector3(0f, 0f, 50f));
            var registry = new EnemyRegistry();
            CreateStationaryEnemy(path, registry, new Vector3(50f, 0f, 0f));

            TowerTargeting targeting = CreateTargeting(Vector3.zero);
            targeting.Initialize(registry, new ClosestTargetStrategy(), 5f);

            targeting.Tick(0.25f);

            Assert.IsNull(targeting.CurrentTarget);
        }

        [Test]
        public void Tick_ClearsCurrentTarget_WhenTargetResolves()
        {
            EnemyPath3D path = CreatePath(new Vector3(0f, 0f, 0f), new Vector3(0f, 0f, 50f));
            var registry = new EnemyRegistry();
            EnemyController enemy = CreateStationaryEnemy(path, registry, new Vector3(1f, 0f, 0f));

            TowerTargeting targeting = CreateTargeting(Vector3.zero);
            targeting.Initialize(registry, new ClosestTargetStrategy(), 5f);
            targeting.Tick(0.25f);
            Assert.AreEqual(enemy, targeting.CurrentTarget);

            enemy.ForceResolve(EnemyResolveReason.Removed);
            targeting.Tick(0.01f);

            Assert.IsNull(targeting.CurrentTarget);
        }

        [Test]
        public void Tick_ClearsCurrentTarget_WhenTargetLeavesRange()
        {
            EnemyPath3D path = CreatePath(new Vector3(0f, 0f, 0f), new Vector3(0f, 0f, 50f));
            var registry = new EnemyRegistry();
            EnemyController enemy = CreateStationaryEnemy(path, registry, new Vector3(1f, 0f, 0f));

            TowerTargeting targeting = CreateTargeting(Vector3.zero);
            targeting.Initialize(registry, new ClosestTargetStrategy(), 5f);
            targeting.Tick(0.25f);
            Assert.AreEqual(enemy, targeting.CurrentTarget);

            enemy.transform.position = new Vector3(50f, 0f, 0f);
            targeting.Tick(0.01f);

            Assert.IsNull(targeting.CurrentTarget);
        }
    }
}
