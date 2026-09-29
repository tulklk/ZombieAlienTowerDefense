using UnityEngine;

namespace AlienDefense.Base.River
{
    /// <summary>An ordered route for boats: its child transforms (Waypoint_00, Waypoint_01, ...) in hierarchy order.
    /// Cached once; the waypoints are scenery and never move at runtime.</summary>
    public sealed class RiverBoatPath : MonoBehaviour
    {
        private Transform[] _waypoints;

        public int Count
        {
            get
            {
                EnsureCache();
                return _waypoints.Length;
            }
        }

        public Vector3 GetPoint(int index)
        {
            EnsureCache();
            return _waypoints[Mathf.Clamp(index, 0, _waypoints.Length - 1)].position;
        }

        private void EnsureCache()
        {
            if (_waypoints != null && _waypoints.Length == transform.childCount)
            {
                return;
            }

            _waypoints = new Transform[transform.childCount];
            for (int i = 0; i < _waypoints.Length; i++)
            {
                _waypoints[i] = transform.GetChild(i);
            }
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.cyan;
            for (int i = 0; i < transform.childCount; i++)
            {
                Vector3 p = transform.GetChild(i).position;
                Gizmos.DrawWireSphere(p, 0.4f);
                if (i + 1 < transform.childCount)
                {
                    Gizmos.DrawLine(p, transform.GetChild(i + 1).position);
                }
            }
        }
#endif
    }
}
