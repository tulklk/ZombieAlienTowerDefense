using System;
using AlienDefense.Save;
using NUnit.Framework;

namespace AlienDefense.Tests.EditMode
{
    public class DailyRewardCalculatorTests
    {
        private static readonly DateTime Day1 = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        [Test]
        public void CanClaim_NeverClaimed_ReturnsTrue()
        {
            Assert.IsTrue(DailyRewardCalculator.CanClaim(null, Day1));
        }

        [Test]
        public void CanClaim_AlreadyClaimedToday_ReturnsFalse()
        {
            Assert.IsFalse(DailyRewardCalculator.CanClaim(Day1, Day1.AddHours(5)));
        }

        [Test]
        public void CanClaim_ClaimedYesterday_ReturnsTrue()
        {
            Assert.IsTrue(DailyRewardCalculator.CanClaim(Day1, Day1.AddDays(1)));
        }

        [Test]
        public void ComputeNextStreakDay_NeverClaimed_ReturnsOne()
        {
            Assert.AreEqual(1, DailyRewardCalculator.ComputeNextStreakDay(null, 0, Day1));
        }

        [Test]
        public void ComputeNextStreakDay_ClaimedExactlyYesterday_Increments()
        {
            int next = DailyRewardCalculator.ComputeNextStreakDay(Day1, 3, Day1.AddDays(1));
            Assert.AreEqual(4, next);
        }

        [Test]
        public void ComputeNextStreakDay_PastDaySeven_WrapsToOne()
        {
            int next = DailyRewardCalculator.ComputeNextStreakDay(Day1, 7, Day1.AddDays(1));
            Assert.AreEqual(1, next);
        }

        [Test]
        public void ComputeNextStreakDay_GapOfTwoOrMoreDays_ResetsToOne()
        {
            int next = DailyRewardCalculator.ComputeNextStreakDay(Day1, 5, Day1.AddDays(3));
            Assert.AreEqual(1, next);
        }

        [Test]
        public void CoinReward_Day1ThroughDay7_MatchesTable()
        {
            for (int day = 1; day <= 7; day++)
            {
                Assert.AreEqual(DailyRewardCalculator.CoinRewardByDay[day - 1], DailyRewardCalculator.CoinReward(day));
            }
        }

        [Test]
        public void GemReward_OnlyDaySeven_IsNonZero()
        {
            for (int day = 1; day <= 6; day++)
            {
                Assert.AreEqual(0, DailyRewardCalculator.GemReward(day));
            }

            Assert.AreEqual(DailyRewardCalculator.Day7GemBonus, DailyRewardCalculator.GemReward(7));
        }
    }
}
