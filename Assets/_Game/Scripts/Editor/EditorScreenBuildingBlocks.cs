using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace AlienDefense.EditorTools
{
    /// <summary>Generic small UI-building primitives shared by the Upgrade/Defense/Shop/Base scene scaffolders —
    /// full-screen images, anchored images/text, a top-left Back button, a stat row, and Build Settings
    /// registration. Purely Editor-time scene construction, no runtime behavior.</summary>
    internal static class EditorScreenBuildingBlocks
    {
        public static Image BuildFullScreenImage(Transform parent, string name, Color color)
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

        public static Image BuildAnchoredImage(Transform parent, string name, Vector2 anchor, Vector2 size, Vector2 anchoredPosition, Color color)
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

        public static TMP_Text BuildAnchoredText(Transform parent, string name, string initialText, Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPosition, Vector2 size, float fontSize, TextAlignmentOptions alignment)
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

        public static Button BuildTopLeftButton(Transform safeArea, string name, string glyph, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(safeArea, false);
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(24f, -24f);
            rect.sizeDelta = new Vector2(64f, 64f);
            go.GetComponent<Image>().color = color;

            BuildAnchoredText(go.transform, "Label", glyph, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(64f, 64f), 32f, TextAlignmentOptions.Center);

            return go.GetComponent<Button>();
        }

        public static Button BuildSmallCircleButton(Transform parent, string name, string glyph, Color color, Vector2 anchor, Vector2 anchoredPosition)
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

        /// <summary>A single left-aligned stat line inside a VerticalLayoutGroup.</summary>
        public static TMP_Text BuildStatRow(Transform parent, string name, string initialText)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            go.GetComponent<LayoutElement>().preferredHeight = 44f;

            var text = go.AddComponent<TextMeshProUGUI>();
            text.text = initialText;
            text.fontSize = 26f;
            text.alignment = TextAlignmentOptions.MidlineLeft;
            text.color = Color.white;
            return text;
        }

        public static Image BuildBarFill(Transform parent, string name, Vector2 anchoredPosition, Vector2 size, Color fillColor)
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

        public static void EnsureSceneInBuildSettings(string scenePath)
        {
            var scenes = new System.Collections.Generic.List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            foreach (EditorBuildSettingsScene existing in scenes)
            {
                if (existing.path == scenePath)
                {
                    return;
                }
            }

            scenes.Add(new EditorBuildSettingsScene(scenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
