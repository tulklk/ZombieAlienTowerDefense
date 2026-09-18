using System.Collections.Generic;
using AlienDefense.Meta;
using AlienDefense.Save;
using NUnit.Framework;

namespace AlienDefense.Tests.EditMode
{
    public class PlayerPowerCalculatorTests
    {
        [Test]
        public void Compute_NullProfile_ReturnsZero()
        {
            Assert.AreEqual(0, PlayerPowerCalculator.Compute(null));
        }

        [Test]
        public void Compute_UnlockedTowers_ScalesWithCount()
        {
            var data = new PlayerProfileSaveData
            {
                SaveVersion = SaveConstants.CurrentSaveVersion,
                ProfileId = "power-test",
                UnlockedTowerIds = new List<string> { "a", "b", "c" },
                LevelProgress = new List<LevelProgressSaveData>(),
                Statistics = new PlayerStatisticsSaveData(),
            };
            ProfileIdentityUtility.EnsureDisplayIdentity(data);
            var profile = new PlayerProfileService(saveService: null, initialData: data);

            int power = PlayerPowerCalculator.Compute(profile, catalog: null);

            // 3 towers * 120 + completedLevels(0)*40
            Assert.AreEqual(360, power);
        }
    }
}
