using UnityEngine;

namespace AlienDefense.UI
{
    /// <summary>Rotates its transform to match a camera's rotation, for world-space UI/icons.
    ///
    /// Every enemy carries one (health bar, shield bar). The gameplay camera barely rotates, yet writing
    /// transform.rotation marks the transform - and its canvas - changed every frame even when the value is the same,
    /// which showed up as TransformChangeSystem cost growing with the enemy count. So it only writes when its world
    /// rotation no longer matches the camera (the camera turned, or the enemy carrying it did).</summary>
    public sealed class WorldSpaceBillboard : MonoBehaviour
    {
        private Transform _cameraTransform;
        private Transform _transform;
        private Quaternion _appliedRotation;
        private bool _hasApplied;

        public void Initialize(Transform cameraTransform)
        {
            _cameraTransform = cameraTransform;
            _hasApplied = false;
        }

        private void Awake()
        {
            _transform = transform;
        }

        private void OnEnable()
        {
            _hasApplied = false; // re-sync after being hidden or pooled
        }

        private void LateUpdate()
        {
            if (_cameraTransform == null)
            {
                return;
            }

            // The world rotation also changes when the parent (the enemy) turns, so compare against what the
            // transform currently has rather than only against the camera.
            Quaternion cameraRotation = _cameraTransform.rotation;
            if (_hasApplied && _appliedRotation == cameraRotation && _transform.rotation == cameraRotation)
            {
                return;
            }

            _transform.rotation = cameraRotation;
            _appliedRotation = cameraRotation;
            _hasApplied = true;
        }
    }
}
