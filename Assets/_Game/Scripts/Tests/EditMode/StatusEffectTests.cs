using System.Reflection;
using AlienDefense.Combat;
using NUnit.Framework;
using UnityEngine;

namespace AlienDefense.Tests.EditMode
{
    public class StatusEffectTests
    {
        private StatusEffectDefinition _definition;

        [TearDown]
        public void TearDown()
        {
            if (_definition != null)
            {
                Object.DestroyImmediate(_definition);
            }
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

        private StatusEffectDefinition CreateDefinition(StatusEffectType type, float duration, float magnitude, float tickInterval, StatusStackingRule stackingRule, int maxStacks)
        {
            _definition = CreateSilently(() => ScriptableObject.CreateInstance<StatusEffectDefinition>());
            SetPrivateField(_definition, "_type", type);
            SetPrivateField(_definition, "_duration", duration);
            SetPrivateField(_definition, "_magnitude", magnitude);
            SetPrivateField(_definition, "_tickInterval", tickInterval);
            SetPrivateField(_definition, "_stackingRule", stackingRule);
            SetPrivateField(_definition, "_maxStacks", maxStacks);
            return _definition;
        }

        [Test]
        public void NewInstance_StartsWithOneStack_AndFullDuration()
        {
            StatusEffectDefinition definition = CreateDefinition(StatusEffectType.Slow, 3f, 0.5f, 1f, StatusStackingRule.RefreshDurationOnly, 1);

            var instance = new StatusEffectInstance(definition);

            Assert.AreEqual(1, instance.StackCount);
            Assert.AreEqual(3f, instance.RemainingDuration, 0.001f);
            Assert.IsFalse(instance.IsExpired);
        }

        [Test]
        public void Reapply_RefreshDurationOnly_NeverIncreasesStack()
        {
            StatusEffectDefinition definition = CreateDefinition(StatusEffectType.Slow, 3f, 0.5f, 1f, StatusStackingRule.RefreshDurationOnly, 1);
            var instance = new StatusEffectInstance(definition);
            instance.Tick(2f);

            instance.Reapply();

            Assert.AreEqual(1, instance.StackCount);
            Assert.AreEqual(3f, instance.RemainingDuration, 0.001f);
        }

        [Test]
        public void Reapply_StackMagnitude_IncreasesStackUpToMaxStacks()
        {
            StatusEffectDefinition definition = CreateDefinition(StatusEffectType.Burn, 4f, 4f, 1f, StatusStackingRule.StackMagnitude, 3);
            var instance = new StatusEffectInstance(definition);

            instance.Reapply();
            instance.Reapply();
            instance.Reapply();
            instance.Reapply();

            Assert.AreEqual(3, instance.StackCount);
        }

        [Test]
        public void Tick_ReturnsTrue_OnlyOnceTickIntervalElapsed()
        {
            StatusEffectDefinition definition = CreateDefinition(StatusEffectType.Burn, 10f, 4f, 1f, StatusStackingRule.StackMagnitude, 3);
            var instance = new StatusEffectInstance(definition);

            Assert.IsFalse(instance.Tick(0.4f));
            Assert.IsFalse(instance.Tick(0.4f));
            Assert.IsTrue(instance.Tick(0.4f));
        }

        [Test]
        public void IsExpired_BecomesTrue_AfterDurationElapses()
        {
            StatusEffectDefinition definition = CreateDefinition(StatusEffectType.Slow, 2f, 0.5f, 1f, StatusStackingRule.RefreshDurationOnly, 1);
            var instance = new StatusEffectInstance(definition);

            instance.Tick(1f);
            Assert.IsFalse(instance.IsExpired);

            instance.Tick(1.5f);
            Assert.IsTrue(instance.IsExpired);
        }
    }
}
