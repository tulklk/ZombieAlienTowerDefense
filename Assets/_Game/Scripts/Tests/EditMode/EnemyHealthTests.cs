using AlienDefense.Enemies;
using NUnit.Framework;
using UnityEngine;

namespace AlienDefense.Tests.EditMode
{
    public class EnemyHealthTests
    {
        private GameObject _healthObject;

        [TearDown]
        public void TearDown()
        {
            if (_healthObject != null)
            {
                Object.DestroyImmediate(_healthObject);
            }
        }

        private EnemyHealth CreateHealth()
        {
            _healthObject = new GameObject("TestEnemyHealth");
            return _healthObject.AddComponent<EnemyHealth>();
        }

        [Test]
        public void Initialize_SetsCurrentHealthToMaximum()
        {
            var health = CreateHealth();

            health.Initialize(100f);

            Assert.AreEqual(100f, health.CurrentHealth);
            Assert.AreEqual(100f, health.MaximumHealth);
            Assert.IsFalse(health.IsDead);
        }

        [Test]
        public void TryApplyDamage_ReducesHealthAndFiresEvent()
        {
            var health = CreateHealth();
            health.Initialize(100f);
            int eventCount = 0;
            health.HealthChanged += (current, max) => eventCount++;

            bool applied = health.TryApplyDamage(30f);

            Assert.IsTrue(applied);
            Assert.AreEqual(70f, health.CurrentHealth);
            Assert.AreEqual(1, eventCount);
        }

        [Test]
        public void TryApplyDamage_RejectsNonPositiveAmount()
        {
            var health = CreateHealth();
            health.Initialize(100f);

            Assert.IsFalse(health.TryApplyDamage(0f));
            Assert.IsFalse(health.TryApplyDamage(-5f));
            Assert.AreEqual(100f, health.CurrentHealth);
        }

        [Test]
        public void TryApplyDamage_ClampsAtZero_NeverGoesNegative()
        {
            var health = CreateHealth();
            health.Initialize(10f);

            health.TryApplyDamage(999f);

            Assert.AreEqual(0f, health.CurrentHealth);
            Assert.IsTrue(health.IsDead);
        }

        [Test]
        public void TryApplyDamage_AfterDeath_IsRejected()
        {
            var health = CreateHealth();
            health.Initialize(10f);
            health.TryApplyDamage(10f);

            bool secondHit = health.TryApplyDamage(5f);

            Assert.IsFalse(secondHit);
            Assert.AreEqual(0f, health.CurrentHealth);
        }

        [Test]
        public void Died_FiresExactlyOnce_EvenWithFurtherDamage()
        {
            var health = CreateHealth();
            health.Initialize(10f);
            int diedCount = 0;
            health.Died += () => diedCount++;

            health.TryApplyDamage(10f);
            health.TryApplyDamage(5f);

            Assert.AreEqual(1, diedCount);
        }

        [Test]
        public void ResetState_RestoresFullHealthAndAllowsDiedToFireAgain()
        {
            var health = CreateHealth();
            health.Initialize(10f);
            health.TryApplyDamage(10f);
            int diedCount = 0;
            health.Died += () => diedCount++;

            health.ResetState();

            Assert.AreEqual(10f, health.CurrentHealth);
            Assert.IsFalse(health.IsDead);

            health.TryApplyDamage(10f);
            Assert.AreEqual(1, diedCount);
        }
    }
}
