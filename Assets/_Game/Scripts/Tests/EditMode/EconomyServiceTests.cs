using AlienDefense.Economy;
using NUnit.Framework;

namespace AlienDefense.Tests.EditMode
{
    public class EconomyServiceTests
    {
        [Test]
        public void Constructor_SetsStartingResource()
        {
            var economy = new EconomyService(250);

            Assert.AreEqual(250, economy.CurrentResource);
        }

        [Test]
        public void Constructor_ClampsNegativeStartingResourceToZero()
        {
            var economy = new EconomyService(-50);

            Assert.AreEqual(0, economy.CurrentResource);
        }

        [Test]
        public void Add_IncreasesResourceAndFiresEvent()
        {
            var economy = new EconomyService(100);
            int eventCount = 0;
            int lastValue = -1;
            economy.ResourceChanged += value => { eventCount++; lastValue = value; };

            economy.Add(50);

            Assert.AreEqual(150, economy.CurrentResource);
            Assert.AreEqual(1, eventCount);
            Assert.AreEqual(150, lastValue);
        }

        [Test]
        public void Add_IgnoresNonPositiveAmount()
        {
            var economy = new EconomyService(100);
            int eventCount = 0;
            economy.ResourceChanged += _ => eventCount++;

            economy.Add(0);
            economy.Add(-10);

            Assert.AreEqual(100, economy.CurrentResource);
            Assert.AreEqual(0, eventCount);
        }

        [Test]
        public void TrySpend_SucceedsWhenAffordable()
        {
            var economy = new EconomyService(100);

            bool result = economy.TrySpend(60);

            Assert.IsTrue(result);
            Assert.AreEqual(40, economy.CurrentResource);
        }

        [Test]
        public void TrySpend_FailsWhenNotAffordable_AndDoesNotChangeResource()
        {
            var economy = new EconomyService(50);

            bool result = economy.TrySpend(100);

            Assert.IsFalse(result);
            Assert.AreEqual(50, economy.CurrentResource);
        }

        [Test]
        public void TrySpend_RejectsNonPositiveAmount()
        {
            var economy = new EconomyService(100);

            Assert.IsFalse(economy.TrySpend(0));
            Assert.IsFalse(economy.TrySpend(-10));
            Assert.AreEqual(100, economy.CurrentResource);
        }

        [Test]
        public void CanAfford_ReflectsCurrentResource()
        {
            var economy = new EconomyService(100);

            Assert.IsTrue(economy.CanAfford(100));
            Assert.IsTrue(economy.CanAfford(0));
            Assert.IsFalse(economy.CanAfford(101));
        }

        [Test]
        public void ResourceNeverGoesNegative_AcrossMultipleSpends()
        {
            var economy = new EconomyService(30);

            economy.TrySpend(20);
            bool secondSpend = economy.TrySpend(20);

            Assert.IsFalse(secondSpend);
            Assert.AreEqual(10, economy.CurrentResource);
        }
    }
}
