using UnityEngine;

namespace AlienDefense.Common
{
    /// <summary>
    /// Fixed Scene object describing the playable area on the XZ plane. Assigned via
    /// [SerializeField] to whichever systems need it (PlayerMovement, TopDownCameraController) —
    /// it is not a ScriptableObject because it is Scene-specific geometry, not level configuration data.
    /// </summary>
    public sealed class LevelBounds : MonoBehaviour
    {
        [SerializeField]
        private Vector2 _center = Vector2.zero;

        [SerializeField]
        private Vector2 _extents = new Vector2(25f, 25f);

        /// <summary>Clamps the X/Z components of a world position to stay inside the bounds; Y is untouched.</summary>
        public Vector3 ClampXZ(Vector3 worldPosition)
        {
            float minX = _center.x - _extents.x;
            float maxX = _center.x + _extents.x;
            float minZ = _center.y - _extents.y;
            float maxZ = _center.y + _extents.y;

            worldPosition.x = Mathf.Clamp(worldPosition.x, minX, maxX);
            worldPosition.z = Mathf.Clamp(worldPosition.z, minZ, maxZ);
            return worldPosition;
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = Color.cyan;
            Vector3 center = new Vector3(_center.x, transform.position.y, _center.y);
            Vector3 size = new Vector3(_extents.x * 2f, 0.1f, _extents.y * 2f);
            Gizmos.DrawWireCube(center, size);
        }
    }
}
