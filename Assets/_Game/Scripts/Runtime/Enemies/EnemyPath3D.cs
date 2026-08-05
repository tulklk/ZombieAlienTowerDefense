using UnityEngine;

namespace AlienDefense.Enemies
{
    /// <summary>Fixed Scene sequence of waypoints enemies walk along, on the XZ plane.</summary>
    public sealed class EnemyPath3D : MonoBehaviour
    {
        [SerializeField]
        private Transform[] _waypoints;

        public int Count => _waypoints?.Length ?? 0;

        public Vector3 GetPoint(int index)
        {
            return _waypoints[index].position;
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
        }
    }
}
