using System.IO;
using UnityEditor;
using UnityEngine;

namespace AlienDefense.EditorTools
{
    /// <summary>Removes the orange/yellow trim from the build-node base (the Tripo tower base model): writes a copy of
    /// its base-colour texture with those accents turned into neutral grey metal, makes a material from it, and remaps
    /// the model's material to it, so every BuildNode base in every scene picks it up. The source texture and model
    /// are left untouched - clearing the remap in the model's import settings brings the orange back.</summary>
    internal static class TowerBaseNeutralColorSetup
    {
        private const string ModelPath = "Assets/_Game/Models/Base/tripo_convert_7c586f8b-a725-4d3b-96d1-87e86cf80088.fbx";
        private const string SourceTexturePath = "Assets/_Game/Models/Base/tripo_convert_7c586f8b-a725-4d3b-96d1-87e86cf80088.fbm/towerbase_basecolor.JPEG";
        private const string NeutralTexturePath = "Assets/_Game/Models/Base/towerbase_basecolor_neutral.jpg";
        private const string MaterialPath = "Assets/_Game/Materials/Environment/MAT_TowerBase_Neutral.mat";
        private const int OutputSize = 2048; // the source imports at 2048 anyway

        // Hue window (degrees) of the orange -> yellow trim, and the saturation band that fades it in.
        private const float HueMin = 12f;
        private const float HueMax = 62f;
        private const float SaturationStart = 0.22f;
        private const float SaturationFull = 0.45f;

        [MenuItem("AlienDefense/Setup/Build Nodes/Remove Orange From Tower Base")]
        private static void Run()
        {
            Texture2D neutral = BuildNeutralTexture();
            if (neutral == null)
            {
                return;
            }

            Material material = BuildMaterial(neutral);
            RemapModelMaterial(material);
            Debug.Log("[TowerBaseNeutralColorSetup] Tower base trim is now neutral grey.");
        }

        private static Texture2D BuildNeutralTexture()
        {
            if (!File.Exists(SourceTexturePath))
            {
                Debug.LogError("[TowerBaseNeutralColorSetup] Missing " + SourceTexturePath);
                return null;
            }

            // Read the file itself (the imported copy is not readable) and keep its full resolution; the importer
            // below caps it at the same size the original imports at.
            var output = new Texture2D(2, 2, TextureFormat.RGB24, false);
            output.LoadImage(File.ReadAllBytes(SourceTexturePath));

            Color32[] pixels = output.GetPixels32();
            for (int i = 0; i < pixels.Length; i++)
            {
                Color32 c = pixels[i];
                Color.RGBToHSV(c, out float h, out float s, out float v);
                float hue = h * 360f;
                if (hue < HueMin || hue > HueMax || s < SaturationStart)
                {
                    continue;
                }

                // Same brightness pattern (bevels and edge lines stay), minus the colour: a cool light-grey metal
                // trim a little darker than the orange was, so it does not glare.
                float amount = Mathf.InverseLerp(SaturationStart, SaturationFull, s);
                float grey = v * 0.62f;
                var metal = new Color(grey * 0.97f, grey, grey * 1.04f);
                Color mixed = Color.Lerp(c, metal, amount);
                pixels[i] = mixed;
            }

            output.SetPixels32(pixels);
            output.Apply();
            File.WriteAllBytes(NeutralTexturePath, output.EncodeToJPG(92));
            Object.DestroyImmediate(output);

            AssetDatabase.ImportAsset(NeutralTexturePath);
            var importer = (TextureImporter)AssetImporter.GetAtPath(NeutralTexturePath);
            importer.textureType = TextureImporterType.Default;
            importer.sRGBTexture = true;
            importer.maxTextureSize = OutputSize;
            importer.mipmapEnabled = true;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(NeutralTexturePath);
        }

        private static Material BuildMaterial(Texture2D baseMap)
        {
            Material original = null;
            foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(ModelPath))
            {
                if (asset is Material m)
                {
                    original = m;
                    break;
                }
            }

            EditorFolderUtility.EnsureFolder(Path.GetDirectoryName(MaterialPath).Replace('\\', '/'));
            var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material == null)
            {
                material = original != null ? new Material(original) : new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(material, MaterialPath);
            }

            material.SetTexture("_BaseMap", baseMap);
            material.SetTexture("_MainTex", baseMap);
            EditorUtility.SetDirty(material);
            AssetDatabase.SaveAssets();
            return material;
        }

        private static void RemapModelMaterial(Material material)
        {
            var importer = (ModelImporter)AssetImporter.GetAtPath(ModelPath);
            foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(ModelPath))
            {
                if (asset is Material m)
                {
                    importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), m.name), material);
                }
            }

            importer.SaveAndReimport();
        }
    }
}
