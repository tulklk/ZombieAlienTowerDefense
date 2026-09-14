using AlienDefense.Enemies;
using AlienDefense.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace AlienDefense.EditorTools
{
    /// <summary>Builds (or reuses) the Phase 15 advanced enemy definitions and prototype prefabs: Armored (EnemyDefense) and Shield (EnemyShield).</summary>
    internal static class AdvancedEnemyPrefabBuilder
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
            public Vector3 Scale;
            public Color Color;
            public float TractorResistance;
        }

        private static readonly EnemySpec ArmoredSpec = new EnemySpec
        {
            Id = "enemy_armored", DisplayName = "Armored Enemy", PrefabName = "Enemy_Armored",
            MaxHealth = 260f, MoveSpeed = 1.3f, RotationSpeed = 240f,
            Reward = 20, BaseDamage = 2,
            Scale = new Vector3(1f, 1.05f, 1f), Color = new Color(0.4f, 0.42f, 0.45f),
            TractorResistance = 1.5f
        };

        private static readonly EnemySpec ShieldSpec = new EnemySpec
        {
            Id = "enemy_shield", DisplayName = "Shield Enemy", PrefabName = "Enemy_Shield",
            MaxHealth = 140f, MoveSpeed = 1.6f, RotationSpeed = 300f,
            Reward = 18, BaseDamage = 1,
            Scale = new Vector3(0.85f, 0.95f, 0.85f), Color = new Color(0.25f, 0.55f, 0.85f),
            TractorResistance = 1.2f
        };

        private const float ArmoredPhysicalReduction = 0.6f;
        private const float ShieldMax = 50f;
        private const float ShieldRegenPerSecond = 5f;
        private const float ShieldRegenDelay = 3f;

        [MenuItem("AlienDefense/Setup/18. Create Advanced Enemy Definitions And Prefabs")]
        public static void CreateAll()
        {
            EditorFolderUtility.EnsureFolder(DataFolder);
            EditorFolderUtility.EnsureFolder(PrefabFolder);

            CreateArmored();
            CreateShield();

            Debug.Log("[AlienDefense Setup] Advanced enemy definitions and prefabs ready.");
        }

        private static void CreateArmored()
        {
            string definitionPath = $"{DataFolder}/EnemyDefinition_{ArmoredSpec.PrefabName}.asset";
            var definition = AssetDatabase.LoadAssetAtPath<EnemyDefinition>(definitionPath);
            if (definition == null)
            {
                definition = ScriptableObject.CreateInstance<EnemyDefinition>();
                AssetDatabase.CreateAsset(definition, definitionPath);
            }

            string prefabPath = $"{PrefabFolder}/{ArmoredSpec.PrefabName}.prefab";
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null)
            {
                GameObject root = BuildHierarchy(ArmoredSpec, addDefense: true, addShield: false);
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                Object.DestroyImmediate(root);
                prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            }
            else
            {
                MigrateAddCaptureController(prefab, prefabPath);
            }

            ApplyDefinition(definition, ArmoredSpec, prefab);
        }

        private static void CreateShield()
        {
            string definitionPath = $"{DataFolder}/EnemyDefinition_{ShieldSpec.PrefabName}.asset";
            var definition = AssetDatabase.LoadAssetAtPath<EnemyDefinition>(definitionPath);
            if (definition == null)
            {
                definition = ScriptableObject.CreateInstance<EnemyDefinition>();
                AssetDatabase.CreateAsset(definition, definitionPath);
            }

            string prefabPath = $"{PrefabFolder}/{ShieldSpec.PrefabName}.prefab";
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null)
            {
                GameObject root = BuildHierarchy(ShieldSpec, addDefense: false, addShield: true);
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                Object.DestroyImmediate(root);
                prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            }
            else
            {
                MigrateAddCaptureController(prefab, prefabPath);
            }

            ApplyDefinition(definition, ShieldSpec, prefab);
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
            EnemyCaptureController captureController = EnemyPrefabBuilder.AddCaptureController(contents, visualRoot);

            var controllerSerialized = new SerializedObject(controller);
            controllerSerialized.FindProperty("_captureController").objectReferenceValue = captureController;
            controllerSerialized.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(contents, prefabPath);
            PrefabUtility.UnloadPrefabContents(contents);
            Debug.Log("[AlienDefense Setup] Migrated " + prefabPath + ": added EnemyCaptureController.");
        }

        private static void ApplyDefinition(EnemyDefinition definition, EnemySpec spec, GameObject prefab)
        {
            var serialized = new SerializedObject(definition);
            serialized.FindProperty("_id").stringValue = spec.Id;
            serialized.FindProperty("_displayName").stringValue = spec.DisplayName;
            serialized.FindProperty("_maxHealth").floatValue = spec.MaxHealth;
            serialized.FindProperty("_moveSpeed").floatValue = spec.MoveSpeed;
            serialized.FindProperty("_rotationSpeed").floatValue = spec.RotationSpeed;
            serialized.FindProperty("_rewardResource").intValue = spec.Reward;
            serialized.FindProperty("_baseDamage").intValue = spec.BaseDamage;
            serialized.FindProperty("_poolPrewarmCount").intValue = 4;
            serialized.FindProperty("_poolDefaultCapacity").intValue = 10;
            serialized.FindProperty("_poolMaximumSize").intValue = 40;
            serialized.FindProperty("_canBeTractorCaptured").boolValue = true;
            serialized.FindProperty("_tractorResistance").floatValue = spec.TractorResistance;
            serialized.FindProperty("_prefab").objectReferenceValue = prefab != null ? prefab.GetComponent<EnemyController>() : null;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            AssetDatabase.SaveAssets();
        }

        private static GameObject BuildHierarchy(EnemySpec spec, bool addDefense, bool addShield)
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

            EnemyDefense defense = null;
            if (addDefense)
            {
                defense = root.AddComponent<EnemyDefense>();
                var defenseSerialized = new SerializedObject(defense);
                defenseSerialized.FindProperty("_physicalDamageReductionPercent").floatValue = ArmoredPhysicalReduction;
                defenseSerialized.ApplyModifiedPropertiesWithoutUndo();
            }

            EnemyShield shield = null;
            EnemyShieldBarView shieldBarView = null;
            if (addShield)
            {
                shield = root.AddComponent<EnemyShield>();
                var shieldSerialized = new SerializedObject(shield);
                shieldSerialized.FindProperty("_maxShield").floatValue = ShieldMax;
                shieldSerialized.FindProperty("_regenPerSecond").floatValue = ShieldRegenPerSecond;
                shieldSerialized.FindProperty("_regenDelayAfterHit").floatValue = ShieldRegenDelay;
                shieldSerialized.ApplyModifiedPropertiesWithoutUndo();

                var shieldBarAnchor = new GameObject("ShieldBarAnchor");
                shieldBarAnchor.transform.SetParent(root.transform, false);
                shieldBarAnchor.transform.localPosition = new Vector3(0f, spec.Scale.y + 0.85f, 0f);
                shieldBarView = BuildShieldBar(shieldBarAnchor.transform, shield);
            }

            var statusController = root.AddComponent<EnemyStatusController>();
            var statusSerialized = new SerializedObject(statusController);
            statusSerialized.FindProperty("_health").objectReferenceValue = health;
            statusSerialized.FindProperty("_movement").objectReferenceValue = movement;
            statusSerialized.ApplyModifiedPropertiesWithoutUndo();

            EnemyCaptureController captureController = EnemyPrefabBuilder.AddCaptureController(root, visualRoot.transform);

            var controllerSerialized = new SerializedObject(controller);
            controllerSerialized.FindProperty("_health").objectReferenceValue = health;
            controllerSerialized.FindProperty("_movement").objectReferenceValue = movement;
            controllerSerialized.FindProperty("_targetPoint").objectReferenceValue = targetPoint.transform;
            controllerSerialized.FindProperty("_healthBarView").objectReferenceValue = healthBarView;
            controllerSerialized.FindProperty("_statusController").objectReferenceValue = statusController;
            controllerSerialized.FindProperty("_defense").objectReferenceValue = defense;
            controllerSerialized.FindProperty("_shield").objectReferenceValue = shield;
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

        private static EnemyShieldBarView BuildShieldBar(Transform parent, EnemyShield shield)
        {
            var canvasObject = new GameObject("EnemyShieldBarCanvas", typeof(Canvas));
            canvasObject.transform.SetParent(parent, false);
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvasObject.transform.localScale = Vector3.one * 0.01f;

            var canvasRect = canvasObject.GetComponent<RectTransform>();
            canvasRect.sizeDelta = new Vector2(120f, 12f);

            var fill = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            fill.transform.SetParent(canvasObject.transform, false);
            var fillRect = fill.GetComponent<RectTransform>();
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;
            var fillImage = fill.GetComponent<Image>();
            fillImage.sprite = EditorScreenBuildingBlocks.SquareBarSprite();
            fillImage.color = new Color(0.4f, 0.75f, 1f, 0.9f);
            fillImage.type = Image.Type.Filled;
            fillImage.fillMethod = Image.FillMethod.Horizontal;
            fillImage.fillAmount = 1f;

            var billboard = canvasObject.AddComponent<WorldSpaceBillboard>();

            var view = canvasObject.AddComponent<EnemyShieldBarView>();
            var viewSerialized = new SerializedObject(view);
            viewSerialized.FindProperty("_shield").objectReferenceValue = shield;
            viewSerialized.FindProperty("_fillImage").objectReferenceValue = fillImage;
            viewSerialized.FindProperty("_visualRoot").objectReferenceValue = canvasObject;
            viewSerialized.FindProperty("_billboard").objectReferenceValue = billboard;
            viewSerialized.ApplyModifiedPropertiesWithoutUndo();

            return view;
        }
    }
}
