using UnityEngine;

namespace AlienDefense.Base.River
{
    /// <summary>The single Update for everything alive on the river: boats, ducks, and the foam that breaks against
    /// the cave rocks. It lives in the base world, which is only active while the Base tab is open, so none of this
    /// costs anything elsewhere in the menu.
    ///
    /// Start positions are randomised once per session (ducks inside their areas, boats along their routes), so the
    /// river never opens on the exact same frame.</summary>
    public sealed class RiverAmbientManager : MonoBehaviour
    {
        [SerializeField]
        private RiverChannel _channel;

        [SerializeField]
        private RiverWaterLevel _water;

        [SerializeField]
        private WaterRipplePool _ripples;

        [SerializeField]
        private RiverBoatMover[] _boats = System.Array.Empty<RiverBoatMover>();

        [SerializeField]
        private RiverDuckSwimmer[] _ducks = System.Array.Empty<RiverDuckSwimmer>();

        [Header("Cave foam")]
        [SerializeField]
        [Tooltip("Points where water breaks against the cave rocks; a foam ring is emitted at one of them periodically.")]
        private Transform[] _foamPoints = System.Array.Empty<Transform>();

        [SerializeField, Min(0.1f)]
        private float _foamInterval = 0.5f;

        private bool _initialized;
        private float _foamTimer;
        private int _foamIndex;

        private void OnEnable()
        {
            if (_initialized)
            {
                return;
            }

            _initialized = true;
            var random = new System.Random(System.Environment.TickCount);
            for (int i = 0; i < _boats.Length; i++)
            {
                if (_boats[i] != null) _boats[i].Initialize(random);
            }

            for (int i = 0; i < _ducks.Length; i++)
            {
                RiverDuckSwimmer duck = _ducks[i];
                if (duck == null)
                {
                    continue;
                }

                // Scatter each duck a little around where it was placed, but only onto water.
                Vector3 start = duck.transform.position;
                for (int attempt = 0; attempt < 8; attempt++)
                {
                    Vector3 candidate = start + new Vector3((float)(random.NextDouble() - 0.5) * 6f, 0f,
                        (float)(random.NextDouble() - 0.5) * 3f);
                    if (_channel == null || _channel.DistanceInside(candidate, out _) > 1.5f)
                    {
                        start = candidate;
                        break;
                    }
                }

                duck.Initialize(random, start);
            }
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            float time = Time.time;

            for (int i = 0; i < _boats.Length; i++)
            {
                if (_boats[i] != null) _boats[i].Tick(dt, time, _ripples);
            }

            for (int i = 0; i < _ducks.Length; i++)
            {
                if (_ducks[i] != null) _ducks[i].Tick(dt, time, _ducks, _boats, _ripples);
            }

            if (_ripples != null && _foamPoints.Length > 0)
            {
                _foamTimer += dt;
                if (_foamTimer >= _foamInterval)
                {
                    _foamTimer = 0f;
                    Transform point = _foamPoints[_foamIndex++ % _foamPoints.Length];
                    if (point != null)
                    {
                        Vector3 p = point.position;
                        p.y = _water != null ? _water.Level + 0.02f : p.y;
                        _ripples.Emit(p, 1.1f, 1.4f, 0.7f);
                    }
                }
            }
        }
    }
}
