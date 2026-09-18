using AlienDefense.Core;
using AlienDefense.Progression;
using AlienDefense.UI;
using AlienDefense.UI.MainMenu;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace AlienDefense.EditorTools
{
    /// <summary>Builds the full MainMenu scene: a single Screen Space Overlay Canvas carrying the background,
    /// TopHUD, feature rails, the center level-selection/preview area (map art + level-circle glow ring +
    /// muiten Prev/Next arrows, all plain UI Images — see BuildLevelPreviewBackdrop), Play button, Quest banner
    /// placeholder, and Bottom Navigation. Everything lives in this one Canvas on purpose: an earlier version put
    /// the level preview in a separate 3D world/camera layer behind the Canvas, which can never actually be seen
    /// since Screen Space Overlay always composites on top of every camera's render and BackgroundOverlay is a
    /// full-screen opaque image — see BuildLevelPreviewBackdrop's doc comment. Every widget with no backing
    /// system yet (Player Level/XP, lobby Energy, Gems, feature rail cards, Daily/FreeReward, Quest) is wired but
    /// left in its disabled placeholder state by MainMenuPresenter/MainMenuResourcePresenter/
    /// BottomNavigationPresenter at runtime — see the MainMenu audit report. All colors/icons here are flat
    /// placeholders for hand-authored art later, except the center level preview (Map1/2/3 + level-circle +
    /// muiten arrows), which now uses the real hand-authored sprites.</summary>
    internal static class MainMenuSceneScaffolder
    {
        private const string SceneFolder = "Assets/_Game/Scenes/Menu";
        private const string ScenePath = SceneFolder + "/MainMenu.unity";

        private static readonly Color PanelColor = new Color(0.08f, 0.12f, 0.22f, 0.85f);
        private static readonly Color AccentGreen = new Color(0.35f, 0.85f, 0.45f, 1f);
        private static readonly Color AccentOrange = new Color(0.95f, 0.6f, 0.15f, 1f);
        private static readonly Color AccentCyan = new Color(0.3f, 0.8f, 0.95f, 1f);
        private static readonly Color BackgroundColor = new Color(0.04f, 0.08f, 0.16f, 1f);

        // TopHUD resource-pill palette — no real icon art for these yet (avatar/lightning/gem/coin), so pills use
        // a colored circle + letter glyph placeholder; swap ResourceWidget's Icon.sprite for real art later and
        // this whole section keeps working unchanged.
        private static readonly Color PillColor = new Color(0.05f, 0.05f, 0.08f, 0.92f);
        private static readonly Color AccentYellow = new Color(0.95f, 0.85f, 0.15f, 1f);
        private static readonly Color AccentPurple = new Color(0.75f, 0.35f, 0.95f, 1f);
        private static readonly Color AccentGold = new Color(0.95f, 0.75f, 0.25f, 1f);

        [MenuItem("AlienDefense/Setup/13. Build MainMenu Scene")]
        public static void BuildMainMenuScene()
        {
            EditorFolderUtility.EnsureFolder(SceneFolder);

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var compositionRootObject = new GameObject("MainMenuCompositionRoot");

            BuildEnvironment();

            (GameObject canvasObject, Transform safeArea) = EditorCanvasUtility.BuildCanvasWithSafeArea("Canvas");
            BuildBackgroundOverlay(safeArea);

            EditorMenuShellLayout.ShellRegions shellRegions = EditorMenuShellLayout.BuildShellRegions(safeArea);

            GameObject playPanel = EditorMenuShellLayout.CreateTabPanel(shellRegions.MenuContentRoot, "PlayPanel", true);
            EditorMenuShellLayout.CreatePlayRow(
                playPanel.transform,
                out RectTransform centerColumn,
                out RectTransform leftRailSlot,
                out RectTransform rightRailSlot);

            (PlayerProfileWidgetView profileWidget, ResourceWidgetView energyWidget, ResourceWidgetView gemWidget, ResourceWidgetView coinWidget) = BuildTopHud(shellRegions.TopHud);
            energyWidget.gameObject.SetActive(false);

            FeatureButtonView[] leftButtons = BuildLeftFeatureRail(leftRailSlot);
            leftButtons[1].gameObject.SetActive(false);
            leftButtons[2].gameObject.SetActive(false);
            (FeatureButtonView dailyButton, FeatureButtonView freeRewardButton) = BuildRightFeatureRail(rightRailSlot);
            (MainMenuLevelSelectionView levelSelectionView, LevelObjectivePanelView objectivesPanel, LevelPreviewView previewView) = BuildLevelSelectionArea(centerColumn);
            PlayButtonView playButtonView = BuildPlayArea(centerColumn);
            QuestBannerView questBanner = BuildQuestBanner(centerColumn);
            BottomNavigationView bottomNavigation = BuildBottomNavigation(shellRegions.BottomNavigation);

            MenuPanelBuilders.ShopPanelBuildResult shopPanel = MenuPanelBuilders.BuildShopPanel(shellRegions.MenuContentRoot);
            MenuPanelBuilders.UpgradePanelBuildResult upgradePanel = MenuPanelBuilders.BuildUpgradePanel(shellRegions.MenuContentRoot);
            MenuPanelBuilders.DefensePanelBuildResult defensePanel = MenuPanelBuilders.BuildDefensePanel(shellRegions.MenuContentRoot);
            MenuPanelBuilders.BasePanelBuildResult basePanel = MenuPanelBuilders.BuildBasePanel(shellRegions.MenuContentRoot);

            DailyRewardPanelView dailyRewardPanel = BuildDailyRewardPanel(safeArea);
            VipPanelView vipPanel = BuildVipPanel(safeArea);

            LevelSceneScaffolder.BuildEventSystem();

            WireCompositionRoot(
                compositionRootObject,
                canvasObject,
                previewView,
                profileWidget, energyWidget, gemWidget, coinWidget,
                leftButtons, dailyButton, freeRewardButton,
                levelSelectionView, playButtonView,
                questBanner, bottomNavigation,
                dailyRewardPanel, vipPanel,
                playPanel, shopPanel, upgradePanel, defensePanel, basePanel);

            ProfilePanelBuilder.BuildProfileOverlay(
                safeArea,
                compositionRootObject,
                shellRegions.MenuContentRoot.gameObject,
                shellRegions.TopHud.gameObject);

            var mainMenuPresenter = compositionRootObject.GetComponent<MainMenuPresenter>();
            var profilePresenter = compositionRootObject.GetComponent<AlienDefense.UI.Profile.ProfilePresenter>();
            if (mainMenuPresenter != null && profilePresenter != null)
            {
                var serializedMain = new SerializedObject(mainMenuPresenter);
                serializedMain.FindProperty("_profilePresenter").objectReferenceValue = profilePresenter;
                serializedMain.ApplyModifiedPropertiesWithoutUndo();
            }

            SceneServicesHostScaffolder.EnsureInActiveScene(includeSaveDebugControls: true);

            EditorSceneManager.SaveScene(scene, ScenePath);
            Debug.Log("[AlienDefense Setup] Saved MainMenu scene to " + ScenePath + ".");
        }

        // ------------------------------------------------------------------ Environment

        private static void BuildEnvironment()
        {
            var cameraObject = new GameObject("MainCamera", typeof(Camera));
            Camera mainCamera = cameraObject.GetComponent<Camera>();
            mainCamera.tag = "MainCamera";
            mainCamera.clearFlags = CameraClearFlags.SolidColor;
            mainCamera.backgroundColor = BackgroundColor;
            mainCamera.orthographic = false;
            mainCamera.fieldOfView = 45f;
            mainCamera.nearClipPlane = 0.1f;
            mainCamera.farClipPlane = 60f;
            mainCamera.cullingMask = 1 << LayerMask.NameToLayer("Default");
            cameraObject.transform.SetPositionAndRotation(new Vector3(0f, 6f, -9f), Quaternion.Euler(28f, 0f, 0f));
            cameraObject.AddComponent<AudioListener>();
        }

        /// <summary>Builds the level preview backdrop as plain UI Images: "level-circle" glow ring behind a
        /// persistent map-art image (MapImage + LockOverlay + LockIcon, all under PreviewGroup so
        /// LevelPreviewView can scale them together for the swap-in pop) — the FIRST child of LevelSelectionArea
        /// with LayoutElement.ignoreLayout so it sits as a full-bleed backdrop BEHIND the title/status/arrows/
        /// objectives that VerticalLayoutGroup lays out normally on top of it.
        ///
        /// This intentionally does NOT live in a separate 3D world/camera layer like the old diorama system did:
        /// the Canvas here is Screen Space Overlay, which always composites on top of every camera's render, and
        /// BackgroundOverlay is a full-screen opaque image, so anything placed behind the Canvas is permanently
        /// invisible no matter the camera/culling-mask setup. Plain UI Images inside the same Canvas render
        /// correctly like every other MainMenu widget.</summary>
        private static (LevelPreviewView previewView, Button previousButton, Button nextButton) BuildLevelPreviewBackdrop(Transform area)
        {
            var backdrop = new GameObject("PreviewBackdrop", typeof(RectTransform), typeof(LayoutElement));
            backdrop.transform.SetParent(area, false);
            backdrop.transform.SetAsFirstSibling();
            backdrop.GetComponent<LayoutElement>().ignoreLayout = true;
            RectTransform backdropRect = backdrop.GetComponent<RectTransform>();
            backdropRect.anchorMin = Vector2.zero;
            backdropRect.anchorMax = Vector2.one;
            backdropRect.offsetMin = Vector2.zero;
            backdropRect.offsetMax = Vector2.zero;

            const float glowSize = 760f;
            Image glowImage = BuildAnchoredImage(backdrop.transform, "PortalGlow", new Vector2(0.5f, 0.5f), new Vector2(glowSize, glowSize), Vector2.zero, Color.white);
            glowImage.sprite = LoadLevelSprite("level-circle.png");
            glowImage.raycastTarget = false;
            glowImage.gameObject.AddComponent<PortalPulseAnimation>();

            var groupObject = new GameObject("PreviewGroup", typeof(RectTransform));
            groupObject.transform.SetParent(backdrop.transform, false); // added after glow -> draws on top
            RectTransform groupRect = groupObject.GetComponent<RectTransform>();
            groupRect.anchorMin = new Vector2(0.5f, 0.5f);
            groupRect.anchorMax = new Vector2(0.5f, 0.5f);
            groupRect.pivot = new Vector2(0.5f, 0.5f);
            groupRect.anchoredPosition = Vector2.zero;
            groupRect.sizeDelta = Vector2.zero;

            const float mapSize = 620f;
            Image mapImage = BuildAnchoredImage(groupObject.transform, "MapImage", new Vector2(0.5f, 0.5f), new Vector2(mapSize, mapSize), Vector2.zero, Color.white);
            mapImage.preserveAspect = true;
            mapImage.raycastTarget = false;

            const float lockIconSize = 240f;
            Image lockIconImage = BuildAnchoredImage(mapImage.transform, "LockIcon", new Vector2(0.5f, 0.5f), new Vector2(lockIconSize, lockIconSize), Vector2.zero, Color.white);
            lockIconImage.sprite = LoadLevelSprite("LockIcon.png");
            lockIconImage.preserveAspect = true;
            lockIconImage.raycastTarget = false;
            lockIconImage.gameObject.SetActive(false);

            // Prev/Next sit ON the ring's left/right edge, vertically centered on it (backdrop's own center IS
            // the ring's center, since PortalGlow above is anchored at backdrop-center with no offset) — matches
            // the reference composition's large arrows flanking the ring, not the old small in-flow row.
            float arrowOffsetX = (glowSize / 2f) - 20f;
            Button previousButton = BuildArrowButton(backdrop.transform, "PreviousButton", mirror: true);
            PositionArrowButton(previousButton.GetComponent<RectTransform>(), -arrowOffsetX);
            Button nextButton = BuildArrowButton(backdrop.transform, "NextButton", mirror: false);
            PositionArrowButton(nextButton.GetComponent<RectTransform>(), arrowOffsetX);

            var previewView = backdrop.AddComponent<LevelPreviewView>();
            var serializedView = new SerializedObject(previewView);
            serializedView.FindProperty("_previewGroup").objectReferenceValue = groupRect;
            serializedView.FindProperty("_mapImage").objectReferenceValue = mapImage;
            serializedView.FindProperty("_lockIconImage").objectReferenceValue = lockIconImage;
            serializedView.ApplyModifiedPropertiesWithoutUndo();

            return (previewView, previousButton, nextButton);
        }

        private static void PositionArrowButton(RectTransform rect, float anchoredX)
        {
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(anchoredX, 0f);
        }

        // ------------------------------------------------------------------ Background

        private static void BuildBackgroundOverlay(Transform safeArea)
        {
            // Parented to the Canvas (not SafeArea) so it truly covers the full screen, notch included.
            Image background = BuildFullScreenImage(safeArea.parent, "BackgroundOverlay", Color.white);
            background.sprite = LoadBottomNavSprite("BG/bg.png");
            background.preserveAspect = false; // fill the whole screen, cropping/stretching as needed
            background.transform.SetAsFirstSibling();
        }

        // ------------------------------------------------------------------ TopHUD

        private static (PlayerProfileWidgetView profile, ResourceWidgetView energy, ResourceWidgetView gem, ResourceWidgetView coin) BuildTopHud(Transform safeArea)
        {
            var topHud = new GameObject("TopHUD", typeof(RectTransform));
            topHud.transform.SetParent(safeArea, false);
            RectTransform topHudRect = topHud.GetComponent<RectTransform>();
            topHudRect.anchorMin = new Vector2(0f, 1f);
            topHudRect.anchorMax = new Vector2(1f, 1f);
            topHudRect.pivot = new Vector2(0.5f, 1f);
            topHudRect.anchoredPosition = new Vector2(0f, -24f);
            topHudRect.sizeDelta = new Vector2(-48f, 140f);

            PlayerProfileWidgetView profileWidget = BuildPlayerProfileWidget(topHud.transform);

            // Reference composition: a single HORIZONTAL row of 3 pills (Energy, Gem, Coin) on the right — not a
            // vertical stack, which is what made the old layout read as "all over the place".
            var resourceRow = new GameObject("ResourceRow", typeof(RectTransform));
            resourceRow.transform.SetParent(topHud.transform, false);
            RectTransform resourceRowRect = resourceRow.GetComponent<RectTransform>();
            resourceRowRect.anchorMin = new Vector2(1f, 1f);
            resourceRowRect.anchorMax = new Vector2(1f, 1f);
            resourceRowRect.pivot = new Vector2(1f, 1f);
            resourceRowRect.anchoredPosition = new Vector2(0f, -10f);
            resourceRowRect.sizeDelta = new Vector2(800f, 64f);
            var resourceLayout = resourceRow.AddComponent<HorizontalLayoutGroup>();
            resourceLayout.spacing = 20f;
            resourceLayout.childAlignment = TextAnchor.MiddleRight;
            resourceLayout.childForceExpandWidth = false;
            resourceLayout.childForceExpandHeight = false;
            resourceLayout.childControlWidth = false;
            resourceLayout.childControlHeight = false;

            ResourceWidgetView energyWidget = BuildResourceWidget(resourceRow.transform, "EnergyWidget", "E", AccentYellow);
            ResourceWidgetView gemWidget = BuildResourceWidget(resourceRow.transform, "GemWidget", "G", AccentPurple);
            ResourceWidgetView coinWidget = BuildResourceWidget(resourceRow.transform, "CoinWidget", "C", AccentGold);

            return (profileWidget, energyWidget, gemWidget, coinWidget);
        }

        private static PlayerProfileWidgetView BuildPlayerProfileWidget(Transform parent)
        {
            // Every child below is anchored/pivoted at its own top-left corner (0,1)-(0,1)-(0,1), so
            // anchoredPosition always simply reads as "(right, down) from the widget's top-left corner" — no
            // mixed center/corner pivot arithmetic to trip over.
            var widget = new GameObject("PlayerProfileWidget", typeof(RectTransform));
            widget.transform.SetParent(parent, false);
            RectTransform rect = widget.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(420f, 140f);
            // No background panel — the reference shows avatar/level/power floating directly on the game
            // background, not boxed inside a panel.

            Sprite circleSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
            Sprite roundedRectSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");

            const float avatarSize = 100f;

            // Avatar, top-left corner of the widget, with the red "!" badge overlapping its top-right corner.
            Image avatar = BuildTopLeftImage(widget.transform, "AvatarImage", Vector2.zero, new Vector2(avatarSize, avatarSize), new Color(0.3f, 0.5f, 0.6f, 1f), roundedRectSprite);
            avatar.raycastTarget = true;
            Button avatarButton = avatar.gameObject.AddComponent<Button>();
            NotificationBadgeView avatarBadge = BuildNotificationBadge(avatar.transform, new Vector2(1f, 1f), new Vector2(4f, 4f));

            // Level number + XP bar + an extra (settings/customize) button, one row level with the avatar's
            // upper half.
            const float rowX = avatarSize + 14f;
            const float rowTop = 6f;
            TMP_Text levelText = BuildTopLeftText(widget.transform, "PlayerLevelText", "1", new Vector2(rowX, rowTop), new Vector2(34f, 40f), 28f, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);

            const float barX = rowX + 40f;
            Image xpFill = BuildTopLeftBarFill(widget.transform, "XPProgress", new Vector2(barX, rowTop + 8f), new Vector2(148f, 24f), AccentGreen, roundedRectSprite);

            var extraButtonObject = new GameObject("ProfileExtraButton", typeof(RectTransform), typeof(Image), typeof(Button));
            extraButtonObject.transform.SetParent(widget.transform, false);
            RectTransform extraButtonRect = extraButtonObject.GetComponent<RectTransform>();
            extraButtonRect.anchorMin = new Vector2(0f, 1f);
            extraButtonRect.anchorMax = new Vector2(0f, 1f);
            extraButtonRect.pivot = new Vector2(0f, 1f);
            extraButtonRect.anchoredPosition = new Vector2(barX + 148f + 12f, rowTop);
            extraButtonRect.sizeDelta = new Vector2(40f, 40f);
            Image extraButtonImage = extraButtonObject.GetComponent<Image>();
            extraButtonImage.sprite = roundedRectSprite;
            extraButtonImage.color = PanelColor;

            // Power/score row, directly under the avatar.
            const float powerRowY = avatarSize + 8f;
            Image powerIcon = BuildTopLeftImage(widget.transform, "PowerIcon", new Vector2(0f, powerRowY), new Vector2(30f, 30f), AccentGreen, circleSprite);
            TMP_Text secondaryText = BuildTopLeftText(widget.transform, "SecondaryProgressText", "—", new Vector2(38f, powerRowY + 2f), new Vector2(220f, 32f), 24f, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
            secondaryText.color = AccentYellow;

            var view = widget.AddComponent<PlayerProfileWidgetView>();
            var serialized = new SerializedObject(view);
            serialized.FindProperty("_avatarImage").objectReferenceValue = avatar;
            serialized.FindProperty("_avatarButton").objectReferenceValue = avatarButton;
            serialized.FindProperty("_extraButton").objectReferenceValue = extraButtonObject.GetComponent<Button>();
            serialized.FindProperty("_playerLevelText").objectReferenceValue = levelText;
            serialized.FindProperty("_xpFillImage").objectReferenceValue = xpFill;
            serialized.FindProperty("_secondaryProgressText").objectReferenceValue = secondaryText;
            serialized.FindProperty("_notificationBadge").objectReferenceValue = avatarBadge;
            serialized.FindProperty("_canvasGroup").objectReferenceValue = widget.AddComponent<CanvasGroup>();
            serialized.ApplyModifiedPropertiesWithoutUndo();

            return view;
        }

        /// <summary>One TopHUD resource pill: a dark rounded-rect background, a colored circular icon (no real
        /// icon art yet — see the AccentYellow/Purple/Gold doc comment — overlapping the pill's left edge like
        /// the reference composition), a small green "+" button on the icon's corner, and the amount to the
        /// right. No real sprites exist yet for Energy/Gem/Coin — iconGlyph is a plain letter placeholder inside
        /// the circle; swap Icon.sprite for real art later without touching layout.</summary>
        private static ResourceWidgetView BuildResourceWidget(Transform parent, string name, string iconGlyph, Color iconColor)
        {
            const float pillWidth = 168f;
            const float pillHeight = 60f;
            const float iconSize = 68f;

            Sprite circleSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
            Sprite roundedRectSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");

            var widget = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            widget.transform.SetParent(parent, false);
            RectTransform widgetRect = widget.GetComponent<RectTransform>();
            // childControlWidth/Height are false on the parent HorizontalLayoutGroup (fixed pill sizes, not
            // stretched) — that mode positions each child in sequence using its OWN current RectTransform size,
            // it does NOT read LayoutElement.preferred* for that. Setting this explicitly (instead of leaving it
            // at the default-new-RectTransform 100x100) is what the layout math actually places pills by.
            widgetRect.anchorMin = new Vector2(0f, 0.5f);
            widgetRect.anchorMax = new Vector2(0f, 0.5f);
            widgetRect.pivot = new Vector2(0f, 0.5f);
            widgetRect.sizeDelta = new Vector2(pillWidth, pillHeight);
            Image pillImage = widget.GetComponent<Image>();
            pillImage.sprite = roundedRectSprite;
            pillImage.color = PillColor;
            var layoutElement = widget.GetComponent<LayoutElement>();
            layoutElement.preferredWidth = pillWidth;
            layoutElement.preferredHeight = pillHeight;

            // Icon centered ON the pill's left edge (half hanging outside it) — built manually (not via
            // BuildAnchoredImage, which would put the pivot at a corner) so it can be centered exactly on that
            // edge point.
            var iconObject = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            iconObject.transform.SetParent(widget.transform, false);
            RectTransform iconRect = iconObject.GetComponent<RectTransform>();
            iconRect.anchorMin = new Vector2(0f, 0.5f);
            iconRect.anchorMax = new Vector2(0f, 0.5f);
            iconRect.pivot = new Vector2(0.5f, 0.5f);
            iconRect.anchoredPosition = Vector2.zero;
            iconRect.sizeDelta = new Vector2(iconSize, iconSize);
            Image icon = iconObject.GetComponent<Image>();
            icon.sprite = circleSprite;
            icon.color = iconColor;

            TMP_Text iconGlyphText = BuildAnchoredText(iconObject.transform, "IconGlyph", iconGlyph, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(iconSize, iconSize), 24f, TextAlignmentOptions.Center);
            iconGlyphText.color = Color.white;
            iconGlyphText.fontStyle = FontStyles.Bold;

            Button addButton = BuildSmallCircleButton(iconObject.transform, "AddButton", "+", AccentGreen, new Vector2(1f, 1f), new Vector2(2f, 2f));
            addButton.GetComponent<RectTransform>().sizeDelta = new Vector2(26f, 26f);

            // Amount text fills the pill from just past the icon to its right edge.
            TMP_Text amount = BuildTopLeftText(widget.transform, "AmountText", "0", new Vector2(iconSize / 2f + 12f, 0f), new Vector2(pillWidth - iconSize / 2f - 24f, pillHeight), 26f, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);

            var view = widget.AddComponent<ResourceWidgetView>();
            var serialized = new SerializedObject(view);
            serialized.FindProperty("_amountText").objectReferenceValue = amount;
            serialized.FindProperty("_addButton").objectReferenceValue = addButton;
            serialized.FindProperty("_canvasGroup").objectReferenceValue = widget.AddComponent<CanvasGroup>();
            serialized.ApplyModifiedPropertiesWithoutUndo();

            return view;
        }

        // ------------------------------------------------------------------ Feature rails

        private static FeatureButtonView[] BuildLeftFeatureRail(Transform parent)
        {
            Transform rail = BuildVerticalRail(parent, "LeftFeatureRail", Vector2.zero, Vector2.one, 0f);

            var buttons = new FeatureButtonView[3];
            buttons[0] = BuildFeatureButton(rail, "FeatureButton_01", new Color(0.6f, 0.4f, 0.9f));
            buttons[1] = BuildFeatureButton(rail, "FeatureButton_02", new Color(0.3f, 0.6f, 0.9f));
            buttons[2] = BuildFeatureButton(rail, "FeatureButton_03", new Color(0.9f, 0.6f, 0.3f));
            return buttons;
        }

        private static (FeatureButtonView daily, FeatureButtonView freeReward) BuildRightFeatureRail(Transform parent)
        {
            Transform rail = BuildVerticalRail(parent, "RightFeatureRail", Vector2.zero, Vector2.one, 0f);

            FeatureButtonView dailyButton = BuildFeatureButton(rail, "DailyButton", new Color(0.9f, 0.3f, 0.4f));
            FeatureButtonView freeRewardButton = BuildFeatureButton(rail, "FreeRewardButton", new Color(0.95f, 0.75f, 0.2f));
            return (dailyButton, freeRewardButton);
        }

        private static Transform BuildVerticalRail(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, float xOffset)
        {
            var rail = new GameObject(name, typeof(RectTransform));
            rail.transform.SetParent(parent, false);
            RectTransform rect = rail.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(xOffset, 0f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var layout = rail.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 16f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            layout.childControlWidth = true;
            layout.childControlHeight = false;

            return rail.transform;
        }

        private static FeatureButtonView BuildFeatureButton(Transform parent, string name, Color color)
        {
            var buttonObject = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
            buttonObject.transform.SetParent(parent, false);
            buttonObject.GetComponent<Image>().color = color;
            var layoutElement = buttonObject.GetComponent<LayoutElement>();
            layoutElement.preferredHeight = 96f;
            layoutElement.minHeight = 96f;

            Image icon = BuildAnchoredImage(buttonObject.transform, "Icon", new Vector2(0.5f, 0.6f), new Vector2(48f, 48f), Vector2.zero, Color.white);
            GameObject badgeIcon = BuildCrownBadge(buttonObject.transform);
            NotificationBadgeView notificationBadge = BuildNotificationBadge(buttonObject.transform, new Vector2(1f, 1f), new Vector2(-6f, -6f));
            TMP_Text countdown = BuildAnchoredText(buttonObject.transform, "CountdownText", "00:00", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 10f), new Vector2(100f, 22f), 16f, TextAlignmentOptions.Center);
            countdown.gameObject.SetActive(false);

            var view = buttonObject.AddComponent<FeatureButtonView>();
            var serialized = new SerializedObject(view);
            serialized.FindProperty("_button").objectReferenceValue = buttonObject.GetComponent<Button>();
            serialized.FindProperty("_icon").objectReferenceValue = icon;
            serialized.FindProperty("_badgeIcon").objectReferenceValue = badgeIcon;
            serialized.FindProperty("_notificationBadge").objectReferenceValue = notificationBadge;
            serialized.FindProperty("_countdownText").objectReferenceValue = countdown;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            return view;
        }

        private static GameObject BuildCrownBadge(Transform parent)
        {
            Image badge = BuildAnchoredImage(parent, "BadgeIcon", new Vector2(0.5f, 1f), new Vector2(28f, 28f), new Vector2(0f, 8f), new Color(1f, 0.85f, 0.2f));
            badge.gameObject.SetActive(false);
            return badge.gameObject;
        }

        // ------------------------------------------------------------------ Level selection area (center)

        private static (MainMenuLevelSelectionView view, LevelObjectivePanelView objectivesPanel, LevelPreviewView previewView) BuildLevelSelectionArea(Transform parent)
        {
            var area = new GameObject("LevelSelectionArea", typeof(RectTransform), typeof(LayoutElement));
            area.transform.SetParent(parent, false);
            area.GetComponent<LayoutElement>().flexibleHeight = 1f;
            area.GetComponent<LayoutElement>().minHeight = 280f;

            // Full-bleed backdrop (glow ring + map art + Prev/Next, which now live on the ring's edges instead
            // of a small in-flow row) first, ignoreLayout so VerticalLayoutGroup below skips it — everything
            // else in this area draws on top of it in normal Canvas sibling order.
            (LevelPreviewView previewView, Button previousButton, Button nextButton) = BuildLevelPreviewBackdrop(area.transform);

            VerticalLayoutGroup areaLayout = area.AddComponent<VerticalLayoutGroup>();
            areaLayout.spacing = 8f;
            areaLayout.childAlignment = TextAnchor.UpperCenter;
            areaLayout.childControlWidth = true;
            areaLayout.childControlHeight = true;
            areaLayout.childForceExpandWidth = true;
            areaLayout.childForceExpandHeight = false;

            TMP_Text title = BuildLayoutText(area.transform, "LevelTitle", "Level 1", 44f, 64f);
            TMP_Text status = BuildLayoutText(area.transform, "LevelStatus", "Not completed", 26f, 40f);

            Button moreButton = BuildSmallCircleButton(area.transform, "MoreButton", "...", PanelColor, new Vector2(1f, 1f), new Vector2(-8f, -8f));
            moreButton.transform.SetParent(area.transform, false);

            LevelObjectivePanelView objectivesPanel = BuildObjectivesPanel(area.transform);

            var view = area.AddComponent<MainMenuLevelSelectionView>();
            var serialized = new SerializedObject(view);
            serialized.FindProperty("_titleText").objectReferenceValue = title;
            serialized.FindProperty("_statusText").objectReferenceValue = status;
            serialized.FindProperty("_previousButton").objectReferenceValue = previousButton;
            serialized.FindProperty("_nextButton").objectReferenceValue = nextButton;
            serialized.FindProperty("_moreButton").objectReferenceValue = moreButton;
            serialized.FindProperty("_objectivesPanel").objectReferenceValue = objectivesPanel;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            return (view, objectivesPanel, previewView);
        }

        private static TMP_Text BuildLayoutText(Transform parent, string name, string text, float fontSize, float height)
        {
            var textObject = new GameObject(name, typeof(RectTransform), typeof(LayoutElement));
            textObject.transform.SetParent(parent, false);
            textObject.GetComponent<LayoutElement>().preferredHeight = height;
            TMP_Text label = textObject.AddComponent<TextMeshProUGUI>();
            label.text = text;
            label.fontSize = fontSize;
            label.alignment = TextAlignmentOptions.Center;
            label.color = Color.white;
            return label;
        }

        /// <summary>Prev/Next arrow, built from the "muiten" chevron sprite (no background box — the reference
        /// shows a bare green chevron floating next to the preview card). Previous mirrors the same sprite via
        /// a uniform-preserving X-flip on the ICON's own RectTransform (not the button's), so the button's own
        /// click/hit area stays a normal, unflipped rect. Sized directly via RectTransform (not LayoutElement —
        /// this now lives directly inside PreviewBackdrop, an absolutely-positioned container with no
        /// LayoutGroup of its own, so LayoutElement.preferredWidth/Height would never actually apply).</summary>
        private static Button BuildArrowButton(Transform parent, string name, bool mirror)
        {
            const float buttonSize = 140f;

            var buttonObject = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            buttonObject.transform.SetParent(parent, false);
            RectTransform buttonRect = buttonObject.GetComponent<RectTransform>();
            buttonRect.sizeDelta = new Vector2(buttonSize, buttonSize);
            Image hitArea = buttonObject.GetComponent<Image>();
            hitArea.color = new Color(0f, 0f, 0f, 0f); // invisible, raycast-only hit area
            hitArea.raycastTarget = true;

            const float iconHeight = 120f;
            const float iconWidth = iconHeight * (1172f / 1342f); // muiten.png's own aspect ratio

            var iconObject = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            iconObject.transform.SetParent(buttonObject.transform, false);
            RectTransform iconRect = iconObject.GetComponent<RectTransform>();
            iconRect.anchorMin = new Vector2(0.5f, 0.5f);
            iconRect.anchorMax = new Vector2(0.5f, 0.5f);
            iconRect.pivot = new Vector2(0.5f, 0.5f);
            iconRect.sizeDelta = new Vector2(iconWidth, iconHeight);
            iconRect.anchoredPosition = Vector2.zero;
            iconRect.localScale = mirror ? new Vector3(-1f, 1f, 1f) : Vector3.one;
            Image iconImage = iconObject.GetComponent<Image>();
            iconImage.sprite = LoadLevelSprite("muiten.png");
            iconImage.color = Color.white; // sprite's own baked-in color (green chevron) — no extra tint
            iconImage.preserveAspect = true;
            iconImage.raycastTarget = false;

            return buttonObject.GetComponent<Button>();
        }

        private static LevelObjectivePanelView BuildObjectivesPanel(Transform parent)
        {
            var panel = new GameObject("ObjectivesPanel", typeof(RectTransform), typeof(LayoutElement));
            panel.transform.SetParent(parent, false);
            panel.GetComponent<LayoutElement>().preferredHeight = 90f;

            var layout = panel.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 12f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = true;

            var slots = new LevelObjectiveView[3];
            var connectors = new Image[2];
            for (int i = 0; i < 3; i++)
            {
                slots[i] = BuildObjectiveSlot(panel.transform, $"Objective_{i + 1:00}");
                if (i < 2)
                {
                    var connectorObject = new GameObject($"Connector_{i + 1:00}", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
                    connectorObject.transform.SetParent(panel.transform, false);
                    var connectorLayout = connectorObject.GetComponent<LayoutElement>();
                    connectorLayout.preferredWidth = 24f;
                    connectorLayout.flexibleWidth = 0f;
                    connectors[i] = connectorObject.GetComponent<Image>();
                    connectors[i].color = new Color(0.25f, 0.25f, 0.3f, 1f);
                }
            }

            // Layout order matters: children were added slot/connector/slot/connector/slot, but connectors were
            // created after all 3 slots in the loop above only for i<2 interleaved correctly already since each
            // slot+connector pair is created together per iteration.

            var view = panel.AddComponent<LevelObjectivePanelView>();
            var serialized = new SerializedObject(view);
            var slotsProperty = serialized.FindProperty("_objectiveSlots");
            slotsProperty.arraySize = slots.Length;
            for (int i = 0; i < slots.Length; i++)
            {
                slotsProperty.GetArrayElementAtIndex(i).objectReferenceValue = slots[i];
            }

            var connectorsProperty = serialized.FindProperty("_connectors");
            connectorsProperty.arraySize = connectors.Length;
            for (int i = 0; i < connectors.Length; i++)
            {
                connectorsProperty.GetArrayElementAtIndex(i).objectReferenceValue = connectors[i];
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();

            return view;
        }

        private static LevelObjectiveView BuildObjectiveSlot(Transform parent, string name)
        {
            var slot = new GameObject(name, typeof(RectTransform), typeof(LayoutElement));
            slot.transform.SetParent(parent, false);
            var layoutElement = slot.GetComponent<LayoutElement>();
            layoutElement.preferredWidth = 90f;

            Image icon = BuildAnchoredImage(slot.transform, "Icon", new Vector2(0.5f, 1f), new Vector2(56f, 56f), new Vector2(0f, 0f), new Color(0.6f, 0.6f, 0.65f));
            TMP_Text requirement = BuildAnchoredText(slot.transform, "RequirementText", string.Empty, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), Vector2.zero, new Vector2(90f, 30f), 14f, TextAlignmentOptions.Center);

            var view = slot.AddComponent<LevelObjectiveView>();
            var serialized = new SerializedObject(view);
            serialized.FindProperty("_icon").objectReferenceValue = icon;
            serialized.FindProperty("_requirementText").objectReferenceValue = requirement;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            return view;
        }

        // ------------------------------------------------------------------ Play button

        private static PlayButtonView BuildPlayArea(Transform parent)
        {
            var playArea = new GameObject("PlayArea", typeof(RectTransform), typeof(LayoutElement));
            playArea.transform.SetParent(parent, false);
            playArea.GetComponent<LayoutElement>().preferredHeight = 120f;

            var buttonObject = new GameObject("PlayButton", typeof(RectTransform), typeof(Image), typeof(Button));
            buttonObject.transform.SetParent(playArea.transform, false);
            RectTransform buttonRect = buttonObject.GetComponent<RectTransform>();
            buttonRect.anchorMin = Vector2.zero;
            buttonRect.anchorMax = Vector2.one;
            buttonRect.offsetMin = Vector2.zero;
            buttonRect.offsetMax = Vector2.zero;
            buttonObject.GetComponent<Image>().color = AccentOrange;

            TMP_Text label = BuildAnchoredText(buttonObject.transform, "PlayLabel", "PLAY", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(300f, 70f), 46f, TextAlignmentOptions.Center);

            var energyCostRoot = new GameObject("EnergyCostRoot", typeof(RectTransform));
            energyCostRoot.transform.SetParent(buttonObject.transform, false);
            RectTransform energyRect = energyCostRoot.GetComponent<RectTransform>();
            energyRect.anchorMin = new Vector2(1f, 0.5f);
            energyRect.anchorMax = new Vector2(1f, 0.5f);
            energyRect.pivot = new Vector2(1f, 0.5f);
            energyRect.anchoredPosition = new Vector2(-24f, 0f);
            energyRect.sizeDelta = new Vector2(90f, 50f);
            var energyLayout = energyCostRoot.AddComponent<HorizontalLayoutGroup>();
            energyLayout.spacing = 6f;
            energyLayout.childAlignment = TextAnchor.MiddleCenter;

            TMP_Text energyIcon = BuildAnchoredText(energyCostRoot.transform, "EnergyIcon", "E", Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(30f, 40f), 28f, TextAlignmentOptions.Center);
            TMP_Text energyCostText = BuildAnchoredText(energyCostRoot.transform, "EnergyCostText", "5", Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(40f, 40f), 28f, TextAlignmentOptions.Center);

            var view = buttonObject.AddComponent<PlayButtonView>();
            var serialized = new SerializedObject(view);
            serialized.FindProperty("_button").objectReferenceValue = buttonObject.GetComponent<Button>();
            serialized.FindProperty("_label").objectReferenceValue = label;
            serialized.FindProperty("_energyCostRoot").objectReferenceValue = energyCostRoot;
            serialized.FindProperty("_energyCostText").objectReferenceValue = energyCostText;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            return view;
        }

        // ------------------------------------------------------------------ Quest banner

        private static QuestBannerView BuildQuestBanner(Transform parent)
        {
            var banner = new GameObject("QuestBanner", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
            banner.transform.SetParent(parent, false);
            banner.GetComponent<LayoutElement>().preferredHeight = 76f;
            banner.GetComponent<Image>().color = PanelColor;

            Image icon = BuildAnchoredImage(banner.transform, "QuestIcon", new Vector2(0f, 0.5f), new Vector2(48f, 48f), new Vector2(16f, 0f), AccentOrange);
            TMP_Text text = BuildAnchoredText(banner.transform, "QuestText", string.Empty, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(76f, 12f), new Vector2(0f, 28f), 22f, TextAlignmentOptions.MidlineLeft);
            TMP_Text progressText = BuildAnchoredText(banner.transform, "QuestProgressText", string.Empty, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(76f, -16f), new Vector2(0f, 22f), 16f, TextAlignmentOptions.MidlineLeft);
            Image progressFill = BuildBarFill(banner.transform, "ProgressBar", new Vector2(76f, -30f), new Vector2(400f, 10f), AccentGreen);
            NotificationBadgeView badge = BuildNotificationBadge(banner.transform, new Vector2(1f, 1f), new Vector2(-12f, -8f));

            var view = banner.AddComponent<QuestBannerView>();
            var serialized = new SerializedObject(view);
            serialized.FindProperty("_button").objectReferenceValue = banner.GetComponent<Button>();
            serialized.FindProperty("_questIcon").objectReferenceValue = icon;
            serialized.FindProperty("_questText").objectReferenceValue = text;
            serialized.FindProperty("_questProgressText").objectReferenceValue = progressText;
            serialized.FindProperty("_progressBarFill").objectReferenceValue = progressFill;
            serialized.FindProperty("_notificationBadge").objectReferenceValue = badge;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            return view;
        }

        // ------------------------------------------------------------------ Daily Reward / VIP popups

        private static DailyRewardPanelView BuildDailyRewardPanel(Transform safeArea)
        {
            GameObject overlay = BuildPopupOverlay(safeArea, "DailyRewardPanel");
            GameObject panel = BuildPopupCard(overlay.transform, "Card", new Vector2(600f, 400f));

            LevelSceneScaffolder.CreateTMPText(panel.transform, "TitleText", "Daily Check-in", 24f, 60f, 32f, TextAlignmentOptions.Center);
            TMP_Text streakDayText = BuildAnchoredText(panel.transform, "StreakDayText", "Day 1/7", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -110f), new Vector2(400f, 44f), 28f, TextAlignmentOptions.Center);
            TMP_Text rewardPreviewText = BuildAnchoredText(panel.transform, "RewardPreviewText", string.Empty, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -170f), new Vector2(480f, 60f), 22f, TextAlignmentOptions.Center);

            (Button claimButton, TMP_Text claimLabel) = BuildLabeledButton(panel.transform, "ClaimButton", "Claim", AccentGreen, new Vector2(0f, -280f), new Vector2(300f, 76f));
            Button closeButton = BuildSmallCircleButton(panel.transform, "CloseButton", "X", PanelColor, new Vector2(1f, 1f), new Vector2(-16f, -16f));

            var view = overlay.AddComponent<DailyRewardPanelView>();
            var serialized = new SerializedObject(view);
            serialized.FindProperty("_streakDayText").objectReferenceValue = streakDayText;
            serialized.FindProperty("_rewardPreviewText").objectReferenceValue = rewardPreviewText;
            serialized.FindProperty("_claimButton").objectReferenceValue = claimButton;
            serialized.FindProperty("_claimButtonLabel").objectReferenceValue = claimLabel;
            serialized.FindProperty("_closeButton").objectReferenceValue = closeButton;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            overlay.SetActive(false);
            return view;
        }

        private static VipPanelView BuildVipPanel(Transform safeArea)
        {
            GameObject overlay = BuildPopupOverlay(safeArea, "VipPanel");
            GameObject panel = BuildPopupCard(overlay.transform, "Card", new Vector2(680f, 620f));

            LevelSceneScaffolder.CreateTMPText(panel.transform, "TitleText", "VIP", 24f, 60f, 32f, TextAlignmentOptions.Center);
            TMP_Text currentTierText = BuildAnchoredText(panel.transform, "CurrentTierText", "No VIP", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -100f), new Vector2(400f, 36f), 20f, TextAlignmentOptions.Center);

            var rows = new VipPanelView.TierRow[VipTierTable.Tiers.Length];
            for (int i = 0; i < rows.Length; i++)
            {
                rows[i] = BuildVipTierRow(panel.transform, i, VipTierTable.Tiers[i].Tier);
            }

            Button closeButton = BuildSmallCircleButton(panel.transform, "CloseButton", "X", PanelColor, new Vector2(1f, 1f), new Vector2(-16f, -16f));

            var view = overlay.AddComponent<VipPanelView>();
            var serialized = new SerializedObject(view);
            serialized.FindProperty("_currentTierText").objectReferenceValue = currentTierText;
            serialized.FindProperty("_closeButton").objectReferenceValue = closeButton;

            SerializedProperty rowsProperty = serialized.FindProperty("_tierRows");
            rowsProperty.arraySize = rows.Length;
            for (int i = 0; i < rows.Length; i++)
            {
                SerializedProperty rowProperty = rowsProperty.GetArrayElementAtIndex(i);
                rowProperty.FindPropertyRelative("CostText").objectReferenceValue = rows[i].CostText;
                rowProperty.FindPropertyRelative("BonusText").objectReferenceValue = rows[i].BonusText;
                rowProperty.FindPropertyRelative("BuyButton").objectReferenceValue = rows[i].BuyButton;
                rowProperty.FindPropertyRelative("OwnedLabel").objectReferenceValue = rows[i].OwnedLabel;
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();

            overlay.SetActive(false);
            return view;
        }

        private static VipPanelView.TierRow BuildVipTierRow(Transform parent, int rowIndex, int tierNumber)
        {
            var row = new GameObject($"TierRow_{tierNumber}", typeof(RectTransform), typeof(Image));
            row.transform.SetParent(parent, false);
            RectTransform rect = row.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, -160f - rowIndex * 140f);
            rect.sizeDelta = new Vector2(600f, 120f);
            row.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.06f);

            LevelSceneScaffolder.CreateTMPText(row.transform, "TierLabel", $"VIP {tierNumber}", -12f, 30f, 22f, TextAlignmentOptions.Center);
            TMP_Text costText = BuildAnchoredText(row.transform, "CostText", "0", new Vector2(0.25f, 0.5f), new Vector2(0.25f, 0.5f), new Vector2(0f, -10f), new Vector2(140f, 30f), 20f, TextAlignmentOptions.Center);
            TMP_Text bonusText = BuildAnchoredText(row.transform, "BonusText", "+0% Coin", new Vector2(0.55f, 0.5f), new Vector2(0.55f, 0.5f), new Vector2(0f, -10f), new Vector2(160f, 30f), 18f, TextAlignmentOptions.Center);

            (Button buyButton, _) = BuildLabeledButton(row.transform, "BuyButton", "Buy", AccentGreen, Vector2.zero, new Vector2(140f, 56f));
            RectTransform buyRect = buyButton.GetComponent<RectTransform>();
            buyRect.anchorMin = new Vector2(1f, 0.5f);
            buyRect.anchorMax = new Vector2(1f, 0.5f);
            buyRect.pivot = new Vector2(1f, 0.5f);
            buyRect.anchoredPosition = new Vector2(-16f, 0f);

            GameObject ownedLabel = LevelSceneScaffolder.CreateTMPText(row.transform, "OwnedLabel", "Owned", -12f, 30f, 20f, TextAlignmentOptions.Center).gameObject;
            RectTransform ownedRect = ownedLabel.GetComponent<RectTransform>();
            ownedRect.anchorMin = new Vector2(1f, 0.5f);
            ownedRect.anchorMax = new Vector2(1f, 0.5f);
            ownedRect.pivot = new Vector2(1f, 0.5f);
            ownedRect.anchoredPosition = new Vector2(-16f, 0f);
            ownedLabel.SetActive(false);

            return new VipPanelView.TierRow
            {
                CostText = costText,
                BonusText = bonusText,
                BuyButton = buyButton,
                OwnedLabel = ownedLabel
            };
        }

        private static GameObject BuildPopupOverlay(Transform safeArea, string name)
        {
            var overlay = new GameObject(name, typeof(RectTransform), typeof(Image));
            overlay.transform.SetParent(safeArea.parent, false); // parented to Canvas so it covers full screen, above SafeArea content
            RectTransform rect = overlay.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            overlay.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.6f);
            return overlay;
        }

        private static GameObject BuildPopupCard(Transform overlay, string name, Vector2 size)
        {
            var card = new GameObject(name, typeof(RectTransform), typeof(Image));
            card.transform.SetParent(overlay, false);
            RectTransform rect = card.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = size;
            card.GetComponent<Image>().color = new Color(0.06f, 0.1f, 0.2f, 0.98f);
            return card;
        }

        private static (Button button, TMP_Text label) BuildLabeledButton(Transform parent, string name, string label, Color color, Vector2 anchoredPosition, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;
            go.GetComponent<Image>().color = color;

            TMP_Text label_ = BuildAnchoredText(go.transform, "Label", label, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, size, 26f, TextAlignmentOptions.Center);

            return (go.GetComponent<Button>(), label_);
        }

        // ------------------------------------------------------------------ Bottom navigation

        private const string BottomNavSpriteFolder = "Assets/_Game/Art/Sprite/MainMenu/";
        private const string LevelSpriteFolder = "Assets/_Game/Art/Sprite/MainMenu/Level/";

        // Vertical budget inside IconGroup (bottom-up, in IconGroup-local units — see BuildNavTab): bottom
        // padding, then the label, then a small gap, then the icon, then top padding — all baked into TileHeight.
        // The label lives INSIDE IconGroup (so it pop-scales together with the tile, staying tucked right under
        // the icon, matching the reference art where the label sits inside the selected tile) but sits close to
        // IconGroup's fixed bottom pivot, so it barely moves even at the 1.25x pop.
        private const float LabelHeight = 32f;
        private const float LabelBottomPadding = 10f;
        private const float IconLabelGap = 6f;
        private const float IconSize = 124f;
        // Mirrors the bottom stack (padding + label + gap) exactly, so the icon lands dead-center in the tile
        // instead of skewed toward the top — the label reserves space at the bottom (even when hidden, on
        // unselected tabs), so without an equal top allowance the icon reads as pushed upward.
        private const float TileTopPadding = LabelBottomPadding + LabelHeight + IconLabelGap;
        private const float TileHeight = LabelBottomPadding + LabelHeight + IconLabelGap + IconSize + TileTopPadding;

        // BuildAnchoredText always centers a text's pivot, so anchoredPosition.y has to be the CENTER, not the
        // bottom edge — add half the label's own height on top of the desired bottom padding.
        private const float LabelAnchoredY = LabelBottomPadding + LabelHeight / 2f;
        private const float IconAnchoredY = LabelBottomPadding + LabelHeight + IconLabelGap;

        // How far IconGroup's own (fixed) bottom sits above the tab's bottom edge — i.e. the bar's/screen's
        // bottom edge — so the tile never touches the very edge of the screen.
        private const float IconGroupBottomY = 16f;

        private static Sprite LoadBottomNavSprite(string fileName)
        {
            string path = BottomNavSpriteFolder + fileName;

            // Single-mode sprites (the plain "Rectangle"/"Rectangle Select" tiles) load directly. The 5 icon
            // sprites are Multiple-mode with exactly one sub-sprite (see the icon-trim tool) — they were trimmed
            // to their tight alpha bounding box so preserveAspect centering lands on the actual artwork instead
            // of whatever off-center padding the source file happened to have; LoadAssetAtPath<Sprite> can't find
            // a Multiple-mode sprite directly, so fall back to scanning the sub-assets for it.
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null)
            {
                foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(path))
                {
                    if (asset is Sprite subSprite)
                    {
                        sprite = subSprite;
                        break;
                    }
                }
            }

            if (sprite == null)
            {
                Debug.LogWarning($"[MainMenuSceneScaffolder] Bottom nav sprite not found: {path}");
            }

            return sprite;
        }

        private static Sprite LoadLevelSprite(string fileName)
        {
            string path = LevelSpriteFolder + fileName;
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null)
            {
                Debug.LogWarning($"[MainMenuSceneScaffolder] Level preview sprite not found: {path}");
            }

            return sprite;
        }

        private static BottomNavigationView BuildBottomNavigation(Transform parent)
        {
            var nav = new GameObject("BottomNavigation", typeof(RectTransform), typeof(Image));
            nav.transform.SetParent(parent, false);
            RectTransform navRect = nav.GetComponent<RectTransform>();
            navRect.anchorMin = Vector2.zero;
            navRect.anchorMax = Vector2.one;
            navRect.offsetMin = Vector2.zero;
            navRect.offsetMax = Vector2.zero;
            nav.GetComponent<Image>().color = new Color(0.06f, 0.1f, 0.2f, 0.95f);

            var layout = nav.AddComponent<HorizontalLayoutGroup>();
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            layout.childControlWidth = true;
            // Must be true: each tab's height is driven by its own LayoutElement.preferredHeight (see BuildNavTab
            // call sites — Play's tab is taller than the rest). With this false, tabs kept the RectTransform's
            // stale default 100px height regardless of preferredHeight, cramming icon+label into a box shorter
            // than the bar itself and making the label overlap the icon.
            layout.childControlHeight = true;

            // Shared by all 5 tabs: "Rectangle" is the (invisible-until-selected) tile shape, "Rectangle Select"
            // is the highlighted tile shown behind whichever tab is currently active.
            Sprite normalTile = LoadBottomNavSprite("Tab/Rectangle.png");
            Sprite selectedTile = LoadBottomNavSprite("Tab/Rectangle Select.png");

            // All 5 tabs share the same column height now — the selected one no longer needs a permanently taller
            // slot for emphasis, since the pop-scale (bigger tile + bigger icon together) already does that job
            // dynamically. A differential height here was also what caused the tile to overlap the label on the
            // shorter (non-Play) tabs once the tile/icon art was sized up. Matches the bar's own height exactly
            // (not less) so the label has a controlled, guaranteed margin from the bar's bottom edge — see
            // BuildNavTab's labelBottomMargin.
            float tabHeight = EditorMenuShellLayout.BottomNavHeight;
            // Per-tab idle animation while selected (user-specified): Play/Base bounce up-down; Upgrade spins 2
            // full turns clockwise then 2 back counter-clockwise; Shop/Defense have none.
            BottomNavTabView shopTab = BuildNavTab(nav.transform, "ShopTab", "Shop", tabHeight, LoadBottomNavSprite("Tab/shop.png"), normalTile, selectedTile, BottomNavIconIdleAnimation.None);
            BottomNavTabView upgradeTab = BuildNavTab(nav.transform, "UpgradeTab", "Upgrade", tabHeight, LoadBottomNavSprite("Tab/storage.png"), normalTile, selectedTile, BottomNavIconIdleAnimation.RotateTwoTurnsBothWays);
            BottomNavTabView playTab = BuildNavTab(nav.transform, "PlayTab", "PLAY", tabHeight, LoadBottomNavSprite("Tab/play.png"), normalTile, selectedTile, BottomNavIconIdleAnimation.BounceUpDown);
            BottomNavTabView baseTab = BuildNavTab(nav.transform, "BaseTab", "Base", tabHeight, LoadBottomNavSprite("Tab/base.png"), normalTile, selectedTile, BottomNavIconIdleAnimation.BounceUpDown);
            BottomNavTabView defenseTab = BuildNavTab(nav.transform, "DefenseTab", "Defense", tabHeight, LoadBottomNavSprite("Tab/tower.png"), normalTile, selectedTile, BottomNavIconIdleAnimation.None);

            var view = nav.AddComponent<BottomNavigationView>();
            var serialized = new SerializedObject(view);
            serialized.FindProperty("_shopTab").objectReferenceValue = shopTab;
            serialized.FindProperty("_upgradeTab").objectReferenceValue = upgradeTab;
            serialized.FindProperty("_playTab").objectReferenceValue = playTab;
            serialized.FindProperty("_baseTab").objectReferenceValue = baseTab;
            serialized.FindProperty("_defenseTab").objectReferenceValue = defenseTab;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            return view;
        }

        private static BottomNavTabView BuildNavTab(Transform parent, string name, string label, float preferredHeight, Sprite iconSprite, Sprite normalTileSprite, Sprite selectedTileSprite, BottomNavIconIdleAnimation idleAnimation)
        {
            // typeof(Image) here is load-bearing: Button/Selectable needs a Graphic on its own GameObject to be
            // a raycast target at all — without one, EventSystem.RaycastAll finds nothing for this tab (only the
            // BottomNavigation panel behind it), so real taps land on nothing and the button never fires. This
            // was invisible to onClick.Invoke()-based testing since that bypasses raycasting entirely.
            var tabObject = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            tabObject.transform.SetParent(parent, false);
            tabObject.GetComponent<Image>().color = Color.clear; // invisible full-tab hit area, not a visible panel
            var layoutElement = tabObject.GetComponent<LayoutElement>();
            layoutElement.preferredHeight = preferredHeight;
            layoutElement.flexibleWidth = 1f;

            // Tile+Icon+Label(+Badge) live under their own "IconGroup" container instead of directly under
            // tabObject. BottomNavTabView pop-scales ONLY this group on selection (Y axis only — see below),
            // never tabObject itself, and never X — so a selected tab never grows wider into its neighbors'
            // columns, only taller. IconGroup is bottom-anchored/bottom-pivoted at a FIXED point close to the
            // label, so the pop grows mostly upward (around the icon) and barely moves the label at all.
            var iconGroupObject = new GameObject("IconGroup", typeof(RectTransform));
            iconGroupObject.transform.SetParent(tabObject.transform, false);
            RectTransform iconGroupRect = iconGroupObject.GetComponent<RectTransform>();
            iconGroupRect.anchorMin = new Vector2(0f, 0f);
            iconGroupRect.anchorMax = new Vector2(1f, 0f);
            iconGroupRect.pivot = new Vector2(0.5f, 0f);
            iconGroupRect.anchoredPosition = new Vector2(0f, IconGroupBottomY);
            iconGroupRect.sizeDelta = new Vector2(0f, TileHeight);

            // Tile stretches to the tab's full column width so neighboring tabs' tiles sit flush against each
            // other with no gap — matching the reference art (a continuous strip of tiles, not floating squares).
            var tileObject = new GameObject("Tile", typeof(RectTransform), typeof(Image));
            tileObject.transform.SetParent(iconGroupObject.transform, false);
            RectTransform tileRect = tileObject.GetComponent<RectTransform>();
            tileRect.anchorMin = new Vector2(0f, 0f);
            tileRect.anchorMax = new Vector2(1f, 0f);
            tileRect.pivot = new Vector2(0.5f, 0f);
            tileRect.anchoredPosition = Vector2.zero;
            tileRect.sizeDelta = new Vector2(0f, TileHeight);
            Image tile = tileObject.GetComponent<Image>();
            tile.sprite = normalTileSprite;
            tile.color = Color.white;
            tile.raycastTarget = false;

            // Icon+Label(+Badge) live under their own "ContentGroup" — separate from Tile — so
            // BottomNavTabView.SetSelected can re-center them as a unit whenever the tile's height changes: Tile
            // is bottom-pivoted and only ever grows UPWARD from a fixed bottom, so its center shifts up by half
            // of whatever height it gains; ContentGroup gets nudged up by that same amount (animated in sync),
            // keeping icon+label sitting in the middle of the tile at every size instead of sliding toward its
            // bottom as it pops taller. At rest (tile at its base height) ContentGroup sits at offset zero, so
            // Icon/Label's own anchoredPosition values below are unchanged from a plain IconGroup-relative
            // layout.
            var contentGroupObject = new GameObject("ContentGroup", typeof(RectTransform));
            contentGroupObject.transform.SetParent(iconGroupObject.transform, false);
            RectTransform contentGroupRect = contentGroupObject.GetComponent<RectTransform>();
            contentGroupRect.anchorMin = new Vector2(0f, 0f);
            contentGroupRect.anchorMax = new Vector2(1f, 0f);
            contentGroupRect.pivot = new Vector2(0.5f, 0f);
            contentGroupRect.anchoredPosition = Vector2.zero;
            contentGroupRect.sizeDelta = new Vector2(0f, TileHeight);

            // Icon sits in the tile's upper portion, with the label reserved below it (see the vertical-budget
            // consts above BuildBottomNavigation). Built manually rather than via BuildAnchoredImage: that
            // helper always sets pivot = anchor, which here would put the pivot at the icon's own BOTTOM edge
            // (anchor is bottom-center) — fine for positioning, but rotating (idle spin animation) around a
            // bottom pivot swings the icon through an arc instead of spinning it in place. Pivot is forced to
            // dead-center instead, with anchoredPosition.y bumped up by half the icon's height so it lands in
            // the exact same visual spot as the bottom-pivot version would have.
            var iconObject = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            iconObject.transform.SetParent(contentGroupObject.transform, false);
            RectTransform iconRect = iconObject.GetComponent<RectTransform>();
            iconRect.anchorMin = new Vector2(0.5f, 0f);
            iconRect.anchorMax = new Vector2(0.5f, 0f);
            iconRect.pivot = new Vector2(0.5f, 0.5f);
            iconRect.sizeDelta = new Vector2(IconSize, IconSize);
            iconRect.anchoredPosition = new Vector2(0f, IconAnchoredY + IconSize / 2f);
            Image icon = iconObject.GetComponent<Image>();
            icon.color = Color.white;
            icon.raycastTarget = false;
            icon.sprite = iconSprite;
            icon.preserveAspect = true;
            NotificationBadgeView badge = BuildNotificationBadge(contentGroupObject.transform, new Vector2(1f, 1f), new Vector2(-10f, 8f));

            // Label lives INSIDE ContentGroup, right under the icon, tucked close to the group's fixed bottom
            // pivot so it barely moves during the pop — matching the reference art, where the label sits inside
            // the selected tile rather than floating below it. Only shown at all while this tab is selected
            // (BottomNavTabView.SetSelected toggles it) — unselected tabs carry no text. White fill + black
            // outline (also per the reference) so it reads clearly against either tile color.
            TMP_Text labelText = BuildAnchoredText(contentGroupObject.transform, "Label", label, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, LabelAnchoredY), new Vector2(150f, LabelHeight), 24f, TextAlignmentOptions.Center);
            labelText.color = Color.white;
            labelText.fontStyle = FontStyles.Bold;
            labelText.outlineWidth = 0.2f;
            labelText.outlineColor = Color.black;
            labelText.gameObject.SetActive(false);

            var button = tabObject.AddComponent<Button>();
            var view = tabObject.AddComponent<BottomNavTabView>();
            var serialized = new SerializedObject(view);
            serialized.FindProperty("_button").objectReferenceValue = button;
            // Same LayoutElement set up above (preferredHeight/flexibleWidth) — BottomNavTabView.SetSelected
            // grows its flexibleWidth on selection to widen this tab's actual column, with the other tabs'
            // columns shrinking to fit in the same layout pass, so the row never overlaps either direction.
            serialized.FindProperty("_columnLayoutElement").objectReferenceValue = layoutElement;
            serialized.FindProperty("_contentGroup").objectReferenceValue = contentGroupRect;
            serialized.FindProperty("_icon").objectReferenceValue = icon;
            serialized.FindProperty("_label").objectReferenceValue = labelText;
            serialized.FindProperty("_notificationBadge").objectReferenceValue = badge;
            // The dedicated Tile image above — swaps to the "Rectangle Select" sprite when selected, and always
            // stretches to exactly match this tab's own (now dynamically-resized) column — see
            // BottomNavTabView.SetSelected.
            serialized.FindProperty("_tileBackground").objectReferenceValue = tile;
            serialized.FindProperty("_normalTileSprite").objectReferenceValue = normalTileSprite;
            serialized.FindProperty("_selectedTileSprite").objectReferenceValue = selectedTileSprite;
            serialized.FindProperty("_iconIdleAnimation").enumValueIndex = (int)idleAnimation;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            return view;
        }

        // ------------------------------------------------------------------ Composition root wiring

        private static void WireCompositionRoot(
            GameObject compositionRootObject,
            GameObject canvasObject,
            LevelPreviewView previewView,
            PlayerProfileWidgetView profileWidget,
            ResourceWidgetView energyWidget,
            ResourceWidgetView gemWidget,
            ResourceWidgetView coinWidget,
            FeatureButtonView[] leftFeatureButtons,
            FeatureButtonView dailyButton,
            FeatureButtonView freeRewardButton,
            MainMenuLevelSelectionView levelSelectionView,
            PlayButtonView playButtonView,
            QuestBannerView questBanner,
            BottomNavigationView bottomNavigation,
            DailyRewardPanelView dailyRewardPanel,
            VipPanelView vipPanel,
            GameObject playPanel,
            MenuPanelBuilders.ShopPanelBuildResult shopPanel,
            MenuPanelBuilders.UpgradePanelBuildResult upgradePanel,
            MenuPanelBuilders.DefensePanelBuildResult defensePanel,
            MenuPanelBuilders.BasePanelBuildResult basePanel)
        {
            var backNavigation = canvasObject.AddComponent<BackNavigationController>();

            var levelSelectionPresenter = compositionRootObject.AddComponent<MainMenuLevelSelectionPresenter>();
            var serializedLevelSelectionPresenter = new SerializedObject(levelSelectionPresenter);
            serializedLevelSelectionPresenter.FindProperty("_view").objectReferenceValue = levelSelectionView;
            serializedLevelSelectionPresenter.FindProperty("_previewView").objectReferenceValue = previewView;
            serializedLevelSelectionPresenter.FindProperty("_playButtonView").objectReferenceValue = playButtonView;
            serializedLevelSelectionPresenter.ApplyModifiedPropertiesWithoutUndo();

            var resourcePresenter = compositionRootObject.AddComponent<MainMenuResourcePresenter>();
            var serializedResourcePresenter = new SerializedObject(resourcePresenter);
            serializedResourcePresenter.FindProperty("_playerProfileWidget").objectReferenceValue = profileWidget;
            serializedResourcePresenter.FindProperty("_energyWidget").objectReferenceValue = energyWidget;
            serializedResourcePresenter.FindProperty("_premiumCurrencyWidget").objectReferenceValue = gemWidget;
            serializedResourcePresenter.FindProperty("_coinWidget").objectReferenceValue = coinWidget;
            serializedResourcePresenter.ApplyModifiedPropertiesWithoutUndo();

            var shopPresenter = compositionRootObject.AddComponent<AlienDefense.UI.Shop.ShopScreenPresenter>();
            var serializedShopPresenter = new SerializedObject(shopPresenter);
            serializedShopPresenter.FindProperty("_view").objectReferenceValue = shopPanel.View;
            serializedShopPresenter.FindProperty("_cardPrefab").objectReferenceValue = shopPanel.CardPrefab;
            serializedShopPresenter.ApplyModifiedPropertiesWithoutUndo();

            var upgradePresenter = compositionRootObject.AddComponent<AlienDefense.UI.Upgrade.TowerUpgradeScreenPresenter>();
            var serializedUpgradePresenter = new SerializedObject(upgradePresenter);
            serializedUpgradePresenter.FindProperty("_view").objectReferenceValue = upgradePanel.View;
            serializedUpgradePresenter.FindProperty("_cardPrefab").objectReferenceValue = upgradePanel.CardPrefab;
            serializedUpgradePresenter.ApplyModifiedPropertiesWithoutUndo();

            var defensePresenter = compositionRootObject.AddComponent<AlienDefense.UI.Defense.DefenseScreenPresenter>();
            var serializedDefensePresenter = new SerializedObject(defensePresenter);
            serializedDefensePresenter.FindProperty("_view").objectReferenceValue = defensePanel.View;
            serializedDefensePresenter.FindProperty("_cardPrefab").objectReferenceValue = defensePanel.CardPrefab;
            serializedDefensePresenter.ApplyModifiedPropertiesWithoutUndo();

            var basePresenter = compositionRootObject.AddComponent<AlienDefense.UI.Base.BaseScreenPresenter>();
            var serializedBasePresenter = new SerializedObject(basePresenter);
            serializedBasePresenter.FindProperty("_view").objectReferenceValue = basePanel.View;
            serializedBasePresenter.ApplyModifiedPropertiesWithoutUndo();

            var menuShellView = compositionRootObject.AddComponent<MenuShellView>();
            var serializedMenuShellView = new SerializedObject(menuShellView);
            serializedMenuShellView.FindProperty("_playPanel").objectReferenceValue = playPanel;
            serializedMenuShellView.FindProperty("_shopPanel").objectReferenceValue = shopPanel.Panel;
            serializedMenuShellView.FindProperty("_upgradePanel").objectReferenceValue = upgradePanel.Panel;
            serializedMenuShellView.FindProperty("_defensePanel").objectReferenceValue = defensePanel.Panel;
            serializedMenuShellView.FindProperty("_basePanel").objectReferenceValue = basePanel.Panel;
            serializedMenuShellView.FindProperty("_levelPreviewRoot").objectReferenceValue = previewView.gameObject;
            serializedMenuShellView.FindProperty("_bottomNavigation").objectReferenceValue = bottomNavigation;
            serializedMenuShellView.ApplyModifiedPropertiesWithoutUndo();

            var menuShellPresenter = compositionRootObject.AddComponent<MenuShellPresenter>();
            var serializedMenuShellPresenter = new SerializedObject(menuShellPresenter);
            serializedMenuShellPresenter.FindProperty("_view").objectReferenceValue = menuShellView;
            serializedMenuShellPresenter.FindProperty("_resourcePresenter").objectReferenceValue = resourcePresenter;
            serializedMenuShellPresenter.FindProperty("_shopPresenter").objectReferenceValue = shopPresenter;
            serializedMenuShellPresenter.FindProperty("_upgradePresenter").objectReferenceValue = upgradePresenter;
            serializedMenuShellPresenter.FindProperty("_defensePresenter").objectReferenceValue = defensePresenter;
            serializedMenuShellPresenter.FindProperty("_basePresenter").objectReferenceValue = basePresenter;
            serializedMenuShellPresenter.ApplyModifiedPropertiesWithoutUndo();

            serializedShopPresenter.FindProperty("_menuShell").objectReferenceValue = menuShellPresenter;
            serializedShopPresenter.ApplyModifiedPropertiesWithoutUndo();

            serializedUpgradePresenter.FindProperty("_menuShell").objectReferenceValue = menuShellPresenter;
            serializedUpgradePresenter.ApplyModifiedPropertiesWithoutUndo();
            serializedDefensePresenter.FindProperty("_menuShell").objectReferenceValue = menuShellPresenter;
            serializedDefensePresenter.ApplyModifiedPropertiesWithoutUndo();

            var bottomNavPresenter = compositionRootObject.AddComponent<BottomNavigationPresenter>();
            var serializedBottomNavPresenter = new SerializedObject(bottomNavPresenter);
            serializedBottomNavPresenter.FindProperty("_view").objectReferenceValue = bottomNavigation;
            serializedBottomNavPresenter.ApplyModifiedPropertiesWithoutUndo();

            var rewardsPresenter = compositionRootObject.AddComponent<MainMenuRewardsPresenter>();
            var serializedRewardsPresenter = new SerializedObject(rewardsPresenter);
            serializedRewardsPresenter.FindProperty("_questBanner").objectReferenceValue = questBanner;
            serializedRewardsPresenter.FindProperty("_dailyRewardPanel").objectReferenceValue = dailyRewardPanel;
            serializedRewardsPresenter.FindProperty("_vipPanel").objectReferenceValue = vipPanel;
            serializedRewardsPresenter.ApplyModifiedPropertiesWithoutUndo();

            var mainMenuView = compositionRootObject.AddComponent<MainMenuView>();
            var serializedView = new SerializedObject(mainMenuView);
            serializedView.FindProperty("_levelSelectionView").objectReferenceValue = levelSelectionView;
            serializedView.FindProperty("_levelPreviewView").objectReferenceValue = previewView;
            serializedView.FindProperty("_playButtonView").objectReferenceValue = playButtonView;
            serializedView.FindProperty("_playerProfileWidget").objectReferenceValue = profileWidget;
            serializedView.FindProperty("_energyWidget").objectReferenceValue = energyWidget;
            serializedView.FindProperty("_premiumCurrencyWidget").objectReferenceValue = gemWidget;
            serializedView.FindProperty("_coinWidget").objectReferenceValue = coinWidget;

            var leftButtonsProperty = serializedView.FindProperty("_leftFeatureButtons");
            leftButtonsProperty.arraySize = leftFeatureButtons.Length;
            for (int i = 0; i < leftFeatureButtons.Length; i++)
            {
                leftButtonsProperty.GetArrayElementAtIndex(i).objectReferenceValue = leftFeatureButtons[i];
            }

            serializedView.FindProperty("_dailyButton").objectReferenceValue = dailyButton;
            serializedView.FindProperty("_freeRewardButton").objectReferenceValue = freeRewardButton;
            serializedView.FindProperty("_questBanner").objectReferenceValue = questBanner;
            serializedView.FindProperty("_bottomNavigation").objectReferenceValue = bottomNavigation;
            serializedView.ApplyModifiedPropertiesWithoutUndo();

            var presenter = compositionRootObject.AddComponent<MainMenuPresenter>();
            var serializedPresenter = new SerializedObject(presenter);
            serializedPresenter.FindProperty("_view").objectReferenceValue = mainMenuView;
            serializedPresenter.FindProperty("_levelSelectionPresenter").objectReferenceValue = levelSelectionPresenter;
            serializedPresenter.FindProperty("_resourcePresenter").objectReferenceValue = resourcePresenter;
            serializedPresenter.FindProperty("_bottomNavigationPresenter").objectReferenceValue = bottomNavPresenter;
            serializedPresenter.FindProperty("_rewardsPresenter").objectReferenceValue = rewardsPresenter;
            serializedPresenter.FindProperty("_menuShellPresenter").objectReferenceValue = menuShellPresenter;
            serializedPresenter.FindProperty("_backNavigation").objectReferenceValue = backNavigation;
            serializedPresenter.ApplyModifiedPropertiesWithoutUndo();
        }

        // ------------------------------------------------------------------ Generic small building blocks

        private static Image BuildFullScreenImage(Transform parent, string name, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            var image = go.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private static Image BuildAnchoredImage(Transform parent, string name, Vector2 anchor, Vector2 size, Vector2 anchoredPosition, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = anchor;
            rect.sizeDelta = size;
            rect.anchoredPosition = anchoredPosition;
            var image = go.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private static TMP_Text BuildAnchoredText(Transform parent, string name, string initialText, Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPosition, Vector2 size, float fontSize, TextAlignmentOptions alignment)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;

            var text = go.AddComponent<TextMeshProUGUI>();
            text.text = initialText;
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = Color.white;
            return text;
        }

        private static Image BuildBarFill(Transform parent, string name, Vector2 anchoredPosition, Vector2 size, Color fillColor)
        {
            var background = new GameObject(name, typeof(RectTransform), typeof(Image));
            background.transform.SetParent(parent, false);
            var backgroundRect = background.GetComponent<RectTransform>();
            backgroundRect.anchorMin = new Vector2(0f, 0.5f);
            backgroundRect.anchorMax = new Vector2(0f, 0.5f);
            backgroundRect.pivot = new Vector2(0f, 0.5f);
            backgroundRect.anchoredPosition = anchoredPosition;
            backgroundRect.sizeDelta = size;
            background.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.35f);

            var fill = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            fill.transform.SetParent(background.transform, false);
            var fillRect = fill.GetComponent<RectTransform>();
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;
            var fillImage = fill.GetComponent<Image>();
            fillImage.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            fillImage.color = fillColor;
            fillImage.type = Image.Type.Filled;
            fillImage.fillMethod = Image.FillMethod.Horizontal;
            fillImage.fillAmount = 0f;

            return fillImage;
        }

        // ------------------------------------------------------------------ Top-left-anchored helpers (TopHUD)
        //
        // BuildAnchoredImage pivots at its own anchor corner, but BuildAnchoredText always pivots at its own
        // CENTER regardless of anchor — mixing the two while hand-laying-out the TopHUD (see BuildPlayerProfileWidget
        // /BuildResourceWidget) is exactly what made the old positions drift. These three all pivot at the same
        // top-left corner, so anchoredPositionDownRight always simply means "(right, down) from the parent's own
        // top-left corner" for every element.

        private static Image BuildTopLeftImage(Transform parent, string name, Vector2 anchoredPositionDownRight, Vector2 size, Color color, Sprite sprite)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(anchoredPositionDownRight.x, -anchoredPositionDownRight.y);
            rect.sizeDelta = size;
            var image = go.GetComponent<Image>();
            image.color = color;
            image.sprite = sprite;
            image.raycastTarget = false;
            return image;
        }

        private static TMP_Text BuildTopLeftText(Transform parent, string name, string text, Vector2 anchoredPositionDownRight, Vector2 size, float fontSize, TextAlignmentOptions alignment, FontStyles fontStyle)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(anchoredPositionDownRight.x, -anchoredPositionDownRight.y);
            rect.sizeDelta = size;
            var textComponent = go.AddComponent<TextMeshProUGUI>();
            textComponent.text = text;
            textComponent.fontSize = fontSize;
            textComponent.alignment = alignment;
            textComponent.fontStyle = fontStyle;
            textComponent.color = Color.white;
            return textComponent;
        }

        private static Image BuildTopLeftBarFill(Transform parent, string name, Vector2 anchoredPositionDownRight, Vector2 size, Color fillColor, Sprite sprite)
        {
            var background = new GameObject(name, typeof(RectTransform), typeof(Image));
            background.transform.SetParent(parent, false);
            var backgroundRect = background.GetComponent<RectTransform>();
            backgroundRect.anchorMin = new Vector2(0f, 1f);
            backgroundRect.anchorMax = new Vector2(0f, 1f);
            backgroundRect.pivot = new Vector2(0f, 1f);
            backgroundRect.anchoredPosition = new Vector2(anchoredPositionDownRight.x, -anchoredPositionDownRight.y);
            backgroundRect.sizeDelta = size;
            var backgroundImage = background.GetComponent<Image>();
            backgroundImage.sprite = sprite;
            backgroundImage.color = new Color(0f, 0f, 0f, 0.35f);

            var fill = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            fill.transform.SetParent(background.transform, false);
            var fillRect = fill.GetComponent<RectTransform>();
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;
            var fillImage = fill.GetComponent<Image>();
            fillImage.sprite = sprite;
            fillImage.color = fillColor;
            fillImage.type = Image.Type.Filled;
            fillImage.fillMethod = Image.FillMethod.Horizontal;
            fillImage.fillAmount = 0f; // presenter-driven (PlayerProfileWidgetView.SetLevel) — never a fake preview value

            return fillImage;
        }

        private static Button BuildSmallCircleButton(Transform parent, string name, string glyph, Color color, Vector2 anchor, Vector2 anchoredPosition)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = anchor;
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = new Vector2(40f, 40f);
            go.GetComponent<Image>().color = color;

            BuildAnchoredText(go.transform, "Label", glyph, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(40f, 40f), 22f, TextAlignmentOptions.Center);

            return go.GetComponent<Button>();
        }

        private static NotificationBadgeView BuildNotificationBadge(Transform parent, Vector2 anchor, Vector2 anchoredPosition)
        {
            var badgeRoot = new GameObject("NotificationBadge", typeof(RectTransform), typeof(Image));
            badgeRoot.transform.SetParent(parent, false);
            var rect = badgeRoot.GetComponent<RectTransform>();
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = anchor;
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = new Vector2(24f, 24f);
            badgeRoot.GetComponent<Image>().color = new Color(0.9f, 0.2f, 0.2f, 1f);

            TMP_Text countText = BuildAnchoredText(badgeRoot.transform, "CountText", string.Empty, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(24f, 24f), 14f, TextAlignmentOptions.Center);
            countText.gameObject.SetActive(false);

            var view = badgeRoot.AddComponent<NotificationBadgeView>();
            var serialized = new SerializedObject(view);
            serialized.FindProperty("_root").objectReferenceValue = badgeRoot;
            serialized.FindProperty("_countText").objectReferenceValue = countText;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            badgeRoot.SetActive(false);

            return view;
        }
    }
}
