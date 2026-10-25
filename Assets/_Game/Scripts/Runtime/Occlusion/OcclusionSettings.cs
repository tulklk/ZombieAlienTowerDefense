using UnityEngine;

namespace AlienDefense.Occlusion
{
    /// <summary>Every tunable of the "UFO hidden behind scenery" fade in one asset (assigned to
    /// PlayerOcclusionController). The scenery is never changed: while something stands between the camera and the
    /// UFO, the UFO's body is drawn see-through on top of it (UFOOcclusionFade + the UFOOcclusionFade shader).</summary>
    [CreateAssetMenu(fileName = "OcclusionSettings", menuName = "AlienDefense/Rendering/Occlusion Settings")]
    public sealed class OcclusionSettings : ScriptableObject
    {
        [Header("UFO look while hidden")]
        [SerializeField]
        [Tooltip("Shader of the UFO's faded copies (AlienDefense/UFOOcclusionFade). Referenced here so builds include it.")]
        private Shader _fadeShader;

        [SerializeField, Range(0.1f, 1f)]
        [Tooltip("UFO opacity while something hides it (1 = fully opaque).")]
        private float _occludedAlpha = 0.38f;

        [SerializeField]
        [Tooltip("Green the UFO turns while hidden, so it reads against the scenery. Default #39FF72.")]
        private Color _tintColor = new Color(0.224f, 1f, 0.447f, 1f);

        [SerializeField, Range(0f, 1f)]
        [Tooltip("How green the UFO is at full fade. 0 = alpha only (own colours), 1 = fully green.")]
        private float _tintStrength = 0.85f;

        [SerializeField]
        [Tooltip("Hide the tractor beam's visuals (cone, ground ring, particles) while the UFO is faded, so the beam " +
            "does not show through the scenery covering it. Visual only - the beam keeps capturing.")]
        private bool _hideTractorBeam = true;

        [Header("Fade")]
        [SerializeField, Min(0.01f)]
        [Tooltip("Seconds from fully visible to the hidden alpha.")]
        private float _fadeInDuration = 0.2f;

        [SerializeField, Min(0.01f)]
        [Tooltip("Seconds from the hidden alpha back to fully visible.")]
        private float _fadeOutDuration = 0.2f;

        [SerializeField, Min(0f)]
        [Tooltip("Anti-flicker: once hidden, the UFO stays faded at least this long.")]
        private float _minimumHiddenTime = 0.10f;

        [SerializeField, Min(0f)]
        [Tooltip("Anti-flicker: the UFO returns to normal only after nothing has hidden it for this long (also " +
            "bridges the gap when flying from one building straight behind the next).")]
        private float _restoreDelay = 0.12f;

        [Header("Detection")]
        [SerializeField, Min(0.01f)]
        [Tooltip("Seconds between occlusion checks (0.04 = 25 per second). The fade still animates every frame.")]
        private float _checkInterval = 0.04f;

        [SerializeField, Min(0f)]
        [Tooltip("Radius of the camera -> UFO sweep: scenery counts as hiding the UFO when it comes this close to " +
            "the line of sight.")]
        private float _castRadius = 0.55f;

        [SerializeField, Min(0f)]
        [Tooltip("UFO inside / touching scenery: within this distance of its box (and not behind it) the UFO fades " +
            "even when the line of sight misses the surface. About the UFO's radius (1.1 m).")]
        private float _insideCheckRadius = 1.1f;

        public Shader FadeShader => _fadeShader;
        public float OccludedAlpha => _occludedAlpha;
        public Color TintColor => _tintColor;
        public float TintStrength => _tintStrength;
        public bool HideTractorBeam => _hideTractorBeam;
        public float FadeInDuration => _fadeInDuration;
        public float FadeOutDuration => _fadeOutDuration;
        public float MinimumHiddenTime => _minimumHiddenTime;
        public float RestoreDelay => _restoreDelay;
        public float CheckInterval => _checkInterval;
        public float CastRadius => _castRadius;
        public float InsideCheckRadius => _insideCheckRadius;
    }
}
