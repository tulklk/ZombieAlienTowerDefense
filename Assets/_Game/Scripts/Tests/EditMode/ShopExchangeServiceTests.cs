using System.IO;
using AlienDefense.Economy;
using AlienDefense.Save;
using NUnit.Framework;
using UnityEngine;

namespace AlienDefense.Tests.EditMode
{
    public class ShopExchangeServiceTests
    {
        private string _testDirectory;

        [SetUp]
        public void SetUp()
        {
            _testDirectory = Path.Combine(Application.temporaryCachePath, "ShopExchangeServiceTests_" + System.Guid.NewGuid().ToString("N"));
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
        public void TryExchange_SufficientGems_SpendsAndGrantsCoin()
        {
            PlayerProfileService profile = CreateProfileService(50);
            var service = new ShopExchangeService(profile);

            bool exchanged = service.TryExchange(0); // pack 0: 10 Gem -> 100 Coin

            Assert.IsTrue(exchanged);
            Assert.AreEqual(40, profile.Gems);
            Assert.AreEqual(100, profile.MetaCurrency);
        }

        [Test]
        public void TryExchange_InsufficientGems_Fails_NoStateChange()
        {
            PlayerProfileService profile = CreateProfileService(5);
            var service = new ShopExchangeService(profile);

            bool exchanged = service.TryExchange(0);

            Assert.IsFalse(exchanged);
            Assert.AreEqual(5, profile.Gems);
            Assert.AreEqual(0, profile.MetaCurrency);
        }

        [Test]
        public void TryExchange_InvalidPackIndex_Fails()
        {
            PlayerProfileService profile = CreateProfileService(1000);
            var service = new ShopExchangeService(profile);

            Assert.IsFalse(service.TryExchange(-1));
            Assert.IsFalse(service.TryExchange(99));
        }

        [Test]
        public void CanAfford_ReflectsCurrentGems()
        {
            PlayerProfileService profile = CreateProfileService(35);
            var service = new ShopExchangeService(profile);

            Assert.IsFalse(service.CanAfford(1)); // pack 1 costs 40
            Assert.IsTrue(service.CanAfford(0)); // pack 0 costs 10
        }
    }
}
