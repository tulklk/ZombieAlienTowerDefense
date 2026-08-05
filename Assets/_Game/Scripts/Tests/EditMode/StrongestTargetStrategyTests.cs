using System.Collections.Generic;
using AlienDefense.Enemies;
using AlienDefense.Towers;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace AlienDefense.Tests.EditMode
{
    public class StrongestTargetStrategyTests
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

        private EnemyController CreateEnemy(EnemyRegistry registry, Vector3 position, float damageTaken)
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

            if (damageTaken > 0f)
            {
                health.TryApplyDamage(damageTaken);
            }

            return controller;
        }

        [Test]
        public void SelectTarget_PicksHighestCurrentHealth()
        {
            var registry = new EnemyRegistry();
            EnemyController damaged = CreateEnemy(registry, new Vector3(1f, 0f, 0f), 50f);
            EnemyController healthy = CreateEnemy(registry, new Vector3(-1f, 0f, 0f), 0f);

            var strategy = new StrongestTargetStrategy();
            EnemyController result = strategy.SelectTarget(registry, Vector3.zero, 5f);

            Assert.AreEqual(healthy, result);
            Assert.AreNotEqual(damaged, result);
        }

        [Test]
        public void SelectTarget_IgnoresEnemiesOutOfRange()
        {
            var registry = new EnemyRegistry();
            CreateEnemy(registry, new Vector3(50f, 0f, 0f), 0f);

            var strategy = new StrongestTargetStrategy();
            EnemyController result = strategy.SelectTarget(registry, Vector3.zero, 5f);

            Assert.IsNull(result);
        }

        [Test]
        public void SelectTarget_EmptyRegistry_ReturnsNull()
        {
            var registry = new EnemyRegistry();
            var strategy = new StrongestTargetStrategy();

            EnemyController result = strategy.SelectTarget(registry, Vector3.zero, 5f);

            Assert.IsNull(result);
        }
    }
}
