using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace AlienDefense.EditorTools
{
    /// <summary>Paints the damage-type badges the art set did not have (user, 2026-10-11), in the same cartoon style
    /// as icon_damage_fire: a bold dark outline, a top-lit gradient fill and a soft highlight. Shapes are signed
    /// distance fields rasterised at 4x4 supersampling, so edges are clean at 128 px:
    ///   icon_damage_fist.png  - a clenched fist (plain / physical hits: the UFO and its rocket)
    ///   icon_damage_frost.png - a snowflake (the Frost tower)
    /// Then wires them: Frost tower's Stats Type Icon, and the victory panel's badge for sources without a tower.</summary>
    public static class DamageTypeIconPainter
    {
        private const string Folder = "Assets/_Game/Art/Sprite/Play/HUD";
        public const string FistPath = Folder + "/icon_damage_fist.png";
        public const string FrostPath = Folder + "/icon_damage_frost.png";
        private const int Size = 128;
        private const int Samples = 4;

        private delegate float Sdf(Vector2 p);

        private sealed class Layer
        {
            public Sdf Shape;
            public Color Top;
            public Color Bottom;
            public Color Outline;
            public float OutlineWidth;
        }

        [MenuItem("Tools/Tower Defense/UI/Paint Damage Type Icons")]
        public static void PaintFromMenu()
        {
            Debug.Log(Paint());
        }

        public static string Paint()
        {
            Write(FistPath, FistLayers());
            Write(FrostPath, FrostLayers());
            AssetDatabase.Refresh();
            ImportAsSprite(FistPath);
            ImportAsSprite(FrostPath);

            Object frost = AssetDatabase.LoadMainAssetAtPath("Assets/_Game/Data/Towers/TowerDefinition_Tower_Frost.asset");
            if (frost != null)
            {
                var so = new SerializedObject(frost);
                so.FindProperty("_statsTypeIcon").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>(FrostPath);
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(frost);
            }

            AssetDatabase.SaveAssets();
            return "[DamageTypeIconPainter] Painted " + FistPath + " and " + FrostPath + "; Frost tower uses the snowflake.";
        }

        // ------------------------------------------------------------------ fist

        private static List<Layer> FistLayers()
        {
            var skinTop = new Color(0.97f, 0.97f, 0.99f);
            var skinBottom = new Color(0.68f, 0.71f, 0.78f);
            var outline = new Color(0.13f, 0.15f, 0.22f);
            const float w = 5f;
            var layers = new List<Layer>
            {
                // Cuff / wrist band.
                new Layer { Shape = p => RoundBox(p, new Vector2(64f, 108f), new Vector2(26f, 12f), 6f),
                    Top = new Color(0.55f, 0.6f, 0.72f), Bottom = new Color(0.36f, 0.4f, 0.52f), Outline = outline, OutlineWidth = w },
                // Back of the hand.
                new Layer { Shape = p => RoundBox(p, new Vector2(62f, 74f), new Vector2(40f, 28f), 16f),
                    Top = skinTop, Bottom = skinBottom, Outline = outline, OutlineWidth = w },
            };

            // Four curled fingers across the top, each with its own outline (the creases between them).
            float[] fingerX = { 31f, 52f, 73f, 94f };
            float[] fingerTop = { 34f, 28f, 29f, 35f };
            for (int i = 0; i < fingerX.Length; i++)
            {
                float cx = fingerX[i];
                float cy = fingerTop[i] + 15f;
                layers.Add(new Layer { Shape = p => RoundBox(p, new Vector2(cx, cy), new Vector2(10.5f, 16f), 9f),
                    Top = skinTop, Bottom = skinBottom, Outline = outline, OutlineWidth = w });
            }

            // Thumb folded across the fingers.
            layers.Add(new Layer { Shape = p => Capsule(p, new Vector2(30f, 66f), new Vector2(74f, 74f), 11f),
                Top = skinTop, Bottom = new Color(0.78f, 0.8f, 0.86f), Outline = outline, OutlineWidth = w });
            return layers;
        }

        // ------------------------------------------------------------------ snowflake

        private static List<Layer> FrostLayers()
        {
            var centre = new Vector2(64f, 64f);
            Sdf flake = p =>
            {
                float d = float.MaxValue;
                for (int a = 0; a < 6; a++)
                {
                    float angle = a * Mathf.PI / 3f + Mathf.PI / 2f;
                    var dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                    Vector2 tip = centre + dir * 48f;
                    d = Mathf.Min(d, Capsule(p, centre, tip, 6.5f));

                    // Two side branches on each arm.
                    Vector2 root = centre + dir * 28f;
                    foreach (float side in new[] { -1f, 1f })
                    {
                        float branchAngle = angle + side * Mathf.PI / 4f;
                        var branchDir = new Vector2(Mathf.Cos(branchAngle), Mathf.Sin(branchAngle));
                        d = Mathf.Min(d, Capsule(p, root, root + branchDir * 16f, 5f));
                    }
                }

                return Mathf.Min(d, Hexagon(p - centre, 13f));
            };

            return new List<Layer>
            {
                new Layer { Shape = flake, Top = new Color(0.86f, 0.98f, 1f), Bottom = new Color(0.27f, 0.72f, 0.98f),
                    Outline = new Color(0.04f, 0.2f, 0.42f), OutlineWidth = 5f },
            };
        }

        // ------------------------------------------------------------------ raster

        private static void Write(string path, List<Layer> layers)
        {
            var pixels = new Color32[Size * Size];
            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    Color sum = Color.clear;
                    for (int sy = 0; sy < Samples; sy++)
                    {
                        for (int sx = 0; sx < Samples; sx++)
                        {
                            // Image space: y grows downward, so the shapes above read top-to-bottom.
                            var p = new Vector2(x + (sx + 0.5f) / Samples, (Size - 1 - y) + (sy + 0.5f) / Samples);
                            sum += Shade(p, layers);
                        }
                    }

                    Color c = sum / (Samples * Samples);
                    // Back to straight alpha.
                    Color straight = c.a > 0.0001f ? new Color(c.r / c.a, c.g / c.a, c.b / c.a, c.a) : Color.clear;
                    pixels[y * Size + x] = straight;
                }
            }

            var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
            texture.SetPixels32(pixels);
            texture.Apply();
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
        }

        /// <summary>Premultiplied colour of one sample: layers painted back to front, each as outline + fill, with a
        /// soft white highlight along the upper part of each fill.</summary>
        private static Color Shade(Vector2 p, List<Layer> layers)
        {
            Color result = Color.clear;
            foreach (Layer layer in layers)
            {
                float d = layer.Shape(p);
                if (d > layer.OutlineWidth)
                {
                    continue;
                }

                Color c;
                if (d > 0f)
                {
                    c = layer.Outline;
                }
                else
                {
                    float t = Mathf.InverseLerp(14f, 114f, p.y);
                    c = Color.Lerp(layer.Top, layer.Bottom, t);
                    // Highlight: lighter just inside the upper edge.
                    float inner = -d;
                    float rim = 1f - Mathf.Clamp01(inner / 7f);
                    float upper = Mathf.Clamp01(1f - t * 1.6f);
                    c = Color.Lerp(c, Color.white, rim * upper * 0.55f);
                }

                c.a = 1f;
                result = new Color(c.r, c.g, c.b, 1f); // opaque layers: the later one simply covers
            }

            return result;
        }

        private static void ImportAsSprite(string path)
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.SaveAndReimport();
        }

        // ------------------------------------------------------------------ distance functions

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

        private static float Hexagon(Vector2 p, float radius)
        {
            var k = new Vector3(-0.866025404f, 0.5f, 0.577350269f);
            p = new Vector2(Mathf.Abs(p.x), Mathf.Abs(p.y));
            float dot = Mathf.Min(k.x * p.x + k.y * p.y, 0f);
            p -= 2f * dot * new Vector2(k.x, k.y);
            p -= new Vector2(Mathf.Clamp(p.x, -k.z * radius, k.z * radius), radius);
            return p.magnitude * Mathf.Sign(p.y);
        }
    }
}
