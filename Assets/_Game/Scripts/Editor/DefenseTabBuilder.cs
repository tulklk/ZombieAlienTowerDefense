using AlienDefense.Towers;
using AlienDefense.UI;
using AlienDefense.UI.Defense;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace AlienDefense.EditorTools
{
    /// <summary>Builds the Defense tab inside MainMenu's DefensePanel (Tower Collection, upgrade card inventory,
    /// Get Cards) and its detail popup (Core Stats / Full Stats / About, level-up panel), and makes sure the data it
    /// runs on exists: the painted UI sprites, the DefenseMetaConfig asset (crit + UFO Base numbers) linked from
    /// TowerCatalog, and each tower's upgrade card / damage type label / card costs.
    ///
    /// Re-runnable, like InventoryScreenBuilder: it deletes its own DefenseRoot and DefenseDetailPopup and rebuilds
    /// them, so tuning the layout here and running the menu item again is the intended workflow. The old unlock
    /// list (DefenseScroll) is only parked inactive. Data that already has a value is never overwritten.
    /// Card and stat cells are inactive scene templates that UiViewPool instantiates from.</summary>
    public static class DefenseTabBuilder
    {
        private const string MainMenuPath = "Assets/_Game/Scenes/Menu/MainMenu.unity";
        private const string RootName = "DefenseRoot";
        private const string PopupName = "DefenseDetailPopup";
        private const string FontPath = "Assets/_Game/Font/Fredoka-Bold SDF.asset";
        private const string TitleMaterialPath = "Assets/_Game/Font/Materials/Fredoka_Title.mat";
        private const string ValueMaterialPath = "Assets/_Game/Font/Materials/Fredoka_HUD.mat";
        private const string ConfigPath = "Assets/_Game/Data/Towers/DefenseMetaConfig.asset";
        private const string CatalogPath = "Assets/_Game/Data/Towers/TowerCatalog.asset";
        private const string Art = "Assets/_Game/Art/Sprite/";

        private const float SidePadding = 36f;
        private const float PopupWidth = 1000f;
        private const float FrameHeight = 1490f;
        private const float TabBarHeight = 150f;

        private static readonly Color DarkText = Hex("#17305C");
        private static TMP_FontAsset _font;
        private static Material _titleMaterial;
        private static Material _valueMaterial;

        [MenuItem("AlienDefense/Setup/31. Build Defense Tab")]
        public static void BuildFromMenu()
        {
            Debug.Log(Build());
        }

        public static string Build()
        {
            if (EditorApplication.isPlaying)
            {
                return "[DefenseTabBuilder] Stop Play mode first.";
            }

            DefenseUiSpritePainter.Paint();
            _font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            _titleMaterial = AssetDatabase.LoadAssetAtPath<Material>(TitleMaterialPath);
            _valueMaterial = AssetDatabase.LoadAssetAtPath<Material>(ValueMaterialPath);
            string dataReport = EnsureData();

            Scene scene = SceneManager.GetActiveScene();
            if (scene.path != MainMenuPath)
            {
                scene = EditorSceneManager.OpenScene(MainMenuPath, OpenSceneMode.Single);
            }

            Transform defensePanel = FindNamed(scene, "DefensePanel");
            Transform safeArea = FindNamed(scene, "SafeArea");
            DefenseScreenPresenter presenter = Object.FindFirstObjectByType<DefenseScreenPresenter>(FindObjectsInactive.Include);
            if (defensePanel == null || safeArea == null || presenter == null)
            {
                return "[DefenseTabBuilder] DefensePanel, SafeArea or DefenseScreenPresenter not found in MainMenu.";
            }

            Transform legacy = defensePanel.Find("DefenseScroll");
            if (legacy != null)
            {
                legacy.gameObject.SetActive(false); // the old unlock list stays in the scene, parked
            }

            DestroyChild(defensePanel, RootName);
            DestroyChild(safeArea, PopupName);

            var view = defensePanel.GetComponent<DefenseScreenView>();
            if (view == null)
            {
                view = defensePanel.gameObject.AddComponent<DefenseScreenView>();
            }

            BuildPage(defensePanel, view);
            TowerPreviewRenderer previewRenderer = TowerPreviewWorldBuilder.Build(scene);
            TowerDetailPopupView popup = BuildPopup(safeArea, previewRenderer);

            var so = new SerializedObject(presenter);
            so.FindProperty("_view").objectReferenceValue = view;
            so.FindProperty("_popup").objectReferenceValue = popup;
            SerializedProperty icons = so.FindProperty("_statIcons");
            icons.FindPropertyRelative("Damage").objectReferenceValue = DefenseUiSpritePainter.Load("icon_stat_damage");
            icons.FindPropertyRelative("Interval").objectReferenceValue = DefenseUiSpritePainter.Load("icon_stat_interval");
            icons.FindPropertyRelative("Range").objectReferenceValue = DefenseUiSpritePainter.Load("icon_stat_range");
            icons.FindPropertyRelative("Target").objectReferenceValue = DefenseUiSpritePainter.Load("icon_stat_target");
            icons.FindPropertyRelative("Burst").objectReferenceValue = DefenseUiSpritePainter.Load("icon_stat_burst");
            icons.FindPropertyRelative("Area").objectReferenceValue = DefenseUiSpritePainter.Load("icon_stat_area");
            icons.FindPropertyRelative("Freeze").objectReferenceValue = LoadSprite(Art + "Play/HUD/icon_damage_frost.png");
            icons.FindPropertyRelative("Crit").objectReferenceValue = DefenseUiSpritePainter.Load("icon_stat_crit");
            icons.FindPropertyRelative("Health").objectReferenceValue = DefenseUiSpritePainter.Load("icon_stat_heart");
            icons.FindPropertyRelative("Bonus").objectReferenceValue = LoadSprite(Art + "Play/HUD/icon-arrow-up-green.png");
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            return "[DefenseTabBuilder] Defense tab built. " + dataReport;
        }

        // ------------------------------------------------------------------ data

        private static string EnsureData()
        {
            string report = "";
            var config = AssetDatabase.LoadAssetAtPath<DefenseMetaConfig>(ConfigPath);
            if (config == null)
            {
                config = ScriptableObject.CreateInstance<DefenseMetaConfig>();
                AssetDatabase.CreateAsset(config, ConfigPath);
                var so = new SerializedObject(config);
                so.FindProperty("_baseIcon").objectReferenceValue = LoadSprite(Art + "MainMenu/Tab/base.png");
                so.FindProperty("_baseTypeIcon").objectReferenceValue = DefenseUiSpritePainter.Load("icon_stat_heart");
                so.FindProperty("_basePreviewModel").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Models/Building/centralbuilding/centralbuilding.fbx");
                SerializedProperty costs = so.FindProperty("_baseLevelCosts");
                int[] coins = { 500, 1000, 2000, 4000, 8000 };
                int[] cards = { 20, 40, 80, 150, 250 };
                costs.arraySize = coins.Length;
                for (int i = 0; i < coins.Length; i++)
                {
                    costs.GetArrayElementAtIndex(i).FindPropertyRelative("Coins").intValue = coins[i];
                    costs.GetArrayElementAtIndex(i).FindPropertyRelative("Cards").intValue = cards[i];
                }

                so.ApplyModifiedPropertiesWithoutUndo();
                report += "Created " + ConfigPath + " (crit 10% / 150% +2% per level; Base +10% health per level, 5 level-ups). ";
            }

            // The Base's showcase model (the central building), only while unset.
            var baseModelSo = new SerializedObject(config);
            if (baseModelSo.FindProperty("_basePreviewModel").objectReferenceValue == null)
            {
                baseModelSo.FindProperty("_basePreviewModel").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Models/Building/centralbuilding/centralbuilding.fbx");
                baseModelSo.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(config);
            }

            // Permanent tower level curve (shared by every tower): damage +10% per level, 9 level-ups. Only filled while empty.
            var configSo = new SerializedObject(config);
            SerializedProperty towerCosts = configSo.FindProperty("_towerLevelCosts");
            if (towerCosts.arraySize == 0)
            {
                int[] towerCoins = { 200, 1000, 2000, 4000, 8000, 12000, 16000, 24000, 32000 };
                int[] towerCards = { 25, 50, 100, 150, 200, 250, 300, 400, 500 };
                towerCosts.arraySize = towerCoins.Length;
                for (int i = 0; i < towerCoins.Length; i++)
                {
                    towerCosts.GetArrayElementAtIndex(i).FindPropertyRelative("Coins").intValue = towerCoins[i];
                    towerCosts.GetArrayElementAtIndex(i).FindPropertyRelative("Cards").intValue = towerCards[i];
                }

                configSo.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(config);
                report += "Permanent tower levels: +10% shot damage each, 9 level-ups (200 coins / 25 cards up to 32000 / 500). ";
            }

            var catalog = AssetDatabase.LoadAssetAtPath<TowerCatalog>(CatalogPath);
            if (catalog != null)
            {
                report += EnsureComingSoonCards(config, catalog);
                var so = new SerializedObject(catalog);
                if (so.FindProperty("_defenseConfig").objectReferenceValue == null)
                {
                    so.FindProperty("_defenseConfig").objectReferenceValue = config;
                    so.ApplyModifiedPropertiesWithoutUndo();
                    EditorUtility.SetDirty(catalog);
                    report += "Linked the config from TowerCatalog. ";
                }

                // tower id -> (card item, damage type label). Only filled where still empty / default.
                for (int i = 0; i < catalog.Count; i++)
                {
                    TowerDefinition tower = catalog.GetTower(i);
                    if (tower == null)
                    {
                        continue;
                    }

                    string card = null, label = null;
                    switch (tower.Id)
                    {
                        case "tower_blaster": card = Meta.MetaItemIds.CardBlaster; label = "Lightning"; break;
                        case "tower_frost": card = Meta.MetaItemIds.CardFrost; label = "Ice"; break;
                        case "tower_mortar": card = Meta.MetaItemIds.CardMortar; label = "Fire"; break;
                    }

                    var towerSo = new SerializedObject(tower);
                    bool changed = false;
                    if (card != null && string.IsNullOrEmpty(towerSo.FindProperty("_upgradeCardItemId").stringValue))
                    {
                        towerSo.FindProperty("_upgradeCardItemId").stringValue = card;
                        towerSo.FindProperty("_damageTypeLabel").stringValue = label;
                        changed = true;
                    }

                    if (changed)
                    {
                        towerSo.ApplyModifiedPropertiesWithoutUndo();
                        EditorUtility.SetDirty(tower);
                        report += tower.Id + ": card " + card + ", " + label + ". ";
                    }
                }
            }

            return report;
        }

        // ------------------------------------------------------------------ page

        private static void BuildPage(Transform defensePanel, DefenseScreenView view)
        {
            RectTransform root = Rect(RootName, defensePanel);
            Stretch(root);

            Image background = Img("Background", root, LoadSprite(Art + "MainMenu/BG/mainmenubg.png"), false);
            Stretch(background.rectTransform);
            background.color = new Color(0.72f, 0.78f, 0.95f, 1f); // a touch darker so the cards stand out

            // Scroll: title, crit line, tower grid, upgrade cards, Get Cards - all scroll together.
            RectTransform scrollRect = Rect("Scroll", root);
            Stretch(scrollRect);
            var scroll = scrollRect.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Elastic;
            scroll.scrollSensitivity = 40f;
            RectTransform viewport = Rect("Viewport", scrollRect);
            Stretch(viewport);
            viewport.gameObject.AddComponent<RectMask2D>();
            var viewportImage = viewport.gameObject.AddComponent<Image>(); // drag surface
            viewportImage.color = new Color(0f, 0f, 0f, 0f);
            RectTransform content = Rect("Content", viewport);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.offsetMin = content.offsetMax = Vector2.zero;
            var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset((int)SidePadding, (int)SidePadding, 26, 60);
            layout.spacing = 22f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            var fitter = content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.viewport = viewport;
            scroll.content = content;

            // Title.
            RectTransform title = Row("Title", content, 104f);
            Image titleBar = Img("Bar", title, DefenseUiSpritePainter.Load("header_green"), true);
            Stretch(titleBar.rectTransform, 30f, 0f);
            Text("Label", title, DefenseStrings.Title, 58f, _titleMaterial, Color.white, true);

            // Towers Crit Damage.
            RectTransform crit = Row("CritPanel", content, 74f);
            Image critBar = Img("Bar", crit, DefenseUiSpritePainter.Load("bar_blue"), true);
            Stretch(critBar.rectTransform);
            RectTransform critRow = Rect("Row", crit);
            Stretch(critRow, 20f, 0f);
            var critLayout = critRow.gameObject.AddComponent<HorizontalLayoutGroup>();
            critLayout.childAlignment = TextAnchor.MiddleCenter;
            critLayout.spacing = 14f;
            critLayout.childControlWidth = true;
            critLayout.childControlHeight = true;
            critLayout.childForceExpandWidth = false;
            critLayout.childForceExpandHeight = false;
            TMP_Text critLabel = Text("Label", critRow, DefenseStrings.CritDamage, 44f, _titleMaterial, Color.white, false);
            Fit(critLabel);
            Image critIcon = Img("Icon", critRow, DefenseUiSpritePainter.Load("icon_stat_crit"), false);
            Size(critIcon.rectTransform, 56f, 56f);
            TMP_Text critValue = Text("Value", critRow, "150%", 46f, _titleMaterial, Hex("#FFE35A"), false);
            Fit(critValue);
            Button critInfo = Btn("InfoButton", crit, DefenseUiSpritePainter.Load("tab_green"));
            RectTransform infoRect = (RectTransform)critInfo.transform;
            infoRect.anchorMin = infoRect.anchorMax = new Vector2(1f, 0.5f);
            infoRect.pivot = new Vector2(1f, 0.5f);
            infoRect.anchoredPosition = new Vector2(-10f, 0f);
            infoRect.sizeDelta = new Vector2(54f, 54f);
            Text("Label", infoRect, "i", 40f, _titleMaterial, Color.white, true);

            // Tower grid, 4 columns.
            RectTransform grid = Rect("TowerGrid", content);
            float columnGap = 16f;
            float cardWidth = Mathf.Floor((1080f - SidePadding * 2f - columnGap * 3f) / 4f);
            var gridLayout = grid.gameObject.AddComponent<GridLayoutGroup>();
            gridLayout.cellSize = new Vector2(cardWidth, Mathf.Round(cardWidth * 1.3f));
            gridLayout.spacing = new Vector2(columnGap, 24f);
            gridLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            gridLayout.constraintCount = 4;
            gridLayout.childAlignment = TextAnchor.UpperLeft;
            gridLayout.padding = new RectOffset(0, 0, 12, 4);
            grid.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            // Upgrade Cards.
            RectTransform cardsHeader = Row("UpgradeCardsHeader", content, 74f);
            Image cardsBar = Img("Bar", cardsHeader, DefenseUiSpritePainter.Load("bar_blue"), true);
            Stretch(cardsBar.rectTransform);
            Text("Label", cardsHeader, DefenseStrings.UpgradeCards, 46f, _titleMaterial, Color.white, true);

            RectTransform cardGrid = Rect("UpgradeCardGrid", content);
            float cardGap = 14f;
            float upgradeCard = Mathf.Floor((1080f - SidePadding * 2f - cardGap * 5f) / 6f);
            var cardLayout = cardGrid.gameObject.AddComponent<GridLayoutGroup>();
            cardLayout.cellSize = new Vector2(upgradeCard, upgradeCard);
            cardLayout.spacing = new Vector2(cardGap, cardGap);
            cardLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            cardLayout.constraintCount = 6;
            cardGrid.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            RectTransform noCards = Row("NoCardsLabel", content, 60f);
            Text("Label", noCards, "No upgrade cards yet - win levels or visit the shop.", 34f, null, new Color(1f, 1f, 1f, 0.75f), true);

            // Get Cards.
            RectTransform getCardsRow = Row("GetCards", content, 150f);
            Button getCards = Btn("Button", getCardsRow, DefenseUiSpritePainter.Load("btn_green"));
            RectTransform getRect = (RectTransform)getCards.transform;
            getRect.anchorMin = getRect.anchorMax = new Vector2(0.5f, 0.5f);
            getRect.sizeDelta = new Vector2(520f, 132f);
            Image chest = Img("Chest", getRect, LoadSprite(Art + "MainMenu/Reward/chest.png"), false);
            chest.rectTransform.anchorMin = chest.rectTransform.anchorMax = new Vector2(0f, 0.5f);
            chest.rectTransform.anchoredPosition = new Vector2(92f, 6f);
            chest.rectTransform.sizeDelta = new Vector2(124f, 124f);
            TMP_Text getLabel = Text("Label", getRect, DefenseStrings.GetCards, 56f, _titleMaterial, Color.white, true);
            getLabel.rectTransform.offsetMin = new Vector2(150f, 0f);

            // Crit info tooltip (one reusable bubble; tap anywhere to close).
            RectTransform tooltipRoot = Rect("CritTooltip", root);
            Stretch(tooltipRoot);
            Button tooltipDismiss = tooltipRoot.gameObject.AddComponent<Button>();
            var dismissImage = tooltipRoot.gameObject.AddComponent<Image>();
            dismissImage.color = new Color(0f, 0f, 0f, 0.35f);
            tooltipDismiss.transition = Selectable.Transition.None;
            Image bubble = Img("Bubble", tooltipRoot, DefenseUiSpritePainter.Load("tooltip"), true);
            bubble.rectTransform.anchorMin = bubble.rectTransform.anchorMax = new Vector2(0.5f, 1f);
            bubble.rectTransform.pivot = new Vector2(0.5f, 1f);
            bubble.rectTransform.anchoredPosition = new Vector2(0f, -250f);
            bubble.rectTransform.sizeDelta = new Vector2(900f, 250f);
            TMP_Text tooltipText = Text("Text", bubble.rectTransform, "", 34f, null, Color.white, true);
            tooltipText.rectTransform.offsetMin = new Vector2(30f, 20f);
            tooltipText.rectTransform.offsetMax = new Vector2(-30f, -20f);
            tooltipText.enableAutoSizing = false;
            tooltipText.textWrappingMode = TextWrappingModes.Normal;
            tooltipRoot.gameObject.SetActive(false);

            // Templates.
            RectTransform templates = Rect("Templates", root);
            templates.sizeDelta = Vector2.zero;
            TowerCollectionCardView towerTemplate = BuildTowerCardTemplate(templates, gridLayout.cellSize);
            UpgradeCardView upgradeTemplate = BuildUpgradeCardTemplate(templates, upgradeCard);

            var so = new SerializedObject(view);
            so.FindProperty("_critPanel").objectReferenceValue = crit.gameObject;
            so.FindProperty("_critValue").objectReferenceValue = critValue;
            so.FindProperty("_critInfoButton").objectReferenceValue = critInfo;
            so.FindProperty("_critTooltip").objectReferenceValue = tooltipRoot.gameObject;
            so.FindProperty("_critTooltipText").objectReferenceValue = tooltipText;
            so.FindProperty("_critTooltipDismiss").objectReferenceValue = tooltipDismiss;
            so.FindProperty("_towerGrid").objectReferenceValue = grid;
            so.FindProperty("_towerCardTemplate").objectReferenceValue = towerTemplate;
            so.FindProperty("_upgradeCardGrid").objectReferenceValue = cardGrid;
            so.FindProperty("_upgradeCardTemplate").objectReferenceValue = upgradeTemplate;
            so.FindProperty("_noCardsLabel").objectReferenceValue = noCards.gameObject;
            so.FindProperty("_getCardsButton").objectReferenceValue = getCards;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>The announced (locked, look-only) cards after the real towers: 6 event towers and 4 Engineer's House
        /// towers, as in the reference. The game has no art for towers that do not exist yet, so they borrow the three
        /// real towers' icons and type icons in turn. Only filled while the list is empty.</summary>
        private static string EnsureComingSoonCards(DefenseMetaConfig config, TowerCatalog catalog)
        {
            var so = new SerializedObject(config);
            SerializedProperty cards = so.FindProperty("_comingSoonCards");
            if (cards.arraySize > 0 || catalog.Count == 0)
            {
                return "";
            }

            const int eventCards = 6, houseCards = 4;
            cards.arraySize = eventCards + houseCards;
            for (int i = 0; i < cards.arraySize; i++)
            {
                bool isEvent = i < eventCards;
                TowerDefinition picture = catalog.GetTower((i * 2 + 1) % catalog.Count);
                TowerDefinition type = catalog.GetTower(i % catalog.Count);
                SerializedProperty card = cards.GetArrayElementAtIndex(i);
                card.FindPropertyRelative("Label").stringValue = isEvent ? "Event\nactivity" : "Unlock in\nEngineer's\nHouse";
                card.FindPropertyRelative("Icon").objectReferenceValue = picture != null ? picture.StatsIcon : null;
                card.FindPropertyRelative("TypeIcon").objectReferenceValue = type != null ? type.StatsTypeIcon : null;
                card.FindPropertyRelative("IsEvent").boolValue = isEvent;
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(config);
            return "Added 6 event + 4 Engineer's House announced cards (display only). ";
        }

        private static TowerCollectionCardView BuildTowerCardTemplate(Transform parent, Vector2 size)
        {
            RectTransform card = Rect("TowerCardTemplate", parent);
            card.sizeDelta = size;
            Image background = card.gameObject.AddComponent<Image>();
            background.sprite = DefenseUiSpritePainter.Load("card_blue");
            background.type = Image.Type.Sliced;
            var button = card.gameObject.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            card.gameObject.AddComponent<UiPressScale>();

            Image icon = Img("TowerIcon", card, null, false);
            icon.rectTransform.anchorMin = new Vector2(0f, 0f);
            icon.rectTransform.anchorMax = new Vector2(1f, 1f);
            icon.rectTransform.offsetMin = new Vector2(14f, 22f);
            icon.rectTransform.offsetMax = new Vector2(-14f, -58f);
            icon.preserveAspect = true;

            Image strip = Img("LevelStrip", card, DefenseUiSpritePainter.Load("strip_green"), true);
            strip.rectTransform.anchorMin = new Vector2(0f, 1f);
            strip.rectTransform.anchorMax = new Vector2(1f, 1f);
            strip.rectTransform.pivot = new Vector2(0.5f, 1f);
            strip.rectTransform.anchoredPosition = new Vector2(0f, -12f);
            strip.rectTransform.sizeDelta = new Vector2(-28f, 46f);
            Image arrow = Img("UpgradeArrow", strip.rectTransform, LoadSprite(Art + "Play/HUD/icon-arrow-up-green.png"), false);
            arrow.rectTransform.anchorMin = arrow.rectTransform.anchorMax = new Vector2(0f, 0.5f);
            arrow.rectTransform.anchoredPosition = new Vector2(16f, 2f);
            arrow.rectTransform.sizeDelta = new Vector2(50f, 56f);
            arrow.preserveAspect = true;
            TMP_Text level = Text("LevelText", strip.rectTransform, "Lvl. 1", 32f, _valueMaterial, Color.white, true);
            level.rectTransform.offsetMin = new Vector2(24f, 0f);

            Image badge = Img("TypeBadge", card, DefenseUiSpritePainter.Load("badge_round"), false);
            badge.rectTransform.anchorMin = badge.rectTransform.anchorMax = new Vector2(0f, 0f);
            badge.rectTransform.anchoredPosition = new Vector2(22f, 18f);
            badge.rectTransform.sizeDelta = new Vector2(74f, 74f);
            Image typeIcon = Img("Icon", badge.rectTransform, null, false);
            Stretch(typeIcon.rectTransform, 13f, 13f);
            typeIcon.preserveAspect = true;

            Image padlock = Img("LockIcon", card, LoadSprite(Art + "MainMenu/Level/LockIcon.png"), false);
            padlock.rectTransform.anchorMin = padlock.rectTransform.anchorMax = new Vector2(1f, 1f);
            padlock.rectTransform.anchoredPosition = new Vector2(-22f, -30f);
            padlock.rectTransform.sizeDelta = new Vector2(68f, 76f);
            padlock.preserveAspect = true;

            TMP_Text lockLabel = Text("LockLabel", card, "Unlock\n200", 34f, _valueMaterial, Color.white, false);
            lockLabel.rectTransform.anchorMin = new Vector2(0f, 1f);
            lockLabel.rectTransform.anchorMax = new Vector2(1f, 1f);
            lockLabel.rectTransform.pivot = new Vector2(0.5f, 1f);
            lockLabel.rectTransform.anchoredPosition = new Vector2(-20f, -18f);
            lockLabel.rectTransform.sizeDelta = new Vector2(-70f, 112f);
            lockLabel.enableAutoSizing = true; // "Unlock in Engineer's House" is three lines
            lockLabel.fontSizeMin = 20f;
            lockLabel.fontSizeMax = 34f;

            var view = card.gameObject.AddComponent<TowerCollectionCardView>();
            var so = new SerializedObject(view);
            so.FindProperty("_button").objectReferenceValue = button;
            so.FindProperty("_background").objectReferenceValue = background;
            so.FindProperty("_towerIcon").objectReferenceValue = icon;
            so.FindProperty("_levelStrip").objectReferenceValue = strip.gameObject;
            so.FindProperty("_levelText").objectReferenceValue = level;
            so.FindProperty("_upgradeArrow").objectReferenceValue = arrow.gameObject;
            so.FindProperty("_typeIcon").objectReferenceValue = typeIcon;
            so.FindProperty("_lockIcon").objectReferenceValue = padlock.gameObject;
            so.FindProperty("_lockLabel").objectReferenceValue = lockLabel;
            so.FindProperty("_normalBackground").objectReferenceValue = DefenseUiSpritePainter.Load("card_blue");
            so.FindProperty("_specialBackground").objectReferenceValue = DefenseUiSpritePainter.Load("card_purple");
            so.FindProperty("_lockedBackground").objectReferenceValue = DefenseUiSpritePainter.Load("card_locked");
            so.FindProperty("_eventBackground").objectReferenceValue = DefenseUiSpritePainter.Load("card_pink");
            so.ApplyModifiedPropertiesWithoutUndo();
            card.gameObject.SetActive(false);
            return view;
        }

        private static UpgradeCardView BuildUpgradeCardTemplate(Transform parent, float size)
        {
            RectTransform card = Rect("UpgradeCardTemplate", parent);
            card.sizeDelta = new Vector2(size, size);
            Image icon = card.gameObject.AddComponent<Image>(); // the card art carries its own frame
            icon.preserveAspect = true;
            icon.raycastTarget = false;
            TMP_Text quantity = Text("Quantity", card, "0", 36f, _valueMaterial, Color.white, false);
            quantity.rectTransform.anchorMin = new Vector2(0f, 0f);
            quantity.rectTransform.anchorMax = new Vector2(1f, 0f);
            quantity.rectTransform.pivot = new Vector2(0.5f, 0f);
            quantity.rectTransform.anchoredPosition = new Vector2(0f, 2f);
            quantity.rectTransform.sizeDelta = new Vector2(-16f, 46f);
            quantity.alignment = TextAlignmentOptions.Right;

            var view = card.gameObject.AddComponent<UpgradeCardView>();
            var so = new SerializedObject(view);
            so.FindProperty("_icon").objectReferenceValue = icon;
            so.FindProperty("_quantity").objectReferenceValue = quantity;
            so.ApplyModifiedPropertiesWithoutUndo();
            card.gameObject.SetActive(false);
            return view;
        }

        // ------------------------------------------------------------------ popup

        private static TowerDetailPopupView BuildPopup(Transform safeArea, TowerPreviewRenderer previewRenderer)
        {
            RectTransform root = Rect(PopupName, safeArea);
            Stretch(root);
            root.SetAsLastSibling();
            var view = root.gameObject.AddComponent<TowerDetailPopupView>();

            // Dark backdrop over the whole screen, blocks everything behind; tapping it closes.
            Image backdrop = Img("Backdrop", root, null, false);
            Stretch(backdrop.rectTransform);
            backdrop.color = new Color(0.02f, 0.05f, 0.12f, 0.72f);
            backdrop.raycastTarget = true;
            backdrop.gameObject.AddComponent<FullScreenBackdrop>();
            var backdropGroup = backdrop.gameObject.AddComponent<CanvasGroup>();
            var backdropButton = backdrop.gameObject.AddComponent<Button>();
            backdropButton.transition = Selectable.Transition.None;

            RectTransform panel = Rect("Panel", root);
            panel.anchorMin = panel.anchorMax = new Vector2(0.5f, 0.5f);
            panel.sizeDelta = new Vector2(PopupWidth, FrameHeight + TabBarHeight + 16f);
            panel.anchoredPosition = new Vector2(0f, -20f);
            var panelGroup = panel.gameObject.AddComponent<CanvasGroup>();

            // Frame (blocks taps so they do not fall through to the backdrop and close the popup).
            Image frame = Img("Frame", panel, DefenseUiSpritePainter.Load("popup_frame"), true);
            frame.raycastTarget = true;
            frame.rectTransform.anchorMin = new Vector2(0f, 1f);
            frame.rectTransform.anchorMax = new Vector2(1f, 1f);
            frame.rectTransform.pivot = new Vector2(0.5f, 1f);
            frame.rectTransform.anchoredPosition = Vector2.zero;
            frame.rectTransform.sizeDelta = new Vector2(0f, FrameHeight);

            RectTransform content = Rect("Content", frame.rectTransform);
            Stretch(content);
            content.offsetMin = new Vector2(30f, 30f);
            content.offsetMax = new Vector2(-30f, -92f);
            var contentGroup = content.gameObject.AddComponent<CanvasGroup>();

            GameObject corePage = BuildCorePage(content, view, out SerializedCore core);
            GameObject fullPage = BuildFullPage(content, out TMP_Text fullLevel, out Transform fullGrid, out TowerStatCellView fullCell);
            GameObject aboutPage = BuildAboutPage(content, out TMP_Text aboutText);

            // Title bar, overlapping the frame's top edge.
            Image titleBar = Img("TitleBar", panel, DefenseUiSpritePainter.Load("header_green"), true);
            titleBar.rectTransform.anchorMin = titleBar.rectTransform.anchorMax = new Vector2(0.5f, 1f);
            titleBar.rectTransform.pivot = new Vector2(0.5f, 1f);
            titleBar.rectTransform.anchoredPosition = new Vector2(0f, 38f);
            titleBar.rectTransform.sizeDelta = new Vector2(640f, 108f);
            TMP_Text title = Text("Title", titleBar.rectTransform, "Tower", 56f, _titleMaterial, Color.white, true);

            Button close = Btn("CloseButton", panel, LoadSprite(Art + "Play/PausePanel/closebtn.png"));
            RectTransform closeRect = (RectTransform)close.transform;
            closeRect.anchorMin = closeRect.anchorMax = new Vector2(1f, 1f);
            closeRect.anchoredPosition = new Vector2(-24f, 6f);
            closeRect.sizeDelta = new Vector2(112f, 112f);
            ((Image)close.targetGraphic).type = Image.Type.Simple;

            Button previous = ArrowButton("PreviousButton", panel, true);
            Button next = ArrowButton("NextButton", panel, false);

            // Tab bar under the frame.
            Image tabBar = Img("TabBar", panel, DefenseUiSpritePainter.Load("popup_frame"), true);
            tabBar.raycastTarget = true;
            tabBar.rectTransform.anchorMin = new Vector2(0f, 0f);
            tabBar.rectTransform.anchorMax = new Vector2(1f, 0f);
            tabBar.rectTransform.pivot = new Vector2(0.5f, 0f);
            tabBar.rectTransform.anchoredPosition = Vector2.zero;
            tabBar.rectTransform.sizeDelta = new Vector2(0f, TabBarHeight);
            RectTransform tabRow = Rect("Tabs", tabBar.rectTransform);
            Stretch(tabRow, 18f, 18f);
            var tabLayout = tabRow.gameObject.AddComponent<HorizontalLayoutGroup>();
            tabLayout.spacing = 10f;
            tabLayout.childControlWidth = tabLayout.childControlHeight = true;
            tabLayout.childForceExpandWidth = tabLayout.childForceExpandHeight = true;
            string[] tabNames = { DefenseStrings.CoreStats, DefenseStrings.FullStats, DefenseStrings.About };
            var tabButtons = new Button[3];
            var tabImages = new Image[3];
            for (int i = 0; i < 3; i++)
            {
                tabButtons[i] = Btn("Tab_" + i, tabRow, DefenseUiSpritePainter.Load(i == 0 ? "tab_green" : "tab_blue"));
                tabImages[i] = (Image)tabButtons[i].targetGraphic;
                Text("Label", tabButtons[i].transform, tabNames[i], 46f, _titleMaterial, Color.white, true);
            }

            // Tooltip (one reusable bubble) and toast.
            RectTransform tooltip = Rect("Tooltip", panel);
            Stretch(tooltip);
            var tooltipDismiss = tooltip.gameObject.AddComponent<Button>();
            tooltipDismiss.transition = Selectable.Transition.None;
            tooltip.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.3f);
            Image bubble = Img("Bubble", tooltip, DefenseUiSpritePainter.Load("tooltip"), true);
            bubble.rectTransform.anchorMin = bubble.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            bubble.rectTransform.sizeDelta = new Vector2(820f, 260f);
            bubble.rectTransform.anchoredPosition = new Vector2(0f, 120f);
            TMP_Text tooltipText = Text("Text", bubble.rectTransform, "", 36f, null, Color.white, true);
            tooltipText.rectTransform.offsetMin = new Vector2(30f, 20f);
            tooltipText.rectTransform.offsetMax = new Vector2(-30f, -20f);
            tooltipText.enableAutoSizing = false;
            tooltipText.textWrappingMode = TextWrappingModes.Normal;
            tooltip.gameObject.SetActive(false);

            Image toast = Img("Toast", panel, DefenseUiSpritePainter.Load("tooltip"), true);
            toast.rectTransform.anchorMin = toast.rectTransform.anchorMax = new Vector2(0.5f, 0f);
            toast.rectTransform.anchoredPosition = new Vector2(0f, TabBarHeight + 420f);
            toast.rectTransform.sizeDelta = new Vector2(620f, 92f);
            var toastGroup = toast.gameObject.AddComponent<CanvasGroup>();
            toastGroup.alpha = 0f;
            toastGroup.blocksRaycasts = false;
            TMP_Text toastText = Text("Text", toast.rectTransform, "", 40f, _titleMaterial, Hex("#FFD0C8"), true);

            // Force gain banner: gold glow, "Force", the total counting up and the gain in green. Shown briefly over the
            // top of the showcase after a level-up (a soft dark shade keeps it readable there); never blocks taps.
            RectTransform force = Rect("ForceGain", panel);
            force.anchorMin = force.anchorMax = new Vector2(0.5f, 1f);
            force.anchoredPosition = new Vector2(0f, -235f);
            force.sizeDelta = new Vector2(820f, 260f);
            var forceGroup = force.gameObject.AddComponent<CanvasGroup>();
            forceGroup.alpha = 0f;
            forceGroup.blocksRaycasts = false;
            forceGroup.interactable = false;
            Image forceShade = Img("Shade", force, LoadSprite("Assets/_Game/Art/Textures/UI/T_VictoryGlow.png"), false);
            Stretch(forceShade.rectTransform, -40f, 0f);
            forceShade.color = new Color(0.02f, 0.06f, 0.16f, 0.7f);
            Image forceGlow = Img("Glow", force, LoadSprite("Assets/_Game/Art/Textures/UI/T_VictoryGlow.png"), false);
            Stretch(forceGlow.rectTransform, -60f, -10f);
            forceGlow.color = new Color(1f, 0.72f, 0.18f, 0.6f);
            forceGlow.material = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Game/Materials/UI/MAT_UIAdditive.mat");
            RectTransform forceContent = Rect("Content", force);
            Stretch(forceContent);
            TMP_Text forceTitle = Text("Title", forceContent, "Force", 72f, _titleMaterial, Color.white, false);
            TopBox(forceTitle.rectTransform, 26f, 96f);
            RectTransform forceRow = Rect("Row", forceContent);
            TopBox(forceRow, 118f, 100f);
            var forceLayout = forceRow.gameObject.AddComponent<HorizontalLayoutGroup>();
            forceLayout.childAlignment = TextAnchor.MiddleCenter;
            forceLayout.spacing = 16f;
            forceLayout.childControlWidth = forceLayout.childControlHeight = true;
            forceLayout.childForceExpandWidth = forceLayout.childForceExpandHeight = false;
            TMP_Text forceValue = Text("Value", forceRow, "0", 84f, _titleMaterial, Hex("#FFD23C"), false);
            Fit(forceValue);
            RectTransform deltaGroup = Rect("Delta", forceRow);
            var deltaLayout = deltaGroup.gameObject.AddComponent<HorizontalLayoutGroup>();
            deltaLayout.childAlignment = TextAnchor.MiddleCenter;
            deltaLayout.spacing = 6f;
            deltaLayout.childControlWidth = deltaLayout.childControlHeight = true;
            deltaLayout.childForceExpandWidth = deltaLayout.childForceExpandHeight = false;
            Image deltaArrow = Img("Arrow", deltaGroup, LoadSprite(Art + "Play/HUD/icon-arrow-up-green.png"), false);
            Size(deltaArrow.rectTransform, 56f, 66f);
            deltaArrow.preserveAspect = true;
            TMP_Text forceDelta = Text("Text", deltaGroup, "0", 68f, _titleMaterial, Hex("#5CFF4A"), false);
            Fit(forceDelta);
            var forceSparkles = new Object[4];
            Vector2[] sparkleAt = { new Vector2(-250f, 40f), new Vector2(240f, 60f), new Vector2(-170f, -70f), new Vector2(300f, -40f) };
            for (int i = 0; i < 4; i++)
            {
                Image sparkle = Img("Sparkle_" + i, force, LoadSprite("Assets/_Game/Art/Textures/UI/T_ChestAuraSparkle.png"), false);
                sparkle.rectTransform.anchoredPosition = sparkleAt[i];
                sparkle.rectTransform.sizeDelta = new Vector2(46f, 46f);
                sparkle.color = new Color(1f, 0.95f, 0.6f, 1f);
                sparkle.material = forceGlow.material;
                forceSparkles[i] = sparkle.rectTransform;
            }

            var so = new SerializedObject(view);
            so.FindProperty("_backdrop").objectReferenceValue = backdropGroup;
            so.FindProperty("_backdropButton").objectReferenceValue = backdropButton;
            so.FindProperty("_panel").objectReferenceValue = panel;
            so.FindProperty("_panelGroup").objectReferenceValue = panelGroup;
            so.FindProperty("_content").objectReferenceValue = content;
            so.FindProperty("_contentGroup").objectReferenceValue = contentGroup;
            so.FindProperty("_title").objectReferenceValue = title;
            so.FindProperty("_closeButton").objectReferenceValue = close;
            so.FindProperty("_previousButton").objectReferenceValue = previous;
            so.FindProperty("_nextButton").objectReferenceValue = next;
            SetArray(so.FindProperty("_tabButtons"), tabButtons);
            SetArray(so.FindProperty("_tabImages"), tabImages);
            so.FindProperty("_tabSelected").objectReferenceValue = DefenseUiSpritePainter.Load("tab_green");
            so.FindProperty("_tabNormal").objectReferenceValue = DefenseUiSpritePainter.Load("tab_blue");
            SetArray(so.FindProperty("_pages"), new Object[] { corePage, fullPage, aboutPage });
            so.FindProperty("_preview").objectReferenceValue = core.Preview;
            so.FindProperty("_previewFrame").objectReferenceValue = core.PreviewFrame;
            so.FindProperty("_previewFrameNormal").objectReferenceValue = DefenseUiSpritePainter.Load("card_blue");
            so.FindProperty("_previewFrameSpecial").objectReferenceValue = DefenseUiSpritePainter.Load("card_purple");
            so.FindProperty("_previewTypeIcon").objectReferenceValue = core.TypeIcon;
            so.FindProperty("_preview3D").objectReferenceValue = core.Preview3D;
            so.FindProperty("_previewRenderer").objectReferenceValue = previewRenderer;
            so.FindProperty("_force").objectReferenceValue = forceGroup;
            so.FindProperty("_forceContent").objectReferenceValue = forceContent;
            so.FindProperty("_forceValue").objectReferenceValue = forceValue;
            so.FindProperty("_forceDelta").objectReferenceValue = forceDelta;
            SetArray(so.FindProperty("_forceSparkles"), forceSparkles);
            so.FindProperty("_coreLevel").objectReferenceValue = core.Level;
            so.FindProperty("_coreCellRow").objectReferenceValue = core.CellRow;
            so.FindProperty("_coreCellTemplate").objectReferenceValue = core.CellTemplate;
            so.FindProperty("_costRow").objectReferenceValue = core.CostRow;
            so.FindProperty("_coinText").objectReferenceValue = core.CoinText;
            so.FindProperty("_cardGroup").objectReferenceValue = core.CardGroup;
            so.FindProperty("_cardIcon").objectReferenceValue = core.CardIcon;
            so.FindProperty("_cardText").objectReferenceValue = core.CardText;
            so.FindProperty("_levelUpButton").objectReferenceValue = core.LevelUp;
            so.FindProperty("_levelUpGroup").objectReferenceValue = core.LevelUpGroup;
            so.FindProperty("_levelUpLabel").objectReferenceValue = core.LevelUpLabel;
            so.FindProperty("_levelUpArrow").objectReferenceValue = core.LevelUpArrow;
            so.FindProperty("_maxLabel").objectReferenceValue = core.MaxLabel;
            so.FindProperty("_bonusText").objectReferenceValue = core.Bonus;
            so.FindProperty("_fullLevel").objectReferenceValue = fullLevel;
            so.FindProperty("_fullGrid").objectReferenceValue = fullGrid;
            so.FindProperty("_fullCellTemplate").objectReferenceValue = fullCell;
            so.FindProperty("_aboutText").objectReferenceValue = aboutText;
            so.FindProperty("_tooltip").objectReferenceValue = tooltip.gameObject;
            so.FindProperty("_tooltipText").objectReferenceValue = tooltipText;
            so.FindProperty("_tooltipDismiss").objectReferenceValue = tooltipDismiss;
            so.FindProperty("_toast").objectReferenceValue = toastGroup;
            so.FindProperty("_toastText").objectReferenceValue = toastText;
            so.ApplyModifiedPropertiesWithoutUndo();

            root.gameObject.SetActive(false);
            return view;
        }

        private struct SerializedCore
        {
            public Image Preview, PreviewFrame, TypeIcon, CardIcon;
            public RawImage Preview3D;
            public TMP_Text Level, CoinText, CardText, LevelUpLabel, Bonus;
            public Transform CellRow;
            public TowerStatCellView CellTemplate;
            public GameObject CostRow, CardGroup, LevelUpArrow, MaxLabel;
            public Button LevelUp;
            public CanvasGroup LevelUpGroup;
        }

        private static GameObject BuildCorePage(RectTransform content, TowerDetailPopupView view, out SerializedCore core)
        {
            core = new SerializedCore();
            RectTransform page = Rect("CorePage", content);
            Stretch(page);

            // Hero preview.
            Image previewFrame = Img("PreviewFrame", page, DefenseUiSpritePainter.Load("card_blue"), true);
            TopBox(previewFrame.rectTransform, 0f, 540f);
            Image glow = Img("Glow", previewFrame.rectTransform, LoadSprite("Assets/_Game/Art/Textures/UI/T_VictoryGlow.png"), false);
            Stretch(glow.rectTransform, 40f, 10f);
            glow.color = new Color(1f, 1f, 1f, 0.45f);
            // The 3D showcase picture, clipped to the frame's rounded inside. The 2D icon below stays as the fallback
            // for an entry that has no model.
            Image previewMask = Img("PreviewMask", previewFrame.rectTransform, DefenseUiSpritePainter.Load("popup_inner"), true);
            Stretch(previewMask.rectTransform, 9f, 9f);
            var mask = previewMask.gameObject.AddComponent<Mask>();
            mask.showMaskGraphic = false;
            RectTransform preview3DRect = Rect("Preview3D", previewMask.rectTransform);
            Stretch(preview3DRect);
            var preview3D = preview3DRect.gameObject.AddComponent<RawImage>();
            preview3D.raycastTarget = false;
            preview3DRect.gameObject.SetActive(false);

            Image preview = Img("Preview", previewFrame.rectTransform, null, false);
            Stretch(preview.rectTransform, 40f, 34f);
            preview.preserveAspect = true;
            Image badge = Img("TypeBadge", previewFrame.rectTransform, DefenseUiSpritePainter.Load("badge_round"), false);
            badge.rectTransform.anchorMin = badge.rectTransform.anchorMax = new Vector2(1f, 0f);
            badge.rectTransform.anchoredPosition = new Vector2(-66f, 70f);
            badge.rectTransform.sizeDelta = new Vector2(88f, 88f);
            Image typeIcon = Img("Icon", badge.rectTransform, null, false);
            Stretch(typeIcon.rectTransform, 15f, 15f);
            typeIcon.preserveAspect = true;

            Image levelPlate = Img("LevelPlate", page, DefenseUiSpritePainter.Load("tab_blue"), true);
            levelPlate.rectTransform.anchorMin = levelPlate.rectTransform.anchorMax = new Vector2(0.5f, 1f);
            levelPlate.rectTransform.anchoredPosition = new Vector2(0f, -540f);
            levelPlate.rectTransform.sizeDelta = new Vector2(360f, 72f);
            TMP_Text level = Text("Level", levelPlate.rectTransform, "Lvl. 1", 44f, _titleMaterial, Color.white, true);

            // Core stat cells.
            RectTransform cellRow = Rect("CoreCells", page);
            TopBox(cellRow, 606f, 300f);
            var rowLayout = cellRow.gameObject.AddComponent<HorizontalLayoutGroup>();
            rowLayout.spacing = 14f;
            rowLayout.childControlWidth = rowLayout.childControlHeight = true;
            rowLayout.childForceExpandWidth = rowLayout.childForceExpandHeight = true;
            TowerStatCellView coreCell = BuildCoreCellTemplate(cellRow);

            // Cost + level up.
            Image costPanel = Img("UpgradePanel", page, DefenseUiSpritePainter.Load("popup_inner"), true);
            TopBox(costPanel.rectTransform, 930f, 310f);
            RectTransform costRow = Rect("CostRow", costPanel.rectTransform);
            costRow.anchorMin = new Vector2(0f, 1f);
            costRow.anchorMax = new Vector2(1f, 1f);
            costRow.pivot = new Vector2(0.5f, 1f);
            costRow.anchoredPosition = new Vector2(0f, -32f);
            costRow.sizeDelta = new Vector2(-40f, 84f);
            var costLayout = costRow.gameObject.AddComponent<HorizontalLayoutGroup>();
            costLayout.childAlignment = TextAnchor.MiddleCenter;
            costLayout.spacing = 60f;
            costLayout.childControlWidth = costLayout.childControlHeight = true;
            costLayout.childForceExpandWidth = costLayout.childForceExpandHeight = false;
            BuildCostGroup(costRow, "Coins", LoadSprite(Art + "MainMenu/Avatar/coinicon.png"), out _, out TMP_Text coinText);
            GameObject cardGroup = BuildCostGroup(costRow, "Cards", null, out Image cardIcon, out TMP_Text cardText);

            Button levelUp = Btn("LevelUpButton", costPanel.rectTransform, DefenseUiSpritePainter.Load("btn_green"));
            RectTransform levelUpRect = (RectTransform)levelUp.transform;
            levelUpRect.anchorMin = levelUpRect.anchorMax = new Vector2(0.5f, 0f);
            levelUpRect.pivot = new Vector2(0.5f, 0f);
            levelUpRect.anchoredPosition = new Vector2(0f, 40f);
            levelUpRect.sizeDelta = new Vector2(430f, 118f);
            var levelUpGroup = levelUp.gameObject.AddComponent<CanvasGroup>();
            RectTransform levelUpRow = Rect("Row", levelUpRect);
            Stretch(levelUpRow, 20f, 8f);
            var levelUpLayout = levelUpRow.gameObject.AddComponent<HorizontalLayoutGroup>();
            levelUpLayout.childAlignment = TextAnchor.MiddleCenter;
            levelUpLayout.spacing = 12f;
            levelUpLayout.childControlWidth = levelUpLayout.childControlHeight = true;
            levelUpLayout.childForceExpandWidth = levelUpLayout.childForceExpandHeight = false;
            Image levelUpArrow = Img("Arrow", levelUpRow, LoadSprite(Art + "Play/HUD/icon-arrow-up-green.png"), false);
            Size(levelUpArrow.rectTransform, 62f, 70f);
            levelUpArrow.preserveAspect = true;
            TMP_Text levelUpLabel = Text("Label", levelUpRow, DefenseStrings.LevelUp, 52f, _titleMaterial, Color.white, false);
            Fit(levelUpLabel);

            TMP_Text maxLabel = Text("MaxLabel", costPanel.rectTransform, DefenseStrings.MaxLevel, 64f, _titleMaterial, Hex("#FFE35A"), true);
            maxLabel.gameObject.SetActive(false);

            TMP_Text bonus = Text("UpgradeBonus", page, DefenseStrings.UpgradeBonus, 42f, _titleMaterial, Color.white, false);
            TopBox(bonus.rectTransform, 1268f, 70f);
            bonus.richText = true;

            core.Preview3D = preview3D;
            core.Preview = preview;
            core.PreviewFrame = previewFrame;
            core.TypeIcon = typeIcon;
            core.Level = level;
            core.CellRow = cellRow;
            core.CellTemplate = coreCell;
            core.CostRow = costRow.gameObject;
            core.CoinText = coinText;
            core.CardGroup = cardGroup;
            core.CardIcon = cardIcon;
            core.CardText = cardText;
            core.LevelUp = levelUp;
            core.LevelUpGroup = levelUpGroup;
            core.LevelUpLabel = levelUpLabel;
            core.LevelUpArrow = levelUpArrow.gameObject;
            core.MaxLabel = maxLabel.gameObject;
            core.Bonus = bonus;
            return page.gameObject;
        }

        private static GameObject BuildCostGroup(Transform parent, string name, Sprite iconSprite, out Image icon, out TMP_Text text)
        {
            RectTransform group = Rect(name, parent);
            var layout = group.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.spacing = 12f;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = layout.childForceExpandHeight = false;
            icon = Img("Icon", group, iconSprite, false);
            Size(icon.rectTransform, 76f, 76f);
            icon.preserveAspect = true;
            text = Text("Text", group, "0/0", 50f, _titleMaterial, Color.white, false);
            text.richText = true;
            Fit(text);
            return group.gameObject;
        }

        /// <summary>Vertical cell for Core Stats: icon, label, value.</summary>
        private static TowerStatCellView BuildCoreCellTemplate(Transform parent)
        {
            Image cell = Img("CoreCellTemplate", parent, DefenseUiSpritePainter.Load("stat_cell"), true);
            Image icon = Img("Icon", cell.rectTransform, null, false);
            icon.rectTransform.anchorMin = icon.rectTransform.anchorMax = new Vector2(0.5f, 1f);
            icon.rectTransform.pivot = new Vector2(0.5f, 1f);
            icon.rectTransform.anchoredPosition = new Vector2(0f, -16f);
            icon.rectTransform.sizeDelta = new Vector2(104f, 104f);
            icon.preserveAspect = true;
            TMP_Text label = Text("Label", cell.rectTransform, "Stat", 38f, null, DarkText, false);
            label.rectTransform.anchorMin = new Vector2(0f, 1f);
            label.rectTransform.anchorMax = new Vector2(1f, 1f);
            label.rectTransform.pivot = new Vector2(0.5f, 1f);
            label.rectTransform.anchoredPosition = new Vector2(0f, -122f);
            label.rectTransform.sizeDelta = new Vector2(-16f, 78f);
            label.textWrappingMode = TextWrappingModes.Normal;
            TMP_Text value = Text("Value", cell.rectTransform, "0", 44f, _valueMaterial, Color.white, false);
            value.rectTransform.anchorMin = new Vector2(0f, 0f);
            value.rectTransform.anchorMax = new Vector2(1f, 0f);
            value.rectTransform.pivot = new Vector2(0.5f, 0f);
            value.rectTransform.anchoredPosition = new Vector2(0f, 20f);
            value.rectTransform.sizeDelta = new Vector2(-12f, 64f);
            value.richText = true;

            var view = cell.gameObject.AddComponent<TowerStatCellView>();
            var so = new SerializedObject(view);
            so.FindProperty("_icon").objectReferenceValue = icon;
            so.FindProperty("_label").objectReferenceValue = label;
            so.FindProperty("_value").objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
            cell.gameObject.SetActive(false);
            return view;
        }

        private static GameObject BuildFullPage(RectTransform content, out TMP_Text level, out Transform grid, out TowerStatCellView cellTemplate)
        {
            RectTransform page = Rect("FullPage", content);
            Stretch(page);
            Image background = Img("Background", page, DefenseUiSpritePainter.Load("popup_inner"), true);
            Stretch(background.rectTransform);
            Image glow = Img("Glow", page, LoadSprite(Art + "MainMenu/title_glow_strip.png"), false);
            TopBox(glow.rectTransform, 18f, 96f);
            glow.color = new Color(0.4f, 0.95f, 1f, 0.9f);
            level = Text("Level", page, "Lvl. 1", 52f, _titleMaterial, Color.white, false);
            TopBox(level.rectTransform, 22f, 86f);

            RectTransform gridRect = Rect("Grid", page);
            TopBox(gridRect, 130f, 1200f);
            gridRect.offsetMin = new Vector2(20f, gridRect.offsetMin.y);
            gridRect.offsetMax = new Vector2(-20f, gridRect.offsetMax.y);
            float gap = 16f;
            float cellWidth = Mathf.Floor((PopupWidth - 60f - 40f - gap) / 2f);
            var layout = gridRect.gameObject.AddComponent<GridLayoutGroup>();
            layout.cellSize = new Vector2(cellWidth, 138f);
            layout.spacing = new Vector2(gap, gap);
            layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            layout.constraintCount = 2;
            grid = gridRect;

            // Horizontal cell: icon left, label over value, optional "?" top-right.
            Image cell = Img("FullCellTemplate", gridRect, DefenseUiSpritePainter.Load("stat_cell"), true);
            Image icon = Img("Icon", cell.rectTransform, null, false);
            icon.rectTransform.anchorMin = icon.rectTransform.anchorMax = new Vector2(0f, 0.5f);
            icon.rectTransform.anchoredPosition = new Vector2(70f, 0f);
            icon.rectTransform.sizeDelta = new Vector2(100f, 100f);
            icon.preserveAspect = true;
            TMP_Text label = Text("Label", cell.rectTransform, "Stat", 36f, null, DarkText, true);
            label.rectTransform.anchorMin = new Vector2(0f, 0.5f);
            label.rectTransform.anchorMax = new Vector2(1f, 1f);
            label.rectTransform.offsetMin = new Vector2(136f, -4f);
            label.rectTransform.offsetMax = new Vector2(-58f, -10f);
            label.alignment = TextAlignmentOptions.BottomLeft;
            TMP_Text value = Text("Value", cell.rectTransform, "0", 40f, _valueMaterial, Color.white, true);
            value.rectTransform.anchorMin = new Vector2(0f, 0f);
            value.rectTransform.anchorMax = new Vector2(1f, 0.5f);
            value.rectTransform.offsetMin = new Vector2(136f, 10f);
            value.rectTransform.offsetMax = new Vector2(-14f, 0f);
            value.alignment = TextAlignmentOptions.Left;
            value.richText = true;
            Button help = Btn("HelpButton", cell.rectTransform, DefenseUiSpritePainter.Load("help_square"));
            RectTransform helpRect = (RectTransform)help.transform;
            helpRect.anchorMin = helpRect.anchorMax = new Vector2(1f, 1f);
            helpRect.anchoredPosition = new Vector2(-34f, -34f);
            helpRect.sizeDelta = new Vector2(46f, 46f);
            Text("Label", helpRect, "?", 34f, _titleMaterial, Color.white, true);

            cellTemplate = cell.gameObject.AddComponent<TowerStatCellView>();
            var so = new SerializedObject(cellTemplate);
            so.FindProperty("_icon").objectReferenceValue = icon;
            so.FindProperty("_label").objectReferenceValue = label;
            so.FindProperty("_value").objectReferenceValue = value;
            so.FindProperty("_helpButton").objectReferenceValue = help;
            so.ApplyModifiedPropertiesWithoutUndo();
            cell.gameObject.SetActive(false);
            page.gameObject.SetActive(false);
            return page.gameObject;
        }

        private static GameObject BuildAboutPage(RectTransform content, out TMP_Text aboutText)
        {
            RectTransform page = Rect("AboutPage", content);
            Stretch(page);
            Image background = Img("Background", page, DefenseUiSpritePainter.Load("popup_inner"), true);
            Stretch(background.rectTransform);
            Image glow = Img("Glow", page, LoadSprite(Art + "MainMenu/title_glow_strip.png"), false);
            TopBox(glow.rectTransform, 18f, 96f);
            glow.color = new Color(0.4f, 0.95f, 1f, 0.9f);
            TMP_Text heading = Text("Heading", page, DefenseStrings.AboutTower, 52f, _titleMaterial, Color.white, false);
            TopBox(heading.rectTransform, 22f, 86f);

            Image box = Img("Box", page, DefenseUiSpritePainter.Load("stat_cell"), true);
            box.rectTransform.anchorMin = new Vector2(0f, 1f);
            box.rectTransform.anchorMax = new Vector2(1f, 1f);
            box.rectTransform.pivot = new Vector2(0.5f, 1f);
            box.rectTransform.anchoredPosition = new Vector2(0f, -136f);
            box.rectTransform.sizeDelta = new Vector2(-40f, 100f);
            var boxLayout = box.gameObject.AddComponent<VerticalLayoutGroup>();
            boxLayout.padding = new RectOffset(26, 26, 22, 26);
            boxLayout.childControlWidth = boxLayout.childControlHeight = true;
            boxLayout.childForceExpandWidth = true;
            boxLayout.childForceExpandHeight = false;
            box.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            aboutText = Text("Text", box.rectTransform, "", 40f, null, DarkText, false);
            aboutText.enableAutoSizing = false;
            aboutText.alignment = TextAlignmentOptions.TopLeft;
            aboutText.textWrappingMode = TextWrappingModes.Normal;
            aboutText.lineSpacing = 6f;
            page.gameObject.SetActive(false);
            return page.gameObject;
        }

        private static Button ArrowButton(string name, Transform panel, bool left)
        {
            Button button = Btn(name, panel, LoadSprite(Art + "MainMenu/Level/muiten.png"));
            ((Image)button.targetGraphic).type = Image.Type.Simple;
            ((Image)button.targetGraphic).preserveAspect = true;
            RectTransform rect = (RectTransform)button.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(left ? 0f : 1f, 1f);
            rect.anchoredPosition = new Vector2(left ? -8f : 8f, -330f);
            rect.sizeDelta = new Vector2(84f, 100f);
            rect.localScale = new Vector3(left ? -1f : 1f, 1f, 1f);
            return button;
        }

        // ------------------------------------------------------------------ helpers

        private static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = parent.gameObject.layer;
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        /// <summary>A full-width layout row of a fixed height.</summary>
        private static RectTransform Row(string name, Transform parent, float height)
        {
            RectTransform rect = Rect(name, parent);
            var element = rect.gameObject.AddComponent<LayoutElement>();
            element.minHeight = element.preferredHeight = height;
            return rect;
        }

        private static Image Img(string name, Transform parent, Sprite sprite, bool sliced)
        {
            RectTransform rect = Rect(name, parent);
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.type = sliced ? Image.Type.Sliced : Image.Type.Simple;
            image.raycastTarget = false;
            return image;
        }

        private static Button Btn(string name, Transform parent, Sprite sprite)
        {
            Image image = Img(name, parent, sprite, true);
            image.raycastTarget = true;
            var button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.transition = Selectable.Transition.None;
            image.gameObject.AddComponent<UiPressScale>();
            return button;
        }

        private static TMP_Text Text(string name, Transform parent, string content, float size, Material material, Color color, bool stretch)
        {
            RectTransform rect = Rect(name, parent);
            var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            if (_font != null)
            {
                text.font = _font;
            }

            if (material != null)
            {
                text.fontSharedMaterial = material; // shared presets only - no per-text material instances
            }

            text.text = content;
            text.fontSize = size;
            text.color = color;
            text.alignment = TextAlignmentOptions.Center;
            text.raycastTarget = false;
            text.enableAutoSizing = true;
            text.fontSizeMin = size * 0.5f;
            text.fontSizeMax = size;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            if (stretch)
            {
                Stretch(rect);
            }

            return text;
        }

        /// <summary>Lets a layout group size this text to its content.</summary>
        private static void Fit(TMP_Text text)
        {
            text.enableAutoSizing = false;
        }

        private static void Size(RectTransform rect, float width, float height)
        {
            var element = rect.gameObject.AddComponent<LayoutElement>();
            element.minWidth = element.preferredWidth = width;
            element.minHeight = element.preferredHeight = height;
        }

        private static void Stretch(RectTransform rect, float horizontal = 0f, float vertical = 0f)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(horizontal, vertical);
            rect.offsetMax = new Vector2(-horizontal, -vertical);
        }

        /// <summary>Full-width box anchored to the parent's top, <paramref name="top"/> below it.</summary>
        private static void TopBox(RectTransform rect, float top, float height)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, -top);
            rect.sizeDelta = new Vector2(0f, height);
        }

        private static void SetArray(SerializedProperty property, Object[] values)
        {
            property.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
            {
                property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            }
        }

        private static void DestroyChild(Transform parent, string name)
        {
            Transform child = parent.Find(name);
            if (child != null)
            {
                Object.DestroyImmediate(child.gameObject);
            }
        }

        private static Sprite LoadSprite(string path)
        {
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        private static Transform FindNamed(Scene scene, string name)
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

        private static Color Hex(string hex)
        {
            ColorUtility.TryParseHtmlString(hex, out Color color);
            return color;
        }
    }
}
