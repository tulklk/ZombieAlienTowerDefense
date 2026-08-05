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
    /// <summary>Verifies Awake()-time button listener registration on the Phase 9 HUD/panel views (only observable in Play Mode).</summary>
    public class GameFlowUIViewsPlayModeTests
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

        private Button CreateChildButton(Transform parent, string name)
        {
            var button = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button)).GetComponent<Button>();
            button.transform.SetParent(parent, false);
            return button;
        }

        [UnityTest]
        public IEnumerator GameHUDView_ButtonClicks_FireEvents()
        {
            var go = new GameObject("TestGameHUDView");
            go.SetActive(false);
            _spawnedObjects.Add(go);

            var view = go.AddComponent<GameHUDView>();
            Button speedButton = CreateChildButton(go.transform, "SpeedButton");
            Button pauseButton = CreateChildButton(go.transform, "PauseButton");
            SetPrivateField(view, "_speedButton", speedButton);
            SetPrivateField(view, "_pauseButton", pauseButton);

            go.SetActive(true);
            yield return null;

            bool speedClicked = false;
            bool pauseClicked = false;
            view.SpeedButtonClicked += () => speedClicked = true;
            view.PauseButtonClicked += () => pauseClicked = true;

            speedButton.onClick.Invoke();
            pauseButton.onClick.Invoke();

            Assert.IsTrue(speedClicked, "SpeedButton click should fire SpeedButtonClicked.");
            Assert.IsTrue(pauseClicked, "PauseButton click should fire PauseButtonClicked.");
        }

        [UnityTest]
        public IEnumerator PausePanelView_ResumeAndRestartClicks_FireEvents_QuitButtonIsDisabled()
        {
            var go = new GameObject("TestPausePanelView");
            go.SetActive(false);
            _spawnedObjects.Add(go);

            var view = go.AddComponent<PausePanelView>();
            Button resumeButton = CreateChildButton(go.transform, "ResumeButton");
            Button restartButton = CreateChildButton(go.transform, "RestartButton");
            Button quitButton = CreateChildButton(go.transform, "QuitButton");
            SetPrivateField(view, "_resumeButton", resumeButton);
            SetPrivateField(view, "_restartButton", restartButton);
            SetPrivateField(view, "_quitButton", quitButton);

            go.SetActive(true);
            yield return null;

            bool resumeClicked = false;
            bool restartClicked = false;
            view.ResumeClicked += () => resumeClicked = true;
            view.RestartClicked += () => restartClicked = true;

            resumeButton.onClick.Invoke();
            restartButton.onClick.Invoke();

            Assert.IsTrue(resumeClicked, "ResumeButton click should fire ResumeClicked.");
            Assert.IsTrue(restartClicked, "RestartButton click should fire RestartClicked.");
            Assert.IsFalse(quitButton.interactable, "QuitButton must stay disabled; no Main Menu exists yet.");
        }

        [UnityTest]
        public IEnumerator GameResultView_RestartClick_FiresEvent()
        {
            var go = new GameObject("TestGameResultView");
            go.SetActive(false);
            _spawnedObjects.Add(go);

            var view = go.AddComponent<GameResultView>();
            Button restartButton = CreateChildButton(go.transform, "RestartButton");
            SetPrivateField(view, "_restartButton", restartButton);

            go.SetActive(true);
            yield return null;

            bool restartClicked = false;
            view.RestartClicked += () => restartClicked = true;

            restartButton.onClick.Invoke();

            Assert.IsTrue(restartClicked, "RestartButton click should fire RestartClicked.");
        }
    }
}
