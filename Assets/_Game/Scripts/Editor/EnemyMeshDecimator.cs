using System.Collections.Generic;
using System.IO;
using AlienDefense.Enemies;
using UnityEditor;
using UnityEngine;
using UnityMeshSimplifier;

namespace AlienDefense.EditorTools
{
    /// <summary>Bakes mobile-weight versions of the enemy skinned meshes. The zombie models came out of Tripo at
    /// 190k-330k triangles EACH (the Boss 50k) - profiling 30 zombies showed the frame waiting ~26 ms on the GPU for
    /// ~8M triangles, which is the game's single biggest cost. At the gameplay camera a zombie is ~80 px tall, so a few
    /// thousand triangles look the same.
    ///
    /// Each mesh is simplified (quadric edge collapse, UnityMeshSimplifier) with bone weights, bind poses and UVs kept,
    /// so the same Animator, bones and 2K texture keep working; the result is saved next to the models as its own asset
    /// and assigned to the enemy prefab's SkinnedMeshRenderer. The source FBX files are never modified, and
    /// "Restore Original Enemy Meshes" puts the imported meshes back.</summary>
    internal static class EnemyMeshDecimator
    {
        private const string EnemyPrefabFolder = "Assets/_Game/Prefabs/Enemies";
        private const string OutputFolder = "Assets/_Game/Models/Zombie/LowPoly";
        private const string Suffix = "_LowPoly";
        private const int DefaultTargetTriangles = 7000;
        private const int BossTargetTriangles = 12000;

        [MenuItem("AlienDefense/Optimization/Bake Low-Poly Enemy Meshes")]
        private static void Bake()
        {
            if (!AssetDatabase.IsValidFolder(OutputFolder))
            {
                AssetDatabase.CreateFolder(Path.GetDirectoryName(OutputFolder).Replace('\\', '/'), Path.GetFileName(OutputFolder));
            }

            var report = new List<string>();
            try
            {
                string[] prefabs = AssetDatabase.FindAssets("t:Prefab", new[] { EnemyPrefabFolder });
                for (int i = 0; i < prefabs.Length; i++)
                {
                    string prefabPath = AssetDatabase.GUIDToAssetPath(prefabs[i]);
                    EditorUtility.DisplayProgressBar("Baking low-poly enemy meshes", prefabPath, i / (float)prefabs.Length);
                    report.Add(BakePrefab(prefabPath));
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                AssetDatabase.SaveAssets();
            }

            Debug.Log("[EnemyMeshDecimator] Done:\n" + string.Join("\n", report));
        }

        [MenuItem("AlienDefense/Optimization/Restore Original Enemy Meshes")]
        private static void Restore()
        {
            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { EnemyPrefabFolder }))
            {
                string prefabPath = AssetDatabase.GUIDToAssetPath(guid);
                GameObject contents = PrefabUtility.LoadPrefabContents(prefabPath);
                try
                {
                    var renderer = contents.GetComponentInChildren<SkinnedMeshRenderer>(true);
                    Mesh original = renderer != null ? FindOriginal(renderer) : null;
                    if (original == null || original == renderer.sharedMesh)
                    {
                        continue;
                    }

                    renderer.sharedMesh = original;
                    PrefabUtility.SaveAsPrefabAsset(contents, prefabPath);
                    Debug.Log($"[EnemyMeshDecimator] {prefabPath}: restored {original.name}");
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(contents);
                }
            }
        }

        private static string BakePrefab(string prefabPath)
        {
            GameObject contents = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                var renderer = contents.GetComponentInChildren<SkinnedMeshRenderer>(true);
                if (renderer == null || renderer.sharedMesh == null)
                {
                    return $"{prefabPath}: no skinned mesh, skipped";
                }

                // Always simplify from the imported original, never from an earlier bake.
                Mesh source = FindOriginal(renderer);
                int sourceTriangles = TriangleCount(source);
                bool isBoss = contents.GetComponent<BossController>() != null;
                int target = isBoss ? BossTargetTriangles : DefaultTargetTriangles;
                if (sourceTriangles <= target * 1.2f)
                {
                    return $"{prefabPath}: {source.name} already light ({sourceTriangles} tris), kept";
                }

                Mesh baked = Simplify(source, target, out bool keptSeams);
                baked.bounds = source.bounds;
                MeshUtility.Optimize(baked);

                string meshPath = $"{OutputFolder}/{contents.name}{Suffix}.asset";
                Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
                if (existing != null)
                {
                    EditorUtility.CopySerialized(baked, existing);
                    Object.DestroyImmediate(baked);
                    baked = existing;
                }
                else
                {
                    AssetDatabase.CreateAsset(baked, meshPath);
                }

                // No CPU copy needed at runtime: the skinning happens from the uploaded buffers.
                var meshSo = new SerializedObject(baked);
                meshSo.FindProperty("m_IsReadable").boolValue = false;
                meshSo.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(baked);

                renderer.sharedMesh = baked;
                PrefabUtility.SaveAsPrefabAsset(contents, prefabPath);
                return $"{prefabPath}: {source.name} {source.vertexCount} verts / {sourceTriangles} tris -> {baked.vertexCount} verts / {TriangleCount(baked)} tris (UV seams kept: {keptSeams})";
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }

        /// <summary>Simplifies with UV seams held (no texture tearing); if the model's seams are so dense that this
        /// cannot get anywhere near the target, simplifies again without holding them.</summary>
        private static Mesh Simplify(Mesh source, int targetTriangles, out bool keptSeams)
        {
            float quality = Mathf.Clamp01(targetTriangles / (float)TriangleCount(source));
            Mesh result = Run(source, quality, preserveSeams: true);
            keptSeams = true;
            if (TriangleCount(result) > targetTriangles * 2)
            {
                Object.DestroyImmediate(result);
                result = Run(source, quality, preserveSeams: false);
                keptSeams = false;
            }

            return result;
        }

        private static Mesh Run(Mesh source, float quality, bool preserveSeams)
        {
            SimplificationOptions options = SimplificationOptions.Default;
            options.PreserveUVSeamEdges = preserveSeams;
            options.PreserveUVFoldoverEdges = preserveSeams;
            options.PreserveBorderEdges = false;
            options.EnableSmartLink = true;

            var simplifier = new MeshSimplifier { SimplificationOptions = options };
            simplifier.Initialize(source);
            simplifier.SimplifyMesh(quality);
            return simplifier.ToMesh();
        }

        /// <summary>The imported mesh, read from the model prefab (ZombieVariant*/BossVariant) nested inside the enemy
        /// prefab - the bake only overrides the mesh on the enemy prefab, so the model keeps the original.</summary>
        private static Mesh FindOriginal(SkinnedMeshRenderer renderer)
        {
            var source = PrefabUtility.GetCorrespondingObjectFromOriginalSource(renderer);
            return source != null && source != renderer && source.sharedMesh != null ? source.sharedMesh : renderer.sharedMesh;
        }

        private static int TriangleCount(Mesh mesh)
        {
            long indices = 0;
            for (int i = 0; i < mesh.subMeshCount; i++)
            {
                indices += mesh.GetIndexCount(i);
            }

            return (int)(indices / 3);
        }
    }
}
