using System;
using AlienDefense.Common;
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

        // This enemy's sideways lane across the road, fixed for its whole walk (see EnemyPath3D.LaneHalfWidth).
        // Re-rolled on every Initialize, so a pooled enemy coming back for the next wave gets a fresh lane.
        private float _laneOffset;
        private bool _isInitialized;
        private bool _isMoving;
        private bool _isPaused;
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

            // Uniform across the whole usable road width, so a wave fans out edge to edge rather than bunching
            // on the centre line. Fully qualified: this file has `using System`, which makes a bare Random
            // ambiguous.
            _laneOffset = UnityEngine.Random.Range(-_path.LaneHalfWidth, _path.LaneHalfWidth);

            Vector3 spawnPoint = LanePoint(0);
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
            _isPaused = false;
        }

        public void StopMovement()
        {
            _isMoving = false;
            CurrentMoveDirection = Vector2.zero;
        }

        /// <summary>Holds this enemy in place without ending its walk (used while a boss intro is on screen).
        /// Unlike StopMovement, un-pausing resumes the path exactly where it was.</summary>
        public void SetPaused(bool paused)
        {
            _isPaused = paused;
            if (paused)
            {
                CurrentMoveDirection = Vector2.zero;
            }
        }

        /// <summary>Re-places a freshly initialized enemy <paramref name="distanceAlongPath"/> metres down its path on
        /// a chosen lane (instead of the random lane at waypoint 0 that Initialize picks), facing the direction of
        /// travel. Used to lay out a boss encounter's formation; the enemy then walks on from there normally.</summary>
        public void PlaceAlongPath(float distanceAlongPath, float laneOffset)
        {
            if (!_isInitialized || _path == null)
            {
                return;
            }

            _laneOffset = Mathf.Clamp(laneOffset, -_path.LaneHalfWidth - 1f, _path.LaneHalfWidth + 1f);

            float remaining = Mathf.Max(0f, distanceAlongPath);
            int segment = 1;
            Vector3 from = LanePoint(0);
            Vector3 to = LanePoint(1);
            while (true)
            {
                float length = Vector3.Distance(from, to);
                if (remaining <= length || segment >= _path.Count - 1)
                {
                    Vector3 position = length > 0.0001f ? Vector3.Lerp(from, to, Mathf.Clamp01(remaining / length)) : from;
                    position.y = SampleGroundHeight(position, position.y);
                    transform.position = position;

                    Vector3 flat = new Vector3(to.x - from.x, 0f, to.z - from.z);
                    if (flat.sqrMagnitude > 0.0001f)
                    {
                        transform.rotation = Quaternion.LookRotation(flat.normalized, Vector3.up);
                    }

                    _targetWaypointIndex = segment;
                    CacheSegmentLength();
                    UpdatePathProgress(position);
                    return;
                }

                remaining -= length;
                segment++;
                from = to;
                to = LanePoint(segment);
            }
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
            if (!_isInitialized || !_isMoving || _isPaused)
            {
                return;
            }

            Vector3 targetPosition = LanePoint(_targetWaypointIndex);
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
            bool hasHit = Physics.Raycast(origin, Vector3.down, out RaycastHit hit, GroundProbeMaxDistance, GroundMask, QueryTriggerInteraction.Ignore);

            // On a bridge the deck's walk surface is the floor: the probe would otherwise catch railing posts at the
            // lane edge or fall through plank gaps. Terrain still wins where the bank rises above the deck ramp.
            if (WalkableSurface.TryGetHeight(position, out float surfaceY))
            {
                return hasHit && hit.collider is TerrainCollider && hit.point.y > surfaceY ? hit.point.y : surfaceY;
            }

            return hasHit ? hit.point.y : fallbackY;
        }

        private void UpdatePathProgress(Vector3 currentPosition)
        {
            int segmentIndex = _targetWaypointIndex - 1;
            float segmentProgress = 1f;

            if (_segmentLength > 0.0001f)
            {
                Vector3 segmentStart = LanePoint(segmentIndex);
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
            Vector3 from = LanePoint(_targetWaypointIndex - 1);
            Vector3 to = LanePoint(_targetWaypointIndex);
            _segmentLength = Vector3.Distance(from, to);
        }

        /// <summary>Waypoint <paramref name="index"/> on this enemy's own lane. Every position this component
        /// steers towards or measures progress against goes through here, so movement, arrival and PathProgress
        /// all agree on the same parallel line.</summary>
        private Vector3 LanePoint(int index)
        {
            return _path.GetLanePoint(index, _laneOffset);
        }
    }
}
