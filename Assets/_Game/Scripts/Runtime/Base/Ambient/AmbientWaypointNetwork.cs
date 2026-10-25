using UnityEngine;

namespace AlienDefense.Base.Ambient
{
    /// <summary>A named group of AmbientWaypoints (the base's road grid, or the yard round one building). Walkers
    /// are handed a start waypoint from it; the links live on the waypoints themselves.</summary>
    public sealed class AmbientWaypointNetwork : MonoBehaviour
    {
        [SerializeField]
        private AmbientWaypoint[] _waypoints = new AmbientWaypoint[0];

        public int Count => _waypoints.Length;

        public AmbientWaypoint Get(int index)
        {
            return _waypoints[index];
        }

        public bool Contains(AmbientWaypoint waypoint)
        {
            for (int i = 0; i < _waypoints.Length; i++)
            {
                if (_waypoints[i] == waypoint)
                {
                    return true;
                }
            }

            return false;
        }

        public void SetWaypoints(AmbientWaypoint[] waypoints)
        {
            _waypoints = waypoints ?? new AmbientWaypoint[0];
        }
    }
}
