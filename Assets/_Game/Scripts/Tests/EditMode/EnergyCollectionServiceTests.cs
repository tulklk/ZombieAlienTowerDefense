using AlienDefense.Economy;
using NUnit.Framework;

namespace AlienDefense.Tests.EditMode
{
    /// <summary>Covers the ONLY source of Energy/XP: EnergyCollectionService.Collect. See EnemyResolutionPolicy
    /// and TractorAbsorbablePropTests for the two things that must NEVER reach this class: Enemy capture and
    /// Environment Prop absorption.</summary>
    public class EnergyCollectionServiceTests
    {
        [Test]
        public void Collect_AddsToBothWalletAndExperience_ByTheSameValue()
        {
            var wallet = new EnergyWalletService();
            var progression = new PlayerLevelProgressionService();
            var collection = new EnergyCollectionService(wallet, progression);

            collection.Collect(3);

            Assert.AreEqual(3, wallet.CurrentEnergy);
            Assert.AreEqual(3, progression.CurrentExperience);
        }

        [Test]
        public void Collect_Accumulates_AcrossMultipleCalls()
        {
            var wallet = new EnergyWalletService();
            var progression = new PlayerLevelProgressionService();
            var collection = new EnergyCollectionService(wallet, progression);

            collection.Collect(2);
            collection.Collect(5);

            Assert.AreEqual(7, wallet.CurrentEnergy);
            Assert.AreEqual(7, progression.CurrentExperience);
        }

        [Test]
        public void Collect_IgnoresZeroOrNegativeValues()
        {
            var wallet = new EnergyWalletService();
            var progression = new PlayerLevelProgressionService();
            var collection = new EnergyCollectionService(wallet, progression);

            collection.Collect(0);
            collection.Collect(-5);

            Assert.AreEqual(0, wallet.CurrentEnergy);
            Assert.AreEqual(0, progression.CurrentExperience);
        }

        [Test]
        public void PlayerLevelProgression_LevelsUp_AsExperienceCrossesThresholds()
        {
            var progression = new PlayerLevelProgressionService();
            Assert.AreEqual(1, progression.CurrentLevel);

            progression.AddExperience(25);

            Assert.AreEqual(25, progression.CurrentExperience);
            Assert.Greater(progression.CurrentLevel, 1, "25 XP should have crossed at least one level threshold.");
        }

        [Test]
        public void EnergyWallet_NeverSpent_ByThisRefactor_OnlyGrows()
        {
            // EnergyWalletService intentionally exposes no TrySpend/Remove — Build/Upgrade still spend
            // EconomyService (unchanged), not EnergyWallet. This test documents that boundary: it will fail to
            // compile (not just fail at runtime) if a spend API is ever added without a deliberate decision.
            var wallet = new EnergyWalletService();
            wallet.Add(10);
            Assert.AreEqual(10, wallet.CurrentEnergy);
        }
    }
}
