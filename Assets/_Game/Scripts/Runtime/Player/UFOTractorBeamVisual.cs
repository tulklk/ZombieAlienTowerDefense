using AlienDefense.Enemies;
using AlienDefense.Environment;
using AlienDefense.Pickups;
using UnityEngine;

namespace AlienDefense.Player
{
    /// <summary>Pure visual reaction to UFOTractorBeamController's counts/events: cone (Outer/Inner), ground
    /// glow/ring, top glow, particles, capture flash. Never decides capture/absorption, never scans any
    /// registry, never touches gameplay state, never Instantiates/Destroys per target. Reacts identically to
    /// Enemy/Energy/Prop — see HandleTotalActiveAbsorptionCountChanged.</summary>
    public sealed class UFOTractorBeamVisual : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        [Header("Cone (top small -> bottom wide, scaled via Transform only)")]
        [SerializeField]
        private MeshRenderer _beamConeOuter;

        [SerializeField]
        private MeshRenderer _beamConeInner;

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
        private ParticleSystem _beamParticles;

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

        [Header("Particle Emission Tuning")]
        [SerializeField, Min(0f)]
        private float _idleEmissionRate = 3f;

        [SerializeField, Min(0f)]
        private float _maxEmissionRate = 45f;

        [SerializeField, Min(0)]
        private int _captureFlashBurstCount = 10;

        private UFOTractorBeamController _controller;
        private MaterialPropertyBlock _outerBlock;
        private MaterialPropertyBlock _innerBlock;
        private Color _outerBaseColor;
        private Color _innerBaseColor;
        private Vector3 _groundRingBaseScale = Vector3.one;

        private float _targetIntensity;
        private float _displayedIntensity;
        private bool _hasEmissionModule;
        private ParticleSystem.EmissionModule _emissionModule;
        private bool _isBeamEnabled;

        public void Initialize(UFOTractorBeamController controller)
        {
            Unsubscribe();
            _controller = controller;

            _outerBlock = new MaterialPropertyBlock();
            _innerBlock = new MaterialPropertyBlock();

            if (_beamConeOuter != null && _beamConeOuter.sharedMaterial != null)
            {
                _outerBaseColor = _beamConeOuter.sharedMaterial.GetColor(BaseColorId);
            }

            if (_beamConeInner != null && _beamConeInner.sharedMaterial != null)
            {
                _innerBaseColor = _beamConeInner.sharedMaterial.GetColor(BaseColorId);
            }

            if (_groundRing != null)
            {
                _groundRingBaseScale = _groundRing.transform.localScale;
            }

            if (_beamParticles != null)
            {
                _emissionModule = _beamParticles.emission;
                _hasEmissionModule = true;
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
        }

        public void SetBeamRadius(float radius)
        {
            ApplyConeRadius(_beamConeOuter, radius);
            // Inner beam reads clearly as "energy core" only if it stays visibly narrower than Outer.
            ApplyConeRadius(_beamConeInner, radius * 0.6f);

            if (_groundGlow != null)
            {
                _groundGlow.transform.localScale = new Vector3(radius * 2f, 1f, radius * 2f);
            }

            if (_groundRing != null)
            {
                _groundRingBaseScale = new Vector3(radius * 2f, 1f, radius * 2f);
                _groundRing.transform.localScale = _groundRingBaseScale;
            }
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

            if (_beamTopGlow != null)
            {
                _beamTopGlow.SetActive(isActive);
            }

            _targetIntensity = _isBeamEnabled ? (isActive ? 1f : _idleIntensity) : 0f;

            if (_hasEmissionModule)
            {
                int max = _controller != null
                    ? _controller.MaxConcurrentCaptures + _controller.MaxConcurrentEnergyAbsorptions + _controller.MaxConcurrentPropAbsorptions
                    : 0;
                float intensity01 = max > 0 ? Mathf.Clamp01((float)count / max) : (isActive ? 1f : 0f);
                _emissionModule.rateOverTime = Mathf.Lerp(_idleEmissionRate, _maxEmissionRate, intensity01);
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

            if (!isEnabled && _beamTopGlow != null)
            {
                _beamTopGlow.SetActive(false);
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
        }

        private void ApplyIntensity(float intensity)
        {
            if (_beamConeOuter != null)
            {
                _outerBlock.SetColor(BaseColorId, ScaleAlpha(_outerBaseColor, intensity));
                _beamConeOuter.SetPropertyBlock(_outerBlock);
            }

            if (_beamConeInner != null)
            {
                // Inner core stays hidden until an Enemy is actually being pulled in, per spec.
                float innerIntensity = Mathf.Clamp01((intensity - _idleIntensity) / Mathf.Max(0.001f, 1f - _idleIntensity));
                _innerBlock.SetColor(BaseColorId, ScaleAlpha(_innerBaseColor, innerIntensity));
                _beamConeInner.SetPropertyBlock(_innerBlock);
            }
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
