using UnityEngine;

namespace AlienDefense.Base.Ambient
{
    /// <summary>A BaseWorld resident that lives in one building and potters round its yard (decoration only):
    /// it comes out of the door, stops and breathes, walks a few legs of the yard route (stopping at each), walks
    /// back round to the door, goes in, stays inside out of sight for a while, and comes out again (user, 2026-10-25).
    ///
    /// The Animator holds the clip order - BreathingIdle -(StartMove)-> StartWalking -(exit time)-> Walking
    /// -(!IsWalking)-> BreathingIdle - so idle never jumps straight to Walking. This script fires StartMove /
    /// IsWalking once per leg (no per-frame parameter writes) and moves the root itself: speed eases up from 0 over
    /// the Start Walking clip, then holds the walking pace. Through the door it does not stop: Home Path (door step
    /// outside -> ... -> inside the door) is walked in one go. Inside, its renderers are off. Root motion is off;
    /// AmbientInPlaceAnimation keeps the clips in place. Route and door are anchors under the plot root, so a
    /// building upgrade (which replaces the building model) never touches them. Resets each time BaseWorld is shown.</summary>
    public sealed class AlienStrollNpc : MonoBehaviour
    {
        private static readonly int StartMoveTrigger = Animator.StringToHash("StartMove");
        private static readonly int IsWalkingBool = Animator.StringToHash("IsWalking");
        private static readonly int IdleState = Animator.StringToHash("BreathingIdle");

        private enum Phase
        {
            Inside,
            Idle,
            Starting,
            Walking,
        }

        [SerializeField]
        private Animator _animator;

        [Header("Route")]
        [SerializeField]
        [Tooltip("The yard waypoints round this plot, in ring order; the walker only moves along their links.")]
        private AmbientWaypointNetwork _localRoute;

        [SerializeField]
        [Tooltip("From the door step (a waypoint of Local Route) through the doorway to a point inside the building. " +
                 "Empty = no house: the walker starts at Spawn and strolls forever.")]
        private AmbientWaypoint[] _homePath = new AmbientWaypoint[0];

        [SerializeField]
        [Tooltip("Used only without a Home Path.")]
        private AmbientWaypoint _spawn;

        [SerializeField]
        [Tooltip("Route choices use this seed; 0 = different every time.")]
        private int _seed;

        [SerializeField]
        [Tooltip("Renderers switched off while the walker is inside the building.")]
        private Renderer[] _renderers = new Renderer[0];

        [Header("Timing")]
        [SerializeField]
        [Tooltip("Seconds before the very first walk (inside the house, or breathing at Spawn).")]
        private Vector2 _firstIdle = new Vector2(1.5f, 4f);

        [SerializeField]
        [Tooltip("Seconds of breathing at every stop outside.")]
        private Vector2 _idle = new Vector2(2f, 5f);

        [SerializeField]
        [Tooltip("Yard legs walked between leaving the house and heading back to it.")]
        private Vector2Int _legsOutside = new Vector2Int(2, 4);

        [SerializeField]
        [Tooltip("Seconds spent inside the building before coming out again.")]
        private Vector2 _insideTime = new Vector2(4f, 8f);

        [SerializeField, Min(0.1f)]
        [Tooltip("Seconds of Start Walking before Walking takes over; the walker speeds up over it.")]
        private float _startWalkingDuration = 2.7f;

        [Header("Movement")]
        [SerializeField, Min(0.1f)]
        [Tooltip("Metres per second while Walking - matched to the Walking clip's stride at this model's scale.")]
        private float _moveSpeed = 1.2f;

        [SerializeField, Range(60f, 720f)]
        private float _rotationSpeed = 220f;

        [SerializeField, Range(0.05f, 1f)]
        private float _arriveDistance = 0.2f;

        private System.Random _random;
        private Phase _phase;
        private float _timer;
        private AmbientWaypoint _at;
        private AmbientWaypoint _cameFrom;
        private AmbientWaypoint _to;
        private Vector3 _segmentStart;
        private float _segmentLength;
        private int _legsLeft;
        private bool _leaving;
        private bool _returning;
        private bool _hidden;

        private bool HasHome => _homePath != null && _homePath.Length >= 2;

        private void OnEnable()
        {
            AmbientWaypoint start = HasHome ? _homePath[_homePath.Length - 1] : _spawn;
            if (start == null)
            {
                enabled = false;
                return;
            }

            _random = new System.Random(_seed != 0 ? _seed : System.Environment.TickCount ^ GetInstanceID());
            _at = start;
            _cameFrom = null;
            _to = null;
            _leaving = false;
            _returning = false;

            AmbientWaypoint lookAt = HasHome ? _homePath[_homePath.Length - 2] : start.PickNext(null, _random);
            Vector3 facing = lookAt != null ? Flat(lookAt.Position - start.Position) : Vector3.zero;
            transform.SetPositionAndRotation(start.Position,
                facing.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(facing) : start.transform.rotation);

            if (_animator != null)
            {
                _animator.applyRootMotion = false;
                _animator.speed = 1f;
                _animator.SetBool(IsWalkingBool, false);
                _animator.ResetTrigger(StartMoveTrigger);
                _animator.Play(IdleState, 0, (float)_random.NextDouble());
            }

            if (HasHome)
            {
                SetHidden(true);
                _phase = Phase.Inside;
                _timer = Range(_firstIdle);
            }
            else
            {
                SetHidden(false);
                EnterIdle(_firstIdle);
            }
        }

        private void Update()
        {
            float deltaTime = Time.deltaTime;
            if (deltaTime <= 0f)
            {
                return;
            }

            switch (_phase)
            {
                case Phase.Inside:
                    _timer -= deltaTime;
                    if (_timer <= 0f)
                    {
                        // Out of the door: the whole home path in one go, then the first stop on the door step.
                        SetHidden(false);
                        _leaving = true;
                        _legsLeft = _random.Next(_legsOutside.x, Mathf.Max(_legsOutside.x, _legsOutside.y) + 1);
                        SetOff(_homePath[_homePath.Length - 2]);
                    }

                    break;

                case Phase.Idle:
                    _timer -= deltaTime;
                    if (_timer <= 0f)
                    {
                        SetOff(ChooseNext());
                    }

                    break;

                case Phase.Starting:
                    _timer += deltaTime;
                    float ramp = Mathf.SmoothStep(0f, 1f, _timer / _startWalkingDuration);
                    Move(_moveSpeed * ramp, deltaTime);
                    if (_phase == Phase.Starting && _timer >= _startWalkingDuration)
                    {
                        _phase = Phase.Walking;
                    }

                    break;

                case Phase.Walking:
                    Move(_moveSpeed, deltaTime);
                    break;
            }
        }

        private AmbientWaypoint ChooseNext()
        {
            if (!HasHome)
            {
                return _at.PickNext(_cameFrom, _random);
            }

            if (_legsLeft > 0)
            {
                _legsLeft--;
                return _at.PickNext(_cameFrom, _random);
            }

            // Time to go home: round the yard the short way to the door step, then through the door.
            _returning = true;
            return _at == _homePath[0] ? _homePath[1] : StepTowards(_homePath[0]);
        }

        /// <summary>The neighbour on the shorter way round the ring to <paramref name="target"/>.</summary>
        private AmbientWaypoint StepTowards(AmbientWaypoint target)
        {
            int count = _localRoute != null ? _localRoute.Count : 0;
            int from = IndexOnRoute(_at);
            int to = IndexOnRoute(target);
            if (count < 2 || from < 0 || to < 0)
            {
                return target;
            }

            int forward = (to - from + count) % count;
            int next = forward <= count - forward ? (from + 1) % count : (from - 1 + count) % count;
            return _localRoute.Get(next);
        }

        private int IndexOnRoute(AmbientWaypoint waypoint)
        {
            for (int i = 0; i < _localRoute.Count; i++)
            {
                if (_localRoute.Get(i) == waypoint)
                {
                    return i;
                }
            }

            return -1;
        }

        private void SetOff(AmbientWaypoint next)
        {
            if (next == null || next == _at)
            {
                EnterIdle(_idle);
                return;
            }

            BeginLeg(next);
            _timer = 0f;
            _phase = Phase.Starting;
            if (_animator != null)
            {
                _animator.SetBool(IsWalkingBool, true);
                _animator.SetTrigger(StartMoveTrigger); // BreathingIdle -> StartWalking -> Walking, in the controller
            }
        }

        private void BeginLeg(AmbientWaypoint next)
        {
            _to = next;
            _segmentStart = transform.position;
            _segmentLength = Flat(_to.Position - _segmentStart).magnitude;
        }

        private void Move(float speed, float deltaTime)
        {
            Vector3 position = transform.position;
            Vector3 toTarget = Flat(_to.Position - position);
            float distance = toTarget.magnitude;
            if (distance <= _arriveDistance)
            {
                Arrive();
                return;
            }

            Vector3 direction = toTarget / distance;
            float step = Mathf.Min(distance, speed * deltaTime);
            position += direction * step;
            float progress = _segmentLength > 0.0001f ? 1f - Mathf.Clamp01((distance - step) / _segmentLength) : 1f;
            position.y = Mathf.Lerp(_segmentStart.y, _to.Position.y, progress);
            transform.SetPositionAndRotation(position,
                Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(direction), _rotationSpeed * deltaTime));
        }

        private void Arrive()
        {
            _cameFrom = _at;
            _at = _to;
            _to = null;

            int homeIndex = HomeIndex(_at);
            if (_leaving && homeIndex > 0)
            {
                BeginLeg(_homePath[homeIndex - 1]); // still in the doorway: keep walking out
                return;
            }

            if (_returning && homeIndex >= 0 && homeIndex < _homePath.Length - 1)
            {
                BeginLeg(_homePath[homeIndex + 1]); // at the door: keep walking in
                return;
            }

            if (_animator != null)
            {
                _animator.SetBool(IsWalkingBool, false); // Walking -> BreathingIdle
            }

            if (_returning && homeIndex == _homePath.Length - 1)
            {
                _returning = false;
                SetHidden(true);
                _phase = Phase.Inside;
                _timer = Range(_insideTime);
                return;
            }

            _leaving = false;
            EnterIdle(_idle);
        }

        private int HomeIndex(AmbientWaypoint waypoint)
        {
            if (!HasHome)
            {
                return -1;
            }

            for (int i = 0; i < _homePath.Length; i++)
            {
                if (_homePath[i] == waypoint)
                {
                    return i;
                }
            }

            return -1;
        }

        private void SetHidden(bool hidden)
        {
            if (_hidden == hidden && _renderers.Length > 0 && _renderers[0] != null && _renderers[0].enabled == !hidden)
            {
                return;
            }

            _hidden = hidden;
            for (int i = 0; i < _renderers.Length; i++)
            {
                if (_renderers[i] != null)
                {
                    _renderers[i].enabled = !hidden;
                }
            }
        }

        private void EnterIdle(Vector2 range)
        {
            _phase = Phase.Idle;
            _timer = Range(range);
        }

        private float Range(Vector2 range)
        {
            return Mathf.Lerp(range.x, range.y, (float)_random.NextDouble());
        }

        private static Vector3 Flat(Vector3 v)
        {
            v.y = 0f;
            return v;
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.6f, 1f, 0.3f, 0.9f);
            if (_spawn != null)
            {
                Gizmos.DrawWireCube(_spawn.Position + Vector3.up * 0.4f, new Vector3(0.8f, 0.8f, 0.8f));
            }

            if (_homePath == null)
            {
                return;
            }

            for (int i = 0; i < _homePath.Length; i++)
            {
                if (_homePath[i] == null)
                {
                    continue;
                }

                Gizmos.DrawWireCube(_homePath[i].Position + Vector3.up * 0.3f, new Vector3(0.5f, 0.5f, 0.5f));
                if (i > 0 && _homePath[i - 1] != null)
                {
                    Gizmos.DrawLine(_homePath[i - 1].Position + Vector3.up * 0.3f, _homePath[i].Position + Vector3.up * 0.3f);
                }
            }
        }
#endif
    }
}
