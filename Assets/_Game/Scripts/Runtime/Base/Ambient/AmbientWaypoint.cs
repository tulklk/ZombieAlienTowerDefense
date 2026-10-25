using System.Collections.Generic;
using UnityEngine;

namespace AlienDefense.Base.Ambient
{
    /// <summary>One node of an ambient walking route (BaseWorld decoration only - never an enemy path). It knows
    /// its neighbours, so a walker only ever moves along a link that was laid on a road or a yard, never straight
    /// across the map. Its position (Y included) is where a walker's feet go.</summary>
    public sealed class AmbientWaypoint : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Waypoints reachable from here in a straight line without crossing a building, water or a big prop. " +
                 "A walker only follows the links listed on the waypoint it stands on, so list a two-way link on both ends " +
                 "(BaseAmbientNpcBuilder does).")]
        private List<AmbientWaypoint> _neighbors = new List<AmbientWaypoint>();

        public IReadOnlyList<AmbientWaypoint> Neighbors => _neighbors;

        public Vector3 Position => transform.position;

        public void AddNeighbor(AmbientWaypoint other)
        {
            if (other != null && other != this && !_neighbors.Contains(other))
            {
                _neighbors.Add(other);
            }
        }

        public void ClearNeighbors()
        {
            _neighbors.Clear();
        }

        /// <summary>A random neighbour, avoiding the one just left unless it is the only way on (a dead end).</summary>
        public AmbientWaypoint PickNext(AmbientWaypoint cameFrom, System.Random random)
        {
            int count = _neighbors.Count;
            if (count == 0)
            {
                return cameFrom;
            }

            int options = 0;
            for (int i = 0; i < count; i++)
            {
                if (_neighbors[i] != null && _neighbors[i] != cameFrom)
                {
                    options++;
                }
            }

            if (options == 0)
            {
                return cameFrom;
            }

            int pick = random.Next(options);
            for (int i = 0; i < count; i++)
            {
                AmbientWaypoint candidate = _neighbors[i];
                if (candidate == null || candidate == cameFrom)
                {
                    continue;
                }

                if (pick-- == 0)
                {
                    return candidate;
                }
            }

            return cameFrom;
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.2f, 0.9f, 1f, 0.9f);
            Gizmos.DrawSphere(transform.position + Vector3.up * 0.2f, 0.35f);
            Gizmos.color = new Color(0.2f, 0.9f, 1f, 0.5f);
            for (int i = 0; i < _neighbors.Count; i++)
            {
                if (_neighbors[i] != null)
                {
                    Gizmos.DrawLine(transform.position + Vector3.up * 0.2f, _neighbors[i].transform.position + Vector3.up * 0.2f);
                }
            }
        }
#endif
    }
}
