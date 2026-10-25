using UnityEngine;

namespace AlienDefense.Base.Ambient
{
    /// <summary>A BaseWorld resident that walks the road grid forever (decoration only: no collider, no health, no
    /// save data, nothing reads it). At every waypoint it picks a random linked neighbour - never the one it just
    /// came from unless it hit a dead end - so it always walks along a road, never across a plot or the river.
    ///
    /// Walks on the right-hand side of the road (_laneOffset), so two walkers meeting head-on pass each other, and
    /// slows to the pace of a walker right in front of it going the same way instead of walking through it.
    /// Movement is this script's; the Walking clip plays in place (root motion off, AmbientInPlaceAnimation on the model), its playback speed nudged with the
    /// walking speed so the feet do not slide. Everything resets each time BaseWorld is shown (OnEnable).</summary>
    public sealed class AlienPatrolNpc : MonoBehaviour
    {
        private static readonly int WalkingState = Animator.StringToHash("Walking");

        [SerializeField]
        private Animator _animator;

        [Header("Route")]
        [SerializeField]
        private AmbientWaypointNetwork _network;

        [SerializeField]
        [Tooltip("Where the walk starts; the walker appears on the road between this and First Target.")]
        private AmbientWaypoint _startWaypoint;

        [SerializeField]
        [Tooltip("A neighbour of Start Waypoint: the direction of the first walk.")]
        private AmbientWaypoint _firstTarget;

        [SerializeField, Range(0f, 1f)]
        [Tooltip("How far along the first link the walker appears (0 = on Start Waypoint).")]
        private float _spawnAlong = 0.5f;

        [SerializeField]
        [Tooltip("Route choices use this seed; 0 = a different walk every time.")]
        private int _seed;

        [Header("Movement")]
        [SerializeField, Min(0.1f)]
        [Tooltip("Metres per second at animation speed 1 - matched to the Walking clip's stride at this model's scale, " +
                 "so the feet do not slide. Change the stride (scale) rather than this to walk faster.")]
        private float _moveSpeed = 1.2f;

        [SerializeField, Range(0f, 0.2f)]
        [Tooltip("Each walker gets a random pace within +/- this share; the animation plays at the same factor.")]
        private float _speedVariation = 0.08f;

        [SerializeField, Range(60f, 720f)]
        [Tooltip("Degrees per second the walker turns towards where it is going.")]
        private float _rotationSpeed = 260f;

        [SerializeField, Range(0f, 0.3f)]
        private float _rotationVariation = 0.15f;

        [SerializeField, Range(0f, 2f)]
        [Tooltip("Metres to the right of the road's centre line the walker keeps to.")]
        private float _laneOffset = 0.9f;

        [SerializeField, Range(0.05f, 1f)]
        private float _arriveDistance = 0.25f;

        [Header("Other walkers")]
        [SerializeField]
        [Tooltip("Walkers this one never walks through: it slows to their pace when right behind them.")]
        private AlienPatrolNpc[] _keepClearOf = new AlienPatrolNpc[0];

        [SerializeField, Range(0.5f, 4f)]
        private float _personalSpace = 1.8f;

        private System.Random _random;
        private AmbientWaypoint _from;
        private AmbientWaypoint _to;
        private Vector3 _target;
        private Vector3 _segmentStart;
        private float _segmentLength;
        private float _baseSpeed;
        private float _turnSpeed;
        private float _animationFactor;
        private float _appliedAnimatorSpeed = -1f;

        /// <summary>Metres per second right now (read by other walkers keeping their distance).</summary>
        public float CurrentSpeed { get; private set; }

        private void OnEnable()
        {
            if (_startWaypoint == null)
            {
                enabled = false;
                return;
            }

            _random = new System.Random(_seed != 0 ? _seed : System.Environment.TickCount ^ GetInstanceID());
            float paceFactor = 1f + ((float)_random.NextDouble() * 2f - 1f) * _speedVariation;
            _baseSpeed = _moveSpeed * paceFactor;
            _animationFactor = paceFactor;
            _turnSpeed = _rotationSpeed * (1f + ((float)_random.NextDouble() * 2f - 1f) * _rotationVariation);

            _from = _startWaypoint;
            _to = _firstTarget != null ? _firstTarget : _startWaypoint.PickNext(null, _random);
            Vector3 lane = Lane(_from.Position, _to.Position);
            Vector3 spawn = Vector3.Lerp(_from.Position, _to.Position, _spawnAlong) + lane;
            transform.position = spawn;
            BeginSegment();
            Vector3 heading = Flat(_target - spawn);
            if (heading.sqrMagnitude > 0.0001f)
            {
                transform.rotation = Quaternion.LookRotation(heading);
            }

            CurrentSpeed = _baseSpeed;
            if (_animator != null)
            {
                _animator.applyRootMotion = false;
                // A random point in the cycle, so two walkers never step in sync.
                _animator.Play(WalkingState, 0, (float)_random.NextDouble());
            }

            SetAnimatorSpeed(_animationFactor);
        }

        private void Update()
        {
            float deltaTime = Time.deltaTime;
            if (deltaTime <= 0f || _to == null)
            {
                return;
            }

            Vector3 position = transform.position;
            Vector3 toTarget = Flat(_target - position);
            float distance = toTarget.magnitude;
            if (distance <= _arriveDistance)
            {
                AmbientWaypoint next = _to.PickNext(_from, _random);
                _from = _to;
                _to = next;
                BeginSegment();
                toTarget = Flat(_target - position);
                distance = toTarget.magnitude;
                if (distance < 0.0001f)
                {
                    return;
                }
            }

            float speed = _baseSpeed * PaceBehindOthers(position);
            CurrentSpeed = speed;
            SetAnimatorSpeed(_animationFactor * speed / _baseSpeed);

            Vector3 direction = toTarget / distance;
            float step = Mathf.Min(distance, speed * deltaTime);
            position += direction * step;
            float progress = _segmentLength > 0.0001f ? 1f - Mathf.Clamp01((distance - step) / _segmentLength) : 1f;
            position.y = Mathf.Lerp(_segmentStart.y, _target.y, progress);

            transform.SetPositionAndRotation(position,
                Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(direction), _turnSpeed * deltaTime));
        }

        private void BeginSegment()
        {
            _target = _to.Position + Lane(_from.Position, _to.Position);
            _segmentStart = transform.position;
            _segmentLength = Flat(_target - _segmentStart).magnitude;
        }

        /// <summary>1 normally; less while another walker is just ahead going the same way.</summary>
        private float PaceBehindOthers(Vector3 position)
        {
            float pace = 1f;
            Vector3 forward = transform.forward;
            float space = _personalSpace * _personalSpace;
            for (int i = 0; i < _keepClearOf.Length; i++)
            {
                AlienPatrolNpc other = _keepClearOf[i];
                if (other == null || other == this || !other.isActiveAndEnabled)
                {
                    continue;
                }

                Vector3 offset = Flat(other.transform.position - position);
                if (offset.sqrMagnitude > space || Vector3.Dot(offset, forward) <= 0f
                    || Vector3.Dot(other.transform.forward, forward) < 0.3f)
                {
                    continue; // behind us, far, or coming the other way (the lanes keep those apart)
                }

                pace = Mathf.Min(pace, Mathf.Clamp(other.CurrentSpeed / _baseSpeed, 0.35f, 1f));
            }

            return pace;
        }

        private void SetAnimatorSpeed(float speed)
        {
            if (_animator == null || Mathf.Abs(speed - _appliedAnimatorSpeed) < 0.02f)
            {
                return;
            }

            _appliedAnimatorSpeed = speed;
            _animator.speed = speed;
        }

        private Vector3 Lane(Vector3 from, Vector3 to)
        {
            Vector3 direction = Flat(to - from);
            return direction.sqrMagnitude > 0.0001f ? Vector3.Cross(Vector3.up, direction.normalized) * _laneOffset : Vector3.zero;
        }

        private static Vector3 Flat(Vector3 v)
        {
            v.y = 0f;
            return v;
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (_startWaypoint == null)
            {
                return;
            }

            Gizmos.color = new Color(1f, 0.8f, 0.2f, 0.9f);
            Gizmos.DrawWireSphere(_startWaypoint.Position + Vector3.up * 0.2f, 0.6f);
            if (_firstTarget != null)
            {
                Gizmos.DrawLine(_startWaypoint.Position + Vector3.up * 0.3f, _firstTarget.Position + Vector3.up * 0.3f);
            }
        }
#endif
    }
}
