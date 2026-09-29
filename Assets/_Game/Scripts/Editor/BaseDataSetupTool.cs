using System.Collections.Generic;
using System.IO;
using AlienDefense.Base;
using AlienDefense.Meta;
using UnityEditor;
using UnityEngine;

namespace AlienDefense.EditorTools
{
    /// <summary>Authors the base's data assets and the building prefab wrappers.
    ///
    /// Idempotent by design (Phase 32): every asset is looked up first and only created when missing, and an
    /// existing asset's hand-tuned numbers are never overwritten. Running this twice is a no-op, so it is safe to
    /// re-run after adding a building.
    ///
    /// Third-party prefabs are never edited. Each building gets a wrapper prefab under _Game that INSTANCES the
    /// pack prefab as a child, so re-importing a pack cannot break our data and our data cannot dirty the pack.</summary>
    public static class BaseDataSetupTool
    {
        private const string DataFolder = "Assets/_Game/Data/Base";
        private const string PrefabFolder = "Assets/_Game/Prefabs/Base/Buildings";
        private const string MetaItemFolder = "Assets/_Game/Data/Meta/Items";
        private const string MetaCatalogPath = "Assets/_Game/Data/Meta/MetaItemCatalog.asset";
        private const string CatalogPath = DataFolder + "/BaseBuildingCatalog.asset";

        private const string WoodItemId = "wood";
        private const int MaxLevels = 3;

        /// <summary>The campaign level the Patrol Post gates against, matching the reference's "Complete campaign
        /// level 2". Resolved against the real level catalog at author time rather than guessed.</summary>
        private const string CampaignLevelId = "level_02";

        /// <summary>One building, described here only so the tool can author it. The real, editable data is the
        /// .asset this produces - this table is a seed, not a source of truth.</summary>
        private readonly struct Seed
        {
            public readonly string Id;
            public readonly string Name;
            public readonly string Description;
            public readonly BaseBuildingType Type;
            public readonly string PlotId;
            public readonly string SourcePrefab;

            public Seed(string id, string name, string description, BaseBuildingType type, string plotId,
                string sourcePrefab)
            {
                Id = id;
                Name = name;
                Description = description;
                Type = type;
                PlotId = plotId;
                SourcePrefab = sourcePrefab;
            }
        }

        private const string PandazolePrefabs = "Assets/Pandazole_Ultimate_Pack/Pandazole Farm Ranch Pack/Prefabs/";

        private static readonly Seed[] Seeds =
        {
            new Seed("central_building", "Central Building",
                "The heart of the base. Raising its level unlocks new structures and higher upgrade caps.",
                BaseBuildingType.Central, "plot_central", PandazolePrefabs + "Bld_FarmerHouse.prefab"),

            new Seed("patrol_post", "Patrol Post",
                "Organizes patrols for automated resource gathering.",
                BaseBuildingType.Support, "plot_patrol", PandazolePrefabs + "Bld_StoreBuilding_01.prefab"),

            new Seed("weapon_workshop", "Weapon Workshop",
                "Unlocking and tuning new combat abilities.",
                BaseBuildingType.Workshop, "plot_workshop", PandazolePrefabs + "Bld_Barn_01.prefab"),

            new Seed("research_center", "Genetics Research Center",
                "Studies alien genetics to unlock new technologies.",
                BaseBuildingType.Research, "plot_research", PandazolePrefabs + "Bld_GreenMouse.prefab"),

            new Seed("sawmill", "Sawmill",
                "Produces wood for building and repairing base structures.",
                BaseBuildingType.Production, "plot_sawmill", PandazolePrefabs + "Bld_FarmMill_01.prefab")
        };

        [MenuItem("Tools/Tower Defense/Base/Setup Base Data")]
        public static void Setup()
        {
            EnsureFolder(DataFolder);
            EnsureFolder(PrefabFolder);

            var missing = new List<string>();

            MetaItemDefinition wood = EnsureWoodItem(missing);
            var definitions = new List<BaseBuildingDefinition>();

            for (int i = 0; i < Seeds.Length; i++)
            {
                definitions.Add(EnsureBuilding(Seeds[i], wood, missing));
            }

            EnsureCatalog(definitions);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            if (missing.Count > 0)
            {
                var sb = new System.Text.StringBuilder("[BaseDataSetupTool] MISSING ASSETS - placeholders left empty:\n");
                for (int i = 0; i < missing.Count; i++)
                {
                    sb.Append("  - ").AppendLine(missing[i]);
                }

                Debug.LogWarning(sb.ToString());
            }

            Debug.Log($"[BaseDataSetupTool] Done. {definitions.Count} buildings in {CatalogPath}.");
        }

        // ------------------------------------------------------------------ Wood

        private static MetaItemDefinition EnsureWoodItem(List<string> missing)
        {
            string path = MetaItemFolder + "/MetaItem_" + WoodItemId + ".asset";
            var item = AssetDatabase.LoadAssetAtPath<MetaItemDefinition>(path);

            if (item == null)
            {
                EnsureFolder(MetaItemFolder);
                item = ScriptableObject.CreateInstance<MetaItemDefinition>();

                var so = new SerializedObject(item);
                so.FindProperty("_id").stringValue = WoodItemId;
                so.FindProperty("_displayName").stringValue = "Wood";
                so.FindProperty("_description").stringValue =
                    "Harvested at the Sawmill. Spent on base construction and repairs.";
                so.FindProperty("_rarity").enumValueIndex = (int)MetaItemRarity.Common;
                so.FindProperty("_kind").enumValueIndex = (int)MetaItemKind.Material;
                so.ApplyModifiedPropertiesWithoutUndo();

                AssetDatabase.CreateAsset(item, path);
                Debug.Log("[BaseDataSetupTool] Created " + path);
            }

            // Reported every run until an icon is assigned, because a null icon silently draws nothing.
            var check = new SerializedObject(item);
            if (check.FindProperty("_icon").objectReferenceValue == null)
            {
                missing.Add("Wood icon sprite -> assign to " + path + " (field: Icon). " +
                    "No wood/log sprite exists anywhere under Assets/_Game/Art.");
            }

            AddToMetaCatalog(item);
            return item;
        }

        /// <summary>Appends the item to MetaItemCatalog if it is not already listed, leaving the existing order
        /// alone.</summary>
        private static void AddToMetaCatalog(MetaItemDefinition item)
        {
            var catalog = AssetDatabase.LoadAssetAtPath<MetaItemCatalog>(MetaCatalogPath);
            if (catalog == null || item == null)
            {
                return;
            }

            var so = new SerializedObject(catalog);
            SerializedProperty items = so.FindProperty("_items");

            for (int i = 0; i < items.arraySize; i++)
            {
                if (items.GetArrayElementAtIndex(i).objectReferenceValue == item)
                {
                    return;
                }
            }

            items.arraySize++;
            items.GetArrayElementAtIndex(items.arraySize - 1).objectReferenceValue = item;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(catalog);
            Debug.Log("[BaseDataSetupTool] Added " + item.name + " to MetaItemCatalog.");
        }

        // ------------------------------------------------------------------ Buildings

        private static BaseBuildingDefinition EnsureBuilding(Seed seed, MetaItemDefinition wood, List<string> missing)
        {
            string path = DataFolder + "/BaseBuilding_" + seed.Id + ".asset";
            var definition = AssetDatabase.LoadAssetAtPath<BaseBuildingDefinition>(path);

            GameObject wrapper = EnsureWrapperPrefab(seed, missing);

            if (definition != null)
            {
                // Already authored - the designer's numbers win. Only heal a level prefab that is still empty.
                FillMissingVisualPrefabs(definition, wrapper);
                return definition;
            }

            definition = ScriptableObject.CreateInstance<BaseBuildingDefinition>();
            var so = new SerializedObject(definition);
            so.FindProperty("_id").stringValue = seed.Id;
            so.FindProperty("_displayName").stringValue = seed.Name;
            so.FindProperty("_description").stringValue = seed.Description;
            so.FindProperty("_type").enumValueIndex = (int)seed.Type;
            so.FindProperty("_plotId").stringValue = seed.PlotId;

            SerializedProperty levels = so.FindProperty("_levels");
            levels.arraySize = MaxLevels;
            for (int i = 0; i < MaxLevels; i++)
            {
                WriteLevel(levels.GetArrayElementAtIndex(i), seed, i + 1, wood, wrapper);
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.CreateAsset(definition, path);
            Debug.Log("[BaseDataSetupTool] Created " + path);
            return definition;
        }

        /// <summary>Seed numbers only. They exist so the base is playable the moment the tool runs; every one of
        /// them is meant to be re-balanced in the Inspector afterwards.</summary>
        private static void WriteLevel(SerializedProperty level, Seed seed, int levelNumber,
            MetaItemDefinition wood, GameObject wrapper)
        {
            level.FindPropertyRelative("_level").intValue = levelNumber;
            // Short first builds so the loop is felt immediately; later ones stretch out.
            level.FindPropertyRelative("_constructionSeconds").floatValue = levelNumber == 1 ? 5f : levelNumber == 2 ? 15f : 30f;
            level.FindPropertyRelative("_forceReward").intValue = 100 * levelNumber;
            level.FindPropertyRelative("_visualPrefab").objectReferenceValue = wrapper;

            SerializedProperty requirements = level.FindPropertyRelative("_requirements");
            var rows = new List<(BuildingRequirementType type, string target, int value)>();

            // The unlock chain has to be completable from an empty save, and wood only comes from the Sawmill.
            // So: Central Lv1 is free, the Sawmill only needs Central Lv1 and no wood, and everything after that is
            // paid for with the wood the Sawmill makes. Requiring wood before the Sawmill exists would deadlock a
            // new player forever - which is exactly what the first version of this seed did.
            bool isCentral = seed.Type == BaseBuildingType.Central;
            bool isSawmill = seed.Type == BaseBuildingType.Production;

            // Capped at the Central's own max level: "Central Lv 4" for a level-3 building would never unlock.
            int requiredCentral = Mathf.Min(isSawmill ? levelNumber : levelNumber + 1, MaxLevels);
            if (!isCentral)
            {
                rows.Add((BuildingRequirementType.BuildingLevel, "central_building", requiredCentral));
            }

            int woodCost;
            if (isCentral)
            {
                woodCost = levelNumber == 1 ? 0 : 50 * (levelNumber - 1);
            }
            else if (isSawmill)
            {
                woodCost = levelNumber == 1 ? 0 : 60 * (levelNumber - 1);
            }
            else
            {
                // Level 1 of the support buildings matches the reference's "3.9K / 10".
                woodCost = levelNumber == 1 ? 10 : 50 * levelNumber;
            }

            if (wood != null && woodCost > 0)
            {
                rows.Add((BuildingRequirementType.Material, WoodItemId, woodCost));
            }

            // The reference's "Complete campaign level 2" on the Patrol Post.
            if (seed.Id == "patrol_post" && levelNumber == 1)
            {
                rows.Add((BuildingRequirementType.CampaignLevel, CampaignLevelId, 2));
            }

            requirements.arraySize = rows.Count;
            for (int i = 0; i < rows.Count; i++)
            {
                SerializedProperty row = requirements.GetArrayElementAtIndex(i);
                row.FindPropertyRelative("_type").enumValueIndex = (int)rows[i].type;
                row.FindPropertyRelative("_targetId").stringValue = rows[i].target;
                row.FindPropertyRelative("_requiredValue").intValue = rows[i].value;
            }

            SerializedProperty bonuses = level.FindPropertyRelative("_bonuses");
            bonuses.arraySize = 1;
            SerializedProperty bonus = bonuses.GetArrayElementAtIndex(0);
            bonus.FindPropertyRelative("_displayName").stringValue = "Building power";
            bonus.FindPropertyRelative("_statId").stringValue = "building_power";
            bonus.FindPropertyRelative("_value").floatValue = 100 * levelNumber;
            bonus.FindPropertyRelative("_isPercent").boolValue = false;

            SerializedProperty production = level.FindPropertyRelative("_production");
            bool producing = seed.Type == BaseBuildingType.Production;
            production.FindPropertyRelative("_materialId").stringValue = producing ? WoodItemId : string.Empty;
            production.FindPropertyRelative("_amountPerInterval").intValue = producing ? 10 * levelNumber : 0;
            production.FindPropertyRelative("_intervalSeconds").floatValue = 60f;
            production.FindPropertyRelative("_storageCapacity").intValue = producing ? 100 * levelNumber : 0;
        }

        private static void FillMissingVisualPrefabs(BaseBuildingDefinition definition, GameObject wrapper)
        {
            if (wrapper == null)
            {
                return;
            }

            var so = new SerializedObject(definition);
            SerializedProperty levels = so.FindProperty("_levels");
            bool changed = false;

            for (int i = 0; i < levels.arraySize; i++)
            {
                SerializedProperty prefab = levels.GetArrayElementAtIndex(i).FindPropertyRelative("_visualPrefab");
                if (prefab.objectReferenceValue == null)
                {
                    prefab.objectReferenceValue = wrapper;
                    changed = true;
                }
            }

            if (changed)
            {
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(definition);
            }
        }

        // ------------------------------------------------------------------ Prefab wrappers

        /// <summary>Creates BP_&lt;building&gt;.prefab holding an instance of the pack prefab, plus a BoxCollider
        /// sized to the model so taps hit the building (Phase 27: box, never a MeshCollider).</summary>
        private static GameObject EnsureWrapperPrefab(Seed seed, List<string> missing)
        {
            string path = PrefabFolder + "/BP_" + seed.Id + ".prefab";
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null)
            {
                return existing;
            }

            var source = AssetDatabase.LoadAssetAtPath<GameObject>(seed.SourcePrefab);
            if (source == null)
            {
                missing.Add($"Model for '{seed.Name}' -> expected {seed.SourcePrefab}. " +
                    "Wrapper prefab was NOT created; the definition's level prefabs are empty.");
                return null;
            }

            var root = new GameObject("BP_" + seed.Id);
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(source, root.transform);
            instance.transform.localPosition = Vector3.zero;

            // Pack pivots are not all centred: shift the model so its footprint is centred on the wrapper's origin
            // and it sits on y = 0. The plot then only has to place the wrapper at its own centre.
            if (TryGetRendererBounds(instance, out Bounds modelBounds))
            {
                instance.transform.localPosition = new Vector3(-modelBounds.center.x, -modelBounds.min.y,
                    -modelBounds.center.z);
            }

            AddFittedBoxCollider(root, instance);

            GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);

            Debug.Log("[BaseDataSetupTool] Created " + path + " wrapping " + seed.SourcePrefab);
            return saved;
        }

        private static bool TryGetRendererBounds(GameObject root, out Bounds bounds)
        {
            bounds = default;
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

        private static void AddFittedBoxCollider(GameObject root, GameObject modelInstance)
        {
            if (!TryGetRendererBounds(modelInstance, out Bounds bounds))
            {
                return;
            }

            var collider = root.AddComponent<BoxCollider>();
            collider.center = bounds.center - root.transform.position;
            collider.size = bounds.size;
        }

        // ------------------------------------------------------------------ Catalog

        private static void EnsureCatalog(List<BaseBuildingDefinition> definitions)
        {
            var catalog = AssetDatabase.LoadAssetAtPath<BaseBuildingCatalog>(CatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<BaseBuildingCatalog>();
                AssetDatabase.CreateAsset(catalog, CatalogPath);
                Debug.Log("[BaseDataSetupTool] Created " + CatalogPath);
            }

            var so = new SerializedObject(catalog);
            SerializedProperty buildings = so.FindProperty("_buildings");
            buildings.arraySize = definitions.Count;
            for (int i = 0; i < definitions.Count; i++)
            {
                buildings.GetArrayElementAtIndex(i).objectReferenceValue = definitions[i];
            }

            so.FindProperty("_centralBuildingId").stringValue = "central_building";

            // Seed the guided progression only when it is empty, so a hand-edited goal list survives a re-run.
            SerializedProperty goals = so.FindProperty("_goals");
            if (goals.arraySize == 0)
            {
                (string id, int level)[] path =
                {
                    ("central_building", 1), ("sawmill", 1), ("central_building", 2), ("patrol_post", 1),
                    ("weapon_workshop", 1), ("research_center", 1), ("central_building", 3)
                };

                goals.arraySize = path.Length;
                for (int i = 0; i < path.Length; i++)
                {
                    SerializedProperty goal = goals.GetArrayElementAtIndex(i);
                    goal.FindPropertyRelative("_buildingId").stringValue = path[i].id;
                    goal.FindPropertyRelative("_level").intValue = path[i].level;
                }
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(catalog);
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            string leaf = Path.GetFileName(path);
            if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
            {
                EnsureFolder(parent);
            }

            AssetDatabase.CreateFolder(parent, leaf);
        }
    }
}
