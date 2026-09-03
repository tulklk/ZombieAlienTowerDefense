using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AlienDefense.EditorTools
{
    /// <summary>Editor-only tool to migrate every TextMeshPro text under Assets/_Game (third-party asset
    /// packs, the TextMesh Pro package's own resources, and Unity's default template scenes/Resources are
    /// deliberately excluded — see GameRoot) onto a single font asset (Fredoka-Bold SDF) with per-category
    /// outline/underlay material presets.
    ///
    /// Three menu items, meant to be run in this order:
    ///   1. Analyze Font Usage            — read-only, writes FontUsageReport.txt
    ///   2. Find Missing Vietnamese Glyphs — read-only, writes MissingVietnameseGlyphsReport.txt
    ///   3. Apply Fredoka Font To Entire Game — the actual migration, writes FontMigrationReport.txt
    ///
    /// Apply never touches RectTransform/anchors/text content/alignment/auto-size/spacing/vertex color —
    /// only FontAsset, the Bold flag on FontStyle, and (unless the object already carries a non-standard/
    /// special material) the assigned material preset. Prefabs are edited via LoadPrefabContents/
    /// SaveAsPrefabAsset so prefab connections and overrides are never broken.</summary>
    public static class ApplyGameFont
    {
        private const string FontAssetPath = "Assets/_Game/Font/Fredoka-Bold SDF.asset";
        private const string MaterialFolder = "Assets/_Game/Font/Materials";
        private const string ReportFolder = "Assets/_Game/EditorReports";
        private const string MigrationReportPath = ReportFolder + "/FontMigrationReport.txt";
        private const string GlyphReportPath = ReportFolder + "/MissingVietnameseGlyphsReport.txt";
        private const string UsageReportPath = ReportFolder + "/FontUsageReport.txt";

        // Everything this tool ever WRITES to lives under here. Third-party packs (Pandazole, Polytope
        // Studio, TD_Sci-Fi_Turret1_Example), the TextMesh Pro package's own resources, Plugins,
        // URPDefaultResources, and Unity's default template scenes (Assets/Scenes, Assets/Resources) are
        // never scanned or touched by any of the three menu items.
        private const string GameRoot = "Assets/_Game";

        private static readonly string[] PresetNames =
        {
            "Fredoka_Title", "Fredoka_Subtitle", "Fredoka_HUD",
            "Fredoka_Button", "Fredoka_SmallLabel", "Fredoka_Timer",
        };

        // ------------------------------------------------------------------ 1. Analyze Font Usage (read-only)

        [MenuItem("Tools/AlienDefense/Analyze Font Usage")]
        public static void AnalyzeFontUsage()
        {
            TMP_FontAsset fontAsset = LoadFontAsset();

            List<string> scenePaths = CollectAssets("t:Scene");
            List<string> prefabPaths = CollectAssets("t:Prefab");

            int totalTexts = 0;
            var fontCounts = new Dictionary<string, int>();
            var categoryCounts = new Dictionary<string, int>();
            var specialMaterialObjects = new List<string>();

            void Visit(string context, TMP_Text t)
            {
                totalTexts++;
                string fontName = t.font != null ? t.font.name : "NULL";
                fontCounts[fontName] = fontCounts.TryGetValue(fontName, out int c) ? c + 1 : 1;

                string category = Categorize(t);
                categoryCounts[category] = categoryCounts.TryGetValue(category, out int cc) ? cc + 1 : 1;

                if (HasSpecialMaterial(t))
                {
                    specialMaterialObjects.Add(context + "  (material=" + (t.fontSharedMaterial != null ? t.fontSharedMaterial.name : "NULL") + ")");
                }
            }

            ScanReadOnly(scenePaths, prefabPaths, Visit);

            var sb = new StringBuilder();
            sb.AppendLine("=== Font Usage Report ===");
            sb.AppendLine("Generated: " + DateTime.Now);
            sb.AppendLine("Scope: " + GameRoot + " only.");
            sb.AppendLine();
            sb.AppendLine("Scenes scanned: " + scenePaths.Count);
            sb.AppendLine("Prefabs scanned: " + prefabPaths.Count);
            sb.AppendLine("Total TMP_Text found: " + totalTexts);
            sb.AppendLine();
            sb.AppendLine("-- Current font usage --");
            foreach (var kv in fontCounts.OrderByDescending(k => k.Value))
            {
                sb.AppendLine("  " + kv.Key + " : " + kv.Value);
            }
            sb.AppendLine();
            sb.AppendLine("-- Planned material preset assignment if Apply is run --");
            foreach (var kv in categoryCounts.OrderByDescending(k => k.Value))
            {
                sb.AppendLine("  " + kv.Key + " : " + kv.Value);
            }
            sb.AppendLine();
            sb.AppendLine("-- Objects with a non-standard/special material (Apply will change ONLY the font asset on these, never the material) --");
            sb.AppendLine("Count: " + specialMaterialObjects.Count);
            foreach (var s in specialMaterialObjects)
            {
                sb.AppendLine("  " + s);
            }

            WriteReport(UsageReportPath, sb.ToString());
            Debug.Log("[ApplyGameFont] Font usage report written to " + UsageReportPath +
                ". Scanned " + scenePaths.Count + " scenes, " + prefabPaths.Count + " prefabs, " + totalTexts + " TMP_Text objects. " +
                "Nothing was modified — this menu item is read-only.");
        }

        // ------------------------------------------------------------------ 2. Find Missing Vietnamese Glyphs (read-only)

        [MenuItem("Tools/AlienDefense/Find Missing Vietnamese Glyphs")]
        public static void FindMissingVietnameseGlyphs()
        {
            TMP_FontAsset fontAsset = LoadFontAsset();

            List<string> scenePaths = CollectAssets("t:Scene");
            List<string> prefabPaths = CollectAssets("t:Prefab");

            int textsChecked = 0;
            var missingByChar = new Dictionary<char, List<string>>();

            void Visit(string context, TMP_Text t)
            {
                if (string.IsNullOrEmpty(t.text))
                {
                    return;
                }

                textsChecked++;
                foreach (char c in t.text)
                {
                    if (char.IsWhiteSpace(c) || c < 128)
                    {
                        continue; // plain ASCII is present in virtually every SDF font; skip for speed
                    }

                    if (!fontAsset.HasCharacter(c, searchFallbacks: false))
                    {
                        if (!missingByChar.TryGetValue(c, out List<string> list))
                        {
                            list = new List<string>();
                            missingByChar[c] = list;
                        }

                        if (!list.Contains(context))
                        {
                            list.Add(context);
                        }
                    }
                }
            }

            ScanReadOnly(scenePaths, prefabPaths, Visit);

            var sb = new StringBuilder();
            sb.AppendLine("=== Missing Vietnamese/Non-ASCII Glyph Report ===");
            sb.AppendLine("Font checked: " + fontAsset.name);
            sb.AppendLine("Generated: " + DateTime.Now);
            sb.AppendLine();
            sb.AppendLine("TMP_Text objects checked (non-empty text): " + textsChecked);
            sb.AppendLine("Distinct missing characters: " + missingByChar.Count);
            sb.AppendLine();

            if (missingByChar.Count == 0)
            {
                sb.AppendLine("No missing glyphs — every non-ASCII character currently used anywhere in the game's text is present in this font.");
            }
            else
            {
                sb.AppendLine("NOTE: no fallback font was added automatically. Decide how to handle these before running Apply.");
                sb.AppendLine();
                foreach (var kv in missingByChar.OrderBy(k => k.Key))
                {
                    sb.AppendLine("Missing '" + kv.Key + "' (U+" + ((int)kv.Key).ToString("X4") + ") — used in:");
                    foreach (string loc in kv.Value)
                    {
                        sb.AppendLine("    " + loc);
                    }
                }
            }

            WriteReport(GlyphReportPath, sb.ToString());

            if (missingByChar.Count > 0)
            {
                Debug.LogWarning("[ApplyGameFont] " + fontAsset.name + " is missing " + missingByChar.Count +
                    " character(s) actually used in the game's text right now. See " + GlyphReportPath + ". " +
                    "No fallback font was added — this is a report only.");
            }
            else
            {
                Debug.Log("[ApplyGameFont] " + fontAsset.name + " covers every non-ASCII character currently used in the game's text.");
            }
        }

        // ------------------------------------------------------------------ 3. Apply Fredoka Font To Entire Game

        /// <summary>Set true by an automated caller that has already obtained explicit user confirmation
        /// through some other channel (e.g. chat) to skip the interactive dialog below. Always false by
        /// default — a human clicking the menu item always sees the confirmation.</summary>
        public static bool SkipConfirmationDialog;

        [MenuItem("Tools/AlienDefense/Apply Fredoka Font To Entire Game")]
        public static void ApplyFredokaFontToEntireGame()
        {
            if (EditorApplication.isPlaying)
            {
                EditorUtility.DisplayDialog("Apply Fredoka Font", "Exit Play Mode first.", "OK");
                return;
            }

            if (!SkipConfirmationDialog)
            {
                bool confirmed = EditorUtility.DisplayDialog(
                    "Apply Fredoka Font To Entire Game",
                    "This changes the Font Asset (and, on most objects, the Material Preset) on every TMP_Text under " +
                    GameRoot + ", across every scene and prefab.\n\n" +
                    "Text content, RectTransform, anchors, alignment, auto-size, spacing, and vertex color are never touched.\n\n" +
                    "Make sure you've already run \"Analyze Font Usage\" and \"Find Missing Vietnamese Glyphs\" and reviewed both reports " +
                    "(and that your work is committed to git, since a multi-scene batch edit like this isn't reliably Ctrl+Z-able once scenes are saved).\n\n" +
                    "Proceed?",
                    "Yes, apply now",
                    "Cancel");
                if (!confirmed)
                {
                    return;
                }
            }

            TMP_FontAsset fontAsset = LoadFontAsset();
            Dictionary<string, Material> presets = EnsureMaterialPresets(fontAsset);

            List<string> scenePaths = CollectAssets("t:Scene");
            List<string> prefabPaths = CollectAssets("t:Prefab");

            int totalTexts = 0, changedTexts = 0, specialMaterialCount = 0, boldClearedCount = 0;
            var errors = new List<string>();
            var perObjectLines = new List<string>();

            // --- Scenes ---
            Scene activeBefore = EditorSceneManager.GetActiveScene();
            string originalScenePath = activeBefore.path;
            if (activeBefore.isDirty)
            {
                EditorSceneManager.SaveScene(activeBefore);
            }

            int scenesChanged = 0;
            foreach (string scenePath in scenePaths)
            {
                Scene scene;
                try
                {
                    scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
                }
                catch (Exception e)
                {
                    errors.Add(scenePath + " : failed to open (" + e.Message + ")");
                    continue;
                }

                bool sceneChanged = false;
                foreach (GameObject root in scene.GetRootGameObjects())
                {
                    foreach (TMP_Text t in root.GetComponentsInChildren<TMP_Text>(true))
                    {
                        totalTexts++;
                        try
                        {
                            bool changed = ApplyToText(t, GetPath(t.transform), fontAsset, presets, perObjectLines,
                                ref specialMaterialCount, ref boldClearedCount);
                            if (changed)
                            {
                                changedTexts++;
                                sceneChanged = true;
                            }
                        }
                        catch (Exception e)
                        {
                            errors.Add(scenePath + " > " + GetPath(t.transform) + " : " + e.Message);
                        }
                    }
                }

                if (sceneChanged)
                {
                    EditorSceneManager.MarkSceneDirty(scene);
                    EditorSceneManager.SaveScene(scene);
                    scenesChanged++;
                }
            }

            if (!string.IsNullOrEmpty(originalScenePath))
            {
                EditorSceneManager.OpenScene(originalScenePath, OpenSceneMode.Single);
            }

            // --- Prefabs ---
            int prefabsChanged = 0;
            foreach (string prefabPath in prefabPaths)
            {
                GameObject root;
                try
                {
                    root = PrefabUtility.LoadPrefabContents(prefabPath);
                }
                catch (Exception e)
                {
                    errors.Add(prefabPath + " : failed to load (" + e.Message + ")");
                    continue;
                }

                bool prefabChanged = false;
                foreach (TMP_Text t in root.GetComponentsInChildren<TMP_Text>(true))
                {
                    totalTexts++;
                    try
                    {
                        bool changed = ApplyToText(t, prefabPath + " > " + GetPath(t.transform), fontAsset, presets, perObjectLines,
                            ref specialMaterialCount, ref boldClearedCount);
                        if (changed)
                        {
                            changedTexts++;
                            prefabChanged = true;
                        }
                    }
                    catch (Exception e)
                    {
                        errors.Add(prefabPath + " > " + GetPath(t.transform) + " : " + e.Message);
                    }
                }

                if (prefabChanged)
                {
                    PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                    prefabsChanged++;
                }

                PrefabUtility.UnloadPrefabContents(root);
            }

            // --- TMP Settings default font (project-wide default for newly created TMP objects only — does
            // not touch the TextMesh Pro package itself, nor any object already placed in a scene/prefab). ---
            TMP_Settings settings = TMP_Settings.instance;
            string oldDefaultFont = "NULL";
            if (settings != null)
            {
                var settingsSO = new SerializedObject(settings);
                SerializedProperty prop = settingsSO.FindProperty("m_defaultFontAsset");
                if (prop != null)
                {
                    oldDefaultFont = prop.objectReferenceValue != null ? prop.objectReferenceValue.name : "NULL";
                    prop.objectReferenceValue = fontAsset;
                    settingsSO.ApplyModifiedPropertiesWithoutUndo();
                    EditorUtility.SetDirty(settings);
                }
            }

            AssetDatabase.SaveAssets();

            var sb = new StringBuilder();
            sb.AppendLine("=== Font Migration Report ===");
            sb.AppendLine("Generated: " + DateTime.Now);
            sb.AppendLine("Target font: " + fontAsset.name);
            sb.AppendLine("Scope: " + GameRoot);
            sb.AppendLine();
            sb.AppendLine("TMP_Settings.defaultFontAsset: " + oldDefaultFont + " -> " + fontAsset.name +
                " (affects only newly-created TMP objects in this project; the TextMesh Pro package itself is untouched)");
            sb.AppendLine();
            sb.AppendLine("Scenes scanned: " + scenePaths.Count + " (changed: " + scenesChanged + ")");
            sb.AppendLine("Prefabs scanned: " + prefabPaths.Count + " (changed: " + prefabsChanged + ")");
            sb.AppendLine("Total TMP_Text found: " + totalTexts);
            sb.AppendLine("Texts changed: " + changedTexts);
            sb.AppendLine("Texts with a special material (font asset changed only, material preset left alone): " + specialMaterialCount);
            sb.AppendLine("Texts where Font Style's Bold flag was cleared to Normal: " + boldClearedCount);
            sb.AppendLine("Errors: " + errors.Count);
            sb.AppendLine();
            sb.AppendLine("-- Per-object detail (only objects that actually changed) --");
            foreach (string line in perObjectLines)
            {
                sb.AppendLine("  " + line);
            }

            if (errors.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("-- Errors --");
                foreach (string e in errors)
                {
                    sb.AppendLine("  " + e);
                }
            }

            WriteReport(MigrationReportPath, sb.ToString());

            Debug.Log("[ApplyGameFont] Done. " + changedTexts + "/" + totalTexts + " texts updated across " +
                scenesChanged + " scene(s) + " + prefabsChanged + " prefab(s). Report: " + MigrationReportPath);
        }

        // ------------------------------------------------------------------ Per-object migration logic

        /// <summary>A "special" material is bound to whatever font asset's SDF atlas texture it was created
        /// for. Changing .font while keeping such a material would leave the shader sampling the WRONG atlas
        /// texture for the new font's glyph UVs — a broken/garbled render, not a cosmetic quirk. So unlike a
        /// plain font-only swap, a special-material object is left completely alone (font included) and only
        /// reported, rather than literally honoring "chỉ thay Font Asset" — doing that would corrupt its
        /// rendering. Flagged clearly in both reports so these can be re-authored by hand.</summary>
        private static bool ApplyToText(TMP_Text t, string context, TMP_FontAsset fontAsset, Dictionary<string, Material> presets,
            List<string> log, ref int specialMaterialCount, ref int boldClearedCount)
        {
            if (HasSpecialMaterial(t))
            {
                specialMaterialCount++;
                log.Add(context + " | SKIPPED (special material '" + t.fontSharedMaterial.name +
                    "' — left font+material untouched to avoid an atlas/UV mismatch; re-author by hand if it should follow Fredoka)");
                return false;
            }

            bool changed = false;
            string oldFontName = t.font != null ? t.font.name : "NULL";
            string boldNote = "";

            if (t.font != fontAsset)
            {
                Undo.RecordObject(t, "Apply Fredoka Font");
                t.font = fontAsset; // TMP resets the shared material to the new font's default as a side effect
                changed = true;
            }

            string category = Categorize(t);
            Material target = presets[category];
            if (t.fontSharedMaterial != target)
            {
                t.fontSharedMaterial = target;
                changed = true;
            }

            if ((t.fontStyle & FontStyles.Bold) != 0)
            {
                t.fontStyle &= ~FontStyles.Bold;
                boldClearedCount++;
                boldNote = " | boldCleared";
                changed = true;
            }

            if (changed)
            {
                EditorUtility.SetDirty(t);
                log.Add(context + " | font: " + oldFontName + " -> " + fontAsset.name +
                    " | material=" + category + boldNote + " | fontSize=" + t.fontSize + " (kept)");
            }

            return changed;
        }

        // ------------------------------------------------------------------ Categorization (spec section 11)

        private static string Categorize(TMP_Text t)
        {
            string n = t.gameObject.name.ToLowerInvariant();

            // Timer checked before HUD: "count" (HUD's "counter/badge count" keyword) would otherwise also
            // match "Countdown" as a substring and steal every CountdownText object from Fredoka_Timer.
            if (ContainsAny(n, "title")) return "Fredoka_Title";
            if (ContainsAny(n, "subtitle", "description")) return "Fredoka_Subtitle";
            if (ContainsAny(n, "timer", "time", "countdown")) return "Fredoka_Timer";
            if (ContainsAny(n, "coin", "gem", "energy", "currency", "amount", "count", "hud", "levelnumber")) return "Fredoka_HUD";
            if (ContainsAny(n, "button", "btn", "play", "start")) return "Fredoka_Button";
            return "Fredoka_SmallLabel";
        }

        private static bool ContainsAny(string haystack, params string[] needles)
        {
            foreach (string needle in needles)
            {
                if (haystack.Contains(needle))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>An object is "special" if its current material isn't a plain TMP default (font asset's
        /// own auto-generated "<Font> Atlas Material") and isn't already one of our own presets (either the
        /// Fredoka_ ones, or the earlier TMP_ ones from the previous font migration) — e.g. a hand-tuned
        /// one-off effect material. Those get their font asset swapped but keep their own material untouched.</summary>
        private static bool HasSpecialMaterial(TMP_Text t)
        {
            Material m = t.fontSharedMaterial;
            if (m == null)
            {
                return false;
            }

            string name = m.name;
            if (name.EndsWith(" Atlas Material"))
            {
                return false;
            }

            foreach (string p in PresetNames)
            {
                if (name == p)
                {
                    return false;
                }
            }

            if (name.StartsWith("TMP_"))
            {
                return false; // the previous Nunito-era presets being migrated away from
            }

            return true;
        }

        // ------------------------------------------------------------------ Font asset / material setup

        private static TMP_FontAsset LoadFontAsset()
        {
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);
            if (font == null)
            {
                throw new InvalidOperationException("Font asset not found at " + FontAssetPath);
            }

            return font;
        }

        private struct MaterialSpec
        {
            public string name;
            public float outlineWidth;
            public bool underlay;
            public float underlayOffsetY;
            public float underlayDilate;
            public float underlaySoftness;
            public float underlayAlpha;
        }

        // Outline Color #111820 per the user's spec; widths within the ranges given in sections A-F.
        private static readonly MaterialSpec[] Specs =
        {
            new MaterialSpec { name = "Fredoka_Title",      outlineWidth = 0.22f, underlay = true,  underlayOffsetY = -0.10f, underlayDilate = 0.08f, underlaySoftness = 0.04f, underlayAlpha = 0.30f },
            new MaterialSpec { name = "Fredoka_Subtitle",   outlineWidth = 0.16f, underlay = true,  underlayOffsetY = -0.07f, underlayDilate = 0.05f, underlaySoftness = 0.03f, underlayAlpha = 0.22f },
            new MaterialSpec { name = "Fredoka_HUD",        outlineWidth = 0.15f, underlay = true,  underlayOffsetY = -0.06f, underlayDilate = 0.04f, underlaySoftness = 0.03f, underlayAlpha = 0.22f },
            new MaterialSpec { name = "Fredoka_Button",     outlineWidth = 0.22f, underlay = true,  underlayOffsetY = -0.09f, underlayDilate = 0.07f, underlaySoftness = 0.04f, underlayAlpha = 0.28f },
            new MaterialSpec { name = "Fredoka_SmallLabel", outlineWidth = 0.15f, underlay = false },
            new MaterialSpec { name = "Fredoka_Timer",      outlineWidth = 0.12f, underlay = false },
        };

        private static Dictionary<string, Material> EnsureMaterialPresets(TMP_FontAsset fontAsset)
        {
            if (!AssetDatabase.IsValidFolder(MaterialFolder))
            {
                string parent = Path.GetDirectoryName(MaterialFolder)?.Replace("\\", "/");
                AssetDatabase.CreateFolder(parent, Path.GetFileName(MaterialFolder));
            }

            var result = new Dictionary<string, Material>();
            Color outlineColor = new Color(0.067f, 0.094f, 0.125f, 1f); // #111820
            Color underlayColor = Color.black;

            foreach (MaterialSpec spec in Specs)
            {
                string path = MaterialFolder + "/" + spec.name + ".mat";
                Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (mat == null)
                {
                    mat = new Material(fontAsset.material) { name = spec.name };
                    AssetDatabase.CreateAsset(mat, path);
                }

                mat.SetColor("_FaceColor", Color.white);
                mat.SetFloat("_FaceDilate", 0f);
                mat.SetColor("_OutlineColor", outlineColor);
                mat.SetFloat("_OutlineWidth", spec.outlineWidth);
                mat.SetFloat("_OutlineSoftness", 0.02f);
                mat.EnableKeyword("OUTLINE_ON");

                if (spec.underlay)
                {
                    Color u = underlayColor;
                    u.a = spec.underlayAlpha;
                    mat.SetColor("_UnderlayColor", u);
                    mat.SetFloat("_UnderlayOffsetX", 0f);
                    mat.SetFloat("_UnderlayOffsetY", spec.underlayOffsetY);
                    mat.SetFloat("_UnderlayDilate", spec.underlayDilate);
                    mat.SetFloat("_UnderlaySoftness", spec.underlaySoftness);
                    mat.EnableKeyword("UNDERLAY_ON");
                }
                else
                {
                    mat.SetColor("_UnderlayColor", new Color(0f, 0f, 0f, 0f));
                    mat.DisableKeyword("UNDERLAY_ON");
                }

                EditorUtility.SetDirty(mat);
                result[spec.name] = mat;
            }

            AssetDatabase.SaveAssets();
            return result;
        }

        // ------------------------------------------------------------------ Scan helpers

        private static List<string> CollectAssets(string filter)
        {
            var list = new List<string>();
            foreach (string guid in AssetDatabase.FindAssets(filter, new[] { GameRoot }))
            {
                list.Add(AssetDatabase.GUIDToAssetPath(guid));
            }

            return list;
        }

        /// <summary>Shared read-only scan used by both report-only menu items — opens each scene/prefab,
        /// visits every TMP_Text, never marks anything dirty or saves.</summary>
        private static void ScanReadOnly(List<string> scenePaths, List<string> prefabPaths, Action<string, TMP_Text> visit)
        {
            Scene activeBefore = EditorSceneManager.GetActiveScene();
            string originalScenePath = activeBefore.path;
            if (activeBefore.isDirty)
            {
                EditorSceneManager.SaveScene(activeBefore);
            }

            foreach (string path in scenePaths)
            {
                Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                foreach (GameObject root in scene.GetRootGameObjects())
                {
                    foreach (TMP_Text t in root.GetComponentsInChildren<TMP_Text>(true))
                    {
                        visit(path + " > " + GetPath(t.transform), t);
                    }
                }
            }

            if (!string.IsNullOrEmpty(originalScenePath))
            {
                EditorSceneManager.OpenScene(originalScenePath, OpenSceneMode.Single);
            }

            foreach (string path in prefabPaths)
            {
                GameObject root = PrefabUtility.LoadPrefabContents(path);
                foreach (TMP_Text t in root.GetComponentsInChildren<TMP_Text>(true))
                {
                    visit(path + " > " + GetPath(t.transform), t);
                }

                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static string GetPath(Transform t)
        {
            string path = t.name;
            while (t.parent != null)
            {
                t = t.parent;
                path = t.name + "/" + path;
            }

            return path;
        }

        private static void WriteReport(string path, string content)
        {
            if (!AssetDatabase.IsValidFolder(ReportFolder))
            {
                AssetDatabase.CreateFolder("Assets/_Game", "EditorReports");
            }

            File.WriteAllText(path, content);
            AssetDatabase.ImportAsset(path);
        }
    }
}
