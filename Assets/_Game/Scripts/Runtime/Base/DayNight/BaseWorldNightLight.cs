using UnityEngine;

namespace AlienDefense.Base.DayNight
{
    /// <summary>One street lamp's night look: the bulb switches to an emissive shared material, and the lamp's
    /// night visuals (a halo round the bulb and a warm pool of light on the ground - additive quads, no real-time
    /// light) are switched on. An optional Light can be added for a lamp that really needs one; it never casts
    /// shadows. Everything is cached once - BaseWorldDayNightController only calls Apply on a change.</summary>
    public sealed class BaseWorldNightLight : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("The lamp's renderer (its bulb is one of its material slots).")]
        private Renderer _lampRenderer;

        [SerializeField, Min(0)]
        private int _bulbMaterialSlot = 1;

        [SerializeField]
        [Tooltip("Shared emissive bulb material used at night (one asset for every lamp).")]
        private Material _nightBulbMaterial;

        [SerializeField]
        [Tooltip("Halo + ground pool. Off by day.")]
        private GameObject _nightVisual;

        [SerializeField]
        [Tooltip("Ground-pool and halo renderers (they get the controller's shared glow materials).")]
        private Renderer[] _glowRenderers = new Renderer[0];

        [SerializeField]
        [Tooltip("Optional real-time light. Leave empty on mobile unless a lamp really needs it; shadows are forced off.")]
        private Light _optionalLight;

        [SerializeField, Min(0f)]
        private float _optionalLightIntensity = 1.5f;

        private Material[] _dayMaterials;
        private Material[] _nightMaterials;
        private bool _bulbOn;
        private bool _visualOn;
        private bool _cached;

        public Renderer[] GlowRenderers => _glowRenderers;

        private void Cache()
        {
            if (_cached)
            {
                return;
            }

            _cached = true;
            if (_lampRenderer != null)
            {
                _dayMaterials = _lampRenderer.sharedMaterials;
                _nightMaterials = (Material[])_dayMaterials.Clone();
                if (_nightBulbMaterial != null && _bulbMaterialSlot < _nightMaterials.Length)
                {
                    _nightMaterials[_bulbMaterialSlot] = _nightBulbMaterial;
                }

                _bulbOn = false; // the scene stores the day bulb
            }

            if (_optionalLight != null)
            {
                _optionalLight.shadows = LightShadows.None;
            }

            _visualOn = _nightVisual != null && _nightVisual.activeSelf;
        }

        /// <param name="night">0 = day, 1 = night (blend while transitioning).</param>
        /// <param name="bulbThreshold">Blend value at which this lamp's bulb switches on - staggered per lamp.</param>
        public void Apply(float night, float bulbThreshold)
        {
            Cache();
            bool visual = night > 0.001f;
            if (_nightVisual != null && visual != _visualOn)
            {
                _visualOn = visual;
                _nightVisual.SetActive(visual);
            }

            bool bulb = night >= bulbThreshold;
            if (_lampRenderer != null && _nightBulbMaterial != null && bulb != _bulbOn)
            {
                _bulbOn = bulb;
                _lampRenderer.sharedMaterials = bulb ? _nightMaterials : _dayMaterials;
            }

            if (_optionalLight != null)
            {
                _optionalLight.enabled = visual;
                _optionalLight.intensity = _optionalLightIntensity * night;
            }
        }

        public void Configure(Renderer lampRenderer, int bulbSlot, Material nightBulb, GameObject nightVisual, Renderer[] glowRenderers)
        {
            _lampRenderer = lampRenderer;
            _bulbMaterialSlot = bulbSlot;
            _nightBulbMaterial = nightBulb;
            _nightVisual = nightVisual;
            _glowRenderers = glowRenderers;
        }
    }
}
