using System.IO;
using AlienDefense.Progression;
using AlienDefense.Save;
using NUnit.Framework;
using UnityEngine;

namespace AlienDefense.Tests.EditMode
{
    public class VipServiceTests
    {
        private string _testDirectory;

        [SetUp]
        public void SetUp()
        {
            _testDirectory = Path.Combine(Application.temporaryCachePath, "VipServiceTests_" + System.Guid.NewGuid().ToString("N"));
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_testDirectory))
            {
                Directory.Delete(_testDirectory, recursive: true);
            }
        }

        private PlayerProfileService CreateProfileService(int startingGems)
        {
            var repository = new SaveFileRepository(_testDirectory);
            var saveService = new SaveService(repository);
            PlayerProfileSaveData data = saveService.LoadOrCreateDefault(null, "level_01");
            data.Gems = startingGems;
            return new PlayerProfileService(saveService, data);
        }

        [Test]
        public void TryPurchase_SufficientGems_SpendsAndRaisesTier()
        {
            PlayerProfileService profile = CreateProfileService(150);
            var service = new VipService(profile);

            bool purchased = service.TryPurchase(1);

            Assert.IsTrue(purchased);
            Assert.AreEqual(1, service.CurrentTier);
            Assert.AreEqual(50, profile.Gems, "Tier 1 costs 100 Gems.");
        }

        [Test]
        public void TryPurchase_InsufficientGems_Fails_NoStateChange()
        {
            PlayerProfileService profile = CreateProfileService(50);
            var service = new VipService(profile);

            bool purchased = service.TryPurchase(1);

            Assert.IsFalse(purchased);
            Assert.AreEqual(0, service.CurrentTier);
            Assert.AreEqual(50, profile.Gems);
        }

        [Test]
        public void TryPurchase_TierNotHigherThanCurrent_Fails()
        {
            PlayerProfileService profile = CreateProfileService(10_000);
            var service = new VipService(profile);
            Assert.IsTrue(service.TryPurchase(2));

            bool purchasedLowerTier = service.TryPurchase(1);

            Assert.IsFalse(purchasedLowerTier);
            Assert.AreEqual(2, service.CurrentTier, "Purchasing a lower/equal tier must not downgrade.");
        }

        [Test]
        public void CurrentCoinBonusMultiplier_ReflectsPurchasedTier()
        {
            PlayerProfileService profile = CreateProfileService(10_000);
            var service = new VipService(profile);

            Assert.AreEqual(0f, service.CurrentCoinBonusMultiplier);

            service.TryPurchase(2);

            Assert.AreEqual(0.25f, service.CurrentCoinBonusMultiplier);
        }
    }
}
