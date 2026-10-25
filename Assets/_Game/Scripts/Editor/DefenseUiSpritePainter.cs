using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace AlienDefense.EditorTools
{
    /// <summary>Paints the UI art the Defense tab needs and the project did not have: 9-sliced rounded panels (card
    /// frames, header bars, stat cells, popup frame, tabs, buttons) and a few stat icons, in the game's cartoon
    /// style - top-lit gradient, dark outline, a soft highlight under the top edge. Signed-distance shapes at 4x4
    /// supersampling, so edges stay clean. Existing art (tower icons, card art, close button, arrows, padlock,
    /// coin, chest) is reused as it is and never repainted here.</summary>
    public static class DefenseUiSpritePainter
    {
        public const string Folder = "Assets/_Game/Art/Sprite/MainMenu/Defense";
        private const int Samples = 4;

        [MenuItem("Tools/Tower Defense/UI/Paint Defense Tab Sprites")]
        public static void PaintFromMenu()
        {
            Paint();
            Debug.Log("[DefenseUiSpritePainter] Sprites written to " + Folder);
        }

        public static void Paint()
        {
            EditorFolderUtility.EnsureFolder(Folder);

            // name, top, bottom, border, radius, border width
            Panel("card_blue", "#3D9CF2", "#1560C2", "#0A3A86", 22, 6);
            Panel("card_purple", "#C460F5", "#7426CE", "#3F1289", 22, 6);
            Panel("card_pink", "#F577C0", "#C4337F", "#7A1553", 22, 6);
            Panel("card_locked", "#2F6FBE", "#1B4A92", "#0A2F6E", 22, 6);
            Panel("strip_green", "#7BEA57", "#2CAE3A", "#12651F", 12, 4);
            Panel("header_green", "#4DE8A8", "#14B274", "#0A62C8", 20, 7);
            Panel("bar_blue", "#3B93CC", "#2372B0", "#73C4F2", 12, 4);
            Panel("stat_cell", "#74C4EE", "#4E9FD6", "#2470BA", 14, 5);
            Panel("popup_frame", "#2A86E6", "#145CBC", "#0A3C92", 30, 9);
            Panel("popup_inner", "#27549E", "#1A428C", "#143674", 14, 4);
            Panel("tab_green", "#5BEA86", "#1FAE52", "#0C7532", 16, 6);
            Panel("tab_blue", "#3594F2", "#1462CC", "#0A3C92", 16, 6);
            Panel("btn_green", "#8BEE55", "#33B42C", "#156C1A", 20, 7);
            Panel("badge_round", "#2A6FD0", "#123E92", "#7CCBFF", 46, 6, 96);
            Panel("help_square", "#3F7BD8", "#1F4EA8", "#0C2C6E", 10, 4);
            Panel("tooltip", "#12356E", "#0B2552", "#7CCBFF", 18, 5);

            Icon("icon_stat_target", TargetLayers());
            Icon("icon_stat_interval", ClockLayers());
            Icon("icon_stat_range", RangeLayers());
            Icon("icon_stat_burst", BurstLayers());
            Icon("icon_stat_area", AreaLayers());
            Icon("icon_stat_crit", CritLayers());
            Icon("icon_stat_heart", HeartLayers());
            Icon("icon_stat_damage", DamageLayers());

            AssetDatabase.SaveAssets();
        }

        public static Sprite Load(string name)
        {
            return AssetDatabase.LoadAssetAtPath<Sprite>(Folder + "/" + name + ".png");
        }

        // ------------------------------------------------------------------ panels

        private static void Panel(string name, string top, string bottom, string border, int radius, int borderWidth, int size = 96)
        {
            Color topColor = Hex(top), bottomColor = Hex(bottom), borderColor = Hex(border);
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    Color sum = Color.clear;
                    for (int sy = 0; sy < Samples; sy++)
                    {
                        for (int sx = 0; sx < Samples; sx++)
                        {
                            var p = new Vector2(x + (sx + 0.5f) / Samples, (size - 1 - y) + (sy + 0.5f) / Samples); // y down
                            float d = RoundBox(p, new Vector2(size * 0.5f, size * 0.5f), new Vector2(size * 0.5f - 1f, size * 0.5f - 1f), radius);
                            if (d > 0f)
                            {
                                continue;
                            }

                            Color c;
                            if (d > -borderWidth)
                            {
                                c = borderColor;
                            }
                            else
                            {
                                float t = Mathf.InverseLerp(borderWidth, size - borderWidth, p.y);
                                c = Color.Lerp(topColor, bottomColor, t);
                                float inner = -d - borderWidth;
                                // Soft highlight just inside the top border, a slightly darker lip at the bottom.
                                if (p.y < size * 0.5f)
                                {
                                    c = Color.Lerp(c, Color.white, Mathf.Clamp01(1f - inner / 5f) * 0.35f);
                                }
                                else
                                {
                                    c = Color.Lerp(c, borderColor, Mathf.Clamp01(1f - inner / 4f) * 0.3f);
                                }
                            }

                            sum += new Color(c.r, c.g, c.b, 1f);
                        }
                    }

                    Color avg = sum / (Samples * Samples);
                    pixels[y * size + x] = avg.a > 0.0001f ? (Color32)new Color(avg.r / avg.a, avg.g / avg.a, avg.b / avg.a, avg.a) : new Color32(0, 0, 0, 0);
                }
            }

            int slice = Mathf.Min(size / 2 - 2, radius + borderWidth + 2);
            Write(name, pixels, size, new Vector4(slice, slice, slice, slice));
        }

        // ------------------------------------------------------------------ icons

        private delegate float Sdf(Vector2 p);

        private sealed class Layer
        {
            public Sdf Shape;
            public Color Top, Bottom, Outline;
            public float OutlineWidth = 5f;
        }

        private static Layer L(Sdf shape, string top, string bottom, string outline = "#132038", float outlineWidth = 5f)
        {
            return new Layer { Shape = shape, Top = Hex(top), Bottom = Hex(bottom), Outline = Hex(outline), OutlineWidth = outlineWidth };
        }

        private static readonly Vector2 C = new Vector2(64f, 64f);

        private static List<Layer> TargetLayers()
        {
            Sdf ring = p => Mathf.Abs((p - C).magnitude - 38f) - 7f;
            Sdf cross = p => Mathf.Min(Capsule(p, new Vector2(64f, 10f), new Vector2(64f, 118f), 5f), Capsule(p, new Vector2(10f, 64f), new Vector2(118f, 64f), 5f));
            return new List<Layer>
            {
                L(p => Mathf.Min(ring(p), cross(p)), "#FFFFFF", "#C9D6EC"),
                L(p => (p - C).magnitude - 11f, "#FFFFFF", "#DDE6F5", "#132038", 4f),
            };
        }

        private static List<Layer> ClockLayers()
        {
            return new List<Layer>
            {
                L(p => (p - C).magnitude - 50f, "#5DF07A", "#1FA845"),
                L(p => (p - C).magnitude - 36f, "#F4FBFF", "#C8DDF0", "#0E6B2A", 4f),
                L(p => Mathf.Min(Capsule(p, C, new Vector2(64f, 38f), 4.5f), Capsule(p, C, new Vector2(82f, 72f), 4.5f)), "#2B3A55", "#1B2740", "#1B2740", 0f),
            };
        }

        private static List<Layer> RangeLayers()
        {
            return new List<Layer>
            {
                L(p => (p - C).magnitude - 52f, "#74F28A", "#22AE48"),
                L(p => Mathf.Abs((p - C).magnitude - 34f) - 5f, "#EFFFF2", "#C6F3CF", "#0E6B2A", 0f),
                L(p => Mathf.Min(Capsule(p, new Vector2(64f, 18f), new Vector2(64f, 110f), 3.5f), Capsule(p, new Vector2(18f, 64f), new Vector2(110f, 64f), 3.5f)), "#EFFFF2", "#C6F3CF", "#0E6B2A", 0f),
                L(p => (p - C).magnitude - 11f, "#FFFFFF", "#E6FFEA", "#0E6B2A", 4f),
            };
        }

        private static List<Layer> BurstLayers()
        {
            var layers = new List<Layer>();
            float[] xs = { 34f, 64f, 94f };
            float[] tops = { 34f, 18f, 34f };
            for (int i = 0; i < 3; i++)
            {
                float x = xs[i], top = tops[i];
                layers.Add(L(p => Capsule(p, new Vector2(x, top + 14f), new Vector2(x, 104f), 13f), "#FFD86B", "#E08A1C"));
                layers.Add(L(p => RoundBox(p, new Vector2(x, 96f), new Vector2(13f, 12f), 4f), "#C9D2E2", "#7F8BA3", "#132038", 4f));
            }

            return layers;
        }

        private static List<Layer> AreaLayers()
        {
            return new List<Layer>
            {
                L(p => Star(p - C, 56f, 34f, 10), "#FFB03A", "#E8501C"),
                L(p => Star(p - C, 34f, 20f, 10), "#FFF2A0", "#FFD23C", "#B8400F", 0f),
            };
        }

        private static List<Layer> CritLayers()
        {
            return new List<Layer>
            {
                L(p => Star(p - C, 58f, 26f, 5), "#FFE66B", "#F0A21A"),
                L(p => Star(p - new Vector2(64f, 66f), 30f, 14f, 5), "#FFFBD6", "#FFE98A", "#B86A0A", 0f),
            };
        }

        private static List<Layer> HeartLayers()
        {
            Sdf heart = p =>
            {
                float a = (p - new Vector2(44f, 48f)).magnitude - 27f;
                float b = (p - new Vector2(84f, 48f)).magnitude - 27f;
                // lower wedge: a diamond-ish triangle pointing down
                Vector2 q = new Vector2(Mathf.Abs(p.x - 64f), p.y - 54f);
                float wedge = Mathf.Max(q.x * 0.72f + q.y * 0.69f - 36f, -q.y - 6f);
                return Mathf.Min(Mathf.Min(a, b), wedge);
            };
            return new List<Layer>
            {
                L(heart, "#FF7A4A", "#E8242E"),
                L(p => (p - new Vector2(42f, 42f)).magnitude - 9f, "#FFD7C2", "#FFB79A", "#E8242E", 0f),
            };
        }

        private static List<Layer> DamageLayers()
        {
            return new List<Layer>
            {
                // A cartoon shell: brass case, grey tip, nose up-right.
                L(p => Capsule(p, new Vector2(38f, 92f), new Vector2(82f, 48f), 19f), "#FFD86B", "#E08A1C"),
                L(p => Capsule(p, new Vector2(84f, 46f), new Vector2(96f, 34f), 15f), "#E7EDF7", "#9CA9C0"),
                L(p => Capsule(p, new Vector2(30f, 100f), new Vector2(40f, 90f), 21f), "#C9872A", "#99601A"),
            };
        }

        private static void Icon(string name, List<Layer> layers)
        {
            const int size = 128;
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    Color sum = Color.clear;
                    for (int sy = 0; sy < Samples; sy++)
                    {
                        for (int sx = 0; sx < Samples; sx++)
                        {
                            var p = new Vector2(x + (sx + 0.5f) / Samples, (size - 1 - y) + (sy + 0.5f) / Samples);
                            Color c = Color.clear;
                            foreach (Layer layer in layers)
                            {
                                float d = layer.Shape(p);
                                if (d > layer.OutlineWidth)
                                {
                                    continue;
                                }

                                if (d > 0f)
                                {
                                    c = layer.Outline;
                                }
                                else
                                {
                                    float t = Mathf.InverseLerp(14f, 114f, p.y);
                                    Color fill = Color.Lerp(layer.Top, layer.Bottom, t);
                                    float rim = 1f - Mathf.Clamp01(-d / 6f);
                                    c = Color.Lerp(fill, Color.white, rim * Mathf.Clamp01(1f - t * 1.7f) * 0.45f);
                                }

                                c.a = 1f;
                            }

                            sum += c.a > 0f ? new Color(c.r, c.g, c.b, 1f) : Color.clear;
                        }
                    }

                    Color avg = sum / (Samples * Samples);
                    pixels[y * size + x] = avg.a > 0.0001f ? (Color32)new Color(avg.r / avg.a, avg.g / avg.a, avg.b / avg.a, avg.a) : new Color32(0, 0, 0, 0);
                }
            }

            Write(name, pixels, size, Vector4.zero);
        }

        // ------------------------------------------------------------------ io + distance functions

        private static void Write(string name, Color32[] pixels, int size, Vector4 border)
        {
            string path = Folder + "/" + name + ".png";
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
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
            importer.spriteBorder = border;
            importer.textureCompression = TextureImporterCompression.Uncompressed; // tiny, and gradients band when compressed
            importer.SaveAndReimport();
        }

        private static Color Hex(string hex)
        {
            ColorUtility.TryParseHtmlString(hex, out Color color);
            return color;
        }

        private static float RoundBox(Vector2 p, Vector2 centre, Vector2 halfSize, float radius)
        {
            Vector2 q = new Vector2(Mathf.Abs(p.x - centre.x), Mathf.Abs(p.y - centre.y)) - halfSize + new Vector2(radius, radius);
            return new Vector2(Mathf.Max(q.x, 0f), Mathf.Max(q.y, 0f)).magnitude + Mathf.Min(Mathf.Max(q.x, q.y), 0f) - radius;
        }

        private static float Capsule(Vector2 p, Vector2 a, Vector2 b, float radius)
        {
            Vector2 pa = p - a, ba = b - a;
            float h = Mathf.Clamp01(Vector2.Dot(pa, ba) / Vector2.Dot(ba, ba));
            return (pa - ba * h).magnitude - radius;
        }

        /// <summary>Approximate distance to an n-pointed star (negative inside): exact enough for an outlined icon.</summary>
        private static float Star(Vector2 p, float outer, float inner, int points)
        {
            float angle = Mathf.Atan2(p.x, -p.y); // 0 = up (y is down in image space)
            float sector = Mathf.PI / points;
            float a = Mathf.Repeat(angle, sector * 2f);
            if (a > sector)
            {
                a = sector * 2f - a;
            }

            float r = p.magnitude;
            var q = new Vector2(Mathf.Sin(a) * r, Mathf.Cos(a) * r);
            var tip = new Vector2(0f, outer);
            var valley = new Vector2(Mathf.Sin(sector) * inner, Mathf.Cos(sector) * inner);
            Vector2 edge = valley - tip;
            Vector2 normal = new Vector2(edge.y, -edge.x).normalized;
            float d = Vector2.Dot(q - tip, normal);
            return -d;
        }
    }
}
