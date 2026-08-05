using AlienDefense.Combat;
using AlienDefense.Towers;
using UnityEditor;
using UnityEngine;

namespace AlienDefense.EditorTools
{
    /// <summary>Builds (or reuses) the three Phase 6 tower definitions and their prototype prefabs.</summary>
    internal static class TowerPrefabBuilder
    {
        private const string DataFolder = "Assets/_Game/Data/Towers";
        private const string PrefabFolder = "Assets/_Game/Prefabs/Towers";
        private const string TowerLayerName = "Tower";

        private const string ProjectileDefinitionPath = "Assets/_Game/Data/Projectiles/Projectile_Blaster.asset";

        private struct LevelSpec
        {
            public int UpgradeCost;
            public float Damage;
            public float Range;
            public float AttacksPerSecond;
            public float TurretRotationSpeed;

            public LevelSpec(int upgradeCost, float damage, float range, float attacksPerSecond, float turretRotationSpeed)
            {
                UpgradeCost = upgradeCost;
                Damage = damage;
                Range = range;
                AttacksPerSecond = attacksPerSecond;
                TurretRotationSpeed = turretRotationSpeed;
            }
        }

        private struct TowerSpec
        {
            public string Id;
            public string DisplayName;
            public string PrefabName;
            public int BuildCost;
            public float SellPercentage;
            public TargetingMode DefaultTargetingMode;
            public Color Color;
            public LevelSpec[] Levels;
        }

        private static readonly TowerSpec BlasterSpec = new TowerSpec
        {
            Id = "tower_blaster", DisplayName = "Blaster Tower", PrefabName = "Tower_Blaster",
            BuildCost = 75, SellPercentage = 0.5f, DefaultTargetingMode = TargetingMode.First,
            Color = new Color(0.25f, 0.55f, 0.95f),
            Levels = new[]
            {
                new LevelSpec(0, 20f, 4f, 1f, 720f),
                new LevelSpec(90, 32f, 4.4f, 1.2f, 720f),
                new LevelSpec(160, 50f, 4.8f, 1.4f, 720f)
            }
        };

        private static readonly TowerSpec RapidSpec = new TowerSpec
        {
            Id = "tower_rapid", DisplayName = "Rapid Gun Tower", PrefabName = "Tower_Rapid",
            BuildCost = 100, SellPercentage = 0.5f, DefaultTargetingMode = TargetingMode.Closest,
            Color = new Color(0.95f, 0.8f, 0.2f),
            Levels = new[]
            {
                new LevelSpec(0, 9f, 3.7f, 2.5f, 900f),
                new LevelSpec(110, 14f, 3.9f, 2.8f, 900f),
                new LevelSpec(190, 20f, 4.1f, 3.2f, 900f)
            }
        };

        private static readonly TowerSpec HeavySpec = new TowerSpec
        {
            Id = "tower_heavy", DisplayName = "Heavy Cannon Tower", PrefabName = "Tower_Heavy",
            BuildCost = 150, SellPercentage = 0.5f, DefaultTargetingMode = TargetingMode.Strongest,
            Color = new Color(0.5f, 0.15f, 0.15f),
            Levels = new[]
            {
                new LevelSpec(0, 60f, 5f, 0.45f, 360f),
                new LevelSpec(220, 90f, 5.3f, 0.5f, 360f),
                new LevelSpec(380, 130f, 5.6f, 0.55f, 360f)
            }
        };

        /// <summary>Reloads the three tower definitions fresh from disk. Use right before a use site that follows
        /// several AssetDatabase/PrefabUtility operations, which can otherwise leave an earlier in-memory reference stale.</summary>
        public static TowerDefinition[] LoadAll()
        {
            return new[]
            {
                AssetDatabase.LoadAssetAtPath<TowerDefinition>($"{DataFolder}/TowerDefinition_{BlasterSpec.PrefabName}.asset"),
                AssetDatabase.LoadAssetAtPath<TowerDefinition>($"{DataFolder}/TowerDefinition_{RapidSpec.PrefabName}.asset"),
                AssetDatabase.LoadAssetAtPath<TowerDefinition>($"{DataFolder}/TowerDefinition_{HeavySpec.PrefabName}.asset")
            };
        }

        [MenuItem("AlienDefense/Setup/8. Create Tower Definitions And Prefabs")]
        public static TowerDefinition[] CreateAll()
        {
            EditorFolderUtility.EnsureFolder(DataFolder);
            EditorFolderUtility.EnsureFolder(PrefabFolder);

            var projectileDefinition = AssetDatabase.LoadAssetAtPath<ProjectileDefinition>(ProjectileDefinitionPath);
            if (projectileDefinition == null)
            {
                projectileDefinition = ProjectilePrefabBuilder.CreateOrLoad();
            }

            TowerDefinition blaster = CreateOrLoadTower(BlasterSpec, projectileDefinition);
            TowerDefinition rapid = CreateOrLoadTower(RapidSpec, projectileDefinition);
            TowerDefinition heavy = CreateOrLoadTower(HeavySpec, projectileDefinition);

            Debug.Log("[AlienDefense Setup] Tower definitions and prefabs ready.");
            return new[] { blaster, rapid, heavy };
        }

        private static TowerDefinition CreateOrLoadTower(TowerSpec spec, ProjectileDefinition projectileDefinition)
        {
            string definitionPath = $"{DataFolder}/TowerDefinition_{spec.PrefabName}.asset";
            var definition = AssetDatabase.LoadAssetAtPath<TowerDefinition>(definitionPath);

            if (definition == null)
            {
                definition = ScriptableObject.CreateInstance<TowerDefinition>();
                AssetDatabase.CreateAsset(definition, definitionPath);
            }

            GameObject prefab = CreateOrLoadPrefab(spec);

            var serializedDefinition = new SerializedObject(definition);
            serializedDefinition.FindProperty("_id").stringValue = spec.Id;
            serializedDefinition.FindProperty("_displayName").stringValue = spec.DisplayName;
            serializedDefinition.FindProperty("_buildCost").intValue = spec.BuildCost;
            serializedDefinition.FindProperty("_sellPercentage").floatValue = spec.SellPercentage;
            serializedDefinition.FindProperty("_defaultTargetingMode").enumValueIndex = (int)spec.DefaultTargetingMode;
            serializedDefinition.FindProperty("_projectileDefinition").objectReferenceValue = projectileDefinition;
            serializedDefinition.FindProperty("_muzzleVfxDefinition").objectReferenceValue = VfxPrefabBuilder.MuzzleFlash;
            serializedDefinition.FindProperty("_prefab").objectReferenceValue =
                prefab != null ? prefab.GetComponent<TowerController>() : null;

            SerializedProperty levelsProperty = serializedDefinition.FindProperty("_levels");
            levelsProperty.arraySize = spec.Levels.Length;
            for (int i = 0; i < spec.Levels.Length; i++)
            {
                LevelSpec level = spec.Levels[i];
                SerializedProperty levelProperty = levelsProperty.GetArrayElementAtIndex(i);
                levelProperty.FindPropertyRelative("_upgradeCost").intValue = level.UpgradeCost;
                levelProperty.FindPropertyRelative("_damage").floatValue = level.Damage;
                levelProperty.FindPropertyRelative("_range").floatValue = level.Range;
                levelProperty.FindPropertyRelative("_attacksPerSecond").floatValue = level.AttacksPerSecond;
                levelProperty.FindPropertyRelative("_turretRotationSpeed").floatValue = level.TurretRotationSpeed;
            }

            serializedDefinition.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssets();
            return definition;
        }

        private static GameObject CreateOrLoadPrefab(TowerSpec spec)
        {
            string prefabPath = $"{PrefabFolder}/{spec.PrefabName}.prefab";

            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (existing != null)
            {
                return existing;
            }

            GameObject root = BuildHierarchy(spec);
            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            Object.DestroyImmediate(root);

            return AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        }

        private static GameObject BuildHierarchy(TowerSpec spec)
        {
            var root = new GameObject(spec.PrefabName);
            int towerLayer = LayerMask.NameToLayer(TowerLayerName);
            if (towerLayer >= 0)
            {
                root.layer = towerLayer;
            }

            var collider = root.AddComponent<BoxCollider>();
            collider.center = new Vector3(0f, 0.5f, 0f);
            collider.size = new Vector3(1f, 1f, 1f);

            var targeting = root.AddComponent<TowerTargeting>();
            var attack = root.AddComponent<TowerAttackController>();
            var visual = root.AddComponent<TowerVisual>();
            var controller = root.AddComponent<TowerController>();

            Material material = EditorMaterialUtility.CreateOrLoadMaterial(
                "Mat_" + spec.PrefabName, "Universal Render Pipeline/Lit", spec.Color);

            GameObject baseMesh = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            baseMesh.name = "Base";
            baseMesh.transform.SetParent(root.transform, false);
            baseMesh.transform.localScale = new Vector3(0.9f, 0.15f, 0.9f);
            baseMesh.transform.localPosition = new Vector3(0f, 0.15f, 0f);
            baseMesh.GetComponent<MeshRenderer>().sharedMaterial = material;
            Object.DestroyImmediate(baseMesh.GetComponent<Collider>());

            var turretPivot = new GameObject("TurretPivot");
            turretPivot.transform.SetParent(root.transform, false);
            turretPivot.transform.localPosition = new Vector3(0f, 0.4f, 0f);

            GameObject turretVisual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            turretVisual.name = "TurretVisual";
            turretVisual.transform.SetParent(turretPivot.transform, false);
            turretVisual.transform.localScale = new Vector3(0.3f, 0.3f, 0.9f);
            turretVisual.transform.localPosition = new Vector3(0f, 0f, 0.35f);
            turretVisual.GetComponent<MeshRenderer>().sharedMaterial = material;
            Object.DestroyImmediate(turretVisual.GetComponent<Collider>());

            var firePoint = new GameObject("FirePoint");
            firePoint.transform.SetParent(turretPivot.transform, false);
            firePoint.transform.localPosition = new Vector3(0f, 0f, 0.8f);

            RangeIndicator rangeIndicator = BuildRangeIndicator(root.transform);

            var attackSerialized = new SerializedObject(attack);
            attackSerialized.FindProperty("_firePoint").objectReferenceValue = firePoint.transform;
            attackSerialized.ApplyModifiedPropertiesWithoutUndo();

            var visualSerialized = new SerializedObject(visual);
            visualSerialized.FindProperty("_turretPivot").objectReferenceValue = turretPivot.transform;
            visualSerialized.FindProperty("_forwardAxis").vector3Value = Vector3.forward;
            visualSerialized.ApplyModifiedPropertiesWithoutUndo();

            var controllerSerialized = new SerializedObject(controller);
            controllerSerialized.FindProperty("_targeting").objectReferenceValue = targeting;
            controllerSerialized.FindProperty("_attack").objectReferenceValue = attack;
            controllerSerialized.FindProperty("_visual").objectReferenceValue = visual;
            controllerSerialized.FindProperty("_rangeIndicator").objectReferenceValue = rangeIndicator;
            controllerSerialized.ApplyModifiedPropertiesWithoutUndo();

            return root;
        }

        private static RangeIndicator BuildRangeIndicator(Transform parent)
        {
            var rangeIndicatorObject = new GameObject("RangeIndicator");
            rangeIndicatorObject.transform.SetParent(parent, false);
            var rangeIndicator = rangeIndicatorObject.AddComponent<RangeIndicator>();

            GameObject visualMesh = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            visualMesh.name = "Visual";
            visualMesh.transform.SetParent(rangeIndicatorObject.transform, false);
            visualMesh.transform.localScale = new Vector3(1f, 0.01f, 1f);
            visualMesh.transform.localPosition = new Vector3(0f, 0.02f, 0f);
            Object.DestroyImmediate(visualMesh.GetComponent<Collider>());

            Material material = EditorMaterialUtility.CreateOrLoadMaterial(
                "Mat_RangeIndicator", "Universal Render Pipeline/Unlit", new Color(0.3f, 0.9f, 0.4f, 0.35f));
            ConfigureTransparent(material);
            visualMesh.GetComponent<MeshRenderer>().sharedMaterial = material;
            visualMesh.SetActive(false);

            var rangeIndicatorSerialized = new SerializedObject(rangeIndicator);
            rangeIndicatorSerialized.FindProperty("_visualRoot").objectReferenceValue = visualMesh.transform;
            rangeIndicatorSerialized.ApplyModifiedPropertiesWithoutUndo();

            return rangeIndicator;
        }

        /// <summary>Switches a URP Lit/Unlit material to alpha-blended Transparent so its color alpha is visible.</summary>
        private static void ConfigureTransparent(Material material)
        {
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", 0f);
            material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            material.SetInt("_ZWrite", 0);
            material.DisableKeyword("_ALPHATEST_ON");
            material.EnableKeyword("_ALPHABLEND_ON");
            material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        }
    }
}
