using System.Collections;
using System.IO;
using System.Reflection;
using AlienDefense.Core;
using AlienDefense.Save;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AlienDefense.Tests.PlayMode
{
    /// <summary>Covers ApplicationLifecycleController flushing a pending debounced save on
    /// OnApplicationPause/Focus-lost/Quit. Unity invokes these automatically only on real OS events, so the test
    /// calls the private message methods directly via reflection (the same technique this project already uses
    /// for other Unity message handlers).</summary>
    public class ApplicationLifecyclePlayModeTests
    {
        private readonly System.Collections.Generic.List<GameObject> _spawnedObjects = new System.Collections.Generic.List<GameObject>();
        private string _testDirectory;

        [SetUp]
        public void SetUp()
        {
            _testDirectory = Path.Combine(Application.temporaryCachePath, "AppLifecycleTests_" + System.Guid.NewGuid().ToString("N"));
        }

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject go in _spawnedObjects)
            {
                if (go != null)
                {
                    Object.Destroy(go);
                }
            }

            _spawnedObjects.Clear();

            if (Directory.Exists(_testDirectory))
            {
                Directory.Delete(_testDirectory, recursive: true);
            }
        }

        private static void InvokePrivateMethod(object target, string methodName, params object[] args)
        {
            MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(method, $"Method '{methodName}' not found on {target.GetType().Name}.");
            method.Invoke(target, args);
        }

        private (PlayerProfileService profileService, SaveFileRepository repository, ApplicationLifecycleController controller) CreateSetup()
        {
            var repository = new SaveFileRepository(_testDirectory);
            var saveService = new SaveService(repository);
            PlayerProfileSaveData data = saveService.LoadOrCreateDefault(null, "level_01");
            var profileService = new PlayerProfileService(saveService, data);

            var go = new GameObject("TestApplicationLifecycleController");
            _spawnedObjects.Add(go);
            var controller = go.AddComponent<ApplicationLifecycleController>();
            controller.Initialize(profileService);

            return (profileService, repository, controller);
        }

        [UnityTest]
        public IEnumerator OnApplicationPause_True_FlushesPendingSave()
        {
            (PlayerProfileService profileService, SaveFileRepository repository, ApplicationLifecycleController controller) = CreateSetup();
            profileService.UpdateSettings(new GameSettings(0.33f, 0.5f, 0.5f, true, true, 1, 60));

            repository.TryReadMain(out string beforePause);

            InvokePrivateMethod(controller, "OnApplicationPause", true);

            repository.TryReadMain(out string afterPause);
            PlayerProfileSaveData saved = JsonUtility.FromJson<PlayerProfileSaveData>(afterPause);
            Assert.AreEqual(0.33f, saved.Settings.MasterVolume, 0.001f);
            Assert.AreNotEqual(beforePause, afterPause, "Pausing must flush the pending debounced save.");
            yield return null;
        }

        [UnityTest]
        public IEnumerator OnApplicationPause_False_DoesNotFlush()
        {
            (PlayerProfileService profileService, SaveFileRepository repository, ApplicationLifecycleController controller) = CreateSetup();
            profileService.UpdateSettings(new GameSettings(0.77f, 0.5f, 0.5f, true, true, 1, 60));
            repository.TryReadMain(out string beforeJson);

            InvokePrivateMethod(controller, "OnApplicationPause", false);

            repository.TryReadMain(out string afterJson);
            Assert.AreEqual(beforeJson, afterJson, "Resuming (pauseStatus=false) must not trigger a flush.");
            yield return null;
        }

        [UnityTest]
        public IEnumerator OnApplicationFocus_Lost_FlushesPendingSave()
        {
            (PlayerProfileService profileService, SaveFileRepository repository, ApplicationLifecycleController controller) = CreateSetup();
            profileService.UpdateSettings(new GameSettings(0.55f, 0.5f, 0.5f, true, true, 1, 60));

            InvokePrivateMethod(controller, "OnApplicationFocus", false);

            repository.TryReadMain(out string json);
            PlayerProfileSaveData saved = JsonUtility.FromJson<PlayerProfileSaveData>(json);
            Assert.AreEqual(0.55f, saved.Settings.MasterVolume, 0.001f);
            yield return null;
        }

        [UnityTest]
        public IEnumerator OnApplicationQuit_FlushesPendingSave()
        {
            (PlayerProfileService profileService, SaveFileRepository repository, ApplicationLifecycleController controller) = CreateSetup();
            profileService.UpdateSettings(new GameSettings(0.11f, 0.5f, 0.5f, true, true, 1, 60));

            InvokePrivateMethod(controller, "OnApplicationQuit");

            repository.TryReadMain(out string json);
            PlayerProfileSaveData saved = JsonUtility.FromJson<PlayerProfileSaveData>(json);
            Assert.AreEqual(0.11f, saved.Settings.MasterVolume, 0.001f);
            yield return null;
        }

        [UnityTest]
        public IEnumerator NoInitialize_LifecycleCallbacks_DoNotThrow()
        {
            var go = new GameObject("UninitializedController");
            _spawnedObjects.Add(go);
            var controller = go.AddComponent<ApplicationLifecycleController>();

            Assert.DoesNotThrow(() => InvokePrivateMethod(controller, "OnApplicationPause", true));
            yield return null;
        }
    }
}
