using UnityEngine;

namespace AlienDefense.Common
{
    /// <summary>Playable area on the XZ plane, used to clamp player and camera positions.</summary>
    public sealed class LevelBounds : MonoBehaviour
    {
        [SerializeField]
        private Vector2 _center = Vector2.zero;

        [SerializeField]
        private Vector2 _extents = new Vector2(25f, 25f);

        /// <summary>Clamps the X/Z components of a world position to stay inside the bounds; Y is untouched.</summary>
        public Vector3 ClampXZ(Vector3 worldPosition)
        {
            return ClampXZ(worldPosition, 0f);
        }

        /// <summary>Same as <see cref="ClampXZ(Vector3)"/> but shrinks the usable range by padding on every side.</summary>
        public Vector3 ClampXZ(Vector3 worldPosition, float padding)
        {
            float minX = _center.x - _extents.x + padding;
            float maxX = _center.x + _extents.x - padding;
            float minZ = _center.y - _extents.y + padding;
            float maxZ = _center.y + _extents.y - padding;

            if (minX > maxX)
            {
                minX = maxX = (minX + maxX) * 0.5f;
            }

            if (minZ > maxZ)
            {
                minZ = maxZ = (minZ + maxZ) * 0.5f;
            }

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
