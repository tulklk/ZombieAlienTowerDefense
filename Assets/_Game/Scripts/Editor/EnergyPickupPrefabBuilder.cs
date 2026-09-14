using AlienDefense.Environment;
using AlienDefense.Pickups;
using UnityEditor;
using UnityEngine;

namespace AlienDefense.EditorTools
{
    /// <summary>Builds (or upgrades) the EnergyPickup prefab: Visual root with Core + GlowShell using
    /// AlienDefense energy-ball shaders. Does not touch collect / tractor gameplay scripts.</summary>
    internal static class EnergyPickupPrefabBuilder
    {
        private const string PrefabFolder = "Assets/_Game/Prefabs/Pickups";
        private const string PrefabPath = PrefabFolder + "/EnergyPickup.prefab";
        private const string OrbMeshPath = "Assets/_Game/Art/Meshes/Pickups/Mesh_EnergyOrb.asset";
        private const string CoreMaterialPath = "Assets/_Game/Materials/VFX/Energy/MAT_EnergyBall_Core.mat";
        private const string GlowMaterialPath = "Assets/_Game/Materials/VFX/Energy/MAT_EnergyBall_Glow.mat";
        private const string CoreShaderName = "AlienDefense/EnergyBallGlow";
        private const string GlowShaderName = "AlienDefense/EnergyBallGlowShell";

        private static readonly Color FallbackPickupColor = new Color(0.4f, 0.95f, 1f, 1f);

        [MenuItem("AlienDefense/Setup/32. Create Energy Pickup Prefab")]
        public static EnergyPickupController CreateOrLoadPrefab()
        {
            EditorFolderUtility.EnsureFolder(PrefabFolder);

            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (existing != null)
            {
                UpgradeEnergyPickupVisual();
                return existing.GetComponent<EnergyPickupController>();
            }

            GameObject root = BuildHierarchy();
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);

            GameObject saved = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Debug.Log("[AlienDefense Setup] Energy Pickup prefab ready at " + PrefabPath + ".");
            return saved != null ? saved.GetComponent<EnergyPickupController>() : null;
        }

        [MenuItem("AlienDefense/Setup/32a. Upgrade Energy Pickup Visual (Glow)")]
        public static void UpgradeEnergyPickupVisual()
        {
            EditorFolderUtility.EnsureFolder("Assets/_Game/Materials/VFX/Energy");
            EnsureEnergyMaterials();

            var prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefabAsset == null)
            {
                CreateOrLoadPrefab();
                return;
            }

            GameObject contents = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                ApplyGlowHierarchy(contents);
                PrefabUtility.SaveAsPrefabAsset(contents, PrefabPath);
                Debug.Log("[AlienDefense Setup] EnergyPickup visual upgraded to Core + GlowShell.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }

        private static GameObject BuildHierarchy()
        {
            EnsureEnergyMaterials();

            var root = new GameObject("EnergyPickup");
            var controller = root.AddComponent<EnergyPickupController>();
            var shake = root.AddComponent<TractorImmuneShake>();

            var visual = new GameObject("Visual");
            visual.transform.SetParent(root.transform, false);
            visual.transform.localScale = Vector3.one * 0.35f;

            BuildCoreAndShell(visual.transform);

            var serializedController = new SerializedObject(controller);
            serializedController.FindProperty("_visualRoot").objectReferenceValue = visual.transform;
            serializedController.ApplyModifiedPropertiesWithoutUndo();

            var serializedShake = new SerializedObject(shake);
            serializedShake.FindProperty("_visualRoot").objectReferenceValue = visual.transform;
            ConfigurePickupShake(serializedShake);

            return root;
        }

        /// <summary>Cargo-full refuse feedback. The ball is a sphere centred on its pivot, so the lean alone is
        /// invisible - the side-to-side sway is what actually reads as a shake.</summary>
        private static void ConfigurePickupShake(SerializedObject serializedShake)
        {
            serializedShake.FindProperty("_shakeAngle").floatValue = 6f;
            serializedShake.FindProperty("_shakeDuration").floatValue = 0.5f;
            serializedShake.FindProperty("_shakeCycles").floatValue = 3f;
            serializedShake.FindProperty("_swayDistance").floatValue = 0.07f;
            serializedShake.FindProperty("_cooldown").floatValue = 0.35f;
            serializedShake.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ApplyGlowHierarchy(GameObject root)
        {
            var controller = root.GetComponent<EnergyPickupController>();
            Transform visual = root.transform.Find("Visual");
            if (visual == null)
            {
                visual = new GameObject("Visual").transform;
                visual.SetParent(root.transform, false);
                visual.localScale = Vector3.one * 0.35f;
            }

            // Strip mesh components from Visual — it is a transform-only scale/spin root now.
            DestroyImmediateComponent<MeshCollider>(visual.gameObject);
            DestroyImmediateComponent<Collider>(visual.gameObject);
            DestroyImmediateComponent<MeshRenderer>(visual.gameObject);
            DestroyImmediateComponent<MeshFilter>(visual.gameObject);

            Transform core = visual.Find("Core");
            if (core == null)
            {
                core = new GameObject("Core").transform;
                core.SetParent(visual, false);
            }

            Transform shell = visual.Find("GlowShell");
            if (shell == null)
            {
                shell = new GameObject("GlowShell").transform;
                shell.SetParent(visual, false);
            }

            shell.localScale = Vector3.one * 1.16f;
            core.localScale = Vector3.one;
            core.localPosition = Vector3.zero;
            shell.localPosition = Vector3.zero;

            ConfigureMeshChild(core.gameObject, LoadCoreMaterial(), castShadows: false);
            ConfigureMeshChild(shell.gameObject, LoadGlowMaterial(), castShadows: false);

            TractorImmuneShake shake = root.GetComponent<TractorImmuneShake>();
            if (shake == null)
            {
                shake = root.AddComponent<TractorImmuneShake>();
            }

            var serializedShake = new SerializedObject(shake);
            serializedShake.FindProperty("_visualRoot").objectReferenceValue = visual;
            ConfigurePickupShake(serializedShake);

            if (controller != null)
            {
                var serializedController = new SerializedObject(controller);
                serializedController.FindProperty("_visualRoot").objectReferenceValue = visual;
                serializedController.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        private static void BuildCoreAndShell(Transform visual)
        {
            var core = new GameObject("Core");
            core.transform.SetParent(visual, false);
            ConfigureMeshChild(core, LoadCoreMaterial(), castShadows: false);

            var shell = new GameObject("GlowShell");
            shell.transform.SetParent(visual, false);
            shell.transform.localScale = Vector3.one * 1.16f;
            ConfigureMeshChild(shell, LoadGlowMaterial(), castShadows: false);
        }

        private static void ConfigureMeshChild(GameObject go, Material material, bool castShadows)
        {
            var filter = go.GetComponent<MeshFilter>();
            if (filter == null)
            {
                filter = go.AddComponent<MeshFilter>();
            }

            var renderer = go.GetComponent<MeshRenderer>();
            if (renderer == null)
            {
                renderer = go.AddComponent<MeshRenderer>();
            }

            Mesh orbMesh = AssetDatabase.LoadAssetAtPath<Mesh>(OrbMeshPath);
            if (orbMesh != null)
            {
                filter.sharedMesh = orbMesh;
            }
            else if (filter.sharedMesh == null)
            {
                var temp = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                filter.sharedMesh = temp.GetComponent<MeshFilter>().sharedMesh;
                Object.DestroyImmediate(temp);
            }

            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = castShadows
                ? UnityEngine.Rendering.ShadowCastingMode.On
                : UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            renderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;

            DestroyImmediateComponent<Collider>(go);
        }

        private static void EnsureEnergyMaterials()
        {
            EditorFolderUtility.EnsureFolder("Assets/_Game/Materials/VFX");
            EditorFolderUtility.EnsureFolder("Assets/_Game/Materials/VFX/Energy");

            if (AssetDatabase.LoadAssetAtPath<Material>(CoreMaterialPath) == null)
            {
                var mat = new Material(EditorMaterialUtility.FindShaderWithFallback(CoreShaderName));
                ApplyCoreDefaults(mat);
                AssetDatabase.CreateAsset(mat, CoreMaterialPath);
            }

            if (AssetDatabase.LoadAssetAtPath<Material>(GlowMaterialPath) == null)
            {
                var mat = new Material(EditorMaterialUtility.FindShaderWithFallback(GlowShaderName));
                ApplyGlowDefaults(mat);
                AssetDatabase.CreateAsset(mat, GlowMaterialPath);
            }

            AssetDatabase.SaveAssets();
        }

        private static Material LoadCoreMaterial()
        {
            EnsureEnergyMaterials();
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(CoreMaterialPath);
            if (mat == null || EditorMaterialUtility.IsShaderBroken(mat.shader))
            {
                mat = EditorMaterialUtility.CreateOrLoadMaterial("Mat_EnergyPickup", "Universal Render Pipeline/Unlit", FallbackPickupColor);
            }

            return mat;
        }

        private static Material LoadGlowMaterial()
        {
            EnsureEnergyMaterials();
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(GlowMaterialPath);
            return mat != null ? mat : LoadCoreMaterial();
        }

        private static void ApplyCoreDefaults(Material mat)
        {
            mat.SetColor("_BaseColor", new Color(0f, 0.455f, 0.851f, 1f));
            mat.SetColor("_CoreColor", new Color(0.333f, 1f, 1f, 1f));
            mat.SetColor("_RimColor", new Color(0.549f, 1f, 1f, 1f));
            mat.SetFloat("_EmissionStrength", 2f);
            mat.SetFloat("_RimPower", 2f);
            mat.SetFloat("_RimStrength", 1.4f);
            mat.SetFloat("_CoreIntensity", 1.3f);
            mat.SetFloat("_PulseSpeed", 2f);
            mat.SetFloat("_PulseAmount", 0.12f);
            mat.SetFloat("_NoiseScale", 3f);
            mat.SetFloat("_NoiseSpeed", 0.7f);
            mat.SetFloat("_NoiseStrength", 0.10f);
        }

        private static void ApplyGlowDefaults(Material mat)
        {
            mat.SetColor("_GlowColor", new Color(0.271f, 0.961f, 1f, 1f));
            mat.SetFloat("_GlowStrength", 1.6f);
            mat.SetFloat("_GlowAlpha", 0.28f);
            mat.SetFloat("_RimPower", 1.4f);
            mat.SetFloat("_PulseSpeed", 1.8f);
            mat.SetFloat("_PulseAmount", 0.08f);
        }

        private static void DestroyImmediateComponent<T>(GameObject go) where T : Component
        {
            T component = go.GetComponent<T>();
            if (component != null)
            {
                Object.DestroyImmediate(component);
            }
        }
    }
}
