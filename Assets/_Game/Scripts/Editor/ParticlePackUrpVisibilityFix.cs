using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace AlienDefense.EditorTools
{
    /// <summary>
    /// Particle Pack materials use Soft Particles / Distortion. Without URP Depth + Opaque textures
    /// those effects fade to nearly invisible. Also upgrades leftover Built-in particle shaders.
    /// </summary>
    public static class ParticlePackUrpVisibilityFix
    {
        private const string ParticlePackRoot = "Assets/UnityTechnologies/ParticlePack";
        private const string UrpParticlesUnlit = "Universal Render Pipeline/Particles/Unlit";

        [MenuItem("AlienDefense/Setup/Fix Particle Pack URP Visibility")]
        public static void Fix()
        {
            EnsurePipelineDepthAndOpaque();
            int upgraded = UpgradeBuiltInParticleMaterials();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log(
                $"[AlienDefense] Particle Pack URP visibility fix done. " +
                $"Enabled Depth+Opaque on AlienDefense URP. Upgraded {upgraded} Built-in particle material(s). " +
                "Re-enter Play Mode / focus the Game view to see the change.");
        }

        private static void EnsurePipelineDepthAndOpaque()
        {
            var pipeline = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (pipeline == null)
            {
                pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(
                    "Assets/_Game/Settings/AlienDefense_URP_Pipeline.asset");
            }

            if (pipeline == null)
            {
                Debug.LogWarning("[AlienDefense] Could not find AlienDefense URP pipeline asset.");
                return;
            }

            var so = new SerializedObject(pipeline);
            so.FindProperty("m_RequireDepthTexture").boolValue = true;
            so.FindProperty("m_RequireOpaqueTexture").boolValue = true;
            SerializedProperty hdr = so.FindProperty("m_SupportsHDR");
            if (hdr != null)
            {
                hdr.boolValue = true;
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(pipeline);
        }

        private static int UpgradeBuiltInParticleMaterials()
        {
            Shader urpUnlit = Shader.Find(UrpParticlesUnlit);
            if (urpUnlit == null)
            {
                Debug.LogWarning($"[AlienDefense] Shader '{UrpParticlesUnlit}' not found.");
                return 0;
            }

            string[] guids = AssetDatabase.FindAssets("t:Material", new[] { ParticlePackRoot });
            int upgraded = 0;
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material == null || material.shader == null)
                {
                    continue;
                }

                string shaderName = material.shader.name;
                bool isBuiltInParticle =
                    shaderName == "Particles/Additive" ||
                    shaderName == "Particles/Alpha Blended" ||
                    shaderName == "Particles/Multiply" ||
                    shaderName == "Legacy Shaders/Particles/Additive" ||
                    shaderName == "Legacy Shaders/Particles/Alpha Blended";

                if (!isBuiltInParticle)
                {
                    continue;
                }

                Texture mainTex = material.HasProperty("_MainTex") ? material.GetTexture("_MainTex") : null;
                Color tint = material.HasProperty("_TintColor")
                    ? material.GetColor("_TintColor")
                    : (material.HasProperty("_Color") ? material.GetColor("_Color") : Color.white);
                bool additive = shaderName.Contains("Additive");

                material.shader = urpUnlit;
                if (material.HasProperty("_BaseMap") && mainTex != null)
                {
                    material.SetTexture("_BaseMap", mainTex);
                }

                if (material.HasProperty("_MainTex") && mainTex != null)
                {
                    material.SetTexture("_MainTex", mainTex);
                }

                if (material.HasProperty("_BaseColor"))
                {
                    material.SetColor("_BaseColor", tint);
                }

                if (material.HasProperty("_Color"))
                {
                    material.SetColor("_Color", tint);
                }

                // Transparent surface; Additive uses Blend=2 (Additive), otherwise Alpha.
                if (material.HasProperty("_Surface"))
                {
                    material.SetFloat("_Surface", 1f);
                }

                if (material.HasProperty("_Blend"))
                {
                    material.SetFloat("_Blend", additive ? 2f : 0f);
                }

                if (material.HasProperty("_SrcBlend"))
                {
                    material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                }

                if (material.HasProperty("_DstBlend"))
                {
                    material.SetFloat("_DstBlend", additive
                        ? (float)BlendMode.One
                        : (float)BlendMode.OneMinusSrcAlpha);
                }

                if (material.HasProperty("_ZWrite"))
                {
                    material.SetFloat("_ZWrite", 0f);
                }

                material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                if (additive)
                {
                    material.EnableKeyword("_ALPHAMODULATE_ON");
                }

                material.renderQueue = 3000;
                EditorUtility.SetDirty(material);
                upgraded++;
                Debug.Log($"[AlienDefense] Upgraded Built-in particle material → URP: {path}");
            }

            return upgraded;
        }
    }
}
