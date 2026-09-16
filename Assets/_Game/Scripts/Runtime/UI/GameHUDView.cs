using System;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AlienDefense.UI
{
    /// <summary>Displays resource, base health, and current game speed; forwards Pause/Speed button clicks. No business logic.</summary>
    public sealed class GameHUDView : MonoBehaviour
    {
        private const float CargoFullLeaveGraceSeconds = 0.4f;

        [SerializeField]
        private TMP_Text _resourceText;

        [SerializeField]
        [Tooltip("Optional. Shows EnergyWalletService.CurrentEnergy - the separate 'energy ball' currency only " +
            "gained by tractor-beaming an EnergyPickup into the UFO (see EnergyWalletService's own doc comment " +
            "for why it's kept apart from _resourceText/EconomyService, the tower build currency).")]
        private TMP_Text _energyText;

        [SerializeField]
        [Tooltip("Optional. Red slash overlay on the energy badge while cargo is full.")]
        private Image _energyForbiddenIcon;

        [SerializeField]
        [Tooltip("Optional. Radial fill over the energy badge ring - how full the cargo is (CurrentEnergy / MaxEnergy).")]
        private Image _energyFillImage;

        [SerializeField, Min(0f)]
        [Tooltip("Seconds the ring takes to slide to a new level. 0 snaps.")]
        private float _energyFillDuration = 0.25f;

        [SerializeField]
        [Tooltip("Optional. Top toast using cargofull.png — stays while beam is over energy with full cargo.")]
        private CanvasGroup _cargoFullBanner;

        [SerializeField]
        private TMP_Text _baseHealthText;

        [SerializeField]
        [Tooltip("Optional.")]
        private Image _baseHealthFillImage;

        [SerializeField]
        private TMP_Text _speedText;

        [SerializeField]
        private Button _speedButton;

        [SerializeField]
        private Button _pauseButton;

        public event Action SpeedButtonClicked;
        public event Action PauseButtonClicked;

        private Tween _bannerTween;
        private Tween _energyFillTween;
        private bool _cargoFullVisual;
        private bool _bannerVisible;
        private bool _bannerHiding;
        private float _lastCargoFullPresenceTime = -999f;
        private Vector3 _bannerBaseScale = Vector3.one;

        private void Awake()
        {
            if (_speedButton != null)
            {
                _speedButton.onClick.AddListener(HandleSpeedClicked);
            }

            if (_pauseButton != null)
            {
                _pauseButton.onClick.AddListener(HandlePauseClicked);
            }

            if (_cargoFullBanner != null)
            {
                _bannerBaseScale = _cargoFullBanner.transform.localScale;
                if (_bannerBaseScale.sqrMagnitude < 0.0001f)
                {
                    _bannerBaseScale = Vector3.one;
                }
            }

            HideCargoFullBannerImmediate();
            SetEnergyCargoFull(false);
            SetEnergyFill(0f, instant: true);
        }

        private void Update()
        {
            if (!_bannerVisible || _bannerHiding || _cargoFullBanner == null)
            {
                return;
            }

            if (Time.unscaledTime - _lastCargoFullPresenceTime > CargoFullLeaveGraceSeconds)
            {
                PlayCargoFullBannerHide();
            }
        }

        public void SetResource(int amount)
        {
            if (_resourceText != null)
            {
                _resourceText.text = amount.ToString();
            }
        }

        /// <summary>Shows the current Energy amount (e.g. "100") on the label; how close that is to capacity is
        /// shown by the badge ring filling up, and by the forbidden overlay once it is full.</summary>
        public void SetEnergy(int current, int max)
        {
            if (_energyText != null)
            {
                _energyText.text = current.ToString();
            }

            SetEnergyFill(max > 0 ? current / (float)max : 0f);

            bool full = max > 0 && current >= max;
            SetEnergyCargoFull(full);
        }

        /// <summary>Slides the badge ring to CurrentEnergy / MaxEnergy. The ring is the readable part of the
        /// badge at a glance - the number tells the exact count, the arc tells how close cargo is to full.</summary>
        private void SetEnergyFill(float normalized, bool instant = false)
        {
            if (_energyFillImage == null)
            {
                return;
            }

            _energyFillTween?.Kill();
            _energyFillTween = null;

            float target = Mathf.Clamp01(normalized);
            if (instant || _energyFillDuration <= 0f || !isActiveAndEnabled)
            {
                _energyFillImage.fillAmount = target;
                return;
            }

            float from = _energyFillImage.fillAmount;
            _energyFillTween = DOTween.To(() => from, value =>
                {
                    from = value;
                    _energyFillImage.fillAmount = value;
                }, target, _energyFillDuration)
                .SetEase(Ease.OutQuad)
                .SetUpdate(true) // keeps moving while the game is paused
                .SetLink(gameObject);
        }

        /// <summary>Shows/hides the forbidden overlay while cargo is at capacity. The icon sits still - the
        /// refused energy balls rocking under the beam and the banner already carry the "cargo full" motion.</summary>
        public void SetEnergyCargoFull(bool isFull)
        {
            _cargoFullVisual = isFull;

            if (_energyForbiddenIcon == null)
            {
                return;
            }

            _energyForbiddenIcon.transform.localScale = Vector3.one;
            _energyForbiddenIcon.gameObject.SetActive(isFull);
        }

        /// <summary>Presence heartbeat while the beam is over Idle energy with a full cargo.
        /// Appears once, stays put while presence keeps arriving, hides after leave grace.</summary>
        public void NotifyCargoFullPresence()
        {
            if (!_cargoFullVisual)
            {
                SetEnergyCargoFull(true);
            }

            _lastCargoFullPresenceTime = Time.unscaledTime;

            if (!_bannerVisible || _bannerHiding)
            {
                PlayCargoFullBannerAppear();
            }
        }

        /// <summary>Legacy entry used by presenter refuse path — maps to presence (no auto-dismiss toast).</summary>
        public void NotifyCargoFullRefuse()
        {
            NotifyCargoFullPresence();
        }

        /// <summary>Text shows only the current value (e.g. "100", not "100/100") - the ring's own fillAmount
        /// is what conveys "out of max" visually, so the max number would just be redundant clutter here.</summary>
        public void SetBaseHealth(int current, int max)
        {
            if (_baseHealthText != null)
            {
                _baseHealthText.text = current.ToString();
            }

            if (_baseHealthFillImage != null)
            {
                _baseHealthFillImage.fillAmount = max > 0 ? (float)current / max : 0f;
            }
        }

        public void SetSpeed(int speed)
        {
            if (_speedText != null)
            {
                _speedText.text = "x" + speed;
            }
        }

        private void PlayCargoFullBannerAppear()
        {
            if (_cargoFullBanner == null)
            {
                return;
            }

            _bannerTween?.Kill();
            _bannerHiding = false;
            _bannerVisible = true;

            Transform bannerTransform = _cargoFullBanner.transform;
            _cargoFullBanner.gameObject.SetActive(true);
            _cargoFullBanner.alpha = 0f;
            bannerTransform.localScale = _bannerBaseScale * 0.85f;

            _bannerTween = DOTween.Sequence()
                .SetUpdate(true)
                .SetTarget(_cargoFullBanner)
                .Append(DOTween.To(() => _cargoFullBanner.alpha, a => _cargoFullBanner.alpha = a, 1f, 0.2f))
                .Join(bannerTransform.DOScale(_bannerBaseScale, 0.2f).SetEase(Ease.OutBack));
        }

        private void PlayCargoFullBannerHide()
        {
            if (_cargoFullBanner == null || _bannerHiding)
            {
                return;
            }

            _bannerHiding = true;
            _bannerTween?.Kill();

            Transform bannerTransform = _cargoFullBanner.transform;
            _bannerTween = DOTween.Sequence()
                .SetUpdate(true)
                .SetTarget(_cargoFullBanner)
                .Append(DOTween.To(() => _cargoFullBanner.alpha, a => _cargoFullBanner.alpha = a, 0f, 0.22f))
                .Join(bannerTransform.DOScale(_bannerBaseScale * 0.9f, 0.22f).SetEase(Ease.InQuad))
                .OnComplete(() =>
                {
                    _bannerVisible = false;
                    _bannerHiding = false;
                    if (_cargoFullBanner != null)
                    {
                        _cargoFullBanner.gameObject.SetActive(false);
                        _cargoFullBanner.transform.localScale = _bannerBaseScale;
                    }
                });
        }

        private void HideCargoFullBannerImmediate()
        {
            _bannerTween?.Kill();
            _bannerTween = null;
            _bannerVisible = false;
            _bannerHiding = false;
            if (_cargoFullBanner != null)
            {
                _cargoFullBanner.alpha = 0f;
                _cargoFullBanner.transform.localScale = _bannerBaseScale;
                _cargoFullBanner.gameObject.SetActive(false);
            }
        }

        private void HandleSpeedClicked()
        {
            SpeedButtonClicked?.Invoke();
        }

        private void HandlePauseClicked()
        {
            PauseButtonClicked?.Invoke();
        }

        private void OnDestroy()
        {
            if (_speedButton != null)
            {
                _speedButton.onClick.RemoveListener(HandleSpeedClicked);
            }

            if (_pauseButton != null)
            {
                _pauseButton.onClick.RemoveListener(HandlePauseClicked);
            }

            _bannerTween?.Kill();
            _energyFillTween?.Kill();
        }
    }
}
