using AlienDefense.Towers;
using AlienDefense.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace AlienDefense.EditorTools
{
    /// <summary>Rebuilds the "Choose Tower" panel into the bottom sheet the reference shows: a translucent navy panel
    /// over the bottom ~38% of the screen with a cyan divider, a header row ("Choose Tower:" + the Free button), and
    /// three portrait tower cards side by side, each drawn from one card sprite.
    ///
    /// It also assigns those card sprites to the TowerDefinitions (TowerDefinition.ChoiceCardSprite), which is what
    /// the panel reads at runtime - the old layout showed white boxes because every TowerDefinition.Icon was empty.
    ///
    /// Only the children of TowerChoiceRoot are rebuilt; the root itself, TowerChoiceView and TowerChoicePresenter
    /// (and every reference to them) are kept, and the card objects keep the names Card0/Card1/Card2 and the order
    /// the catalog uses. Re-runnable.</summary>
    internal static class TowerChoicePanelBuilder
    {
        private const string CardSpriteDir = "Assets/_Game/Art/Sprite/Play/TowerUpdate";
        private const string TowerDataDir = "Assets/_Game/Data/Towers";
        private const string FontAssetPath = "Assets/_Game/Font/Fredoka-Bold SDF.asset";
        private const string TitleMaterialPath = "Assets/_Game/Font/Materials/Fredoka_Title.mat";
        private const string ButtonMaterialPath = "Assets/_Game/Font/Materials/Fredoka_Button.mat";
        private const string GemIconPath = "Assets/_Game/Art/Sprite/MainMenu/Avatar/diamondicon.png";
        private const string RefreshButtonSpritePath = "Assets/_Game/Art/Sprite/Play/LevelUpdate/refreshbtn.png";

        // Canvas reference resolution is 1080x2280.
        private const float PanelHeightFraction = 0.38f;
        private const float HeaderHeight = 92f;
        private const float HeaderTopMargin = 16f;
        private const float SidePadding = 36f;
        private const float CardsSidePadding = 24f;
        private const float CardsBottomPadding = 28f;
        private const float CardSpacing = 18f;
        private const float TopBarHeight = 130f;

        private static readonly Color PanelColor = new Color32(0x13, 0x2C, 0x43, 0xF0);
        private static readonly Color DividerColor = new Color32(0x28, 0xD8, 0xF5, 0xFF);
        private static readonly Color DividerGlowColor = new Color32(0x28, 0xD8, 0xF5, 0x2E);
        private static readonly Color ScrimColor = new Color(0f, 0f, 0f, 0.45f);
        private static readonly Color RerollGreen = new Color32(0x36, 0xC7, 0x4C, 0xFF);
        private static readonly Color GemPillColor = new Color32(0x0A, 0x18, 0x26, 0xE6);

        [MenuItem("AlienDefense/Setup/HUD/Rebuild Tower Choice Panel (Open Scene)")]
        private static void Rebuild()
        {
            var view = Object.FindFirstObjectByType<TowerChoiceView>(FindObjectsInactive.Include);
            if (view == null)
            {
                Debug.LogWarning("[TowerChoicePanelBuilder] No TowerChoiceView in the open scene - open Level_01 first.");
                return;
            }

            AssignCardSpritesToTowers();

            var root = (RectTransform)view.transform;
            Stretch(root);
            var scrim = root.GetComponent<Image>();
            if (scrim != null)
            {
                // Dims the battlefield without hiding it; still eats taps meant for the world behind the sheet.
                scrim.color = ScrimColor;
                scrim.raycastTarget = true;
            }

            for (int i = root.childCount - 1; i >= 0; i--)
            {
                Undo.DestroyObjectImmediate(root.GetChild(i).gameObject);
            }

            RectTransform panel = BuildPanel(root, out CanvasGroup panelGroup);
            TMP_Text gemText = BuildTopGemBar(root);
            BuildDivider(panel);
            BuildHeader(panel, out TMP_Text title, out Button reroll);
            RectTransform cardsRow = BuildCardsRow(panel);

            var images = new Image[3];
            var buttons = new Button[3];
            for (int i = 0; i < 3; i++)
            {
                BuildCard(cardsRow, i, out buttons[i], out images[i]);
            }

            var so = new SerializedObject(view);
            so.FindProperty("_root").objectReferenceValue = view.gameObject;
            AssignArray(so, "_cardImages", images);
            AssignArray(so, "_cardButtons", buttons);
            ClearArray(so, "_nameTexts");       // the card sprite carries the name...
            ClearArray(so, "_descriptionTexts"); // ...and the description
            ClearArray(so, "_costTexts");
            ClearArray(so, "_iconImages");
            so.FindProperty("_rerollButton").objectReferenceValue = reroll;
            so.FindProperty("_panelRect").objectReferenceValue = panel;
            so.FindProperty("_panelGroup").objectReferenceValue = panelGroup;
            so.FindProperty("_gemText").objectReferenceValue = gemText;
            so.FindProperty("_showRerollButton").boolValue = true;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(view);

            root.SetAsLastSibling();
            view.gameObject.SetActive(false); // the presenter shows it when a build channel completes
            EditorSceneManager.MarkSceneDirty(view.gameObject.scene);
            Debug.Log($"[TowerChoicePanelBuilder] Rebuilt the Choose Tower panel ('{title.text}', 3 cards). Save the scene to keep it.");
        }

        /// <summary>The bottom sheet itself. Anchored to the bottom edge so it keeps its share of the screen on any
        /// portrait aspect, inside whatever SafeArea the canvas applies.</summary>
        private static RectTransform BuildPanel(RectTransform root, out CanvasGroup group)
        {
            GameObject go = NewUi("Background", root);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(1f, PanelHeightFraction);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var image = go.AddComponent<Image>();
            image.color = PanelColor;
            image.raycastTarget = true; // taps on the sheet's own background do nothing, rather than falling through

            group = go.AddComponent<CanvasGroup>();
            return rect;
        }

        /// <summary>The strip across the very top of the choice screen showing the player's saved Gems, like the
        /// reference. Part of this panel, so it only exists while a tower is being chosen and never fights the
        /// gameplay HUD for space.</summary>
        private static TMP_Text BuildTopGemBar(RectTransform root)
        {
            GameObject bar = NewUi("TopGemBar", root);
            var barRect = (RectTransform)bar.transform;
            barRect.anchorMin = new Vector2(0f, 1f);
            barRect.anchorMax = new Vector2(1f, 1f);
            barRect.pivot = new Vector2(0.5f, 1f);
            barRect.offsetMin = new Vector2(0f, -TopBarHeight);
            barRect.offsetMax = Vector2.zero;

            var barImage = bar.AddComponent<Image>();
            barImage.color = PanelColor;
            barImage.raycastTarget = true;

            GameObject pill = NewUi("GemPill", bar.transform);
            var pillRect = (RectTransform)pill.transform;
            pillRect.anchorMin = new Vector2(1f, 0.5f);
            pillRect.anchorMax = new Vector2(1f, 0.5f);
            pillRect.pivot = new Vector2(1f, 0.5f);
            pillRect.anchoredPosition = new Vector2(-SidePadding, 0f);
            pillRect.sizeDelta = new Vector2(250f, 74f);

            var pillImage = pill.AddComponent<Image>();
            pillImage.sprite = BuiltInSprite();
            pillImage.type = Image.Type.Sliced;
            pillImage.color = GemPillColor;
            pillImage.raycastTarget = false;

            GameObject icon = NewUi("GemIcon", pill.transform);
            var iconRect = (RectTransform)icon.transform;
            iconRect.anchorMin = new Vector2(0f, 0.5f);
            iconRect.anchorMax = new Vector2(0f, 0.5f);
            iconRect.pivot = new Vector2(0f, 0.5f);
            iconRect.anchoredPosition = new Vector2(-14f, 0f);
            iconRect.sizeDelta = new Vector2(84f, 84f);

            var iconImage = icon.AddComponent<Image>();
            iconImage.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(GemIconPath);
            iconImage.preserveAspect = true;
            iconImage.raycastTarget = false;

            TMP_Text amount = NewText(pill.transform, "GemAmountText", "0", 34f, TextAlignmentOptions.MidlineRight, ButtonMaterialPath);
            var amountRect = (RectTransform)amount.transform;
            amountRect.offsetMin = new Vector2(80f, 0f);
            amountRect.offsetMax = new Vector2(-22f, 0f);
            return amount;
        }

        private static void BuildDivider(RectTransform panel)
        {
            GameObject glow = NewUi("TopDividerGlow", panel);
            var glowRect = (RectTransform)glow.transform;
            StretchTop(glowRect, 22f, 0f);
            var glowImage = glow.AddComponent<Image>();
            glowImage.color = DividerGlowColor;
            glowImage.raycastTarget = false;

            GameObject go = NewUi("TopDivider", panel);
            var rect = (RectTransform)go.transform;
            StretchTop(rect, 4f, 0f);
            var image = go.AddComponent<Image>();
            image.color = DividerColor;
            image.raycastTarget = false;
        }

        private static void BuildHeader(RectTransform panel, out TMP_Text title, out Button reroll)
        {
            GameObject header = NewUi("HeaderRow", panel);
            var rect = (RectTransform)header.transform;
            StretchTop(rect, HeaderHeight, HeaderTopMargin);
            rect.offsetMin = new Vector2(SidePadding, rect.offsetMin.y);
            rect.offsetMax = new Vector2(-SidePadding, rect.offsetMax.y);

            title = NewText(header.transform, "Title", "Choose Tower:", 46f, TextAlignmentOptions.MidlineLeft, TitleMaterialPath);
            var titleRect = (RectTransform)title.transform;
            titleRect.anchorMin = new Vector2(0f, 0f);
            titleRect.anchorMax = new Vector2(0.62f, 1f);
            titleRect.offsetMin = Vector2.zero;
            titleRect.offsetMax = Vector2.zero;

            reroll = BuildRerollButton(rect);
        }

        /// <summary>The reference's green "Free" pill. Built so the layout matches, but left hidden by
        /// TowerChoiceView unless its Show Reroll Button is ticked - this project has nothing to reroll.</summary>
        private static Button BuildRerollButton(RectTransform header)
        {
            GameObject go = NewUi("RerollButton", header);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = new Vector2(1f, 0.5f);
            rect.anchorMax = new Vector2(1f, 0.5f);
            rect.pivot = new Vector2(1f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(230f, 74f);

            var refreshSprite = AssetDatabase.LoadAssetAtPath<Sprite>(RefreshButtonSpritePath);
            var background = go.AddComponent<Image>();
            background.raycastTarget = true;
            if (refreshSprite != null)
            {
                // The project already has the green "Refresh" pill drawn, label included.
                background.sprite = refreshSprite;
                background.preserveAspect = true;
                rect.sizeDelta = new Vector2(250f, 250f * refreshSprite.rect.height / refreshSprite.rect.width);
            }
            else
            {
                background.sprite = BuiltInSprite();
                background.type = Image.Type.Sliced;
                background.color = RerollGreen;
            }

            var button = go.AddComponent<Button>();
            button.targetGraphic = background;
            ColorBlock colors = button.colors;
            colors.pressedColor = new Color(0.82f, 0.82f, 0.82f, 1f);
            colors.fadeDuration = 0.05f;
            button.colors = colors;

            if (refreshSprite == null)
            {
                TMP_Text label = NewText(go.transform, "FreeText", "Free", 30f, TextAlignmentOptions.Center, ButtonMaterialPath);
                Stretch((RectTransform)label.transform);
            }

            return button;
        }

        private static RectTransform BuildCardsRow(RectTransform panel)
        {
            GameObject area = NewUi("CardsArea", panel);
            var areaRect = (RectTransform)area.transform;
            Stretch(areaRect);
            areaRect.offsetMin = new Vector2(CardsSidePadding, CardsBottomPadding);
            areaRect.offsetMax = new Vector2(-CardsSidePadding, -(HeaderTopMargin + HeaderHeight + 12f));

            GameObject row = NewUi("CardsRow", area.transform);
            var rowRect = (RectTransform)row.transform;
            Stretch(rowRect);

            var layout = row.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = CardSpacing;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = true;
            return rowRect;
        }

        /// <summary>One card: the button is the whole slot, and the artwork inside keeps the sprite's own portrait
        /// aspect (AspectRatioFitter) so it is never stretched, whatever width the row hands it.</summary>
        private static void BuildCard(RectTransform row, int index, out Button button, out Image art)
        {
            GameObject card = NewUi("Card" + index, row);
            var hit = card.AddComponent<Image>();
            hit.color = new Color(1f, 1f, 1f, 0f); // invisible, but it is what receives the tap
            hit.raycastTarget = true;

            button = card.AddComponent<Button>();
            button.targetGraphic = hit;
            button.transition = Selectable.Transition.None; // UiPressScale gives the feedback instead
            card.AddComponent<UiPressScale>();

            GameObject image = NewUi("CardImage", card.transform);
            var imageRect = (RectTransform)image.transform;
            Stretch(imageRect);

            art = image.AddComponent<Image>();
            art.preserveAspect = true;
            art.raycastTarget = false;

            var fitter = image.AddComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            fitter.aspectRatio = 1024f / 1536f; // the card sprites' own ratio; kept in sync below
        }

        /// <summary>Gives each TowerDefinition its card artwork, matched by the tower's own name.</summary>
        private static void AssignCardSpritesToTowers()
        {
            foreach (string guid in AssetDatabase.FindAssets("t:TowerDefinition", new[] { TowerDataDir }))
            {
                var tower = AssetDatabase.LoadAssetAtPath<TowerDefinition>(AssetDatabase.GUIDToAssetPath(guid));
                if (tower == null)
                {
                    continue;
                }

                string key = (tower.name + " " + tower.Id + " " + tower.DisplayName).ToLowerInvariant();
                string file = key.Contains("blaster") ? "blastertowerupdatecard"
                    : key.Contains("frost") ? "frosttowerupdatecard"
                    : key.Contains("mortar") ? "mortaltowerupdatecard"
                    : null;
                if (file == null)
                {
                    Debug.LogWarning($"[TowerChoicePanelBuilder] No card sprite matched '{tower.name}'.");
                    continue;
                }

                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{CardSpriteDir}/{file}.png");
                if (sprite == null)
                {
                    Debug.LogWarning($"[TowerChoicePanelBuilder] Missing sprite {CardSpriteDir}/{file}.png");
                    continue;
                }

                var so = new SerializedObject(tower);
                so.FindProperty("_choiceCardSprite").objectReferenceValue = sprite;
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(tower);
            }

            AssetDatabase.SaveAssets();
        }

        // ------------------------------------------------------------------------------------------------------

        private static void AssignArray(SerializedObject so, string field, Object[] values)
        {
            SerializedProperty array = so.FindProperty(field);
            array.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
            {
                array.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            }
        }

        private static void ClearArray(SerializedObject so, string field)
        {
            SerializedProperty array = so.FindProperty(field);
            array.arraySize = 3;
            for (int i = 0; i < 3; i++)
            {
                array.GetArrayElementAtIndex(i).objectReferenceValue = null;
            }
        }

        private static GameObject NewUi(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(go, "Tower choice panel");
            go.transform.SetParent(parent, false);
            return go;
        }

        /// <summary>The project's own Fredoka font with one of its shared outlined materials - assigning the shared
        /// material (instead of setting outlineWidth, which clones a material per label) keeps the look and costs
        /// nothing at runtime.</summary>
        private static TMP_Text NewText(Transform parent, string name, string content, float fontSize,
            TextAlignmentOptions alignment, string materialPath)
        {
            GameObject go = NewUi(name, parent);
            var text = go.AddComponent<TextMeshProUGUI>();
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);
            var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (font != null)
            {
                text.font = font;
            }

            if (material != null)
            {
                text.fontSharedMaterial = material;
            }

            text.text = content;
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = Color.white;
            text.raycastTarget = false;
            Stretch((RectTransform)go.transform);
            return text;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = Vector2.zero;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        /// <summary>Anchored across the top edge of its parent, a fixed height below it.</summary>
        private static void StretchTop(RectTransform rect, float height, float topMargin)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.offsetMin = new Vector2(0f, -(topMargin + height));
            rect.offsetMax = new Vector2(0f, -topMargin);
        }

        private static Sprite BuiltInSprite()
        {
            return AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        }
    }
}
