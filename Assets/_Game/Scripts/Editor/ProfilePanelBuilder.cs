using System;
using AlienDefense.UI;
using AlienDefense.UI.MainMenu;
using AlienDefense.UI.Profile;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using static AlienDefense.EditorTools.EditorScreenBuildingBlocks;

namespace AlienDefense.EditorTools
{
    /// <summary>Builds ProfileRoot under MainMenu SafeArea (fullscreen overlay) + off-screen UFO RT preview.
    /// Also wires avatar Button + ProfilePresenter on MainMenuCompositionRoot. Menu: AlienDefense/Setup/14.</summary>
    internal static class ProfilePanelBuilder
    {
        private const string MainMenuScenePath = "Assets/_Game/Scenes/Menu/MainMenu.unity";
        private const string UfoPrefabPath = "Assets/_Game/Prefabs/UI/Profile/UFO_ProfilePreview.prefab";
        private const string UfoGameplayPrefabPath = "Assets/_Game/Prefabs/Player/UFO_Player.prefab";
        private const string PreviewLayerName = "MainMenuPreview";
        private const string PendingFlagPath = "Assets/_Game/EditorTemp/profile_overlay_inject.pending";

        private static readonly Color OverlayNavy = new Color(0.02f, 0.04f, 0.1f, 0.9f);
        private static readonly Color AccentCyan = new Color(0.3f, 0.8f, 0.95f, 1f);
        private static readonly Color AccentGreen = new Color(0.25f, 0.9f, 0.35f, 1f);
        private static readonly Color AccentYellow = new Color(0.98f, 0.88f, 0.15f, 1f);
        private static readonly Color AccentBlue = new Color(0.15f, 0.4f, 0.85f, 1f);
        private static readonly Color PanelColor = new Color(0.08f, 0.12f, 0.22f, 0.92f);
        private static readonly Color CardColor = new Color(0.55f, 0.78f, 0.95f, 0.96f);
        private static readonly Color CardBorder = new Color(0.12f, 0.28f, 0.55f, 1f);
        private static readonly Color StatValueGreen = new Color(0x15 / 255f, 0x81 / 255f, 0x45 / 255f, 1f);
        private static readonly Color DarkText = new Color(0.12f, 0.16f, 0.22f, 1f);

        public readonly struct ProfileBuildResult
        {
            public ProfileBuildResult(ProfileView view, ProfilePresenter presenter, ProfileUfoPreviewController ufoPreview)
            {
                View = view;
                Presenter = presenter;
                UfoPreview = ufoPreview;
            }

            public ProfileView View { get; }
            public ProfilePresenter Presenter { get; }
            public ProfileUfoPreviewController UfoPreview { get; }
        }

        [MenuItem("AlienDefense/Setup/14. Inject Profile Overlay Into MainMenu")]
        public static void InjectIntoMainMenuMenuItem()
        {
            InjectIntoMainMenuScene(forceRebuild: true);
        }

        /// <summary>Called by auto-inject after domain reload when a pending flag exists.</summary>
        public static void InjectIntoMainMenuScene(bool forceRebuild = false)
        {
            Scene scene = EditorSceneManager.OpenScene(MainMenuScenePath, OpenSceneMode.Single);

            // Orphan preview worlds from prior rebuilds leak empty space / cameras.
            for (int i = 0; i < scene.rootCount; i++)
            {
                GameObject rootGo = scene.GetRootGameObjects()[i];
                if (rootGo != null && rootGo.name == "ProfileUfoPreviewWorld")
                {
                    UnityEngine.Object.DestroyImmediate(rootGo);
                }
            }

            Transform safeArea = FindSafeArea(scene);
            if (safeArea == null)
            {
                Debug.LogError("[ProfilePanelBuilder] SafeArea not found in MainMenu.");
                return;
            }

            Transform existing = safeArea.Find("ProfileRoot");
            if (existing != null)
            {
                if (!forceRebuild)
                {
                    EnsureWiring(scene, existing.GetComponent<ProfileView>(), FindCompositionRoot(scene));
                    EditorSceneManager.MarkSceneDirty(scene);
                    EditorSceneManager.SaveScene(scene);
                    Debug.Log("[ProfilePanelBuilder] ProfileRoot already present — wiring refreshed.");
                    return;
                }

                UnityEngine.Object.DestroyImmediate(existing.gameObject);
            }

            GameObject compositionRoot = FindCompositionRoot(scene);
            if (compositionRoot == null)
            {
                Debug.LogError("[ProfilePanelBuilder] MainMenuCompositionRoot not found.");
                return;
            }

            GameObject menuContentRoot = GameObject.Find("MenuContentRoot");
            GameObject topHud = GameObject.Find("TopHUD");

            ProfileBuildResult built = BuildProfileOverlay(safeArea, compositionRoot, menuContentRoot, topHud);
            EnsureAvatarButtonOnHud(scene);
            EnsureWiring(scene, built.View, compositionRoot);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[ProfilePanelBuilder] Injected Profile overlay into MainMenu and saved.");
        }

        public static ProfileBuildResult BuildProfileOverlay(
            Transform safeArea,
            GameObject compositionRoot,
            GameObject menuContentRoot,
            GameObject topHud)
        {
            var root = new GameObject("ProfileRoot", typeof(RectTransform), typeof(CanvasGroup));
            root.transform.SetParent(safeArea, false);
            root.transform.SetAsLastSibling();
            RectTransform rootRect = root.GetComponent<RectTransform>();
            StretchFull(rootRect);
            CanvasGroup canvasGroup = root.GetComponent<CanvasGroup>();
            // Alpha 1 in scene so Edit Mode shows sprites when ProfileRoot is enabled.
            // ProfileView.Awake forces alpha 0 at runtime before Open.
            canvasGroup.alpha = 1f;
            canvasGroup.blocksRaycasts = true;
            canvasGroup.interactable = false;
            root.SetActive(false);

            Image bg = BuildFullScreenImage(root.transform, "Background", Color.white);
            bg.raycastTarget = true;
            Sprite bgSprite = LoadMainMenuSprite("BG/bg.png");
            if (bgSprite != null)
            {
                bg.sprite = bgSprite;
                bg.type = Image.Type.Simple;
                bg.preserveAspect = false;
                bg.color = Color.white;
            }
            else
            {
                bg.color = OverlayNavy;
            }

            var content = new GameObject("Content", typeof(RectTransform));
            content.transform.SetParent(root.transform, false);
            StretchFull(content.GetComponent<RectTransform>());

            RectTransform header = BuildHeader(content.transform, out Button backButton);
            RectTransform showcase = BuildShowcase(content.transform, out RawImage ufoImage);
            RectTransform card = BuildProfileCard(content.transform,
                out Image avatarImage,
                out Button editAvatarButton,
                out TMP_Text nameText,
                out Button editNameButton,
                out TMP_Text idText,
                out Button copyIdButton,
                out TMP_Text levelText,
                out TMP_Text energyText,
                out TMP_Text energyTimerText,
                out Image energyFill,
                out ProfileStatRowView powerRow,
                out ProfileStatRowView towersRow,
                out ProfileStatRowView campaignRow,
                out ProfileStatRowView damageRow,
                out ProfileStatRowView killsRow);

            Sprite hudAvatar = FindHudAvatarSprite();
            if (hudAvatar != null && avatarImage != null)
            {
                avatarImage.sprite = hudAvatar;
                avatarImage.color = Color.white;
            }

            (CanvasGroup toastGroup, TMP_Text toastText) = BuildToast(root.transform);
            EditNamePopupView editNamePopup = BuildEditNamePopup(root.transform);

            ProfileBottomBarView bottomBar = BuildProfileBottomBar(root.transform, avatarImage != null ? avatarImage.sprite : null);

            var view = root.AddComponent<ProfileView>();
            var serializedView = new SerializedObject(view);
            serializedView.FindProperty("_root").objectReferenceValue = root;
            serializedView.FindProperty("_canvasGroup").objectReferenceValue = canvasGroup;
            serializedView.FindProperty("_contentRoot").objectReferenceValue = content.GetComponent<RectTransform>();
            serializedView.FindProperty("_backButton").objectReferenceValue = backButton;
            serializedView.FindProperty("_editNameButton").objectReferenceValue = editNameButton;
            serializedView.FindProperty("_editAvatarButton").objectReferenceValue = editAvatarButton;
            serializedView.FindProperty("_copyIdButton").objectReferenceValue = copyIdButton;
            serializedView.FindProperty("_avatarImage").objectReferenceValue = avatarImage;
            serializedView.FindProperty("_playerNameText").objectReferenceValue = nameText;
            serializedView.FindProperty("_playerIdText").objectReferenceValue = idText;
            serializedView.FindProperty("_levelText").objectReferenceValue = levelText;
            serializedView.FindProperty("_energyText").objectReferenceValue = energyText;
            serializedView.FindProperty("_energyTimerText").objectReferenceValue = energyTimerText;
            serializedView.FindProperty("_energyFill").objectReferenceValue = energyFill;
            serializedView.FindProperty("_powerRow").objectReferenceValue = powerRow;
            serializedView.FindProperty("_towersRow").objectReferenceValue = towersRow;
            serializedView.FindProperty("_campaignRow").objectReferenceValue = campaignRow;
            serializedView.FindProperty("_damageRow").objectReferenceValue = damageRow;
            serializedView.FindProperty("_killsRow").objectReferenceValue = killsRow;
            serializedView.FindProperty("_toastGroup").objectReferenceValue = toastGroup;
            serializedView.FindProperty("_toastText").objectReferenceValue = toastText;
            serializedView.FindProperty("_editNamePopup").objectReferenceValue = editNamePopup;
            serializedView.FindProperty("_ufoPreviewImage").objectReferenceValue = ufoImage;
            serializedView.FindProperty("_header").objectReferenceValue = header;
            serializedView.FindProperty("_showcase").objectReferenceValue = showcase;
            serializedView.FindProperty("_card").objectReferenceValue = card;
            serializedView.ApplyModifiedPropertiesWithoutUndo();

            ProfileUfoPreviewController ufoPreview = BuildUfoPreviewWorld(ufoImage);

            ProfilePresenter presenter = compositionRoot.GetComponent<ProfilePresenter>();
            if (presenter == null)
            {
                presenter = compositionRoot.AddComponent<ProfilePresenter>();
            }

            var serializedPresenter = new SerializedObject(presenter);
            serializedPresenter.FindProperty("_view").objectReferenceValue = view;
            serializedPresenter.FindProperty("_ufoPreview").objectReferenceValue = ufoPreview;
            serializedPresenter.FindProperty("_menuContentRoot").objectReferenceValue = menuContentRoot;
            serializedPresenter.FindProperty("_topHudRoot").objectReferenceValue = topHud;
            serializedPresenter.FindProperty("_bottomBar").objectReferenceValue = bottomBar;
            GameObject bottomNav = GameObject.Find("BottomNavigationSlot");
            if (bottomNav != null)
            {
                serializedPresenter.FindProperty("_bottomNavigationRoot").objectReferenceValue = bottomNav;
            }

            serializedPresenter.ApplyModifiedPropertiesWithoutUndo();

            return new ProfileBuildResult(view, presenter, ufoPreview);
        }

        private static void EnsureWiring(Scene scene, ProfileView view, GameObject compositionRoot)
        {
            if (compositionRoot == null || view == null)
            {
                return;
            }

            MainMenuPresenter mainMenuPresenter = compositionRoot.GetComponent<MainMenuPresenter>();
            ProfilePresenter profilePresenter = compositionRoot.GetComponent<ProfilePresenter>();
            if (mainMenuPresenter != null && profilePresenter != null)
            {
                var serialized = new SerializedObject(mainMenuPresenter);
                serialized.FindProperty("_profilePresenter").objectReferenceValue = profilePresenter;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        private static void EnsureAvatarButtonOnHud(Scene scene)
        {
            PlayerProfileWidgetView widget = UnityEngine.Object.FindFirstObjectByType<PlayerProfileWidgetView>();
            if (widget == null)
            {
                return;
            }

            var serialized = new SerializedObject(widget);
            SerializedProperty avatarImageProp = serialized.FindProperty("_avatarImage");
            Image avatarImage = avatarImageProp.objectReferenceValue as Image;
            if (avatarImage == null)
            {
                return;
            }

            Button button = avatarImage.GetComponent<Button>();
            if (button == null)
            {
                button = avatarImage.gameObject.AddComponent<Button>();
            }

            avatarImage.raycastTarget = true;
            serialized.FindProperty("_avatarButton").objectReferenceValue = button;

            Transform extra = widget.transform.Find("ProfileExtraButton");
            if (extra != null)
            {
                Button extraButton = extra.GetComponent<Button>();
                if (extraButton != null)
                {
                    serialized.FindProperty("_extraButton").objectReferenceValue = extraButton;
                }
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static RectTransform BuildHeader(Transform parent, out Button backButton)
        {
            // Back lives on ProfileBottomBar only (reference layout).
            backButton = null;

            var header = new GameObject("Header", typeof(RectTransform));
            header.transform.SetParent(parent, false);
            RectTransform rect = header.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(0f, 88f);

            TMP_Text title = BuildAnchoredText(header.transform, "Title", "Player profile",
                new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(28f, 0f), new Vector2(520f, 64f), 40f,
                TextAlignmentOptions.MidlineLeft);
            title.fontStyle = FontStyles.Bold;
            title.color = Color.white;
            title.enableVertexGradient = false;

            return rect;
        }

        private static RectTransform BuildShowcase(Transform parent, out RawImage ufoImage)
        {
            var showcase = new GameObject("Showcase", typeof(RectTransform));
            showcase.transform.SetParent(parent, false);
            RectTransform rect = showcase.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            // Compact UFO band so ProfileCard can sit higher.
            rect.anchoredPosition = new Vector2(0f, -56f);
            rect.sizeDelta = new Vector2(600f, 500f);

            Image ring = BuildAnchoredImage(showcase.transform, "HoloRing", new Vector2(0.5f, 0.18f),
                new Vector2(320f, 80f), Vector2.zero, new Color(AccentCyan.r, AccentCyan.g, AccentCyan.b, 0.35f));
            ring.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
            ring.rectTransform.localScale = Vector3.one * 2f;

            var rawGo = new GameObject("UfoPreview", typeof(RectTransform), typeof(RawImage));
            rawGo.transform.SetParent(showcase.transform, false);
            RectTransform rawRect = rawGo.GetComponent<RectTransform>();
            rawRect.anchorMin = new Vector2(0.5f, 0.5f);
            rawRect.anchorMax = new Vector2(0.5f, 0.5f);
            rawRect.pivot = new Vector2(0.5f, 0.5f);
            rawRect.anchoredPosition = Vector2.zero;
            rawRect.sizeDelta = new Vector2(560f, 460f);
            ufoImage = rawGo.GetComponent<RawImage>();
            ufoImage.color = Color.white;
            ufoImage.raycastTarget = false;

            return rect;
        }

        private static RectTransform BuildProfileCard(
            Transform parent,
            out Image avatarImage,
            out Button editAvatarButton,
            out TMP_Text nameText,
            out Button editNameButton,
            out TMP_Text idText,
            out Button copyIdButton,
            out TMP_Text levelText,
            out TMP_Text energyText,
            out TMP_Text energyTimerText,
            out Image energyFill,
            out ProfileStatRowView powerRow,
            out ProfileStatRowView towersRow,
            out ProfileStatRowView campaignRow,
            out ProfileStatRowView damageRow,
            out ProfileStatRowView killsRow)
        {
            Sprite uiSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            Sprite knobSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");

            var card = new GameObject("ProfileCard", typeof(RectTransform), typeof(Image));
            card.transform.SetParent(parent, false);
            RectTransform rect = card.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            // Sit higher so UFO showcase stays a small band above the card.
            rect.anchoredPosition = new Vector2(0f, 420f);
            rect.sizeDelta = new Vector2(720f, 580f);
            Image cardImage = card.GetComponent<Image>();
            cardImage.sprite = uiSprite;
            cardImage.type = Image.Type.Sliced;
            cardImage.color = CardColor;
            var outline = card.AddComponent<Outline>();
            outline.effectColor = CardBorder;
            outline.effectDistance = new Vector2(5f, -5f);

            // Left column region
            var left = new GameObject("LeftColumn", typeof(RectTransform));
            left.transform.SetParent(card.transform, false);
            RectTransform leftRect = left.GetComponent<RectTransform>();
            leftRect.anchorMin = new Vector2(0f, 0f);
            leftRect.anchorMax = new Vector2(0.34f, 1f);
            leftRect.offsetMin = new Vector2(16f, 16f);
            leftRect.offsetMax = new Vector2(-8f, -16f);

            var avatarGo = new GameObject("Avatar", typeof(RectTransform), typeof(Image));
            avatarGo.transform.SetParent(left.transform, false);
            RectTransform avatarRect = avatarGo.GetComponent<RectTransform>();
            avatarRect.anchorMin = new Vector2(0.5f, 1f);
            avatarRect.anchorMax = new Vector2(0.5f, 1f);
            avatarRect.pivot = new Vector2(0.5f, 1f);
            avatarRect.anchoredPosition = new Vector2(0f, -8f);
            avatarRect.sizeDelta = new Vector2(150f, 150f);
            avatarImage = avatarGo.GetComponent<Image>();
            avatarImage.sprite = uiSprite;
            avatarImage.type = Image.Type.Sliced;
            avatarImage.color = new Color(0.35f, 0.55f, 0.85f, 1f);

            var refreshGo = new GameObject("RefreshButton", typeof(RectTransform), typeof(Image), typeof(Button));
            refreshGo.transform.SetParent(avatarGo.transform, false);
            RectTransform refreshRect = refreshGo.GetComponent<RectTransform>();
            refreshRect.anchorMin = refreshRect.anchorMax = new Vector2(1f, 0f);
            refreshRect.pivot = new Vector2(0.5f, 0.5f);
            refreshRect.anchoredPosition = new Vector2(-4f, 4f);
            refreshRect.sizeDelta = new Vector2(42f, 42f);
            Image refreshImage = refreshGo.GetComponent<Image>();
            refreshImage.sprite = knobSprite;
            refreshImage.color = AccentBlue;
            TMP_Text refreshGlyph = BuildAnchoredText(refreshGo.transform, "Label", "R",
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(36f, 36f), 20f,
                TextAlignmentOptions.Center);
            refreshGlyph.color = Color.white;
            editAvatarButton = refreshGo.GetComponent<Button>();

            levelText = BuildAnchoredText(left.transform, "LevelText", "Level: 1",
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -170f), new Vector2(200f, 40f), 30f,
                TextAlignmentOptions.Center);
            levelText.fontStyle = FontStyles.Bold;
            levelText.color = AccentYellow;

            var energyRow = new GameObject("EnergyRow", typeof(RectTransform));
            energyRow.transform.SetParent(left.transform, false);
            RectTransform energyRect = energyRow.GetComponent<RectTransform>();
            energyRect.anchorMin = new Vector2(0f, 1f);
            energyRect.anchorMax = new Vector2(1f, 1f);
            energyRect.pivot = new Vector2(0.5f, 1f);
            energyRect.anchoredPosition = new Vector2(0f, -220f);
            energyRect.sizeDelta = new Vector2(0f, 40f);

            var bolt = new GameObject("Bolt", typeof(RectTransform), typeof(Image));
            bolt.transform.SetParent(energyRow.transform, false);
            RectTransform boltRect = bolt.GetComponent<RectTransform>();
            boltRect.anchorMin = boltRect.anchorMax = new Vector2(0f, 0.5f);
            boltRect.pivot = new Vector2(0f, 0.5f);
            boltRect.anchoredPosition = Vector2.zero;
            boltRect.sizeDelta = new Vector2(34f, 34f);
            bolt.GetComponent<Image>().sprite = knobSprite;
            bolt.GetComponent<Image>().color = AccentYellow;

            TMP_Text plus = BuildAnchoredText(bolt.transform, "Plus", "+",
                new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(2f, -2f), new Vector2(16f, 16f), 14f,
                TextAlignmentOptions.Center);
            plus.color = AccentGreen;
            plus.fontStyle = FontStyles.Bold;

            var track = new GameObject("Track", typeof(RectTransform), typeof(Image));
            track.transform.SetParent(energyRow.transform, false);
            RectTransform trackRect = track.GetComponent<RectTransform>();
            trackRect.anchorMin = new Vector2(0f, 0.5f);
            trackRect.anchorMax = new Vector2(1f, 0.5f);
            trackRect.pivot = new Vector2(0.5f, 0.5f);
            trackRect.anchoredPosition = new Vector2(12f, 0f);
            trackRect.offsetMin = new Vector2(40f, -14f);
            trackRect.offsetMax = new Vector2(0f, 14f);
            track.GetComponent<Image>().sprite = uiSprite;
            track.GetComponent<Image>().type = Image.Type.Sliced;
            track.GetComponent<Image>().color = new Color(0.05f, 0.05f, 0.08f, 0.92f);

            var fillGo = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            fillGo.transform.SetParent(track.transform, false);
            StretchFull(fillGo.GetComponent<RectTransform>());
            fillGo.GetComponent<RectTransform>().offsetMin = new Vector2(3f, 3f);
            fillGo.GetComponent<RectTransform>().offsetMax = new Vector2(-3f, -3f);
            energyFill = fillGo.GetComponent<Image>();
            energyFill.sprite = uiSprite;
            energyFill.type = Image.Type.Filled;
            energyFill.fillMethod = Image.FillMethod.Horizontal;
            energyFill.fillAmount = 0f;
            energyFill.color = new Color(0.95f, 0.75f, 0.15f, 1f);

            energyText = BuildAnchoredText(track.transform, "EnergyText", "—",
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(100f, 28f), 20f,
                TextAlignmentOptions.Center);
            energyText.color = Color.white;
            energyText.fontStyle = FontStyles.Bold;

            energyTimerText = BuildAnchoredText(energyRow.transform, "EnergyTimer", string.Empty,
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(12f, -14f), new Vector2(120f, 16f), 14f,
                TextAlignmentOptions.Center);
            energyTimerText.gameObject.SetActive(false);

            // Right column
            var right = new GameObject("RightColumn", typeof(RectTransform));
            right.transform.SetParent(card.transform, false);
            RectTransform rightRect = right.GetComponent<RectTransform>();
            rightRect.anchorMin = new Vector2(0.34f, 0f);
            rightRect.anchorMax = new Vector2(1f, 1f);
            rightRect.offsetMin = new Vector2(8f, 16f);
            rightRect.offsetMax = new Vector2(-16f, -16f);

            nameText = BuildAnchoredText(right.transform, "PlayerName", "Pilot",
                new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(-48f, -8f), new Vector2(-56f, 40f), 28f,
                TextAlignmentOptions.MidlineLeft);
            nameText.fontStyle = FontStyles.Bold;
            nameText.color = Color.white;
            // Fix sizeDelta for stretch: use offsets
            RectTransform nameRect = nameText.rectTransform;
            nameRect.anchorMin = new Vector2(0f, 1f);
            nameRect.anchorMax = new Vector2(1f, 1f);
            nameRect.pivot = new Vector2(0f, 1f);
            nameRect.anchoredPosition = new Vector2(0f, -4f);
            nameRect.sizeDelta = new Vector2(-52f, 40f);

            editNameButton = BuildSmallIconButtonAbsolute(right.transform, "EditNameButton", "E", AccentBlue,
                new Vector2(1f, 1f), new Vector2(-4f, -8f), new Vector2(40f, 40f));

            var idBar = new GameObject("IdRow", typeof(RectTransform), typeof(Image));
            idBar.transform.SetParent(right.transform, false);
            RectTransform idBarRect = idBar.GetComponent<RectTransform>();
            idBarRect.anchorMin = new Vector2(0f, 1f);
            idBarRect.anchorMax = new Vector2(1f, 1f);
            idBarRect.pivot = new Vector2(0.5f, 1f);
            idBarRect.anchoredPosition = new Vector2(0f, -52f);
            idBarRect.sizeDelta = new Vector2(0f, 44f);
            idBar.GetComponent<Image>().sprite = uiSprite;
            idBar.GetComponent<Image>().type = Image.Type.Sliced;
            idBar.GetComponent<Image>().color = new Color(0.45f, 0.7f, 0.95f, 0.9f);

            copyIdButton = BuildSmallIconButtonAbsolute(idBar.transform, "CopyIdButton", "C", new Color(1f, 1f, 1f, 0.35f),
                new Vector2(0f, 0.5f), new Vector2(6f, 0f), new Vector2(52f, 52f));
            idText = BuildAnchoredText(idBar.transform, "PlayerId", "ID: —",
                new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(20f, 0f), new Vector2(-44f, 50f), 34f,
                TextAlignmentOptions.MidlineLeft);
            idText.color = Color.white;
            RectTransform idTextRect = idText.rectTransform;
            idTextRect.anchorMin = new Vector2(0f, 0f);
            idTextRect.anchorMax = new Vector2(1f, 1f);
            idTextRect.offsetMin = new Vector2(48f, 2f);
            idTextRect.offsetMax = new Vector2(-8f, -2f);

            float statTop = -100f;
            float statStep = 58f;
            powerRow = BuildAnchoredStatRow(right.transform, "PowerRow", "Power", "0", new Color(0.3f, 0.85f, 0.4f, 1f), statTop);
            towersRow = BuildAnchoredStatRow(right.transform, "TowersRow", "Central Building", "0", new Color(0.9f, 0.35f, 0.3f, 1f), statTop - statStep);
            campaignRow = BuildAnchoredStatRow(right.transform, "CampaignRow", "Campaign mission", "—", new Color(0.3f, 0.75f, 0.95f, 1f), statTop - statStep * 2f);
            damageRow = BuildAnchoredStatRow(right.transform, "DamageRow", "Towers damage", "0", new Color(0.7f, 0.7f, 0.75f, 1f), statTop - statStep * 3f);
            killsRow = BuildAnchoredStatRow(right.transform, "KillsRow", "Zombies killed", "0", new Color(0.35f, 0.8f, 0.35f, 1f), statTop - statStep * 4f);

            return rect;
        }

        private static ProfileStatRowView BuildAnchoredStatRow(Transform parent, string name, string label, string value, Color iconColor, float yFromTop)
        {
            Sprite uiSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            Sprite knobSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");

            var row = new GameObject(name, typeof(RectTransform));
            row.transform.SetParent(parent, false);
            RectTransform rowRect = row.GetComponent<RectTransform>();
            rowRect.anchorMin = new Vector2(0f, 1f);
            rowRect.anchorMax = new Vector2(1f, 1f);
            rowRect.pivot = new Vector2(0.5f, 1f);
            rowRect.anchoredPosition = new Vector2(0f, yFromTop);
            rowRect.sizeDelta = new Vector2(0f, 56f);

            var iconGo = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            iconGo.transform.SetParent(row.transform, false);
            RectTransform iconRect = iconGo.GetComponent<RectTransform>();
            iconRect.anchorMin = iconRect.anchorMax = new Vector2(0f, 0.5f);
            iconRect.pivot = new Vector2(0f, 0.5f);
            iconRect.anchoredPosition = new Vector2(0f, 4f);
            iconRect.sizeDelta = new Vector2(64f, 64f);
            Image icon = iconGo.GetComponent<Image>();
            icon.sprite = knobSprite;
            icon.color = iconColor;

            TMP_Text labelText = BuildAnchoredText(row.transform, "Label", label,
                new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(20f, 4f), new Vector2(-140f, 50f), 42f,
                TextAlignmentOptions.MidlineLeft);
            labelText.color = DarkText;
            RectTransform labelRect = labelText.rectTransform;
            labelRect.anchorMin = new Vector2(0f, 0.5f);
            labelRect.anchorMax = new Vector2(1f, 0.5f);
            labelRect.pivot = new Vector2(0f, 0.5f);
            labelRect.anchoredPosition = new Vector2(72f, 4f);
            labelRect.sizeDelta = new Vector2(-180f, 50f);

            TMP_Text valueText = BuildAnchoredText(row.transform, "Value", value,
                new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-4f, 4f), new Vector2(120f, 50f), 44f,
                TextAlignmentOptions.MidlineRight);
            valueText.fontStyle = FontStyles.Bold;
            valueText.color = StatValueGreen;

            var sep = new GameObject("Sep", typeof(RectTransform), typeof(Image));
            sep.transform.SetParent(row.transform, false);
            RectTransform sepRect = sep.GetComponent<RectTransform>();
            sepRect.anchorMin = new Vector2(0f, 0f);
            sepRect.anchorMax = new Vector2(1f, 0f);
            sepRect.pivot = new Vector2(0.5f, 0f);
            sepRect.anchoredPosition = Vector2.zero;
            sepRect.sizeDelta = new Vector2(0f, 2f);
            sep.GetComponent<Image>().sprite = uiSprite;
            sep.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.5f);

            var view = row.AddComponent<ProfileStatRowView>();
            var serialized = new SerializedObject(view);
            serialized.FindProperty("_icon").objectReferenceValue = icon;
            serialized.FindProperty("_labelText").objectReferenceValue = labelText;
            serialized.FindProperty("_valueText").objectReferenceValue = valueText;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return view;
        }

        private static Button BuildSmallIconButtonAbsolute(
            Transform parent, string name, string glyph, Color bg, Vector2 anchor, Vector2 anchoredPos, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPos;
            rect.sizeDelta = size;
            Image image = go.GetComponent<Image>();
            image.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            image.type = Image.Type.Sliced;
            image.color = bg;
            TMP_Text label = BuildAnchoredText(go.transform, "Label", glyph,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, size, 20f,
                TextAlignmentOptions.Center);
            label.color = Color.white;
            return go.GetComponent<Button>();
        }

        private static ProfileBottomBarView BuildProfileBottomBar(Transform profileRoot, Sprite avatarSprite)
        {
            Transform existing = profileRoot.Find("ProfileBottomBar");
            if (existing != null)
            {
                UnityEngine.Object.DestroyImmediate(existing.gameObject);
            }

            var root = new GameObject("ProfileBottomBar", typeof(RectTransform), typeof(Image));
            root.transform.SetParent(profileRoot, false);
            RectTransform rootRect = root.GetComponent<RectTransform>();
            rootRect.anchorMin = new Vector2(0f, 0f);
            rootRect.anchorMax = new Vector2(1f, 0f);
            rootRect.pivot = new Vector2(0.5f, 0f);
            rootRect.anchoredPosition = Vector2.zero;
            rootRect.sizeDelta = new Vector2(0f, 190f);
            Image barBg = root.GetComponent<Image>();
            barBg.color = new Color(0.06f, 0.12f, 0.28f, 0.96f);

            var layoutGo = new GameObject("Row", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            layoutGo.transform.SetParent(root.transform, false);
            StretchFull(layoutGo.GetComponent<RectTransform>());
            RectTransform layoutRect = layoutGo.GetComponent<RectTransform>();
            layoutRect.offsetMin = new Vector2(24f, 16f);
            layoutRect.offsetMax = new Vector2(-24f, -16f);
            HorizontalLayoutGroup layout = layoutGo.GetComponent<HorizontalLayoutGroup>();
            layout.spacing = 20f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            layout.padding = new RectOffset(16, 16, 8, 8);

            Button back = BuildBottomBackSlot(layoutGo.transform);
            Button profile = BuildBottomProfileSlot(layoutGo.transform, avatarSprite, out Image profileTile);
            Button settings = BuildBottomSettingsSlot(layoutGo.transform, out Image settingsTile);

            ProfileBottomBarView barView = root.AddComponent<ProfileBottomBarView>();
            var serialized = new SerializedObject(barView);
            serialized.FindProperty("_root").objectReferenceValue = root;
            serialized.FindProperty("_backButton").objectReferenceValue = back;
            serialized.FindProperty("_profileButton").objectReferenceValue = profile;
            serialized.FindProperty("_settingsButton").objectReferenceValue = settings;
            serialized.FindProperty("_profileTile").objectReferenceValue = profileTile;
            serialized.FindProperty("_settingsTile").objectReferenceValue = settingsTile;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            root.SetActive(false);
            return barView;
        }

        private static Button BuildBottomBackSlot(Transform parent)
        {
            Sprite uiSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            Sprite knobSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");

            var go = new GameObject("BackButton", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            go.GetComponent<LayoutElement>().preferredWidth = 120f;
            go.GetComponent<LayoutElement>().preferredHeight = 140f;
            go.GetComponent<LayoutElement>().flexibleWidth = 0.6f;
            Image tile = go.GetComponent<Image>();
            tile.sprite = uiSprite;
            tile.type = Image.Type.Sliced;
            tile.color = AccentBlue;

            var circle = new GameObject("Circle", typeof(RectTransform), typeof(Image));
            circle.transform.SetParent(go.transform, false);
            RectTransform circleRect = circle.GetComponent<RectTransform>();
            circleRect.anchorMin = circleRect.anchorMax = new Vector2(0.5f, 0.5f);
            circleRect.sizeDelta = new Vector2(72f, 72f);
            Image circleImage = circle.GetComponent<Image>();
            circleImage.sprite = knobSprite;
            circleImage.color = new Color(0.55f, 0.85f, 1f, 1f);

            TMP_Text label = BuildAnchoredText(circle.transform, "Label", "<",
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(60f, 60f), 40f,
                TextAlignmentOptions.Center);
            label.color = Color.white;
            label.fontStyle = FontStyles.Bold;
            return go.GetComponent<Button>();
        }

        private static Button BuildBottomProfileSlot(Transform parent, Sprite avatarSprite, out Image tile)
        {
            Sprite uiSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");

            var go = new GameObject("ProfileButton", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            go.GetComponent<LayoutElement>().preferredWidth = 180f;
            go.GetComponent<LayoutElement>().preferredHeight = 140f;
            go.GetComponent<LayoutElement>().flexibleWidth = 1f;
            tile = go.GetComponent<Image>();
            tile.sprite = uiSprite;
            tile.type = Image.Type.Sliced;
            tile.color = AccentYellow;

            var avatar = new GameObject("Avatar", typeof(RectTransform), typeof(Image));
            avatar.transform.SetParent(go.transform, false);
            RectTransform avatarRect = avatar.GetComponent<RectTransform>();
            avatarRect.anchorMin = avatarRect.anchorMax = new Vector2(0.5f, 0.62f);
            avatarRect.sizeDelta = new Vector2(56f, 56f);
            Image avatarImage = avatar.GetComponent<Image>();
            avatarImage.sprite = avatarSprite != null ? avatarSprite : uiSprite;
            avatarImage.color = Color.white;

            TMP_Text label = BuildAnchoredText(go.transform, "Label", "Profile",
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -42f), new Vector2(160f, 36f), 26f,
                TextAlignmentOptions.Center);
            label.color = Color.white;
            label.fontStyle = FontStyles.Bold;
            return go.GetComponent<Button>();
        }

        private static Button BuildBottomSettingsSlot(Transform parent, out Image tile)
        {
            Sprite uiSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");

            var go = new GameObject("SettingsButton", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            go.GetComponent<LayoutElement>().preferredWidth = 180f;
            go.GetComponent<LayoutElement>().preferredHeight = 140f;
            go.GetComponent<LayoutElement>().flexibleWidth = 1f;
            tile = go.GetComponent<Image>();
            tile.sprite = uiSprite;
            tile.type = Image.Type.Sliced;
            tile.color = AccentBlue;

            TMP_Text gear = BuildAnchoredText(go.transform, "Gear", "S",
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 18f), new Vector2(80f, 60f), 42f,
                TextAlignmentOptions.Center);
            gear.color = Color.white;

            TMP_Text label = BuildAnchoredText(go.transform, "Label", "Settings",
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -42f), new Vector2(160f, 36f), 26f,
                TextAlignmentOptions.Center);
            label.color = Color.white;
            label.fontStyle = FontStyles.Bold;
            return go.GetComponent<Button>();
        }

        private static (CanvasGroup group, TMP_Text text) BuildToast(Transform parent)
        {
            var toast = new GameObject("Toast", typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
            toast.transform.SetParent(parent, false);
            RectTransform rect = toast.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = new Vector2(0f, 180f);
            rect.sizeDelta = new Vector2(280f, 56f);
            toast.GetComponent<Image>().color = new Color(0.05f, 0.1f, 0.18f, 0.92f);
            CanvasGroup group = toast.GetComponent<CanvasGroup>();
            group.alpha = 0f;
            toast.SetActive(false);

            TMP_Text text = BuildAnchoredText(toast.transform, "ToastText", "ID copied",
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(260f, 48f), 24f,
                TextAlignmentOptions.Center);
            return (group, text);
        }

        private static EditNamePopupView BuildEditNamePopup(Transform parent)
        {
            var popupRoot = new GameObject("EditNamePopup", typeof(RectTransform));
            popupRoot.transform.SetParent(parent, false);
            StretchFull(popupRoot.GetComponent<RectTransform>());

            Image dim = BuildFullScreenImage(popupRoot.transform, "Dim", new Color(0f, 0f, 0f, 0.65f));
            dim.raycastTarget = true;

            var panel = new GameObject("Panel", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup));
            panel.transform.SetParent(popupRoot.transform, false);
            RectTransform panelRect = panel.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.sizeDelta = new Vector2(560f, 280f);
            panel.GetComponent<Image>().color = PanelColor;
            VerticalLayoutGroup panelLayout = panel.GetComponent<VerticalLayoutGroup>();
            panelLayout.padding = new RectOffset(24, 24, 24, 24);
            panelLayout.spacing = 16f;
            panelLayout.childAlignment = TextAnchor.UpperCenter;
            panelLayout.childControlWidth = true;
            panelLayout.childForceExpandWidth = true;

            TMP_Text title = BuildStatRow(panel.transform, "Title", "Edit Name");
            title.fontSize = 30f;
            title.fontStyle = FontStyles.Bold;
            title.alignment = TextAlignmentOptions.Center;

            var inputGo = new GameObject("Input", typeof(RectTransform), typeof(Image), typeof(TMP_InputField), typeof(LayoutElement));
            inputGo.transform.SetParent(panel.transform, false);
            inputGo.GetComponent<LayoutElement>().preferredHeight = 56f;
            inputGo.GetComponent<Image>().color = new Color(0.05f, 0.08f, 0.12f, 1f);
            TMP_InputField input = inputGo.GetComponent<TMP_InputField>();

            var textArea = new GameObject("Text Area", typeof(RectTransform), typeof(RectMask2D));
            textArea.transform.SetParent(inputGo.transform, false);
            StretchFull(textArea.GetComponent<RectTransform>(), 8f);

            TMP_Text placeholder = CreateTmpChild(textArea.transform, "Placeholder", "Name...", 26f, new Color(1f, 1f, 1f, 0.35f));
            TMP_Text inputText = CreateTmpChild(textArea.transform, "Text", string.Empty, 26f, Color.white);
            input.textViewport = textArea.GetComponent<RectTransform>();
            input.textComponent = inputText;
            input.placeholder = placeholder;
            input.characterLimit = 20;

            TMP_Text errorText = BuildStatRow(panel.transform, "Error", string.Empty);
            errorText.fontSize = 20f;
            errorText.color = new Color(1f, 0.4f, 0.35f, 1f);
            errorText.gameObject.SetActive(false);

            var buttons = new GameObject("Buttons", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
            buttons.transform.SetParent(panel.transform, false);
            buttons.GetComponent<LayoutElement>().preferredHeight = 56f;
            HorizontalLayoutGroup btnLayout = buttons.GetComponent<HorizontalLayoutGroup>();
            btnLayout.spacing = 16f;
            btnLayout.childAlignment = TextAnchor.MiddleCenter;
            btnLayout.childForceExpandWidth = true;

            Button cancel = BuildPopupButton(buttons.transform, "CancelButton", "Cancel", new Color(0.25f, 0.3f, 0.4f, 1f));
            Button save = BuildPopupButton(buttons.transform, "SaveButton", "Save", AccentGreen);

            var view = popupRoot.AddComponent<EditNamePopupView>();
            var serialized = new SerializedObject(view);
            serialized.FindProperty("_root").objectReferenceValue = popupRoot;
            serialized.FindProperty("_input").objectReferenceValue = input;
            serialized.FindProperty("_cancelButton").objectReferenceValue = cancel;
            serialized.FindProperty("_saveButton").objectReferenceValue = save;
            serialized.FindProperty("_errorText").objectReferenceValue = errorText;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            popupRoot.SetActive(false);
            return view;
        }

        private static Button BuildPopupButton(Transform parent, string name, string label, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            go.GetComponent<LayoutElement>().flexibleWidth = 1f;
            go.GetComponent<Image>().color = color;
            BuildAnchoredText(go.transform, "Label", label,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(160f, 48f), 26f,
                TextAlignmentOptions.Center);
            return go.GetComponent<Button>();
        }

        private static TMP_Text CreateTmpChild(Transform parent, string name, string text, float size, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            StretchFull(go.GetComponent<RectTransform>());
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = size;
            tmp.color = color;
            tmp.alignment = TextAlignmentOptions.MidlineLeft;
            ApplyDefaultUiFont(tmp);
            return tmp;
        }

        private static ProfileUfoPreviewController BuildUfoPreviewWorld(RawImage targetImage)
        {
            int previewLayer = LayerMask.NameToLayer(PreviewLayerName);
            var worldRoot = new GameObject("ProfileUfoPreviewWorld");
            worldRoot.transform.position = new Vector3(1000f, 1000f, 1000f);
            if (previewLayer >= 0)
            {
                ProfileUfoPreviewController.ApplyLayerRecursive(worldRoot.transform, previewLayer);
            }

            var cameraGo = new GameObject("ProfileUfoPreviewCamera", typeof(Camera));
            cameraGo.transform.SetParent(worldRoot.transform, false);
            cameraGo.transform.localPosition = new Vector3(0f, 1.2f, -4.5f);
            cameraGo.transform.localRotation = Quaternion.Euler(15f, 0f, 0f);
            Camera cam = cameraGo.GetComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0f, 0f, 0f, 0f);
            cam.orthographic = false;
            cam.fieldOfView = 30f;
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 80f;
            cam.enabled = false;
            cam.depth = -20;
            cam.allowHDR = false;
            cam.allowMSAA = false;
            if (previewLayer >= 0)
            {
                cam.cullingMask = 1 << previewLayer;
                cameraGo.layer = previewLayer;
            }

            BuildPreviewLights(worldRoot.transform, previewLayer);

            var ufoHolder = new GameObject("UfoRoot").transform;
            ufoHolder.SetParent(worldRoot.transform, false);
            ufoHolder.localPosition = Vector3.zero;
            if (previewLayer >= 0)
            {
                ufoHolder.gameObject.layer = previewLayer;
            }

            var modelPivot = new GameObject("ModelPivot").transform;
            modelPivot.SetParent(ufoHolder, false);

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(UfoPrefabPath);
            if (prefab == null)
            {
                prefab = AssetDatabase.LoadAssetAtPath<GameObject>(UfoGameplayPrefabPath);
            }

            if (prefab != null)
            {
                GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, modelPivot);
                instance.name = "UFO_ProfilePreview";
                instance.transform.localPosition = Vector3.zero;
                instance.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
                instance.transform.localScale = Vector3.one * 2.55f;
                ProfileUfoPreviewController.StripGameplay(instance);
                if (previewLayer >= 0)
                {
                    ProfileUfoPreviewController.ApplyLayerRecursive(instance.transform, previewLayer);
                }
            }
            else
            {
                var placeholder = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                placeholder.name = "UfoPlaceholder";
                placeholder.transform.SetParent(modelPivot, false);
                UnityEngine.Object.DestroyImmediate(placeholder.GetComponent<Collider>());
            }

            if (targetImage != null)
            {
                targetImage.raycastTarget = false;
                targetImage.color = Color.white;
            }

            var controller = worldRoot.AddComponent<ProfileUfoPreviewController>();
            var serialized = new SerializedObject(controller);
            serialized.FindProperty("_previewCamera").objectReferenceValue = cam;
            serialized.FindProperty("_ufoRoot").objectReferenceValue = ufoHolder;
            serialized.FindProperty("_modelPivot").objectReferenceValue = modelPivot;
            serialized.FindProperty("_targetImage").objectReferenceValue = targetImage;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return controller;
        }

        private static void BuildPreviewLights(Transform worldRoot, int previewLayer)
        {
            Light key = CreatePreviewLight(worldRoot, "KeyLight", new Color(0.85f, 0.96f, 1f), 1.2f,
                new Vector3(30f, -40f, 0f));
            Light fill = CreatePreviewLight(worldRoot, "FillLight", new Color(0.43f, 0.87f, 1f), 0.5f,
                new Vector3(20f, 140f, 0f));
            Light rim = CreatePreviewLight(worldRoot, "RimLight", new Color(0.13f, 0.9f, 1f), 0.55f,
                new Vector3(-25f, 180f, 0f));
            if (previewLayer >= 0)
            {
                key.gameObject.layer = previewLayer;
                fill.gameObject.layer = previewLayer;
                rim.gameObject.layer = previewLayer;
            }
        }

        private static Light CreatePreviewLight(Transform parent, string name, Color color, float intensity, Vector3 euler)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localRotation = Quaternion.Euler(euler);
            Light light = go.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = color;
            light.intensity = intensity;
            light.shadows = LightShadows.None;
            return light;
        }

        private static Transform FindSafeArea(Scene scene)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                Transform canvas = root.name == "Canvas" ? root.transform : root.transform.Find("Canvas");
                if (canvas == null && root.GetComponent<Canvas>() != null)
                {
                    canvas = root.transform;
                }

                if (canvas == null)
                {
                    continue;
                }

                Transform safe = canvas.Find("SafeArea");
                if (safe != null)
                {
                    return safe;
                }

                safe = FindDeepChild(canvas, "SafeArea");
                if (safe != null)
                {
                    return safe;
                }
            }

            return null;
        }

        private static GameObject FindCompositionRoot(Scene scene)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.name == "MainMenuCompositionRoot")
                {
                    return root;
                }

                if (root.GetComponent<MainMenuPresenter>() != null)
                {
                    return root;
                }
            }

            return UnityEngine.Object.FindFirstObjectByType<MainMenuPresenter>()?.gameObject;
        }

        private const string MainMenuSpriteFolder = "Assets/_Game/Art/Sprite/MainMenu/";

        private static Sprite LoadMainMenuSprite(string fileName)
        {
            string path = MainMenuSpriteFolder + fileName;
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null)
            {
                Debug.LogWarning($"[ProfilePanelBuilder] Sprite not found: {path}");
            }

            return sprite;
        }

        private static Sprite FindHudAvatarSprite()
        {
            PlayerProfileWidgetView widget = UnityEngine.Object.FindFirstObjectByType<PlayerProfileWidgetView>();
            if (widget == null)
            {
                return null;
            }

            Image[] images = widget.GetComponentsInChildren<Image>(true);
            for (int i = 0; i < images.Length; i++)
            {
                Image image = images[i];
                if (image != null && image.sprite != null &&
                    (image.name.IndexOf("avatar", StringComparison.OrdinalIgnoreCase) >= 0 ||
                     image.transform.parent != null &&
                     image.transform.parent.name.IndexOf("avatar", StringComparison.OrdinalIgnoreCase) >= 0))
                {
                    return image.sprite;
                }
            }

            for (int i = 0; i < images.Length; i++)
            {
                if (images[i] != null && images[i].sprite != null && images[i].type == Image.Type.Simple)
                {
                    return images[i].sprite;
                }
            }

            return null;
        }

        private static Transform FindDeepChild(Transform parent, string name)
        {
            foreach (Transform child in parent)
            {
                if (child.name == name)
                {
                    return child;
                }

                Transform found = FindDeepChild(child, name);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }

        private static void StretchFull(RectTransform rect, float inset = 0f)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(inset, inset);
            rect.offsetMax = new Vector2(-inset, -inset);
        }

        /// <summary>Writes a pending flag so the next domain reload injects Profile into MainMenu automatically.</summary>
        public static void RequestAutoInject()
        {
            EditorFolderUtility.EnsureFolder("Assets/_Game/EditorTemp");
            System.IO.File.WriteAllText(PendingFlagPath, "1");
            AssetDatabase.Refresh();
            Debug.Log("[ProfilePanelBuilder] Auto-inject pending on next domain reload.");
        }
    }

    /// <summary>Runs Profile inject when the pending flag is imported OR after a full domain reload.
    /// Hot-reload alone does not re-run InitializeOnLoad static ctors, so AssetPostprocessor is required.</summary>
    internal sealed class ProfileOverlayAutoInject : AssetPostprocessor
    {
        private const string PendingFlagPath = "Assets/_Game/EditorTemp/profile_overlay_inject.pending";

        [InitializeOnLoadMethod]
        private static void OnDomainReload()
        {
            EditorApplication.delayCall += TryInject;
        }

        private static void OnPostprocessAllAssets(
            string[] importedAssets,
            string[] deletedAssets,
            string[] movedAssets,
            string[] movedFromAssetPaths)
        {
            if (importedAssets == null)
            {
                return;
            }

            for (int i = 0; i < importedAssets.Length; i++)
            {
                if (string.Equals(importedAssets[i], PendingFlagPath, StringComparison.OrdinalIgnoreCase))
                {
                    EditorApplication.delayCall += TryInject;
                    return;
                }
            }
        }

        private static void TryInject()
        {
            if (!System.IO.File.Exists(PendingFlagPath))
            {
                return;
            }

            try
            {
                System.IO.File.Delete(PendingFlagPath);
                string meta = PendingFlagPath + ".meta";
                if (System.IO.File.Exists(meta))
                {
                    System.IO.File.Delete(meta);
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning("[ProfileOverlayAutoInject] Could not delete pending flag: " + ex.Message);
            }

            try
            {
                ProfilePanelBuilder.InjectIntoMainMenuScene(forceRebuild: true);
            }
            catch (System.Exception ex)
            {
                Debug.LogError("[ProfileOverlayAutoInject] Inject failed: " + ex);
            }
        }
    }

    /// <summary>Patches Profile TMP fonts, adds Profile 3-tab bottom bar + Settings overlay, wires chrome swap.</summary>
    internal static class ProfileNavAndFontFixer
    {
        private const string MainMenuScenePath = "Assets/_Game/Scenes/Menu/MainMenu.unity";
        private const string PendingFlagPath = "Assets/_Game/EditorTemp/profile_nav_fix.pending";

        private static readonly Color AccentCyan = new Color(0.3f, 0.8f, 0.95f, 1f);
        private static readonly Color AccentYellow = new Color(0.95f, 0.85f, 0.2f, 1f);
        private static readonly Color AccentBlue = new Color(0.2f, 0.45f, 0.85f, 1f);
        private static readonly Color PanelColor = new Color(0.08f, 0.12f, 0.22f, 0.96f);

        [MenuItem("AlienDefense/Setup/16. Fix Profile Fonts + Bottom Nav")]
        public static void FixMenuItem()
        {
            FixMainMenuScene();
        }

        public static void FixMainMenuScene()
        {
            // Full visual rebuild of ProfileRoot (2-column card + bottom chrome).
            ProfilePanelBuilder.InjectIntoMainMenuScene(forceRebuild: true);

            Scene scene = EditorSceneManager.GetActiveScene();
            Transform safeArea = FindDeep(scene, "SafeArea");
            Transform profileRoot = safeArea != null ? FindDirect(safeArea, "ProfileRoot") : null;
            if (profileRoot == null)
            {
                Debug.LogError("[ProfileNavAndFontFixer] ProfileRoot missing after rebuild.");
                return;
            }

            PatchFonts(profileRoot);
            ProfileBottomBarView bottomBar = profileRoot.GetComponentInChildren<ProfileBottomBarView>(true);
            if (bottomBar == null)
            {
                bottomBar = EnsureBottomBar(profileRoot);
            }

            ProfileSettingsOverlayView settings = EnsureSettingsOverlay(profileRoot);
            WirePresenter(scene, safeArea, profileRoot, bottomBar, settings);
            profileRoot.SetAsLastSibling();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[ProfileNavAndFontFixer] Profile restyle + fonts + settings wired and saved.");
        }

        private static void PatchFonts(Transform profileRoot)
        {
            TextMeshProUGUI[] texts = profileRoot.GetComponentsInChildren<TextMeshProUGUI>(true);
            int patched = 0;
            for (int i = 0; i < texts.Length; i++)
            {
                ApplyDefaultUiFont(texts[i]);
                patched++;
            }

            Debug.Log("[ProfileNavAndFontFixer] Patched " + patched + " TMP texts under ProfileRoot.");
        }

        private static ProfileBottomBarView EnsureBottomBar(Transform profileRoot)
        {
            Transform existing = profileRoot.Find("ProfileBottomBar");
            if (existing != null)
            {
                ProfileBottomBarView view = existing.GetComponent<ProfileBottomBarView>();
                if (view != null)
                {
                    return view;
                }
            }

            if (existing != null)
            {
                UnityEngine.Object.DestroyImmediate(existing.gameObject);
            }

            var root = new GameObject("ProfileBottomBar", typeof(RectTransform));
            root.transform.SetParent(profileRoot, false);
            RectTransform rootRect = root.GetComponent<RectTransform>();
            rootRect.anchorMin = new Vector2(0f, 0f);
            rootRect.anchorMax = new Vector2(1f, 0f);
            rootRect.pivot = new Vector2(0.5f, 0f);
            rootRect.anchoredPosition = Vector2.zero;
            rootRect.sizeDelta = new Vector2(0f, 200f);

            var layoutGo = new GameObject("Row", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            layoutGo.transform.SetParent(root.transform, false);
            Stretch(layoutGo.GetComponent<RectTransform>(), 24f, 16f, 24f, 24f);
            HorizontalLayoutGroup layout = layoutGo.GetComponent<HorizontalLayoutGroup>();
            layout.spacing = 24f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            layout.padding = new RectOffset(32, 32, 12, 12);

            Button back = BuildCircleSlot(layoutGo.transform, "BackButton", "<", Color.white, AccentCyan, out _);
            Button profile = BuildTileSlot(layoutGo.transform, "ProfileButton", "Profile", AccentYellow, out Image profileTile);
            Button settings = BuildTileSlot(layoutGo.transform, "SettingsButton", "Settings", AccentBlue, out Image settingsTile);

            ProfileBottomBarView barView = root.AddComponent<ProfileBottomBarView>();
            var serialized = new SerializedObject(barView);
            serialized.FindProperty("_root").objectReferenceValue = root;
            serialized.FindProperty("_backButton").objectReferenceValue = back;
            serialized.FindProperty("_profileButton").objectReferenceValue = profile;
            serialized.FindProperty("_settingsButton").objectReferenceValue = settings;
            serialized.FindProperty("_profileTile").objectReferenceValue = profileTile;
            serialized.FindProperty("_settingsTile").objectReferenceValue = settingsTile;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            root.SetActive(false);
            return barView;
        }

        private static Button BuildCircleSlot(Transform parent, string name, string glyph, Color bg, Color glyphColor, out Image tile)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            go.GetComponent<LayoutElement>().preferredWidth = 96f;
            go.GetComponent<LayoutElement>().preferredHeight = 96f;
            go.GetComponent<LayoutElement>().flexibleWidth = 0f;
            tile = go.GetComponent<Image>();
            tile.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
            tile.color = bg;
            TMP_Text label = BuildAnchoredText(go.transform, "Label", glyph,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(80f, 80f), 40f,
                TextAlignmentOptions.Center);
            label.color = glyphColor;
            label.fontStyle = FontStyles.Bold;
            return go.GetComponent<Button>();
        }

        private static Button BuildTileSlot(Transform parent, string name, string caption, Color bg, out Image tile)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            go.GetComponent<LayoutElement>().preferredWidth = 160f;
            go.GetComponent<LayoutElement>().preferredHeight = 140f;
            go.GetComponent<LayoutElement>().flexibleWidth = 1f;
            tile = go.GetComponent<Image>();
            tile.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            tile.color = bg;
            TMP_Text label = BuildAnchoredText(go.transform, "Label", caption,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -36f), new Vector2(140f, 40f), 26f,
                TextAlignmentOptions.Center);
            label.color = Color.white;
            label.fontStyle = FontStyles.Bold;
            return go.GetComponent<Button>();
        }

        private static ProfileSettingsOverlayView EnsureSettingsOverlay(Transform profileRoot)
        {
            Transform existing = profileRoot.Find("SettingsOverlay");
            if (existing != null)
            {
                ProfileSettingsOverlayView view = existing.GetComponent<ProfileSettingsOverlayView>();
                if (view != null)
                {
                    return view;
                }

                UnityEngine.Object.DestroyImmediate(existing.gameObject);
            }

            var root = new GameObject("SettingsOverlay", typeof(RectTransform));
            root.transform.SetParent(profileRoot, false);
            StretchFull(root.GetComponent<RectTransform>());

            Image dim = BuildFullScreenImage(root.transform, "Dim", new Color(0f, 0f, 0f, 0.55f));
            dim.raycastTarget = true;

            var panel = new GameObject("Panel", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup));
            panel.transform.SetParent(root.transform, false);
            RectTransform panelRect = panel.GetComponent<RectTransform>();
            panelRect.anchorMin = panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.sizeDelta = new Vector2(560f, 420f);
            panel.GetComponent<Image>().color = PanelColor;
            VerticalLayoutGroup vlg = panel.GetComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(28, 28, 28, 28);
            vlg.spacing = 18f;
            vlg.childControlWidth = true;
            vlg.childForceExpandWidth = true;

            TMP_Text title = BuildStatRow(panel.transform, "Title", "Settings");
            title.fontSize = 34f;
            title.fontStyle = FontStyles.Bold;
            title.alignment = TextAlignmentOptions.Center;

            BuildToggleRow(panel.transform, "SfxRow", "SFX", out Button sfxBtn, out Image sfxIcon, out TMP_Text sfxLabel);
            BuildToggleRow(panel.transform, "MusicRow", "Music", out Button musicBtn, out Image musicIcon, out TMP_Text musicLabel);
            BuildToggleRow(panel.transform, "VibrationRow", "Vibration", out Button vibBtn, out Image vibIcon, out TMP_Text vibLabel);

            Button close = BuildPopupClose(panel.transform, "CloseButton", "Close");

            ProfileSettingsOverlayView overlay = root.AddComponent<ProfileSettingsOverlayView>();
            var serialized = new SerializedObject(overlay);
            serialized.FindProperty("_root").objectReferenceValue = root;
            serialized.FindProperty("_closeButton").objectReferenceValue = close;
            serialized.FindProperty("_sfxButton").objectReferenceValue = sfxBtn;
            serialized.FindProperty("_musicButton").objectReferenceValue = musicBtn;
            serialized.FindProperty("_vibrationButton").objectReferenceValue = vibBtn;
            serialized.FindProperty("_sfxIcon").objectReferenceValue = sfxIcon;
            serialized.FindProperty("_musicIcon").objectReferenceValue = musicIcon;
            serialized.FindProperty("_vibrationIcon").objectReferenceValue = vibIcon;
            serialized.FindProperty("_sfxLabel").objectReferenceValue = sfxLabel;
            serialized.FindProperty("_musicLabel").objectReferenceValue = musicLabel;
            serialized.FindProperty("_vibrationLabel").objectReferenceValue = vibLabel;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            root.SetActive(false);
            return overlay;
        }

        private static void BuildToggleRow(
            Transform parent,
            string name,
            string label,
            out Button button,
            out Image icon,
            out TMP_Text labelText)
        {
            var row = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
            row.transform.SetParent(parent, false);
            row.GetComponent<LayoutElement>().preferredHeight = 72f;
            row.GetComponent<Image>().color = new Color(0.05f, 0.08f, 0.14f, 0.9f);
            HorizontalLayoutGroup layout = row.GetComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(16, 16, 8, 8);
            layout.spacing = 16f;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childForceExpandWidth = true;

            var iconGo = new GameObject("Icon", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            iconGo.transform.SetParent(row.transform, false);
            iconGo.GetComponent<LayoutElement>().preferredWidth = 48f;
            iconGo.GetComponent<LayoutElement>().preferredHeight = 48f;
            icon = iconGo.GetComponent<Image>();
            icon.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
            icon.color = Color.white;

            labelText = BuildStatRow(row.transform, "Label", label + "  ON");
            labelText.fontSize = 26f;

            button = row.GetComponent<Button>();
        }

        private static Button BuildPopupClose(Transform parent, string name, string caption)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            go.GetComponent<LayoutElement>().preferredHeight = 64f;
            go.GetComponent<Image>().color = AccentCyan;
            TMP_Text label = BuildAnchoredText(go.transform, "Label", caption,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(200f, 48f), 28f,
                TextAlignmentOptions.Center);
            label.fontStyle = FontStyles.Bold;
            return go.GetComponent<Button>();
        }

        private static void WirePresenter(
            Scene scene,
            Transform safeArea,
            Transform profileRoot,
            ProfileBottomBarView bottomBar,
            ProfileSettingsOverlayView settings)
        {
            ProfilePresenter presenter = UnityEngine.Object.FindFirstObjectByType<ProfilePresenter>();
            if (presenter == null)
            {
                Debug.LogError("[ProfileNavAndFontFixer] ProfilePresenter missing.");
                return;
            }

            GameObject bottomNav = FindDeep(scene, "BottomNavigationSlot")?.gameObject;
            Sprite avatarSprite = null;
            PlayerProfileWidgetView widget = UnityEngine.Object.FindFirstObjectByType<PlayerProfileWidgetView>();
            if (widget != null)
            {
                var widgetSo = new SerializedObject(widget);
                Image avatarImage = widgetSo.FindProperty("_avatarImage").objectReferenceValue as Image;
                if (avatarImage != null)
                {
                    avatarSprite = avatarImage.sprite;
                }
            }

            var serialized = new SerializedObject(presenter);
            serialized.FindProperty("_bottomBar").objectReferenceValue = bottomBar;
            serialized.FindProperty("_settingsOverlay").objectReferenceValue = settings;
            if (bottomNav != null)
            {
                serialized.FindProperty("_bottomNavigationRoot").objectReferenceValue = bottomNav;
            }

            if (avatarSprite != null)
            {
                serialized.FindProperty("_defaultAvatarSprite").objectReferenceValue = avatarSprite;
            }

            Transform menuContent = FindDeep(scene, "MenuContentRoot");
            Transform topHud = FindDirect(safeArea, "TopHUD");
            if (menuContent != null)
            {
                serialized.FindProperty("_menuContentRoot").objectReferenceValue = menuContent.gameObject;
            }

            if (topHud != null)
            {
                serialized.FindProperty("_topHudRoot").objectReferenceValue = topHud.gameObject;
            }

            ProfileView profileView = profileRoot.GetComponent<ProfileView>();
            if (profileView != null)
            {
                serialized.FindProperty("_view").objectReferenceValue = profileView;
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void StretchFull(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static void Stretch(RectTransform rect, float left, float bottom, float right, float top)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(-right, -top);
        }

        private static Transform FindDeep(Scene scene, string name)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                Transform found = FindDeep(root.transform, name);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }

        private static Transform FindDeep(Transform parent, string name)
        {
            if (parent.name == name)
            {
                return parent;
            }

            for (int i = 0; i < parent.childCount; i++)
            {
                Transform found = FindDeep(parent.GetChild(i), name);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }

        private static Transform FindDirect(Transform parent, string name)
        {
            for (int i = 0; i < parent.childCount; i++)
            {
                if (parent.GetChild(i).name == name)
                {
                    return parent.GetChild(i);
                }
            }

            return null;
        }

        public static void RequestAutoFix()
        {
            EditorFolderUtility.EnsureFolder("Assets/_Game/EditorTemp");
            System.IO.File.WriteAllText(PendingFlagPath, "1");
            AssetDatabase.Refresh();
        }
    }

    internal sealed class ProfileNavAndFontFixAutoRun : AssetPostprocessor
    {
        private const string PendingFlagPath = "Assets/_Game/EditorTemp/profile_nav_fix.pending";

        [InitializeOnLoadMethod]
        private static void OnDomainReload()
        {
            EditorApplication.delayCall += TryRun;
        }

        private static void OnPostprocessAllAssets(
            string[] importedAssets,
            string[] deletedAssets,
            string[] movedAssets,
            string[] movedFromAssetPaths)
        {
            if (importedAssets == null)
            {
                return;
            }

            for (int i = 0; i < importedAssets.Length; i++)
            {
                if (string.Equals(importedAssets[i], PendingFlagPath, System.StringComparison.OrdinalIgnoreCase))
                {
                    EditorApplication.delayCall += TryRun;
                    return;
                }
            }
        }

        private static void TryRun()
        {
            if (!System.IO.File.Exists(PendingFlagPath))
            {
                return;
            }

            try
            {
                System.IO.File.Delete(PendingFlagPath);
                string meta = PendingFlagPath + ".meta";
                if (System.IO.File.Exists(meta))
                {
                    System.IO.File.Delete(meta);
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning("[ProfileNavAndFontFixAutoRun] " + ex.Message);
            }

            try
            {
                ProfileNavAndFontFixer.FixMainMenuScene();
            }
            catch (System.Exception ex)
            {
                Debug.LogError("[ProfileNavAndFontFixAutoRun] " + ex);
            }
        }
    }

}
