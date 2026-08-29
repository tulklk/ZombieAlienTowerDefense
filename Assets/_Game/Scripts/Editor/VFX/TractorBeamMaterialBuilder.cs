using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace AlienDefense.EditorTools
{
    /// <summary>Builds the 4 persistent Tractor Beam materials: URP Particles/Unlit, Transparent + Additive,
    /// Vertex-Color multiply enabled (reads the alpha gradient baked into TractorBeamMeshGenerator's meshes),
    /// no shadows, no PBR, no texture.</summary>
    internal static class TractorBeamMaterialBuilder
    {
        private const string MaterialFolder = "Assets/_Game/Art/Materials/VFX";
        private const string ParticleUnlitShaderName = "Universal Render Pipeline/Particles/Unlit";

        private const string OuterPath = MaterialFolder + "/MAT_TractorBeam_Outer.mat";
        private const string InnerPath = MaterialFolder + "/MAT_TractorBeam_Inner.mat";
        private const string GroundPath = MaterialFolder + "/MAT_TractorBeam_Ground.mat";
        private const string GlowPath = MaterialFolder + "/MAT_TractorBeam_Glow.mat";
        private const string RingPath = MaterialFolder + "/MAT_TractorBeam_Ring.mat";

        // Sci-fi cyan-blue palette; a hint of the UFO's green accent is intentionally left out of the beam core
        // to keep it reading as "energy beam" rather than matching the ship's hull tint.
        private static readonly Color OuterColor = new Color(0.15f, 0.55f, 1f, 0.16f);
        private static readonly Color InnerColor = new Color(0.66f, 0.91f, 1f, 0.28f);
        private static readonly Color GroundColor = new Color(0.27f, 0.72f, 1f, 0.35f);
        private static readonly Color GlowColor = new Color(0.85f, 0.97f, 1f, 0.9f);

        // Deliberately its own (dimmer, more cyan) material — reusing GlowColor here read as a stark white
        // outline instead of a soft accent ring.
        private static readonly Color RingColor = new Color(0.35f, 0.78f, 1f, 0.4f);

        public static Material CreateOrLoadOuter() => CreateOrLoad(OuterPath, OuterColor);
        public static Material CreateOrLoadInner() => CreateOrLoad(InnerPath, InnerColor);
        public static Material CreateOrLoadGround() => CreateOrLoad(GroundPath, GroundColor);
        public static Material CreateOrLoadGlow() => CreateOrLoad(GlowPath, GlowColor);
        public static Material CreateOrLoadRing() => CreateOrLoad(RingPath, RingColor);

        [MenuItem("AlienDefense/Setup/24. Create Tractor Beam VFX Materials")]
        public static void CreateAll()
        {
            EditorFolderUtility.EnsureFolder(MaterialFolder);
            CreateOrLoadOuter();
            CreateOrLoadInner();
            CreateOrLoadGround();
            CreateOrLoadGlow();
            CreateOrLoadRing();
            Debug.Log("[AlienDefense Setup] Tractor Beam VFX materials ready.");
        }

        private static Material CreateOrLoad(string path, Color color)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(FindShader());
                AssetDatabase.CreateAsset(material, path);
            }

            ConfigureAdditiveVertexColor(material, color);
            EditorUtility.SetDirty(material);
            AssetDatabase.SaveAssets();
            return material;
        }

        private static Shader FindShader()
        {
            Shader shader = Shader.Find(ParticleUnlitShaderName);
            if (shader != null)
            {
                return shader;
            }

            Debug.LogWarning($"[AlienDefense Setup] Shader '{ParticleUnlitShaderName}' not found; falling back to Universal Render Pipeline/Unlit. " +
                "Vertex Color gradients baked into the beam meshes will not be visible until this is fixed manually in the material.");
            return EditorMaterialUtility.FindShaderWithFallback("Universal Render Pipeline/Unlit");
        }

        /// <summary>Transparent + Additive + ZWrite Off + Cull Off + no shadows + Vertex Color Multiply.</summary>
        private static void ConfigureAdditiveVertexColor(Material material, Color color)
        {
            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", color);
            }
            else
            {
                material.color = color;
            }

            material.SetFloat("_Surface", 1f); // Transparent
            material.SetFloat("_Blend", 2f);   // Additive
            material.SetFloat("_ZWrite", 0f);
            material.SetFloat("_Cull", (float)CullMode.Off);
            material.SetFloat("_ColorMode", 0f); // Vertex Color: Multiply against Base Color (reads baked alpha)
            material.SetFloat("_SoftParticlesEnabled", 0f); // project has no camera depth texture
            material.SetFloat("_DistortionEnabled", 0f);
            material.SetShaderPassEnabled("ShadowCaster", false);
            material.renderQueue = (int)RenderQueue.Transparent;

            material.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            material.SetInt("_DstBlend", (int)BlendMode.One);
            material.DisableKeyword("_ALPHATEST_ON");
            material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            material.EnableKeyword("_ALPHABLEND_ON");
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        }
    }
}
