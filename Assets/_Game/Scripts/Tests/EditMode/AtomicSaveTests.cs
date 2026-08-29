using System.IO;
using AlienDefense.Save;
using NUnit.Framework;
using UnityEngine;

namespace AlienDefense.Tests.EditMode
{
    public class AtomicSaveTests
    {
        private string _testDirectory;

        [SetUp]
        public void SetUp()
        {
            _testDirectory = Path.Combine(Application.temporaryCachePath, "AtomicSaveTests_" + System.Guid.NewGuid().ToString("N"));
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
        public void WriteAtomic_CreatesMainFile_WithExactContent()
        {
            var repository = new SaveFileRepository(_testDirectory);

            SaveWriteResult result = repository.WriteAtomic("{\"value\":1}");

            Assert.IsTrue(result.Success);
            Assert.IsTrue(File.Exists(Path.Combine(_testDirectory, "profile.json")));
            Assert.AreEqual("{\"value\":1}", File.ReadAllText(Path.Combine(_testDirectory, "profile.json")));
        }

        [Test]
        public void WriteAtomic_DoesNotLeaveTempFileBehind()
        {
            var repository = new SaveFileRepository(_testDirectory);

            repository.WriteAtomic("{\"value\":1}");

            Assert.IsFalse(File.Exists(Path.Combine(_testDirectory, "profile.tmp")));
        }

        [Test]
        public void WriteAtomic_SecondWrite_BacksUpFirstWrite()
        {
            var repository = new SaveFileRepository(_testDirectory);

            repository.WriteAtomic("{\"value\":1}");
            repository.WriteAtomic("{\"value\":2}");

            string backupPath = Path.Combine(_testDirectory, "profile.backup.json");
            Assert.IsTrue(File.Exists(backupPath));
            Assert.AreEqual("{\"value\":1}", File.ReadAllText(backupPath));
            Assert.AreEqual("{\"value\":2}", File.ReadAllText(Path.Combine(_testDirectory, "profile.json")));
        }

        [Test]
        public void WriteAtomic_EmptyPayload_Fails_AndWritesNothing()
        {
            var repository = new SaveFileRepository(_testDirectory);

            SaveWriteResult result = repository.WriteAtomic("");

            Assert.IsFalse(result.Success);
            Assert.IsFalse(File.Exists(Path.Combine(_testDirectory, "profile.json")));
        }

        [Test]
        public void TryReadMain_NoFileYet_ReturnsFalse()
        {
            var repository = new SaveFileRepository(_testDirectory);

            bool found = repository.TryReadMain(out string json);

            Assert.IsFalse(found);
            Assert.IsNull(json);
        }

        [Test]
        public void TryReadMain_AfterWrite_ReturnsWrittenContent()
        {
            var repository = new SaveFileRepository(_testDirectory);
            repository.WriteAtomic("{\"value\":42}");

            bool found = repository.TryReadMain(out string json);

            Assert.IsTrue(found);
            Assert.AreEqual("{\"value\":42}", json);
        }

        [Test]
        public void RecoverInterruptedWrite_OrphanedTempWithNoMain_PromotesTempToMain()
        {
            var repository = new SaveFileRepository(_testDirectory);
            Directory.CreateDirectory(_testDirectory);
            File.WriteAllText(Path.Combine(_testDirectory, "profile.tmp"), "{\"value\":\"interrupted\"}");

            repository.RecoverInterruptedWrite();

            Assert.IsTrue(repository.TryReadMain(out string json));
            Assert.AreEqual("{\"value\":\"interrupted\"}", json);
            Assert.IsFalse(File.Exists(Path.Combine(_testDirectory, "profile.tmp")));
        }

        [Test]
        public void RecoverInterruptedWrite_MainAlreadyExists_LeavesItUntouched()
        {
            var repository = new SaveFileRepository(_testDirectory);
            repository.WriteAtomic("{\"value\":\"good\"}");
            Directory.CreateDirectory(_testDirectory);
            File.WriteAllText(Path.Combine(_testDirectory, "profile.tmp"), "{\"value\":\"stale\"}");

            repository.RecoverInterruptedWrite();

            repository.TryReadMain(out string json);
            Assert.AreEqual("{\"value\":\"good\"}", json);
        }
    }
}
