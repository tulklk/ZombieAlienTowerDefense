using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace AlienDefense.EditorTools
{
    /// <summary>Builds the 3 shared, normalized (unit-sized) meshes the Tractor Beam VFX scales via Transform at
    /// runtime: a truncated cone (lateral surface only), a filled disc, and a ring. Soft edges are baked directly
    /// into vertex color alpha — no texture, no Shader Graph, generated once and saved as persistent mesh assets.</summary>
    internal static class TractorBeamMeshGenerator
    {
        private const string MeshFolder = "Assets/_Game/Art/Meshes/VFX";
        private const string ConeMeshPath = MeshFolder + "/Mesh_TractorBeamCone.asset";
        private const string DiscMeshPath = MeshFolder + "/Mesh_TractorBeamDisc.asset";
        private const string RingMeshPath = MeshFolder + "/Mesh_TractorBeamRing.asset";

        private const int RadialSegments = 20;

        /// <summary>Normalized cone: base radius 1 at y=0, top radius 0.15 at y=1. Scale Transform to fit.</summary>
        public static Mesh CreateOrLoadConeMesh()
        {
            return CreateOrLoadMesh(ConeMeshPath, BuildConeMesh);
        }

        /// <summary>Normalized filled disc: radius 1 on the XZ plane, center bright fading to a transparent rim.</summary>
        public static Mesh CreateOrLoadDiscMesh()
        {
            return CreateOrLoadMesh(DiscMeshPath, BuildDiscMesh);
        }

        /// <summary>Normalized ring: outer radius 1 on the XZ plane, brightest at its mid-band, transparent at both edges.</summary>
        public static Mesh CreateOrLoadRingMesh()
        {
            return CreateOrLoadMesh(RingMeshPath, BuildRingMesh);
        }

        [MenuItem("AlienDefense/Setup/23. Create Tractor Beam VFX Meshes")]
        public static void CreateAll()
        {
            EditorFolderUtility.EnsureFolder(MeshFolder);
            CreateOrLoadConeMesh();
            CreateOrLoadDiscMesh();
            CreateOrLoadRingMesh();
            Debug.Log("[AlienDefense Setup] Tractor Beam VFX meshes ready.");
        }

        private static Mesh CreateOrLoadMesh(string path, System.Func<Mesh> factory)
        {
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing != null)
            {
                return existing;
            }

            EditorFolderUtility.EnsureFolder(MeshFolder);
            Mesh mesh = factory();
            AssetDatabase.CreateAsset(mesh, path);
            AssetDatabase.SaveAssets();
            return mesh;
        }

        private static Mesh BuildConeMesh()
        {
            const float bottomRadius = 1f;
            const float topRadius = 0.15f;
            const byte bottomAlpha = 90; // soft near the ground
            const byte topAlpha = 230;   // bright near the UFO

            var vertices = new List<Vector3>();
            var colors = new List<Color32>();
            var uvs = new List<Vector2>();
            var triangles = new List<int>();

            for (int i = 0; i <= RadialSegments; i++)
            {
                float t = (float)i / RadialSegments;
                float angle = t * Mathf.PI * 2f;
                float cos = Mathf.Cos(angle);
                float sin = Mathf.Sin(angle);

                vertices.Add(new Vector3(cos * bottomRadius, 0f, sin * bottomRadius));
                colors.Add(new Color32(255, 255, 255, bottomAlpha));
                uvs.Add(new Vector2(t, 0f));

                vertices.Add(new Vector3(cos * topRadius, 1f, sin * topRadius));
                colors.Add(new Color32(255, 255, 255, topAlpha));
                uvs.Add(new Vector2(t, 1f));
            }

            for (int i = 0; i < RadialSegments; i++)
            {
                int bottomA = i * 2;
                int topA = bottomA + 1;
                int bottomB = (i + 1) * 2;
                int topB = bottomB + 1;

                triangles.Add(bottomA);
                triangles.Add(topA);
                triangles.Add(bottomB);

                triangles.Add(topA);
                triangles.Add(topB);
                triangles.Add(bottomB);
            }

            return BuildMesh("TractorBeamCone", vertices, colors, uvs, triangles);
        }

        private static Mesh BuildDiscMesh()
        {
            var vertices = new List<Vector3> { Vector3.zero };
            var colors = new List<Color32> { new Color32(255, 255, 255, 255) };
            var uvs = new List<Vector2> { new Vector2(0.5f, 0.5f) };
            var triangles = new List<int>();

            for (int i = 0; i <= RadialSegments; i++)
            {
                float t = (float)i / RadialSegments;
                float angle = t * Mathf.PI * 2f;
                float cos = Mathf.Cos(angle);
                float sin = Mathf.Sin(angle);

                vertices.Add(new Vector3(cos, 0f, sin));
                colors.Add(new Color32(255, 255, 255, 0));
                uvs.Add(new Vector2(cos * 0.5f + 0.5f, sin * 0.5f + 0.5f));
            }

            for (int i = 1; i <= RadialSegments; i++)
            {
                triangles.Add(0);
                triangles.Add(i);
                triangles.Add(i + 1);
            }

            return BuildMesh("TractorBeamDisc", vertices, colors, uvs, triangles);
        }

        private static Mesh BuildRingMesh()
        {
            const float innerRadius = 0.8f;
            const float midRadius = 0.9f;
            const float outerRadius = 1f;

            var vertices = new List<Vector3>();
            var colors = new List<Color32>();
            var uvs = new List<Vector2>();
            var triangles = new List<int>();

            for (int i = 0; i <= RadialSegments; i++)
            {
                float t = (float)i / RadialSegments;
                float angle = t * Mathf.PI * 2f;
                float cos = Mathf.Cos(angle);
                float sin = Mathf.Sin(angle);

                vertices.Add(new Vector3(cos * innerRadius, 0f, sin * innerRadius));
                colors.Add(new Color32(255, 255, 255, 0));
                uvs.Add(new Vector2(t, 0f));

                vertices.Add(new Vector3(cos * midRadius, 0f, sin * midRadius));
                colors.Add(new Color32(255, 255, 255, 255));
                uvs.Add(new Vector2(t, 0.5f));

                vertices.Add(new Vector3(cos * outerRadius, 0f, sin * outerRadius));
                colors.Add(new Color32(255, 255, 255, 0));
                uvs.Add(new Vector2(t, 1f));
            }

            for (int i = 0; i < RadialSegments; i++)
            {
                int a0 = i * 3, a1 = a0 + 1, a2 = a0 + 2;
                int b0 = (i + 1) * 3, b1 = b0 + 1, b2 = b0 + 2;

                triangles.Add(a0); triangles.Add(a1); triangles.Add(b0);
                triangles.Add(a1); triangles.Add(b1); triangles.Add(b0);

                triangles.Add(a1); triangles.Add(a2); triangles.Add(b1);
                triangles.Add(a2); triangles.Add(b2); triangles.Add(b1);
            }

            return BuildMesh("TractorBeamRing", vertices, colors, uvs, triangles);
        }

        private static Mesh BuildMesh(string name, List<Vector3> vertices, List<Color32> colors, List<Vector2> uvs, List<int> triangles)
        {
            var mesh = new Mesh { name = name };
            mesh.SetVertices(vertices);
            mesh.SetColors(colors);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
