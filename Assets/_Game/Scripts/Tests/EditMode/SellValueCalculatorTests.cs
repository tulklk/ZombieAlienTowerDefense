using AlienDefense.Towers;
using NUnit.Framework;

namespace AlienDefense.Tests.EditMode
{
    public class SellValueCalculatorTests
    {
        [Test]
        public void Calculate_FloorsToNearestInt()
        {
            int result = SellValueCalculator.Calculate(75, 0.7f);

            Assert.AreEqual(52, result);
        }

        [Test]
        public void Calculate_IncludesUpgradesInInvestedAmount()
        {
            int result = SellValueCalculator.Calculate(75 + 90, 0.5f);

            Assert.AreEqual(82, result);
        }

        [Test]
        public void Calculate_ZeroInvested_ReturnsZero()
        {
            int result = SellValueCalculator.Calculate(0, 0.5f);

            Assert.AreEqual(0, result);
        }

        [Test]
        public void Calculate_ZeroPercentage_ReturnsZero()
        {
            int result = SellValueCalculator.Calculate(100, 0f);

            Assert.AreEqual(0, result);
        }

        [Test]
        public void Calculate_FullPercentage_ReturnsFullInvestment()
        {
            int result = SellValueCalculator.Calculate(100, 1f);

            Assert.AreEqual(100, result);
        }
    }
}
