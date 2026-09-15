using System.Collections.Generic;
using System.Reflection;
using AlienDefense.Combat;
using AlienDefense.Enemies;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace AlienDefense.Tests.EditMode
{
    /// <summary>Frost slow hits building up (per enemy, after a random number of hits) to an Ice Stun that fully stops
    /// movement for its duration, then gives back exactly the speed the remaining effects allow.</summary>
    public class IceStunProcTests
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

        private StatusEffectDefinition CreateEffect(StatusEffectType type, float duration, float magnitude)
        {
            var definition = CreateSilently(() => ScriptableObject.CreateInstance<StatusEffectDefinition>());
            _scriptableObjects.Add(definition);
            SetPrivateField(definition, "_type", type);
            SetPrivateField(definition, "_duration", duration);
            SetPrivateField(definition, "_magnitude", magnitude);
            SetPrivateField(definition, "_tickInterval", 1f);
            SetPrivateField(definition, "_stackingRule", StatusStackingRule.RefreshDurationOnly);
            SetPrivateField(definition, "_maxStacks", 1);
            return definition;
        }

        private StatusEffectDefinition CreateFrostSlow(float slowDuration, StatusEffectDefinition stun, int minHits, int maxHits, float cooldown = 0.3f)
        {
            StatusEffectDefinition slow = CreateEffect(StatusEffectType.Slow, slowDuration, 0.5f);
            SetPrivateField(slow, "_procEffect", stun);
            SetPrivateField(slow, "_procMinHits", minHits);
            SetPrivateField(slow, "_procMaxHits", maxHits);
            SetPrivateField(slow, "_procCooldown", cooldown);
            return slow;
        }

        private (EnemyStatusController statusController, EnemyMovement movement) CreateEnemy()
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

        private static float SpeedMultiplier(EnemyMovement movement)
        {
            return (float)GetPrivateField(movement, "_statusSpeedMultiplier");
        }

        [Test]
        public void Stun_TriggersOnlyOnTheRequiredHit_AndStopsMovementCompletely()
        {
            (EnemyStatusController enemy, EnemyMovement movement) = CreateEnemy();
            StatusEffectDefinition stun = CreateEffect(StatusEffectType.Stun, 2f, 0f);
            StatusEffectDefinition slow = CreateFrostSlow(10f, stun, 4, 4);

            for (int hit = 1; hit <= 3; hit++)
            {
                enemy.ApplyStatus(slow);
                Assert.IsFalse(enemy.IsStunned, $"Stunned too early, on hit {hit}.");
                Assert.AreEqual(0.5f, SpeedMultiplier(movement), 0.001f, "A normal Frost hit only slows.");
            }

            enemy.ApplyStatus(slow);

            Assert.IsTrue(enemy.IsStunned);
            Assert.AreEqual(0f, SpeedMultiplier(movement), 0.0001f, "A stun must stop movement, not just slow it.");
        }

        [Test]
        public void Stun_LastsExactlyItsDuration_ThenGivesBackTheStillRunningSlow()
        {
            (EnemyStatusController enemy, EnemyMovement movement) = CreateEnemy();
            StatusEffectDefinition stun = CreateEffect(StatusEffectType.Stun, 2f, 0f);
            StatusEffectDefinition slow = CreateFrostSlow(10f, stun, 1, 1);

            enemy.ApplyStatus(slow);
            Assert.IsTrue(enemy.IsStunned);

            enemy.Tick(1.95f);
            Assert.IsTrue(enemy.IsStunned);
            Assert.AreEqual(0f, SpeedMultiplier(movement), 0.0001f);

            enemy.Tick(0.1f);
            Assert.IsFalse(enemy.IsStunned);
            Assert.AreEqual(0.5f, SpeedMultiplier(movement), 0.001f, "Slow is still running: back to the slowed speed.");
        }

        [Test]
        public void Stun_OutlastingTheSlow_GivesBackFullSpeed()
        {
            (EnemyStatusController enemy, EnemyMovement movement) = CreateEnemy();
            StatusEffectDefinition stun = CreateEffect(StatusEffectType.Stun, 2f, 0f);
            StatusEffectDefinition slow = CreateFrostSlow(1f, stun, 1, 1);

            enemy.ApplyStatus(slow);
            enemy.Tick(1.2f); // slow gone, stun still holding
            Assert.AreEqual(0f, SpeedMultiplier(movement), 0.0001f);

            enemy.Tick(1f);
            Assert.IsFalse(enemy.IsStunned);
            Assert.AreEqual(1f, SpeedMultiplier(movement), 0.001f);
        }

        [Test]
        public void HitsWhileStunned_OrDuringTheCooldown_DoNotCount()
        {
            (EnemyStatusController enemy, _) = CreateEnemy();
            StatusEffectDefinition stun = CreateEffect(StatusEffectType.Stun, 2f, 0f);
            StatusEffectDefinition slow = CreateFrostSlow(10f, stun, 3, 3, cooldown: 0.3f);

            for (int i = 0; i < 3; i++)
            {
                enemy.ApplyStatus(slow);
            }

            Assert.IsTrue(enemy.IsStunned);
            for (int i = 0; i < 6; i++)
            {
                enemy.ApplyStatus(slow); // ignored: no chain stun
            }

            enemy.Tick(2.05f);
            Assert.IsFalse(enemy.IsStunned);

            for (int i = 0; i < 3; i++)
            {
                enemy.ApplyStatus(slow); // still inside the 0.3 s cooldown
            }

            Assert.IsFalse(enemy.IsStunned);

            enemy.Tick(0.35f);
            enemy.ApplyStatus(slow);
            enemy.ApplyStatus(slow);
            Assert.IsFalse(enemy.IsStunned);
            enemy.ApplyStatus(slow);
            Assert.IsTrue(enemy.IsStunned, "The count starts over after the cooldown.");
        }

        [Test]
        public void Counters_ArePerEnemy()
        {
            (EnemyStatusController enemyA, _) = CreateEnemy();
            (EnemyStatusController enemyB, _) = CreateEnemy();
            StatusEffectDefinition stun = CreateEffect(StatusEffectType.Stun, 2f, 0f);
            StatusEffectDefinition slow = CreateFrostSlow(10f, stun, 3, 3);

            enemyA.ApplyStatus(slow);
            enemyA.ApplyStatus(slow);
            enemyB.ApplyStatus(slow);
            enemyA.ApplyStatus(slow);

            Assert.IsTrue(enemyA.IsStunned);
            Assert.IsFalse(enemyB.IsStunned, "Enemy A's hits must not count towards enemy B.");
        }

        [Test]
        public void RequiredHits_AreRolledBetweenMinAndMax()
        {
            StatusEffectDefinition stun = CreateEffect(StatusEffectType.Stun, 2f, 0f);
            StatusEffectDefinition slow = CreateFrostSlow(10f, stun, 3, 6);
            var seen = new HashSet<int>();

            for (int sample = 0; sample < 60; sample++)
            {
                (EnemyStatusController enemy, _) = CreateEnemy();
                int hits = 0;
                while (!enemy.IsStunned && hits < 20)
                {
                    enemy.ApplyStatus(slow);
                    hits++;
                }

                Assert.That(hits, Is.InRange(3, 6));
                seen.Add(hits);
            }

            Assert.Greater(seen.Count, 1, "The number of hits needed should vary, not be a fixed X.");
        }

        [Test]
        public void Clear_ForPoolReuse_ResetsTheCountAndTheStun()
        {
            (EnemyStatusController enemy, EnemyMovement movement) = CreateEnemy();
            StatusEffectDefinition stun = CreateEffect(StatusEffectType.Stun, 2f, 0f);
            StatusEffectDefinition slow = CreateFrostSlow(10f, stun, 3, 3);

            for (int i = 0; i < 3; i++)
            {
                enemy.ApplyStatus(slow);
            }

            Assert.IsTrue(enemy.IsStunned);
            enemy.Clear();
            Assert.IsFalse(enemy.IsStunned);
            Assert.AreEqual(1f, SpeedMultiplier(movement), 0.001f);

            enemy.ApplyStatus(slow);
            enemy.ApplyStatus(slow);
            Assert.IsFalse(enemy.IsStunned, "A reused enemy starts counting from zero.");
            enemy.ApplyStatus(slow);
            Assert.IsTrue(enemy.IsStunned);
        }

        [Test]
        public void DurationScale_ShortensTheStun()
        {
            StatusEffectDefinition stun = CreateEffect(StatusEffectType.Stun, 2f, 0f);
            var instance = new StatusEffectInstance(stun, 0.5f);

            Assert.AreEqual(1f, instance.RemainingDuration, 0.0001f);
            instance.Reapply();
            Assert.AreEqual(1f, instance.RemainingDuration, 0.0001f);
        }
    }
}
