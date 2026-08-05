using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace AlienDefense.EditorTools
{
    /// <summary>One-time Editor bootstrap: creates the URP pipeline asset and imports TMP essentials.</summary>
    internal static class ProjectSetupTool
    {
        private const string SettingsFolder = "Assets/_Game/Settings";
        private const string RendererDataPath = SettingsFolder + "/AlienDefense_URP_RendererData.asset";
        private const string PipelineAssetPath = SettingsFolder + "/AlienDefense_URP_Pipeline.asset";
        private const string UrpPackagePath = "Packages/com.unity.render-pipelines.universal";

        [MenuItem("AlienDefense/Setup/1. Configure Render Pipeline (URP)")]
        public static void ConfigureRenderPipeline()
        {
            if (AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelineAssetPath) != null)
            {
                Debug.Log("[AlienDefense Setup] URP Asset already exists at " + PipelineAssetPath + ". Skipping creation, re-assigning it as active pipeline.");
                var existing = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelineAssetPath);
                AssignAsActivePipeline(existing);
                return;
            }

            EditorFolderUtility.EnsureFolder(SettingsFolder);

            var rendererData = ScriptableObject.CreateInstance<UniversalRendererData>();
            AssetDatabase.CreateAsset(rendererData, RendererDataPath);
            ResourceReloader.ReloadAllNullIn(rendererData, UrpPackagePath);

            var pipelineAsset = UniversalRenderPipelineAsset.Create(rendererData);
            ApplyMobileStylizedDefaults(pipelineAsset);
            AssetDatabase.CreateAsset(pipelineAsset, PipelineAssetPath);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            AssignAsActivePipeline(pipelineAsset);

            Debug.Log("[AlienDefense Setup] Created " + PipelineAssetPath + " and assigned it as the active render pipeline.");
        }

        private static void AssignAsActivePipeline(UniversalRenderPipelineAsset pipelineAsset)
        {
            GraphicsSettings.defaultRenderPipeline = pipelineAsset;
            QualitySettings.renderPipeline = pipelineAsset;
        }

        private static void ApplyMobileStylizedDefaults(UniversalRenderPipelineAsset asset)
        {
            asset.supportsCameraDepthTexture = false;
            asset.supportsCameraOpaqueTexture = false;
            asset.supportsHDR = false;
            asset.renderScale = 1f;
            asset.msaaSampleCount = 4;
            asset.shadowDistance = 35f;
            asset.shadowCascadeCount = 1;
            asset.mainLightShadowmapResolution = 1024;
            asset.maxAdditionalLightsCount = 4;
            asset.useSRPBatcher = true;
        }

        [MenuItem("AlienDefense/Setup/2. Import TextMeshPro Essentials")]
        public static void ImportTmpEssentials()
        {
            TMP_PackageResourceImporter.ImportResources(importEssentials: true, importExamples: false, interactive: false);
            Debug.Log("[AlienDefense Setup] TextMeshPro Essential Resources imported into Assets/TextMesh Pro/.");
        }

        [MenuItem("AlienDefense/Setup/0. Run Full Setup (URP + TMP + Level_01)")]
        public static void RunFullSetup()
        {
            ConfigureRenderPipeline();
            ImportTmpEssentials();
            LevelSceneScaffolder.BuildLevel01SceneSkeleton();
            Debug.Log("[AlienDefense Setup] Full Phase 1 setup complete.");
        }
    }
}
