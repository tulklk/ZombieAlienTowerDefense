using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AlienDefense.EditorTools
{
    /// <summary>Visual QA helpers for building Level_02: a contact sheet of prefabs (picked from a text list) and
    /// gameplay-camera captures at arbitrary focus points, rendered with the level's own Main Camera and its real
    /// TopDownCameraController offset, so what is checked is what the player sees.</summary>
    public static class Level02PreviewTools
    {
        /// <summary>Renders every prefab listed in <paramref name="listFile"/> (first line = output PNG path, then one
        /// asset path per line) into a grid, isolated in a preview scene. Returns one info line per prefab.</summary>
        public static string ContactSheet(string listFile, int cell, int columns)
        {
            string[] lines = File.ReadAllLines(listFile);
            string outFile = lines[0].Trim();
            var list = new List<string>();
            for (int i = 1; i < lines.Length; i++)
            {
                if (lines[i].Trim().Length > 0)
                {
                    list.Add(lines[i].Trim());
                }
            }

            int rows = (list.Count + columns - 1) / columns;
            var sheet = new Texture2D(columns * cell, rows * cell, TextureFormat.RGB24, false);
            Scene scene = EditorSceneManager.NewPreviewScene();
            var camGo = new GameObject("PreviewCamera");
            SceneManager.MoveGameObjectToScene(camGo, scene);
            var cam = camGo.AddComponent<Camera>();
            cam.scene = scene;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.55f, 0.6f, 0.65f);
            cam.fieldOfView = 30f;
            cam.farClipPlane = 2000f;
            var lightGo = new GameObject("PreviewLight");
            SceneManager.MoveGameObjectToScene(lightGo, scene);
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.2f;
            lightGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            var rt = new RenderTexture(cell, cell, 24);
            cam.targetTexture = rt;
            var tex = new Texture2D(cell, cell, TextureFormat.RGB24, false);
            var info = new System.Text.StringBuilder();

            for (int i = 0; i < list.Count; i++)
            {
                var asset = AssetDatabase.LoadAssetAtPath<GameObject>(list[i]);
                string label = Path.GetFileNameWithoutExtension(list[i]);
                if (asset == null)
                {
                    info.AppendLine(i + " MISSING " + list[i]);
                    continue;
                }

                var go = Object.Instantiate(asset);
                SceneManager.MoveGameObjectToScene(go, scene);
                go.transform.position = Vector3.zero;
                if (!TryBounds(go, out Bounds bounds, out int tris))
                {
                    info.AppendLine(i + " " + label + " (no renderers)");
                    Object.DestroyImmediate(go);
                    continue;
                }

                var rotation = Quaternion.Euler(30f, 45f, 0f);
                cam.transform.rotation = rotation;
                cam.transform.position = bounds.center + rotation * Vector3.back * (bounds.extents.magnitude / Mathf.Sin(15f * Mathf.Deg2Rad));
                cam.Render();
                RenderTexture.active = rt;
                tex.ReadPixels(new Rect(0, 0, cell, cell), 0, 0);
                tex.Apply();
                RenderTexture.active = null;
                sheet.SetPixels(i % columns * cell, (rows - 1 - i / columns) * cell, cell, cell, tex.GetPixels());
                info.AppendLine(i + " " + label + " size=" + bounds.size.ToString("F1") + " tris=" + tris);
                Object.DestroyImmediate(go);
            }

            sheet.Apply();
            File.WriteAllBytes(outFile, sheet.EncodeToPNG());
            cam.targetTexture = null;
            Object.DestroyImmediate(rt);
            Object.DestroyImmediate(tex);
            Object.DestroyImmediate(sheet);
            EditorSceneManager.ClosePreviewScene(scene);
            return info.ToString();
        }

        /// <summary>Renders the open level's Main Camera placed exactly as the TopDownCameraController would place it
        /// for each focus point (ground height sampled from the Terrain), at phone portrait resolution.</summary>
        public static string CaptureGameplayViews(string outPrefix, Vector2[] focusPoints, int width = 540, int height = 1140)
        {
            var controller = Object.FindFirstObjectByType<AlienDefense.CameraSystem.TopDownCameraController>();
            if (controller == null)
            {
                return "No TopDownCameraController in the open scene.";
            }

            var so = new SerializedObject(controller);
            Vector3 offset = so.FindProperty("_positionOffset").vector3Value;
            var camTransform = (Transform)so.FindProperty("_cameraTransform").objectReferenceValue;
            var cam = camTransform.GetComponent<Camera>();
            Vector3 oldPosition = camTransform.position;
            Quaternion oldRotation = camTransform.rotation;
            var log = new System.Text.StringBuilder();

            for (int i = 0; i < focusPoints.Length; i++)
            {
                Vector3 focus = new Vector3(focusPoints[i].x, GroundHeight(focusPoints[i]), focusPoints[i].y);
                camTransform.position = focus + offset;
                camTransform.LookAt(focus);
                var rt = new RenderTexture(width, height, 24);
                cam.targetTexture = rt;
                cam.aspect = (float)width / height;
                cam.Render();
                RenderTexture.active = rt;
                var tex = new Texture2D(width, height, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                tex.Apply();
                RenderTexture.active = null;
                cam.targetTexture = null;
                string file = outPrefix + i + ".png";
                File.WriteAllBytes(file, tex.EncodeToPNG());
                Object.DestroyImmediate(rt);
                Object.DestroyImmediate(tex);
                log.AppendLine(file + " focus=" + focus.ToString("F1"));
            }

            cam.ResetAspect();
            camTransform.SetPositionAndRotation(oldPosition, oldRotation);
            return log.ToString();
        }

        /// <summary>Straight-down orthographic overview of a world rectangle (minX, minZ, maxX, maxZ).</summary>
        public static void CaptureOverview(string outFile, Rect area, int pixelsPerUnit)
        {
            var go = new GameObject("Level02OverviewCamera");
            var cam = go.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = area.height * 0.5f;
            cam.aspect = area.width / area.height;
            cam.transform.position = new Vector3(area.center.x, 200f, area.center.y);
            cam.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            cam.farClipPlane = 400f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
            int w = Mathf.RoundToInt(area.width * pixelsPerUnit);
            int h = Mathf.RoundToInt(area.height * pixelsPerUnit);
            var rt = new RenderTexture(w, h, 24);
            cam.targetTexture = rt;
            bool fog = RenderSettings.fog;
            RenderSettings.fog = false; // the overview camera is 200 m up - fog would wash everything out
            cam.Render();
            RenderSettings.fog = fog;
            RenderTexture.active = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply();
            RenderTexture.active = null;
            File.WriteAllBytes(outFile, tex.EncodeToPNG());
            cam.targetTexture = null;
            Object.DestroyImmediate(rt);
            Object.DestroyImmediate(tex);
            Object.DestroyImmediate(go);
        }

        public static float GroundHeight(Vector2 xz)
        {
            foreach (Terrain terrain in Terrain.activeTerrains)
            {
                Vector3 origin = terrain.transform.position;
                Vector3 size = terrain.terrainData.size;
                if (xz.x >= origin.x && xz.x <= origin.x + size.x && xz.y >= origin.z && xz.y <= origin.z + size.z)
                {
                    return origin.y + terrain.SampleHeight(new Vector3(xz.x, 0f, xz.y));
                }
            }

            return 0f;
        }

        private static bool TryBounds(GameObject go, out Bounds bounds, out int tris)
        {
            bounds = default;
            tris = 0;
            bool any = false;
            foreach (Renderer renderer in go.GetComponentsInChildren<Renderer>())
            {
                if (renderer is ParticleSystemRenderer)
                {
                    continue;
                }

                if (!any)
                {
                    bounds = renderer.bounds;
                    any = true;
                }
                else
                {
                    bounds.Encapsulate(renderer.bounds);
                }

                Mesh mesh = renderer is SkinnedMeshRenderer skinned ? skinned.sharedMesh : renderer.GetComponent<MeshFilter>()?.sharedMesh;
                if (mesh != null)
                {
                    for (int s = 0; s < mesh.subMeshCount; s++)
                    {
                        tris += (int)mesh.GetIndexCount(s) / 3;
                    }
                }
            }

            return any;
        }
    }
}
