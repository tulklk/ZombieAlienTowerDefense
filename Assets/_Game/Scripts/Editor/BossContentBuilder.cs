using AlienDefense.Enemies;
using AlienDefense.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace AlienDefense.EditorTools
{
    /// <summary>Builds (or reuses) the Phase 15 Boss: BossBehaviorDefinition, EnemyDefinition, and prototype prefab.</summary>
    internal static class BossContentBuilder
    {
        private const string DataFolder = "Assets/_Game/Data/Enemies";
        private const string PrefabFolder = "Assets/_Game/Prefabs/Enemies";
        private const string EnemyLayerName = "Enemy";
        private const string PrefabName = "Enemy_Boss";
        private const string MinionDefinitionPath = "Assets/_Game/Data/Enemies/EnemyDefinition_Enemy_Normal.asset";

        private const float MaxHealth = 1400f;
        private const float MoveSpeed = 1f;
        private const float RotationSpeed = 180f;
        private const int Reward = 150;
        private const int BaseDamage = 5;
        private static readonly Vector3 Scale = new Vector3(1.8f, 2f, 1.8f);
        private static readonly Color BossColor = new Color(0.5f, 0.1f, 0.55f);

        [MenuItem("AlienDefense/Setup/20. Create Boss Definition And Prefab")]
        public static EnemyDefinition CreateAll()
        {
            EditorFolderUtility.EnsureFolder(DataFolder);
            EditorFolderUtility.EnsureFolder(PrefabFolder);

            BossBehaviorDefinition behavior = CreateOrLoadBehavior();

            string definitionPath = $"{DataFolder}/EnemyDefinition_{PrefabName}.asset";
            var definition = AssetDatabase.LoadAssetAtPath<EnemyDefinition>(definitionPath);
            if (definition == null)
            {
                definition = ScriptableObject.CreateInstance<EnemyDefinition>();
                AssetDatabase.CreateAsset(definition, definitionPath);
            }

            string prefabPath = $"{PrefabFolder}/{PrefabName}.prefab";
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null)
            {
                GameObject root = BuildHierarchy(behavior);
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                Object.DestroyImmediate(root);
                prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            }

            var serialized = new SerializedObject(definition);
            serialized.FindProperty("_id").stringValue = "enemy_boss";
            serialized.FindProperty("_displayName").stringValue = "Alien Overlord";
            serialized.FindProperty("_maxHealth").floatValue = MaxHealth;
            serialized.FindProperty("_moveSpeed").floatValue = MoveSpeed;
            serialized.FindProperty("_rotationSpeed").floatValue = RotationSpeed;
            serialized.FindProperty("_rewardResource").intValue = Reward;
            serialized.FindProperty("_baseDamage").intValue = BaseDamage;
            serialized.FindProperty("_poolPrewarmCount").intValue = 1;
            serialized.FindProperty("_poolDefaultCapacity").intValue = 2;
            serialized.FindProperty("_poolMaximumSize").intValue = 2;
            serialized.FindProperty("_canBeTractorCaptured").boolValue = false;
            serialized.FindProperty("_tractorResistance").floatValue = 1f;
            serialized.FindProperty("_prefab").objectReferenceValue = prefab != null ? prefab.GetComponent<EnemyController>() : null;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            AssetDatabase.SaveAssets();
            Debug.Log("[AlienDefense Setup] Boss definition and prefab ready.");
            return definition;
        }

        private static BossBehaviorDefinition CreateOrLoadBehavior()
        {
            string path = $"{DataFolder}/BossBehaviorDefinition_{PrefabName}.asset";
            var behavior = AssetDatabase.LoadAssetAtPath<BossBehaviorDefinition>(path);
            if (behavior == null)
            {
                behavior = ScriptableObject.CreateInstance<BossBehaviorDefinition>();
                AssetDatabase.CreateAsset(behavior, path);
            }

            var minionDefinition = AssetDatabase.LoadAssetAtPath<EnemyDefinition>(MinionDefinitionPath);

            var serialized = new SerializedObject(behavior);
            serialized.FindProperty("_phaseTwoHealthThreshold").floatValue = 0.5f;
            serialized.FindProperty("_minionDefinition").objectReferenceValue = minionDefinition;
            serialized.FindProperty("_minionCountPerBurst").intValue = 2;
            serialized.FindProperty("_minionSpawnInterval").floatValue = 8f;
            serialized.FindProperty("_phaseTwoMinionIntervalMultiplier").floatValue = 0.6f;
            serialized.FindProperty("_phaseTwoSpeedMultiplier").floatValue = 1.4f;
            serialized.FindProperty("_phaseOneDamageReductionPercent").floatValue = 0.1f;
            serialized.FindProperty("_phaseTwoDamageReductionPercent").floatValue = 0.3f;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            AssetDatabase.SaveAssets();
            return behavior;
        }

        private static GameObject BuildHierarchy(BossBehaviorDefinition behavior)
        {
            var root = new GameObject(PrefabName);
            int enemyLayer = LayerMask.NameToLayer(EnemyLayerName);
            if (enemyLayer >= 0)
            {
                root.layer = enemyLayer;
            }

            var collider = root.AddComponent<CapsuleCollider>();
            collider.isTrigger = true;
            collider.center = new Vector3(0f, Scale.y * 0.5f, 0f);
            collider.height = Scale.y;
            collider.radius = Scale.x * 0.5f;

            var health = root.AddComponent<EnemyHealth>();
            var movement = root.AddComponent<EnemyMovement>();
            var controller = root.AddComponent<EnemyController>();

            var visualRoot = new GameObject("VisualRoot");
            visualRoot.transform.SetParent(root.transform, false);

            GameObject model = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            model.name = "Model";
            model.transform.SetParent(visualRoot.transform, false);
            model.transform.localScale = Scale;
            model.transform.localPosition = new Vector3(0f, Scale.y * 0.5f, 0f);
            Object.DestroyImmediate(model.GetComponent<Collider>());

            var renderer = model.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = EditorMaterialUtility.CreateOrLoadMaterial(
                "Mat_Enemy_" + PrefabName, "Universal Render Pipeline/Lit", BossColor);

            var hitFlash = root.AddComponent<EnemyHitFlash>();
            var hitFlashSerialized = new SerializedObject(hitFlash);
            hitFlashSerialized.FindProperty("_health").objectReferenceValue = health;
            hitFlashSerialized.FindProperty("_renderer").objectReferenceValue = renderer;
            hitFlashSerialized.ApplyModifiedPropertiesWithoutUndo();

            var targetPoint = new GameObject("TargetPoint");
            targetPoint.transform.SetParent(root.transform, false);
            targetPoint.transform.localPosition = new Vector3(0f, Scale.y * 0.5f, 0f);

            var healthBarAnchor = new GameObject("HealthBarAnchor");
            healthBarAnchor.transform.SetParent(root.transform, false);
            healthBarAnchor.transform.localPosition = new Vector3(0f, Scale.y + 0.7f, 0f);
            EnemyHealthBarView healthBarView = BuildHealthBar(healthBarAnchor.transform, health);

            var defense = root.AddComponent<EnemyDefense>();
            var defenseSerialized = new SerializedObject(defense);
            defenseSerialized.FindProperty("_physicalDamageReductionPercent").floatValue = 0.1f;
            defenseSerialized.ApplyModifiedPropertiesWithoutUndo();

            var statusController = root.AddComponent<EnemyStatusController>();
            var statusSerialized = new SerializedObject(statusController);
            statusSerialized.FindProperty("_health").objectReferenceValue = health;
            statusSerialized.FindProperty("_movement").objectReferenceValue = movement;
            statusSerialized.ApplyModifiedPropertiesWithoutUndo();

            var bossController = root.AddComponent<BossController>();
            var bossSerialized = new SerializedObject(bossController);
            bossSerialized.FindProperty("_enemyController").objectReferenceValue = controller;
            bossSerialized.FindProperty("_health").objectReferenceValue = health;
            bossSerialized.FindProperty("_movement").objectReferenceValue = movement;
            bossSerialized.FindProperty("_defense").objectReferenceValue = defense;
            bossSerialized.FindProperty("_behavior").objectReferenceValue = behavior;
            bossSerialized.ApplyModifiedPropertiesWithoutUndo();

            var controllerSerialized = new SerializedObject(controller);
            controllerSerialized.FindProperty("_health").objectReferenceValue = health;
            controllerSerialized.FindProperty("_movement").objectReferenceValue = movement;
            controllerSerialized.FindProperty("_targetPoint").objectReferenceValue = targetPoint.transform;
            controllerSerialized.FindProperty("_healthBarView").objectReferenceValue = healthBarView;
            controllerSerialized.FindProperty("_statusController").objectReferenceValue = statusController;
            controllerSerialized.FindProperty("_defense").objectReferenceValue = defense;
            controllerSerialized.FindProperty("_bossController").objectReferenceValue = bossController;
            controllerSerialized.ApplyModifiedPropertiesWithoutUndo();

            return root;
        }

        private static EnemyHealthBarView BuildHealthBar(Transform parent, EnemyHealth health)
        {
            var canvasObject = new GameObject("EnemyHealthBarCanvas", typeof(Canvas));
            canvasObject.transform.SetParent(parent, false);
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvasObject.transform.localScale = Vector3.one * 0.02f;

            var canvasRect = canvasObject.GetComponent<RectTransform>();
            canvasRect.sizeDelta = new Vector2(140f, 20f);

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
            fillImage.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            fillImage.color = new Color(0.75f, 0.15f, 0.2f);
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
