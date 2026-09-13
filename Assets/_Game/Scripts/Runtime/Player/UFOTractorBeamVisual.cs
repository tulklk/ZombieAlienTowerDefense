using AlienDefense.Enemies;
using AlienDefense.Environment;
using AlienDefense.Pickups;
using AlienDefense.UI;
using DG.Tweening;
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
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        [Header("Cone (top small -> bottom wide, scaled via Transform only)")]
        [SerializeField]
        private MeshRenderer _beamConeOuter;

        [SerializeField]
        private MeshRenderer _beamConeInner;

        [SerializeField, Range(0.1f, 1f)]
        [Tooltip("The cone's base radius as a fraction of the gameplay AttractionRadius. The ground ring always " +
            "marks the full capture area (see SetBeamRadius), so keep this just under 1: the cone then lands " +
            "inside the ring's rim. Only the cone's width - never what the beam can actually reach - depends " +
            "on this value.")]
        private float _visualRadiusScale = 0.28f;

        [Header("Ground")]
        [SerializeField]
        private GameObject _groundGlow;

        [SerializeField]
        [Tooltip("The glowing circle the beam paints on the ground. Sized to the beam's footprint (see " +
            "SetBeamRadius), breathes with the pulse, and is kept lying flat on the actual terrain every frame " +
            "rather than at the beam's fixed ground anchor, which floats above or sinks below the surface as the " +
            "ground rises and falls.")]
        private GameObject _groundRing;

        [SerializeField]
        [Tooltip("Optional. A thin ring inside Ground Ring that keeps expanding from the centre out to the rim and " +
            "fading - the ripple of the beam hitting the ground. Its scale is relative to Ground Ring, so it " +
            "follows the beam radius automatically.")]
        private Renderer _groundRipple;

        [SerializeField, Min(0.1f)]
        [Tooltip("Seconds for one ripple to travel from the centre to the rim.")]
        private float _groundRippleDuration = 1.3f;

        [SerializeField, Range(0f, 1f)]
        [Tooltip("Ripple size, as a fraction of the ring, at the moment it appears.")]
        private float _groundRippleStartScale = 0.25f;

        [SerializeField, Min(0f)]
        [Tooltip("How far above the terrain the ring floats - just enough never to z-fight the ground.")]
        private float _groundRingHeightOffset = 0.05f;

        [SerializeField, Min(0f)]
        [Tooltip("How much longer than its gameplay length the beam cone may stretch so it still reaches the " +
            "ground over low terrain. Past this (flying off a cliff edge) the cone simply ends in the air.")]
        private float _maxGroundReachExtension = 4f;

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

        [Header("XP Popup")]
        [SerializeField]
        [Tooltip("Optional. Spawned (Instantiate, never pooled - these are rare, roughly once per Energy Pickup " +
            "or Prop/animal absorbed) once per XP-granting absorption, showing '+N XP'. See " +
            "HandleEnergyPickupCollected/HandlePropAbsorbed - amount comes from the source's own ExperienceValue/" +
            "ExperienceReward, never guessed here.")]
        private XpPopupView _xpPopupPrefab;

        [SerializeField]
        [Tooltip("Optional. Where a spawned XP popup appears and starts rising from. Falls back to this " +
            "Transform (the beam root, under the UFO) if left empty.")]
        private Transform _xpPopupSpawnAnchor;

        private UFOTractorBeamController _controller;
        private Transform _cameraTransform;
        private Camera _camera;
        private MaterialPropertyBlock _outerBlock;
        private Color _outerBaseColor;
        private Vector3 _groundRingBaseScale = Vector3.one;

        private float _targetIntensity;
        private float _displayedIntensity;
        private Tweener _intensityTween;
        private Tweener _groundRingTween;
        private Tweener _groundRippleTween;
        private MaterialPropertyBlock _rippleBlock;
        private Color _rippleBaseColor = Color.white;
        private float _rippleProgress;
        private Terrain[] _terrains;

        // The beam's gameplay geometry (see SetBeamLength) and where the cone's base was authored, in beam-root
        // space. The cone's top never moves - it's the capture socket - so reaching the ground only ever means
        // lowering the base and lengthening the cone to match.
        private float _beamLength;
        private float _beamBaseLocalY;
        private bool _hasBeamBaseLocalY;
        private const int GroundSampleCount = 8;
        private const float MinVisualBeamLength = 0.5f;

        // Where T_BeamGroundRing draws its bright rim, as a fraction of the quad's half-size (the texture fades out
        // between here and the quad edge). Keep in sync with the texture if it is ever regenerated.
        private const float RingTextureRimRadius = 0.9f;
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

        public void Initialize(UFOTractorBeamController controller, Transform cameraTransform = null)
        {
            Unsubscribe();
            _controller = controller;
            _cameraTransform = cameraTransform;
            _camera = cameraTransform != null ? cameraTransform.GetComponent<Camera>() : null;
            if (_camera == null)
            {
                _camera = Camera.main;
            }

            _outerBlock = new MaterialPropertyBlock();

            if (_beamConeOuter != null && _beamConeOuter.sharedMaterial != null)
            {
                _outerBaseColor = _beamConeOuter.sharedMaterial.GetColor(BeamColorId);
            }

            if (_groundRing != null)
            {
                _groundRingBaseScale = _groundRing.transform.localScale;
            }

            // Cached once: Terrain.activeTerrains allocates a fresh array on every call.
            _terrains = Terrain.activeTerrains;

            // Captured once, before LateUpdate ever moves the cone - a later re-Initialize must not mistake an
            // already-lowered base for the authored one.
            if (!_hasBeamBaseLocalY && _beamConeOuter != null)
            {
                _beamBaseLocalY = _beamConeOuter.transform.localPosition.y;
                _hasBeamBaseLocalY = true;
            }

            if (_groundRipple != null && _groundRipple.sharedMaterial != null &&
                _groundRipple.sharedMaterial.HasProperty(BaseColorId))
            {
                _rippleBaseColor = _groundRipple.sharedMaterial.GetColor(BaseColorId);
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

            _intensityTween?.Kill();
            _intensityTween = null;
            _displayedIntensity = _targetIntensity;
            ApplyIntensity(_displayedIntensity);
            RestartGroundRingPulse();
        }

        /// <summary>Eases the cone toward its new intensity instead of stepping toward it every frame. Re-targets
        /// (kill + restart from wherever it currently is) each time the capture count changes, so a burst of
        /// captures reads as one continuous brighten rather than a stack of competing fades. The duration is
        /// scaled by how far there is left to travel, so a small change stays quick.</summary>
        private void RetargetIntensity()
        {
            _intensityTween?.Kill();

            float distance = Mathf.Abs(_targetIntensity - _displayedIntensity);
            if (distance <= 0.001f)
            {
                _displayedIntensity = _targetIntensity;
                ApplyIntensity(_displayedIntensity);
                return;
            }

            float fullSweep = _displayedIntensity < _targetIntensity ? _fadeInDuration : _fadeOutDuration;
            _intensityTween = DOTween
                .To(() => _displayedIntensity, v => { _displayedIntensity = v; ApplyIntensity(v); }, _targetIntensity, fullSweep * distance)
                .SetEase(Ease.OutQuad);
        }

        /// <summary>Restarts the ground ring's slow breathing at whatever base scale SetBeamRadius last computed
        /// (the radius changes with the Radius/Magnet skills, so the loop has to be rebuilt when it does).</summary>
        private void RestartGroundRingPulse()
        {
            _groundRingTween?.Kill();
            _groundRingTween = null;

            if (_groundRing == null)
            {
                return;
            }

            if (!_isBeamEnabled || _pulseScaleAmplitude <= 0f || _pulseFrequency <= 0f)
            {
                _groundRing.transform.localScale = _groundRingBaseScale;
                return;
            }

            // One yoyo loop is half a cycle, hence the /2 on the period.
            float halfCycle = 1f / (_pulseFrequency * 2f);
            _groundRing.transform.localScale = _groundRingBaseScale * (1f - _pulseScaleAmplitude);
            _groundRingTween = _groundRing.transform
                .DOScale(_groundRingBaseScale * (1f + _pulseScaleAmplitude), halfCycle)
                .SetEase(Ease.InOutSine)
                .SetLoops(-1, LoopType.Yoyo);
        }

        /// <summary>Loops a ripple from the middle of the ground ring out to its rim, fading in quickly and out
        /// slowly so it never pops on or cuts off at the edge. One value tween drives both scale and alpha; the
        /// alpha goes through a MaterialPropertyBlock so the shared material asset is never modified.</summary>
        private void RestartGroundRipple()
        {
            _groundRippleTween?.Kill();
            _groundRippleTween = null;

            if (_groundRipple == null)
            {
                return;
            }

            _rippleBlock ??= new MaterialPropertyBlock();

            if (!_isBeamEnabled)
            {
                ApplyGroundRipple(1f); // fully faded
                return;
            }

            _rippleProgress = 0f;
            _groundRippleTween = DOTween.To(() => _rippleProgress, ApplyGroundRipple, 1f, _groundRippleDuration)
                .SetEase(Ease.OutSine) // bursts out of the centre, then eases as it reaches the rim
                .SetLoops(-1, LoopType.Restart);
        }

        private void ApplyGroundRipple(float progress)
        {
            _rippleProgress = progress;
            if (_groundRipple == null)
            {
                return;
            }

            // The ripple quad lies flat through a 90-degree X rotation, so its ground-plane axes are local X/Y.
            float scale = Mathf.Lerp(_groundRippleStartScale, 1f, progress);
            _groundRipple.transform.localScale = new Vector3(scale, scale, 1f);

            const float fadeInPortion = 0.2f;
            float alpha = progress < fadeInPortion
                ? progress / fadeInPortion
                : 1f - (progress - fadeInPortion) / (1f - fadeInPortion);

            Color color = _rippleBaseColor;
            color.a *= Mathf.Clamp01(alpha);
            _rippleBlock.SetColor(BaseColorId, color);
            _groundRipple.SetPropertyBlock(_rippleBlock);
        }

        /// <summary>Grounds the beam every frame: the ring lies on the terrain and the cone reaches down to meet
        /// it. BeamGroundAnchor sits a fixed distance under the UFO (1.5m above the ground on flat terrain), so
        /// left to the anchor the cone stopped in mid-air with the ring floating detached below it.
        ///
        /// Terrain only, on purpose: a physics raycast would also hit the trees and bushes the beam sweeps over
        /// and make the ring and cone jump onto their tops.</summary>
        private void LateUpdate()
        {
            if (!TrySampleGroundUnderBeam(out float groundY))
            {
                return;
            }

            PlaceGroundRing(groundY);
            StretchBeamToGround(groundY);
        }

        /// <summary>Highest terrain point under the ring - its centre plus points around its edge. Sampling the
        /// centre alone let the uphill side of the ring sink into sloped ground and vanish.</summary>
        private bool TrySampleGroundUnderBeam(out float groundY)
        {
            groundY = float.MinValue;
            if (_terrains == null)
            {
                return false;
            }

            Vector3 centre = transform.position;
            float radius = _groundRing != null ? _groundRingBaseScale.x * 0.5f : 0f;
            bool found = false;

            for (int i = -1; i < GroundSampleCount; i++)
            {
                Vector3 point = centre;
                if (i >= 0)
                {
                    float angle = i * Mathf.PI * 2f / GroundSampleCount;
                    point.x += Mathf.Cos(angle) * radius;
                    point.z += Mathf.Sin(angle) * radius;
                }

                if (TrySampleTerrainHeight(point, out float height))
                {
                    groundY = Mathf.Max(groundY, height);
                    found = true;
                }
            }

            return found;
        }

        private bool TrySampleTerrainHeight(Vector3 point, out float height)
        {
            for (int i = 0; i < _terrains.Length; i++)
            {
                Terrain terrain = _terrains[i];
                if (terrain == null || terrain.terrainData == null)
                {
                    continue;
                }

                Vector3 origin = terrain.transform.position;
                Vector3 size = terrain.terrainData.size;
                if (point.x < origin.x || point.x > origin.x + size.x ||
                    point.z < origin.z || point.z > origin.z + size.z)
                {
                    continue;
                }

                height = origin.y + terrain.SampleHeight(point);
                return true;
            }

            height = 0f;
            return false;
        }

        private void PlaceGroundRing(float groundY)
        {
            if (_groundRing == null || !_groundRing.activeInHierarchy)
            {
                return;
            }

            Vector3 centre = transform.position;
            _groundRing.transform.SetPositionAndRotation(
                new Vector3(centre.x, groundY + _groundRingHeightOffset, centre.z),
                Quaternion.identity); // flat on the ground even if the UFO banks while flying
        }

        /// <summary>Keeps the cone's top pinned at the capture socket and moves its base down (or up) to the
        /// ground, lengthening the cone to match; the particle emitters and their height clamps follow, so the
        /// sparkles rise out of the ring rather than out of thin air. Visual only - gameplay keeps using
        /// BeamGroundAnchor and the controller's BeamLength untouched.</summary>
        private void StretchBeamToGround(float groundY)
        {
            if (!_hasBeamBaseLocalY || _beamLength <= 0f)
            {
                return;
            }

            float topWorldY = transform.TransformPoint(0f, _beamBaseLocalY + _beamLength, 0f).y;
            float visualLength = Mathf.Clamp(topWorldY - groundY, MinVisualBeamLength, _beamLength + _maxGroundReachExtension);
            float baseWorldY = topWorldY - visualLength;

            PlaceConeBase(_beamConeOuter, baseWorldY, visualLength);
            PlaceConeBase(_beamConeInner, baseWorldY, visualLength);

            SetWorldHeight(_beamParticles != null ? _beamParticles.transform : null, baseWorldY);
            SetWorldHeight(_beamStreakParticles != null ? _beamStreakParticles.transform : null, baseWorldY);
            SetWorldHeight(_beamOrbParticles != null ? _beamOrbParticles.transform : null, baseWorldY);

            _beamSparkAttractor?.SetMaxHeight(visualLength);
            _beamStreakAttractor?.SetMaxHeight(visualLength);
            _beamOrbAttractor?.SetMaxHeight(visualLength);
        }

        private static void PlaceConeBase(MeshRenderer cone, float baseWorldY, float length)
        {
            if (cone == null)
            {
                return;
            }

            SetWorldHeight(cone.transform, baseWorldY);
            ApplyConeScale(cone, length, keepRadius: true);
        }

        private static void SetWorldHeight(Transform target, float worldY)
        {
            if (target == null)
            {
                return;
            }

            Vector3 position = target.position;
            position.y = worldY;
            target.position = position;
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
            _beamLength = length;
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
                // The ring IS the capture area: sized from the gameplay radius itself (not the narrower cone), so
                // anything the beam can grab is inside the rim the player sees. The ring texture draws its bright
                // rim at RingTextureRimRadius of the quad, not at the quad's edge, so the quad is scaled up to put
                // that visible rim - rather than the invisible quad border - exactly on AttractionRadius.
                float ringDiameter = radius * 2f / RingTextureRimRadius;
                _groundRingBaseScale = new Vector3(ringDiameter, 1f, ringDiameter);
                _groundRing.transform.localScale = _groundRingBaseScale;
                RestartGroundRingPulse(); // the loop tweens around this base scale, so it has to be rebuilt
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
            RetargetIntensity();

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
            SpawnXpPopup(pickup.ExperienceValue);
        }

        private void HandlePropAbsorbed(TractorAbsorbableProp prop)
        {
            _captureFlashParticles?.Emit(_captureFlashBurstCount);
            SpawnXpPopup(prop.ExperienceReward);
        }

        /// <summary>amount is whatever the source itself already decided to grant (EnergyPickupController.
        /// ExperienceValue or TractorAbsorbableProp.ExperienceReward, both capped at 2 - see
        /// EnemyDefinition.ExperienceReward's tooltip) - never recomputed or guessed here.</summary>
        private void SpawnXpPopup(int amount)
        {
            if (_xpPopupPrefab == null || amount <= 0)
            {
                return;
            }

            // Screen Space - Overlay (see XpPopupView's class doc for why), so there's no more "behind the beam
            // cone" risk to dodge with a world-space offset - a small upward nudge off the UFO's own position is
            // enough; XpPopupView reprojects it (and its own further rise) to screen space every frame itself.
            Vector3 basePosition = _xpPopupSpawnAnchor != null ? _xpPopupSpawnAnchor.position : transform.position;
            Vector3 spawnPosition = basePosition + Vector3.up * 0.6f;

            XpPopupView popup = Instantiate(_xpPopupPrefab);
            popup.Show(amount, spawnPosition, _camera);
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
            RetargetIntensity();
            RestartGroundRingPulse(); // stops breathing when the beam is switched off, resumes when it's back
            RestartGroundRipple();

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

            _intensityTween?.Kill();
            _groundRingTween?.Kill();
            _groundRippleTween?.Kill();
            _intensityTween = null;
            _groundRingTween = null;
            _groundRippleTween = null;
        }
    }
}
