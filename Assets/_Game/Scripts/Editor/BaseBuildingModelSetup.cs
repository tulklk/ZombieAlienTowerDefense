using System.IO;
using UnityEditor;
using UnityEngine;

namespace AlienDefense.EditorTools
{
    /// <summary>Puts the project's own building models (Assets/_Game/Models/Building) on the Base plots.
    ///
    /// The source FBX files are AI-generated at 0.5-1.5 million triangles each - far beyond a mobile budget for a
    /// building seen from the base camera. Each is re-meshed once with the project's QEM MeshDecimator (UVs kept, so
    /// the PBR textures still sit right) into Assets/_Game/Models/Optimized/Base, then wrapped in the existing
    /// BP_&lt;buildingId&gt; prefab: the FBX is instanced (materials and transform chain untouched), its mesh reference is
    /// pointed at the optimised copy, it is scaled to the plot, centred and grounded, and a fitted BoxCollider is added.
    /// Saving over the existing wrapper keeps its GUID, so every BaseBuildingDefinition keeps pointing at it.
    ///
    /// Source FBX files are never modified. Decimation of a 1.5M-triangle mesh takes minutes, so the menu item runs
    /// deferred and writes its progress to Temp/BaseBuildingModelSetup.log.</summary>
    public static class BaseBuildingModelSetup
    {
        private const string ModelsFolder = "Assets/_Game/Models/Building";
        private const string OutputFolder = "Assets/_Game/Models/Optimized/Base";
        private const string PrefabFolder = "Assets/_Game/Prefabs/Base/Buildings";
        private const string LogPath = "Temp/BaseBuildingModelSetup.log";

        /// <summary>Building id -> model folder, triangle budget, footprint width on the plot (m), yaw.</summary>
        private static readonly (string buildingId, string model, int budget, float width, float yaw)[] Mappings =
        {
            ("central_building", "centralbuilding", 20000, 9.5f, 0f),
            ("research_center", "geneticsearchcenter", 20000, 10f, 0f),
            ("weapon_workshop", "weaponworkshop", 20000, 10.5f, 0f),
            ("patrol_post", "pastrolpost", 16000, 9f, 0f),
            ("sawmill", "engineerhouse", 16000, 8f, 0f)
        };

        [MenuItem("Tools/Tower Defense/Base/Import Building Models")]
        public static void Import()
        {
            EditorApplication.delayCall += () => Run(redecimate: false);
        }

        [MenuItem("Tools/Tower Defense/Base/Import Building Models (Re-decimate)")]
        public static void ImportRedecimate()
        {
            EditorApplication.delayCall += () => Run(redecimate: true);
        }

        /// <summary>Decimates without saving - for comparing budgets on a model before committing to one.</summary>
        public static Mesh PreviewDecimate(Mesh source, int budget, out string report)
        {
            Mesh mesh = MeshDecimator.Decimate(source, budget);
            report = MeshDecimator.LastReport;
            return mesh;
        }

        /// <summary>Re-wraps only (no decimation) - for tuning width/yaw after the optimised meshes exist.</summary>
        public static void RewrapOnly()
        {
            Run(redecimate: false);
        }

        public static void Run(bool redecimate)
        {
            File.WriteAllText(LogPath, "started " + System.DateTime.Now.ToString("HH:mm:ss") + "\n");
            EnsureFolder(OutputFolder);

            foreach ((string buildingId, string model, int budget, float width, float yaw) in Mappings)
            {
                string fbxPath = $"{ModelsFolder}/{model}/{model}.fbx";
                var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
                MeshFilter sourceFilter = fbx != null ? fbx.GetComponentInChildren<MeshFilter>() : null;
                if (sourceFilter == null || sourceFilter.sharedMesh == null)
                {
                    Log($"MISSING ASSET: {fbxPath} (no mesh) - {buildingId} keeps its current model.");
                    continue;
                }

                Mesh optimized = EnsureOptimizedMesh(sourceFilter.sharedMesh, model, budget, redecimate);
                WrapPrefab(buildingId, fbx, sourceFilter.sharedMesh, optimized, width, yaw);
            }

            LimitTextureSizes();
            AssetDatabase.SaveAssets();
            Log("done " + System.DateTime.Now.ToString("HH:mm:ss"));
        }

        private static Mesh EnsureOptimizedMesh(Mesh source, string model, int budget, bool redecimate)
        {
            string path = $"{OutputFolder}/{model}_Mobile.asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing != null && !redecimate)
            {
                Log($"{model}: reusing {path} ({existing.triangles.Length / 3} tris)");
                return existing;
            }

            var started = System.DateTime.Now;
            Log($"{model}: decimating {source.triangles.Length / 3} -> {budget} tris...");

            // These FBX meshes are authored at centimetre scale (~0.009 units across, scaled x100 by the importer).
            // At that size the quadric errors underflow and the decimator shreds the surface; working on a copy
            // brought to ~10 units and scaling the result back keeps it faithful to the original.
            float extent = Mathf.Max(source.bounds.size.x, source.bounds.size.y, source.bounds.size.z);
            float scale = extent > 0f ? 10f / extent : 1f;
            Mesh working = Object.Instantiate(source);
            ScaleVertices(working, scale);
            Mesh simplified = MeshDecimator.Decimate(working, budget);
            Object.DestroyImmediate(working);
            ScaleVertices(simplified, 1f / scale);
            simplified.name = model + "_Mobile";

            if (existing != null)
            {
                existing.Clear();
                EditorUtility.CopySerialized(simplified, existing);
                existing.name = simplified.name;
                EditorUtility.SetDirty(existing);
                Object.DestroyImmediate(simplified);
                simplified = existing;
            }
            else
            {
                AssetDatabase.CreateAsset(simplified, path);
            }

            Log($"{model}: {source.triangles.Length / 3} -> {simplified.triangles.Length / 3} tris in " +
                $"{(System.DateTime.Now - started).TotalSeconds:0}s | {MeshDecimator.LastReport}");
            return simplified;
        }

        private static void WrapPrefab(string buildingId, GameObject fbx, Mesh sourceMesh, Mesh optimized, float width, float yaw)
        {
            string path = $"{PrefabFolder}/BP_{buildingId}.prefab";
            EnsureFolder(PrefabFolder);

            var root = new GameObject("BP_" + buildingId);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(fbx, root.transform);
            instance.transform.localRotation = Quaternion.Euler(0f, yaw, 0f) * instance.transform.localRotation;

            foreach (MeshFilter filter in instance.GetComponentsInChildren<MeshFilter>(true))
            {
                if (filter.sharedMesh == sourceMesh)
                {
                    filter.sharedMesh = optimized;
                }
            }

            // Importer adds an Animator to FBX roots; these are static buildings.
            foreach (Animator animator in instance.GetComponentsInChildren<Animator>(true))
            {
                animator.enabled = false;
            }

            foreach (Renderer renderer in instance.GetComponentsInChildren<Renderer>(true))
            {
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
                renderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            }

            // Scale to the plot footprint, then centre on the wrapper origin with the base on y = 0.
            Bounds bounds = RendererBounds(instance);
            float footprint = Mathf.Max(bounds.size.x, bounds.size.z);
            if (footprint > 0.0001f)
            {
                instance.transform.localScale *= width / footprint;
            }

            bounds = RendererBounds(instance);
            instance.transform.position += new Vector3(-bounds.center.x, -bounds.min.y, -bounds.center.z);
            bounds = RendererBounds(instance);

            var collider = root.AddComponent<BoxCollider>();
            collider.center = bounds.center;
            collider.size = bounds.size;

            PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            Log($"{buildingId}: wrapped {fbx.name} -> {path}, footprint {bounds.size.x:0.0} x {bounds.size.z:0.0} m, height {bounds.size.y:0.0} m");
        }

        /// <summary>The PBR maps ship at 2048 px; at base-camera distance 1024 is indistinguishable and quarters the
        /// memory. Only the import setting changes - the source images are untouched.</summary>
        private static void LimitTextureSizes()
        {
            foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { ModelsFolder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null || importer.maxTextureSize <= 1024)
                {
                    continue;
                }

                importer.maxTextureSize = 1024;
                importer.SaveAndReimport();
                Log("texture max size 1024: " + path);
            }
        }

        private static void ScaleVertices(Mesh mesh, float scale)
        {
            Vector3[] vertices = mesh.vertices;
            for (int i = 0; i < vertices.Length; i++)
            {
                vertices[i] *= scale;
            }

            mesh.vertices = vertices;
            mesh.RecalculateBounds();
        }

        private static Bounds RendererBounds(GameObject root)
        {
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            Bounds bounds = renderers.Length > 0 ? renderers[0].bounds : new Bounds(root.transform.position, Vector3.zero);
            for (int i = 1; i < renderers.Length; i++)
            {
                bounds.Encapsulate(renderers[i].bounds);
            }

            return bounds;
        }

        private static void Log(string line)
        {
            Debug.Log("[BaseBuildingModelSetup] " + line);
            File.AppendAllText(LogPath, line + "\n");
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
