using System.Collections.Generic;
using AlienDefense.Core;
using AlienDefense.Progression;
using NUnit.Framework;

namespace AlienDefense.Tests.EditMode
{
    /// <summary>The queue that carries "these rewards were just granted" from a finished level to MainMenu. The
    /// rewards themselves are already in the profile by then, so the only thing that can go wrong here is the
    /// animation playing twice and making a player think they were paid twice.</summary>
    public sealed class PendingRewardPresentationTests
    {
        private readonly List<VictoryReward> _buffer = new List<VictoryReward>();

        [Test]
        public void Consume_HandsOverTheRewards_ThenLeavesNothingBehind()
        {
            var presentation = new PendingRewardPresentation();
            presentation.Set(new[]
            {
                new VictoryReward(VictoryRewardType.Coins, 5800),
                new VictoryReward(VictoryRewardType.Experience, 3000),
            });

            Assert.IsTrue(presentation.HasPending);
            Assert.IsTrue(presentation.TryConsume(_buffer));
            Assert.AreEqual(2, _buffer.Count);
            Assert.AreEqual(5800, _buffer[0].Amount);
            Assert.IsFalse(presentation.HasPending, "The queue must empty as it is read.");
        }

        [Test]
        public void SecondConsume_ReturnsNothing_SoReloadingMainMenuCannotReplayTheShower()
        {
            var presentation = new PendingRewardPresentation();
            presentation.Set(new[] { new VictoryReward(VictoryRewardType.Coins, 1200) });

            Assert.IsTrue(presentation.TryConsume(_buffer));
            Assert.IsFalse(presentation.TryConsume(_buffer));
        }

        [Test]
        public void Set_DropsEmptyRewards_SoNoIconFliesForNothing()
        {
            var presentation = new PendingRewardPresentation();
            presentation.Set(new[]
            {
                new VictoryReward(VictoryRewardType.Coins, 0),
                new VictoryReward(VictoryRewardType.Gems, 3),
            });

            Assert.IsTrue(presentation.TryConsume(_buffer));
            Assert.AreEqual(1, _buffer.Count);
            Assert.AreEqual(VictoryRewardType.Gems, _buffer[0].Type);
        }

        [Test]
        public void Set_ReplacesAnEarlierRunThatNeverReachedMainMenu()
        {
            var presentation = new PendingRewardPresentation();
            presentation.Set(new[] { new VictoryReward(VictoryRewardType.Coins, 100) });
            presentation.Set(new[] { new VictoryReward(VictoryRewardType.Coins, 250) });

            Assert.IsTrue(presentation.TryConsume(_buffer));
            Assert.AreEqual(1, _buffer.Count);
            Assert.AreEqual(250, _buffer[0].Amount);
        }
    }
}
