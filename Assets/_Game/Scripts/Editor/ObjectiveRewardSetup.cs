using System.Collections.Generic;
using AlienDefense.Core;
using AlienDefense.Meta;
using AlienDefense.UI.MainMenu;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace AlienDefense.EditorTools
{
    /// <summary>Builds MetaItem catalog, seeds Level 2+ objective rewards, and wires MainMenu objective claim UI.</summary>
    public static class ObjectiveRewardSetup
    {
        private const string CatalogPath = "Assets/_Game/Data/Meta/MetaItemCatalog.asset";
        private const string ItemsFolder = "Assets/_Game/Data/Meta/Items";
        private const string LevelCatalogPath = "Assets/_Game/Data/Levels/LevelCatalog.asset";
        private const string MainMenuPath = "Assets/_Game/Scenes/Menu/MainMenu.unity";
        private const string AutoSetupSessionKey = "AlienDefense.ObjectiveRewardAutoSetup.v1";

        [InitializeOnLoadMethod]
        private static void AutoSetupWhenNeeded()
        {
            EditorApplication.delayCall += () =>
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode)
                {
                    return;
                }

                if (SessionState.GetBool(AutoSetupSessionKey, false))
                {
                    return;
                }

                SessionState.SetBool(AutoSetupSessionKey, true);

                try
                {
                    EnsureFolders();
                    MetaItemCatalog catalog = AssetDatabase.LoadAssetAtPath<MetaItemCatalog>(CatalogPath);
                    if (catalog == null)
                    {
                        catalog = BuildCatalog();
                    }

                    LevelCatalog levels = AssetDatabase.LoadAssetAtPath<LevelCatalog>(LevelCatalogPath);
                    if (levels != null && levels.Count > 1)
                    {
                        LevelCatalogEntry entry = levels.GetEntry(1);
                        if (entry != null && (entry.ClearRewards == null || !entry.ClearRewards.HasRewards))
                        {
                            SeedLevelCatalogRewards();
                        }
                    }

                    // Wire MainMenu UI only when that scene is already open (avoid hijacking editor focus).
                    var active = EditorSceneManager.GetActiveScene();
                    if (active.path != null && active.path.Replace('\\', '/').EndsWith("MainMenu.unity"))
                    {
                        bool needsWire = FindNamed(active, "ObjectiveRewardOverlay") == null
                            || FindNamed(active, "RewardBubble") == null;
                        if (needsWire)
                        {
                            WireMainMenu(catalog);
                        }
                    }
                }
                catch (System.Exception ex)
                {
                    Debug.LogWarning($"[ObjectiveRewardSetup] Auto-setup skipped: {ex.Message}");
                }
            };
        }

        [MenuItem("AlienDefense/Objectives/Setup Meta Items + Rewards + MainMenu UI")]
        public static void RunFullSetup()
        {
            EnsureFolders();
            MetaItemCatalog catalog = BuildCatalog();
            SeedLevelCatalogRewards();
            WireMainMenu(catalog);
            AssetDatabase.SaveAssets();
            Debug.Log("[ObjectiveRewardSetup] Done.");
        }

        private static void EnsureFolders()
        {
            if (!AssetDatabase.IsValidFolder("Assets/_Game/Data/Meta"))
            {
                AssetDatabase.CreateFolder("Assets/_Game/Data", "Meta");
            }

            if (!AssetDatabase.IsValidFolder(ItemsFolder))
            {
                AssetDatabase.CreateFolder("Assets/_Game/Data/Meta", "Items");
            }
        }

        private static MetaItemCatalog BuildCatalog()
        {
            const string RewardFolder = "Assets/_Game/Art/Sprite/MainMenu/Reward";
            Sprite chest = AssetDatabase.LoadAssetAtPath<Sprite>($"{RewardFolder}/chest.png");
            Sprite chip = AssetDatabase.LoadAssetAtPath<Sprite>($"{RewardFolder}/chipicon.png");
            Sprite volt = AssetDatabase.LoadAssetAtPath<Sprite>($"{RewardFolder}/voltdripicon.png");
            Sprite mystery = AssetDatabase.LoadAssetAtPath<Sprite>($"{RewardFolder}/mysteryitem.png");
            Sprite evolution = AssetDatabase.LoadAssetAtPath<Sprite>($"{RewardFolder}/evolutioncoinicon.png");

            var defs = new List<MetaItemDefinition>
            {
                CreateOrUpdateItem(MetaItemIds.Microcircuit, "Microcircuit", "High-tech resource required for research and scientific upgrades.", MetaItemRarity.Epic, new Color(1f, 0.75f, 0.15f), chip ?? chest),
                CreateOrUpdateItem(MetaItemIds.VoltDrip, "Volt Drip", "Currency for levelling up Pantheon.", MetaItemRarity.Uncommon, new Color(0.3f, 0.9f, 0.4f), volt ?? chest),
                CreateOrUpdateItem(MetaItemIds.CardMortar, "Mortar upgrade card", "Upgrade part required for the Mortar.", MetaItemRarity.Common, new Color(0.25f, 0.55f, 0.95f), chest),
                CreateOrUpdateItem(MetaItemIds.CardIceDagger, "Ice Dagger upgrade card", "Upgrade part required for the Ice Dagger.", MetaItemRarity.Common, new Color(0.35f, 0.85f, 0.95f), chest),
                CreateOrUpdateItem(MetaItemIds.CardTurret, "Turret upgrade card", "Upgrade part required for the Turret.", MetaItemRarity.Common, new Color(0.25f, 0.55f, 0.95f), chest),
                CreateOrUpdateItem(MetaItemIds.CardTesla, "Tesla upgrade card", "Upgrade part required for the Tesla.", MetaItemRarity.Common, new Color(0.55f, 0.4f, 0.95f), chest),
                CreateOrUpdateItem(MetaItemIds.CardUfo, "UFO upgrade card", "Upgrade part required for the UFO base.", MetaItemRarity.Rare, new Color(0.7f, 0.35f, 0.95f), chest),
                CreateOrUpdateItem(MetaItemIds.ResearchResource, "Mystery Item", "A mysterious reward. Complete the objective to reveal it.", MetaItemRarity.Rare, new Color(0.6f, 0.3f, 0.9f), mystery ?? chest),
                CreateOrUpdateItem(MetaItemIds.RareAtom, "Evolution Coin", "Use Evolution Coins to buy unique perks!", MetaItemRarity.Epic, new Color(1f, 0.85f, 0.2f), evolution ?? chest),
            };

            MetaItemCatalog catalog = AssetDatabase.LoadAssetAtPath<MetaItemCatalog>(CatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<MetaItemCatalog>();
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }

            SerializedObject so = new SerializedObject(catalog);
            SerializedProperty items = so.FindProperty("_items");
            items.arraySize = defs.Count;
            for (int i = 0; i < defs.Count; i++)
            {
                items.GetArrayElementAtIndex(i).objectReferenceValue = defs[i];
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(catalog);
            return catalog;
        }

        private static MetaItemDefinition CreateOrUpdateItem(
            string id,
            string displayName,
            string description,
            MetaItemRarity rarity,
            Color header,
            Sprite icon)
        {
            string path = $"{ItemsFolder}/MetaItem_{id}.asset";
            MetaItemDefinition def = AssetDatabase.LoadAssetAtPath<MetaItemDefinition>(path);
            if (def == null)
            {
                def = ScriptableObject.CreateInstance<MetaItemDefinition>();
                AssetDatabase.CreateAsset(def, path);
            }

            SerializedObject so = new SerializedObject(def);
            so.FindProperty("_id").stringValue = id;
            so.FindProperty("_displayName").stringValue = displayName;
            so.FindProperty("_description").stringValue = description;
            so.FindProperty("_rarity").enumValueIndex = (int)rarity;
            so.FindProperty("_headerColor").colorValue = header;
            so.FindProperty("_icon").objectReferenceValue = icon;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(def);
            return def;
        }

        private static void SeedLevelCatalogRewards()
        {
            LevelCatalog catalog = AssetDatabase.LoadAssetAtPath<LevelCatalog>(LevelCatalogPath);
            if (catalog == null)
            {
                Debug.LogError("[ObjectiveRewardSetup] LevelCatalog missing.");
                return;
            }

            SerializedObject so = new SerializedObject(catalog);
            SerializedProperty entries = so.FindProperty("_entries");
            for (int i = 0; i < entries.arraySize; i++)
            {
                SerializedProperty entry = entries.GetArrayElementAtIndex(i);
                if (i == 0)
                {
                    ClearBundle(entry.FindPropertyRelative("_clearRewards"));
                    ClearBundle(entry.FindPropertyRelative("_hp50Rewards"));
                    ClearBundle(entry.FindPropertyRelative("_perfectRewards"));
                    continue;
                }

                if (i == 1)
                {
                    // Screenshot reference amounts (Level 2 stand-in for "Campaign Level 6").
                    WriteBundle(entry.FindPropertyRelative("_clearRewards"),
                        Item(MetaItemIds.Microcircuit, 90),
                        Item(MetaItemIds.VoltDrip, 154));
                    WriteBundle(entry.FindPropertyRelative("_hp50Rewards"),
                        Item(MetaItemIds.ResearchResource, 300, hide: true));
                    WriteBundle(entry.FindPropertyRelative("_perfectRewards"),
                        Item(MetaItemIds.RareAtom, 1));
                }
                else
                {
                    WriteBundle(entry.FindPropertyRelative("_clearRewards"),
                        Item(MetaItemIds.Microcircuit, 75),
                        Item(MetaItemIds.VoltDrip, 112));
                    WriteBundle(entry.FindPropertyRelative("_hp50Rewards"),
                        Item(MetaItemIds.ResearchResource, 300, hide: true));
                    WriteBundle(entry.FindPropertyRelative("_perfectRewards"),
                        Item(MetaItemIds.RareAtom, 1));
                }
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(catalog);
        }

        private static ObjectiveRewardEntry Item(string id, int amount, bool hide = false)
        {
            return new ObjectiveRewardEntry
            {
                Kind = ObjectiveRewardKind.MetaItem,
                ItemId = id,
                Amount = amount,
                HideUntilUnlocked = hide
            };
        }

        private static void ClearBundle(SerializedProperty bundleProp)
        {
            SerializedProperty rewards = bundleProp.FindPropertyRelative("_rewards");
            rewards.ClearArray();
        }

        private static void WriteBundle(SerializedProperty bundleProp, params ObjectiveRewardEntry[] entries)
        {
            SerializedProperty rewards = bundleProp.FindPropertyRelative("_rewards");
            rewards.ClearArray();
            for (int i = 0; i < entries.Length; i++)
            {
                rewards.InsertArrayElementAtIndex(i);
                SerializedProperty e = rewards.GetArrayElementAtIndex(i);
                e.FindPropertyRelative("Kind").enumValueIndex = (int)entries[i].Kind;
                e.FindPropertyRelative("ItemId").stringValue = entries[i].ItemId;
                e.FindPropertyRelative("Amount").intValue = entries[i].Amount;
                e.FindPropertyRelative("HideUntilUnlocked").boolValue = entries[i].HideUntilUnlocked;
            }
        }

        private static void WireMainMenu(MetaItemCatalog catalog)
        {
            var scene = EditorSceneManager.OpenScene(MainMenuPath, OpenSceneMode.Single);
            Transform objectivesPanel = FindNamed(scene, "ObjectivesPanel");
            if (objectivesPanel == null)
            {
                Debug.LogError("[ObjectiveRewardSetup] ObjectivesPanel not found.");
                return;
            }

            for (int i = 1; i <= 3; i++)
            {
                Transform slot = objectivesPanel.Find($"Objective_0{i}");
                if (slot == null)
                {
                    continue;
                }

                EnsureSlotClaimUi(slot);
                EnsurePerSlotBubble(slot, catalog);
            }

            Button dismiss = EnsureSharedDismissBlocker(objectivesPanel);
            ObjectiveRewardOverlayView overlay = EnsureOverlay(scene, catalog);

            LevelObjectivePanelView panelView = objectivesPanel.GetComponent<LevelObjectivePanelView>();
            if (panelView != null)
            {
                SerializedObject panelSo = new SerializedObject(panelView);
                if (panelSo.FindProperty("_previewBubble") != null)
                {
                    panelSo.FindProperty("_previewBubble").objectReferenceValue = null;
                }

                panelSo.FindProperty("_dismissBlocker").objectReferenceValue = dismiss;
                panelSo.FindProperty("_detailOverlay").objectReferenceValue = overlay;
                panelSo.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(panelView);
            }

            // Disable legacy shared bubble if present.
            Transform legacy = objectivesPanel.parent != null
                ? objectivesPanel.parent.Find("RewardPreviewBubble")
                : null;
            if (legacy != null)
            {
                legacy.gameObject.SetActive(false);
            }

            MainMenuLevelSelectionPresenter presenter = null;
            foreach (MainMenuLevelSelectionPresenter p in Resources.FindObjectsOfTypeAll<MainMenuLevelSelectionPresenter>())
            {
                if (p != null && p.gameObject.scene.IsValid() && !string.IsNullOrEmpty(p.gameObject.scene.path))
                {
                    presenter = p;
                    break;
                }
            }
            if (presenter != null)
            {
                SerializedObject so = new SerializedObject(presenter);
                so.FindProperty("_rewardOverlay").objectReferenceValue = overlay;
                so.FindProperty("_metaItemCatalog").objectReferenceValue = catalog;
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(presenter);
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        private static void EnsureSlotClaimUi(Transform slot)
        {
            LevelObjectiveView view = slot.GetComponent<LevelObjectiveView>();
            if (view == null)
            {
                return;
            }

            Transform icon = slot.Find("Icon");
            Button chestButton = slot.GetComponent<Button>();
            Image iconImage = icon != null ? icon.GetComponent<Image>() : null;
            if (iconImage != null)
            {
                // Chests must receive UI raycasts or Button.onClick never fires.
                iconImage.raycastTarget = true;
            }

            // Transparent hit pad on the slot root so the whole chest area is clickable.
            Image hitPad = slot.GetComponent<Image>();
            if (hitPad == null)
            {
                hitPad = slot.gameObject.AddComponent<Image>();
                hitPad.color = new Color(1f, 1f, 1f, 0f);
            }

            hitPad.raycastTarget = true;

            if (chestButton == null)
            {
                chestButton = slot.gameObject.AddComponent<Button>();
            }

            chestButton.targetGraphic = hitPad;
            chestButton.transition = Selectable.Transition.None;

            Transform glowT = slot.Find("Glow");
            if (glowT == null && icon != null)
            {
                var glowGo = new GameObject("Glow", typeof(RectTransform), typeof(Image));
                glowGo.transform.SetParent(icon, false);
                glowGo.transform.SetAsFirstSibling();
                RectTransform glowRt = glowGo.GetComponent<RectTransform>();
                glowRt.anchorMin = Vector2.zero;
                glowRt.anchorMax = Vector2.one;
                glowRt.offsetMin = new Vector2(-12f, -12f);
                glowRt.offsetMax = new Vector2(12f, 12f);
                Image glowImg = glowGo.GetComponent<Image>();
                glowImg.color = new Color(0.2f, 0.95f, 1f, 0.35f);
                glowImg.raycastTarget = false;
                glowGo.SetActive(false);
                glowT = glowGo.transform;
            }

            Transform notif = slot.Find("NotificationDot");
            if (notif == null)
            {
                var dot = new GameObject("NotificationDot", typeof(RectTransform), typeof(Image));
                dot.transform.SetParent(slot, false);
                RectTransform dotRt = dot.GetComponent<RectTransform>();
                dotRt.anchorMin = new Vector2(1f, 1f);
                dotRt.anchorMax = new Vector2(1f, 1f);
                dotRt.pivot = new Vector2(0.5f, 0.5f);
                dotRt.anchoredPosition = new Vector2(-8f, -8f);
                dotRt.sizeDelta = new Vector2(22f, 22f);
                Image dotImg = dot.GetComponent<Image>();
                dotImg.color = new Color(1f, 0.25f, 0.25f, 1f);
                dotImg.raycastTarget = false;
                Sprite knob = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
                if (knob != null)
                {
                    dotImg.sprite = knob;
                }

                dot.SetActive(false);
                notif = dot.transform;
            }

            SerializedObject so = new SerializedObject(view);
            so.FindProperty("_chestButton").objectReferenceValue = chestButton;
            if (glowT != null)
            {
                so.FindProperty("_glow").objectReferenceValue = glowT.GetComponent<Image>();
            }

            so.FindProperty("_notificationDot").objectReferenceValue = notif != null ? notif.gameObject : null;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(view);
        }

        private static void EnsurePerSlotBubble(Transform slot, MetaItemCatalog catalog)
        {
            LevelObjectiveView slotView = slot.GetComponent<LevelObjectiveView>();
            if (slotView == null)
            {
                return;
            }

            int objectiveIndex = 1;
            if (slot.name.EndsWith("02"))
            {
                objectiveIndex = 2;
            }
            else if (slot.name.EndsWith("03"))
            {
                objectiveIndex = 3;
            }

            Sprite bubbleSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Game/Art/Sprite/MainMenu/Reward/bubblepanel.png");
            Sprite mysterySprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Game/Art/Sprite/MainMenu/Reward/mysteryitem.png");
            Sprite chip = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Game/Art/Sprite/MainMenu/Reward/chipicon.png");
            Sprite volt = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Game/Art/Sprite/MainMenu/Reward/voltdripicon.png");
            Sprite evo = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Game/Art/Sprite/MainMenu/Reward/evolutioncoinicon.png");

            Transform existing = slot.Find("RewardBubble");
            GameObject root;
            if (existing != null)
            {
                root = existing.gameObject;
            }
            else
            {
                root = new GameObject("RewardBubble", typeof(RectTransform), typeof(LayoutElement), typeof(ObjectiveRewardBubbleView));
                root.transform.SetParent(slot, false);
            }

            LayoutElement ignore = root.GetComponent<LayoutElement>();
            if (ignore == null)
            {
                ignore = root.AddComponent<LayoutElement>();
            }

            ignore.ignoreLayout = true;

            RectTransform rootRt = root.GetComponent<RectTransform>();
            rootRt.anchorMin = new Vector2(0.5f, 1f);
            rootRt.anchorMax = new Vector2(0.5f, 1f);
            rootRt.pivot = new Vector2(0.5f, 0f);
            bool isNewBubble = existing == null;
            if (isNewBubble)
            {
                rootRt.anchoredPosition = new Vector2(0f, 6f);
                rootRt.sizeDelta = new Vector2(180f, 110f);
            }

            Transform bgT = root.transform.Find("BubbleBackground");
            if (bgT == null)
            {
                var bg = new GameObject("BubbleBackground", typeof(RectTransform), typeof(Image));
                bg.transform.SetParent(root.transform, false);
                bg.transform.SetAsFirstSibling();
                bgT = bg.transform;
            }

            RectTransform bgRt = bgT.GetComponent<RectTransform>();
            bgRt.anchorMin = Vector2.zero;
            bgRt.anchorMax = Vector2.one;
            bgRt.offsetMin = Vector2.zero;
            bgRt.offsetMax = Vector2.zero;
            Image bgImg = bgT.GetComponent<Image>();
            bgImg.sprite = bubbleSprite;
            bgImg.type = Image.Type.Simple;
            bgImg.preserveAspect = false; // stretch to cover all reward items
            bgImg.raycastTarget = true;
            bgImg.color = Color.white;

            Transform containerT = root.transform.Find("RewardContainer");
            if (containerT == null)
            {
                var container = new GameObject("RewardContainer", typeof(RectTransform));
                container.transform.SetParent(root.transform, false);
                containerT = container.transform;
            }

            // Free manual positioning in Edit — remove layout drivers if present.
            HorizontalLayoutGroup hlg = containerT.GetComponent<HorizontalLayoutGroup>();
            if (hlg != null)
            {
                UnityEngine.Object.DestroyImmediate(hlg);
            }

            ContentSizeFitter fit = containerT.GetComponent<ContentSizeFitter>();
            if (fit != null)
            {
                UnityEngine.Object.DestroyImmediate(fit);
            }

            RectTransform cRt = containerT.GetComponent<RectTransform>();
            cRt.anchorMin = new Vector2(0.5f, 0.55f);
            cRt.anchorMax = new Vector2(0.5f, 0.55f);
            cRt.pivot = new Vector2(0.5f, 0.5f);
            cRt.anchoredPosition = new Vector2(0f, 8f);
            cRt.sizeDelta = new Vector2(160f, 88f);

            // Strip leftover runtime clones.
            for (int i = containerT.childCount - 1; i >= 0; i--)
            {
                Transform child = containerT.GetChild(i);
                if (child.name.Contains("(Clone)"))
                {
                    UnityEngine.Object.DestroyImmediate(child.gameObject);
                }
            }

            ObjectiveRewardItemView item1 = EnsureStaticRewardItem(containerT, "RewardItem_01", new Vector2(-36f, 0f));
            ObjectiveRewardItemView item2 = EnsureStaticRewardItem(containerT, "RewardItem_02", new Vector2(36f, 0f));
            ObjectiveRewardItemView item3 = EnsureStaticRewardItem(containerT, "RewardItem_03", new Vector2(0f, 0f));

            // Level-2-style edit preview per objective (sprites/amounts only — do not move RectTransforms).
            if (objectiveIndex == 1)
            {
                ApplyEditPreview(item1, chip, "90", true);
                ApplyEditPreview(item2, volt, "154", true);
                ApplyEditPreview(item3, null, "0", false);
            }
            else if (objectiveIndex == 2)
            {
                ApplyEditPreview(item1, mysterySprite, "300", true);
                ApplyEditPreview(item2, null, "0", false);
                ApplyEditPreview(item3, null, "0", false);
            }
            else
            {
                ApplyEditPreview(item1, evo, "1", true);
                ApplyEditPreview(item2, null, "0", false);
                ApplyEditPreview(item3, null, "0", false);
            }

            Transform claimT = root.transform.Find("ClaimButton");
            if (claimT == null)
            {
                var claim = new GameObject("ClaimButton", typeof(RectTransform), typeof(Image), typeof(Button));
                claim.transform.SetParent(root.transform, false);
                claimT = claim.transform;
                RectTransform crt = claim.GetComponent<RectTransform>();
                crt.anchorMin = new Vector2(0.5f, 0f);
                crt.anchorMax = new Vector2(0.5f, 0f);
                crt.pivot = new Vector2(0.5f, 0f);
                crt.anchoredPosition = new Vector2(0f, -32f);
                crt.sizeDelta = new Vector2(120f, 36f);
                claim.GetComponent<Image>().color = new Color(0.25f, 0.85f, 0.45f, 1f);
                TMP_Text label = EnsureTmp(claim.transform, "Label", "CLAIM", 22f, Vector2.zero, new Vector2(110f, 32f));
                label.alignment = TextAlignmentOptions.Center;
                label.fontStyle = FontStyles.Bold;
                label.raycastTarget = false;
            }

            claimT.gameObject.SetActive(false);

            ObjectiveRewardBubbleView bubbleView = root.GetComponent<ObjectiveRewardBubbleView>();
            if (bubbleView == null)
            {
                bubbleView = root.AddComponent<ObjectiveRewardBubbleView>();
            }

            var slots = new ObjectiveRewardItemView[] { item1, item2, item3 };
            SerializedObject bso = new SerializedObject(bubbleView);
            bso.FindProperty("_bubbleBackground").objectReferenceValue = bgImg;
            bso.FindProperty("_rewardContainer").objectReferenceValue = cRt;
            SerializedProperty slotsProp = bso.FindProperty("_itemSlots");
            if (slotsProp != null)
            {
                slotsProp.arraySize = slots.Length;
                for (int s = 0; s < slots.Length; s++)
                {
                    slotsProp.GetArrayElementAtIndex(s).objectReferenceValue = slots[s];
                }
            }

            // Clear legacy prefab field if still present.
            SerializedProperty prefabProp = bso.FindProperty("_rewardItemPrefab");
            if (prefabProp != null)
            {
                prefabProp.objectReferenceValue = null;
            }

            bso.FindProperty("_claimButton").objectReferenceValue = claimT.GetComponent<Button>();
            bso.FindProperty("_claimButtonRoot").objectReferenceValue = claimT.gameObject;
            bso.FindProperty("_itemCatalog").objectReferenceValue = catalog;
            bso.FindProperty("_mysteryIcon").objectReferenceValue = mysterySprite;
            bso.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(bubbleView);

            SerializedObject slotSo = new SerializedObject(slotView);
            slotSo.FindProperty("_rewardBubble").objectReferenceValue = bubbleView;
            slotSo.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(slotView);

            // Visible in Edit so designer can drag RectTransforms; Play Hide() closes until tap.
            root.SetActive(true);
        }

        private static ObjectiveRewardItemView EnsureStaticRewardItem(Transform container, string name, Vector2 anchoredPos)
        {
            Transform existing = container.Find(name);
            GameObject go;
            if (existing != null)
            {
                go = existing.gameObject;
            }
            else
            {
                go = new GameObject(
                    name,
                    typeof(RectTransform),
                    typeof(LayoutElement),
                    typeof(Image),
                    typeof(Button),
                    typeof(ObjectiveRewardItemView));
                go.transform.SetParent(container, false);
            }

            RectTransform rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            // Only seed default position/size for brand-new items — keep designer hand-tuned layout.
            if (existing == null)
            {
                rt.anchoredPosition = anchoredPos;
                rt.sizeDelta = new Vector2(93f, 122f);
            }

            LayoutElement le = go.GetComponent<LayoutElement>();
            le.ignoreLayout = true;
            le.preferredWidth = 93f;
            le.preferredHeight = 122f;

            Image hit = go.GetComponent<Image>();
            hit.color = new Color(1f, 1f, 1f, 0f);
            hit.raycastTarget = true;

            Transform iconT = go.transform.Find("Icon");
            if (iconT == null)
            {
                var icon = new GameObject("Icon", typeof(RectTransform), typeof(Image));
                icon.transform.SetParent(go.transform, false);
                iconT = icon.transform;
            }

            RectTransform irt = iconT.GetComponent<RectTransform>();
            irt.anchorMin = new Vector2(0.5f, 1f);
            irt.anchorMax = new Vector2(0.5f, 1f);
            irt.pivot = new Vector2(0.5f, 1f);
            if (existing == null)
            {
                irt.anchoredPosition = new Vector2(0f, -2f);
                irt.sizeDelta = new Vector2(81f, 81f);
            }

            Image iconImg = iconT.GetComponent<Image>();
            iconImg.preserveAspect = true;
            iconImg.raycastTarget = false;

            TMP_Text amount;
            if (existing == null)
            {
                amount = EnsureTmp(go.transform, "Amount", "0", 29f, new Vector2(0f, -36f), new Vector2(93f, 35f));
            }
            else
            {
                Transform amountT = go.transform.Find("Amount");
                amount = amountT != null
                    ? amountT.GetComponent<TMP_Text>()
                    : EnsureTmp(go.transform, "Amount", "0", 29f, new Vector2(0f, -36f), new Vector2(93f, 35f));
            }

            amount.alignment = TextAlignmentOptions.Center;
            amount.fontStyle = FontStyles.Bold;
            amount.color = Color.white;
            amount.raycastTarget = false;

            Button btn = go.GetComponent<Button>();
            btn.targetGraphic = hit;
            btn.transition = Selectable.Transition.None;

            ObjectiveRewardItemView view = go.GetComponent<ObjectiveRewardItemView>();
            if (view == null)
            {
                view = go.AddComponent<ObjectiveRewardItemView>();
            }

            SerializedObject so = new SerializedObject(view);
            so.FindProperty("_icon").objectReferenceValue = iconImg;
            so.FindProperty("_amountText").objectReferenceValue = amount;
            so.FindProperty("_button").objectReferenceValue = btn;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(view);
            return view;
        }

        private static void ApplyEditPreview(ObjectiveRewardItemView item, Sprite icon, string amount, bool active)
        {
            if (item == null)
            {
                return;
            }

            item.gameObject.SetActive(active);
            if (!active)
            {
                return;
            }

            Transform iconT = item.transform.Find("Icon");
            Image iconImg = iconT != null ? iconT.GetComponent<Image>() : null;
            if (iconImg != null)
            {
                iconImg.sprite = icon;
                iconImg.enabled = icon != null;
                iconImg.preserveAspect = true;
                iconImg.color = Color.white;
                EditorUtility.SetDirty(iconImg);
            }

            Transform amountT = item.transform.Find("Amount");
            TMP_Text amountText = amountT != null ? amountT.GetComponent<TMP_Text>() : null;
            if (amountText != null)
            {
                amountText.text = amount ?? string.Empty;
                EditorUtility.SetDirty(amountText);
            }
        }

        private static Button EnsureSharedDismissBlocker(Transform objectivesPanel)
        {
            Transform parent = objectivesPanel.parent != null ? objectivesPanel.parent : objectivesPanel;
            Transform existing = parent.Find("RewardBubbleDismiss");
            GameObject root;
            if (existing != null)
            {
                root = existing.gameObject;
            }
            else
            {
                root = new GameObject("RewardBubbleDismiss", typeof(RectTransform), typeof(Image), typeof(Button));
                root.transform.SetParent(parent, false);
                root.transform.SetAsFirstSibling();
            }

            RectTransform rt = root.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            Image img = root.GetComponent<Image>();
            img.color = new Color(0f, 0f, 0f, 0.01f);
            img.raycastTarget = true;

            Button btn = root.GetComponent<Button>();
            btn.transition = Selectable.Transition.None;
            root.SetActive(false);
            return btn;
        }

        private static ObjectiveRewardItemView EnsureBubbleItemPrefab(Transform objectivesPanel)
        {
            Transform templates = objectivesPanel.Find("RewardItemTemplates");
            if (templates == null)
            {
                var t = new GameObject("RewardItemTemplates", typeof(RectTransform));
                t.transform.SetParent(objectivesPanel, false);
                templates = t.transform;
                templates.gameObject.SetActive(false);
            }

            Transform item = templates.Find("ObjectiveRewardItem");
            if (item == null)
            {
                var go = new GameObject(
                    "ObjectiveRewardItem",
                    typeof(RectTransform),
                    typeof(LayoutElement),
                    typeof(Image),
                    typeof(Button),
                    typeof(ObjectiveRewardItemView));
                go.transform.SetParent(templates, false);
                RectTransform rt = go.GetComponent<RectTransform>();
                rt.sizeDelta = new Vector2(93f, 122f);
                LayoutElement le = go.GetComponent<LayoutElement>();
                le.preferredWidth = 93f;
                le.preferredHeight = 122f;

                Image hit = go.GetComponent<Image>();
                hit.color = new Color(1f, 1f, 1f, 0f);
                hit.raycastTarget = true;

                var icon = new GameObject("Icon", typeof(RectTransform), typeof(Image));
                icon.transform.SetParent(go.transform, false);
                RectTransform irt = icon.GetComponent<RectTransform>();
                irt.anchorMin = new Vector2(0.5f, 1f);
                irt.anchorMax = new Vector2(0.5f, 1f);
                irt.pivot = new Vector2(0.5f, 1f);
                irt.anchoredPosition = new Vector2(0f, -2f);
                irt.sizeDelta = new Vector2(81f, 81f);
                Image iconImg = icon.GetComponent<Image>();
                iconImg.preserveAspect = true;
                iconImg.raycastTarget = false;

                TMP_Text amount = EnsureTmp(go.transform, "Amount", "0", 29f, new Vector2(0f, -36f), new Vector2(93f, 35f));
                amount.alignment = TextAlignmentOptions.Center;
                amount.fontStyle = FontStyles.Bold;
                amount.color = Color.white;
                amount.raycastTarget = false;

                Button btn = go.GetComponent<Button>();
                btn.targetGraphic = hit;
                btn.transition = Selectable.Transition.None;

                SerializedObject so = new SerializedObject(go.GetComponent<ObjectiveRewardItemView>());
                so.FindProperty("_icon").objectReferenceValue = iconImg;
                so.FindProperty("_amountText").objectReferenceValue = amount;
                so.FindProperty("_button").objectReferenceValue = btn;
                so.ApplyModifiedPropertiesWithoutUndo();

                item = go.transform;
            }

            return item.GetComponent<ObjectiveRewardItemView>();
        }

        private static ObjectiveRewardOverlayView EnsureOverlay(UnityEngine.SceneManagement.Scene scene, MetaItemCatalog catalog)
        {
            Transform safe = FindNamed(scene, "SafeArea");
            if (safe == null)
            {
                Debug.LogError("[ObjectiveRewardSetup] SafeArea missing.");
                return null;
            }

            Transform existing = safe.Find("ObjectiveRewardOverlay");
            GameObject root;
            if (existing != null)
            {
                root = existing.gameObject;
            }
            else
            {
                root = new GameObject("ObjectiveRewardOverlay", typeof(RectTransform), typeof(ObjectiveRewardOverlayView));
                root.transform.SetParent(safe, false);
            }

            RectTransform rootRt = root.GetComponent<RectTransform>();
            rootRt.anchorMin = Vector2.zero;
            rootRt.anchorMax = Vector2.one;
            rootRt.offsetMin = Vector2.zero;
            rootRt.offsetMax = Vector2.zero;

            // Blocker
            Transform blockerT = root.transform.Find("Blocker");
            if (blockerT == null)
            {
                var blocker = new GameObject("Blocker", typeof(RectTransform), typeof(Image), typeof(Button), typeof(CanvasGroup));
                blocker.transform.SetParent(root.transform, false);
                RectTransform brt = blocker.GetComponent<RectTransform>();
                brt.anchorMin = Vector2.zero;
                brt.anchorMax = Vector2.one;
                brt.offsetMin = Vector2.zero;
                brt.offsetMax = Vector2.zero;
                Image bImg = blocker.GetComponent<Image>();
                bImg.color = new Color(0f, 0f, 0f, 0.82f);
                blockerT = blocker.transform;
            }

            // Content
            Transform contentT = root.transform.Find("Content");
            if (contentT == null)
            {
                var content = new GameObject("Content", typeof(RectTransform));
                content.transform.SetParent(root.transform, false);
                RectTransform crt = content.GetComponent<RectTransform>();
                crt.anchorMin = new Vector2(0.5f, 0.5f);
                crt.anchorMax = new Vector2(0.5f, 0.5f);
                crt.sizeDelta = new Vector2(900f, 700f);
                contentT = content.transform;
            }

            TMP_Text title = EnsureTmp(contentT, "Title", "YOUR REWARDS", 64f, new Vector2(0f, 260f), new Vector2(800f, 80f));
            title.alignment = TextAlignmentOptions.Center;
            title.fontStyle = FontStyles.Bold;

            Transform gridT = contentT.Find("RewardsGrid");
            if (gridT == null)
            {
                var grid = new GameObject("RewardsGrid", typeof(RectTransform), typeof(GridLayoutGroup));
                grid.transform.SetParent(contentT, false);
                RectTransform grt = grid.GetComponent<RectTransform>();
                grt.anchorMin = new Vector2(0.5f, 0.5f);
                grt.anchorMax = new Vector2(0.5f, 0.5f);
                grt.anchoredPosition = new Vector2(0f, 20f);
                grt.sizeDelta = new Vector2(820f, 420f);
                GridLayoutGroup glg = grid.GetComponent<GridLayoutGroup>();
                glg.cellSize = new Vector2(150f, 170f);
                glg.spacing = new Vector2(24f, 24f);
                glg.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
                glg.constraintCount = 4;
                glg.childAlignment = TextAnchor.MiddleCenter;
                gridT = grid.transform;
            }

            EnsureTmp(contentT, "TapToClose", "Tap to Close", 28f, new Vector2(0f, -280f), new Vector2(400f, 40f));

            // Item prefab (inactive child template)
            Transform prefabT = root.transform.Find("RewardItemPrefab");
            if (prefabT == null)
            {
                prefabT = CreateItemPrefab(root.transform).transform;
            }

            // Detail popup
            Transform detailT = root.transform.Find("RewardDetailPopup");
            if (detailT == null)
            {
                detailT = CreateDetailPopup(root.transform).transform;
            }

            ObjectiveRewardOverlayView overlay = root.GetComponent<ObjectiveRewardOverlayView>();
            SerializedObject so = new SerializedObject(overlay);
            so.FindProperty("_blockerGroup").objectReferenceValue = blockerT.GetComponent<CanvasGroup>();
            so.FindProperty("_blockerButton").objectReferenceValue = blockerT.GetComponent<Button>();
            so.FindProperty("_contentRoot").objectReferenceValue = contentT.GetComponent<RectTransform>();
            so.FindProperty("_titleText").objectReferenceValue = title;
            so.FindProperty("_rewardsGrid").objectReferenceValue = gridT;
            so.FindProperty("_itemPrefab").objectReferenceValue = prefabT.GetComponent<ObjectiveRewardItemView>();
            so.FindProperty("_detailRoot").objectReferenceValue = detailT.gameObject;
            so.FindProperty("_detailHeader").objectReferenceValue = detailT.Find("Panel/Header")?.GetComponent<Image>();
            so.FindProperty("_detailTitle").objectReferenceValue = detailT.Find("Panel/Header/Title")?.GetComponent<TMP_Text>();
            so.FindProperty("_detailIcon").objectReferenceValue = detailT.Find("Panel/Icon")?.GetComponent<Image>();
            so.FindProperty("_detailAmount").objectReferenceValue = detailT.Find("Panel/Amount")?.GetComponent<TMP_Text>();
            so.FindProperty("_detailDescription").objectReferenceValue = detailT.Find("Panel/Description")?.GetComponent<TMP_Text>();
            so.FindProperty("_detailCloseButton").objectReferenceValue = detailT.Find("Panel/Header/CloseButton")?.GetComponent<Button>();
            so.FindProperty("_itemCatalog").objectReferenceValue = catalog;
            so.FindProperty("_coinsIcon").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Game/Art/Sprite/MainMenu/Avatar/coinicon.png");
            so.FindProperty("_gemsIcon").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Game/Art/Sprite/MainMenu/Avatar/diamondicon.png");
            so.ApplyModifiedPropertiesWithoutUndo();

            root.SetActive(false);
            EditorUtility.SetDirty(root);
            return overlay;
        }

        private static GameObject CreateItemPrefab(Transform parent)
        {
            var go = new GameObject("RewardItemPrefab", typeof(RectTransform), typeof(Image), typeof(Button), typeof(ObjectiveRewardItemView));
            go.transform.SetParent(parent, false);
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(140f, 160f);
            Image frame = go.GetComponent<Image>();
            frame.color = new Color(0.15f, 0.45f, 0.75f, 0.9f);

            var iconGo = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            iconGo.transform.SetParent(go.transform, false);
            RectTransform irt = iconGo.GetComponent<RectTransform>();
            irt.anchorMin = new Vector2(0.5f, 0.55f);
            irt.anchorMax = new Vector2(0.5f, 0.55f);
            irt.sizeDelta = new Vector2(90f, 90f);

            TMP_Text amount = EnsureTmp(go.transform, "Amount", "0", 30f, new Vector2(0f, -58f), new Vector2(120f, 36f));
            amount.alignment = TextAlignmentOptions.Center;
            amount.fontStyle = FontStyles.Bold;

            SerializedObject so = new SerializedObject(go.GetComponent<ObjectiveRewardItemView>());
            so.FindProperty("_frame").objectReferenceValue = frame;
            so.FindProperty("_icon").objectReferenceValue = iconGo.GetComponent<Image>();
            so.FindProperty("_amountText").objectReferenceValue = amount;
            so.FindProperty("_button").objectReferenceValue = go.GetComponent<Button>();
            so.ApplyModifiedPropertiesWithoutUndo();
            go.SetActive(false);
            return go;
        }

        private static GameObject CreateDetailPopup(Transform parent)
        {
            var root = new GameObject("RewardDetailPopup", typeof(RectTransform));
            root.transform.SetParent(parent, false);
            RectTransform rootRt = root.GetComponent<RectTransform>();
            rootRt.anchorMin = Vector2.zero;
            rootRt.anchorMax = Vector2.one;
            rootRt.offsetMin = Vector2.zero;
            rootRt.offsetMax = Vector2.zero;

            var panel = new GameObject("Panel", typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(root.transform, false);
            RectTransform prt = panel.GetComponent<RectTransform>();
            prt.anchorMin = new Vector2(0.5f, 0.5f);
            prt.anchorMax = new Vector2(0.5f, 0.5f);
            prt.sizeDelta = new Vector2(520f, 480f);
            panel.GetComponent<Image>().color = new Color(0.08f, 0.18f, 0.32f, 0.98f);

            var header = new GameObject("Header", typeof(RectTransform), typeof(Image));
            header.transform.SetParent(panel.transform, false);
            RectTransform hrt = header.GetComponent<RectTransform>();
            hrt.anchorMin = new Vector2(0f, 1f);
            hrt.anchorMax = new Vector2(1f, 1f);
            hrt.pivot = new Vector2(0.5f, 1f);
            hrt.anchoredPosition = Vector2.zero;
            hrt.sizeDelta = new Vector2(0f, 70f);
            header.GetComponent<Image>().color = new Color(0.2f, 0.65f, 0.95f, 1f);

            TMP_Text title = EnsureTmp(header.transform, "Title", "Item", 32f, new Vector2(0f, -8f), new Vector2(400f, 50f));
            title.alignment = TextAlignmentOptions.Center;
            title.fontStyle = FontStyles.Bold;

            var close = new GameObject("CloseButton", typeof(RectTransform), typeof(Image), typeof(Button));
            close.transform.SetParent(header.transform, false);
            RectTransform crt = close.GetComponent<RectTransform>();
            crt.anchorMin = new Vector2(1f, 0.5f);
            crt.anchorMax = new Vector2(1f, 0.5f);
            crt.anchoredPosition = new Vector2(-28f, 0f);
            crt.sizeDelta = new Vector2(44f, 44f);
            close.GetComponent<Image>().color = new Color(0.9f, 0.25f, 0.25f, 1f);

            var icon = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            icon.transform.SetParent(panel.transform, false);
            RectTransform irt = icon.GetComponent<RectTransform>();
            irt.anchoredPosition = new Vector2(0f, 40f);
            irt.sizeDelta = new Vector2(140f, 140f);

            TMP_Text amount = EnsureTmp(panel.transform, "Amount", "0", 40f, new Vector2(0f, -60f), new Vector2(200f, 50f));
            amount.alignment = TextAlignmentOptions.Center;
            amount.fontStyle = FontStyles.Bold;

            TMP_Text desc = EnsureTmp(panel.transform, "Description", string.Empty, 24f, new Vector2(0f, -150f), new Vector2(440f, 120f));
            desc.alignment = TextAlignmentOptions.Center;

            root.SetActive(false);
            return root;
        }

        private static TMP_Text EnsureTmp(Transform parent, string name, string text, float size, Vector2 pos, Vector2 sizeDelta)
        {
            Transform existing = parent.Find(name);
            GameObject go = existing != null ? existing.gameObject : new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            if (existing == null)
            {
                go.transform.SetParent(parent, false);
            }

            RectTransform rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = sizeDelta;
            TMP_Text tmp = go.GetComponent<TMP_Text>();
            tmp.text = text;
            tmp.fontSize = size;
            tmp.color = Color.white;
            return tmp;
        }

        private static Transform FindNamed(UnityEngine.SceneManagement.Scene scene, string name)
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
    }
}
