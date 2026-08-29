using AlienDefense.Combat;
using AlienDefense.Towers;
using UnityEditor;
using UnityEngine;

namespace AlienDefense.EditorTools
{
    /// <summary>Builds (or reuses/migrates) the Frost (Slow) and Mortar (Splash) tower definitions and prototype
    /// prefabs — together with Blaster (see TowerPrefabBuilder) these are the 3 towers the game ships.</summary>
    internal static class AdvancedTowerPrefabBuilder
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
            public TowerAttackBehavior AttackBehavior;
            public float SplashRadius;
            public string TurretModelPath;
            public LevelSpec[] Levels;
        }

        private static readonly TowerSpec FrostSpec = new TowerSpec
        {
            Id = "tower_frost", DisplayName = "Frost Tower", PrefabName = "Tower_Frost",
            BuildCost = 120, SellPercentage = 0.5f, DefaultTargetingMode = TargetingMode.Closest,
            AttackBehavior = TowerAttackBehavior.Status, SplashRadius = 0f,
            TurretModelPath = TowerModelAttacher.Turret2Path,
            Levels = new[]
            {
                new LevelSpec(0, 6f, 4.2f, 1.1f, 720f),
                new LevelSpec(130, 9f, 4.5f, 1.2f, 720f),
                new LevelSpec(210, 13f, 4.8f, 1.35f, 720f)
            }
        };

        private static readonly TowerSpec MortarSpec = new TowerSpec
        {
            Id = "tower_mortar", DisplayName = "Mortar Tower", PrefabName = "Tower_Mortar",
            BuildCost = 180, SellPercentage = 0.5f, DefaultTargetingMode = TargetingMode.Strongest,
            AttackBehavior = TowerAttackBehavior.Splash, SplashRadius = 2.5f,
            TurretModelPath = TowerModelAttacher.Turret3Path,
            Levels = new[]
            {
                new LevelSpec(0, 35f, 4.6f, 0.35f, 300f),
                new LevelSpec(260, 55f, 4.9f, 0.4f, 300f),
                new LevelSpec(420, 80f, 5.2f, 0.45f, 300f)
            }
        };

        /// <summary>Reloads the Frost/Mortar tower definitions fresh from disk (see TowerPrefabBuilder.LoadAll doc).</summary>
        public static TowerDefinition[] LoadAll()
        {
            return new[]
            {
                AssetDatabase.LoadAssetAtPath<TowerDefinition>($"{DataFolder}/TowerDefinition_{FrostSpec.PrefabName}.asset"),
                AssetDatabase.LoadAssetAtPath<TowerDefinition>($"{DataFolder}/TowerDefinition_{MortarSpec.PrefabName}.asset")
            };
        }

        [MenuItem("AlienDefense/Setup/19. Create Advanced Tower Definitions And Prefabs")]
        public static TowerDefinition[] CreateAll()
        {
            EditorFolderUtility.EnsureFolder(DataFolder);
            EditorFolderUtility.EnsureFolder(PrefabFolder);

            var projectileDefinition = AssetDatabase.LoadAssetAtPath<ProjectileDefinition>(ProjectileDefinitionPath);
            if (projectileDefinition == null)
            {
                projectileDefinition = ProjectilePrefabBuilder.CreateOrLoad();
            }

            StatusEffectDefinitionBuilder.CreateAll();
            StatusEffectDefinition slow = StatusEffectDefinitionBuilder.Slow;

            TowerDefinition frost = CreateOrLoadTower(FrostSpec, projectileDefinition, slow);
            TowerDefinition mortar = CreateOrLoadTower(MortarSpec, projectileDefinition, null);

            Debug.Log("[AlienDefense Setup] Advanced tower definitions and prefabs ready.");
            return new[] { frost, mortar };
        }

        private static TowerDefinition CreateOrLoadTower(TowerSpec spec, ProjectileDefinition projectileDefinition, StatusEffectDefinition statusEffectOnHit)
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
            serializedDefinition.FindProperty("_attackBehavior").enumValueIndex = (int)spec.AttackBehavior;
            serializedDefinition.FindProperty("_statusEffectOnHit").objectReferenceValue = statusEffectOnHit;
            serializedDefinition.FindProperty("_splashRadius").floatValue = spec.SplashRadius;
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
                MigrateAttachTurretModel(existing, spec);
                return existing;
            }

            GameObject root = BuildHierarchy(spec);
            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            Object.DestroyImmediate(root);

            return AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        }

        /// <summary>Idempotent: replaces an existing prefab's placeholder-primitive (or older) visual with the
        /// TD_Sci-Fi turret model. See TowerPrefabBuilder.MigrateAttachTurretModel for the Blaster equivalent.</summary>
        private static void MigrateAttachTurretModel(GameObject prefabAsset, TowerSpec spec)
        {
            GameObject turretModel = AssetDatabase.LoadAssetAtPath<GameObject>(spec.TurretModelPath);
            if (turretModel == null)
            {
                Debug.LogWarning($"[AlienDefense Setup] Turret model not found at '{spec.TurretModelPath}'; leaving {spec.PrefabName} unchanged.");
                return;
            }

            if (TowerModelAttacher.HasModelAttached(prefabAsset.transform, turretModel))
            {
                return;
            }

            string path = AssetDatabase.GetAssetPath(prefabAsset);
            GameObject contents = PrefabUtility.LoadPrefabContents(path);

            TowerModelAttacher.DestroyAllChildrenExcept(contents.transform, "RangeIndicator");
            (Transform turretPivot, Transform firePoint) = TowerModelAttacher.Attach(contents.transform, turretModel);

            var attack = contents.GetComponent<TowerAttackController>();
            if (attack != null)
            {
                var attackSerialized = new SerializedObject(attack);
                attackSerialized.FindProperty("_firePoint").objectReferenceValue = firePoint;
                attackSerialized.ApplyModifiedPropertiesWithoutUndo();
            }

            var visual = contents.GetComponent<TowerVisual>();
            if (visual != null)
            {
                var visualSerialized = new SerializedObject(visual);
                visualSerialized.FindProperty("_turretPivot").objectReferenceValue = turretPivot;
                visualSerialized.FindProperty("_forwardAxis").vector3Value = Vector3.forward;
                visualSerialized.ApplyModifiedPropertiesWithoutUndo();
            }

            PrefabUtility.SaveAsPrefabAsset(contents, path);
            PrefabUtility.UnloadPrefabContents(contents);
            Debug.Log($"[AlienDefense Setup] Migrated {path}: attached {turretModel.name} model.");
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

            GameObject turretModel = AssetDatabase.LoadAssetAtPath<GameObject>(spec.TurretModelPath);
            (Transform turretPivot, Transform firePoint) = TowerModelAttacher.Attach(root.transform, turretModel);

            RangeIndicator rangeIndicator = BuildRangeIndicator(root.transform);

            var attackSerialized = new SerializedObject(attack);
            attackSerialized.FindProperty("_firePoint").objectReferenceValue = firePoint;
            attackSerialized.ApplyModifiedPropertiesWithoutUndo();

            var visualSerialized = new SerializedObject(visual);
            visualSerialized.FindProperty("_turretPivot").objectReferenceValue = turretPivot;
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
