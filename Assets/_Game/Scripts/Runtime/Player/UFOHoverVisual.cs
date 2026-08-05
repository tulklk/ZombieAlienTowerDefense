using AlienDefense.CameraSystem;
using UnityEngine;

namespace AlienDefense.Player
{
    /// <summary>
    /// Purely cosmetic hover bob/spin/tilt for the UFO's Model child. Lives on the Model transform
    /// itself and only ever touches its own local position/rotation — the gameplay root (with the
    /// CharacterController and collider) driven by <see cref="PlayerMovement"/> never moves because of this.
    /// Uses scaled Time so hover freezes with the rest of gameplay on Pause, matching every other
    /// animated system in the project.
    /// </summary>
    public sealed class UFOHoverVisual : MonoBehaviour
    {
        [Header("Hover")]
        [SerializeField, Min(0f)]
        private float _hoverAmplitude = 0.15f;

        [SerializeField, Min(0f)]
        private float _hoverFrequency = 1.2f;

        [Header("Idle Spin")]
        [SerializeField]
        private float _idleSpinDegreesPerSecond = 20f;

        [Header("Movement Tilt")]
        [SerializeField, Min(0f)]
        private float _tiltDegrees = 8f;

        [SerializeField, Min(0.01f)]
        private float _tiltSmoothTime = 0.2f;

        [SerializeField]
        [Tooltip("Optional. Must implement IMovementDirectionSource. Leave empty for idle-only hover.")]
        private MonoBehaviour _movementDirectionSource;

        private IMovementDirectionSource _resolvedSource;
        private Vector3 _basePosition;
        private float _spinAngle;
        private Vector2 _currentTilt;
        private Vector2 _tiltVelocity;

        private void Awake()
        {
            _basePosition = transform.localPosition;
            _resolvedSource = _movementDirectionSource as IMovementDirectionSource;
        }

        private void Update()
        {
            _spinAngle = (_spinAngle + _idleSpinDegreesPerSecond * Time.deltaTime) % 360f;

            float bob = Mathf.Sin(Time.time * _hoverFrequency * Mathf.PI * 2f) * _hoverAmplitude;
            transform.localPosition = _basePosition + new Vector3(0f, bob, 0f);

            Vector2 moveDirection = _resolvedSource?.MovementDirection ?? Vector2.zero;
            Vector2 targetTilt = new Vector2(moveDirection.y, -moveDirection.x) * _tiltDegrees;
            _currentTilt = Vector2.SmoothDamp(_currentTilt, targetTilt, ref _tiltVelocity, _tiltSmoothTime);

            transform.localRotation = Quaternion.Euler(_currentTilt.x, _spinAngle, _currentTilt.y);
        }
    }
}
