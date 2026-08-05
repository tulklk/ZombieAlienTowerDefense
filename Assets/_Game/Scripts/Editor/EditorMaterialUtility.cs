using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace AlienDefense.EditorTools
{
    /// <summary>Creates and self-heals persistent prototype material assets used by the Editor builders.</summary>
    internal static class EditorMaterialUtility
    {
        private const string MaterialsFolder = "Assets/_Game/Materials/Generated";

        /// <summary>Returns a persistent material asset by name, creating it if missing and repairing its shader if broken.</summary>
        public static Material CreateOrLoadMaterial(string materialName, string preferredShaderName, Color color)
        {
            EditorFolderUtility.EnsureFolder(MaterialsFolder);
            string path = $"{MaterialsFolder}/{materialName}.mat";

            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(FindShaderWithFallback(preferredShaderName)) { color = color };
                AssetDatabase.CreateAsset(material, path);
                AssetDatabase.SaveAssets();
                return material;
            }

            if (IsShaderBroken(material.shader))
            {
                material.shader = FindShaderWithFallback(preferredShaderName);
                material.color = color;
                EditorUtility.SetDirty(material);
                AssetDatabase.SaveAssets();
                Debug.Log($"[AlienDefense Setup] Repaired material '{materialName}' with a broken shader reference.");
            }

            return material;
        }

        public static Shader FindShaderWithFallback(string preferredShaderName)
        {
            Shader shader = Shader.Find(preferredShaderName);
            if (shader != null)
            {
                return shader;
            }

            Debug.LogWarning($"[AlienDefense Setup] Shader '{preferredShaderName}' not found (URP may not have been active yet); falling back to Universal Render Pipeline/Lit.");
            shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader != null)
            {
                return shader;
            }

            Debug.LogWarning("[AlienDefense Setup] Universal Render Pipeline/Lit not found either; falling back to built-in Standard.");
            shader = Shader.Find("Standard");
            if (shader == null)
            {
                Debug.LogError("[AlienDefense Setup] Could not find any usable shader. Generated materials will render magenta until URP is active.");
            }

            return shader;
        }

        /// <summary>True if a shader is missing, errored, or incompatible with the active render pipeline.</summary>
        public static bool IsShaderBroken(Shader shader)
        {
            if (shader == null || shader.name == "Hidden/InternalErrorShader")
            {
                return true;
            }

            bool urpActive = GraphicsSettings.currentRenderPipeline != null;
            bool isBuiltInFallbackShader = shader.name == "Standard" || shader.name == "Standard (Specular setup)";
            return urpActive && isBuiltInFallbackShader;
        }
    }
}
