using System.Reflection;
using AlienDefense.Combat;
using AlienDefense.Enemies;
using NUnit.Framework;
using UnityEngine;

namespace AlienDefense.Tests.EditMode
{
    public class ArmorTests
    {
        private GameObject _enemyObject;

        [TearDown]
        public void TearDown()
        {
            if (_enemyObject != null)
            {
                Object.DestroyImmediate(_enemyObject);
            }
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(field, $"Field '{fieldName}' not found on {target.GetType().Name}.");
            field.SetValue(target, value);
        }

        private EnemyDefense CreateDefense(float reductionPercent)
        {
            _enemyObject = new GameObject("TestEnemy");
            var defense = _enemyObject.AddComponent<EnemyDefense>();
            SetPrivateField(defense, "_physicalDamageReductionPercent", reductionPercent);
            defense.ResetState();
            return defense;
        }

        [Test]
        public void ModifyIncomingDamage_Physical_IsReducedByPercent()
        {
            EnemyDefense defense = CreateDefense(0.5f);

            float result = defense.ModifyIncomingDamage(100f, DamageType.Physical);

            Assert.AreEqual(50f, result, 0.001f);
        }

        [Test]
        public void ModifyIncomingDamage_Energy_BypassesArmor()
        {
            EnemyDefense defense = CreateDefense(0.9f);

            float result = defense.ModifyIncomingDamage(100f, DamageType.Energy);

            Assert.AreEqual(100f, result, 0.001f);
        }

        [Test]
        public void ModifyIncomingDamage_Explosive_BypassesArmor()
        {
            EnemyDefense defense = CreateDefense(0.9f);

            float result = defense.ModifyIncomingDamage(100f, DamageType.Explosive);

            Assert.AreEqual(100f, result, 0.001f);
        }

        [Test]
        public void ModifyIncomingDamage_True_BypassesArmor()
        {
            EnemyDefense defense = CreateDefense(0.9f);

            float result = defense.ModifyIncomingDamage(100f, DamageType.True);

            Assert.AreEqual(100f, result, 0.001f);
        }

        [Test]
        public void SetPhysicalDamageReductionPercent_OverridesActiveReduction()
        {
            EnemyDefense defense = CreateDefense(0.1f);

            defense.SetPhysicalDamageReductionPercent(0.8f);

            Assert.AreEqual(20f, defense.ModifyIncomingDamage(100f, DamageType.Physical), 0.001f);
        }

        [Test]
        public void ResetState_DiscardsRuntimeOverride_RestoresInspectorBase()
        {
            EnemyDefense defense = CreateDefense(0.1f);
            defense.SetPhysicalDamageReductionPercent(0.9f);

            defense.ResetState();

            Assert.AreEqual(90f, defense.ModifyIncomingDamage(100f, DamageType.Physical), 0.001f);
        }

        [Test]
        public void EnemyHealth_TryApplyDamage_ConsultsDefense_ForPhysicalOnly()
        {
            EnemyDefense defense = CreateDefense(0.5f);
            var health = _enemyObject.AddComponent<EnemyHealth>();
            health.Initialize(100f);
            health.SetDefenseAndShield(defense, null);

            health.TryApplyDamage(new DamageInfo(40f, null, Vector3.zero, DamageType.Physical));

            Assert.AreEqual(80f, health.CurrentHealth, 0.001f);
        }
    }
}
