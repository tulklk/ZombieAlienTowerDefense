using AlienDefense.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AlienDefense.EditorTools
{
    /// <summary>Builds the standard Canvas + SafeArea root shared by every UI scene (Level, MainMenu, LevelSelection, Bootstrap),
    /// plus reusable full-screen-safe layout pieces (centered button stacks) so menu screens stay correctly
    /// positioned and sized across aspect ratios instead of relying on fixed pixel offsets tuned for one panel size.</summary>
    internal static class EditorCanvasUtility
    {
        public static (GameObject canvasObject, Transform safeArea) BuildCanvasWithSafeArea(string canvasName)
        {
            var canvasObject = new GameObject(canvasName);
            var canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            canvasObject.AddComponent<GraphicRaycaster>();

            var safeAreaObject = new GameObject("SafeArea", typeof(RectTransform), typeof(SafeAreaFitter));
            safeAreaObject.transform.SetParent(canvasObject.transform, false);
            var safeAreaRect = safeAreaObject.GetComponent<RectTransform>();
            safeAreaRect.anchorMin = Vector2.zero;
            safeAreaRect.anchorMax = Vector2.one;
            safeAreaRect.offsetMin = Vector2.zero;
            safeAreaRect.offsetMax = Vector2.zero;

            return (canvasObject, safeAreaObject.transform);
        }

        /// <summary>A vertically-centered (anchored 0.5/0.5, not tied to any fixed screen height), auto-sized
        /// stack for a handful of full-width buttons. Correct on any aspect ratio because nothing here is placed
        /// via a hard-coded pixel offset from an edge.</summary>
        public static Transform BuildCenteredButtonStack(Transform parent, float width, float spacing)
        {
            var containerObject = new GameObject("ButtonStack", typeof(RectTransform));
            containerObject.transform.SetParent(parent, false);
            var rect = containerObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(width, 0f);

            var layoutGroup = containerObject.AddComponent<VerticalLayoutGroup>();
            layoutGroup.spacing = spacing;
            layoutGroup.childAlignment = TextAnchor.MiddleCenter;
            layoutGroup.childForceExpandWidth = true;
            layoutGroup.childForceExpandHeight = false;
            layoutGroup.childControlWidth = true;
            layoutGroup.childControlHeight = false;

            // Height only: the container's width is fixed by sizeDelta.x above and stretched onto children via
            // childControlWidth/childForceExpandWidth. Also fitting horizontalFit here would fight that — the
            // container would shrink to its children's preferred width while those children simultaneously wait
            // to be stretched to the container's width, collapsing everything to 0 (the exact bug reported).
            var fitter = containerObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            return containerObject.transform;
        }

        /// <summary>A full-width menu button sized by LayoutElement (works inside a VerticalLayoutGroup like
        /// BuildCenteredButtonStack), not by a fixed RectTransform size tuned for a specific panel.</summary>
        public static Button BuildStackedMenuButton(Transform parent, string name, string label, float height, Color color)
        {
            var buttonObject = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
            buttonObject.transform.SetParent(parent, false);

            var layoutElement = buttonObject.GetComponent<LayoutElement>();
            layoutElement.preferredHeight = height;
            layoutElement.minHeight = height;

            buttonObject.GetComponent<Image>().color = color;
            Button button = buttonObject.GetComponent<Button>();

            var labelObject = new GameObject("Label", typeof(RectTransform));
            labelObject.transform.SetParent(buttonObject.transform, false);
            var labelRect = labelObject.GetComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;

            var text = labelObject.AddComponent<TextMeshProUGUI>();
            text.text = label;
            text.fontSize = 34f;
            text.alignment = TextAlignmentOptions.Center;
            text.color = Color.white;

            return button;
        }
    }
}
