using System.Collections.Generic;
using AlienDefense.Data;
using AlienDefense.Meta;
using AlienDefense.Progression;
using AlienDefense.Towers;
using AlienDefense.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace AlienDefense.EditorTools
{
    /// <summary>One-click data setup for the victory rewards and the damage statistics:
    /// <list type="bullet">
    /// <item>VictoryRewardCatalog: name, popup text, icon and colour of every reward kind (WinPanel/Reward sprites).</item>
    /// <item>Meta inventory items for the cards/blueprints the rewards are stored as (renames the Turret / Ice Dagger
    /// placeholder cards to Blaster / Frost, adds the Reactor and Anti-gravity blueprints).</item>
    /// <item>TowerDefinition stats icons (WinPanel/Reward/Stats).</item>
    /// <item>Level 1's Victory Rewards - only when the list is still empty, so Inspector edits are never overwritten.</item>
    /// <item>The open scene's VictoryPanel: star images on the statistics rows, star sprites, reward grid area -
    /// added in place, nothing is rebuilt or moved.</item>
    /// </list>
    /// Re-runnable.</summary>
    internal static class VictoryRewardSetup
    {
        private const string RewardSpriteDir = "Assets/_Game/Art/Sprite/Play/WinPanel/Reward";
        private const string StatsSpriteDir = RewardSpriteDir + "/Stats";
        private const string CoinIconPath = "Assets/_Game/Art/Sprite/MainMenu/Avatar/coinicon.png";
        private const string GemIconPath = "Assets/_Game/Art/Sprite/MainMenu/Avatar/diamondicon.png";
        private const string XpIconPath = "Assets/_Game/Art/Sprite/Play/HUD/icon_xp_badge.png";
        internal const string CatalogPath = "Assets/_Game/Data/UI/VictoryRewardCatalog.asset";
        private const string MetaItemDir = "Assets/_Game/Data/Meta/Items";
        private const string MetaCatalogPath = "Assets/_Game/Data/Meta/MetaItemCatalog.asset";
        private const string Level01Path = "Assets/_Game/Data/Levels/Level_01_Definition.asset";
        private const string TowerDataDir = "Assets/_Game/Data/Towers";
        private const int StarsPerRow = 3;

        private static readonly Color CoinTheme = new Color(0.27f, 0.72f, 0.33f, 1f);
        private static readonly Color GemTheme = new Color(0.55f, 0.32f, 0.85f, 1f);
        private static readonly Color XpTheme = new Color(0.18f, 0.56f, 0.87f, 1f);
        private static readonly Color CardTheme = new Color(0.10f, 0.62f, 0.78f, 1f);
        private static readonly Color BlueprintTheme = new Color(0.20f, 0.40f, 0.85f, 1f);

        private readonly struct RewardArt
        {
            public readonly VictoryRewardType Type;
            public readonly string DisplayName;
            public readonly string Description;
            public readonly string IconPath;
            public readonly Color Color;
            public readonly bool OwnFrame;

            public RewardArt(VictoryRewardType type, string displayName, string description, string iconPath, Color color, bool ownFrame)
            {
                Type = type;
                DisplayName = displayName;
                Description = description;
                IconPath = iconPath;
                Color = color;
                OwnFrame = ownFrame;
            }
        }

        private static RewardArt[] Rewards => new[]
        {
            new RewardArt(VictoryRewardType.Coins, "Coins", "Basic currency for essential items and upgrades", CoinIconPath, CoinTheme, false),
            new RewardArt(VictoryRewardType.Gems, "Gems", "Premium currency earned by clearing a level with three stars", GemIconPath, GemTheme, false),
            new RewardArt(VictoryRewardType.Experience, "Player XP", "Earn XP to level up your character", XpIconPath, XpTheme, false),
            new RewardArt(VictoryRewardType.UfoBaseCard, "UFO Base upgrade card", "Upgrade part required for the UFO Base", Card("ufobaseupdatecard"), CardTheme, true),
            new RewardArt(VictoryRewardType.BlasterCard, "Blaster Tower upgrade card", "Upgrade part required for the Blaster Tower", Card("blastertowercard"), CardTheme, true),
            new RewardArt(VictoryRewardType.MortarCard, "Mortar Tower upgrade card", "Upgrade part required for the Mortar Tower", Card("mortaltowercard"), CardTheme, true),
            new RewardArt(VictoryRewardType.FrostCard, "Frost Tower upgrade card", "Upgrade part required for the Frost Tower", Card("frosttowercard"), CardTheme, true),
            new RewardArt(VictoryRewardType.TeslaCard, "Tesla upgrade card", "Upgrade part required for the Tesla", Card("teslaupdatecard"), CardTheme, true),
            new RewardArt(VictoryRewardType.ReactorBlueprint, "Reactor blueprint", "Upgrade part required for the Reactor", Card("reactorblueprintcard"), BlueprintTheme, true),
            new RewardArt(VictoryRewardType.AntiGravityBlueprint, "Anti-gravity blueprint", "Upgrade part required for the Anti-Gravity", Card("antigravitybluesrpintcard"), BlueprintTheme, true),
        };

        private static string Card(string fileName) => RewardSpriteDir + "/" + fileName + ".png";

        [MenuItem("AlienDefense/Setup/Victory/Configure Rewards + Damage Stats")]
        private static void ConfigureAll()
        {
            FillCatalog(LoadOrCreateCatalog());
            SetupMetaItems();
            SetupTowerStatsIcons();
            SetupLevel01Rewards();
            UpgradeOpenScenePanel();
            AssetDatabase.SaveAssets();
            Debug.Log("[VictoryRewardSetup] Rewards, catalog, meta items, tower stats icons and the victory panel are configured.");
        }

        internal static VictoryRewardCatalog LoadOrCreateCatalog()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<VictoryRewardCatalog>(CatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<VictoryRewardCatalog>();
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }

            return catalog;
        }

        /// <summary>Writes one catalog entry per reward kind.</summary>
        internal static void FillCatalog(VictoryRewardCatalog catalog)
        {
            RewardArt[] rewards = Rewards;
            var so = new SerializedObject(catalog);
            SerializedProperty array = so.FindProperty("_entries");
            array.arraySize = rewards.Length;
            for (int i = 0; i < rewards.Length; i++)
            {
                SerializedProperty entry = array.GetArrayElementAtIndex(i);
                entry.FindPropertyRelative("Type").intValue = (int)rewards[i].Type;
                entry.FindPropertyRelative("DisplayName").stringValue = rewards[i].DisplayName;
                entry.FindPropertyRelative("Description").stringValue = rewards[i].Description;
                entry.FindPropertyRelative("Icon").objectReferenceValue = LoadSprite(rewards[i].IconPath);
                entry.FindPropertyRelative("HeaderColor").colorValue = rewards[i].Color;
                entry.FindPropertyRelative("IconHasOwnFrame").boolValue = rewards[i].OwnFrame;
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(catalog);
        }

        /// <summary>Every card/blueprint reward is stored in the meta inventory under LevelRewardService.GetItemId;
        /// this gives each of those ids a MetaItemDefinition with the matching name, text and card art.</summary>
        private static void SetupMetaItems()
        {
            var metaCatalog = AssetDatabase.LoadAssetAtPath<MetaItemCatalog>(MetaCatalogPath);
            var catalogItems = new List<MetaItemDefinition>();
            SerializedObject catalogSo = null;
            SerializedProperty itemsProperty = null;
            if (metaCatalog != null)
            {
                catalogSo = new SerializedObject(metaCatalog);
                itemsProperty = catalogSo.FindProperty("_items");
                for (int i = 0; i < itemsProperty.arraySize; i++)
                {
                    catalogItems.Add(itemsProperty.GetArrayElementAtIndex(i).objectReferenceValue as MetaItemDefinition);
                }
            }

            foreach (RewardArt reward in Rewards)
            {
                string itemId = LevelRewardService.GetItemId(reward.Type);
                if (string.IsNullOrEmpty(itemId))
                {
                    continue; // currencies and XP are not inventory items
                }

                string path = $"{MetaItemDir}/MetaItem_{itemId}.asset";
                var item = AssetDatabase.LoadAssetAtPath<MetaItemDefinition>(path);
                if (item == null)
                {
                    item = ScriptableObject.CreateInstance<MetaItemDefinition>();
                    AssetDatabase.CreateAsset(item, path);
                }

                var so = new SerializedObject(item);
                so.FindProperty("_id").stringValue = itemId;
                so.FindProperty("_displayName").stringValue = reward.DisplayName;
                so.FindProperty("_description").stringValue = reward.Description;
                Sprite icon = LoadSprite(reward.IconPath);
                if (icon != null)
                {
                    so.FindProperty("_icon").objectReferenceValue = icon;
                }

                so.FindProperty("_headerColor").colorValue = reward.Color;
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(item);

                if (itemsProperty != null && !catalogItems.Contains(item))
                {
                    catalogItems.Add(item);
                    itemsProperty.arraySize++;
                    itemsProperty.GetArrayElementAtIndex(itemsProperty.arraySize - 1).objectReferenceValue = item;
                }
            }

            if (catalogSo != null)
            {
                catalogSo.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(metaCatalog);
            }
        }

        private static void SetupTowerStatsIcons()
        {
            foreach (string guid in AssetDatabase.FindAssets("t:TowerDefinition", new[] { TowerDataDir }))
            {
                var tower = AssetDatabase.LoadAssetAtPath<TowerDefinition>(AssetDatabase.GUIDToAssetPath(guid));
                if (tower == null)
                {
                    continue;
                }

                string key = (tower.name + " " + tower.Id).ToLowerInvariant();
                string file = key.Contains("blaster") ? "blastertowericon"
                    : key.Contains("mortar") ? "mortaltowericon"
                    : key.Contains("frost") ? "frosttowericon"
                    : null;
                if (file == null)
                {
                    continue;
                }

                var so = new SerializedObject(tower);
                so.FindProperty("_statsIcon").objectReferenceValue = LoadSprite($"{StatsSpriteDir}/{file}.png");
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(tower);
            }
        }

        /// <summary>Level 1's default payout. Only written into an empty list: once a designer has touched the
        /// numbers in the Inspector, re-running the setup leaves them alone.</summary>
        private static void SetupLevel01Rewards()
        {
            var level = AssetDatabase.LoadAssetAtPath<LevelDefinition>(Level01Path);
            if (level == null)
            {
                Debug.LogWarning("[VictoryRewardSetup] " + Level01Path + " not found; Level 1 rewards not set.");
                return;
            }

            var so = new SerializedObject(level);
            SerializedProperty rewards = so.FindProperty("_victoryRewards");
            if (rewards.arraySize > 0)
            {
                Debug.Log("[VictoryRewardSetup] Level 1 already has Victory Rewards; left unchanged.");
                return;
            }

            var defaults = new[]
            {
                new LevelRewardEntry(VictoryRewardType.Coins, 5800),
                new LevelRewardEntry(VictoryRewardType.Experience, 3000),
                new LevelRewardEntry(VictoryRewardType.UfoBaseCard, 5),
                new LevelRewardEntry(VictoryRewardType.BlasterCard, 10),
                new LevelRewardEntry(VictoryRewardType.FrostCard, 20),
                new LevelRewardEntry(VictoryRewardType.MortarCard, 10),
                new LevelRewardEntry(VictoryRewardType.TeslaCard, 5),
                new LevelRewardEntry(VictoryRewardType.ReactorBlueprint, 1),
                new LevelRewardEntry(VictoryRewardType.AntiGravityBlueprint, 3),
            };

            rewards.arraySize = defaults.Length;
            for (int i = 0; i < defaults.Length; i++)
            {
                SerializedProperty element = rewards.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("Type").intValue = (int)defaults[i].Type;
                element.FindPropertyRelative("Amount").intValue = defaults[i].Amount;
                element.FindPropertyRelative("Grant").intValue = (int)defaults[i].Grant;
                element.FindPropertyRelative("Requirement").intValue = (int)defaults[i].Requirement;
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(level);
        }

        /// <summary>Adds what the new panel code can use to the existing VictoryPanel without rebuilding it.</summary>
        private static void UpgradeOpenScenePanel()
        {
            var view = Object.FindFirstObjectByType<VictoryPanelView>(FindObjectsInactive.Include);
            if (view == null)
            {
                Debug.LogWarning("[VictoryRewardSetup] No VictoryPanelView in the open scene (open Level_01 to wire the panel).");
                return;
            }

            var so = new SerializedObject(view);
            ApplyPanelReferences(so);

            SerializedProperty rewardItems = so.FindProperty("_rewardItems");
            if (rewardItems.arraySize > 0)
            {
                var firstRoot = rewardItems.GetArrayElementAtIndex(0).FindPropertyRelative("Root").objectReferenceValue as GameObject;
                if (firstRoot != null && so.FindProperty("_rewardGrid").objectReferenceValue == null)
                {
                    so.FindProperty("_rewardGrid").objectReferenceValue = firstRoot.transform.parent as RectTransform;
                }

                for (int i = 0; i < rewardItems.arraySize; i++)
                {
                    PreserveAspect(rewardItems.GetArrayElementAtIndex(i).FindPropertyRelative("Icon"));
                }
            }

            SerializedProperty leaders = so.FindProperty("_leaderRows");
            for (int i = 0; i < leaders.arraySize; i++)
            {
                PreserveAspect(leaders.GetArrayElementAtIndex(i).FindPropertyRelative("Icon"));
            }

            SerializedProperty statRows = so.FindProperty("_statRows");
            for (int i = 0; i < statRows.arraySize; i++)
            {
                SerializedProperty row = statRows.GetArrayElementAtIndex(i);
                PreserveAspect(row.FindPropertyRelative("Icon"));
                var rowRoot = row.FindPropertyRelative("Root").objectReferenceValue as GameObject;
                if (rowRoot != null && row.FindPropertyRelative("Stars").arraySize == 0)
                {
                    AddStatStars(rowRoot.transform, row);
                }
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(view);
            EditorSceneManager.MarkSceneDirty(view.gameObject.scene);
            EditorSceneManager.SaveScene(view.gameObject.scene);
        }

        /// <summary>Catalog and star sprites; shared with VictoryPanelBuilder.</summary>
        internal static void ApplyPanelReferences(SerializedObject viewSo)
        {
            viewSo.FindProperty("_rewardCatalog").objectReferenceValue = LoadOrCreateCatalog();
            viewSo.FindProperty("_starSprite").objectReferenceValue = LoadSprite(StatsSpriteDir + "/star.png");
            viewSo.FindProperty("_noStarSprite").objectReferenceValue = LoadSprite(StatsSpriteDir + "/nostar.png");
        }

        /// <summary>Three small stars on the row's top edge, left of the damage value (rows are 720 x 84).</summary>
        internal static void AddStatStars(Transform row, SerializedProperty rowProperty)
        {
            const float size = 30f;
            const float gap = 4f;
            var holder = new GameObject("Stars", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            holder.transform.SetParent(row, false);
            var holderRect = (RectTransform)holder.transform;
            holderRect.anchorMin = holderRect.anchorMax = new Vector2(1f, 1f);
            holderRect.pivot = new Vector2(1f, 1f);
            holderRect.anchoredPosition = new Vector2(-196f, -8f);
            holderRect.sizeDelta = new Vector2(StarsPerRow * size + (StarsPerRow - 1) * gap, size);
            var layout = holder.GetComponent<HorizontalLayoutGroup>();
            layout.spacing = gap;
            layout.childAlignment = TextAnchor.MiddleRight;
            layout.childControlWidth = layout.childControlHeight = false;
            layout.childForceExpandWidth = layout.childForceExpandHeight = false;

            SerializedProperty stars = rowProperty.FindPropertyRelative("Stars");
            stars.arraySize = StarsPerRow;
            for (int i = 0; i < StarsPerRow; i++)
            {
                var star = new GameObject("Star_0" + (i + 1), typeof(RectTransform), typeof(Image));
                star.transform.SetParent(holder.transform, false);
                ((RectTransform)star.transform).sizeDelta = new Vector2(size, size);
                var image = star.GetComponent<Image>();
                image.sprite = LoadSprite(StatsSpriteDir + "/nostar.png");
                image.preserveAspect = true;
                image.raycastTarget = false;
                stars.GetArrayElementAtIndex(i).objectReferenceValue = image;
            }
        }

        private static void PreserveAspect(SerializedProperty imageProperty)
        {
            if (imageProperty != null && imageProperty.objectReferenceValue is Image image && !image.preserveAspect)
            {
                image.preserveAspect = true;
                EditorUtility.SetDirty(image);
            }
        }

        private static Sprite LoadSprite(string path)
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null)
            {
                // Sprites imported as Multiple keep their first sub-sprite as a separate object.
                foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(path))
                {
                    if (asset is Sprite subSprite)
                    {
                        return subSprite;
                    }
                }

                Debug.LogWarning("[VictoryRewardSetup] Missing sprite " + path);
            }

            return sprite;
        }
    }
}
