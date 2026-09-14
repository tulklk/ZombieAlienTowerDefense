using AlienDefense.Enemies;
using AlienDefense.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace AlienDefense.EditorTools
{
    /// <summary>Builds (or reuses) the three Phase 3 enemy definitions and their prototype prefabs.</summary>
    internal static class EnemyPrefabBuilder
    {
        private const string DataFolder = "Assets/_Game/Data/Enemies";
        private const string PrefabFolder = "Assets/_Game/Prefabs/Enemies";
        private const string EnemyLayerName = "Enemy";

        private struct EnemySpec
        {
            public string Id;
            public string DisplayName;
            public string PrefabName;
            public float MaxHealth;
            public float MoveSpeed;
            public float RotationSpeed;
            public int Reward;
            public int BaseDamage;
            public int PoolPrewarm;
            public int PoolDefault;
            public int PoolMax;
            public Vector3 Scale;
            public Color Color;
            public float TractorResistance;
        }

        private static readonly EnemySpec NormalSpec = new EnemySpec
        {
            Id = "enemy_normal", DisplayName = "Normal Enemy", PrefabName = "Enemy_Normal",
            MaxHealth = 100f, MoveSpeed = 1.8f, RotationSpeed = 360f,
            Reward = 10, BaseDamage = 1,
            PoolPrewarm = 10, PoolDefault = 20, PoolMax = 100,
            Scale = new Vector3(0.8f, 0.9f, 0.8f), Color = new Color(0.6f, 0.6f, 0.65f),
            TractorResistance = 1.0f
        };

        private static readonly EnemySpec RunnerSpec = new EnemySpec
        {
            Id = "enemy_runner", DisplayName = "Runner Enemy", PrefabName = "Enemy_Runner",
            MaxHealth = 60f, MoveSpeed = 3f, RotationSpeed = 540f,
            Reward = 9, BaseDamage = 1,
            PoolPrewarm = 8, PoolDefault = 16, PoolMax = 100,
            Scale = new Vector3(0.6f, 0.7f, 0.6f), Color = new Color(0.2f, 0.7f, 0.9f),
            TractorResistance = 0.8f
        };

        private static readonly EnemySpec TankSpec = new EnemySpec
        {
            Id = "enemy_tank", DisplayName = "Tank Enemy", PrefabName = "Enemy_Tank",
            MaxHealth = 350f, MoveSpeed = 1.1f, RotationSpeed = 240f,
            Reward = 25, BaseDamage = 2,
            PoolPrewarm = 5, PoolDefault = 10, PoolMax = 60,
            Scale = new Vector3(1.15f, 1.2f, 1.15f), Color = new Color(0.55f, 0.15f, 0.15f),
            TractorResistance = 1.7f
        };

        [MenuItem("AlienDefense/Setup/5. Create Enemy Definitions And Prefabs")]
        public static EnemyDefinition CreateAll()
        {
            EditorFolderUtility.EnsureFolder(DataFolder);
            EditorFolderUtility.EnsureFolder(PrefabFolder);

            EnemyDefinition normal = CreateOrLoadEnemy(NormalSpec);
            CreateOrLoadEnemy(RunnerSpec);
            CreateOrLoadEnemy(TankSpec);

            Debug.Log("[AlienDefense Setup] Enemy definitions and prefabs ready.");
            return normal;
        }

        private static EnemyDefinition CreateOrLoadEnemy(EnemySpec spec)
        {
            string definitionPath = $"{DataFolder}/EnemyDefinition_{spec.PrefabName}.asset";
            var definition = AssetDatabase.LoadAssetAtPath<EnemyDefinition>(definitionPath);

            if (definition == null)
            {
                definition = ScriptableObject.CreateInstance<EnemyDefinition>();
                AssetDatabase.CreateAsset(definition, definitionPath);
            }

            GameObject prefab = CreateOrLoadPrefab(spec);

            var serializedDefinition = new SerializedObject(definition);
            serializedDefinition.FindProperty("_id").stringValue = spec.Id;
            serializedDefinition.FindProperty("_displayName").stringValue = spec.DisplayName;
            serializedDefinition.FindProperty("_maxHealth").floatValue = spec.MaxHealth;
            serializedDefinition.FindProperty("_moveSpeed").floatValue = spec.MoveSpeed;
            serializedDefinition.FindProperty("_rotationSpeed").floatValue = spec.RotationSpeed;
            serializedDefinition.FindProperty("_rewardResource").intValue = spec.Reward;
            serializedDefinition.FindProperty("_baseDamage").intValue = spec.BaseDamage;
            serializedDefinition.FindProperty("_defeatedVfxDefinition").objectReferenceValue = VfxPrefabBuilder.EnemyDefeated;
            serializedDefinition.FindProperty("_poolPrewarmCount").intValue = spec.PoolPrewarm;
            serializedDefinition.FindProperty("_poolDefaultCapacity").intValue = spec.PoolDefault;
            serializedDefinition.FindProperty("_poolMaximumSize").intValue = spec.PoolMax;
            serializedDefinition.FindProperty("_canBeTractorCaptured").boolValue = true;
            serializedDefinition.FindProperty("_tractorResistance").floatValue = spec.TractorResistance;
            serializedDefinition.FindProperty("_prefab").objectReferenceValue =
                prefab != null ? prefab.GetComponent<EnemyController>() : null;
            serializedDefinition.ApplyModifiedPropertiesWithoutUndo();

            AssetDatabase.SaveAssets();
            return definition;
        }

        private static GameObject CreateOrLoadPrefab(EnemySpec spec)
        {
            string prefabPath = $"{PrefabFolder}/{spec.PrefabName}.prefab";

            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (existing != null)
            {
                MigrateRepairBrokenModelMaterial(existing, spec, prefabPath);
                MigrateFixHealthBarFillSprite(existing, prefabPath);
                MigrateAddStatusController(existing, prefabPath);
                MigrateAddCaptureController(existing, prefabPath);
                return existing;
            }

            GameObject root = BuildHierarchy(spec);
            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            Object.DestroyImmediate(root);

            return AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        }

        /// <summary>Re-links an existing prefab's Model renderer to its persistent material if it points elsewhere or nowhere.</summary>
        private static void MigrateRepairBrokenModelMaterial(GameObject prefabAsset, EnemySpec spec, string prefabPath)
        {
            Material material = EditorMaterialUtility.CreateOrLoadMaterial(
                "Mat_Enemy_" + spec.PrefabName, "Universal Render Pipeline/Lit", spec.Color);

            Transform modelTransform = prefabAsset.transform.Find("VisualRoot/Model");
            Renderer existingRenderer = modelTransform != null ? modelTransform.GetComponent<MeshRenderer>() : null;
            if (existingRenderer == null || existingRenderer.sharedMaterial == material)
            {
                return;
            }

            GameObject contents = PrefabUtility.LoadPrefabContents(prefabPath);

            Renderer renderer = contents.transform.Find("VisualRoot/Model")?.GetComponent<MeshRenderer>();
            if (renderer != null && renderer.sharedMaterial != material)
            {
                renderer.sharedMaterial = material;
                PrefabUtility.SaveAsPrefabAsset(contents, prefabPath);
                Debug.Log("[AlienDefense Setup] Migrated " + prefabPath + ": re-linked Model material to the persistent asset.");
            }

            PrefabUtility.UnloadPrefabContents(contents);
        }

        /// <summary>Assigns the Fill image's sprite on an existing prefab if missing, so Image.Type.Filled actually clips.</summary>
        private static void MigrateFixHealthBarFillSprite(GameObject prefabAsset, string prefabPath)
        {
            Transform fillTransform = prefabAsset.transform.Find("HealthBarAnchor/EnemyHealthBarCanvas/Fill");
            Image existingFill = fillTransform != null ? fillTransform.GetComponent<Image>() : null;
            if (existingFill == null || existingFill.sprite != null)
            {
                return;
            }

            GameObject contents = PrefabUtility.LoadPrefabContents(prefabPath);

            Image fillImage = contents.transform.Find("HealthBarAnchor/EnemyHealthBarCanvas/Fill")?.GetComponent<Image>();
            if (fillImage != null && fillImage.sprite == null)
            {
                fillImage.sprite = EditorScreenBuildingBlocks.SquareBarSprite();
                PrefabUtility.SaveAsPrefabAsset(contents, prefabPath);
                Debug.Log("[AlienDefense Setup] Migrated " + prefabPath + ": assigned Fill sprite so the health bar reflects damage.");
            }

            PrefabUtility.UnloadPrefabContents(contents);
        }

        /// <summary>Adds EnemyStatusController (Phase 15) to prefabs built before it existed, so Slow/Burn work on every enemy.</summary>
        private static void MigrateAddStatusController(GameObject prefabAsset, string prefabPath)
        {
            if (prefabAsset.GetComponent<EnemyStatusController>() != null)
            {
                return;
            }

            GameObject contents = PrefabUtility.LoadPrefabContents(prefabPath);

            var health = contents.GetComponent<EnemyHealth>();
            var movement = contents.GetComponent<EnemyMovement>();
            var controller = contents.GetComponent<EnemyController>();
            EnemyStatusController statusController = AddStatusController(contents, health, movement);

            var controllerSerialized = new SerializedObject(controller);
            controllerSerialized.FindProperty("_statusController").objectReferenceValue = statusController;
            controllerSerialized.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(contents, prefabPath);
            PrefabUtility.UnloadPrefabContents(contents);
            Debug.Log("[AlienDefense Setup] Migrated " + prefabPath + ": added EnemyStatusController.");
        }

        private static EnemyStatusController AddStatusController(GameObject root, EnemyHealth health, EnemyMovement movement)
        {
            var statusController = root.AddComponent<EnemyStatusController>();
            var serialized = new SerializedObject(statusController);
            serialized.FindProperty("_health").objectReferenceValue = health;
            serialized.FindProperty("_movement").objectReferenceValue = movement;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return statusController;
        }

        /// <summary>Adds EnemyCaptureController (Tractor Beam) to prefabs built before it existed.</summary>
        private static void MigrateAddCaptureController(GameObject prefabAsset, string prefabPath)
        {
            if (prefabAsset.GetComponent<EnemyCaptureController>() != null)
            {
                return;
            }

            GameObject contents = PrefabUtility.LoadPrefabContents(prefabPath);

            var controller = contents.GetComponent<EnemyController>();
            Transform visualRoot = contents.transform.Find("VisualRoot");
            EnemyCaptureController captureController = AddCaptureController(contents, visualRoot);

            var controllerSerialized = new SerializedObject(controller);
            controllerSerialized.FindProperty("_captureController").objectReferenceValue = captureController;
            controllerSerialized.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(contents, prefabPath);
            PrefabUtility.UnloadPrefabContents(contents);
            Debug.Log("[AlienDefense Setup] Migrated " + prefabPath + ": added EnemyCaptureController.");
        }

        internal static EnemyCaptureController AddCaptureController(GameObject root, Transform visualRoot)
        {
            var captureController = root.AddComponent<EnemyCaptureController>();
            var serialized = new SerializedObject(captureController);
            serialized.FindProperty("_visualRoot").objectReferenceValue = visualRoot;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return captureController;
        }

        private static GameObject BuildHierarchy(EnemySpec spec)
        {
            var root = new GameObject(spec.PrefabName);
            int enemyLayer = LayerMask.NameToLayer(EnemyLayerName);
            if (enemyLayer >= 0)
            {
                root.layer = enemyLayer;
            }

            var collider = root.AddComponent<CapsuleCollider>();
            collider.isTrigger = true;
            collider.center = new Vector3(0f, spec.Scale.y * 0.5f, 0f);
            collider.height = spec.Scale.y;
            collider.radius = spec.Scale.x * 0.5f;

            var health = root.AddComponent<EnemyHealth>();
            var movement = root.AddComponent<EnemyMovement>();
            var controller = root.AddComponent<EnemyController>();

            var visualRoot = new GameObject("VisualRoot");
            visualRoot.transform.SetParent(root.transform, false);

            GameObject model = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            model.name = "Model";
            model.transform.SetParent(visualRoot.transform, false);
            model.transform.localScale = spec.Scale;
            model.transform.localPosition = new Vector3(0f, spec.Scale.y * 0.5f, 0f);
            Object.DestroyImmediate(model.GetComponent<Collider>());

            var renderer = model.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = EditorMaterialUtility.CreateOrLoadMaterial(
                "Mat_Enemy_" + spec.PrefabName, "Universal Render Pipeline/Lit", spec.Color);

            var hitFlash = root.AddComponent<EnemyHitFlash>();
            var hitFlashSerialized = new SerializedObject(hitFlash);
            hitFlashSerialized.FindProperty("_health").objectReferenceValue = health;
            hitFlashSerialized.FindProperty("_renderer").objectReferenceValue = renderer;
            hitFlashSerialized.ApplyModifiedPropertiesWithoutUndo();

            var targetPoint = new GameObject("TargetPoint");
            targetPoint.transform.SetParent(root.transform, false);
            targetPoint.transform.localPosition = new Vector3(0f, spec.Scale.y * 0.5f, 0f);

            var healthBarAnchor = new GameObject("HealthBarAnchor");
            healthBarAnchor.transform.SetParent(root.transform, false);
            healthBarAnchor.transform.localPosition = new Vector3(0f, spec.Scale.y + 0.6f, 0f);

            EnemyHealthBarView healthBarView = BuildHealthBar(healthBarAnchor.transform, health);
            EnemyStatusController statusController = AddStatusController(root, health, movement);
            EnemyCaptureController captureController = AddCaptureController(root, visualRoot.transform);

            var controllerSerialized = new SerializedObject(controller);
            controllerSerialized.FindProperty("_health").objectReferenceValue = health;
            controllerSerialized.FindProperty("_movement").objectReferenceValue = movement;
            controllerSerialized.FindProperty("_targetPoint").objectReferenceValue = targetPoint.transform;
            controllerSerialized.FindProperty("_healthBarView").objectReferenceValue = healthBarView;
            controllerSerialized.FindProperty("_statusController").objectReferenceValue = statusController;
            controllerSerialized.FindProperty("_captureController").objectReferenceValue = captureController;
            controllerSerialized.ApplyModifiedPropertiesWithoutUndo();

            return root;
        }

        private static EnemyHealthBarView BuildHealthBar(Transform parent, EnemyHealth health)
        {
            var canvasObject = new GameObject("EnemyHealthBarCanvas", typeof(Canvas));
            canvasObject.transform.SetParent(parent, false);
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvasObject.transform.localScale = Vector3.one * 0.01f;

            var canvasRect = canvasObject.GetComponent<RectTransform>();
            canvasRect.sizeDelta = new Vector2(120f, 18f);

            var background = new GameObject("Background", typeof(RectTransform), typeof(Image));
            background.transform.SetParent(canvasObject.transform, false);
            var backgroundRect = background.GetComponent<RectTransform>();
            backgroundRect.anchorMin = Vector2.zero;
            backgroundRect.anchorMax = Vector2.one;
            backgroundRect.offsetMin = Vector2.zero;
            backgroundRect.offsetMax = Vector2.zero;
            background.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.6f);

            var fill = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            fill.transform.SetParent(canvasObject.transform, false);
            var fillRect = fill.GetComponent<RectTransform>();
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.offsetMin = new Vector2(2f, 2f);
            fillRect.offsetMax = new Vector2(-2f, -2f);
            var fillImage = fill.GetComponent<Image>();
            fillImage.sprite = EditorScreenBuildingBlocks.SquareBarSprite();
            fillImage.color = new Color(0.2f, 0.85f, 0.3f);
            fillImage.type = Image.Type.Filled;
            fillImage.fillMethod = Image.FillMethod.Horizontal;
            fillImage.fillAmount = 1f;

            var billboard = canvasObject.AddComponent<WorldSpaceBillboard>();

            var view = canvasObject.AddComponent<EnemyHealthBarView>();
            var viewSerialized = new SerializedObject(view);
            viewSerialized.FindProperty("_health").objectReferenceValue = health;
            viewSerialized.FindProperty("_fillImage").objectReferenceValue = fillImage;
            viewSerialized.FindProperty("_visualRoot").objectReferenceValue = canvasObject;
            viewSerialized.FindProperty("_billboard").objectReferenceValue = billboard;
            viewSerialized.ApplyModifiedPropertiesWithoutUndo();

            return view;
        }
    }
}
