using AlienDefense.UI.MainMenu;
using NUnit.Framework;

namespace AlienDefense.Tests.EditMode
{
    public class CurrencyFormatterTests
    {
        [TestCase(0, "0")]
        [TestCase(999, "999")]
        [TestCase(8900, "8.9K")]
        [TestCase(27600, "27.6K")]
        [TestCase(1000, "1K")]
        [TestCase(1000000, "1M")]
        [TestCase(1250000, "1.25M")]
        [TestCase(-5, "0")]
        public void Format_ReturnsExpectedCompactString(int amount, string expected)
        {
            Assert.AreEqual(expected, CurrencyFormatter.Format(amount));
        }
    }
}
