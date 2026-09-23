using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace AlienDefense.EditorTools
{
    /// <summary>Generates mobile-weight replacements for the handful of AI-generated (Tripo) props that dominate the
    /// frame's triangle count, and points the scene/prefabs at them.
    ///
    /// Measured on Level_01 with 100 enemies alive: 2,121k triangles per frame, of which 1,620k came from just eight
    /// renderers - the six BuildNode pads (404,738 tris each), the PlayerBase (502,308) and the UFO landing medallion
    /// (492,676). They are also shadow casters, so each one is submitted twice. At ~24 m from a fixed top-down camera
    /// none of that density is visible, so they are re-meshed down to a few thousand triangles.
    ///
    /// The source FBX files are never touched: each simplified mesh is written as its own asset under
    /// Assets/_Game/Models/Optimized, and only MeshFilter.sharedMesh references are re-pointed (materials, transforms,
    /// colliders and prefab links stay as they are). Re-running is safe: it regenerates the assets in place, so the
    /// existing references keep resolving.</summary>
    internal static class MobileMeshOptimizer
    {
        private const string OutputFolder = "Assets/_Game/Models/Optimized";

        private readonly struct Target
        {
            public readonly string SourceAssetPath;
            public readonly string MeshName;
            public readonly int TriangleBudget;

            public Target(string sourceAssetPath, string meshName, int triangleBudget)
            {
                SourceAssetPath = sourceAssetPath;
                MeshName = meshName;
                TriangleBudget = triangleBudget;
            }
        }

        /// <summary>Budgets are sized from how large each prop is on screen and from how much hard-surface detail it
        /// has. These are all machined shapes with flat faces and sharp edges, which tolerate far less decimation than
        /// an organic mesh: a first pass at 2.5k left the build pad visibly melted, so the budgets sit where the
        /// silhouette and the panel lines still read at gameplay distance while still cutting ~95% of the triangles.</summary>
        private static readonly Target[] Targets =
        {
            new Target("Assets/_Game/Models/Base/tripo_convert_7c586f8b-a725-4d3b-96d1-87e86cf80088.fbx",
                "tripo_node_7c586f8b", 6000),
            new Target("Assets/_Game/Models/Base/futuristic+base+3d+model/tripo_convert_cae021c1-8485-4d8b-ab07-7a41ecea1a35.fbx",
                "tripo_node_cae021c1", 40000),
            new Target("Assets/_Game/Models/UFOLanding/golden+circular+medallion+3d+model/tripo_convert_30bbdae6-c7bf-42b9-a691-55c803251b5d.fbx",
                "tripo_node_30bbdae6", 60000),
        };

        [MenuItem("AlienDefense/Optimize/Rebuild Mobile Meshes (Heavy Props)")]
        private static void Run()
        {
            if (!Directory.Exists(OutputFolder))
            {
                Directory.CreateDirectory(OutputFolder);
                AssetDatabase.Refresh();
            }

            var replacements = new Dictionary<Mesh, Mesh>();
            var report = new StringBuilder("[MobileMeshOptimizer]");

            foreach (Target target in Targets)
            {
                Mesh source = FindMesh(target.SourceAssetPath, target.MeshName);
                if (source == null)
                {
                    Debug.LogWarning($"[MobileMeshOptimizer] {target.MeshName} not found in {target.SourceAssetPath}.");
                    continue;
                }

                Mesh simplified = MeshDecimator.Decimate(source, target.TriangleBudget);
                string path = $"{OutputFolder}/{target.MeshName}_Mobile.asset";
                simplified.name = target.MeshName + "_Mobile";

                var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
                if (existing != null)
                {
                    // Keep the asset (and therefore every reference to it) and swap its contents.
                    existing.Clear();
                    CopyInto(simplified, existing);
                    existing.name = simplified.name;
                    EditorUtility.SetDirty(existing);
                    Object.DestroyImmediate(simplified);
                    simplified = existing;
                }
                else
                {
                    AssetDatabase.CreateAsset(simplified, path);
                }

                replacements[source] = simplified;
                report.AppendLine();
                report.Append($"  {target.MeshName}: {source.triangles.Length / 3} -> {simplified.triangles.Length / 3} tris, " +
                    $"{source.vertexCount} -> {simplified.vertexCount} verts | {MeshDecimator.LastReport}");
            }

            AssetDatabase.SaveAssets();

            int prefabs = RemapPrefabs(replacements);
            int sceneRenderers = RemapOpenScenes(replacements);
            report.AppendLine();
            report.Append($"  re-pointed {sceneRenderers} scene renderers and {prefabs} prefab renderers.");
            Debug.Log(report.ToString());
        }

        /// <summary>Quick sanity check on a primitive, so the decimator can be verified in a second instead of
        /// through a three-minute bake of a 400k-triangle prop.</summary>
        [MenuItem("AlienDefense/Optimize/Decimator Self Test")]
        private static void SelfTest()
        {
            GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Mesh source = sphere.GetComponent<MeshFilter>().sharedMesh;
            Mesh result = MeshDecimator.Decimate(source, 200);
            Debug.Log($"[MobileMeshOptimizer] self test: {source.triangles.Length / 3} -> {result.triangles.Length / 3} tris" +
                $" | {MeshDecimator.LastReport}");
            Object.DestroyImmediate(result);
            Object.DestroyImmediate(sphere);
        }

        private static Mesh FindMesh(string assetPath, string meshName)
        {
            foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(assetPath))
            {
                if (asset is Mesh mesh && mesh.name == meshName)
                {
                    return mesh;
                }
            }

            return null;
        }

        private static void CopyInto(Mesh source, Mesh destination)
        {
            destination.indexFormat = source.indexFormat;
            destination.vertices = source.vertices;
            destination.normals = source.normals;
            destination.tangents = source.tangents;
            destination.uv = source.uv;
            destination.subMeshCount = source.subMeshCount;
            for (int i = 0; i < source.subMeshCount; i++)
            {
                destination.SetTriangles(source.GetTriangles(i), i, false);
            }

            destination.RecalculateBounds();
        }

        private static int RemapPrefabs(Dictionary<Mesh, Mesh> replacements)
        {
            if (replacements.Count == 0)
            {
                return 0;
            }

            int changed = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/_Game" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null || !NeedsRemap(prefab, replacements))
                {
                    continue;
                }

                GameObject contents = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    int count = RemapHierarchy(contents, replacements);
                    if (count > 0)
                    {
                        PrefabUtility.SaveAsPrefabAsset(contents, path);
                        changed += count;
                    }
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(contents);
                }
            }

            return changed;
        }

        private static bool NeedsRemap(GameObject prefab, Dictionary<Mesh, Mesh> replacements)
        {
            foreach (MeshFilter filter in prefab.GetComponentsInChildren<MeshFilter>(true))
            {
                if (filter.sharedMesh != null && replacements.ContainsKey(filter.sharedMesh))
                {
                    return true;
                }
            }

            return false;
        }

        private static int RemapHierarchy(GameObject root, Dictionary<Mesh, Mesh> replacements)
        {
            int changed = 0;
            foreach (MeshFilter filter in root.GetComponentsInChildren<MeshFilter>(true))
            {
                if (filter.sharedMesh != null && replacements.TryGetValue(filter.sharedMesh, out Mesh replacement))
                {
                    filter.sharedMesh = replacement;
                    EditorUtility.SetDirty(filter);
                    changed++;
                }
            }

            return changed;
        }

        private static int RemapOpenScenes(Dictionary<Mesh, Mesh> replacements)
        {
            if (replacements.Count == 0)
            {
                return 0;
            }

            int changed = 0;
            foreach (MeshFilter filter in Object.FindObjectsByType<MeshFilter>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (filter.sharedMesh == null || !replacements.TryGetValue(filter.sharedMesh, out Mesh replacement))
                {
                    continue;
                }

                Undo.RecordObject(filter, "Mobile mesh");
                filter.sharedMesh = replacement;
                EditorUtility.SetDirty(filter);
                changed++;
            }

            if (changed > 0)
            {
                EditorSceneManager.MarkAllScenesDirty();
                EditorSceneManager.SaveOpenScenes();
            }

            return changed;
        }
    }
}
