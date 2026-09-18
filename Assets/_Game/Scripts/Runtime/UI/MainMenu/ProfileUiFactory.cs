using AlienDefense.UI.Profile;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AlienDefense.UI.MainMenu
{
    /// <summary>Builds Profile overlay + UFO preview at runtime when the MainMenu scene has not been
    /// scaffolded yet (editor inject optional). Idempotent: returns existing ProfilePresenter if present.</summary>
    public static class ProfileUiFactory
    {
        private static readonly Color OverlayNavy = new Color(0.02f, 0.05f, 0.12f, 0.82f);
        private static readonly Color AccentCyan = new Color(0.3f, 0.8f, 0.95f, 1f);
        private static readonly Color AccentGreen = new Color(0.25f, 0.9f, 0.35f, 1f);
        private static readonly Color PanelColor = new Color(0.08f, 0.12f, 0.22f, 0.92f);
        private static readonly Color CardColor = new Color(0.55f, 0.78f, 0.95f, 0.96f);

        public static ProfilePresenter Ensure(MonoBehaviour host)
        {
            if (host == null)
            {
                return null;
            }

            ProfilePresenter existing = host.GetComponent<ProfilePresenter>();
            if (existing != null)
            {
                return existing;
            }

            Canvas canvas = Object.FindFirstObjectByType<Canvas>();
            if (canvas == null)
            {
                Debug.LogError("[ProfileUiFactory] No Canvas found.");
                return null;
            }

            Transform safeArea = canvas.transform.Find("SafeArea") ?? canvas.transform;
            GameObject menuContent = GameObject.Find("MenuContentRoot");
            GameObject topHud = GameObject.Find("TopHUD");
            GameObject bottomNav = GameObject.Find("BottomNavigationSlot");

            ProfileView view = BuildOverlay(safeArea);
            ProfileUfoPreviewController ufo = BuildUfoPreview(view.UfoPreviewImage);

            ProfilePresenter presenter = host.gameObject.AddComponent<ProfilePresenter>();
            presenter.Configure(view, ufo, menuContent, topHud, null, null, null, bottomNav);
            return presenter;
        }

        private static ProfileView BuildOverlay(Transform safeArea)
        {
            var root = new GameObject("ProfileRoot", typeof(RectTransform), typeof(CanvasGroup));
            root.transform.SetParent(safeArea, false);
            root.transform.SetAsLastSibling();
            Stretch(root.GetComponent<RectTransform>());
            CanvasGroup canvasGroup = root.GetComponent<CanvasGroup>();
            // Match editor inject: alpha 1 in hierarchy for inspection; Awake forces 0 at Play.
            canvasGroup.alpha = 1f;
            root.SetActive(false);

            Image bg = CreateFullImage(root.transform, "Background", Color.white, true);
            Sprite bgSprite = FindMainMenuBackgroundSprite();
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
            Stretch(content.GetComponent<RectTransform>());

            RectTransform header = CreateHeader(content.transform, out Button backButton);
            RectTransform showcase = CreateShowcase(content.transform, out RawImage ufoImage);
            RectTransform card = CreateCard(content.transform,
                out Image avatarImage,
                out Button editAvatarButton,
                out TMP_Text nameText,
                out Button editNameButton,
                out TMP_Text idText,
                out Button copyIdButton,
                out TMP_Text levelText,
                out TMP_Text energyText,
                out TMP_Text energyTimerText,
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

            (CanvasGroup toastGroup, TMP_Text toastText) = CreateToast(root.transform);
            EditNamePopupView editPopup = CreateEditNamePopup(root.transform);

            ProfileView view = root.AddComponent<ProfileView>();
            view.Wire(
                root,
                canvasGroup,
                content.GetComponent<RectTransform>(),
                backButton,
                editNameButton,
                editAvatarButton,
                copyIdButton,
                avatarImage,
                nameText,
                idText,
                levelText,
                energyText,
                energyTimerText,
                powerRow,
                towersRow,
                campaignRow,
                damageRow,
                killsRow,
                toastGroup,
                toastText,
                editPopup,
                ufoImage,
                header,
                showcase,
                card);
            return view;
        }

        private static RectTransform CreateHeader(Transform parent, out Button backButton)
        {
            var header = new GameObject("Header", typeof(RectTransform));
            header.transform.SetParent(parent, false);
            RectTransform rect = header.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.sizeDelta = new Vector2(0f, 96f);

            backButton = CreateButton(header.transform, "BackButton", "<", new Vector2(0f, 1f), new Vector2(16f, -16f),
                new Vector2(64f, 64f), AccentCyan);
            CreateText(header.transform, "Title", "PROFILE", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(400f, 64f),
                40f, TextAlignmentOptions.Center, AccentCyan, FontStyles.Bold);
            return rect;
        }

        private static RectTransform CreateShowcase(Transform parent, out RawImage ufoImage)
        {
            var showcase = new GameObject("Showcase", typeof(RectTransform));
            showcase.transform.SetParent(parent, false);
            RectTransform rect = showcase.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, -64f);
            rect.sizeDelta = new Vector2(300f, 200f);

            var rawGo = new GameObject("UfoPreview", typeof(RectTransform), typeof(RawImage));
            rawGo.transform.SetParent(showcase.transform, false);
            RectTransform rawRect = rawGo.GetComponent<RectTransform>();
            rawRect.anchorMin = rawRect.anchorMax = rawRect.pivot = new Vector2(0.5f, 0.5f);
            rawRect.sizeDelta = new Vector2(200f, 220f);
            ufoImage = rawGo.GetComponent<RawImage>();
            ufoImage.raycastTarget = false;
            return rect;
        }

        private static RectTransform CreateCard(
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
            out ProfileStatRowView powerRow,
            out ProfileStatRowView towersRow,
            out ProfileStatRowView campaignRow,
            out ProfileStatRowView damageRow,
            out ProfileStatRowView killsRow)
        {
            var card = new GameObject("ProfileCard", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup));
            card.transform.SetParent(parent, false);
            RectTransform rect = card.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = new Vector2(0f, 420f);
            rect.sizeDelta = new Vector2(680f, 780f);
            card.GetComponent<Image>().color = CardColor;
            VerticalLayoutGroup layout = card.GetComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(28, 28, 24, 24);
            layout.spacing = 10f;
            layout.childControlWidth = true;
            layout.childForceExpandWidth = true;

            var identity = new GameObject("Identity", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
            identity.transform.SetParent(card.transform, false);
            identity.GetComponent<LayoutElement>().preferredHeight = 100f;
            identity.GetComponent<HorizontalLayoutGroup>().spacing = 16f;

            var avatarGo = new GameObject("Avatar", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
            avatarGo.transform.SetParent(identity.transform, false);
            avatarGo.GetComponent<LayoutElement>().preferredWidth = 96f;
            avatarGo.GetComponent<LayoutElement>().preferredHeight = 96f;
            avatarImage = avatarGo.GetComponent<Image>();
            avatarImage.color = new Color(0.3f, 0.5f, 0.6f, 1f);
            editAvatarButton = avatarGo.GetComponent<Button>();

            var nameCol = new GameObject("NameCol", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(LayoutElement));
            nameCol.transform.SetParent(identity.transform, false);
            nameCol.GetComponent<LayoutElement>().flexibleWidth = 1f;

            var nameRow = new GameObject("NameRow", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            nameRow.transform.SetParent(nameCol.transform, false);
            nameText = CreateLayoutText(nameRow.transform, "Name", "Pilot", 32f, FontStyles.Bold);
            editNameButton = CreateLayoutButton(nameRow.transform, "EditName", "✎", AccentGreen);

            var idRow = new GameObject("IdRow", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            idRow.transform.SetParent(nameCol.transform, false);
            idText = CreateLayoutText(idRow.transform, "Id", "ID: —", 34f, FontStyles.Normal);
            copyIdButton = CreateLayoutButton(idRow.transform, "CopyId", "⧉", AccentCyan);

            levelText = CreateLayoutText(card.transform, "Level", "Level 1", 28f, FontStyles.Bold);
            levelText.color = AccentGreen;

            energyText = CreateLayoutText(card.transform, "Energy", "Energy  —", 24f, FontStyles.Normal);
            energyTimerText = CreateLayoutText(card.transform, "EnergyTimer", string.Empty, 20f, FontStyles.Normal);
            energyTimerText.gameObject.SetActive(false);

            powerRow = CreateStatRow(card.transform, "Power", "0");
            towersRow = CreateStatRow(card.transform, "Towers Unlocked", "0");
            campaignRow = CreateStatRow(card.transform, "Campaign", "—");
            damageRow = CreateStatRow(card.transform, "Tower Damage", "0");
            killsRow = CreateStatRow(card.transform, "Zombies Killed", "0");
            return rect;
        }

        private static ProfileStatRowView CreateStatRow(Transform parent, string label, string value)
        {
            var row = new GameObject(label + "Row", typeof(RectTransform), typeof(Image), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
            row.transform.SetParent(parent, false);
            row.GetComponent<LayoutElement>().preferredHeight = 56f;
            row.GetComponent<Image>().color = new Color(0.05f, 0.08f, 0.14f, 0.65f);
            HorizontalLayoutGroup layout = row.GetComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(12, 12, 6, 6);
            layout.childForceExpandWidth = true;

            TMP_Text labelText = CreateLayoutText(row.transform, "Label", label, 42f, FontStyles.Normal);
            TMP_Text valueText = CreateLayoutText(row.transform, "Value", value, 44f, FontStyles.Bold);
            valueText.color = new Color(0x15 / 255f, 0x81 / 255f, 0x45 / 255f, 1f);
            valueText.alignment = TextAlignmentOptions.MidlineRight;

            ProfileStatRowView view = row.AddComponent<ProfileStatRowView>();
            view.Wire(null, labelText, valueText);
            return view;
        }

        private static (CanvasGroup, TMP_Text) CreateToast(Transform parent)
        {
            var toast = new GameObject("Toast", typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
            toast.transform.SetParent(parent, false);
            RectTransform rect = toast.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = new Vector2(0f, 180f);
            rect.sizeDelta = new Vector2(280f, 56f);
            toast.GetComponent<Image>().color = new Color(0.05f, 0.1f, 0.18f, 0.92f);
            CanvasGroup group = toast.GetComponent<CanvasGroup>();
            group.alpha = 0f;
            toast.SetActive(false);
            TMP_Text text = CreateText(toast.transform, "Text", "ID copied", new Vector2(0.5f, 0.5f), Vector2.zero,
                new Vector2(260f, 48f), 24f, TextAlignmentOptions.Center, Color.white, FontStyles.Normal);
            return (group, text);
        }

        private static EditNamePopupView CreateEditNamePopup(Transform parent)
        {
            var popup = new GameObject("EditNamePopup", typeof(RectTransform));
            popup.transform.SetParent(parent, false);
            Stretch(popup.GetComponent<RectTransform>());
            CreateFullImage(popup.transform, "Dim", new Color(0f, 0f, 0f, 0.65f), true);

            var panel = new GameObject("Panel", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup));
            panel.transform.SetParent(popup.transform, false);
            RectTransform panelRect = panel.GetComponent<RectTransform>();
            panelRect.anchorMin = panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.sizeDelta = new Vector2(560f, 280f);
            panel.GetComponent<Image>().color = PanelColor;
            VerticalLayoutGroup layout = panel.GetComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(24, 24, 24, 24);
            layout.spacing = 14f;
            layout.childControlWidth = true;
            layout.childForceExpandWidth = true;

            CreateLayoutText(panel.transform, "Title", "Edit Name", 30f, FontStyles.Bold);

            var inputGo = new GameObject("Input", typeof(RectTransform), typeof(Image), typeof(TMP_InputField), typeof(LayoutElement));
            inputGo.transform.SetParent(panel.transform, false);
            inputGo.GetComponent<LayoutElement>().preferredHeight = 56f;
            inputGo.GetComponent<Image>().color = new Color(0.05f, 0.08f, 0.12f, 1f);
            TMP_InputField input = inputGo.GetComponent<TMP_InputField>();

            var textArea = new GameObject("Text Area", typeof(RectTransform), typeof(RectMask2D));
            textArea.transform.SetParent(inputGo.transform, false);
            Stretch(textArea.GetComponent<RectTransform>(), 8f);
            TMP_Text placeholder = CreateText(textArea.transform, "Placeholder", "Name...", new Vector2(0f, 0.5f),
                Vector2.zero, new Vector2(500f, 48f), 26f, TextAlignmentOptions.MidlineLeft, new Color(1f, 1f, 1f, 0.35f), FontStyles.Normal);
            Stretch(placeholder.rectTransform);
            TMP_Text inputText = CreateText(textArea.transform, "Text", string.Empty, new Vector2(0f, 0.5f),
                Vector2.zero, new Vector2(500f, 48f), 26f, TextAlignmentOptions.MidlineLeft, Color.white, FontStyles.Normal);
            Stretch(inputText.rectTransform);
            input.textViewport = textArea.GetComponent<RectTransform>();
            input.textComponent = inputText;
            input.placeholder = placeholder;
            input.characterLimit = 20;

            TMP_Text error = CreateLayoutText(panel.transform, "Error", string.Empty, 20f, FontStyles.Normal);
            error.color = new Color(1f, 0.4f, 0.35f, 1f);
            error.gameObject.SetActive(false);

            var buttons = new GameObject("Buttons", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
            buttons.transform.SetParent(panel.transform, false);
            buttons.GetComponent<LayoutElement>().preferredHeight = 56f;
            buttons.GetComponent<HorizontalLayoutGroup>().spacing = 16f;
            Button cancel = CreateLayoutButton(buttons.transform, "Cancel", "Cancel", new Color(0.25f, 0.3f, 0.4f, 1f));
            Button save = CreateLayoutButton(buttons.transform, "Save", "Save", AccentGreen);

            EditNamePopupView view = popup.AddComponent<EditNamePopupView>();
            view.Wire(popup, input, cancel, save, error);
            popup.SetActive(false);
            return view;
        }

        private static ProfileUfoPreviewController BuildUfoPreview(RawImage target)
        {
            int previewLayer = LayerMask.NameToLayer(ProfileUfoPreviewController.PreviewLayerName);
            var world = new GameObject("ProfileUfoPreviewWorld");
            world.transform.position = new Vector3(1000f, 1000f, 1000f);

            var camGo = new GameObject("ProfileUfoPreviewCamera", typeof(Camera));
            camGo.transform.SetParent(world.transform, false);
            camGo.transform.localPosition = new Vector3(0f, 1.2f, -4.5f);
            camGo.transform.localRotation = Quaternion.Euler(15f, 0f, 0f);
            Camera cam = camGo.GetComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0f, 0f, 0f, 0f);
            cam.fieldOfView = 30f;
            cam.enabled = false;
            cam.depth = -20;
            cam.allowHDR = false;
            if (previewLayer >= 0)
            {
                cam.cullingMask = 1 << previewLayer;
            }

            var ufoRoot = new GameObject("UfoRoot").transform;
            ufoRoot.SetParent(world.transform, false);
            var modelPivot = new GameObject("ModelPivot").transform;
            modelPivot.SetParent(ufoRoot, false);

            GameObject visual = TryLoadUfoPrefab();
            if (visual != null)
            {
                GameObject instance = Object.Instantiate(visual, modelPivot);
                instance.name = "UFO_ProfilePreview";
                instance.transform.localPosition = Vector3.zero;
                instance.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
                instance.transform.localScale = Vector3.one * 0.85f;
                ProfileUfoPreviewController.StripGameplay(instance);
            }
            else
            {
                GameObject capsule = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                capsule.name = "UfoPlaceholder";
                capsule.transform.SetParent(modelPivot, false);
                Object.Destroy(capsule.GetComponent<Collider>());
            }

            if (previewLayer >= 0)
            {
                ProfileUfoPreviewController.ApplyLayerRecursive(world.transform, previewLayer);
            }

            if (target != null)
            {
                target.raycastTarget = false;
                target.color = Color.white;
            }

            ProfileUfoPreviewController controller = world.AddComponent<ProfileUfoPreviewController>();
            controller.Configure(cam, ufoRoot, target);
            return controller;
        }

        private static GameObject TryLoadUfoPrefab()
        {
#if UNITY_EDITOR
            GameObject preview = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/_Game/Prefabs/UI/Profile/UFO_ProfilePreview.prefab");
            if (preview != null)
            {
                return preview;
            }

            return UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/_Game/Prefabs/Player/UFO_Player.prefab");
#else
            return null;
#endif
        }

        private static Button CreateButton(Transform parent, string name, string label, Vector2 anchor, Vector2 pos, Vector2 size, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = rect.pivot = anchor;
            rect.anchoredPosition = pos;
            rect.sizeDelta = size;
            go.GetComponent<Image>().color = color;
            CreateText(go.transform, "Label", label, new Vector2(0.5f, 0.5f), Vector2.zero, size, 28f,
                TextAlignmentOptions.Center, Color.white, FontStyles.Bold);
            return go.GetComponent<Button>();
        }

        private static Button CreateLayoutButton(Transform parent, string name, string label, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            go.GetComponent<LayoutElement>().preferredWidth = 56f;
            go.GetComponent<LayoutElement>().preferredHeight = 48f;
            go.GetComponent<LayoutElement>().flexibleWidth = name == "Cancel" || name == "Save" ? 1f : 0f;
            go.GetComponent<Image>().color = color;
            CreateText(go.transform, "Label", label, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(120f, 40f), 22f,
                TextAlignmentOptions.Center, Color.white, FontStyles.Bold);
            return go.GetComponent<Button>();
        }

        private static TMP_Text CreateLayoutText(Transform parent, string name, string text, float size, FontStyles style)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            go.GetComponent<LayoutElement>().preferredHeight = 40f;
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = size;
            tmp.fontStyle = style;
            tmp.color = Color.white;
            tmp.alignment = TextAlignmentOptions.MidlineLeft;
            ApplyUiFont(tmp);
            return tmp;
        }

        private static TMP_Text CreateText(Transform parent, string name, string text, Vector2 anchor, Vector2 pos, Vector2 size,
            float fontSize, TextAlignmentOptions align, Color color, FontStyles style)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = rect.pivot = anchor;
            rect.anchoredPosition = pos;
            rect.sizeDelta = size;
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.alignment = align;
            tmp.color = color;
            tmp.fontStyle = style;
            ApplyUiFont(tmp);
            return tmp;
        }

        private static void ApplyUiFont(TMP_Text text)
        {
            if (text == null)
            {
                return;
            }

#if UNITY_EDITOR
            TMP_FontAsset font = UnityEditor.AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
                "Assets/_Game/Font/Fredoka-Bold SDF.asset");
            if (font != null)
            {
                text.font = font;
            }
#else
            if (TMP_Settings.defaultFontAsset != null)
            {
                text.font = TMP_Settings.defaultFontAsset;
            }
#endif
            text.isOrthographic = true;
        }

        private static Image CreateFullImage(Transform parent, string name, Color color, bool raycast)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            Stretch(go.GetComponent<RectTransform>());
            Image image = go.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = raycast;
            return image;
        }

        private static Sprite FindMainMenuBackgroundSprite()
        {
            GameObject overlay = GameObject.Find("BackgroundOverlay");
            if (overlay != null)
            {
                Image image = overlay.GetComponent<Image>();
                if (image != null && image.sprite != null)
                {
                    return image.sprite;
                }
            }

#if UNITY_EDITOR
            return UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(
                "Assets/_Game/Art/Sprite/MainMenu/BG/bg.png");
#else
            return null;
#endif
        }

        private static Sprite FindHudAvatarSprite()
        {
            PlayerProfileWidgetView widget = Object.FindFirstObjectByType<PlayerProfileWidgetView>();
            if (widget == null)
            {
                return null;
            }

            Image[] images = widget.GetComponentsInChildren<Image>(true);
            for (int i = 0; i < images.Length; i++)
            {
                Image image = images[i];
                if (image == null || image.sprite == null)
                {
                    continue;
                }

                string n = image.name;
                if (n.IndexOf("avatar", System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return image.sprite;
                }
            }

            for (int i = 0; i < images.Length; i++)
            {
                if (images[i] != null && images[i].sprite != null)
                {
                    return images[i].sprite;
                }
            }

            return null;
        }

        private static void Stretch(RectTransform rect, float inset = 0f)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(inset, inset);
            rect.offsetMax = new Vector2(-inset, -inset);
        }
    }
}
