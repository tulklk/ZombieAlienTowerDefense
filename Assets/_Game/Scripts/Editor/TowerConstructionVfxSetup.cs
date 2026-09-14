using System.Collections.Generic;
using System.IO;
using AlienDefense.Towers;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace AlienDefense.EditorTools
{
    /// <summary>Adds the bottom-up construction reveal to every Tower prefab: creates one
    /// AlienDefense/TowerBuildReveal copy per tower material (properties + keywords copied, originals untouched),
    /// a small additive spark particle system beside the model, and a configured TowerConstructionVFX on the root.
    /// Safe to re-run - existing build materials are refreshed from their originals, existing components reused.</summary>
    internal static class TowerConstructionVfxSetup
    {
        private const string ShaderPath = "Assets/_Game/Shaders/Towers/TowerBuildReveal.shader";
        private const string BuildMaterialFolder = "Assets/_Game/Materials/Towers/Build";
        private const string ParticleMaterialPath = "Assets/_Game/Materials/Towers/Build/MAT_TowerConstructionParticle.mat";
        private const string ParticleTexturePath = "Assets/_Game/Art/Textures/VFX/T_UFO_TakeoffGlow.png";
        private const string ParticleChildName = "PS_TowerConstruction";

        [MenuItem("AlienDefense/Setup/Towers/Setup Construction VFX")]
        public static void SetupAll()
        {
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(ShaderPath);
            if (shader == null)
            {
                Debug.LogError("[TowerConstructionVfxSetup] Missing shader at " + ShaderPath);
                return;
            }

            EditorFolderUtility.EnsureFolder(BuildMaterialFolder);
            Material particleMaterial = CreateOrUpdateParticleMaterial();

            int prefabs = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/_Game/Prefabs" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (asset == null || asset.GetComponent<TowerController>() == null)
                {
                    continue;
                }

                GameObject root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    if (SetupTower(root, shader, particleMaterial))
                    {
                        PrefabUtility.SaveAsPrefabAsset(root, path);
                        prefabs++;
                        Debug.Log("[TowerConstructionVfxSetup] Construction VFX ready on " + path);
                    }
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"[TowerConstructionVfxSetup] Done - {prefabs} tower prefab(s) configured.");
        }

        private static bool SetupTower(GameObject root, Shader shader, Material particleMaterial)
        {
            Transform visualRoot = FindVisualRoot(root.transform);
            if (visualRoot == null)
            {
                Debug.LogWarning("[TowerConstructionVfxSetup] No visual model found under " + root.name);
                return false;
            }

            // One build material per distinct original material, in every slot of every renderer.
            var pairs = new List<TowerConstructionVFX.MaterialPair>();
            var seen = new HashSet<Material>();
            foreach (Renderer renderer in visualRoot.GetComponentsInChildren<Renderer>(true))
            {
                if (!(renderer is MeshRenderer) && !(renderer is SkinnedMeshRenderer))
                {
                    continue;
                }

                foreach (Material original in renderer.sharedMaterials)
                {
                    if (original == null || !seen.Add(original))
                    {
                        continue;
                    }

                    pairs.Add(new TowerConstructionVFX.MaterialPair
                    {
                        Original = original,
                        Build = CreateOrUpdateBuildMaterial(original, shader)
                    });
                }
            }

            ParticleSystem particles = EnsureParticles(root.transform, particleMaterial);

            var vfx = root.GetComponent<TowerConstructionVFX>();
            if (vfx == null)
            {
                vfx = root.AddComponent<TowerConstructionVFX>();
            }

            var so = new SerializedObject(vfx);
            so.FindProperty("_visualRoot").objectReferenceValue = visualRoot;
            so.FindProperty("_constructionParticles").objectReferenceValue = particles;
            SerializedProperty pairsProp = so.FindProperty("_materialPairs");
            pairsProp.arraySize = pairs.Count;
            for (int i = 0; i < pairs.Count; i++)
            {
                SerializedProperty element = pairsProp.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("Original").objectReferenceValue = pairs[i].Original;
                element.FindPropertyRelative("Build").objectReferenceValue = pairs[i].Build;
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            return true;
        }

        /// <summary>The child that holds the tower's meshes: not the range indicator, not the spark particles,
        /// and the one with the most mesh renderers.</summary>
        private static Transform FindVisualRoot(Transform root)
        {
            Transform best = null;
            int bestCount = 0;
            for (int i = 0; i < root.childCount; i++)
            {
                Transform child = root.GetChild(i);
                if (child.name == ParticleChildName || child.GetComponent<RangeIndicator>() != null)
                {
                    continue;
                }

                int count = child.GetComponentsInChildren<MeshRenderer>(true).Length +
                            child.GetComponentsInChildren<SkinnedMeshRenderer>(true).Length;
                if (count > bestCount)
                {
                    best = child;
                    bestCount = count;
                }
            }

            return best;
        }

        private static Material CreateOrUpdateBuildMaterial(Material original, Shader shader)
        {
            string path = $"{BuildMaterialFolder}/{original.name}_Build.mat";
            var build = AssetDatabase.LoadAssetAtPath<Material>(path);
            bool created = build == null;
            if (created)
            {
                build = new Material(shader);
            }

            build.shader = shader;
            build.CopyPropertiesFromMaterial(original);
            build.shaderKeywords = original.shaderKeywords;
            build.enableInstancing = original.enableInstancing;
            build.renderQueue = (int)RenderQueue.AlphaTest;
            build.SetFloat("_BuildProgress", 1f);
            build.name = Path.GetFileNameWithoutExtension(path);

            if (created)
            {
                AssetDatabase.CreateAsset(build, path);
            }
            else
            {
                EditorUtility.SetDirty(build);
            }

            return build;
        }

        private static Material CreateOrUpdateParticleMaterial()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(ParticleMaterialPath);
            bool created = material == null;
            if (created)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
            }

            material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(ParticleTexturePath));
            material.SetColor("_BaseColor", new Color(0.91f, 1f, 1f, 1f)); // #E8FFFF core, tinted per particle
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", 2f); // additive
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)BlendMode.One);
            material.SetFloat("_ZWrite", 0f);
            material.SetFloat("_Cull", (float)CullMode.Off);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.DisableKeyword("_ALPHABLEND_ON");
            material.renderQueue = (int)RenderQueue.Transparent;

            if (created)
            {
                AssetDatabase.CreateAsset(material, ParticleMaterialPath);
            }
            else
            {
                EditorUtility.SetDirty(material);
            }

            return material;
        }

        private static ParticleSystem EnsureParticles(Transform root, Material material)
        {
            Transform existing = root.Find(ParticleChildName);
            GameObject go = existing != null ? existing.gameObject : new GameObject(ParticleChildName);
            go.transform.SetParent(root, false);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;

            var ps = go.GetComponent<ParticleSystem>();
            if (ps == null)
            {
                ps = go.AddComponent<ParticleSystem>();
            }

            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            ParticleSystem.MainModule main = ps.main;
            main.playOnAwake = false;
            main.loop = true;
            main.duration = 1f;
            main.maxParticles = 30;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.3f, 0.7f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.05f, 0.35f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.08f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.306f, 0.961f, 1f), new Color(0.459f, 1f, 0.859f)); // #4EF5FF / #75FFDB
            main.gravityModifier = -0.05f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;

            ParticleSystem.EmissionModule emission = ps.emission;
            emission.enabled = true;
            emission.rateOverTime = 34f;
            emission.rateOverDistance = 0f;
            emission.SetBursts(new ParticleSystem.Burst[0]);

            // A thin ring around the tower: it rides the construction line, so it reads as a scan ring.
            ParticleSystem.ShapeModule shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.6f;
            shape.radiusThickness = 0.15f;
            shape.arc = 360f;
            shape.rotation = new Vector3(-90f, 0f, 0f); // circle lies flat on XZ
            shape.randomDirectionAmount = 0.2f;

            ParticleSystem.ColorOverLifetimeModule color = ps.colorOverLifetime;
            color.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(new Color(0.91f, 1f, 1f), 0f), new GradientColorKey(new Color(0.306f, 0.961f, 1f), 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.15f), new GradientAlphaKey(0f, 1f) });
            color.color = new ParticleSystem.MinMaxGradient(gradient);

            ParticleSystem.SizeOverLifetimeModule size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.2f));

            var collision = ps.collision; collision.enabled = false;
            var lights = ps.lights; lights.enabled = false;
            var trails = ps.trails; trails.enabled = false;
            var noise = ps.noise; noise.enabled = false;

            var psr = go.GetComponent<ParticleSystemRenderer>();
            psr.renderMode = ParticleSystemRenderMode.Billboard;
            psr.sharedMaterial = material;
            psr.shadowCastingMode = ShadowCastingMode.Off;
            psr.receiveShadows = false;
            psr.lightProbeUsage = LightProbeUsage.Off;
            psr.reflectionProbeUsage = ReflectionProbeUsage.Off;
            psr.sortMode = ParticleSystemSortMode.None;

            return ps;
        }
    }
}
