using System.Collections.Generic;
using AlienDefense.Base;
using AlienDefense.Core;
using AlienDefense.Meta;
using AlienDefense.UI.BaseBuilding;
using AlienDefense.UI.MainMenu;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace AlienDefense.EditorTools
{
    /// <summary>Builds the 3D base world and the Base tab UI inside MainMenu, and wires every reference.
    ///
    /// "Setup Base Scene" is idempotent: it only creates what is missing (world, UI, materials, prefabs) and always
    /// re-wires references, so running it twice changes nothing. "Rebuild Base Scene" deletes the two generated
    /// roots (BaseWorld, BaseWorldUI) and builds them again - use it after changing the layout constants here.
    ///
    /// Third-party prefabs are never edited: they are instanced into the scene, or wrapped in prefabs under
    /// Assets/_Game/Prefabs/Base. Every asset is looked up by name with AssetDatabase and reported as MISSING
    /// ASSET when absent, instead of guessing a path.</summary>
    public static partial class BaseSceneSetupTool
    {
        private const string MainMenuPath = "Assets/_Game/Scenes/Menu/MainMenu.unity";
        private const string BootstrapPath = "Assets/_Game/Scenes/Bootstrap/Bootstrap.unity";
        private const string CatalogPath = "Assets/_Game/Data/Base/BaseBuildingCatalog.asset";
        private const string MetaCatalogPath = "Assets/_Game/Data/Meta/MetaItemCatalog.asset";
        private const string MaterialsFolder = "Assets/_Game/Art/Base/Materials";
        private const string IconsFolder = "Assets/_Game/Art/Base/Icons";
        private const string ConstructionFolder = "Assets/_Game/Prefabs/Base/Construction";
        private const string EnvironmentFolder = "Assets/_Game/Prefabs/Base/Environment";
        private const string FontPath = "Assets/_Game/Font/Fredoka-Bold SDF.asset";

        private const string WorldRootName = "BaseWorld";
        private const string UiRootName = "BaseWorldUI";
        private const string LayerName = "BaseWorld";
        private const int BaseLayer = 17;

        // ------------------------------------------------------------------ Layout (world units)

        private const float PlotYaw = 180f;
        private const float CameraPitch = 50f;
        private const float CameraYaw = 45f;
        private const float CameraDistance = 132f;
        private const float CameraFov = 35f;
        private const float GroundHalfSize = 110f;
        private const float RoadWidth = 4f;
        private const float RiverZ = -44f;
        private const float RiverWidth = 9f;

        private static readonly (string id, Vector3 position)[] Plots =
        {
            ("central_building", new Vector3(11f, 0f, 11f)),
            ("research_center", new Vector3(-11f, 0f, 11f)),
            ("weapon_workshop", new Vector3(11f, 0f, -11f)),
            ("patrol_post", new Vector3(-11f, 0f, -11f)),
            ("sawmill", new Vector3(37f, 0f, 11f))
        };

        /// <summary>Locked expansion plots: decoration only (fenced dirt, no collider, no logic).</summary>
        private static readonly Vector3[] ExpansionPlots =
        {
            new Vector3(37f, 0f, -11f),
            new Vector3(-37f, 0f, 11f),
            new Vector3(-37f, 0f, -11f)
        };

        // Roads: (center, size) on the ground plane.
        private static readonly (Vector3 center, Vector2 size)[] Roads =
        {
            (new Vector3(4f, 0f, 0f), new Vector2(92f, RoadWidth)),        // east-west main road, x -42..50
            (new Vector3(0f, 0f, -8f), new Vector2(RoadWidth, 64f)),       // north-south main road, z -40..24
        };

        private static readonly Color RoadColor = Hex("#D8BC84");
        private static readonly Color SkyColor = Hex("#9FD8F5");

        private static readonly Color PanelWhite = Hex("#F2F6FC");
        private static readonly Color HeaderBlue = Hex("#2F6FD6");
        private static readonly Color SectionBlue = Hex("#DCE8FA");
        private static readonly Color TextDark = Hex("#24324A");
        private static readonly Color StartBlue = Hex("#3B82F6");
        private static readonly Color DeltaGreen = Hex("#2EAD4B");

        private static readonly List<string> Missing = new List<string>();

        [MenuItem("Tools/Tower Defense/Base/Setup Base Scene")]
        public static void Setup()
        {
            Run(rebuild: false);
        }

        [MenuItem("Tools/Tower Defense/Base/Rebuild Base Scene")]
        public static void Rebuild()
        {
            Run(rebuild: true);
        }

        private static void Run(bool rebuild)
        {
            Missing.Clear();

            if (AssetDatabase.LoadAssetAtPath<BaseBuildingCatalog>(CatalogPath) == null)
            {
                BaseDataSetupTool.Setup();
            }

            var catalog = AssetDatabase.LoadAssetAtPath<BaseBuildingCatalog>(CatalogPath);
            if (catalog == null)
            {
                Debug.LogError("[BaseSceneSetupTool] BaseBuildingCatalog could not be created at " + CatalogPath);
                return;
            }

            EnsureLayer();
            EnsureWoodIcon();

            Scene scene = OpenScene(MainMenuPath);
            if (!scene.IsValid())
            {
                return;
            }

            GameObject world = FindRoot(scene, WorldRootName);
            Transform basePanel = FindNamed(scene, "BasePanel");
            if (basePanel == null)
            {
                Debug.LogError("[BaseSceneSetupTool] MainMenu has no BasePanel. Nothing was built.");
                return;
            }

            Transform ui = basePanel.Find(UiRootName);

            if (rebuild)
            {
                if (world != null) Object.DestroyImmediate(world);
                if (ui != null) Object.DestroyImmediate(ui.gameObject);
                world = null;
                ui = null;
            }

            if (world == null)
            {
                world = BuildWorld(scene, catalog);
            }

            if (ui == null)
            {
                ui = BuildUi(basePanel).transform;
            }

            Wire(scene, world, ui.gameObject, basePanel);
            ConstructionPlotVisualSetupEditor.WireConstructionExtras(world, scene);

            world.SetActive(false);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            AssignBootstrapCatalog(catalog);

            AssetDatabase.SaveAssets();
            ReportMissing();
            Debug.Log("[BaseSceneSetupTool] Done (" + (rebuild ? "rebuild" : "setup") + ").");
        }

        // ------------------------------------------------------------------ Project setup

        private static void EnsureLayer()
        {
            var tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            SerializedProperty layers = tagManager.FindProperty("layers");
            SerializedProperty layer = layers.GetArrayElementAtIndex(BaseLayer);
            if (layer.stringValue == LayerName)
            {
                return;
            }

            if (!string.IsNullOrEmpty(layer.stringValue))
            {
                Debug.LogWarning($"[BaseSceneSetupTool] Layer {BaseLayer} was '{layer.stringValue}', renamed to '{LayerName}'.");
            }

            layer.stringValue = LayerName;
            tagManager.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>No wood icon ships with the project. Renders one from the Polytope log pile so the Wood item
        /// is recognisable, and only if the item still has no icon - a real icon assigned later is never replaced.</summary>
        private static void EnsureWoodIcon()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<MetaItemCatalog>(MetaCatalogPath);
            if (catalog == null || !catalog.TryGet("wood", out MetaItemDefinition wood) || wood.Icon != null)
            {
                return;
            }

            string path = IconsFolder + "/T_Icon_Wood_Placeholder.png";
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null)
            {
                sprite = RenderIcon(BuildLogPile(), path, 256);
            }

            if (sprite == null)
            {
                Missing.Add("Wood icon: placeholder render failed. Assign MetaItem_wood.asset > Icon.");
                return;
            }

            var so = new SerializedObject(wood);
            so.FindProperty("_icon").objectReferenceValue = sprite;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(wood);
            Missing.Add("Wood icon: using a rendered PLACEHOLDER (" + path + "). Replace MetaItem_wood.asset > Icon " +
                "with real art when available.");
        }

        /// <summary>Three stacked logs from primitives: bark-brown bodies with pale cut ends - reads as "wood" at
        /// icon size far better than any pack model.</summary>
        private static GameObject BuildLogPile()
        {
            var root = new GameObject("WoodIconLogs");
            var bark = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            bark.SetColor("_BaseColor", Hex("#8A5A2E"));
            bark.SetFloat("_Smoothness", 0.1f);
            var cut = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            cut.SetColor("_BaseColor", Hex("#F0C98A"));
            cut.SetFloat("_Smoothness", 0.1f);

            Vector3[] positions = { new Vector3(-0.52f, 0.5f, 0f), new Vector3(0.52f, 0.5f, 0f), new Vector3(0f, 1.38f, 0f) };
            foreach (Vector3 position in positions)
            {
                GameObject log = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                log.transform.SetParent(root.transform, false);
                log.transform.localPosition = position;
                log.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                log.transform.localScale = new Vector3(1f, 1.4f, 1f);
                log.GetComponent<Renderer>().sharedMaterial = bark;

                GameObject face = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                face.transform.SetParent(root.transform, false);
                face.transform.localPosition = position + new Vector3(0f, 0f, -1.41f);
                face.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                face.transform.localScale = new Vector3(0.86f, 0.02f, 0.86f);
                face.GetComponent<Renderer>().sharedMaterial = cut;
            }

            return root;
        }

        internal static Sprite RenderIcon(GameObject instance, string path, int size)
        {
            EnsureFolder(IconsFolder);

            Vector3 origin = new Vector3(5000f, 5000f, 5000f);
            instance.transform.position = origin;
            instance.transform.rotation = Quaternion.Euler(0f, 30f, 0f);
            if (!TryGetBounds(instance, out Bounds bounds))
            {
                Object.DestroyImmediate(instance);
                return null;
            }

            var cameraObject = new GameObject("IconCamera");
            var camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0f, 0f, 0f, 0f);
            camera.orthographic = true;
            camera.orthographicSize = bounds.extents.magnitude * 0.72f;
            camera.nearClipPlane = 0.01f;
            camera.farClipPlane = bounds.extents.magnitude * 6f;
            camera.transform.rotation = Quaternion.Euler(30f, -30f, 0f);
            camera.transform.position = bounds.center - camera.transform.forward * bounds.extents.magnitude * 3f;

            var lightObject = new GameObject("IconLight");
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.3f;
            light.transform.rotation = Quaternion.Euler(40f, -60f, 0f);

            var rt = new RenderTexture(size, size, 24, RenderTextureFormat.ARGB32);
            camera.targetTexture = rt;
            camera.Render();

            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = rt;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.ReadPixels(new Rect(0, 0, size, size), 0, 0);
            texture.Apply();
            RenderTexture.active = previous;

            System.IO.File.WriteAllBytes(path, texture.EncodeToPNG());

            camera.targetTexture = null;
            Object.DestroyImmediate(rt);
            Object.DestroyImmediate(texture);
            Object.DestroyImmediate(cameraObject);
            Object.DestroyImmediate(lightObject);
            Object.DestroyImmediate(instance);

            AssetDatabase.ImportAsset(path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        private static void AssignBootstrapCatalog(BaseBuildingCatalog catalog)
        {
            Scene bootstrap = SceneManager.GetSceneByPath(BootstrapPath);
            bool opened = false;
            if (!bootstrap.isLoaded)
            {
                bootstrap = EditorSceneManager.OpenScene(BootstrapPath, OpenSceneMode.Additive);
                opened = true;
            }

            bool changed = false;
            foreach (GameObject root in bootstrap.GetRootGameObjects())
            {
                foreach (BootstrapLoadingController controller in root.GetComponentsInChildren<BootstrapLoadingController>(true))
                {
                    var so = new SerializedObject(controller);
                    SerializedProperty property = so.FindProperty("_baseBuildingCatalog");
                    if (property != null && property.objectReferenceValue != catalog)
                    {
                        property.objectReferenceValue = catalog;
                        so.ApplyModifiedPropertiesWithoutUndo();
                        changed = true;
                    }
                }
            }

            if (changed)
            {
                EditorSceneManager.MarkSceneDirty(bootstrap);
                EditorSceneManager.SaveScene(bootstrap);
            }

            if (opened)
            {
                EditorSceneManager.CloseScene(bootstrap, true);
            }
        }

        // ------------------------------------------------------------------ World

        private static GameObject BuildWorld(Scene scene, BaseBuildingCatalog catalog)
        {
            Material road = EnsureMaterial("MAT_Base_Road", RoadColor, 0.05f);



            var root = new GameObject(WorldRootName);
            SceneManager.MoveGameObjectToScene(root, scene);
            root.transform.position = Vector3.zero;

            // Lighting: the base's own single directional light, limited to the base layer.
            var lightObject = new GameObject("BaseSun");
            lightObject.transform.SetParent(root.transform, false);
            lightObject.transform.rotation = Quaternion.Euler(52f, -35f, 0f);
            var sun = lightObject.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(1f, 0.96f, 0.88f);
            sun.intensity = 1.25f;
            sun.shadows = LightShadows.None;
            sun.cullingMask = 1 << BaseLayer;

            BuildCamera(root.transform);

            // Ground.
            Transform environment = new GameObject("Environment").transform;
            environment.SetParent(root.transform, false);


            Transform roads = new GameObject("Roads").transform;
            roads.SetParent(environment, false);
            for (int i = 0; i < Roads.Length; i++)
            {
                CreateSlab("Road_" + i, roads, Roads[i].center + new Vector3(0f, 0.02f, 0f),
                    new Vector3(Roads[i].size.x, 0.04f, Roads[i].size.y), road);
            }

            // Short driveways from the main roads to each plot.
            // Every plot sits beside the east-west road, so the driveway runs along Z from the road edge to the plot.
            foreach ((string id, Vector3 position) in Plots)
            {
                float side = Mathf.Sign(position.z);
                var center = new Vector3(position.x, 0.021f, side * 3.8f);
                CreateSlab("Driveway_" + id, roads, center, new Vector3(3f, 0.04f, 3.8f), road);
            }

            // The bridge where the main road crosses the river (the river itself is BaseRiverSystem, outside
            // Environment because its boats and ducks move).
            Transform bridgeRoot = new GameObject("Bridge").transform;
            bridgeRoot.SetParent(environment, false);
            PlaceBridge(bridgeRoot);

            // Painted ground, tiered cliffs, clustered nature and plot-edge dressing (BaseEnvironmentDressing.cs).
            BuildEnvironmentDressing(environment);
            BuildZoneProps(environment);

            // Expansion plots (decoration): empty slots here, dressed as construction sites below.
            Transform expansions = new GameObject("ExpansionPlots").transform;
            expansions.SetParent(environment, false);
            foreach (Vector3 position in ExpansionPlots)
            {
                var plot = new GameObject("ExpansionPlot");
                plot.transform.SetParent(expansions, false);
                plot.transform.position = position;
                plot.transform.rotation = Quaternion.Euler(0f, PlotYaw, 0f);
            }

            // Building plots.
            Transform plotsRoot = new GameObject("Plots").transform;
            plotsRoot.SetParent(root.transform, false);
            foreach ((string id, Vector3 position) in Plots)
            {
                BaseBuildingDefinition definition = FindDefinition(catalog, id);
                if (definition == null)
                {
                    Missing.Add($"BaseBuildingDefinition '{id}' not in {CatalogPath}. Plot_{id} was not created.");
                    continue;
                }

                CreatePlot(plotsRoot, definition, position);
            }

            // River, cave, boats and ducks (BaseRiverSystem.cs).
            BuildRiverSystem(root.transform);

            SetLayerRecursively(root, BaseLayer);
            MarkEnvironmentStatic(environment.gameObject);
            DisableDecorColliders(environment.gameObject);

            // Empty building plots and expansion slots get their construction-site visuals (own tool, own prefabs).
            ConstructionPlotVisualSetupEditor.ApplyToWorld(root);
            return root;
        }

        private static void BuildCamera(Transform root)
        {
            Vector3 focus = new Vector3(2f, 0f, 0f);

            var rig = new GameObject("CameraRig");
            rig.transform.SetParent(root, false);
            rig.transform.position = focus;

            var cameraObject = new GameObject("BaseCamera");
            cameraObject.transform.SetParent(rig.transform, false);
            Quaternion rotation = Quaternion.Euler(CameraPitch, CameraYaw, 0f);
            cameraObject.transform.localRotation = rotation;
            cameraObject.transform.localPosition = -(rotation * Vector3.forward) * CameraDistance;

            var camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = SkyColor;
            camera.cullingMask = 1 << BaseLayer;
            camera.fieldOfView = CameraFov;
            camera.nearClipPlane = 5f;
            camera.farClipPlane = 400f;
            camera.depth = 5f;

            var controller = cameraObject.AddComponent<BaseCameraController>();
            var so = new SerializedObject(controller);
            so.FindProperty("_rig").objectReferenceValue = rig.transform;
            so.FindProperty("_minX").floatValue = -36f;
            so.FindProperty("_maxX").floatValue = 42f;
            so.FindProperty("_minZ").floatValue = -30f;
            so.FindProperty("_maxZ").floatValue = 30f;
            so.FindProperty("_allowPinchZoom").boolValue = true;
            so.FindProperty("_minZoom").floatValue = 22f;
            so.FindProperty("_maxZoom").floatValue = 45f;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void CreatePlot(Transform parent, BaseBuildingDefinition definition, Vector3 position)
        {
            var plot = new GameObject("Plot_" + definition.Id);
            plot.transform.SetParent(parent, false);
            plot.transform.position = position;
            plot.transform.rotation = Quaternion.Euler(0f, PlotYaw, 0f);

            // Footprint and height from the level-1 wrapper's fitted collider.
            Vector3 footprint = new Vector3(9f, 6f, 9f);
            if (definition.TryGetLevel(1, out BuildingLevelDefinition level1) && level1.VisualPrefab != null)
            {
                BoxCollider box = level1.VisualPrefab.GetComponent<BoxCollider>();
                if (box != null)
                {
                    footprint = box.size;
                }
            }
            else
            {
                Missing.Add($"{definition.name} > Levels[0] > Visual Prefab is empty - the plot will show no model.");
            }

            // Construction state: wooden scaffold site fitted to the footprint (its own tool and prefabs).
            GameObject scaffolding = ConstructionPlotVisualSetupEditor.CreateConstructionSite(plot.transform, footprint);
            Transform interior = scaffolding.transform.Find("InteriorProps");

            var buildingRoot = new GameObject("BuildingRoot").transform;
            buildingRoot.SetParent(plot.transform, false);

            var anchor = new GameObject("IndicatorAnchor").transform;
            anchor.SetParent(plot.transform, false);
            anchor.localPosition = new Vector3(0f, footprint.y + 1.5f, 0f);

            var collider = plot.AddComponent<BoxCollider>();
            float side = Mathf.Max(10f, Mathf.Max(footprint.x, footprint.z) + 1f);
            collider.size = new Vector3(side, Mathf.Max(4f, footprint.y), side);
            collider.center = new Vector3(0f, collider.size.y * 0.5f, 0f);

            var view = plot.AddComponent<BaseBuildingView>();
            var so = new SerializedObject(view);
            so.FindProperty("_definition").objectReferenceValue = definition;
            so.FindProperty("_scaffoldingRoot").objectReferenceValue = scaffolding;
            so.FindProperty("_constructionInteriorRoot").objectReferenceValue = interior != null ? interior.gameObject : null;
            so.FindProperty("_buildingRoot").objectReferenceValue = buildingRoot;
            so.FindProperty("_indicatorAnchor").objectReferenceValue = anchor;
            so.FindProperty("_tapCollider").objectReferenceValue = collider;
            so.ApplyModifiedPropertiesWithoutUndo();

            // The plot's own children must not steal taps from the plot collider.
            foreach (Collider c in scaffolding.GetComponentsInChildren<Collider>(true)) c.enabled = false;
        }

        private static void PlaceBridge(Transform parent)
        {
            GameObject prefab = FindPrefab("PT_Wooden_Bridge_02");
            if (prefab == null)
            {
                Missing.Add("PT_Wooden_Bridge_02 (Polytope) - the road crosses the river with no bridge.");
                return;
            }

            var bridge = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            bridge.transform.position = new Vector3(0f, 0f, RiverZ);
            TryGetBounds(bridge, out Bounds bounds);

            // Turn the bridge's long axis onto world Z (across the river) and scale it to span both banks.
            bool longOnX = bounds.size.x > bounds.size.z;
            if (longOnX)
            {
                bridge.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
            }

            float length = Mathf.Max(bounds.size.x, bounds.size.z);
            float scale = (RiverWidth + 4f) / Mathf.Max(0.1f, length);
            bridge.transform.localScale = Vector3.one * scale;

            TryGetBounds(bridge, out bounds);
            bridge.transform.position += new Vector3(-bounds.center.x, -bounds.min.y, RiverZ - bounds.center.z);
        }

        /// <summary>A few props that give each district its purpose: logs by the sawmill, crates by the workshop,
        /// hay and a well by the headquarters.</summary>
        private static void BuildZoneProps(Transform parent)
        {
            Transform props = new GameObject("ZoneProps").transform;
            props.SetParent(parent, false);

            PlaceProp(props, "PT_Pine_Tree_03_logs", new Vector3(44f, 0f, 3f), 30f, 1f);
            PlaceProp(props, "PT_Pine_Tree_03_logs", new Vector3(47f, 0f, 7f), -20f, 1f);
            PlaceProp(props, "PT_Pine_Tree_03_stump", new Vector3(46f, 0f, 19f), 0f, 1f);
            PlaceProp(props, "PT_Pine_Tree_03_stump", new Vector3(30f, 0f, 20f), 60f, 1f);
            PlaceProp(props, "PT_Pine_Tree_03_green_cut", new Vector3(49f, 0f, 14f), 10f, 0.6f);

            PlaceProp(props, "Prop_WoodenCrates_01", new Vector3(18f, 0f, -4f), 20f, 1f);
            PlaceProp(props, "Prop_WoodenBox_02", new Vector3(20f, 0f, -5f), -15f, 1f);
            PlaceProp(props, "Prop_MetalBerrel_01", new Vector3(19f, 0f, -18f), 0f, 1f);
            PlaceProp(props, "Prop_Pallete_01", new Vector3(4f, 0f, -18f), 45f, 1f);

            PlaceProp(props, "Env_Well_01", new Vector3(3.5f, 0f, 5f), 45f, 1f);
            PlaceProp(props, "Prop_Haystack_01", new Vector3(-4f, 0f, 18f), 0f, 1f);
            PlaceProp(props, "Prop_Wheelbarrow", new Vector3(-18f, 0f, -4f), 120f, 1f);
            PlaceProp(props, "Prop_Berrel_03", new Vector3(-4.5f, 0f, -18f), 0f, 1f);
            PlaceProp(props, "Prop_FoodSack_01", new Vector3(-20f, 0f, 4.5f), 0f, 1f);
        }

        private static void PlaceProp(Transform parent, string prefabName, Vector3 position, float yaw, float scale)
        {
            GameObject prefab = FindPrefab(prefabName);
            if (prefab == null)
            {
                Missing.Add("Prop " + prefabName + " - skipped.");
                return;
            }

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            instance.transform.position = position;
            instance.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            instance.transform.localScale = Vector3.one * scale;
        }

        // ------------------------------------------------------------------ UI

        private static GameObject BuildUi(Transform basePanel)
        {
            RectTransform root = CreateRect(UiRootName, basePanel, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            root.gameObject.AddComponent<BaseWorldPresenter>();

            RectTransform indicatorLayer = CreateRect("IndicatorLayer", root, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            // Clip widgets to the content area so a bubble near the edge never draws over the top HUD / bottom nav.
            indicatorLayer.gameObject.AddComponent<RectMask2D>();
            BuildIndicatorTemplate(indicatorLayer);
            BuildQuestTracker(root);
            BuildBanner(root);
            BuildPopup(root);
            return root.gameObject;
        }

        private static void BuildIndicatorTemplate(RectTransform layer)
        {
            RectTransform template = CreateRect("IndicatorTemplate", layer, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, Vector2.zero);
            template.sizeDelta = new Vector2(10f, 10f);
            var view = template.gameObject.AddComponent<BaseBuildingIndicatorView>();

            Sprite panel = LoadSprite("Assets/_Game/Art/Sprite/Minimap/T_MinimapPanel.png");

            // Level badge.
            RectTransform badge = CreateRect("LevelBadge", template, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            badge.sizeDelta = new Vector2(78f, 78f);
            Image badgeBorder = badge.gameObject.AddComponent<Image>();
            badgeBorder.sprite = panel;
            badgeBorder.type = Image.Type.Sliced;
            badgeBorder.color = Color.white;
            badgeBorder.raycastTarget = false;
            Image badgeFill = CreateImage("Fill", badge, panel, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(66f, 66f), HeaderBlue);
            badgeFill.type = Image.Type.Sliced;
            badgeFill.preserveAspect = false;
            TMP_Text level = CreateText("Level", badge, "1", 42f, new Vector2(0.5f, 0.5f), new Vector2(0f, 2f), new Vector2(70f, 70f), Color.white);
            Image arrow = CreateImage("UpgradeArrow", badge, LoadSprite("Assets/_Game/Art/Sprite/Play/HUD/icon-arrow-up-green.png"),
                new Vector2(0.5f, 0.5f), new Vector2(40f, 34f), new Vector2(50f, 50f), Color.white);

            // Timer bar under the badge.
            RectTransform timer = CreateRect("Timer", template, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            timer.anchoredPosition = new Vector2(0f, -72f);
            timer.sizeDelta = new Vector2(250f, 50f);
            Image timerBack = timer.gameObject.AddComponent<Image>();
            timerBack.sprite = LoadSprite("Assets/_Game/Art/Sprite/Loading/background-loading-bar.png");
            timerBack.raycastTarget = false;
            Image timerFill = CreateImage("Fill", timer, LoadSprite("Assets/_Game/Art/Sprite/Loading/fll-loading-bar.png"),
                new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(236f, 36f), Color.white);
            timerFill.preserveAspect = false;
            timerFill.type = Image.Type.Filled;
            timerFill.fillMethod = Image.FillMethod.Horizontal;
            timerFill.fillOrigin = 0;
            timerFill.fillAmount = 0.3f;
            TMP_Text timerText = CreateText("Time", timer, "12s", 30f, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(240f, 48f), Color.white);
            AddOutline(timerText);

            // Bubble above the badge.
            RectTransform bubble = CreateRect("Bubble", template, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            bubble.anchoredPosition = new Vector2(0f, 112f);
            bubble.sizeDelta = new Vector2(120f, 130f);
            Image bubbleImage = bubble.gameObject.AddComponent<Image>();
            bubbleImage.sprite = LoadSprite("Assets/_Game/Art/Sprite/MainMenu/Reward/bubblepanel.png");
            bubbleImage.preserveAspect = true;
            var bubbleButton = bubble.gameObject.AddComponent<Button>();
            bubbleButton.targetGraphic = bubbleImage;
            Image bubbleIcon = CreateImage("Icon", bubble, null, new Vector2(0.5f, 0.5f), new Vector2(0f, 14f), new Vector2(66f, 66f), Color.white);
            TMP_Text bubbleAmount = CreateText("Amount", bubble, "+10", 34f, new Vector2(0.5f, 0.5f), new Vector2(0f, 72f), new Vector2(180f, 44f), Color.white);
            AddOutline(bubbleAmount);

            var so = new SerializedObject(view);
            so.FindProperty("_levelBadgeRoot").objectReferenceValue = badge.gameObject;
            so.FindProperty("_levelText").objectReferenceValue = level;
            so.FindProperty("_upgradeArrow").objectReferenceValue = arrow.gameObject;
            so.FindProperty("_timerRoot").objectReferenceValue = timer.gameObject;
            so.FindProperty("_timerFill").objectReferenceValue = timerFill;
            so.FindProperty("_timerText").objectReferenceValue = timerText;
            so.FindProperty("_bubbleButton").objectReferenceValue = bubbleButton;
            so.FindProperty("_bubbleIcon").objectReferenceValue = bubbleIcon;
            so.FindProperty("_bubbleAmountText").objectReferenceValue = bubbleAmount;
            so.ApplyModifiedPropertiesWithoutUndo();

            // The construction progress bar (navy bar, hammer, orange fill) replaces the plain timer above.
            ConstructionPlotVisualSetupEditor.RebuildIndicatorTimer(view);

            template.gameObject.SetActive(false);
        }

        private static void BuildQuestTracker(RectTransform root)
        {
            RectTransform tracker = CreateRect("QuestTracker", root, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), Vector2.zero, Vector2.zero);
            tracker.anchoredPosition = new Vector2(0f, 80f);
            tracker.sizeDelta = new Vector2(860f, 104f);
            var view = tracker.gameObject.AddComponent<BaseQuestTrackerView>();

            RectTransform body = CreateRect("Body", tracker, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            Image glow = CreateImage("Glow", body, LoadSprite("Assets/_Game/Art/Textures/UI/T_VictoryGlow.png"), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(1000f, 240f), new Color(1f, 0.85f, 0.3f, 0f));
            glow.preserveAspect = false;

            Image background = body.gameObject.AddComponent<Image>();
            background.sprite = LoadSprite("Assets/_Game/Art/Sprite/Minimap/T_MinimapPanel.png");
            background.type = Image.Type.Sliced;
            background.color = new Color(0.07f, 0.12f, 0.24f, 0.88f);
            var button = body.gameObject.AddComponent<Button>();
            button.targetGraphic = background;

            CreateImage("Icon", body, LoadSprite("Assets/_Game/Art/Sprite/Play/LevelUpdate/star.png"), new Vector2(0f, 0.5f),
                new Vector2(62f, 0f), new Vector2(68f, 68f), Color.white);
            TMP_Text label = CreateText("Label", body, "Build Headquarters lvl. 1 (0/1)", 36f, new Vector2(0.5f, 0.5f),
                new Vector2(40f, 0f), new Vector2(700f, 90f), Color.white);
            label.alignment = TextAlignmentOptions.MidlineLeft;

            var so = new SerializedObject(view);
            so.FindProperty("_root").objectReferenceValue = body.gameObject;
            so.FindProperty("_label").objectReferenceValue = label;
            so.FindProperty("_button").objectReferenceValue = button;
            so.FindProperty("_glow").objectReferenceValue = glow;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void BuildBanner(RectTransform root)
        {
            RectTransform banner = CreateRect("CompletionBanner", root, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), Vector2.zero, Vector2.zero);
            banner.anchoredPosition = new Vector2(0f, -300f);
            banner.sizeDelta = new Vector2(960f, 440f);
            var view = banner.gameObject.AddComponent<BaseCompletionBannerView>();
            var group = banner.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = false;
            group.interactable = false;

            Image glow = CreateImage("Glow", banner, LoadSprite("Assets/_Game/Art/Textures/UI/T_VictoryGlow.png"), new Vector2(0.5f, 0.5f),
                new Vector2(0f, 40f), new Vector2(760f, 760f), new Color(1f, 0.86f, 0.35f, 0f));

            RectTransform forceBlock = CreateRect("ForceBlock", banner, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            forceBlock.anchoredPosition = new Vector2(0f, 80f);
            forceBlock.sizeDelta = new Vector2(640f, 180f);
            CreateImage("Icon", forceBlock, LoadSprite("Assets/_Game/Art/Sprite/MainMenu/Avatar/lightningicon.png"), new Vector2(0.5f, 0.5f),
                new Vector2(-210f, 0f), new Vector2(110f, 110f), Color.white);
            TMP_Text forceLabel = CreateText("Label", forceBlock, "Force", 40f, new Vector2(0.5f, 0.5f), new Vector2(-20f, 48f), new Vector2(280f, 56f), Color.white);
            forceLabel.alignment = TextAlignmentOptions.MidlineLeft;
            AddOutline(forceLabel);
            TMP_Text forceValue = CreateText("Value", forceBlock, "1234", 84f, new Vector2(0.5f, 0.5f), new Vector2(-20f, -20f), new Vector2(280f, 100f), Color.white);
            forceValue.alignment = TextAlignmentOptions.MidlineLeft;
            AddOutline(forceValue);
            TMP_Text forceDelta = CreateText("Delta", forceBlock, "↑100", 50f, new Vector2(0.5f, 0.5f), new Vector2(170f, -20f), new Vector2(200f, 80f), new Color(0.45f, 1f, 0.45f));
            forceDelta.alignment = TextAlignmentOptions.MidlineLeft;
            AddOutline(forceDelta);

            RectTransform message = CreateRect("Message", banner, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            message.anchoredPosition = new Vector2(0f, -120f);
            message.sizeDelta = new Vector2(960f, 170f);
            var messageGroup = message.gameObject.AddComponent<CanvasGroup>();
            messageGroup.alpha = 0f;
            Image ribbon = message.gameObject.AddComponent<Image>();
            ribbon.color = new Color(0f, 0f, 0f, 0.45f);
            ribbon.raycastTarget = false;
            TMP_Text building = CreateText("Building", message, "Headquarters Lvl 2", 44f, new Vector2(0.5f, 0.5f), new Vector2(0f, 38f), new Vector2(900f, 64f), Color.white);
            TMP_Text done = CreateText("Done", message, "Upgrade done!", 58f, new Vector2(0.5f, 0.5f), new Vector2(0f, -32f), new Vector2(900f, 76f), new Color(1f, 0.86f, 0.2f));
            AddOutline(done);

            var so = new SerializedObject(view);
            so.FindProperty("_group").objectReferenceValue = group;
            so.FindProperty("_glow").objectReferenceValue = glow;
            so.FindProperty("_forceBlock").objectReferenceValue = forceBlock;
            so.FindProperty("_forceLabel").objectReferenceValue = forceLabel;
            so.FindProperty("_forceValue").objectReferenceValue = forceValue;
            so.FindProperty("_forceDelta").objectReferenceValue = forceDelta;
            so.FindProperty("_messageGroup").objectReferenceValue = messageGroup;
            so.FindProperty("_buildingText").objectReferenceValue = building;
            so.FindProperty("_doneText").objectReferenceValue = done;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void BuildPopup(RectTransform root)
        {
            Sprite panelSprite = LoadSprite("Assets/_Game/Art/Sprite/Minimap/T_MinimapPanel.png");

            RectTransform popupObject = CreateRect("BuildingPopup", root, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var view = popupObject.gameObject.AddComponent<BaseBuildingPopupView>();

            RectTransform popupRoot = CreateRect("Root", popupObject, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            RectTransform dim = CreateRect("Dim", popupRoot, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            Image dimImage = dim.gameObject.AddComponent<Image>();
            dimImage.color = new Color(0f, 0f, 0f, 0.55f);
            var dimGroup = dim.gameObject.AddComponent<CanvasGroup>();
            var dimButton = dim.gameObject.AddComponent<Button>();
            dimButton.targetGraphic = dimImage;
            dimButton.transition = Selectable.Transition.None;

            RectTransform panel = CreateRect("Panel", popupRoot, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            panel.anchoredPosition = new Vector2(0f, 10f);
            panel.sizeDelta = new Vector2(940f, 1200f);
            Image panelImage = panel.gameObject.AddComponent<Image>();
            panelImage.sprite = panelSprite;
            panelImage.type = Image.Type.Sliced;
            panelImage.color = PanelWhite;

            RectTransform header = CreateRect("Header", panel, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -124f), Vector2.zero);
            Image headerImage = header.gameObject.AddComponent<Image>();
            headerImage.sprite = panelSprite;
            headerImage.type = Image.Type.Sliced;
            headerImage.color = HeaderBlue;
            headerImage.raycastTarget = false;
            TMP_Text title = CreateText("Title", header, "Headquarters", 52f, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(660f, 100f), Color.white);
            AddOutline(title);

            RectTransform close = CreateRect("CloseButton", panel, new Vector2(1f, 1f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero);
            close.anchoredPosition = new Vector2(-62f, -62f);
            close.sizeDelta = new Vector2(100f, 100f);
            Image closeImage = close.gameObject.AddComponent<Image>();
            closeImage.sprite = LoadSprite("Assets/_Game/Art/Sprite/Play/PausePanel/closebtn.png");
            closeImage.preserveAspect = true;
            var closeButton = close.gameObject.AddComponent<Button>();
            closeButton.targetGraphic = closeImage;

            RectTransform body = CreateRect("Body", panel, Vector2.zero, Vector2.one, new Vector2(36f, 200f), new Vector2(-36f, -150f));
            var layout = body.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 20f;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            TMP_Text description = CreateText("Description", body, "Description", 32f, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(860f, 80f), TextDark);
            description.enableAutoSizing = false;
            description.fontSize = 32f;
            description.textWrappingMode = TextWrappingModes.Normal;

            GameObject bonusSection = CreateSection(body, "BonusSection", "Upgrade bonus", panelSprite, out TMP_Text bonusHeader, out RectTransform bonusRows);
            GameObject productionSection = CreateSection(body, "ProductionSection", "Extraction", panelSprite, out _, out RectTransform productionRows);
            GameObject requirementsSection = CreateSection(body, "RequirementsSection", "Requirements", panelSprite, out _, out RectTransform requirementRows);

            TMP_Text maxLevel = CreateText("MaxLevel", body, "Max level reached", 40f, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(860f, 70f), new Color(0.95f, 0.65f, 0.1f));
            maxLevel.gameObject.AddComponent<LayoutElement>().preferredHeight = 70f;

            // Footer buttons.
            Button start = CreateButton("StartButton", panel, panelSprite, true, StartBlue, new Vector2(0.5f, 0f), new Vector2(-218f, 100f),
                new Vector2(410f, 130f), "Start 5s", out TMP_Text startLabel);
            Button finish = CreateButton("FinishButton", panel, LoadSprite("Assets/_Game/Art/Sprite/MainMenu/Btn/StartBtn.png"), false, Color.white,
                new Vector2(0.5f, 0f), new Vector2(218f, 100f), new Vector2(410f, 130f), "Finish  5", out TMP_Text finishLabel);
            RectTransform finishLabelRect = finishLabel.rectTransform;
            finishLabelRect.anchoredPosition = new Vector2(-30f, 6f);
            finishLabelRect.sizeDelta = new Vector2(300f, 100f);
            CreateImage("Gem", finish.transform, LoadSprite("Assets/_Game/Art/Sprite/MainMenu/Avatar/diamondicon.png"), new Vector2(0.5f, 0.5f),
                new Vector2(140f, 6f), new Vector2(64f, 64f), Color.white);

            // Row templates (inactive; pooled by the view).
            RectTransform templates = CreateRect("Templates", panel, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero);
            BaseStatRowView statTemplate = BuildStatRow(templates);
            BaseRequirementRowView requirementTemplate = BuildRequirementRow(templates, panelSprite);

            var so = new SerializedObject(view);
            so.FindProperty("_root").objectReferenceValue = popupRoot.gameObject;
            so.FindProperty("_dim").objectReferenceValue = dimGroup;
            so.FindProperty("_panel").objectReferenceValue = panel;
            so.FindProperty("_titleText").objectReferenceValue = title;
            so.FindProperty("_closeButton").objectReferenceValue = closeButton;
            so.FindProperty("_dimCloseButton").objectReferenceValue = dimButton;
            so.FindProperty("_descriptionText").objectReferenceValue = description;
            so.FindProperty("_bonusSection").objectReferenceValue = bonusSection;
            so.FindProperty("_bonusHeader").objectReferenceValue = bonusHeader;
            so.FindProperty("_bonusRows").objectReferenceValue = bonusRows;
            so.FindProperty("_productionSection").objectReferenceValue = productionSection;
            so.FindProperty("_productionRows").objectReferenceValue = productionRows;
            so.FindProperty("_requirementsSection").objectReferenceValue = requirementsSection;
            so.FindProperty("_requirementRows").objectReferenceValue = requirementRows;
            so.FindProperty("_statRowTemplate").objectReferenceValue = statTemplate;
            so.FindProperty("_requirementRowTemplate").objectReferenceValue = requirementTemplate;
            so.FindProperty("_startButton").objectReferenceValue = start;
            so.FindProperty("_startLabel").objectReferenceValue = startLabel;
            so.FindProperty("_finishButton").objectReferenceValue = finish;
            so.FindProperty("_finishLabel").objectReferenceValue = finishLabel;
            so.FindProperty("_maxLevelLabel").objectReferenceValue = maxLevel.gameObject;
            so.FindProperty("_body").objectReferenceValue = body;
            so.ApplyModifiedPropertiesWithoutUndo();

            templates.gameObject.SetActive(false);
            popupRoot.gameObject.SetActive(false);
        }

        private static GameObject CreateSection(RectTransform parent, string name, string headerText, Sprite sprite,
            out TMP_Text header, out RectTransform rows)
        {
            RectTransform section = CreateRect(name, parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            Image background = section.gameObject.AddComponent<Image>();
            background.sprite = sprite;
            background.type = Image.Type.Sliced;
            background.color = SectionBlue;
            background.raycastTarget = false;
            var layout = section.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(24, 24, 14, 18);
            layout.spacing = 6f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            header = CreateText("Header", section, headerText, 36f, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(800f, 50f), HeaderBlue);
            header.alignment = TextAlignmentOptions.MidlineLeft;
            header.gameObject.AddComponent<LayoutElement>().preferredHeight = 50f;

            rows = CreateRect("Rows", section, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            var rowsLayout = rows.gameObject.AddComponent<VerticalLayoutGroup>();
            rowsLayout.spacing = 6f;
            rowsLayout.childControlWidth = true;
            rowsLayout.childControlHeight = true;
            rowsLayout.childForceExpandWidth = true;
            rowsLayout.childForceExpandHeight = false;
            return section.gameObject;
        }

        private static BaseStatRowView BuildStatRow(Transform parent)
        {
            RectTransform row = CreateRect("StatRowTemplate", parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            row.sizeDelta = new Vector2(820f, 62f);
            row.gameObject.AddComponent<LayoutElement>().preferredHeight = 62f;
            var view = row.gameObject.AddComponent<BaseStatRowView>();

            Image icon = CreateImage("Icon", row, null, new Vector2(0f, 0.5f), new Vector2(28f, 0f), new Vector2(52f, 52f), Color.white);
            TMP_Text label = CreateText("Label", row, "Building power", 32f, new Vector2(0f, 0.5f), new Vector2(280f, 0f), new Vector2(420f, 56f), TextDark);
            label.alignment = TextAlignmentOptions.MidlineLeft;
            TMP_Text value = CreateText("Value", row, "100", 36f, new Vector2(1f, 0.5f), new Vector2(-230f, 0f), new Vector2(220f, 56f), TextDark);
            value.alignment = TextAlignmentOptions.MidlineRight;
            TMP_Text delta = CreateText("Delta", row, "+100", 32f, new Vector2(1f, 0.5f), new Vector2(-60f, 0f), new Vector2(110f, 56f), DeltaGreen);
            delta.alignment = TextAlignmentOptions.MidlineLeft;

            var so = new SerializedObject(view);
            so.FindProperty("_icon").objectReferenceValue = icon;
            so.FindProperty("_label").objectReferenceValue = label;
            so.FindProperty("_value").objectReferenceValue = value;
            so.FindProperty("_delta").objectReferenceValue = delta;
            so.ApplyModifiedPropertiesWithoutUndo();
            return view;
        }

        private static BaseRequirementRowView BuildRequirementRow(Transform parent, Sprite panelSprite)
        {
            RectTransform row = CreateRect("RequirementRowTemplate", parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            row.sizeDelta = new Vector2(820f, 72f);
            row.gameObject.AddComponent<LayoutElement>().preferredHeight = 72f;
            var view = row.gameObject.AddComponent<BaseRequirementRowView>();

            Image icon = CreateImage("Icon", row, null, new Vector2(0f, 0.5f), new Vector2(30f, 0f), new Vector2(56f, 56f), Color.white);
            TMP_Text label = CreateText("Label", row, "Headquarters Lvl 2", 32f, new Vector2(0f, 0.5f), new Vector2(300f, 0f), new Vector2(460f, 60f), TextDark);
            label.alignment = TextAlignmentOptions.MidlineLeft;
            Image check = CreateImage("Check", row, LoadSprite("Assets/_Game/Art/Sprite/MainMenu/Reward/tick-icon.png"), new Vector2(1f, 0.5f),
                new Vector2(-190f, 0f), new Vector2(52f, 52f), Color.white);
            Image cross = CreateImage("Cross", row, LoadSprite("Assets/_Game/Art/Sprite/Play/PausePanel/closebtn.png"), new Vector2(1f, 0.5f),
                new Vector2(-190f, 0f), new Vector2(48f, 48f), Color.white);
            Button go = CreateButton("GoButton", row, panelSprite, true, StartBlue, new Vector2(1f, 0.5f), new Vector2(-70f, 0f),
                new Vector2(120f, 62f), "GO", out TMP_Text goLabel);
            goLabel.fontSize = 32f;
            goLabel.fontSizeMax = 32f;

            var so = new SerializedObject(view);
            so.FindProperty("_icon").objectReferenceValue = icon;
            so.FindProperty("_label").objectReferenceValue = label;
            so.FindProperty("_check").objectReferenceValue = check.gameObject;
            so.FindProperty("_cross").objectReferenceValue = cross.gameObject;
            so.FindProperty("_goButton").objectReferenceValue = go;
            so.ApplyModifiedPropertiesWithoutUndo();
            return view;
        }

        // ------------------------------------------------------------------ Wiring

        private static void Wire(Scene scene, GameObject world, GameObject ui, Transform basePanel)
        {
            var presenter = ui.GetComponent<BaseWorldPresenter>();
            var cameraController = world.GetComponentInChildren<BaseCameraController>(true);
            BaseBuildingView[] views = world.GetComponentsInChildren<BaseBuildingView>(true);
            MenuShellPresenter shell = FindInScene<MenuShellPresenter>(scene);
            Transform mainCamera = FindNamed(scene, "MainCamera");
            Transform background = FindNamed(scene, "BackgroundOverlay");

            var so = new SerializedObject(presenter);
            so.FindProperty("_worldRoot").objectReferenceValue = world;
            Transform staticScenery = world.transform.Find("Environment");
            so.FindProperty("_staticBatchRoot").objectReferenceValue = staticScenery != null ? staticScenery.gameObject : null;
            so.FindProperty("_cameraController").objectReferenceValue = cameraController;
            SerializedProperty viewArray = so.FindProperty("_buildingViews");
            viewArray.arraySize = views.Length;
            for (int i = 0; i < views.Length; i++)
            {
                viewArray.GetArrayElementAtIndex(i).objectReferenceValue = views[i];
            }

            so.FindProperty("_buildingLayers").intValue = 1 << BaseLayer;
            so.FindProperty("_menuCamera").objectReferenceValue = mainCamera != null ? mainCamera.GetComponent<Camera>() : null;
            so.FindProperty("_menuBackground").objectReferenceValue = background != null ? background.gameObject : null;
            so.FindProperty("_indicatorLayer").objectReferenceValue = ui.transform.Find("IndicatorLayer");
            so.FindProperty("_indicatorTemplate").objectReferenceValue = ui.GetComponentInChildren<BaseBuildingIndicatorView>(true);
            so.FindProperty("_popup").objectReferenceValue = ui.GetComponentInChildren<BaseBuildingPopupView>(true);
            so.FindProperty("_banner").objectReferenceValue = ui.GetComponentInChildren<BaseCompletionBannerView>(true);
            so.FindProperty("_questTracker").objectReferenceValue = ui.GetComponentInChildren<BaseQuestTrackerView>(true);
            so.FindProperty("_menuShell").objectReferenceValue = shell;
            so.FindProperty("_items").objectReferenceValue = AssetDatabase.LoadAssetAtPath<MetaItemCatalog>(MetaCatalogPath);
            so.FindProperty("_buildBubbleIcon").objectReferenceValue = LoadSprite("Assets/_Game/Art/Sprite/Play/HUD/icon-arrow-up-green.png");
            so.FindProperty("_powerIcon").objectReferenceValue = LoadSprite("Assets/_Game/Art/Sprite/MainMenu/Avatar/lightningicon.png");
            so.FindProperty("_campaignIcon").objectReferenceValue = LoadSprite("Assets/_Game/Art/Sprite/Play/LevelUpdate/ufoicon.png");
            SerializedProperty production = so.FindProperty("_productionIcon");
            if (production.objectReferenceValue == null)
            {
                production.objectReferenceValue = LoadSprite(IconsFolder + "/T_Icon_Wood_Placeholder.png");
            }

            so.ApplyModifiedPropertiesWithoutUndo();

            if (shell != null)
            {
                var shellSo = new SerializedObject(shell);
                shellSo.FindProperty("_baseWorldPresenter").objectReferenceValue = presenter;
                shellSo.ApplyModifiedPropertiesWithoutUndo();
            }
            else
            {
                Missing.Add("MainMenu has no MenuShellPresenter - BaseWorldPresenter never receives services.");
            }

            // The old list-style Base content would sit on top of the world and swallow every drag.
            Transform oldScroll = basePanel.Find("BaseScroll");
            if (oldScroll != null)
            {
                oldScroll.gameObject.SetActive(false);
            }

            // BaseWorldUI must draw above anything else left in the panel.
            ui.transform.SetAsLastSibling();

            // Other lights in MainMenu (the profile UFO preview's three) light "Everything"; keep them off the base
            // so it is lit by its own single sun only.
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (Light light in root.GetComponentsInChildren<Light>(true))
                {
                    if (light.transform.IsChildOf(world.transform))
                    {
                        continue;
                    }

                    if ((light.cullingMask & (1 << BaseLayer)) != 0)
                    {
                        light.cullingMask &= ~(1 << BaseLayer);
                        EditorUtility.SetDirty(light);
                    }
                }
            }

            if (mainCamera == null) Missing.Add("MainMenu 'MainCamera' not found - BaseWorldPresenter._menuCamera is empty.");
            if (background == null) Missing.Add("MainMenu 'BackgroundOverlay' not found - BaseWorldPresenter._menuBackground is empty.");
            Missing.Add("Time icon (clock) - no asset; BaseWorldPresenter._timeIcon left empty (row shows no icon).");
            Missing.Add("Audio - MainMenu has no AudioService; BaseWorldPresenter._audio and the 4 clips are empty (silent).");
            Missing.Add("NPC workers - no human/character models in the project; no NPCs placed.");
        }

        // ------------------------------------------------------------------ Helpers (world)

        private static Material EnsureMaterial(string name, Color color, float smoothness)
        {
            EnsureFolder(MaterialsFolder);
            string path = MaterialsFolder + "/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null)
            {
                return material;
            }

            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            material = new Material(shader) { name = name, enableInstancing = true };
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Smoothness", smoothness);
            material.SetFloat("_Metallic", 0f);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static GameObject CreateSlab(string name, Transform parent, Vector3 localPosition, Vector3 size, Material material)
        {
            GameObject slab = GameObject.CreatePrimitive(PrimitiveType.Cube);
            slab.name = name;
            Object.DestroyImmediate(slab.GetComponent<Collider>());
            slab.transform.SetParent(parent, false);
            slab.transform.localPosition = localPosition;
            slab.transform.localScale = size;
            var renderer = slab.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return slab;
        }

        private static void AddIfFound(List<(GameObject prefab, float scale)> pool, string name, float scale)
        {
            GameObject prefab = FindPrefab(name);
            if (prefab != null)
            {
                pool.Add((prefab, scale));
            }
            else
            {
                Missing.Add("Nature prefab " + name + " - skipped.");
            }
        }

        private static readonly Dictionary<string, GameObject> PrefabCache = new Dictionary<string, GameObject>();

        /// <summary>Finds a prefab by exact file name anywhere in Assets (no guessed paths). Null when absent.</summary>
        private static GameObject FindPrefab(string fileName)
        {
            if (PrefabCache.TryGetValue(fileName, out GameObject cached) && cached != null)
            {
                return cached;
            }

            foreach (string guid in AssetDatabase.FindAssets(fileName + " t:Prefab"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (System.IO.Path.GetFileNameWithoutExtension(path) == fileName && !path.Contains("/Demo"))
                {
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    PrefabCache[fileName] = prefab;
                    return prefab;
                }
            }

            return null;
        }

        private static BaseBuildingDefinition FindDefinition(BaseBuildingCatalog catalog, string id)
        {
            foreach (BaseBuildingDefinition definition in catalog.Buildings)
            {
                if (definition != null && definition.Id == id)
                {
                    return definition;
                }
            }

            return null;
        }

        private static bool TryGetBounds(GameObject root, out Bounds bounds)
        {
            bounds = new Bounds(root.transform.position, Vector3.zero);
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0)
            {
                return false;
            }

            bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
            {
                bounds.Encapsulate(renderers[i].bounds);
            }

            return true;
        }

        private static void SetLayerRecursively(GameObject root, int layer)
        {
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                t.gameObject.layer = layer;
            }
        }

        private static void MarkEnvironmentStatic(GameObject environment)
        {
            foreach (Transform t in environment.GetComponentsInChildren<Transform>(true))
            {
                GameObjectUtility.SetStaticEditorFlags(t.gameObject, StaticEditorFlags.BatchingStatic);
            }
        }

        private static void DisableDecorColliders(GameObject environment)
        {
            foreach (Collider c in environment.GetComponentsInChildren<Collider>(true))
            {
                c.enabled = false;
            }
        }

        private static float Range(System.Random random, float min, float max)
        {
            return min + (float)random.NextDouble() * (max - min);
        }

        // ------------------------------------------------------------------ Helpers (UI)

        private static RectTransform CreateRect(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax,
            Vector2 offsetMin, Vector2 offsetMax)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = LayerMask.NameToLayer("UI");
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
            return rect;
        }

        private static Image CreateImage(string name, Transform parent, Sprite sprite, Vector2 anchor, Vector2 position,
            Vector2 size, Color color)
        {
            RectTransform rect = CreateRect(name, parent, anchor, anchor, Vector2.zero, Vector2.zero);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.raycastTarget = false;
            image.preserveAspect = true;
            return image;
        }

        private static TMP_Text CreateText(string name, Transform parent, string content, float size, Vector2 anchor,
            Vector2 position, Vector2 rectSize, Color color)
        {
            RectTransform rect = CreateRect(name, parent, anchor, anchor, Vector2.zero, Vector2.zero);
            rect.anchoredPosition = position;
            rect.sizeDelta = rectSize;
            var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            if (font != null)
            {
                text.font = font;
            }

            text.text = content;
            text.fontSize = size;
            text.color = color;
            text.alignment = TextAlignmentOptions.Center;
            text.raycastTarget = false;
            text.enableAutoSizing = true;
            text.fontSizeMin = size * 0.5f;
            text.fontSizeMax = size;
            return text;
        }

        /// <summary>Dark outline via TMP's per-text material properties would need a material instance per label;
        /// a Shadow component is cheap and keeps the shared font material.</summary>
        private static void AddOutline(TMP_Text text)
        {
            var shadow = text.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.6f);
            shadow.effectDistance = new Vector2(2f, -3f);
        }

        private static Button CreateButton(string name, Transform parent, Sprite sprite, bool sliced, Color color, Vector2 anchor,
            Vector2 position, Vector2 size, string label, out TMP_Text labelText)
        {
            RectTransform rect = CreateRect(name, parent, anchor, anchor, Vector2.zero, Vector2.zero);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.type = sliced ? Image.Type.Sliced : Image.Type.Simple;
            image.color = color;
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            ColorBlock colors = button.colors;
            colors.disabledColor = new Color(0.55f, 0.55f, 0.55f, 0.8f);
            button.colors = colors;
            labelText = CreateText("Label", rect, label, 40f, new Vector2(0.5f, 0.5f), new Vector2(0f, 4f), size - new Vector2(30f, 20f), Color.white);
            AddOutline(labelText);
            return button;
        }

        // ------------------------------------------------------------------ Helpers (general)

        private static Scene OpenScene(string path)
        {
            Scene active = SceneManager.GetSceneByPath(path);
            if (active.isLoaded)
            {
                return active;
            }

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return default;
            }

            return EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
        }

        private static GameObject FindRoot(Scene scene, string name)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.name == name)
                {
                    return root;
                }
            }

            return null;
        }

        private static Transform FindNamed(Scene scene, string name)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                {
                    if (t.name == name)
                    {
                        return t;
                    }
                }
            }

            return null;
        }

        private static T FindInScene<T>(Scene scene) where T : Component
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                T found = root.GetComponentInChildren<T>(true);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }

        private static Sprite LoadSprite(string path)
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null)
            {
                Missing.Add("UI sprite " + path);
            }

            return sprite;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            string parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(path));
        }

        private static void ReportMissing()
        {
            if (Missing.Count == 0)
            {
                return;
            }

            var sb = new System.Text.StringBuilder("[BaseSceneSetupTool] MISSING ASSET / placeholder report:\n");
            foreach (string line in Missing)
            {
                sb.Append(" - ").AppendLine(line);
            }

            Debug.LogWarning(sb.ToString());
        }

        private static Color Hex(string hex)
        {
            return ColorUtility.TryParseHtmlString(hex, out Color color) ? color : Color.white;
        }
    }
}
