using System.Collections.Generic;
using AlienDefense.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace AlienDefense.EditorTools
{
    /// <summary>Rebuilds the pause screen from the PausePanel sprites: the three round audio/haptics toggles, the
    /// panel art, the dynamic content that sits inside it (subtitle, damage leaders, ability icons), the X, and the
    /// Leave / Continue buttons - then wires it all to PausePanelView and GameStateUIController.
    ///
    /// pausepanel.png already carries the frame, the "Pause" title, the section headers and the empty slots, so this
    /// builder never draws those; it only positions the live content into the holes in that artwork. The layout
    /// constants below are fractions of the sprite, measured off it once, which keeps them valid if the panel is
    /// resized.
    ///
    /// The panel's children are rebuilt from scratch on every run, so re-running after a tweak is the intended
    /// workflow.</summary>
    internal static class PausePanelBuilder
    {
        private const string SpriteDir = "Assets/_Game/Art/Sprite/Play/PausePanel";

        // Canvas reference resolution is 1080x1920; pausepanel.png is 1137x1383 (0.822).
        private const float PanelWidth = 880f;
        private const float PanelHeight = 1070f;
        private const float PanelCenterY = 120f;
        private const float ToggleSize = 130f;

        // Fractions of the panel sprite (0 = top/left, 1 = bottom/right).
        private const float SubtitleY = 0.202f;
        private const float BoxTop = 0.351f;
        private const float BoxBottom = 0.621f;
        private const float BoxLeft = 0.100f;
        private const float BoxRight = 0.900f;
        private const float SlotY = 0.789f;
        private const float SlotFirstX = 0.154f;
        private const float SlotStepX = 0.114f;
        private const float SlotSizeX = 0.107f;

        private const int DamageRowCount = 4;
        private const int AbilitySlotCount = 5;

        private static readonly Color Dim = new Color(0f, 0f, 0f, 0.72f);
        private static readonly Color BarColor = new Color(0.36f, 0.86f, 1f, 1f);
        private static readonly Color BarTrack = new Color(0f, 0f, 0f, 0.28f);
        private static readonly Color SubtitleColor = new Color(0.82f, 0.95f, 1f, 1f);

        [MenuItem("AlienDefense/Setup/HUD/Rebuild Pause Panel (Open Scene)")]
        private static void Rebuild()
        {
            Scene scene = EditorSceneManager.GetActiveScene();
            var controller = Object.FindFirstObjectByType<GameStateUIController>(FindObjectsInactive.Include);
            if (controller == null)
            {
                Debug.LogError("[PausePanelBuilder] No GameStateUIController in " + scene.path);
                return;
            }

            var controllerSo = new SerializedObject(controller);
            SerializedProperty panelProperty = controllerSo.FindProperty("_pausePanel");
            var existing = panelProperty.objectReferenceValue as GameObject;

            Transform parent = existing != null ? existing.transform.parent : FindPanelParent(controller);
            if (parent == null)
            {
                Debug.LogError("[PausePanelBuilder] Could not find a Canvas/SafeArea to build under.");
                return;
            }

            GameObject panelRoot = existing != null ? existing : new GameObject("PausePanel", typeof(RectTransform));
            if (existing == null)
            {
                panelRoot.transform.SetParent(parent, false);
            }

            for (int i = panelRoot.transform.childCount - 1; i >= 0; i--)
            {
                Object.DestroyImmediate(panelRoot.transform.GetChild(i).gameObject);
            }

            PausePanelView view = Build(panelRoot);
            panelRoot.transform.SetAsLastSibling(); // above the HUD and the minimap

            panelProperty.objectReferenceValue = panelRoot;
            controllerSo.FindProperty("_pausePanelView").objectReferenceValue = view;
            AssignHiddenGroups(controllerSo, panelRoot);
            controllerSo.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(controller);

            panelRoot.SetActive(false); // GameStateUIController shows it when the game pauses
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[PausePanelBuilder] Rebuilt PausePanel in " + scene.path);
        }

        private static PausePanelView Build(GameObject panelRoot)
        {
            Stretch(panelRoot.GetComponent<RectTransform>() ?? panelRoot.AddComponent<RectTransform>());
            Image dim = panelRoot.GetComponent<Image>() ?? panelRoot.AddComponent<Image>();
            dim.sprite = null;
            dim.color = Dim;
            dim.raycastTarget = true; // swallows taps meant for the gameplay behind the pause screen

            PausePanelView view = panelRoot.GetComponent<PausePanelView>() ?? panelRoot.AddComponent<PausePanelView>();
            var so = new SerializedObject(view);

            BuildToggles(panelRoot.transform, so);

            GameObject panel = Sprite(panelRoot.transform, "Panel", Load("pausepanel"), new Vector2(0.5f, 0.5f),
                new Vector2(0f, PanelCenterY), new Vector2(PanelWidth, PanelHeight));
            Transform panelTransform = panel.transform;

            // The empty pill under the baked "Pause" title.
            TMP_Text subtitle = Text(panelTransform, "SubtitleText", "Perfect Clear", new Vector2(0.5f, 1f),
                new Vector2(0f, -SubtitleY * PanelHeight - 16f), new Vector2(420f, 44f), 30f,
                TextAlignmentOptions.Center, SubtitleColor, FontStyles.Bold);

            // The baked statistics box.
            float boxHeight = (BoxBottom - BoxTop) * PanelHeight;
            float boxWidth = (BoxRight - BoxLeft) * PanelWidth;
            var box = new GameObject("StatsArea", typeof(RectTransform));
            box.transform.SetParent(panelTransform, false);
            Place(box.GetComponent<RectTransform>(), new Vector2(0.5f, 1f),
                new Vector2(0f, -BoxTop * PanelHeight), new Vector2(boxWidth, boxHeight));

            BuildDamageRows(box.transform, so, boxWidth);
            TMP_Text empty = Text(box.transform, "NoStatisticsLabel", "No statistics yet", new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(boxWidth - 40f, 60f), 38f, TextAlignmentOptions.Center, Color.white, FontStyles.Bold);
            so.FindProperty("_noStatisticsLabel").objectReferenceValue = empty.gameObject;

            BuildAbilitySlots(panelTransform, so);

            Button close = SpriteButton(panelTransform, "CloseButton", Load("closebtn"), new Vector2(1f, 1f),
                new Vector2(30f, 26f), new Vector2(150f, 150f));
            Button leave = SpriteButton(panelRoot.transform, "LeaveButton", Load("leavebtn"), new Vector2(0.5f, 0f),
                new Vector2(-232f, 210f), new Vector2(430f, 211f));
            Button continueButton = SpriteButton(panelRoot.transform, "ContinueButton", Load("continuebtn"),
                new Vector2(0.5f, 0f), new Vector2(232f, 210f), new Vector2(430f, 215f));

            so.FindProperty("_titleText").objectReferenceValue = null; // the title is part of the artwork
            so.FindProperty("_subtitleText").objectReferenceValue = subtitle;
            so.FindProperty("_closeButton").objectReferenceValue = close;
            so.FindProperty("_leaveButton").objectReferenceValue = leave;
            so.FindProperty("_continueButton").objectReferenceValue = continueButton;
            so.FindProperty("_restartButton").objectReferenceValue = null;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(view);
            return view;
        }

        /// <summary>The three round buttons in the screen's top-left corner: sound, music, vibration.</summary>
        private static void BuildToggles(Transform parent, SerializedObject so)
        {
            var row = new GameObject("SettingToggles", typeof(RectTransform));
            row.transform.SetParent(parent, false);
            RectTransform rowRect = row.GetComponent<RectTransform>();
            rowRect.anchorMin = new Vector2(0f, 1f);
            rowRect.anchorMax = new Vector2(0f, 1f);
            rowRect.pivot = new Vector2(0f, 1f);
            rowRect.anchoredPosition = new Vector2(40f, -40f);
            rowRect.sizeDelta = new Vector2(ToggleSize * 3f + 40f, ToggleSize);

            BuildToggle(row.transform, so, "_sfxToggle", "SfxToggle", "sfxicon", 0f);
            BuildToggle(row.transform, so, "_musicToggle", "MusicToggle", "musicbgicon", ToggleSize + 20f);
            BuildToggle(row.transform, so, "_vibrationToggle", "VibrationToggle", "vibrationicon", (ToggleSize + 20f) * 2f);
        }

        private static void BuildToggle(Transform parent, SerializedObject so, string property, string name, string sprite, float x)
        {
            Button button = SpriteButton(parent, name, Load(sprite), new Vector2(0f, 1f),
                new Vector2(x, 0f), new Vector2(ToggleSize, ToggleSize));
            SerializedProperty toggle = so.FindProperty(property);
            toggle.FindPropertyRelative("Button").objectReferenceValue = button;
            toggle.FindPropertyRelative("Icon").objectReferenceValue = button.GetComponent<Image>();
            toggle.FindPropertyRelative("OffOverlay").objectReferenceValue = null;
        }

        private static void BuildDamageRows(Transform parent, SerializedObject so, float boxWidth)
        {
            SerializedProperty rows = so.FindProperty("_damageRows");
            rows.arraySize = DamageRowCount;
            const float rowHeight = 56f;
            const float spacing = 8f;
            float rowWidth = boxWidth - 56f;

            for (int i = 0; i < DamageRowCount; i++)
            {
                var rowObject = new GameObject("DamageRow_" + i, typeof(RectTransform));
                rowObject.transform.SetParent(parent, false);
                Place(rowObject.GetComponent<RectTransform>(), new Vector2(0.5f, 1f),
                    new Vector2(0f, -18f - i * (rowHeight + spacing)), new Vector2(rowWidth, rowHeight));

                GameObject icon = Solid(rowObject.transform, "Icon", Color.white, new Vector2(0f, 0.5f),
                    new Vector2(4f, 0f), new Vector2(44f, 44f));
                TMP_Text nameText = Text(rowObject.transform, "NameText", "-", new Vector2(0f, 1f),
                    new Vector2(58f, 0f), new Vector2(rowWidth - 240f, 32f), 26f, TextAlignmentOptions.Left,
                    Color.white, FontStyles.Bold);
                TMP_Text valueText = Text(rowObject.transform, "ValueText", "0", new Vector2(1f, 1f),
                    new Vector2(-4f, 0f), new Vector2(170f, 32f), 26f, TextAlignmentOptions.Right,
                    Color.white, FontStyles.Bold);

                float barWidth = rowWidth - 62f;
                Solid(rowObject.transform, "BarTrack", BarTrack, new Vector2(0f, 0f),
                    new Vector2(58f, 6f), new Vector2(barWidth, 12f));
                GameObject bar = Solid(rowObject.transform, "Bar", BarColor, new Vector2(0f, 0f),
                    new Vector2(58f, 6f), new Vector2(barWidth, 12f));
                var barImage = bar.GetComponent<Image>();
                barImage.type = Image.Type.Filled;
                barImage.fillMethod = Image.FillMethod.Horizontal;
                barImage.fillOrigin = (int)Image.OriginHorizontal.Left;
                barImage.fillAmount = 0f;

                SerializedProperty row = rows.GetArrayElementAtIndex(i);
                row.FindPropertyRelative("Root").objectReferenceValue = rowObject;
                row.FindPropertyRelative("Icon").objectReferenceValue = icon.GetComponent<Image>();
                row.FindPropertyRelative("NameText").objectReferenceValue = nameText;
                row.FindPropertyRelative("ValueText").objectReferenceValue = valueText;
                row.FindPropertyRelative("Bar").objectReferenceValue = barImage;
                rowObject.SetActive(false);
            }
        }

        /// <summary>Icons dropped into the five empty slots the artwork already draws (the sixth frame on that row is
        /// the grid button in the sprite, not a slot).</summary>
        private static void BuildAbilitySlots(Transform parent, SerializedObject so)
        {
            SerializedProperty slots = so.FindProperty("_abilitySlots");
            slots.arraySize = AbilitySlotCount;
            float size = SlotSizeX * PanelWidth;

            for (int i = 0; i < AbilitySlotCount; i++)
            {
                float centerX = (SlotFirstX + i * SlotStepX) * PanelWidth - PanelWidth * 0.5f;
                var slot = new GameObject("AbilitySlot_" + i, typeof(RectTransform));
                slot.transform.SetParent(parent, false);
                Place(slot.GetComponent<RectTransform>(), new Vector2(0.5f, 1f),
                    new Vector2(centerX, -SlotY * PanelHeight + size * 0.5f), new Vector2(size, size));

                GameObject icon = Solid(slot.transform, "Icon", Color.white, new Vector2(0.5f, 0.5f),
                    Vector2.zero, new Vector2(size - 16f, size - 16f));
                var iconImage = icon.GetComponent<Image>();
                iconImage.sprite = null;
                iconImage.type = Image.Type.Simple;
                iconImage.enabled = false; // nothing picked yet

                TMP_Text rank = Text(slot.transform, "RankText", string.Empty, new Vector2(1f, 0f),
                    new Vector2(-4f, 2f), new Vector2(56f, 28f), 22f, TextAlignmentOptions.BottomRight,
                    Color.white, FontStyles.Bold);

                SerializedProperty entry = slots.GetArrayElementAtIndex(i);
                entry.FindPropertyRelative("Root").objectReferenceValue = slot;
                entry.FindPropertyRelative("Icon").objectReferenceValue = iconImage;
                entry.FindPropertyRelative("RankText").objectReferenceValue = rank;
            }
        }

        /// <summary>HUD groups that step aside while the pause screen is open (the mockup shows the toggles in the
        /// corner the HUD normally occupies).</summary>
        private static void AssignHiddenGroups(SerializedObject controllerSo, GameObject panelRoot)
        {
            string[] names = { "TopHUD", "MinimapRoot", "BottomControls", "MissileCooldown", "BossHealthBar" };
            Transform parent = panelRoot.transform.parent;
            var groups = new List<CanvasGroup>();
            foreach (string name in names)
            {
                Transform target = parent != null ? parent.Find(name) : null;
                if (target == null)
                {
                    continue;
                }

                var group = target.GetComponent<CanvasGroup>();
                if (group == null)
                {
                    group = target.gameObject.AddComponent<CanvasGroup>();
                }

                groups.Add(group);
                EditorUtility.SetDirty(target.gameObject);
            }

            SerializedProperty property = controllerSo.FindProperty("_hiddenWhilePaused");
            property.arraySize = groups.Count;
            for (int i = 0; i < groups.Count; i++)
            {
                property.GetArrayElementAtIndex(i).objectReferenceValue = groups[i];
            }
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

        private static Sprite Load(string fileName)
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{SpriteDir}/{fileName}.png");
            if (sprite == null)
            {
                Debug.LogWarning("[PausePanelBuilder] Missing sprite " + fileName);
            }

            return sprite;
        }

        private static Sprite BuiltInSprite()
        {
            return AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        }

        private static GameObject Sprite(Transform parent, string name, Sprite sprite, Vector2 anchor,
            Vector2 anchoredPosition, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            Place(go.GetComponent<RectTransform>(), anchor, anchoredPosition, size);
            var image = go.GetComponent<Image>();
            image.sprite = sprite;
            image.raycastTarget = false;
            return go;
        }

        private static GameObject Solid(Transform parent, string name, Color color, Vector2 anchor,
            Vector2 anchoredPosition, Vector2 size)
        {
            GameObject go = Sprite(parent, name, BuiltInSprite(), anchor, anchoredPosition, size);
            var image = go.GetComponent<Image>();
            image.color = color;
            image.type = Image.Type.Sliced;
            return go;
        }

        private static Button SpriteButton(Transform parent, string name, Sprite sprite, Vector2 anchor,
            Vector2 anchoredPosition, Vector2 size)
        {
            GameObject go = Sprite(parent, name, sprite, anchor, anchoredPosition, size);
            var image = go.GetComponent<Image>();
            image.raycastTarget = true;
            var button = go.AddComponent<Button>();
            button.targetGraphic = image;
            button.transition = Selectable.Transition.ColorTint;
            ColorBlock colors = button.colors;
            colors.pressedColor = new Color(0.82f, 0.82f, 0.82f, 1f);
            colors.fadeDuration = 0.05f;
            button.colors = colors;
            return button;
        }

        private static TMP_Text Text(Transform parent, string name, string content, Vector2 anchor,
            Vector2 anchoredPosition, Vector2 size, float fontSize, TextAlignmentOptions alignment, Color color,
            FontStyles style = FontStyles.Normal)
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
    }
}
