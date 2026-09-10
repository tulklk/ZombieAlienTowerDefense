using UnityEngine;

namespace AlienDefense.Player
{
    /// <summary>Keeps one beam particle system's live particles inside the actual visible cone (BeamConeOuter)
    /// and optionally funnels them toward the vertical axis as they age. Attached to PS_EnergySparks,
    /// PS_EnergyStreaks and PS_EnergyOrbs alike - the funnel pull (_pullStrengthStart/End) is tuned per system
    /// (0 disables it), but the hard radius clamp against the cone's real taper always applies to all three, so
    /// none of them can visibly spill outside the beam regardless of per-particle random speed/noise.
    ///
    /// Two separate mechanisms:
    /// 1) Optional soft inward pull on velocity (X/Z only, Y untouched) - a gentle "funnel" look over a
    ///    particle's lifetime. Fallback for when Particle System modules alone aren't enough: Velocity-over-
    ///    Lifetime's own "radial" property pulls toward the shape's origin in full 3D (not just horizontally) -
    ///    on this beam that origin sits at the ground, so a strong radial pull measurably fights the particles'
    ///    upward motion too (confirmed via GetParticles sampling, not assumed).
    /// 2) A hard per-frame clamp: BeamConeOuter's mesh (Mesh_TractorBeamCone) is a straight-sided cone, base
    ///    radius 1 at mesh Y=0 down to 0.15 at mesh Y=1 (read directly off the mesh, not guessed - see
    ///    UFOTractorBeamVisual.ConeTaperRatio). A particle's allowed horizontal radius at any moment is
    ///    therefore Lerp(baseRadius, topRadius, currentHeight / beamLength), shrunk by a small safety margin.
    ///    Exceeding it clamps the X/Z position back onto the cone surface every frame - a continuous per-frame
    ///    correction as the particle's own trajectory would otherwise cross the boundary, not a discontinuous
    ///    jump, so it never reads as a teleport.
    ///
    /// GetParticles/SetParticles only - never Instantiates a GameObject per particle.</summary>
    [RequireComponent(typeof(ParticleSystem))]
    public sealed class UFOBeamParticleAttractor : MonoBehaviour
    {
        [SerializeField, Min(0f)]
        [Tooltip("Inward funnel pull strength at the START of a particle's life. 0 disables the funnel effect " +
            "entirely (the hard radius clamp below still applies either way).")]
        private float _pullStrengthStart;

        [SerializeField, Min(0f)]
        [Tooltip("Inward funnel pull strength at the END of a particle's life - stronger, so particles visibly " +
            "funnel together right before reaching the UFO. 0 disables the funnel effect.")]
        private float _pullStrengthEnd;

        [SerializeField, Min(1)]
        private int _maxParticlesToProcess = 80;

        [SerializeField, Min(0f)]
        [Tooltip("Local-space Y a particle is not allowed to pass (kept in sync with the real beam length via " +
            "SetMaxHeight, called from UFOTractorBeamVisual.SetBeamLength) - a size/speed outlier that would " +
            "otherwise overshoot into the UFO model is clamped here and hidden (startSize forced to 0) instead, " +
            "so particles can never visibly pierce the UFO regardless of their randomized speed roll.")]
        private float _maxHeight = 3.2f;

        [SerializeField, Min(0f)]
        [Tooltip("Allowed horizontal radius at ground level (Y=0), kept in sync with the beam's real visual " +
            "radius via SetRadiusProfile.")]
        private float _baseRadius = 0.98f;

        [SerializeField, Min(0f)]
        [Tooltip("Allowed horizontal radius at the top (Y=_maxHeight, right at the UFO), kept in sync via " +
            "SetRadiusProfile. BeamConeOuter's mesh tapers to 15% of its base radius, so this is normally much " +
            "smaller than _baseRadius.")]
        private float _topRadius = 0.15f;

        [SerializeField, Range(0.5f, 1f)]
        [Tooltip("Safety margin applied to the cone-taper radius clamp so particles sit clearly inside the " +
            "visible cone surface instead of exactly on its edge.")]
        private float _radiusSafetyMargin = 0.9f;

        private ParticleSystem _particles;
        private ParticleSystem.Particle[] _buffer;

        private void Awake()
        {
            _particles = GetComponent<ParticleSystem>();
            _buffer = new ParticleSystem.Particle[_maxParticlesToProcess];
        }

        /// <summary>Keeps the overshoot clamp in sync with the beam's real length. Safe to call every time the
        /// beam's geometry changes (cheap - just a field write).</summary>
        public void SetMaxHeight(float height)
        {
            _maxHeight = Mathf.Max(0f, height);
        }

        /// <summary>Keeps the cone-taper radius clamp in sync with the beam's real visual radius. Safe to call
        /// every time the beam's geometry changes (cheap - just field writes).</summary>
        public void SetRadiusProfile(float baseRadius, float topRadius)
        {
            _baseRadius = Mathf.Max(0f, baseRadius);
            _topRadius = Mathf.Max(0f, topRadius);
        }

        private void LateUpdate()
        {
            // _buffer is only null if this LateUpdate beat Awake, which happens during Play Mode teardown -
            // GetParticles(null) throws ArgumentNullException rather than returning 0, so it must be guarded.
            if (_particles == null || _buffer == null || !_particles.isPlaying && _particles.particleCount == 0)
            {
                return;
            }

            int count = _particles.GetParticles(_buffer);
            if (count == 0)
            {
                return;
            }

            float dt = Time.deltaTime;
            if (dt <= 0f)
            {
                return;
            }

            bool hasFunnelPull = _pullStrengthStart > 0f || _pullStrengthEnd > 0f;

            for (int i = 0; i < count; i++)
            {
                ParticleSystem.Particle p = _buffer[i];

                if (hasFunnelPull)
                {
                    float normalizedAge = p.startLifetime > 0f
                        ? Mathf.Clamp01(1f - (p.remainingLifetime / p.startLifetime))
                        : 0f;
                    float pull = Mathf.Lerp(_pullStrengthStart, _pullStrengthEnd, normalizedAge);

                    // Horizontal-only (local X/Z) - Y is left completely untouched so this can never fight the
                    // upward travel, unlike the built-in Velocity-over-Lifetime "radial" property.
                    Vector3 velocity = p.velocity;
                    Vector3 horizontal = new Vector3(p.position.x, 0f, p.position.z);
                    velocity -= horizontal * pull * dt;
                    p.velocity = velocity;
                }

                if (_maxHeight > 0f && p.position.y >= _maxHeight)
                {
                    Vector3 pos = p.position;
                    pos.y = _maxHeight;
                    p.position = pos;
                    p.startSize = 0f; // hide instead of letting a fast/long-lived outlier fly into the UFO model
                }

                // Hard clamp to the cone's real taper (base -> top), independent of the funnel pull above - this
                // is what actually guarantees "never outside the beam" regardless of spawn spread or noise drift.
                if (_maxHeight > 0f)
                {
                    float t = Mathf.Clamp01(p.position.y / _maxHeight);
                    float allowedRadius = Mathf.Lerp(_baseRadius, _topRadius, t) * _radiusSafetyMargin;
                    Vector3 horizontalPos = new Vector3(p.position.x, 0f, p.position.z);
                    float dist = horizontalPos.magnitude;
                    if (dist > allowedRadius && dist > 0.0001f)
                    {
                        Vector3 clamped = horizontalPos * (allowedRadius / dist);
                        p.position = new Vector3(clamped.x, p.position.y, clamped.z);

                        // Kill the outward-pointing part of the velocity so the particle doesn't immediately
                        // push past the boundary again next frame.
                        Vector3 outwardDir = horizontalPos / dist;
                        Vector3 vel = p.velocity;
                        float outwardSpeed = Vector3.Dot(new Vector3(vel.x, 0f, vel.z), outwardDir);
                        if (outwardSpeed > 0f)
                        {
                            vel -= outwardDir * outwardSpeed;
                            p.velocity = vel;
                        }
                    }
                }

                _buffer[i] = p;
            }

            _particles.SetParticles(_buffer, count);
        }
    }
}
