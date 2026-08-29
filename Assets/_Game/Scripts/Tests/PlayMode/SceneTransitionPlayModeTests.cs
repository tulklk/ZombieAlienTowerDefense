using System.Collections;
using System.Reflection;
using AlienDefense.Core;
using AlienDefense.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AlienDefense.Tests.PlayMode
{
    /// <summary>Exercises failure/rejection paths with nonexistent scene names. Successful Single-scene transitions
    /// that replace the test host scene are not automated here; see the manual Bootstrap flow checklist.</summary>
    public class SceneTransitionPlayModeTests
    {
        private GameObject _serviceObject;
        private GameObject _overlayTemplateObject;

        [TearDown]
        public void TearDown()
        {
            if (_serviceObject != null)
            {
                Object.Destroy(_serviceObject);
            }

            if (_overlayTemplateObject != null)
            {
                Object.Destroy(_overlayTemplateObject);
            }
        }

        private SceneTransitionService CreateService()
        {
            _serviceObject = new GameObject("TestSceneTransitionService");
            return _serviceObject.AddComponent<SceneTransitionService>();
        }

        [UnityTest]
        public IEnumerator TryLoadScene_WithMissingScene_FiresFailedAndResetsState()
        {
            SceneTransitionService service = CreateService();
            string startedName = null;
            string failedName = null;
            bool completedFired = false;
            service.SceneLoadStarted += name => startedName = name;
            service.SceneLoadFailed += (name, reason) => failedName = name;
            service.SceneLoadCompleted += _ => completedFired = true;

            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex(".*"));
            bool accepted = service.TryLoadScene("__NonExistentSceneForPhase12Tests__");

            Assert.IsTrue(accepted);
            yield return null;

            Assert.AreEqual("__NonExistentSceneForPhase12Tests__", startedName);
            Assert.AreEqual("__NonExistentSceneForPhase12Tests__", failedName);
            Assert.IsFalse(completedFired);
            Assert.IsFalse(service.IsTransitioning);
            Assert.AreEqual(SceneTransitionState.Failed, service.State);
        }

        [UnityTest]
        public IEnumerator TryLoadScene_WithEmptyName_IsRejectedWithoutStartingTransition()
        {
            SceneTransitionService service = CreateService();
            bool startedFired = false;
            service.SceneLoadStarted += _ => startedFired = true;

            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("empty name"));
            bool accepted = service.TryLoadScene("");

            Assert.IsFalse(accepted);
            Assert.IsFalse(startedFired);
            Assert.IsFalse(service.IsTransitioning);
            yield return null;
        }

        [UnityTest]
        public IEnumerator TryLoadScene_ResetsTimeScale_BeforeFiringStarted()
        {
            SceneTransitionService service = CreateService();
            Time.timeScale = 0.5f;

            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex(".*"));
            service.TryLoadScene("__NonExistentSceneForPhase12Tests__");

            Assert.AreEqual(1f, Time.timeScale);
            yield return null;

            Time.timeScale = 1f;
        }

        [UnityTest]
        public IEnumerator TryLoadScene_DestroysLoadingOverlayOnFailure()
        {
            SceneTransitionService service = CreateService();
            _overlayTemplateObject = new GameObject("TestLoadingOverlayTemplate");
            var overlayTemplate = _overlayTemplateObject.AddComponent<LoadingOverlayView>();
            AssignLoadingOverlayPrefab(service, overlayTemplate);

            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex(".*"));
            bool accepted = service.TryLoadScene("__NonExistentSceneForPhase12Tests__");

            Assert.IsTrue(accepted);
            yield return null;

            Assert.IsFalse(service.IsTransitioning);
            Assert.IsFalse(service.HasActiveLoadingOverlay, "Overlay instance must be destroyed after a failed transition.");

            LoadingOverlayView[] remainingOverlays = Object.FindObjectsByType<LoadingOverlayView>(FindObjectsSortMode.None);
            Assert.AreEqual(1, remainingOverlays.Length, "Only the template overlay should remain; spawned instance must be destroyed.");
            Assert.AreSame(overlayTemplate, remainingOverlays[0]);
        }

        [UnityTest]
        public IEnumerator TryLoadScene_DestroysOrphanLoadingOverlayOnFailure()
        {
            SceneTransitionService service = CreateService();

            var prefabSourceObject = new GameObject("LoadingOverlayPrefabSource");
            var prefabSource = prefabSourceObject.AddComponent<LoadingOverlayView>();
            AssignLoadingOverlayPrefab(service, prefabSource);

            _overlayTemplateObject = new GameObject("LoadingOverlay");
            _overlayTemplateObject.AddComponent<LoadingOverlayView>();

            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex(".*"));
            service.TryLoadScene("__NonExistentSceneForPhase12Tests__");

            yield return null;

            LoadingOverlayView[] remainingOverlays = Object.FindObjectsByType<LoadingOverlayView>(FindObjectsSortMode.None);
            Assert.AreEqual(1, remainingOverlays.Length, "Only the prefab source should remain.");
            Assert.AreSame(prefabSource, remainingOverlays[0]);

            Object.Destroy(prefabSourceObject);
        }

        [UnityTest]
        public IEnumerator TryLoadScene_DestroysOrphanLoadingCanvasOnFailure()
        {
            SceneTransitionService service = CreateService();

            var prefabSourceObject = new GameObject("LoadingOverlayPrefabSource");
            var prefabSource = prefabSourceObject.AddComponent<LoadingOverlayView>();
            AssignLoadingOverlayPrefab(service, prefabSource);

            _overlayTemplateObject = new GameObject("LoadingCanvas");
            _overlayTemplateObject.AddComponent<LoadingOverlayView>();

            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex(".*"));
            service.TryLoadScene("__NonExistentSceneForPhase12Tests__");

            yield return null;

            LoadingOverlayView[] remainingOverlays = Object.FindObjectsByType<LoadingOverlayView>(FindObjectsSortMode.None);
            Assert.AreEqual(1, remainingOverlays.Length, "Only the prefab source should remain.");
            Assert.AreSame(prefabSource, remainingOverlays[0]);

            Object.Destroy(prefabSourceObject);
        }

        [UnityTest]
        public IEnumerator TryLoadScene_SpawnsOverlayAsChildOfService()
        {
            SceneTransitionService service = CreateService();
            _overlayTemplateObject = new GameObject("LoadingOverlayPrefabSource");
            var prefabSource = _overlayTemplateObject.AddComponent<LoadingOverlayView>();
            AssignLoadingOverlayPrefab(service, prefabSource);

            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex(".*"));
            service.TryLoadScene("__NonExistentSceneForPhase12Tests__");
            yield return null;

            Assert.IsFalse(service.HasActiveLoadingOverlay);
            Assert.AreEqual(0, service.transform.childCount, "Spawned overlay must be destroyed and not left parented to the service.");
        }

        [UnityTest]
        public IEnumerator TryLoadScene_RejectsInfrastructureSceneNames()
        {
            SceneTransitionService service = CreateService();

            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("infrastructure scene"));
            Assert.IsFalse(service.TryLoadScene(SceneNames.Bootstrap));
            Assert.IsFalse(service.IsTransitioning);
            yield return null;
        }

        private static void AssignLoadingOverlayPrefab(SceneTransitionService service, LoadingOverlayView prefab)
        {
            FieldInfo field = typeof(SceneTransitionService).GetField(
                "_loadingOverlayPrefab",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field);
            field.SetValue(service, prefab);
        }
    }
}
