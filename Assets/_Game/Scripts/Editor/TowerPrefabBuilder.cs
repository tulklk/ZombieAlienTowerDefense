using AlienDefense.Combat;
using AlienDefense.Towers;
using UnityEditor;
using UnityEngine;

namespace AlienDefense.EditorTools
{
    /// <summary>Builds (or reuses/migrates) the Blaster tower definition and prototype prefab. Also owns the
    /// one-time cleanup of the retired Rapid/Heavy tower assets (replaced by Frost/Mortar; see
    /// AdvancedTowerPrefabBuilder) so the game ships exactly 3 tower types.</summary>
    internal static class TowerPrefabBuilder
    {
        private const string DataFolder = "Assets/_Game/Data/Towers";
        private const string PrefabFolder = "Assets/_Game/Prefabs/Towers";
        private const string TowerLayerName = "Tower";

        private const string ProjectileDefinitionPath = "Assets/_Game/Data/Projectiles/Projectile_Blaster.asset";

        // Retired 2026-08: replaced by the 3-tower catalog (Blaster/Frost/Mortar) requested to match the
        // reference "Súng trụ / Dao Băng / Cối" tower-select UI. Kept here only so RemoveLegacyTowerAssets can
        // find and delete their leftover assets on projects that built them before this change.
        private static readonly string[] LegacyPrefabNames = { "Tower_Rapid", "Tower_Heavy" };

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
            public string TurretModelPath;
            public LevelSpec[] Levels;
        }

        private static readonly TowerSpec BlasterSpec = new TowerSpec
        {
            Id = "tower_blaster", DisplayName = "Blaster Tower", PrefabName = "Tower_Blaster",
            BuildCost = 75, SellPercentage = 0.5f, DefaultTargetingMode = TargetingMode.First,
            TurretModelPath = TowerModelAttacher.Turret1Path,
            Levels = new[]
            {
                new LevelSpec(0, 20f, 4f, 1f, 720f),
                new LevelSpec(90, 32f, 4.4f, 1.2f, 720f),
                new LevelSpec(160, 50f, 4.8f, 1.4f, 720f)
            }
        };

        /// <summary>Reloads the Blaster tower definition fresh from disk. Use right before a use site that follows
        /// several AssetDatabase/PrefabUtility operations, which can otherwise leave an earlier in-memory reference stale.</summary>
        public static TowerDefinition[] LoadAll()
        {
            return new[]
            {
                AssetDatabase.LoadAssetAtPath<TowerDefinition>($"{DataFolder}/TowerDefinition_{BlasterSpec.PrefabName}.asset")
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
            RemoveLegacyTowerAssets();

            Debug.Log("[AlienDefense Setup] Tower definitions and prefabs ready.");
            return new[] { blaster };
        }

        /// <summary>Deletes the prefab/definition/material assets of tower types that no longer ship (Rapid, Heavy).
        /// Safe to call repeatedly: AssetDatabase.DeleteAsset is a no-op if the asset is already gone.</summary>
        private static void RemoveLegacyTowerAssets()
        {
            bool removedAny = false;
            foreach (string prefabName in LegacyPrefabNames)
            {
                removedAny |= AssetDatabase.DeleteAsset($"{PrefabFolder}/{prefabName}.prefab");
                removedAny |= AssetDatabase.DeleteAsset($"{DataFolder}/TowerDefinition_{prefabName}.asset");
                removedAny |= AssetDatabase.DeleteAsset($"Assets/_Game/Materials/Generated/Mat_{prefabName}.mat");
            }

            if (removedAny)
            {
                AssetDatabase.SaveAssets();
                Debug.Log("[AlienDefense Setup] Removed retired tower assets (Rapid/Heavy).");
            }
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
                MigrateAttachTurretModel(existing, spec);
                return existing;
            }

            GameObject root = BuildHierarchy(spec);
            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            Object.DestroyImmediate(root);

            return AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        }

        /// <summary>Idempotent: replaces an existing prefab's placeholder-primitive (or older) visual with the
        /// TD_Sci-Fi turret model, re-wiring TowerAttackController/_firePoint and TowerVisual/_turretPivot. Skips
        /// entirely if the correct model is already attached.</summary>
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
