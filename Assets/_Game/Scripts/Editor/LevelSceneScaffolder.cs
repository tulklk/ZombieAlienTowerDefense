using AlienDefense.Audio;
using AlienDefense.Building;
using AlienDefense.CameraSystem;
using AlienDefense.Combat;
using AlienDefense.Common;
using AlienDefense.Core;
using AlienDefense.Data;
using AlienDefense.DebugTools;
using AlienDefense.Enemies;
using AlienDefense.Player;
using AlienDefense.Towers;
using AlienDefense.UI;
using AlienDefense.Vfx;
using AlienDefense.Waves;
using TMPro;
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
        private const string BuildNodeLayerName = "BuildNode";
        private const string TowerLayerName = "Tower";
        private static readonly Vector3 PlayerStartPosition = new Vector3(0f, 0f, -18f);

        private static readonly Vector3[] BuildNodePositions =
        {
            new Vector3(-3f, 0f, 15f), new Vector3(3f, 0f, 15f),
            new Vector3(-3f, 0f, 8f), new Vector3(3f, 0f, 8f),
            new Vector3(-3f, 0f, 0f), new Vector3(3f, 0f, 0f),
            new Vector3(-3f, 0f, -8f), new Vector3(3f, 0f, -8f),
            new Vector3(-3f, 0f, -15f), new Vector3(3f, 0f, -15f)
        };

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
            WaveDefinition[] waves = WaveDefinitionBuilder.CreateAll();
            ProjectileDefinition projectileDefinition = ProjectilePrefabBuilder.CreateOrLoad();
            TowerDefinition[] towerDefinitions = TowerPrefabBuilder.CreateAll();
            GameObject buildNodePrefab = BuildNodePrefabBuilder.CreateOrLoad();
            VfxPrefabBuilder.CreateAll();
            playerDefinition = WirePlayerDefinitionProjectile(playerDefinition, projectileDefinition);
            levelDefinition = WireLevelDefinitionWaves(levelDefinition, waves);
            if (levelDefinition == null)
            {
                Debug.LogError("[AlienDefense Setup] Could not reload LevelDefinition after wiring waves; aborting.");
                return;
            }

            EditorFolderUtility.EnsureFolder(LevelSceneFolder);

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            LevelCompositionRoot compositionRoot = BuildCompositionRoot();
            GameObject playerInstance = BuildPlayer(playerPrefab, playerDefinition);
            (LevelBounds levelBounds, EnemyPath3D enemyPath, Transform towerSpawnPoint) = BuildEnvironment();
            Transform cameraTransform = BuildCameraRig(playerInstance, levelBounds);
            (Transform enemyRuntimeParent, Transform projectileRuntimeParent, Transform towerRuntimeParent, Transform vfxRuntimeParent) = BuildRuntimeContainers();
            AudioService audioService = BuildAudioService();
            BuildNode[] buildNodes = BuildBuildNodes(buildNodePrefab);
            towerDefinitions = TowerPrefabBuilder.LoadAll();
            (EnemyDebugSpawner debugSpawner, WaveController waveController, WaveDebugControls waveDebugControls, TowerDebugSpawner towerDebugSpawner,
                WorldSelectionController worldSelectionController, BuildNodeVisualCoordinator buildNodeVisualCoordinator) =
                BuildSystems(normalEnemyDefinition, enemyPath, towerDefinitions[0], towerSpawnPoint, cameraTransform, buildNodes);
            towerDefinitions = TowerPrefabBuilder.LoadAll();
            (BuildBarPresenter buildBarPresenter, TowerDetailsPresenter towerDetailsPresenter, GameHUDPresenter gameHUDPresenter, GameStateUIController gameStateUIController) =
                BuildCanvas(waveController, towerDefinitions);
            BuildEventSystem();

            WirePlayerLevelBounds(playerInstance, levelBounds);
            WireCompositionRootPlayer(compositionRoot, playerInstance);
            WireCompositionRootEnemySystem(compositionRoot, enemyRuntimeParent, cameraTransform, debugSpawner);
            WireCompositionRootWaveSystem(compositionRoot, waveController, waveDebugControls);
            WireCompositionRootCombatSystem(compositionRoot, playerInstance, projectileRuntimeParent);
            WireCompositionRootTowerSystem(compositionRoot, towerRuntimeParent, towerDebugSpawner);
            WireCompositionRootBuildSystem(compositionRoot, worldSelectionController, buildBarPresenter, buildNodeVisualCoordinator, towerDetailsPresenter);
            WireCompositionRootVfxSystem(compositionRoot, vfxRuntimeParent);
            WireCompositionRootAudioSystem(compositionRoot, audioService);
            WireCompositionRootGameFlowUI(compositionRoot, gameHUDPresenter, gameStateUIController);

            EditorSceneManager.SaveScene(scene, LevelScenePath);

            Debug.Log("[AlienDefense Setup] Saved scene skeleton to " + LevelScenePath + ".");
        }

        private static PlayerDefinition WirePlayerDefinitionProjectile(PlayerDefinition playerDefinition, ProjectileDefinition projectileDefinition)
        {
            var serialized = new SerializedObject(playerDefinition);
            serialized.FindProperty("_projectileDefinition").objectReferenceValue = projectileDefinition;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssets();

            return AssetDatabase.LoadAssetAtPath<PlayerDefinition>(AssetDatabase.GetAssetPath(playerDefinition));
        }

        private static LevelDefinition WireLevelDefinitionWaves(LevelDefinition levelDefinition, WaveDefinition[] waves)
        {
            var serialized = new SerializedObject(levelDefinition);
            SerializedProperty wavesProperty = serialized.FindProperty("_waves");
            wavesProperty.arraySize = waves.Length;
            for (int i = 0; i < waves.Length; i++)
            {
                wavesProperty.GetArrayElementAtIndex(i).objectReferenceValue = waves[i];
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssets();

            return AssetDatabase.LoadAssetAtPath<LevelDefinition>(LevelDefinitionPath);
        }

        private static LevelCompositionRoot BuildCompositionRoot()
        {
            LevelDefinition levelDefinition = AssetDatabase.LoadAssetAtPath<LevelDefinition>(LevelDefinitionPath);

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
                string assetName = levelDefinition != null ? levelDefinition.name : "the LevelDefinition asset";
                Debug.LogError("[AlienDefense Setup] CompositionRoot's Level Definition failed to wire. " +
                    "Select CompositionRoot in the Hierarchy and drag " + assetName +
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

            var positionOffset = new Vector3(0f, 20f, -15f);
            cameraObject.transform.localRotation = Quaternion.LookRotation((-positionOffset).normalized, Vector3.up);

            var serializedController = new SerializedObject(controller);
            serializedController.FindProperty("_followTarget").objectReferenceValue = followTarget;
            serializedController.FindProperty("_cameraTransform").objectReferenceValue = cameraObject.transform;
            serializedController.FindProperty("_focusPointAnchor").objectReferenceValue = followTargetAnchor.transform;
            serializedController.FindProperty("_positionOffset").vector3Value = positionOffset;
            serializedController.FindProperty("_movementDirectionSource").objectReferenceValue =
                playerInstance.GetComponent<PlayerController>();
            if (levelBounds != null)
            {
                serializedController.FindProperty("_levelBounds").objectReferenceValue = levelBounds;
            }
            serializedController.ApplyModifiedPropertiesWithoutUndo();

            return cameraObject.transform;
        }

        private static (LevelBounds bounds, EnemyPath3D path, Transform towerSpawnPoint) BuildEnvironment()
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
            light.shadows = LightShadows.Hard;
            lightingObject.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            var levelBoundsObject = new GameObject("LevelBounds");
            levelBoundsObject.transform.SetParent(environment.transform, false);
            var levelBounds = levelBoundsObject.AddComponent<LevelBounds>();

            EnemyPath3D path = BuildEnemyPath(environment.transform);

            var towerSpawnPointObject = new GameObject("TowerSpawnPoint_Debug");
            towerSpawnPointObject.transform.SetParent(environment.transform, false);
            towerSpawnPointObject.transform.position = new Vector3(2.5f, 0f, 0f);

            return (levelBounds, path, towerSpawnPointObject.transform);
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

            bool previousLogEnabled = Debug.unityLogger.logEnabled;
            Debug.unityLogger.logEnabled = false;
            var path = pathObject.AddComponent<EnemyPath3D>();
            Debug.unityLogger.logEnabled = previousLogEnabled;

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

        private static (Transform enemies, Transform projectiles, Transform towers, Transform vfx) BuildRuntimeContainers()
        {
            var runtime = new GameObject("Runtime");

            var enemies = new GameObject("Enemies");
            enemies.transform.SetParent(runtime.transform, false);

            var towers = new GameObject("Towers");
            towers.transform.SetParent(runtime.transform, false);

            var projectiles = new GameObject("Projectiles");
            projectiles.transform.SetParent(runtime.transform, false);

            var vfx = new GameObject("VFX");
            vfx.transform.SetParent(runtime.transform, false);

            return (enemies.transform, projectiles.transform, towers.transform, vfx.transform);
        }

        private static BuildNode[] BuildBuildNodes(GameObject buildNodePrefab)
        {
            if (buildNodePrefab == null)
            {
                Debug.LogError("[AlienDefense Setup] BuildNode prefab missing; no build nodes created.");
                return new BuildNode[0];
            }

            var buildNodesParent = new GameObject("BuildNodes");
            var nodes = new BuildNode[BuildNodePositions.Length];

            for (int i = 0; i < BuildNodePositions.Length; i++)
            {
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(buildNodePrefab);
                instance.transform.SetParent(buildNodesParent.transform, false);
                instance.transform.position = BuildNodePositions[i];
                instance.name = $"BuildNode_{i:00}";
                nodes[i] = instance.GetComponent<BuildNode>();
            }

            return nodes;
        }

        private static (EnemyDebugSpawner debugSpawner, WaveController waveController, WaveDebugControls waveDebugControls, TowerDebugSpawner towerDebugSpawner,
            WorldSelectionController worldSelectionController, BuildNodeVisualCoordinator buildNodeVisualCoordinator) BuildSystems(
            EnemyDefinition debugDefinition, EnemyPath3D path, TowerDefinition towerDefinition, Transform towerSpawnPoint,
            Transform cameraTransform, BuildNode[] buildNodes)
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

            var waveControllerObject = new GameObject("WaveController");
            waveControllerObject.transform.SetParent(systems.transform, false);
            var waveController = waveControllerObject.AddComponent<WaveController>();

            var serializedWaveController = new SerializedObject(waveController);
            serializedWaveController.FindProperty("_path").objectReferenceValue = path;
            serializedWaveController.FindProperty("_autoStartFirstWave").boolValue = true;
            serializedWaveController.FindProperty("_autoStartNextWaves").boolValue = true;
            serializedWaveController.ApplyModifiedPropertiesWithoutUndo();

            var waveDebugObject = new GameObject("WaveDebugControls");
            waveDebugObject.transform.SetParent(systems.transform, false);
            var waveDebugControls = waveDebugObject.AddComponent<WaveDebugControls>();
            var serializedWaveDebug = new SerializedObject(waveDebugControls);
            serializedWaveDebug.FindProperty("_waveController").objectReferenceValue = waveController;
            serializedWaveDebug.ApplyModifiedPropertiesWithoutUndo();

            var towerSpawnerObject = new GameObject("TowerDebugSpawner");
            towerSpawnerObject.transform.SetParent(systems.transform, false);
            var towerSpawner = towerSpawnerObject.AddComponent<TowerDebugSpawner>();

            var serializedTowerSpawner = new SerializedObject(towerSpawner);
            serializedTowerSpawner.FindProperty("_definition").objectReferenceValue = towerDefinition;
            serializedTowerSpawner.FindProperty("_spawnPoint").objectReferenceValue = towerSpawnPoint;
            serializedTowerSpawner.FindProperty("_autoSpawnOnPlay").boolValue = false;
            serializedTowerSpawner.ApplyModifiedPropertiesWithoutUndo();

            var worldSelectionObject = new GameObject("WorldSelectionController");
            worldSelectionObject.transform.SetParent(systems.transform, false);
            var worldSelectionController = worldSelectionObject.AddComponent<WorldSelectionController>();

            int buildNodeLayer = LayerMask.NameToLayer(BuildNodeLayerName);
            int towerLayer = LayerMask.NameToLayer(TowerLayerName);
            if (buildNodeLayer < 0 || towerLayer < 0)
            {
                Debug.LogWarning($"[AlienDefense Setup] Layer '{BuildNodeLayerName}' or '{TowerLayerName}' not found; WorldSelectionController may not hit everything expected.");
            }

            int interactableMask = (buildNodeLayer >= 0 ? 1 << buildNodeLayer : 0) | (towerLayer >= 0 ? 1 << towerLayer : 0);

            var serializedWorldSelection = new SerializedObject(worldSelectionController);
            serializedWorldSelection.FindProperty("_worldCamera").objectReferenceValue =
                cameraTransform != null ? cameraTransform.GetComponent<Camera>() : null;
            serializedWorldSelection.FindProperty("_interactableLayerMask").intValue = interactableMask;
            serializedWorldSelection.ApplyModifiedPropertiesWithoutUndo();

            var buildCoordinatorObject = new GameObject("BuildNodeVisualCoordinator");
            buildCoordinatorObject.transform.SetParent(systems.transform, false);
            var buildNodeVisualCoordinator = buildCoordinatorObject.AddComponent<BuildNodeVisualCoordinator>();

            var serializedCoordinator = new SerializedObject(buildNodeVisualCoordinator);
            SerializedProperty nodesProperty = serializedCoordinator.FindProperty("_nodes");
            nodesProperty.arraySize = buildNodes.Length;
            for (int i = 0; i < buildNodes.Length; i++)
            {
                nodesProperty.GetArrayElementAtIndex(i).objectReferenceValue = buildNodes[i];
            }
            serializedCoordinator.ApplyModifiedPropertiesWithoutUndo();

            return (spawner, waveController, waveDebugControls, towerSpawner, worldSelectionController, buildNodeVisualCoordinator);
        }

        private static (BuildBarPresenter buildBarPresenter, TowerDetailsPresenter towerDetailsPresenter, GameHUDPresenter gameHUDPresenter, GameStateUIController gameStateUIController) BuildCanvas(WaveController waveController, TowerDefinition[] towerDefinitions)
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

            var topHUD = new GameObject("TopHUD", typeof(RectTransform));
            topHUD.transform.SetParent(safeAreaObject.transform, false);
            var topHUDRect = topHUD.GetComponent<RectTransform>();
            topHUDRect.anchorMin = new Vector2(0f, 1f);
            topHUDRect.anchorMax = new Vector2(1f, 1f);
            topHUDRect.pivot = new Vector2(0.5f, 1f);
            topHUDRect.anchoredPosition = Vector2.zero;
            topHUDRect.sizeDelta = new Vector2(0f, 320f);

            GameHUDView gameHUDView = BuildGameHUDPanel(topHUD.transform);
            GameHUDPresenter gameHUDPresenter = BuildGameHUDPresenter(topHUD.transform, gameHUDView);

            WaveHUDView waveHUDView = BuildWavePanel(topHUD.transform, 100f);
            BuildWaveHUDPresenter(topHUD.transform, waveController, waveHUDView);

            var bottomControls = new GameObject("BottomControls", typeof(RectTransform));
            bottomControls.transform.SetParent(safeAreaObject.transform, false);
            var bottomRect = bottomControls.GetComponent<RectTransform>();
            bottomRect.anchorMin = Vector2.zero;
            bottomRect.anchorMax = Vector2.one;
            bottomRect.offsetMin = Vector2.zero;
            bottomRect.offsetMax = Vector2.zero;

            BuildMovementJoystick(bottomControls.transform);

            var gameplayInteractionObject = new GameObject("GameplayInteractionGroup", typeof(RectTransform), typeof(CanvasGroup));
            gameplayInteractionObject.transform.SetParent(safeAreaObject.transform, false);
            var gameplayInteractionRect = gameplayInteractionObject.GetComponent<RectTransform>();
            gameplayInteractionRect.anchorMin = Vector2.zero;
            gameplayInteractionRect.anchorMax = Vector2.one;
            gameplayInteractionRect.offsetMin = Vector2.zero;
            gameplayInteractionRect.offsetMax = Vector2.zero;
            var gameplayInteractionGroup = gameplayInteractionObject.GetComponent<CanvasGroup>();

            BuildBarPresenter buildBarPresenter = BuildBuildPanel(gameplayInteractionObject.transform, towerDefinitions);
            TowerDetailsPresenter towerDetailsPresenter = BuildTowerDetailsPanel(gameplayInteractionObject.transform);

            PausePanelView pausePanelView = BuildPausePanel(safeAreaObject.transform);
            GameResultView victoryResultView = BuildGameResultPanel(safeAreaObject.transform, "VictoryPanel", "Victory!", new Color(0.3f, 0.85f, 0.35f));
            GameResultView defeatResultView = BuildGameResultPanel(safeAreaObject.transform, "DefeatPanel", "Defeat", new Color(0.85f, 0.3f, 0.3f));

            GameStateUIController gameStateUIController = BuildGameStateUIController(
                safeAreaObject.transform,
                pausePanelView.gameObject, pausePanelView,
                victoryResultView.gameObject, victoryResultView,
                defeatResultView.gameObject, defeatResultView,
                gameplayInteractionGroup);

            return (buildBarPresenter, towerDetailsPresenter, gameHUDPresenter, gameStateUIController);
        }

        private static GameHUDView BuildGameHUDPanel(Transform parent)
        {
            var panel = new GameObject("GameHUD", typeof(RectTransform));
            panel.transform.SetParent(parent, false);
            var panelRect = panel.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0f, 1f);
            panelRect.anchorMax = new Vector2(1f, 1f);
            panelRect.pivot = new Vector2(0.5f, 1f);
            panelRect.anchoredPosition = Vector2.zero;
            panelRect.sizeDelta = new Vector2(0f, 100f);

            TMP_Text resourceText = CreateAnchoredTMPText(panel.transform, "ResourceText", "0",
                new Vector2(0f, 1f), new Vector2(20f, -10f), new Vector2(220f, 44f), 28f, TextAlignmentOptions.MidlineLeft);

            (TMP_Text baseHealthText, Image baseHealthFillImage) = BuildBaseHealthDisplay(panel.transform);

            (Button speedButton, TMP_Text speedText) = BuildHUDIconButton(panel.transform, "SpeedButton", "x1", new Vector2(-140f, -10f));
            (Button pauseButton, TMP_Text _) = BuildHUDIconButton(panel.transform, "PauseButton", "II", new Vector2(-20f, -10f));

            var view = panel.AddComponent<GameHUDView>();
            var serializedView = new SerializedObject(view);
            serializedView.FindProperty("_resourceText").objectReferenceValue = resourceText;
            serializedView.FindProperty("_baseHealthText").objectReferenceValue = baseHealthText;
            serializedView.FindProperty("_baseHealthFillImage").objectReferenceValue = baseHealthFillImage;
            serializedView.FindProperty("_speedText").objectReferenceValue = speedText;
            serializedView.FindProperty("_speedButton").objectReferenceValue = speedButton;
            serializedView.FindProperty("_pauseButton").objectReferenceValue = pauseButton;
            serializedView.ApplyModifiedPropertiesWithoutUndo();

            return view;
        }

        private static TMP_Text CreateAnchoredTMPText(Transform parent, string name, string initialText,
            Vector2 anchor, Vector2 anchoredPosition, Vector2 sizeDelta, float fontSize, TextAlignmentOptions alignment)
        {
            var textObject = new GameObject(name, typeof(RectTransform));
            textObject.transform.SetParent(parent, false);
            var rect = textObject.GetComponent<RectTransform>();
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = anchor;
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = sizeDelta;

            var text = textObject.AddComponent<TextMeshProUGUI>();
            text.text = initialText;
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = Color.white;

            return text;
        }

        private static (TMP_Text text, Image fill) BuildBaseHealthDisplay(Transform parent)
        {
            TMP_Text text = CreateAnchoredTMPText(parent, "BaseHealthText", "0/0",
                new Vector2(0.5f, 1f), new Vector2(0f, -10f), new Vector2(240f, 30f), 26f, TextAlignmentOptions.Center);

            var background = new GameObject("BaseHealthFillBar", typeof(RectTransform), typeof(Image));
            background.transform.SetParent(parent, false);
            var backgroundRect = background.GetComponent<RectTransform>();
            backgroundRect.anchorMin = new Vector2(0.5f, 1f);
            backgroundRect.anchorMax = new Vector2(0.5f, 1f);
            backgroundRect.pivot = new Vector2(0.5f, 1f);
            backgroundRect.anchoredPosition = new Vector2(0f, -44f);
            backgroundRect.sizeDelta = new Vector2(200f, 12f);
            background.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.4f);

            var fill = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            fill.transform.SetParent(background.transform, false);
            var fillRect = fill.GetComponent<RectTransform>();
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;
            var fillImage = fill.GetComponent<Image>();
            fillImage.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            fillImage.color = new Color(0.2f, 0.85f, 0.3f);
            fillImage.type = Image.Type.Filled;
            fillImage.fillMethod = Image.FillMethod.Horizontal;
            fillImage.fillAmount = 1f;

            return (text, fillImage);
        }

        private static (Button button, TMP_Text label) BuildHUDIconButton(Transform parent, string name, string initialLabel, Vector2 anchoredPosition)
        {
            var buttonObject = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            buttonObject.transform.SetParent(parent, false);
            var rect = buttonObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(1f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 1f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = new Vector2(90f, 70f);

            buttonObject.GetComponent<Image>().color = new Color(0.15f, 0.15f, 0.2f, 0.85f);
            Button button = buttonObject.GetComponent<Button>();

            TMP_Text label = CreateAnchoredTMPText(buttonObject.transform, "Label", initialLabel,
                new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(90f, 70f), 26f, TextAlignmentOptions.Center);

            return (button, label);
        }

        private static GameHUDPresenter BuildGameHUDPresenter(Transform parent, GameHUDView view)
        {
            var presenterObject = new GameObject("GameHUDPresenter");
            presenterObject.transform.SetParent(parent, false);
            var presenter = presenterObject.AddComponent<GameHUDPresenter>();

            var serialized = new SerializedObject(presenter);
            serialized.FindProperty("_view").objectReferenceValue = view;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            return presenter;
        }

        private static PausePanelView BuildPausePanel(Transform parent)
        {
            var panel = new GameObject("PausePanel", typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(parent, false);
            var panelRect = panel.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.anchoredPosition = Vector2.zero;
            panelRect.sizeDelta = new Vector2(560f, 480f);
            panel.GetComponent<Image>().color = new Color(0.05f, 0.05f, 0.08f, 0.92f);

            CreateTMPText(panel.transform, "TitleText", "Paused", -30f, 60f, 40f, TextAlignmentOptions.Center);

            Button resumeButton = BuildDetailsButton(panel.transform, "ResumeButton", "Resume", 0f, -140f, new Color(0.15f, 0.35f, 0.15f, 0.9f));
            Button restartButton = BuildDetailsButton(panel.transform, "RestartButton", "Restart", 0f, -250f, new Color(0.2f, 0.2f, 0.35f, 0.9f));
            Button quitButton = BuildDetailsButton(panel.transform, "QuitButton", "Quit", 0f, -360f, new Color(0.3f, 0.1f, 0.1f, 0.9f));

            var view = panel.AddComponent<PausePanelView>();
            var serializedView = new SerializedObject(view);
            serializedView.FindProperty("_resumeButton").objectReferenceValue = resumeButton;
            serializedView.FindProperty("_restartButton").objectReferenceValue = restartButton;
            serializedView.FindProperty("_quitButton").objectReferenceValue = quitButton;
            serializedView.ApplyModifiedPropertiesWithoutUndo();

            panel.SetActive(false);

            return view;
        }

        private static GameResultView BuildGameResultPanel(Transform parent, string name, string titleText, Color accentColor)
        {
            var panel = new GameObject(name, typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(parent, false);
            var panelRect = panel.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.anchoredPosition = Vector2.zero;
            panelRect.sizeDelta = new Vector2(560f, 420f);
            panel.GetComponent<Image>().color = new Color(0.05f, 0.05f, 0.08f, 0.92f);

            TMP_Text title = CreateTMPText(panel.transform, "TitleText", titleText, -40f, 70f, 46f, TextAlignmentOptions.Center);
            title.color = accentColor;

            Button restartButton = BuildDetailsButton(panel.transform, "RestartButton", "Restart", 0f, -220f, new Color(0.2f, 0.2f, 0.35f, 0.9f));

            var view = panel.AddComponent<GameResultView>();
            var serializedView = new SerializedObject(view);
            serializedView.FindProperty("_restartButton").objectReferenceValue = restartButton;
            serializedView.ApplyModifiedPropertiesWithoutUndo();

            panel.SetActive(false);

            return view;
        }

        private static GameStateUIController BuildGameStateUIController(
            Transform parent,
            GameObject pausePanel, PausePanelView pausePanelView,
            GameObject victoryPanel, GameResultView victoryResultView,
            GameObject defeatPanel, GameResultView defeatResultView,
            CanvasGroup gameplayInteractionGroup)
        {
            var controllerObject = new GameObject("GameStateUIController");
            controllerObject.transform.SetParent(parent, false);
            var controller = controllerObject.AddComponent<GameStateUIController>();

            var serialized = new SerializedObject(controller);
            serialized.FindProperty("_pausePanel").objectReferenceValue = pausePanel;
            serialized.FindProperty("_pausePanelView").objectReferenceValue = pausePanelView;
            serialized.FindProperty("_victoryPanel").objectReferenceValue = victoryPanel;
            serialized.FindProperty("_victoryResultView").objectReferenceValue = victoryResultView;
            serialized.FindProperty("_defeatPanel").objectReferenceValue = defeatPanel;
            serialized.FindProperty("_defeatResultView").objectReferenceValue = defeatResultView;
            serialized.FindProperty("_gameplayInteractionGroup").objectReferenceValue = gameplayInteractionGroup;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            return controller;
        }

        private static WaveHUDView BuildWavePanel(Transform parent, float topOffset)
        {
            var panel = new GameObject("WavePanel", typeof(RectTransform));
            panel.transform.SetParent(parent, false);
            var panelRect = panel.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0f, 1f);
            panelRect.anchorMax = new Vector2(1f, 1f);
            panelRect.pivot = new Vector2(0.5f, 1f);
            panelRect.anchoredPosition = new Vector2(0f, -topOffset);
            panelRect.sizeDelta = new Vector2(0f, 220f);

            TMP_Text waveText = CreateTMPText(panel.transform, "WaveText", "Wave 1/1", 0f, 50f, 34f, TextAlignmentOptions.Center);
            TMP_Text progressText = CreateTMPText(panel.transform, "EnemyProgressText", "0/0", -50f, 36f, 24f, TextAlignmentOptions.Center);
            Image fillImage = BuildProgressBar(panel.transform, -90f);
            (GameObject countdownPanel, TMP_Text countdownText) = BuildCountdownPanel(panel.transform, -130f);

            var view = panel.AddComponent<WaveHUDView>();
            var serializedView = new SerializedObject(view);
            serializedView.FindProperty("_waveText").objectReferenceValue = waveText;
            serializedView.FindProperty("_enemyProgressText").objectReferenceValue = progressText;
            serializedView.FindProperty("_progressFillImage").objectReferenceValue = fillImage;
            serializedView.FindProperty("_countdownPanel").objectReferenceValue = countdownPanel;
            serializedView.FindProperty("_countdownText").objectReferenceValue = countdownText;
            serializedView.ApplyModifiedPropertiesWithoutUndo();

            return view;
        }

        private static TMP_Text CreateTMPText(Transform parent, string name, string initialText, float yOffset, float height, float fontSize, TextAlignmentOptions alignment)
        {
            var textObject = new GameObject(name, typeof(RectTransform));
            textObject.transform.SetParent(parent, false);
            var rect = textObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, yOffset);
            rect.sizeDelta = new Vector2(0f, height);

            var text = textObject.AddComponent<TextMeshProUGUI>();
            text.text = initialText;
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = Color.white;

            return text;
        }

        private static Image BuildProgressBar(Transform parent, float yOffset)
        {
            var background = new GameObject("WaveProgressBar", typeof(RectTransform), typeof(Image));
            background.transform.SetParent(parent, false);
            var backgroundRect = background.GetComponent<RectTransform>();
            backgroundRect.anchorMin = new Vector2(0f, 1f);
            backgroundRect.anchorMax = new Vector2(1f, 1f);
            backgroundRect.pivot = new Vector2(0.5f, 1f);
            backgroundRect.anchoredPosition = new Vector2(0f, yOffset);
            backgroundRect.sizeDelta = new Vector2(0f, 24f);
            background.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.4f);

            var fill = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            fill.transform.SetParent(background.transform, false);
            var fillRect = fill.GetComponent<RectTransform>();
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;
            var fillImage = fill.GetComponent<Image>();
            fillImage.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            fillImage.color = new Color(0.9f, 0.75f, 0.15f);
            fillImage.type = Image.Type.Filled;
            fillImage.fillMethod = Image.FillMethod.Horizontal;
            fillImage.fillAmount = 0f;

            return fillImage;
        }

        private static (GameObject panel, TMP_Text text) BuildCountdownPanel(Transform parent, float yOffset)
        {
            var panel = new GameObject("CountdownPanel", typeof(RectTransform));
            panel.transform.SetParent(parent, false);
            var panelRect = panel.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0f, 1f);
            panelRect.anchorMax = new Vector2(1f, 1f);
            panelRect.pivot = new Vector2(0.5f, 1f);
            panelRect.anchoredPosition = new Vector2(0f, yOffset);
            panelRect.sizeDelta = new Vector2(0f, 40f);

            CreateTMPText(panel.transform, "CountdownLabel", "Wave starts in", 0f, 40f, 20f, TextAlignmentOptions.MidlineLeft);
            TMP_Text countdownText = CreateTMPText(panel.transform, "CountdownText", "0", 0f, 40f, 24f, TextAlignmentOptions.MidlineRight);

            return (panel, countdownText);
        }

        private static void BuildWaveHUDPresenter(Transform parent, WaveController waveController, WaveHUDView view)
        {
            var presenterObject = new GameObject("WaveHUDPresenter");
            presenterObject.transform.SetParent(parent, false);
            var presenter = presenterObject.AddComponent<WaveHUDPresenter>();

            var serialized = new SerializedObject(presenter);
            serialized.FindProperty("_waveController").objectReferenceValue = waveController;
            serialized.FindProperty("_view").objectReferenceValue = view;
            serialized.ApplyModifiedPropertiesWithoutUndo();
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

        private static BuildBarPresenter BuildBuildPanel(Transform parent, TowerDefinition[] towerDefinitions)
        {
            var panel = new GameObject("BuildPanel", typeof(RectTransform));
            panel.transform.SetParent(parent, false);
            var panelRect = panel.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0f, 0f);
            panelRect.anchorMax = new Vector2(1f, 0f);
            panelRect.pivot = new Vector2(0.5f, 0f);
            panelRect.anchoredPosition = new Vector2(0f, 420f);
            panelRect.sizeDelta = new Vector2(0f, 150f);

            var buttonNames = new[] { "Blaster", "Rapid", "Heavy" };
            var buttonViews = new TowerBuildButtonView[towerDefinitions.Length];

            const float buttonWidth = 150f;
            const float spacing = 20f;
            float totalWidth = buttonViews.Length * buttonWidth + (buttonViews.Length - 1) * spacing;
            float startX = -totalWidth / 2f + buttonWidth / 2f;

            for (int i = 0; i < towerDefinitions.Length; i++)
            {
                float x = startX + i * (buttonWidth + spacing);
                string buttonName = "TowerButton_" + (i < buttonNames.Length ? buttonNames[i] : i.ToString());
                buttonViews[i] = BuildTowerButton(panel.transform, buttonName, x, buttonWidth);
            }

            float lastButtonCenterX = startX + (towerDefinitions.Length - 1) * (buttonWidth + spacing);
            float cancelX = lastButtonCenterX + buttonWidth / 2f + spacing + 110f / 2f;
            Button cancelButton = BuildCancelButton(panel.transform, cancelX);

            var presenterObject = new GameObject("BuildBarPresenter");
            presenterObject.transform.SetParent(panel.transform, false);
            var presenter = presenterObject.AddComponent<BuildBarPresenter>();

            var serializedPresenter = new SerializedObject(presenter);
            SerializedProperty definitionsProperty = serializedPresenter.FindProperty("_towerDefinitions");
            definitionsProperty.arraySize = towerDefinitions.Length;
            SerializedProperty viewsProperty = serializedPresenter.FindProperty("_buttonViews");
            viewsProperty.arraySize = buttonViews.Length;
            for (int i = 0; i < towerDefinitions.Length; i++)
            {
                definitionsProperty.GetArrayElementAtIndex(i).objectReferenceValue = towerDefinitions[i];
                viewsProperty.GetArrayElementAtIndex(i).objectReferenceValue = buttonViews[i];
            }
            serializedPresenter.FindProperty("_cancelButton").objectReferenceValue = cancelButton;
            serializedPresenter.ApplyModifiedPropertiesWithoutUndo();

            return presenter;
        }

        private static TowerBuildButtonView BuildTowerButton(Transform parent, string name, float x, float width)
        {
            var buttonObject = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            buttonObject.transform.SetParent(parent, false);
            var rect = buttonObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(x, 0f);
            rect.sizeDelta = new Vector2(width, 130f);

            buttonObject.GetComponent<Image>().color = new Color(0.15f, 0.15f, 0.2f, 0.85f);
            var button = buttonObject.GetComponent<Button>();

            var iconObject = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            iconObject.transform.SetParent(buttonObject.transform, false);
            var iconRect = iconObject.GetComponent<RectTransform>();
            iconRect.anchorMin = new Vector2(0.5f, 1f);
            iconRect.anchorMax = new Vector2(0.5f, 1f);
            iconRect.pivot = new Vector2(0.5f, 1f);
            iconRect.anchoredPosition = new Vector2(0f, -10f);
            iconRect.sizeDelta = new Vector2(60f, 60f);
            var iconImage = iconObject.GetComponent<Image>();
            iconImage.color = new Color(1f, 1f, 1f, 0.9f);

            TMP_Text costText = CreateTMPText(buttonObject.transform, "CostText", "0", -80f, 30f, 22f, TextAlignmentOptions.Center);

            var selectedIndicator = new GameObject("SelectedIndicator", typeof(RectTransform), typeof(Image));
            selectedIndicator.transform.SetParent(buttonObject.transform, false);
            var selectedRect = selectedIndicator.GetComponent<RectTransform>();
            selectedRect.anchorMin = Vector2.zero;
            selectedRect.anchorMax = Vector2.one;
            selectedRect.offsetMin = new Vector2(-4f, -4f);
            selectedRect.offsetMax = new Vector2(4f, 4f);
            selectedIndicator.GetComponent<Image>().color = new Color(0.95f, 0.85f, 0.2f, 0.5f);
            selectedIndicator.transform.SetAsFirstSibling();
            selectedIndicator.SetActive(false);

            var view = buttonObject.AddComponent<TowerBuildButtonView>();
            var serializedView = new SerializedObject(view);
            serializedView.FindProperty("_button").objectReferenceValue = button;
            serializedView.FindProperty("_icon").objectReferenceValue = iconImage;
            serializedView.FindProperty("_costText").objectReferenceValue = costText;
            serializedView.FindProperty("_selectedIndicator").objectReferenceValue = selectedIndicator;
            serializedView.ApplyModifiedPropertiesWithoutUndo();

            return view;
        }

        private static Button BuildCancelButton(Transform parent, float x)
        {
            var buttonObject = new GameObject("CancelBuildButton", typeof(RectTransform), typeof(Image), typeof(Button));
            buttonObject.transform.SetParent(parent, false);
            var rect = buttonObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(x, 0f);
            rect.sizeDelta = new Vector2(110f, 130f);

            buttonObject.GetComponent<Image>().color = new Color(0.3f, 0.1f, 0.1f, 0.85f);

            CreateTMPText(buttonObject.transform, "Label", "Cancel", 0f, 130f, 22f, TextAlignmentOptions.Center);

            return buttonObject.GetComponent<Button>();
        }

        private static TowerDetailsPresenter BuildTowerDetailsPanel(Transform parent)
        {
            var panel = new GameObject("TowerDetailsPanel", typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(parent, false);
            var panelRect = panel.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.anchoredPosition = Vector2.zero;
            panelRect.sizeDelta = new Vector2(560f, 560f);
            panel.GetComponent<Image>().color = new Color(0.08f, 0.08f, 0.12f, 0.92f);

            var iconObject = new GameObject("TowerIcon", typeof(RectTransform), typeof(Image));
            iconObject.transform.SetParent(panel.transform, false);
            var iconRect = iconObject.GetComponent<RectTransform>();
            iconRect.anchorMin = new Vector2(0.5f, 1f);
            iconRect.anchorMax = new Vector2(0.5f, 1f);
            iconRect.pivot = new Vector2(0.5f, 1f);
            iconRect.anchoredPosition = new Vector2(0f, -20f);
            iconRect.sizeDelta = new Vector2(90f, 90f);
            var iconImage = iconObject.GetComponent<Image>();
            iconImage.color = Color.white;

            TMP_Text nameText = CreateTMPText(panel.transform, "TowerNameText", "Tower", -125f, 40f, 30f, TextAlignmentOptions.Center);
            TMP_Text levelText = CreateTMPText(panel.transform, "LevelText", "Lv 1", -170f, 30f, 22f, TextAlignmentOptions.Center);
            TMP_Text damageText = CreateTMPText(panel.transform, "DamageText", "Damage: 0", -205f, 26f, 20f, TextAlignmentOptions.Center);
            TMP_Text rangeText = CreateTMPText(panel.transform, "RangeText", "Range: 0", -233f, 26f, 20f, TextAlignmentOptions.Center);
            TMP_Text attackSpeedText = CreateTMPText(panel.transform, "AttackSpeedText", "Speed: 0/s", -261f, 26f, 20f, TextAlignmentOptions.Center);
            TMP_Text targetingText = CreateTMPText(panel.transform, "TargetingText", "Targeting: First", -289f, 26f, 20f, TextAlignmentOptions.Center);
            TMP_Text upgradeCostText = CreateTMPText(panel.transform, "UpgradeCostText", "Upgrade cost: 0", -325f, 28f, 22f, TextAlignmentOptions.Center);
            TMP_Text sellValueText = CreateTMPText(panel.transform, "SellValueText", "Sell value: 0", -358f, 28f, 22f, TextAlignmentOptions.Center);

            Button upgradeButton = BuildDetailsButton(panel.transform, "UpgradeButton", "Upgrade", -170f, -420f, new Color(0.15f, 0.35f, 0.15f, 0.9f));
            Button sellButton = BuildDetailsButton(panel.transform, "SellButton", "Sell", 0f, -420f, new Color(0.35f, 0.25f, 0.1f, 0.9f));
            Button closeButton = BuildDetailsButton(panel.transform, "CloseButton", "Close", 170f, -420f, new Color(0.25f, 0.1f, 0.1f, 0.9f));

            var view = panel.AddComponent<TowerDetailsView>();
            var serializedView = new SerializedObject(view);
            serializedView.FindProperty("_root").objectReferenceValue = panel;
            serializedView.FindProperty("_icon").objectReferenceValue = iconImage;
            serializedView.FindProperty("_nameText").objectReferenceValue = nameText;
            serializedView.FindProperty("_levelText").objectReferenceValue = levelText;
            serializedView.FindProperty("_damageText").objectReferenceValue = damageText;
            serializedView.FindProperty("_rangeText").objectReferenceValue = rangeText;
            serializedView.FindProperty("_attackSpeedText").objectReferenceValue = attackSpeedText;
            serializedView.FindProperty("_targetingText").objectReferenceValue = targetingText;
            serializedView.FindProperty("_upgradeCostText").objectReferenceValue = upgradeCostText;
            serializedView.FindProperty("_sellValueText").objectReferenceValue = sellValueText;
            serializedView.FindProperty("_upgradeButton").objectReferenceValue = upgradeButton;
            serializedView.FindProperty("_sellButton").objectReferenceValue = sellButton;
            serializedView.FindProperty("_closeButton").objectReferenceValue = closeButton;
            serializedView.ApplyModifiedPropertiesWithoutUndo();

            var presenterObject = new GameObject("TowerDetailsPresenter");
            presenterObject.transform.SetParent(panel.transform, false);
            var presenter = presenterObject.AddComponent<TowerDetailsPresenter>();
            var serializedPresenter = new SerializedObject(presenter);
            serializedPresenter.FindProperty("_view").objectReferenceValue = view;
            serializedPresenter.ApplyModifiedPropertiesWithoutUndo();

            panel.SetActive(false);

            return presenter;
        }

        private static Button BuildDetailsButton(Transform parent, string name, string label, float x, float y, Color color)
        {
            var buttonObject = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            buttonObject.transform.SetParent(parent, false);
            var rect = buttonObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(x, y);
            rect.sizeDelta = new Vector2(150f, 90f);

            buttonObject.GetComponent<Image>().color = color;
            CreateTMPText(buttonObject.transform, "Label", label, 0f, 90f, 24f, TextAlignmentOptions.Center);

            return buttonObject.GetComponent<Button>();
        }

        private static void BuildEventSystem()
        {
            var eventSystemObject = new GameObject("EventSystem");
            eventSystemObject.AddComponent<EventSystem>();
            eventSystemObject.AddComponent<InputSystemUIInputModule>();
        }

        private static AudioService BuildAudioService()
        {
            const int sfxSourceCount = 6;

            var audioObject = new GameObject("AudioService");
            var audioService = audioObject.AddComponent<AudioService>();

            var musicObject = new GameObject("MusicSource", typeof(AudioSource));
            musicObject.transform.SetParent(audioObject.transform, false);
            var musicSource = musicObject.GetComponent<AudioSource>();
            musicSource.playOnAwake = false;
            musicSource.loop = true;

            var sfxSources = new AudioSource[sfxSourceCount];
            for (int i = 0; i < sfxSourceCount; i++)
            {
                var sfxObject = new GameObject($"SfxSource_{i:00}", typeof(AudioSource));
                sfxObject.transform.SetParent(audioObject.transform, false);
                var sfxSource = sfxObject.GetComponent<AudioSource>();
                sfxSource.playOnAwake = false;
                sfxSources[i] = sfxSource;
            }

            var serialized = new SerializedObject(audioService);
            serialized.FindProperty("_musicSource").objectReferenceValue = musicSource;
            SerializedProperty sfxProperty = serialized.FindProperty("_sfxSources");
            sfxProperty.arraySize = sfxSources.Length;
            for (int i = 0; i < sfxSources.Length; i++)
            {
                sfxProperty.GetArrayElementAtIndex(i).objectReferenceValue = sfxSources[i];
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();

            return audioService;
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

        private static void WireCompositionRootCombatSystem(
            LevelCompositionRoot compositionRoot,
            GameObject playerInstance,
            Transform projectileRuntimeParent)
        {
            var autoAttack = playerInstance.GetComponent<PlayerAutoAttack>();

            var serialized = new SerializedObject(compositionRoot);
            serialized.FindProperty("_playerAutoAttack").objectReferenceValue = autoAttack;
            serialized.FindProperty("_projectileRuntimeParent").objectReferenceValue = projectileRuntimeParent;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void WireCompositionRootWaveSystem(
            LevelCompositionRoot compositionRoot,
            WaveController waveController,
            WaveDebugControls waveDebugControls)
        {
            var serialized = new SerializedObject(compositionRoot);
            serialized.FindProperty("_waveController").objectReferenceValue = waveController;
            serialized.FindProperty("_waveDebugControls").objectReferenceValue = waveDebugControls;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void WireCompositionRootTowerSystem(
            LevelCompositionRoot compositionRoot,
            Transform towerRuntimeParent,
            TowerDebugSpawner towerDebugSpawner)
        {
            var serialized = new SerializedObject(compositionRoot);
            serialized.FindProperty("_towerRuntimeParent").objectReferenceValue = towerRuntimeParent;
            serialized.FindProperty("_towerDebugSpawner").objectReferenceValue = towerDebugSpawner;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void WireCompositionRootBuildSystem(
            LevelCompositionRoot compositionRoot,
            WorldSelectionController worldSelectionController,
            BuildBarPresenter buildBarPresenter,
            BuildNodeVisualCoordinator buildNodeVisualCoordinator,
            TowerDetailsPresenter towerDetailsPresenter)
        {
            var serialized = new SerializedObject(compositionRoot);
            serialized.FindProperty("_worldSelectionController").objectReferenceValue = worldSelectionController;
            serialized.FindProperty("_buildBarPresenter").objectReferenceValue = buildBarPresenter;
            serialized.FindProperty("_buildNodeVisualCoordinator").objectReferenceValue = buildNodeVisualCoordinator;
            serialized.FindProperty("_towerDetailsPresenter").objectReferenceValue = towerDetailsPresenter;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void WireCompositionRootVfxSystem(LevelCompositionRoot compositionRoot, Transform vfxRuntimeParent)
        {
            var serialized = new SerializedObject(compositionRoot);
            serialized.FindProperty("_vfxRuntimeParent").objectReferenceValue = vfxRuntimeParent;
            serialized.FindProperty("_buildVfxDefinition").objectReferenceValue = VfxPrefabBuilder.TowerBuild;
            serialized.FindProperty("_upgradeVfxDefinition").objectReferenceValue = VfxPrefabBuilder.TowerUpgrade;
            serialized.FindProperty("_sellVfxDefinition").objectReferenceValue = VfxPrefabBuilder.TowerSell;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void WireCompositionRootAudioSystem(LevelCompositionRoot compositionRoot, AudioService audioService)
        {
            var serialized = new SerializedObject(compositionRoot);
            serialized.FindProperty("_audioService").objectReferenceValue = audioService;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void WireCompositionRootGameFlowUI(
            LevelCompositionRoot compositionRoot,
            GameHUDPresenter gameHUDPresenter,
            GameStateUIController gameStateUIController)
        {
            var serialized = new SerializedObject(compositionRoot);
            serialized.FindProperty("_gameHUDPresenter").objectReferenceValue = gameHUDPresenter;
            serialized.FindProperty("_gameStateUIController").objectReferenceValue = gameStateUIController;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
