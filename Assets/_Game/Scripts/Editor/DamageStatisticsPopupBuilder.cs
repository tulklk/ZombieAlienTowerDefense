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
    /// <summary>Lays out the victory screen's Damage Statistics popup like the reference (user, 2026-10-11): on the
    /// damagestatistispanel sprite, a fire badge + "All Damage: 2.4K" in the top box, and in the big box one row per
    /// damage source - round icon, damage-type badge + name, five stars, a rounded dark bar with a yellow fill and the
    /// percent on it, the damage value on the right, a thin divider between rows. Everything is anchored to the
    /// sprite's own boxes (fractions of the panel), so it lines up whatever size the panel is. Rows keep the same
    /// VictoryPanelView.StatRow fields, so the runtime fills them as before. Safe to run again.</summary>
    public static class DamageStatisticsPopupBuilder
    {
        private const string StatsDir = "Assets/_Game/Art/Sprite/Play/WinPanel/Reward/Stats";
        private const string FireIcon = "Assets/_Game/Art/Sprite/Play/HUD/icon_damage_fire.png";
        private const string LightningIcon = "Assets/_Game/Art/Sprite/MainMenu/Avatar/lightningicon.png";
        private const string FontPath = "Assets/_Game/Font/Fredoka-Bold SDF.asset";
        private const string FontMaterialPath = "Assets/_Game/Font/Materials/Fredoka_HUD.mat";
        private const int RowCount = 8;
        private const int StarCount = 5;
        private const float RowHeight = 150f;

        private static readonly string[] Scenes =
        {
            "Assets/_Game/Scenes/Levels/Level_01.unity",
            "Assets/_Game/Scenes/Levels/Level_02.unity",
            "Assets/_Game/Scenes/Levels/Level_03.unity",
        };

        private static readonly Color TrackColor = new Color(0.05f, 0.09f, 0.2f, 1f);
        private static readonly Color FillColor = new Color(1f, 0.8f, 0.16f, 1f);
        private static readonly Color DividerColor = new Color(0.62f, 0.78f, 1f, 0.55f);

        private static TMP_FontAsset _font;
        private static Material _fontMaterial;
        private static Sprite _rounded;

        [MenuItem("Tools/Tower Defense/UI/Rebuild Damage Statistics Popup (Levels 1-3)")]
        public static void RebuildFromMenu()
        {
            Debug.Log(RebuildAll());
        }

        public static string RebuildAll()
        {
            if (EditorApplication.isPlaying)
            {
                return "[DamageStatisticsPopupBuilder] Stop Play mode first.";
            }

            AssignTowerTypeIcons();
            var report = new List<string>();
            foreach (string path in Scenes)
            {
                Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                var view = Object.FindFirstObjectByType<VictoryPanelView>(FindObjectsInactive.Include);
                if (view == null)
                {
                    report.Add(scene.name + ": no VictoryPanelView");
                    continue;
                }

                report.Add(scene.name + ": " + Rebuild(view));
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }

            AssetDatabase.SaveAssets();
            return "[DamageStatisticsPopupBuilder] Done.\n" + string.Join("\n", report);
        }

        /// <summary>Mortar burns (fire badge), Blaster is an energy weapon (lightning). No badge art exists for
        /// the others, which then show their name without one.</summary>
        private static void AssignTowerTypeIcons()
        {
            SetTypeIcon("Assets/_Game/Data/Towers/TowerDefinition_Tower_Mortar.asset", FireIcon);
            SetTypeIcon("Assets/_Game/Data/Towers/TowerDefinition_Tower_Blaster.asset", LightningIcon);
        }

        private static void SetTypeIcon(string towerPath, string iconPath)
        {
            Object tower = AssetDatabase.LoadMainAssetAtPath(towerPath);
            if (tower == null)
            {
                return;
            }

            var so = new SerializedObject(tower);
            so.FindProperty("_statsTypeIcon").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>(iconPath);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(tower);
        }

        public static string Rebuild(VictoryPanelView view)
        {
            _font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            _fontMaterial = AssetDatabase.LoadAssetAtPath<Material>(FontMaterialPath);
            _rounded = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");

            var so = new SerializedObject(view);
            var panel = so.FindProperty("_statsPopupPanel").objectReferenceValue as RectTransform;
            if (panel == null)
            {
                return "no stats popup panel";
            }

            foreach (string old in new[] { "TotalHolder", "StatScroll", "StatList" })
            {
                Transform t = panel.Find(old);
                if (t != null)
                {
                    Object.DestroyImmediate(t.gameObject);
                }
            }

            // ---- "All Damage" in the sprite's top box.
            RectTransform totalBox = Box(panel, "TotalHolder", new Vector2(0.116f, 0.768f), new Vector2(0.884f, 0.852f));
            var totalRow = new GameObject("TotalRow", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(ContentSizeFitter));
            totalRow.transform.SetParent(totalBox, false);
            var totalRect = (RectTransform)totalRow.transform;
            totalRect.anchorMin = totalRect.anchorMax = totalRect.pivot = new Vector2(0.5f, 0.5f);
            totalRect.anchoredPosition = Vector2.zero;
            var totalLayout = totalRow.GetComponent<HorizontalLayoutGroup>();
            totalLayout.spacing = 14f;
            totalLayout.childAlignment = TextAnchor.MiddleCenter;
            totalLayout.childControlWidth = totalLayout.childControlHeight = true;
            totalLayout.childForceExpandWidth = totalLayout.childForceExpandHeight = false;
            var totalFitter = totalRow.GetComponent<ContentSizeFitter>();
            totalFitter.horizontalFit = totalFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            Image fire = NewImage(totalRow.transform, "FireIcon", AssetDatabase.LoadAssetAtPath<Sprite>(FireIcon));
            var fireLayout = fire.gameObject.AddComponent<LayoutElement>();
            fireLayout.preferredWidth = fireLayout.preferredHeight = 72f;
            TMP_Text total = NewText(totalRow.transform, "TotalText", "All Damage: 0", 42f, TextAlignmentOptions.MidlineLeft);
            so.FindProperty("_statsTotalText").objectReferenceValue = total;

            // ---- Rows in the sprite's big box.
            RectTransform listBox = Box(panel, "StatList", new Vector2(0.116f, 0.115f), new Vector2(0.884f, 0.755f));
            var scroll = listBox.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 28f;

            var viewport = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D), typeof(Image));
            viewport.transform.SetParent(listBox, false);
            var viewportRect = (RectTransform)viewport.transform;
            viewportRect.anchorMin = Vector2.zero;
            viewportRect.anchorMax = Vector2.one;
            viewportRect.offsetMin = new Vector2(8f, 8f);
            viewportRect.offsetMax = new Vector2(-8f, -8f);
            viewport.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0f); // raycast surface for dragging

            var content = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            content.transform.SetParent(viewport.transform, false);
            var contentRect = (RectTransform)content.transform;
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.pivot = new Vector2(0.5f, 1f);
            contentRect.anchoredPosition = Vector2.zero;
            contentRect.sizeDelta = Vector2.zero;
            var contentLayout = content.GetComponent<VerticalLayoutGroup>();
            contentLayout.childControlWidth = contentLayout.childControlHeight = true;
            contentLayout.childForceExpandWidth = true;
            contentLayout.childForceExpandHeight = false;
            contentLayout.padding = new RectOffset(0, 0, 6, 6);
            content.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.viewport = viewportRect;
            scroll.content = contentRect;

            Sprite star = AssetDatabase.LoadAssetAtPath<Sprite>(StatsDir + "/star.png");
            Sprite noStar = AssetDatabase.LoadAssetAtPath<Sprite>(StatsDir + "/nostar.png");
            so.FindProperty("_starSprite").objectReferenceValue = star;
            so.FindProperty("_noStarSprite").objectReferenceValue = noStar;
            so.FindProperty("_untieredTypeIcon").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>(DamageTypeIconPainter.FistPath);

            SerializedProperty rows = so.FindProperty("_statRows");
            rows.arraySize = RowCount;
            for (int i = 0; i < RowCount; i++)
            {
                BuildRow(content.transform, i, rows.GetArrayElementAtIndex(i), noStar);
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(view);
            return "rows " + RowCount + " rebuilt (" + StarCount + " stars, rounded bars)";
        }

        private static void BuildRow(Transform content, int index, SerializedProperty row, Sprite noStar)
        {
            var go = new GameObject("StatRow_0" + (index + 1), typeof(RectTransform), typeof(LayoutElement));
            go.transform.SetParent(content, false);
            go.GetComponent<LayoutElement>().preferredHeight = RowHeight;
            Transform t = go.transform;

            if (index > 0)
            {
                Image divider = NewImage(t, "Divider", null);
                divider.color = DividerColor;
                Place(divider.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(-40f, 4f));
            }

            Image icon = NewImage(t, "Icon", null);
            icon.preserveAspect = true;
            Place(icon.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(6f, 0f), new Vector2(112f, 112f));

            Image typeIcon = NewImage(t, "TypeIcon", null);
            typeIcon.preserveAspect = true;
            Place(typeIcon.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(134f, -22f), new Vector2(38f, 38f));

            TMP_Text name = NewText(t, "NameText", "Mortar", 40f, TextAlignmentOptions.MidlineLeft);
            name.overflowMode = TextOverflowModes.Ellipsis;
            Place(name.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(178f, -14f), new Vector2(250f, 54f));

            var starsHolder = new GameObject("Stars", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            starsHolder.transform.SetParent(t, false);
            const float starSize = 38f;
            Place((RectTransform)starsHolder.transform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-12f, -22f),
                new Vector2(StarCount * starSize + (StarCount - 1) * 2f, starSize));
            var starLayout = starsHolder.GetComponent<HorizontalLayoutGroup>();
            starLayout.spacing = 2f;
            starLayout.childAlignment = TextAnchor.MiddleRight;
            starLayout.childControlWidth = starLayout.childControlHeight = false;
            starLayout.childForceExpandWidth = starLayout.childForceExpandHeight = false;
            SerializedProperty stars = row.FindPropertyRelative("Stars");
            stars.arraySize = StarCount;
            for (int s = 0; s < StarCount; s++)
            {
                Image starImage = NewImage(starsHolder.transform, "Star_0" + (s + 1), noStar);
                starImage.preserveAspect = true;
                starImage.rectTransform.sizeDelta = new Vector2(starSize, starSize);
                stars.GetArrayElementAtIndex(s).objectReferenceValue = starImage;
            }

            // Dark rounded track with a dark rim, the yellow fill inside it, the percent on top.
            Image rim = NewImage(t, "BarTrack", _rounded);
            rim.type = Image.Type.Sliced;
            rim.pixelsPerUnitMultiplier = 0.35f; // bigger corner radius: a pill, like the reference
            rim.color = new Color(0.02f, 0.04f, 0.1f, 1f);
            Place(rim.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(134f, 24f), new Vector2(316f, 50f));

            Image track = NewImage(rim.transform, "Track", _rounded);
            track.type = Image.Type.Sliced;
            track.pixelsPerUnitMultiplier = 0.4f;
            track.color = TrackColor;
            Stretch(track.rectTransform, 4f);

            var fillArea = new GameObject("FillArea", typeof(RectTransform));
            fillArea.transform.SetParent(track.transform, false);
            Stretch((RectTransform)fillArea.transform, 2f);

            Image bar = NewImage(fillArea.transform, "Bar", _rounded);
            bar.type = Image.Type.Sliced;
            bar.pixelsPerUnitMultiplier = 0.45f;
            bar.color = FillColor;
            bar.rectTransform.anchorMin = Vector2.zero;
            bar.rectTransform.anchorMax = new Vector2(0.6f, 1f);
            bar.rectTransform.offsetMin = bar.rectTransform.offsetMax = Vector2.zero;

            Image shine = NewImage(bar.transform, "Shine", _rounded);
            shine.type = Image.Type.Sliced;
            shine.pixelsPerUnitMultiplier = 0.6f;
            shine.color = new Color(1f, 0.95f, 0.6f, 0.55f);
            shine.rectTransform.anchorMin = new Vector2(0f, 0.55f);
            shine.rectTransform.anchorMax = new Vector2(1f, 0.9f);
            shine.rectTransform.offsetMin = new Vector2(6f, 0f);
            shine.rectTransform.offsetMax = new Vector2(-6f, 0f);

            TMP_Text percent = NewText(rim.transform, "PercentText", "60%", 30f, TextAlignmentOptions.Center);
            Stretch(percent.rectTransform, 0f);

            TMP_Text value = NewText(t, "ValueText", "2,3K", 42f, TextAlignmentOptions.MidlineRight);
            Place(value.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-12f, 22f), new Vector2(190f, 54f));

            row.FindPropertyRelative("Root").objectReferenceValue = go;
            row.FindPropertyRelative("Icon").objectReferenceValue = icon;
            row.FindPropertyRelative("TypeIcon").objectReferenceValue = typeIcon;
            row.FindPropertyRelative("NameText").objectReferenceValue = name;
            row.FindPropertyRelative("PercentText").objectReferenceValue = percent;
            row.FindPropertyRelative("ValueText").objectReferenceValue = value;
            row.FindPropertyRelative("Bar").objectReferenceValue = bar;
            go.SetActive(false);
        }

        private static RectTransform Box(Transform parent, string name, Vector2 min, Vector2 max)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            return rect;
        }

        private static Image NewImage(Transform parent, string name, Sprite sprite)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var image = go.GetComponent<Image>();
            image.sprite = sprite;
            image.raycastTarget = false;
            return image;
        }

        private static TMP_Text NewText(Transform parent, string name, string content, float size, TextAlignmentOptions alignment)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var text = go.GetComponent<TextMeshProUGUI>();
            text.font = _font;
            if (_fontMaterial != null)
            {
                text.fontSharedMaterial = _fontMaterial; // white face, dark outline
            }

            text.text = content;
            text.fontSize = size;
            text.fontStyle = FontStyles.Bold;
            text.color = Color.white;
            text.alignment = alignment;
            text.enableWordWrapping = false;
            text.raycastTarget = false;
            return text;
        }

        private static void Place(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 position, Vector2 size)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        private static void Stretch(RectTransform rect, float inset)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(inset, inset);
            rect.offsetMax = new Vector2(-inset, -inset);
        }
    }
}
