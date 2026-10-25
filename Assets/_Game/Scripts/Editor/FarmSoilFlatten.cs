using System.Collections.Generic;
using AlienDefense.Environment;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AlienDefense.EditorTools
{
    /// <summary>Sinks the furrowed soil beds (PF_FarmPlot_*) of the crop fields flush with the ground (user,
    /// 2026-10-12: "I don't want the soil to stick up"): only the furrowed top stays visible, SoilTop above the
    /// terrain, and whatever grows on a bed comes down with it. Works in place on the open scene - nothing is
    /// rebuilt, so hand edits survive - and is safe to run again. The builders place new beds the same way
    /// (Level02Pickups / Level03CoastThemes use the same SoilTop).</summary>
    public static class FarmSoilFlatten
    {
        /// <summary>How far the bed's top stays above the ground: enough not to z-fight with the terrain.</summary>
        public const float SoilTop = 0.04f;

        [MenuItem("Tools/Tower Defense/Levels/Flatten Farm Soil Beds (open scene)")]
        public static void FlattenFromMenu()
        {
            Debug.Log(Flatten(SceneManager.GetActiveScene()));
        }

        public static string Flatten(Scene scene)
        {
            Terrain[] terrains = Terrain.activeTerrains;
            var beds = new List<Transform>();
            var crops = new List<TractorAbsorbableProp>();
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                {
                    if (t.name.StartsWith("PF_FarmPlot"))
                    {
                        beds.Add(t);
                    }
                }

                crops.AddRange(root.GetComponentsInChildren<TractorAbsorbableProp>(true));
            }

            int lowered = 0, cropsLowered = 0;
            foreach (Transform bed in beds)
            {
                Bounds before = Bounds(bed);
                // Highest ground under the bed (centre and corners), so no part of the top is buried on a gentle slope.
                float ground = Ground(terrains, before.center);
                foreach (float sx in new[] { -1f, 1f })
                {
                    foreach (float sz in new[] { -1f, 1f })
                    {
                        ground = Mathf.Max(ground, Ground(terrains, before.center + new Vector3(sx * before.extents.x, 0f, sz * before.extents.z)));
                    }
                }

                float delta = ground + SoilTop - before.max.y;
                if (Mathf.Abs(delta) < 0.005f)
                {
                    continue;
                }

                // Everything standing on this bed (it was lifted onto the raised soil) comes down to the new top.
                foreach (TractorAbsorbableProp crop in crops)
                {
                    Vector3 p = crop.transform.position;
                    if (p.x < before.min.x - 0.05f || p.x > before.max.x + 0.05f || p.z < before.min.z - 0.05f || p.z > before.max.z + 0.05f)
                    {
                        continue;
                    }

                    Bounds cb = Bounds(crop.transform);
                    float target = Ground(terrains, p) + SoilTop - 0.02f;
                    if (cb.min.y > target + 0.03f)
                    {
                        crop.transform.position += Vector3.up * (target - cb.min.y);
                        cropsLowered++;
                    }
                }

                bed.position += Vector3.up * delta;
                lowered++;
            }

            if (lowered > 0 || cropsLowered > 0)
            {
                EditorSceneManager.MarkSceneDirty(scene);
            }

            return "[FarmSoilFlatten] " + scene.name + ": " + lowered + " of " + beds.Count + " soil beds sunk flush with the ground, " + cropsLowered + " crops lowered onto them.";
        }

        private static Bounds Bounds(Transform t)
        {
            Renderer[] renderers = t.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
            {
                return new Bounds(t.position, Vector3.zero);
            }

            Bounds b = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
            {
                b.Encapsulate(renderers[i].bounds);
            }

            return b;
        }

        private static float Ground(Terrain[] terrains, Vector3 p)
        {
            foreach (Terrain terrain in terrains)
            {
                Vector3 o = terrain.transform.position;
                Vector3 s = terrain.terrainData.size;
                if (p.x >= o.x && p.x <= o.x + s.x && p.z >= o.z && p.z <= o.z + s.z)
                {
                    return o.y + terrain.SampleHeight(p);
                }
            }

            return p.y;
        }
    }
}
