using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace AlienDefense.EditorTools
{
    /// <summary>Per-level sky + environment lighting (user, 2026-10-11): each level gets its own skybox from the
    /// imported "Fantasy Skybox FREE" pack (through a project-owned material - the pack's own materials are never
    /// touched), a sun / trilight ambient / fog / post profile tuned to that sky, and a small reflection cubemap
    /// baked from the sky (so the UFO and towers reflect the same sky in a build without a lighting bake).
    /// Level 1 = fresh clear day, Level 2 = slightly golden day, Level 3 = bright tropical cyan.
    /// Everything stays static (no runtime sky code, no realtime GI, shadows stay as they are).
    /// The gameplay camera looks 50 degrees down, so the sky itself only shows in the boss intro / victory shots
    /// and past the map edges; its main job in gameplay is mood through sun, ambient, reflections and post.
    /// Safe to run again.</summary>
    public static class LevelSkySetup
    {
        private const string SkyRoot = "Assets/_Game/Art/Sky";
        private const string Pack = "Assets/Fantasy Skybox FREE";
        private const string VolumeDir = "Assets/_Game/Settings/Volumes";
        private const int ReflectionSize = 128;

        /// <summary>One sky + lighting look. Rotation NaN = turn the sky so its painted sun sits where the scene's
        /// directional light comes from (needs SunU, the sun's horizontal position in the panorama, 0..1).</summary>
        public sealed class Look
        {
            public string Name;
            public string Source;
            public Color Tint = new Color(0.5f, 0.5f, 0.5f, 0.5f);
            public float Exposure = 1f;
            public float Rotation = float.NaN;
            public float SunU = -1f;
            public Color SunColor;

            /// <summary>Same sun direction in every level (Level 1's), so sky, shading and readability follow one logic -
            /// a level rebuild (Level02Builder.SetupLighting) would otherwise bring back its own angle.</summary>
            public Vector3 SunEuler = new Vector3(50f, 325f, 0f);
            public float SunIntensity;
            public Color AmbientSky, AmbientEquator, AmbientGround;
            public float ReflectionIntensity = 0.7f;
            public float FogStart = 90f, FogEnd = 320f;

            /// <summary>The fog is the sky's horizon colour pulled this far towards FogTint (a pure sky horizon reads too
            /// saturated on distant terrain).</summary>
            public Color FogTint = Color.white;
            public float FogTintAmount = 0.35f;
        }

        public sealed class Level
        {
            public string Scene;
            public string Folder;
            public string Profile;
            public System.Action<VolumeProfile> Post; // null = keep the profile as it is
            public Look[] Candidates;
            public int Chosen;
        }

        // --------------------------------------------------------------------------- the three levels

        public static readonly Level[] Levels =
        {
            new Level
            {
                Scene = "Assets/_Game/Scenes/Levels/Level_01.unity",
                Folder = "Level01",
                Profile = VolumeDir + "/PP_Level01.asset",
                Post = null, // Level 1's profile is the reference look - unchanged
                Chosen = 0,
                Candidates = new[]
                {
                    Day("Sky_Level01_ClearDay", Pack + "/Cubemaps/Classic/FS000_Day_01.mat", 0f),
                    Day("Sky_Level01_ClearDay", Pack + "/Cubemaps/Classic/FS000_Day_06.mat", 0f),
                    DayPano("Sky_Level01_ClearDay", Pack + "/Panoramics/FS003/FS003_Day.mat", 0.76f),
                },
            },
            new Level
            {
                Scene = "Assets/_Game/Scenes/Levels/Level_02.unity",
                Folder = "Level02",
                Profile = VolumeDir + "/PP_Level02.asset",
                Post = WarmPost,
                Chosen = 0,
                Candidates = new[]
                {
                    Warm(DayPano("Sky_Level02_WarmDay", Pack + "/Panoramics/FS002/FS002_Day.mat", 0.51f)),
                    Warm(DayPano("Sky_Level02_WarmDay", Pack + "/Panoramics/FS003/FS003_Day.mat", 0.76f)),
                    Warm(new Look { Name = "Sky_Level02_WarmDay", Source = Pack + "/Panoramics/FS002/FS002_Sunrise.mat", Exposure = 1.15f, Rotation = 0f }),
                },
            },
            new Level
            {
                Scene = "Assets/_Game/Scenes/Levels/Level_03.unity",
                Folder = "Level03",
                Profile = VolumeDir + "/PP_Level03.asset",
                Post = TropicalPost,
                Chosen = 0,
                Candidates = new[]
                {
                    Tropical(Day("Sky_Level03_TropicalDay", Pack + "/Cubemaps/Classic/FS000_Day_02.mat", 0f)),
                    Tropical(DayPano("Sky_Level03_TropicalDay", Pack + "/Panoramics/FS017/FS017_Day.mat", 0.69f)),
                    Tropical(Day("Sky_Level03_TropicalDay", Pack + "/Cubemaps/Classic/FS000_Day_04.mat", 0f)),
                },
            },
        };

        /// <summary>Level 1 baseline: neutral, slightly warm sun, cool-blue sky ambient, fresh green bounce.</summary>
        private static Look Day(string name, string source, float rotation)
        {
            return new Look
            {
                Name = name,
                Source = source,
                Rotation = rotation,
                SunColor = new Color(1f, 0.95f, 0.87f),
                SunIntensity = 1.18f,
                AmbientSky = new Color(0.74f, 0.87f, 1f),
                AmbientEquator = new Color(0.6f, 0.72f, 0.78f),
                AmbientGround = new Color(0.34f, 0.43f, 0.37f),
                ReflectionIntensity = 0.7f,
            };
        }

        private static Look DayPano(string name, string source, float sunU)
        {
            Look look = Day(name, source, float.NaN);
            look.SunU = sunU;
            return look;
        }

        /// <summary>Level 2: about 15% warmer - golden sun, warm horizon tint on a still-blue sky, sandy bounce.</summary>
        private static Look Warm(Look look)
        {
            look.Tint = new Color(0.56f, 0.5f, 0.42f, 0.5f);
            look.SunColor = new Color(1f, 0.89f, 0.72f);
            look.SunIntensity = 1.22f;
            look.AmbientSky = new Color(0.78f, 0.83f, 0.92f);
            look.AmbientEquator = new Color(0.74f, 0.68f, 0.57f);
            look.AmbientGround = new Color(0.46f, 0.4f, 0.31f);
            look.ReflectionIntensity = 0.6f;
            look.FogTint = new Color(1f, 0.9f, 0.74f);
            look.FogTintAmount = 0.55f; // the warm-tinted cyan horizon alone reads green
            return look;
        }

        /// <summary>Level 3: clean bright summer light, cyan sky ambient, turquoise-green bounce.</summary>
        private static Look Tropical(Look look)
        {
            look.Exposure = 1.05f;
            look.SunColor = new Color(1f, 0.96f, 0.88f);
            look.SunIntensity = 1.24f;
            look.AmbientSky = new Color(0.68f, 0.9f, 1f);
            look.AmbientEquator = new Color(0.58f, 0.8f, 0.8f);
            look.AmbientGround = new Color(0.38f, 0.48f, 0.42f);
            look.ReflectionIntensity = 0.75f;
            return look;
        }

        private static void WarmPost(VolumeProfile profile)
        {
            ColorAdjustments color = Get<ColorAdjustments>(profile);
            color.contrast.Override(12f);
            color.saturation.Override(6f);
            WhiteBalance balance = Get<WhiteBalance>(profile);
            balance.temperature.Override(6f); // 9 tinted the green tractor beam yellow over the sand
            balance.tint.Override(0f);
        }

        private static void TropicalPost(VolumeProfile profile)
        {
            ColorAdjustments color = Get<ColorAdjustments>(profile);
            color.postExposure.Override(0.08f);
            color.contrast.Override(14f);
            color.saturation.Override(14f);
        }

        private static T Get<T>(VolumeProfile profile) where T : VolumeComponent
        {
            if (!profile.TryGet(out T component))
            {
                component = profile.Add<T>(true);
                component.name = typeof(T).Name;
                component.hideFlags = HideFlags.HideInInspector | HideFlags.HideInHierarchy;
                AssetDatabase.AddObjectToAsset(component, profile);
            }

            component.active = true;
            return component;
        }

        // --------------------------------------------------------------------------- menu

        [MenuItem("Tools/Tower Defense/Levels/Apply Level Skies + Environment Lighting")]
        public static void ApplyFromMenu()
        {
            Debug.Log(ApplyAll());
        }

        public static string ApplyAll()
        {
            if (EditorApplication.isPlaying)
            {
                return "[LevelSkySetup] Stop Play mode first.";
            }

            var report = new List<string>();
            for (int i = 0; i < Levels.Length; i++)
            {
                report.Add(ApplyLevel(i));
            }

            return "[LevelSkySetup] Done.\n" + string.Join("\n", report);
        }

        public static string ApplyLevel(int index)
        {
            Level level = Levels[index];
            Look look = level.Candidates[level.Chosen];
            Scene scene = EditorSceneManager.OpenScene(level.Scene, OpenSceneMode.Single);

            string folder = SkyRoot + "/" + level.Folder;
            EnsureFolder(folder);

            // Project-owned sky material (references the pack's texture; the pack's material is untouched).
            Light sun = FindSun();
            if (sun != null)
            {
                sun.transform.rotation = Quaternion.Euler(look.SunEuler);
                EditorUtility.SetDirty(sun.transform);
            }

            Material preview = BuildMaterial(look, sun);
            string materialPath = folder + "/" + look.Name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material == null || material.shader != preview.shader)
            {
                if (material != null)
                {
                    AssetDatabase.DeleteAsset(materialPath);
                }

                material = new Material(preview);
                AssetDatabase.CreateAsset(material, materialPath);
            }
            else
            {
                material.CopyPropertiesFromMaterial(preview);
                EditorUtility.SetDirty(material);
            }

            Object.DestroyImmediate(preview);

            // Small reflection cubemap rendered from that sky.
            string reflectionPath = folder + "/" + look.Name + "_Reflection.cubemap";
            Cubemap reflection = BakeReflection(material, ReflectionSize);
            AssetDatabase.DeleteAsset(reflectionPath);
            AssetDatabase.CreateAsset(reflection, reflectionPath);

            ApplyEnvironment(look, material, reflection, sun);

            // Own post profile per level (Level 2 / 3 are rebuilt from Level 1's and then nudged).
            VolumeProfile profile = EnsureProfile(level);
            Volume volume = Object.FindFirstObjectByType<Volume>(FindObjectsInactive.Include);
            if (volume != null && profile != null)
            {
                volume.sharedProfile = profile;
                volume.name = "GlobalVolume_" + level.Folder;
                EditorUtility.SetDirty(volume);
            }

            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            return scene.name + ": sky " + look.Source.Substring(look.Source.LastIndexOf('/') + 1) + " -> " + materialPath
                + " (exposure " + look.Exposure + ", tint " + look.Tint + ", rotation " + material.GetFloat("_Rotation").ToString("0") + "), sun "
                + look.SunColor + " x" + look.SunIntensity + ", fog " + RenderSettings.fogColor + " " + look.FogStart + "-" + look.FogEnd
                + ", reflection " + reflectionPath + " x" + look.ReflectionIntensity + ", post " + (profile != null ? profile.name : "-");
        }

        // --------------------------------------------------------------------------- shared by the menu and the preview harness

        /// <summary>In-memory copy of the pack material with this look's tint / exposure / rotation.</summary>
        public static Material BuildMaterial(Look look, Light sun)
        {
            var source = AssetDatabase.LoadAssetAtPath<Material>(look.Source);
            var material = new Material(source) { name = look.Name };
            material.SetColor("_Tint", look.Tint);
            material.SetFloat("_Exposure", look.Exposure);
            material.SetFloat("_Rotation", ResolveRotation(look, sun));
            return material;
        }

        /// <summary>Turns a panorama so its painted sun sits in the same compass direction the light comes from.
        /// Skybox/Panoramic samples u = 0.5 - atan2(d.z, d.x) / 2pi of the direction rotated by +rotation about Y.</summary>
        private static float ResolveRotation(Look look, Light sun)
        {
            if (!float.IsNaN(look.Rotation))
            {
                return look.Rotation;
            }

            if (look.SunU < 0f || sun == null)
            {
                return 0f;
            }

            Vector3 toSun = -sun.transform.forward;
            float sunAngle = Mathf.Atan2(toSun.z, toSun.x) * Mathf.Rad2Deg;
            float rotation = (0.5f - look.SunU) * 360f - sunAngle;
            return Mathf.Repeat(rotation, 360f);
        }

        /// <summary>Renders the sky (nothing else) into a small mip-mapped cubemap.</summary>
        public static Cubemap BakeReflection(Material sky, int size)
        {
            Material previous = RenderSettings.skybox;
            RenderSettings.skybox = sky;
            var go = new GameObject("SkyReflectionCamera") { hideFlags = HideFlags.HideAndDontSave };
            var camera = go.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.Skybox;
            camera.cullingMask = 0;
            camera.enabled = false;
            var cube = new Cubemap(size, TextureFormat.RGBA32, true) { name = sky.name + "_Reflection" };
            camera.RenderToCubemap(cube);
            cube.Apply(true, false);
            Object.DestroyImmediate(go);
            RenderSettings.skybox = previous;
            return cube;
        }

        public static void ApplyEnvironment(Look look, Material sky, Cubemap reflection, Light sun)
        {
            RenderSettings.skybox = sky;
            if (sun != null)
            {
                sun.color = look.SunColor;
                sun.intensity = look.SunIntensity;
                RenderSettings.sun = sun;
                EditorUtility.SetDirty(sun);
            }

            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = look.AmbientSky;
            RenderSettings.ambientEquatorColor = look.AmbientEquator;
            RenderSettings.ambientGroundColor = look.AmbientGround;
            RenderSettings.ambientIntensity = 1f;

            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Custom;
            RenderSettings.customReflectionTexture = reflection;
            RenderSettings.reflectionIntensity = look.ReflectionIntensity;

            // Fog only ever touches the far distance (the gameplay view ends ~40 m away); tint it with the sky's own
            // horizon so the boss intro / victory shots melt into the sky instead of a flat colour.
            Color horizon = Color.Lerp(Horizon(reflection), look.FogTint, look.FogTintAmount);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = horizon;
            RenderSettings.fogStartDistance = look.FogStart;
            RenderSettings.fogEndDistance = look.FogEnd;

            foreach (Camera camera in Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (camera.CompareTag("MainCamera"))
                {
                    camera.clearFlags = CameraClearFlags.Skybox;
                    camera.backgroundColor = horizon;
                    if (!Application.isPlaying)
                    {
                        EditorUtility.SetDirty(camera);
                    }
                }
            }
        }

        /// <summary>Average colour of the band just above the horizon on the four side faces.</summary>
        private static Color Horizon(Cubemap cube)
        {
            int size = cube.width;
            Color sum = Color.black;
            int count = 0;
            CubemapFace[] sides = { CubemapFace.PositiveX, CubemapFace.NegativeX, CubemapFace.PositiveZ, CubemapFace.NegativeZ };
            foreach (CubemapFace face in sides)
            {
                // Cubemap rows run top to bottom: the horizon is the middle row.
                for (int y = size / 2 - size / 8; y < size / 2; y++)
                {
                    for (int x = 0; x < size; x += 4)
                    {
                        sum += cube.GetPixel(face, x, y);
                        count++;
                    }
                }
            }

            Color horizon = sum / Mathf.Max(1, count);
            horizon.a = 1f;
            return horizon;
        }

        private static VolumeProfile EnsureProfile(Level level)
        {
            if (level.Post == null)
            {
                return AssetDatabase.LoadAssetAtPath<VolumeProfile>(level.Profile);
            }

            // Rebuilt from Level 1's profile each run so the three stay one art direction.
            AssetDatabase.DeleteAsset(level.Profile);
            AssetDatabase.CopyAsset(Levels[0].Profile, level.Profile);
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(level.Profile);
            level.Post(profile);
            foreach (VolumeComponent component in profile.components)
            {
                EditorUtility.SetDirty(component);
            }

            EditorUtility.SetDirty(profile);
            return profile;
        }

        private static Light FindSun()
        {
            foreach (Light light in Object.FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (light.type == LightType.Directional)
                {
                    return light;
                }
            }

            return null;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
