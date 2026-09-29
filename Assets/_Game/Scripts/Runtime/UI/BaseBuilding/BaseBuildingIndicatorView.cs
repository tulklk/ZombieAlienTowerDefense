using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AlienDefense.UI.BaseBuilding
{
    /// <summary>The little floating widget over one building: level badge with its green upgrade arrow, the
    /// construction timer, and the tap-able bubble.
    ///
    /// Screen-space and positioned by BaseWorldPresenter from the building's world anchor, rather than a
    /// world-space canvas per building: one overlay canvas for all of them keeps the text a constant readable
    /// size at any camera distance, and avoids a canvas (and its rebuild cost) per building.
    ///
    /// Only one of timer / bubble is shown at a time, by the priority the design asks for: a running build first,
    /// then a collectable resource.</summary>
    public sealed class BaseBuildingIndicatorView : MonoBehaviour
    {
        [Header("Level badge")]
        [SerializeField]
        private GameObject _levelBadgeRoot;

        [SerializeField]
        private TMP_Text _levelText;

        [SerializeField]
        private GameObject _upgradeArrow;

        [Header("Timer")]
        [SerializeField]
        private GameObject _timerRoot;

        [SerializeField]
        [Tooltip("Filled Image, horizontal.")]
        private Image _timerFill;

        [SerializeField]
        private TMP_Text _timerText;

        [SerializeField]
        [Tooltip("Optional: faded in with a small pop when the construction bar appears.")]
        private CanvasGroup _timerGroup;

        [SerializeField]
        [Tooltip("Optional: the hammer icon, gently swinging while the build runs.")]
        private RectTransform _timerIcon;

        [Header("Bubble")]
        [SerializeField]
        private Button _bubbleButton;

        [SerializeField]
        private Image _bubbleIcon;

        [SerializeField]
        private TMP_Text _bubbleAmountText;

        private RectTransform _rect;
        private System.Action _bubbleClicked;
        private float _displayedFill;

        public RectTransform Rect => _rect != null ? _rect : (_rect = (RectTransform)transform);

        private void Awake()
        {
            _rect = (RectTransform)transform;
            if (_bubbleButton != null)
            {
                _bubbleButton.onClick.AddListener(HandleBubble);
            }
        }

        private void OnDestroy()
        {
            if (_bubbleButton != null)
            {
                _bubbleButton.onClick.RemoveListener(HandleBubble);
            }
        }

        public void BindLevel(int level, bool canUpgrade)
        {
            if (_levelBadgeRoot != null)
            {
                _levelBadgeRoot.SetActive(level > 0);
            }

            if (_levelText != null)
            {
                _levelText.text = level.ToString();
            }

            if (_upgradeArrow != null)
            {
                _upgradeArrow.SetActive(level > 0 && canUpgrade);
            }
        }

        /// <summary>progress01 &lt; 0 hides the timer.</summary>
        public void BindTimer(float progress01, string remainingText)
        {
            bool visible = progress01 >= 0f;
            if (_timerRoot != null && _timerRoot.activeSelf != visible)
            {
                _timerRoot.SetActive(visible);
                if (visible)
                {
                    // Start the bar where the build actually is (a build restored from the save is not at 0).
                    _displayedFill = Mathf.Clamp01(progress01);
                    PlayTimerAppear();
                }
                else
                {
                    StopTimerTweens();
                }
            }

            if (!visible)
            {
                _displayedFill = 0f;
                return;
            }

            // The timer text updates a few times a second; the bar is eased every frame in TickVisuals so it
            // glides instead of stepping.
            _targetFill = Mathf.Clamp01(progress01);
            if (_timerText != null)
            {
                _timerText.text = remainingText;
            }
        }

        private float _targetFill;

        public void BindBubble(Sprite icon, string amountText, System.Action clicked)
        {
            _bubbleClicked = clicked;
            bool visible = clicked != null;

            if (_bubbleButton != null)
            {
                _bubbleButton.gameObject.SetActive(visible);
            }

            if (_bubbleIcon != null)
            {
                _bubbleIcon.sprite = icon;
                _bubbleIcon.enabled = icon != null;
            }

            if (_bubbleAmountText != null)
            {
                _bubbleAmountText.gameObject.SetActive(visible && !string.IsNullOrEmpty(amountText));
                _bubbleAmountText.text = amountText;
            }
        }

        /// <summary>Called every frame by the presenter while the base is on screen. Only eases the bar - the
        /// expensive parts (text, state) are refreshed at a lower rate.</summary>
        public void TickVisuals(float unscaledDeltaTime)
        {
            if (_timerFill == null || _timerRoot == null || !_timerRoot.activeSelf)
            {
                return;
            }

            _displayedFill = Mathf.MoveTowards(_displayedFill, _targetFill, unscaledDeltaTime * 2f);
            _timerFill.fillAmount = _displayedFill;
        }

        private void HandleBubble()
        {
            _bubbleClicked?.Invoke();
        }

        private Tween _timerPop;
        private Tween _hammerSwing;

        /// <summary>Bar pops in (alpha 0 -> 1, scale 0.85 -> 1) and the hammer starts swinging. At most two tweens
        /// per visible bar, killed as soon as it hides - nothing runs for idle plots.</summary>
        private void PlayTimerAppear()
        {
            StopTimerTweens();
            Transform bar = _timerRoot.transform;
            bar.localScale = Vector3.one * 0.85f;
            if (_timerGroup != null)
            {
                _timerGroup.alpha = 0f;
            }

            Sequence pop = DOTween.Sequence().Append(bar.DOScale(1f, 0.25f).SetEase(Ease.OutBack));
            if (_timerGroup != null)
            {
                pop.Join(DOTween.To(() => _timerGroup.alpha, a => _timerGroup.alpha = a, 1f, 0.2f));
            }

            _timerPop = pop.SetUpdate(true).SetLink(gameObject);
            StartHammerSwing();
        }

        private void StartHammerSwing()
        {
            if (_timerIcon == null)
            {
                return;
            }

            _hammerSwing?.Kill();
            _timerIcon.localRotation = Quaternion.Euler(0f, 0f, 12f);
            _hammerSwing = _timerIcon.DOLocalRotate(new Vector3(0f, 0f, -22f), 0.32f)
                .SetEase(Ease.InOutSine)
                .SetLoops(-1, LoopType.Yoyo)
                .SetUpdate(true)
                .SetLink(gameObject);
        }

        private void StopTimerTweens()
        {
            _timerPop?.Kill(true);
            _hammerSwing?.Kill();
            _timerPop = null;
            _hammerSwing = null;
        }

        private void OnEnable()
        {
            // Back on screen with a build still running: resume the swing (OnDisable stopped it).
            if (_timerRoot != null && _timerRoot.activeSelf)
            {
                StartHammerSwing();
            }
        }

        private void OnDisable()
        {
            // The indicator hides whenever its building leaves the screen; nothing may keep animating off screen.
            StopTimerTweens();
            if (_timerRoot != null && _timerRoot.activeSelf)
            {
                _timerRoot.transform.localScale = Vector3.one;
                if (_timerGroup != null)
                {
                    _timerGroup.alpha = 1f;
                }
            }
        }
    }
}
