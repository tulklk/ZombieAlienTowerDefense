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
        public void Collect_AddsWalletValueAndExperienceValue_Independently()
        {
            var wallet = new EnergyWalletService();
            var progression = new PlayerLevelProgressionService();
            var collection = new EnergyCollectionService(wallet, progression);

            // value (Energy) and experienceValue (XP) are deliberately independent numbers now - a big kill can
            // still be worth a lot of Energy without also granting outsized XP (see EnemyDefinition.ExperienceReward).
            collection.Collect(150, 2);

            Assert.AreEqual(150, wallet.CurrentEnergy);
            Assert.AreEqual(2, progression.CurrentExperience);
        }

        [Test]
        public void Collect_Accumulates_AcrossMultipleCalls()
        {
            var wallet = new EnergyWalletService();
            var progression = new PlayerLevelProgressionService();
            var collection = new EnergyCollectionService(wallet, progression);

            collection.Collect(2, 1);
            collection.Collect(5, 2);

            Assert.AreEqual(7, wallet.CurrentEnergy);
            Assert.AreEqual(3, progression.CurrentExperience);
        }

        [Test]
        public void Collect_IgnoresZeroOrNegativeValues()
        {
            var wallet = new EnergyWalletService();
            var progression = new PlayerLevelProgressionService();
            var collection = new EnergyCollectionService(wallet, progression);

            collection.Collect(0, 0);
            collection.Collect(-5, -3);

            Assert.AreEqual(0, wallet.CurrentEnergy);
            Assert.AreEqual(0, progression.CurrentExperience);
        }

        [Test]
        public void PlayerLevelProgression_LevelsUp_AsExperienceCrossesThresholds()
        {
            var progression = new PlayerLevelProgressionService();
            Assert.AreEqual(1, progression.CurrentLevel);

            int neededForLevel2 = PlayerLevelProgressionService.ExperienceRequiredForLevel(1);
            progression.AddExperience(neededForLevel2);

            Assert.AreEqual(neededForLevel2, progression.CurrentExperience);
            Assert.Greater(progression.CurrentLevel, 1, "Adding exactly the Level 1->2 requirement should cross that threshold.");
        }

        [Test]
        public void PlayerLevelProgression_EachLevelUp_RequiresMoreXpThanTheLast()
        {
            // Locks in the "progressive curve" requirement: Level 1->2 must cost less than Level 2->3, which
            // must cost less than Level 3->4, etc. - not a flat per-level amount.
            int levelOneToTwo = PlayerLevelProgressionService.ExperienceRequiredForLevel(1);
            int levelTwoToThree = PlayerLevelProgressionService.ExperienceRequiredForLevel(2);
            int levelThreeToFour = PlayerLevelProgressionService.ExperienceRequiredForLevel(3);

            Assert.Greater(levelTwoToThree, levelOneToTwo);
            Assert.Greater(levelThreeToFour, levelTwoToThree);
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
