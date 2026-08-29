using AlienDefense.UI;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace AlienDefense.EditorTools
{
    /// <summary>Creates the reusable LevelCard prefab that LevelSelectionPresenter instantiates once per catalog entry.</summary>
    internal static class LevelCardPrefabBuilder
    {
        private const string PrefabFolder = "Assets/_Game/Prefabs/UI";
        private const string PrefabPath = PrefabFolder + "/LevelCard.prefab";

        [MenuItem("AlienDefense/Setup/14. Create LevelCard Prefab")]
        public static GameObject CreateOrLoad()
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (existing != null)
            {
                Debug.Log("[AlienDefense Setup] LevelCard prefab already exists at " + PrefabPath + ", reusing it.");
                return existing;
            }

            EditorFolderUtility.EnsureFolder(PrefabFolder);

            var cardObject = new GameObject("LevelCard", typeof(RectTransform), typeof(Image), typeof(Button));
            var rect = cardObject.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(460f, 160f);
            cardObject.GetComponent<Image>().color = new Color(0.12f, 0.12f, 0.18f, 0.9f);
            Button playButton = cardObject.GetComponent<Button>();

            TMP_Text titleText = LevelSceneScaffolder.CreateTMPText(cardObject.transform, "TitleText", "Level", -20f, 50f, 32f, TextAlignmentOptions.Center);

            var lockedOverlay = new GameObject("LockedOverlay", typeof(RectTransform), typeof(Image));
            lockedOverlay.transform.SetParent(cardObject.transform, false);
            var lockedRect = lockedOverlay.GetComponent<RectTransform>();
            lockedRect.anchorMin = Vector2.zero;
            lockedRect.anchorMax = Vector2.one;
            lockedRect.offsetMin = Vector2.zero;
            lockedRect.offsetMax = Vector2.zero;
            lockedOverlay.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.65f);
            LevelSceneScaffolder.CreateTMPText(lockedOverlay.transform, "LockedText", "Locked", 0f, 40f, 24f, TextAlignmentOptions.Center);

            var view = cardObject.AddComponent<LevelCardView>();
            var serializedView = new SerializedObject(view);
            serializedView.FindProperty("_titleText").objectReferenceValue = titleText;
            serializedView.FindProperty("_lockedOverlay").objectReferenceValue = lockedOverlay;
            serializedView.FindProperty("_playButton").objectReferenceValue = playButton;
            serializedView.ApplyModifiedPropertiesWithoutUndo();

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(cardObject, PrefabPath);
            Object.DestroyImmediate(cardObject);

            Debug.Log("[AlienDefense Setup] Created " + PrefabPath + ".");
            return prefab;
        }
    }
}
