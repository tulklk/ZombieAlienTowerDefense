using System.Collections.Generic;
using UnityEngine;

namespace AlienDefense.Common
{
    /// <summary>Marks a collider as ground that stands above the Terrain - e.g. a bridge deck over a river - so systems
    /// that otherwise follow Terrain only (the UFO's hover height, the tractor beam's ground ring) treat it as the
    /// floor instead of dropping to the riverbed underneath.
    ///
    /// Queried with Collider.Raycast against just the registered colliders, so it needs no layer changes and never
    /// picks up the buildings the UFO is meant to sail through. Enemies need no extra work: their ground probe
    /// already raycasts the Default layer this collider sits on.</summary>
    [RequireComponent(typeof(Collider))]
    public sealed class WalkableSurface : MonoBehaviour
    {
        private const float ProbeHeight = 50f;

        private static readonly List<WalkableSurface> ActiveSurfaces = new List<WalkableSurface>();

        private Collider _collider;

        private void Awake()
        {
            _collider = GetComponent<Collider>();
        }

        private void OnEnable()
        {
            if (_collider == null)
            {
                _collider = GetComponent<Collider>();
            }

            if (!ActiveSurfaces.Contains(this))
            {
                ActiveSurfaces.Add(this);
            }
        }

        private void OnDisable()
        {
            ActiveSurfaces.Remove(this);
        }

        /// <summary>Highest registered surface directly below/above <paramref name="point"/> (XZ only). False when
        /// no surface covers that spot.</summary>
        public static bool TryGetHeight(Vector3 point, out float height)
        {
            height = float.MinValue;
            bool found = false;

            for (int i = 0; i < ActiveSurfaces.Count; i++)
            {
                Collider surface = ActiveSurfaces[i]._collider;
                if (surface == null || !surface.enabled)
                {
                    continue;
                }

                Bounds bounds = surface.bounds;
                if (point.x < bounds.min.x || point.x > bounds.max.x || point.z < bounds.min.z || point.z > bounds.max.z)
                {
                    continue;
                }

                var ray = new Ray(new Vector3(point.x, bounds.max.y + ProbeHeight, point.z), Vector3.down);
                if (surface.Raycast(ray, out RaycastHit hit, ProbeHeight + bounds.size.y + 1f) && hit.point.y > height)
                {
                    height = hit.point.y;
                    found = true;
                }
            }

            return found;
        }
    }
}
