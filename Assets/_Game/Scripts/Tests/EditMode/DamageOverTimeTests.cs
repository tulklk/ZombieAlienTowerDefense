using System.Collections.Generic;
using System.Reflection;
using AlienDefense.Combat;
using AlienDefense.Enemies;
using NUnit.Framework;
using UnityEngine;

namespace AlienDefense.Tests.EditMode
{
    public class DamageOverTimeTests
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

        private static T CreateSilently<T>(System.Func<T> factory)
        {
            bool previousLogEnabled = Debug.unityLogger.logEnabled;
            Debug.unityLogger.logEnabled = false;
            T result = factory();
            Debug.unityLogger.logEnabled = previousLogEnabled;
            return result;
        }

        private StatusEffectDefinition CreateBurn(float magnitude, int maxStacks)
        {
            var definition = CreateSilently(() => ScriptableObject.CreateInstance<StatusEffectDefinition>());
            _scriptableObjects.Add(definition);
            SetPrivateField(definition, "_type", StatusEffectType.Burn);
            SetPrivateField(definition, "_duration", 5f);
            SetPrivateField(definition, "_magnitude", magnitude);
            SetPrivateField(definition, "_tickInterval", 1f);
            SetPrivateField(definition, "_stackingRule", StatusStackingRule.StackMagnitude);
            SetPrivateField(definition, "_maxStacks", maxStacks);
            return definition;
        }

        private (EnemyStatusController statusController, EnemyHealth health) CreateStatusController()
        {
            var go = new GameObject("TestEnemy");
            _spawnedObjects.Add(go);
            var health = go.AddComponent<EnemyHealth>();
            health.Initialize(100f);

            var statusController = go.AddComponent<EnemyStatusController>();
            SetPrivateField(statusController, "_health", health);

            return (statusController, health);
        }

        [Test]
        public void Burn_DoesNotDamage_BeforeFirstTickInterval()
        {
            (EnemyStatusController statusController, EnemyHealth health) = CreateStatusController();
            statusController.ApplyStatus(CreateBurn(4f, 3));

            statusController.Tick(0.5f);

            Assert.AreEqual(100f, health.CurrentHealth, 0.001f);
        }

        [Test]
        public void Burn_DealsDamage_ThroughNormalDamagePipeline_OnEachTick()
        {
            (EnemyStatusController statusController, EnemyHealth health) = CreateStatusController();
            StatusEffectDefinition burn = CreateBurn(4f, 3);
            statusController.ApplyStatus(burn);

            statusController.Tick(1f);

            Assert.AreEqual(96f, health.CurrentHealth, 0.001f);
        }

        [Test]
        public void Burn_TickDamage_ScalesWithStackCount()
        {
            (EnemyStatusController statusController, EnemyHealth health) = CreateStatusController();
            StatusEffectDefinition burn = CreateBurn(4f, 3);

            statusController.ApplyStatus(burn);
            statusController.ApplyStatus(burn);
            statusController.ApplyStatus(burn);

            statusController.Tick(1f);

            // 3 stacks x 4 magnitude = 12 damage in a single tick.
            Assert.AreEqual(88f, health.CurrentHealth, 0.001f);
        }

        [Test]
        public void Burn_StopsTicking_AfterEnemyDies()
        {
            (EnemyStatusController statusController, EnemyHealth health) = CreateStatusController();
            StatusEffectDefinition burn = CreateBurn(4f, 3);
            statusController.ApplyStatus(burn);
            health.TryApplyDamage(999f);
            Assert.IsTrue(health.IsDead);

            Assert.DoesNotThrow(() =>
            {
                statusController.Tick(1f);
                statusController.Tick(1f);
            });
            Assert.AreEqual(0f, health.CurrentHealth, 0.001f);
        }

        [Test]
        public void Burn_NeverGrantsRewardOrResolvesEnemy_OnlyAppliesDamage()
        {
            (EnemyStatusController statusController, EnemyHealth health) = CreateStatusController();
            StatusEffectDefinition burn = CreateBurn(999f, 1);
            statusController.ApplyStatus(burn);
            int diedCount = 0;
            health.Died += () => diedCount++;

            statusController.Tick(1f);

            Assert.AreEqual(1, diedCount);
            Assert.AreEqual(0f, health.CurrentHealth);
        }
    }
}
