using System.Collections.Generic;
using System.Reflection;
using AlienDefense.Enemies;
using AlienDefense.Towers;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace AlienDefense.Tests.EditMode
{
    public class FirstTargetStrategyTests
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

        private static T CreateSilently<T>(System.Func<T> factory)
        {
            bool previousLogEnabled = Debug.unityLogger.logEnabled;
            Debug.unityLogger.logEnabled = false;
            T result = factory();
            Debug.unityLogger.logEnabled = previousLogEnabled;
            return result;
        }

        private EnemyController CreateEnemy(EnemyRegistry registry, Vector3 position, float pathProgress)
        {
            var go = new GameObject("TestEnemy");
            _spawnedObjects.Add(go);
            var health = go.AddComponent<EnemyHealth>();
            var movement = go.AddComponent<EnemyMovement>();
            var controller = go.AddComponent<EnemyController>();

            var controllerSerialized = new SerializedObject(controller);
            controllerSerialized.FindProperty("_health").objectReferenceValue = health;
            controllerSerialized.FindProperty("_movement").objectReferenceValue = movement;
            controllerSerialized.ApplyModifiedPropertiesWithoutUndo();

            var pathObject = new GameObject("TestPath");
            _spawnedObjects.Add(pathObject);
            var waypointA = new GameObject("A");
            waypointA.transform.SetParent(pathObject.transform);
            var waypointB = new GameObject("B");
            waypointB.transform.position = new Vector3(0f, 0f, 10f);
            waypointB.transform.SetParent(pathObject.transform);
            var path = CreateSilently(() => pathObject.AddComponent<EnemyPath3D>());
            var pathSerialized = new SerializedObject(path);
            SerializedProperty waypointsProperty = pathSerialized.FindProperty("_waypoints");
            waypointsProperty.arraySize = 2;
            waypointsProperty.GetArrayElementAtIndex(0).objectReferenceValue = waypointA.transform;
            waypointsProperty.GetArrayElementAtIndex(1).objectReferenceValue = waypointB.transform;
            pathSerialized.ApplyModifiedPropertiesWithoutUndo();

            var definition = CreateSilently(() => ScriptableObject.CreateInstance<EnemyDefinition>());
            _scriptableObjects.Add(definition);

            controller.Initialize(definition, path, null, null, registry, null, null);
            controller.Movement.StopMovement();
            controller.transform.position = position;
            SetPathProgress(movement, pathProgress);

            return controller;
        }

        private static void SetPathProgress(EnemyMovement movement, float value)
        {
            FieldInfo field = typeof(EnemyMovement).GetField("<PathProgress>k__BackingField", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(field, "Could not find PathProgress backing field via reflection.");
            field.SetValue(movement, value);
        }

        [Test]
        public void SelectTarget_PicksHighestPathProgress_AmongInRangeTargets()
        {
            var registry = new EnemyRegistry();
            EnemyController low = CreateEnemy(registry, new Vector3(1f, 0f, 0f), 0.2f);
            EnemyController high = CreateEnemy(registry, new Vector3(-1f, 0f, 0f), 0.8f);

            var strategy = new FirstTargetStrategy();
            EnemyController result = strategy.SelectTarget(registry, Vector3.zero, 5f);

            Assert.AreEqual(high, result);
            Assert.AreNotEqual(low, result);
        }

        [Test]
        public void SelectTarget_IgnoresEnemiesOutOfRange()
        {
            var registry = new EnemyRegistry();
            CreateEnemy(registry, new Vector3(50f, 0f, 0f), 0.9f);
            EnemyController inRange = CreateEnemy(registry, new Vector3(1f, 0f, 0f), 0.1f);

            var strategy = new FirstTargetStrategy();
            EnemyController result = strategy.SelectTarget(registry, Vector3.zero, 5f);

            Assert.AreEqual(inRange, result);
        }

        [Test]
        public void SelectTarget_IgnoresResolvedEnemies()
        {
            var registry = new EnemyRegistry();
            EnemyController resolved = CreateEnemy(registry, new Vector3(1f, 0f, 0f), 0.9f);
            resolved.ForceResolve(EnemyResolveReason.Removed);
            EnemyController alive = CreateEnemy(registry, new Vector3(1.5f, 0f, 0f), 0.1f);

            var strategy = new FirstTargetStrategy();
            EnemyController result = strategy.SelectTarget(registry, Vector3.zero, 5f);

            Assert.AreEqual(alive, result);
        }

        [Test]
        public void SelectTarget_EmptyRegistry_ReturnsNull()
        {
            var registry = new EnemyRegistry();
            var strategy = new FirstTargetStrategy();

            EnemyController result = strategy.SelectTarget(registry, Vector3.zero, 5f);

            Assert.IsNull(result);
        }
    }
}
