using UnityEngine;

namespace AlienDefense.Base.River
{
    /// <summary>The river's shape as data: a centreline with a half-width per point (baked by the editor tool from
    /// the same curve that generated the water mesh). Swimmers ask it how far inside the water they are and which way
    /// is "back towards the middle" - no physics, no raycasts, no NavMesh.</summary>
    public sealed class RiverChannel : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("World-space centreline points (XZ used).")]
        private Vector3[] _centerline = System.Array.Empty<Vector3>();

        [SerializeField]
        [Tooltip("Half the water width at each centreline point.")]
        private float[] _halfWidths = System.Array.Empty<float>();

        public int PointCount => _centerline.Length;

        public void SetShape(Vector3[] centerline, float[] halfWidths)
        {
            _centerline = centerline;
            _halfWidths = halfWidths;
        }

        /// <summary>Metres from <paramref name="position"/> to the nearest bank (positive inside the water, negative
        /// on land), and the horizontal direction pointing from the position back to the centreline.</summary>
        public float DistanceInside(Vector3 position, out Vector3 towardCenter)
        {
            towardCenter = Vector3.zero;
            if (_centerline.Length < 2)
            {
                return float.MaxValue;
            }

            float bestDistance = float.MaxValue;
            float bestHalfWidth = 0f;
            Vector3 bestPoint = _centerline[0];
            var p = new Vector2(position.x, position.z);
            for (int i = 0; i < _centerline.Length - 1; i++)
            {
                var a = new Vector2(_centerline[i].x, _centerline[i].z);
                var b = new Vector2(_centerline[i + 1].x, _centerline[i + 1].z);
                Vector2 ab = b - a;
                float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(0.0001f, ab.sqrMagnitude));
                Vector2 closest = a + ab * t;
                float distance = (p - closest).sqrMagnitude;
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    bestHalfWidth = Mathf.Lerp(_halfWidths[i], _halfWidths[i + 1], t);
                    bestPoint = new Vector3(closest.x, position.y, closest.y);
                }
            }

            Vector3 toCenter = bestPoint - position;
            toCenter.y = 0f;
            towardCenter = toCenter.sqrMagnitude > 0.0001f ? toCenter.normalized : Vector3.zero;
            return bestHalfWidth - Mathf.Sqrt(bestDistance);
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            for (int i = 0; i < _centerline.Length - 1; i++)
            {
                Vector3 a = _centerline[i];
                Vector3 b = _centerline[i + 1];
                Vector3 side = Vector3.Cross(Vector3.up, (b - a).normalized);
                Gizmos.color = Color.cyan;
                Gizmos.DrawLine(a, b);
                Gizmos.color = new Color(0f, 1f, 1f, 0.35f);
                Gizmos.DrawLine(a + side * _halfWidths[i], b + side * _halfWidths[i + 1]);
                Gizmos.DrawLine(a - side * _halfWidths[i], b - side * _halfWidths[i + 1]);
            }
        }
#endif
    }
}
