using AlienDefense.Base;
using NUnit.Framework;

namespace AlienDefense.Tests.EditMode
{
    public class BaseHealthServiceTests
    {
        [Test]
        public void Constructor_SetsCurrentHealthToMax()
        {
            var health = new BaseHealthService(20);

            Assert.AreEqual(20, health.CurrentHealth);
            Assert.AreEqual(20, health.MaxHealth);
            Assert.IsFalse(health.IsDestroyed);
        }

        [Test]
        public void TakeDamage_ReducesHealthAndFiresEvent()
        {
            var health = new BaseHealthService(20);
            int eventCount = 0;
            health.HealthChanged += (current, max) => eventCount++;

            health.TakeDamage(5);

            Assert.AreEqual(15, health.CurrentHealth);
            Assert.AreEqual(1, eventCount);
        }

        [Test]
        public void TakeDamage_ClampsAtZero_NeverGoesNegative()
        {
            var health = new BaseHealthService(10);

            health.TakeDamage(999);

            Assert.AreEqual(0, health.CurrentHealth);
            Assert.IsTrue(health.IsDestroyed);
        }

        [Test]
        public void TakeDamage_RejectsNonPositiveAmount()
        {
            var health = new BaseHealthService(10);
            int eventCount = 0;
            health.HealthChanged += (current, max) => eventCount++;

            health.TakeDamage(0);
            health.TakeDamage(-5);

            Assert.AreEqual(10, health.CurrentHealth);
            Assert.AreEqual(0, eventCount);
        }

        [Test]
        public void Destroyed_FiresExactlyOnce_EvenWithFurtherDamage()
        {
            var health = new BaseHealthService(10);
            int destroyedCount = 0;
            health.Destroyed += () => destroyedCount++;

            health.TakeDamage(10);
            health.TakeDamage(5);
            health.TakeDamage(1);

            Assert.AreEqual(1, destroyedCount);
        }

        [Test]
        public void TakeDamage_AfterDestroyed_DoesNotFireHealthChanged()
        {
            var health = new BaseHealthService(10);
            health.TakeDamage(10);
            int eventCount = 0;
            health.HealthChanged += (current, max) => eventCount++;

            health.TakeDamage(5);

            Assert.AreEqual(0, eventCount);
            Assert.AreEqual(0, health.CurrentHealth);
        }
    }
}
