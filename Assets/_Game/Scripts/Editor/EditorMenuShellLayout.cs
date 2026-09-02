using UnityEngine;
using UnityEngine.UI;

namespace AlienDefense.EditorTools
{
    internal static class EditorMenuShellLayout
    {
        public const float TopHudHeight = 140f;
        public const float TopHudTopMargin = 24f;
        // Tall enough to comfortably contain the (now taller, since the icon was re-centered — see
        // MainMenuSceneScaffolder's TileTopPadding) unselected tile without it poking above the bar; the
        // selected tile's extra pop-taller growth is still expected/intended to poke above this.
        public const float BottomNavHeight = 250f;

        public readonly struct ShellRegions
        {
            public ShellRegions(RectTransform topHud, RectTransform menuContentRoot, RectTransform bottomNavigation)
            {
                TopHud = topHud;
                MenuContentRoot = menuContentRoot;
                BottomNavigation = bottomNavigation;
            }

            public RectTransform TopHud { get; }
            public RectTransform MenuContentRoot { get; }
            public RectTransform BottomNavigation { get; }
        }

        public static ShellRegions BuildShellRegions(Transform safeArea)
        {
            RectTransform topHud = CreateTopHudRegion(safeArea);
            RectTransform bottomNavigation = CreateBottomNavRegion(safeArea);
            RectTransform menuContentRoot = CreateMenuContentRoot(safeArea, topHud, bottomNavigation);
            return new ShellRegions(topHud, menuContentRoot, bottomNavigation);
        }

        public static GameObject CreateTabPanel(Transform contentRoot, string name, bool active)
        {
            var panel = new GameObject(name, typeof(RectTransform));
            panel.transform.SetParent(contentRoot, false);
            RectTransform rect = panel.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            panel.SetActive(active);
            return panel;
        }

        public static (RectTransform scrollRect, RectTransform content) BuildScrollPanel(Transform parent, string name)
        {
            var scrollObject = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(ScrollRect));
            scrollObject.transform.SetParent(parent, false);
            RectTransform scrollRectTransform = scrollObject.GetComponent<RectTransform>();
            scrollRectTransform.anchorMin = Vector2.zero;
            scrollRectTransform.anchorMax = Vector2.one;
            scrollRectTransform.offsetMin = Vector2.zero;
            scrollRectTransform.offsetMax = Vector2.zero;
            scrollObject.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0f);

            var viewport = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(Mask));
            viewport.transform.SetParent(scrollObject.transform, false);
            RectTransform viewportRect = viewport.GetComponent<RectTransform>();
            viewportRect.anchorMin = Vector2.zero;
            viewportRect.anchorMax = Vector2.one;
            viewportRect.offsetMin = Vector2.zero;
            viewportRect.offsetMax = Vector2.zero;
            viewport.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0f);
            viewport.GetComponent<Mask>().showMaskGraphic = false;

            var content = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            content.transform.SetParent(viewport.transform, false);
            RectTransform contentRect = content.GetComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.pivot = new Vector2(0.5f, 1f);
            contentRect.anchoredPosition = Vector2.zero;
            contentRect.sizeDelta = new Vector2(0f, 0f);

            VerticalLayoutGroup layout = content.GetComponent<VerticalLayoutGroup>();
            layout.spacing = 16f;
            layout.padding = new RectOffset(24, 24, 24, 24);
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            ContentSizeFitter fitter = content.GetComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            ScrollRect scrollRect = scrollObject.GetComponent<ScrollRect>();
            scrollRect.viewport = viewportRect;
            scrollRect.content = contentRect;
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;

            return (scrollRectTransform, contentRect);
        }

        public static RectTransform CreatePlayRow(Transform playPanel, out RectTransform centerColumn, out RectTransform leftRailSlot, out RectTransform rightRailSlot)
        {
            var row = new GameObject("PlayRow", typeof(RectTransform));
            row.transform.SetParent(playPanel, false);
            RectTransform rowRect = row.GetComponent<RectTransform>();
            rowRect.anchorMin = Vector2.zero;
            rowRect.anchorMax = Vector2.one;
            rowRect.offsetMin = Vector2.zero;
            rowRect.offsetMax = Vector2.zero;

            HorizontalLayoutGroup layout = row.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 8f;
            layout.padding = new RectOffset(8, 8, 8, 8);
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = true;

            leftRailSlot = CreateRailSlot(row.transform, "LeftRailSlot", 110f);
            centerColumn = CreateFlexibleColumn(row.transform, "CenterColumn");
            rightRailSlot = CreateRailSlot(row.transform, "RightRailSlot", 110f);

            return rowRect;
        }

        private static RectTransform CreateTopHudRegion(Transform safeArea)
        {
            var topHud = new GameObject("TopHUD", typeof(RectTransform));
            topHud.transform.SetParent(safeArea, false);
            RectTransform rect = topHud.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, -TopHudTopMargin);
            rect.sizeDelta = new Vector2(-48f, TopHudHeight);
            return rect;
        }

        private static RectTransform CreateBottomNavRegion(Transform safeArea)
        {
            var bottomNav = new GameObject("BottomNavigationSlot", typeof(RectTransform));
            bottomNav.transform.SetParent(safeArea, false);
            RectTransform rect = bottomNav.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(0f, BottomNavHeight);
            return rect;
        }

        private static RectTransform CreateMenuContentRoot(Transform safeArea, RectTransform topHud, RectTransform bottomNav)
        {
            var contentRoot = new GameObject("MenuContentRoot", typeof(RectTransform));
            contentRoot.transform.SetParent(safeArea, false);
            RectTransform rect = contentRoot.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            float topInset = TopHudHeight + TopHudTopMargin;
            rect.offsetMin = new Vector2(0f, BottomNavHeight);
            rect.offsetMax = new Vector2(0f, -topInset);
            return rect;
        }

        private static RectTransform CreateRailSlot(Transform parent, string name, float width)
        {
            var slot = new GameObject(name, typeof(RectTransform), typeof(LayoutElement));
            slot.transform.SetParent(parent, false);
            LayoutElement layoutElement = slot.GetComponent<LayoutElement>();
            layoutElement.preferredWidth = width;
            layoutElement.minWidth = width;
            layoutElement.flexibleWidth = 0f;
            return slot.GetComponent<RectTransform>();
        }

        private static RectTransform CreateFlexibleColumn(Transform parent, string name)
        {
            var column = new GameObject(name, typeof(RectTransform), typeof(LayoutElement), typeof(VerticalLayoutGroup));
            column.transform.SetParent(parent, false);
            LayoutElement layoutElement = column.GetComponent<LayoutElement>();
            layoutElement.flexibleWidth = 1f;
            layoutElement.minWidth = 200f;

            VerticalLayoutGroup layout = column.GetComponent<VerticalLayoutGroup>();
            layout.spacing = 12f;
            layout.padding = new RectOffset(8, 8, 8, 8);
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            return column.GetComponent<RectTransform>();
        }
    }
}
