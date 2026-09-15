using System.Collections.Generic;
using AlienDefense.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AlienDefense.EditorTools
{
    /// <summary>Builds Level_01's river/waterfall no-fly barrier as a dense field of small PlayerNoFlyZone circles
    /// that follows the visible water edge: across the river (which runs along Z here) it samples every 0.25 m,
    /// keeping only points inside the water meshes (RiverSegment_*, waterfall pool) where the terrain actually
    /// dips below the water surface, and lays small circles over those wet spans with their rims on the edge -
    /// so the UFO is stopped at the waterline rather than metres up the bank. The barrier continues past both
    /// ends of the river to the map edge, and the bridge is left open as the crossing. Safe to re-run.</summary>
    internal static class Level01RiverNoFlySetup
    {
        private const string ScenePath = "Assets/_Game/Scenes/Levels/Level_01.unity";
        private const float StepZ = 0.8f;
        private const float SampleX = 0.25f;
        private const float CircleRadius = 0.8f;
        private const float CircleSpacing = 0.8f;
        private const float EdgeInset = 0.55f;   // circle centre this far inside the waterline -> rim ~0.25 m past it
        private const float MinZ = 26f, MaxZ = 114f; // past both ends of the playable area

        [MenuItem("AlienDefense/Setup/Level 01/Build River No-Fly Zones")]
        private static void Run()
        {
            if (EditorSceneManager.GetActiveScene().path != ScenePath)
            {
                Debug.LogError("[Level01RiverNoFlySetup] Open Level_01 first.");
                return;
            }

            GameObject river = GameObject.Find("Maps/Environment/RiverSystem");
            GameObject road = GameObject.Find("Maps/ZombieRoad");
            if (river == null || road == null)
            {
                Debug.LogError("[Level01RiverNoFlySetup] RiverSystem or ZombieRoad not found.");
                return;
            }

            var triangles = new List<Vector3[]>();
            foreach (MeshFilter filter in river.GetComponentsInChildren<MeshFilter>())
            {
                string n = filter.name;
                if ((n.StartsWith("RiverSegment") || n.StartsWith("WaterfallPlungePool")) && filter.sharedMesh != null)
                {
                    CollectTriangles(filter, triangles);
                }
            }

            Rect crossing = default;
            GameObject bridge = GameObject.Find("Maps/ZombieRoad/PT_Wooden_Bridge_02");
            bool hasCrossing = bridge != null;
            if (hasCrossing)
            {
                Bounds b = WorldBounds(bridge);
                crossing = Rect.MinMaxRect(b.min.x - 3f, b.min.z + 0.1f, b.max.x + 3f, b.max.z - 0.1f);
            }

            Transform old = road.transform.Find("PlayerNoFlyZones");
            if (old != null)
            {
                Object.DestroyImmediate(old.gameObject);
            }

            var group = new GameObject("PlayerNoFlyZones").transform;
            group.SetParent(road.transform, false);

            // Wet span per Z slice.
            var slices = new List<(float z, float min, float max, bool real)>();
            for (float z = MinZ; z <= MaxZ; z += StepZ)
            {
                bool found = WetSpan(triangles, z, out float min, out float max);
                slices.Add((z, min, max, found));
            }

            FillGaps(slices);

            int count = 0;
            foreach (var s in slices)
            {
                if (float.IsNaN(s.min))
                {
                    continue;
                }

                float from = s.min + EdgeInset, to = s.max - EdgeInset;
                if (to < from)
                {
                    from = to = (s.min + s.max) * 0.5f;
                }

                int n = Mathf.Max(1, Mathf.CeilToInt((to - from) / CircleSpacing) + 1);
                for (int i = 0; i < n; i++)
                {
                    float x = n == 1 ? from : Mathf.Lerp(from, to, i / (float)(n - 1));
                    if (hasCrossing && s.z + CircleRadius > crossing.yMin && s.z - CircleRadius < crossing.yMax
                        && x > crossing.xMin && x < crossing.xMax)
                    {
                        continue; // the bridge
                    }

                    var go = new GameObject("NoFly_River_" + (++count).ToString("0000"));
                    go.transform.SetParent(group, false);
                    go.transform.position = new Vector3(x, 3f, s.z);
                    go.AddComponent<PlayerNoFlyZone>().Radius = CircleRadius;
                }
            }

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
            Debug.Log($"[Level01RiverNoFlySetup] {count} no-fly circles along the waterline; crossing left open at the bridge {crossing}.");
        }

        /// <summary>X extent of the visible water at this Z: inside a water triangle and the terrain lower than
        /// that triangle's surface.</summary>
        private static bool WetSpan(List<Vector3[]> triangles, float z, out float min, out float max)
        {
            min = float.MaxValue;
            max = float.MinValue;

            // candidate X range from the meshes first
            float lo = float.MaxValue, hi = float.MinValue;
            foreach (Vector3[] t in triangles)
            {
                float tMinZ = Mathf.Min(t[0].z, t[1].z, t[2].z), tMaxZ = Mathf.Max(t[0].z, t[1].z, t[2].z);
                if (z < tMinZ || z > tMaxZ)
                {
                    continue;
                }

                lo = Mathf.Min(lo, Mathf.Min(t[0].x, t[1].x, t[2].x));
                hi = Mathf.Max(hi, Mathf.Max(t[0].x, t[1].x, t[2].x));
            }

            if (hi < lo)
            {
                return false;
            }

            for (float x = lo; x <= hi; x += SampleX)
            {
                if (!SurfaceAt(triangles, x, z, out float surfaceY))
                {
                    continue;
                }

                float ground = GroundHeight(x, z);
                if (float.IsNaN(ground) || ground > surfaceY - 0.02f)
                {
                    continue; // bank above the waterline
                }

                min = Mathf.Min(min, x);
                max = Mathf.Max(max, x);
            }

            return max >= min;
        }

        private static bool SurfaceAt(List<Vector3[]> triangles, float x, float z, out float y)
        {
            var p = new Vector2(x, z);
            foreach (Vector3[] t in triangles)
            {
                Vector2 a = new Vector2(t[0].x, t[0].z), b = new Vector2(t[1].x, t[1].z), c = new Vector2(t[2].x, t[2].z);
                float d = (b.y - c.y) * (a.x - c.x) + (c.x - b.x) * (a.y - c.y);
                if (Mathf.Abs(d) < 1e-6f)
                {
                    continue;
                }

                float w1 = ((b.y - c.y) * (p.x - c.x) + (c.x - b.x) * (p.y - c.y)) / d;
                float w2 = ((c.y - a.y) * (p.x - c.x) + (a.x - c.x) * (p.y - c.y)) / d;
                float w3 = 1f - w1 - w2;
                if (w1 < -0.001f || w2 < -0.001f || w3 < -0.001f)
                {
                    continue;
                }

                y = w1 * t[0].y + w2 * t[1].y + w3 * t[2].y;
                return true;
            }

            y = 0f;
            return false;
        }

        private static float GroundHeight(float x, float z)
        {
            foreach (Terrain terrain in Terrain.activeTerrains)
            {
                Vector3 o = terrain.transform.position, size = terrain.terrainData.size;
                if (x >= o.x && x <= o.x + size.x && z >= o.z && z <= o.z + size.z)
                {
                    return o.y + terrain.SampleHeight(new Vector3(x, 0f, z));
                }
            }

            return float.NaN;
        }

        /// <summary>Slices with no water (beyond the ends of the river) copy the nearest wet slice, so the
        /// barrier reaches the map edge instead of leaving a way round.</summary>
        private static void FillGaps(List<(float z, float min, float max, bool real)> slices)
        {
            for (int i = 0; i < slices.Count; i++)
            {
                if (slices[i].real)
                {
                    continue;
                }

                int nearest = -1;
                for (int d = 1; d < slices.Count && nearest < 0; d++)
                {
                    if (i - d >= 0 && slices[i - d].real)
                    {
                        nearest = i - d;
                    }
                    else if (i + d < slices.Count && slices[i + d].real)
                    {
                        nearest = i + d;
                    }
                }

                // Only extend past the ends of the river - a dry slice in the middle (e.g. under the bridge deck)
                // must stay open.
                bool beyondEnds = nearest >= 0 && (AllDry(slices, 0, i) || AllDry(slices, i, slices.Count - 1));
                slices[i] = beyondEnds
                    ? (slices[i].z, slices[nearest].min, slices[nearest].max, false)
                    : (slices[i].z, float.NaN, float.NaN, false);
            }
        }

        private static bool AllDry(List<(float z, float min, float max, bool real)> slices, int from, int to)
        {
            for (int i = from; i <= to; i++)
            {
                if (slices[i].real)
                {
                    return false;
                }
            }

            return true;
        }

        private static void CollectTriangles(MeshFilter filter, List<Vector3[]> triangles)
        {
            Vector3[] vertices = filter.sharedMesh.vertices;
            int[] indices = filter.sharedMesh.triangles;
            Matrix4x4 m = filter.transform.localToWorldMatrix;
            for (int i = 0; i < indices.Length; i += 3)
            {
                triangles.Add(new[]
                {
                    m.MultiplyPoint3x4(vertices[indices[i]]),
                    m.MultiplyPoint3x4(vertices[indices[i + 1]]),
                    m.MultiplyPoint3x4(vertices[indices[i + 2]])
                });
            }
        }

        private static Bounds WorldBounds(GameObject go)
        {
            Renderer[] renderers = go.GetComponentsInChildren<Renderer>();
            Bounds b = renderers[0].bounds;
            foreach (Renderer r in renderers)
            {
                b.Encapsulate(r.bounds);
            }

            return b;
        }
    }
}
