using AlienDefense.Economy;
using NUnit.Framework;

namespace AlienDefense.Tests.EditMode
{
    public class LevelRewardCalculatorTests
    {
        [Test]
        public void CalculateCoinReward_NoVipBonus_MatchesBaseFormula()
        {
            // BaseCoinReward(20) + CoinPerStar(10) * stars
            Assert.AreEqual(20, LevelRewardCalculator.CalculateCoinReward(0, 0f));
            Assert.AreEqual(30, LevelRewardCalculator.CalculateCoinReward(1, 0f));
            Assert.AreEqual(50, LevelRewardCalculator.CalculateCoinReward(3, 0f));
        }

        [Test]
        public void CalculateCoinReward_WithVipBonus_ScalesUp()
        {
            // 50 * 1.10 = 55 exactly, avoiding a .5 case (Mathf.RoundToInt rounds half-to-even, not half-up).
            int reward = LevelRewardCalculator.CalculateCoinReward(3, 0.10f);
            Assert.AreEqual(55, reward);
        }

        [Test]
        public void CalculateCoinReward_NegativeVipBonus_TreatedAsZero()
        {
            Assert.AreEqual(50, LevelRewardCalculator.CalculateCoinReward(3, -0.5f));
        }

        [Test]
        public void CalculateGemReward_FirstTimePerfect_GrantsBonus()
        {
            Assert.AreEqual(5, LevelRewardCalculator.CalculateGemReward(3, isFirstTimeCompletion: true));
        }

        [Test]
        public void CalculateGemReward_NotFirstTime_GrantsNothing()
        {
            Assert.AreEqual(0, LevelRewardCalculator.CalculateGemReward(3, isFirstTimeCompletion: false));
        }

        [Test]
        public void CalculateGemReward_NotPerfect_GrantsNothingEvenFirstTime()
        {
            Assert.AreEqual(0, LevelRewardCalculator.CalculateGemReward(2, isFirstTimeCompletion: true));
        }
    }
}
