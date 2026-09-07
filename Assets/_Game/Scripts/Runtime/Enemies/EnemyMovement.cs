using System;
using UnityEngine;

namespace AlienDefense.Enemies
{
    /// <summary>Moves an enemy along an EnemyPath3D and tracks its progress toward the base.</summary>
    public sealed class EnemyMovement : MonoBehaviour
    {
        private EnemyPath3D _path;
        private float _moveSpeed;
        private float _rotationSpeed;
        private float _arrivalThreshold;

        private int _targetWaypointIndex;
        private float _segmentLength;
        private bool _isInitialized;
        private bool _isMoving;
        private bool _destinationReachedFired;

        private float _statusSpeedMultiplier = 1f;
        private float _behaviorSpeedMultiplier = 1f;

        // Ground-snapping: EnemyPath3D waypoints are straight-line-interpolated (including Y), but on curved
        // hilly terrain a straight line between two waypoints can dip below (or float above) the real surface
        // mid-segment. Measured directly on the Level_01 hillside path: enemies sank up to ~0.5 units into the
        // slope this way, burying their legs — not an animation/rig bug, a movement one. "Default" is the layer
        // every terrain chunk and bridge deck in this project actually uses (confirmed by inspecting live
        // colliders); Enemy/Player/Tower/etc are on their own layers and excluded automatically.
        //
        // Computed lazily (NOT a static field initializer): Unity forbids calling LayerMask.NameToLayer/GetMask
        // from a static or instance field initializer / constructor (it throws "NameToLayer is not allowed to
        // be called from a MonoBehaviour constructor") — that exception was silently unwinding through every
        // EnemyController.Awake() during pool prewarm, which aborted LevelCompositionRoot's wave setup entirely
        // (0 waves, no enemies ever spawned). Must only ever run from Awake/Start/Update onward.
        private static int _groundMask = -1;
        private static int GroundMask => _groundMask >= 0 ? _groundMask : (_groundMask = LayerMask.GetMask("Default"));
        private const float GroundProbeUpOffset = 4f;
        private const float GroundProbeMaxDistance = 12f;

        public Vector2 CurrentMoveDirection { get; private set; }
        public float PathProgress { get; private set; }

        public event Action DestinationReached;

        public void Initialize(EnemyPath3D path, float moveSpeed, float rotationSpeed, float arrivalThreshold)
        {
            _path = path;
            _moveSpeed = Mathf.Max(0.01f, moveSpeed);
            _rotationSpeed = Mathf.Max(0f, rotationSpeed);
            _arrivalThreshold = Mathf.Max(0.01f, arrivalThreshold);

            if (_path == null || _path.Count < 2)
            {
                Debug.LogError("[EnemyMovement] Path is null or has fewer than two waypoints.", this);
                _isInitialized = false;
                return;
            }

            Vector3 spawnPoint = _path.GetPoint(0);
            spawnPoint.y = SampleGroundHeight(spawnPoint, spawnPoint.y);
            transform.position = spawnPoint;
            _targetWaypointIndex = 1;
            CacheSegmentLength();

            PathProgress = 0f;
            CurrentMoveDirection = Vector2.zero;
            _destinationReachedFired = false;
            _statusSpeedMultiplier = 1f;
            _behaviorSpeedMultiplier = 1f;
            _isInitialized = true;
            _isMoving = true;
        }

        public void StopMovement()
        {
            _isMoving = false;
            CurrentMoveDirection = Vector2.zero;
        }

        /// <summary>Driven by EnemyStatusController (e.g. Slow). Not editable directly on EnemyDefinition.</summary>
        public void SetStatusSpeedMultiplier(float multiplier)
        {
            _statusSpeedMultiplier = Mathf.Max(0f, multiplier);
        }

        /// <summary>Driven by non-status gameplay behavior (e.g. Boss phase-two speed-up).</summary>
        public void SetBehaviorSpeedMultiplier(float multiplier)
        {
            _behaviorSpeedMultiplier = Mathf.Max(0f, multiplier);
        }

        private void Update()
        {
            if (!_isInitialized || !_isMoving)
            {
                return;
            }

            Vector3 targetPosition = _path.GetPoint(_targetWaypointIndex);
            Vector3 currentPosition = transform.position;
            Vector3 toTarget = targetPosition - currentPosition;

            Vector3 flatDirection = new Vector3(toTarget.x, 0f, toTarget.z);
            if (flatDirection.sqrMagnitude > 0.0001f)
            {
                CurrentMoveDirection = new Vector2(flatDirection.x, flatDirection.z).normalized;
                Quaternion targetRotation = Quaternion.LookRotation(flatDirection.normalized, Vector3.up);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, _rotationSpeed * Time.deltaTime);
            }

            float effectiveSpeed = _moveSpeed * _statusSpeedMultiplier * _behaviorSpeedMultiplier;
            Vector3 newPosition = Vector3.MoveTowards(currentPosition, targetPosition, effectiveSpeed * Time.deltaTime);
            newPosition.y = SampleGroundHeight(newPosition, newPosition.y);
            transform.position = newPosition;

            UpdatePathProgress(newPosition);

            // Horizontal-only: newPosition.y now follows the real terrain surface and can legitimately differ
            // from the path waypoint's own Y (see SampleGroundHeight) — that difference must never block or
            // delay reaching the next waypoint the way a full 3D distance check could.
            float flatDistance = Vector2.Distance(new Vector2(newPosition.x, newPosition.z), new Vector2(targetPosition.x, targetPosition.z));
            if (flatDistance <= _arrivalThreshold)
            {
                AdvanceToNextWaypoint();
            }
        }

        /// <summary>Raycasts straight down onto the real ground/terrain surface beneath <paramref name="position"/>
        /// and returns its height, falling back to <paramref name="fallbackY"/> (the path's own interpolated Y)
        /// when nothing is hit — e.g. briefly off the playable area. See the GroundMask field doc for why.</summary>
        private static float SampleGroundHeight(Vector3 position, float fallbackY)
        {
            Vector3 origin = new Vector3(position.x, position.y + GroundProbeUpOffset, position.z);
            if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, GroundProbeMaxDistance, GroundMask, QueryTriggerInteraction.Ignore))
            {
                return hit.point.y;
            }

            return fallbackY;
        }

        private void UpdatePathProgress(Vector3 currentPosition)
        {
            int segmentIndex = _targetWaypointIndex - 1;
            float segmentProgress = 1f;

            if (_segmentLength > 0.0001f)
            {
                Vector3 segmentStart = _path.GetPoint(segmentIndex);
                float traveled = Vector3.Distance(segmentStart, currentPosition);
                segmentProgress = Mathf.Clamp01(traveled / _segmentLength);
            }

            PathProgress = segmentIndex + segmentProgress;
        }

        private void AdvanceToNextWaypoint()
        {
            if (_targetWaypointIndex >= _path.Count - 1)
            {
                _isMoving = false;
                PathProgress = _path.Count - 1;

                if (!_destinationReachedFired)
                {
                    _destinationReachedFired = true;
                    DestinationReached?.Invoke();
                }

                return;
            }

            _targetWaypointIndex++;
            CacheSegmentLength();
        }

        private void CacheSegmentLength()
        {
            Vector3 from = _path.GetPoint(_targetWaypointIndex - 1);
            Vector3 to = _path.GetPoint(_targetWaypointIndex);
            _segmentLength = Vector3.Distance(from, to);
        }
    }
}
