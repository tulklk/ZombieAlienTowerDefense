using AlienDefense.Core;
using AlienDefense.UI;
using AlienDefense.UI.Defense;
using AlienDefense.UI.MainMenu;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using static AlienDefense.EditorTools.EditorScreenBuildingBlocks;

namespace AlienDefense.EditorTools
{
    /// <summary>Builds the Defense (tower unlock) scene: a card grid (TowerUnlockCardView per TowerCatalog
    /// entry) driven by DefenseScreenPresenter, a Coin display, and a Back button. This is distinct from the
    /// Upgrade screen: Defense unlocks a NEW tower type; Upgrade levels up one already owned.</summary>
    internal static class DefenseSceneScaffolder
    {
        private const string SceneFolder = "Assets/_Game/Scenes/Menu";
        private const string ScenePath = SceneFolder + "/Defense.unity";

        private static readonly Color PanelColor = new Color(0.08f, 0.12f, 0.22f, 0.85f);
        private static readonly Color AccentGreen = new Color(0.35f, 0.85f, 0.45f, 1f);
        private static readonly Color BackgroundColor = new Color(0.04f, 0.08f, 0.16f, 1f);

        [MenuItem("AlienDefense/Setup/18. Build Defense Scene")]
        public static void BuildDefenseScene()
        {
            EditorFolderUtility.EnsureFolder(SceneFolder);

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            (GameObject canvasObject, Transform safeArea) = EditorCanvasUtility.BuildCanvasWithSafeArea("Canvas");
            BuildFullScreenImage(canvasObject.transform, "BackgroundOverlay", BackgroundColor).transform.SetAsFirstSibling();

            LevelSceneScaffolder.CreateTMPText(safeArea, "TitleText", "Unlock Towers", 40f, 90f, 48f, TextAlignmentOptions.Center);
            Button backButton = BuildTopLeftButton(safeArea, "BackButton", "<", PanelColor);
            ResourceWidgetView coinWidget = BuildCoinWidget(safeArea);

            Transform cardContainer = BuildCardGrid(safeArea);
            TowerUnlockCardView cardPrefab = BuildOrLoadCardPrefab();

            var view = safeArea.gameObject.AddComponent<DefenseScreenView>();
            var serializedView = new SerializedObject(view);
            serializedView.FindProperty("_cardContainer").objectReferenceValue = cardContainer;
            serializedView.FindProperty("_backButton").objectReferenceValue = backButton;
            serializedView.FindProperty("_coinWidget").objectReferenceValue = coinWidget;
            serializedView.ApplyModifiedPropertiesWithoutUndo();

            var backNavigation = canvasObject.AddComponent<BackNavigationController>();

            var presenterObject = new GameObject("DefenseScreenPresenter");
            presenterObject.transform.SetParent(canvasObject.transform, false);
            var presenter = presenterObject.AddComponent<DefenseScreenPresenter>();
            var serializedPresenter = new SerializedObject(presenter);
            serializedPresenter.FindProperty("_view").objectReferenceValue = view;
            serializedPresenter.FindProperty("_cardPrefab").objectReferenceValue = cardPrefab;
            serializedPresenter.FindProperty("_backNavigation").objectReferenceValue = backNavigation;
            serializedPresenter.ApplyModifiedPropertiesWithoutUndo();

            LevelSceneScaffolder.BuildEventSystem();
            SceneServicesHostScaffolder.EnsureInActiveScene(includeSaveDebugControls: false);

            EditorSceneManager.SaveScene(scene, ScenePath);
            EnsureSceneInBuildSettings(ScenePath);

            Debug.Log("[AlienDefense Setup] Saved Defense scene to " + ScenePath + ".");
        }

        private static TowerUnlockCardView BuildOrLoadCardPrefab()
        {
            const string prefabPath = "Assets/_Game/Prefabs/UI/Defense/TowerUnlockCard.prefab";
            EditorFolderUtility.EnsureFolder("Assets/_Game/Prefabs/UI/Defense");

            var go = new GameObject("TowerUnlockCard", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            go.GetComponent<Image>().color = PanelColor;
            var layoutElement = go.GetComponent<LayoutElement>();
            layoutElement.preferredWidth = 300f;
            layoutElement.preferredHeight = 300f;

            Image icon = BuildAnchoredImage(go.transform, "Icon", new Vector2(0.5f, 1f), new Vector2(96f, 96f), new Vector2(0f, -20f), Color.white);
            TMP_Text nameText = BuildAnchoredText(go.transform, "NameText", "Tower", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -128f), new Vector2(280f, 36f), 26f, TextAlignmentOptions.Center);

            var unlockButtonObject = new GameObject("UnlockButton", typeof(RectTransform), typeof(Image), typeof(Button));
            unlockButtonObject.transform.SetParent(go.transform, false);
            RectTransform buttonRect = unlockButtonObject.GetComponent<RectTransform>();
            buttonRect.anchorMin = new Vector2(0.5f, 0f);
            buttonRect.anchorMax = new Vector2(0.5f, 0f);
            buttonRect.pivot = new Vector2(0.5f, 0f);
            buttonRect.anchoredPosition = new Vector2(0f, 20f);
            buttonRect.sizeDelta = new Vector2(260f, 64f);
            unlockButtonObject.GetComponent<Image>().color = AccentGreen;
            TMP_Text costText = BuildAnchoredText(unlockButtonObject.transform, "CostText", "0", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(260f, 64f), 26f, TextAlignmentOptions.Center);

            GameObject unlockedLabel = BuildAnchoredText(go.transform, "UnlockedLabel", "Unlocked", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 40f), new Vector2(260f, 44f), 22f, TextAlignmentOptions.Center).gameObject;
            unlockedLabel.SetActive(false);

            var view = go.AddComponent<TowerUnlockCardView>();
            var serialized = new SerializedObject(view);
            serialized.FindProperty("_icon").objectReferenceValue = icon;
            serialized.FindProperty("_nameText").objectReferenceValue = nameText;
            serialized.FindProperty("_costText").objectReferenceValue = costText;
            serialized.FindProperty("_unlockButton").objectReferenceValue = unlockButtonObject.GetComponent<Button>();
            serialized.FindProperty("_unlockedLabel").objectReferenceValue = unlockedLabel;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            GameObject prefabAsset = PrefabUtility.SaveAsPrefabAsset(go, prefabPath);
            Object.DestroyImmediate(go);

            return prefabAsset.GetComponent<TowerUnlockCardView>();
        }

        private static Transform BuildCardGrid(Transform safeArea)
        {
            var container = new GameObject("CardContainer", typeof(RectTransform));
            container.transform.SetParent(safeArea, false);
            RectTransform rect = container.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(0f, -40f);
            rect.sizeDelta = new Vector2(940f, 1400f);

            var grid = container.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(300f, 300f);
            grid.spacing = new Vector2(20f, 20f);
            grid.childAlignment = TextAnchor.UpperCenter;

            return container.transform;
        }

        private static ResourceWidgetView BuildCoinWidget(Transform safeArea)
        {
            var widget = new GameObject("CoinWidget", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
            widget.transform.SetParent(safeArea, false);
            RectTransform rect = widget.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(1f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 1f);
            rect.anchoredPosition = new Vector2(-24f, -24f);
            rect.sizeDelta = new Vector2(180f, 56f);
            widget.GetComponent<Image>().color = PanelColor;

            TMP_Text amount = BuildAnchoredText(widget.transform, "AmountText", "0", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(160f, 40f), 26f, TextAlignmentOptions.Center);

            var view = widget.AddComponent<ResourceWidgetView>();
            var serialized = new SerializedObject(view);
            serialized.FindProperty("_amountText").objectReferenceValue = amount;
            serialized.FindProperty("_canvasGroup").objectReferenceValue = widget.GetComponent<CanvasGroup>();
            serialized.ApplyModifiedPropertiesWithoutUndo();

            return view;
        }
    }
}
