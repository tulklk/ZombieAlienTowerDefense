using AlienDefense.Core;
using AlienDefense.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace AlienDefense.EditorTools
{
    /// <summary>Builds the LevelSelection scene: a vertical list of LevelCard slots (populated at runtime by
    /// LevelSelectionPresenter from the LevelCatalog, never at Editor-build time), Back button, and EventSystem.</summary>
    internal static class LevelSelectionSceneScaffolder
    {
        private const string SceneFolder = "Assets/_Game/Scenes/Menu";
        private const string ScenePath = SceneFolder + "/LevelSelection.unity";

        [MenuItem("AlienDefense/Setup/15. Build LevelSelection Scene")]
        public static void BuildLevelSelectionScene()
        {
            GameObject cardPrefab = LevelCardPrefabBuilder.CreateOrLoad();

            EditorFolderUtility.EnsureFolder(SceneFolder);

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            (GameObject canvasObject, Transform safeArea) = EditorCanvasUtility.BuildCanvasWithSafeArea("Canvas");

            LevelSceneScaffolder.CreateTMPText(safeArea, "TitleText", "Select Level", 200f, 80f, 44f, TextAlignmentOptions.Center);

            Transform cardContainer = BuildCardContainer(safeArea);

            Button backButton = BuildBackButton(safeArea);

            var view = safeArea.gameObject.AddComponent<LevelSelectionView>();
            var serializedView = new SerializedObject(view);
            serializedView.FindProperty("_cardContainer").objectReferenceValue = cardContainer;
            serializedView.FindProperty("_backButton").objectReferenceValue = backButton;
            serializedView.ApplyModifiedPropertiesWithoutUndo();

            var backNavigation = canvasObject.AddComponent<BackNavigationController>();

            var presenterObject = new GameObject("LevelSelectionPresenter");
            presenterObject.transform.SetParent(canvasObject.transform, false);
            var presenter = presenterObject.AddComponent<LevelSelectionPresenter>();
            var serializedPresenter = new SerializedObject(presenter);
            serializedPresenter.FindProperty("_view").objectReferenceValue = view;
            serializedPresenter.FindProperty("_cardPrefab").objectReferenceValue = cardPrefab.GetComponent<LevelCardView>();
            serializedPresenter.FindProperty("_backNavigation").objectReferenceValue = backNavigation;
            serializedPresenter.ApplyModifiedPropertiesWithoutUndo();

            LevelSceneScaffolder.BuildEventSystem();

            SceneServicesHostScaffolder.EnsureInActiveScene(includeSaveDebugControls: false);

            EditorSceneManager.SaveScene(scene, ScenePath);

            Debug.Log("[AlienDefense Setup] Saved LevelSelection scene to " + ScenePath + ".");
        }

        private static Transform BuildCardContainer(Transform parent)
        {
            var containerObject = new GameObject("CardContainer", typeof(RectTransform));
            containerObject.transform.SetParent(parent, false);
            var containerRect = containerObject.GetComponent<RectTransform>();
            containerRect.anchorMin = new Vector2(0.5f, 1f);
            containerRect.anchorMax = new Vector2(0.5f, 1f);
            containerRect.pivot = new Vector2(0.5f, 1f);
            containerRect.anchoredPosition = new Vector2(0f, -320f);
            containerRect.sizeDelta = new Vector2(460f, 1200f);

            var layoutGroup = containerObject.AddComponent<VerticalLayoutGroup>();
            layoutGroup.spacing = 24f;
            layoutGroup.childAlignment = TextAnchor.UpperCenter;
            layoutGroup.childForceExpandWidth = true;
            layoutGroup.childForceExpandHeight = false;
            layoutGroup.childControlHeight = false;
            layoutGroup.childControlWidth = true;

            var fitter = containerObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            return containerObject.transform;
        }

        /// <summary>Bottom-anchored (not a fixed offset from the top), so it stays clear of the card list and
        /// on-screen regardless of safe-area height/aspect ratio.</summary>
        private static Button BuildBackButton(Transform parent)
        {
            var buttonObject = new GameObject("BackButton", typeof(RectTransform), typeof(Image), typeof(Button));
            buttonObject.transform.SetParent(parent, false);
            var rect = buttonObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = new Vector2(0f, 40f);
            rect.sizeDelta = new Vector2(320f, 120f);

            buttonObject.GetComponent<Image>().color = new Color(0.3f, 0.1f, 0.1f, 0.9f);

            LevelSceneScaffolder.CreateTMPText(buttonObject.transform, "Label", "Back", 0f, 120f, 32f, TextAlignmentOptions.Center);

            return buttonObject.GetComponent<Button>();
        }
    }
}
