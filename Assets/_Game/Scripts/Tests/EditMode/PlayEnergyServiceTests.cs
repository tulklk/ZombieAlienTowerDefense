using System;
using System.IO;
using AlienDefense.Economy;
using AlienDefense.Save;
using NUnit.Framework;
using UnityEngine;

namespace AlienDefense.Tests.EditMode
{
    /// <summary>Lobby energy: starts full, a start costs its price, it refills one point per interval from the
    /// clock (also across a reload), and a start without enough energy spends nothing.</summary>
    public class PlayEnergyServiceTests
    {
        private static readonly DateTime T0 = new DateTime(2026, 9, 21, 12, 0, 0, DateTimeKind.Utc);
        private string _testDirectory;

        [SetUp]
        public void SetUp()
        {
            _testDirectory = Path.Combine(Application.temporaryCachePath, "PlayEnergyServiceTests_" + Guid.NewGuid().ToString("N"));
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_testDirectory))
            {
                Directory.Delete(_testDirectory, recursive: true);
            }
        }

        [Test]
        public void NewProfile_StartsFull_WithNoCountdown()
        {
            PlayEnergyService energy = Create(CreateProfile());

            Assert.AreEqual(60, energy.GetCurrent(T0));
            Assert.AreEqual(TimeSpan.Zero, energy.GetTimeToNext(T0));
        }

        [Test]
        public void Spend_TakesTheCost_AndStartsTheCountdown()
        {
            PlayEnergyService energy = Create(CreateProfile());

            Assert.IsTrue(energy.TrySpendForLevel(T0));

            Assert.AreEqual(55, energy.GetCurrent(T0));
            Assert.AreEqual(TimeSpan.FromMinutes(8), energy.GetTimeToNext(T0));
            Assert.AreEqual(TimeSpan.FromMinutes(8) - TimeSpan.FromSeconds(164), energy.GetTimeToNext(T0.AddSeconds(164)));
        }

        [Test]
        public void Refill_OnePointPerInterval_UpToTheCap()
        {
            PlayEnergyService energy = Create(CreateProfile());
            energy.TrySpendForLevel(T0);

            Assert.AreEqual(56, energy.GetCurrent(T0.AddMinutes(8)));
            Assert.AreEqual(57, energy.GetCurrent(T0.AddMinutes(16).AddSeconds(30)));
            Assert.AreEqual(60, energy.GetCurrent(T0.AddHours(5)), "Never above the maximum.");
            Assert.AreEqual(TimeSpan.Zero, energy.GetTimeToNext(T0.AddHours(5)));
        }

        [Test]
        public void SpendWhileRefilling_KeepsTheProgressTowardTheNextPoint()
        {
            PlayEnergyService energy = Create(CreateProfile());
            energy.TrySpendForLevel(T0);                       // 55, next point at +8m

            energy.TrySpendForLevel(T0.AddMinutes(3));         // 50, still next point at +8m

            Assert.AreEqual(50, energy.GetCurrent(T0.AddMinutes(3)));
            Assert.AreEqual(TimeSpan.FromMinutes(5), energy.GetTimeToNext(T0.AddMinutes(3)));
        }

        [Test]
        public void NotEnough_SpendsNothing()
        {
            PlayerProfileService profile = CreateProfile();
            profile.SetPlayEnergyState(4, T0.Ticks);
            PlayEnergyService energy = Create(profile);

            Assert.IsFalse(energy.TrySpendForLevel(T0));
            Assert.AreEqual(4, energy.GetCurrent(T0));
        }

        [Test]
        public void Energy_SurvivesASaveReload()
        {
            Create(CreateProfile()).TrySpendForLevel(T0);

            PlayEnergyService reloaded = Create(CreateProfile());

            Assert.AreEqual(55, reloaded.GetCurrent(T0.AddMinutes(1)));
        }

        private static PlayEnergyService Create(PlayerProfileService profile)
        {
            return new PlayEnergyService(profile, max: 60, costPerLevel: 5, regenMinutes: 8f);
        }

        private PlayerProfileService CreateProfile()
        {
            var saveService = new SaveService(new SaveFileRepository(_testDirectory));
            PlayerProfileSaveData data = saveService.LoadOrCreateDefault(null, "level_01");
            return new PlayerProfileService(saveService, data);
        }
    }
}
