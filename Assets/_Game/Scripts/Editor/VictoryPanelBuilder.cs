using System.Collections.Generic;
using System.IO;
using AlienDefense.Progression;
using AlienDefense.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace AlienDefense.EditorTools
{
    /// <summary>Rebuilds the victory screen around winpanel.png: the dark overlay and golden glow, the level result,
    /// the damage leaders, the reward grid, Next, and the two popups (reward detail, full damage statistics). Wires
    /// everything to VictoryPanelView and points GameStateUIController at it.
    ///
    /// winpanel.png already draws the frame, the "Victory!" banner and the "REWARD" divider, so this only places live
    /// content into the two empty boxes; the layout constants are fractions of that sprite, measured off it once.
    ///
    /// Re-runnable: the panel's children are rebuilt from scratch each time.</summary>
    internal static class VictoryPanelBuilder
    {
        private const string WinSpriteDir = "Assets/_Game/Art/Sprite/Play/WinPanel";
        private const string PauseSpriteDir = "Assets/_Game/Art/Sprite/Play/PausePanel";
        private const string MenuIconDir = "Assets/_Game/Art/Sprite/MainMenu/Avatar";
        private const string GeneratedTextureDir = "Assets/_Game/Art/Textures/UI";
        private const string CatalogPath = "Assets/_Game/Data/UI/VictoryRewardCatalog.asset";

        // Canvas reference resolution is 1080x1920; winpanel.png is 941x1672.
        private const float PanelWidth = 880f;
        private const float PanelHeight = 1563f;
        private const float PanelCenterY = 90f;

        // Fractions of the panel sprite (0 = top/left).
        private const float TopBoxTop = 0.266f;
        private const float TopBoxBottom = 0.466f;
        private const float BoxLeft = 0.064f;
        private const float BoxRight = 0.936f;
        private const float BottomBoxTop = 0.544f;
        private const float BottomBoxBottom = 0.879f;

        private const int LeaderRowCount = 2;
        private const int RewardItemCount = 8;
        private const int StatRowCount = 8;

        private static readonly Color Dim = new Color(0.02f, 0.03f, 0.06f, 0.78f);
        private static readonly Color BarColor = new Color(1f, 0.82f, 0.28f, 1f);
        private static readonly Color BarTrack = new Color(0f, 0f, 0f, 0.35f);
        private static readonly Color SubText = new Color(0.82f, 0.95f, 1f, 1f);
        private static readonly Color PopupBody = new Color(0.10f, 0.33f, 0.62f, 1f);
        private static readonly Color RowTint = new Color(0.07f, 0.24f, 0.45f, 0.75f);
        private static readonly Color CoinTheme = new Color(0.27f, 0.72f, 0.33f, 1f);
        private static readonly Color GemTheme = new Color(0.55f, 0.32f, 0.85f, 1f);
        private static readonly Color XpTheme = new Color(0.18f, 0.56f, 0.87f, 1f);

        [MenuItem("AlienDefense/Setup/HUD/Rebuild Victory Panel (Open Scene)")]
        private static void Rebuild()
        {
            Scene scene = EditorSceneManager.GetActiveScene();
            var controller = Object.FindFirstObjectByType<GameStateUIController>(FindObjectsInactive.Include);
            if (controller == null)
            {
                Debug.LogError("[VictoryPanelBuilder] No GameStateUIController in " + scene.path);
                return;
            }

            VictoryRewardCatalog catalog = EnsureCatalog();

            var controllerSo = new SerializedObject(controller);
            SerializedProperty panelProperty = controllerSo.FindProperty("_victoryPanel");
            var existing = panelProperty.objectReferenceValue as GameObject;
            Transform parent = existing != null ? existing.transform.parent : FindPanelParent(controller);
            if (parent == null)
            {
                Debug.LogError("[VictoryPanelBuilder] Could not find a Canvas/SafeArea to build under.");
                return;
            }

            GameObject root = existing != null ? existing : new GameObject("VictoryPanel", typeof(RectTransform));
            if (existing == null)
            {
                root.transform.SetParent(parent, false);
            }

            for (int i = root.transform.childCount - 1; i >= 0; i--)
            {
                Object.DestroyImmediate(root.transform.GetChild(i).gameObject);
            }

            VictoryPanelView view = Build(root, catalog);
            root.transform.SetAsLastSibling(); // above the HUD, the minimap and the pause panel

            panelProperty.objectReferenceValue = root;
            controllerSo.FindProperty("_victoryPanelView").objectReferenceValue = view;
            controllerSo.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(controller);

            root.SetActive(false); // GameStateUIController shows it on victory
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[VictoryPanelBuilder] Rebuilt VictoryPanel in " + scene.path);
        }

        private static VictoryPanelView Build(GameObject root, VictoryRewardCatalog catalog)
        {
            Stretch(root.GetComponent<RectTransform>() ?? root.AddComponent<RectTransform>());
            var overlayGroup = root.GetComponent<CanvasGroup>() ?? root.AddComponent<CanvasGroup>();
            VictoryPanelView view = root.GetComponent<VictoryPanelView>() ?? root.AddComponent<VictoryPanelView>();
            var so = new SerializedObject(view);

            GameObject dim = Solid(root.transform, "DarkOverlay", Dim, Vector2.zero, Vector2.zero, Vector2.zero);
            Stretch(dim.GetComponent<RectTransform>());
            dim.GetComponent<Image>().raycastTarget = true;

            // ---- Golden glow behind the panel
            var vfx = new GameObject("VictoryVFX", typeof(RectTransform), typeof(CanvasGroup));
            vfx.transform.SetParent(root.transform, false);
            Place(vfx.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0f, PanelCenterY + PanelHeight * 0.36f), new Vector2(10f, 10f));
            var vfxGroup = vfx.GetComponent<CanvasGroup>();
            GameObject rays = Sprite(vfx.transform, "LightRays", LoadOrCreateRays(), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1500f, 1500f));
            rays.GetComponent<Image>().color = new Color(1f, 0.86f, 0.45f, 0.5f);
            GameObject glow = Sprite(vfx.transform, "GoldenGlow", LoadOrCreateGlow(), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1100f, 1100f));
            glow.GetComponent<Image>().color = new Color(1f, 0.79f, 0.32f, 0.85f);

            // ---- Panel art
            GameObject panel = Sprite(root.transform, "MainPanel", Load(WinSpriteDir, "winpanel"), new Vector2(0.5f, 0.5f),
                new Vector2(0f, PanelCenterY), new Vector2(PanelWidth, PanelHeight));
            Transform panelTransform = panel.transform;

            float boxWidth = (BoxRight - BoxLeft) * PanelWidth;

            // ---- Top box: level result + damage leaders
            var topBox = new GameObject("DamageLeaderSection", typeof(RectTransform));
            topBox.transform.SetParent(panelTransform, false);
            float topHeight = (TopBoxBottom - TopBoxTop) * PanelHeight;
            Place(topBox.GetComponent<RectTransform>(), new Vector2(0.5f, 1f), new Vector2(0f, -TopBoxTop * PanelHeight), new Vector2(boxWidth, topHeight));

            TMP_Text levelText = Text(topBox.transform, "LevelText", "CAMPAIGN LEVEL 1", new Vector2(0.5f, 1f),
                new Vector2(0f, -10f), new Vector2(boxWidth - 40f, 56f), 42f, TextAlignmentOptions.Center, Color.white, FontStyles.Bold);
            TMP_Text resultText = Text(topBox.transform, "ResultText", "Perfect Clear", new Vector2(0.5f, 1f),
                new Vector2(0f, -66f), new Vector2(boxWidth - 40f, 40f), 28f, TextAlignmentOptions.Center, SubText, FontStyles.Bold);

            BuildLeaderRows(topBox.transform, so, boxWidth, -112f);
            TMP_Text noDamage = Text(topBox.transform, "NoDamageLabel", "No damage recorded", new Vector2(0.5f, 1f),
                new Vector2(0f, -150f), new Vector2(boxWidth - 40f, 50f), 30f, TextAlignmentOptions.Center, SubText);
            so.FindProperty("_noDamageLabel").objectReferenceValue = noDamage.gameObject;

            Button statsButton = LabelButton(topBox.transform, "StatisticsButton", "STATS", new Color(0.24f, 0.72f, 0.36f, 1f),
                new Vector2(1f, 1f), new Vector2(-6f, -6f), new Vector2(118f, 56f), 24f);

            // ---- Bottom box: reward grid
            var rewardSection = new GameObject("RewardSection", typeof(RectTransform));
            rewardSection.transform.SetParent(panelTransform, false);
            float bottomHeight = (BottomBoxBottom - BottomBoxTop) * PanelHeight;
            Place(rewardSection.GetComponent<RectTransform>(), new Vector2(0.5f, 1f), new Vector2(0f, -BottomBoxTop * PanelHeight), new Vector2(boxWidth, bottomHeight));

            BuildRewardGrid(rewardSection.transform, so, boxWidth);
            TMP_Text noReward = Text(rewardSection.transform, "NoRewardLabel", "No rewards", new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(boxWidth - 40f, 60f), 34f, TextAlignmentOptions.Center, SubText, FontStyles.Bold);
            so.FindProperty("_noRewardLabel").objectReferenceValue = noReward.gameObject;

            // ---- Next
            Button next = SpriteButton(root.transform, "NextButton", Load(PauseSpriteDir, "continuebtn"), new Vector2(0.5f, 0f),
                new Vector2(0f, 150f), new Vector2(430f, 215f));

            // ---- Popups
            BuildRewardPopup(root.transform, so);
            BuildStatisticsPopup(root.transform, so);

            so.FindProperty("_overlayGroup").objectReferenceValue = overlayGroup;
            so.FindProperty("_mainPanel").objectReferenceValue = panel.GetComponent<RectTransform>();
            so.FindProperty("_victoryVfx").objectReferenceValue = vfxGroup;
            so.FindProperty("_levelText").objectReferenceValue = levelText;
            so.FindProperty("_resultText").objectReferenceValue = resultText;
            so.FindProperty("_statisticsButton").objectReferenceValue = statsButton;
            so.FindProperty("_nextButton").objectReferenceValue = next;
            so.FindProperty("_rewardCatalog").objectReferenceValue = catalog;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(view);
            return view;
        }

        private static void BuildLeaderRows(Transform parent, SerializedObject so, float boxWidth, float firstY)
        {
            SerializedProperty rows = so.FindProperty("_leaderRows");
            rows.arraySize = LeaderRowCount;
            const float rowHeight = 78f;
            float rowWidth = boxWidth - 40f;

            for (int i = 0; i < LeaderRowCount; i++)
            {
                var rowObject = new GameObject("Leader_0" + (i + 1), typeof(RectTransform), typeof(CanvasGroup));
                rowObject.transform.SetParent(parent, false);
                Place(rowObject.GetComponent<RectTransform>(), new Vector2(0.5f, 1f),
                    new Vector2(0f, firstY - i * (rowHeight + 8f)), new Vector2(rowWidth, rowHeight));

                GameObject icon = Solid(rowObject.transform, "Icon", Color.white, new Vector2(0f, 0.5f), new Vector2(6f, 0f), new Vector2(58f, 58f));
                TMP_Text nameText = Text(rowObject.transform, "NameText", "-", new Vector2(0f, 1f),
                    new Vector2(74f, -2f), new Vector2(rowWidth - 280f, 36f), 28f, TextAlignmentOptions.Left, Color.white, FontStyles.Bold);
                TMP_Text valueText = Text(rowObject.transform, "ValueText", "0", new Vector2(1f, 1f),
                    new Vector2(-6f, -2f), new Vector2(190f, 36f), 28f, TextAlignmentOptions.Right, Color.white, FontStyles.Bold);

                float barWidth = rowWidth - 80f;
                Solid(rowObject.transform, "BarTrack", BarTrack, new Vector2(0f, 0f), new Vector2(74f, 8f), new Vector2(barWidth, 16f));
                GameObject bar = Solid(rowObject.transform, "Bar", BarColor, new Vector2(0f, 0f), new Vector2(74f, 8f), new Vector2(barWidth, 16f));
                Image barImage = MakeFilled(bar);

                SerializedProperty row = rows.GetArrayElementAtIndex(i);
                row.FindPropertyRelative("Root").objectReferenceValue = rowObject;
                row.FindPropertyRelative("Icon").objectReferenceValue = icon.GetComponent<Image>();
                row.FindPropertyRelative("NameText").objectReferenceValue = nameText;
                row.FindPropertyRelative("ValueText").objectReferenceValue = valueText;
                row.FindPropertyRelative("Bar").objectReferenceValue = barImage;
                rowObject.SetActive(false);
            }
        }

        private static void BuildRewardGrid(Transform parent, SerializedObject so, float boxWidth)
        {
            SerializedProperty items = so.FindProperty("_rewardItems");
            items.arraySize = RewardItemCount;
            const int columns = 4;
            const float tile = 150f;
            const float spacingX = 26f;
            const float spacingY = 30f;
            float totalWidth = columns * tile + (columns - 1) * spacingX;
            float startX = -totalWidth * 0.5f + tile * 0.5f;

            for (int i = 0; i < RewardItemCount; i++)
            {
                int column = i % columns;
                int rowIndex = i / columns;
                var itemObject = new GameObject("RewardItem_0" + (i + 1), typeof(RectTransform));
                itemObject.transform.SetParent(parent, false);
                Place(itemObject.GetComponent<RectTransform>(), new Vector2(0.5f, 1f),
                    new Vector2(startX + column * (tile + spacingX), -40f - rowIndex * (tile + spacingY + 46f)),
                    new Vector2(tile, tile + 46f));

                GameObject frame = Solid(itemObject.transform, "Frame", XpTheme, new Vector2(0.5f, 1f), new Vector2(0f, 0f), new Vector2(tile, tile));
                GameObject icon = Sprite(frame.transform, "Icon", null, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(tile - 28f, tile - 28f));
                TMP_Text amount = Text(itemObject.transform, "AmountText", "0", new Vector2(0.5f, 1f),
                    new Vector2(0f, -tile - 2f), new Vector2(tile + 20f, 44f), 30f, TextAlignmentOptions.Center, Color.white, FontStyles.Bold);

                var button = frame.AddComponent<Button>();
                Image frameImage = frame.GetComponent<Image>();
                frameImage.raycastTarget = true;
                button.targetGraphic = frameImage;
                ColorBlock colors = button.colors;
                colors.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
                colors.fadeDuration = 0.05f;
                button.colors = colors;

                SerializedProperty item = items.GetArrayElementAtIndex(i);
                item.FindPropertyRelative("Root").objectReferenceValue = itemObject;
                item.FindPropertyRelative("Button").objectReferenceValue = button;
                item.FindPropertyRelative("Frame").objectReferenceValue = frameImage;
                item.FindPropertyRelative("Icon").objectReferenceValue = icon.GetComponent<Image>();
                item.FindPropertyRelative("AmountText").objectReferenceValue = amount;
                itemObject.SetActive(false);
            }
        }

        private static void BuildRewardPopup(Transform parent, SerializedObject so)
        {
            (GameObject popupRoot, CanvasGroup group) = PopupRoot(parent, "RewardDetailPopup");

            GameObject panel = Solid(popupRoot.transform, "Panel", PopupBody, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(760f, 520f));
            GameObject header = Solid(panel.transform, "Header", CoinTheme, new Vector2(0.5f, 1f), new Vector2(0f, -18f), new Vector2(620f, 96f));
            TMP_Text title = Text(header.transform, "Title", "Reward", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(580f, 70f),
                40f, TextAlignmentOptions.Center, Color.white, FontStyles.Bold);

            Button close = SpriteButton(panel.transform, "CloseButton", Load(PauseSpriteDir, "closebtn"), new Vector2(1f, 1f),
                new Vector2(30f, 30f), new Vector2(130f, 130f));

            GameObject iconHolder = Solid(panel.transform, "IconHolder", new Color(0.15f, 0.45f, 0.78f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -132f), new Vector2(620f, 200f));
            GameObject icon = Sprite(iconHolder.transform, "Icon", null, new Vector2(0.5f, 0.5f), new Vector2(-150f, 0f), new Vector2(150f, 150f));
            TMP_Text amount = Text(iconHolder.transform, "AmountText", "0", new Vector2(0.5f, 0.5f), new Vector2(70f, 0f),
                new Vector2(300f, 80f), 48f, TextAlignmentOptions.Left, Color.white, FontStyles.Bold);

            GameObject descHolder = Solid(panel.transform, "DescriptionHolder", new Color(0.13f, 0.41f, 0.72f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -348f), new Vector2(620f, 140f));
            TMP_Text description = Text(descHolder.transform, "Description", string.Empty, new Vector2(0.5f, 0.5f), Vector2.zero,
                new Vector2(580f, 120f), 28f, TextAlignmentOptions.Center, Color.white, FontStyles.Bold);

            so.FindProperty("_rewardPopup").objectReferenceValue = group;
            so.FindProperty("_rewardPopupPanel").objectReferenceValue = panel.GetComponent<RectTransform>();
            so.FindProperty("_rewardPopupHeader").objectReferenceValue = header.GetComponent<Image>();
            so.FindProperty("_rewardPopupTitle").objectReferenceValue = title;
            so.FindProperty("_rewardPopupIcon").objectReferenceValue = icon.GetComponent<Image>();
            so.FindProperty("_rewardPopupAmount").objectReferenceValue = amount;
            so.FindProperty("_rewardPopupDescription").objectReferenceValue = description;
            so.FindProperty("_rewardPopupClose").objectReferenceValue = close;
            popupRoot.SetActive(false);
        }

        private static void BuildStatisticsPopup(Transform parent, SerializedObject so)
        {
            (GameObject popupRoot, CanvasGroup group) = PopupRoot(parent, "DamageStatisticsPopup");

            GameObject panel = Solid(popupRoot.transform, "Panel", PopupBody, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(840f, 1100f));
            GameObject header = Solid(panel.transform, "Header", new Color(0.22f, 0.76f, 0.55f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -18f), new Vector2(680f, 96f));
            Text(header.transform, "Title", "Damage Statistics", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(640f, 70f),
                40f, TextAlignmentOptions.Center, Color.white, FontStyles.Bold);

            Button close = SpriteButton(panel.transform, "CloseButton", Load(PauseSpriteDir, "closebtn"), new Vector2(1f, 1f),
                new Vector2(30f, 30f), new Vector2(130f, 130f));

            GameObject totalHolder = Solid(panel.transform, "TotalHolder", new Color(0.15f, 0.45f, 0.78f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -132f), new Vector2(760f, 84f));
            TMP_Text total = Text(totalHolder.transform, "TotalText", "All Damage: 0", new Vector2(0.5f, 0.5f), Vector2.zero,
                new Vector2(720f, 70f), 34f, TextAlignmentOptions.Center, Color.white, FontStyles.Bold);

            // Scrollable list so a level with many towers still fits.
            var scrollObject = new GameObject("StatScroll", typeof(RectTransform), typeof(ScrollRect));
            scrollObject.transform.SetParent(panel.transform, false);
            Place(scrollObject.GetComponent<RectTransform>(), new Vector2(0.5f, 1f), new Vector2(0f, -230f), new Vector2(760f, 820f));
            var scroll = scrollObject.GetComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 28f;

            GameObject viewport = Solid(scrollObject.transform, "Viewport", new Color(0.09f, 0.3f, 0.56f, 0.6f), Vector2.zero, Vector2.zero, Vector2.zero);
            Stretch(viewport.GetComponent<RectTransform>());
            viewport.AddComponent<Mask>().showMaskGraphic = true;

            const float rowHeight = 92f;
            var content = new GameObject("Content", typeof(RectTransform));
            content.transform.SetParent(viewport.transform, false);
            var contentRect = content.GetComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.pivot = new Vector2(0.5f, 1f);
            contentRect.anchoredPosition = Vector2.zero;
            contentRect.sizeDelta = new Vector2(0f, StatRowCount * rowHeight + 16f);
            scroll.viewport = viewport.GetComponent<RectTransform>();
            scroll.content = contentRect;

            SerializedProperty rows = so.FindProperty("_statRows");
            rows.arraySize = StatRowCount;
            for (int i = 0; i < StatRowCount; i++)
            {
                var rowObject = new GameObject("StatRow_0" + (i + 1), typeof(RectTransform));
                rowObject.transform.SetParent(content.transform, false);
                Place(rowObject.GetComponent<RectTransform>(), new Vector2(0.5f, 1f), new Vector2(0f, -8f - i * rowHeight), new Vector2(720f, rowHeight - 8f));
                Solid(rowObject.transform, "Background", RowTint, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(720f, rowHeight - 8f));

                GameObject icon = Solid(rowObject.transform, "Icon", Color.white, new Vector2(0f, 0.5f), new Vector2(10f, 0f), new Vector2(62f, 62f));
                TMP_Text nameText = Text(rowObject.transform, "NameText", "-", new Vector2(0f, 1f), new Vector2(84f, -4f),
                    new Vector2(340f, 36f), 28f, TextAlignmentOptions.Left, Color.white, FontStyles.Bold);
                TMP_Text valueText = Text(rowObject.transform, "ValueText", "0", new Vector2(1f, 1f), new Vector2(-10f, -4f),
                    new Vector2(180f, 36f), 28f, TextAlignmentOptions.Right, Color.white, FontStyles.Bold);

                Solid(rowObject.transform, "BarTrack", BarTrack, new Vector2(0f, 0f), new Vector2(84f, 8f), new Vector2(430f, 22f));
                GameObject bar = Solid(rowObject.transform, "Bar", BarColor, new Vector2(0f, 0f), new Vector2(84f, 8f), new Vector2(430f, 22f));
                Image barImage = MakeFilled(bar);
                TMP_Text percent = Text(rowObject.transform, "PercentText", "0%", new Vector2(0f, 0f), new Vector2(84f, 8f),
                    new Vector2(430f, 22f), 20f, TextAlignmentOptions.Center, Color.white, FontStyles.Bold);

                SerializedProperty row = rows.GetArrayElementAtIndex(i);
                row.FindPropertyRelative("Root").objectReferenceValue = rowObject;
                row.FindPropertyRelative("Icon").objectReferenceValue = icon.GetComponent<Image>();
                row.FindPropertyRelative("NameText").objectReferenceValue = nameText;
                row.FindPropertyRelative("PercentText").objectReferenceValue = percent;
                row.FindPropertyRelative("ValueText").objectReferenceValue = valueText;
                row.FindPropertyRelative("Bar").objectReferenceValue = barImage;
                rowObject.SetActive(false);
            }

            so.FindProperty("_statsPopup").objectReferenceValue = group;
            so.FindProperty("_statsPopupPanel").objectReferenceValue = panel.GetComponent<RectTransform>();
            so.FindProperty("_statsTotalText").objectReferenceValue = total;
            so.FindProperty("_statsPopupClose").objectReferenceValue = close;
            popupRoot.SetActive(false);
        }

        /// <summary>A full-screen dimmer plus CanvasGroup: while it is on it eats the taps meant for the panel behind.</summary>
        private static (GameObject, CanvasGroup) PopupRoot(Transform parent, string name)
        {
            var popupRoot = new GameObject(name, typeof(RectTransform), typeof(CanvasGroup));
            popupRoot.transform.SetParent(parent, false);
            Stretch(popupRoot.GetComponent<RectTransform>());
            GameObject shade = Solid(popupRoot.transform, "Shade", new Color(0f, 0f, 0f, 0.55f), Vector2.zero, Vector2.zero, Vector2.zero);
            Stretch(shade.GetComponent<RectTransform>());
            shade.GetComponent<Image>().raycastTarget = true;
            return (popupRoot, popupRoot.GetComponent<CanvasGroup>());
        }

        // ------------------------------------------------------------------------------------------------------------
        // Catalog + generated art
        // ------------------------------------------------------------------------------------------------------------

        private static VictoryRewardCatalog EnsureCatalog()
        {
            EnsureFolder(Path.GetDirectoryName(CatalogPath).Replace('\\', '/'));
            var catalog = AssetDatabase.LoadAssetAtPath<VictoryRewardCatalog>(CatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<VictoryRewardCatalog>();
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }

            var entries = new List<VictoryRewardCatalog.Entry>
            {
                new VictoryRewardCatalog.Entry
                {
                    Type = VictoryRewardType.Coins,
                    DisplayName = "Coins",
                    Description = "Basic currency for essential items and upgrades",
                    Icon = Load(MenuIconDir, "coinicon"),
                    HeaderColor = CoinTheme,
                },
                new VictoryRewardCatalog.Entry
                {
                    Type = VictoryRewardType.Gems,
                    DisplayName = "Gems",
                    Description = "Premium currency earned by clearing a level with three stars",
                    Icon = Load(MenuIconDir, "diamondicon"),
                    HeaderColor = GemTheme,
                },
                new VictoryRewardCatalog.Entry
                {
                    Type = VictoryRewardType.Experience,
                    DisplayName = "Experience",
                    Description = "Experience collected during this level",
                    Icon = Load(MenuIconDir, "lightningicon"),
                    HeaderColor = XpTheme,
                },
            };

            var so = new SerializedObject(catalog);
            SerializedProperty array = so.FindProperty("_entries");
            array.arraySize = entries.Count;
            for (int i = 0; i < entries.Count; i++)
            {
                SerializedProperty entry = array.GetArrayElementAtIndex(i);
                entry.FindPropertyRelative("Type").enumValueIndex = (int)entries[i].Type;
                entry.FindPropertyRelative("DisplayName").stringValue = entries[i].DisplayName;
                entry.FindPropertyRelative("Description").stringValue = entries[i].Description;
                entry.FindPropertyRelative("Icon").objectReferenceValue = entries[i].Icon;
                entry.FindPropertyRelative("HeaderColor").colorValue = entries[i].HeaderColor;
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(catalog);
            return catalog;
        }

        /// <summary>Soft radial glow, generated once so the victory burst needs no new art and no particle system.</summary>
        private static Sprite LoadOrCreateGlow()
        {
            const string path = GeneratedTextureDir + "/T_VictoryGlow.png";
            var existing = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (existing != null)
            {
                return existing;
            }

            const int size = 256;
            var pixels = new Color32[size * size];
            float radius = size * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float distance = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(radius, radius)) / radius;
                    float alpha = Mathf.Clamp01(1f - distance);
                    alpha = alpha * alpha * alpha; // soft falloff
                    pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
                }
            }

            return WriteSprite(path, size, size, pixels);
        }

        /// <summary>Angular light rays behind the banner - same idea, generated once.</summary>
        private static Sprite LoadOrCreateRays()
        {
            const string path = GeneratedTextureDir + "/T_VictoryRays.png";
            var existing = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (existing != null)
            {
                return existing;
            }

            const int size = 256;
            const int rayCount = 16;
            var pixels = new Color32[size * size];
            float radius = size * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    var offset = new Vector2(x + 0.5f - radius, y + 0.5f - radius);
                    float distance = offset.magnitude / radius;
                    float angle = Mathf.Atan2(offset.y, offset.x);
                    float wedge = Mathf.Abs(Mathf.Sin(angle * rayCount * 0.5f));
                    float alpha = Mathf.Clamp01(1f - distance) * Mathf.Pow(wedge, 3f) * 0.9f;
                    pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
                }
            }

            return WriteSprite(path, size, size, pixels);
        }

        private static Sprite WriteSprite(string path, int width, int height, Color32[] pixels)
        {
            EnsureFolder(GeneratedTextureDir);
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            texture.SetPixels32(pixels);
            texture.Apply();
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single; // the project preset defaults to Multiple
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        // ------------------------------------------------------------------------------------------------------------

        private static Transform FindPanelParent(GameStateUIController controller)
        {
            foreach (Canvas canvas in Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (canvas.renderMode == RenderMode.WorldSpace)
                {
                    continue;
                }

                Transform found = canvas.transform.Find("SafeArea");
                if (found != null)
                {
                    return found;
                }
            }

            return controller.transform.parent;
        }

        private static Sprite Load(string directory, string fileName)
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{directory}/{fileName}.png");
            if (sprite == null)
            {
                Debug.LogWarning("[VictoryPanelBuilder] Missing sprite " + fileName);
            }

            return sprite;
        }

        private static Sprite BuiltInSprite()
        {
            return AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        }

        private static Image MakeFilled(GameObject target)
        {
            var image = target.GetComponent<Image>();
            image.type = Image.Type.Filled;
            image.fillMethod = Image.FillMethod.Horizontal;
            image.fillOrigin = (int)Image.OriginHorizontal.Left;
            image.fillAmount = 0f;
            return image;
        }

        private static GameObject Sprite(Transform parent, string name, Sprite sprite, Vector2 anchor, Vector2 anchoredPosition, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            Place(go.GetComponent<RectTransform>(), anchor, anchoredPosition, size);
            var image = go.GetComponent<Image>();
            image.sprite = sprite;
            image.enabled = sprite != null;
            image.raycastTarget = false;
            return go;
        }

        private static GameObject Solid(Transform parent, string name, Color color, Vector2 anchor, Vector2 anchoredPosition, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            Place(go.GetComponent<RectTransform>(), anchor, anchoredPosition, size);
            var image = go.GetComponent<Image>();
            image.sprite = BuiltInSprite();
            image.type = Image.Type.Sliced;
            image.color = color;
            image.raycastTarget = false;
            return go;
        }

        private static Button SpriteButton(Transform parent, string name, Sprite sprite, Vector2 anchor, Vector2 anchoredPosition, Vector2 size)
        {
            GameObject go = Sprite(parent, name, sprite, anchor, anchoredPosition, size);
            var image = go.GetComponent<Image>();
            image.enabled = true;
            image.raycastTarget = true;
            return AddButton(go, image);
        }

        private static Button LabelButton(Transform parent, string name, string label, Color color, Vector2 anchor,
            Vector2 anchoredPosition, Vector2 size, float fontSize)
        {
            GameObject go = Solid(parent, name, color, anchor, anchoredPosition, size);
            var image = go.GetComponent<Image>();
            image.raycastTarget = true;
            Text(go.transform, "Label", label, new Vector2(0.5f, 0.5f), Vector2.zero, size, fontSize,
                TextAlignmentOptions.Center, Color.white, FontStyles.Bold);
            return AddButton(go, image);
        }

        private static Button AddButton(GameObject go, Image image)
        {
            var button = go.AddComponent<Button>();
            button.targetGraphic = image;
            ColorBlock colors = button.colors;
            colors.pressedColor = new Color(0.82f, 0.82f, 0.82f, 1f);
            colors.fadeDuration = 0.05f;
            button.colors = colors;
            return button;
        }

        private static TMP_Text Text(Transform parent, string name, string content, Vector2 anchor, Vector2 anchoredPosition,
            Vector2 size, float fontSize, TextAlignmentOptions alignment, Color color, FontStyles style = FontStyles.Normal)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            Place(go.GetComponent<RectTransform>(), anchor, anchoredPosition, size);
            var text = go.AddComponent<TextMeshProUGUI>();
            text.text = content;
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = color;
            text.fontStyle = style;
            text.raycastTarget = false;
            return text;
        }

        private static void Place(RectTransform rect, Vector2 anchor, Vector2 anchoredPosition, Vector2 size)
        {
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = anchor;
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = Vector2.zero;
        }

        private static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder))
            {
                return;
            }

            string parent = Path.GetDirectoryName(folder).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
        }
    }
}
