using UnityEngine;

namespace AlienDefense.Environment
{
    /// <summary>Constant slow spin for decorative parts (windmill blades, wind-pump fans). Pure Transform rotation -
    /// no physics, no allocation - and it stops ticking while the watched renderer is off screen.</summary>
    [DisallowMultipleComponent]
    public sealed class AmbientSpinner : MonoBehaviour
    {
        [SerializeField]
        private Vector3 _localAxis = Vector3.forward;

        [SerializeField]
        private float _degreesPerSecond = 25f;

        [SerializeField]
        [Tooltip("Optional. When assigned, the spin only runs while this renderer is visible to a camera.")]
        private Renderer _visibilityRenderer;

        public void Configure(Vector3 localAxis, float degreesPerSecond, Renderer visibilityRenderer)
        {
            _localAxis = localAxis;
            _degreesPerSecond = degreesPerSecond;
            _visibilityRenderer = visibilityRenderer;
        }

        private void Update()
        {
            if (_visibilityRenderer != null && !_visibilityRenderer.isVisible)
            {
                return;
            }

            transform.Rotate(_localAxis, _degreesPerSecond * Time.deltaTime, Space.Self);
        }
    }
}
