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
    /// <summary>Builds the MainMenu scene: Canvas/SafeArea with Play/Settings/Exit, MainMenuPresenter as the
    /// scene's IApplicationServicesReceiver entry point, BackNavigationController, and EventSystem.</summary>
    internal static class MainMenuSceneScaffolder
    {
        private const string SceneFolder = "Assets/_Game/Scenes/Menu";
        private const string ScenePath = SceneFolder + "/MainMenu.unity";

        [MenuItem("AlienDefense/Setup/13. Build MainMenu Scene")]
        public static void BuildMainMenuScene()
        {
            EditorFolderUtility.EnsureFolder(SceneFolder);

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            (GameObject canvasObject, Transform safeArea) = EditorCanvasUtility.BuildCanvasWithSafeArea("Canvas");

            LevelSceneScaffolder.CreateTMPText(safeArea, "TitleText", "Alien Defense 3D", 260f, 120f, 56f, TextAlignmentOptions.Center);

            Transform buttonStack = EditorCanvasUtility.BuildCenteredButtonStack(safeArea, 640f, 32f);
            Button playButton = EditorCanvasUtility.BuildStackedMenuButton(buttonStack, "PlayButton", "Play", 140f, new Color(0.15f, 0.35f, 0.15f, 0.9f));
            Button settingsButton = EditorCanvasUtility.BuildStackedMenuButton(buttonStack, "SettingsButton", "Settings", 140f, new Color(0.2f, 0.2f, 0.35f, 0.9f));
            Button exitButton = EditorCanvasUtility.BuildStackedMenuButton(buttonStack, "ExitButton", "Exit", 140f, new Color(0.3f, 0.1f, 0.1f, 0.9f));

            var view = safeArea.gameObject.AddComponent<MainMenuView>();
            var serializedView = new SerializedObject(view);
            serializedView.FindProperty("_playButton").objectReferenceValue = playButton;
            serializedView.FindProperty("_settingsButton").objectReferenceValue = settingsButton;
            serializedView.FindProperty("_exitButton").objectReferenceValue = exitButton;
            serializedView.ApplyModifiedPropertiesWithoutUndo();

            var backNavigation = canvasObject.AddComponent<BackNavigationController>();

            var presenterObject = new GameObject("MainMenuPresenter");
            presenterObject.transform.SetParent(canvasObject.transform, false);
            var presenter = presenterObject.AddComponent<MainMenuPresenter>();
            var serializedPresenter = new SerializedObject(presenter);
            serializedPresenter.FindProperty("_view").objectReferenceValue = view;
            serializedPresenter.FindProperty("_backNavigation").objectReferenceValue = backNavigation;
            serializedPresenter.ApplyModifiedPropertiesWithoutUndo();

            LevelSceneScaffolder.BuildEventSystem();

            SceneServicesHostScaffolder.EnsureInActiveScene(includeSaveDebugControls: true);

            EditorSceneManager.SaveScene(scene, ScenePath);

            Debug.Log("[AlienDefense Setup] Saved MainMenu scene to " + ScenePath + ".");
        }
    }
}
