using AlienDefense.Core;
using AlienDefense.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace AlienDefense.EditorTools
{
    /// <summary>Builds the boss health bars from the HUD sprites: the screen-space bar (bosshealthbar frame with a red
    /// fill and a white trailing layer) and the skull icon on the boss's own world-space bar. The prefab is rebuilt
    /// in place, keeping its root, so the scene instance and anything referencing it (pause CanvasGroup, composition
    /// root) stay wired.</summary>
    internal static class BossHealthBarPrefabBuilder
    {
        private const string PrefabFolder = "Assets/_Game/Prefabs/UI";
        private const string PrefabPath = PrefabFolder + "/BossHealthBar.prefab";
        private const string BossPrefabPath = "Assets/_Game/Prefabs/Enemies/Enemy_Boss.prefab";
        private const string FrameSpritePath = "Assets/_Game/Art/Sprite/Play/HUD/bosshealthbar.png";
        public const string IconSpritePath = "Assets/_Game/Art/Sprite/Play/HUD/bossicon.png";

        // Pixel rects measured on bosshealthbar.png (2172x724, y from the top).
        private const float SpriteWidth = 2172f;
        private const float SpriteHeight = 724f;
        private static readonly Rect VisibleArea = Rect.MinMaxRect(26f, 112f, 2125f, 572f);
        // The transparent groove plus a few pixels of overlap, so the frame's inner border hides the fill's corners.
        private static readonly Rect GrooveArea = Rect.MinMaxRect(473f, 274f, 2050f, 423f);

        /// <summary>Bar width on the 1080x1920 canvas; the height follows the sprite's visible aspect.</summary>
        private const float SceneBarWidth = 520f;
        private const float SceneBarTopOffset = -320f;

        private static readonly Color TrackColor = new Color(0.20f, 0.04f, 0.06f, 1f);
        private static readonly Color FillColor = new Color(0.90f, 0.16f, 0.22f, 1f);
        private static readonly Color GhostColor = Color.white;

        [MenuItem("AlienDefense/Setup/HUD/Rebuild Boss Health Bars (Open Scene)")]
        public static void RebuildAllForOpenScene()
        {
            CreateOrRebuild();
            ApplyIconToBossPrefab();

            GameObject canvas = GameObject.Find("Canvas");
            Transform safeArea = canvas != null ? canvas.transform.Find("SafeArea") : null;
            Transform bar = safeArea != null ? safeArea.Find("BossHealthBar") : null;
            if (bar == null)
            {
                Debug.LogWarning("[BossHealthBarPrefabBuilder] Canvas/SafeArea/BossHealthBar not found in the open scene; " +
                                 "the prefabs were rebuilt but nothing in the scene was laid out.");
                return;
            }

            ApplySceneLayout((RectTransform)bar);

            var root = Object.FindFirstObjectByType<LevelCompositionRoot>();
            if (root != null)
            {
                var rootSo = new SerializedObject(root);
                SerializedProperty presenter = rootSo.FindProperty("_bossHealthBarPresenter");
                if (presenter != null && presenter.objectReferenceValue == null)
                {
                    presenter.objectReferenceValue = bar.GetComponentInChildren<BossHealthBarPresenter>(true);
                    rootSo.ApplyModifiedPropertiesWithoutUndo();
                }
            }

            EditorUtility.SetDirty(bar.gameObject);
            EditorSceneManager.MarkSceneDirty(bar.gameObject.scene);
            EditorSceneManager.SaveScene(bar.gameObject.scene);
            Debug.Log("[BossHealthBarPrefabBuilder] Boss health bars rebuilt.");
        }

        public static void ApplySceneLayout(RectTransform rect)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, SceneBarTopOffset);
            rect.sizeDelta = new Vector2(SceneBarWidth, SceneBarWidth * VisibleArea.height / VisibleArea.width);
        }

        public static GameObject CreateOrRebuild()
        {
            EditorFolderUtility.EnsureFolder(PrefabFolder);

            if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) == null)
            {
                var fresh = new GameObject("BossHealthBar", typeof(RectTransform));
                Build(fresh);
                PrefabUtility.SaveAsPrefabAsset(fresh, PrefabPath);
                Object.DestroyImmediate(fresh);
            }
            else
            {
                GameObject contents = PrefabUtility.LoadPrefabContents(PrefabPath);
                try
                {
                    Build(contents);
                    PrefabUtility.SaveAsPrefabAsset(contents, PrefabPath);
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(contents);
                }
            }

            return AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        }

        private static void Build(GameObject root)
        {
            var frameSprite = AssetDatabase.LoadAssetAtPath<Sprite>(FrameSpritePath);
            if (frameSprite == null)
            {
                Debug.LogError("[BossHealthBarPrefabBuilder] Missing sprite " + FrameSpritePath);
            }

            // Keep the root (and its components) - only the children are replaced.
            for (int i = root.transform.childCount - 1; i >= 0; i--)
            {
                Object.DestroyImmediate(root.transform.GetChild(i).gameObject);
            }

            ApplySceneLayout((RectTransform)root.transform);

            RectTransform visuals = NewRect("Visuals", root.transform);
            Stretch(visuals);

            // The sprite has transparent padding; stretch it past the root so its visible part fills the root rect.
            RectTransform bar = NewRect("Bar", visuals);
            float sx = SpriteWidth / VisibleArea.width;
            float sy = SpriteHeight / VisibleArea.height;
            float minX = -(VisibleArea.xMin / SpriteWidth) * sx;
            float minY = -((SpriteHeight - VisibleArea.yMax) / SpriteHeight) * sy;
            bar.anchorMin = new Vector2(minX, minY);
            bar.anchorMax = new Vector2(minX + sx, minY + sy);
            bar.offsetMin = bar.offsetMax = Vector2.zero;

            // Drawn back to front: track, white trail, red fill, then the frame on top.
            NewGrooveImage("Track", bar, TrackColor, filled: false);
            Image ghost = NewGrooveImage("GhostFill", bar, GhostColor, filled: true);
            Image fill = NewGrooveImage("Fill", bar, FillColor, filled: true);

            RectTransform frameRect = NewRect("Frame", bar);
            Stretch(frameRect);
            var frame = frameRect.gameObject.AddComponent<Image>();
            frame.sprite = frameSprite;
            frame.raycastTarget = false;

            visuals.gameObject.SetActive(false);

            var view = root.GetComponent<BossHealthBarView>();
            if (view == null)
            {
                view = root.AddComponent<BossHealthBarView>();
            }

            var viewSo = new SerializedObject(view);
            viewSo.FindProperty("_visualRoot").objectReferenceValue = visuals.gameObject;
            viewSo.FindProperty("_fillImage").objectReferenceValue = fill;
            viewSo.FindProperty("_ghostFillImage").objectReferenceValue = ghost;
            viewSo.FindProperty("_punchTarget").objectReferenceValue = bar;
            viewSo.ApplyModifiedPropertiesWithoutUndo();

            RectTransform presenterRect = NewRect("BossHealthBarPresenter", root.transform);
            var presenter = presenterRect.gameObject.AddComponent<BossHealthBarPresenter>();
            var presenterSo = new SerializedObject(presenter);
            presenterSo.FindProperty("_view").objectReferenceValue = view;
            presenterSo.ApplyModifiedPropertiesWithoutUndo();
        }

        private static Image NewGrooveImage(string name, RectTransform parent, Color color, bool filled)
        {
            RectTransform rect = NewRect(name, parent);
            rect.anchorMin = new Vector2(GrooveArea.xMin / SpriteWidth, 1f - GrooveArea.yMax / SpriteHeight);
            rect.anchorMax = new Vector2(GrooveArea.xMax / SpriteWidth, 1f - GrooveArea.yMin / SpriteHeight);
            rect.offsetMin = rect.offsetMax = Vector2.zero;

            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = EditorScreenBuildingBlocks.SquareBarSprite();
            image.color = color;
            image.raycastTarget = false;
            if (filled)
            {
                image.type = Image.Type.Filled;
                image.fillMethod = Image.FillMethod.Horizontal;
                image.fillOrigin = (int)Image.OriginHorizontal.Left;
                image.fillAmount = 1f;
            }

            return image;
        }

        // ------------------------------------------------------------------ world-space bar on the boss

        /// <summary>Puts the skull icon on the left end of Enemy_Boss's own health bar and makes sure that bar is on.</summary>
        public static void ApplyIconToBossPrefab()
        {
            var icon = AssetDatabase.LoadAssetAtPath<Sprite>(IconSpritePath);
            GameObject root = PrefabUtility.LoadPrefabContents(BossPrefabPath);
            try
            {
                Transform anchor = root.transform.Find("HealthBarAnchor");
                if (anchor != null)
                {
                    anchor.gameObject.SetActive(true);
                }

                var barView = root.GetComponentInChildren<EnemyHealthBarView>(true);
                if (barView == null)
                {
                    Debug.LogError("[BossHealthBarPrefabBuilder] Enemy_Boss has no EnemyHealthBarView.");
                    return;
                }

                AddWorldIcon(barView.transform, icon);
                PrefabUtility.SaveAsPrefabAsset(root, BossPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        /// <summary>Shared with BossContentBuilder so a freshly generated boss gets the same icon.</summary>
        public static void AddWorldIcon(Transform barCanvas, Sprite icon)
        {
            Transform existing = barCanvas.Find("BossIcon");
            if (existing != null)
            {
                Object.DestroyImmediate(existing.gameObject);
            }

            float size = ((RectTransform)barCanvas).sizeDelta.y * 2.2f;

            RectTransform rect = NewRect("BossIcon", barCanvas);
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(-size * 0.3f, 0f);
            rect.sizeDelta = new Vector2(size, size);
            rect.SetAsLastSibling();

            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = icon;
            image.preserveAspect = true;
            image.raycastTarget = false;
        }

        private static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }
    }
}
