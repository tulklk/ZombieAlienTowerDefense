using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace AlienDefense.UI.Profile
{
    /// <summary>Off-screen UFO garage preview → RenderTexture → RawImage. Visual-only; no Player scripts.</summary>
    public sealed class ProfileUfoPreviewController : MonoBehaviour
    {
        public const string PreviewLayerName = "MainMenuPreview";

        [SerializeField]
        private Camera _previewCamera;

        [SerializeField]
        private Transform _ufoRoot;

        [SerializeField]
        private Transform _modelPivot;

        [SerializeField]
        private RawImage _targetImage;

        [SerializeField]
        private int _textureWidth = 512;

        [SerializeField]
        private int _textureHeight = 384;

        [SerializeField]
        private float _hoverAmplitude = 0.04f;

        [SerializeField]
        private float _hoverFrequency = 0.55f;

        [SerializeField]
        private float _spinDegreesPerSecond = 20f;

        [SerializeField]
        private float _cameraFov = 22f;

        [SerializeField]
        private float _cameraPitch = 15f;

        [SerializeField]
        private float _frameFill = 1.45f;

        private RenderTexture _renderTexture;
        private Vector3 _restRootPosition;
        private Quaternion _restRootRotation;
        private Vector3 _restRootScale = Vector3.one;
        private Vector3 _pivotBaseLocalPosition;
        private float _spinAngle;
        private Tween _bobTween;
        private Tween _entryTween;
        private bool _active;
        private bool _framed;
        private bool _idleRunning;

        public void Configure(Camera previewCamera, Transform ufoRoot, RawImage targetImage)
        {
            _previewCamera = previewCamera;
            _ufoRoot = ufoRoot;
            _targetImage = targetImage;
            CacheRestPose();
            EnsureRenderTexture();
            ApplyCameraDefaults();
            SetActive(false);
        }

        public void SetPreviewModel(GameObject prefab)
        {
            if (prefab == null || _ufoRoot == null)
            {
                return;
            }

            EnsureModelPivot();
            for (int i = _modelPivot.childCount - 1; i >= 0; i--)
            {
                Destroy(_modelPivot.GetChild(i).gameObject);
            }

            GameObject instance = Instantiate(prefab, _modelPivot);
            instance.name = prefab.name;
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            instance.transform.localScale = Vector3.one;
            StripGameplay(instance);
            ApplyLayerRecursive(instance.transform, LayerMask.NameToLayer(PreviewLayerName));
            _framed = false;
            _pivotBaseLocalPosition = _modelPivot.localPosition;
        }

        public void Show() => SetActive(true);

        public void Hide() => SetActive(false);

        private void Awake()
        {
            CacheRestPose();
            EnsureModelPivot();
            if (_modelPivot != null)
            {
                _pivotBaseLocalPosition = _modelPivot.localPosition;
            }

            EnsureRenderTexture();
            ApplyCameraDefaults();
            SetActive(false);
        }

        private void Update()
        {
            if (!_idleRunning || _modelPivot == null)
            {
                return;
            }

            _spinAngle = (_spinAngle + _spinDegreesPerSecond * Time.unscaledDeltaTime) % 360f;
            _modelPivot.localRotation = Quaternion.Euler(0f, _spinAngle, 0f);
        }

        private void OnDestroy()
        {
            StopIdle();
            _entryTween?.Kill();
            ReleaseRenderTexture();
        }

        public void SetActive(bool active)
        {
            _active = active;
            if (_previewCamera != null)
            {
                _previewCamera.enabled = active;
            }

            if (_ufoRoot != null)
            {
                _ufoRoot.gameObject.SetActive(active);
            }

            if (_targetImage != null)
            {
                _targetImage.raycastTarget = false;
                Color c = _targetImage.color;
                c.a = 1f;
                _targetImage.color = c;
            }

            if (active)
            {
                EnsureRenderTexture();
                // Always re-frame after hierarchy may have changed (inactive beams, etc.).
                FrameTarget();
                _framed = true;
                PlayEntryThenIdle();
            }
            else
            {
                StopIdle();
                _entryTween?.Kill();
                _entryTween = null;
            }
        }

        private void CacheRestPose()
        {
            if (_ufoRoot == null)
            {
                return;
            }

            _restRootPosition = _ufoRoot.localPosition;
            _restRootRotation = _ufoRoot.localRotation;
            _restRootScale = _ufoRoot.localScale;
        }

        private void EnsureModelPivot()
        {
            if (_ufoRoot == null)
            {
                return;
            }

            if (_modelPivot != null)
            {
                return;
            }

            Transform existing = _ufoRoot.Find("ModelPivot");
            if (existing != null)
            {
                _modelPivot = existing;
                return;
            }

            var pivot = new GameObject("ModelPivot");
            pivot.transform.SetParent(_ufoRoot, false);
            _modelPivot = pivot.transform;
            for (int i = _ufoRoot.childCount - 1; i >= 0; i--)
            {
                Transform child = _ufoRoot.GetChild(i);
                if (child == _modelPivot || child.name.Contains("Hologram"))
                {
                    continue;
                }

                child.SetParent(_modelPivot, true);
            }
        }

        private void ApplyCameraDefaults()
        {
            if (_previewCamera == null)
            {
                return;
            }

            _previewCamera.clearFlags = CameraClearFlags.SolidColor;
            _previewCamera.backgroundColor = new Color(0f, 0f, 0f, 0f);
            _previewCamera.orthographic = false;
            _previewCamera.fieldOfView = _cameraFov;
            _previewCamera.nearClipPlane = 0.05f;
            _previewCamera.farClipPlane = 80f;
            _previewCamera.allowHDR = false;
            _previewCamera.allowMSAA = false;

            int layer = LayerMask.NameToLayer(PreviewLayerName);
            if (layer >= 0)
            {
                _previewCamera.cullingMask = 1 << layer;
            }
        }

        private void FrameTarget()
        {
            EnsureModelPivot();
            if (_previewCamera == null || _modelPivot == null)
            {
                return;
            }

            // Activate root so activeInHierarchy is true while measuring.
            bool wasActive = _ufoRoot != null && _ufoRoot.gameObject.activeSelf;
            if (_ufoRoot != null && !wasActive)
            {
                _ufoRoot.gameObject.SetActive(true);
            }

            Renderer[] renderers = _modelPivot.GetComponentsInChildren<Renderer>(true);
            Bounds bounds = default;
            bool hasBounds = false;
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer r = renderers[i];
                if (!IsBodyRenderer(r))
                {
                    continue;
                }

                if (!hasBounds)
                {
                    bounds = r.bounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(r.bounds);
                }
            }

            if (!hasBounds)
            {
                _previewCamera.transform.localPosition = new Vector3(0f, 1.1f, -3.8f);
                _previewCamera.transform.localRotation = Quaternion.Euler(_cameraPitch, 0f, 0f);
                if (_ufoRoot != null && !wasActive)
                {
                    _ufoRoot.gameObject.SetActive(false);
                }

                return;
            }

            Vector3 center = bounds.center;
            float radius = Mathf.Max(bounds.extents.x, bounds.extents.y, bounds.extents.z);
            float halfFovRad = _cameraFov * 0.5f * Mathf.Deg2Rad;
            float distance = (radius / Mathf.Max(0.01f, Mathf.Sin(halfFovRad))) / Mathf.Max(0.2f, _frameFill);
            distance = Mathf.Clamp(distance, 1.2f, 8f);

            Vector3 camDir = Quaternion.Euler(_cameraPitch, 0f, 0f) * Vector3.back;
            _previewCamera.transform.position = center + camDir * distance;
            _previewCamera.transform.rotation = Quaternion.LookRotation(center - _previewCamera.transform.position, Vector3.up);

            if (_ufoRoot != null && !wasActive)
            {
                _ufoRoot.gameObject.SetActive(false);
            }
        }

        private static bool IsBodyRenderer(Renderer r)
        {
            if (r == null || !r.enabled || !r.gameObject.activeInHierarchy)
            {
                return false;
            }

            if (r is ParticleSystemRenderer)
            {
                return false;
            }

            string n = r.gameObject.name;
            if (n.IndexOf("Beam", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                n.IndexOf("Shadow", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                n.IndexOf("Particle", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                n.IndexOf("Glow", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                n.IndexOf("Spark", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                n.IndexOf("Streak", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                n.IndexOf("Orb", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return false;
            }

            return true;
        }

        private void PlayEntryThenIdle()
        {
            StopIdle();
            _entryTween?.Kill();
            if (_ufoRoot == null)
            {
                return;
            }

            _ufoRoot.localScale = _restRootScale * 0.85f;
            _ufoRoot.localPosition = _restRootPosition + Vector3.down * 0.05f;
            _ufoRoot.localRotation = _restRootRotation;

            Sequence seq = DOTween.Sequence().SetUpdate(true);
            seq.Join(_ufoRoot.DOScale(_restRootScale, 0.35f).SetEase(Ease.OutBack));
            seq.Join(_ufoRoot.DOLocalMove(_restRootPosition, 0.35f).SetEase(Ease.OutQuad));
            seq.OnComplete(StartIdle);
            _entryTween = seq;
        }

        private void StartIdle()
        {
            StopIdle();
            EnsureModelPivot();
            if (_modelPivot == null)
            {
                return;
            }

            _pivotBaseLocalPosition = _modelPivot.localPosition;
            _spinAngle = _modelPivot.localEulerAngles.y;

            if (_hoverAmplitude > 0f && _hoverFrequency > 0f)
            {
                float halfCycle = 1f / (_hoverFrequency * 2f);
                _modelPivot.localPosition = _pivotBaseLocalPosition - new Vector3(0f, _hoverAmplitude, 0f);
                _bobTween = _modelPivot.DOLocalMoveY(_pivotBaseLocalPosition.y + _hoverAmplitude, halfCycle)
                    .SetEase(Ease.InOutSine)
                    .SetLoops(-1, LoopType.Yoyo)
                    .SetUpdate(true);
            }

            _idleRunning = true;
        }

        private void StopIdle()
        {
            _idleRunning = false;
            _bobTween?.Kill();
            _bobTween = null;
            if (_modelPivot != null)
            {
                _modelPivot.localPosition = _pivotBaseLocalPosition;
            }

            if (_ufoRoot != null)
            {
                _ufoRoot.localPosition = _restRootPosition;
                _ufoRoot.localScale = _restRootScale;
            }
        }

        private void EnsureRenderTexture()
        {
            if (_renderTexture != null)
            {
                BindTexture();
                return;
            }

            _renderTexture = new RenderTexture(_textureWidth, _textureHeight, 16, RenderTextureFormat.ARGB32)
            {
                name = "ProfileUfoPreviewRT",
                antiAliasing = 2,
                filterMode = FilterMode.Bilinear,
            };
            _renderTexture.Create();
            BindTexture();
        }

        private void BindTexture()
        {
            if (_previewCamera != null)
            {
                _previewCamera.targetTexture = _renderTexture;
            }

            if (_targetImage != null)
            {
                _targetImage.texture = _renderTexture;
            }
        }

        private void ReleaseRenderTexture()
        {
            if (_previewCamera != null)
            {
                _previewCamera.targetTexture = null;
            }

            if (_targetImage != null)
            {
                _targetImage.texture = null;
            }

            if (_renderTexture != null)
            {
                _renderTexture.Release();
                Destroy(_renderTexture);
                _renderTexture = null;
            }
        }

        public static void StripGameplay(GameObject root)
        {
            if (root == null)
            {
                return;
            }

            foreach (MonoBehaviour behaviour in root.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (behaviour == null)
                {
                    continue;
                }

                string typeName = behaviour.GetType().Name;
                if (typeName.Contains("Player") || typeName.Contains("Movement") ||
                    typeName.Contains("Hover") || typeName.Contains("Flight") ||
                    typeName.Contains("Controller") || typeName.Contains("Weapon") ||
                    typeName.Contains("Health") || typeName.Contains("Input") ||
                    typeName.Contains("Audio") || typeName.Contains("Pool") ||
                    typeName.Contains("Tractor") || typeName.Contains("Beam") ||
                    typeName.Contains("Combat") || typeName.Contains("Damage") ||
                    typeName.Contains("Target") || typeName.Contains("Attractor"))
                {
                    behaviour.enabled = false;
                }
            }

            foreach (CharacterController cc in root.GetComponentsInChildren<CharacterController>(true))
            {
                cc.enabled = false;
            }

            foreach (Collider col in root.GetComponentsInChildren<Collider>(true))
            {
                col.enabled = false;
            }

            foreach (Rigidbody rb in root.GetComponentsInChildren<Rigidbody>(true))
            {
                rb.isKinematic = true;
                rb.detectCollisions = false;
            }

            string[] disableNames =
            {
                "TractorBeamRoot", "GroundIndicator", "CameraFollowTarget", "CollectionPoint",
                "Shadow", "BeamConeOuter", "BeamConeInner", "TractorBeamParticles", "HoverEffect",
                "VFX", "BeamGroundGlow", "BeamTopGlow", "BeamGroundAnchor", "CaptureSocket",
                "CaptureFlashPoint", "PS_EnergySparks", "PS_EnergyStreaks", "PS_EnergyOrbs"
            };
            Transform[] all = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < all.Length; i++)
            {
                for (int n = 0; n < disableNames.Length; n++)
                {
                    if (all[i].name == disableNames[n] ||
                        all[i].name.IndexOf(disableNames[n], System.StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        all[i].gameObject.SetActive(false);
                        break;
                    }
                }
            }
        }

        public static void ApplyLayerRecursive(Transform root, int layer)
        {
            if (root == null || layer < 0)
            {
                return;
            }

            root.gameObject.layer = layer;
            for (int i = 0; i < root.childCount; i++)
            {
                ApplyLayerRecursive(root.GetChild(i), layer);
            }
        }
    }
}
