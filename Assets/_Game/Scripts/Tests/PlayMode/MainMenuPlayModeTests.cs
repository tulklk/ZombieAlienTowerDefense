using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using AlienDefense.UI;
using AlienDefense.UI.MainMenu;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace AlienDefense.Tests.PlayMode
{
    /// <summary>Covers the MainMenu dumb views' button-click wiring (Awake-time listener registration, only
    /// observable in Play Mode) and MainMenuPresenter.HandleExitRequested's Editor-safe no-op path. Does not
    /// exercise Play end-to-end: that would trigger a real SceneTransitionService scene load, which is unsafe
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
        public IEnumerator PlayButtonView_Click_FiresClicked()
        {
            var go = new GameObject("TestPlayButtonView");
            go.SetActive(false);
            _spawnedObjects.Add(go);

            var view = go.AddComponent<PlayButtonView>();
            Button button = CreateChildButton(go.transform, "Button");
            SetPrivateField(view, "_button", button);

            go.SetActive(true);
            yield return null;

            bool clicked = false;
            view.Clicked += () => clicked = true;

            button.onClick.Invoke();

            Assert.IsTrue(clicked, "PlayButtonView's button click should fire Clicked.");
        }

        [UnityTest]
        public IEnumerator MainMenuLevelSelectionView_PreviousNextMoreClicks_FireEvents()
        {
            var go = new GameObject("TestLevelSelectionView");
            go.SetActive(false);
            _spawnedObjects.Add(go);

            var view = go.AddComponent<MainMenuLevelSelectionView>();
            Button previousButton = CreateChildButton(go.transform, "PreviousButton");
            Button nextButton = CreateChildButton(go.transform, "NextButton");
            Button moreButton = CreateChildButton(go.transform, "MoreButton");
            SetPrivateField(view, "_previousButton", previousButton);
            SetPrivateField(view, "_nextButton", nextButton);
            SetPrivateField(view, "_moreButton", moreButton);

            go.SetActive(true);
            yield return null;

            bool previousClicked = false;
            bool nextClicked = false;
            bool moreClicked = false;
            view.PreviousClicked += () => previousClicked = true;
            view.NextClicked += () => nextClicked = true;
            view.MoreClicked += () => moreClicked = true;

            previousButton.onClick.Invoke();
            nextButton.onClick.Invoke();
            moreButton.onClick.Invoke();

            Assert.IsTrue(previousClicked, "PreviousButton click should fire PreviousClicked.");
            Assert.IsTrue(nextClicked, "NextButton click should fire NextClicked.");
            Assert.IsTrue(moreClicked, "MoreButton click should fire MoreClicked.");
        }

        [UnityTest]
        public IEnumerator BottomNavTabView_Click_FiresClicked_AndDisabledPreventsInteraction()
        {
            var go = new GameObject("TestBottomNavTabView");
            go.SetActive(false);
            _spawnedObjects.Add(go);

            var view = go.AddComponent<BottomNavTabView>();
            Button button = CreateChildButton(go.transform, "Button");
            SetPrivateField(view, "_button", button);

            go.SetActive(true);
            yield return null;

            bool clicked = false;
            view.Clicked += () => clicked = true;
            button.onClick.Invoke();
            Assert.IsTrue(clicked, "Tab click should fire Clicked.");

            view.SetDisabled(true);
            Assert.IsFalse(button.interactable, "SetDisabled(true) must make the tab non-interactable.");
        }

        [UnityTest]
        public IEnumerator MainMenuPresenter_HandleExitRequested_DoesNotThrow_InEditor()
        {
            var go = new GameObject("TestMainMenuPresenter");
            _spawnedObjects.Add(go);
            var presenter = go.AddComponent<MainMenuPresenter>();

            LogAssert.Expect(LogType.Log, new System.Text.RegularExpressions.Regex("Exit requested"));
            Assert.DoesNotThrow(() => InvokePrivateMethod(presenter, "HandleExitRequested"));

            yield return null;
        }
    }
}
