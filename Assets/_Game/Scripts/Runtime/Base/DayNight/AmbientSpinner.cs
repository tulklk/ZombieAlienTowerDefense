using UnityEngine;

namespace AlienDefense.Base.DayNight
{
    /// <summary>Slowly turns a decorative part (a landmark's floating ring, a radar dish) round its local up axis,
    /// plus an optional gentle bob. Decoration only - nothing reads it. Lives outside the static-batched Environment
    /// root (batched meshes cannot move). Stops with BaseWorld (Update does not run while it is hidden).</summary>
    public sealed class AmbientSpinner : MonoBehaviour
    {
        [SerializeField]
        private float _degreesPerSecond = 20f;

        [SerializeField, Min(0f)]
        private float _bobHeight;

        [SerializeField, Min(0.1f)]
        private float _bobSeconds = 3f;

        private Vector3 _restPosition;
        private float _phase;

        private void Awake()
        {
            _restPosition = transform.localPosition;
            _phase = (transform.position.x * 0.37f + transform.position.z * 0.21f) % 6.28f;
        }

        private void Update()
        {
            float deltaTime = Time.deltaTime;
            transform.Rotate(0f, _degreesPerSecond * deltaTime, 0f, Space.Self);
            if (_bobHeight > 0f)
            {
                float t = Time.time * (6.2831853f / _bobSeconds) + _phase;
                transform.localPosition = _restPosition + Vector3.up * (Mathf.Sin(t) * _bobHeight);
            }
        }

        public void Configure(float degreesPerSecond, float bobHeight)
        {
            _degreesPerSecond = degreesPerSecond;
            _bobHeight = bobHeight;
        }
    }
}
