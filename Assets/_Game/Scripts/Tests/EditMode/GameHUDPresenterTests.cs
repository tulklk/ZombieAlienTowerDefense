using System.Collections.Generic;
using System.Reflection;
using AlienDefense.Base;
using AlienDefense.Core;
using AlienDefense.Economy;
using AlienDefense.UI;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AlienDefense.Tests.EditMode
{
    public class GameHUDPresenterTests
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

        private static object GetPrivateField(object target, string fieldName)
        {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(field, $"Field '{fieldName}' not found on {target.GetType().Name}.");
            return field.GetValue(target);
        }

        private static void InvokePrivateMethod(object target, string methodName)
        {
            MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(method, $"Method '{methodName}' not found on {target.GetType().Name}.");
            method.Invoke(target, null);
        }

        private GameHUDView CreateView()
        {
            var go = new GameObject("TestGameHUDView");
            _spawnedObjects.Add(go);
            var view = go.AddComponent<GameHUDView>();

            var resourceText = new GameObject("ResourceText").AddComponent<TextMeshProUGUI>();
            resourceText.transform.SetParent(go.transform);
            _spawnedObjects.Add(resourceText.gameObject);
            SetPrivateField(view, "_resourceText", resourceText);

            var energyText = new GameObject("EnergyText").AddComponent<TextMeshProUGUI>();
            energyText.transform.SetParent(go.transform);
            _spawnedObjects.Add(energyText.gameObject);
            SetPrivateField(view, "_energyText", energyText);

            var baseHealthText = new GameObject("BaseHealthText").AddComponent<TextMeshProUGUI>();
            baseHealthText.transform.SetParent(go.transform);
            _spawnedObjects.Add(baseHealthText.gameObject);
            SetPrivateField(view, "_baseHealthText", baseHealthText);

            var fillImage = new GameObject("Fill", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            fillImage.transform.SetParent(go.transform);
            _spawnedObjects.Add(fillImage.gameObject);
            SetPrivateField(view, "_baseHealthFillImage", fillImage);

            var speedText = new GameObject("SpeedText").AddComponent<TextMeshProUGUI>();
            speedText.transform.SetParent(go.transform);
            _spawnedObjects.Add(speedText.gameObject);
            SetPrivateField(view, "_speedText", speedText);

            var speedButton = new GameObject("SpeedButton", typeof(RectTransform), typeof(Image), typeof(Button)).GetComponent<Button>();
            speedButton.transform.SetParent(go.transform);
            _spawnedObjects.Add(speedButton.gameObject);
            SetPrivateField(view, "_speedButton", speedButton);

            var pauseButton = new GameObject("PauseButton", typeof(RectTransform), typeof(Image), typeof(Button)).GetComponent<Button>();
            pauseButton.transform.SetParent(go.transform);
            _spawnedObjects.Add(pauseButton.gameObject);
            SetPrivateField(view, "_pauseButton", pauseButton);

            return view;
        }

        private GameHUDPresenter CreatePresenter(GameHUDView view)
        {
            var go = new GameObject("TestGameHUDPresenter");
            _spawnedObjects.Add(go);
            var presenter = go.AddComponent<GameHUDPresenter>();
            SetPrivateField(presenter, "_view", view);
            return presenter;
        }

        [Test]
        public void Initialize_PushesCurrentValuesToView()
        {
            GameHUDView view = CreateView();
            GameHUDPresenter presenter = CreatePresenter(view);
            var economy = new EconomyService(250);
            var energyWallet = new EnergyWalletService(5);
            var baseHealth = new BaseHealthService(20);
            var gameSpeed = new GameSpeedController(new FakeTimeScaleTarget());

            presenter.Initialize(economy, energyWallet, baseHealth, gameSpeed, new GameFlowController());

            var resourceText = (TMP_Text)GetPrivateField(view, "_resourceText");
            var energyText = (TMP_Text)GetPrivateField(view, "_energyText");
            var baseHealthText = (TMP_Text)GetPrivateField(view, "_baseHealthText");
            var speedText = (TMP_Text)GetPrivateField(view, "_speedText");
            Assert.AreEqual("250", resourceText.text);
            Assert.AreEqual("5/10", energyText.text);
            Assert.AreEqual("20", baseHealthText.text);
            Assert.AreEqual("x1", speedText.text);
        }

        [Test]
        public void ResourceChanged_UpdatesView()
        {
            GameHUDView view = CreateView();
            GameHUDPresenter presenter = CreatePresenter(view);
            var economy = new EconomyService(250);
            presenter.Initialize(economy, new EnergyWalletService(), new BaseHealthService(20), new GameSpeedController(new FakeTimeScaleTarget()), new GameFlowController());

            economy.Add(50);

            var resourceText = (TMP_Text)GetPrivateField(view, "_resourceText");
            Assert.AreEqual("300", resourceText.text);
        }

        [Test]
        public void EnergyChanged_UpdatesView()
        {
            GameHUDView view = CreateView();
            GameHUDPresenter presenter = CreatePresenter(view);
            var energyWallet = new EnergyWalletService();
            presenter.Initialize(new EconomyService(0), energyWallet, new BaseHealthService(20), new GameSpeedController(new FakeTimeScaleTarget()), new GameFlowController());

            energyWallet.Add(3);

            var energyText = (TMP_Text)GetPrivateField(view, "_energyText");
            Assert.AreEqual("3/10", energyText.text);
        }

        [Test]
        public void BaseHealthChanged_UpdatesView()
        {
            GameHUDView view = CreateView();
            GameHUDPresenter presenter = CreatePresenter(view);
            var baseHealth = new BaseHealthService(20);
            presenter.Initialize(new EconomyService(250), new EnergyWalletService(), baseHealth, new GameSpeedController(new FakeTimeScaleTarget()), new GameFlowController());

            baseHealth.TakeDamage(5);

            var baseHealthText = (TMP_Text)GetPrivateField(view, "_baseHealthText");
            Assert.AreEqual("15", baseHealthText.text);
        }

        [Test]
        public void SpeedButtonClicked_TogglesBetweenX1AndX2()
        {
            GameHUDView view = CreateView();
            GameHUDPresenter presenter = CreatePresenter(view);
            var gameSpeed = new GameSpeedController(new FakeTimeScaleTarget());
            presenter.Initialize(new EconomyService(0), new EnergyWalletService(), new BaseHealthService(20), gameSpeed, new GameFlowController());

            InvokePrivateMethod(presenter, "HandleSpeedButtonClicked");
            Assert.AreEqual(2, gameSpeed.CurrentSpeed);

            InvokePrivateMethod(presenter, "HandleSpeedButtonClicked");
            Assert.AreEqual(1, gameSpeed.CurrentSpeed);
        }

        [Test]
        public void PauseButtonClicked_PausesGameFlowAndGameSpeed()
        {
            GameHUDView view = CreateView();
            GameHUDPresenter presenter = CreatePresenter(view);
            var gameSpeed = new GameSpeedController(new FakeTimeScaleTarget());
            var gameFlow = new GameFlowController();
            gameFlow.BeginPreparingWave();
            gameFlow.BeginPlayingWave();
            presenter.Initialize(new EconomyService(0), new EnergyWalletService(), new BaseHealthService(20), gameSpeed, gameFlow);

            InvokePrivateMethod(presenter, "HandlePauseButtonClicked");

            Assert.AreEqual(GameState.Paused, gameFlow.CurrentState);
            Assert.IsTrue(gameSpeed.IsPaused);
        }

        private sealed class FakeTimeScaleTarget : ITimeScaleTarget
        {
            public float TimeScale { get; set; } = 1f;
        }
    }
}
