using System.Collections.Generic;
using UnityEngine;

namespace AlienDefense.Enemies
{
    /// <summary>Fixed Scene sequence of waypoints enemies walk along, on the XZ plane.</summary>
    public sealed class EnemyPath3D : MonoBehaviour
    {
        [SerializeField]
        private Transform[] _waypoints;

        [SerializeField, Min(0f)]
        [Tooltip("Half the usable width of the road this path runs down the middle of. Each enemy picks a fixed " +
            "sideways lane inside +/- this and keeps it the whole way, so a wave fans out across the road instead " +
            "of marching single file. Keep it a little inside the painted road edge so enemy bodies don't hang " +
            "over the verge. 0 = every enemy on the centre line.")]
        private float _laneHalfWidth;

        public int Count => _waypoints?.Length ?? 0;

        public float LaneHalfWidth => _laneHalfWidth;

        /// <summary>Read-only access to the underlying waypoint Transforms, in path order. Purely additive (no
        /// existing member changed) - added so other systems (e.g. the minimap) can reuse this path's own data
        /// instead of keeping a second waypoint list. Never mutate the returned array's contents.</summary>
        public IReadOnlyList<Transform> Waypoints => _waypoints;

        public Vector3 GetPoint(int index)
        {
            return _waypoints[index].position;
        }

        /// <summary>Waypoint <paramref name="index"/> pushed sideways by <paramref name="lateralOffset"/> metres
        /// (positive = right of the direction of travel) - one point of a lane running parallel to the centre
        /// line.
        ///
        /// At a bend the push is along the bisector of the two segments meeting there and stretched by
        /// 1/cos(half the turn angle) (a mitre join), which is what keeps the lane the same distance from BOTH
        /// segments; a plain per-segment perpendicular would make every enemy on the outside of a bend cut the
        /// corner and the ones on the inside overshoot it. The stretch is capped so a hairpin can't fling the
        /// point far off the road. Y is left at the waypoint's own height - enemies ground-snap as they walk.</summary>
        public Vector3 GetLanePoint(int index, float lateralOffset)
        {
            Vector3 point = _waypoints[index].position;
            if (Mathf.Approximately(lateralOffset, 0f) || Count < 2)
            {
                return point;
            }

            Vector3 incoming = index > 0 ? FlatDirection(_waypoints[index - 1].position, point) : Vector3.zero;
            Vector3 outgoing = index < Count - 1 ? FlatDirection(point, _waypoints[index + 1].position) : Vector3.zero;

            Vector3 tangent = incoming + outgoing;
            if (tangent.sqrMagnitude < 0.0001f)
            {
                // An end point (only one neighbour), or a perfect U-turn where the two directions cancel out.
                tangent = outgoing.sqrMagnitude > 0f ? outgoing : incoming;
            }

            tangent.Normalize();
            Vector3 lateral = Vector3.Cross(Vector3.up, tangent);

            Vector3 reference = incoming.sqrMagnitude > 0f ? incoming : outgoing;
            float cosHalfTurn = Vector3.Dot(lateral, Vector3.Cross(Vector3.up, reference));
            const float maxMitreStretch = 1f / 0.6f;
            float mitre = Mathf.Min(maxMitreStretch, 1f / Mathf.Max(0.0001f, cosHalfTurn));

            return point + lateral * (lateralOffset * mitre);
        }

        private static Vector3 FlatDirection(Vector3 from, Vector3 to)
        {
            Vector3 direction = to - from;
            direction.y = 0f;
            return direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.zero;
        }

        public bool TryGetPoint(int index, out Vector3 point)
        {
            if (_waypoints == null || index < 0 || index >= _waypoints.Length || _waypoints[index] == null)
            {
                point = default;
                return false;
            }

            point = _waypoints[index].position;
            return true;
        }

#if UNITY_EDITOR
        [ContextMenu("Populate From Children")]
        private void PopulateFromChildren()
        {
            int childCount = transform.childCount;
            _waypoints = new Transform[childCount];
            for (int i = 0; i < childCount; i++)
            {
                _waypoints[i] = transform.GetChild(i);
            }
        }
#endif

        private void OnValidate()
        {
            if (_waypoints == null || _waypoints.Length < 2)
            {
                Debug.LogError($"[EnemyPath3D] '{name}' needs at least two waypoints.", this);
                return;
            }

            for (int i = 0; i < _waypoints.Length; i++)
            {
                if (_waypoints[i] == null)
                {
                    Debug.LogError($"[EnemyPath3D] '{name}' has a null waypoint at index {i}.", this);
                }
            }
        }

        private void OnDrawGizmos()
        {
            if (_waypoints == null)
            {
                return;
            }

            Gizmos.color = Color.yellow;
            for (int i = 0; i < _waypoints.Length; i++)
            {
                if (_waypoints[i] == null)
                {
                    continue;
                }

                Gizmos.DrawSphere(_waypoints[i].position, 0.3f);

                if (i + 1 < _waypoints.Length && _waypoints[i + 1] != null)
                {
                    Vector3 from = _waypoints[i].position;
                    Vector3 to = _waypoints[i + 1].position;
                    Gizmos.DrawLine(from, to);

                    Vector3 direction = (to - from).normalized;
                    Vector3 midpoint = Vector3.Lerp(from, to, 0.5f);
                    Gizmos.DrawLine(midpoint, midpoint - direction * 0.5f + Vector3.up * 0.3f);
                }
            }

            DrawLaneEdgeGizmos();
        }

        /// <summary>The two outermost lanes, so the spread can be checked against the painted road in the Scene
        /// view.</summary>
        private void DrawLaneEdgeGizmos()
        {
            if (_laneHalfWidth <= 0f || _waypoints.Length < 2 || System.Array.IndexOf(_waypoints, null) >= 0)
            {
                return;
            }

            Gizmos.color = new Color(1f, 0.85f, 0f, 0.45f);
            for (int i = 0; i + 1 < _waypoints.Length; i++)
            {
                Gizmos.DrawLine(GetLanePoint(i, _laneHalfWidth), GetLanePoint(i + 1, _laneHalfWidth));
                Gizmos.DrawLine(GetLanePoint(i, -_laneHalfWidth), GetLanePoint(i + 1, -_laneHalfWidth));
            }
        }
    }
}
