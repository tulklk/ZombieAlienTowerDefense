using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AlienDefense.UI
{
    /// <summary>The "Base under attack!" text and the red pulsing screen-edge vignette. Pure presentation: told
    /// when to show / hide / flash by BaseThreatWarningPresenter. Both graphics have raycasts off, so the warning
    /// never blocks the joystick or the HUD buttons. Every tween is stored, killed before a new one starts, and linked
    /// to this GameObject; they run on scaled time, so pausing the game freezes the warning like the rest of combat.</summary>
    public sealed class BaseThreatWarningView : MonoBehaviour
    {
        [SerializeField]
        private TMP_Text _text;

        [SerializeField]
        [Tooltip("Full-screen Image using the AlienDefense/UI/BaseWarningVignette material. Its colour alpha is the pulse.")]
        private Image _vignette;

        [Header("Text")]
        [SerializeField, Min(0.05f)]
        private float _textInDuration = 0.25f;

        [SerializeField, Min(0.05f)]
        private float _textOutDuration = 0.2f;

        [SerializeField, Range(1f, 1.2f)]
        private float _textPulseScale = 1.04f;

        [Header("Vignette")]
        [SerializeField, Range(0f, 1f)]
        private float _pulseLow = 0.12f;

        [SerializeField, Range(0f, 1f)]
        private float _pulseHigh = 0.32f;

        [SerializeField, Min(0.1f)]
        [Tooltip("Seconds for one low -> high -> low pulse.")]
        private float _pulsePeriod = 0.6f;

        [SerializeField, Range(0f, 1f)]
        [Tooltip("Peak of the extra flash when the base actually takes damage.")]
        private float _damageFlash = 0.45f;

        [SerializeField, Min(0.05f)]
        private float _vignetteOutDuration = 0.3f;

        private Tween _textTween;
        private Tween _textPulse;
        private Tween _vignetteTween;
        private bool _showing;

        private void Awake()
        {
            HideImmediate();
        }

        public void Show()
        {
            if (_showing)
            {
                return;
            }

            _showing = true;
            KillAll();

            if (_text != null)
            {
                _text.gameObject.SetActive(true);
                Transform t = _text.transform;
                t.localScale = Vector3.one * 0.85f;
                SetTextAlpha(0f);
                _textTween = DOTween.Sequence()
                    .Append(t.DOScale(1.08f, _textInDuration * 0.65f).SetEase(Ease.OutQuad))
                    .Join(DOTween.To(() => _text.alpha, SetTextAlpha, 1f, _textInDuration * 0.65f))
                    .Append(t.DOScale(1f, _textInDuration * 0.35f).SetEase(Ease.OutSine))
                    .OnComplete(StartTextPulse)
                    .SetLink(gameObject);
            }

            if (_vignette != null)
            {
                _vignette.gameObject.SetActive(true);
                _vignetteTween = DOTween.Sequence()
                    .Append(DOTween.To(GetVignetteAlpha, SetVignetteAlpha, _pulseHigh, 0.2f).SetEase(Ease.OutQuad))
                    .OnComplete(StartVignettePulse)
                    .SetLink(gameObject);
            }
        }

        public void Hide()
        {
            if (!_showing)
            {
                return;
            }

            _showing = false;
            KillAll();

            if (_text != null && _text.gameObject.activeSelf)
            {
                _textTween = DOTween.To(() => _text.alpha, SetTextAlpha, 0f, _textOutDuration)
                    .OnComplete(() => _text.gameObject.SetActive(false))
                    .SetLink(gameObject);
            }

            if (_vignette != null && _vignette.gameObject.activeSelf)
            {
                _vignetteTween = DOTween.To(GetVignetteAlpha, SetVignetteAlpha, 0f, _vignetteOutDuration)
                    .OnComplete(() => _vignette.gameObject.SetActive(false))
                    .SetLink(gameObject);
            }
        }

        /// <summary>One stronger red flash (the base was just hit), then back to the normal pulse.</summary>
        public void FlashDamage()
        {
            if (!_showing || _vignette == null)
            {
                return;
            }

            _vignetteTween?.Kill();
            _vignetteTween = DOTween.Sequence()
                .Append(DOTween.To(GetVignetteAlpha, SetVignetteAlpha, _damageFlash, 0.08f).SetEase(Ease.OutQuad))
                .Append(DOTween.To(GetVignetteAlpha, SetVignetteAlpha, _pulseLow, 0.25f).SetEase(Ease.InQuad))
                .OnComplete(StartVignettePulse)
                .SetLink(gameObject);
        }

        public void HideImmediate()
        {
            _showing = false;
            KillAll();
            if (_text != null)
            {
                SetTextAlpha(0f);
                _text.transform.localScale = Vector3.one;
                _text.gameObject.SetActive(false);
            }

            if (_vignette != null)
            {
                SetVignetteAlpha(0f);
                _vignette.gameObject.SetActive(false);
            }
        }

        private void StartTextPulse()
        {
            _textPulse?.Kill();
            _textPulse = _text.transform.DOScale(_textPulseScale, _pulsePeriod * 0.5f)
                .SetEase(Ease.InOutSine)
                .SetLoops(-1, LoopType.Yoyo)
                .SetLink(gameObject);
        }

        private void StartVignettePulse()
        {
            _vignetteTween?.Kill();
            SetVignetteAlpha(_pulseHigh);
            _vignetteTween = DOTween.To(GetVignetteAlpha, SetVignetteAlpha, _pulseLow, _pulsePeriod * 0.5f)
                .SetEase(Ease.InOutSine)
                .SetLoops(-1, LoopType.Yoyo)
                .SetLink(gameObject);
        }

        private void KillAll()
        {
            _textTween?.Kill();
            _textPulse?.Kill();
            _vignetteTween?.Kill();
            _textTween = null;
            _textPulse = null;
            _vignetteTween = null;
        }

        private void SetTextAlpha(float alpha)
        {
            _text.alpha = alpha;
        }

        private float GetVignetteAlpha()
        {
            return _vignette.color.a;
        }

        private void SetVignetteAlpha(float alpha)
        {
            Color c = _vignette.color;
            c.a = alpha;
            _vignette.color = c;
        }

        private void OnDisable()
        {
            KillAll();
        }
    }
}
