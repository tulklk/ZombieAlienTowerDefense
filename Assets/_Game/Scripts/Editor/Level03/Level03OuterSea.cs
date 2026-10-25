using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace AlienDefense.EditorTools
{
    /// <summary>Open sea around Level_03's water surface (2026-10-11): with the play area widened out over the sea
    /// strips, the camera can look past the edge of the generated water grid. This frame of four flat quads continues
    /// it outwards with the same stylized-water material (vertex colour R = 0: deep water, no foam), so the view
    /// ends in sea instead of sky. Eight triangles, no collider, no shadows.</summary>
    public static partial class Level03Builder
    {
        private const float OuterSeaReach = 70f;
        private const float SeabedTileDepth = 16f;

        /// <summary>Flat sea-bed terrain tiles past the main terrain's +-Z edges (2026-10-11), under the outer sea. The
        /// widened play area reaches over the open sea there, and the UFO hover, tractor ring and Energy scatter all
        /// follow Terrain.SampleHeight - without ground under it the UFO would drop a metre at the old terrain edge.
        /// Hidden under the opaque water; 33 x 33 heightmap, no shadows.</summary>
        private static void EnsureSeabedTiles(Transform root, Terrain main)
        {
            Transform parent = main.transform.parent;
            for (int side = -1; side <= 1; side += 2)
            {
                string name = side < 0 ? "Seabed_South" : "Seabed_North";
                Transform old = parent.Find(name);
                if (old != null)
                {
                    Object.DestroyImmediate(old.gameObject);
                }

                string dataPath = TerrainFolder + "/TD_Level03_" + name + ".asset";
                var data = AssetDatabase.LoadAssetAtPath<TerrainData>(dataPath);
                if (data == null)
                {
                    data = new TerrainData();
                    AssetDatabase.CreateAsset(data, dataPath);
                }

                data.heightmapResolution = 33;
                data.size = new Vector3(TerrainSize.x, TerrainSize.y, SeabedTileDepth);
                var heights = new float[33, 33];
                float h = SeabedY / TerrainSize.y;
                for (int z = 0; z < 33; z++)
                {
                    for (int x = 0; x < 33; x++)
                    {
                        heights[z, x] = h;
                    }
                }

                data.SetHeights(0, 0, heights);
                data.terrainLayers = main.terrainData.terrainLayers;
                EditorUtility.SetDirty(data);

                GameObject go = Terrain.CreateTerrainGameObject(data);
                go.name = name;
                go.transform.SetParent(parent, false);
                go.layer = main.gameObject.layer;
                float z0 = side < 0 ? TerrainOrigin.z - SeabedTileDepth : TerrainOrigin.z + TerrainSize.z;
                go.transform.position = new Vector3(TerrainOrigin.x, TerrainOrigin.y, z0);
                var terrain = go.GetComponent<Terrain>();
                terrain.materialTemplate = main.materialTemplate;
                terrain.shadowCastingMode = ShadowCastingMode.Off;
                terrain.drawTreesAndFoliage = false;
                terrain.heightmapPixelError = 50f;
                terrain.basemapDistance = 0f;
                terrain.allowAutoConnect = false; // different heightmap resolution than the main terrain
                Object.DestroyImmediate(go.GetComponent<TerrainCollider>());
            }

            Report.Add("Sea bed: two flat terrain tiles, " + SeabedTileDepth + " m past each Z edge of the main terrain, under the outer sea.");
        }

        private static void EnsureOuterSea(Transform root)
        {
            Transform water = root.Find("Environment/Water");
            Transform surface = water != null ? water.Find("WaterSurface") : null;
            Renderer surfaceRenderer = surface != null ? surface.GetComponent<Renderer>() : null;
            if (surfaceRenderer == null)
            {
                Report.Add("WARNING: no Environment/Water/WaterSurface - outer sea skipped.");
                return;
            }

            foreach (Terrain terrain in root.GetComponentsInChildren<Terrain>(true))
            {
                if (terrain.terrainData != null && terrain.terrainData.name == "TD_Level03")
                {
                    EnsureSeabedTiles(root, terrain);
                    break;
                }
            }

            Bounds inner = surfaceRenderer.bounds;
            float y = inner.center.y;
            float x0 = inner.min.x, x1 = inner.max.x, z0 = inner.min.z, z1 = inner.max.z;
            float X0 = x0 - OuterSeaReach, X1 = x1 + OuterSeaReach, Z0 = z0 - OuterSeaReach, Z1 = z1 + OuterSeaReach;

            // Four quads around the inner rectangle: -Z band, +Z band (full width), -X and +X bands (between them).
            Vector3[] vertices =
            {
                new Vector3(X0, y, Z0), new Vector3(X1, y, Z0), new Vector3(X1, y, z0), new Vector3(X0, y, z0),
                new Vector3(X0, y, z1), new Vector3(X1, y, z1), new Vector3(X1, y, Z1), new Vector3(X0, y, Z1),
                new Vector3(X0, y, z0), new Vector3(x0, y, z0), new Vector3(x0, y, z1), new Vector3(X0, y, z1),
                new Vector3(x1, y, z0), new Vector3(X1, y, z0), new Vector3(X1, y, z1), new Vector3(x1, y, z1),
            };
            var triangles = new int[24];
            for (int q = 0; q < 4; q++)
            {
                int v = q * 4, t = q * 6;
                triangles[t] = v;
                triangles[t + 1] = v + 3;
                triangles[t + 2] = v + 2;
                triangles[t + 3] = v;
                triangles[t + 4] = v + 2;
                triangles[t + 5] = v + 1;
            }

            var colors = new Color[vertices.Length]; // R = 0: deep, far from any shore

            string meshPath = ArtFolder + "/Meshes/OuterSea.asset";
            var mesh = AssetDatabase.LoadAssetAtPath<UnityEngine.Mesh>(meshPath);
            if (mesh == null)
            {
                mesh = new UnityEngine.Mesh { name = "OuterSea" };
                AssetDatabase.CreateAsset(mesh, meshPath);
            }

            mesh.Clear();
            mesh.vertices = vertices;
            mesh.colors = colors;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            EditorUtility.SetDirty(mesh);

            Transform old = water.Find("OuterSea");
            if (old != null)
            {
                Object.DestroyImmediate(old.gameObject);
            }

            var go = new GameObject("OuterSea", typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(water, false);
            go.layer = surface.gameObject.layer;
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = surfaceRenderer.sharedMaterial;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
            Report.Add("Outer sea: frame from the water grid (X " + x0.ToString("0") + ".." + x1.ToString("0") + ", Z " + z0.ToString("0") + ".." + z1.ToString("0")
                + ") out " + OuterSeaReach + " m, 8 triangles.");
        }
    }
}
