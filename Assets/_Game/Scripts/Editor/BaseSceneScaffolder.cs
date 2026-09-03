using AlienDefense.Core;
using AlienDefense.UI;
using AlienDefense.UI.Base;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace AlienDefense.EditorTools
{
    /// <summary>Builds the Base (profile/stats) scene: a read-only summary of the player's own progress
    /// (completed levels, total stars, unlocked towers, VIP tier, Coin, Gems) and a Back button.</summary>
    internal static class BaseSceneScaffolder
    {
        private const string SceneFolder = "Assets/_Game/Scenes/Menu";
        private const string ScenePath = SceneFolder + "/Base.unity";

        private static readonly Color PanelColor = new Color(0.08f, 0.12f, 0.22f, 0.85f);
        private static readonly Color BackgroundColor = new Color(0.04f, 0.08f, 0.16f, 1f);

        [MenuItem("AlienDefense/Setup/17. Build Base Scene")]
        public static void BuildBaseScene()
        {
            EditorFolderUtility.EnsureFolder(SceneFolder);

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            (GameObject canvasObject, Transform safeArea) = EditorCanvasUtility.BuildCanvasWithSafeArea("Canvas");
            EditorScreenBuildingBlocks.BuildFullScreenImage(canvasObject.transform, "BackgroundOverlay", BackgroundColor).transform.SetAsFirstSibling();

            LevelSceneScaffolder.CreateTMPText(safeArea, "TitleText", "Base", 40f, 90f, 48f, TextAlignmentOptions.Center);
            Button backButton = EditorScreenBuildingBlocks.BuildTopLeftButton(safeArea, "BackButton", "<", PanelColor);

            var statsPanel = new GameObject("StatsPanel", typeof(RectTransform), typeof(Image));
            statsPanel.transform.SetParent(safeArea, false);
            RectTransform statsRect = statsPanel.GetComponent<RectTransform>();
            statsRect.anchorMin = new Vector2(0.5f, 0.5f);
            statsRect.anchorMax = new Vector2(0.5f, 0.5f);
            statsRect.pivot = new Vector2(0.5f, 0.5f);
            statsRect.anchoredPosition = Vector2.zero;
            statsRect.sizeDelta = new Vector2(700f, 480f);
            statsPanel.GetComponent<Image>().color = PanelColor;

            var layout = statsPanel.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 20f;
            layout.padding = new RectOffset(40, 40, 40, 40);
            layout.childAlignment = TextAnchor.UpperLeft;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            layout.childControlHeight = false;

            TMP_Text completedLevelsText = EditorScreenBuildingBlocks.BuildStatRow(statsPanel.transform, "CompletedLevelsText", "Levels completed: 0");
            TMP_Text totalStarsText = EditorScreenBuildingBlocks.BuildStatRow(statsPanel.transform, "TotalStarsText", "Total stars: 0");
            TMP_Text unlockedTowersText = EditorScreenBuildingBlocks.BuildStatRow(statsPanel.transform, "UnlockedTowersText", "Towers unlocked: 0");
            TMP_Text vipTierText = EditorScreenBuildingBlocks.BuildStatRow(statsPanel.transform, "VipTierText", "VIP: None");
            TMP_Text coinText = EditorScreenBuildingBlocks.BuildStatRow(statsPanel.transform, "CoinText", "Coin: 0");
            TMP_Text gemText = EditorScreenBuildingBlocks.BuildStatRow(statsPanel.transform, "GemText", "Gem: 0");

            var view = safeArea.gameObject.AddComponent<BaseScreenView>();
            var serializedView = new SerializedObject(view);
            serializedView.FindProperty("_completedLevelsText").objectReferenceValue = completedLevelsText;
            serializedView.FindProperty("_totalStarsText").objectReferenceValue = totalStarsText;
            serializedView.FindProperty("_unlockedTowersText").objectReferenceValue = unlockedTowersText;
            serializedView.FindProperty("_vipTierText").objectReferenceValue = vipTierText;
            serializedView.FindProperty("_coinText").objectReferenceValue = coinText;
            serializedView.FindProperty("_gemText").objectReferenceValue = gemText;
            serializedView.FindProperty("_backButton").objectReferenceValue = backButton;
            serializedView.ApplyModifiedPropertiesWithoutUndo();

            var backNavigation = canvasObject.AddComponent<BackNavigationController>();

            var presenterObject = new GameObject("BaseScreenPresenter");
            presenterObject.transform.SetParent(canvasObject.transform, false);
            var presenter = presenterObject.AddComponent<BaseScreenPresenter>();
            var serializedPresenter = new SerializedObject(presenter);
            serializedPresenter.FindProperty("_view").objectReferenceValue = view;
            serializedPresenter.FindProperty("_backNavigation").objectReferenceValue = backNavigation;
            serializedPresenter.ApplyModifiedPropertiesWithoutUndo();

            LevelSceneScaffolder.BuildEventSystem();
            SceneServicesHostScaffolder.EnsureInActiveScene(includeSaveDebugControls: false);

            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorScreenBuildingBlocks.EnsureSceneInBuildSettings(ScenePath);

            Debug.Log("[AlienDefense Setup] Saved Base scene to " + ScenePath + ".");
        }
    }
}
