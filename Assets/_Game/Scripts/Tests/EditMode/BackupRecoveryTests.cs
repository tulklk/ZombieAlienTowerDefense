using System.IO;
using System.Text.RegularExpressions;
using AlienDefense.Save;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AlienDefense.Tests.EditMode
{
    public class BackupRecoveryTests
    {
        private string _testDirectory;

        [SetUp]
        public void SetUp()
        {
            _testDirectory = Path.Combine(Application.temporaryCachePath, "BackupRecoveryTests_" + System.Guid.NewGuid().ToString("N"));
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_testDirectory))
            {
                Directory.Delete(_testDirectory, recursive: true);
            }
        }

        private static PlayerProfileSaveData ValidProfile(string profileId)
        {
            return new PlayerProfileSaveData { SaveVersion = SaveConstants.CurrentSaveVersion, ProfileId = profileId, CreatedUtcTicks = 1, LastUpdatedUtcTicks = 1 };
        }

        [Test]
        public void LoadOrCreateDefault_MainValid_UsesMain_IgnoresBackup()
        {
            var repository = new SaveFileRepository(_testDirectory);
            var service = new SaveService(repository);
            repository.WriteAtomic(JsonUtility.ToJson(ValidProfile("main-profile")));

            PlayerProfileSaveData result = service.LoadOrCreateDefault(null, "level_01");

            Assert.AreEqual("main-profile", result.ProfileId);
        }

        [Test]
        public void LoadOrCreateDefault_MainCorrupt_BackupValid_RestoresFromBackup()
        {
            var repository = new SaveFileRepository(_testDirectory);
            var service = new SaveService(repository);

            repository.WriteAtomic(JsonUtility.ToJson(ValidProfile("first-good-profile")));
            repository.WriteAtomic(JsonUtility.ToJson(ValidProfile("second-good-profile")));
            // At this point: main = second-good-profile, backup = first-good-profile.
            repository.DebugCorruptMainFile();

            LogAssert.Expect(LogType.Error, new Regex("Deserialize failed"));
            PlayerProfileSaveData result = service.LoadOrCreateDefault(null, "level_01");

            Assert.AreEqual("first-good-profile", result.ProfileId);
        }

        [Test]
        public void LoadOrCreateDefault_MainCorrupt_BackupAlsoCorrupt_CreatesDefaultProfile_NoCrash()
        {
            var repository = new SaveFileRepository(_testDirectory);
            var service = new SaveService(repository);

            Directory.CreateDirectory(_testDirectory);
            File.WriteAllText(Path.Combine(_testDirectory, "profile.json"), "not valid json {{{");
            File.WriteAllText(Path.Combine(_testDirectory, "profile.backup.json"), "also not valid json {{{");

            LogAssert.Expect(LogType.Error, new Regex("Deserialize failed"));
            LogAssert.Expect(LogType.Error, new Regex("Deserialize failed"));
            PlayerProfileSaveData result = service.LoadOrCreateDefault(null, "level_01");

            Assert.IsNotNull(result);
            Assert.AreEqual(SaveConstants.CurrentSaveVersion, result.SaveVersion);
            Assert.AreEqual("level_01", result.HighestUnlockedLevelId);
        }

        [Test]
        public void LoadOrCreateDefault_NoFilesAtAll_CreatesDefaultProfile()
        {
            var repository = new SaveFileRepository(_testDirectory);
            var service = new SaveService(repository);

            PlayerProfileSaveData result = service.LoadOrCreateDefault(null, "level_01");

            Assert.IsNotNull(result);
            Assert.IsNotEmpty(result.ProfileId);
            Assert.AreEqual("level_01", result.HighestUnlockedLevelId);
        }

        [Test]
        public void LoadOrCreateDefault_MainCorruptBackupValid_AlsoRestoresMainFileOnDisk()
        {
            var repository = new SaveFileRepository(_testDirectory);
            var service = new SaveService(repository);
            // A second write is required so the first write becomes the backup (WriteAtomic only backs up an
            // *existing* main file at write time); corrupting main without ever writing twice would leave no
            // backup to recover from at all.
            repository.WriteAtomic(JsonUtility.ToJson(ValidProfile("only-good-profile")));
            repository.WriteAtomic(JsonUtility.ToJson(ValidProfile("only-good-profile")));
            repository.DebugCorruptMainFile();

            LogAssert.Expect(LogType.Error, new Regex("Deserialize failed"));
            service.LoadOrCreateDefault(null, "level_01");

            repository.TryReadMain(out string mainJsonAfterRecovery);
            PlayerProfileSaveData restoredMain = JsonUtility.FromJson<PlayerProfileSaveData>(mainJsonAfterRecovery);
            Assert.AreEqual("only-good-profile", restoredMain.ProfileId);
        }
    }
}
