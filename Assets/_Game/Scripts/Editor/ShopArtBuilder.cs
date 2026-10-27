using System.IO;
using UnityEditor;
using UnityEngine;

namespace AlienDefense.EditorTools
{
    /// <summary>Paints the Shop's UI sprites (no pack art of this kind exists in the project): the blue-to-purple
    /// gradient, the faint alien / UFO / star / spiral pattern, white rounded frames that are tinted per card,
    /// blue and orange tabs, the diagonal value ribbon, the back arrow, clock, no-ads and badge icons. Product
    /// pictures are composed from the project's own icons (diamond, chest, safe) and the hero picture of the
    /// Construction Queue Pack is rendered from the 3D Base. Output: Assets/_Game/Art/UI/Shop. Re-runnable.</summary>
    public static class ShopArtBuilder
    {
        public const string Folder = "Assets/_Game/Art/UI/Shop";
        private const string Sprites = "Assets/_Game/Art/Sprite/";

        public static void BuildAll()
        {
            EnsureFolder("Assets/_Game/Art/UI");
            EnsureFolder(Folder);
            PaintGradient();
            PaintPattern();
            PaintPanel("T_Shop_Panel", 128, 34, 5, new Color(0.06f, 0.12f, 0.28f), Color.clear);
            PaintPanel("T_Shop_PanelCyan", 128, 34, 5, new Color(0.06f, 0.12f, 0.28f), new Color(0.35f, 0.9f, 1f));
            PaintTab("T_Shop_TabBlue", new Color(0.29f, 0.62f, 0.98f), new Color(0.13f, 0.36f, 0.82f));
            PaintTab("T_Shop_TabOrange", new Color(1f, 0.78f, 0.25f), new Color(0.98f, 0.5f, 0.12f));
            PaintPill();
            PaintRibbon();
            PaintArrow();
            PaintClock();
            PaintNoAds();
            PaintBadge();
            ComposeProducts();
            AssetDatabase.Refresh();
        }

        public static Sprite Load(string name)
        {
            return AssetDatabase.LoadAssetAtPath<Sprite>(Folder + "/" + name + ".png");
        }

        // ------------------------------------------------------------------ painted sprites

        private static void PaintGradient()
        {
            const int h = 256;
            var tex = new Texture2D(4, h, TextureFormat.RGBA32, false);
            Color top = Hex("#2C8FE0"), mid = Hex("#6A36D6"), bottom = Hex("#4A2CB8");
            for (int y = 0; y < h; y++)
            {
                float t = 1f - y / (h - 1f);
                Color c = t < 0.18f ? Color.Lerp(top, mid, t / 0.18f) : Color.Lerp(mid, bottom, (t - 0.18f) / 0.82f);
                for (int x = 0; x < 4; x++)
                {
                    tex.SetPixel(x, y, c);
                }
            }

            Save(tex, "T_Shop_Gradient", Vector4.zero, TextureWrapMode.Clamp);
        }

        /// <summary>White shapes on transparent, meant to be tiled at low alpha: alien heads, saucers, four-point
        /// stars and spirals on a staggered grid.</summary>
        private static void PaintPattern()
        {
            const int size = 512;
            var px = new Color[size * size];
            for (int row = 0; row < 4; row++)
            {
                for (int col = 0; col < 4; col++)
                {
                    float cx = col * 128f + (row % 2 == 0 ? 64f : 0f);
                    float cy = row * 128f + 64f;
                    int kind = (row * 3 + col) % 4;
                    for (int dy = -60; dy <= 60; dy++)
                    {
                        for (int dx = -60; dx <= 60; dx++)
                        {
                            float a = Shape(kind, dx, dy);
                            if (a <= 0f)
                            {
                                continue;
                            }

                            int x = ((int)cx + dx + size) % size;
                            int y = ((int)cy + dy + size) % size;
                            Color c = px[y * size + x];
                            c.r = c.g = c.b = 1f;
                            c.a = Mathf.Max(c.a, a);
                            px[y * size + x] = c;
                        }
                    }
                }
            }

            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.SetPixels(px);
            Save(tex, "T_Shop_Pattern", Vector4.zero, TextureWrapMode.Repeat);
        }

        private static float Shape(int kind, float x, float y)
        {
            switch (kind)
            {
                case 0: // alien head: egg with two dark eyes
                {
                    float head = Ellipse(x, y + 4f, 30f, 38f);
                    float eyes = Mathf.Max(Ellipse(x - 13f, y - 2f, 9f, 14f), Ellipse(x + 13f, y - 2f, 9f, 14f));
                    return Mathf.Clamp01(head - eyes);
                }
                case 1: // saucer
                {
                    float disc = Ellipse(x, y + 4f, 44f, 12f);
                    float dome = Ellipse(x, y - 8f, 20f, 18f) * (y < -2f ? 1f : 0f);
                    return Mathf.Max(disc, dome);
                }
                case 2: // four-point sparkle
                {
                    float ax = Mathf.Abs(x), ay = Mathf.Abs(y);
                    float d = Mathf.Sqrt(ax) + Mathf.Sqrt(ay);
                    return Mathf.Clamp01((7.2f - d) * 1.4f);
                }
                default: // spiral (Archimedean band)
                {
                    float r = Mathf.Sqrt(x * x + y * y);
                    if (r > 36f)
                    {
                        return 0f;
                    }

                    float angle = Mathf.Atan2(y, x);
                    float turns = (r / 12f - angle / (2f * Mathf.PI));
                    float band = Mathf.Abs(turns - Mathf.Round(turns));
                    return Mathf.Clamp01((0.22f - band) * 9f) * Mathf.Clamp01((36f - r) * 0.5f);
                }
            }
        }

        private static float Ellipse(float x, float y, float rx, float ry)
        {
            float d = Mathf.Sqrt(x * x / (rx * rx) + y * y / (ry * ry));
            return Mathf.Clamp01((1f - d) * Mathf.Min(rx, ry));
        }

        /// <summary>White rounded box with a dark outline, a soft top highlight and (optionally) a coloured inner rim -
        /// tinted per card through Image.color. 9-sliced.</summary>
        private static void PaintPanel(string name, int size, float radius, float outline, Color outlineColor, Color rim)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            float half = size * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = RoundedBox(x + 0.5f - half, y + 0.5f - half, half - 1f, radius);
                    float alpha = Mathf.Clamp01(0.5f - d);
                    float inner = -d;
                    Color fill = Color.white;
                    float v = y / (size - 1f);
                    fill *= Mathf.Lerp(0.86f, 1f, v);
                    if (v > 0.72f && inner > outline + 2f)
                    {
                        fill = Color.Lerp(fill, Color.white, 0.35f * Mathf.InverseLerp(0.72f, 1f, v));
                    }

                    Color c = fill;
                    if (rim.a > 0f && inner < outline + 5f)
                    {
                        c = Color.Lerp(rim, fill, Mathf.Clamp01(inner - outline - 3f));
                    }

                    if (inner < outline)
                    {
                        c = Color.Lerp(outlineColor, c, Mathf.Clamp01(inner - outline + 1f));
                    }

                    c.a = alpha;
                    tex.SetPixel(x, y, c);
                }
            }

            Save(tex, name, new Vector4(radius + 6, radius + 6, radius + 6, radius + 6), TextureWrapMode.Clamp);
        }

        private static void PaintTab(string name, Color top, Color bottom)
        {
            const int size = 128;
            const float radius = 26f, outline = 5f;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            float half = size * 0.5f;
            Color edge = Hex("#0D1E45");
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = RoundedBox(x + 0.5f - half, y + 0.5f - half, half - 1f, radius);
                    float inner = -d;
                    float v = y / (size - 1f);
                    Color c = Color.Lerp(bottom, top, Mathf.SmoothStep(0f, 1f, v));
                    if (v > 0.55f && inner > outline + 4f)
                    {
                        c = Color.Lerp(c, Color.white, 0.18f);
                    }

                    if (inner < outline)
                    {
                        c = Color.Lerp(edge, c, Mathf.Clamp01(inner - outline + 1f));
                    }

                    c.a = Mathf.Clamp01(0.5f - d);
                    tex.SetPixel(x, y, c);
                }
            }

            Save(tex, name, new Vector4(32, 32, 32, 32), TextureWrapMode.Clamp);
        }

        private static void PaintPill()
        {
            const int w = 128, h = 64;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    float px = x + 0.5f - w * 0.5f, py = y + 0.5f - h * 0.5f;
                    float d = RoundedRect(px, py, w * 0.5f - 1f, h * 0.5f - 1f, h * 0.5f - 1f);
                    Color c = Color.Lerp(Hex("#05070D"), Hex("#22262F"), y / (h - 1f));
                    if (-d < 3f)
                    {
                        c = Color.Lerp(Hex("#3A3F4A"), c, Mathf.Clamp01(-d - 2f));
                    }

                    c.a = Mathf.Clamp01(0.5f - d);
                    tex.SetPixel(x, y, c);
                }
            }

            Save(tex, "T_Shop_Pill", new Vector4(31, 31, 31, 31), TextureWrapMode.Clamp);
        }

        /// <summary>Orange band across the top-right corner (45 degrees), dark edges, transparent elsewhere.</summary>
        private static void PaintRibbon()
        {
            const int size = 256;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    // Distance of the pixel from the top-right corner along the diagonal.
                    float along = ((size - x) + (size - y)) / 1.41421f;
                    float center = 120f, halfWidth = 34f;
                    float d = Mathf.Abs(along - center) - halfWidth;
                    Color c = Color.Lerp(Hex("#FFB13B"), Hex("#F27A12"), Mathf.InverseLerp(center - halfWidth, center + halfWidth, along));
                    if (d > -4f)
                    {
                        c = Color.Lerp(c, Hex("#FFF2C9"), Mathf.Clamp01(d + 4f) * 0.9f);
                    }

                    c.a = Mathf.Clamp01(0.5f - (d - 3f));
                    tex.SetPixel(x, y, c);
                }
            }

            Save(tex, "T_Shop_Ribbon", Vector4.zero, TextureWrapMode.Clamp);
        }

        private static void PaintArrow()
        {
            const int size = 128;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float px = x + 0.5f, py = y + 0.5f - size * 0.5f;
                    // Head: triangle pointing left; shaft: rounded bar.
                    float head = Mathf.Max(px - 66f, Mathf.Abs(py) - (px - 14f) * 0.95f);
                    float shaft = RoundedRect(px - 82f, py, 34f, 15f, 15f);
                    float d = Mathf.Min(head, shaft);
                    Color c = Color.white;
                    if (d > -6f)
                    {
                        c = Color.Lerp(c, Hex("#0D1E45"), Mathf.Clamp01(d + 6f));
                    }

                    c.a = Mathf.Clamp01(0.5f - (d - 6f));
                    tex.SetPixel(x, y, c);
                }
            }

            Save(tex, "T_Shop_BackArrow", Vector4.zero, TextureWrapMode.Clamp);
        }

        private static void PaintClock()
        {
            const int size = 64;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float px = x + 0.5f - 32f, py = y + 0.5f - 32f;
                    float r = Mathf.Sqrt(px * px + py * py);
                    float ring = Mathf.Abs(r - 25f) - 4f;
                    float hand1 = Mathf.Max(Mathf.Abs(px) - 3f, Mathf.Max(-py - 1f, py - 17f));
                    float hand2 = Mathf.Max(Mathf.Abs(py) - 3f, Mathf.Max(-px - 1f, px - 12f));
                    float d = Mathf.Min(ring, Mathf.Min(hand1, hand2));
                    Color c = Color.white;
                    c.a = Mathf.Clamp01(0.5f - d);
                    tex.SetPixel(x, y, c);
                }
            }

            Save(tex, "T_Shop_Clock", Vector4.zero, TextureWrapMode.Clamp);
        }

        private static void PaintNoAds()
        {
            const int size = 128;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float px = x + 0.5f - 64f, py = y + 0.5f - 64f;
                    float r = Mathf.Sqrt(px * px + py * py);
                    float ring = Mathf.Abs(r - 52f) - 9f;
                    float slash = Mathf.Max(Mathf.Abs(px + py) / 1.41421f - 8f, r - 50f);
                    float red = Mathf.Min(ring, slash);
                    Color c = red < 0.5f ? Hex("#E5262C") : (r < 44f ? Hex("#FFF4E0") : Color.clear);
                    if (red < 0.5f)
                    {
                        c.a = Mathf.Clamp01(0.5f - red);
                    }
                    else if (r < 44f)
                    {
                        c.a = 1f;
                    }

                    tex.SetPixel(x, y, c);
                }
            }

            Save(tex, "T_Shop_NoAds", Vector4.zero, TextureWrapMode.Clamp);
        }

        private static void PaintBadge()
        {
            const int size = 64;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float px = x + 0.5f - 32f, py = y + 0.5f - 32f;
                    float r = Mathf.Sqrt(px * px + py * py);
                    Color c = r > 25f ? Color.white : Color.Lerp(Hex("#C8141B"), Hex("#FF4A3D"), (py + 25f) / 50f);
                    c.a = Mathf.Clamp01(30.5f - r);
                    tex.SetPixel(x, y, c);
                }
            }

            Save(tex, "T_Shop_Badge", Vector4.zero, TextureWrapMode.Clamp);
        }

        // ------------------------------------------------------------------ composed product pictures

        private static void ComposeProducts()
        {
            Color[] gem = ReadPng(Sprites + "MainMenu/Avatar/diamondicon.png", out int gw, out int gh);
            Color[] chest = ReadPng(Sprites + "MainMenu/Reward/chest.png", out int cw, out int ch);
            Color[] open = ReadPng(Sprites + "MainMenu/Reward/openchest.png", out int ow, out int oh);
            Color[] safe = ReadPng(Sprites + "MainMenu/Event/uudai.png", out int sw, out int sh);
            if (gem == null)
            {
                Debug.LogWarning("[ShopArtBuilder] diamondicon.png not readable; product pictures skipped.");
                return;
            }

            var src = new Src(gem, gw, gh);
            Compose("T_Shop_Gems_Handful", 512, c => Pile(c, src, 3, 0.36f, 0.42f));
            Compose("T_Shop_Gems_Pile", 512, c => Pile(c, src, 6, 0.33f, 0.36f));
            Compose("T_Shop_Gems_Mountain", 512, c => Pile(c, src, 15, 0.27f, 0.22f));
            Compose("T_Shop_Gems_Banner", 512, c => Pile(c, src, 10, 0.3f, 0.2f));
            if (chest != null)
            {
                Compose("T_Shop_Gems_Suitcase", 512, c =>
                {
                    Stamp(c, new Src(chest, cw, ch), 0.5f, 0.42f, 0.8f);
                    Pile(c, src, 4, 0.2f, 0.66f);
                });
            }

            if (open != null)
            {
                Compose("T_Shop_Gems_Chest", 512, c =>
                {
                    Stamp(c, new Src(open, ow, oh), 0.5f, 0.42f, 0.86f);
                    Pile(c, src, 5, 0.22f, 0.6f);
                });
            }

            if (safe != null)
            {
                Compose("T_Shop_Gems_Safe", 512, c =>
                {
                    Stamp(c, new Src(safe, sw, sh), 0.52f, 0.52f, 0.84f);
                    Pile(c, src, 5, 0.2f, 0.18f);
                });
            }
        }

        private readonly struct Src
        {
            public readonly Color[] Px;
            public readonly int W, H;

            public Src(Color[] px, int w, int h)
            {
                Px = px;
                W = w;
                H = h;
            }
        }

        private sealed class Canvas
        {
            public readonly Color[] Px;
            public readonly int Size;

            public Canvas(int size)
            {
                Size = size;
                Px = new Color[size * size];
            }
        }

        /// <summary>A pyramid of <paramref name="count"/> gems, each <paramref name="scale"/> of the canvas wide,
        /// bottom row at height <paramref name="baseY"/> (0..1), back rows first.</summary>
        private static void Pile(Canvas canvas, Src gem, int count, float scale, float baseY)
        {
            int rows = 1;
            while (rows * (rows + 1) / 2 < count)
            {
                rows++;
            }

            int placed = 0;
            var random = new System.Random(count * 17);
            for (int row = rows - 1; row >= 0 && placed < count; row--)
            {
                int inRow = Mathf.Min(rows - row, count - placed);
                for (int i = 0; i < inRow; i++)
                {
                    float x = 0.5f + (i - (inRow - 1) * 0.5f) * scale * 0.72f + (float)(random.NextDouble() - 0.5) * 0.03f;
                    float y = baseY + row * scale * 0.48f;
                    Stamp(canvas, gem, x, y, scale * (0.9f + (float)random.NextDouble() * 0.2f));
                    placed++;
                }
            }
        }

        private static void Stamp(Canvas canvas, Src src, float cx, float cy, float width)
        {
            int size = canvas.Size;
            int w = Mathf.RoundToInt(width * size);
            int h = Mathf.RoundToInt(w * (float)src.H / src.W);
            int x0 = Mathf.RoundToInt(cx * size - w * 0.5f);
            int y0 = Mathf.RoundToInt(cy * size - h * 0.5f);
            for (int y = 0; y < h; y++)
            {
                int ty = y0 + y;
                if (ty < 0 || ty >= size)
                {
                    continue;
                }

                for (int x = 0; x < w; x++)
                {
                    int tx = x0 + x;
                    if (tx < 0 || tx >= size)
                    {
                        continue;
                    }

                    Color s = Sample(src, (x + 0.5f) / w, (y + 0.5f) / h);
                    if (s.a <= 0.003f)
                    {
                        continue;
                    }

                    Color d = canvas.Px[ty * size + tx];
                    float a = s.a + d.a * (1f - s.a);
                    Color o = a > 0f ? (s * s.a + d * d.a * (1f - s.a)) / a : Color.clear;
                    o.a = a;
                    canvas.Px[ty * size + tx] = o;
                }
            }
        }

        private static Color Sample(Src src, float u, float v)
        {
            float x = u * (src.W - 1), y = v * (src.H - 1);
            int x0 = (int)x, y0 = (int)y;
            int x1 = Mathf.Min(x0 + 1, src.W - 1), y1 = Mathf.Min(y0 + 1, src.H - 1);
            float fx = x - x0, fy = y - y0;
            Color a = Color.Lerp(src.Px[y0 * src.W + x0], src.Px[y0 * src.W + x1], fx);
            Color b = Color.Lerp(src.Px[y1 * src.W + x0], src.Px[y1 * src.W + x1], fx);
            return Color.Lerp(a, b, fy);
        }

        private static void Compose(string name, int size, System.Action<Canvas> draw)
        {
            var canvas = new Canvas(size);
            draw(canvas);
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.SetPixels(canvas.Px);
            Save(tex, name, Vector4.zero, TextureWrapMode.Clamp);
        }

        private static Color[] ReadPng(string path, out int width, out int height)
        {
            width = height = 0;
            if (!File.Exists(path))
            {
                return null;
            }

            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!tex.LoadImage(File.ReadAllBytes(path)))
            {
                Object.DestroyImmediate(tex);
                return null;
            }

            // Pre-shrink very large sources (1-2k px) so composing stays quick and filtering stays clean.
            const int maxSide = 512;
            if (tex.width > maxSide || tex.height > maxSide)
            {
                float k = maxSide / (float)Mathf.Max(tex.width, tex.height);
                int w = Mathf.RoundToInt(tex.width * k), h = Mathf.RoundToInt(tex.height * k);
                var rt = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
                RenderTexture previous = RenderTexture.active;
                Graphics.Blit(tex, rt);
                var small = new Texture2D(w, h, TextureFormat.RGBA32, false);
                RenderTexture.active = rt;
                small.ReadPixels(new Rect(0, 0, w, h), 0, 0);
                small.Apply();
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(rt);
                Object.DestroyImmediate(tex);
                tex = small;
            }

            width = tex.width;
            height = tex.height;
            Color[] px = tex.GetPixels();
            Object.DestroyImmediate(tex);
            return px;
        }

        // ------------------------------------------------------------------ hero render

        /// <summary>The Construction Queue Pack picture: the Base's construction sites, rendered from the 3D Base
        /// (BaseWorld must be in the open scene). Saved as a 1024x560 sprite.</summary>
        public static Sprite RenderConstructionHero(GameObject baseWorld)
        {
            if (baseWorld == null)
            {
                return null;
            }

            bool wasActive = baseWorld.activeSelf;
            baseWorld.SetActive(true);
            Camera source = baseWorld.GetComponentInChildren<Camera>(true);
            var go = new GameObject("ShopHeroCamera");
            var cam = go.AddComponent<Camera>();
            if (source != null)
            {
                cam.CopyFrom(source);
            }

            cam.enabled = false;
            cam.fieldOfView = 30f;
            cam.cullingMask = 1 << 17;
            go.transform.rotation = Quaternion.Euler(34f, 40f, 0f);
            go.transform.position = new Vector3(-10f, 1.5f, 10f) - go.transform.forward * 30f;
            const int w = 1024, h = 560;
            var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply();
            RenderTexture.active = previous;
            cam.targetTexture = null;
            Object.DestroyImmediate(rt);
            Object.DestroyImmediate(go);
            baseWorld.SetActive(wasActive);

            // Soft vignette at the edges so the picture sits nicely inside the rounded card.
            Color[] px = tex.GetPixels();
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    float u = x / (w - 1f) * 2f - 1f, v = y / (h - 1f) * 2f - 1f;
                    float k = 1f - 0.28f * Mathf.Clamp01((u * u + v * v) - 0.35f);
                    Color c = px[y * w + x] * k;
                    c.a = 1f;
                    px[y * w + x] = c;
                }
            }

            tex.SetPixels(px);
            Save(tex, "T_Shop_ConstructionHero", Vector4.zero, TextureWrapMode.Clamp);
            AssetDatabase.Refresh();
            return Load("T_Shop_ConstructionHero");
        }

        // ------------------------------------------------------------------ helpers

        private static float RoundedBox(float x, float y, float half, float radius)
        {
            return RoundedRect(x, y, half, half, radius);
        }

        private static float RoundedRect(float x, float y, float halfW, float halfH, float radius)
        {
            float qx = Mathf.Abs(x) - (halfW - radius);
            float qy = Mathf.Abs(y) - (halfH - radius);
            float outside = new Vector2(Mathf.Max(qx, 0f), Mathf.Max(qy, 0f)).magnitude;
            return outside + Mathf.Min(Mathf.Max(qx, qy), 0f) - radius;
        }

        private static void Save(Texture2D tex, string name, Vector4 border, TextureWrapMode wrap)
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
            importer.wrapMode = wrap;
            importer.spriteBorder = border;
            importer.textureCompression = TextureImporterCompression.Compressed;
            importer.SaveAndReimport();
        }

        private static void EnsureFolder(string path)
        {
            if (!AssetDatabase.IsValidFolder(path))
            {
                string parent = Path.GetDirectoryName(path).Replace('\\', '/');
                AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
            }
        }

        private static Color Hex(string hex)
        {
            ColorUtility.TryParseHtmlString(hex, out Color color);
            return color;
        }
    }
}
