using AlienDefense.Core;
using AlienDefense.DebugTools;
using AlienDefense.Save;
using AlienDefense.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace AlienDefense.EditorTools
{
    /// <summary>Builds Bootstrap with embedded LoadingOverlay, menu scenes with SceneServicesHost, and the
    /// LoadingOverlay prefab.</summary>
    internal static class BootstrapSceneScaffolder
    {
        private const string BootstrapSceneFolder = "Assets/_Game/Scenes/Bootstrap";
        private const string BootstrapScenePath = BootstrapSceneFolder + "/Bootstrap.unity";
        private const string CatalogPath = "Assets/_Game/Data/Levels/LevelCatalog.asset";
        private const string LoadingOverlayPrefabPath = "Assets/_Game/Prefabs/UI/LoadingOverlay.prefab";
        private const string MainMenuScenePath = "Assets/_Game/Scenes/Menu/MainMenu.unity";
        private const string LevelSelectionScenePath = "Assets/_Game/Scenes/Menu/LevelSelection.unity";

        [MenuItem("AlienDefense/Setup/12a. Wire Bootstrap Scene Loading Controller")]
        public static void WireBootstrapSceneLoadingController()
        {
            if (!System.IO.File.Exists(BootstrapScenePath))
            {
                BuildBootstrapScene();
                return;
            }

            Scene scene = EditorSceneManager.OpenScene(BootstrapScenePath, OpenSceneMode.Single);
            LoadingOverlayView overlayView = Object.FindFirstObjectByType<LoadingOverlayView>(FindObjectsInactive.Include);
            if (overlayView == null)
            {
                Debug.LogError("[AlienDefense Setup] Bootstrap scene has no LoadingOverlayView. Run menu 12 first.");
                return;
            }

            LevelCatalog catalog = AssetDatabase.LoadAssetAtPath<LevelCatalog>(CatalogPath);
            PlayerProfileDefaults profileDefaults = PlayerProfileDefaultsBuilder.CreateOrLoad();

            LoadingOverlayView.SuppressEditorLifecycleCallbacks = true;
            try
            {
                ResetLoadingOverlaySceneInstance(overlayView.gameObject);
                Camera bootstrapCamera = EnsureBootstrapUICameraInScene(scene);
                BootstrapLoadingController controller = EnsureBootstrapHostController(overlayView);
                WireBootstrapLoadingController(controller, overlayView, catalog, profileDefaults, bootstrapCamera);
            }
            finally
            {
                LoadingOverlayView.SuppressEditorLifecycleCallbacks = false;
            }

            EnsureBuildSettings();

            GameObject bootstrapLauncher = GameObject.Find("BootstrapLauncher");
            if (bootstrapLauncher != null)
            {
                Object.DestroyImmediate(bootstrapLauncher);
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            Debug.Log("[AlienDefense Setup] Wired BootstrapLoadingController on BootstrapHost.");
        }

        private static BootstrapLoadingController EnsureBootstrapHostController(LoadingOverlayView overlayView)
        {
            GameObject hostObject = GameObject.Find("BootstrapHost");
            if (hostObject == null)
            {
                hostObject = new GameObject("BootstrapHost");
            }

            BootstrapLoadingController controller = hostObject.GetComponent<BootstrapLoadingController>();
            if (controller == null)
            {
                controller = hostObject.AddComponent<BootstrapLoadingController>();
            }

            BootstrapLoadingController overlayController = overlayView.GetComponent<BootstrapLoadingController>();
            if (overlayController != null && overlayController != controller)
            {
                Object.DestroyImmediate(overlayController);
            }

            return controller;
        }

        private static void ResetLoadingOverlaySceneInstance(GameObject loadingInstance)
        {
            if (PrefabUtility.IsPartOfPrefabInstance(loadingInstance))
            {
                PrefabUtility.RevertPrefabInstance(loadingInstance, InteractionMode.AutomatedAction);
            }

            loadingInstance.name = "LoadingOverlay";
            loadingInstance.SetActive(true);
            loadingInstance.transform.localScale = Vector3.one;
        }

        [MenuItem("AlienDefense/Setup/12. Build Bootstrap Scene")]
        public static void BuildBootstrapScene()
        {
            LoadingOverlayView.SuppressEditorLifecycleCallbacks = true;
            try
            {
                EditorFolderUtility.EnsureFolder(BootstrapSceneFolder);
                EnsureLoadingOverlayPrefabExists();

                GameObject prefabRoot = LoadLoadingOverlayPrefabRoot();
                if (prefabRoot == null)
                {
                    Debug.LogError("[AlienDefense Setup] Could not create or load LoadingOverlay prefab.");
                    return;
                }

                EnsureBuildSettings();

                LevelCatalog catalog = AssetDatabase.LoadAssetAtPath<LevelCatalog>(CatalogPath);
                if (catalog == null)
                {
                    Debug.LogWarning("[AlienDefense Setup] LevelCatalog asset not found at " + CatalogPath +
                        ". Run 'AlienDefense/Setup/11. Create Level Catalog Asset' first, or assign it manually.");
                }

                PlayerProfileDefaults profileDefaults = PlayerProfileDefaultsBuilder.CreateOrLoad();

                Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

                Camera bootstrapCamera = EnsureBootstrapUICameraInScene(scene);

                GameObject bootstrapHost = new GameObject("BootstrapHost");
                BootstrapLoadingController bootstrapController = bootstrapHost.AddComponent<BootstrapLoadingController>();

                GameObject loadingInstance = (GameObject)PrefabUtility.InstantiatePrefab(prefabRoot, scene);
                if (loadingInstance == null)
                {
                    Debug.LogError("[AlienDefense Setup] Failed to instantiate LoadingOverlay prefab into Bootstrap scene.");
                    Object.DestroyImmediate(bootstrapHost);
                    return;
                }

                loadingInstance.name = "LoadingOverlay";
                ResetLoadingOverlaySceneInstance(loadingInstance);

                LoadingOverlayView instanceOverlay = loadingInstance.GetComponent<LoadingOverlayView>();
                if (instanceOverlay == null)
                {
                    Debug.LogError("[AlienDefense Setup] LoadingOverlay prefab is missing LoadingOverlayView.");
                    Object.DestroyImmediate(loadingInstance);
                    Object.DestroyImmediate(bootstrapHost);
                    return;
                }

                WireBootstrapLoadingController(bootstrapController, instanceOverlay, catalog, profileDefaults, bootstrapCamera);

                EditorSceneManager.SaveScene(scene, BootstrapScenePath);
                AssetDatabase.SaveAssets();

                Debug.Log("[AlienDefense Setup] Saved Bootstrap scene with embedded LoadingOverlay to " + BootstrapScenePath + ".");
            }
            catch (System.Exception exception)
            {
                Debug.LogError("[AlienDefense Setup] Build Bootstrap Scene failed: " + exception);
            }
            finally
            {
                LoadingOverlayView.SuppressEditorLifecycleCallbacks = false;
            }
        }

        [MenuItem("AlienDefense/Setup/12b. Migrate Loading Overlay To Prefab")]
        public static void MigrateLoadingOverlayToPrefab()
        {
            LoadingOverlayView prefabView = null;

            if (System.IO.File.Exists(BootstrapScenePath))
            {
                Scene scene = EditorSceneManager.OpenScene(BootstrapScenePath, OpenSceneMode.Single);
                Transform loadingCanvas = FindEmbeddedLoadingCanvasInScene();
                if (loadingCanvas != null)
                {
                    EditorFolderUtility.EnsureFolder("Assets/_Game/Prefabs/UI");

                    GameObject loadingObject = loadingCanvas.gameObject;
                    loadingObject.transform.SetParent(null, true);

                    GameObject prefabAsset = PrefabUtility.SaveAsPrefabAsset(loadingObject, LoadingOverlayPrefabPath);
                    Object.DestroyImmediate(loadingObject);

                    prefabView = prefabAsset.GetComponent<LoadingOverlayView>();
                    Debug.Log("[AlienDefense Setup] Migrated embedded LoadingCanvas to " + LoadingOverlayPrefabPath + ".");

                    EditorSceneManager.MarkSceneDirty(scene);
                    EditorSceneManager.SaveScene(scene);
                }
            }

            if (prefabView == null)
            {
                EnsureLoadingOverlayPrefabExists();
                Debug.Log("[AlienDefense Setup] No embedded LoadingCanvas found; ensured prefab at " + LoadingOverlayPrefabPath + ".");
            }

            SceneServicesHostScaffolder.EnsureInSceneAtPath(MainMenuScenePath, includeSaveDebugControls: true);
            SceneServicesHostScaffolder.EnsureInSceneAtPath(LevelSelectionScenePath, includeSaveDebugControls: false);
            Debug.Log("[AlienDefense Setup] Loading overlay prefab wired to menu SceneTransitionService hosts.");
        }

        private static void EnsureBuildSettings()
        {
            string[] orderedPaths =
            {
                BootstrapScenePath,
                MainMenuScenePath,
                LevelSelectionScenePath,
                "Assets/_Game/Scenes/Levels/Level_01.unity",
            };

            var scenes = new EditorBuildSettingsScene[orderedPaths.Length];
            for (int i = 0; i < orderedPaths.Length; i++)
            {
                scenes[i] = new EditorBuildSettingsScene(orderedPaths[i], true);
            }

            EditorBuildSettings.scenes = scenes;
        }

        private static Camera EnsureBootstrapUICameraInScene(Scene scene)
        {
            GameObject[] roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                if (roots[i].name == LoadingOverlayCameraUtility.BootstrapUICameraName)
                {
                    Camera existingCamera = roots[i].GetComponent<Camera>();
                    if (existingCamera != null)
                    {
                        return existingCamera;
                    }
                }
            }

            Camera bootstrapCamera = LoadingOverlayCameraUtility.CreateOverlayCamera(
                null,
                LoadingOverlayCameraUtility.BootstrapUICameraName);
            SceneManager.MoveGameObjectToScene(bootstrapCamera.gameObject, scene);
            return bootstrapCamera;
        }

        private static void WireBootstrapLoadingController(
            BootstrapLoadingController bootstrapController,
            LoadingOverlayView loadingOverlay,
            LevelCatalog catalog,
            PlayerProfileDefaults profileDefaults,
            Camera bootstrapUICamera = null)
        {
            var serializedController = new SerializedObject(bootstrapController);
            serializedController.FindProperty("_loadingOverlay").objectReferenceValue = loadingOverlay;
            serializedController.FindProperty("_levelCatalog").objectReferenceValue = catalog;
            serializedController.FindProperty("_playerProfileDefaults").objectReferenceValue = profileDefaults;
            serializedController.FindProperty("_bootstrapUICamera").objectReferenceValue = bootstrapUICamera;
            serializedController.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(bootstrapController);
        }

        private static Transform FindEmbeddedLoadingCanvasInScene()
        {
            GameObject[] roots = SceneManager.GetActiveScene().GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                Transform[] children = roots[i].GetComponentsInChildren<Transform>(true);
                for (int j = 0; j < children.Length; j++)
                {
                    if (children[j].name == "LoadingCanvas")
                    {
                        return children[j];
                    }
                }
            }

            return null;
        }

        internal static GameObject LoadLoadingOverlayPrefabRoot()
        {
            return AssetDatabase.LoadAssetAtPath<GameObject>(LoadingOverlayPrefabPath);
        }

        internal static void EnsureLoadingOverlayPrefabExists()
        {
            GameObject prefabRoot = LoadLoadingOverlayPrefabRoot();
            if (prefabRoot != null && prefabRoot.GetComponent<LoadingOverlayView>() != null)
            {
                return;
            }

            if (prefabRoot != null)
            {
                Debug.LogWarning("[AlienDefense Setup] LoadingOverlay prefab exists but has no LoadingOverlayView; rebuilding it.");
            }

            EditorFolderUtility.EnsureFolder("Assets/_Game/Prefabs/UI");

            LoadingOverlayView view = BuildLoadingOverlayHierarchy();
            PrefabUtility.SaveAsPrefabAsset(view.gameObject, LoadingOverlayPrefabPath);
            Object.DestroyImmediate(view.gameObject);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(LoadingOverlayPrefabPath, ImportAssetOptions.ForceUpdate);
        }

        private static LoadingOverlayView BuildLoadingOverlayHierarchy()
        {
            (GameObject canvasObject, Transform safeArea) = EditorCanvasUtility.BuildCanvasWithSafeArea("LoadingCanvas");

            Image background = BuildCoverBackgroundImage(canvasObject.transform, "Background", new Color(0.03f, 0.03f, 0.05f, 1f));

            BuildCornerText(safeArea, "VersionText", "Version: 0");

            Image logo = BuildAnchoredImage(safeArea, "Logo", new Vector2(0.5f, 0f), new Vector2(600f, 160f), new Vector2(0f, 300f), Color.clear);

            (RectTransform track, Image fillImage, RectTransform handle) = BuildLoadingProgressBar(safeArea);

            TMP_Text percentageText = BuildBottomText(safeArea, "PercentageText", "Đang tải 0.0 %", 110f);
            TMP_Text loadingText = LevelSceneScaffolder.CreateTMPText(safeArea, "LoadingText", "Loading...", 0f, 60f, 32f, TextAlignmentOptions.Center);
            loadingText.gameObject.SetActive(false);

            var canvasGroup = safeArea.gameObject.AddComponent<CanvasGroup>();

            Canvas canvas = canvasObject.GetComponent<Canvas>();
            if (canvas != null)
            {
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
            }

            var view = canvasObject.AddComponent<LoadingOverlayView>();
            var serializedView = new SerializedObject(view);
            serializedView.FindProperty("_canvasGroup").objectReferenceValue = canvasGroup;
            serializedView.FindProperty("_loadingText").objectReferenceValue = loadingText;
            serializedView.FindProperty("_progressFillImage").objectReferenceValue = fillImage;
            serializedView.FindProperty("_backgroundImage").objectReferenceValue = background;
            serializedView.FindProperty("_logoImage").objectReferenceValue = logo;
            serializedView.FindProperty("_percentageText").objectReferenceValue = percentageText;
            serializedView.FindProperty("_progressBarTrack").objectReferenceValue = track;
            serializedView.FindProperty("_progressHandle").objectReferenceValue = handle;
            serializedView.ApplyModifiedPropertiesWithoutUndo();

            canvasObject.transform.localScale = Vector3.one;

            return view;
        }

        private static Image BuildCoverBackgroundImage(Transform parent, string name, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(LoadingBackgroundCover));
            go.transform.SetParent(parent, false);
            go.transform.SetAsFirstSibling();
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(1080f, 1920f);
            var image = go.GetComponent<Image>();
            image.color = color;
            image.preserveAspect = true;
            image.raycastTarget = false;
            return image;
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

        private static TMP_Text BuildCornerText(Transform parent, string name, string initialText)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(1f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 1f);
            rect.anchoredPosition = new Vector2(-32f, -40f);
            rect.sizeDelta = new Vector2(360f, 48f);

            var text = go.AddComponent<TextMeshProUGUI>();
            text.text = initialText;
            text.fontSize = 28f;
            text.fontStyle = FontStyles.Bold;
            text.alignment = TextAlignmentOptions.TopRight;
            text.color = Color.white;
            return text;
        }

        private static TMP_Text BuildBottomText(Transform parent, string name, string initialText, float yOffsetFromBottom)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = new Vector2(0f, yOffsetFromBottom);
            rect.sizeDelta = new Vector2(700f, 44f);

            var text = go.AddComponent<TextMeshProUGUI>();
            text.text = initialText;
            text.fontSize = 30f;
            text.fontStyle = FontStyles.Bold;
            text.alignment = TextAlignmentOptions.Center;
            text.color = Color.white;
            return text;
        }

        private static (RectTransform track, Image fill, RectTransform handle) BuildLoadingProgressBar(Transform parent)
        {
            var track = new GameObject("ProgressBarTrack", typeof(RectTransform), typeof(Image));
            track.transform.SetParent(parent, false);
            var trackRect = track.GetComponent<RectTransform>();
            trackRect.anchorMin = new Vector2(0.5f, 0f);
            trackRect.anchorMax = new Vector2(0.5f, 0f);
            trackRect.pivot = new Vector2(0.5f, 0f);
            trackRect.anchoredPosition = new Vector2(0f, 170f);
            trackRect.sizeDelta = new Vector2(760f, 28f);
            var trackImage = track.GetComponent<Image>();
            trackImage.color = new Color(0f, 0f, 0f, 0.4f);
            trackImage.raycastTarget = false;

            var fillGo = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            fillGo.transform.SetParent(track.transform, false);
            var fillRect = fillGo.GetComponent<RectTransform>();
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;
            var fillImage = fillGo.GetComponent<Image>();
            fillImage.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            fillImage.color = new Color(0.35f, 0.9f, 0.5f);
            fillImage.type = Image.Type.Filled;
            fillImage.fillMethod = Image.FillMethod.Horizontal;
            fillImage.fillAmount = 0f;
            fillImage.raycastTarget = false;

            var handleGo = new GameObject("Handle", typeof(RectTransform), typeof(Image));
            handleGo.transform.SetParent(track.transform, false);
            var handleRect = handleGo.GetComponent<RectTransform>();
            handleRect.anchorMin = new Vector2(0f, 0.5f);
            handleRect.anchorMax = new Vector2(0f, 0.5f);
            handleRect.pivot = new Vector2(0.5f, 0.5f);
            handleRect.sizeDelta = new Vector2(64f, 64f);
            handleRect.anchoredPosition = Vector2.zero;
            var handleImage = handleGo.GetComponent<Image>();
            handleImage.color = Color.clear;
            handleImage.raycastTarget = false;
            handleGo.AddComponent<UIHoverBobAnimation>();

            return (trackRect, fillImage, handleRect);
        }
    }

    /// <summary>Adds or updates SceneServicesHost + SceneTransitionService on menu and level scenes.</summary>
    internal static class SceneServicesHostScaffolder
    {
        private const string LoadingOverlayPrefabPath = "Assets/_Game/Prefabs/UI/LoadingOverlay.prefab";

        public static void EnsureInActiveScene(bool includeSaveDebugControls)
        {
            EnsureInScene(SceneManager.GetActiveScene(), includeSaveDebugControls);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        }

        public static void EnsureInSceneAtPath(string scenePath, bool includeSaveDebugControls)
        {
            if (!System.IO.File.Exists(scenePath))
            {
                return;
            }

            Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            EnsureInScene(scene, includeSaveDebugControls);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        private static void EnsureInScene(Scene scene, bool includeSaveDebugControls)
        {
            BootstrapSceneScaffolder.EnsureLoadingOverlayPrefabExists();

            GameObject prefabRoot = AssetDatabase.LoadAssetAtPath<GameObject>(LoadingOverlayPrefabPath);
            LoadingOverlayView loadingOverlayPrefab = prefabRoot != null ? prefabRoot.GetComponent<LoadingOverlayView>() : null;
            if (loadingOverlayPrefab == null)
            {
                Debug.LogError("[AlienDefense Setup] LoadingOverlay prefab not found at " + LoadingOverlayPrefabPath + ".");
                return;
            }

            SceneServicesHost existingHost = Object.FindFirstObjectByType<SceneServicesHost>();
            GameObject hostObject;
            if (existingHost != null)
            {
                hostObject = existingHost.gameObject;
            }
            else
            {
                hostObject = new GameObject("SceneServicesHost");
            }

            SceneTransitionService sceneTransition = hostObject.GetComponent<SceneTransitionService>();
            if (sceneTransition == null)
            {
                sceneTransition = hostObject.AddComponent<SceneTransitionService>();
            }

            SceneServicesHost sceneServicesHost = hostObject.GetComponent<SceneServicesHost>();
            if (sceneServicesHost == null)
            {
                sceneServicesHost = hostObject.AddComponent<SceneServicesHost>();
            }

            SaveDebugControls saveDebugControls = null;
            if (includeSaveDebugControls)
            {
                saveDebugControls = hostObject.GetComponent<SaveDebugControls>();
                if (saveDebugControls == null)
                {
                    saveDebugControls = hostObject.AddComponent<SaveDebugControls>();
                }
            }

            WireSceneTransition(sceneTransition, loadingOverlayPrefab);
            WireSceneServicesHost(sceneServicesHost, sceneTransition, saveDebugControls);
        }

        private static void WireSceneTransition(SceneTransitionService sceneTransition, LoadingOverlayView prefab)
        {
            var serializedTransition = new SerializedObject(sceneTransition);
            serializedTransition.FindProperty("_loadingOverlayPrefab").objectReferenceValue = prefab;
            serializedTransition.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(sceneTransition);
        }

        private static void WireSceneServicesHost(
            SceneServicesHost sceneServicesHost,
            SceneTransitionService sceneTransition,
            SaveDebugControls saveDebugControls)
        {
            var serializedHost = new SerializedObject(sceneServicesHost);
            serializedHost.FindProperty("_sceneTransition").objectReferenceValue = sceneTransition;
            serializedHost.FindProperty("_saveDebugControls").objectReferenceValue = saveDebugControls;
            serializedHost.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(sceneServicesHost);
        }
    }
}
