using UnityEngine;

namespace AlienDefense.Base.River
{
    /// <summary>Ambient boat: follows a RiverBoatPath with smooth turning, bobs and sways on the water, leaves a
    /// light wake, and - when its path runs into a RiverCaveTransition - darkens into the cave and is recycled to the
    /// start of the route. With no path it simply floats in place (a moored boat).
    ///
    /// Transform-only (no Rigidbody). It has no Update of its own: RiverAmbientManager ticks every river actor from
    /// one place.</summary>
    public sealed class RiverBoatMover : MonoBehaviour
    {
        [Header("Route")]
        [SerializeField]
        private RiverBoatPath _path;

        [SerializeField, Min(0f)]
        private float _moveSpeed = 0.9f;

        [SerializeField, Min(0.1f)]
        [Tooltip("How fast the heading turns toward the next waypoint (higher = tighter turns).")]
        private float _rotationSpeed = 1.2f;

        [SerializeField, Min(0.05f)]
        private float _waypointReachDistance = 1.2f;

        [SerializeField]
        private bool _loop = true;

        [SerializeField]
        [Tooltip("Travel back and forth instead of looping. Ignored when the route ends in a cave.")]
        private bool _pingPong;

        [SerializeField]
        private bool _randomStartPosition = true;

        [Header("Water motion")]
        [SerializeField]
        private RiverWaterLevel _water;

        [SerializeField]
        [Tooltip("Model offset below the water line (negative sinks the hull).")]
        private float _waterlineOffset = -0.12f;

        [SerializeField]
        private float _bobHeight = 0.05f;

        [SerializeField]
        private float _bobSpeed = 1.5f;

        [SerializeField]
        [Tooltip("Degrees of roll/pitch sway.")]
        private float _swayAngle = 1.5f;

        [SerializeField]
        private float _swaySpeed = 1.2f;

        [Header("Cave (optional)")]
        [SerializeField]
        private RiverCaveTransition _cave;

        [SerializeField, Min(0f)]
        [Tooltip("Seconds the boat stays hidden in the cave before it reappears at the start of the route.")]
        private float _respawnDelay = 4f;

        [Header("Wake")]
        [SerializeField, Min(0.05f)]
        private float _wakeInterval = 0.35f;

        [SerializeField]
        [Tooltip("Stern offset (local) where wake rings are emitted, mirrored left/right on X.")]
        private Vector3 _wakeOffset = new Vector3(0.45f, 0f, -1.3f);

        private int _target = 1;
        private int _direction = 1;
        private float _heading;
        private float _phase;
        private float _wakeTimer;
        private float _hiddenTimer = -1f;
        private Renderer[] _renderers;
        private MaterialPropertyBlock _block;
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private float _tint = -1f;

        public bool IsVisible => _hiddenTimer < 0f;

        public void Initialize(System.Random random)
        {
            _renderers = GetComponentsInChildren<Renderer>(true);
            _block = new MaterialPropertyBlock();
            _phase = (float)random.NextDouble() * 10f;
            _heading = transform.eulerAngles.y;

            if (_path == null || _path.Count < 2)
            {
                return;
            }

            int start = 0;
            if (_randomStartPosition)
            {
                // Somewhere along the open-water part of the route (not already inside the cave).
                int last = _cave != null ? Mathf.Max(1, _path.Count - 3) : _path.Count - 1;
                start = random.Next(0, last);
            }

            PlaceAt(start);
        }

        private void PlaceAt(int index)
        {
            Vector3 p = _path.GetPoint(index);
            Vector3 next = _path.GetPoint(Mathf.Min(index + 1, _path.Count - 1));
            transform.position = new Vector3(p.x, WaterY(0f), p.z);
            Vector3 forward = next - p;
            _heading = forward.sqrMagnitude > 0.001f ? Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg : _heading;
            _target = Mathf.Min(index + 1, _path.Count - 1);
            _direction = 1;
            ApplyTint(0f);
        }

        public void Tick(float dt, float time, WaterRipplePool ripples)
        {
            float bob = Mathf.Sin((time + _phase) * _bobSpeed) * _bobHeight;
            float roll = Mathf.Sin((time + _phase) * _swaySpeed) * _swayAngle;
            float pitch = Mathf.Sin((time + _phase * 1.3f) * _swaySpeed * 0.8f) * _swayAngle * 0.5f;

            if (_hiddenTimer >= 0f)
            {
                _hiddenTimer += dt;
                if (_hiddenTimer >= _respawnDelay)
                {
                    _hiddenTimer = -1f;
                    SetRenderersEnabled(true);
                    PlaceAt(0);
                }

                return;
            }

            bool moving = _path != null && _path.Count >= 2 && _moveSpeed > 0f;
            Vector3 position = transform.position;
            if (moving)
            {
                Vector3 target = _path.GetPoint(_target);
                Vector3 toTarget = target - position;
                toTarget.y = 0f;

                if (toTarget.magnitude <= _waypointReachDistance)
                {
                    AdvanceTarget();
                    target = _path.GetPoint(_target);
                    toTarget = target - position;
                    toTarget.y = 0f;
                }

                // Turn smoothly toward the target, and slow down while the turn is sharp - no instant 90° snaps.
                float desired = Mathf.Atan2(toTarget.x, toTarget.z) * Mathf.Rad2Deg;
                float delta = Mathf.DeltaAngle(_heading, desired);
                _heading += Mathf.Clamp(delta, -1f, 1f) * Mathf.Min(Mathf.Abs(delta), _rotationSpeed * 60f * dt);
                float turnSlowdown = Mathf.Lerp(1f, 0.35f, Mathf.Clamp01(Mathf.Abs(delta) / 90f));

                Vector3 forward = Quaternion.Euler(0f, _heading, 0f) * Vector3.forward;
                position += forward * (_moveSpeed * turnSlowdown * dt);

                _wakeTimer += dt;
                if (ripples != null && _wakeTimer >= _wakeInterval)
                {
                    _wakeTimer = 0f;
                    EmitWake(ripples);
                }

                if (_cave != null)
                {
                    float depth = _cave.Depth01(position);
                    ApplyTint(Mathf.Clamp01(depth));
                    if (depth >= _cave.HiddenDepth)
                    {
                        _hiddenTimer = 0f;
                        SetRenderersEnabled(false);
                    }
                }
            }

            position.y = WaterY(bob);
            transform.SetPositionAndRotation(position, Quaternion.Euler(pitch, _heading, roll));
        }

        private void AdvanceTarget()
        {
            int last = _path.Count - 1;
            if (_cave == null && _pingPong)
            {
                if (_target + _direction > last || _target + _direction < 0)
                {
                    _direction = -_direction;
                }

                _target += _direction;
                return;
            }

            if (_target < last)
            {
                _target++;
            }
            else if (_loop && _cave == null)
            {
                _target = 0;
            }
        }

        private void EmitWake(WaterRipplePool ripples)
        {
            float waterY = _water != null ? _water.Level + 0.02f : transform.position.y;
            for (int side = -1; side <= 1; side += 2)
            {
                Vector3 local = new Vector3(_wakeOffset.x * side, 0f, _wakeOffset.z);
                Vector3 world = transform.TransformPoint(local);
                world.y = waterY;
                ripples.Emit(world, 0.9f, 1.2f, 0.8f);
            }
        }

        private float WaterY(float bob)
        {
            float level = _water != null ? _water.Level : transform.position.y;
            return level + _waterlineOffset + bob;
        }

        /// <summary>Darkens the boat as it goes deeper into the cave (0 = normal, 1 = full cave tint).</summary>
        private void ApplyTint(float amount)
        {
            if (_renderers == null || Mathf.Abs(amount - _tint) < 0.01f)
            {
                return;
            }

            _tint = amount;
            Color tint = _cave != null ? Color.Lerp(Color.white, _cave.CaveTint, amount) : Color.white;
            for (int i = 0; i < _renderers.Length; i++)
            {
                if (amount <= 0f)
                {
                    _renderers[i].SetPropertyBlock(null);
                    continue;
                }

                _renderers[i].GetPropertyBlock(_block);
                _block.SetColor(BaseColorId, tint);
                _renderers[i].SetPropertyBlock(_block);
            }
        }

        private void SetRenderersEnabled(bool enabled)
        {
            for (int i = 0; i < _renderers.Length; i++)
            {
                _renderers[i].enabled = enabled;
            }
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (_path == null || _path.Count < 2)
            {
                return;
            }

            Gizmos.color = Color.cyan;
            for (int i = 0; i < _path.Count - 1; i++)
            {
                Gizmos.DrawLine(_path.GetPoint(i), _path.GetPoint(i + 1));
            }
        }
#endif
    }
}
