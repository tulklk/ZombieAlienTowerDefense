using System.IO;
using UnityEditor;
using UnityEngine;

namespace AlienDefense.EditorTools
{
    /// <summary>Paints the Inventory art the project lacks (2026-10-28): the six UFO-part glyphs (+ "All"), equipment
    /// icons (a steel Mk I and a gold Mk II per part), artifact medallions, a pre-blurred hangar backdrop made from the
    /// menu's own sci-fi room (behindbg.png - no realtime blur), the blue->purple page gradient, green / grey buttons
    /// and the merge arrow. Output: Assets/_Game/Art/UI/Inventory. Re-runnable.</summary>
    public static class InventoryArtBuilder
    {
        public const string Folder = "Assets/_Game/Art/UI/Inventory";
        public static readonly string[] GlyphNames = { "Stick", "Chair", "Reactor", "Cargo", "Antigravity", "Engine" };

        // Slot index = EquipmentSlotType (Controls, Seat, Core, CargoBay, AntiGravity, Engine).
        private static readonly Color[] SlotColors =
        {
            Hex("#3F8CFF"), Hex("#9B5BFF"), Hex("#FF7A3D"), Hex("#2FC3C9"), Hex("#45D16E"), Hex("#FF4D6D"),
        };

        public static void BuildAll()
        {
            if (!AssetDatabase.IsValidFolder("Assets/_Game/Art/UI")) AssetDatabase.CreateFolder("Assets/_Game/Art", "UI");
            if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/_Game/Art/UI", "Inventory");

            Paint("T_Inv_Glyph_All", 128, (x, y) => Mask(GlyphAll(x, y)), Vector4.zero);
            for (int s = 0; s < 6; s++)
            {
                int slot = s;
                Paint("T_Inv_Glyph_" + GlyphNames[s], 128, (x, y) => Mask(GlyphSdf(slot, x, y)), Vector4.zero);
                Paint("T_Inv_Equip_" + GlyphNames[s] + "_1", 256, (x, y) => Metal(GlyphSdf(slot, x * 1.12f, y * 1.12f), y, Hex("#F2F6FB"), Hex("#7C8DA6")), Vector4.zero);
                Paint("T_Inv_Equip_" + GlyphNames[s] + "_2", 256, (x, y) => Metal(GlyphSdf(slot, x * 1.12f, y * 1.12f), y, Hex("#FFEFB0"), Hex("#C9801C")), Vector4.zero);
                Paint("T_Inv_Artifact_" + GlyphNames[s], 256, (x, y) => Medallion(slot, x, y), Vector4.zero);
            }

            PaintGradient();
            PaintButton("T_Inv_ButtonGreen", Hex("#7BE25A"), Hex("#2FA83A"));
            PaintButton("T_Inv_ButtonGrey", Hex("#B8BEC6"), Hex("#7D848D"));
            PaintButton("T_Inv_TabGreen", Hex("#6FDD58"), Hex("#2E9F3A"));
            Paint("T_Inv_MergeArrow", 128, (x, y) => Outlined(MergeArrow(x, y)), Vector4.zero);
            BlurHangar();
            AssetDatabase.Refresh();
        }

        public static Sprite Load(string name) => AssetDatabase.LoadAssetAtPath<Sprite>(Folder + "/" + name + ".png");

        public static Texture2D LoadTexture(string name) => AssetDatabase.LoadAssetAtPath<Texture2D>(Folder + "/" + name + ".png");

        // ------------------------------------------------------------------ shapes (coords -1..1, y up; sdf < 0 inside)

        private static float GlyphAll(float x, float y)
        {
            float d = 9f;
            for (int i = 0; i < 4; i++)
            {
                float cx = i % 2 == 0 ? -0.45f : 0.45f, cy = i < 2 ? -0.45f : 0.45f;
                d = Mathf.Min(d, Box(x - cx, y - cy, 0.33f, 0.33f, 0.1f));
            }

            return d;
        }

        public static float GlyphSdf(int slot, float x, float y)
        {
            switch (slot)
            {
                case 0: // flight stick: gamepad
                {
                    float body = Mathf.Min(Box(x, y - 0.08f, 0.82f, 0.38f, 0.36f),
                        Mathf.Min(Circle(x + 0.52f, y + 0.28f, 0.34f), Circle(x - 0.52f, y + 0.28f, 0.34f)));
                    float cross = Mathf.Min(Box(x + 0.45f, y - 0.06f, 0.2f, 0.065f, 0.02f), Box(x + 0.45f, y - 0.06f, 0.065f, 0.2f, 0.02f));
                    float buttons = Mathf.Min(Circle(x - 0.36f, y - 0.12f, 0.08f), Circle(x - 0.55f, y + 0.04f, 0.08f));
                    return Mathf.Max(body, -Mathf.Min(cross, buttons));
                }
                case 1: // chair
                {
                    float back = Box(x, y - 0.3f, 0.42f, 0.5f, 0.16f);
                    float seat = Box(x, y + 0.28f, 0.62f, 0.14f, 0.08f);
                    float stem = Box(x, y + 0.58f, 0.08f, 0.2f, 0.02f);
                    float foot = Box(x, y + 0.82f, 0.46f, 0.07f, 0.06f);
                    return Mathf.Min(Mathf.Min(back, seat), Mathf.Min(stem, foot));
                }
                case 2: // reactor: target rings
                {
                    float r = Mathf.Sqrt(x * x + y * y);
                    return Mathf.Min(Mathf.Min(Mathf.Abs(r - 0.78f) - 0.12f, Mathf.Abs(r - 0.42f) - 0.1f), r - 0.16f);
                }
                case 3: // cargo: rounded container outline with side handles
                {
                    float outer = Box(x, y, 0.5f, 0.78f, 0.26f);
                    float inner = Box(x, y, 0.3f, 0.58f, 0.14f);
                    float handles = Mathf.Min(Box(x - 0.66f, y, 0.1f, 0.42f, 0.05f), Box(x + 0.66f, y, 0.1f, 0.42f, 0.05f));
                    return Mathf.Min(Mathf.Max(outer, -inner), handles);
                }
                case 4: // antigravity: figure floating over a ring
                {
                    float head = Circle(x, y - 0.5f, 0.22f);
                    float torso = Ellipse(x, y - 0.06f, 0.3f, 0.24f);
                    float ring = Mathf.Abs(Ellipse(x, y + 0.56f, 0.82f, 0.22f)) - 0.07f;
                    return Mathf.Min(Mathf.Min(head, torso), ring);
                }
                default: // engine: cylinder with fins and a nozzle
                {
                    float body = Box(x - 0.1f, y, 0.52f, 0.38f, 0.12f);
                    float nozzle = Box(x + 0.68f, y, 0.14f, 0.26f, 0.04f);
                    float fins = Mathf.Min(Box(x - 0.66f, y, 0.06f, 0.5f, 0.03f), Box(x - 0.8f, y, 0.05f, 0.34f, 0.03f));
                    float grooves = Mathf.Min(Box(x + 0.1f, y, 0.035f, 0.4f, 0f), Box(x - 0.25f, y, 0.035f, 0.4f, 0f));
                    return Mathf.Min(Mathf.Min(Mathf.Max(body, -grooves), nozzle), fins);
                }
            }
        }

        private static float MergeArrow(float x, float y)
        {
            float a = Segment(x, y, -0.8f, 0.5f, 0.05f, 0f) - 0.09f;
            float b = Segment(x, y, -0.8f, -0.5f, 0.05f, 0f) - 0.09f;
            float c = Segment(x, y, 0.05f, 0f, 0.55f, 0f) - 0.09f;
            float head = Mathf.Max(Mathf.Abs(y) - (0.9f - x) * 0.85f, 0.45f - x);
            head = Mathf.Max(head, x - 0.9f);
            return Mathf.Min(Mathf.Min(a, b), Mathf.Min(c, head));
        }

        // ------------------------------------------------------------------ fills

        private static Color Mask(float d)
        {
            return new Color(1f, 1f, 1f, Mathf.Clamp01(0.5f - d * 60f));
        }

        private static Color Outlined(float d)
        {
            Color c = d < -0.03f ? Color.white : Hex("#14244A");
            c.a = Mathf.Clamp01(0.5f - (d - 0.07f) * 60f);
            return c;
        }

        /// <summary>Metallic fill: vertical gradient, rim light, dark outline.</summary>
        private static Color Metal(float d, float y, Color light, Color dark)
        {
            float alpha = Mathf.Clamp01(0.5f - (d - 0.06f) * 120f);
            if (alpha <= 0f) return Color.clear;
            Color c = Color.Lerp(dark, light, Mathf.InverseLerp(-0.9f, 0.9f, y));
            if (d > -0.05f) c = Color.Lerp(c, Color.white, 0.35f);   // rim highlight
            if (d > 0f) c = Hex("#1B2333");                          // outline
            c.a = alpha;
            return c;
        }

        private static Color Medallion(int slot, float x, float y)
        {
            float r = Mathf.Sqrt(x * x + y * y);
            float alpha = Mathf.Clamp01(0.5f - (r - 0.94f) * 120f);
            if (alpha <= 0f) return Color.clear;
            Color c;
            if (r > 0.92f) c = Hex("#3B2208");
            else if (r > 0.72f) c = Color.Lerp(Hex("#C7841D"), Hex("#FFE08A"), Mathf.InverseLerp(-0.8f, 0.8f, y));
            else c = Color.Lerp(SlotColors[slot] * 0.65f, SlotColors[slot], Mathf.InverseLerp(-0.7f, 0.7f, y));
            if (r <= 0.72f)
            {
                float g = GlyphSdf(slot, x / 0.5f, y / 0.5f) * 0.5f;
                if (g < 0.03f) c = Color.Lerp(Hex("#14244A"), c, Mathf.Clamp01((g - 0.01f) * 80f));
                if (g < 0f) c = Color.Lerp(Color.white, Hex("#14244A"), Mathf.Clamp01((g + 0.01f) * 80f));
            }

            c.a = alpha;
            return c;
        }

        // ------------------------------------------------------------------ backgrounds / buttons

        private static void PaintGradient()
        {
            var tex = new Texture2D(4, 256, TextureFormat.RGBA32, false);
            Color top = Hex("#1E73D6"), bottom = Hex("#5A33DA");
            for (int y = 0; y < 256; y++)
            {
                Color c = Color.Lerp(bottom, top, y / 255f);
                for (int x = 0; x < 4; x++) tex.SetPixel(x, y, c);
            }

            Save(tex, "T_Inv_Gradient", Vector4.zero, false);
        }

        private static void PaintButton(string name, Color top, Color bottom)
        {
            const int size = 128;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            Color edge = Hex("#0D1E45");
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float px = (x + 0.5f) / size * 2f - 1f, py = (y + 0.5f) / size * 2f - 1f;
                    float d = Box(px, py, 0.98f, 0.98f, 0.4f) * size * 0.5f;
                    float v = y / (size - 1f);
                    Color c = Color.Lerp(bottom, top, Mathf.SmoothStep(0f, 1f, v));
                    if (v > 0.6f && -d > 8f) c = Color.Lerp(c, Color.white, 0.16f);
                    if (-d < 5f) c = Color.Lerp(edge, c, Mathf.Clamp01(-d - 4f));
                    c.a = Mathf.Clamp01(0.5f - d);
                    tex.SetPixel(x, y, c);
                }
            }

            Save(tex, name, new Vector4(32, 32, 32, 32), true);
        }

        /// <summary>The menu's hangar picture, shrunk and box-blurred three times, slightly darkened.</summary>
        private static void BlurHangar()
        {
            const string source = "Assets/_Game/Art/Sprite/MainMenu/BG/behindbg.png";
            if (!File.Exists(source)) return;
            var src = new Texture2D(2, 2);
            src.LoadImage(File.ReadAllBytes(source));
            int w = 272, h = Mathf.RoundToInt(272f * src.height / src.width);
            var rt = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            RenderTexture prev = RenderTexture.active;
            Graphics.Blit(src, rt);
            var small = new Texture2D(w, h, TextureFormat.RGBA32, false);
            RenderTexture.active = rt;
            small.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            small.Apply();
            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(rt);
            Object.DestroyImmediate(src);
            Color[] px = small.GetPixels();
            for (int pass = 0; pass < 3; pass++) px = BoxBlur(px, w, h, 4);
            for (int i = 0; i < px.Length; i++)
            {
                Color c = px[i] * 0.72f;
                c.a = 1f;
                px[i] = c;
            }

            small.SetPixels(px);
            Save(small, "T_Inv_HangarBlur", Vector4.zero, false);
        }

        private static Color[] BoxBlur(Color[] src, int w, int h, int r)
        {
            var tmp = new Color[src.Length];
            var dst = new Color[src.Length];
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    Color sum = Color.clear;
                    int n = 0;
                    for (int k = -r; k <= r; k++)
                    {
                        int xx = Mathf.Clamp(x + k, 0, w - 1);
                        sum += src[y * w + xx];
                        n++;
                    }

                    tmp[y * w + x] = sum / n;
                }
            }

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    Color sum = Color.clear;
                    int n = 0;
                    for (int k = -r; k <= r; k++)
                    {
                        int yy = Mathf.Clamp(y + k, 0, h - 1);
                        sum += tmp[yy * w + x];
                        n++;
                    }

                    dst[y * w + x] = sum / n;
                }
            }

            return dst;
        }

        // ------------------------------------------------------------------ sdf helpers

        private static float Box(float x, float y, float hw, float hh, float r)
        {
            float qx = Mathf.Abs(x) - (hw - r), qy = Mathf.Abs(y) - (hh - r);
            return new Vector2(Mathf.Max(qx, 0f), Mathf.Max(qy, 0f)).magnitude + Mathf.Min(Mathf.Max(qx, qy), 0f) - r;
        }

        private static float Circle(float x, float y, float r) => Mathf.Sqrt(x * x + y * y) - r;

        private static float Ellipse(float x, float y, float rx, float ry) => (Mathf.Sqrt(x * x / (rx * rx) + y * y / (ry * ry)) - 1f) * Mathf.Min(rx, ry);

        private static float Segment(float x, float y, float ax, float ay, float bx, float by)
        {
            var p = new Vector2(x - ax, y - ay);
            var ab = new Vector2(bx - ax, by - ay);
            float t = Mathf.Clamp01(Vector2.Dot(p, ab) / ab.sqrMagnitude);
            return (p - ab * t).magnitude;
        }

        // ------------------------------------------------------------------ io

        private static void Paint(string name, int size, System.Func<float, float, Color> shader, Vector4 border)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var px = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    // Texture rows go bottom-up, like the shapes (y up).
                    float u = (x + 0.5f) / size * 2f - 1f, v = (y + 0.5f) / size * 2f - 1f;
                    px[y * size + x] = shader(u, v);
                }
            }

            tex.SetPixels(px);
            Save(tex, name, border, false);
        }

        private static void Save(Texture2D tex, string name, Vector4 border, bool sliced)
        {
            tex.Apply();
            string path = Folder + "/" + name + ".png";
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.spriteBorder = border;
            importer.SaveAndReimport();
        }

        private static Color Hex(string hex)
        {
            ColorUtility.TryParseHtmlString(hex, out Color color);
            return color;
        }
    }
}
