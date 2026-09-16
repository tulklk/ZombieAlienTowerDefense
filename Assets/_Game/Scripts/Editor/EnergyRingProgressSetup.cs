using AlienDefense.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace AlienDefense.EditorTools
{
    /// <summary>Adds the radial progress arc to the HUD energy badge in existing Level scenes: an EnergyFill image
    /// drawn over the badge's ring frame, filled clockwise from the top by CurrentEnergy / MaxEnergy
    /// (GameHUDView.SetEnergy drives it). The frame underneath stays visible as the empty track, so the badge reads
    /// like the collection meters players already know - number for the exact count, arc for "how close to full".
    ///
    /// Re-runnable: an existing EnergyFill is re-configured in place rather than duplicated.</summary>
    internal static class EnergyRingProgressSetup
    {
        private const string RingSpritePath = "Assets/_Game/Art/Sprite/Play/HUD/khung.png";
        private static readonly Color FillColor = new Color(0.66f, 0.97f, 1f, 1f);  // #A8F7FF - bright arc
        private static readonly Color TrackColor = new Color(0.29f, 0.44f, 0.53f, 1f); // #4A7087 - the frame dimmed to read as the empty part

        /// <summary>Patches the scene that is open, so a level with its own HUD layout (or one that fails to load
        /// cleanly) is never opened behind the user's back.</summary>
        [MenuItem("AlienDefense/Setup/HUD/Add Energy Progress Ring (Open Scene)")]
        private static void UpgradeCurrentScene()
        {
            Scene scene = EditorSceneManager.GetActiveScene();
            int patched = UpgradeOpenScene();
            if (patched > 0)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }

            Debug.Log($"[EnergyRingProgressSetup] {scene.path}: patched {patched} GameHUDView(s)");
        }

        internal static int UpgradeOpenScene()
        {
            int count = 0;
            foreach (GameHUDView view in Object.FindObjectsByType<GameHUDView>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                Transform ring = FindDeepChild(view.transform, "EnergyRing");
                if (ring == null)
                {
                    Debug.LogWarning("[EnergyRingProgressSetup] No EnergyRing under " + view.name, view);
                    continue;
                }

                Image fill = EnsureFill(ring);
                var so = new SerializedObject(view);
                SerializedProperty property = so.FindProperty("_energyFillImage");
                if (property == null)
                {
                    Debug.LogWarning("[EnergyRingProgressSetup] GameHUDView has no _energyFillImage field.", view);
                    continue;
                }

                property.objectReferenceValue = fill;
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(view);
                count++;
            }

            return count;
        }

        /// <summary>The arc itself: the same ring sprite as the badge frame, tinted bright, radial-filled. Sits
        /// directly above the frame but below the icon and the forbidden overlay.</summary>
        private static Image EnsureFill(Transform ring)
        {
            Transform existing = ring.Find("EnergyFill");
            GameObject go = existing != null
                ? existing.gameObject
                : new GameObject("EnergyFill", typeof(RectTransform), typeof(Image));

            if (existing == null)
            {
                go.transform.SetParent(ring, false);
                go.transform.SetSiblingIndex(0);
            }

            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = Vector2.zero; // exactly over the frame

            var image = go.GetComponent<Image>();
            Image ringImage = ring.GetComponent<Image>();
            if (ringImage != null)
            {
                // The frame doubles as the track, so it has to sit clearly below the arc in brightness.
                ringImage.color = TrackColor;
                EditorUtility.SetDirty(ringImage);
            }

            image.sprite = ringImage != null && ringImage.sprite != null
                ? ringImage.sprite
                : AssetDatabase.LoadAssetAtPath<Sprite>(RingSpritePath);
            image.color = FillColor;
            image.raycastTarget = false;
            image.type = Image.Type.Filled;
            image.fillMethod = Image.FillMethod.Radial360;
            image.fillOrigin = (int)Image.Origin360.Top;
            image.fillClockwise = true;
            image.fillAmount = 0f;
            EditorUtility.SetDirty(image);
            return image;
        }

        private static Transform FindDeepChild(Transform root, string name)
        {
            if (root.name == name)
            {
                return root;
            }

            for (int i = 0; i < root.childCount; i++)
            {
                Transform found = FindDeepChild(root.GetChild(i), name);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }
    }
}
