using System;
using System.IO;
using AlienDefense.UI;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace AlienDefense.EditorTools
{
    /// <summary>The floating "+N XP" popup: a white number next to a generated orange star badge with XP on it, like
    /// the reference. Also drops its canvas below the gameplay HUD's (it used to sort at 50, which put it on top of
    /// the Ability choice / pause / win panels). The prefab is rebuilt in place, so the UFO's reference to it and its
    /// XpPopupView settings (duration, rise) survive.</summary>
    internal static class XpPopupBadgeSetup
    {
        private const string PrefabPath = "Assets/_Game/Prefabs/UI/Prefab_XpPopup.prefab";
        private const string BadgeTexturePath = "Assets/_Game/Art/Sprite/Play/HUD/icon_xp_badge.png";
        private const string FontPath = "Assets/_Game/Font/Fredoka-Bold SDF.asset";

        /// <summary>Under the main Canvas (0) so panels cover the popup, still over the 3D world (overlay).</summary>
        private const int CanvasSortingOrder = -1;

        private static readonly Color BadgeLight = Hex(0xFFC12E);
        private static readonly Color BadgeDeep = Hex(0xFF7A16);
        private static readonly Color BadgeOutline = Hex(0x7A2A05);
        private static readonly Color BadgeLetters = Hex(0xFFF3D6);
        private static readonly Color NumberColor = Hex(0xFFFFFF);
        private static readonly Color NumberOutline = Hex(0x2A1606);

        [MenuItem("AlienDefense/Setup/HUD/Rebuild XP Popup")]
        private static void Run()
        {
            Sprite badge = EnsureBadgeSprite();
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);

            GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                var canvas = root.GetComponent<Canvas>();
                if (canvas != null)
                {
                    canvas.sortingOrder = CanvasSortingOrder;
                }

                for (int i = root.transform.childCount - 1; i >= 0; i--)
                {
                    Object.DestroyImmediate(root.transform.GetChild(i).gameObject);
                }

                RectTransform content = NewRect("Content", root.transform);
                content.sizeDelta = Vector2.zero;

                // "+6"
                RectTransform numberRect = NewRect("Amount", content);
                numberRect.sizeDelta = new Vector2(120f, 56f);
                numberRect.anchoredPosition = new Vector2(-56f, 0f);
                var number = numberRect.gameObject.AddComponent<TextMeshProUGUI>();
                number.font = font;
                number.fontSize = 50f;
                number.fontStyle = FontStyles.Bold;
                number.alignment = TextAlignmentOptions.Right;
                number.textWrappingMode = TextWrappingModes.NoWrap;
                number.color = NumberColor;
                number.outlineColor = NumberOutline;
                number.outlineWidth = 0.25f;
                number.raycastTarget = false;
                number.text = "+0";

                // The badge, tilted like the reference.
                RectTransform badgeRect = NewRect("XpBadge", content);
                badgeRect.sizeDelta = new Vector2(78f, 78f);
                badgeRect.anchoredPosition = new Vector2(44f, 0f);
                badgeRect.localRotation = Quaternion.Euler(0f, 0f, -8f);
                var badgeImage = badgeRect.gameObject.AddComponent<Image>();
                badgeImage.sprite = badge;
                badgeImage.raycastTarget = false;
                badgeImage.preserveAspect = true;

                RectTransform lettersRect = NewRect("XpLabel", badgeRect);
                lettersRect.anchorMin = Vector2.zero;
                lettersRect.anchorMax = Vector2.one;
                lettersRect.offsetMin = lettersRect.offsetMax = Vector2.zero;
                var letters = lettersRect.gameObject.AddComponent<TextMeshProUGUI>();
                letters.font = font;
                letters.fontSize = 34f;
                letters.fontStyle = FontStyles.Bold;
                letters.alignment = TextAlignmentOptions.Center;
                letters.color = BadgeLetters;
                letters.outlineColor = Hex(0x7A2A05);
                letters.outlineWidth = 0.22f;
                letters.raycastTarget = false;
                letters.text = "XP";

                var view = root.GetComponent<XpPopupView>();
                var so = new SerializedObject(view);
                so.FindProperty("_rectTransform").objectReferenceValue = root.transform;
                so.FindProperty("_label").objectReferenceValue = number;
                so.FindProperty("_content").objectReferenceValue = content;
                so.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }

            Debug.Log("[XpPopupBadgeSetup] XP popup rebuilt with the star badge; canvas sorted under the HUD.");
        }

        private static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        private static Sprite EnsureBadgeSprite()
        {
            var existing = AssetDatabase.LoadAssetAtPath<Sprite>(BadgeTexturePath);
            if (existing != null)
            {
                return existing;
            }

            const int size = 128;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    texture.SetPixel(x, y, BadgePixel((x + 0.5f) / size, (y + 0.5f) / size));
                }
            }

            File.WriteAllBytes(BadgeTexturePath, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(BadgeTexturePath);

            var importer = (TextureImporter)AssetImporter.GetAtPath(BadgeTexturePath);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.mipmapEnabled = false;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(BadgeTexturePath);
        }

        /// <summary>An eight-point star badge: dark rim, deep orange body, lighter towards the middle.</summary>
        private static Color BadgePixel(float u, float v)
        {
            float dx = u - 0.5f;
            float dy = v - 0.5f;
            float r = Mathf.Sqrt(dx * dx + dy * dy) * 2f;
            float angle = Mathf.Atan2(dy, dx);

            // Rounded eight-point star, points up/down/left/right and on the diagonals.
            float wave = Mathf.Cos(angle * 8f);
            float star = 0.82f + 0.16f * Mathf.Sign(wave) * Mathf.Pow(Mathf.Abs(wave), 0.6f); // fat star, softly rounded points
            float edge = star;
            float alpha = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(edge - 0.06f, edge, r));
            if (alpha <= 0.01f)
            {
                return new Color(0f, 0f, 0f, 0f);
            }

            float rim = Mathf.InverseLerp(edge - 0.22f, edge - 0.08f, r);
            Color body = Color.Lerp(BadgeLight, BadgeDeep, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.1f, 0.75f, r)));
            Color color = Color.Lerp(body, BadgeOutline, Mathf.SmoothStep(0f, 1f, rim));
            color.a = alpha;
            return color;
        }

        private static Color Hex(int rgb)
        {
            return new Color(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f, 1f);
        }
    }
}
