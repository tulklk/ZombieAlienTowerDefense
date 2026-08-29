using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using AlienDefense.Core;
using AlienDefense.Data;
using AlienDefense.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace AlienDefense.Tests.PlayMode
{
    /// <summary>Covers LevelSelectionPresenter's card building and lock-gating logic using a hand-built LevelCatalog
    /// with intentionally nonexistent scene names, so selecting an "unlocked" card never triggers a real scene load.</summary>
    public class LevelSelectionPlayModeTests
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
                    Object.Destroy(go);
                }
            }

            _spawnedObjects.Clear();

            foreach (Object asset in _scriptableObjects)
            {
                if (asset != null)
                {
                    Object.Destroy(asset);
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

        private static object GetPrivateField(object target, string fieldName)
        {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(field, $"Field '{fieldName}' not found on {target.GetType().Name}.");
            return field.GetValue(target);
        }

        private static T CreateSilently<T>(System.Func<T> factory)
        {
            bool previousLogEnabled = Debug.unityLogger.logEnabled;
            Debug.unityLogger.logEnabled = false;
            T result = factory();
            Debug.unityLogger.logEnabled = previousLogEnabled;
            return result;
        }

        private LevelDefinition CreateLevelDefinition(string levelId)
        {
            var definition = CreateSilently(() => ScriptableObject.CreateInstance<LevelDefinition>());
            _scriptableObjects.Add(definition);
            SetPrivateField(definition, "_levelId", levelId);
            return definition;
        }

        private LevelCatalog CreateCatalog(params (string levelId, string sceneName)[] levels)
        {
            var entries = new LevelCatalogEntry[levels.Length];
            for (int i = 0; i < levels.Length; i++)
            {
                var entry = new LevelCatalogEntry();
                SetPrivateField(entry, "_levelDefinition", CreateLevelDefinition(levels[i].levelId));
                SetPrivateField(entry, "_sceneName", levels[i].sceneName);
                entries[i] = entry;
            }

            var catalog = CreateSilently(() => ScriptableObject.CreateInstance<LevelCatalog>());
            _scriptableObjects.Add(catalog);
            SetPrivateField(catalog, "_entries", entries);
            return catalog;
        }

        private LevelCardView CreateCardTemplate()
        {
            var go = new GameObject("TestLevelCardTemplate");
            go.SetActive(false);
            _spawnedObjects.Add(go);

            var titleText = new GameObject("TitleText").AddComponent<TMPro.TextMeshProUGUI>();
            titleText.transform.SetParent(go.transform);
            var lockedOverlay = new GameObject("LockedOverlay");
            lockedOverlay.transform.SetParent(go.transform);
            var playButton = new GameObject("PlayButton", typeof(RectTransform), typeof(Image), typeof(Button)).GetComponent<Button>();
            playButton.transform.SetParent(go.transform);

            var view = go.AddComponent<LevelCardView>();
            SetPrivateField(view, "_titleText", titleText);
            SetPrivateField(view, "_lockedOverlay", lockedOverlay);
            SetPrivateField(view, "_playButton", playButton);

            return view;
        }

        private LevelSelectionView CreateView()
        {
            var go = new GameObject("TestLevelSelectionView");
            _spawnedObjects.Add(go);
            var view = go.AddComponent<LevelSelectionView>();

            var container = new GameObject("CardContainer", typeof(RectTransform));
            container.transform.SetParent(go.transform);
            _spawnedObjects.Add(container);
            SetPrivateField(view, "_cardContainer", container.transform);

            return view;
        }

        private LevelSelectionPresenter CreatePresenter(LevelSelectionView view, LevelCardView cardTemplate)
        {
            var go = new GameObject("TestLevelSelectionPresenter");
            _spawnedObjects.Add(go);
            var presenter = go.AddComponent<LevelSelectionPresenter>();
            SetPrivateField(presenter, "_view", view);
            SetPrivateField(presenter, "_cardPrefab", cardTemplate);
            return presenter;
        }

        [UnityTest]
        public IEnumerator ReceiveApplicationServices_BuildsOneCardPerCatalogEntry()
        {
            LevelCatalog catalog = CreateCatalog(
                ("level_01", "__NonExistentTestScene_01__"),
                ("level_02", "__NonExistentTestScene_02__"));
            var services = new ApplicationServices(CreateTransitionService(), new LevelLaunchContext(), catalog, null, null);

            LevelSelectionView view = CreateView();
            LevelCardView cardTemplate = CreateCardTemplate();
            LevelSelectionPresenter presenter = CreatePresenter(view, cardTemplate);

            presenter.ReceiveApplicationServices(services);
            yield return null;

            var spawnedCards = (List<LevelCardView>)GetPrivateField(presenter, "_spawnedCards");
            Assert.AreEqual(2, spawnedCards.Count);
        }

        [UnityTest]
        public IEnumerator FirstCatalogEntry_IsUnlocked_RestAreLocked()
        {
            LevelCatalog catalog = CreateCatalog(
                ("level_01", "__NonExistentTestScene_01__"),
                ("level_02", "__NonExistentTestScene_02__"));
            var services = new ApplicationServices(CreateTransitionService(), new LevelLaunchContext(), catalog, null, null);

            LevelSelectionView view = CreateView();
            LevelCardView cardTemplate = CreateCardTemplate();
            LevelSelectionPresenter presenter = CreatePresenter(view, cardTemplate);

            presenter.ReceiveApplicationServices(services);
            yield return null;

            var spawnedCards = (List<LevelCardView>)GetPrivateField(presenter, "_spawnedCards");
            var firstPlayButton = (Button)GetPrivateField(spawnedCards[0], "_playButton");
            var secondPlayButton = (Button)GetPrivateField(spawnedCards[1], "_playButton");

            Assert.IsTrue(firstPlayButton.interactable, "First level should be unlocked by default.");
            Assert.IsFalse(secondPlayButton.interactable, "Second level should stay locked with no progression yet.");
        }

        [UnityTest]
        public IEnumerator SelectingLockedCard_DoesNotSetLevelLaunchContext()
        {
            LevelCatalog catalog = CreateCatalog(
                ("level_01", "__NonExistentTestScene_01__"),
                ("level_02", "__NonExistentTestScene_02__"));
            var launchContext = new LevelLaunchContext();
            var services = new ApplicationServices(CreateTransitionService(), launchContext, catalog, null, null);

            LevelSelectionView view = CreateView();
            LevelCardView cardTemplate = CreateCardTemplate();
            LevelSelectionPresenter presenter = CreatePresenter(view, cardTemplate);

            presenter.ReceiveApplicationServices(services);
            yield return null;

            var spawnedCards = (List<LevelCardView>)GetPrivateField(presenter, "_spawnedCards");
            InvokeSelected(spawnedCards[1]);

            Assert.IsFalse(launchContext.HasSelection, "A locked card's Selected event must never reach LevelLaunchContext.");
        }

        [UnityTest]
        public IEnumerator SelectingUnlockedCard_SetsLevelLaunchContext_ToItsLevelId()
        {
            LevelCatalog catalog = CreateCatalog(("level_01", "__NonExistentTestScene_01__"));
            var launchContext = new LevelLaunchContext();
            var services = new ApplicationServices(CreateTransitionService(), launchContext, catalog, null, null);

            LevelSelectionView view = CreateView();
            LevelCardView cardTemplate = CreateCardTemplate();
            LevelSelectionPresenter presenter = CreatePresenter(view, cardTemplate);

            presenter.ReceiveApplicationServices(services);
            yield return null;

            var spawnedCards = (List<LevelCardView>)GetPrivateField(presenter, "_spawnedCards");
            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex(".*"));
            InvokeSelected(spawnedCards[0]);
            yield return null;

            Assert.IsTrue(launchContext.HasSelection);
            Assert.AreEqual("level_01", launchContext.SelectedLevelId);
        }

        private SceneTransitionService CreateTransitionService()
        {
            var go = new GameObject("TestSceneTransitionService");
            _spawnedObjects.Add(go);
            return go.AddComponent<SceneTransitionService>();
        }

        private static void InvokeSelected(LevelCardView card)
        {
            FieldInfo eventField = typeof(LevelCardView).GetField("Selected", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(eventField, "Selected event backing field not found via reflection.");
            var handler = (System.Action)eventField.GetValue(card);
            handler?.Invoke();
        }
    }
}
