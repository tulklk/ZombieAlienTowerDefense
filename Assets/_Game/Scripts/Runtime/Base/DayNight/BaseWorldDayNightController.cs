using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace AlienDefense.Base.DayNight
{
    /// <summary>Real-time day / night for the Base tab's 3D world, from the device's local clock (DateTime.Now -
    /// no network, no save data, unaffected by Time.timeScale).
    ///
    /// Lives under BaseWorld, so it follows the world's own lifecycle: BaseWorldPresenter switches the world on when
    /// the Base tab opens - OnEnable then reads the clock and applies the right look instantly, before the first
    /// visible frame - and off when another tab opens. While the world is shown it re-reads the clock every
    /// Time Check Interval seconds and when the app comes back from the background; only a change of period does any
    /// work (a short blend if the player is watching).
    ///
    /// What changes: BaseSun (warm sun -> cool moon), the ambient light (the scene's own skybox ambient, tinted per
    /// channel - so day is exactly the authored look), reflection strength, the camera's clear colour, the river's
    /// colours (its shader is unlit), and the street lamps (emissive bulbs + additive light pools, no real-time
    /// lights - the mobile pipeline allows one additional light per object). Ambient and reflections are global, so
    /// the scene's own values are put back whenever the world is hidden; the other menu previews never see the night.
    /// UI is untouched: it is drawn by the menu's own canvas, not by the base camera.</summary>
    public sealed class BaseWorldDayNightController : MonoBehaviour
    {
        private static readonly int ShallowId = Shader.PropertyToID("_ShallowColor");
        private static readonly int DeepId = Shader.PropertyToID("_DeepColor");
        private static readonly int FoamId = Shader.PropertyToID("_FoamColor");
        private static readonly int HighlightId = Shader.PropertyToID("_HighlightStrength");
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        [Header("Clock")]
        [SerializeField]
        [Tooltip("Off = always Day (unless Debug Override is on).")]
        private bool _useRealDeviceTime = true;

        [SerializeField, Range(0f, 24f)]
        private float _dayStartHour = 6f;

        [SerializeField, Range(0f, 24f)]
        private float _nightStartHour = 18f;

        [SerializeField, Range(5f, 300f)]
        [Tooltip("Seconds between clock checks while the base is shown (the check itself is one DateTime.Now).")]
        private float _timeCheckInterval = 30f;

        [SerializeField]
        private bool _enableSmoothTransitions = true;

        [SerializeField, Range(0.2f, 10f)]
        private float _transitionDuration = 3f;

        [Header("Debug (Editor / development builds only)")]
        [SerializeField]
        private bool _debugOverrideEnabled;

        [SerializeField, Range(0f, 23.99f)]
        private float _debugHour = 12f;

        [Header("Scene references")]
        [SerializeField]
        private Light _sun;

        [SerializeField]
        private Camera _worldCamera;

        [SerializeField]
        private Renderer[] _waterRenderers = new Renderer[0];

        [SerializeField]
        private BaseWorldNightLight[] _nightLights = new BaseWorldNightLight[0];

        [Header("Lamp glow")]
        [SerializeField]
        [Tooltip("Shared additive material of the light pools on the ground. One runtime copy is made for all lamps " +
                 "so the glow can fade without touching the asset.")]
        private Material _groundGlowMaterial;

        [SerializeField]
        private Material _bulbGlowMaterial;

        [SerializeField]
        private Color _groundGlowColor = new Color(1f, 0.80f, 0.45f, 0.8f);

        [SerializeField]
        private Color _bulbGlowColor = new Color(1f, 0.86f, 0.55f, 1f);

        [Header("Presets")]
        [SerializeField]
        private BaseWorldLightingPreset _day = new BaseWorldLightingPreset();

        [SerializeField]
        private BaseWorldLightingPreset _night = new BaseWorldLightingPreset();

        [Header("Alien tech & ambience (filled by BaseWorldDayNightBuilder)")]
        [SerializeField]
        [Tooltip("Shared emissive materials of the alien tech decor. One runtime copy of each is made and swapped onto " +
                 "Tech Renderers, so the glow can grow at night without touching the assets.")]
        private Material[] _techGlowMaterials = new Material[0];

        [SerializeField]
        private Renderer[] _techRenderers = new Renderer[0];

        [SerializeField, Min(0f)]
        [Tooltip("Emission multiplier by day (the tech still reads as lit, but sunlight dominates).")]
        private float _techEmissionDay = 0.6f;

        [SerializeField, Min(0f)]
        private float _techEmissionNight = 2.2f;

        [SerializeField]
        [Tooltip("Shown only at night (tech halos, beacon glow, fireflies). Their glow quads fade with the lamps.")]
        private GameObject[] _nightObjects = new GameObject[0];

        [SerializeField]
        [Tooltip("Halo quads among the night objects; they get the shared bulb-glow runtime material (fades in).")]
        private Renderer[] _nightGlowRenderers = new Renderer[0];

        [SerializeField]
        [Tooltip("Shown only by day (drifting pollen).")]
        private GameObject[] _dayObjects = new GameObject[0];

        private DayPeriod _period;
        private bool _hasPeriod;
        private float _blend;          // 0 = day, 1 = night
        private float _blendFrom;
        private float _blendTo;
        private float _transitionTime = -1f;
        private float _nextCheck;

        private bool _sceneAmbientCaptured;
        private SphericalHarmonicsL2 _sceneAmbient;
        private float _sceneReflection;

        private MaterialPropertyBlock _block;
        private Material _groundGlowRuntime;
        private Material _bulbGlowRuntime;
        private float[] _bulbThresholds;
        private Material[] _techRuntime;
        private Color[] _techBaseEmission;
        private bool _nightObjectsOn;
        private bool _dayObjectsOn;
        private bool _togglesSynced; // first blend always writes the toggles (the scene stores night objects off)
        private static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");

        public DayPeriod CurrentPeriod => _period;

        private void Awake()
        {
            _block = new MaterialPropertyBlock();
            if (_groundGlowMaterial != null)
            {
                _groundGlowRuntime = new Material(_groundGlowMaterial) { name = _groundGlowMaterial.name + " (Runtime)" };
            }

            if (_bulbGlowMaterial != null)
            {
                _bulbGlowRuntime = new Material(_bulbGlowMaterial) { name = _bulbGlowMaterial.name + " (Runtime)" };
            }

            // Each lamp's bulb switches on at its own point of the blend, so the street lights come on in a ripple
            // rather than all in the same frame. Fixed per lamp, computed once.
            _bulbThresholds = new float[_nightLights.Length];
            for (int i = 0; i < _nightLights.Length; i++)
            {
                _bulbThresholds[i] = 0.3f + 0.4f * Mathf.Repeat(i * 0.618034f, 1f);
                BaseWorldNightLight lamp = _nightLights[i];
                if (lamp == null)
                {
                    continue;
                }

                Renderer[] glows = lamp.GlowRenderers;
                for (int g = 0; g < glows.Length; g++)
                {
                    if (glows[g] == null)
                    {
                        continue;
                    }

                    // The halo is the quad facing the camera; the pool lies on the ground.
                    bool halo = glows[g].name.StartsWith("Bulb", StringComparison.Ordinal);
                    Material runtime = halo ? _bulbGlowRuntime : _groundGlowRuntime;
                    if (runtime != null)
                    {
                        glows[g].sharedMaterial = runtime;
                    }
                }
            }

            for (int i = 0; i < _nightGlowRenderers.Length; i++)
            {
                if (_nightGlowRenderers[i] != null && _bulbGlowRuntime != null)
                {
                    _nightGlowRenderers[i].sharedMaterial = _bulbGlowRuntime;
                }
            }

            // One runtime copy per tech glow material, swapped onto every renderer that uses the asset (once, here).
            _techRuntime = new Material[_techGlowMaterials.Length];
            _techBaseEmission = new Color[_techGlowMaterials.Length];
            for (int m = 0; m < _techGlowMaterials.Length; m++)
            {
                Material asset = _techGlowMaterials[m];
                if (asset == null)
                {
                    continue;
                }

                _techRuntime[m] = new Material(asset) { name = asset.name + " (Runtime)" };
                _techBaseEmission[m] = asset.HasProperty(EmissionId) ? asset.GetColor(EmissionId) : Color.black;
            }

            for (int r = 0; r < _techRenderers.Length; r++)
            {
                Renderer renderer = _techRenderers[r];
                if (renderer == null)
                {
                    continue;
                }

                Material[] materials = renderer.sharedMaterials;
                bool changed = false;
                for (int s = 0; s < materials.Length; s++)
                {
                    for (int m = 0; m < _techGlowMaterials.Length; m++)
                    {
                        if (materials[s] == _techGlowMaterials[m] && _techRuntime[m] != null)
                        {
                            materials[s] = _techRuntime[m];
                            changed = true;
                        }
                    }
                }

                if (changed)
                {
                    renderer.sharedMaterials = materials;
                }
            }
        }

        private void OnEnable()
        {
            if (!_sceneAmbientCaptured)
            {
                _sceneAmbient = RenderSettings.ambientProbe;
                _sceneReflection = RenderSettings.reflectionIntensity;
                _sceneAmbientCaptured = true;
            }

            // The tab was just opened (or the menu loaded): no blend - the first visible frame is already right.
            SetPeriod(EvaluatePeriod(), instant: true, force: true);
            _nextCheck = Time.unscaledTime + _timeCheckInterval;
        }

        private void OnDisable()
        {
            _transitionTime = -1f;
            if (_sceneAmbientCaptured)
            {
                RenderSettings.ambientProbe = _sceneAmbient;
                RenderSettings.reflectionIntensity = _sceneReflection;
            }
        }

        private void OnDestroy()
        {
            if (_groundGlowRuntime != null)
            {
                Destroy(_groundGlowRuntime);
            }

            if (_bulbGlowRuntime != null)
            {
                Destroy(_bulbGlowRuntime);
            }

            if (_techRuntime != null)
            {
                for (int m = 0; m < _techRuntime.Length; m++)
                {
                    if (_techRuntime[m] != null)
                    {
                        Destroy(_techRuntime[m]);
                    }
                }
            }
        }

        private void Update()
        {
            if (_transitionTime >= 0f)
            {
                _transitionTime += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(_transitionTime / _transitionDuration);
                ApplyBlend(Mathf.Lerp(_blendFrom, _blendTo, Mathf.SmoothStep(0f, 1f, t)));
                if (t >= 1f)
                {
                    _transitionTime = -1f; // done: nothing runs per frame until the next change
                }
            }

            if (Time.unscaledTime >= _nextCheck)
            {
                _nextCheck = Time.unscaledTime + _timeCheckInterval;
                SetPeriod(EvaluatePeriod(), instant: !_enableSmoothTransitions);
            }
        }

        /// <summary>Back from the background (Android resume): the clock may have crossed a threshold meanwhile.
        /// Nobody was watching, so the new look is applied at once.</summary>
        private void OnApplicationFocus(bool hasFocus)
        {
            if (hasFocus && isActiveAndEnabled)
            {
                SetPeriod(EvaluatePeriod(), instant: true);
                _nextCheck = Time.unscaledTime + _timeCheckInterval;
            }
        }

        private void OnApplicationPause(bool paused)
        {
            OnApplicationFocus(!paused);
        }

        private DayPeriod EvaluatePeriod()
        {
            float hour;
            if (_debugOverrideEnabled && (Application.isEditor || Debug.isDebugBuild))
            {
                hour = _debugHour;
            }
            else if (_useRealDeviceTime)
            {
                DateTime now = DateTime.Now;
                hour = now.Hour + now.Minute / 60f;
            }
            else
            {
                return DayPeriod.Day;
            }

            return PeriodAt(hour);
        }

        /// <summary>Day from Day Start (inclusive) to Night Start (exclusive), wrapping round midnight if needed.</summary>
        public DayPeriod PeriodAt(float hour)
        {
            bool day = _dayStartHour <= _nightStartHour
                ? hour >= _dayStartHour && hour < _nightStartHour
                : hour >= _dayStartHour || hour < _nightStartHour;
            return day ? DayPeriod.Day : DayPeriod.Night;
        }

        /// <param name="force">Apply even if the period did not change (OnEnable: hiding the world put the scene's
        /// own ambient back, so it has to be set again).</param>
        private void SetPeriod(DayPeriod period, bool instant, bool force = false)
        {
            float target = Target(period);
            bool same = _hasPeriod && period == _period;
            if (same && !force)
            {
                if (_transitionTime >= 0f && !instant)
                {
                    return; // already blending towards it
                }

                if (_transitionTime < 0f && Mathf.Approximately(_blend, target))
                {
                    return; // same period as before: nothing to do
                }
            }

            _hasPeriod = true;
            _period = period;
            if (instant)
            {
                _transitionTime = -1f;
                ApplyBlend(target);
                return;
            }

            _blendFrom = _blend;
            _blendTo = target;
            _transitionTime = 0f;
        }

        private static float Target(DayPeriod period)
        {
            return period == DayPeriod.Night ? 1f : 0f;
        }

        private void ApplyBlend(float night)
        {
            _blend = night;
            BaseWorldLightingPreset day = _day, dark = _night;

            if (_sun != null)
            {
                _sun.color = Color.Lerp(day.LightColor, dark.LightColor, night);
                _sun.intensity = Mathf.Lerp(day.LightIntensity, dark.LightIntensity, night);
                _sun.transform.rotation = Quaternion.Slerp(Quaternion.Euler(day.LightRotation), Quaternion.Euler(dark.LightRotation), night);
            }

            if (_sceneAmbientCaptured)
            {
                Color tint = Color.Lerp(day.AmbientTint * day.AmbientIntensity, dark.AmbientTint * dark.AmbientIntensity, night);
                SphericalHarmonicsL2 ambient = _sceneAmbient;
                for (int coefficient = 0; coefficient < 9; coefficient++)
                {
                    ambient[0, coefficient] *= tint.r;
                    ambient[1, coefficient] *= tint.g;
                    ambient[2, coefficient] *= tint.b;
                }

                RenderSettings.ambientProbe = ambient;
                RenderSettings.reflectionIntensity = _sceneReflection * Mathf.Lerp(day.ReflectionIntensity, dark.ReflectionIntensity, night);
            }

            if (_worldCamera != null)
            {
                _worldCamera.backgroundColor = Color.Lerp(day.CameraBackground, dark.CameraBackground, night);
            }

            for (int i = 0; i < _waterRenderers.Length; i++)
            {
                Renderer water = _waterRenderers[i];
                if (water == null)
                {
                    continue;
                }

                water.GetPropertyBlock(_block);
                _block.SetColor(ShallowId, Color.Lerp(day.WaterShallow, dark.WaterShallow, night));
                _block.SetColor(DeepId, Color.Lerp(day.WaterDeep, dark.WaterDeep, night));
                _block.SetColor(FoamId, Color.Lerp(day.WaterFoam, dark.WaterFoam, night));
                _block.SetFloat(HighlightId, Mathf.Lerp(day.WaterHighlight, dark.WaterHighlight, night));
                water.SetPropertyBlock(_block);
            }

            // Glows fade in over the second half of the blend, after the sun has mostly gone.
            float glow = Mathf.Clamp01((night - 0.35f) / 0.65f);

            if (_techRuntime != null)
            {
                float emission = Mathf.Lerp(_techEmissionDay, _techEmissionNight, night);
                for (int m = 0; m < _techRuntime.Length; m++)
                {
                    if (_techRuntime[m] != null)
                    {
                        _techRuntime[m].SetColor(EmissionId, _techBaseEmission[m] * emission);
                    }
                }
            }

            bool nightOn = glow > 0.001f;
            bool force = !_togglesSynced;
            _togglesSynced = true;
            if (force || nightOn != _nightObjectsOn)
            {
                _nightObjectsOn = nightOn;
                for (int i = 0; i < _nightObjects.Length; i++)
                {
                    if (_nightObjects[i] != null)
                    {
                        _nightObjects[i].SetActive(nightOn);
                    }
                }
            }

            bool dayOn = night < 0.5f;
            if (force || dayOn != _dayObjectsOn)
            {
                _dayObjectsOn = dayOn;
                for (int i = 0; i < _dayObjects.Length; i++)
                {
                    if (_dayObjects[i] != null)
                    {
                        _dayObjects[i].SetActive(dayOn);
                    }
                }
            }
            if (_groundGlowRuntime != null)
            {
                _groundGlowRuntime.SetColor(BaseColorId, new Color(_groundGlowColor.r, _groundGlowColor.g, _groundGlowColor.b, _groundGlowColor.a * glow));
            }

            if (_bulbGlowRuntime != null)
            {
                _bulbGlowRuntime.SetColor(BaseColorId, new Color(_bulbGlowColor.r, _bulbGlowColor.g, _bulbGlowColor.b, _bulbGlowColor.a * glow));
            }

            for (int i = 0; i < _nightLights.Length; i++)
            {
                if (_nightLights[i] != null)
                {
                    _nightLights[i].Apply(glow, _bulbThresholds != null && i < _bulbThresholds.Length ? _bulbThresholds[i] : 0.5f);
                }
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            // Changing the debug hour (or the thresholds) in Play mode re-reads the clock on the next frame and
            // blends, exactly like a real threshold crossing.
            if (Application.isPlaying && isActiveAndEnabled)
            {
                _nextCheck = 0f;
            }
        }

        [ContextMenu("Preview Day")]
        private void PreviewDay()
        {
            EditorPreview(0f);
        }

        [ContextMenu("Preview Night")]
        private void PreviewNight()
        {
            EditorPreview(1f);
        }

        /// <summary>Edit-mode look without entering Play. Uses the glow assets directly (no runtime copies), and
        /// Preview Day puts every value back to the authored day state, so remember to preview Day before saving.</summary>
        private void EditorPreview(float night)
        {
            if (Application.isPlaying)
            {
                _debugOverrideEnabled = true;
                _debugHour = night > 0.5f ? 22f : 12f;
                _nextCheck = 0f;
                return;
            }

            if (!_sceneAmbientCaptured)
            {
                _sceneAmbient = RenderSettings.ambientProbe;
                _sceneReflection = RenderSettings.reflectionIntensity;
                _sceneAmbientCaptured = true;
            }

            ApplyBlend(night);
            if (night <= 0f)
            {
                RenderSettings.ambientProbe = _sceneAmbient;
                RenderSettings.reflectionIntensity = _sceneReflection;
                _sceneAmbientCaptured = false;
            }

            UnityEditor.SceneView.RepaintAll();
        }
#endif
    }
}
