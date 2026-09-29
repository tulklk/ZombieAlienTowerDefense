using UnityEngine;

namespace AlienDefense.Base.River
{
    /// <summary>The stretch of river inside the cave. A boat inside this box is tinted progressively darker the
    /// deeper it goes (its "Depth" axis is the box's local +Z), so it fades into the cave instead of popping out of
    /// existence; once hidden, the mover recycles it. Pure bounds maths - no trigger colliders, no physics.</summary>
    public sealed class RiverCaveTransition : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Local size of the zone. +Z points from the cave mouth into the mountain.")]
        private Vector3 _size = new Vector3(12f, 6f, 16f);

        [SerializeField]
        [Tooltip("Colour an object is tinted toward at the far end of the zone.")]
        private Color _caveTint = new Color(0.05f, 0.12f, 0.16f, 1f);

        [SerializeField, Range(0f, 1f)]
        [Tooltip("Depth (0 = mouth, 1 = far end) at which a boat counts as hidden and may be recycled.")]
        private float _hiddenDepth = 0.8f;

        public Color CaveTint => _caveTint;
        public float HiddenDepth => _hiddenDepth;

        /// <summary>0 at the mouth, 1 at the far end; negative when outside (in front of) the zone.</summary>
        public float Depth01(Vector3 worldPosition)
        {
            Vector3 local = transform.InverseTransformPoint(worldPosition);
            if (Mathf.Abs(local.x) > _size.x * 0.5f || Mathf.Abs(local.y) > _size.y * 0.5f)
            {
                return -1f;
            }

            return local.z / Mathf.Max(0.01f, _size.z) + 0.5f;
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawWireCube(Vector3.zero, _size);
            Gizmos.DrawLine(new Vector3(0f, 0f, -_size.z * 0.5f), new Vector3(0f, 0f, _size.z * 0.5f));
        }
#endif
    }
}
