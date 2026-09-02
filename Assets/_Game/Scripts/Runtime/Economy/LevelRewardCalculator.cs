using UnityEngine;

namespace AlienDefense.Economy
{
    /// <summary>Pure formula for the Coin (+ rare Gem) reward granted on winning a level. Placeholder numbers,
    /// easy to retune later — separate from the star formula (LevelCompositionRoot.BuildLevelCompletedResult)
    /// and from the in-level EconomyService gold, which never touches MetaCurrency/Gems.</summary>
    public static class LevelRewardCalculator
    {
        private const int BaseCoinReward = 20;
        private const int CoinPerStar = 10;
        private const int FirstTimePerfectGemBonus = 5;

        /// <param name="vipCoinBonusMultiplier">e.g. 0.25 = +25% Coin. See VipTierTable.</param>
        public static int CalculateCoinReward(int stars, float vipCoinBonusMultiplier)
        {
            int baseReward = BaseCoinReward + CoinPerStar * Mathf.Clamp(stars, 0, 3);
            return Mathf.RoundToInt(baseReward * (1f + Mathf.Max(0f, vipCoinBonusMultiplier)));
        }

        /// <summary>A one-time bonus the very first time a level is 3-starred — rewards skillful first clears
        /// with the scarce premium currency instead of inventing an ongoing Gem-farming loop.</summary>
        public static int CalculateGemReward(int stars, bool isFirstTimeCompletion)
        {
            return isFirstTimeCompletion && stars >= 3 ? FirstTimePerfectGemBonus : 0;
        }
    }
}
