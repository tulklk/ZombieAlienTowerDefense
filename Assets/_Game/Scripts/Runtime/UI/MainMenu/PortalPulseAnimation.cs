using UnityEngine;

namespace AlienDefense.UI.MainMenu
{
    /// <summary>Very slow scale pulse for the portal ring behind the level diorama preview — cheap placeholder
    /// VFX (see section 24/60 of the MainMenu spec: "very slow pulse/rotation... not too bright", "avoid heavy
    /// shader transitions"). Only ever touches its own localScale, unscaled time so it keeps pulsing even if
    /// Time.timeScale is 0.</summary>
    public sealed class PortalPulseAnimation : MonoBehaviour
    {
        [SerializeField, Range(0f, 0.2f)]
        private float _amplitude = 0.06f;

        [SerializeField, Min(0.01f)]
        private float _frequency = 0.25f;

        private Vector3 _baseScale;

        private void Awake()
        {
            _baseScale = transform.localScale;
        }

        private void Update()
        {
            float pulse = 1f + Mathf.Sin(Time.unscaledTime * _frequency * Mathf.PI * 2f) * _amplitude;
            transform.localScale = _baseScale * pulse;
        }
    }
}
