using System.Collections.Generic;
using AlienDefense.Meta;
using AlienDefense.UI;
using AlienDefense.UI.Inventory;
using AlienDefense.UI.MainMenu;
using AlienDefense.UI.Profile;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace AlienDefense.EditorTools
{
    /// <summary>Builds the Inventory tab (2026-10-28) inside MainMenu's UpgradePanel, replacing the old greybox
    /// screen: blurred hangar + UFO preview (the Profile garage camera, shared), six part sockets, total bonus, the part
    /// filter bar with Craft / Merge, Equipment / Artifacts / Materials pages, sub-tabs, popups (detail, craft, artifact,
    /// container, rewards), the "Artifacts merge" screen and the artifact progression overview. Also authors the data
    /// the catalog was missing (12 equipment pieces with level costs and recipes, 6 artifact families, a container) and
    /// adds them to the existing MetaItemCatalog. Re-runnable: rebuilds its own objects; data only created when missing
    /// unless "overwrite" is used.</summary>
    public static class InventoryTabBuilder
    {
        private const string MainMenuPath = "Assets/_Game/Scenes/Menu/MainMenu.unity";
        private const string CatalogPath = "Assets/_Game/Data/Meta/MetaItemCatalog.asset";
        private const string EquipmentFolder = "Assets/_Game/Data/Meta/Equipment";
        private const string ArtifactFolder = "Assets/_Game/Data/Meta/Artifacts";
        private const string FontPath = "Assets/_Game/Font/Fredoka-Bold SDF.asset";
        private const string OutlinedPath = "Assets/_Game/Font/Materials/Fredoka_Title.mat";
        private const string Art = "Assets/_Game/Art/Sprite/";
        private const string RootName = "InventoryRoot";
        private const string BackdropName = "InventoryBackdrop";

        private const float UfoAreaHeight = 800f;
        private const float BarHeight = 124f;
        private const float SubTabHeight = 132f;

        private static TMP_FontAsset _font;
        private static Material _outlined;

        [MenuItem("AlienDefense/Setup/30. Build Inventory Tab")]
        public static void BuildFromMenu() => Debug.Log(Build(false));

        [MenuItem("AlienDefense/Setup/30b. Build Inventory Tab (overwrite inventory data)")]
        public static void BuildOverwriteFromMenu() => Debug.Log(Build(true));

        public static string Build(bool overwriteData)
        {
            if (EditorApplication.isPlaying)
            {
                return "[InventoryTabBuilder] Stop Play mode first.";
            }

            _font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            _outlined = AssetDatabase.LoadAssetAtPath<Material>(OutlinedPath);
            ShopArtBuilder.BuildAll();
            InventoryArtBuilder.BuildAll();
            string data = BuildData(overwriteData);

            Scene scene = EditorSceneManager.OpenScene(MainMenuPath, OpenSceneMode.Single);
            Transform upgradePanel = FindNamed(scene, "UpgradePanel");
            Transform safeArea = FindNamed(scene, "SafeArea");
            var shell = Object.FindFirstObjectByType<MenuShellPresenter>(FindObjectsInactive.Include);
            var preview = Object.FindFirstObjectByType<ProfileUfoPreviewController>(FindObjectsInactive.Include);
            if (upgradePanel == null || safeArea == null || shell == null)
            {
                return "[InventoryTabBuilder] UpgradePanel / SafeArea / MenuShellPresenter not found.";
            }

            DestroyChild(upgradePanel, RootName);
            DestroyChild(safeArea, BackdropName);

            RectTransform backdrop = BuildBackdrop(safeArea);
            RectTransform root = Box(RootName, upgradePanel);
            Stretch(root);
            var view = root.gameObject.AddComponent<InventoryScreenView>();
            var presenter = root.gameObject.AddComponent<InventoryScreenPresenter>();
            // Everything is authored for a 1080-wide column; UiDesignWidthScaler shrinks it uniformly on tall phones.
            RectTransform column = Box("Column", root);
            column.anchorMin = column.anchorMax = column.pivot = new Vector2(0.5f, 1f);
            column.sizeDelta = new Vector2(1080f, 1866f);
            var scaler = root.gameObject.AddComponent<UiDesignWidthScaler>();
            Wire(scaler, ("_column", column));
            BuildScreen(column, view);

            StyleNavBadges(scene);
            Wire(presenter, ("_view", view), ("_ufoPreview", preview), ("_backdrop", backdrop.gameObject));
            Wire(shell, ("_inventoryPresenter", presenter));
            backdrop.gameObject.SetActive(false);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            return "[InventoryTabBuilder] Inventory built (preview " + (preview != null ? "shared" : "MISSING") + "). " + data;
        }

        // ================================================================== data

        private static readonly string[] Mk1Names = { "Pilot Stick", "Cockpit Chair", "Fusion Reactor", "Cargo Pod", "Grav Ring", "Ion Engine" };
        private static readonly string[] Mk2Names = { "Ace Stick", "Commander Throne", "Quantum Reactor", "Hyper Cargo", "Grav Lens", "Plasma Engine" };
        private static readonly string[] Abilities = { "Handling", "Pilot Focus", "Energy Output", "Cargo Bay", "Hover Range", "Flight Speed" };
        private static readonly string[] LevelMaterials = { "microcircuit", "microcircuit", "reactor_blueprint", "anti_gravity_blueprint", "anti_gravity_blueprint", "reactor_blueprint" };
        private static readonly string[] ArtifactNames = { "Pilot Medal", "Throne Sigil", "Core Crystal", "Cargo Seal", "Grav Shard", "Thruster Rune" };
        private static readonly string[] ArtifactEffects = { "Weapon Power", "Crit Chance", "Energy Gain", "Loot Bonus", "Shield", "Flight Speed" };
        private static readonly string[] SlotIds = { "stick", "chair", "reactor", "cargo", "antigravity", "engine" };

        private static string BuildData(bool overwrite)
        {
            EnsureFolder(EquipmentFolder);
            EnsureFolder(ArtifactFolder);
            var catalog = AssetDatabase.LoadAssetAtPath<MetaItemCatalog>(CatalogPath);
            if (catalog == null)
            {
                return "MetaItemCatalog missing - no data.";
            }

            var equipment = new List<EquipmentDefinition>();
            var artifacts = new List<ArtifactDefinition>();
            int created = 0;
            for (int s = 0; s < 6; s++)
            {
                for (int mk = 1; mk <= 2; mk++)
                {
                    string path = EquipmentFolder + "/Equipment_" + SlotIds[s] + "_mk" + mk + ".asset";
                    var def = AssetDatabase.LoadAssetAtPath<EquipmentDefinition>(path);
                    bool isNew = def == null;
                    if (isNew)
                    {
                        def = ScriptableObject.CreateInstance<EquipmentDefinition>();
                        AssetDatabase.CreateAsset(def, path);
                        created++;
                    }

                    if (isNew || overwrite) WriteEquipment(def, s, mk);
                    equipment.Add(def);
                }

                string artPath = ArtifactFolder + "/Artifact_" + SlotIds[s] + ".asset";
                var art = AssetDatabase.LoadAssetAtPath<ArtifactDefinition>(artPath);
                bool artNew = art == null;
                if (artNew)
                {
                    art = ScriptableObject.CreateInstance<ArtifactDefinition>();
                    AssetDatabase.CreateAsset(art, artPath);
                    created++;
                }

                if (artNew || overwrite) WriteArtifact(art, s);
                artifacts.Add(art);
            }

            string containerPath = "Assets/_Game/Data/Meta/Items/MetaItem_resource_container.asset";
            var container = AssetDatabase.LoadAssetAtPath<MetaItemDefinition>(containerPath);
            bool containerNew = container == null;
            if (containerNew)
            {
                container = ScriptableObject.CreateInstance<MetaItemDefinition>();
                AssetDatabase.CreateAsset(container, containerPath);
                created++;
            }

            if (containerNew || overwrite) WriteContainer(container);

            var so = new SerializedObject(catalog);
            AddUnique(so.FindProperty("_equipment"), equipment.ToArray());
            AddUnique(so.FindProperty("_artifacts"), artifacts.ToArray());
            AddUnique(so.FindProperty("_items"), new Object[] { container });
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(catalog);
            return $"Inventory data: {equipment.Count} equipment, {artifacts.Count} artifacts, 1 container ({created} created).";
        }

        private static void WriteEquipment(EquipmentDefinition def, int slot, int mk)
        {
            var so = new SerializedObject(def);
            string id = "eq_" + SlotIds[slot] + "_mk" + mk;
            so.FindProperty("_id").stringValue = id;
            so.FindProperty("_displayName").stringValue = mk == 1 ? Mk1Names[slot] : Mk2Names[slot];
            so.FindProperty("_description").stringValue = mk == 1
                ? "Standard " + EquipmentSlotNames.Get((EquipmentSlotType)slot).ToLower() + " for the UFO. Raises the UFO's power while equipped."
                : "Elite " + EquipmentSlotNames.Get((EquipmentSlotType)slot).ToLower() + " built from rare blueprints. Starts at Rare.";
            so.FindProperty("_icon").objectReferenceValue = InventoryArtBuilder.Load("T_Inv_Equip_" + InventoryArtBuilder.GlyphNames[slot] + "_" + mk);
            so.FindProperty("_slot").enumValueIndex = slot;
            so.FindProperty("_abilityDisplayName").stringValue = Abilities[slot];

            // Rarity ladder: Mk I Common -> Legendary, Mk II Rare -> Legendary.
            int[] rarities = mk == 1 ? new[] { 0, 1, 2, 3, 4 } : new[] { 2, 3, 4 };
            float[] power = mk == 1 ? new[] { 20f, 35f, 60f, 100f, 160f } : new[] { 70f, 120f, 190f };
            float[] ability = mk == 1 ? new[] { 10f, 18f, 30f, 50f, 80f } : new[] { 35f, 55f, 90f };
            float[] speed = mk == 1 ? new[] { 5f, 8f, 12f, 18f, 25f } : new[] { 12f, 18f, 28f };
            int[] maxLevel = mk == 1 ? new[] { 10, 15, 20, 25, 30 } : new[] { 20, 25, 30 };
            int[] copies = mk == 1 ? new[] { 2, 2, 3, 3, 0 } : new[] { 3, 3, 0 };
            int[] coins = mk == 1 ? new[] { 500, 1500, 4000, 10000, 0 } : new[] { 4000, 10000, 0 };
            SerializedProperty tiers = so.FindProperty("_tiers");
            tiers.arraySize = rarities.Length;
            for (int i = 0; i < rarities.Length; i++)
            {
                SerializedProperty t = tiers.GetArrayElementAtIndex(i);
                t.FindPropertyRelative("_rarity").enumValueIndex = rarities[i];
                t.FindPropertyRelative("_weaponPower").floatValue = power[i];
                t.FindPropertyRelative("_abilityPercent").floatValue = ability[i];
                t.FindPropertyRelative("_flightSpeedPercent").floatValue = speed[i];
                t.FindPropertyRelative("_maxLevel").intValue = maxLevel[i];
                t.FindPropertyRelative("_upgradeDuplicates").intValue = copies[i];
                t.FindPropertyRelative("_upgradeCurrency").intValue = coins[i];
            }

            so.FindProperty("_statGrowthPerLevel").floatValue = 0.1f;
            so.FindProperty("_levelCoinCost").intValue = mk == 1 ? 300 : 800;
            so.FindProperty("_levelCostGrowth").floatValue = 1.35f;
            so.FindProperty("_levelMaterialId").stringValue = LevelMaterials[slot];
            so.FindProperty("_levelMaterialAmount").intValue = mk == 1 ? 2 : 4;
            SerializedProperty recipe = so.FindProperty("_recipe");
            var costs = mk == 1
                ? new[] { new MetaItemAmount("microcircuit", 10), new MetaItemAmount("volt_drip", 50) }
                : new[] { new MetaItemAmount("microcircuit", 30), new MetaItemAmount(LevelMaterials[slot] == "microcircuit" ? "rare_atom" : LevelMaterials[slot], 5), new MetaItemAmount("volt_drip", 150) };
            recipe.arraySize = costs.Length;
            for (int i = 0; i < costs.Length; i++)
            {
                recipe.GetArrayElementAtIndex(i).FindPropertyRelative("ItemId").stringValue = costs[i].ItemId;
                recipe.GetArrayElementAtIndex(i).FindPropertyRelative("Amount").intValue = costs[i].Amount;
            }

            so.FindProperty("_recipeCoins").intValue = mk == 1 ? 500 : 2000;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(def);
        }

        private static void WriteArtifact(ArtifactDefinition art, int slot)
        {
            var so = new SerializedObject(art);
            so.FindProperty("_id").stringValue = "art_" + SlotIds[slot];
            so.FindProperty("_displayName").stringValue = ArtifactNames[slot];
            so.FindProperty("_description").stringValue = "An alien relic tied to the UFO's " + EquipmentSlotNames.Get((EquipmentSlotType)slot).ToLower()
                + ". Merge 5 of the same rarity into 1 of the next.";
            so.FindProperty("_icon").objectReferenceValue = InventoryArtBuilder.Load("T_Inv_Artifact_" + InventoryArtBuilder.GlyphNames[slot]);
            so.FindProperty("_baseRarity").enumValueIndex = 0;
            so.FindProperty("_maxRarity").enumValueIndex = 4;
            so.FindProperty("_mergeInputCount").intValue = 5;
            so.FindProperty("_slot").enumValueIndex = slot;
            so.FindProperty("_effectName").stringValue = ArtifactEffects[slot];
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(art);
        }

        private static void WriteContainer(MetaItemDefinition item)
        {
            var so = new SerializedObject(item);
            so.FindProperty("_id").stringValue = "resource_container";
            so.FindProperty("_displayName").stringValue = "Resource Container";
            so.FindProperty("_description").stringValue = "Open it for coins, materials and sometimes an artifact.";
            so.FindProperty("_icon").objectReferenceValue = Sprite(Art + "MainMenu/Reward/chest.png");
            so.FindProperty("_rarity").enumValueIndex = 2;
            so.FindProperty("_kind").enumValueIndex = (int)MetaItemKind.Container;
            so.FindProperty("_headerColor").colorValue = Hex("#2FB8D9");
            var rewards = new List<(ContainerRewardKind, string, int, int, float)>
            {
                (ContainerRewardKind.Coins, "", 1000, 3000, 1f),
                (ContainerRewardKind.Item, "wood", 100, 300, 1f),
                (ContainerRewardKind.Item, "volt_drip", 30, 80, 0.8f),
                (ContainerRewardKind.Item, "microcircuit", 2, 6, 0.7f),
            };
            for (int s = 0; s < 6; s++) rewards.Add((ContainerRewardKind.Artifact, "art_" + SlotIds[s], 1, 1, 0.08f));
            SerializedProperty list = so.FindProperty("_containerRewards");
            list.arraySize = rewards.Count;
            for (int i = 0; i < rewards.Count; i++)
            {
                SerializedProperty r = list.GetArrayElementAtIndex(i);
                r.FindPropertyRelative("Kind").enumValueIndex = (int)rewards[i].Item1;
                r.FindPropertyRelative("Id").stringValue = rewards[i].Item2;
                r.FindPropertyRelative("Min").intValue = rewards[i].Item3;
                r.FindPropertyRelative("Max").intValue = rewards[i].Item4;
                r.FindPropertyRelative("Chance").floatValue = rewards[i].Item5;
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(item);
        }

        private static void AddUnique(SerializedProperty array, Object[] values)
        {
            foreach (Object value in values)
            {
                bool found = false;
                for (int i = 0; i < array.arraySize; i++)
                {
                    if (array.GetArrayElementAtIndex(i).objectReferenceValue == value) found = true;
                }

                if (!found)
                {
                    array.arraySize++;
                    array.GetArrayElementAtIndex(array.arraySize - 1).objectReferenceValue = value;
                }
            }
        }

        // ================================================================== screen

        private static RectTransform BuildBackdrop(Transform safeArea)
        {
            RectTransform backdrop = Box(BackdropName, safeArea);
            backdrop.SetSiblingIndex(0); // under the TopHUD
            backdrop.anchorMin = new Vector2(0f, 1f);
            backdrop.anchorMax = new Vector2(1f, 1f);
            backdrop.pivot = new Vector2(0.5f, 1f);
            // One picture from the notch down to the filter bar: behind the HUD (drawn before it) and seen through
            // the UFO area of the Inventory page (which has no background of its own there).
            backdrop.anchoredPosition = new Vector2(0f, 300f);
            backdrop.sizeDelta = new Vector2(0f, 300f + 164f + UfoAreaHeight + 20f);
            var raw = backdrop.gameObject.AddComponent<RawImage>();
            raw.texture = InventoryArtBuilder.LoadTexture("T_Inv_HangarBlur");
            raw.uvRect = new Rect(0f, 0.05f, 1f, 0.89f);
            raw.raycastTarget = false;
            return backdrop;
        }

        private static void BuildScreen(RectTransform root, InventoryScreenView view)
        {
            Sprite panel = ShopArtBuilder.Load("T_Shop_Panel");
            Sprite panelCyan = ShopArtBuilder.Load("T_Shop_PanelCyan");

            // Backgrounds: the page gradient below the UFO area; above it the hangar backdrop (InventoryBackdrop, under
            // the HUD) shows through, darkened slightly towards the filter bar.
            Image gradient = Img("Background", root, InventoryArtBuilder.Load("T_Inv_Gradient"), false, Color.white);
            Stretch(gradient.rectTransform);
            gradient.rectTransform.offsetMax = new Vector2(0f, -UfoAreaHeight);
            Image vignette = Img("HangarFade", root, null, false, new Color(0.04f, 0.08f, 0.22f, 0.35f));
            Place(vignette.rectTransform, 0.5f, 0f, UfoAreaHeight - 170f, 1080f, 170f);
            vignette.rectTransform.anchorMin = new Vector2(0f, 1f);
            vignette.rectTransform.anchorMax = new Vector2(1f, 1f);
            vignette.rectTransform.sizeDelta = new Vector2(0f, 170f);

            // ---- UFO area (Equipment + Materials) ----
            RectTransform ufoArea = Box("UFOEquipmentArea", root);
            TopFull(ufoArea, 0f, UfoAreaHeight);
            view.UfoArea = ufoArea.gameObject;

            RectTransform bonus = Box("PowerBonus", ufoArea);
            Place(bonus, 0.5f, 0f, 22f, 300f, 74f);
            Image bonusBg = Img("Bg", bonus, ShopArtBuilder.Load("T_Shop_Pill"), true, new Color(1f, 1f, 1f, 0.85f));
            Stretch(bonusBg.rectTransform);
            Image bonusIcon = Img("Icon", bonus, Sprite(Art + "MainMenu/Avatar/strong.png"), false, Color.white);
            bonusIcon.preserveAspect = true;
            Place(bonusIcon.rectTransform, 0f, 26f, 8f, 60f, 58f);
            view.BonusText = Txt("Value", bonus, "0%", 56f, Color.white, TextAlignmentOptions.Center, true);
            Place(view.BonusText.rectTransform, 0f, 86f, 2f, 200f, 70f);

            Image glow = Img("UfoGlow", ufoArea, AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Game/Art/Textures/UI/T_ChestAuraGlow.png"), false, new Color(0.3f, 0.95f, 1f, 0.35f));
            Place(glow.rectTransform, 0.5f, 0f, 330f, 760f, 420f);
            Image fallback = Img("UfoFallback", ufoArea, Sprite(Art + "Play/LevelUpdate/ufoicon.png"), false, Color.white);
            fallback.preserveAspect = true;
            Place(fallback.rectTransform, 0.5f, 0f, 260f, 420f, 300f);
            fallback.gameObject.SetActive(false);
            view.UfoFallback = fallback;
            RectTransform previewRect = Box("UFOPreview", ufoArea);
            Place(previewRect, 0.5f, 0f, 175f, 600f, 450f);
            var raw = previewRect.gameObject.AddComponent<RawImage>();
            raw.raycastTarget = false;
            view.UfoPreview = raw;

            // Six sockets: left Flight stick / Reactor / Antigravity, right Chair / Cargo / Engine.
            var slots = new InvItemCardView[6];
            (EquipmentSlotType slot, bool left, int row)[] layout =
            {
                (EquipmentSlotType.Controls, true, 0), (EquipmentSlotType.Core, true, 1), (EquipmentSlotType.AntiGravity, true, 2),
                (EquipmentSlotType.Seat, false, 0), (EquipmentSlotType.CargoBay, false, 1), (EquipmentSlotType.Engine, false, 2),
            };
            foreach (var (slot, left, row) in layout)
            {
                InvItemCardView card = BuildCard(ufoArea, "Slot_" + EquipmentSlotNames.Get(slot).Replace(" ", ""), 158f);
                Place((RectTransform)card.transform, left ? 0f : 1f, left ? 40f : -40f, 120f + row * 200f, 158f, 158f);
                slots[(int)slot] = card;
            }

            view.Slots = slots;

            // Current UFO card (all tabs).
            RectTransform current = Box("CurrentUFOInfo", root);
            Place(current, 0.5f, 0f, 640f, 200f, 160f);
            Image currentBg = Img("Card", current, panel, true, Hex("#3BB54A"));
            Place(currentBg.rectTransform, 0.5f, 0f, 0f, 112f, 112f);
            Image ufoIcon = Img("Icon", currentBg.rectTransform, Sprite(Art + "Play/LevelUpdate/ufoicon.png"), false, Color.white);
            ufoIcon.preserveAspect = true;
            Stretch(ufoIcon.rectTransform, 8f, 8f);
            Image arrow = Img("Upgrade", currentBg.rectTransform, Sprite(Art + "Play/HUD/icon-arrow-up-green.png"), false, Color.white);
            arrow.preserveAspect = true;
            Place(arrow.rectTransform, 1f, 18f, -18f, 50f, 50f);
            TMP_Text currentName = Txt("Name", current, "Saucers", 38f, Color.white, TextAlignmentOptions.Center, true);
            Place(currentName.rectTransform, 0.5f, 0f, 108f, 220f, 48f);
            Image sparkle = Img("Sparkle", current, AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Game/Art/Textures/UI/T_ChestAuraSparkle.png"), false, new Color(0.5f, 0.9f, 1f, 0.7f));
            Place(sparkle.rectTransform, 0.5f, 0f, -30f, 240f, 220f);
            sparkle.rectTransform.SetAsFirstSibling();
            sparkle.gameObject.AddComponent<UiConstantRotate>();

            // ---- Artifact progression (Artifacts tab, same region) ----
            view.Progress = BuildProgress(root);

            // ---- Filter bar ----
            RectTransform bar = Box("EquipmentCategoryBar", root);
            TopFull(bar, UfoAreaHeight, BarHeight);
            Image barBg = Img("Bg", bar, null, false, Hex("#2D86E2"));
            Stretch(barBg.rectTransform);
            Image barTop = Img("TopLine", bar, null, false, Hex("#73C8FF"));
            TopFull(barTop.rectTransform, 0f, 10f);
            Image barBottom = Img("BottomLine", bar, null, false, Hex("#123E86"));
            barBottom.rectTransform.anchorMin = Vector2.zero;
            barBottom.rectTransform.anchorMax = new Vector2(1f, 0f);
            barBottom.rectTransform.pivot = new Vector2(0.5f, 0f);
            barBottom.rectTransform.sizeDelta = new Vector2(0f, 10f);
            RectTransform icons = Box("FilterIcons", bar);
            icons.anchorMin = new Vector2(0f, 0f);
            icons.anchorMax = new Vector2(0f, 1f);
            icons.pivot = new Vector2(0f, 0.5f);
            icons.anchoredPosition = new Vector2(10f, 0f);
            icons.sizeDelta = new Vector2(690f, 0f);
            Image iconsBg = Img("Bg", icons, panel, true, new Color(0.6f, 0.85f, 1f, 0.25f));
            Stretch(iconsBg.rectTransform, 0f, 16f);
            var iconsLayout = icons.gameObject.AddComponent<HorizontalLayoutGroup>();
            iconsLayout.padding = new RectOffset(8, 8, 18, 18);
            iconsLayout.spacing = 4f;
            iconsLayout.childAlignment = TextAnchor.MiddleLeft;
            iconsLayout.childControlWidth = iconsLayout.childControlHeight = true;
            iconsLayout.childForceExpandWidth = iconsLayout.childForceExpandHeight = false;
            view.FilterIcons = icons.gameObject;
            var filters = new InvToggleButtonView[7];
            filters[0] = FilterButton(icons, "Btn_All", InventoryArtBuilder.Load("T_Inv_Glyph_All"));
            for (int i = 0; i < 6; i++)
            {
                EquipmentSlotType slot = EquipmentSlotNames.BarOrder[i];
                filters[i + 1] = FilterButton(icons, "Btn_" + EquipmentSlotNames.Get(slot).Replace(" ", ""), Glyph(slot));
            }

            view.Filters = filters;

            Button craft = Btn("Btn_Craft", bar, InventoryArtBuilder.Load("T_Inv_ButtonGreen"));
            RectTransform craftRect = (RectTransform)craft.transform;
            craftRect.anchorMin = craftRect.anchorMax = craftRect.pivot = new Vector2(1f, 0.5f);
            craftRect.anchoredPosition = new Vector2(-24f, 0f);
            craftRect.sizeDelta = new Vector2(330f, 104f);
            TMP_Text craftLabel = Txt("Label", craftRect, "Craft", 62f, Color.white, TextAlignmentOptions.Center, true);
            Stretch(craftLabel.rectTransform, 10f, 6f);
            view.CraftButton = craft;
            view.CraftBadge = Badge(craftRect);

            Button merge = Btn("Btn_Merge", bar, InventoryArtBuilder.Load("T_Inv_ButtonGreen"));
            RectTransform mergeRect = (RectTransform)merge.transform;
            mergeRect.anchorMin = mergeRect.anchorMax = mergeRect.pivot = new Vector2(1f, 0.5f);
            mergeRect.anchoredPosition = new Vector2(-24f, 0f);
            mergeRect.sizeDelta = new Vector2(330f, 104f);
            Image mergeIcon = Img("Icon", mergeRect, InventoryArtBuilder.Load("T_Inv_MergeArrow"), false, Color.white);
            Place(mergeIcon.rectTransform, 0f, 26f, 22f, 64f, 60f);
            TMP_Text mergeLabel = Txt("Label", mergeRect, "Merge", 58f, Color.white, TextAlignmentOptions.Center, true);
            Stretch(mergeLabel.rectTransform, 10f, 6f);
            mergeLabel.rectTransform.offsetMin = new Vector2(84f, 6f);
            view.MergeButton = merge;
            view.MergeImage = (Image)merge.targetGraphic;
            view.ButtonGreen = InventoryArtBuilder.Load("T_Inv_ButtonGreen");
            view.ButtonGrey = InventoryArtBuilder.Load("T_Inv_ButtonGrey");

            // ---- Content pages ----
            RectTransform content = Box("InventoryContentArea", root);
            content.anchorMin = Vector2.zero;
            content.anchorMax = Vector2.one;
            content.offsetMin = new Vector2(0f, SubTabHeight + 8f);
            content.offsetMax = new Vector2(0f, -(UfoAreaHeight + BarHeight));
            view.ContentGroup = content.gameObject.AddComponent<CanvasGroup>();

            view.EquipmentPage = Page(content, "EquipmentContent", out view.EquipmentScroll, out RectTransform equipmentContent);
            view.EquipmentGrid = Grid(equipmentContent, "Grid");
            view.EquipmentEmpty = EmptyText(view.EquipmentPage.transform);
            view.ArtifactPage = Page(content, "ArtifactsContent", out view.ArtifactScroll, out RectTransform artifactContent);
            view.ArtifactGrid = Grid(artifactContent, "Grid");
            view.ArtifactEmpty = EmptyText(view.ArtifactPage.transform);
            view.MaterialsPage = Page(content, "MaterialsContent", out view.MaterialsScroll, out RectTransform materialsContent);
            view.ContainersSection = Section(materialsContent, "Section_Containers", "Containers", out view.ContainersGrid);
            view.MaterialsSection = Section(materialsContent, "Section_Materials", "Materials", out view.MaterialsGrid);
            view.MaterialsEmpty = EmptyText(view.MaterialsPage.transform);

            // ---- Sub-tabs (fixed above the main bottom navigation) ----
            RectTransform tabs = Box("InventorySubTabs", root);
            tabs.anchorMin = new Vector2(0.5f, 0f);
            tabs.anchorMax = new Vector2(0.5f, 0f);
            tabs.pivot = new Vector2(0.5f, 0f);
            tabs.anchoredPosition = new Vector2(0f, 8f);
            tabs.sizeDelta = new Vector2(1040f, SubTabHeight - 8f);
            Image tabsBg = Img("Bg", tabs, panelCyan, true, Hex("#2C7EDB"));
            Stretch(tabsBg.rectTransform);
            string[] tabNames = { "Equipment", "Artifacts", "Materials" };
            var tabButtons = new InvToggleButtonView[3];
            for (int i = 0; i < 3; i++)
            {
                tabButtons[i] = TabButton(tabs, "Btn_" + tabNames[i], tabNames[i], 22f + i * 334f);
            }

            view.Tabs = tabButtons;

            // ---- Templates ----
            RectTransform templates = Box("Templates", root);
            templates.sizeDelta = Vector2.zero;
            view.CardTemplate = BuildCard(templates, "CardTemplate", 150f);
            view.CardTemplate.gameObject.SetActive(false);

            // ---- Merge screen + popups (on top) ----
            view.Merge = BuildMerge(root);
            view.Popups = BuildPopups(root);

            view.SlotGlyphs = new Sprite[6];
            for (int i = 0; i < 6; i++) view.SlotGlyphs[i] = Glyph((EquipmentSlotType)i);
            view.RarityColors = new[] { Hex("#8396AE"), Hex("#3DB54E"), Hex("#2F86E6"), Hex("#9B45E2"), Hex("#F2A93B") };
            view.EmptySlotColor = Hex("#1A4A92");
            view.LockedColor = Hex("#1C58B0");
            view.CoinIcon = Sprite(Art + "MainMenu/Avatar/coinicon.png");
            view.GemIcon = Sprite(Art + "MainMenu/Avatar/diamondicon.png");
            EditorUtility.SetDirty(view);
        }

        private static InvProgressView BuildProgress(RectTransform root)
        {
            RectTransform area = Box("EquipmentProgressOverview", root);
            TopFull(area, 0f, UfoAreaHeight);
            var view = area.gameObject.AddComponent<InvProgressView>();
            var icons = new Image[30];
            var bgs = new Image[30];
            var locks = new GameObject[30];
            (EquipmentSlotType slot, bool left, int row)[] layout =
            {
                (EquipmentSlotType.Controls, true, 0), (EquipmentSlotType.Core, true, 1), (EquipmentSlotType.AntiGravity, true, 2),
                (EquipmentSlotType.Seat, false, 0), (EquipmentSlotType.CargoBay, false, 1), (EquipmentSlotType.Engine, false, 2),
            };
            foreach (var (slot, left, row) in layout)
            {
                RectTransform panel = Box("Progress_" + EquipmentSlotNames.Get(slot).Replace(" ", ""), area);
                Place(panel, left ? 0f : 1f, left ? 26f : -26f, 60f + row * 196f, 486f, 168f);
                Image bg = Img("Bg", panel, ShopArtBuilder.Load("T_Shop_PanelCyan"), true, Hex("#1E63C8"));
                Stretch(bg.rectTransform);
                Image glyph = Img("Glyph", panel, Glyph(slot), false, Hex("#BDE6FF"));
                Place(glyph.rectTransform, 0f, 22f, 16f, 46f, 46f);
                TMP_Text title = Txt("Title", panel, EquipmentSlotNames.Get(slot), 42f, Color.white, TextAlignmentOptions.Left, true);
                Place(title.rectTransform, 0f, 78f, 12f, 380f, 54f);
                for (int r = 0; r < 5; r++)
                {
                    int i = (int)slot * 5 + r;
                    Image cell = Img("Cell" + r, panel, ShopArtBuilder.Load("T_Shop_Panel"), true, Hex("#1C58B0"));
                    Place(cell.rectTransform, 0f, 22f + r * 90f, 76f, 80f, 80f);
                    bgs[i] = cell;
                    Image icon = Img("Icon", cell.rectTransform, null, false, Color.white);
                    icon.preserveAspect = true;
                    Stretch(icon.rectTransform, 8f, 8f);
                    icons[i] = icon;
                    Image lockIcon = Img("Lock", cell.rectTransform, Sprite(Art + "MainMenu/Level/LockIcon.png"), false, new Color(0.85f, 0.9f, 1f, 0.9f));
                    lockIcon.preserveAspect = true;
                    Stretch(lockIcon.rectTransform, 18f, 16f);
                    locks[i] = lockIcon.gameObject;
                }
            }

            var so = new SerializedObject(view);
            SetArray(so.FindProperty("_icons"), icons);
            SetArray(so.FindProperty("_backgrounds"), bgs);
            SetArray(so.FindProperty("_locks"), locks);
            so.ApplyModifiedPropertiesWithoutUndo();
            area.gameObject.SetActive(false);
            return view;
        }

        // ------------------------------------------------------------------ cards / buttons

        private static InvItemCardView BuildCard(Transform parent, string name, float size)
        {
            RectTransform root = Box(name, parent);
            root.sizeDelta = new Vector2(size, size);
            var element = root.gameObject.AddComponent<LayoutElement>();
            element.preferredWidth = element.preferredHeight = size;
            var view = root.gameObject.AddComponent<InvItemCardView>();
            Image bg = Img("Background", root, ShopArtBuilder.Load("T_Shop_Panel"), true, Hex("#8396AE"));
            bg.raycastTarget = true;
            Stretch(bg.rectTransform);
            var button = root.gameObject.AddComponent<Button>();
            button.targetGraphic = bg;
            button.transition = Selectable.Transition.None;
            root.gameObject.AddComponent<UiPressScale>();
            Image empty = Img("EmptyGlyph", root, null, false, new Color(1f, 1f, 1f, 0.32f));
            empty.preserveAspect = true;
            Stretch(empty.rectTransform, size * 0.24f, size * 0.24f);
            Image icon = Img("Icon", root, null, false, Color.white);
            icon.preserveAspect = true;
            Stretch(icon.rectTransform, size * 0.1f, size * 0.1f);
            RectTransform part = Box("PartBadge", root);
            Place(part, 0f, -10f, -10f, size * 0.32f, size * 0.32f);
            Image partBg = Img("Bg", part, Sprite(Art + "MainMenu/Defense/badge_round.png"), false, Color.white);
            Stretch(partBg.rectTransform);
            Image partIcon = Img("Icon", part, null, false, Color.white);
            partIcon.preserveAspect = true;
            Stretch(partIcon.rectTransform, size * 0.06f, size * 0.06f);
            TMP_Text level = Txt("Level", root, "Lvl.1", size * 0.2f, Color.white, TextAlignmentOptions.Center, true);
            Place(level.rectTransform, 0.5f, size * 0.08f, 2f, size * 0.7f, size * 0.24f);
            TMP_Text amount = Txt("Amount", root, "", size * 0.24f, Color.white, TextAlignmentOptions.BottomRight, true);
            Stretch(amount.rectTransform, size * 0.06f, size * 0.02f);
            Image up = Img("UpgradeArrow", root, Sprite(Art + "Play/HUD/icon-arrow-up-green.png"), false, Color.white);
            up.preserveAspect = true;
            Place(up.rectTransform, 1f, size * 0.12f, -size * 0.12f, size * 0.32f, size * 0.32f);
            GameObject newBadge = Badge(root);
            RectTransform tag = Box("NewTag", root);
            Place(tag, 1f, size * 0.12f, -size * 0.1f, size * 0.6f, size * 0.24f);
            Image tagBg = Img("Bg", tag, ShopArtBuilder.Load("T_Shop_Panel"), true, Hex("#FF7A1F"));
            Stretch(tagBg.rectTransform);
            TMP_Text tagText = Txt("Text", tag, "NEW", size * 0.17f, Color.white, TextAlignmentOptions.Center, true);
            Stretch(tagText.rectTransform, 2f, 0f);
            tag.gameObject.SetActive(false);
            Image tick = Img("Equipped", root, Sprite(Art + "MainMenu/Reward/tick-icon.png"), false, Color.white);
            tick.preserveAspect = true;
            tick.rectTransform.anchorMin = tick.rectTransform.anchorMax = tick.rectTransform.pivot = Vector2.zero;
            tick.rectTransform.anchoredPosition = new Vector2(4f, 4f);
            tick.rectTransform.sizeDelta = new Vector2(size * 0.28f, size * 0.28f);
            TMP_Text label = Txt("Label", root, "", 28f, Color.white, TextAlignmentOptions.Top, true);
            label.rectTransform.anchorMin = new Vector2(0f, 0f);
            label.rectTransform.anchorMax = new Vector2(1f, 0f);
            label.rectTransform.pivot = new Vector2(0.5f, 1f);
            label.rectTransform.anchoredPosition = new Vector2(0f, -6f);
            label.rectTransform.sizeDelta = new Vector2(40f, 64f);
            label.textWrappingMode = TextWrappingModes.Normal;
            Wire(view, ("_button", button), ("_background", bg), ("_icon", icon), ("_emptyGlyph", empty), ("_partBadge", part.gameObject),
                ("_partIcon", partIcon), ("_level", level), ("_amount", amount), ("_upgradeArrow", up.gameObject), ("_newBadge", newBadge),
                ("_newTag", tag.gameObject), ("_equippedMark", tick.gameObject), ("_label", label));
            return view;
        }

        private static InvToggleButtonView FilterButton(Transform parent, string name, Sprite glyph)
        {
            RectTransform root = Box(name, parent);
            var element = root.gameObject.AddComponent<LayoutElement>();
            element.preferredWidth = element.minWidth = 92f;
            element.preferredHeight = 88f;
            Image hit = root.gameObject.AddComponent<Image>();
            hit.color = Color.clear;
            var button = root.gameObject.AddComponent<Button>();
            button.targetGraphic = hit;
            button.transition = Selectable.Transition.None;
            root.gameObject.AddComponent<UiPressScale>();
            Image icon = Img("Icon", root, glyph, false, Color.white);
            icon.preserveAspect = true;
            Stretch(icon.rectTransform, 12f, 10f);
            GameObject badge = Badge(root);
            ((RectTransform)badge.transform).sizeDelta = new Vector2(40f, 40f);
            var view = root.gameObject.AddComponent<InvToggleButtonView>();
            Wire(view, ("_button", button), ("_icon", icon), ("_badge", badge));
            return view;
        }

        private static InvToggleButtonView TabButton(Transform parent, string name, string label, float x)
        {
            Button button = Btn(name, parent, ShopArtBuilder.Load("T_Shop_TabBlue"));
            RectTransform rect = (RectTransform)button.transform;
            Place(rect, 0f, x, 14f, 326f, 96f);
            TMP_Text text = Txt("Label", rect, label, 48f, Color.white, TextAlignmentOptions.Center, true);
            Stretch(text.rectTransform, 10f, 6f);
            GameObject badge = Badge(rect);
            var view = rect.gameObject.AddComponent<InvToggleButtonView>();
            Wire(view, ("_button", button), ("_background", button.targetGraphic), ("_badge", badge),
                ("_normalSprite", ShopArtBuilder.Load("T_Shop_TabBlue")), ("_selectedSprite", InventoryArtBuilder.Load("T_Inv_TabGreen")));
            return view;
        }

        private static GameObject Page(Transform parent, string name, out ScrollRect scroll, out RectTransform content)
        {
            RectTransform page = Box(name, parent);
            Stretch(page);
            scroll = page.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.scrollSensitivity = 40f;
            RectTransform viewport = Box("Viewport", page);
            Stretch(viewport);
            viewport.gameObject.AddComponent<RectMask2D>();
            Image hit = viewport.gameObject.AddComponent<Image>();
            hit.color = Color.clear;
            content = Box("Content", viewport);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.offsetMin = content.offsetMax = Vector2.zero;
            var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(30, 30, 26, 40);
            layout.spacing = 22f;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.viewport = viewport;
            scroll.content = content;
            return page.gameObject;
        }

        private static Transform Grid(Transform parent, string name)
        {
            RectTransform grid = Box(name, parent);
            var layout = grid.gameObject.AddComponent<GridLayoutGroup>();
            layout.cellSize = new Vector2(150f, 150f);
            layout.spacing = new Vector2(20f, 22f);
            layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            layout.constraintCount = 6;
            layout.childAlignment = TextAnchor.UpperLeft;
            var gridWidth = grid.gameObject.AddComponent<LayoutElement>();
            gridWidth.preferredWidth = 6 * 150f + 5 * 20f; // fixed column, centred by the page, cards left-aligned
            return grid;
        }

        private static GameObject Section(Transform parent, string name, string title, out Transform grid)
        {
            RectTransform section = Box(name, parent);
            section.gameObject.AddComponent<LayoutElement>().preferredWidth = 6 * 150f + 5 * 20f;
            var layout = section.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 18f;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            RectTransform header = Box("Header", section);
            var headerElement = header.gameObject.AddComponent<LayoutElement>();
            headerElement.preferredHeight = headerElement.minHeight = 72f;
            Image headerBg = Img("Bg", header, ShopArtBuilder.Load("T_Shop_Panel"), true, Hex("#2A6CB9"));
            Stretch(headerBg.rectTransform);
            TMP_Text text = Txt("Title", header, title, 50f, Color.white, TextAlignmentOptions.Center, true);
            Stretch(text.rectTransform, 10f, 4f);
            grid = Grid(section, "Grid");
            return section.gameObject;
        }

        private static TMP_Text EmptyText(Transform page)
        {
            TMP_Text text = Txt("Empty", page, "", 40f, new Color(1f, 1f, 1f, 0.8f), TextAlignmentOptions.Center, true);
            Place(text.rectTransform, 0.5f, 0f, 80f, 900f, 140f);
            text.textWrappingMode = TextWrappingModes.Normal;
            text.gameObject.SetActive(false);
            return text;
        }

        // ------------------------------------------------------------------ merge screen

        private static InvMergeView BuildMerge(RectTransform root)
        {
            RectTransform overlay = Box("ArtifactMerge", root);
            overlay.anchorMin = Vector2.zero;
            overlay.anchorMax = Vector2.one;
            overlay.offsetMin = new Vector2(0f, -250f); // over the main bottom navigation
            overlay.offsetMax = Vector2.zero;
            var view = overlay.gameObject.AddComponent<InvMergeView>();
            Image bg = Img("Background", overlay, InventoryArtBuilder.Load("T_Inv_Gradient"), false, Color.white);
            bg.raycastTarget = true;
            Stretch(bg.rectTransform);
            RectTransform hangar = Box("Hangar", overlay);
            TopFull(hangar, 0f, 700f);
            var hangarImage = hangar.gameObject.AddComponent<RawImage>();
            hangarImage.texture = InventoryArtBuilder.LoadTexture("T_Inv_HangarBlur");
            hangarImage.uvRect = new Rect(0f, 0.15f, 1f, 0.5f);
            hangarImage.raycastTarget = false;

            RectTransform panel = Box("Panel", overlay);
            Place(panel, 0.5f, 0f, 300f, 1020f, 860f);
            Image frame = Img("Frame", panel, ShopArtBuilder.Load("T_Shop_PanelCyan"), true, Hex("#2A7FE0"));
            Place(frame.rectTransform, 0.5f, 0f, 50f, 1020f, 810f);
            Image title = Img("Title", panel, Sprite(Art + "MainMenu/Defense/header_green.png"), true, Color.white);
            Place(title.rectTransform, 0.5f, 0f, 0f, 960f, 106f);
            TMP_Text titleText = Txt("Text", title.rectTransform, "Artifacts merge", 58f, Color.white, TextAlignmentOptions.Center, true);
            Stretch(titleText.rectTransform, 20f, 8f);

            var rows = new InvMergeView.Row[4];
            for (int r = 0; r < 4; r++)
            {
                RectTransform row = Box("Row" + r, panel);
                Place(row, 0.5f, 0f, 130f + r * 158f, 960f, 146f);
                Image highlight = Img("Highlight", row, ShopArtBuilder.Load("T_Shop_Panel"), true, new Color(1f, 0.85f, 0.3f, 0.9f));
                Stretch(highlight.rectTransform, -6f, -6f);
                highlight.gameObject.SetActive(false);
                Image rowBg = Img("Bg", row, ShopArtBuilder.Load("T_Shop_Panel"), true, Hex("#2266C2"));
                rowBg.raycastTarget = true;
                Stretch(rowBg.rectTransform);
                var button = row.gameObject.AddComponent<Button>();
                button.targetGraphic = rowBg;
                button.transition = Selectable.Transition.None;
                var sources = new InvItemCardView[5];
                for (int s = 0; s < 5; s++)
                {
                    sources[s] = BuildCard(row, "Source" + s, 112f);
                    Place((RectTransform)sources[s].transform, 0f, 20f + s * 124f, 17f, 112f, 112f);
                    sources[s].GetComponent<Button>().enabled = false;
                }

                Image arrow = Img("Arrow", row, InventoryArtBuilder.Load("T_Inv_MergeArrow"), false, Color.white);
                Place(arrow.rectTransform, 0f, 652f, 33f, 92f, 80f);
                InvItemCardView result = BuildCard(row, "Result", 112f);
                Place((RectTransform)result.transform, 0f, 812f, 17f, 112f, 112f);
                result.GetComponent<Button>().enabled = false;
                rows[r] = new InvMergeView.Row { Button = button, Highlight = highlight.gameObject, Sources = sources, Result = result };
            }

            Image messageBg = Img("MessageBar", panel, ShopArtBuilder.Load("T_Shop_Panel"), true, Hex("#2A6CB9"));
            Place(messageBg.rectTransform, 0.5f, 0f, 772f, 960f, 70f);
            TMP_Text message = Txt("Message", messageBg.rectTransform, "There are no artifacts available for merging", 38f, Color.white, TextAlignmentOptions.Center, true);
            Stretch(message.rectTransform, 16f, 4f);

            RectTransform bottom = Box("BottomBar", overlay);
            bottom.anchorMin = Vector2.zero;
            bottom.anchorMax = new Vector2(1f, 0f);
            bottom.pivot = new Vector2(0.5f, 0f);
            bottom.anchoredPosition = Vector2.zero;
            bottom.sizeDelta = new Vector2(0f, 250f);
            Image bottomBg = Img("Bg", bottom, null, false, Hex("#1F62C4"));
            bottomBg.raycastTarget = true;
            Stretch(bottomBg.rectTransform);
            bottomBg.rectTransform.offsetMin = new Vector2(0f, -300f);
            Image line = Img("Line", bottom, null, false, Hex("#DDF3FF"));
            TopFull(line.rectTransform, 0f, 10f);
            Button back = Btn("Back", bottom, ShopArtBuilder.Load("T_Shop_TabBlue"));
            Place((RectTransform)back.transform, 0f, 14f, 50f, 200f, 160f);
            Image backArrow = Img("Arrow", back.transform, ShopArtBuilder.Load("T_Shop_BackArrow"), false, Color.white);
            backArrow.preserveAspect = true;
            Stretch(backArrow.rectTransform, 34f, 26f);
            Button mergeButton = Btn("Merge", bottom, InventoryArtBuilder.Load("T_Inv_ButtonGrey"));
            Place((RectTransform)mergeButton.transform, 0.5f, 60f, 48f, 560f, 160f);
            TMP_Text mergeLabel = Txt("Label", mergeButton.transform, "Merge", 76f, Color.white, TextAlignmentOptions.Center, true);
            Stretch(mergeLabel.rectTransform, 10f, 8f);
            Image flash = Img("Flash", overlay, null, false, Color.white);
            Stretch(flash.rectTransform);
            flash.gameObject.SetActive(false);

            Wire(view, ("_message", message), ("_back", back), ("_merge", mergeButton), ("_mergeImage", mergeButton.targetGraphic),
                ("_mergeOn", InventoryArtBuilder.Load("T_Inv_ButtonGreen")), ("_mergeOff", InventoryArtBuilder.Load("T_Inv_ButtonGrey")), ("_flash", flash));
            var so = new SerializedObject(view);
            SerializedProperty rowsProp = so.FindProperty("_rows");
            rowsProp.arraySize = rows.Length;
            for (int r = 0; r < rows.Length; r++)
            {
                SerializedProperty p = rowsProp.GetArrayElementAtIndex(r);
                p.FindPropertyRelative("Button").objectReferenceValue = rows[r].Button;
                p.FindPropertyRelative("Highlight").objectReferenceValue = rows[r].Highlight;
                p.FindPropertyRelative("Result").objectReferenceValue = rows[r].Result;
                SetArray(p.FindPropertyRelative("Sources"), rows[r].Sources);
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            overlay.gameObject.SetActive(false);
            return view;
        }

        // ------------------------------------------------------------------ popups

        private static InventoryPopupsView BuildPopups(RectTransform root)
        {
            RectTransform layer = Box("PopupLayer", root);
            layer.anchorMin = Vector2.zero;
            layer.anchorMax = Vector2.one;
            layer.offsetMin = new Vector2(0f, -260f);
            layer.offsetMax = new Vector2(0f, 200f);
            var view = layer.gameObject.AddComponent<InventoryPopupsView>();
            Image dim = Img("Dim", layer, null, false, new Color(0f, 0f, 0f, 0.72f));
            dim.raycastTarget = true;
            dim.rectTransform.anchorMin = Vector2.zero;
            dim.rectTransform.anchorMax = Vector2.one;
            dim.rectTransform.offsetMin = new Vector2(0f, -400f);
            dim.rectTransform.offsetMax = new Vector2(0f, 400f);
            var closes = new List<Button>();
            var dimButton = dim.gameObject.AddComponent<Button>();
            dimButton.transition = Selectable.Transition.None;
            closes.Add(dimButton);

            // Equipment detail.
            RectTransform eq = PopupPanel(layer, "EquipmentDetailPopup", 920f, 1140f, "Equipment", closes);
            var e = view.Equipment;
            e.Root = eq;
            e.Card = BuildCard(eq, "Card", 230f);
            Place((RectTransform)e.Card.transform, 0f, 60f, 110f, 230f, 230f);
            e.Card.GetComponent<Button>().enabled = false;
            e.Name = Txt("Name", eq, "", 52f, Color.white, TextAlignmentOptions.Left, true);
            Place(e.Name.rectTransform, 0f, 320f, 110f, 560f, 66f);
            e.Rarity = Txt("Rarity", eq, "", 42f, Color.white, TextAlignmentOptions.Left, true);
            Place(e.Rarity.rectTransform, 0f, 320f, 180f, 560f, 54f);
            e.Level = Txt("Level", eq, "", 40f, Color.white, TextAlignmentOptions.Left, true);
            Place(e.Level.rectTransform, 0f, 320f, 238f, 560f, 52f);
            e.State = Txt("State", eq, "", 36f, Hex("#7CFF6B"), TextAlignmentOptions.Left, true);
            Place(e.State.rectTransform, 0f, 320f, 294f, 560f, 48f);
            Image statsBg = Img("StatsBg", eq, ShopArtBuilder.Load("T_Shop_Panel"), true, Hex("#1E58B0"));
            Place(statsBg.rectTransform, 0.5f, 0f, 370f, 820f, 250f);
            e.Stats = Txt("Stats", statsBg.rectTransform, "", 38f, Color.white, TextAlignmentOptions.TopLeft, true);
            Stretch(e.Stats.rectTransform, 30f, 20f);
            e.Stats.enableAutoSizing = false;
            e.Stats.textWrappingMode = TextWrappingModes.Normal;
            Button equip = Btn("EquipButton", eq, ShopArtBuilder.Load("T_Shop_TabBlue"));
            Place((RectTransform)equip.transform, 0f, 50f, 660f, 360f, 120f);
            e.EquipButton = equip;
            e.EquipLabel = Txt("Label", equip.transform, "Equip", 54f, Color.white, TextAlignmentOptions.Center, true);
            Stretch(e.EquipLabel.rectTransform, 10f, 6f);
            Button upgrade = Btn("UpgradeButton", eq, InventoryArtBuilder.Load("T_Inv_ButtonGreen"));
            Place((RectTransform)upgrade.transform, 1f, -50f, 660f, 420f, 120f);
            e.UpgradeButton = upgrade;
            e.UpgradeImage = (Image)upgrade.targetGraphic;
            e.UpgradeLabel = Txt("Label", upgrade.transform, "Upgrade", 54f, Color.white, TextAlignmentOptions.Center, true);
            Stretch(e.UpgradeLabel.rectTransform, 10f, 6f);
            e.UpgradeCost = Txt("UpgradeCost", eq, "", 32f, Color.white, TextAlignmentOptions.Center, true);
            Place(e.UpgradeCost.rectTransform, 0.5f, 0f, 792f, 840f, 46f);
            Button rarity = Btn("RarityButton", eq, InventoryArtBuilder.Load("T_Inv_ButtonGreen"));
            Place((RectTransform)rarity.transform, 0.5f, 0f, 860f, 820f, 140f);
            e.RarityButton = rarity;
            e.RarityImage = (Image)rarity.targetGraphic;
            e.RarityCost = Txt("Label", rarity.transform, "", 36f, Color.white, TextAlignmentOptions.Center, true);
            Stretch(e.RarityCost.rectTransform, 20f, 8f);
            e.RarityCost.textWrappingMode = TextWrappingModes.Normal;
            e.Gain = Txt("Gain", eq, "+0%", 60f, Hex("#7CFF6B"), TextAlignmentOptions.Center, true);
            Place(e.Gain.rectTransform, 0f, 60f, 200f, 230f, 80f);
            e.Gain.gameObject.SetActive(false);

            // Craft.
            RectTransform craft = PopupPanel(layer, "CraftPopup", 980f, 1500f, "Craft", closes);
            var c = view.Craft;
            c.Root = craft;
            GameObject page = Page(craft, "List", out c.Scroll, out RectTransform list);
            RectTransform pageRect = (RectTransform)page.transform;
            pageRect.offsetMin = new Vector2(10f, 30f);
            pageRect.offsetMax = new Vector2(-10f, -110f);
            list.GetComponent<VerticalLayoutGroup>().padding = new RectOffset(20, 20, 10, 20);
            list.GetComponent<VerticalLayoutGroup>().spacing = 16f;
            c.List = list;
            c.RowTemplate = BuildCraftRow(list);
            c.RowTemplate.gameObject.SetActive(false);
            c.Empty = EmptyText(craft);

            // Artifact detail.
            RectTransform artifact = PopupPanel(layer, "ArtifactDetailPopup", 900f, 820f, "Artifact", closes);
            var a = view.Artifact;
            a.Root = artifact;
            a.Card = BuildCard(artifact, "Card", 220f);
            Place((RectTransform)a.Card.transform, 0f, 60f, 110f, 220f, 220f);
            a.Card.GetComponent<Button>().enabled = false;
            a.Name = Txt("Name", artifact, "", 52f, Color.white, TextAlignmentOptions.Left, true);
            Place(a.Name.rectTransform, 0f, 310f, 116f, 540f, 66f);
            a.Rarity = Txt("Rarity", artifact, "", 40f, Color.white, TextAlignmentOptions.Left, true);
            Place(a.Rarity.rectTransform, 0f, 310f, 190f, 540f, 52f);
            a.Effect = Txt("Effect", artifact, "", 44f, Hex("#7CFF6B"), TextAlignmentOptions.Left, true);
            Place(a.Effect.rectTransform, 0f, 310f, 256f, 540f, 56f);
            a.Progress = Txt("Progress", artifact, "", 38f, Color.white, TextAlignmentOptions.Center, true);
            Place(a.Progress.rectTransform, 0.5f, 0f, 380f, 800f, 56f);
            a.Description = Txt("Description", artifact, "", 34f, new Color(0.9f, 0.95f, 1f), TextAlignmentOptions.Top, true);
            Place(a.Description.rectTransform, 0.5f, 0f, 470f, 780f, 220f);
            a.Description.textWrappingMode = TextWrappingModes.Normal;
            a.Description.enableAutoSizing = false;

            // Material / container.
            RectTransform material = PopupPanel(layer, "MaterialDetailPopup", 900f, 820f, "Item", closes);
            var m = view.Material;
            m.Root = material;
            m.Card = BuildCard(material, "Card", 220f);
            Place((RectTransform)m.Card.transform, 0f, 60f, 110f, 220f, 220f);
            m.Card.GetComponent<Button>().enabled = false;
            m.Name = Txt("Name", material, "", 52f, Color.white, TextAlignmentOptions.Left, true);
            Place(m.Name.rectTransform, 0f, 310f, 130f, 540f, 66f);
            m.Amount = Txt("Amount", material, "", 40f, Hex("#FFE35A"), TextAlignmentOptions.Left, true);
            Place(m.Amount.rectTransform, 0f, 310f, 210f, 540f, 52f);
            m.Description = Txt("Description", material, "", 36f, new Color(0.9f, 0.95f, 1f), TextAlignmentOptions.Top, true);
            Place(m.Description.rectTransform, 0.5f, 0f, 370f, 780f, 200f);
            m.Description.textWrappingMode = TextWrappingModes.Normal;
            m.Description.enableAutoSizing = false;
            m.OpenOne = Btn("OpenOne", material, InventoryArtBuilder.Load("T_Inv_ButtonGreen"));
            Place((RectTransform)m.OpenOne.transform, 0f, 70f, 620f, 360f, 120f);
            TMP_Text openLabel = Txt("Label", m.OpenOne.transform, "Open 1", 52f, Color.white, TextAlignmentOptions.Center, true);
            Stretch(openLabel.rectTransform, 10f, 6f);
            m.OpenMax = Btn("OpenMax", material, InventoryArtBuilder.Load("T_Inv_ButtonGreen"));
            Place((RectTransform)m.OpenMax.transform, 1f, -70f, 620f, 360f, 120f);
            m.OpenMaxLabel = Txt("Label", m.OpenMax.transform, "Open Max", 52f, Color.white, TextAlignmentOptions.Center, true);
            Stretch(m.OpenMaxLabel.rectTransform, 10f, 6f);

            // Reward.
            RectTransform reward = PopupPanel(layer, "RewardPopup", 920f, 820f, "Rewards!", closes);
            var r = view.Reward;
            r.Root = reward;
            r.Title = reward.Find("TitleBanner/Title").GetComponent<TMP_Text>();
            Image rays = Img("Rays", reward, AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Game/Art/Textures/UI/T_ChestAuraRays.png"), false, new Color(1f, 0.9f, 0.5f, 0.4f));
            rays.rectTransform.anchorMin = rays.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            rays.rectTransform.sizeDelta = new Vector2(1300f, 1300f);
            rays.rectTransform.SetAsFirstSibling();
            r.Glow = rays.rectTransform;
            RectTransform grid = Box("Grid", reward);
            Place(grid, 0.5f, 0f, 150f, 840f, 440f);
            var gridLayout = grid.gameObject.AddComponent<GridLayoutGroup>();
            gridLayout.cellSize = new Vector2(170f, 170f);
            gridLayout.spacing = new Vector2(30f, 90f);
            gridLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            gridLayout.constraintCount = 4;
            gridLayout.childAlignment = TextAnchor.UpperCenter;
            gridLayout.padding = new RectOffset(0, 0, 20, 0);
            r.Grid = grid;
            r.Template = BuildCard(grid, "RewardTemplate", 170f);
            r.Template.GetComponent<Button>().enabled = false;
            r.Template.gameObject.SetActive(false);
            Button ok = Btn("OkButton", reward, InventoryArtBuilder.Load("T_Inv_ButtonGreen"));
            Place((RectTransform)ok.transform, 0.5f, 0f, 650f, 400f, 130f);
            TMP_Text okLabel = Txt("Label", ok.transform, "Collect", 58f, Color.white, TextAlignmentOptions.Center, true);
            Stretch(okLabel.rectTransform, 10f, 6f);
            closes.Add(ok);
            Image flash = Img("Flash", reward, null, false, Color.white);
            Stretch(flash.rectTransform, -40f, -40f);
            flash.gameObject.SetActive(false);
            r.Flash = flash;

            var so = new SerializedObject(view);
            so.FindProperty("_dim").objectReferenceValue = dim;
            SetArray(so.FindProperty("_closeButtons"), closes.ToArray());
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(view);
            layer.gameObject.SetActive(false);
            return view;
        }

        private static InvCraftRowView BuildCraftRow(Transform parent)
        {
            RectTransform row = Box("CraftRow", parent);
            var element = row.gameObject.AddComponent<LayoutElement>();
            element.preferredHeight = element.minHeight = 230f;
            element.flexibleWidth = 1f;
            var view = row.gameObject.AddComponent<InvCraftRowView>();
            Image bg = Img("Bg", row, ShopArtBuilder.Load("T_Shop_Panel"), true, Hex("#1E5BB0"));
            Stretch(bg.rectTransform);
            InvItemCardView card = BuildCard(row, "Result", 170f);
            Place((RectTransform)card.transform, 0f, 26f, 30f, 170f, 170f);
            card.GetComponent<Button>().enabled = false;
            TMP_Text name = Txt("Name", row, "", 44f, Color.white, TextAlignmentOptions.Left, true);
            Place(name.rectTransform, 0f, 222f, 18f, 640f, 56f);
            TMP_Text req = Txt("Requirements", row, "", 30f, Color.white, TextAlignmentOptions.TopLeft, true);
            Place(req.rectTransform, 0f, 222f, 80f, 440f, 140f);
            req.enableAutoSizing = true;
            req.fontSizeMin = 20f;
            req.textWrappingMode = TextWrappingModes.Normal;
            Button button = Btn("CraftButton", row, InventoryArtBuilder.Load("T_Inv_ButtonGreen"));
            Place((RectTransform)button.transform, 1f, -24f, 100f, 230f, 104f);
            TMP_Text label = Txt("Label", button.transform, "Craft", 50f, Color.white, TextAlignmentOptions.Center, true);
            Stretch(label.rectTransform, 8f, 6f);
            Wire(view, ("_result", card), ("_name", name), ("_requirements", req), ("_button", button),
                ("_buttonImage", button.targetGraphic), ("_buttonLabel", label));
            return view;
        }

        private static RectTransform PopupPanel(Transform parent, string name, float width, float height, string title, List<Button> closes)
        {
            RectTransform panel = Box(name, parent);
            panel.anchorMin = panel.anchorMax = new Vector2(0.5f, 0.5f);
            panel.sizeDelta = new Vector2(width, height);
            Image frame = Img("Frame", panel, ShopArtBuilder.Load("T_Shop_PanelCyan"), true, Hex("#2D6FD6"));
            frame.raycastTarget = true;
            Stretch(frame.rectTransform);
            Image banner = Img("TitleBanner", panel, ShopArtBuilder.Load("T_Shop_TabOrange"), true, Color.white);
            Place(banner.rectTransform, 0.5f, 0f, -40f, Mathf.Min(width - 160f, 620f), 116f);
            TMP_Text text = Txt("Title", banner.rectTransform, title, 56f, Color.white, TextAlignmentOptions.Center, true);
            Stretch(text.rectTransform, 24f, 10f);
            Button close = Btn("Close", panel, ShopArtBuilder.Load("T_Shop_Panel"));
            ((Image)close.targetGraphic).color = Hex("#E2463C");
            Place((RectTransform)close.transform, 1f, 20f, -20f, 90f, 90f);
            TMP_Text x = Txt("X", close.transform, "X", 54f, Color.white, TextAlignmentOptions.Center, true);
            Stretch(x.rectTransform);
            closes.Add(close);
            return panel;
        }

        /// <summary>The bottom navigation's notification dots were 24 px blank squares: make them the red "!" circle
        /// of the reference (the Inventory tab's badge now lights up for new / upgradable / mergeable items).</summary>
        private static void StyleNavBadges(Scene scene)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (NotificationBadgeView badge in root.GetComponentsInChildren<NotificationBadgeView>(true))
                {
                    if (badge.GetComponentInParent<BottomNavigationView>(true) == null)
                    {
                        continue;
                    }

                    var so = new SerializedObject(badge);
                    var rootObject = so.FindProperty("_root").objectReferenceValue as GameObject;
                    RectTransform rect = rootObject != null ? (RectTransform)rootObject.transform : (RectTransform)badge.transform;
                    rect.sizeDelta = new Vector2(58f, 58f);
                    rect.anchoredPosition = new Vector2(-16f, -6f);
                    var image = rect.GetComponent<Image>();
                    if (image != null)
                    {
                        image.sprite = ShopArtBuilder.Load("T_Shop_Badge");
                        image.color = Color.white;
                        image.type = Image.Type.Simple;
                    }

                    if (rect.Find("Mark") == null)
                    {
                        TMP_Text mark = Txt("Mark", rect, "!", 40f, Color.white, TextAlignmentOptions.Center, true);
                        Stretch(mark.rectTransform);
                    }

                    EditorUtility.SetDirty(rect);
                }
            }
        }

        // ================================================================== helpers

        private static Sprite Glyph(EquipmentSlotType slot) => InventoryArtBuilder.Load("T_Inv_Glyph_" + InventoryArtBuilder.GlyphNames[(int)slot]);

        private static GameObject Badge(Transform parent)
        {
            Image badge = Img("Badge", parent, ShopArtBuilder.Load("T_Shop_Badge"), false, Color.white);
            badge.rectTransform.anchorMin = badge.rectTransform.anchorMax = new Vector2(1f, 1f);
            badge.rectTransform.anchoredPosition = new Vector2(-8f, -8f);
            badge.rectTransform.sizeDelta = new Vector2(48f, 48f);
            TMP_Text mark = Txt("Mark", badge.rectTransform, "!", 34f, Color.white, TextAlignmentOptions.Center, true);
            Stretch(mark.rectTransform);
            badge.gameObject.SetActive(false);
            return badge.gameObject;
        }

        private static RectTransform Box(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = 5;
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        private static void Place(RectTransform rect, float ax, float x, float y, float w, float h)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(ax, 1f);
            rect.pivot = new Vector2(ax, 1f);
            rect.anchoredPosition = new Vector2(x, -y);
            rect.sizeDelta = new Vector2(w, h);
        }

        private static void TopFull(RectTransform rect, float top, float height)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, -top);
            rect.sizeDelta = new Vector2(0f, height);
        }

        private static void Stretch(RectTransform rect, float h = 0f, float v = 0f)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = new Vector2(h, v);
            rect.offsetMax = new Vector2(-h, -v);
        }

        private static Image Img(string name, Transform parent, Sprite sprite, bool sliced, Color color)
        {
            RectTransform rect = Box(name, parent);
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.type = sliced ? Image.Type.Sliced : Image.Type.Simple;
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private static Button Btn(string name, Transform parent, Sprite sprite)
        {
            Image image = Img(name, parent, sprite, true, Color.white);
            image.raycastTarget = true;
            var button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.transition = Selectable.Transition.None;
            var press = image.gameObject.AddComponent<UiPressScale>();
            var so = new SerializedObject(press);
            SerializedProperty scale = so.FindProperty("_pressedScale");
            if (scale != null)
            {
                scale.floatValue = 0.94f;
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            return button;
        }

        private static TMP_Text Txt(string name, Transform parent, string content, float size, Color color, TextAlignmentOptions align, bool outlined)
        {
            RectTransform rect = Box(name, parent);
            var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            if (_font != null) text.font = _font;
            if (outlined && _outlined != null) text.fontSharedMaterial = _outlined;
            text.text = content;
            text.fontSize = size;
            text.color = color;
            text.alignment = align;
            text.raycastTarget = false;
            text.enableAutoSizing = true;
            text.fontSizeMin = size * 0.45f;
            text.fontSizeMax = size;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            return text;
        }

        private static void Wire(Object target, params (string field, Object value)[] values)
        {
            var so = new SerializedObject(target);
            foreach ((string field, Object value) in values)
            {
                SerializedProperty property = so.FindProperty(field);
                if (property == null)
                {
                    Debug.LogError("[InventoryTabBuilder] " + target.GetType().Name + " has no field " + field);
                    continue;
                }

                property.objectReferenceValue = value;
            }

            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetArray(SerializedProperty property, Object[] values)
        {
            property.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++) property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        }

        private static Sprite Sprite(string path) => AssetDatabase.LoadAssetAtPath<Sprite>(path);

        private static Transform FindNamed(Scene scene, string name)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                {
                    if (t.name == name) return t;
                }
            }

            return null;
        }

        private static void DestroyChild(Transform parent, string name)
        {
            Transform child = parent.Find(name);
            while (child != null)
            {
                Object.DestroyImmediate(child.gameObject);
                child = parent.Find(name);
            }
        }

        private static void EnsureFolder(string path)
        {
            if (!AssetDatabase.IsValidFolder(path))
            {
                string parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
                AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(path));
            }
        }

        private static Color Hex(string hex)
        {
            ColorUtility.TryParseHtmlString(hex, out Color color);
            return color;
        }
    }
}
