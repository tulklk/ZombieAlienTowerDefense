using AlienDefense.Enemies;
using AlienDefense.Environment;
using AlienDefense.Pickups;
using UnityEngine;

namespace AlienDefense.Player
{
    /// <summary>Pure visual reaction to UFOTractorBeamController's counts/events: cone (Outer only — Inner is
    /// kept as a wired-but-always-hidden slot, see Initialize), ground glow/ring, top glow, particles, capture
    /// flash. Never decides capture/absorption, never scans any registry, never touches gameplay state, never
    /// Instantiates/Destroys per target. Reacts identically to Enemy/Energy/Prop — see
    /// HandleTotalActiveAbsorptionCountChanged.</summary>
    public sealed class UFOTractorBeamVisual : MonoBehaviour
    {
        // AlienDefense/UFOTractorBeam (and Shader Graphs/UFOTractorBeam via UFOTractorBeamFunction.hlsl)
        // use _BeamColor, not URP _BaseColor. Scaling its alpha drives idle/capturing fade via MPB.
        private static readonly int BeamColorId = Shader.PropertyToID("_BeamColor");
        private static readonly int FlowSpeedId = Shader.PropertyToID("_FlowSpeed");

        [Header("Cone (top small -> bottom wide, scaled via Transform only)")]
        [SerializeField]
        private MeshRenderer _beamConeOuter;

        [SerializeField]
        private MeshRenderer _beamConeInner;

        [SerializeField, Range(0.1f, 1f)]
        [Tooltip("Cone/ground-glow radius as a fraction of the gameplay AttractionRadius — keeps the visual " +
            "beam narrower than the actual capture area (see SetBeamRadius) instead of rendering it exactly " +
            "as wide as enemies can be pulled in from. Kept at its narrowest baseline on purpose: a future " +
            "level-up system is expected to raise this value as the player upgrades the tractor beam.")]
        private float _visualRadiusScale = 0.28f;

        [Header("Ground")]
        [SerializeField]
        private GameObject _groundGlow;

        [SerializeField]
        private GameObject _groundRing;

        [Header("Top (bright patch just under the UFO)")]
        [SerializeField]
        private GameObject _beamTopGlow;

        [Header("Particles (one system for every enemy currently being captured, never per-Enemy)")]
        [SerializeField]
        [Tooltip("PS_EnergySparks — small billboard sparkles scattered through the beam.")]
        private ParticleSystem _beamParticles;

        [SerializeField]
        [Tooltip("Optional. PS_EnergyStreaks — larger stretched-billboard streaks, same ground-to-UFO motion " +
            "as _beamParticles but bigger/rarer, read as the beam actively pulling something up.")]
        private ParticleSystem _beamStreakParticles;

        [SerializeField]
        [Tooltip("Optional. PS_EnergyOrbs — occasional small mesh 'resource orb' particles pulled up alongside " +
            "the streaks/sparks. Constant light trickle, not scaled by capture intensity like the other two.")]
        private ParticleSystem _beamOrbParticles;

        [SerializeField]
        [Tooltip("Persistent system on the UFO; a capture just Emits a burst on it, never Instantiates.")]
        private ParticleSystem _captureFlashParticles;

        [Header("Fade")]
        [SerializeField, Min(0.01f)]
        private float _fadeInDuration = 0.2f;

        [SerializeField, Min(0.01f)]
        private float _fadeOutDuration = 0.3f;

        [SerializeField, Range(0f, 1f)]
        private float _idleIntensity = 0.3f;

        [Header("Pulse (ground ring only, very subtle)")]
        [SerializeField, Min(0f)]
        private float _pulseFrequency = 1f;

        [SerializeField, Range(0f, 0.2f)]
        private float _pulseScaleAmplitude = 0.04f;

        [Header("Particle Emission Tuning (_beamParticles / PS_EnergySparks)")]
        [SerializeField, Min(0f)]
        private float _idleEmissionRate = 3f;

        [SerializeField, Min(0f)]
        private float _maxEmissionRate = 45f;

        [Header("Particle Emission Tuning (_beamStreakParticles / PS_EnergyStreaks)")]
        [SerializeField, Min(0f)]
        private float _idleStreakEmissionRate = 10f;

        [SerializeField, Min(0f)]
        private float _maxStreakEmissionRate = 32f;

        [SerializeField, Min(0)]
        private int _captureFlashBurstCount = 10;

        private UFOTractorBeamController _controller;
        private MaterialPropertyBlock _outerBlock;
        private Color _outerBaseColor;
        private Vector3 _groundRingBaseScale = Vector3.one;

        private float _targetIntensity;
        private float _displayedIntensity;
        private bool _hasEmissionModule;
        private ParticleSystem.EmissionModule _emissionModule;
        private bool _hasStreakEmissionModule;
        private ParticleSystem.EmissionModule _streakEmissionModule;
        private UFOBeamParticleAttractor _beamSparkAttractor;
        private UFOBeamParticleAttractor _beamStreakAttractor;
        private UFOBeamParticleAttractor _beamOrbAttractor;
        private bool _isBeamEnabled;

        // Mesh_TractorBeamCone is a straight-sided cone: radius 1 at its base (mesh Y=0) tapering to 0.15 at its
        // top (mesh Y=1) - read directly off the mesh's vertices, not guessed. Ground-spawned particles must
        // shrink their allowed horizontal radius by this same ratio as they rise, or they visibly poke outside
        // the visible cone well before reaching the (much narrower) top - see UFOBeamParticleAttractor.
        private const float ConeTaperRatio = 0.15f;

        public void Initialize(UFOTractorBeamController controller)
        {
            Unsubscribe();
            _controller = controller;

            _outerBlock = new MaterialPropertyBlock();

            if (_beamConeOuter != null && _beamConeOuter.sharedMaterial != null)
            {
                _outerBaseColor = _beamConeOuter.sharedMaterial.GetColor(BeamColorId);
            }

            if (_groundRing != null)
            {
                _groundRingBaseScale = _groundRing.transform.localScale;
            }

            if (_beamConeInner != null)
            {
                // The small inner "energy core" cone as a distinct capture-only indicator has been retired —
                // per request, capturing is now communicated entirely by the Outer cone's own intensity ramp
                // (idle -> full, see ApplyIntensity/HandleTotalActiveAbsorptionCountChanged). The reference is
                // kept (rather than removed) so existing prefabs don't dangle a broken slot; always force it
                // hidden instead.
                _beamConeInner.gameObject.SetActive(false);
            }

            if (_beamParticles != null)
            {
                _emissionModule = _beamParticles.emission;
                _hasEmissionModule = true;
                _beamSparkAttractor = _beamParticles.GetComponent<UFOBeamParticleAttractor>();
            }

            if (_beamStreakParticles != null)
            {
                _streakEmissionModule = _beamStreakParticles.emission;
                _hasStreakEmissionModule = true;
                _beamStreakAttractor = _beamStreakParticles.GetComponent<UFOBeamParticleAttractor>();
            }

            if (_beamOrbParticles != null)
            {
                _beamOrbAttractor = _beamOrbParticles.GetComponent<UFOBeamParticleAttractor>();
            }

            if (_controller != null)
            {
                _controller.TotalActiveAbsorptionCountChanged += HandleTotalActiveAbsorptionCountChanged;
                _controller.EnemyCaptureCompleted += HandleEnemyCaptureCompleted;
                _controller.EnergyPickupCollected += HandleEnergyPickupCollected;
                _controller.PropAbsorbed += HandlePropAbsorbed;
                _controller.BeamEnabledChanged += HandleBeamEnabledChanged;
                _controller.BeamGeometryChanged += HandleBeamGeometryChanged;
            }

            HandleBeamEnabledChanged(_controller != null && _controller.IsEnabled);
            HandleTotalActiveAbsorptionCountChanged(_controller != null ? _controller.TotalActiveAbsorptionCount : 0);
            if (_controller != null && _controller.BeamLength > 0f)
            {
                HandleBeamGeometryChanged(_controller.BeamLength, _controller.AttractionRadius);
            }

            _displayedIntensity = _targetIntensity;
            ApplyIntensity(_displayedIntensity);
        }

        private void Update()
        {
            if (!_isBeamEnabled && _displayedIntensity <= 0.001f)
            {
                return;
            }

            float rate = _displayedIntensity < _targetIntensity ? 1f / _fadeInDuration : 1f / _fadeOutDuration;
            _displayedIntensity = Mathf.MoveTowards(_displayedIntensity, _targetIntensity, rate * Time.deltaTime);

            float pulse = 1f;
            if (_groundRing != null && _isBeamEnabled)
            {
                pulse = 1f + Mathf.Sin(Time.time * _pulseFrequency * Mathf.PI * 2f) * _pulseScaleAmplitude;
                _groundRing.transform.localScale = _groundRingBaseScale * pulse;
            }

            ApplyIntensity(_displayedIntensity);
        }

        /// <summary>Beam length: distance from CaptureSocket to BeamGroundAnchor. Beam radius: gameplay
        /// AttractionRadius. Only the Transform is touched — the cone mesh itself is never regenerated.</summary>
        private void HandleBeamGeometryChanged(float length, float radius)
        {
            SetBeamLength(length);
            SetBeamRadius(radius);
        }

        public void SetBeamLength(float length)
        {
            ApplyConeScale(_beamConeOuter, length, keepRadius: true);
            ApplyConeScale(_beamConeInner, length, keepRadius: true);
            _beamSparkAttractor?.SetMaxHeight(length);
            _beamStreakAttractor?.SetMaxHeight(length);
            _beamOrbAttractor?.SetMaxHeight(length);
        }

        public void SetBeamRadius(float radius)
        {
            // radius here is UFOTractorBeamController.AttractionRadius (a gameplay value — how far the beam
            // actually reaches to pull enemies in). Rendering the cones at that same radius made the beam's
            // ground-level flare nearly 2 screen-widths wide once the camera was framed close enough to read
            // the UFO clearly (measured directly via Camera.WorldToScreenPoint) — badly out of proportion with
            // the reference. _visualRadiusScale lets the VISUAL beam be narrower than the actual capture area
            // without touching gameplay (AttractionRadius, and therefore ScanForNewCaptures' admission range,
            // is untouched).
            float visualRadius = radius * _visualRadiusScale;
            ApplyConeRadius(_beamConeOuter, visualRadius);
            // Inner beam reads clearly as "energy core" only if it stays visibly narrower than Outer.
            ApplyConeRadius(_beamConeInner, visualRadius * 0.6f);

            if (_groundGlow != null)
            {
                _groundGlow.transform.localScale = new Vector3(visualRadius * 2f, 1f, visualRadius * 2f);
            }

            if (_groundRing != null)
            {
                _groundRingBaseScale = new Vector3(visualRadius * 2f, 1f, visualRadius * 2f);
                _groundRing.transform.localScale = _groundRingBaseScale;
            }

            // Keeps each particle system's Shape.radius (a Cone shape's base radius) synced to the same
            // visual radius the cones/glow use, instead of a stale hand-tuned value left over from before
            // AttractionRadius was rescaled — see the _visualRadiusScale comment above.
            // Ground-spawn systems use a Circle shape sized ~80% of the visual beam radius (spec: 70-90%) so
            // particles spawn spread across the beam's footprint but clearly inside its edge, not exactly on it.
            ApplyParticleShapeRadius(_beamParticles, visualRadius * 0.8f);
            ApplyParticleShapeRadius(_beamStreakParticles, visualRadius * 0.8f);
            ApplyParticleShapeRadius(_beamOrbParticles, visualRadius * 0.8f);

            // Keeps each attractor's hard cone-taper clamp synced to the real visual radius, so particles that
            // rise toward the (much narrower) top can never end up outside BeamConeOuter's actual silhouette.
            float topRadius = visualRadius * ConeTaperRatio;
            _beamSparkAttractor?.SetRadiusProfile(visualRadius, topRadius);
            _beamStreakAttractor?.SetRadiusProfile(visualRadius, topRadius);
            _beamOrbAttractor?.SetRadiusProfile(visualRadius, topRadius);
        }

        private static void ApplyParticleShapeRadius(ParticleSystem particles, float radius)
        {
            if (particles == null || radius <= 0f)
            {
                return;
            }

            ParticleSystem.ShapeModule shape = particles.shape;
            shape.radius = radius;
        }

        /// <summary>Overrides how fast the beam's energy streaks travel (AlienDefense/UFOTractorBeam's
        /// _FlowSpeed) — e.g. a faster pull for a stronger tractor-beam upgrade tier. Uses the same
        /// MaterialPropertyBlock as the intensity fade, never renderer.material, so this never clones the
        /// shared beam material.</summary>
        public void SetFlowSpeed(float speed)
        {
            if (_beamConeOuter == null)
            {
                return;
            }

            _outerBlock ??= new MaterialPropertyBlock();
            _beamConeOuter.GetPropertyBlock(_outerBlock);
            _outerBlock.SetFloat(FlowSpeedId, speed);
            _beamConeOuter.SetPropertyBlock(_outerBlock);
        }

        private static void ApplyConeScale(MeshRenderer cone, float length, bool keepRadius)
        {
            if (cone == null || length <= 0f)
            {
                return;
            }

            Vector3 scale = cone.transform.localScale;
            scale.y = length;
            cone.transform.localScale = scale;
        }

        private static void ApplyConeRadius(MeshRenderer cone, float radius)
        {
            if (cone == null || radius <= 0f)
            {
                return;
            }

            Vector3 scale = cone.transform.localScale;
            scale.x = radius;
            scale.z = radius;
            cone.transform.localScale = scale;
        }

        /// <summary>count is the SUM across Enemy capture + Energy absorption + Prop absorption (see
        /// UFOTractorBeamController.TotalActiveAbsorptionCount) — the beam visual never needs to know which
        /// category is currently active, only "how much is happening right now".</summary>
        private void HandleTotalActiveAbsorptionCountChanged(int count)
        {
            bool isActive = count > 0;

            // BeamTopGlow used to only switch on while something was actively being captured, reading as "the
            // beam only turns on once a zombie arrives" — per request it now stays on continuously whenever the
            // beam itself is enabled (see HandleBeamEnabledChanged), independent of capture count. Cone/particle
            // intensity still ramps up during an active capture below, so a capture still reads as "brighter".

            _targetIntensity = _isBeamEnabled ? (isActive ? 1f : _idleIntensity) : 0f;

            if (_hasEmissionModule || _hasStreakEmissionModule)
            {
                int max = _controller != null
                    ? _controller.MaxConcurrentCaptures + _controller.MaxConcurrentEnergyAbsorptions + _controller.MaxConcurrentPropAbsorptions
                    : 0;
                float intensity01 = max > 0 ? Mathf.Clamp01((float)count / max) : (isActive ? 1f : 0f);

                if (_hasEmissionModule)
                {
                    _emissionModule.rateOverTime = Mathf.Lerp(_idleEmissionRate, _maxEmissionRate, intensity01);
                }

                if (_hasStreakEmissionModule)
                {
                    _streakEmissionModule.rateOverTime = Mathf.Lerp(_idleStreakEmissionRate, _maxStreakEmissionRate, intensity01);
                }
            }
        }

        private void HandleEnemyCaptureCompleted(EnemyController enemy)
        {
            _captureFlashParticles?.Emit(_captureFlashBurstCount);
        }

        private void HandleEnergyPickupCollected(EnergyPickupController pickup)
        {
            _captureFlashParticles?.Emit(_captureFlashBurstCount);
        }

        private void HandlePropAbsorbed(TractorAbsorbableProp prop)
        {
            _captureFlashParticles?.Emit(_captureFlashBurstCount);
        }

        private void HandleBeamEnabledChanged(bool isEnabled)
        {
            _isBeamEnabled = isEnabled;

            if (_groundGlow != null)
            {
                _groundGlow.SetActive(isEnabled);
            }

            if (_groundRing != null)
            {
                _groundRing.SetActive(isEnabled);
            }

            // Always on while the beam is enabled (default true — see UFOTractorBeamController._isEnabled),
            // not just while a capture is in progress. Only SetEnabled(false) (pause/game-over, if ever wired
            // up) turns it off.
            if (_beamTopGlow != null)
            {
                _beamTopGlow.SetActive(isEnabled);
            }

            int currentCount = _controller != null ? _controller.TotalActiveAbsorptionCount : 0;
            _targetIntensity = isEnabled ? (currentCount > 0 ? 1f : _idleIntensity) : 0f;

            if (_beamParticles != null)
            {
                if (isEnabled && !_beamParticles.isPlaying)
                {
                    _beamParticles.Play();
                }
                else if (!isEnabled && _beamParticles.isPlaying)
                {
                    _beamParticles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                }
            }

            if (_beamStreakParticles != null)
            {
                if (isEnabled && !_beamStreakParticles.isPlaying)
                {
                    _beamStreakParticles.Play();
                }
                else if (!isEnabled && _beamStreakParticles.isPlaying)
                {
                    _beamStreakParticles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                }
            }

            if (_beamOrbParticles != null)
            {
                if (isEnabled && !_beamOrbParticles.isPlaying)
                {
                    _beamOrbParticles.Play();
                }
                else if (!isEnabled && _beamOrbParticles.isPlaying)
                {
                    _beamOrbParticles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                }
            }
        }

        private void ApplyIntensity(float intensity)
        {
            if (_beamConeOuter != null)
            {
                _outerBlock.SetColor(BeamColorId, ScaleAlpha(_outerBaseColor, intensity));
                _beamConeOuter.SetPropertyBlock(_outerBlock);
            }

            // Inner cone retired as a separate "capture only" indicator (always hidden, see Initialize) — the
            // Outer cone alone now carries the idle -> capturing intensity ramp.
        }

        private static Color ScaleAlpha(Color color, float scale)
        {
            color.a *= Mathf.Clamp01(scale);
            return color;
        }

        public void Unsubscribe()
        {
            if (_controller != null)
            {
                _controller.TotalActiveAbsorptionCountChanged -= HandleTotalActiveAbsorptionCountChanged;
                _controller.EnemyCaptureCompleted -= HandleEnemyCaptureCompleted;
                _controller.EnergyPickupCollected -= HandleEnergyPickupCollected;
                _controller.PropAbsorbed -= HandlePropAbsorbed;
                _controller.BeamEnabledChanged -= HandleBeamEnabledChanged;
                _controller.BeamGeometryChanged -= HandleBeamGeometryChanged;
            }
        }

        private void OnDestroy()
        {
            Unsubscribe();
        }
    }
}
