using System.Collections.Generic;
using AlienDefense.Common;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AlienDefense.EditorTools
{
    /// <summary>Gives Level_01's wooden bridge a smooth, gap-free walking surface. The deck's own mesh collider has
    /// gaps between the planks, so enemies' ground probes dropped through onto the beams underneath, and the UFO
    /// (which follows Terrain only) sank to the riverbed while crossing. This samples the deck, fills the gaps with
    /// the highest plank nearby, and saves the result as an invisible MeshCollider + WalkableSurface under the
    /// bridge. Safe to re-run.</summary>
    internal static class Level01BridgeWalkSurfaceSetup
    {
        private const string ScenePath = "Assets/_Game/Scenes/Levels/Level_01.unity";
        private const string BridgePath = "Maps/ZombieRoad/PT_Wooden_Bridge_02";
        private const string MeshPath = "Assets/_Game/Materials/Generated/Level01_BridgeWalkSurface.asset";
        private const string SurfaceName = "WalkSurface";
        private const float Step = 0.2f;
        private const float FillRadius = 0.2f;  // at least the widest plank gap
        private const float RampLength = 1.2f;  // eases the deck ends down onto the banks
        private const float SidePadding = 0.6f; // over the railing strips, so the crossing has no dip at its edges
        private const float RailClearance = 0.72f; // hits higher than this above the bridge pivot are railings

        [MenuItem("AlienDefense/Setup/Level 01/Build Bridge Walk Surface")]
        private static void Run()
        {
            if (EditorSceneManager.GetActiveScene().path != ScenePath)
            {
                Debug.LogError("[Level01BridgeWalkSurfaceSetup] Open Level_01 first.");
                return;
            }

            GameObject bridge = GameObject.Find(BridgePath);
            if (bridge == null)
            {
                Debug.LogError("[Level01BridgeWalkSurfaceSetup] Bridge not found at " + BridgePath);
                return;
            }

            Transform old = bridge.transform.Find(SurfaceName);
            if (old != null)
            {
                Object.DestroyImmediate(old.gameObject);
            }

            var deckColliders = new List<Collider>(bridge.GetComponentsInChildren<Collider>());
            if (deckColliders.Count == 0)
            {
                Debug.LogError("[Level01BridgeWalkSurfaceSetup] The bridge has no colliders to sample.");
                return;
            }

            Bounds bounds = deckColliders[0].bounds;
            foreach (Collider c in deckColliders)
            {
                bounds.Encapsulate(c.bounds);
            }

            float railY = bridge.transform.position.y + RailClearance;
            int nx = Mathf.CeilToInt(bounds.size.x / Step) + 1;
            int nz = Mathf.CeilToInt(bounds.size.z / Step) + 1;
            var heights = new float[nx, nz];
            var valid = new bool[nx, nz];

            for (int i = 0; i < nx; i++)
            {
                for (int j = 0; j < nz; j++)
                {
                    float x = bounds.min.x + i * Step, z = bounds.min.z + j * Step;
                    valid[i, j] = SampleDeck(deckColliders, x, z, bounds.max.y + 5f, railY, out heights[i, j]);
                }
            }

            // Keep only the rows (along Z) that are deck from end to end - drops the railing strips on both sides.
            int zMin = -1, zMax = -1;
            for (int j = 0; j < nz; j++)
            {
                int count = 0;
                for (int i = 0; i < nx; i++)
                {
                    if (valid[i, j])
                    {
                        count++;
                    }
                }

                if (count >= nx - 1)
                {
                    if (zMin < 0)
                    {
                        zMin = j;
                    }

                    zMax = j;
                }
            }

            if (zMin < 0 || zMax - zMin < 2)
            {
                Debug.LogError("[Level01BridgeWalkSurfaceSetup] Could not find a continuous deck to build on.");
                return;
            }

            var go = new GameObject(SurfaceName);
            go.transform.SetParent(bridge.transform, false);
            go.layer = bridge.layer;
            Transform t = go.transform;

            // Final grid: the deck rows, padded sideways over the railing strips (so the UFO does not dip at the very
            // edge of the crossing) and with a ramp at each end that eases down onto the bank instead of a step.
            int ramp = Mathf.RoundToInt(RampLength / Step), pad = Mathf.RoundToInt(SidePadding / Step);
            int cols = nx + ramp * 2;
            int rows = zMax - zMin + 1 + pad * 2;
            var vertices = new Vector3[cols * rows];
            for (int c = 0; c < cols; c++)
            {
                int i = Mathf.Clamp(c - ramp, 0, nx - 1);
                float x = bounds.min.x + (c - ramp) * Step;
                for (int r = 0; r < rows; r++)
                {
                    int src = Mathf.Clamp(r - pad + zMin, zMin, zMax);
                    float z = bounds.min.z + (r - pad + zMin) * Step;
                    float h = valid[i, src] ? heights[i, src] : NeighbourHeight(heights, valid, i, src);

                    int beyond = c < ramp ? ramp - c : c > ramp + nx - 1 ? c - (ramp + nx - 1) : 0;
                    if (beyond > 0 && TryTerrainHeight(x, z, out float ground))
                    {
                        h = Mathf.Lerp(h, ground, Mathf.SmoothStep(0f, 1f, beyond / (float)ramp)) - (beyond == ramp ? 0.05f : 0f);
                    }

                    vertices[c * rows + r] = t.InverseTransformPoint(new Vector3(x, h, z));
                }
            }

            var triangles = new List<int>();
            for (int i = 0; i < cols - 1; i++)
            {
                for (int j = 0; j < rows - 1; j++)
                {
                    int v00 = i * rows + j, v01 = i * rows + j + 1, v10 = (i + 1) * rows + j, v11 = (i + 1) * rows + j + 1;
                    // clockwise seen from above -> normals face up (queries ignore back faces)
                    triangles.Add(v00); triangles.Add(v01); triangles.Add(v11);
                    triangles.Add(v00); triangles.Add(v11); triangles.Add(v10);
                }
            }

            var mesh = new Mesh { name = "Level01_BridgeWalkSurface" };
            mesh.vertices = vertices;
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            if (AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath) != null)
            {
                AssetDatabase.DeleteAsset(MeshPath);
            }

            AssetDatabase.CreateAsset(mesh, MeshPath);
            AssetDatabase.SaveAssets();

            var collider = go.AddComponent<MeshCollider>();
            collider.sharedMesh = mesh;
            go.AddComponent<WalkableSurface>();

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
            Debug.Log($"[Level01BridgeWalkSurfaceSetup] Walk surface {cols}x{rows} vertices, " +
                $"deck z {bounds.min.z + zMin * Step:F2}..{bounds.min.z + zMax * Step:F2}, bounds {collider.bounds}.");
        }

        private static bool TryTerrainHeight(float x, float z, out float height)
        {
            foreach (Terrain terrain in Terrain.activeTerrains)
            {
                Vector3 o = terrain.transform.position, size = terrain.terrainData.size;
                if (x >= o.x && x <= o.x + size.x && z >= o.z && z <= o.z + size.z)
                {
                    height = o.y + terrain.SampleHeight(new Vector3(x, 0f, z));
                    return true;
                }
            }

            height = 0f;
            return false;
        }

        /// <summary>Highest deck hit within FillRadius of (x, z), ignoring railings - bridges the plank gaps.</summary>
        private static bool SampleDeck(List<Collider> colliders, float x, float z, float fromY, float railY, out float height)
        {
            height = float.MinValue;
            bool found = false;
            const int n = 2;
            for (int a = -n; a <= n; a++)
            {
                for (int b = -n; b <= n; b++)
                {
                    var ray = new Ray(new Vector3(x + a * FillRadius / n, fromY, z + b * FillRadius / n), Vector3.down);
                    foreach (Collider c in colliders)
                    {
                        if (c.Raycast(ray, out RaycastHit hit, 50f) && hit.point.y < railY && hit.point.y > height)
                        {
                            height = hit.point.y;
                            found = true;
                        }
                    }
                }
            }

            return found;
        }

        private static float NeighbourHeight(float[,] heights, bool[,] valid, int i, int j)
        {
            for (int d = 1; d < heights.GetLength(0); d++)
            {
                if (i - d >= 0 && valid[i - d, j])
                {
                    return heights[i - d, j];
                }

                if (i + d < heights.GetLength(0) && valid[i + d, j])
                {
                    return heights[i + d, j];
                }
            }

            return heights[i, j];
        }
    }
}
