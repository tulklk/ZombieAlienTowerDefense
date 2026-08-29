using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using AlienDefense.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace AlienDefense.Tests.PlayMode
{
    /// <summary>Covers MainMenuView's button-click wiring (Awake-time listener registration, only observable in
    /// Play Mode) and MainMenuPresenter.HandleExitClicked's Editor-safe no-op path. Does not exercise
    /// HandlePlayClicked end-to-end: that would trigger a real SceneTransitionService scene load, which is unsafe
    /// to automate (see SceneTransitionPlayModeTests's summary for why).</summary>
    public class MainMenuPlayModeTests
    {
        private readonly List<GameObject> _spawnedObjects = new List<GameObject>();

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
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(field, $"Field '{fieldName}' not found on {target.GetType().Name}.");
            field.SetValue(target, value);
        }

        private static void InvokePrivateMethod(object target, string methodName)
        {
            MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(method, $"Method '{methodName}' not found on {target.GetType().Name}.");
            method.Invoke(target, null);
        }

        private Button CreateChildButton(Transform parent, string name)
        {
            var button = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button)).GetComponent<Button>();
            button.transform.SetParent(parent, false);
            return button;
        }

        [UnityTest]
        public IEnumerator MainMenuView_PlayAndExitClicks_FireEvents_SettingsButtonIsDisabled()
        {
            var go = new GameObject("TestMainMenuView");
            go.SetActive(false);
            _spawnedObjects.Add(go);

            var view = go.AddComponent<MainMenuView>();
            Button playButton = CreateChildButton(go.transform, "PlayButton");
            Button settingsButton = CreateChildButton(go.transform, "SettingsButton");
            Button exitButton = CreateChildButton(go.transform, "ExitButton");
            SetPrivateField(view, "_playButton", playButton);
            SetPrivateField(view, "_settingsButton", settingsButton);
            SetPrivateField(view, "_exitButton", exitButton);

            go.SetActive(true);
            yield return null;

            bool playClicked = false;
            bool exitClicked = false;
            view.PlayClicked += () => playClicked = true;
            view.ExitClicked += () => exitClicked = true;

            playButton.onClick.Invoke();
            exitButton.onClick.Invoke();

            Assert.IsTrue(playClicked, "PlayButton click should fire PlayClicked.");
            Assert.IsTrue(exitClicked, "ExitButton click should fire ExitClicked.");
            Assert.IsFalse(settingsButton.interactable, "SettingsButton must stay disabled; Settings UI is Phase 17.");
        }

        [UnityTest]
        public IEnumerator MainMenuPresenter_HandleExitClicked_DoesNotThrow_InEditor()
        {
            var go = new GameObject("TestMainMenuPresenter");
            _spawnedObjects.Add(go);
            var presenter = go.AddComponent<MainMenuPresenter>();

            LogAssert.Expect(LogType.Log, new System.Text.RegularExpressions.Regex("Exit requested"));
            Assert.DoesNotThrow(() => InvokePrivateMethod(presenter, "HandleExitClicked"));

            yield return null;
        }
    }
}
