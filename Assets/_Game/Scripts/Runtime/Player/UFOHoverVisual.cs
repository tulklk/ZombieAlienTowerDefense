using AlienDefense.CameraSystem;
using DG.Tweening;
using UnityEngine;

namespace AlienDefense.Player
{
    /// <summary>Cosmetic hover bob/spin/tilt for the UFO's Model child; never moves the gameplay root.
    ///
    /// The bob is a looping eased DOTween on localPosition (nothing else writes position here, so the tween can
    /// own it outright). Spin and tilt stay per-frame on purpose: they are combined into a single localRotation
    /// write, and the tilt has to chase live joystick input (IMovementDirectionSource) every frame - a tween
    /// toward a target that changes every frame would just be a worse SmoothDamp.</summary>
    public sealed class UFOHoverVisual : MonoBehaviour
    {
        [Header("Hover")]
        [SerializeField, Min(0f)]
        private float _hoverAmplitude = 0.15f;

        [SerializeField, Min(0f)]
        [Tooltip("Full up-down cycles per second.")]
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
        private Tweener _bobTween;

        private void Awake()
        {
            _basePosition = transform.localPosition;
            _resolvedSource = _movementDirectionSource as IMovementDirectionSource;
        }

        private void OnEnable()
        {
            _bobTween?.Kill();

            if (_hoverAmplitude <= 0f || _hoverFrequency <= 0f)
            {
                return;
            }

            // One yoyo loop is half a cycle, hence the /2 on the period.
            float halfCycle = 1f / (_hoverFrequency * 2f);
            transform.localPosition = _basePosition - new Vector3(0f, _hoverAmplitude, 0f);
            _bobTween = transform.DOLocalMoveY(_basePosition.y + _hoverAmplitude, halfCycle)
                .SetEase(Ease.InOutSine)
                .SetLoops(-1, LoopType.Yoyo);
        }

        private void OnDisable()
        {
            _bobTween?.Kill();
            _bobTween = null;
            transform.localPosition = _basePosition;
        }

        private void Update()
        {
            _spinAngle = (_spinAngle + _idleSpinDegreesPerSecond * Time.deltaTime) % 360f;

            Vector2 moveDirection = _resolvedSource?.MovementDirection ?? Vector2.zero;
            Vector2 targetTilt = new Vector2(moveDirection.y, -moveDirection.x) * _tiltDegrees;
            _currentTilt = Vector2.SmoothDamp(_currentTilt, targetTilt, ref _tiltVelocity, _tiltSmoothTime);

            transform.localRotation = Quaternion.Euler(_currentTilt.x, _spinAngle, _currentTilt.y);
        }

        private void OnDestroy()
        {
            _bobTween?.Kill();
        }
    }
}
