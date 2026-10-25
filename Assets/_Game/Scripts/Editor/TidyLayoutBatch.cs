using System;
using System.IO;
using System.Text;
using AlienDefense.Common;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace AlienDefense.EditorTools
{
    /// <summary>One entry point that rebuilds the tidy plot layout of Level 2 and Level 3 in the right order (Level 3
    /// pickups; Level 2 full build, then the tools a Level 2 rebuild always needs again: occlusion markers, the
    /// no-shadow / static-batching pass and the level's sky) and writes a report plus check renders (a top-down
    /// overview and a few game-camera views per level) to Logs/TidyLayout. Usable from the menu or headless:
    /// Unity -batchmode -projectPath ... -executeMethod AlienDefense.EditorTools.TidyLayoutBatch.RunAndExit</summary>
    public static class TidyLayoutBatch
    {
        private const string Level02 = "Assets/_Game/Scenes/Levels/Level_02.unity";
        private const string Level03 = "Assets/_Game/Scenes/Levels/Level_03.unity";
        private const string OutDir = "Logs/TidyLayout"; // not Temp: Unity empties Temp on exit

        [MenuItem("Tools/Tower Defense/Levels/Rebuild Tidy Layout (Level 2 + 3)")]
        public static void RunFromMenu()
        {
            Debug.Log(Run(true, true));
        }

        public static void RunAndExit()
        {
            int code = 0;
            try
            {
                Run(true, true);
            }
            catch (Exception e)
            {
                Directory.CreateDirectory(OutDir);
                File.AppendAllText(OutDir + "/report.txt", "\nEXCEPTION: " + e);
                code = 1;
            }

            EditorApplication.Exit(code);
        }

        public static void RunLevel3AndExit()
        {
            int code = 0;
            try
            {
                Run(false, true);
            }
            catch (Exception e)
            {
                Directory.CreateDirectory(OutDir);
                File.AppendAllText(OutDir + "/report.txt", "\nEXCEPTION: " + e);
                code = 1;
            }

            EditorApplication.Exit(code);
        }

        public static void RendersOnlyAndExit()
        {
            try
            {
                Directory.CreateDirectory(OutDir);
                File.WriteAllText(OutDir + "/report.txt", LevelXpBudget.Report());
                Renders(Level02, "L2");
                Renders(Level03, "L3");
            }
            catch (Exception e)
            {
                File.AppendAllText(OutDir + "/report.txt", "\nEXCEPTION: " + e);
            }

            EditorApplication.Exit(0);
        }

        public static string Run(bool level2, bool level3)
        {
            Directory.CreateDirectory(OutDir);
            var report = new StringBuilder();
            LogType filter = Debug.unityLogger.filterLogType;
            Debug.unityLogger.filterLogType = LogType.Error; // the kit prefabs' OnValidate warnings would bury the report
            try
            {
                if (level3)
                {
                    report.AppendLine(StripRefusals(Level03Builder.RefreshPickups()));
                }

                if (level2)
                {
                    EditorSceneManager.OpenScene(Level02, OpenSceneMode.Single);
                    report.AppendLine(Level02Builder.Build(true));
                    Scene scene = EditorSceneManager.OpenScene(Level02, OpenSceneMode.Single);
                    report.AppendLine(OcclusionSetup.SetupScene(scene));
                    EditorSceneManager.MarkSceneDirty(scene);
                    EditorSceneManager.SaveScene(scene);
                    report.AppendLine(LevelLookLikeLevel01.Apply());
                    report.AppendLine(LevelSkySetup.ApplyLevel(1));
                }
            }
            finally
            {
                Debug.unityLogger.filterLogType = filter;
                File.WriteAllText(OutDir + "/report.txt", report.ToString());
            }

            if (level2)
            {
                Renders(Level02, "L2");
            }

            if (level3)
            {
                Renders(Level03, "L3");
            }

            return report.ToString();
        }

        private static string StripRefusals(string text)
        {
            var kept = new StringBuilder();
            foreach (string line in text.Split('\n'))
            {
                if (!line.Contains(" - "))
                {
                    kept.AppendLine(line);
                }
            }

            return kept.ToString();
        }

        // ------------------------------------------------------------------ check renders

        private static void Renders(string scenePath, string prefix)
        {
            EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            LevelBounds bounds = UnityEngine.Object.FindFirstObjectByType<LevelBounds>();
            if (bounds == null)
            {
                return;
            }

            bool fog = RenderSettings.fog;
            RenderSettings.fog = false;
            float half = Mathf.Max(bounds.Extents.x, bounds.Extents.y) + 2f;
            Shot(OutDir + "/" + prefix + "_overview.png", new Vector3(bounds.Center.x, 150f, bounds.Center.y), Quaternion.Euler(90f, 270f, 0f), 1600, 1600, half, false);
            RenderSettings.fog = fog;

            // Game-camera views (offset and angle of TopDownCameraController) on a 3 x 2 grid of spots across the map.
            int n = 0;
            for (int ix = 0; ix < 2; ix++)
            {
                for (int iz = 0; iz < 3; iz++)
                {
                    float x = bounds.Center.x + (ix == 0 ? -0.45f : 0.5f) * bounds.Extents.x;
                    float z = bounds.Center.y + (iz - 1) * 0.72f * bounds.Extents.y;
                    Shot(OutDir + "/" + prefix + "_view" + n + ".png", new Vector3(x + 15.7f, 2f + 3f + 18.3f, z), Quaternion.Euler(50f, 270f, 0f), 540, 1140, 0f, true);
                    n++;
                }
            }
        }

        private static void Shot(string path, Vector3 position, Quaternion rotation, int width, int height, float orthoHalf, bool post)
        {
            var go = new GameObject("TidyLayoutShot") { hideFlags = HideFlags.HideAndDontSave };
            go.transform.SetPositionAndRotation(position, rotation);
            var camera = go.AddComponent<Camera>();
            camera.farClipPlane = 400f;
            camera.nearClipPlane = 0.3f;
            camera.clearFlags = CameraClearFlags.Skybox;
            if (orthoHalf > 0f)
            {
                camera.orthographic = true;
                camera.orthographicSize = orthoHalf;
                camera.aspect = 1f;
            }
            else
            {
                camera.fieldOfView = 40f;
            }

            go.AddComponent<UniversalAdditionalCameraData>().renderPostProcessing = post;
            var rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            camera.targetTexture = rt;
            camera.Render();
            RenderTexture.active = rt;
            var texture = new Texture2D(width, height, TextureFormat.RGB24, false) { hideFlags = HideFlags.HideAndDontSave };
            texture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            texture.Apply();
            RenderTexture.active = null;
            File.WriteAllBytes(path, texture.EncodeToPNG());
            camera.targetTexture = null;
            UnityEngine.Object.DestroyImmediate(texture);
            UnityEngine.Object.DestroyImmediate(rt);
            UnityEngine.Object.DestroyImmediate(go);
        }
    }
}
