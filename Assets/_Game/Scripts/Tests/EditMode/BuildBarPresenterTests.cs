using System.Collections.Generic;
using System.Reflection;
using AlienDefense.Building;
using AlienDefense.Economy;
using AlienDefense.Towers;
using AlienDefense.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace AlienDefense.Tests.EditMode
{
    public class BuildBarPresenterTests
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

        private TowerDefinition CreateTowerDefinition(int buildCost)
        {
            var definition = CreateSilently(() => ScriptableObject.CreateInstance<TowerDefinition>());
            _scriptableObjects.Add(definition);
            SetPrivateField(definition, "_buildCost", buildCost);
            return definition;
        }

        private TowerBuildButtonView CreateButtonView()
        {
            var go = new GameObject("TestButton", typeof(RectTransform), typeof(Image), typeof(Button));
            _spawnedObjects.Add(go);
            var view = go.AddComponent<TowerBuildButtonView>();
            SetPrivateField(view, "_button", go.GetComponent<Button>());

            var costTextObject = new GameObject("CostText", typeof(RectTransform));
            costTextObject.transform.SetParent(go.transform);
            _spawnedObjects.Add(costTextObject);
            var costText = costTextObject.AddComponent<TMPro.TextMeshProUGUI>();
            SetPrivateField(view, "_costText", costText);

            var selectedIndicator = new GameObject("SelectedIndicator");
            selectedIndicator.transform.SetParent(go.transform);
            selectedIndicator.SetActive(false);
            _spawnedObjects.Add(selectedIndicator);
            SetPrivateField(view, "_selectedIndicator", selectedIndicator);

            return view;
        }

        private (BuildBarPresenter presenter, TowerBuildButtonView[] views, TowerDefinition[] definitions, Button cancelButton) CreatePresenter(params int[] costs)
        {
            var definitions = new TowerDefinition[costs.Length];
            var views = new TowerBuildButtonView[costs.Length];
            for (int i = 0; i < costs.Length; i++)
            {
                definitions[i] = CreateTowerDefinition(costs[i]);
                views[i] = CreateButtonView();
            }

            var cancelObject = new GameObject("CancelButton", typeof(RectTransform), typeof(Image), typeof(Button));
            _spawnedObjects.Add(cancelObject);
            var cancelButton = cancelObject.GetComponent<Button>();

            var presenterObject = new GameObject("TestPresenter");
            _spawnedObjects.Add(presenterObject);
            var presenter = presenterObject.AddComponent<BuildBarPresenter>();
            SetPrivateField(presenter, "_towerDefinitions", definitions);
            SetPrivateField(presenter, "_buttonViews", views);
            SetPrivateField(presenter, "_cancelButton", cancelButton);

            return (presenter, views, definitions, cancelButton);
        }

        [Test]
        public void Initialize_SetsCostOnEachButton()
        {
            (BuildBarPresenter presenter, TowerBuildButtonView[] views, TowerDefinition[] definitions, _) = CreatePresenter(75, 100, 150);
            var economy = new EconomyService(1000);
            var selection = new BuildSelectionService();

            presenter.Initialize(economy, selection);

            var costField = typeof(TowerBuildButtonView).GetField("_costText", BindingFlags.NonPublic | BindingFlags.Instance);
            var costText = (TMPro.TMP_Text)costField.GetValue(views[1]);
            Assert.AreEqual("100", costText.text);
        }

        [Test]
        public void Initialize_SetsAffordableState_BasedOnResource()
        {
            (BuildBarPresenter presenter, TowerBuildButtonView[] views, TowerDefinition[] definitions, _) = CreatePresenter(75, 200);
            var economy = new EconomyService(100);
            var selection = new BuildSelectionService();

            presenter.Initialize(economy, selection);

            var buttonField = typeof(TowerBuildButtonView).GetField("_button", BindingFlags.NonPublic | BindingFlags.Instance);
            var affordableButton = (Button)buttonField.GetValue(views[0]);
            var unaffordableButton = (Button)buttonField.GetValue(views[1]);

            Assert.IsTrue(affordableButton.interactable);
            Assert.IsFalse(unaffordableButton.interactable);
        }

        [Test]
        public void ResourceChanged_RefreshesAffordability()
        {
            (BuildBarPresenter presenter, TowerBuildButtonView[] views, TowerDefinition[] definitions, _) = CreatePresenter(150);
            var economy = new EconomyService(100);
            var selection = new BuildSelectionService();
            presenter.Initialize(economy, selection);

            var buttonField = typeof(TowerBuildButtonView).GetField("_button", BindingFlags.NonPublic | BindingFlags.Instance);
            var button = (Button)buttonField.GetValue(views[0]);
            Assert.IsFalse(button.interactable);

            economy.Add(100);

            Assert.IsTrue(button.interactable);
        }

        [Test]
        public void ButtonClicked_SelectsCorrespondingTowerDefinition_ViaSelectionService_NotEconomy()
        {
            (BuildBarPresenter presenter, TowerBuildButtonView[] views, TowerDefinition[] definitions, _) = CreatePresenter(75, 100);
            var economy = new EconomyService(1000);
            var selection = new BuildSelectionService();
            presenter.Initialize(economy, selection);

            int startingResource = economy.CurrentResource;
            InvokePrivateMethod(views[1], "HandleClicked");

            Assert.AreEqual(definitions[1], selection.SelectedTowerDefinition);
            Assert.AreEqual(startingResource, economy.CurrentResource);
        }

        [Test]
        public void ButtonClicked_UpdatesSelectedVisualOnAllButtons()
        {
            (BuildBarPresenter presenter, TowerBuildButtonView[] views, TowerDefinition[] definitions, _) = CreatePresenter(75, 100);
            var economy = new EconomyService(1000);
            var selection = new BuildSelectionService();
            presenter.Initialize(economy, selection);

            InvokePrivateMethod(views[0], "HandleClicked");

            var indicatorField = typeof(TowerBuildButtonView).GetField("_selectedIndicator", BindingFlags.NonPublic | BindingFlags.Instance);
            var selectedIndicator = (GameObject)indicatorField.GetValue(views[0]);
            var otherIndicator = (GameObject)indicatorField.GetValue(views[1]);

            Assert.IsTrue(selectedIndicator.activeSelf);
            Assert.IsFalse(otherIndicator.activeSelf);
        }

        [Test]
        public void CancelClicked_ClearsSelection()
        {
            (BuildBarPresenter presenter, TowerBuildButtonView[] views, TowerDefinition[] definitions, Button cancelButton) = CreatePresenter(75);
            var economy = new EconomyService(1000);
            var selection = new BuildSelectionService();
            presenter.Initialize(economy, selection);
            selection.SelectTower(definitions[0]);

            cancelButton.onClick.Invoke();

            Assert.IsNull(selection.SelectedTowerDefinition);
        }
    }
}
