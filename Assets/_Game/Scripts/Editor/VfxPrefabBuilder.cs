using AlienDefense.Vfx;
using UnityEditor;
using UnityEngine;

namespace AlienDefense.EditorTools
{
    /// <summary>Builds (or reuses) simple placeholder ParticleSystem prefabs and VfxDefinition assets for Phase 10.
    /// Prototype-quality only; replace the particle prefabs with real art whenever it becomes available.</summary>
    internal static class VfxPrefabBuilder
    {
        private const string DataFolder = "Assets/_Game/Data/Vfx";
        private const string PrefabFolder = "Assets/_Game/Prefabs/Vfx";

        private struct VfxSpec
        {
            public string Id;
            public string Name;
            public Color Color;
            public float Lifetime;
            public float StartSize;
            public int BurstCount;

            public VfxSpec(string id, string name, Color color, float lifetime, float startSize, int burstCount)
            {
                Id = id;
                Name = name;
                Color = color;
                Lifetime = lifetime;
                StartSize = startSize;
                BurstCount = burstCount;
            }
        }

        private static readonly VfxSpec MuzzleSpec = new VfxSpec("vfx_muzzle", "Vfx_MuzzleFlash", new Color(1f, 0.85f, 0.3f), 0.15f, 0.3f, 6);
        private static readonly VfxSpec HitSpec = new VfxSpec("vfx_hit", "Vfx_ProjectileHit", new Color(1f, 0.6f, 0.2f), 0.25f, 0.35f, 10);
        private static readonly VfxSpec DefeatedSpec = new VfxSpec("vfx_defeated", "Vfx_EnemyDefeated", new Color(1f, 0.55f, 0.15f), 0.4f, 0.5f, 16);
        private static readonly VfxSpec BuildSpec = new VfxSpec("vfx_build", "Vfx_TowerBuild", new Color(0.3f, 0.85f, 0.4f), 0.4f, 0.6f, 14);
        private static readonly VfxSpec UpgradeSpec = new VfxSpec("vfx_upgrade", "Vfx_TowerUpgrade", new Color(0.3f, 0.6f, 0.95f), 0.4f, 0.6f, 14);
        private static readonly VfxSpec SellSpec = new VfxSpec("vfx_sell", "Vfx_TowerSell", new Color(0.9f, 0.75f, 0.2f), 0.35f, 0.55f, 12);

        public static VfxDefinition MuzzleFlash => CreateOrLoad(MuzzleSpec);
        public static VfxDefinition ProjectileHit => CreateOrLoad(HitSpec);
        public static VfxDefinition EnemyDefeated => CreateOrLoad(DefeatedSpec);
        public static VfxDefinition TowerBuild => CreateOrLoad(BuildSpec);
        public static VfxDefinition TowerUpgrade => CreateOrLoad(UpgradeSpec);
        public static VfxDefinition TowerSell => CreateOrLoad(SellSpec);

        [MenuItem("AlienDefense/Setup/9. Create Placeholder Vfx Prefabs And Definitions")]
        public static void CreateAll()
        {
            CreateOrLoad(MuzzleSpec);
            CreateOrLoad(HitSpec);
            CreateOrLoad(DefeatedSpec);
            CreateOrLoad(BuildSpec);
            CreateOrLoad(UpgradeSpec);
            CreateOrLoad(SellSpec);

            Debug.Log("[AlienDefense Setup] Placeholder Vfx prefabs and definitions ready. Replace with real art when available.");
        }

        private static VfxDefinition CreateOrLoad(VfxSpec spec)
        {
            EditorFolderUtility.EnsureFolder(DataFolder);
            EditorFolderUtility.EnsureFolder(PrefabFolder);

            string definitionPath = $"{DataFolder}/VfxDefinition_{spec.Name}.asset";
            var definition = AssetDatabase.LoadAssetAtPath<VfxDefinition>(definitionPath);
            if (definition != null)
            {
                return definition;
            }

            GameObject prefab = CreateOrLoadPrefab(spec);

            definition = ScriptableObject.CreateInstance<VfxDefinition>();
            AssetDatabase.CreateAsset(definition, definitionPath);

            var serialized = new SerializedObject(definition);
            serialized.FindProperty("_id").stringValue = spec.Id;
            serialized.FindProperty("_prefab").objectReferenceValue = prefab != null ? prefab.GetComponent<PooledVfx>() : null;
            serialized.FindProperty("_lifetime").floatValue = spec.Lifetime;
            serialized.FindProperty("_poolPrewarmCount").intValue = 2;
            serialized.FindProperty("_poolDefaultCapacity").intValue = 6;
            serialized.FindProperty("_poolMaximumSize").intValue = 24;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            AssetDatabase.SaveAssets();
            return definition;
        }

        private static GameObject CreateOrLoadPrefab(VfxSpec spec)
        {
            string prefabPath = $"{PrefabFolder}/{spec.Name}.prefab";

            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (existing != null)
            {
                return existing;
            }

            GameObject root = BuildHierarchy(spec);
            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            Object.DestroyImmediate(root);

            return AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        }

        private static GameObject BuildHierarchy(VfxSpec spec)
        {
            var root = new GameObject(spec.Name, typeof(ParticleSystem));

            var particleSystem = root.GetComponent<ParticleSystem>();
            ParticleSystem.MainModule main = particleSystem.main;
            main.loop = false;
            main.duration = spec.Lifetime;
            main.startLifetime = spec.Lifetime;
            main.startSpeed = 2f;
            main.startSize = spec.StartSize;
            main.startColor = spec.Color;
            main.playOnAwake = false;
            main.stopAction = ParticleSystemStopAction.None;

            ParticleSystem.EmissionModule emission = particleSystem.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)spec.BurstCount) });

            ParticleSystem.ShapeModule shape = particleSystem.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.15f;

            var renderer = root.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.sharedMaterial = EditorMaterialUtility.CreateOrLoadMaterial(
                "Mat_Vfx_" + spec.Name, "Universal Render Pipeline/Particles/Unlit", spec.Color);

            var pooledVfx = root.AddComponent<PooledVfx>();
            var serializedVfx = new SerializedObject(pooledVfx);
            SerializedProperty systemsProperty = serializedVfx.FindProperty("_particleSystems");
            systemsProperty.arraySize = 1;
            systemsProperty.GetArrayElementAtIndex(0).objectReferenceValue = particleSystem;
            serializedVfx.ApplyModifiedPropertiesWithoutUndo();

            return root;
        }
    }
}
