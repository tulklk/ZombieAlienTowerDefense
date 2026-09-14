using AlienDefense.UI;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace AlienDefense.EditorTools
{
    /// <summary>Builds (or reuses) a screen-space Boss health bar prefab. Not auto-wired into Level_01's Canvas;
    /// see the Phase 15 report's manual Editor Setup steps for dragging it in and assigning LevelCompositionRoot.</summary>
    internal static class BossHealthBarPrefabBuilder
    {
        private const string PrefabFolder = "Assets/_Game/Prefabs/UI";
        private const string PrefabPath = PrefabFolder + "/BossHealthBar.prefab";

        [MenuItem("AlienDefense/Setup/21. Create Boss Health Bar UI Prefab")]
        public static GameObject CreateOrLoad()
        {
            EditorFolderUtility.EnsureFolder(PrefabFolder);

            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (existing != null)
            {
                return existing;
            }

            GameObject root = BuildHierarchy();
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);

            Debug.Log("[AlienDefense Setup] Boss health bar UI prefab ready at " + PrefabPath + ".");
            return AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        }

        private static GameObject BuildHierarchy()
        {
            var root = new GameObject("BossHealthBar", typeof(RectTransform));
            var rootRect = root.GetComponent<RectTransform>();
            rootRect.anchorMin = new Vector2(0.5f, 1f);
            rootRect.anchorMax = new Vector2(0.5f, 1f);
            rootRect.pivot = new Vector2(0.5f, 1f);
            rootRect.anchoredPosition = new Vector2(0f, -40f);
            rootRect.sizeDelta = new Vector2(640f, 90f);

            var visuals = new GameObject("Visuals", typeof(RectTransform));
            visuals.transform.SetParent(root.transform, false);
            var visualsRect = visuals.GetComponent<RectTransform>();
            visualsRect.anchorMin = Vector2.zero;
            visualsRect.anchorMax = Vector2.one;
            visualsRect.offsetMin = Vector2.zero;
            visualsRect.offsetMax = Vector2.zero;

            var nameObject = new GameObject("NameText", typeof(RectTransform));
            nameObject.transform.SetParent(visuals.transform, false);
            var nameRect = nameObject.GetComponent<RectTransform>();
            nameRect.anchorMin = new Vector2(0f, 1f);
            nameRect.anchorMax = new Vector2(1f, 1f);
            nameRect.pivot = new Vector2(0.5f, 1f);
            nameRect.anchoredPosition = Vector2.zero;
            nameRect.sizeDelta = new Vector2(0f, 34f);
            var nameText = nameObject.AddComponent<TextMeshProUGUI>();
            nameText.text = "Boss";
            nameText.fontSize = 30f;
            nameText.alignment = TextAlignmentOptions.Center;
            nameText.color = Color.white;

            var background = new GameObject("Background", typeof(RectTransform), typeof(Image));
            background.transform.SetParent(visuals.transform, false);
            var backgroundRect = background.GetComponent<RectTransform>();
            backgroundRect.anchorMin = new Vector2(0f, 0f);
            backgroundRect.anchorMax = new Vector2(1f, 0f);
            backgroundRect.pivot = new Vector2(0.5f, 0f);
            backgroundRect.anchoredPosition = Vector2.zero;
            backgroundRect.sizeDelta = new Vector2(0f, 36f);
            background.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.6f);

            var fill = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            fill.transform.SetParent(background.transform, false);
            var fillRect = fill.GetComponent<RectTransform>();
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.offsetMin = new Vector2(3f, 3f);
            fillRect.offsetMax = new Vector2(-3f, -3f);
            var fillImage = fill.GetComponent<Image>();
            fillImage.sprite = EditorScreenBuildingBlocks.SquareBarSprite();
            fillImage.color = new Color(0.75f, 0.15f, 0.2f);
            fillImage.type = Image.Type.Filled;
            fillImage.fillMethod = Image.FillMethod.Horizontal;
            fillImage.fillAmount = 1f;

            visuals.SetActive(false);

            var view = root.AddComponent<BossHealthBarView>();
            var viewSerialized = new SerializedObject(view);
            viewSerialized.FindProperty("_visualRoot").objectReferenceValue = visuals;
            viewSerialized.FindProperty("_fillImage").objectReferenceValue = fillImage;
            viewSerialized.FindProperty("_nameText").objectReferenceValue = nameText;
            viewSerialized.ApplyModifiedPropertiesWithoutUndo();

            var presenterObject = new GameObject("BossHealthBarPresenter");
            presenterObject.transform.SetParent(root.transform, false);
            var presenter = presenterObject.AddComponent<BossHealthBarPresenter>();
            var presenterSerialized = new SerializedObject(presenter);
            presenterSerialized.FindProperty("_view").objectReferenceValue = view;
            presenterSerialized.ApplyModifiedPropertiesWithoutUndo();

            return root;
        }
    }
}
