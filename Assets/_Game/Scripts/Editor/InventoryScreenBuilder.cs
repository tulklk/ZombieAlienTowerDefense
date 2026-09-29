using AlienDefense.Meta;
using AlienDefense.UI.Inventory;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace AlienDefense.EditorTools
{
    /// <summary>Builds the Inventory screen inside MainMenu's UpgradePanel.
    ///
    /// Re-runnable: it deletes its own InventoryRoot first and rebuilds, so tuning the layout here and running
    /// the menu item again is the intended workflow. It deliberately does NOT touch the existing tower-upgrade
    /// scroll - that object is only deactivated, so nothing the Upgrade tab already had is destroyed.
    ///
    /// Card templates are inactive scene objects rather than prefab assets: UiViewPool instantiates from them,
    /// and keeping them in the scene means the whole screen is editable in one place with no prefab round-trip.</summary>
    public static class InventoryScreenBuilder
    {
        private const string MainMenuPath = "Assets/_Game/Scenes/Menu/MainMenu.unity";
        private const string RootName = "InventoryRoot";
        private const string FontAssetPath = "Assets/_Game/Font/Fredoka-Bold SDF.asset";

        private const float PreviewHeight = 700f;
        private const float TabBarHeight = 108f;
        private const float SlotSize = 150f;
        private const float CategoryBarHeight = 96f;

        private static readonly Color PanelBlue = HexColor("#1B6FC4");
        private static readonly Color PanelDeep = HexColor("#0E4C8F");
        private static readonly Color TabInactive = HexColor("#2C86D8");
        private static readonly Color TabActive = HexColor("#3FC55C");
        private static readonly Color TextBright = Color.white;
        private static readonly Color GreenBadge = HexColor("#3FC55C");

        [MenuItem("AlienDefense/Setup/30. Build Inventory Screen")]
        public static void Build()
        {
            Scene scene = EditorSceneManager.GetActiveScene();
            if (scene.path != MainMenuPath)
            {
                scene = EditorSceneManager.OpenScene(MainMenuPath, OpenSceneMode.Single);
            }

            Transform upgradePanel = FindNamed(scene, "UpgradePanel");
            if (upgradePanel == null)
            {
                Debug.LogError("[InventoryScreenBuilder] UpgradePanel not found in MainMenu.");
                return;
            }

            // Park the tower-upgrade scroll instead of deleting it - the Upgrade tab's previous content stays in
            // the scene and can be switched back on at any time.
            Transform legacyScroll = upgradePanel.Find("UpgradeScroll");
            if (legacyScroll != null)
            {
                legacyScroll.gameObject.SetActive(false);
            }

            Transform existing = upgradePanel.Find(RootName);
            if (existing != null)
            {
                Object.DestroyImmediate(existing.gameObject);
            }

            RectTransform root = CreateRect(RootName, upgradePanel, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            var screenView = root.gameObject.AddComponent<InventoryScreenView>();
            var presenter = root.gameObject.AddComponent<InventoryScreenPresenter>();

            RarityPalette palette = LoadOrCreateRarityPalette();

            // ---------------- Preview area (visible on every tab) ----------------
            RectTransform preview = CreateRect("PreviewArea", root,
                new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -PreviewHeight), Vector2.zero);
            preview.pivot = new Vector2(0.5f, 1f);

            TMP_Text currencyText = CreateText("CraftCurrencyText", preview, "0", 40f,
                new Vector2(0.5f, 1f), new Vector2(0f, -46f), new Vector2(300f, 64f));

            CreateImage("UfoPreview", preview, LoadSprite("Assets/_Game/Art/Sprite/Play/LevelUpdate/ufoicon.png"),
                new Vector2(0.5f, 0.5f), new Vector2(0f, -30f), new Vector2(420f, 300f), Color.white);

            var slots = new EquipmentSlotView[6];
            EquipmentSlotType[] order =
            {
                EquipmentSlotType.Controls, EquipmentSlotType.Seat,
                EquipmentSlotType.Core, EquipmentSlotType.CargoBay,
                EquipmentSlotType.AntiGravity, EquipmentSlotType.Engine
            };

            for (int i = 0; i < 6; i++)
            {
                bool left = i % 2 == 0;
                int row = i / 2;
                float x = left ? -400f : 400f;
                float y = -150f - row * (SlotSize + 30f);
                slots[i] = CreateEquipmentSlot(preview, order[i], new Vector2(x, y), palette);
            }

            // ---------------- Content area ----------------
            float contentTop = -PreviewHeight;
            RectTransform content = CreateRect("ContentRoot", root,
                new Vector2(0f, 0f), new Vector2(1f, 1f),
                new Vector2(0f, TabBarHeight), new Vector2(0f, contentTop));

            var contentGroup = content.gameObject.AddComponent<CanvasGroup>();

            EquipmentPanelView equipmentPanel = BuildEquipmentContent(content, slots, currencyText, palette);
            ArtifactsPanelView artifactsPanel = BuildArtifactsPanel(content, palette);
            MaterialsPanelView materialsPanel = BuildMaterialsPanel(content, palette);

            // ---------------- Tab bar (fixed) ----------------
            RectTransform tabBar = CreateRect("InventoryTabBar", root,
                new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(12f, 8f), new Vector2(-12f, TabBarHeight));

            var tabLayout = tabBar.gameObject.AddComponent<HorizontalLayoutGroup>();
            tabLayout.spacing = 10f;
            tabLayout.childForceExpandWidth = true;
            tabLayout.childForceExpandHeight = true;
            tabLayout.childAlignment = TextAnchor.MiddleCenter;

            var tabButtons = new InventoryTabButtonView[3];
            tabButtons[0] = CreateTabButton(tabBar, InventoryTab.Equipment, "Equipment");
            tabButtons[1] = CreateTabButton(tabBar, InventoryTab.Artifacts, "Artifacts");
            tabButtons[2] = CreateTabButton(tabBar, InventoryTab.Materials, "Materials");

            // ---------------- Wire the views ----------------
            var viewSo = new SerializedObject(screenView);
            viewSo.FindProperty("_equipmentPanel").objectReferenceValue = equipmentPanel;
            viewSo.FindProperty("_artifactsPanel").objectReferenceValue = artifactsPanel;
            viewSo.FindProperty("_materialsPanel").objectReferenceValue = materialsPanel;
            viewSo.FindProperty("_contentGroup").objectReferenceValue = contentGroup;
            SerializedProperty tabsProperty = viewSo.FindProperty("_tabButtons");
            tabsProperty.arraySize = tabButtons.Length;
            for (int i = 0; i < tabButtons.Length; i++)
            {
                tabsProperty.GetArrayElementAtIndex(i).objectReferenceValue = tabButtons[i];
            }

            viewSo.ApplyModifiedPropertiesWithoutUndo();

            var presenterSo = new SerializedObject(presenter);
            presenterSo.FindProperty("_view").objectReferenceValue = screenView;
            presenterSo.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[InventoryScreenBuilder] Inventory screen rebuilt under UpgradePanel and MainMenu saved.");
        }

        // ------------------------------------------------------------------ Equipment

        private static EquipmentPanelView BuildEquipmentContent(RectTransform parent, EquipmentSlotView[] slots,
            TMP_Text currencyText, RarityPalette palette)
        {
            // The component lives on an always-active object because it also owns the sockets above.
            RectTransform host = CreateRect("EquipmentPanel", parent, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var view = host.gameObject.AddComponent<EquipmentPanelView>();

            RectTransform tabContent = CreateRect("TabContent", host, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            // Category bar
            RectTransform categoryBar = CreateRect("CategoryBar", tabContent,
                new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(12f, -CategoryBarHeight), new Vector2(-12f, 0f));
            AddPanelImage(categoryBar, PanelDeep);
            var categoryLayout = categoryBar.gameObject.AddComponent<HorizontalLayoutGroup>();
            categoryLayout.spacing = 8f;
            categoryLayout.padding = new RectOffset(10, 10, 10, 10);
            categoryLayout.childForceExpandWidth = true;
            categoryLayout.childForceExpandHeight = true;

            var categories = new EquipmentCategoryButtonView[7];
            categories[0] = CreateCategoryButton(categoryBar, true, EquipmentSlotType.Controls, "All");
            for (int i = 0; i < 6; i++)
            {
                EquipmentSlotType slot = (EquipmentSlotType)i;
                categories[i + 1] = CreateCategoryButton(categoryBar, false, slot, slot.ToString());
            }

            // Grid
            RectTransform gridRoot = CreateRect("GridRoot", tabContent,
                new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0f, 0f), new Vector2(0f, -CategoryBarHeight));

            ScrollRect scroll = CreateScroll("EquipmentScroll", gridRoot, out RectTransform gridContent);
            var grid = gridContent.gameObject.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(200f, 240f);
            grid.spacing = new Vector2(16f, 16f);
            grid.padding = new RectOffset(16, 16, 16, 16);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 4;
            var gridFitter = gridContent.gameObject.AddComponent<ContentSizeFitter>();
            gridFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            EquipmentCardView cardTemplate = CreateEquipmentCardTemplate(gridRoot, palette);
            GameObject emptyState = CreateEmptyState(gridRoot, "No equipment available", out TMP_Text emptyText);

            // Detail
            EquipmentDetailView detail = BuildEquipmentDetail(tabContent, palette);

            var so = new SerializedObject(view);
            so.FindProperty("_tabContentRoot").objectReferenceValue = tabContent.gameObject;
            SerializedProperty slotsProperty = so.FindProperty("_slots");
            slotsProperty.arraySize = slots.Length;
            for (int i = 0; i < slots.Length; i++)
            {
                slotsProperty.GetArrayElementAtIndex(i).objectReferenceValue = slots[i];
            }

            so.FindProperty("_craftCurrencyText").objectReferenceValue = currencyText;
            SerializedProperty categoriesProperty = so.FindProperty("_categoryButtons");
            categoriesProperty.arraySize = categories.Length;
            for (int i = 0; i < categories.Length; i++)
            {
                categoriesProperty.GetArrayElementAtIndex(i).objectReferenceValue = categories[i];
            }

            so.FindProperty("_gridRoot").objectReferenceValue = gridRoot.gameObject;
            so.FindProperty("_grid").objectReferenceValue = gridContent;
            so.FindProperty("_cardPrefab").objectReferenceValue = cardTemplate;
            so.FindProperty("_emptyState").objectReferenceValue = emptyState;
            so.FindProperty("_emptyStateText").objectReferenceValue = emptyText;
            so.FindProperty("_gridScroll").objectReferenceValue = scroll;
            so.FindProperty("_detail").objectReferenceValue = detail;
            so.FindProperty("_rarityPalette").objectReferenceValue = palette;
            so.ApplyModifiedPropertiesWithoutUndo();

            detail.gameObject.SetActive(false);
            return view;
        }

        private static EquipmentDetailView BuildEquipmentDetail(RectTransform parent, RarityPalette palette)
        {
            RectTransform detailRoot = CreateRect("EquipmentDetail", parent, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var view = detailRoot.gameObject.AddComponent<EquipmentDetailView>();

            RectTransform comparison = CreateRect("UpgradeComparison", detailRoot,
                new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(16f, -420f), new Vector2(-16f, -10f));
            AddPanelImage(comparison, PanelBlue);

            TMP_Text currentRarity = CreateText("CurrentRarity", comparison, "Epic", 38f,
                new Vector2(0.25f, 1f), new Vector2(0f, -44f), new Vector2(280f, 56f));
            GameObject rarityArrow = CreateImage("RarityArrow", comparison, null,
                new Vector2(0.5f, 1f), new Vector2(0f, -44f), new Vector2(36f, 36f), TextBright).gameObject;
            TMP_Text nextRarity = CreateText("NextRarity", comparison, "Legendary", 38f,
                new Vector2(0.75f, 1f), new Vector2(0f, -44f), new Vector2(280f, 56f));

            RectTransform statsContainer = CreateRect("StatsContainer", comparison,
                new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(16f, 16f), new Vector2(-16f, -90f));
            var statsLayout = statsContainer.gameObject.AddComponent<VerticalLayoutGroup>();
            statsLayout.spacing = 8f;
            statsLayout.childForceExpandWidth = true;
            statsLayout.childForceExpandHeight = true;

            StatComparisonRowView weaponRow = CreateStatRow(statsContainer, "Weapon Power");
            StatComparisonRowView abilityRow = CreateStatRow(statsContainer, "Ability");
            StatComparisonRowView speedRow = CreateStatRow(statsContainer, "Flight Speed");
            StatComparisonRowView maxLevelRow = CreateStatRow(statsContainer, "Max level");

            // Current item + requirements
            RectTransform currentPanel = CreateRect("CurrentItemPanel", detailRoot,
                new Vector2(0f, 1f), new Vector2(0f, 1f), Vector2.zero, Vector2.zero);
            currentPanel.pivot = new Vector2(0f, 1f);
            currentPanel.anchoredPosition = new Vector2(24f, -450f);
            currentPanel.sizeDelta = new Vector2(220f, 260f);

            EquipmentCardView currentCard = CreateEquipmentCardTemplate(currentPanel, palette);
            currentCard.name = "CurrentCard";
            currentCard.gameObject.SetActive(true);
            StretchToParent((RectTransform)currentCard.transform);

            RectTransform requirements = CreateRect("RequirementsContainer", detailRoot,
                new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(270f, -700f), new Vector2(-24f, -450f));
            AddPanelImage(requirements, PanelDeep);
            var reqLayout = requirements.gameObject.AddComponent<HorizontalLayoutGroup>();
            reqLayout.spacing = 12f;
            reqLayout.padding = new RectOffset(16, 16, 16, 16);
            reqLayout.childForceExpandWidth = true;
            reqLayout.childForceExpandHeight = true;

            CraftRequirementView duplicateReq = CreateRequirement(requirements, "DuplicateRequirement");
            CraftRequirementView currencyReq = CreateRequirement(requirements, "CurrencyRequirement");

            Button craftButton = CreateButton("CraftButton", detailRoot, "Craft", TabActive,
                new Vector2(0.5f, 0f), new Vector2(0f, 90f), new Vector2(420f, 110f), out TMP_Text craftLabel);
            Button backButton = CreateButton("BackButton", detailRoot, "<", TabInactive,
                new Vector2(0f, 0f), new Vector2(90f, 90f), new Vector2(140f, 110f), out _);

            var so = new SerializedObject(view);
            so.FindProperty("_currentRarityText").objectReferenceValue = currentRarity;
            so.FindProperty("_nextRarityText").objectReferenceValue = nextRarity;
            so.FindProperty("_rarityArrow").objectReferenceValue = rarityArrow;
            so.FindProperty("_weaponPowerRow").objectReferenceValue = weaponRow;
            so.FindProperty("_abilityRow").objectReferenceValue = abilityRow;
            so.FindProperty("_flightSpeedRow").objectReferenceValue = speedRow;
            so.FindProperty("_maxLevelRow").objectReferenceValue = maxLevelRow;
            so.FindProperty("_currentCard").objectReferenceValue = currentCard;
            so.FindProperty("_punchTarget").objectReferenceValue = currentCard.transform;
            so.FindProperty("_duplicateRequirement").objectReferenceValue = duplicateReq;
            so.FindProperty("_currencyRequirement").objectReferenceValue = currencyReq;
            so.FindProperty("_craftButton").objectReferenceValue = craftButton;
            so.FindProperty("_craftLabel").objectReferenceValue = craftLabel;
            so.FindProperty("_backButton").objectReferenceValue = backButton;
            so.FindProperty("_rarityPalette").objectReferenceValue = palette;
            so.ApplyModifiedPropertiesWithoutUndo();

            return view;
        }

        // ------------------------------------------------------------------ Artifacts

        private static ArtifactsPanelView BuildArtifactsPanel(RectTransform parent, RarityPalette palette)
        {
            RectTransform panelRoot = CreateRect("ArtifactsPanel", parent, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var view = panelRoot.gameObject.AddComponent<ArtifactsPanelView>();

            // Inventory state
            RectTransform inventoryRoot = CreateRect("InventoryState", panelRoot, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            RectTransform toolbar = CreateRect("ArtifactToolbar", inventoryRoot,
                new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(12f, -CategoryBarHeight), new Vector2(-12f, 0f));
            Button mergeButton = CreateButton("MergeButton", toolbar, "Merge", TabActive,
                new Vector2(1f, 0.5f), new Vector2(-120f, 0f), new Vector2(230f, 80f), out _);

            RectTransform gridHost = CreateRect("GridRoot", inventoryRoot,
                new Vector2(0f, 0f), new Vector2(1f, 1f), Vector2.zero, new Vector2(0f, -CategoryBarHeight));
            CreateScroll("ArtifactScroll", gridHost, out RectTransform gridContent);
            var grid = gridContent.gameObject.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(170f, 170f);
            grid.spacing = new Vector2(16f, 16f);
            grid.padding = new RectOffset(16, 16, 16, 16);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 5;
            var fitter = gridContent.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            ArtifactCardView cardTemplate = CreateArtifactCardTemplate(gridHost, palette);
            GameObject emptyState = CreateEmptyState(gridHost, "No artifacts available", out TMP_Text emptyText);

            // Merge state
            RectTransform mergeRoot = CreateRect("MergeState", panelRoot, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            RectTransform rowHost = CreateRect("RowHost", mergeRoot,
                new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0f, 120f), Vector2.zero);
            CreateScroll("MergeScroll", rowHost, out RectTransform rowContent);
            var rowLayout = rowContent.gameObject.AddComponent<VerticalLayoutGroup>();
            rowLayout.spacing = 12f;
            rowLayout.padding = new RectOffset(16, 16, 16, 16);
            rowLayout.childForceExpandWidth = true;
            rowLayout.childForceExpandHeight = false;
            var rowFitter = rowContent.gameObject.AddComponent<ContentSizeFitter>();
            rowFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            ArtifactMergeRowView rowTemplate = CreateMergeRowTemplate(rowHost, palette);
            GameObject noMerge = CreateEmptyState(rowHost, "There are no artifacts available for merging", out TMP_Text noMergeText);
            Button backButton = CreateButton("BackButton", mergeRoot, "<", TabInactive,
                new Vector2(0f, 0f), new Vector2(90f, 60f), new Vector2(140f, 100f), out _);

            var so = new SerializedObject(view);
            so.FindProperty("_inventoryRoot").objectReferenceValue = inventoryRoot.gameObject;
            so.FindProperty("_artifactGrid").objectReferenceValue = gridContent;
            so.FindProperty("_artifactCardPrefab").objectReferenceValue = cardTemplate;
            so.FindProperty("_openMergeButton").objectReferenceValue = mergeButton;
            so.FindProperty("_emptyState").objectReferenceValue = emptyState;
            so.FindProperty("_emptyStateText").objectReferenceValue = emptyText;
            so.FindProperty("_mergeRoot").objectReferenceValue = mergeRoot.gameObject;
            so.FindProperty("_mergeRowContainer").objectReferenceValue = rowContent;
            so.FindProperty("_mergeRowPrefab").objectReferenceValue = rowTemplate;
            so.FindProperty("_noMergeState").objectReferenceValue = noMerge;
            so.FindProperty("_noMergeText").objectReferenceValue = noMergeText;
            so.FindProperty("_backButton").objectReferenceValue = backButton;
            so.FindProperty("_rarityPalette").objectReferenceValue = palette;
            so.ApplyModifiedPropertiesWithoutUndo();

            mergeRoot.gameObject.SetActive(false);
            return view;
        }

        // ------------------------------------------------------------------ Materials

        private static MaterialsPanelView BuildMaterialsPanel(RectTransform parent, RarityPalette palette)
        {
            RectTransform panelRoot = CreateRect("MaterialsPanel", parent, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var view = panelRoot.gameObject.AddComponent<MaterialsPanelView>();

            CreateScroll("MaterialsScroll", panelRoot, out RectTransform content);
            var contentLayout = content.gameObject.AddComponent<VerticalLayoutGroup>();
            contentLayout.spacing = 12f;
            contentLayout.padding = new RectOffset(16, 16, 16, 24);
            contentLayout.childForceExpandWidth = true;
            contentLayout.childForceExpandHeight = false;
            contentLayout.childControlHeight = true;
            var contentFitter = content.gameObject.AddComponent<ContentSizeFitter>();
            contentFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            RectTransform containersSection = CreateSection(content, "ContainersSection", "Containers",
                out RectTransform containersGrid, 4);
            RectTransform materialsSection = CreateSection(content, "MaterialsSection", "Materials",
                out RectTransform materialsGrid, 6);

            InventoryItemView itemTemplate = CreateItemTemplate(panelRoot, palette);
            GameObject emptyState = CreateEmptyState(panelRoot, "No materials available", out TMP_Text emptyText);

            var so = new SerializedObject(view);
            so.FindProperty("_containersSection").objectReferenceValue = containersSection.gameObject;
            so.FindProperty("_containersGrid").objectReferenceValue = containersGrid;
            so.FindProperty("_materialsSection").objectReferenceValue = materialsSection.gameObject;
            so.FindProperty("_materialsGrid").objectReferenceValue = materialsGrid;
            so.FindProperty("_itemPrefab").objectReferenceValue = itemTemplate;
            so.FindProperty("_rarityPalette").objectReferenceValue = palette;
            so.FindProperty("_emptyState").objectReferenceValue = emptyState;
            so.FindProperty("_emptyStateText").objectReferenceValue = emptyText;
            so.ApplyModifiedPropertiesWithoutUndo();

            return view;
        }

        private static RectTransform CreateSection(RectTransform parent, string name, string header,
            out RectTransform grid, int columns)
        {
            RectTransform section = CreateRect(name, parent, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var layout = section.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 8f;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            layout.childControlHeight = true;
            var fitter = section.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            RectTransform headerRect = CreateRect("Header", section, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            AddPanelImage(headerRect, PanelDeep);
            var headerElement = headerRect.gameObject.AddComponent<LayoutElement>();
            headerElement.preferredHeight = 76f;
            CreateText("Label", headerRect, header, 38f, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(600f, 60f));

            grid = CreateRect("Grid", section, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var gridLayout = grid.gameObject.AddComponent<GridLayoutGroup>();
            gridLayout.cellSize = new Vector2(150f, 150f);
            gridLayout.spacing = new Vector2(14f, 14f);
            gridLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            gridLayout.constraintCount = columns;
            var gridFitter = grid.gameObject.AddComponent<ContentSizeFitter>();
            gridFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            return section;
        }

        // ------------------------------------------------------------------ Templates

        private static InventoryItemView CreateItemTemplate(RectTransform parent, RarityPalette palette)
        {
            RectTransform rect = CreateRect("ItemTemplate", parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, Vector2.zero);
            rect.sizeDelta = new Vector2(150f, 150f);
            var view = rect.gameObject.AddComponent<InventoryItemView>();

            Image frame = AddPanelImage(rect, Color.white);
            Image icon = CreateImage("Icon", rect, null, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(110f, 110f), Color.white);
            TMP_Text quantity = CreateText("Quantity", rect, "0", 30f, new Vector2(1f, 0f), new Vector2(-14f, 16f), new Vector2(120f, 40f));
            quantity.alignment = TextAlignmentOptions.BottomRight;
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = frame;

            var so = new SerializedObject(view);
            so.FindProperty("_rarityFrame").objectReferenceValue = frame;
            so.FindProperty("_icon").objectReferenceValue = icon;
            so.FindProperty("_quantityText").objectReferenceValue = quantity;
            so.FindProperty("_button").objectReferenceValue = button;
            so.ApplyModifiedPropertiesWithoutUndo();

            rect.gameObject.SetActive(false);
            return view;
        }

        private static EquipmentCardView CreateEquipmentCardTemplate(RectTransform parent, RarityPalette palette)
        {
            RectTransform rect = CreateRect("EquipmentCardTemplate", parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, Vector2.zero);
            rect.sizeDelta = new Vector2(200f, 240f);
            var view = rect.gameObject.AddComponent<EquipmentCardView>();

            Image frame = AddPanelImage(rect, Color.white);
            Image icon = CreateImage("Icon", rect, null, new Vector2(0.5f, 0.6f), Vector2.zero, new Vector2(140f, 140f), Color.white);
            TMP_Text level = CreateText("Level", rect, "Lvl. 1", 28f, new Vector2(0.5f, 1f), new Vector2(0f, -26f), new Vector2(180f, 40f));
            GameObject equippedBadge = CreateImage("EquippedBadge", rect,
                LoadSprite("Assets/_Game/Art/Sprite/MainMenu/Reward/tick-icon.png"),
                new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(70f, 70f), GreenBadge).gameObject;
            TMP_Text equippedText = CreateText("EquippedText", rect, "equipped", 24f,
                new Vector2(0.5f, 0f), new Vector2(0f, 26f), new Vector2(180f, 36f));
            GameObject upgradeBadge = CreateImage("UpgradeBadge", rect, null,
                new Vector2(1f, 1f), new Vector2(-18f, -18f), new Vector2(46f, 46f), GreenBadge).gameObject;
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = frame;

            var so = new SerializedObject(view);
            so.FindProperty("_rarityFrame").objectReferenceValue = frame;
            so.FindProperty("_icon").objectReferenceValue = icon;
            so.FindProperty("_levelText").objectReferenceValue = level;
            so.FindProperty("_equippedBadge").objectReferenceValue = equippedBadge;
            so.FindProperty("_equippedText").objectReferenceValue = equippedText;
            so.FindProperty("_upgradeBadge").objectReferenceValue = upgradeBadge;
            so.FindProperty("_button").objectReferenceValue = button;
            so.ApplyModifiedPropertiesWithoutUndo();

            rect.gameObject.SetActive(false);
            return view;
        }

        private static ArtifactCardView CreateArtifactCardTemplate(RectTransform parent, RarityPalette palette)
        {
            RectTransform rect = CreateRect("ArtifactCardTemplate", parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, Vector2.zero);
            rect.sizeDelta = new Vector2(170f, 170f);
            var view = rect.gameObject.AddComponent<ArtifactCardView>();

            Image frame = AddPanelImage(rect, Color.white);
            Image icon = CreateImage("Icon", rect, null, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(120f, 120f), Color.white);
            TMP_Text amount = CreateText("Amount", rect, "0", 28f, new Vector2(1f, 0f), new Vector2(-14f, 14f), new Vector2(110f, 38f));
            amount.alignment = TextAlignmentOptions.BottomRight;
            GameObject mergeReady = CreateImage("MergeReadyBadge", rect, null,
                new Vector2(1f, 1f), new Vector2(-16f, -16f), new Vector2(42f, 42f), GreenBadge).gameObject;
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = frame;

            var so = new SerializedObject(view);
            so.FindProperty("_rarityFrame").objectReferenceValue = frame;
            so.FindProperty("_icon").objectReferenceValue = icon;
            so.FindProperty("_amountText").objectReferenceValue = amount;
            so.FindProperty("_mergeReadyBadge").objectReferenceValue = mergeReady;
            so.FindProperty("_button").objectReferenceValue = button;
            so.ApplyModifiedPropertiesWithoutUndo();

            rect.gameObject.SetActive(false);
            return view;
        }

        private static ArtifactMergeRowView CreateMergeRowTemplate(RectTransform parent, RarityPalette palette)
        {
            RectTransform rect = CreateRect("MergeRowTemplate", parent, new Vector2(0f, 1f), new Vector2(1f, 1f),
                Vector2.zero, Vector2.zero);
            rect.sizeDelta = new Vector2(0f, 150f);
            var element = rect.gameObject.AddComponent<LayoutElement>();
            element.preferredHeight = 150f;
            AddPanelImage(rect, PanelBlue);
            var view = rect.gameObject.AddComponent<ArtifactMergeRowView>();

            RectTransform inputs = CreateRect("InputSlots", rect,
                new Vector2(0f, 0f), new Vector2(0.65f, 1f), new Vector2(12f, 12f), new Vector2(-12f, -12f));
            var inputLayout = inputs.gameObject.AddComponent<HorizontalLayoutGroup>();
            inputLayout.spacing = 8f;
            inputLayout.childForceExpandWidth = false;
            inputLayout.childForceExpandHeight = false;
            inputLayout.childAlignment = TextAnchor.MiddleLeft;

            RectTransform slotRect = CreateRect("InputSlotTemplate", inputs, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, Vector2.zero);
            slotRect.sizeDelta = new Vector2(110f, 110f);
            var slotImage = slotRect.gameObject.AddComponent<Image>();
            slotImage.color = Color.white;
            var slotElement = slotRect.gameObject.AddComponent<LayoutElement>();
            slotElement.preferredWidth = 110f;
            slotElement.preferredHeight = 110f;
            slotRect.gameObject.SetActive(false);

            CreateImage("Arrow", rect, null, new Vector2(0.7f, 0.5f), Vector2.zero, new Vector2(52f, 52f), TextBright);

            Image resultFrame = CreateImage("ResultFrame", rect, null, new Vector2(0.86f, 0.5f), Vector2.zero,
                new Vector2(120f, 120f), Color.white);
            Image resultIcon = CreateImage("ResultIcon", resultFrame.rectTransform, null, new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(90f, 90f), Color.white);

            Button mergeButton = CreateButton("RowMergeButton", rect, "", TabActive,
                new Vector2(1f, 0.5f), new Vector2(-70f, 0f), new Vector2(120f, 120f), out _);

            var so = new SerializedObject(view);
            so.FindProperty("_inputSlotContainer").objectReferenceValue = inputs;
            so.FindProperty("_inputSlotPrefab").objectReferenceValue = slotImage;
            so.FindProperty("_resultIcon").objectReferenceValue = resultIcon;
            so.FindProperty("_resultFrame").objectReferenceValue = resultFrame;
            so.FindProperty("_mergeButton").objectReferenceValue = mergeButton;
            so.ApplyModifiedPropertiesWithoutUndo();

            rect.gameObject.SetActive(false);
            return view;
        }

        private static EquipmentSlotView CreateEquipmentSlot(RectTransform parent, EquipmentSlotType slot,
            Vector2 position, RarityPalette palette)
        {
            RectTransform rect = CreateRect("Slot_" + slot, parent, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                Vector2.zero, Vector2.zero);
            rect.anchoredPosition = position;
            rect.sizeDelta = new Vector2(SlotSize, SlotSize);
            AddPanelImage(rect, PanelDeep);

            var view = rect.gameObject.AddComponent<EquipmentSlotView>();

            Image emptyIcon = CreateImage("EmptyIcon", rect, null, new Vector2(0.5f, 0.5f), Vector2.zero,
                new Vector2(96f, 96f), Color.white);
            Image itemIcon = CreateImage("ItemIcon", rect, null, new Vector2(0.5f, 0.5f), Vector2.zero,
                new Vector2(110f, 110f), Color.white);
            Image rarityFrame = CreateImage("RarityFrame", rect, null, new Vector2(0.5f, 0.5f), Vector2.zero,
                new Vector2(SlotSize, SlotSize), Color.white);
            rarityFrame.transform.SetAsFirstSibling();
            TMP_Text level = CreateText("LevelText", rect, "Lvl. 1", 26f, new Vector2(1f, 1f),
                new Vector2(-8f, -8f), new Vector2(110f, 36f));
            GameObject upgradeIndicator = CreateImage("UpgradeIndicator", rect, null, new Vector2(1f, 1f),
                new Vector2(-6f, -6f), new Vector2(42f, 42f), GreenBadge).gameObject;
            var button = rect.gameObject.AddComponent<Button>();

            var so = new SerializedObject(view);
            so.FindProperty("_slot").enumValueIndex = (int)slot;
            so.FindProperty("_emptyIcon").objectReferenceValue = emptyIcon;
            so.FindProperty("_itemIcon").objectReferenceValue = itemIcon;
            so.FindProperty("_rarityFrame").objectReferenceValue = rarityFrame;
            so.FindProperty("_levelText").objectReferenceValue = level;
            so.FindProperty("_upgradeIndicator").objectReferenceValue = upgradeIndicator;
            so.FindProperty("_button").objectReferenceValue = button;
            so.ApplyModifiedPropertiesWithoutUndo();

            return view;
        }

        private static StatComparisonRowView CreateStatRow(RectTransform parent, string label)
        {
            RectTransform rect = CreateRect("StatRow_" + label.Replace(" ", string.Empty), parent,
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var view = rect.gameObject.AddComponent<StatComparisonRowView>();

            TMP_Text name = CreateText("Name", rect, label, 32f, new Vector2(0f, 0.5f), new Vector2(230f, 0f), new Vector2(440f, 46f));
            name.alignment = TextAlignmentOptions.Left;
            TMP_Text current = CreateText("Current", rect, "0", 32f, new Vector2(0.62f, 0.5f), Vector2.zero, new Vector2(150f, 46f));
            GameObject arrow = CreateImage("Arrow", rect, null, new Vector2(0.72f, 0.5f), Vector2.zero, new Vector2(30f, 30f), TextBright).gameObject;
            TMP_Text next = CreateText("Next", rect, "0", 32f, new Vector2(0.84f, 0.5f), Vector2.zero, new Vector2(150f, 46f));
            GameObject badge = CreateImage("IncreaseBadge", rect, null, new Vector2(0.95f, 0.5f), Vector2.zero, new Vector2(30f, 30f), GreenBadge).gameObject;

            var so = new SerializedObject(view);
            so.FindProperty("_nameText").objectReferenceValue = name;
            so.FindProperty("_currentValueText").objectReferenceValue = current;
            so.FindProperty("_nextValueText").objectReferenceValue = next;
            so.FindProperty("_arrow").objectReferenceValue = arrow;
            so.FindProperty("_increaseBadge").objectReferenceValue = badge;
            so.ApplyModifiedPropertiesWithoutUndo();

            return view;
        }

        private static CraftRequirementView CreateRequirement(RectTransform parent, string name)
        {
            RectTransform rect = CreateRect(name, parent, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var view = rect.gameObject.AddComponent<CraftRequirementView>();
            AddPanelImage(rect, PanelBlue);

            Image icon = CreateImage("Icon", rect, null, new Vector2(0.5f, 0.6f), Vector2.zero, new Vector2(100f, 100f), Color.white);
            TMP_Text amount = CreateText("Amount", rect, "0/0", 30f, new Vector2(0.5f, 0f), new Vector2(0f, 26f), new Vector2(180f, 40f));

            var so = new SerializedObject(view);
            so.FindProperty("_icon").objectReferenceValue = icon;
            so.FindProperty("_amountText").objectReferenceValue = amount;
            so.ApplyModifiedPropertiesWithoutUndo();

            return view;
        }

        private static InventoryTabButtonView CreateTabButton(RectTransform parent, InventoryTab tab, string label)
        {
            RectTransform rect = CreateRect("Tab_" + tab, parent, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var view = rect.gameObject.AddComponent<InventoryTabButtonView>();
            Image background = AddPanelImage(rect, TabInactive);
            TMP_Text text = CreateText("Label", rect, label, 38f, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(320f, 56f));
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = background;

            var so = new SerializedObject(view);
            so.FindProperty("_tab").enumValueIndex = (int)tab;
            so.FindProperty("_button").objectReferenceValue = button;
            so.FindProperty("_background").objectReferenceValue = background;
            so.FindProperty("_label").objectReferenceValue = text;
            so.ApplyModifiedPropertiesWithoutUndo();

            return view;
        }

        private static EquipmentCategoryButtonView CreateCategoryButton(RectTransform parent, bool isAll,
            EquipmentSlotType slot, string label)
        {
            RectTransform rect = CreateRect("Cat_" + label, parent, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var view = rect.gameObject.AddComponent<EquipmentCategoryButtonView>();
            Image background = AddPanelImage(rect, TabInactive);
            Image icon = CreateImage("Icon", rect, null, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(52f, 52f), Color.white);
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = background;

            var so = new SerializedObject(view);
            so.FindProperty("_isAll").boolValue = isAll;
            so.FindProperty("_slot").enumValueIndex = (int)slot;
            so.FindProperty("_button").objectReferenceValue = button;
            so.FindProperty("_background").objectReferenceValue = background;
            so.FindProperty("_icon").objectReferenceValue = icon;
            so.ApplyModifiedPropertiesWithoutUndo();

            return view;
        }

        // ------------------------------------------------------------------ Primitives

        private static ScrollRect CreateScroll(string name, RectTransform parent, out RectTransform content)
        {
            RectTransform scrollRect = CreateRect(name, parent, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var scroll = scrollRect.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Elastic;
            scroll.scrollSensitivity = 24f;

            RectTransform viewport = CreateRect("Viewport", scrollRect, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            // RectMask2D, not Mask: no extra draw call and no stencil, which is what the mobile budget wants.
            viewport.gameObject.AddComponent<RectMask2D>();

            content = CreateRect("Content", viewport, new Vector2(0f, 1f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero);
            content.pivot = new Vector2(0.5f, 1f);

            scroll.viewport = viewport;
            scroll.content = content;
            return scroll;
        }

        private static GameObject CreateEmptyState(RectTransform parent, string message, out TMP_Text text)
        {
            RectTransform rect = CreateRect("EmptyState", parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, Vector2.zero);
            rect.sizeDelta = new Vector2(760f, 120f);
            text = CreateText("Label", rect, message, 34f, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(760f, 120f));
            rect.gameObject.SetActive(false);
            return rect.gameObject;
        }

        private static RectTransform CreateRect(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax,
            Vector2 offsetMin, Vector2 offsetMax)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
            return rect;
        }

        private static Image AddPanelImage(RectTransform rect, Color color)
        {
            var image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            return image;
        }

        private static Image CreateImage(string name, Transform parent, Sprite sprite, Vector2 anchor,
            Vector2 position, Vector2 size, Color color)
        {
            RectTransform rect = CreateRect(name, parent, anchor, anchor, Vector2.zero, Vector2.zero);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.raycastTarget = false;
            image.preserveAspect = true;
            return image;
        }

        private static TMP_Text CreateText(string name, Transform parent, string content, float size,
            Vector2 anchor, Vector2 position, Vector2 rectSize)
        {
            RectTransform rect = CreateRect(name, parent, anchor, anchor, Vector2.zero, Vector2.zero);
            rect.anchoredPosition = position;
            rect.sizeDelta = rectSize;
            var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);
            if (font != null)
            {
                // Without this the text falls back to TMP's default asset, which this project does not ship
                // glyphs for - every label renders as boxes.
                text.font = font;
            }

            text.text = content;
            text.fontSize = size;
            text.color = TextBright;
            text.alignment = TextAlignmentOptions.Center;
            text.raycastTarget = false;
            text.enableAutoSizing = true;
            text.fontSizeMin = size * 0.5f;
            text.fontSizeMax = size;
            return text;
        }

        private static Button CreateButton(string name, Transform parent, string label, Color color,
            Vector2 anchor, Vector2 position, Vector2 size, out TMP_Text labelText)
        {
            RectTransform rect = CreateRect(name, parent, anchor, anchor, Vector2.zero, Vector2.zero);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            var image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            labelText = string.IsNullOrEmpty(label)
                ? null
                : CreateText("Label", rect, label, 38f, new Vector2(0.5f, 0.5f), Vector2.zero, size);
            return button;
        }

        private static void StretchToParent(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static RarityPalette LoadOrCreateRarityPalette()
        {
            const string path = "Assets/_Game/Data/Meta/RarityPalette.asset";
            var palette = AssetDatabase.LoadAssetAtPath<RarityPalette>(path);
            if (palette != null)
            {
                return palette;
            }

            palette = ScriptableObject.CreateInstance<RarityPalette>();
            var so = new SerializedObject(palette);
            SerializedProperty entries = so.FindProperty("_entries");

            (MetaItemRarity rarity, string hex, string name)[] defaults =
            {
                (MetaItemRarity.Common, "#9AA3AD", "Common"),
                (MetaItemRarity.Uncommon, "#4CD964", "Uncommon"),
                (MetaItemRarity.Rare, "#3AA7FF", "Rare"),
                (MetaItemRarity.Epic, "#C061FF", "Epic"),
                (MetaItemRarity.Legendary, "#FFC53A", "Legendary")
            };

            entries.arraySize = defaults.Length;
            for (int i = 0; i < defaults.Length; i++)
            {
                SerializedProperty entry = entries.GetArrayElementAtIndex(i);
                entry.FindPropertyRelative("_rarity").enumValueIndex = (int)defaults[i].rarity;
                Color color = HexColor(defaults[i].hex);
                entry.FindPropertyRelative("_frameColor").colorValue = color;
                entry.FindPropertyRelative("_labelColor").colorValue = color;
                entry.FindPropertyRelative("_displayName").stringValue = defaults[i].name;
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.CreateAsset(palette, path);
            AssetDatabase.SaveAssets();
            Debug.Log("[InventoryScreenBuilder] Created " + path);
            return palette;
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

        private static Color HexColor(string hex)
        {
            return ColorUtility.TryParseHtmlString(hex, out Color color) ? color : Color.white;
        }
    }
}
