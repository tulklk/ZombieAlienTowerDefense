using AlienDefense.Common;
using UnityEngine;

namespace AlienDefense.CameraSystem
{
    /// <summary>Perspective top-down follow camera: smooth position, look-ahead, level bounds clamp.</summary>
    public sealed class TopDownCameraController : MonoBehaviour
    {
        [Header("Target")]
        [SerializeField]
        private Transform _followTarget;

        [SerializeField]
        private Transform _cameraTransform;

        [SerializeField]
        [Tooltip("Optional visual anchor (e.g. CameraRig/FollowTarget) kept at the camera's current focus point.")]
        private Transform _focusPointAnchor;

        [Header("Composition")]
        [SerializeField]
        private Vector3 _positionOffset = new Vector3(0f, 20f, -15f);

        [SerializeField]
        private Vector3 _lookAtOffset = Vector3.zero;

        [SerializeField, Min(0.01f)]
        private float _followSmoothTime = 0.25f;

        [Header("Look Ahead")]
        [SerializeField, Min(0f)]
        private float _lookAheadDistance = 2f;

        [SerializeField, Min(0.01f)]
        private float _lookAheadSmoothTime = 0.3f;

        [SerializeField]
        [Tooltip("Optional. Must implement IMovementDirectionSource.")]
        private MonoBehaviour _movementDirectionSource;

        [Header("Bounds")]
        [SerializeField]
        [Tooltip("Optional. Clamps the focus point so the camera never looks outside the level.")]
        private LevelBounds _levelBounds;

        [SerializeField, Min(0f)]
        [Tooltip("Extra margin (world units) the focus point is kept away from the level edge.")]
        private float _cameraBoundsPadding = 1.5f;

        private IMovementDirectionSource _resolvedMovementSource;
        private Vector3 _positionVelocity;
        private Vector3 _lookAheadVelocity;
        private Vector3 _currentLookAhead;
        private bool _hasSnapped;

        private void Awake()
        {
            if (_followTarget == null || _cameraTransform == null)
            {
                Debug.LogError("[TopDownCameraController] FollowTarget and CameraTransform must both be assigned.", this);
                enabled = false;
                return;
            }

            _resolvedMovementSource = _movementDirectionSource as IMovementDirectionSource;
        }

        private void LateUpdate()
        {
            Vector2 moveDirection = _resolvedMovementSource?.MovementDirection ?? Vector2.zero;
            Vector3 desiredLookAhead = new Vector3(moveDirection.x, 0f, moveDirection.y) * _lookAheadDistance;
            _currentLookAhead = Vector3.SmoothDamp(_currentLookAhead, desiredLookAhead, ref _lookAheadVelocity, _lookAheadSmoothTime);

            Vector3 focusPoint = _followTarget.position + _lookAtOffset + _currentLookAhead;
            if (_levelBounds != null)
            {
                focusPoint = _levelBounds.ClampXZ(focusPoint, _cameraBoundsPadding);
            }

            Vector3 desiredCameraPosition = focusPoint + _positionOffset;

            if (!_hasSnapped)
            {
                _cameraTransform.position = desiredCameraPosition;
                _hasSnapped = true;
            }
            else
            {
                _cameraTransform.position = Vector3.SmoothDamp(
                    _cameraTransform.position,
                    desiredCameraPosition,
                    ref _positionVelocity,
                    _followSmoothTime);
            }

            if (_focusPointAnchor != null)
            {
                _focusPointAnchor.position = focusPoint;
            }
        }

        /// <summary>Hard-snaps the camera to the current follow target (used after intro teleports).</summary>
        public void SnapToTargetImmediate()
        {
            if (_followTarget == null || _cameraTransform == null)
            {
                return;
            }

            _positionVelocity = Vector3.zero;
            _lookAheadVelocity = Vector3.zero;
            _currentLookAhead = Vector3.zero;

            Vector3 focusPoint = _followTarget.position + _lookAtOffset;
            if (_levelBounds != null)
            {
                focusPoint = _levelBounds.ClampXZ(focusPoint, _cameraBoundsPadding);
            }

            _cameraTransform.position = focusPoint + _positionOffset;
            _hasSnapped = true;

            if (_focusPointAnchor != null)
            {
                _focusPointAnchor.position = focusPoint;
            }
        }
    }
}
