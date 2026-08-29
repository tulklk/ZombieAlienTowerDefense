using System.IO;
using AlienDefense.Save;
using AlienDefense.Settings;
using NUnit.Framework;
using UnityEngine;

namespace AlienDefense.Tests.EditMode
{
    public class SettingsServiceTests
    {
        private string _testDirectory;
        private int _originalQualityLevel;
        private int _originalTargetFrameRate;

        [SetUp]
        public void SetUp()
        {
            _testDirectory = Path.Combine(Application.temporaryCachePath, "SettingsServiceTests_" + System.Guid.NewGuid().ToString("N"));
            _originalQualityLevel = QualitySettings.GetQualityLevel();
            _originalTargetFrameRate = Application.targetFrameRate;
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_testDirectory))
            {
                Directory.Delete(_testDirectory, recursive: true);
            }

            QualitySettings.SetQualityLevel(_originalQualityLevel, true);
            Application.targetFrameRate = _originalTargetFrameRate;
        }

        private PlayerProfileService CreateProfileService()
        {
            var repository = new SaveFileRepository(_testDirectory);
            var saveService = new SaveService(repository);
            PlayerProfileSaveData data = saveService.LoadOrCreateDefault(null, "level_01");
            return new PlayerProfileService(saveService, data);
        }

        [Test]
        public void Constructor_AppliesProfileSettings_ToEngine()
        {
            PlayerProfileService profileService = CreateProfileService();
            profileService.UpdateSettings(new GameSettings(1f, 1f, 1f, true, true, 1, 30));
            profileService.FlushPendingSave();

            var settingsService = new SettingsService(profileService);

            Assert.AreEqual(1, QualitySettings.GetQualityLevel());
            Assert.AreEqual(30, Application.targetFrameRate);
        }

        [Test]
        public void SetMasterVolume_ClampsAboveOne()
        {
            var settingsService = new SettingsService(CreateProfileService());

            settingsService.SetMasterVolume(5f);

            Assert.AreEqual(1f, settingsService.Current.MasterVolume);
        }

        [Test]
        public void SetMasterVolume_ClampsBelowZero()
        {
            var settingsService = new SettingsService(CreateProfileService());

            settingsService.SetMasterVolume(-2f);

            Assert.AreEqual(0f, settingsService.Current.MasterVolume);
        }

        [Test]
        public void SetTargetFrameRate_OnlyAllows30Or60()
        {
            var settingsService = new SettingsService(CreateProfileService());

            settingsService.SetTargetFrameRate(144);

            Assert.AreEqual(60, settingsService.Current.TargetFrameRate);
        }

        [Test]
        public void SetTargetFrameRate_30_IsAccepted()
        {
            var settingsService = new SettingsService(CreateProfileService());

            settingsService.SetTargetFrameRate(30);

            Assert.AreEqual(30, settingsService.Current.TargetFrameRate);
            Assert.AreEqual(30, Application.targetFrameRate);
        }

        [Test]
        public void SettingsChanged_FiresOnEveryChange_WithUpdatedValue()
        {
            var settingsService = new SettingsService(CreateProfileService());
            GameSettings? received = null;
            settingsService.SettingsChanged += settings => received = settings;

            settingsService.SetSfxVolume(0.25f);

            Assert.IsTrue(received.HasValue);
            Assert.AreEqual(0.25f, received.Value.SfxVolume, 0.001f);
        }

        [Test]
        public void ChangedSetting_PersistsThroughPlayerProfileService()
        {
            PlayerProfileService profileService = CreateProfileService();
            var settingsService = new SettingsService(profileService);

            settingsService.SetHapticsEnabled(false);
            profileService.FlushPendingSave();

            Assert.IsFalse(profileService.CurrentSettings.HapticsEnabled);
        }

        [Test]
        public void NoAudioServiceAttached_DoesNotThrow()
        {
            var settingsService = new SettingsService(CreateProfileService());

            Assert.DoesNotThrow(() => settingsService.SetMusicVolume(0.5f));
        }
    }
}
