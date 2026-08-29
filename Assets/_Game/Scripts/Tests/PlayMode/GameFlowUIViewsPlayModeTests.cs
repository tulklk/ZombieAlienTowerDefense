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
        public IEnumerator PausePanelView_ResumeRestartAndMainMenuClicks_FireEvents()
        {
            var go = new GameObject("TestPausePanelView");
            go.SetActive(false);
            _spawnedObjects.Add(go);

            var view = go.AddComponent<PausePanelView>();
            Button resumeButton = CreateChildButton(go.transform, "ResumeButton");
            Button restartButton = CreateChildButton(go.transform, "RestartButton");
            Button mainMenuButton = CreateChildButton(go.transform, "MainMenuButton");
            SetPrivateField(view, "_resumeButton", resumeButton);
            SetPrivateField(view, "_restartButton", restartButton);
            SetPrivateField(view, "_mainMenuButton", mainMenuButton);

            go.SetActive(true);
            yield return null;

            bool resumeClicked = false;
            bool restartClicked = false;
            bool mainMenuClicked = false;
            view.ResumeClicked += () => resumeClicked = true;
            view.RestartClicked += () => restartClicked = true;
            view.MainMenuClicked += () => mainMenuClicked = true;

            resumeButton.onClick.Invoke();
            restartButton.onClick.Invoke();
            mainMenuButton.onClick.Invoke();

            Assert.IsTrue(resumeClicked, "ResumeButton click should fire ResumeClicked.");
            Assert.IsTrue(restartClicked, "RestartButton click should fire RestartClicked.");
            Assert.IsTrue(mainMenuClicked, "MainMenuButton click should fire MainMenuClicked.");
        }

        [UnityTest]
        public IEnumerator GameResultView_RestartLevelSelectionAndNextLevelClicks_FireEvents()
        {
            var go = new GameObject("TestGameResultView");
            go.SetActive(false);
            _spawnedObjects.Add(go);

            var view = go.AddComponent<GameResultView>();
            Button restartButton = CreateChildButton(go.transform, "RestartButton");
            Button levelSelectionButton = CreateChildButton(go.transform, "LevelSelectionButton");
            Button nextLevelButton = CreateChildButton(go.transform, "NextLevelButton");
            SetPrivateField(view, "_restartButton", restartButton);
            SetPrivateField(view, "_levelSelectionButton", levelSelectionButton);
            SetPrivateField(view, "_nextLevelButton", nextLevelButton);

            go.SetActive(true);
            yield return null;

            bool restartClicked = false;
            bool levelSelectionClicked = false;
            bool nextLevelClicked = false;
            view.RestartClicked += () => restartClicked = true;
            view.LevelSelectionClicked += () => levelSelectionClicked = true;
            view.NextLevelClicked += () => nextLevelClicked = true;

            restartButton.onClick.Invoke();
            levelSelectionButton.onClick.Invoke();
            nextLevelButton.onClick.Invoke();

            Assert.IsTrue(restartClicked, "RestartButton click should fire RestartClicked.");
            Assert.IsTrue(levelSelectionClicked, "LevelSelectionButton click should fire LevelSelectionClicked.");
            Assert.IsTrue(nextLevelClicked, "NextLevelButton click should fire NextLevelClicked.");
        }
    }
}
