using System;
using UnityEngine;

namespace AlienDefense.Save
{
    /// <summary>Pure formulas for the 7-day login streak — no save-data mutation, PlayerProfileService is the
    /// only thing that writes DailyRewardSaveData. Placeholder reward table, easy to retune later.</summary>
    public static class DailyRewardCalculator
    {
        public static readonly int[] CoinRewardByDay = { 20, 30, 40, 60, 80, 100, 150 };
        public const int Day7GemBonus = 10;

        public static bool CanClaim(DateTime? lastClaimUtc, DateTime nowUtc)
        {
            return lastClaimUtc == null || lastClaimUtc.Value.Date < nowUtc.Date;
        }

        /// <summary>1 if never claimed or the streak was broken (gap of more than 1 day); previous+1 (wrapping
        /// 7 back to 1) if claimed exactly yesterday.</summary>
        public static int ComputeNextStreakDay(DateTime? lastClaimUtc, int previousStreakDay, DateTime nowUtc)
        {
            if (lastClaimUtc == null)
            {
                return 1;
            }

            int daysSinceLastClaim = (nowUtc.Date - lastClaimUtc.Value.Date).Days;
            if (daysSinceLastClaim != 1)
            {
                return 1;
            }

            int next = previousStreakDay + 1;
            return next > 7 ? 1 : next;
        }

        public static int CoinReward(int streakDay)
        {
            int index = Mathf.Clamp(streakDay - 1, 0, CoinRewardByDay.Length - 1);
            return CoinRewardByDay[index];
        }

        public static int GemReward(int streakDay)
        {
            return streakDay == 7 ? Day7GemBonus : 0;
        }
    }
}
