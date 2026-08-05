using UnityEngine;

namespace AlienDefense.UI
{
    /// <summary>Rotates its transform to match a camera's rotation, for world-space UI/icons.</summary>
    public sealed class WorldSpaceBillboard : MonoBehaviour
    {
        private Transform _cameraTransform;

        public void Initialize(Transform cameraTransform)
        {
            _cameraTransform = cameraTransform;
        }

        private void LateUpdate()
        {
            if (_cameraTransform == null)
            {
                return;
            }

            transform.rotation = _cameraTransform.rotation;
        }
    }
}
