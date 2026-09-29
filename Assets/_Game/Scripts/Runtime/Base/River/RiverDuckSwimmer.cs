using UnityEngine;

namespace AlienDefense.Base.River
{
    /// <summary>One ambient duck: wanders slowly inside its swim area, steers away from the banks (using the
    /// RiverChannel's shape - no physics, no NavMesh), keeps a little distance from other ducks and boats, pauses now
    /// and then, bobs, and leaves small ripples while moving.
    ///
    /// No Update of its own: RiverAmbientManager ticks all ducks together.</summary>
    public sealed class RiverDuckSwimmer : MonoBehaviour
    {
        [Header("Swimming")]
        [SerializeField]
        private float _swimSpeedMin = 0.25f;

        [SerializeField]
        private float _swimSpeedMax = 0.55f;

        [SerializeField]
        [Tooltip("How quickly the duck turns toward its wanted heading.")]
        private float _turnSpeed = 1.5f;

        [SerializeField]
        private float _directionChangeIntervalMin = 3f;

        [SerializeField]
        private float _directionChangeIntervalMax = 7f;

        [SerializeField, Range(0f, 1f)]
        [Tooltip("Chance that a direction change is a short pause instead.")]
        private float _pauseChance = 0.2f;

        [Header("Water")]
        [SerializeField]
        private RiverWaterLevel _water;

        [SerializeField]
        private float _waterlineOffset = -0.25f;

        [SerializeField]
        private float _bobHeight = 0.02f;

        [SerializeField]
        private float _bobSpeed = 1.8f;

        [Header("Area")]
        [SerializeField]
        private RiverChannel _channel;

        [SerializeField]
        [Tooltip("Keeps this duck's group local to one stretch of river.")]
        private Transform _riverAreaCenter;

        [SerializeField]
        private Vector2 _swimAreaSize = new Vector2(20f, 12f);

        [SerializeField, Min(0.2f)]
        [Tooltip("Metres from the bank at which the duck starts turning back toward the middle.")]
        private float _bankMargin = 1.4f;

        [SerializeField]
        private float _separationDistance = 1.2f;

        [Header("Ripples")]
        [SerializeField, Min(0.1f)]
        private float _rippleInterval = 0.7f;

        [SerializeField]
        [Tooltip("Model yaw correction if the imported model's front is not +Z.")]
        private float _modelYawOffset;

        private float _heading;
        private float _wantedHeading;
        private float _speed;
        private float _currentSpeed;
        private float _timer;
        private float _phase;
        private float _rippleTimer;
        private System.Random _random;

        public Vector3 Position => transform.position;
        public float SeparationDistance => _separationDistance;

        public void Initialize(System.Random random, Vector3 startPosition)
        {
            _random = random;
            _phase = (float)random.NextDouble() * 10f;
            _heading = (float)random.NextDouble() * 360f;
            _wantedHeading = _heading;
            PickNewBehaviour();
            _timer = Range(0f, _directionChangeIntervalMax);
            transform.position = new Vector3(startPosition.x, WaterY(0f), startPosition.z);
        }

        public void Tick(float dt, float time, RiverDuckSwimmer[] ducks, RiverBoatMover[] boats, WaterRipplePool ripples)
        {
            _timer -= dt;
            if (_timer <= 0f)
            {
                PickNewBehaviour();
            }

            Vector3 position = transform.position;
            Vector3 steer = Vector3.zero;

            // Banks: turn back toward the middle before reaching land.
            if (_channel != null)
            {
                float inside = _channel.DistanceInside(position, out Vector3 toCenter);
                if (inside < _bankMargin)
                {
                    steer += toCenter * (1f - Mathf.Clamp01(inside / _bankMargin)) * 3f;
                }
            }

            // Area: stay with the group's stretch of river.
            if (_riverAreaCenter != null)
            {
                Vector3 local = position - _riverAreaCenter.position;
                if (Mathf.Abs(local.x) > _swimAreaSize.x * 0.5f || Mathf.Abs(local.z) > _swimAreaSize.y * 0.5f)
                {
                    Vector3 back = -local;
                    back.y = 0f;
                    steer += back.normalized * 1.5f;
                }
            }

            // Separation from the other ducks and from boats.
            for (int i = 0; i < ducks.Length; i++)
            {
                RiverDuckSwimmer other = ducks[i];
                if (other == null || other == this)
                {
                    continue;
                }

                Vector3 away = position - other.Position;
                away.y = 0f;
                float d = away.magnitude;
                if (d > 0.001f && d < _separationDistance)
                {
                    steer += away / d * (1f - d / _separationDistance);
                }
            }

            for (int i = 0; i < boats.Length; i++)
            {
                if (boats[i] == null || !boats[i].IsVisible)
                {
                    continue;
                }

                Vector3 away = position - boats[i].transform.position;
                away.y = 0f;
                float d = away.magnitude;
                if (d > 0.001f && d < 4f)
                {
                    steer += away / d * (1f - d / 4f) * 2f;
                }
            }

            if (steer.sqrMagnitude > 0.01f)
            {
                _wantedHeading = Mathf.Atan2(steer.x, steer.z) * Mathf.Rad2Deg;
                if (_speed <= 0.01f)
                {
                    _speed = _swimSpeedMin;
                }
            }

            float delta = Mathf.DeltaAngle(_heading, _wantedHeading);
            _heading += delta * Mathf.Clamp01(_turnSpeed * dt);
            _currentSpeed = Mathf.MoveTowards(_currentSpeed, _speed, dt * 0.5f);

            Vector3 forward = Quaternion.Euler(0f, _heading, 0f) * Vector3.forward;
            position += forward * (_currentSpeed * dt);

            float bob = Mathf.Sin((time + _phase) * _bobSpeed) * _bobHeight;
            position.y = WaterY(bob);
            float wobble = Mathf.Sin((time + _phase) * 1.3f) * 4f;
            transform.SetPositionAndRotation(position, Quaternion.Euler(0f, _heading + _modelYawOffset + wobble, 0f));

            if (_currentSpeed > 0.05f && ripples != null)
            {
                _rippleTimer += dt;
                if (_rippleTimer >= _rippleInterval)
                {
                    _rippleTimer = 0f;
                    float scale = transform.localScale.x;
                    Vector3 behind = position - forward * (0.35f * scale);
                    behind.y = _water != null ? _water.Level + 0.02f : position.y;
                    ripples.Emit(behind, 0.45f * scale, 1f, 0.75f);
                }
            }
        }

        private void PickNewBehaviour()
        {
            _timer = Range(_directionChangeIntervalMin, _directionChangeIntervalMax);
            if (_random != null && _random.NextDouble() < _pauseChance)
            {
                // Short rest: drift to a stop for 1-2 seconds.
                _speed = 0f;
                _timer = Range(1f, 2f);
                return;
            }

            _speed = Range(_swimSpeedMin, _swimSpeedMax);
            _wantedHeading = _heading + Range(-80f, 80f);
        }

        private float WaterY(float bob)
        {
            float level = _water != null ? _water.Level : transform.position.y;
            return level + _waterlineOffset + bob;
        }

        private float Range(float min, float max)
        {
            return _random == null ? (min + max) * 0.5f : min + (float)_random.NextDouble() * (max - min);
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (_riverAreaCenter == null)
            {
                return;
            }

            Gizmos.color = Color.green;
            Gizmos.DrawWireCube(_riverAreaCenter.position, new Vector3(_swimAreaSize.x, 0.2f, _swimAreaSize.y));
        }
#endif
    }
}
