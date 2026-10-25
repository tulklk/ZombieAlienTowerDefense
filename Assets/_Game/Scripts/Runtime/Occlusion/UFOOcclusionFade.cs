using System.Collections.Generic;
using AlienDefense.Player;
using UnityEngine;

namespace AlienDefense.Occlusion
{
    /// <summary>Fades the player UFO's BODY while scenery hides it from the camera (PlayerOcclusionController decides
    /// when, via SetOccluded). Only the renderers under _visualRoot fade - never the thruster VFX or the drop shadow -
    /// and nothing but their materials. The tractor beam's visuals are not faded but hidden for as long as the UFO is
    /// (OcclusionSettings.HideTractorBeam, see SetBeamHidden); colliders/movement/beam logic are untouched.
    ///
    /// The UFO's materials are URP Lit Opaque, where alpha does nothing, so while faded each body renderer uses a copy
    /// with the UFOOcclusionFade shader (made once per renderer slot on the first fade, owned and destroyed by this
    /// component - the shared material assets are never edited) and _FadeAlpha / _FadeTint move through a
    /// MaterialPropertyBlock. At full opacity the original materials are put back, so outside occlusion the UFO
    /// renders exactly as before. Alpha moves with Mathf.MoveTowards each frame: 1 -> 0.38 over FadeInDuration and
    /// back over FadeOutDuration, no coroutines.</summary>
    [DisallowMultipleComponent]
    public sealed class UFOOcclusionFade : MonoBehaviour
    {
        private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int UseTextureAlphaId = Shader.PropertyToID("_UseTextureAlpha");
        private static readonly int FadeAlphaId = Shader.PropertyToID("_FadeAlpha");
        private static readonly int FadeTintId = Shader.PropertyToID("_FadeTint");

        [SerializeField]
        [Tooltip("The UFO's visible body (UFO_Player/ufo). Only renderers under it fade, minus beam/VFX/shadow.")]
        private Transform _visualRoot;

        [SerializeField]
        [Tooltip("Optional here: PlayerOcclusionController hands its settings over at start.")]
        private OcclusionSettings _settings;

        private Renderer[] _renderers = new Renderer[0];
        private Material[][] _originalMaterials;
        private Material[][] _fadeMaterials;
        private bool[] _hadPropertyBlock;
        private MaterialPropertyBlock _block;
        private readonly List<Material> _ownedMaterials = new List<Material>();

        private bool _occluded;
        private bool _swapped;
        private float _alpha = 1f;
        private Renderer[] _beamRenderers;
        private bool _beamHidden;

        public bool IsOccluded => _occluded;
        public float CurrentAlpha => _alpha;

        private void Awake()
        {
            _block = new MaterialPropertyBlock();
            CacheRenderers();
        }

        public void Configure(OcclusionSettings settings)
        {
            if (settings != null)
            {
                _settings = settings;
            }
        }

        /// <summary>Called by PlayerOcclusionController (already debounced there). The fade follows in Update.</summary>
        public void SetOccluded(bool occluded)
        {
            _occluded = occluded;
            SetBeamHidden(occluded && _settings != null && _settings.HideTractorBeam);
        }

        /// <summary>Tractor beam visuals (cone, ground glow/ring, particles) off while the UFO is faded, back on
        /// when it is not. Rendering only (Renderer.forceRenderingOff): the beam keeps pulling and capturing, its
        /// GameObjects stay active and UFOTractorBeamVisual's own SetActive / enabled state is never fought over.</summary>
        private void SetBeamHidden(bool hidden)
        {
            if (_beamHidden == hidden)
            {
                return;
            }

            if (_beamRenderers == null)
            {
                // Once, on the first hide - so anything the beam visual set up at Initialize is included.
                var beam = GetComponentInChildren<UFOTractorBeamController>(true);
                _beamRenderers = beam != null ? beam.GetComponentsInChildren<Renderer>(true) : new Renderer[0];
            }

            _beamHidden = hidden;
            for (int i = 0; i < _beamRenderers.Length; i++)
            {
                if (_beamRenderers[i] != null)
                {
                    _beamRenderers[i].forceRenderingOff = hidden;
                }
            }
        }

        private void CacheRenderers()
        {
            Transform root = _visualRoot != null ? _visualRoot : transform;
            var found = new List<Renderer>();
            root.GetComponentsInChildren(true, found);
            for (int i = found.Count - 1; i >= 0; i--)
            {
                if (IsExcluded(found[i]))
                {
                    found.RemoveAt(i);
                }
            }

            _renderers = found.ToArray();
            _originalMaterials = new Material[_renderers.Length][];
            _fadeMaterials = new Material[_renderers.Length][];
            _hadPropertyBlock = new bool[_renderers.Length];
            for (int i = 0; i < _renderers.Length; i++)
            {
                _originalMaterials[i] = _renderers[i].sharedMaterials; // copy of the array - all material slots
            }
        }

        /// <summary>Beam, rings, particles, thruster VFX and the drop shadow keep their full alpha.</summary>
        private bool IsExcluded(Renderer r)
        {
            if (r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer)
            {
                return true;
            }

            if (r.GetComponentInParent<UFOTractorBeamController>(true) != null)
            {
                return true;
            }

            for (Transform t = r.transform; t != null && t != transform; t = t.parent)
            {
                string n = t.name;
                if (n.Contains("Beam") || n.Contains("Thruster") || n.Contains("Takeoff") || n.Contains("VFX")
                    || n.Contains("Shadow") || n.Contains("Ring") || n.Contains("Indicator"))
                {
                    return true;
                }
            }

            return false;
        }

        private void Update()
        {
            if (_settings == null || _renderers.Length == 0)
            {
                return;
            }

            float hidden = Mathf.Clamp01(_settings.OccludedAlpha);
            float range = Mathf.Max(0.01f, 1f - hidden);
            float target = _occluded ? hidden : 1f;
            if (Mathf.Approximately(_alpha, target) && (_swapped || target >= 1f))
            {
                if (!_occluded && _swapped)
                {
                    Restore(); // fully visible again: back to the original materials
                }

                return;
            }

            float duration = _occluded ? _settings.FadeInDuration : _settings.FadeOutDuration;
            _alpha = Mathf.MoveTowards(_alpha, target, range / Mathf.Max(0.01f, duration) * Time.deltaTime);

            if (_alpha < 1f && !_swapped)
            {
                SwapToFadeMaterials();
            }

            if (_swapped)
            {
                ApplyFade((1f - _alpha) / range * _settings.TintStrength);
            }

            if (_alpha >= 1f && !_occluded)
            {
                Restore();
            }
        }

        private void SwapToFadeMaterials()
        {
            Shader shader = _settings.FadeShader;
            if (shader == null)
            {
                return;
            }

            for (int i = 0; i < _renderers.Length; i++)
            {
                Renderer r = _renderers[i];
                if (r == null)
                {
                    continue;
                }

                if (_fadeMaterials[i] == null)
                {
                    Material[] originals = _originalMaterials[i];
                    var copies = new Material[originals.Length]; // once per renderer, on the first fade
                    for (int m = 0; m < originals.Length; m++)
                    {
                        copies[m] = CreateFadeCopy(originals[m], shader);
                    }

                    _fadeMaterials[i] = copies;
                }

                _hadPropertyBlock[i] = r.HasPropertyBlock();
                r.sharedMaterials = _fadeMaterials[i];
            }

            _swapped = true;
        }

        private Material CreateFadeCopy(Material original, Shader shader)
        {
            var copy = new Material(shader) { name = (original != null ? original.name : "UFO") + " (UFO Fade)", hideFlags = HideFlags.DontSave };
            if (original != null)
            {
                string textureName = original.HasProperty("_BaseMap") ? "_BaseMap" : original.HasProperty("_MainTex") ? "_MainTex" : null;
                if (textureName != null && original.GetTexture(textureName) != null)
                {
                    copy.SetTexture(BaseMapId, original.GetTexture(textureName));
                    copy.SetTextureScale(BaseMapId, original.GetTextureScale(textureName));
                    copy.SetTextureOffset(BaseMapId, original.GetTextureOffset(textureName));
                }

                Color color = original.HasProperty("_BaseColor") ? original.GetColor("_BaseColor")
                    : original.HasProperty("_Color") ? original.GetColor("_Color") : Color.white;
                copy.SetColor(BaseColorId, color);
                bool transparentOriginal = original.HasProperty("_Surface") && original.GetFloat("_Surface") > 0.5f;
                copy.SetFloat(UseTextureAlphaId, transparentOriginal ? 1f : 0f);
            }

            _ownedMaterials.Add(copy);
            return copy;
        }

        private void ApplyFade(float tint)
        {
            for (int i = 0; i < _renderers.Length; i++)
            {
                Renderer r = _renderers[i];
                if (r == null)
                {
                    continue;
                }

                r.GetPropertyBlock(_block); // keep anything another system put in this renderer's block
                _block.SetFloat(FadeAlphaId, _alpha);
                _block.SetFloat(FadeTintId, tint);
                r.SetPropertyBlock(_block);
            }
        }

        private void Restore()
        {
            _alpha = 1f;
            if (!_swapped)
            {
                return;
            }

            for (int i = 0; i < _renderers.Length; i++)
            {
                Renderer r = _renderers[i];
                if (r == null)
                {
                    continue;
                }

                r.sharedMaterials = _originalMaterials[i];
                if (_hadPropertyBlock[i])
                {
                    r.GetPropertyBlock(_block);
                    _block.SetFloat(FadeAlphaId, 1f);
                    _block.SetFloat(FadeTintId, 0f);
                    r.SetPropertyBlock(_block);
                }
                else
                {
                    r.SetPropertyBlock(null);
                }
            }

            _swapped = false;
        }

        private void OnDisable()
        {
            _occluded = false;
            SetBeamHidden(false);
            Restore();
        }

        private void OnDestroy()
        {
            for (int i = 0; i < _ownedMaterials.Count; i++)
            {
                if (_ownedMaterials[i] != null)
                {
                    Destroy(_ownedMaterials[i]);
                }
            }

            _ownedMaterials.Clear();
        }
    }
}
