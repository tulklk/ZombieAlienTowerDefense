using System.Collections.Generic;
using UnityEngine;

namespace AlienDefense.Occlusion
{
    /// <summary>MARKER ONLY: tags a piece of scenery (building, large tree, rock, cliff prop) that can stand between the
    /// camera and the player UFO. It never touches its own renderers, materials or colours - when it hides the UFO it
    /// is the UFO that fades (see UFOOcclusionFade), the scenery stays exactly as authored.
    ///
    /// Put on the object's ROOT. Registered in a static list while enabled so PlayerOcclusionController can test it
    /// without colliders or any per-frame searching. Its box is baked in its own space in the editor (BakeLocalBounds),
    /// so a rotated building is tested as the rotated box it is, not as the larger world-aligned box around it.</summary>
    [DisallowMultipleComponent]
    public sealed class OcclusionObject : MonoBehaviour
    {
        private static readonly List<OcclusionObject> RegisteredObjects = new List<OcclusionObject>(256);

        [SerializeField, HideInInspector]
        private Bounds _localBounds;

        [SerializeField, HideInInspector]
        private bool _hasLocalBounds;

        private Renderer[] _renderers; // only read for bounds when no box was baked - never modified
        private int _registryIndex = -1;
        private Vector3 _sphereCenter;
        private float _sphereRadius;
        private bool _boundsDirty = true;

        /// <summary>Every enabled OcclusionObject in the loaded scenes.</summary>
        public static IReadOnlyList<OcclusionObject> Registered => RegisteredObjects;

        private void Awake()
        {
            if (!_hasLocalBounds)
            {
                var found = new List<Renderer>();
                GetComponentsInChildren(true, found);
                found.RemoveAll(r => r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer || r is BillboardRenderer);
                _renderers = found.ToArray();
            }
        }

        private void OnEnable()
        {
            _registryIndex = RegisteredObjects.Count;
            RegisteredObjects.Add(this);
            _boundsDirty = true;
        }

        private void OnDisable()
        {
            if (_registryIndex >= 0 && _registryIndex < RegisteredObjects.Count && RegisteredObjects[_registryIndex] == this)
            {
                // Swap-remove: O(1), the moved object learns its new index.
                int last = RegisteredObjects.Count - 1;
                OcclusionObject moved = RegisteredObjects[last];
                RegisteredObjects[_registryIndex] = moved;
                moved._registryIndex = _registryIndex;
                RegisteredObjects.RemoveAt(last);
            }
            else
            {
                RegisteredObjects.Remove(this);
            }

            _registryIndex = -1;
        }

        /// <summary>Recomputes the cached bounding sphere when the object moved (e.g. a tree being tractor-beamed).</summary>
        internal void RefreshBoundsIfMoved()
        {
            if (!_boundsDirty && !transform.hasChanged)
            {
                return;
            }

            transform.hasChanged = false;
            _boundsDirty = false;
            if (_hasLocalBounds)
            {
                Vector3 scale = transform.lossyScale;
                float maxScale = Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z));
                _sphereCenter = transform.TransformPoint(_localBounds.center);
                _sphereRadius = _localBounds.extents.magnitude * maxScale;
                return;
            }

            bool any = false;
            Bounds combined = default;
            for (int i = 0; _renderers != null && i < _renderers.Length; i++)
            {
                Renderer r = _renderers[i];
                if (r == null)
                {
                    continue;
                }

                if (!any)
                {
                    combined = r.bounds;
                    any = true;
                }
                else
                {
                    combined.Encapsulate(r.bounds);
                }
            }

            _sphereCenter = combined.center;
            _sphereRadius = any ? combined.extents.magnitude : 0f;
        }

        /// <summary>True when this object stands between <paramref name="origin"/> (the camera) and
        /// <paramref name="target"/> (the UFO's centre): the line of sight, swept by <paramref name="castRadius"/>,
        /// enters the object before reaching the UFO - or the UFO is inside / touching it without the object being
        /// behind the UFO. An object behind or beside the UFO never counts.</summary>
        internal bool Occludes(Vector3 origin, Vector3 direction, float targetDistance, Vector3 target, float castRadius, float insideRadius)
        {
            if (_sphereRadius <= 0f)
            {
                return false;
            }

            // Coarse: bounding sphere vs the segment origin -> target.
            float along = Mathf.Clamp(Vector3.Dot(_sphereCenter - origin, direction), 0f, targetDistance);
            Vector3 nearest = origin + direction * along;
            float reach = _sphereRadius + Mathf.Max(castRadius, insideRadius);
            if ((_sphereCenter - nearest).sqrMagnitude > reach * reach)
            {
                return false;
            }

            if (_hasLocalBounds)
            {
                return OccludesBox(transform, _localBounds, origin, targetDistance, target, castRadius, insideRadius);
            }

            for (int i = 0; _renderers != null && i < _renderers.Length; i++)
            {
                Renderer r = _renderers[i];
                if (r == null || !r.enabled || !r.gameObject.activeInHierarchy)
                {
                    continue;
                }

                Bounds bounds = r.bounds;
                Vector3 closest = bounds.ClosestPoint(target);
                if ((closest - target).sqrMagnitude <= insideRadius * insideRadius && (closest - origin).magnitude <= targetDistance + 0.1f)
                {
                    return true;
                }

                bounds.Expand(castRadius * 2f);
                if (bounds.IntersectRay(new Ray(origin, direction), out float distance) && distance < targetDistance)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>The same two tests against a box given in <paramref name="space"/>'s local coordinates.</summary>
        private static bool OccludesBox(Transform space, Bounds box, Vector3 origin, float targetDistance, Vector3 target, float castRadius, float insideRadius)
        {
            Vector3 localOrigin = space.InverseTransformPoint(origin);
            Vector3 localTarget = space.InverseTransformPoint(target);

            // Inside / touching, and not behind the UFO.
            Vector3 closest = space.TransformPoint(box.ClosestPoint(localTarget));
            if ((closest - target).sqrMagnitude <= insideRadius * insideRadius && (closest - origin).magnitude <= targetDistance + 0.1f)
            {
                return true;
            }

            // In front: the line of sight, swept by castRadius, enters the box before reaching the UFO.
            Vector3 scale = space.lossyScale;
            float minScale = Mathf.Max(0.0001f, Mathf.Min(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z)));
            box.Expand(2f * castRadius / minScale);
            Vector3 localDirection = localTarget - localOrigin;
            float localLength = localDirection.magnitude;
            if (localLength < 0.0001f || !box.IntersectRay(new Ray(localOrigin, localDirection / localLength), out float localDistance))
            {
                return false;
            }

            Vector3 entry = space.TransformPoint(localOrigin + localDirection / localLength * localDistance);
            return (entry - origin).magnitude < targetDistance;
        }

#if UNITY_EDITOR
        /// <summary>Editor only: measures every renderer into this root's local space and stores the box (before any
        /// static batching merges the meshes). Called by the occlusion setup tool and when the component is added.</summary>
        public void BakeLocalBounds()
        {
            bool any = false;
            Bounds box = default;
            Matrix4x4 toRoot = transform.worldToLocalMatrix;
            foreach (Renderer r in GetComponentsInChildren<Renderer>(true))
            {
                if (r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer || r is BillboardRenderer)
                {
                    continue;
                }

                Bounds local = r.localBounds;
                Matrix4x4 toRootFromRenderer = toRoot * r.transform.localToWorldMatrix;
                for (int corner = 0; corner < 8; corner++)
                {
                    Vector3 p = local.center + Vector3.Scale(local.extents, new Vector3(
                        (corner & 1) == 0 ? -1f : 1f, (corner & 2) == 0 ? -1f : 1f, (corner & 4) == 0 ? -1f : 1f));
                    Vector3 q = toRootFromRenderer.MultiplyPoint3x4(p);
                    if (!any)
                    {
                        box = new Bounds(q, Vector3.zero);
                        any = true;
                    }
                    else
                    {
                        box.Encapsulate(q);
                    }
                }
            }

            _localBounds = box;
            _hasLocalBounds = any;
        }

        private void Reset()
        {
            BakeLocalBounds();
        }
#endif
    }
}
