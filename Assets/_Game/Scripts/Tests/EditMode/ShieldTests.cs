using System.Reflection;
using AlienDefense.Combat;
using AlienDefense.Enemies;
using NUnit.Framework;
using UnityEngine;

namespace AlienDefense.Tests.EditMode
{
    public class ShieldTests
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

        private EnemyShield CreateShield(float maxShield)
        {
            _enemyObject = new GameObject("TestEnemy");
            var shield = _enemyObject.AddComponent<EnemyShield>();
            SetPrivateField(shield, "_maxShield", maxShield);
            SetPrivateField(shield, "_regenPerSecond", 5f);
            SetPrivateField(shield, "_regenDelayAfterHit", 3f);
            shield.ResetState();
            return shield;
        }

        [Test]
        public void ResetState_StartsAtFullShield()
        {
            EnemyShield shield = CreateShield(50f);

            Assert.AreEqual(50f, shield.CurrentShield, 0.001f);
            Assert.IsTrue(shield.HasShield);
        }

        [Test]
        public void Absorb_LessThanCurrentShield_ReducesShield_ReturnsZeroLeftover()
        {
            EnemyShield shield = CreateShield(50f);

            float leftover = shield.Absorb(20f);

            Assert.AreEqual(0f, leftover, 0.001f);
            Assert.AreEqual(30f, shield.CurrentShield, 0.001f);
        }

        [Test]
        public void Absorb_MoreThanCurrentShield_DepletesShield_ReturnsLeftoverDamage()
        {
            EnemyShield shield = CreateShield(20f);

            float leftover = shield.Absorb(35f);

            Assert.AreEqual(15f, leftover, 0.001f);
            Assert.AreEqual(0f, shield.CurrentShield, 0.001f);
            Assert.IsFalse(shield.HasShield);
        }

        [Test]
        public void Absorb_WhileDepleted_ReturnsFullIncomingDamage()
        {
            EnemyShield shield = CreateShield(10f);
            shield.Absorb(10f);

            float leftover = shield.Absorb(25f);

            Assert.AreEqual(25f, leftover, 0.001f);
        }

        [Test]
        public void EnemyHealth_TryApplyDamage_AbsorbsThroughShieldBeforeHealth()
        {
            EnemyShield shield = CreateShield(30f);
            var health = _enemyObject.AddComponent<EnemyHealth>();
            health.Initialize(100f);
            health.SetDefenseAndShield(null, shield);

            health.TryApplyDamage(new DamageInfo(20f, null, Vector3.zero));

            Assert.AreEqual(100f, health.CurrentHealth, 0.001f, "Shield should fully absorb damage within its capacity.");
            Assert.AreEqual(10f, shield.CurrentShield, 0.001f);
        }

        [Test]
        public void EnemyHealth_TryApplyDamage_OverflowsToHealth_OnceShieldDepleted()
        {
            EnemyShield shield = CreateShield(15f);
            var health = _enemyObject.AddComponent<EnemyHealth>();
            health.Initialize(100f);
            health.SetDefenseAndShield(null, shield);

            health.TryApplyDamage(new DamageInfo(40f, null, Vector3.zero));

            Assert.AreEqual(0f, shield.CurrentShield, 0.001f);
            Assert.AreEqual(75f, health.CurrentHealth, 0.001f);
        }

        [Test]
        public void EnemyHealth_TrueDamage_BypassesShield_HitsHealthDirectly()
        {
            EnemyShield shield = CreateShield(50f);
            var health = _enemyObject.AddComponent<EnemyHealth>();
            health.Initialize(100f);
            health.SetDefenseAndShield(null, shield);

            health.TryApplyDamage(new DamageInfo(20f, null, Vector3.zero, DamageType.True));

            Assert.AreEqual(50f, shield.CurrentShield, 0.001f, "True damage must bypass the shield untouched.");
            Assert.AreEqual(80f, health.CurrentHealth, 0.001f);
        }
    }
}
