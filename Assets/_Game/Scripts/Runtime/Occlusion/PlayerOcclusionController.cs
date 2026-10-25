using System.Collections.Generic;
using AlienDefense.Player;
using UnityEngine;

namespace AlienDefense.Occlusion
{
    /// <summary>Lives on the gameplay camera and decides whether the player UFO is hidden by scenery; the UFO then
    /// fades (UFOOcclusionFade on UFO_Player). The scenery itself is NEVER modified.
    ///
    /// Every CheckInterval it follows the real line of sight - this camera's current position to the middle of the
    /// UFO's body (OcclusionTarget) - against every OcclusionObject marker: the UFO counts as hidden while ANY marker's
    /// box is entered by that line (swept by CastRadius) before it reaches the UFO, or the UFO is inside / touching
    /// one without it being behind the UFO. Several occluders at once are fine; the state only ends when none is left,
    /// and a short hysteresis (MinimumHiddenTime / RestoreDelay) bridges flying from one building to the next, so the
    /// UFO never flashes back to full opacity in between. Objects behind or beside the UFO, the ground, roads, water,
    /// VFX and anything without a marker never count.
    ///
    /// Collider-free on purpose: several gameplay systems query physics on every layer (tractor-beam immune scan,
    /// energy scatter, boss-intro camera), so collider proxies on scenery could change gameplay; the marker boxes give
    /// the same answer as a sphere-cast against them, allocation-free.</summary>
    [DisallowMultipleComponent]
    public sealed class PlayerOcclusionController : MonoBehaviour
    {
        private static readonly int TintId = Shader.PropertyToID("_UFOOcclusionTint");

        [SerializeField]
        private OcclusionSettings _settings;

        [SerializeField]
        [Tooltip("Middle of the UFO's body (UFO_Player/OcclusionTarget). If empty, found once at start.")]
        private Transform _target;

        [SerializeField]
        [Tooltip("The UFO's fade component (on UFO_Player). If empty, found once at start.")]
        private UFOOcclusionFade _ufoFade;

        [SerializeField]
        [Tooltip("Defaults to the Camera on this GameObject.")]
        private Camera _camera;

        [SerializeField]
        [Tooltip("Draws the camera -> UFO line (red = hidden, green = clear) and logs 'UFO Occluded' / 'UFO Visible'.")]
        private bool _debugOcclusion;

        private readonly List<OcclusionObject> _hits = new List<OcclusionObject>(16);
        private float _checkTimer;
        private bool _occluded;
        private float _occludedSince;
        private float _lastSeenHidden;

        public bool IsUfoOccluded => _occluded;

        private void Awake()
        {
            if (_camera == null)
            {
                _camera = GetComponent<Camera>();
            }
        }

        private void Start()
        {
            if (_target == null || _ufoFade == null)
            {
                var player = FindFirstObjectByType<PlayerController>(); // once, not per frame
                if (player != null)
                {
                    if (_ufoFade == null)
                    {
                        _ufoFade = player.GetComponent<UFOOcclusionFade>();
                    }

                    if (_target == null)
                    {
                        Transform found = player.transform.Find("OcclusionTarget");
                        _target = found != null ? found : player.transform;
                    }
                }
            }

            if (_settings == null || _settings.FadeShader == null || _ufoFade == null)
            {
                Debug.LogWarning("[Occlusion] Missing OcclusionSettings / fade shader / UFOOcclusionFade - UFO occlusion fade is off.", this);
                enabled = false;
                return;
            }

            _ufoFade.Configure(_settings);
            PushGlobals();
        }

        private void LateUpdate()
        {
            if (_target == null || _camera == null || _ufoFade == null)
            {
                return;
            }

            _checkTimer += Time.deltaTime;
            if (_checkTimer < _settings.CheckInterval)
            {
                return;
            }

            _checkTimer = 0f;
#if UNITY_EDITOR
            PushGlobals(); // edits to the settings asset show up live while playing
#endif
            float now = Time.time;
            Vector3 origin = _camera.transform.position;
            Vector3 target = _target.position;
            FindOccluders(OcclusionObject.Registered, origin, target, _settings.CastRadius, _settings.InsideCheckRadius, _hits);
            bool hiddenNow = _hits.Count > 0;

            if (hiddenNow)
            {
                _lastSeenHidden = now;
                if (!_occluded)
                {
                    _occluded = true;
                    _occludedSince = now;
                    _ufoFade.SetOccluded(true);
                    if (_debugOcclusion)
                    {
                        Debug.Log("[Occlusion] UFO Occluded by " + _hits[0].name + (_hits.Count > 1 ? " (+" + (_hits.Count - 1) + ")" : ""), _hits[0]);
                    }
                }
            }
            else if (_occluded && now - _lastSeenHidden > _settings.RestoreDelay && now - _occludedSince >= _settings.MinimumHiddenTime)
            {
                _occluded = false;
                _ufoFade.SetOccluded(false);
                if (_debugOcclusion)
                {
                    Debug.Log("[Occlusion] UFO Visible", this);
                }
            }

            if (_debugOcclusion)
            {
                Debug.DrawLine(origin, target, hiddenNow ? Color.red : Color.green, _settings.CheckInterval);
            }
        }

        /// <summary>Every candidate hiding <paramref name="target"/> from <paramref name="origin"/> (ALL of them, not
        /// just the nearest), into <paramref name="results"/> (cleared first, never allocated). The single detection
        /// path: the runtime check uses it, and so can editor tests.</summary>
        public static void FindOccluders(IReadOnlyList<OcclusionObject> candidates, Vector3 origin, Vector3 target,
            float castRadius, float insideRadius, List<OcclusionObject> results)
        {
            results.Clear();
            Vector3 toTarget = target - origin;
            float distance = toTarget.magnitude;
            if (distance < 0.01f)
            {
                return;
            }

            Vector3 direction = toTarget / distance;
            for (int i = 0; i < candidates.Count; i++)
            {
                OcclusionObject candidate = candidates[i];
                if (candidate == null)
                {
                    continue;
                }

                candidate.RefreshBoundsIfMoved();
                if (candidate.Occludes(origin, direction, distance, target, castRadius, insideRadius))
                {
                    results.Add(candidate);
                }
            }
        }

        private void PushGlobals()
        {
            if (_settings != null)
            {
                Shader.SetGlobalColor(TintId, _settings.TintColor);
            }
        }

        private void OnDisable()
        {
            _occluded = false;
            if (_ufoFade != null)
            {
                _ufoFade.SetOccluded(false);
            }
        }
    }
}
