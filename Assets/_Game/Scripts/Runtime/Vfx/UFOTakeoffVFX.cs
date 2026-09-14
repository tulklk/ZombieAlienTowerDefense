using DG.Tweening;
using UnityEngine;

namespace AlienDefense.Vfx
{
    /// <summary>Visual-only choreography for the UFO's level-start takeoff: the engine glowing up on the landing pad,
    /// an energy ring and sparks on the pad, a shockwave rolling out over the ground as the UFO leaves, energy streaks
    /// funnelling up after it, and a faint short beam under the saucer - then everything winding down as the UFO
    /// travels to its gameplay start point.
    ///
    /// UFOFlightIntro owns the flight and calls into this at its phase boundaries (Prepare, PlayStartup, PlayLiftOff,
    /// PlayTravel, StopTakeoff). Everything inside a phase is timed here with DOTween, so the intro never has to know
    /// what the effect is made of. Never touches movement, input, the tractor beam or any gameplay state.
    ///
    /// No Update: all timing is tweens, and fades go through a MaterialPropertyBlock so the shared materials are
    /// never instanced. Every particle system here is expected to have Play On Awake off - nothing shows until the
    /// intro asks for it.</summary>
    public sealed class UFOTakeoffVFX : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        [Header("Ground - on the landing pad")]
        [SerializeField]
        private ParticleSystem _groundRing;

        [SerializeField]
        private ParticleSystem _shockwave;

        [SerializeField]
        private ParticleSystem _groundSparks;

        [SerializeField]
        [Tooltip("Streaks rising from the pad into the UFO. Spawned on the ground on purpose (not under the UFO) so " +
            "they keep coming up from the pad while the UFO climbs away.")]
        private ParticleSystem _upwardEnergy;

        [SerializeField]
        private UFOTakeoffParticleAttractor _upwardEnergyAttractor;

        [SerializeField]
        private Renderer _groundGlow;

        [Header("UFO - under the saucer")]
        [SerializeField]
        private ParticleSystem _thrusterGlow;

        [SerializeField]
        [Tooltip("Optional. A short, faint cone of light under the saucer during lift-off only - the same green as " +
            "the gameplay tractor beam, but much shorter and dimmer, never reaching the ground, and gone before the " +
            "tractor beam switches on.")]
        private Renderer _takeoffBeam;

        [Header("Toggles")]
        [SerializeField]
        private bool _shockwaveEnabled = true;

        [SerializeField]
        private bool _takeoffBeamEnabled = true;

        [Header("Intensity")]
        [SerializeField, Range(0f, 2f)]
        [Tooltip("Scales every emission rate, burst count and glow/beam brightness together. 1 = as authored, " +
            "0 = the whole effect off.")]
        private float _intensity = 1f;

        [Header("Startup - seconds after PlayStartup")]
        [SerializeField, Min(0f)]
        private float _thrusterDelay = 0.10f;

        [SerializeField, Min(0f)]
        private float _groundGlowDelay = 0.15f;

        [SerializeField, Min(0f)]
        private float _groundRingDelay = 0.25f;

        [SerializeField, Min(0f)]
        private float _groundSparksDelay = 0.35f;

        [Header("Lift-off - seconds after PlayLiftOff")]
        [SerializeField, Min(0f)]
        private float _shockwaveDelay = 0.15f;

        [SerializeField, Min(0f)]
        private float _upwardEnergyDelay = 0.20f;

        [SerializeField, Min(0f)]
        private float _takeoffBeamDelay = 0.30f;

        [SerializeField, Min(0f)]
        private float _groundGlowFadeDelay = 0.80f;

        [SerializeField, Min(0f)]
        private float _takeoffBeamFadeDelay = 1.10f;

        [Header("Ground glow")]
        [SerializeField, Range(0f, 1f)]
        private float _groundGlowPeakAlpha = 0.55f;

        [SerializeField, Min(0.01f)]
        private float _groundGlowFadeInDuration = 0.35f;

        [SerializeField, Min(0.01f)]
        private float _groundGlowFadeOutDuration = 0.6f;

        [SerializeField, Min(0.01f)]
        private float _groundGlowStartScale = 0.8f;

        [SerializeField, Min(0.01f)]
        private float _groundGlowPeakScale = 1.15f;

        [Header("Takeoff beam")]
        [SerializeField, Range(0f, 1f)]
        private float _takeoffBeamPeakAlpha = 0.30f;

        [SerializeField, Min(0.01f)]
        private float _takeoffBeamFadeInDuration = 0.25f;

        [SerializeField, Min(0.01f)]
        private float _takeoffBeamFadeOutDuration = 0.4f;

        [Header("Travel / stop")]
        [SerializeField, Range(0f, 1f)]
        [Tooltip("Thruster glow emission, as a fraction of its takeoff rate, while the UFO travels to its start point.")]
        private float _travelThrusterRate = 0.45f;

        [SerializeField, Min(0.01f)]
        private float _stopFadeDuration = 0.35f;

        private Sequence _startupSequence;
        private Sequence _liftOffSequence;
        private Tween _groundGlowTween;
        private Tween _takeoffBeamTween;
        private Tween _attractorOffTween;
        private MaterialPropertyBlock _block;

        private bool _hasCachedAuthoredValues;
        private float _thrusterBaseRate;
        private float _upwardEnergyBaseRate;
        private ParticleSystem.Burst[] _groundRingBaseBursts;
        private ParticleSystem.Burst[] _shockwaveBaseBursts;
        private ParticleSystem.Burst[] _groundSparksBaseBursts;
        private Color _groundGlowBaseColor = Color.white;
        private Color _takeoffBeamBaseColor = Color.white;
        private Vector3 _groundGlowBaseScale = Vector3.one;
        private float _groundGlowAlpha;
        private float _takeoffBeamAlpha;

        public bool ShockwaveEnabled
        {
            get => _shockwaveEnabled;
            set => _shockwaveEnabled = value;
        }

        public bool TakeoffBeamEnabled
        {
            get => _takeoffBeamEnabled;
            set => _takeoffBeamEnabled = value;
        }

        public float Intensity
        {
            get => _intensity;
            set => _intensity = Mathf.Clamp(value, 0f, 2f);
        }

        private void Awake()
        {
            CacheAuthoredValues();
            HideStaticVisuals();
        }

        /// <summary>Hard reset to "nothing visible, ready to play": stops and clears every particle, restores
        /// authored rates/bursts at the current intensity, zeroes the glow and beam. Call before PlayStartup - it is
        /// also what makes a scene reload start clean.</summary>
        public void Prepare()
        {
            CacheAuthoredValues();
            KillTweens();

            Clear(_groundRing);
            Clear(_shockwave);
            Clear(_groundSparks);
            Clear(_upwardEnergy);
            Clear(_thrusterGlow);

            SetRate(_thrusterGlow, _thrusterBaseRate * _intensity);
            SetRate(_upwardEnergy, _upwardEnergyBaseRate * _intensity);
            ApplyBursts(_groundRing, _groundRingBaseBursts);
            ApplyBursts(_shockwave, _shockwaveBaseBursts);
            ApplyBursts(_groundSparks, _groundSparksBaseBursts);

            if (_upwardEnergyAttractor != null)
            {
                _upwardEnergyAttractor.enabled = false;
            }

            HideStaticVisuals();
        }

        /// <summary>Same as Prepare - kept as the explicit "throw away whatever is showing" entry point.</summary>
        public void ResetVfx()
        {
            Prepare();
        }

        /// <summary>The UFO is still sitting on the pad: engine glow, pad glow, energy ring, sparks.</summary>
        public void PlayStartup()
        {
            if (_intensity <= 0f)
            {
                return;
            }

            _startupSequence?.Kill();
            _startupSequence = DOTween.Sequence().SetLink(gameObject);
            _startupSequence.InsertCallback(_thrusterDelay, () => Play(_thrusterGlow));
            _startupSequence.InsertCallback(_groundGlowDelay, FadeInGroundGlow);
            _startupSequence.InsertCallback(_groundRingDelay, () => Play(_groundRing));
            _startupSequence.InsertCallback(_groundSparksDelay, () => Play(_groundSparks));
        }

        /// <summary>The UFO leaves the pad: shockwave, rising energy, the short beam; the pad glow then fades.</summary>
        public void PlayLiftOff()
        {
            if (_intensity <= 0f)
            {
                return;
            }

            _liftOffSequence?.Kill();
            _liftOffSequence = DOTween.Sequence().SetLink(gameObject);

            if (_shockwaveEnabled)
            {
                _liftOffSequence.InsertCallback(_shockwaveDelay, PlayShockwave);
            }

            _liftOffSequence.InsertCallback(_upwardEnergyDelay, StartUpwardEnergy);

            if (_takeoffBeamEnabled)
            {
                _liftOffSequence.InsertCallback(_takeoffBeamDelay, FadeInTakeoffBeam);
                _liftOffSequence.InsertCallback(_takeoffBeamFadeDelay, () => FadeOutTakeoffBeam(_takeoffBeamFadeOutDuration));
            }

            _liftOffSequence.InsertCallback(_groundGlowFadeDelay, () => FadeOutGroundGlow(_groundGlowFadeOutDuration));
        }

        public void PlayShockwave()
        {
            if (_shockwaveEnabled)
            {
                Play(_shockwave);
            }
        }

        /// <summary>The UFO is flying over to its start point: the pad effects are done, the engine idles lower.
        /// Streaks already in the air are left to finish rather than being cleared.</summary>
        public void PlayTravel()
        {
            StopEmitting(_upwardEnergy);
            ScheduleAttractorOff();
            SetRate(_thrusterGlow, _thrusterBaseRate * _intensity * _travelThrusterRate);

            // Only matters if the lift was shorter than these fades were scheduled for.
            FadeOutGroundGlow(_groundGlowFadeOutDuration);
            FadeOutTakeoffBeam(_takeoffBeamFadeOutDuration);
        }

        /// <summary>The intro is over: stop emitting everywhere and fade the rest out, handing the screen back to the
        /// normal gameplay effects.</summary>
        public void StopTakeoff()
        {
            _startupSequence?.Kill();
            _liftOffSequence?.Kill();

            StopEmitting(_groundRing);
            StopEmitting(_shockwave);
            StopEmitting(_groundSparks);
            StopEmitting(_upwardEnergy);
            StopEmitting(_thrusterGlow);

            FadeOutGroundGlow(_stopFadeDuration);
            FadeOutTakeoffBeam(_stopFadeDuration);
            ScheduleAttractorOff();
        }

        private void StartUpwardEnergy()
        {
            _attractorOffTween?.Kill();
            if (_upwardEnergyAttractor != null)
            {
                _upwardEnergyAttractor.enabled = true;
            }

            Play(_upwardEnergy);
        }

        /// <summary>Turns the funnel steering off once the longest-lived streak has had time to finish.</summary>
        private void ScheduleAttractorOff()
        {
            if (_upwardEnergyAttractor == null || !_upwardEnergyAttractor.enabled)
            {
                return;
            }

            float longestLife = _upwardEnergy != null ? _upwardEnergy.main.startLifetime.constantMax : 0f;
            _attractorOffTween?.Kill();
            // ignoreTimeScale: false - the streaks age in game time, so the wait must too (DelayedCall's default is
            // real time, which switched the steering off while streaks were still flying whenever time ran slow).
            _attractorOffTween = DOVirtual.DelayedCall(longestLife + 0.1f, () =>
            {
                if (_upwardEnergyAttractor != null)
                {
                    _upwardEnergyAttractor.enabled = false;
                }
            }, false).SetLink(gameObject);
        }

        private void FadeInGroundGlow()
        {
            if (_groundGlow == null)
            {
                return;
            }

            _groundGlowTween?.Kill();
            _groundGlow.enabled = true;

            Transform glow = _groundGlow.transform;
            glow.localScale = _groundGlowBaseScale * _groundGlowStartScale;
            float peak = Mathf.Clamp01(_groundGlowPeakAlpha * _intensity);

            _groundGlowTween = DOTween.Sequence().SetLink(gameObject)
                .Join(DOTween.To(() => _groundGlowAlpha, SetGroundGlowAlpha, peak, _groundGlowFadeInDuration).SetEase(Ease.OutQuad))
                .Join(glow.DOScale(_groundGlowBaseScale * _groundGlowPeakScale, _groundGlowFadeInDuration).SetEase(Ease.OutSine));
        }

        private void FadeOutGroundGlow(float duration)
        {
            if (_groundGlow == null || !_groundGlow.enabled)
            {
                return;
            }

            _groundGlowTween?.Kill();
            _groundGlowTween = DOTween.To(() => _groundGlowAlpha, SetGroundGlowAlpha, 0f, duration)
                .SetEase(Ease.InQuad)
                .SetLink(gameObject)
                .OnComplete(() =>
                {
                    if (_groundGlow != null)
                    {
                        _groundGlow.enabled = false;
                    }
                });
        }

        private void FadeInTakeoffBeam()
        {
            if (_takeoffBeam == null || !_takeoffBeamEnabled)
            {
                return;
            }

            _takeoffBeamTween?.Kill();
            _takeoffBeam.enabled = true;
            float peak = Mathf.Clamp01(_takeoffBeamPeakAlpha * _intensity);
            _takeoffBeamTween = DOTween.To(() => _takeoffBeamAlpha, SetTakeoffBeamAlpha, peak, _takeoffBeamFadeInDuration)
                .SetEase(Ease.OutQuad)
                .SetLink(gameObject);
        }

        private void FadeOutTakeoffBeam(float duration)
        {
            if (_takeoffBeam == null || !_takeoffBeam.enabled)
            {
                return;
            }

            _takeoffBeamTween?.Kill();
            _takeoffBeamTween = DOTween.To(() => _takeoffBeamAlpha, SetTakeoffBeamAlpha, 0f, duration)
                .SetEase(Ease.InQuad)
                .SetLink(gameObject)
                .OnComplete(() =>
                {
                    if (_takeoffBeam != null)
                    {
                        _takeoffBeam.enabled = false;
                    }
                });
        }

        private void SetGroundGlowAlpha(float alpha)
        {
            _groundGlowAlpha = alpha;
            SetAlpha(_groundGlow, _groundGlowBaseColor, alpha);
        }

        private void SetTakeoffBeamAlpha(float alpha)
        {
            _takeoffBeamAlpha = alpha;
            SetAlpha(_takeoffBeam, _takeoffBeamBaseColor, alpha);
        }

        private void SetAlpha(Renderer target, Color baseColor, float alpha)
        {
            if (target == null)
            {
                return;
            }

            _block ??= new MaterialPropertyBlock();
            baseColor.a = alpha;
            _block.SetColor(BaseColorId, baseColor);
            target.SetPropertyBlock(_block);
        }

        private void HideStaticVisuals()
        {
            if (_groundGlow != null)
            {
                _groundGlow.transform.localScale = _groundGlowBaseScale * _groundGlowStartScale;
                SetGroundGlowAlpha(0f);
                _groundGlow.enabled = false;
            }

            if (_takeoffBeam != null)
            {
                SetTakeoffBeamAlpha(0f);
                _takeoffBeam.enabled = false;
            }
        }

        /// <summary>Remembers the rates, bursts, colours and scale as authored, once, so intensity is always applied
        /// to the original values rather than compounding on whatever a previous run left behind.</summary>
        private void CacheAuthoredValues()
        {
            if (_hasCachedAuthoredValues)
            {
                return;
            }

            _hasCachedAuthoredValues = true;
            _thrusterBaseRate = GetRate(_thrusterGlow);
            _upwardEnergyBaseRate = GetRate(_upwardEnergy);
            _groundRingBaseBursts = GetBursts(_groundRing);
            _shockwaveBaseBursts = GetBursts(_shockwave);
            _groundSparksBaseBursts = GetBursts(_groundSparks);
            _groundGlowBaseColor = GetBaseColor(_groundGlow);
            _takeoffBeamBaseColor = GetBaseColor(_takeoffBeam);

            if (_groundGlow != null)
            {
                _groundGlowBaseScale = _groundGlow.transform.localScale;
            }
        }

        private static float GetRate(ParticleSystem particles)
        {
            return particles != null ? particles.emission.rateOverTimeMultiplier : 0f;
        }

        private static void SetRate(ParticleSystem particles, float rate)
        {
            if (particles == null)
            {
                return;
            }

            ParticleSystem.EmissionModule emission = particles.emission;
            emission.rateOverTimeMultiplier = rate;
        }

        private static ParticleSystem.Burst[] GetBursts(ParticleSystem particles)
        {
            if (particles == null)
            {
                return null;
            }

            var bursts = new ParticleSystem.Burst[particles.emission.burstCount];
            particles.emission.GetBursts(bursts);
            return bursts;
        }

        private void ApplyBursts(ParticleSystem particles, ParticleSystem.Burst[] authored)
        {
            if (particles == null || authored == null)
            {
                return;
            }

            var scaled = new ParticleSystem.Burst[authored.Length];
            for (int i = 0; i < authored.Length; i++)
            {
                scaled[i] = authored[i];
                float count = authored[i].count.constantMax * _intensity;
                scaled[i].count = new ParticleSystem.MinMaxCurve(_intensity > 0f ? Mathf.Max(1f, Mathf.Round(count)) : 0f);
            }

            particles.emission.SetBursts(scaled);
        }

        private static Color GetBaseColor(Renderer target)
        {
            return target != null && target.sharedMaterial != null && target.sharedMaterial.HasProperty(BaseColorId)
                ? target.sharedMaterial.GetColor(BaseColorId)
                : Color.white;
        }

        private void Play(ParticleSystem particles)
        {
            if (particles != null && _intensity > 0f)
            {
                particles.Play(true);
            }
        }

        private static void Clear(ParticleSystem particles)
        {
            if (particles != null)
            {
                particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
        }

        private static void StopEmitting(ParticleSystem particles)
        {
            if (particles != null)
            {
                particles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }
        }

        private void KillTweens()
        {
            _startupSequence?.Kill();
            _liftOffSequence?.Kill();
            _groundGlowTween?.Kill();
            _takeoffBeamTween?.Kill();
            _attractorOffTween?.Kill();
            _startupSequence = null;
            _liftOffSequence = null;
            _groundGlowTween = null;
            _takeoffBeamTween = null;
            _attractorOffTween = null;
        }

        private void OnDestroy()
        {
            KillTweens();
        }
    }
}
