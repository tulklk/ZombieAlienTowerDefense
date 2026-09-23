using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

namespace AlienDefense.EditorTools
{
    /// <summary>Applies the project settings that a Galaxy Note10-class phone needs and that nothing in the scene can
    /// set for itself. Everything here was chosen from a profile of Level_01 with 100 enemies alive, and everything
    /// here is reversible by re-running with different values - no gameplay data is touched.
    ///
    /// What it changes and why:
    /// - Shadow cascades 2 -> 1 on the tiers the game actually ships with. Every cascade re-renders the shadow casters,
    ///   and the shadow pass was already 1,301k of the 2,121k triangles in the frame. The camera sits 24 m up and sees
    ///   about 30 m of ground, so the shadow distance drops from 50 m to 35 m as well: nothing that was visible stops
    ///   casting, and the single remaining cascade covers 35 m at 2048 (~3.4 cm per texel, finer than the old far
    ///   cascade).
    /// - GPU skinning, so the 100+ skinned zombies are skinned on the GPU instead of the main thread.
    /// - ASTC instead of the generic (ETC2) Android texture format, and a 1024 cap on the character/prop textures that
    ///   ship at 2048. At gameplay distance a zombie covers well under 200 px, so 2048 was never visible - this is
    ///   memory and bandwidth, not sharpness.
    /// - Animator culling on the enemy prefabs: their motion is script-driven, not root motion, so an enemy that is off
    ///   camera does not need its animator evaluated at all.</summary>
    internal static class MobileRenderSettingsSetup
    {
        /// <summary>The tiers the game ships on: the saved profile defaults to quality level 2 (Medium), and the
        /// project's own pipeline asset backs the top tier. The lower tiers already use one cascade.</summary>
        private static readonly string[] PipelineAssets =
        {
            "Assets/URPDefaultResources/Medium.asset",
            "Assets/_Game/Settings/AlienDefense_URP_Pipeline.asset",
        };

        private const int ShadowCascades = 1;
        private const float ShadowDistance = 35f;

        /// <summary>Folders whose 2048 textures are characters and props seen from 24 m.</summary>
        private static readonly string[] TextureFolders =
        {
            "Assets/_Game/Models",
        };

        private const int MobileTextureSize = 1024;

        [MenuItem("AlienDefense/Optimize/Apply Mobile Render Settings")]
        private static void Run()
        {
            var report = new StringBuilder("[MobileRenderSettingsSetup]");

            foreach (string path in PipelineAssets)
            {
                var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(path);
                if (pipeline == null)
                {
                    Debug.LogWarning($"[MobileRenderSettingsSetup] missing pipeline asset {path}");
                    continue;
                }

                var serialized = new SerializedObject(pipeline);
                SerializedProperty cascades = serialized.FindProperty("m_ShadowCascadeCount");
                SerializedProperty distance = serialized.FindProperty("m_ShadowDistance");
                report.AppendLine();
                report.Append($"  {System.IO.Path.GetFileName(path)}: cascades {cascades.intValue} -> {ShadowCascades}, " +
                    $"shadow distance {distance.floatValue} -> {ShadowDistance}");
                cascades.intValue = ShadowCascades;
                distance.floatValue = ShadowDistance;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(pipeline);
            }

            report.AppendLine();
            report.Append($"  gpuSkinning {PlayerSettings.gpuSkinning} -> true; " +
                $"Android texture compression {EditorUserBuildSettings.androidBuildSubtarget} -> ASTC");
            PlayerSettings.gpuSkinning = true;
            EditorUserBuildSettings.androidBuildSubtarget = MobileTextureSubtarget.ASTC;

            int textures = ApplyTextureBudget();
            int animators = ApplyAnimatorCulling();
            report.AppendLine();
            report.Append($"  {textures} textures capped at {MobileTextureSize} for Android, " +
                $"{animators} enemy animators set to cull when off camera.");

            AssetDatabase.SaveAssets();
            Debug.Log(report.ToString());
        }

        private static int ApplyTextureBudget()
        {
            int changed = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", TextureFolders))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                if (importer == null || texture == null || (texture.width <= MobileTextureSize && texture.height <= MobileTextureSize))
                {
                    continue;
                }

                TextureImporterPlatformSettings settings = importer.GetPlatformTextureSettings("Android");
                if (settings.overridden && settings.maxTextureSize == MobileTextureSize)
                {
                    continue;
                }

                settings.overridden = true;
                settings.maxTextureSize = MobileTextureSize;
                settings.format = TextureImporterFormat.ASTC_6x6;
                settings.textureCompression = TextureImporterCompression.Compressed;
                importer.SetPlatformTextureSettings(settings);
                importer.SaveAndReimport();
                changed++;
            }

            return changed;
        }

        /// <summary>CullCompletely rather than CullUpdateTransforms: nothing in the game reads an off-camera enemy's
        /// bone transforms (movement, targeting and damage all work off the root), so the animator can stop entirely.</summary>
        private static int ApplyAnimatorCulling()
        {
            int changed = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/_Game/Prefabs" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null || prefab.GetComponentInChildren<AlienDefense.Enemies.EnemyController>(true) == null)
                {
                    continue;
                }

                GameObject contents = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    var touched = new List<Animator>();
                    foreach (Animator animator in contents.GetComponentsInChildren<Animator>(true))
                    {
                        if (animator.cullingMode == AnimatorCullingMode.CullCompletely)
                        {
                            continue;
                        }

                        animator.cullingMode = AnimatorCullingMode.CullCompletely;
                        touched.Add(animator);
                    }

                    if (touched.Count > 0)
                    {
                        PrefabUtility.SaveAsPrefabAsset(contents, path);
                        changed += touched.Count;
                    }
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(contents);
                }
            }

            return changed;
        }
    }
}
