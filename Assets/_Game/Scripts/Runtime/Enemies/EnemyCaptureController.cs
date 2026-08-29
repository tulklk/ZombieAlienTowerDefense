using System;
using UnityEngine;

namespace AlienDefense.Enemies
{
    /// <summary>Owns this enemy's Transform while captured: pulls it to a moving ground anchor, then lifts it to a
    /// moving capture socket, shrinking/spinning VisualRoot. Knows nothing about EnemyRegistry, EnemyPool, Economy
    /// or WaveController — purely follows the anchors it is given.</summary>
    public sealed class EnemyCaptureController : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Shrunk/spun during Lift. Never the gameplay root.")]
        private Transform _visualRoot;

        private TractorCaptureRequest _request;
        private Action _onCaptureCompleted;
        private Vector3 _initialVisualScale = Vector3.one;
        private float _groundY;
        private float _liftStartDistance = 1f;

        public EnemyCaptureState State { get; private set; } = EnemyCaptureState.Inactive;

        private void Awake()
        {
            if (_visualRoot != null)
            {
                _initialVisualScale = _visualRoot.localScale;
            }
        }

        /// <summary>Starts Pulling. onCaptureCompleted fires exactly once, when the enemy reaches the capture socket.</summary>
        public void BeginCapture(in TractorCaptureRequest request, Action onCaptureCompleted)
        {
            if (!request.IsValid)
            {
                Debug.LogWarning($"[EnemyCaptureController] BeginCapture on '{name}' with an invalid request " +
                    $"(BeamGroundAnchor={(request.BeamGroundAnchor != null ? "set" : "NULL")}, " +
                    $"CaptureSocket={(request.CaptureSocket != null ? "set" : "NULL")}). " +
                    "The enemy will stop at the ground anchor and never lift — check UFOTractorBeamController's " +
                    "Beam Ground Anchor / Capture Socket fields in the Inspector.", this);
            }

            _request = request;
            _onCaptureCompleted = onCaptureCompleted;
            _groundY = transform.position.y;
            State = EnemyCaptureState.Pulling;
        }

        /// <summary>Interrupts an in-progress capture (e.g. level cleanup). No-op if not currently capturing.</summary>
        public void Abort()
        {
            if (State != EnemyCaptureState.Pulling && State != EnemyCaptureState.Lifting)
            {
                return;
            }

            State = EnemyCaptureState.Aborted;
            _onCaptureCompleted = null;
            RestoreVisual();
        }

        /// <summary>Called by EnemyController on Initialize and on pool reuse.</summary>
        public void ResetState()
        {
            State = EnemyCaptureState.Inactive;
            _onCaptureCompleted = null;
            RestoreVisual();
        }

        private void RestoreVisual()
        {
            if (_visualRoot == null)
            {
                return;
            }

            _visualRoot.localScale = _initialVisualScale;
            _visualRoot.localRotation = Quaternion.identity;
        }

        private void Update()
        {
            Tick(Time.deltaTime);
        }

        /// <summary>Advances Pull/Lift by deltaTime. Separated from Update() so tests can drive it with an explicit
        /// deltaTime instead of relying on Time.deltaTime.</summary>
        public void Tick(float deltaTime)
        {
            switch (State)
            {
                case EnemyCaptureState.Pulling:
                    TickPull(deltaTime);
                    break;
                case EnemyCaptureState.Lifting:
                    TickLift(deltaTime);
                    break;
            }
        }

        private void TickPull(float deltaTime)
        {
            if (!_request.IsValid)
            {
                Abort();
                return;
            }

            Vector3 anchor = _request.BeamGroundAnchor.position;
            Vector3 target = new Vector3(anchor.x, _groundY, anchor.z);
            Vector3 current = transform.position;
            Vector3 next = Vector3.MoveTowards(current, target, _request.PullSpeed * deltaTime);
            transform.position = next;

            float dx = next.x - anchor.x;
            float dz = next.z - anchor.z;
            if (dx * dx + dz * dz <= _request.CenterThreshold * _request.CenterThreshold)
            {
                BeginLift(next);
            }
        }

        private void BeginLift(Vector3 currentPosition)
        {
            State = EnemyCaptureState.Lifting;
            _liftStartDistance = _request.IsValid
                ? Mathf.Max(0.01f, Vector3.Distance(currentPosition, _request.CaptureSocket.position))
                : 1f;
        }

        private void TickLift(float deltaTime)
        {
            if (!_request.IsValid)
            {
                Abort();
                return;
            }

            Vector3 socket = _request.CaptureSocket.position;
            Vector3 current = transform.position;
            Vector3 next = Vector3.MoveTowards(current, socket, _request.LiftSpeed * deltaTime);
            transform.position = next;

            float remainingDistance = Vector3.Distance(next, socket);
            UpdateLiftVisual(remainingDistance, deltaTime);

            if (remainingDistance <= _request.SocketThreshold)
            {
                CompleteCapture();
            }
        }

        private void UpdateLiftVisual(float remainingDistance, float deltaTime)
        {
            if (_visualRoot == null)
            {
                return;
            }

            if (_request.ShrinkDuringLift)
            {
                float progress = Mathf.Clamp01(1f - remainingDistance / _liftStartDistance);
                float scale = Mathf.Lerp(1f, _request.MinimumVisualScale, progress);
                _visualRoot.localScale = _initialVisualScale * scale;
            }

            if (_request.SpinSpeedDegreesPerSecond > 0f)
            {
                _visualRoot.Rotate(Vector3.up, _request.SpinSpeedDegreesPerSecond * deltaTime, Space.Self);
            }
        }

        private void CompleteCapture()
        {
            State = EnemyCaptureState.Completed;
            Action callback = _onCaptureCompleted;
            _onCaptureCompleted = null;
            callback?.Invoke();
        }
    }
}
