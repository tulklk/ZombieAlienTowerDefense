using DG.Tweening;
using UnityEngine;

namespace AlienDefense.UI.Defense
{
    /// <summary>The Defense popup's 3D showcase: ONE camera, ONE RenderTexture, ONE small environment (pad, grass,
    /// rocks, a workshop prop, a soft painted backdrop) and ONE model at a time, all on their own layer so no other
    /// camera sees them. Visual only: it clones just the model branch of a tower prefab (meshes, no gameplay
    /// components), switches off anything scripted that came along, and never touches the source prefab.
    ///
    /// The camera renders only while the showcase is on screen (SetVisible); the texture is created once and
    /// released with this object. The model is framed from its renderer bounds, so towers of different sizes (and
    /// the base building) need no per-model camera values.</summary>
    public sealed class TowerPreviewRenderer : MonoBehaviour
    {
        [SerializeField] private Camera _camera;
        [SerializeField] private Transform _anchor;
        [SerializeField] private Transform _pad;

        [Header("Render texture")]
        [SerializeField, Min(64)] private int _textureWidth = 640;
        [SerializeField, Min(64)] private int _textureHeight = 368;

        [Header("Framing")]
        [SerializeField, Range(5f, 40f)] private float _cameraPitch = 30f;
        [SerializeField, Range(-80f, 80f)] private float _cameraYaw = 28f;
        [SerializeField, Range(0.3f, 0.95f)]
        [Tooltip("Share of the picture's height the model fills.")]
        private float _fill = 0.86f;
        [SerializeField, Range(-0.3f, 0.3f)]
        [Tooltip("Moves the model up (+) or down (-) in the picture, as a share of its height.")]
        private float _verticalBias = 0.21f;
        [SerializeField, Range(-180f, 180f)]
        [Tooltip("Turn applied to every model so its muzzle points to the left of the picture, a little towards the viewer (measured on the tower models: 0 shows their back).")]
        private float _modelYaw = 60f;

        [SerializeField, Min(0.1f)]
        [Tooltip("Models larger than this (metres, biggest side) are scaled down to it so they still stand on the pad.")]
        private float _maxModelSize = 2.2f;

        [Header("Idle")]
        [SerializeField, Range(0f, 30f)] private float _swayDegrees = 8f;
        [SerializeField, Min(0.5f)] private float _swaySeconds = 5f;

        private RenderTexture _texture;
        private GameObject _model;
        private GameObject _modelSource;
        private Tween _sway;
        private Tween _punch;
        private bool _visible;

        /// <summary>The picture; bind it to a RawImage. Created on first use.</summary>
        public RenderTexture Texture
        {
            get
            {
                EnsureTexture();
                return _texture;
            }
        }

        private void Awake()
        {
            if (_camera != null)
            {
                _camera.enabled = false;
            }
        }

        private void EnsureTexture()
        {
            if (_texture != null)
            {
                return;
            }

            _texture = new RenderTexture(_textureWidth, _textureHeight, 16, RenderTextureFormat.ARGB32)
            {
                name = "TowerPreviewRT",
                antiAliasing = 2,
                useMipMap = false,
            };
            if (_camera != null)
            {
                _camera.targetTexture = _texture;
            }
        }

        /// <summary>Turns the showcase on or off. Off = the camera does not render and the idle motion stops.</summary>
        public void SetVisible(bool visible)
        {
            if (_visible == visible)
            {
                return;
            }

            _visible = visible;
            if (visible)
            {
                EnsureTexture();
            }

            gameObject.SetActive(visible);
            if (_camera != null)
            {
                _camera.enabled = visible;
            }

            if (visible)
            {
                // Renderer bounds are only valid while active, so a model handed over while hidden is framed now.
                if (_model != null)
                {
                    _sway?.Kill();
                    _anchor.localRotation = Quaternion.identity;
                    Frame();
                }

                StartSway();
            }
            else
            {
                _sway?.Kill();
                _punch?.Kill();
            }
        }

        /// <summary>Shows a visual-only clone of <paramref name="source"/> (a model branch or a model asset). The same
        /// source again is a no-op, so refreshing the popup never re-instantiates. Null clears the pad.</summary>
        public void Show(GameObject source)
        {
            if (source == _modelSource && _model != null)
            {
                return;
            }

            if (_model != null)
            {
                Destroy(_model);
                _model = null;
            }

            _modelSource = source;
            if (source == null || _anchor == null)
            {
                return;
            }

            _anchor.localRotation = Quaternion.identity;
            _anchor.localScale = Vector3.one;
            _model = Instantiate(source, _anchor);
            _model.name = source.name;
            _model.transform.localPosition = Vector3.zero;
            _model.transform.localRotation = Quaternion.Euler(0f, _modelYaw, 0f) * source.transform.localRotation;
            _model.SetActive(true);
            MakeVisualOnly(_model);
            if (_visible)
            {
                _sway?.Kill();
                Frame();
                StartSway();
            }
        }

        /// <summary>Switches off everything that is not a renderer and moves the clone onto this object's layer.</summary>
        private void MakeVisualOnly(GameObject model)
        {
            foreach (Behaviour behaviour in model.GetComponentsInChildren<Behaviour>(true))
            {
                behaviour.enabled = false; // scripts, animators, lights, audio - nothing of the gameplay runs here
            }

            foreach (Collider collider in model.GetComponentsInChildren<Collider>(true))
            {
                collider.enabled = false;
            }

            foreach (ParticleSystem particles in model.GetComponentsInChildren<ParticleSystem>(true))
            {
                particles.gameObject.SetActive(false);
            }

            int layer = gameObject.layer;
            foreach (Transform t in model.GetComponentsInChildren<Transform>(true))
            {
                t.gameObject.layer = layer;
            }
        }

        /// <summary>Scales an oversized model down, stands it centred on the pad and pulls the camera back until the
        /// model fills the configured share of the picture.</summary>
        private void Frame()
        {
            if (!TryGetBounds(out Bounds bounds))
            {
                return;
            }

            float biggest = Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z));
            if (biggest > _maxModelSize)
            {
                _model.transform.localScale *= _maxModelSize / biggest;
                TryGetBounds(out bounds);
            }

            // Stand the model's own pivot (the middle of its base - where it stands on a build node) on the middle of
            // the pad, feet on the pad. Centring the bounds instead would let a long barrel push the base off-centre.
            // A model whose pivot lies outside its own footprint falls back to the bounds centre.
            Vector3 pivot = _model.transform.position;
            bool pivotInside = pivot.x >= bounds.min.x && pivot.x <= bounds.max.x && pivot.z >= bounds.min.z && pivot.z <= bounds.max.z;
            Vector3 foot = pivotInside ? new Vector3(pivot.x, bounds.min.y, pivot.z) : new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
            Vector3 offset = _anchor.position - foot;
            _model.transform.position += offset;
            bounds.center += offset;

            if (_pad != null)
            {
                // The pad grows a little with the footprint so every model stands well inside its rim.
                float footprint = Mathf.Max(bounds.size.x, bounds.size.z);
                float padScale = Mathf.Clamp(footprint / 2f, 0.85f, 1.45f);
                _pad.localScale = new Vector3(padScale, 1f, padScale);
            }

            if (_camera == null)
            {
                return;
            }

            // Sphere fit: far enough that the model's bounding sphere takes _fill of the vertical field of view.
            // The picture is centred on the pad, so the sphere also has to reach whatever overhangs to one side.
            Vector3 centre = new Vector3(_anchor.position.x, bounds.center.y, _anchor.position.z);
            float radius = bounds.extents.magnitude + Vector3.Distance(centre, bounds.center) * 0.5f;
            float halfFov = _camera.fieldOfView * 0.5f * Mathf.Deg2Rad;
            float distance = radius / (Mathf.Sin(halfFov) * Mathf.Max(0.3f, _fill)) * 0.85f;
            Quaternion rotation = Quaternion.Euler(_cameraPitch, 180f + _cameraYaw, 0f);
            Vector3 focus = centre - Vector3.up * (bounds.size.y * _verticalBias);
            _camera.transform.SetPositionAndRotation(focus - rotation * Vector3.forward * distance, rotation);
            _camera.nearClipPlane = 0.1f;
            _camera.farClipPlane = distance + 40f;
        }

        private bool TryGetBounds(out Bounds bounds)
        {
            bounds = default;
            bool any = false;
            foreach (Renderer renderer in _model.GetComponentsInChildren<Renderer>(false))
            {
                if (renderer is ParticleSystemRenderer || renderer is TrailRenderer || !renderer.enabled)
                {
                    continue;
                }

                if (any)
                {
                    bounds.Encapsulate(renderer.bounds);
                }
                else
                {
                    bounds = renderer.bounds;
                    any = true;
                }
            }

            return any;
        }

        /// <summary>A slow look-around: the model (not the camera) turns a few degrees either way.</summary>
        private void StartSway()
        {
            _sway?.Kill();
            if (_anchor == null || _swayDegrees <= 0f)
            {
                return;
            }

            _anchor.localRotation = Quaternion.Euler(0f, -_swayDegrees, 0f);
            _sway = _anchor.DOLocalRotate(new Vector3(0f, _swayDegrees, 0f), _swaySeconds)
                .SetEase(Ease.InOutSine).SetLoops(-1, LoopType.Yoyo).SetUpdate(true).SetLink(gameObject);
        }

        /// <summary>The level-up pop: the model swells and settles.</summary>
        public void PlayPunch()
        {
            if (_anchor == null)
            {
                return;
            }

            _punch?.Kill(true);
            _anchor.localScale = Vector3.one;
            _punch = _anchor.DOPunchScale(Vector3.one * 0.09f, 0.4f, 5, 0.6f).SetUpdate(true).SetLink(gameObject);
        }

        private void OnDestroy()
        {
            _sway?.Kill();
            _punch?.Kill();
            if (_camera != null)
            {
                _camera.targetTexture = null;
            }

            if (_texture != null)
            {
                _texture.Release();
                Destroy(_texture);
                _texture = null;
            }
        }
    }
}
