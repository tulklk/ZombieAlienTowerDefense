using AlienDefense.CameraSystem;
using AlienDefense.Common;
using AlienDefense.Core;
using AlienDefense.Data;
using AlienDefense.DebugTools;
using AlienDefense.Enemies;
using AlienDefense.Player;
using AlienDefense.UI;
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
    /// <summary>Builds the Level_01 scene skeleton and its default LevelDefinition asset.</summary>
    internal static class LevelSceneScaffolder
    {
        private const string LevelDataFolder = "Assets/_Game/Data/Levels";
        private const string LevelDefinitionPath = LevelDataFolder + "/Level_01_Definition.asset";

        private const string LevelSceneFolder = "Assets/_Game/Scenes/Levels";
        private const string LevelScenePath = LevelSceneFolder + "/Level_01.unity";

        private const string GroundLayerName = "Ground";
        private static readonly Vector3 PlayerStartPosition = new Vector3(0f, 0f, -18f);

        private static readonly Vector3[] EnemyPathPoints =
        {
            new Vector3(0f, 0f, 20f),
            new Vector3(0f, 0f, 10f),
            new Vector3(0f, 0f, 0f),
            new Vector3(0f, 0f, -10f),
            new Vector3(0f, 0f, -20f)
        };

        private static readonly string[] EnemyWaypointNames =
        {
            "Waypoint_00", "Waypoint_01", "Waypoint_02", "Waypoint_03", "Waypoint_End"
        };

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
            EnemyDefinition normalEnemyDefinition = EnemyPrefabBuilder.CreateAll();

            EditorFolderUtility.EnsureFolder(LevelSceneFolder);

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            LevelCompositionRoot compositionRoot = BuildCompositionRoot(levelDefinition);
            GameObject playerInstance = BuildPlayer(playerPrefab, playerDefinition);
            (LevelBounds levelBounds, EnemyPath3D enemyPath) = BuildEnvironment();
            Transform cameraTransform = BuildCameraRig(playerInstance, levelBounds);
            Transform enemyRuntimeParent = BuildRuntimeContainers();
            EnemyDebugSpawner debugSpawner = BuildSystems(normalEnemyDefinition, enemyPath);
            BuildCanvas();
            BuildEventSystem();

            WirePlayerLevelBounds(playerInstance, levelBounds);
            WireCompositionRootPlayer(compositionRoot, playerInstance);
            WireCompositionRootEnemySystem(compositionRoot, enemyRuntimeParent, cameraTransform, debugSpawner);

            EditorSceneManager.SaveScene(scene, LevelScenePath);

            Debug.Log("[AlienDefense Setup] Saved scene skeleton to " + LevelScenePath + ".");
        }

        private static LevelCompositionRoot BuildCompositionRoot(LevelDefinition levelDefinition)
        {
            var rootObject = new GameObject("CompositionRoot");
            var compositionRoot = rootObject.AddComponent<LevelCompositionRoot>();

            var serializedRoot = new SerializedObject(compositionRoot);
            serializedRoot.Update();
            serializedRoot.FindProperty("_levelDefinition").objectReferenceValue = levelDefinition;
            serializedRoot.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(compositionRoot);

            var verifyRoot = new SerializedObject(compositionRoot);
            if (verifyRoot.FindProperty("_levelDefinition").objectReferenceValue == null)
            {
                Debug.LogError("[AlienDefense Setup] CompositionRoot's Level Definition failed to wire. " +
                    "Select CompositionRoot in the Hierarchy and drag " + levelDefinition.name +
                    " into the Level Definition field manually, then save the scene.", rootObject);
            }

            return compositionRoot;
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

        private static Transform BuildCameraRig(GameObject playerInstance, LevelBounds levelBounds)
        {
            Transform followTarget = playerInstance.transform.Find("CameraFollowTarget");
            if (followTarget == null)
            {
                Debug.LogWarning("[AlienDefense Setup] UFO_Player has no CameraFollowTarget child; " +
                    "falling back to following the root transform.", playerInstance);
                followTarget = playerInstance.transform;
            }

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

            cameraObject.transform.localRotation = Quaternion.Euler(60f, 0f, 0f);

            var serializedController = new SerializedObject(controller);
            serializedController.FindProperty("_followTarget").objectReferenceValue = followTarget;
            serializedController.FindProperty("_cameraTransform").objectReferenceValue = cameraObject.transform;
            serializedController.FindProperty("_focusPointAnchor").objectReferenceValue = followTargetAnchor.transform;
            serializedController.FindProperty("_positionOffset").vector3Value = new Vector3(0f, 12f, -9f);
            serializedController.FindProperty("_movementDirectionSource").objectReferenceValue =
                playerInstance.GetComponent<PlayerController>();
            if (levelBounds != null)
            {
                serializedController.FindProperty("_levelBounds").objectReferenceValue = levelBounds;
            }
            serializedController.ApplyModifiedPropertiesWithoutUndo();

            return cameraObject.transform;
        }

        private static (LevelBounds bounds, EnemyPath3D path) BuildEnvironment()
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

            EnemyPath3D path = BuildEnemyPath(environment.transform);

            return (levelBounds, path);
        }

        private static EnemyPath3D BuildEnemyPath(Transform environmentParent)
        {
            var pathObject = new GameObject("EnemyPath");
            pathObject.transform.SetParent(environmentParent, false);

            var waypointTransforms = new Transform[EnemyPathPoints.Length];
            for (int i = 0; i < EnemyPathPoints.Length; i++)
            {
                var waypoint = new GameObject(EnemyWaypointNames[i]);
                waypoint.transform.SetParent(pathObject.transform, false);
                waypoint.transform.position = EnemyPathPoints[i];
                waypointTransforms[i] = waypoint.transform;
            }

            var path = pathObject.AddComponent<EnemyPath3D>();
            var serializedPath = new SerializedObject(path);
            SerializedProperty waypointsProperty = serializedPath.FindProperty("_waypoints");
            waypointsProperty.arraySize = waypointTransforms.Length;
            for (int i = 0; i < waypointTransforms.Length; i++)
            {
                waypointsProperty.GetArrayElementAtIndex(i).objectReferenceValue = waypointTransforms[i];
            }
            serializedPath.ApplyModifiedPropertiesWithoutUndo();

            var baseTarget = new GameObject("BaseTarget");
            baseTarget.transform.SetParent(environmentParent, false);
            baseTarget.transform.position = EnemyPathPoints[EnemyPathPoints.Length - 1];

            return path;
        }

        private static Transform BuildRuntimeContainers()
        {
            var runtime = new GameObject("Runtime");

            var enemies = new GameObject("Enemies");
            enemies.transform.SetParent(runtime.transform, false);

            new GameObject("Towers").transform.SetParent(runtime.transform, false);
            new GameObject("Projectiles").transform.SetParent(runtime.transform, false);
            new GameObject("VFX").transform.SetParent(runtime.transform, false);

            return enemies.transform;
        }

        private static EnemyDebugSpawner BuildSystems(EnemyDefinition debugDefinition, EnemyPath3D path)
        {
            var systems = new GameObject("Systems");

            var spawnerObject = new GameObject("EnemyDebugSpawner");
            spawnerObject.transform.SetParent(systems.transform, false);
            var spawner = spawnerObject.AddComponent<EnemyDebugSpawner>();

            var serializedSpawner = new SerializedObject(spawner);
            serializedSpawner.FindProperty("_definition").objectReferenceValue = debugDefinition;
            serializedSpawner.FindProperty("_path").objectReferenceValue = path;
            serializedSpawner.FindProperty("_autoSpawnOnPlay").boolValue = false;
            serializedSpawner.ApplyModifiedPropertiesWithoutUndo();

            return spawner;
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

            var safeAreaObject = new GameObject("SafeArea", typeof(RectTransform), typeof(SafeAreaFitter));
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

        private static void WireCompositionRootEnemySystem(
            LevelCompositionRoot compositionRoot,
            Transform enemyRuntimeParent,
            Transform cameraTransform,
            EnemyDebugSpawner debugSpawner)
        {
            var serialized = new SerializedObject(compositionRoot);
            serialized.FindProperty("_enemyRuntimeParent").objectReferenceValue = enemyRuntimeParent;
            serialized.FindProperty("_cameraTransform").objectReferenceValue = cameraTransform;
            serialized.FindProperty("_debugSpawner").objectReferenceValue = debugSpawner;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
