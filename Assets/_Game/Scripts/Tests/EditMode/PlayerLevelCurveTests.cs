using AlienDefense.Progression;
using NUnit.Framework;

namespace AlienDefense.Tests.EditMode
{
    /// <summary>Player level from lifetime XP: 1000 to reach level 2, then 500 more per level.</summary>
    public class PlayerLevelCurveTests
    {
        private readonly PlayerLevelCurve _curve = new PlayerLevelCurve(xpForLevel2: 1000, xpIncreasePerLevel: 500, maxLevel: 5);

        [TestCase(0, 1, 0, 1000)]
        [TestCase(999, 1, 999, 1000)]
        [TestCase(1000, 2, 0, 1500)]
        [TestCase(3000, 3, 500, 2000)]
        [TestCase(12000, 5, 0, 0)]
        public void Evaluate_SplitsXpIntoLevelAndProgress(int xp, int level, int into, int need)
        {
            PlayerLevelProgress progress = _curve.Evaluate(xp);

            Assert.AreEqual(level, progress.Level);
            Assert.AreEqual(into, progress.XpIntoLevel);
            Assert.AreEqual(need, progress.XpForNextLevel);
        }

        [Test]
        public void Progress01_IsTheShareOfTheCurrentLevel_AndFullAtMax()
        {
            Assert.AreEqual(0.25f, _curve.Evaluate(3000).Progress01, 0.0001f);
            Assert.AreEqual(1f, _curve.Evaluate(99999).Progress01);
            Assert.IsTrue(_curve.Evaluate(99999).IsMaxLevel);
        }
    }
}
