using UnityEngine;
using UnityEngine.UI;

namespace AlienDefense.UI.Minimap
{
    /// <summary>Inspector front-end for the AlienDefense/UI/MinimapRadarScan material on the minimap's ScanOverlay
    /// image. Pure presentation - knows nothing about waves, paths or gameplay, matching the rest of this folder.
    ///
    /// There is deliberately no Update(): the sweep animates from _Time inside the shader, on the GPU. Driving it
    /// from C# would cost a managed call per frame for something the shader does for free.
    ///
    /// The one value the shader cannot work out for itself is the rect's height in local units, which it needs to
    /// turn object-space Y into a 0-1 sweep coordinate (see the shader header for why UV cannot be used with a
    /// 9-sliced sprite). That is pushed here, and again whenever the rect is resized.</summary>
    [RequireComponent(typeof(Image))]
    [ExecuteAlways]
    public sealed class MinimapRadarScanOverlay : BaseMeshEffect
    {
        private static readonly int ScanColorId = Shader.PropertyToID("_ScanColor");
        private static readonly int ScanHotColorId = Shader.PropertyToID("_ScanHotColor");
        private static readonly int BaseTintId = Shader.PropertyToID("_BaseTint");
        private static readonly int BaseAlphaId = Shader.PropertyToID("_BaseAlpha");
        private static readonly int ScanIntensityId = Shader.PropertyToID("_ScanIntensity");
        private static readonly int ScanLineIntensityId = Shader.PropertyToID("_ScanLineIntensity");
        private static readonly int ScanWidthId = Shader.PropertyToID("_ScanWidth");
        private static readonly int ScanSoftnessId = Shader.PropertyToID("_ScanSoftness");
        private static readonly int ScanLineWidthId = Shader.PropertyToID("_ScanLineWidth");
        private static readonly int ScanSpeedId = Shader.PropertyToID("_ScanSpeed");
        private static readonly int StripeStrengthId = Shader.PropertyToID("_StripeStrength");
        private static readonly int StripeCountId = Shader.PropertyToID("_StripeCount");
        private static readonly int RectHeightId = Shader.PropertyToID("_RectHeight");

        [Header("Sweep")]
        [SerializeField, Range(0f, 3f)]
        [Tooltip("Sweeps per second. 0.35 = one pass roughly every 3 seconds, which reads as a radar rather than " +
            "a strobe.")]
        private float _scanSpeed = 0.35f;

        [SerializeField, Range(0.01f, 0.6f)]
        [Tooltip("Half-height of the lit band, as a fraction of the minimap's height.")]
        private float _scanWidth = 0.22f;

        [SerializeField, Range(0f, 1f)]
        [Tooltip("0 = hard-edged band, 1 = fully feathered.")]
        private float _scanSoftness = 0.85f;

        [SerializeField, Range(0.001f, 0.2f)]
        [Tooltip("Thickness of the bright core line at the head of the sweep.")]
        private float _scanLineWidth = 0.025f;

        [Header("Brightness")]
        [SerializeField, Range(0f, 1f)]
        [Tooltip("How much the whole overlay tints the minimap when the sweep is nowhere near. Keep this low or " +
            "the path and markers underneath get muddy.")]
        private float _baseAlpha = 0.10f;

        [SerializeField, Range(0f, 1f)]
        private float _scanIntensity = 0.38f;

        [SerializeField, Range(0f, 1f)]
        private float _scanLineIntensity = 0.55f;

        [Header("Colour")]
        [SerializeField]
        private Color _scanColor = new Color(0.35f, 0.85f, 1f, 1f);

        [SerializeField]
        [Tooltip("Colour of the thin core line - near white sells the 'sweep just passed' moment.")]
        private Color _scanHotColor = new Color(0.85f, 0.98f, 1f, 1f);

        [SerializeField]
        [Tooltip("Faint tint over the un-scanned part of the map. A slightly darker navy makes the sweep pop " +
            "without hiding anything.")]
        private Color _baseTint = new Color(0.04f, 0.22f, 0.42f, 1f);

        [Header("CRT striping (optional, off by default)")]
        [SerializeField, Range(0f, 0.5f)]
        private float _stripeStrength;

        [SerializeField, Range(4f, 128f)]
        private float _stripeCount = 42f;

        private Image _image;
        private Material _runtimeMaterial;
        private RectTransform _rectTransform;

        protected override void Awake()
        {
            base.Awake();
            CacheReferences();
            Apply();
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            CacheReferences();
            Apply();
            EnableCanvasUv1Channel();
        }

        /// <summary>Writes each vertex's position inside the rect, normalised to 0-1, into UV1. The shader reads
        /// the y of that to place the sweep. Runs only when the UI mesh is rebuilt (resize, enable, colour
        /// change) - not per frame.</summary>
        public override void ModifyMesh(VertexHelper vertexHelper)
        {
            if (!IsActive() || _rectTransform == null)
            {
                return;
            }

            Rect rect = _rectTransform.rect;
            if (rect.width <= 0f || rect.height <= 0f)
            {
                return;
            }

            UIVertex vertex = default;
            for (int i = 0; i < vertexHelper.currentVertCount; i++)
            {
                vertexHelper.PopulateUIVertex(ref vertex, i);
                vertex.uv1 = new Vector4(
                    Mathf.InverseLerp(rect.xMin, rect.xMax, vertex.position.x),
                    Mathf.InverseLerp(rect.yMin, rect.yMax, vertex.position.y),
                    0f,
                    0f);
                vertexHelper.SetUIVertex(vertex, i);
            }
        }

        /// <summary>UV1 is stripped from the UI vertex stream unless the Canvas is told to keep it, and a
        /// stripped UV1 arrives in the shader as zero - which parks the sweep at the bottom edge forever.</summary>
        private void EnableCanvasUv1Channel()
        {
            Canvas canvas = _image != null ? _image.canvas : null;
            if (canvas == null)
            {
                return;
            }

            canvas.additionalShaderChannels |= AdditionalCanvasShaderChannels.TexCoord1;
        }

        protected override void OnRectTransformDimensionsChange()
        {
            base.OnRectTransformDimensionsChange();

            if (_runtimeMaterial == null || _rectTransform == null)
            {
                return;
            }

            _runtimeMaterial.SetFloat(RectHeightId, _rectTransform.rect.height);
        }

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();

            if (!isActiveAndEnabled)
            {
                return;
            }

            CacheReferences();
            Apply();
            EnableCanvasUv1Channel();
        }
#endif

        protected override void OnDestroy()
        {
            base.OnDestroy();

            if (_runtimeMaterial == null)
            {
                return;
            }

            // The material is an instance owned by this component, so it has to be cleaned up by hand.
            if (Application.isPlaying)
            {
                Destroy(_runtimeMaterial);
            }
            else
            {
                DestroyImmediate(_runtimeMaterial);
            }

            _runtimeMaterial = null;
        }

        private void CacheReferences()
        {
            if (_image == null)
            {
                _image = GetComponent<Image>();
            }

            if (_rectTransform == null)
            {
                _rectTransform = (RectTransform)transform;
            }

            if (_image == null || _image.material == null)
            {
                return;
            }

            // Work on an instance so tweaking one minimap never writes back into the shared material asset.
            if (_runtimeMaterial == null || _image.material != _runtimeMaterial)
            {
                if (_image.material.shader != null
                    && _image.material.shader.name == "AlienDefense/UI/MinimapRadarScan")
                {
                    _runtimeMaterial = new Material(_image.material) { name = _image.material.name + " (instance)" };
                    _image.material = _runtimeMaterial;
                }
            }
        }

        /// <summary>Pushes every inspector value into the material. Called on enable and on inspector edits - not
        /// per frame, because the sweep itself is animated by the shader.</summary>
        public void Apply()
        {
            if (_runtimeMaterial == null)
            {
                return;
            }

            _runtimeMaterial.SetColor(ScanColorId, _scanColor);
            _runtimeMaterial.SetColor(ScanHotColorId, _scanHotColor);
            _runtimeMaterial.SetColor(BaseTintId, _baseTint);
            _runtimeMaterial.SetFloat(BaseAlphaId, _baseAlpha);
            _runtimeMaterial.SetFloat(ScanIntensityId, _scanIntensity);
            _runtimeMaterial.SetFloat(ScanLineIntensityId, _scanLineIntensity);
            _runtimeMaterial.SetFloat(ScanWidthId, _scanWidth);
            _runtimeMaterial.SetFloat(ScanSoftnessId, _scanSoftness);
            _runtimeMaterial.SetFloat(ScanLineWidthId, _scanLineWidth);
            _runtimeMaterial.SetFloat(ScanSpeedId, _scanSpeed);
            _runtimeMaterial.SetFloat(StripeStrengthId, _stripeStrength);
            _runtimeMaterial.SetFloat(StripeCountId, _stripeCount);

            if (_rectTransform != null)
            {
                _runtimeMaterial.SetFloat(RectHeightId, _rectTransform.rect.height);
            }
        }

        /// <summary>Lets gameplay speed the sweep up (for example while a boss wave is incoming) without any
        /// per-frame cost - call it once when the state changes.</summary>
        public void SetScanSpeed(float sweepsPerSecond)
        {
            _scanSpeed = Mathf.Clamp(sweepsPerSecond, 0f, 3f);
            _runtimeMaterial?.SetFloat(ScanSpeedId, _scanSpeed);
        }
    }
}
