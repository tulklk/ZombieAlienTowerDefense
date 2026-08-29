using System.Collections.Generic;
using System.Reflection;
using AlienDefense.Building;
using AlienDefense.Core;
using AlienDefense.Economy;
using AlienDefense.Towers;
using AlienDefense.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace AlienDefense.Tests.EditMode
{
    public class TowerDetailsPresenterTests
    {
        private readonly List<GameObject> _spawnedObjects = new List<GameObject>();
        private readonly List<Object> _scriptableObjects = new List<Object>();

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

            foreach (Object asset in _scriptableObjects)
            {
                if (asset != null)
                {
                    Object.DestroyImmediate(asset);
                }
            }

            _scriptableObjects.Clear();
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

        private static T CreateSilently<T>(System.Func<T> factory)
        {
            bool previousLogEnabled = Debug.unityLogger.logEnabled;
            Debug.unityLogger.logEnabled = false;
            T result = factory();
            Debug.unityLogger.logEnabled = previousLogEnabled;
            return result;
        }

        private TowerController CreateInitializedTower(int upgradeCostLevel2)
        {
            var go = new GameObject("TestTower");
            _spawnedObjects.Add(go);

            var targeting = go.AddComponent<TowerTargeting>();
            var attack = go.AddComponent<TowerAttackController>();
            var visual = go.AddComponent<TowerVisual>();
            var controller = go.AddComponent<TowerController>();

            var firePointObject = new GameObject("FirePoint");
            firePointObject.transform.SetParent(go.transform);
            _spawnedObjects.Add(firePointObject);

            SetPrivateField(attack, "_firePoint", firePointObject.transform);
            SetPrivateField(controller, "_targeting", targeting);
            SetPrivateField(controller, "_attack", attack);
            SetPrivateField(controller, "_visual", visual);

            var definition = CreateSilently(() => ScriptableObject.CreateInstance<TowerDefinition>());
            _scriptableObjects.Add(definition);

            var level1 = new TowerLevelData();
            SetPrivateField(level1, "_upgradeCost", 0);
            SetPrivateField(level1, "_damage", 20f);
            SetPrivateField(level1, "_range", 4f);
            SetPrivateField(level1, "_attacksPerSecond", 1f);
            SetPrivateField(level1, "_turretRotationSpeed", 720f);

            var level2 = new TowerLevelData();
            SetPrivateField(level2, "_upgradeCost", upgradeCostLevel2);
            SetPrivateField(level2, "_damage", 32f);
            SetPrivateField(level2, "_range", 4.4f);
            SetPrivateField(level2, "_attacksPerSecond", 1.2f);
            SetPrivateField(level2, "_turretRotationSpeed", 720f);

            SetPrivateField(definition, "_levels", new[] { level1, level2 });
            SetPrivateField(definition, "_sellPercentage", 0.5f);

            var gameFlow = new GameFlowController();
            gameFlow.BeginPreparingWave();
            controller.Initialize(definition, null, null, null, gameFlow);
            controller.RegisterInvestment(75);

            return controller;
        }

        private TowerDetailsView CreateView()
        {
            var go = new GameObject("TestView");
            _spawnedObjects.Add(go);
            var view = go.AddComponent<TowerDetailsView>();

            var costText = new GameObject("UpgradeCostText").AddComponent<TMPro.TextMeshProUGUI>();
            costText.transform.SetParent(go.transform);
            _spawnedObjects.Add(costText.gameObject);
            SetPrivateField(view, "_upgradeCostText", costText);

            var sellText = new GameObject("SellValueText").AddComponent<TMPro.TextMeshProUGUI>();
            sellText.transform.SetParent(go.transform);
            _spawnedObjects.Add(sellText.gameObject);
            SetPrivateField(view, "_sellValueText", sellText);

            var upgradeButton = new GameObject("UpgradeButton", typeof(RectTransform), typeof(Image), typeof(Button)).GetComponent<Button>();
            upgradeButton.transform.SetParent(go.transform);
            _spawnedObjects.Add(upgradeButton.gameObject);
            SetPrivateField(view, "_upgradeButton", upgradeButton);

            var rootObject = new GameObject("Root");
            rootObject.transform.SetParent(go.transform);
            rootObject.SetActive(false);
            _spawnedObjects.Add(rootObject);
            SetPrivateField(view, "_root", rootObject);

            return view;
        }

        private TowerDetailsPresenter CreatePresenter(TowerDetailsView view)
        {
            var go = new GameObject("TestPresenter");
            _spawnedObjects.Add(go);
            var presenter = go.AddComponent<TowerDetailsPresenter>();
            SetPrivateField(presenter, "_view", view);
            return presenter;
        }

        [Test]
        public void Selecting_Tower_ShowsPanel()
        {
            TowerController tower = CreateInitializedTower(90);
            TowerDetailsView view = CreateView();
            TowerDetailsPresenter presenter = CreatePresenter(view);
            var selection = new TowerSelectionService();
            var economy = new EconomyService(200);
            presenter.Initialize(selection, economy, new TowerUpgradeService(economy, new GameFlowController()), null);

            selection.Select(tower);

            var rootField = typeof(TowerDetailsView).GetField("_root", BindingFlags.NonPublic | BindingFlags.Instance);
            var root = (GameObject)rootField.GetValue(view);
            Assert.IsTrue(root.activeSelf);
        }

        [Test]
        public void Deselecting_HidesPanel()
        {
            TowerController tower = CreateInitializedTower(90);
            TowerDetailsView view = CreateView();
            TowerDetailsPresenter presenter = CreatePresenter(view);
            var selection = new TowerSelectionService();
            var economy = new EconomyService(200);
            presenter.Initialize(selection, economy, new TowerUpgradeService(economy, new GameFlowController()), null);
            selection.Select(tower);

            selection.Clear();

            var rootField = typeof(TowerDetailsView).GetField("_root", BindingFlags.NonPublic | BindingFlags.Instance);
            var root = (GameObject)rootField.GetValue(view);
            Assert.IsFalse(root.activeSelf);
        }

        [Test]
        public void Refresh_ShowsUpgradeCostFromNextLevel()
        {
            TowerController tower = CreateInitializedTower(90);
            TowerDetailsView view = CreateView();
            TowerDetailsPresenter presenter = CreatePresenter(view);
            var selection = new TowerSelectionService();
            var economy = new EconomyService(200);
            presenter.Initialize(selection, economy, new TowerUpgradeService(economy, new GameFlowController()), null);

            selection.Select(tower);

            var costField = typeof(TowerDetailsView).GetField("_upgradeCostText", BindingFlags.NonPublic | BindingFlags.Instance);
            var costText = (TMPro.TMP_Text)costField.GetValue(view);
            Assert.AreEqual("90", costText.text);
        }

        [Test]
        public void UpgradeClicked_CallsUpgradeService_NotEconomyDirectly()
        {
            TowerController tower = CreateInitializedTower(90);
            TowerDetailsView view = CreateView();
            TowerDetailsPresenter presenter = CreatePresenter(view);
            var selection = new TowerSelectionService();
            var economy = new EconomyService(200);
            var gameFlow = new GameFlowController();
            gameFlow.BeginPreparingWave();
            var upgradeService = new TowerUpgradeService(economy, gameFlow);
            presenter.Initialize(selection, economy, upgradeService, null);
            selection.Select(tower);

            InvokePrivateMethod(presenter, "HandleUpgradeClicked");

            Assert.AreEqual(110, economy.CurrentResource);
            Assert.AreEqual(1, tower.CurrentLevelIndex);
        }

        [Test]
        public void CloseClicked_ClearsSelection()
        {
            TowerController tower = CreateInitializedTower(90);
            TowerDetailsView view = CreateView();
            TowerDetailsPresenter presenter = CreatePresenter(view);
            var selection = new TowerSelectionService();
            var economy = new EconomyService(200);
            presenter.Initialize(selection, economy, new TowerUpgradeService(economy, new GameFlowController()), null);
            selection.Select(tower);

            InvokePrivateMethod(presenter, "HandleCloseClicked");

            Assert.IsNull(selection.SelectedTower);
        }
    }
}
