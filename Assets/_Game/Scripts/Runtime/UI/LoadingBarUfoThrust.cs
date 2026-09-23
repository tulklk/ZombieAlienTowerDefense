using UnityEngine;
using UnityEngine.UI;

namespace AlienDefense.UI
{
    /// <summary>Engine-thrust decoration for the Bootstrap loading bar: a soft round glow under the UFO handle and
    /// a stretched streak trailing behind it, both breathing slightly so the bar reads as "being pushed along".
    ///
    /// Deliberately a follower, not a driver: LoadingOverlayView still owns the handle's position (it is the thing
    /// that knows the real load progress). This component only reads where the handle ended up and places the two
    /// glow images around it, so the progress logic stays in one place and nothing here can desync the bar.
    ///
    /// Two stretched Images rather than a particle system or a custom shader - on a loading screen the GPU is
    /// already busy decompressing and uploading the next scene, and two extra UI quads batch into the existing
    /// canvas for free.</summary>
    [ExecuteAlways]
    public sealed class LoadingBarUfoThrust : MonoBehaviour
    {
        [Header("References")]
        [SerializeField]
        [Tooltip("The UFO icon that LoadingOverlayView slides along the bar (the 'Handle' object).")]
        private RectTransform _ufoIcon;

        [SerializeField]
        [Tooltip("Soft round glow sitting under/behind the UFO. Must be a sibling BEFORE the UFO so it draws behind.")]
        private RectTransform _glowBack;

        [SerializeField]
        [Tooltip("Horizontally stretched streak trailing behind the UFO. Also a sibling before the UFO.")]
        private RectTransform _trail;

        [Header("Placement (local units, relative to the UFO)")]
        [SerializeField]
        private Vector2 _glowOffset = new Vector2(-4f, 0f);

        [SerializeField]
        private float _glowSize = 96f;

        [SerializeField]
        [Tooltip("How far behind the UFO's centre the streak starts. The streak's pivot is its right edge, so it " +
            "grows to the left from here.")]
        private float _trailOffsetX = 14f;

        [SerializeField]
        private float _trailLength = 150f;

        [SerializeField]
        private float _trailHeight = 40f;

        [Header("Colour")]
        [SerializeField]
        private Color _glowColor = new Color(0.45f, 0.92f, 1f, 1f);

        [SerializeField]
        private Color _trailColor = new Color(0.30f, 0.80f, 1f, 1f);

        [SerializeField, Range(0f, 1f)]
        private float _glowIntensity = 0.75f;

        [SerializeField, Range(0f, 1f)]
        private float _trailIntensity = 0.5f;

        [Header("Motion")]
        [SerializeField, Range(0f, 20f)]
        [Tooltip("Flicker rate of the thruster. 6-9 reads as an engine; below 3 looks like a slow breath.")]
        private float _pulseSpeed = 7f;

        [SerializeField, Range(0f, 1f)]
        private float _pulseAmount = 0.28f;

        [SerializeField, Range(0f, 1f)]
        [Tooltip("How much the streak stretches and shrinks with the pulse.")]
        private float _trailStretchAmount = 0.18f;

        [SerializeField, Range(0f, 12f)]
        [Tooltip("Vertical bob of the UFO itself. Keep it small - the bar is only a few dozen pixels tall.")]
        private float _bobAmplitude = 2.5f;

        [SerializeField, Range(0f, 10f)]
        private float _bobSpeed = 3.2f;

        [SerializeField, Range(0f, 12f)]
        [Tooltip("How quickly the thrust catches up with the UFO. Lower = the streak lags behind, which sells the " +
            "sense of being dragged along. 0 = rigid.")]
        private float _followLag = 6f;

        private Image _glowImage;
        private Image _trailImage;
        private float _bobBaseY;
        private bool _bobBaseCaptured;
        private float _smoothedX;
        private bool _smoothedXPrimed;

        private void OnEnable()
        {
            CacheImages();
            _bobBaseCaptured = false;
            _smoothedXPrimed = false;
            ApplyStaticSetup();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            CacheImages();
            ApplyStaticSetup();
        }
#endif

        private void CacheImages()
        {
            if (_glowBack != null && _glowImage == null)
            {
                _glowImage = _glowBack.GetComponent<Image>();
            }

            if (_trail != null && _trailImage == null)
            {
                _trailImage = _trail.GetComponent<Image>();
            }
        }

        /// <summary>Sizes and colours that never change per frame - set once so LateUpdate only moves things.</summary>
        private void ApplyStaticSetup()
        {
            if (_glowBack != null)
            {
                _glowBack.sizeDelta = new Vector2(_glowSize, _glowSize);
            }

            if (_trail != null)
            {
                _trail.sizeDelta = new Vector2(_trailLength, _trailHeight);
            }
        }

        private void LateUpdate()
        {
            if (_ufoIcon == null)
            {
                return;
            }

            // LoadingOverlayView writes the handle's X every time progress changes and leaves Y alone, so the bob
            // can live on Y without the two fighting each other.
            Vector2 iconPosition = _ufoIcon.anchoredPosition;

            if (!_bobBaseCaptured)
            {
                _bobBaseY = iconPosition.y;
                _bobBaseCaptured = true;
            }

            // Unscaled: a loading screen may well be running at timeScale 0.
            float time = Time.unscaledTime;
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                // Time.unscaledTime does not advance while the editor is not playing, so the preview would freeze.
                time = (float)UnityEditor.EditorApplication.timeSinceStartup;
            }
#endif
            float wave = Mathf.Sin(time * _pulseSpeed);

            if (_bobAmplitude > 0f)
            {
                iconPosition.y = _bobBaseY + Mathf.Sin(time * _bobSpeed) * _bobAmplitude;
                _ufoIcon.anchoredPosition = iconPosition;
            }

            // The thrust trails slightly behind the icon rather than being welded to it.
            // Outside play mode Time.unscaledDeltaTime is 0, so an exponential follow would never advance and the
            // thrust would sit frozen at the left edge while the icon moved away. Snap in that case.
            float deltaTime = Application.isPlaying ? Time.unscaledDeltaTime : 0f;

            if (!_smoothedXPrimed || _followLag <= 0f || deltaTime <= 0f)
            {
                _smoothedX = iconPosition.x;
                _smoothedXPrimed = true;
            }
            else
            {
                _smoothedX = Mathf.Lerp(_smoothedX, iconPosition.x, 1f - Mathf.Exp(-_followLag * deltaTime));
            }

            float pulse = 1f + wave * _pulseAmount;
            float stretch = 1f + wave * _trailStretchAmount;

            if (_glowBack != null)
            {
                _glowBack.anchoredPosition = new Vector2(_smoothedX + _glowOffset.x, iconPosition.y + _glowOffset.y);
                if (_glowImage != null)
                {
                    Color c = _glowColor;
                    c.a = Mathf.Clamp01(_glowIntensity * pulse);
                    _glowImage.color = c;
                }
            }

            if (_trail != null)
            {
                _trail.anchoredPosition = new Vector2(_smoothedX - _trailOffsetX, iconPosition.y);
                _trail.sizeDelta = new Vector2(_trailLength * stretch, _trailHeight);

                if (_trailImage != null)
                {
                    Color c = _trailColor;
                    c.a = Mathf.Clamp01(_trailIntensity * pulse);
                    _trailImage.color = c;
                }
            }
        }

    }
}
