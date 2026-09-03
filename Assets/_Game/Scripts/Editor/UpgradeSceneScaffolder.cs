using AlienDefense.Core;
using AlienDefense.Towers;
using AlienDefense.UI;
using AlienDefense.UI.MainMenu;
using AlienDefense.UI.Upgrade;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace AlienDefense.EditorTools
{
    /// <summary>Builds the TowerCatalog data asset (from every TowerDefinition already under Assets/_Game/Data/Towers)
    /// and the Upgrade scene: a card grid (TowerUpgradeCardView per tower) driven by TowerUpgradeScreenPresenter,
    /// a Coin display, and a Back button. Reachable from MainMenu's bottom-nav Upgrade tab.</summary>
    internal static class UpgradeSceneScaffolder
    {
        private const string SceneFolder = "Assets/_Game/Scenes/Menu";
        private const string ScenePath = SceneFolder + "/Upgrade.unity";
        private const string TowerDataFolder = "Assets/_Game/Data/Towers";
        private const string TowerCatalogPath = TowerDataFolder + "/TowerCatalog.asset";

        private static readonly Color PanelColor = new Color(0.08f, 0.12f, 0.22f, 0.85f);
        private static readonly Color AccentGreen = new Color(0.35f, 0.85f, 0.45f, 1f);
        private static readonly Color BackgroundColor = new Color(0.04f, 0.08f, 0.16f, 1f);

        [MenuItem("AlienDefense/Setup/15. Build/Refresh Tower Catalog Asset")]
        public static void BuildTowerCatalog()
        {
            EditorFolderUtility.EnsureFolder(TowerDataFolder);

            string[] guids = AssetDatabase.FindAssets("t:TowerDefinition", new[] { TowerDataFolder });
            var towers = new TowerDefinition[guids.Length];
            for (int i = 0; i < guids.Length; i++)
            {
                towers[i] = AssetDatabase.LoadAssetAtPath<TowerDefinition>(AssetDatabase.GUIDToAssetPath(guids[i]));
            }

            TowerCatalog catalog = AssetDatabase.LoadAssetAtPath<TowerCatalog>(TowerCatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<TowerCatalog>();
                AssetDatabase.CreateAsset(catalog, TowerCatalogPath);
            }

            var serializedCatalog = new SerializedObject(catalog);
            SerializedProperty towersProperty = serializedCatalog.FindProperty("_towers");
            towersProperty.arraySize = towers.Length;
            for (int i = 0; i < towers.Length; i++)
            {
                towersProperty.GetArrayElementAtIndex(i).objectReferenceValue = towers[i];
            }

            serializedCatalog.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();

            Debug.Log($"[AlienDefense Setup] TowerCatalog now has {towers.Length} tower(s): {TowerCatalogPath}");
        }

        [MenuItem("AlienDefense/Setup/16. Build Upgrade Scene")]
        public static void BuildUpgradeScene()
        {
            BuildTowerCatalog();

            EditorFolderUtility.EnsureFolder(SceneFolder);

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            (GameObject canvasObject, Transform safeArea) = EditorCanvasUtility.BuildCanvasWithSafeArea("Canvas");
            BuildFullScreenImage(canvasObject.transform, "BackgroundOverlay", BackgroundColor).transform.SetAsFirstSibling();

            LevelSceneScaffolder.CreateTMPText(safeArea, "TitleText", "Upgrade Tower", 40f, 90f, 48f, TextAlignmentOptions.Center);

            Button backButton = BuildTopLeftButton(safeArea, "BackButton", "<");
            ResourceWidgetView coinWidget = BuildCoinWidget(safeArea);

            Transform cardContainer = BuildCardGrid(safeArea);
            TowerUpgradeCardView cardPrefab = BuildOrLoadCardPrefab();

            var view = safeArea.gameObject.AddComponent<TowerUpgradeScreenView>();
            var serializedView = new SerializedObject(view);
            serializedView.FindProperty("_cardContainer").objectReferenceValue = cardContainer;
            serializedView.FindProperty("_backButton").objectReferenceValue = backButton;
            serializedView.FindProperty("_coinWidget").objectReferenceValue = coinWidget;
            serializedView.ApplyModifiedPropertiesWithoutUndo();

            var backNavigation = canvasObject.AddComponent<BackNavigationController>();

            var presenterObject = new GameObject("TowerUpgradeScreenPresenter");
            presenterObject.transform.SetParent(canvasObject.transform, false);
            var presenter = presenterObject.AddComponent<TowerUpgradeScreenPresenter>();
            var serializedPresenter = new SerializedObject(presenter);
            serializedPresenter.FindProperty("_view").objectReferenceValue = view;
            serializedPresenter.FindProperty("_cardPrefab").objectReferenceValue = cardPrefab;
            serializedPresenter.FindProperty("_backNavigation").objectReferenceValue = backNavigation;
            serializedPresenter.ApplyModifiedPropertiesWithoutUndo();

            LevelSceneScaffolder.BuildEventSystem();

            SceneServicesHostScaffolder.EnsureInActiveScene(includeSaveDebugControls: false);

            EditorSceneManager.SaveScene(scene, ScenePath);
            EnsureUpgradeSceneInBuildSettings();

            Debug.Log("[AlienDefense Setup] Saved Upgrade scene to " + ScenePath + ".");
        }

        private static void EnsureUpgradeSceneInBuildSettings()
        {
            var scenes = new System.Collections.Generic.List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            foreach (EditorBuildSettingsScene existing in scenes)
            {
                if (existing.path == ScenePath)
                {
                    return;
                }
            }

            scenes.Add(new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        private static TowerUpgradeCardView BuildOrLoadCardPrefab()
        {
            const string prefabPath = "Assets/_Game/Prefabs/UI/Upgrade/TowerUpgradeCard.prefab";
            EditorFolderUtility.EnsureFolder("Assets/_Game/Prefabs/UI/Upgrade");

            var go = new GameObject("TowerUpgradeCard", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            go.GetComponent<Image>().color = PanelColor;
            var layoutElement = go.GetComponent<LayoutElement>();
            layoutElement.preferredWidth = 300f;
            layoutElement.preferredHeight = 340f;

            Image icon = BuildAnchoredImage(go.transform, "Icon", new Vector2(0.5f, 1f), new Vector2(96f, 96f), new Vector2(0f, -20f), Color.white);
            TMP_Text nameText = BuildAnchoredText(go.transform, "NameText", "Tower", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -128f), new Vector2(280f, 36f), 26f, TextAlignmentOptions.Center);
            TMP_Text levelText = BuildAnchoredText(go.transform, "LevelText", "Lv 1/1", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -168f), new Vector2(280f, 30f), 20f, TextAlignmentOptions.Center);

            var upgradeButtonObject = new GameObject("UpgradeButton", typeof(RectTransform), typeof(Image), typeof(Button));
            upgradeButtonObject.transform.SetParent(go.transform, false);
            RectTransform buttonRect = upgradeButtonObject.GetComponent<RectTransform>();
            buttonRect.anchorMin = new Vector2(0.5f, 0f);
            buttonRect.anchorMax = new Vector2(0.5f, 0f);
            buttonRect.pivot = new Vector2(0.5f, 0f);
            buttonRect.anchoredPosition = new Vector2(0f, 20f);
            buttonRect.sizeDelta = new Vector2(260f, 64f);
            upgradeButtonObject.GetComponent<Image>().color = AccentGreen;
            TMP_Text costText = BuildAnchoredText(upgradeButtonObject.transform, "CostText", "0", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(260f, 64f), 28f, TextAlignmentOptions.Center);

            Image lockedOverlay = BuildFullScreenImage(go.transform, "LockedOverlay", new Color(0f, 0f, 0f, 0.6f));
            lockedOverlay.gameObject.SetActive(false);

            var view = go.AddComponent<TowerUpgradeCardView>();
            var serialized = new SerializedObject(view);
            serialized.FindProperty("_icon").objectReferenceValue = icon;
            serialized.FindProperty("_nameText").objectReferenceValue = nameText;
            serialized.FindProperty("_levelText").objectReferenceValue = levelText;
            serialized.FindProperty("_costText").objectReferenceValue = costText;
            serialized.FindProperty("_upgradeButton").objectReferenceValue = upgradeButtonObject.GetComponent<Button>();
            serialized.FindProperty("_lockedOverlay").objectReferenceValue = lockedOverlay.gameObject;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            GameObject prefabAsset = PrefabUtility.SaveAsPrefabAsset(go, prefabPath);
            Object.DestroyImmediate(go);

            return prefabAsset.GetComponent<TowerUpgradeCardView>();
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
            grid.cellSize = new Vector2(300f, 340f);
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

        private static Button BuildTopLeftButton(Transform safeArea, string name, string glyph)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(safeArea, false);
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(24f, -24f);
            rect.sizeDelta = new Vector2(64f, 64f);
            go.GetComponent<Image>().color = PanelColor;

            BuildAnchoredText(go.transform, "Label", glyph, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(64f, 64f), 32f, TextAlignmentOptions.Center);

            return go.GetComponent<Button>();
        }

        private static Image BuildFullScreenImage(Transform parent, string name, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            var image = go.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private static Image BuildAnchoredImage(Transform parent, string name, Vector2 anchor, Vector2 size, Vector2 anchoredPosition, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = anchor;
            rect.sizeDelta = size;
            rect.anchoredPosition = anchoredPosition;
            var image = go.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private static TMP_Text BuildAnchoredText(Transform parent, string name, string initialText, Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPosition, Vector2 size, float fontSize, TextAlignmentOptions alignment)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;

            var text = go.AddComponent<TextMeshProUGUI>();
            text.text = initialText;
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = Color.white;
            return text;
        }
    }
}
