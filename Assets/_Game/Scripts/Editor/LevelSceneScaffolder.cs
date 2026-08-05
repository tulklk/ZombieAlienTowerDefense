using AlienDefense.CameraSystem;
using AlienDefense.Common;
using AlienDefense.Core;
using AlienDefense.Data;
using AlienDefense.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.OnScreen;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace AlienDefense.EditorTools
{
    /// <summary>
    /// Builds the Level_01 scene skeleton (composition root, UFO_Player instance, top-down camera
    /// rig, environment, runtime containers, portrait Canvas with a virtual joystick, EventSystem)
    /// and the matching default LevelDefinition asset, all through Editor APIs so GameObjects/
    /// components come out correctly serialized.
    ///
    /// EnemyPath and BuildNodes are added in Phase 3. Re-running "Build Level_01 Scene Skeleton"
    /// overwrites the scene file, so hand edits made in the Editor after running it will be lost
    /// if you run it again.
    /// </summary>
    internal static class LevelSceneScaffolder
    {
        private const string LevelDataFolder = "Assets/_Game/Data/Levels";
        private const string LevelDefinitionPath = LevelDataFolder + "/Level_01_Definition.asset";

        private const string LevelSceneFolder = "Assets/_Game/Scenes/Levels";
        private const string LevelScenePath = LevelSceneFolder + "/Level_01.unity";

        private const string GroundLayerName = "Ground";
        private static readonly Vector3 PlayerStartPosition = new Vector3(0f, 0f, -18f);

        [MenuItem("AlienDefense/Setup/3. Create Default LevelDefinition Asset")]
        public static LevelDefinition CreateDefaultLevelDefinition()
        {
            var existing = AssetDatabase.LoadAssetAtPath<LevelDefinition>(LevelDefinitionPath);
            if (existing != null)
            {
                Debug.Log("[AlienDefense Setup] LevelDefinition already exists at " + LevelDefinitionPath + ", reusing it.");
                return existing;
            }

            EditorFolderUtility.EnsureFolder(LevelDataFolder);

            var definition = ScriptableObject.CreateInstance<LevelDefinition>();
            AssetDatabase.CreateAsset(definition, LevelDefinitionPath);
            AssetDatabase.SaveAssets();

            Debug.Log("[AlienDefense Setup] Created " + LevelDefinitionPath + " (starting resource 250, base health 20).");
            return definition;
        }

        [MenuItem("AlienDefense/Setup/4. Build Level_01 Scene Skeleton")]
        public static void BuildLevel01SceneSkeleton()
        {
            LevelDefinition levelDefinition = CreateDefaultLevelDefinition();
            PlayerDefinition playerDefinition = PlayerPrefabBuilder.CreateOrLoadPlayerDefinition();
            GameObject playerPrefab = PlayerPrefabBuilder.CreateOrLoadPrefab();

            EditorFolderUtility.EnsureFolder(LevelSceneFolder);

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            LevelCompositionRoot compositionRoot = BuildCompositionRoot(levelDefinition);
            BuildSystemsPlaceholder();
            GameObject playerInstance = BuildPlayer(playerPrefab, playerDefinition);
            LevelBounds levelBounds = BuildEnvironment();
            BuildCameraRig(playerInstance.transform, levelBounds);
            BuildRuntimeContainers();
            BuildCanvas();
            BuildEventSystem();

            WirePlayerLevelBounds(playerInstance, levelBounds);
            WireCompositionRootPlayer(compositionRoot, playerInstance);

            EditorSceneManager.SaveScene(scene, LevelScenePath);

            Debug.Log("[AlienDefense Setup] Saved scene skeleton to " + LevelScenePath +
                      ". EnemyPath and BuildNodes are added in Phase 3.");
        }

        private static LevelCompositionRoot BuildCompositionRoot(LevelDefinition levelDefinition)
        {
            var rootObject = new GameObject("CompositionRoot");
            var compositionRoot = rootObject.AddComponent<LevelCompositionRoot>();

            var serializedRoot = new SerializedObject(compositionRoot);
            serializedRoot.FindProperty("_levelDefinition").objectReferenceValue = levelDefinition;
            serializedRoot.ApplyModifiedPropertiesWithoutUndo();

            return compositionRoot;
        }

        private static void BuildSystemsPlaceholder()
        {
            // Populated in later phases: WaveController, BuildService, EnemyRegistry, factories, PoolRoot.
            new GameObject("Systems");
        }

        private static GameObject BuildPlayer(GameObject playerPrefab, PlayerDefinition playerDefinition)
        {
            var playerParent = new GameObject("Player");

            GameObject instance;
            if (playerPrefab != null)
            {
                instance = (GameObject)PrefabUtility.InstantiatePrefab(playerPrefab);
                instance.transform.SetParent(playerParent.transform, false);
            }
            else
            {
                Debug.LogError("[AlienDefense Setup] UFO_Player prefab missing; Player parent left empty.");
                return playerParent;
            }

            float hoverHeight = playerDefinition != null ? playerDefinition.HoverHeight : 1.5f;
            instance.transform.position = PlayerStartPosition + new Vector3(0f, hoverHeight, 0f);

            return instance;
        }

        private static void BuildCameraRig(Transform followTarget, LevelBounds levelBounds)
        {
            var rig = new GameObject("CameraRig");
            var controller = rig.AddComponent<TopDownCameraController>();

            var followTargetAnchor = new GameObject("FollowTarget");
            followTargetAnchor.transform.SetParent(rig.transform, false);

            var cameraObject = new GameObject("Main Camera");
            cameraObject.transform.SetParent(rig.transform, false);
            cameraObject.tag = "MainCamera";

            var camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = false;
            camera.fieldOfView = 40f;
            camera.nearClipPlane = 0.3f;
            camera.farClipPlane = 100f;

            cameraObject.AddComponent<AudioListener>();

            // Reference top-down composition: offset (0, 12, -9), pitch 60deg. Rotation is fixed
            // by TopDownCameraController design (no free rotation in MVP) — set once here.
            cameraObject.transform.localRotation = Quaternion.Euler(60f, 0f, 0f);

            var serializedController = new SerializedObject(controller);
            serializedController.FindProperty("_followTarget").objectReferenceValue = followTarget;
            serializedController.FindProperty("_cameraTransform").objectReferenceValue = cameraObject.transform;
            serializedController.FindProperty("_focusPointAnchor").objectReferenceValue = followTargetAnchor.transform;
            serializedController.FindProperty("_positionOffset").vector3Value = new Vector3(0f, 12f, -9f);
            serializedController.FindProperty("_movementDirectionSource").objectReferenceValue =
                followTarget.GetComponent<PlayerController>();
            if (levelBounds != null)
            {
                serializedController.FindProperty("_levelBounds").objectReferenceValue = levelBounds;
            }
            serializedController.ApplyModifiedPropertiesWithoutUndo();
        }

        private static LevelBounds BuildEnvironment()
        {
            var environment = new GameObject("Environment");

            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.SetParent(environment.transform, false);
            ground.transform.localScale = new Vector3(5f, 1f, 5f); // 50x50 units

            int groundLayer = LayerMask.NameToLayer(GroundLayerName);
            if (groundLayer >= 0)
            {
                ground.layer = groundLayer;
            }
            else
            {
                Debug.LogWarning($"[AlienDefense Setup] Layer '{GroundLayerName}' not found; leaving Ground on Default layer.");
            }

            var lightingObject = new GameObject("Lighting");
            lightingObject.transform.SetParent(environment.transform, false);
            var light = lightingObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(1f, 0.96f, 0.88f);
            light.intensity = 1.15f;
            light.shadows = LightShadows.Soft;
            lightingObject.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            var levelBoundsObject = new GameObject("LevelBounds");
            levelBoundsObject.transform.SetParent(environment.transform, false);
            var levelBounds = levelBoundsObject.AddComponent<LevelBounds>();

            return levelBounds;
        }

        private static void BuildRuntimeContainers()
        {
            var runtime = new GameObject("Runtime");
            new GameObject("Enemies").transform.SetParent(runtime.transform, false);
            new GameObject("Towers").transform.SetParent(runtime.transform, false);
            new GameObject("Projectiles").transform.SetParent(runtime.transform, false);
            new GameObject("VFX").transform.SetParent(runtime.transform, false);
        }

        private static void BuildCanvas()
        {
            var canvasObject = new GameObject("Canvas");
            var canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            canvasObject.AddComponent<GraphicRaycaster>();

            var safeAreaObject = new GameObject("SafeArea", typeof(RectTransform));
            safeAreaObject.transform.SetParent(canvasObject.transform, false);
            var safeAreaRect = safeAreaObject.GetComponent<RectTransform>();
            safeAreaRect.anchorMin = Vector2.zero;
            safeAreaRect.anchorMax = Vector2.one;
            safeAreaRect.offsetMin = Vector2.zero;
            safeAreaRect.offsetMax = Vector2.zero;

            var bottomControls = new GameObject("BottomControls", typeof(RectTransform));
            bottomControls.transform.SetParent(safeAreaObject.transform, false);
            var bottomRect = bottomControls.GetComponent<RectTransform>();
            bottomRect.anchorMin = Vector2.zero;
            bottomRect.anchorMax = Vector2.one;
            bottomRect.offsetMin = Vector2.zero;
            bottomRect.offsetMax = Vector2.zero;

            BuildMovementJoystick(bottomControls.transform);
        }

        private static void BuildMovementJoystick(Transform parent)
        {
            var background = new GameObject("MovementJoystick", typeof(RectTransform), typeof(Image));
            background.transform.SetParent(parent, false);
            var backgroundRect = background.GetComponent<RectTransform>();
            backgroundRect.anchorMin = Vector2.zero;
            backgroundRect.anchorMax = Vector2.zero;
            backgroundRect.pivot = new Vector2(0.5f, 0.5f);
            backgroundRect.anchoredPosition = new Vector2(220f, 260f);
            backgroundRect.sizeDelta = new Vector2(260f, 260f);

            var backgroundImage = background.GetComponent<Image>();
            backgroundImage.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Background.psd");
            backgroundImage.type = Image.Type.Simple;
            backgroundImage.color = new Color(1f, 1f, 1f, 0.35f);

            var handle = new GameObject("Handle", typeof(RectTransform), typeof(Image), typeof(OnScreenStick));
            handle.transform.SetParent(background.transform, false);
            var handleRect = handle.GetComponent<RectTransform>();
            handleRect.anchorMin = new Vector2(0.5f, 0.5f);
            handleRect.anchorMax = new Vector2(0.5f, 0.5f);
            handleRect.pivot = new Vector2(0.5f, 0.5f);
            handleRect.anchoredPosition = Vector2.zero;
            handleRect.sizeDelta = new Vector2(120f, 120f);

            var handleImage = handle.GetComponent<Image>();
            handleImage.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
            handleImage.color = Color.white;

            var onScreenStick = handle.GetComponent<OnScreenStick>();
            onScreenStick.controlPath = "<Gamepad>/leftStick";
        }

        private static void BuildEventSystem()
        {
            var eventSystemObject = new GameObject("EventSystem");
            eventSystemObject.AddComponent<EventSystem>();
            eventSystemObject.AddComponent<InputSystemUIInputModule>();
        }

        private static void WirePlayerLevelBounds(GameObject playerInstance, LevelBounds levelBounds)
        {
            var playerController = playerInstance.GetComponent<PlayerController>();
            if (playerController == null || levelBounds == null)
            {
                return;
            }

            var serialized = new SerializedObject(playerController);
            serialized.FindProperty("_levelBounds").objectReferenceValue = levelBounds;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void WireCompositionRootPlayer(LevelCompositionRoot compositionRoot, GameObject playerInstance)
        {
            var playerController = playerInstance.GetComponent<PlayerController>();
            if (playerController == null)
            {
                return;
            }

            var serialized = new SerializedObject(compositionRoot);
            serialized.FindProperty("_player").objectReferenceValue = playerController;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
