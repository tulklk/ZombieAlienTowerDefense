using System;
using AlienDefense.Common;
using UnityEngine;

namespace AlienDefense.Pickups
{
    /// <summary>One tractor-beamable Energy Pickup: sits Idle on the ground (spawned by EnergyDropService on an
    /// Enemy's Defeated resolve), then Pulls/Lifts into the UFO when admitted by the beam. Grants no reward
    /// itself — Collected only reports "this pickup is done"; EnergyPickupFactory is what actually calls
    /// EnergyCollectionService and returns this instance to its pool. Disabled (Update stopped) whenever Idle,
    /// per the "no per-frame cost for objects sitting still" performance requirement.</summary>
    [DisallowMultipleComponent]
    public sealed class EnergyPickupController : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Shrunk/spun during Lift. Optional.")]
        private Transform _visualRoot;

        private TractorEnergyAbsorptionRequest _request;
        private Vector3 _initialVisualScale = Vector3.one;
        private float _groundY;
        private float _liftStartDistance = 1f;
        private int _generation;

        public EnergyPickupState State { get; private set; } = EnergyPickupState.Idle;
        public int Value { get; private set; }

        /// <summary>XP granted on Collected — separate from Value (the Energy currency amount) so the two can
        /// diverge; see EnergyCollectionService.Collect and EnemyDefinition.ExperienceReward.</summary>
        public int ExperienceValue { get; private set; }
        public int Generation => _generation;

        /// <summary>True only while this pickup is eligible to start a fresh tractor beam absorption.</summary>
        public bool IsAbsorbable => State == EnergyPickupState.Idle;

        /// <summary>Fired exactly once per absorption, when this pickup reaches the capture socket. Subscribers:
        /// UFOTractorBeamController (frees its concurrent slot / re-fires its own public event) and
        /// EnergyPickupFactory (grants Energy/XP via EnergyCollectionService, then releases to pool).</summary>
        public event Action<EnergyPickupController> Collected;

        private void Awake()
        {
            if (_visualRoot != null)
            {
                _initialVisualScale = _visualRoot.localScale;
            }
        }

        /// <summary>Called by EnergyPickupFactory right after spawning/reactivating this instance. experienceValue
        /// defaults to 0 (no XP) so existing single-arg callers - state-machine tests that never touch reward -
        /// keep compiling unchanged.</summary>
        public void Initialize(int value, int experienceValue = 0)
        {
            Value = value;
            ExperienceValue = experienceValue;
            _generation++;
            State = EnergyPickupState.Idle;
            enabled = false;
            RestoreVisual();
        }

        /// <summary>Admission into a tractor beam. Fails silently if not currently Idle.</summary>
        public bool TryBeginAbsorption(in TractorEnergyAbsorptionRequest request)
        {
            if (State != EnergyPickupState.Idle || !request.IsValid)
            {
                return false;
            }

            _request = request;
            _groundY = transform.position.y;
            State = EnergyPickupState.Pulling;
            enabled = true;
            return true;
        }

        /// <summary>Called by the owning pool when this instance is returned, including prewarm.</summary>
        public void HandleReturnedToPool()
        {
            State = EnergyPickupState.Idle;
            enabled = false;
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

        /// <summary>Advances Pull/Lift by deltaTime. Separated from Update() so tests can drive it with an
        /// explicit deltaTime instead of relying on Time.deltaTime.</summary>
        public void Tick(float deltaTime)
        {
            switch (State)
            {
                case EnergyPickupState.Pulling:
                    TickPull(deltaTime);
                    break;
                case EnergyPickupState.Lifting:
                    TickLift(deltaTime);
                    break;
            }
        }

        private void TickPull(float deltaTime)
        {
            if (!_request.IsValid)
            {
                return;
            }

            float boost = _request.SpeedBoost?.PullSpeedMultiplier ?? 1f;
            transform.position = TractorPullLiftMotion.TickPull(
                transform.position, _request.BeamGroundAnchor.position, _groundY,
                _request.PullSpeed * boost, _request.CenterThreshold, deltaTime, out bool reachedCenter);

            if (reachedCenter)
            {
                BeginLift();
            }
        }

        private void BeginLift()
        {
            State = EnergyPickupState.Lifting;
            _liftStartDistance = _request.IsValid
                ? Mathf.Max(0.01f, Vector3.Distance(transform.position, _request.CaptureSocket.position))
                : 1f;
        }

        private void TickLift(float deltaTime)
        {
            if (!_request.IsValid)
            {
                return;
            }

            float boost = _request.SpeedBoost?.PullSpeedMultiplier ?? 1f;
            transform.position = TractorPullLiftMotion.TickLift(
                transform.position, _request.CaptureSocket.position,
                _request.LiftSpeed * boost, _request.SocketThreshold, deltaTime,
                out float remainingDistance, out bool reachedSocket);

            UpdateLiftVisual(remainingDistance, deltaTime);

            if (reachedSocket)
            {
                CompleteAbsorption();
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
                float progress = TractorPullLiftMotion.ComputeLiftProgress(remainingDistance, _liftStartDistance);
                float scale = Mathf.Lerp(1f, _request.MinimumVisualScale, progress);
                _visualRoot.localScale = _initialVisualScale * scale;
            }

            if (_request.SpinSpeedDegreesPerSecond > 0f)
            {
                _visualRoot.Rotate(Vector3.up, _request.SpinSpeedDegreesPerSecond * deltaTime, Space.Self);
            }
        }

        private void CompleteAbsorption()
        {
            State = EnergyPickupState.Collected;
            enabled = false;
            Collected?.Invoke(this);
        }
    }
}
