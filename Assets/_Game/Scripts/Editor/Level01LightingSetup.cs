using System.Collections.Generic;
using System.IO;
using AlienDefense.Vfx;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace AlienDefense.EditorTools
{
    /// <summary>Level_01's lighting setup: warm sunlit farm, soft cool shadows, polished post-processing.
    ///
    /// Everything is applied from the values at the top of this file, so the look can be re-tuned and re-applied, and
    /// running it twice changes nothing the second time:
    /// - URP (Android = Medium asset, Editor = AlienDefense asset): main-light soft shadows, 2 cascades, HDR grading,
    ///   SSAO configured (inactive by default); the Editor renderer also gets the PostProcessData it was missing.
    /// - Scene: warm sun, gradient ambient (cool shadow side), faint distance fog, Global Volume with a level-only
    ///   profile (ACES, colour adjustments, bloom, vignette), post-processing on the gameplay camera, the lighting
    ///   intro, no shadows from small decoration.
    /// - Glow balance: energy balls kept from blowing out to white under ACES, tower emission capped so no tower
    ///   outshines the UFO (hues unchanged).
    ///
    /// The UFO and tractor beam keep their own authored colours - this tool does not touch them.</summary>
    internal static class Level01LightingSetup
    {
        private const string ScenePath = "Assets/_Game/Scenes/Levels/Level_01.unity";
        private const string ProfilePath = "Assets/_Game/Settings/Volumes/PP_Level01.asset";

        // ---- Sun / ambient
        private static readonly Vector3 SunRotation = new Vector3(50f, -35f, 0f);
        private static readonly Color SunColor = Hex("#FFF0D7");
        private const float SunIntensity = 1.15f;
        private const float SunShadowStrength = 0.78f;
        private static readonly Color AmbientSky = Hex("#BBDFFF");
        private static readonly Color AmbientEquator = Hex("#91B1C0");
        private static readonly Color AmbientGround = Hex("#526B61");
        private const float ReflectionIntensity = 0.7f;

        // ---- Fog (linear, only reaches the far background)
        private const bool FogEnabled = true;
        private static readonly Color FogColor = Hex("#BFDDF2");
        private const float FogStart = 60f;
        private const float FogEnd = 250f;

        // ---- Post
        private const float PostExposure = 0.05f;
        private const float Contrast = 10f;
        private const float Saturation = 0f;
        private const float BloomThreshold = 1.1f;
        private const float BloomIntensity = 0.25f;
        private const float BloomScatter = 0.6f;
        private const float VignetteIntensity = 0.06f;
        private const float VignetteSmoothness = 0.45f;
        // Measured at 1080x2280 with 30 zombies: +2.4 ms GPU (+38%) even half-res/4-sample on a desktop GPU, versus
        // ~0.1 ms for tonemapping + grading + bloom + vignette together. On a Note10-class GPU that is not worth it, so
        // the feature stays configured but inactive; flip this for high-end targets.
        private const bool SsaoEnabled = false;
        private const float SsaoIntensity = 0.5f;
        private const float SsaoRadius = 0.2f;

        // ---- URP shadows
        private const float ShadowDistance = 50f;
        private const int ShadowResolution = 2048;

        // ---- Glow balance
        private const float TowerEmissionMax = 1.9f;
        private const float SmallPropSize = 1.1f; // renderers smaller than this (bounds diagonal, m) cast no shadow

        [MenuItem("AlienDefense/Setup/Level 01/Apply Lighting")]
        private static void ApplyAll()
        {
            var report = new List<string>();
            ApplyPipeline(report);
            ApplyEnergyBalls(report);
            ApplyTowerEmission(report);

            var scene = EditorSceneManager.GetActiveScene();
            if (scene.path != ScenePath)
            {
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                {
                    return;
                }

                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            }

            VolumeProfile profile = ApplyProfile(report);
            Volume volume = ApplySceneLighting(profile, report);
            ApplyIntro(volume, report);
            ApplySmallPropShadows(report);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            string summary = string.Join("\n", report);
            File.WriteAllText("Temp/Level01LightingSetup_report.txt", summary);
            Debug.Log("[Level01LightingSetup]\n" + summary);
        }

        // ------------------------------------------------------------------------------------------------------------
        // URP
        // ------------------------------------------------------------------------------------------------------------

        private static void ApplyPipeline(List<string> report)
        {
            var defaultRenderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>("Assets/URPDefaultResources/Default_Forward_Renderer.asset");
            var gameRenderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>("Assets/_Game/Settings/AlienDefense_URP_RendererData.asset");
            if (gameRenderer != null && gameRenderer.postProcessData == null && defaultRenderer != null)
            {
                gameRenderer.postProcessData = defaultRenderer.postProcessData;
                EditorUtility.SetDirty(gameRenderer);
            }

            // Medium = Android default, AlienDefense = Editor/Standalone (Ultra). Soft shadow quality: 1 Low, 2 Medium.
            ConfigurePipeline("Assets/URPDefaultResources/Medium.asset", 1, report);
            ConfigurePipeline("Assets/_Game/Settings/AlienDefense_URP_Pipeline.asset", 2, report);
            ConfigureSsao(defaultRenderer, report);
            ConfigureSsao(gameRenderer, report);
        }

        private static void ConfigurePipeline(string path, int softShadowQuality, List<string> report)
        {
            var asset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(path);
            if (asset == null)
            {
                report.Add("missing pipeline " + path);
                return;
            }

            asset.shadowDistance = ShadowDistance;
            asset.shadowCascadeCount = 2;
            asset.mainLightShadowmapResolution = ShadowResolution;
            asset.colorGradingMode = ColorGradingMode.HighDynamicRange;
            asset.supportsHDR = true;
            var so = new SerializedObject(asset);
            so.FindProperty("m_MainLightShadowsSupported").boolValue = true;
            so.FindProperty("m_AdditionalLightShadowsSupported").boolValue = false;
            so.FindProperty("m_SoftShadowsSupported").boolValue = true;
            so.FindProperty("m_SoftShadowQuality").intValue = softShadowQuality;
            so.FindProperty("m_Cascade2Split").floatValue = 0.3f;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(asset);
            report.Add($"{asset.name}: shadows {ShadowDistance} m, 2 cascades, {ShadowResolution}, soft q{softShadowQuality}, HDR grading");
        }

        private static void ConfigureSsao(UniversalRendererData renderer, List<string> report)
        {
            if (renderer == null)
            {
                return;
            }

            ScriptableRendererFeature ssao = null;
            foreach (ScriptableRendererFeature feature in renderer.rendererFeatures)
            {
                if (feature != null && feature.GetType().Name == "ScreenSpaceAmbientOcclusion")
                {
                    ssao = feature;
                }
            }

            if (ssao == null)
            {
                ssao = (ScriptableRendererFeature)ScriptableObject.CreateInstance("ScreenSpaceAmbientOcclusion");
                ssao.name = "ScreenSpaceAmbientOcclusion";
                AssetDatabase.AddObjectToAsset(ssao, renderer);
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(ssao, out string _, out long localId);
                var rendererSo = new SerializedObject(renderer);
                SerializedProperty features = rendererSo.FindProperty("m_RendererFeatures");
                SerializedProperty map = rendererSo.FindProperty("m_RendererFeatureMap");
                features.arraySize++;
                features.GetArrayElementAtIndex(features.arraySize - 1).objectReferenceValue = ssao;
                map.arraySize++;
                map.GetArrayElementAtIndex(map.arraySize - 1).longValue = localId;
                rendererSo.ApplyModifiedPropertiesWithoutUndo();
            }

            // Mobile-weight: half resolution, depth-only (no DepthNormals pass), 4 samples, cheap blur.
            var so = new SerializedObject(ssao);
            SetEnum(so, "m_Settings.AOMethod", "InterleavedGradient");
            SetEnum(so, "m_Settings.Source", "Depth");
            SetEnum(so, "m_Settings.NormalSamples", "Low");
            SetEnum(so, "m_Settings.Samples", "Low");
            SetEnum(so, "m_Settings.BlurQuality", "Low");
            so.FindProperty("m_Settings.Downsample").boolValue = true;
            so.FindProperty("m_Settings.AfterOpaque").boolValue = false;
            so.FindProperty("m_Settings.Intensity").floatValue = SsaoIntensity;
            so.FindProperty("m_Settings.DirectLightingStrength").floatValue = 0.2f;
            so.FindProperty("m_Settings.Radius").floatValue = SsaoRadius;
            so.FindProperty("m_Settings.Falloff").floatValue = 60f;
            so.ApplyModifiedPropertiesWithoutUndo();
            ssao.SetActive(SsaoEnabled);
            EditorUtility.SetDirty(ssao);
            EditorUtility.SetDirty(renderer);
            report.Add($"{renderer.name}: SSAO {(SsaoEnabled ? "on" : "configured, inactive")} {SsaoIntensity}/{SsaoRadius} (half-res, depth, 4 samples)");
        }

        // ------------------------------------------------------------------------------------------------------------
        // Volume + scene lighting
        // ------------------------------------------------------------------------------------------------------------

        private static VolumeProfile ApplyProfile(List<string> report)
        {
            EnsureFolder(Path.GetDirectoryName(ProfilePath).Replace('\\', '/'));
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(ProfilePath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, ProfilePath);
            }

            GetComponent<Tonemapping>(profile).mode.Override(TonemappingMode.ACES);

            ColorAdjustments color = GetComponent<ColorAdjustments>(profile);
            color.postExposure.Override(PostExposure);
            color.contrast.Override(Contrast);
            color.saturation.Override(Saturation);

            Bloom bloom = GetComponent<Bloom>(profile);
            bloom.threshold.Override(BloomThreshold);
            bloom.intensity.Override(BloomIntensity);
            bloom.scatter.Override(BloomScatter);
            bloom.highQualityFiltering.Override(false);
            bloom.downscale.Override(BloomDownscaleMode.Half);
            bloom.maxIterations.Override(5);

            Vignette vignette = GetComponent<Vignette>(profile);
            vignette.intensity.Override(VignetteIntensity);
            vignette.smoothness.Override(VignetteSmoothness);
            vignette.color.Override(Color.black);
            vignette.rounded.Override(false);

            EditorUtility.SetDirty(profile);
            report.Add($"PP_Level01: ACES, exposure {PostExposure}, contrast {Contrast}, saturation {Saturation}, bloom {BloomThreshold}/{BloomIntensity}/{BloomScatter}, vignette {VignetteIntensity}/{VignetteSmoothness}");
            return profile;
        }

        private static T GetComponent<T>(VolumeProfile profile) where T : VolumeComponent
        {
            if (!profile.TryGet(out T component))
            {
                component = profile.Add<T>();
                component.name = typeof(T).Name;
                AssetDatabase.AddObjectToAsset(component, profile);
            }

            component.active = true;
            return component;
        }

        private static Volume ApplySceneLighting(VolumeProfile profile, List<string> report)
        {
            GameObject sunObject = GameObject.Find("Maps/ZombieRoad/Directional Light");
            Light sun = sunObject != null ? sunObject.GetComponent<Light>() : RenderSettings.sun;
            if (sun != null)
            {
                sun.transform.rotation = Quaternion.Euler(SunRotation);
                sun.useColorTemperature = false;
                sun.color = SunColor;
                sun.intensity = SunIntensity;
                sun.shadows = LightShadows.Soft;
                sun.shadowStrength = SunShadowStrength;
                RenderSettings.sun = sun;
                EditorUtility.SetDirty(sun);
                EditorUtility.SetDirty(sun.transform);
            }

            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = AmbientSky;
            RenderSettings.ambientEquatorColor = AmbientEquator;
            RenderSettings.ambientGroundColor = AmbientGround;
            RenderSettings.reflectionIntensity = ReflectionIntensity;
            RenderSettings.fog = FogEnabled;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = FogColor;
            RenderSettings.fogStartDistance = FogStart;
            RenderSettings.fogEndDistance = FogEnd;

            Transform lighting = FindOrCreate(GameObject.Find("Maps/Environment").transform, "Lighting");
            Transform volumeTransform = FindOrCreate(lighting, "GlobalVolume_Level01");
            volumeTransform.gameObject.layer = 0;
            var volume = volumeTransform.GetComponent<Volume>();
            if (volume == null)
            {
                volume = volumeTransform.gameObject.AddComponent<Volume>();
            }

            volume.isGlobal = true;
            volume.priority = 0f;
            volume.weight = 1f;
            volume.sharedProfile = profile;

            Camera camera = Camera.main;
            if (camera != null)
            {
                UniversalAdditionalCameraData data = camera.GetUniversalAdditionalCameraData();
                data.renderPostProcessing = true;
                data.renderShadows = true;
                data.volumeLayerMask = 1; // Default - the volume's layer
                EditorUtility.SetDirty(data);
            }

            report.Add($"Sun {SunRotation} {ColorUtility.ToHtmlStringRGB(SunColor)} x{SunIntensity} shadow {SunShadowStrength}; ambient gradient; fog {FogEnabled} {FogStart}-{FogEnd}");
            return volume;
        }

        private static void ApplyIntro(Volume volume, List<string> report)
        {
            Transform lighting = FindOrCreate(GameObject.Find("Maps/Environment").transform, "Lighting");
            Transform host = FindOrCreate(lighting, "LevelLightingIntro");
            var intro = host.GetComponent<LevelLightingIntro>();
            if (intro == null)
            {
                intro = host.gameObject.AddComponent<LevelLightingIntro>();
            }

            var so = new SerializedObject(intro);
            so.FindProperty("_levelVolume").objectReferenceValue = volume;
            so.FindProperty("_ufoEmissiveRenderers").arraySize = 0; // exposure/bloom only; UFO materials stay as authored
            so.ApplyModifiedPropertiesWithoutUndo();
            report.Add("LevelLightingIntro wired (exposure + bloom)");
        }

        private static void ApplySmallPropShadows(List<string> report)
        {
            int turnedOff = 0;
            foreach (Renderer renderer in GameObject.Find("Maps").GetComponentsInChildren<Renderer>(true))
            {
                if (renderer is ParticleSystemRenderer || renderer.shadowCastingMode == ShadowCastingMode.Off || renderer is SkinnedMeshRenderer)
                {
                    continue;
                }

                if (renderer.bounds.size.magnitude < SmallPropSize)
                {
                    renderer.shadowCastingMode = ShadowCastingMode.Off;
                    EditorUtility.SetDirty(renderer);
                    turnedOff++;
                }
            }

            report.Add($"Small decoration shadows off: {turnedOff} renderers (< {SmallPropSize} m)");
        }

        // ------------------------------------------------------------------------------------------------------------
        // Glow balance
        // ------------------------------------------------------------------------------------------------------------

        private static void ApplyEnergyBalls(List<string> report)
        {
            // Bright cyan, but not blown out to white by ACES.
            var core = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Game/Materials/VFX/Energy/MAT_EnergyBall_Core.mat");
            if (core != null)
            {
                core.SetFloat("_EmissionStrength", 1.15f);
                core.SetFloat("_CoreIntensity", 0.85f);
                core.SetFloat("_RimStrength", 1.1f);
                EditorUtility.SetDirty(core);
            }

            var glow = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Game/Materials/VFX/Energy/MAT_EnergyBall_Glow.mat");
            if (glow != null)
            {
                glow.SetFloat("_GlowStrength", 1.0f);
                EditorUtility.SetDirty(glow);
            }

            report.Add("Energy ball: core emission 1.15, glow 1.0");
        }

        /// <summary>Caps tower emission intensity (it was x4.7-6.3, far above the bloom threshold) without changing
        /// each tower's colour.</summary>
        private static void ApplyTowerEmission(List<string> report)
        {
            var seen = new HashSet<Material>();
            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/_Game/Prefabs/Towers" }))
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
                foreach (Renderer renderer in prefab.GetComponentsInChildren<Renderer>(true))
                {
                    foreach (Material material in renderer.sharedMaterials)
                    {
                        if (material == null || !seen.Add(material) || !material.IsKeywordEnabled("_EMISSION") || !material.HasProperty("_EmissionColor"))
                        {
                            continue;
                        }

                        Color emission = material.GetColor("_EmissionColor");
                        float intensity = Mathf.Max(emission.r, Mathf.Max(emission.g, emission.b));
                        if (intensity <= TowerEmissionMax)
                        {
                            continue;
                        }

                        material.SetColor("_EmissionColor", emission / intensity * TowerEmissionMax);
                        EditorUtility.SetDirty(material);
                        report.Add($"Tower {material.name}: emission x{intensity:F2} -> x{TowerEmissionMax}");
                    }
                }
            }
        }

        // ------------------------------------------------------------------------------------------------------------

        private static Transform FindOrCreate(Transform parent, string name)
        {
            Transform child = parent.Find(name);
            if (child == null)
            {
                child = new GameObject(name).transform;
                child.SetParent(parent, false);
            }

            return child;
        }

        private static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder))
            {
                return;
            }

            string parent = Path.GetDirectoryName(folder).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
        }

        private static void SetEnum(SerializedObject so, string path, string value)
        {
            SerializedProperty property = so.FindProperty(path);
            int index = System.Array.IndexOf(property.enumNames, value);
            if (index >= 0)
            {
                property.enumValueIndex = index;
            }
        }

        private static Color Hex(string hex)
        {
            ColorUtility.TryParseHtmlString(hex, out Color color);
            return color;
        }
    }
}
