using AlienDefense.Combat;
using UnityEditor;
using UnityEngine;

namespace AlienDefense.EditorTools
{
    /// <summary>Builds (or reuses) the Phase 5 Projectile_Blaster definition and prototype prefab.</summary>
    internal static class ProjectilePrefabBuilder
    {
        private const string DataFolder = "Assets/_Game/Data/Projectiles";
        private const string DefinitionPath = DataFolder + "/Projectile_Blaster.asset";

        private const string PrefabFolder = "Assets/_Game/Prefabs/Projectiles";
        private const string PrefabPath = PrefabFolder + "/Projectile_Blaster.prefab";

        private const string ProjectileLayerName = "Projectile";

        [MenuItem("AlienDefense/Setup/7. Create Projectile Definition And Prefab")]
        public static ProjectileDefinition CreateOrLoad()
        {
            EditorFolderUtility.EnsureFolder(DataFolder);
            EditorFolderUtility.EnsureFolder(PrefabFolder);

            GameObject prefab = CreateOrLoadPrefab();

            var definition = AssetDatabase.LoadAssetAtPath<ProjectileDefinition>(DefinitionPath);
            if (definition == null)
            {
                definition = ScriptableObject.CreateInstance<ProjectileDefinition>();
                AssetDatabase.CreateAsset(definition, DefinitionPath);
            }

            var serialized = new SerializedObject(definition);
            serialized.FindProperty("_id").stringValue = "projectile_blaster";
            serialized.FindProperty("_displayName").stringValue = "Blaster Bolt";
            serialized.FindProperty("_prefab").objectReferenceValue = prefab != null ? prefab.GetComponent<ProjectileController>() : null;
            serialized.FindProperty("_speed").floatValue = 10f;
            serialized.FindProperty("_maximumLifetime").floatValue = 5f;
            serialized.FindProperty("_hitDistance").floatValue = 0.25f;
            serialized.FindProperty("_hitVfxDefinition").objectReferenceValue = VfxPrefabBuilder.ProjectileHit;
            serialized.FindProperty("_poolPrewarmCount").intValue = 20;
            serialized.FindProperty("_poolDefaultCapacity").intValue = 40;
            serialized.FindProperty("_poolMaximumSize").intValue = 200;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            AssetDatabase.SaveAssets();
            Debug.Log("[AlienDefense Setup] Projectile definition and prefab ready.");

            return AssetDatabase.LoadAssetAtPath<ProjectileDefinition>(DefinitionPath);
        }

        private static GameObject CreateOrLoadPrefab()
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (existing != null)
            {
                MigrateRepairBrokenMeshMaterial(existing);
                return existing;
            }

            GameObject root = BuildHierarchy();
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);

            return AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        }

        /// <summary>Re-links an existing prefab's Mesh/Trail renderers to the persistent material if they point elsewhere or nowhere.</summary>
        private static void MigrateRepairBrokenMeshMaterial(GameObject prefabAsset)
        {
            Material material = EditorMaterialUtility.CreateOrLoadMaterial(
                "Mat_Projectile_Blaster", "Universal Render Pipeline/Unlit", new Color(0.3f, 0.9f, 1f));

            Transform meshTransform = prefabAsset.transform.Find("VisualRoot/Mesh");
            Renderer existingRenderer = meshTransform != null ? meshTransform.GetComponent<MeshRenderer>() : null;
            if (existingRenderer == null || existingRenderer.sharedMaterial == material)
            {
                return;
            }

            GameObject contents = PrefabUtility.LoadPrefabContents(PrefabPath);

            Renderer renderer = contents.transform.Find("VisualRoot/Mesh")?.GetComponent<MeshRenderer>();
            if (renderer != null && renderer.sharedMaterial != material)
            {
                renderer.sharedMaterial = material;

                TrailRenderer trail = contents.transform.Find("Trail")?.GetComponent<TrailRenderer>();
                if (trail != null)
                {
                    trail.sharedMaterial = material;
                }

                PrefabUtility.SaveAsPrefabAsset(contents, PrefabPath);
                Debug.Log("[AlienDefense Setup] Migrated " + PrefabPath + ": re-linked Mesh material to the persistent asset.");
            }

            PrefabUtility.UnloadPrefabContents(contents);
        }

        private static GameObject BuildHierarchy()
        {
            var root = new GameObject("Projectile_Blaster");
            int projectileLayer = LayerMask.NameToLayer(ProjectileLayerName);
            if (projectileLayer >= 0)
            {
                root.layer = projectileLayer;
            }

            var controller = root.AddComponent<ProjectileController>();

            var visualRoot = new GameObject("VisualRoot");
            visualRoot.transform.SetParent(root.transform, false);

            GameObject mesh = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            mesh.name = "Mesh";
            mesh.transform.SetParent(visualRoot.transform, false);
            mesh.transform.localScale = Vector3.one * 0.2f;
            Object.DestroyImmediate(mesh.GetComponent<Collider>());

            Material material = EditorMaterialUtility.CreateOrLoadMaterial(
                "Mat_Projectile_Blaster", "Universal Render Pipeline/Unlit", new Color(0.3f, 0.9f, 1f));
            mesh.GetComponent<MeshRenderer>().sharedMaterial = material;

            var trailObject = new GameObject("Trail");
            trailObject.transform.SetParent(root.transform, false);
            var trail = trailObject.AddComponent<TrailRenderer>();
            trail.time = 0.15f;
            trail.startWidth = 0.08f;
            trail.endWidth = 0f;
            trail.sharedMaterial = material;

            var controllerSerialized = new SerializedObject(controller);
            controllerSerialized.FindProperty("_trail").objectReferenceValue = trail;
            controllerSerialized.ApplyModifiedPropertiesWithoutUndo();

            return root;
        }
    }
}
