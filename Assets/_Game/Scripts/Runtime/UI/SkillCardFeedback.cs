using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace AlienDefense.UI
{
    /// <summary>The "you picked this one" visuals for a single skill card: dim/undim, a punch scale, a border
    /// glow, a gold burst behind the star row and a short sparkle fan. Pure decoration - it is handed a result
    /// that has already been committed to PlayerSkillService and never decides anything about the skill itself
    /// (see SkillChoicePresenter.HandleCardClicked, which only calls in after the rank actually went up).
    ///
    /// Everything runs on unscaled time: the popup is shown with GameSpeed paused, so a scaled tween would sit
    /// at 0 and the whole sequence would never play.
    ///
    /// The sparkles are plain Images placed in the scene up front rather than a ParticleSystem, because this
    /// canvas is Screen Space - Overlay and a world-space particle renderer draws underneath it. They are also
    /// never instantiated or destroyed - the fan is a fixed set of children that this component moves and fades.</summary>
    public sealed class SkillCardFeedback : MonoBehaviour
    {
        [Header("References")]
        [SerializeField]
        [Tooltip("CanvasGroup on the card itself, so dimming this card never drags the other two down with it.")]
        private CanvasGroup _canvasGroup;

        [SerializeField]
        [Tooltip("The card's own RectTransform - the thing that punch-scales.")]
        private RectTransform _cardTransform;

        [SerializeField]
        [Tooltip("Additive overlay hugging the card silhouette, sitting just above the card background and below " +
            "its contents. Rests at alpha 0.")]
        private Image _glowBorder;

        [SerializeField]
        [Tooltip("Soft radial glow behind the star row. Rests at alpha 0.")]
        private Image _starGlow;

        [SerializeField]
        [Tooltip("The sparkle fan around the star row. Rest at alpha 0 on top of the card; count is whatever is " +
            "wired here, so the fan can be thinned per-platform without touching code.")]
        private Image[] _sparkles;

        [SerializeField]
        [Tooltip("Dark panel covering the whole card, on top of its contents. Rests at alpha 0 and fades in to " +
            "dim this card. See Dimmed Alpha for why a card is darkened rather than just faded.")]
        private Image _dimOverlay;

        [Header("Dim")]
        [SerializeField, Range(0f, 1f)]
        [Tooltip("Alpha the two cards that were NOT picked fade to. Kept high on purpose: this popup sits over " +
            "live gameplay and the HUD, so fading a card far enough to read as 'dim' also makes the minimap and " +
            "the missile cooldown badge show straight through it. The darkening is done by Dim Overlay Alpha " +
            "instead, which cannot reveal anything behind the card.")]
        private float _dimmedAlpha = 0.85f;

        [SerializeField, Range(0f, 1f)]
        [Tooltip("How opaque the dark panel over an unpicked card gets. Together with Dimmed Alpha this lands " +
            "around 40% perceived brightness.")]
        private float _dimOverlayAlpha = 0.55f;

        [SerializeField, Min(0.01f)]
        private float _dimDuration = 0.2f;

        [Header("Punch")]
        [SerializeField, Range(1f, 1.3f)]
        private float _punchScale = 1.04f;

        [SerializeField, Min(0.01f)]
        private float _punchUpDuration = 0.12f;

        [SerializeField, Min(0.01f)]
        private float _punchDownDuration = 0.15f;

        [Header("Border Glow")]
        [SerializeField, Range(0f, 1f)]
        private float _glowPeakAlpha = 0.6f;

        [SerializeField, Min(0.01f)]
        [Tooltip("Time from alpha 0 up to the peak.")]
        private float _glowRiseDuration = 0.12f;

        [SerializeField]
        [Tooltip("Leave the picked card lit at its peak instead of letting the glow fade back out - the card " +
            "stays highlighted right up until the popup closes. Nothing lingers: ResetVisualState clears it the " +
            "next time the popup opens. Off restores the old 0 -> peak -> 0 flash.")]
        private bool _holdGlowWhileSelected = true;

        [Header("Star Glow")]
        [SerializeField, Min(0f)]
        [Tooltip("Delay after the pick before the gold burst fires, so the punch reads first.")]
        private float _starGlowDelay = 0.15f;

        [SerializeField, Min(0.01f)]
        private float _starGlowDuration = 0.5f;

        [SerializeField, Range(0f, 1f)]
        private float _starGlowPeakAlpha = 0.85f;

        [SerializeField]
        private Vector2 _starGlowScaleRange = new Vector2(0.7f, 1.3f);

        [Header("Sparkles")]
        [SerializeField, Min(0.01f)]
        private float _sparkleDuration = 0.5f;

        [SerializeField, Min(0f)]
        [Tooltip("How far a sparkle travels from the star row, in card-local units.")]
        private float _sparkleDistance = 78f;

        [SerializeField, Range(0f, 180f)]
        [Tooltip("Width of the upward fan the sparkles spread across. 180 = a full half-circle.")]
        private float _sparkleSpread = 150f;

        [SerializeField, Min(0f)]
        private float _sparkleStartScale = 0.3f;

        private Sequence _sequence;
        private Tweener _dimTween;
        private Tweener _dimOverlayTween;
        private Vector2 _sparkleHome;
        private bool _sparkleHomeCaptured;

        private void Awake()
        {
            CaptureSparkleHome();
        }

        /// <summary>The sparkles all start from wherever the first one was authored, which is the star row. Read
        /// once, because PlaySelected moves them away from it.</summary>
        private void CaptureSparkleHome()
        {
            if (_sparkleHomeCaptured || _sparkles == null || _sparkles.Length == 0 || _sparkles[0] == null)
            {
                return;
            }

            _sparkleHome = _sparkles[0].rectTransform.anchoredPosition;
            _sparkleHomeCaptured = true;
        }

        /// <summary>Back to a card that has never been picked: full alpha, no scale, every glow off. Called for
        /// all three cards each time the popup opens, so last level-up's dimmed card can't linger.</summary>
        public void ResetVisualState()
        {
            KillTweens();
            CaptureSparkleHome();

            if (_canvasGroup != null)
            {
                _canvasGroup.alpha = 1f;
                _canvasGroup.interactable = true;
                _canvasGroup.blocksRaycasts = true;
            }

            if (_cardTransform != null)
            {
                _cardTransform.localScale = Vector3.one;
            }

            SetImageAlpha(_glowBorder, 0f);
            SetImageAlpha(_dimOverlay, 0f);

            if (_starGlow != null)
            {
                SetImageAlpha(_starGlow, 0f);
                _starGlow.rectTransform.localScale = Vector3.one * _starGlowScaleRange.x;
            }

            if (_sparkles == null)
            {
                return;
            }

            for (int i = 0; i < _sparkles.Length; i++)
            {
                if (_sparkles[i] == null)
                {
                    continue;
                }

                SetImageAlpha(_sparkles[i], 0f);
                _sparkles[i].rectTransform.anchoredPosition = _sparkleHome;
                _sparkles[i].rectTransform.localScale = Vector3.one * _sparkleStartScale;
            }
        }

        /// <summary>Fades this card down (or back up). Used on the two cards the player did not pick.</summary>
        public void SetDimmed(bool dimmed)
        {
            _dimTween?.Kill();
            _dimOverlayTween?.Kill();

            if (_canvasGroup != null)
            {
                float target = dimmed ? _dimmedAlpha : 1f;
                _dimTween = DOTween.To(() => _canvasGroup.alpha, a => _canvasGroup.alpha = a, target, _dimDuration)
                    .SetEase(Ease.OutQuad)
                    .SetUpdate(true)
                    .SetLink(gameObject);
            }

            if (_dimOverlay != null)
            {
                _dimOverlayTween = FadeImage(_dimOverlay, dimmed ? _dimOverlayAlpha : 0f, _dimDuration)
                    .SetEase(Ease.OutQuad)
                    .SetUpdate(true)
                    .SetLink(gameObject);
            }
        }

        /// <summary>Plays the full "picked" sequence and returns how long it runs, so the caller knows when it is
        /// safe to close the popup. Safe to call on a card with nothing wired - it just returns a duration.</summary>
        public float PlaySelected()
        {
            KillTweens();
            CaptureSparkleHome();

            _sequence = DOTween.Sequence().SetUpdate(true).SetLink(gameObject);

            if (_cardTransform != null)
            {
                _cardTransform.localScale = Vector3.one;
                _sequence.Append(_cardTransform.DOScale(_punchScale, _punchUpDuration).SetEase(Ease.OutQuad));
                _sequence.Append(_cardTransform.DOScale(1f, _punchDownDuration).SetEase(Ease.OutBack));
            }

            if (_glowBorder != null)
            {
                // Rises alongside the punch rather than after it, so the card lights up as it pops.
                SetImageAlpha(_glowBorder, 0f);
                _sequence.Insert(0f, FadeImage(_glowBorder, _glowPeakAlpha, _glowRiseDuration).SetEase(Ease.OutQuad));

                if (!_holdGlowWhileSelected)
                {
                    _sequence.Insert(_glowRiseDuration,
                        FadeImage(_glowBorder, 0f, _glowRiseDuration * 2f).SetEase(Ease.InQuad));
                }
            }

            if (_starGlow != null)
            {
                RectTransform glowRect = _starGlow.rectTransform;
                SetImageAlpha(_starGlow, 0f);
                glowRect.localScale = Vector3.one * _starGlowScaleRange.x;

                float half = _starGlowDuration * 0.5f;
                _sequence.Insert(_starGlowDelay, FadeImage(_starGlow, _starGlowPeakAlpha, half).SetEase(Ease.OutQuad));
                _sequence.Insert(_starGlowDelay + half, FadeImage(_starGlow, 0f, half).SetEase(Ease.InQuad));
                _sequence.Insert(_starGlowDelay,
                    glowRect.DOScale(_starGlowScaleRange.y, _starGlowDuration).SetEase(Ease.OutQuad));
            }

            InsertSparkles();

            return _sequence.Duration();
        }

        /// <summary>Fans the sparkles out from the star row. Angles are derived from the index rather than drawn
        /// at random, so the burst is identical every time and nothing allocates a Random.</summary>
        private void InsertSparkles()
        {
            if (_sparkles == null || _sparkles.Length == 0)
            {
                return;
            }

            for (int i = 0; i < _sparkles.Length; i++)
            {
                Image sparkle = _sparkles[i];
                if (sparkle == null)
                {
                    continue;
                }

                RectTransform rect = sparkle.rectTransform;
                rect.anchoredPosition = _sparkleHome;
                rect.localScale = Vector3.one * _sparkleStartScale;
                SetImageAlpha(sparkle, 0f);

                // Spread the fan across _sparkleSpread degrees centred on straight up (90 degrees).
                float t = _sparkles.Length == 1 ? 0.5f : i / (float)(_sparkles.Length - 1);
                float angle = (90f - _sparkleSpread * 0.5f + _sparkleSpread * t) * Mathf.Deg2Rad;

                // Alternate the reach so the ring does not look like a drawn arc.
                float reach = _sparkleDistance * ((i & 1) == 0 ? 1f : 0.68f);
                Vector2 target = _sparkleHome + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * reach;

                float fadeIn = _sparkleDuration * 0.25f;

                _sequence.Insert(_starGlowDelay, MoveAnchored(rect, target, _sparkleDuration).SetEase(Ease.OutCubic));
                _sequence.Insert(_starGlowDelay, FadeImage(sparkle, 1f, fadeIn).SetEase(Ease.OutQuad));
                _sequence.Insert(_starGlowDelay + fadeIn,
                    FadeImage(sparkle, 0f, _sparkleDuration - fadeIn).SetEase(Ease.InQuad));
                _sequence.Insert(_starGlowDelay,
                    rect.DOScale(1f, _sparkleDuration * 0.4f).SetEase(Ease.OutBack));
            }
        }

        /// <summary>Image.DOFade and RectTransform.DOAnchorPos live in DOTweenModuleUI.cs, which sits in
        /// Assets/Plugins and so compiles into Assembly-CSharp - an assembly this asmdef cannot reference. The
        /// generic DOTween.To in the core DLL is the supported way in, and is what the rest of this UI already
        /// uses (see SkillChoiceView's fade tweens).</summary>
        private static Tweener FadeImage(Image image, float targetAlpha, float duration)
        {
            return DOTween.To(() => image.color.a, a => SetImageAlpha(image, a), targetAlpha, duration);
        }

        private static Tweener MoveAnchored(RectTransform rect, Vector2 target, float duration)
        {
            return DOTween.To(() => rect.anchoredPosition, p => rect.anchoredPosition = p, target, duration);
        }

        private static void SetImageAlpha(Image image, float alpha)
        {
            if (image == null)
            {
                return;
            }

            Color color = image.color;
            color.a = alpha;
            image.color = color;
        }

        private void KillTweens()
        {
            _sequence?.Kill();
            _sequence = null;
            _dimTween?.Kill();
            _dimTween = null;
            _dimOverlayTween?.Kill();
            _dimOverlayTween = null;
        }

        private void OnDisable()
        {
            // A popup closed mid-sequence must not leave a half-faded card behind for next time.
            KillTweens();
        }

        private void OnDestroy()
        {
            KillTweens();
        }
    }
}
