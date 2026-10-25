using System.IO;
using AlienDefense.UI.MainMenu;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace AlienDefense.EditorTools
{
    /// <summary>The light-ray halo behind opened objective chests on the main menu (user, 2026-10-11): a warm amber glow, a gold ray burst (colour-ramped texture:
    /// white-hot root, gold, amber tips) with a fainter counter-turning additive burst, a hot core and twinkling glints, clipped
    /// to the slot, breathing (gold since 2026-10-11 on user request; was cyan) (LevelObjectiveView
    /// animates it and hides it while the chest is locked). Generates its three textures, adds "AuraMask/Aura" behind
    /// each chest icon, removes the stray duplicate Glow children, and wires LevelObjectiveView._aura. Safe to run again.</summary>
    public static class ObjectiveChestAuraSetup
    {
        private const string ScenePath = "Assets/_Game/Scenes/Menu/MainMenu.unity";
        private const string TextureDir = "Assets/_Game/Art/Textures/UI";
        private const string RaysPath = TextureDir + "/T_ChestAuraRays.png";
        private const string GlowPath = TextureDir + "/T_ChestAuraGlow.png";
        private const string SparklePath = TextureDir + "/T_ChestAuraSparkle.png";
        private const string AdditivePath = "Assets/_Game/Materials/UI/MAT_UIAdditive.mat";
        private const float MaskSize = 128f;
        // Everything fades to nothing inside the mask: a layer bigger than MaskSize got cut flat by the RectMask2D and
        // showed as a square frame round the chest (user, 2026-10-15).
        private const float RaySize = 126f;

        [MenuItem("Tools/Tower Defense/UI/Setup Objective Chest Aura (MainMenu)")]
        public static void SetupFromMenu()
        {
            Debug.Log(Setup());
        }

        public static string Setup()
        {
            if (EditorApplication.isPlaying)
            {
                return "[ObjectiveChestAuraSetup] Stop Play mode first.";
            }

            Sprite rays = WriteSprite(RaysPath, RaysPixel, GoldByRadius);
            Sprite glow = WriteSprite(GlowPath, GlowPixel, null);
            Sprite sparkle = WriteSprite(SparklePath, SparklePixel, null);
            var additive = AssetDatabase.LoadAssetAtPath<Material>(AdditivePath);

            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            int count = 0, removed = 0;
            foreach (LevelObjectiveView view in Object.FindObjectsByType<LevelObjectiveView>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var so = new SerializedObject(view);
                var icon = so.FindProperty("_icon").objectReferenceValue as Image;
                var keptGlow = so.FindProperty("_glow").objectReferenceValue as Image;
                if (icon == null)
                {
                    continue;
                }

                // Earlier setup runs left several unused "Glow" copies under the icon.
                for (int i = icon.transform.childCount - 1; i >= 0; i--)
                {
                    Transform child = icon.transform.GetChild(i);
                    if (child.name == "Glow" && (keptGlow == null || child != keptGlow.transform))
                    {
                        Object.DestroyImmediate(child.gameObject);
                        removed++;
                    }
                }

                Transform old = view.transform.Find("AuraMask");
                if (old != null)
                {
                    Object.DestroyImmediate(old.gameObject);
                }

                var mask = new GameObject("AuraMask", typeof(RectTransform), typeof(RectMask2D));
                mask.transform.SetParent(view.transform, false);
                mask.transform.SetSiblingIndex(icon.transform.GetSiblingIndex()); // behind the chest
                var maskRect = (RectTransform)mask.transform;
                var iconRect = icon.rectTransform;
                maskRect.anchorMin = iconRect.anchorMin;
                maskRect.anchorMax = iconRect.anchorMax;
                maskRect.pivot = new Vector2(0.5f, 0.5f);
                maskRect.anchoredPosition = iconRect.anchoredPosition;
                maskRect.sizeDelta = new Vector2(MaskSize, MaskSize);

                var aura = new GameObject("Aura", typeof(RectTransform), typeof(CanvasGroup));
                aura.transform.SetParent(mask.transform, false);
                var auraRect = (RectTransform)aura.transform;
                auraRect.anchorMin = auraRect.anchorMax = auraRect.pivot = new Vector2(0.5f, 0.5f);
                auraRect.anchoredPosition = Vector2.zero;
                auraRect.sizeDelta = new Vector2(RaySize, RaySize);
                var group = aura.GetComponent<CanvasGroup>();
                group.blocksRaycasts = false;
                group.interactable = false;

                // Warm gold: a wide soft glow, the main burst, a fainter counter-turning burst, a hot core, glints.
                // Normal blend for the glow and the main burst (additive gold over the blue slot washed out to white /
                // beige); the rays carry their own colour ramp (white-hot root -> gold -> amber tips).
                AddImage(aura.transform, "AuraGlow", glow, null, new Color(1f, 0.58f, 0.06f, 0.95f), MaskSize * 0.96f);
                Image mainRays = AddImage(aura.transform, "AuraRays", rays, null, Color.white, RaySize);
                Image counterRays = AddImage(aura.transform, "AuraRaysCounter", rays, additive, new Color(1f, 0.8f, 0.35f, 0.4f), RaySize * 0.8f);
                counterRays.rectTransform.localEulerAngles = new Vector3(0f, 0f, 11f);
                AddImage(aura.transform, "AuraCore", glow, additive, new Color(1f, 0.92f, 0.6f, 1f), 78f);

                Vector2[] glints = { new Vector2(-44f, 30f), new Vector2(46f, 38f), new Vector2(40f, -40f), new Vector2(-38f, -34f) };
                float[] glintSize = { 26f, 20f, 24f, 18f };
                SerializedProperty sparkles = so.FindProperty("_auraSparkles");
                sparkles.arraySize = glints.Length;
                for (int g = 0; g < glints.Length; g++)
                {
                    Image glint = AddImage(aura.transform, "Sparkle_0" + (g + 1), sparkle, additive, new Color(1f, 0.97f, 0.8f, 1f), glintSize[g]);
                    glint.rectTransform.anchoredPosition = glints[g];
                    sparkles.GetArrayElementAtIndex(g).objectReferenceValue = glint;
                }

                so.FindProperty("_aura").objectReferenceValue = auraRect;
                so.FindProperty("_auraRays").objectReferenceValue = mainRays.rectTransform;
                so.FindProperty("_auraCounterRays").objectReferenceValue = counterRays.rectTransform;
                so.ApplyModifiedPropertiesWithoutUndo();
                aura.SetActive(false); // LevelObjectiveView shows it for opened chests
                count++;
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            return "[ObjectiveChestAuraSetup] Aura added to " + count + " chests; " + removed + " stray Glow copies removed.";
        }

        private static Image AddImage(Transform parent, string name, Sprite sprite, Material material, Color color, float size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(size, size);
            var image = go.GetComponent<Image>();
            image.sprite = sprite;
            image.material = material;
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        // ------------------------------------------------------------------ textures (white; tinted by the Image)

        /// <summary>16 rays of uneven length and width (long / short alternating, slightly jittered), each with a
        /// soft gaussian cross-section, brightest at the root and fading smoothly to nothing - light, not spokes.</summary>
        private static float RaysPixel(float x, float y)
        {
            const int count = 16;
            float r = Mathf.Sqrt(x * x + y * y);
            float angle = Mathf.Atan2(y, x);
            float step = Mathf.PI * 2f / count;
            float best = 0f;
            for (int i = 0; i < count; i++)
            {
                float jitter = Mathf.Sin(i * 12.9898f) * 0.12f;
                float centre = i * step + jitter;
                float d = Mathf.DeltaAngle(angle * Mathf.Rad2Deg, centre * Mathf.Rad2Deg) * Mathf.Deg2Rad;
                bool longRay = i % 2 == 0;
                float length = longRay ? 1f : 0.68f + Mathf.Abs(Mathf.Sin(i * 7.31f)) * 0.12f;
                float width = (longRay ? 0.085f : 0.065f) * (1f + r * 0.4f); // rays widen a little outward
                float across = Mathf.Exp(-(d * d) / (2f * width * width));
                float along = Mathf.Pow(Mathf.Clamp01(1f - r / length), 1.15f);
                best = Mathf.Max(best, across * along);
            }

            float core = Mathf.Exp(-r * r * 22f) * 0.6f;
            return Mathf.Clamp01(best * 1.15f + core);
        }

        /// <summary>Colour ramp of the ray burst by distance from the centre: white-hot root, rich gold, amber tips.</summary>
        private static Color GoldByRadius(float r)
        {
            var hot = new Color(1f, 0.98f, 0.82f);
            var gold = new Color(1f, 0.82f, 0.2f);
            var amber = new Color(1f, 0.55f, 0.05f);
            return r < 0.3f ? Color.Lerp(hot, gold, r / 0.3f) : Color.Lerp(gold, amber, Mathf.InverseLerp(0.3f, 0.9f, r));
        }

        private static float GlowPixel(float x, float y)
        {
            float r = Mathf.Sqrt(x * x + y * y);
            float soft = Mathf.Exp(-r * r * 3.2f);
            float edge = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.75f, 1f, r));
            return Mathf.Clamp01(soft * edge);
        }

        /// <summary>A four-point glint: two thin crossed streaks over a small round core.</summary>
        private static float SparklePixel(float x, float y)
        {
            float ax = Mathf.Abs(x), ay = Mathf.Abs(y);
            float streakH = Mathf.Exp(-ay * 26f) * Mathf.Clamp01(1f - ax);
            float streakV = Mathf.Exp(-ax * 26f) * Mathf.Clamp01(1f - ay);
            float core = Mathf.Exp(-(x * x + y * y) * 30f);
            return Mathf.Clamp01(Mathf.Max(streakH, streakV) + core);
        }

        private delegate float Pixel(float x, float y);

        private delegate Color Ramp(float r);

        /// <summary>White texture with the shape in alpha, or coloured by the ramp (distance from the centre).</summary>
        private static Sprite WriteSprite(string path, Pixel pixel, Ramp ramp)
        {
            const int size = 256;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float u = (x + 0.5f) / size * 2f - 1f;
                    float v = (y + 0.5f) / size * 2f - 1f;
                    byte a = (byte)Mathf.RoundToInt(pixel(u, v) * 255f);
                    Color32 rgb = ramp != null ? (Color32)ramp(Mathf.Sqrt(u * u + v * v)) : new Color32(255, 255, 255, 255);
                    pixels[y * size + x] = new Color32(rgb.r, rgb.g, rgb.b, a);
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply();
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }
    }
}
