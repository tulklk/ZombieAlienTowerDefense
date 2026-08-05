using System.Collections.Generic;
using System.Reflection;
using AlienDefense.Core;
using AlienDefense.UI;
using NUnit.Framework;
using UnityEngine;

namespace AlienDefense.Tests.EditMode
{
    public class GameStateUIControllerTests
    {
        private readonly List<GameObject> _spawnedObjects = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject go in _spawnedObjects)
            {
                if (go != null)
                {
                    Object.DestroyImmediate(go);
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

        private GameObject CreatePanel(string name)
        {
            var panel = new GameObject(name);
            _spawnedObjects.Add(panel);
            panel.SetActive(false);
            return panel;
        }

        private GameStateUIController CreateController(
            GameObject pausePanel, GameObject victoryPanel, GameObject defeatPanel, CanvasGroup gameplayGroup)
        {
            var go = new GameObject("TestGameStateUIController");
            _spawnedObjects.Add(go);
            var controller = go.AddComponent<GameStateUIController>();
            SetPrivateField(controller, "_pausePanel", pausePanel);
            SetPrivateField(controller, "_victoryPanel", victoryPanel);
            SetPrivateField(controller, "_defeatPanel", defeatPanel);
            SetPrivateField(controller, "_gameplayInteractionGroup", gameplayGroup);
            return controller;
        }

        private CanvasGroup CreateGameplayGroup()
        {
            var go = new GameObject("GameplayInteractionGroup", typeof(CanvasGroup));
            _spawnedObjects.Add(go);
            return go.GetComponent<CanvasGroup>();
        }

        [Test]
        public void GameStateChanged_ToPaused_ShowsOnlyPausePanel()
        {
            GameObject pausePanel = CreatePanel("PausePanel");
            GameObject victoryPanel = CreatePanel("VictoryPanel");
            GameObject defeatPanel = CreatePanel("DefeatPanel");
            CanvasGroup group = CreateGameplayGroup();
            GameStateUIController controller = CreateController(pausePanel, victoryPanel, defeatPanel, group);
            var gameFlow = new GameFlowController();
            gameFlow.BeginPreparingWave();
            gameFlow.BeginPlayingWave();
            controller.Initialize(gameFlow, new GameSpeedController(new FakeTimeScaleTarget()), null);

            gameFlow.Pause();

            Assert.IsTrue(pausePanel.activeSelf);
            Assert.IsFalse(victoryPanel.activeSelf);
            Assert.IsFalse(defeatPanel.activeSelf);
        }

        [Test]
        public void GameStateChanged_ToVictory_ShowsOnlyVictoryPanel()
        {
            GameObject pausePanel = CreatePanel("PausePanel");
            GameObject victoryPanel = CreatePanel("VictoryPanel");
            GameObject defeatPanel = CreatePanel("DefeatPanel");
            CanvasGroup group = CreateGameplayGroup();
            GameStateUIController controller = CreateController(pausePanel, victoryPanel, defeatPanel, group);
            var gameFlow = new GameFlowController();
            controller.Initialize(gameFlow, new GameSpeedController(new FakeTimeScaleTarget()), null);

            gameFlow.ReportVictory();

            Assert.IsFalse(pausePanel.activeSelf);
            Assert.IsTrue(victoryPanel.activeSelf);
            Assert.IsFalse(defeatPanel.activeSelf);
        }

        [Test]
        public void GameplayInteractionGroup_IsInteractable_OnlyDuringPreparingOrPlayingWave()
        {
            GameObject pausePanel = CreatePanel("PausePanel");
            GameObject victoryPanel = CreatePanel("VictoryPanel");
            GameObject defeatPanel = CreatePanel("DefeatPanel");
            CanvasGroup group = CreateGameplayGroup();
            GameStateUIController controller = CreateController(pausePanel, victoryPanel, defeatPanel, group);
            var gameFlow = new GameFlowController();
            controller.Initialize(gameFlow, new GameSpeedController(new FakeTimeScaleTarget()), null);

            gameFlow.BeginPreparingWave();
            Assert.IsTrue(group.interactable);

            gameFlow.BeginPlayingWave();
            Assert.IsTrue(group.interactable);

            gameFlow.Pause();
            Assert.IsFalse(group.interactable);
        }

        [Test]
        public void ResumeClicked_WhilePaused_ResumesGameFlowAndGameSpeed()
        {
            GameObject pausePanel = CreatePanel("PausePanel");
            GameObject victoryPanel = CreatePanel("VictoryPanel");
            GameObject defeatPanel = CreatePanel("DefeatPanel");
            CanvasGroup group = CreateGameplayGroup();
            GameStateUIController controller = CreateController(pausePanel, victoryPanel, defeatPanel, group);
            var gameFlow = new GameFlowController();
            gameFlow.BeginPreparingWave();
            gameFlow.BeginPlayingWave();
            var gameSpeed = new GameSpeedController(new FakeTimeScaleTarget());
            gameSpeed.Pause();
            controller.Initialize(gameFlow, gameSpeed, null);
            gameFlow.Pause();

            InvokePrivateMethod(controller, "HandleResumeClicked");

            Assert.AreEqual(GameState.PlayingWave, gameFlow.CurrentState);
            Assert.IsFalse(gameSpeed.IsPaused);
        }

        [Test]
        public void RestartClicked_WithNoRestartServiceAssigned_DoesNotThrow()
        {
            GameObject pausePanel = CreatePanel("PausePanel");
            GameObject victoryPanel = CreatePanel("VictoryPanel");
            GameObject defeatPanel = CreatePanel("DefeatPanel");
            CanvasGroup group = CreateGameplayGroup();
            GameStateUIController controller = CreateController(pausePanel, victoryPanel, defeatPanel, group);
            controller.Initialize(new GameFlowController(), new GameSpeedController(new FakeTimeScaleTarget()), null);

            Assert.DoesNotThrow(() => InvokePrivateMethod(controller, "HandleRestartClicked"));
        }

        [Test]
        public void ResumeClicked_WhileNotPaused_IsIgnored()
        {
            GameObject pausePanel = CreatePanel("PausePanel");
            GameObject victoryPanel = CreatePanel("VictoryPanel");
            GameObject defeatPanel = CreatePanel("DefeatPanel");
            CanvasGroup group = CreateGameplayGroup();
            GameStateUIController controller = CreateController(pausePanel, victoryPanel, defeatPanel, group);
            var gameFlow = new GameFlowController();
            gameFlow.BeginPreparingWave();
            gameFlow.BeginPlayingWave();
            var gameSpeed = new GameSpeedController(new FakeTimeScaleTarget());
            controller.Initialize(gameFlow, gameSpeed, null);

            InvokePrivateMethod(controller, "HandleResumeClicked");

            Assert.AreEqual(GameState.PlayingWave, gameFlow.CurrentState);
        }

        private sealed class FakeTimeScaleTarget : ITimeScaleTarget
        {
            public float TimeScale { get; set; } = 1f;
        }
    }
}
