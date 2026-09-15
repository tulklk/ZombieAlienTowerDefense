using DG.Tweening;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace AlienDefense.Vfx
{
    /// <summary>The first ~1.5 s of a level: the scene "wakes up" instead of popping in. Exposure opens up from slightly
    /// dim, Bloom grows to its normal strength and the UFO's cyan lights come on - core first, then the engine ring.
    /// Presentation only; nothing here touches gameplay.
    ///
    /// The level's own Volume profile is never modified. The dim/low-bloom start lives on a temporary override Volume
    /// (higher priority) whose weight tweens from 1 to 0, so URP blends back to the authored look by itself; the
    /// override is destroyed when the intro is done. The UFO glow uses a MaterialPropertyBlock that is cleared at the
    /// end, so the renderers go back to plain SRP-batched materials. One DOTween sequence, no per-frame Update.</summary>
    public sealed class LevelLightingIntro : MonoBehaviour
    {
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        [Header("Scene")]
        [SerializeField]
        [Tooltip("The level's global Volume; its priority is used to place the temporary override above it.")]
        private Volume _levelVolume;

        [SerializeField]
        [Tooltip("UFO renderers whose material emission (the cyan lights) fades in.")]
        private Renderer[] _ufoEmissiveRenderers = new Renderer[0];

        [Header("Start state (blended back to the level Volume)")]
        [SerializeField]
        [Tooltip("Post Exposure added on top of the level's at time 0 (e.g. -0.17 on -0.08 = -0.25).")]
        private float _startExposureOffset = -0.17f;

        [SerializeField, Min(0f)]
        private float _startBloomIntensity = 0.15f;

        [SerializeField, Range(0f, 1f)]
        [Tooltip("UFO emission at time 0, as a fraction of the material's.")]
        private float _startEmission = 0.2f;

        [SerializeField, Range(0f, 1f)]
        [Tooltip("UFO emission once the core has woken up (before the engine ring joins in).")]
        private float _coreEmission = 0.55f;

        [Header("Timing (seconds from level start)")]
        [SerializeField, Min(0f)]
        private float _coreWakeTime = 0.2f;

        [SerializeField, Min(0f)]
        private float _engineTime = 0.4f;

        [SerializeField, Min(0.05f)]
        private float _settleTime = 1.2f;

        [SerializeField, Min(0f)]
        [Tooltip("Extra hold after settling before the temporary state is cleaned up.")]
        private float _cleanupDelay = 0.3f;

        [SerializeField]
        private bool _playOnStart = true;

        private Volume _overrideVolume;
        private VolumeProfile _overrideProfile;
        private MaterialPropertyBlock _block;
        private Color[] _baseEmission = new Color[0];
        private Sequence _sequence;
        private float _emission = 1f;

        private void Start()
        {
            if (_playOnStart)
            {
                Play();
            }
        }

        public void Play()
        {
            Cleanup();

            float baseExposure = 0f;
            float baseBloom = 0f;
            VolumeProfile levelProfile = _levelVolume != null ? _levelVolume.sharedProfile : null;
            if (levelProfile != null)
            {
                if (levelProfile.TryGet(out ColorAdjustments levelColor) && levelColor.postExposure.overrideState)
                {
                    baseExposure = levelColor.postExposure.value;
                }

                if (levelProfile.TryGet(out Bloom levelBloom) && levelBloom.intensity.overrideState)
                {
                    baseBloom = levelBloom.intensity.value;
                }
            }

            CreateOverrideVolume(baseExposure + _startExposureOffset, Mathf.Min(_startBloomIntensity, baseBloom));
            CaptureEmission();
            SetEmission(_startEmission);

            float settle = Mathf.Max(_engineTime + 0.05f, _settleTime);
            _sequence = DOTween.Sequence().SetLink(gameObject);
            _sequence.Insert(_coreWakeTime, DOTween.To(() => _emission, SetEmission, _coreEmission, Mathf.Max(0.01f, _engineTime - _coreWakeTime)).SetEase(Ease.OutQuad));
            _sequence.Insert(_engineTime, DOTween.To(() => _emission, SetEmission, 1f, settle - _engineTime).SetEase(Ease.OutSine));
            _sequence.Insert(_engineTime, DOTween.To(() => _overrideVolume.weight, w => _overrideVolume.weight = w, 0f, settle - _engineTime).SetEase(Ease.InOutSine));
            _sequence.InsertCallback(settle + _cleanupDelay, Cleanup);
        }

        private void CreateOverrideVolume(float exposure, float bloomIntensity)
        {
            _overrideProfile = ScriptableObject.CreateInstance<VolumeProfile>();
            _overrideProfile.name = "LevelLightingIntro (runtime)";
            ColorAdjustments color = _overrideProfile.Add<ColorAdjustments>();
            color.postExposure.Override(exposure);
            Bloom bloom = _overrideProfile.Add<Bloom>();
            bloom.intensity.Override(bloomIntensity);

            var host = new GameObject("LevelLightingIntro_Override");
            host.transform.SetParent(transform, false);
            host.layer = _levelVolume != null ? _levelVolume.gameObject.layer : gameObject.layer;
            _overrideVolume = host.AddComponent<Volume>();
            _overrideVolume.isGlobal = true;
            _overrideVolume.priority = (_levelVolume != null ? _levelVolume.priority : 0f) + 10f;
            _overrideVolume.weight = 1f;
            _overrideVolume.sharedProfile = _overrideProfile;
        }

        private void CaptureEmission()
        {
            _block ??= new MaterialPropertyBlock();
            _baseEmission = new Color[_ufoEmissiveRenderers.Length];
            for (int i = 0; i < _ufoEmissiveRenderers.Length; i++)
            {
                Renderer target = _ufoEmissiveRenderers[i];
                Material material = target != null ? target.sharedMaterial : null;
                _baseEmission[i] = material != null && material.HasProperty(EmissionColorId) ? material.GetColor(EmissionColorId) : Color.black;
            }
        }

        private void SetEmission(float factor)
        {
            _emission = factor;
            for (int i = 0; i < _ufoEmissiveRenderers.Length; i++)
            {
                Renderer target = _ufoEmissiveRenderers[i];
                if (target == null)
                {
                    continue;
                }

                target.GetPropertyBlock(_block);
                _block.SetColor(EmissionColorId, _baseEmission[i] * factor);
                target.SetPropertyBlock(_block);
            }
        }

        private void Cleanup()
        {
            if (_sequence != null && _sequence.IsActive())
            {
                _sequence.Kill();
            }

            _sequence = null;

            for (int i = 0; i < _ufoEmissiveRenderers.Length; i++)
            {
                if (_ufoEmissiveRenderers[i] != null)
                {
                    _ufoEmissiveRenderers[i].SetPropertyBlock(null); // back to the plain, batchable material
                }
            }

            if (_overrideVolume != null)
            {
                Destroy(_overrideVolume.gameObject);
            }

            _overrideVolume = null;

            if (_overrideProfile != null)
            {
                foreach (VolumeComponent component in _overrideProfile.components)
                {
                    Destroy(component);
                }

                Destroy(_overrideProfile);
            }

            _overrideProfile = null;
        }

        private void OnDestroy()
        {
            Cleanup();
        }
    }
}
