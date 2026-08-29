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
    }
}
