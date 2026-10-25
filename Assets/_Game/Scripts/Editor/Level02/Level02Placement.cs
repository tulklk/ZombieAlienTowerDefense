using System.Collections.Generic;
using AlienDefense.Environment;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace AlienDefense.EditorTools
{
    /// <summary>Placement engine for Level_02 decoration. Every placement goes through the same rules:
    /// movement lanes (roads, plazas, the base and landing pad) stay clear, BuildNodes keep a ring of space, tall
    /// objects may not stand on the camera side of the enemy road or a BuildNode (the camera looks down -X, so a tall
    /// object at higher X hides what is behind it), and an occupancy grid keeps props from interpenetrating.
    /// Decoration inside the playable area is absorbable by the tractor beam like Level_01's (buildings, fences,
    /// fields and cliff dressing are not); nothing gets a collider.</summary>
    public static partial class Level02Builder
    {
        private enum Cat { Tree, Bush, Flower, Grass, Rock, Prop, Fence, Building, Animal, Vehicle, Field, CliffDressing }

        private const float OccCell = 0.5f;
        private static readonly Rect OccArea = Rect.MinMaxRect(-80f, -56f, 80f, 56f); // covers the widened rim (Z 47) + ledges
        private static int _occW;
        private static int _occH;
        private static byte[] _occupancy; // 0 free, 1 soft (props), 2 hard (buildings, trunks, big rocks)
        private static readonly List<Vector3> TreeCanopies = new List<Vector3>(); // x, z, radius
        private static readonly Dictionary<string, Bounds> KitBounds = new Dictionary<string, Bounds>();
        private static readonly Dictionary<Cat, int> PlacedCount = new Dictionary<Cat, int>();
        private static Terrain _terrain;

        private static void InitPlacement(Terrain terrain)
        {
            _terrain = terrain;
            _occW = Mathf.CeilToInt(OccArea.width / OccCell);
            _occH = Mathf.CeilToInt(OccArea.height / OccCell);
            _occupancy = new byte[_occW * _occH];
            TreeCanopies.Clear();
            PlacedCount.Clear();
            KitBounds.Clear();
        }

        private static float Rand(float min, float max) => min + (float)_rng.NextDouble() * (max - min);

        private static bool Chance(float p) => _rng.NextDouble() < p;

        private static T Pick<T>(params T[] items) => items[_rng.Next(items.Length)];

        private static float TerrainY(Vector2 xz) => _terrain.transform.position.y + _terrain.SampleHeight(new Vector3(xz.x, 0f, xz.y));

        /// <summary>Local-space bounds of a kit prefab (measured once on a temporary instance).</summary>
        private static Bounds KitLocalBounds(string name)
        {
            if (KitBounds.TryGetValue(name, out Bounds cached))
            {
                return cached;
            }

            Bounds b = new Bounds(Vector3.zero, Vector3.zero);
            if (Kit.TryGetValue(name, out GameObject prefab) && prefab != null)
            {
                var temp = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                temp.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                bool any = false;
                foreach (Renderer r in temp.GetComponentsInChildren<Renderer>())
                {
                    if (r is ParticleSystemRenderer)
                    {
                        continue;
                    }

                    if (!any)
                    {
                        b = r.bounds;
                        any = true;
                    }
                    else
                    {
                        b.Encapsulate(r.bounds);
                    }
                }

                Object.DestroyImmediate(temp);
            }

            KitBounds[name] = b;
            return b;
        }

        // ------------------------------------------------------------------ rules

        private static string Blocker(Vector2 p, float radius, float height, Cat cat)
        {
            bool tiny = cat == Cat.Flower || cat == Cat.Grass;
            bool cliff = cat == Cat.CliffDressing;
            if (cliff)
            {
                return null;
            }

            // Roads: tiny vegetation may line the edge; everything else keeps off the dirt.
            float edge = RoadEdge(p);
            float roadClear = tiny ? 0.1f : cat == Cat.Tree ? 1.2f : cat == Cat.Bush ? 0.7f : 0.45f;
            if (edge - radius < roadClear)
            {
                return "road";
            }

            foreach (Clearing c in Clearings)
            {
                if (!c.Movement)
                {
                    continue;
                }

                float d = ClearingDistance(c, p);
                float limit = tiny ? 0.9f : 1.05f + radius / Mathf.Min(c.Radii.x, c.Radii.y);
                if (d < limit)
                {
                    return "plaza";
                }
            }

            foreach (Vector2 node in BuildNodeXZ)
            {
                if (Vector2.Distance(node, p) - radius < (tiny ? 2.6f : BuildNodeClearRadius))
                {
                    return "buildnode";
                }
            }

            Vector2 landing = BaseTargetXZ + LandingOffset;
            Vector2 baseModel = BaseTargetXZ + PlayerBaseOffset;
            if (Vector2.Distance(p, landing) - radius < (tiny ? 3.5f : 5.5f) || Vector2.Distance(p, baseModel) - radius < (tiny ? 4f : 6.5f))
            {
                return "base";
            }

            // Camera occlusion: tall things may not stand just camera-side (+X) of the road or a BuildNode.
            if (height > 2.3f)
            {
                float shadow = height * 0.84f + radius;
                for (int i = 0; i < _mainRoadSamples.Count; i += 2)
                {
                    Vector2 r = _mainRoadSamples[i];
                    float dx = p.x - r.x;
                    if (dx > 0f && dx < shadow && Mathf.Abs(p.y - r.y) < radius + MainRoadWidth * 0.5f + 0.4f)
                    {
                        return "occludes-road";
                    }
                }

                foreach (Vector2 node in BuildNodeXZ)
                {
                    float dx = p.x - node.x;
                    if (dx > 0f && dx < shadow + 2f && Mathf.Abs(p.y - node.y) < radius + 2.2f)
                    {
                        return "occludes-node";
                    }
                }
            }

            return null;
        }

        private static bool Occupied(Vector2 p, float radius, bool hardOnly)
        {
            int x0 = Mathf.FloorToInt((p.x - radius - OccArea.xMin) / OccCell);
            int x1 = Mathf.FloorToInt((p.x + radius - OccArea.xMin) / OccCell);
            int z0 = Mathf.FloorToInt((p.y - radius - OccArea.yMin) / OccCell);
            int z1 = Mathf.FloorToInt((p.y + radius - OccArea.yMin) / OccCell);
            for (int z = Mathf.Max(0, z0); z <= Mathf.Min(_occH - 1, z1); z++)
            {
                for (int x = Mathf.Max(0, x0); x <= Mathf.Min(_occW - 1, x1); x++)
                {
                    float cx = OccArea.xMin + (x + 0.5f) * OccCell;
                    float cz = OccArea.yMin + (z + 0.5f) * OccCell;
                    if ((cx - p.x) * (cx - p.x) + (cz - p.y) * (cz - p.y) > radius * radius)
                    {
                        continue;
                    }

                    byte v = _occupancy[z * _occW + x];
                    if (v == 2 || (!hardOnly && v == 1))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static void Mark(Vector2 p, float radius, byte level)
        {
            int x0 = Mathf.FloorToInt((p.x - radius - OccArea.xMin) / OccCell);
            int x1 = Mathf.FloorToInt((p.x + radius - OccArea.xMin) / OccCell);
            int z0 = Mathf.FloorToInt((p.y - radius - OccArea.yMin) / OccCell);
            int z1 = Mathf.FloorToInt((p.y + radius - OccArea.yMin) / OccCell);
            for (int z = Mathf.Max(0, z0); z <= Mathf.Min(_occH - 1, z1); z++)
            {
                for (int x = Mathf.Max(0, x0); x <= Mathf.Min(_occW - 1, x1); x++)
                {
                    float cx = OccArea.xMin + (x + 0.5f) * OccCell;
                    float cz = OccArea.yMin + (z + 0.5f) * OccCell;
                    if ((cx - p.x) * (cx - p.x) + (cz - p.y) * (cz - p.y) <= radius * radius)
                    {
                        int i = z * _occW + x;
                        _occupancy[i] = (byte)Mathf.Max(_occupancy[i], level);
                    }
                }
            }
        }

        /// <summary>Marks an oriented rectangle (a building footprint) hard-occupied.</summary>
        private static void MarkRect(Vector2 center, Vector2 halfSize, float yaw, byte level)
        {
            float reach = halfSize.magnitude;
            int x0 = Mathf.FloorToInt((center.x - reach - OccArea.xMin) / OccCell);
            int x1 = Mathf.FloorToInt((center.x + reach - OccArea.xMin) / OccCell);
            int z0 = Mathf.FloorToInt((center.y - reach - OccArea.yMin) / OccCell);
            int z1 = Mathf.FloorToInt((center.y + reach - OccArea.yMin) / OccCell);
            for (int z = Mathf.Max(0, z0); z <= Mathf.Min(_occH - 1, z1); z++)
            {
                for (int x = Mathf.Max(0, x0); x <= Mathf.Min(_occW - 1, x1); x++)
                {
                    var c = new Vector2(OccArea.xMin + (x + 0.5f) * OccCell, OccArea.yMin + (z + 0.5f) * OccCell);
                    Vector2 local = Rotate(c - center, yaw);
                    if (Mathf.Abs(local.x) <= halfSize.x && Mathf.Abs(local.y) <= halfSize.y)
                    {
                        int i = z * _occW + x;
                        _occupancy[i] = (byte)Mathf.Max(_occupancy[i], level);
                    }
                }
            }
        }

        // ------------------------------------------------------------------ placement

        /// <summary>Places a kit prefab if the rules allow it. Returns null (and places nothing) otherwise.
        /// <paramref name="force"/> skips the rules (art-directed landmark placement) but still marks occupancy.</summary>
        private static GameObject Place(string name, Transform parent, Vector2 xz, float yaw, float scale, Cat cat, bool force = false)
        {
            if (!Kit.TryGetValue(name, out GameObject prefab) || prefab == null)
            {
                return null;
            }

            Bounds lb = KitLocalBounds(name);
            float footprint = Mathf.Max(lb.size.x, lb.size.z) * 0.5f * scale;
            float height = lb.size.y * scale;
            bool tiny = cat == Cat.Flower || cat == Cat.Grass;
            float occRadius = cat == Cat.Tree ? Mathf.Min(0.6f, footprint * 0.3f) : cat == Cat.Bush ? footprint * 0.6f : tiny ? 0.2f : footprint * 0.75f;

            if (!force && (cat == Cat.Tree || height > 2.3f))
            {
                // A canopy or roof may not hang over a BuildNode ring: the node must read clearly from the camera.
                foreach (Vector2 node in BuildNodeXZ)
                {
                    if (Vector2.Distance(node, xz) - footprint < BuildNodeRingRadius + 0.6f)
                    {
                        Reject("buildnode-canopy");
                        return null;
                    }
                }
            }

            if (!force)
            {
                string reason = Blocker(xz, cat == Cat.Tree ? footprint * 0.45f : footprint * 0.8f, height, cat);
                if (reason != null)
                {
                    Reject(reason);
                    return null;
                }

                if (Occupied(xz, occRadius, tiny))
                {
                    Reject("occupied");
                    return null;
                }

                if (cat == Cat.Tree)
                {
                    float canopy = footprint * 0.75f;
                    foreach (Vector3 t in TreeCanopies)
                    {
                        if (Vector2.Distance(new Vector2(t.x, t.y), xz) < (canopy + t.z) * 0.72f)
                        {
                            Reject("canopy");
                            return null;
                        }
                    }
                }
            }

            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            float sink = cat == Cat.Tree || cat == Cat.Building ? 0.12f : cat == Cat.Field ? 0.02f : 0.04f;
            float y = TerrainY(xz);
            if (cat == Cat.Building || cat == Cat.Field || cat == Cat.Fence || cat == Cat.Vehicle)
            {
                // Seat on the lowest point under the footprint so no corner floats.
                float r = Mathf.Max(0.3f, footprint * 0.8f);
                for (int i = 0; i < 8; i++)
                {
                    float a = i * Mathf.PI * 0.25f;
                    y = Mathf.Min(y, TerrainY(xz + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r));
                }
            }

            go.transform.SetPositionAndRotation(new Vector3(xz.x, y - sink, xz.y), Quaternion.Euler(0f, yaw, 0f));
            go.transform.localScale = Vector3.one * scale;

            if (cat == Cat.Building)
            {
                MarkRect(xz + Rotate(new Vector2(lb.center.x, lb.center.z) * scale, -yaw), new Vector2(lb.size.x * 0.5f * scale + 0.3f, lb.size.z * 0.5f * scale + 0.3f), yaw, 2);
            }
            else if (!tiny)
            {
                Mark(xz, occRadius, cat == Cat.Tree || cat == Cat.Rock || cat == Cat.Fence || cat == Cat.Vehicle ? (byte)2 : (byte)1);
            }

            if (cat == Cat.Tree || cat == Cat.CliffDressing && height > 2f)
            {
                TreeCanopies.Add(new Vector3(xz.x, xz.y, footprint * 0.75f));
            }

            Finish(go, cat, height, xz);
            PlacedCount[cat] = PlacedCount.TryGetValue(cat, out int n) ? n + 1 : 1;
            return go;
        }

        private static readonly Dictionary<string, int> Rejections = new Dictionary<string, int>();

        private static void Reject(string reason) => Rejections[reason] = Rejections.TryGetValue(reason, out int n) ? n + 1 : 1;

        private static void Finish(GameObject go, Cat cat, float height, Vector2 xz)
        {
            bool castShadows = cat == Cat.Building || cat == Cat.Tree || cat == Cat.Vehicle || cat == Cat.Animal ||
                ((cat == Cat.Prop || cat == Cat.Rock || cat == Cat.Bush) && height > 0.9f);
            foreach (Renderer renderer in go.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer is ParticleSystemRenderer)
                {
                    continue;
                }

                renderer.shadowCastingMode = castShadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
                renderer.receiveShadows = cat != Cat.Grass && cat != Cat.Flower;
                renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
                renderer.lightProbeUsage = LightProbeUsage.Off;
            }

            bool isStatic = cat == Cat.Building || cat == Cat.Fence || cat == Cat.Field || cat == Cat.CliffDressing;
            bool inside = InsidePlayable(xz, -0.5f);
            if (isStatic || !inside)
            {
                // Out of the UFO's reach (or structural): not absorbable, so it can be batched as static.
                foreach (TractorAbsorbableProp prop in go.GetComponentsInChildren<TractorAbsorbableProp>(true))
                {
                    Object.DestroyImmediate(prop);
                }

                bool hasMover = go.GetComponentInChildren<AmbientSpinner>(true) != null || go.GetComponentInChildren<Animator>(true) != null
                    || go.GetComponentInChildren<ParticleSystem>(true) != null;
                if (!hasMover)
                {
                    foreach (Transform t in go.GetComponentsInChildren<Transform>(true))
                    {
                        GameObjectUtility.SetStaticEditorFlags(t.gameObject, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic);
                    }
                }

                return;
            }

            // Everything else inside the playable area keeps the kit prefab's TractorAbsorbableProp (XP only, as in
            // Level_01).
        }

        /// <summary>Tries a few jittered spots around <paramref name="around"/> until one passes the rules.</summary>
        private static GameObject PlaceNear(string name, Transform parent, Vector2 around, float spread, Cat cat, float scaleMin = 0.9f, float scaleMax = 1.1f, int attempts = 6, float? yaw = null)
        {
            for (int i = 0; i < attempts; i++)
            {
                Vector2 p = around + (i == 0 && spread < 0.01f ? Vector2.zero : Random2(spread));
                GameObject go = Place(name, parent, p, yaw ?? Rand(0f, 360f), Rand(scaleMin, scaleMax), cat);
                if (go != null)
                {
                    return go;
                }
            }

            return null;
        }

        private static Vector2 Random2(float radius)
        {
            float a = Rand(0f, Mathf.PI * 2f);
            float r = radius * Mathf.Sqrt((float)_rng.NextDouble());
            return new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
        }

        /// <summary>A run of fence segments from <paramref name="from"/> to <paramref name="to"/>, stretched to fit
        /// exactly; gaps where <paramref name="gapChance"/> hits (for a used, lived-in look).</summary>
        private static int FenceRun(Transform parent, string name, Vector2 from, Vector2 to, float gapChance, bool force = false)
        {
            Bounds lb = KitLocalBounds(name);
            float segmentLength = Mathf.Max(0.5f, lb.size.x);
            float length = Vector2.Distance(from, to);
            int count = Mathf.Max(1, Mathf.RoundToInt(length / segmentLength));
            float step = length / count;
            Vector2 dir = (to - from).normalized;
            float yaw = -Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            // Decide the gaps first, then drop lone segments stranded between two gaps (they read as litter).
            var keep = new bool[count];
            for (int i = 0; i < count; i++)
            {
                keep[i] = !(gapChance > 0f && Chance(gapChance));
            }

            for (int i = 0; i < count; i++)
            {
                bool leftGap = i == 0 || !keep[i - 1];
                bool rightGap = i == count - 1 || !keep[i + 1];
                if (count > 2 && keep[i] && leftGap && rightGap)
                {
                    keep[i] = false;
                }
            }

            int placed = 0;
            for (int i = 0; i < count; i++)
            {
                if (!keep[i])
                {
                    continue;
                }

                Vector2 mid = from + dir * (step * (i + 0.5f));
                if (!force)
                {
                    string reason = Blocker(mid, 0.3f, 1f, Cat.Fence);
                    if (reason != null)
                    {
                        Reject("fence-" + reason);
                        continue;
                    }

                    if (Occupied(mid, 0.3f, true))
                    {
                        Reject("fence-occupied"); // never through a building, trunk, big rock or parked vehicle
                        continue;
                    }
                }

                var go = (GameObject)PrefabUtility.InstantiatePrefab(Kit[name], parent);
                Vector3 offset = Quaternion.Euler(0f, yaw, 0f) * new Vector3(lb.center.x * (step / segmentLength), 0f, lb.center.z);
                float y = Mathf.Min(TerrainY(mid - dir * step * 0.5f), TerrainY(mid + dir * step * 0.5f));
                go.transform.SetPositionAndRotation(new Vector3(mid.x, y - 0.05f, mid.y) - offset, Quaternion.Euler(0f, yaw + Rand(-1.5f, 1.5f), 0f));
                go.transform.localScale = new Vector3(step / segmentLength, 1f, 1f);
                Mark(mid, 0.35f, 1);
                Finish(go, Cat.Fence, lb.size.y, mid);
                PlacedCount[Cat.Fence] = PlacedCount.TryGetValue(Cat.Fence, out int n) ? n + 1 : 1;
                placed++;
            }

            return placed;
        }
    }
}
