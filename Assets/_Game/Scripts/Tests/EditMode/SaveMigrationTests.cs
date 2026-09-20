using AlienDefense.Save;
using NUnit.Framework;

namespace AlienDefense.Tests.EditMode
{
    public class SaveMigrationTests
    {
        [Test]
        public void Migrate_CurrentVersion_ReturnsUpToDate_Unchanged()
        {
            var data = new PlayerProfileSaveData { SaveVersion = SaveConstants.CurrentSaveVersion, ProfileId = "p1" };

            (SaveMigrationPipeline.MigrationOutcome outcome, PlayerProfileSaveData result) = SaveMigrationPipeline.Migrate(data);

            Assert.AreEqual(SaveMigrationPipeline.MigrationOutcome.UpToDate, outcome);
            Assert.AreEqual("p1", result.ProfileId);
        }

        [Test]
        public void Migrate_MissingVersion_TreatedAsZero_MigratesToCurrentVersion()
        {
            var data = new PlayerProfileSaveData { SaveVersion = 0, ProfileId = "legacy" };

            (SaveMigrationPipeline.MigrationOutcome outcome, PlayerProfileSaveData result) = SaveMigrationPipeline.Migrate(data);

            Assert.AreEqual(SaveMigrationPipeline.MigrationOutcome.Migrated, outcome);
            Assert.AreEqual(SaveConstants.CurrentSaveVersion, result.SaveVersion);
            Assert.AreEqual("legacy", result.ProfileId);
        }

        [Test]
        public void Migrate_FutureVersion_DoesNotModifyData_ReportsFutureVersion()
        {
            var data = new PlayerProfileSaveData { SaveVersion = SaveConstants.CurrentSaveVersion + 1, ProfileId = "from-the-future" };

            (SaveMigrationPipeline.MigrationOutcome outcome, PlayerProfileSaveData result) = SaveMigrationPipeline.Migrate(data);

            Assert.AreEqual(SaveMigrationPipeline.MigrationOutcome.FutureVersion, outcome);
            Assert.AreEqual(SaveConstants.CurrentSaveVersion + 1, result.SaveVersion, "A future-version save must never be silently downgraded/overwritten.");
        }

        [Test]
        public void Migrate_NullData_ReturnsUnrecoverable()
        {
            (SaveMigrationPipeline.MigrationOutcome outcome, PlayerProfileSaveData result) = SaveMigrationPipeline.Migrate(null);

            Assert.AreEqual(SaveMigrationPipeline.MigrationOutcome.Unrecoverable, outcome);
            Assert.IsNull(result);
        }

        [Test]
        public void Migrate_V1ToV2_FillsIdentityAndStatistics()
        {
            var data = new PlayerProfileSaveData
            {
                SaveVersion = 1,
                ProfileId = "abcdef1234567890",
                DisplayName = null,
                Statistics = null,
            };

            (SaveMigrationPipeline.MigrationOutcome outcome, PlayerProfileSaveData result) = SaveMigrationPipeline.Migrate(data);

            Assert.AreEqual(SaveMigrationPipeline.MigrationOutcome.Migrated, outcome);
            Assert.AreEqual(SaveConstants.CurrentSaveVersion, result.SaveVersion);
            Assert.IsNotNull(result.Statistics);
            Assert.AreEqual(0L, result.Statistics.TotalTowerDamage);
            Assert.IsFalse(string.IsNullOrWhiteSpace(result.DisplayName));
            Assert.IsTrue(result.DisplayName.StartsWith("UFO_Pilot_"));
        }

        [Test]
        public void Migrate_V2ToV3_EnsuresInventory()
        {
            var data = new PlayerProfileSaveData
            {
                SaveVersion = 2,
                ProfileId = "v2-profile",
                Inventory = null,
            };

            (SaveMigrationPipeline.MigrationOutcome outcome, PlayerProfileSaveData result) = SaveMigrationPipeline.Migrate(data);

            Assert.AreEqual(SaveMigrationPipeline.MigrationOutcome.Migrated, outcome);
            Assert.AreEqual(SaveConstants.CurrentSaveVersion, result.SaveVersion);
            Assert.IsNotNull(result.Inventory);
        }

        [Test]
        public void Migrate_V3ToV4_FillsBestRemainingHpPercentFromStars()
        {
            var data = new PlayerProfileSaveData
            {
                SaveVersion = 3,
                ProfileId = "v3-profile",
                LevelProgress = new System.Collections.Generic.List<LevelProgressSaveData>
                {
                    new LevelProgressSaveData
                    {
                        LevelId = "L2",
                        IsCompleted = true,
                        BestStars = 2,
                        BestRemainingHpPercent = 0,
                    },
                    new LevelProgressSaveData
                    {
                        LevelId = "L3",
                        IsCompleted = true,
                        BestStars = 1,
                        BestRemainingHpPercent = 0,
                    },
                    new LevelProgressSaveData
                    {
                        LevelId = "L4",
                        IsCompleted = true,
                        BestStars = 3,
                        BestRemainingHpPercent = 0,
                    },
                },
            };

            (SaveMigrationPipeline.MigrationOutcome outcome, PlayerProfileSaveData result) = SaveMigrationPipeline.Migrate(data);

            Assert.AreEqual(SaveMigrationPipeline.MigrationOutcome.Migrated, outcome);
            Assert.AreEqual(4, result.SaveVersion);
            Assert.AreEqual(50, result.LevelProgress[0].BestRemainingHpPercent);
            Assert.AreEqual(1, result.LevelProgress[1].BestRemainingHpPercent);
            Assert.AreEqual(100, result.LevelProgress[2].BestRemainingHpPercent);
        }
    }
}
