using System.Collections.Generic;
using System.Reflection;
using AlienDefense.Combat;
using AlienDefense.Enemies;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace AlienDefense.Tests.EditMode
{
    public class SlowStackingTests
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

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(field, $"Field '{fieldName}' not found on {target.GetType().Name}.");
            field.SetValue(target, value);
        }

        private static object GetPrivateField(object target, string fieldName)
        {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(field, $"Field '{fieldName}' not found on {target.GetType().Name}.");
            return field.GetValue(target);
        }

        private static T CreateSilently<T>(System.Func<T> factory)
        {
            bool previousLogEnabled = Debug.unityLogger.logEnabled;
            Debug.unityLogger.logEnabled = false;
            T result = factory();
            Debug.unityLogger.logEnabled = previousLogEnabled;
            return result;
        }

        private StatusEffectDefinition CreateSlow(float magnitude)
        {
            var definition = CreateSilently(() => ScriptableObject.CreateInstance<StatusEffectDefinition>());
            _scriptableObjects.Add(definition);
            SetPrivateField(definition, "_type", StatusEffectType.Slow);
            SetPrivateField(definition, "_duration", 3f);
            SetPrivateField(definition, "_magnitude", magnitude);
            SetPrivateField(definition, "_tickInterval", 1f);
            SetPrivateField(definition, "_stackingRule", StatusStackingRule.RefreshDurationOnly);
            SetPrivateField(definition, "_maxStacks", 1);
            return definition;
        }

        private (EnemyStatusController statusController, EnemyMovement movement) CreateStatusController()
        {
            var go = new GameObject("TestEnemy");
            _spawnedObjects.Add(go);
            var health = go.AddComponent<EnemyHealth>();
            health.Initialize(100f);
            var movement = go.AddComponent<EnemyMovement>();

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
            movement.Initialize(path, moveSpeed: 2f, rotationSpeed: 0f, arrivalThreshold: 0.1f);

            var statusController = go.AddComponent<EnemyStatusController>();
            SetPrivateField(statusController, "_health", health);
            SetPrivateField(statusController, "_movement", movement);

            return (statusController, movement);
        }

        [Test]
        public void ApplyStatus_Slow_ReducesMovementSpeedMultiplier()
        {
            (EnemyStatusController statusController, EnemyMovement movement) = CreateStatusController();
            StatusEffectDefinition slow = CreateSlow(0.5f);

            statusController.ApplyStatus(slow);

            Assert.AreEqual(0.5f, (float)GetPrivateField(movement, "_statusSpeedMultiplier"), 0.001f);
        }

        [Test]
        public void ApplyStatus_TwoDifferentSlows_StrongestMagnitudeWins()
        {
            (EnemyStatusController statusController, EnemyMovement movement) = CreateStatusController();
            StatusEffectDefinition weakSlow = CreateSlow(0.7f);
            StatusEffectDefinition strongSlow = CreateSlow(0.3f);

            statusController.ApplyStatus(weakSlow);
            statusController.ApplyStatus(strongSlow);

            Assert.AreEqual(0.3f, (float)GetPrivateField(movement, "_statusSpeedMultiplier"), 0.001f);
        }

        [Test]
        public void ApplyStatus_Slow_ClampsAtMinimumMultiplier()
        {
            (EnemyStatusController statusController, EnemyMovement movement) = CreateStatusController();
            StatusEffectDefinition extremeSlow = CreateSlow(0.01f);

            statusController.ApplyStatus(extremeSlow);

            Assert.AreEqual(0.2f, (float)GetPrivateField(movement, "_statusSpeedMultiplier"), 0.001f);
        }

        [Test]
        public void ReapplyingSameSlow_RefreshesDuration_DoesNotStack()
        {
            (EnemyStatusController statusController, _) = CreateStatusController();
            StatusEffectDefinition slow = CreateSlow(0.5f);

            statusController.ApplyStatus(slow);
            statusController.ApplyStatus(slow);

            var active = (System.Collections.IDictionary)GetPrivateField(statusController, "_activeEffects");
            Assert.AreEqual(1, active.Count);
        }

        [Test]
        public void Expiry_RemovesEffect_AndRestoresFullSpeed()
        {
            (EnemyStatusController statusController, EnemyMovement movement) = CreateStatusController();
            StatusEffectDefinition slow = CreateSlow(0.5f);
            statusController.ApplyStatus(slow);

            statusController.Tick(9999f);

            Assert.AreEqual(1f, (float)GetPrivateField(movement, "_statusSpeedMultiplier"), 0.001f);
        }
    }
}
