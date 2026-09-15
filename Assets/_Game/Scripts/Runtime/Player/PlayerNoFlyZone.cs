using System.Collections.Generic;
using UnityEngine;

namespace AlienDefense.Player
{
    /// <summary>A circular area (XZ) the player UFO may not enter - e.g. one link of the chain covering Level_01's
    /// river and waterfall. Enforced in PlayerMovement as a position constraint rather than with physics: the UFO
    /// deliberately collides with nothing (it sails through buildings), so a collider wall would need project-wide
    /// layer changes and would also catch tap raycasts and the tractor beam's overlap queries.
    ///
    /// Zones register themselves while enabled; overlapping circles form a smooth barrier the UFO slides along.
    ///
    /// Level_01 has ~480 of these and PlayerMovement asks every frame, so the zones are treated as static: their
    /// centres are copied into flat arrays once (whenever a zone is enabled/disabled) together with the bounding box of
    /// the whole set, and a query from outside that box - most of the map - returns without touching a single zone or
    /// Transform.</summary>
    public sealed class PlayerNoFlyZone : MonoBehaviour
    {
        [SerializeField, Min(0.1f)]
        private float _radius = 3f;

        private static readonly List<PlayerNoFlyZone> ActiveZones = new List<PlayerNoFlyZone>();

        private static float[] s_centreX = new float[0];
        private static float[] s_centreZ = new float[0];
        private static float[] s_radius = new float[0];
        private static float s_minX, s_maxX, s_minZ, s_maxZ, s_maxRadius;
        private static bool s_dirty = true;

        public float Radius
        {
            get => _radius;
            set
            {
                _radius = Mathf.Max(0.1f, value);
                s_dirty = true;
            }
        }

        private void OnEnable()
        {
            if (!ActiveZones.Contains(this))
            {
                ActiveZones.Add(this);
                s_dirty = true;
            }
        }

        private void OnDisable()
        {
            if (ActiveZones.Remove(this))
            {
                s_dirty = true;
            }
        }

        /// <summary>True when <paramref name="point"/> lies inside any active zone grown by <paramref name="margin"/>.</summary>
        public static bool IsInside(Vector3 point, float margin)
        {
            if (!PrepareQuery(point, margin))
            {
                return false;
            }

            for (int i = 0; i < s_centreX.Length; i++)
            {
                float r = s_radius[i] + margin;
                float dx = point.x - s_centreX[i], dz = point.z - s_centreZ[i];
                if (dx * dx + dz * dz < r * r)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>Returns <paramref name="target"/> pushed out of every active zone (each grown by
        /// <paramref name="margin"/>, e.g. the UFO's radius). Pushing radially lets movement along a chain of
        /// overlapping circles slide smoothly instead of snagging. Y is left untouched.</summary>
        public static Vector3 Resolve(Vector3 target, float margin)
        {
            if (!PrepareQuery(target, margin))
            {
                return target;
            }

            // a few passes settle the overlap between neighbouring circles
            for (int pass = 0; pass < 3; pass++)
            {
                bool moved = false;
                for (int i = 0; i < s_centreX.Length; i++)
                {
                    float r = s_radius[i] + margin;
                    float dx = target.x - s_centreX[i], dz = target.z - s_centreZ[i];
                    float sqr = dx * dx + dz * dz;
                    if (sqr >= r * r)
                    {
                        continue;
                    }

                    float distance = Mathf.Sqrt(sqr);
                    if (distance < 0.0001f)
                    {
                        dx = 1f;
                        dz = 0f;
                        distance = 1f;
                    }

                    target.x = s_centreX[i] + dx / distance * (r + 0.001f);
                    target.z = s_centreZ[i] + dz / distance * (r + 0.001f);
                    moved = true;
                }

                if (!moved)
                {
                    break;
                }
            }

            return target;
        }

        /// <summary>Rebuilds the cached centres if the set changed; false when the point is clear of every zone's
        /// reach, so the caller can skip the per-zone loop.</summary>
        private static bool PrepareQuery(Vector3 point, float margin)
        {
            if (s_dirty)
            {
                Rebuild();
            }

            if (s_centreX.Length == 0)
            {
                return false;
            }

            float reach = s_maxRadius + Mathf.Max(0f, margin);
            return point.x >= s_minX - reach && point.x <= s_maxX + reach && point.z >= s_minZ - reach && point.z <= s_maxZ + reach;
        }

        private static void Rebuild()
        {
            s_dirty = false;
            for (int i = ActiveZones.Count - 1; i >= 0; i--)
            {
                if (ActiveZones[i] == null)
                {
                    ActiveZones.RemoveAt(i);
                }
            }

            int count = ActiveZones.Count;
            if (s_centreX.Length != count)
            {
                s_centreX = new float[count];
                s_centreZ = new float[count];
                s_radius = new float[count];
            }

            s_minX = s_minZ = float.MaxValue;
            s_maxX = s_maxZ = float.MinValue;
            s_maxRadius = 0f;
            for (int i = 0; i < count; i++)
            {
                PlayerNoFlyZone zone = ActiveZones[i];
                Vector3 centre = zone.transform.position;
                s_centreX[i] = centre.x;
                s_centreZ[i] = centre.z;
                s_radius[i] = zone._radius;
                s_minX = Mathf.Min(s_minX, centre.x);
                s_maxX = Mathf.Max(s_maxX, centre.x);
                s_minZ = Mathf.Min(s_minZ, centre.z);
                s_maxZ = Mathf.Max(s_maxZ, centre.z);
                s_maxRadius = Mathf.Max(s_maxRadius, zone._radius);
            }
        }

        private void OnValidate()
        {
            s_dirty = true;
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.2f, 0.6f, 1f, 0.6f);
            Vector3 c = transform.position;
            const int segments = 24;
            for (int i = 0; i < segments; i++)
            {
                float a0 = i * Mathf.PI * 2f / segments, a1 = (i + 1) * Mathf.PI * 2f / segments;
                Gizmos.DrawLine(c + new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0)) * _radius, c + new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1)) * _radius);
            }
        }
    }
}
