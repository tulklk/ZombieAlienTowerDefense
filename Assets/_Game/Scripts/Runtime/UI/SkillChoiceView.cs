using System;
using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AlienDefense.UI
{
    /// <summary>Immutable per-card data SkillChoicePresenter hands to SkillChoiceView.Show - never mutated,
    /// never read back by the presenter (index is round-tripped through CardClicked instead). Rank is the rank
    /// this card would GRANT if picked (current + 1), which is what the star row displays as filled.</summary>
    public readonly struct SkillCardData
    {
        public readonly string Name;
        public readonly string Description;
        public readonly int Rank;
        public readonly int MaxRank;
        public readonly Sprite Icon;

        public SkillCardData(string name, string description, int rank, int maxRank, Sprite icon)
        {
            Name = name;
            Description = description;
            Rank = rank;
            MaxRank = maxRank;
            Icon = icon;
        }
    }

    /// <summary>Pure display for the "pick 1 of 3" level-up popup - always exactly 3 fixed card slots (never a
    /// dynamic list; the offer size is fixed by spec), each with a name/description/icon and a star row, plus
    /// the "Ability choice" board along the top whose slots show one icon per skill the player has already
    /// learned (see SetAcquiredSkills) and a Refresh button that re-rolls the offer. No business logic: never
    /// picks the offer, never applies an upgrade, never decides which skills count as learned - see
    /// SkillChoicePresenter.</summary>
    public sealed class SkillChoiceView : MonoBehaviour
    {
        /// <summary>One card's 5 star Images, left to right. A nested class only because Unity can't serialize
        /// a 2D array - there is no behaviour here, just the per-card row the Inspector needs to hold.</summary>
        [Serializable]
        private sealed class StarRow
        {
            public Image[] Stars = new Image[5];
        }

        [SerializeField]
        [Tooltip("The whole popup root - shown/hidden as a unit.")]
        private GameObject _root;

        [Header("Cards")]
        [SerializeField]
        private TMP_Text[] _nameTexts = new TMP_Text[3];

        [SerializeField]
        private TMP_Text[] _descriptionTexts = new TMP_Text[3];

        [SerializeField]
        private Image[] _iconImages = new Image[3];

        [SerializeField]
        private Button[] _cardButtons = new Button[3];

        [Header("Stars")]
        [SerializeField]
        [Tooltip("One row of 5 star Images per card, in the same order as Card Buttons.")]
        private StarRow[] _cardStarRows = new StarRow[3];

        [SerializeField]
        private Sprite _filledStarSprite;

        [SerializeField]
        private Sprite _emptyStarSprite;

        [Header("Recommendation")]
        [SerializeField]
        [Tooltip("One marker per card, in the same order as Card Buttons: the frame plus the caution and " +
            "recommend badges. Exactly one is shown at a time - see Show's recommendedIndex.")]
        private GameObject[] _recommendMarkers = new GameObject[3];

        [Header("Selection Feedback")]
        [SerializeField]
        [Tooltip("One per card, same order as Card Buttons: the dim/glow/sparkle visuals played after a pick is " +
            "committed. Leave an entry empty to skip that card's feedback.")]
        private SkillCardFeedback[] _cardFeedbacks = new SkillCardFeedback[3];

        [SerializeField, Min(0f)]
        [Tooltip("How long the picked card stays highlighted after its animation finishes, before the popup is " +
            "allowed to close.")]
        private float _selectionHoldSeconds = 0.3f;

        [SerializeField, Min(0.01f)]
        [Tooltip("How long the star pop takes. Every filled star pops on the SAME timestamp - no stagger.")]
        private float _starPopDuration = 0.3f;

        [SerializeField, Min(0f)]
        [Tooltip("Delay before the stars pop, matched to the star glow burst so the flash and the stars land " +
            "together.")]
        private float _starPopDelay = 0.15f;

        [SerializeField, Range(0f, 1f)]
        private float _starPopStartScale = 0.4f;

        [SerializeField, Range(1f, 2f)]
        private float _starPopOvershoot = 1.2f;

        [Header("Ability Board")]
        [SerializeField]
        [Tooltip("The icon Image inside each slot of the 'Ability choice' board, left to right. Filled in order " +
            "with whatever SetAcquiredSkills is given; the remaining slots are left showing the empty socket art.")]
        private Image[] _boardSlotIcons = new Image[6];

        [SerializeField]
        [Tooltip("The 'maxed out' frame behind each board slot, same order as Board Slot Icons. Shown only for a " +
            "slot whose skill has reached its last rank.")]
        private GameObject[] _boardSlotBoosts = new GameObject[6];

        [Header("Refresh")]
        [SerializeField]
        [Tooltip("Optional. Re-rolls the 3 offered skills without closing the popup.")]
        private Button _refreshButton;

        [Header("Appear Animation")]
        [SerializeField]
        [Tooltip("Optional. Everything except the dimmer - scaled up from Appear Start Scale on Show. Leave " +
            "empty to skip the pop-in.")]
        private RectTransform _content;

        [SerializeField]
        [Tooltip("Optional. Faded in alongside the pop-in.")]
        private CanvasGroup _canvasGroup;

        [SerializeField, Min(0.01f)]
        private float _appearDuration = 0.28f;

        [SerializeField, Range(0.1f, 1f)]
        private float _appearStartScale = 0.82f;

        [Header("Star Highlight")]
        [SerializeField]
        [Tooltip("The washed-out end of the newly-earned star's breathing colour - it fades between this and " +
            "Star Pulse Bright Tint and back. Multiplied into the star sprite, so it dims rather than recolours.")]
        private Color _starPulseDimTint = new Color(0.55f, 0.55f, 0.55f, 1f);

        [SerializeField]
        [Tooltip("The strong end of the breathing colour - white leaves the star art untouched.")]
        private Color _starPulseBrightTint = Color.white;

        [SerializeField, Min(0.01f)]
        [Tooltip("Full dim->bright->dim cycles per second for the newly-earned star.")]
        private float _starPulseFrequency = 1.1f;

        private Tweener _appearScaleTween;
        private Tweener _appearFadeTween;
        private readonly Tweener[] _starPulseTweens = new Tweener[3];
        private Sequence _starPopSequence;

        /// <summary>True from the moment a committed pick starts animating until the popup closes. The presenter
        /// reads this to swallow repeat clicks and Refresh presses.</summary>
        public bool IsPlayingSelection { get; private set; }

        /// <summary>Fired with the clicked card's index (0-2) - SkillChoicePresenter maps this back to which
        /// SkillType it actually offered at that slot.</summary>
        public event Action<int> CardClicked;

        /// <summary>Fired when Refresh is pressed - the presenter re-rolls and calls Show again.</summary>
        public event Action RefreshClicked;

        private void Awake()
        {
            for (int i = 0; i < _cardButtons.Length; i++)
            {
                if (_cardButtons[i] == null)
                {
                    continue;
                }

                int capturedIndex = i;
                _cardButtons[i].onClick.AddListener(() => CardClicked?.Invoke(capturedIndex));
            }

            _refreshButton?.onClick.AddListener(() => RefreshClicked?.Invoke());
        }

        public void Show(SkillCardData[] cards)
        {
            Show(cards, -1);
        }

        /// <param name="recommendedIndex">Card to mark as the suggested pick, or -1 for none. Which card that is
        /// is SkillChoicePresenter's call - the view only draws the badge.</param>
        public void Show(SkillCardData[] cards, int recommendedIndex)
        {
            if (_root != null)
            {
                _root.SetActive(true);
            }

            // Every open starts from a clean slate, so a card left dimmed or half-glowing by the previous
            // level-up can never bleed into this one.
            IsPlayingSelection = false;
            _starPopSequence?.Kill();
            _starPopSequence = null;
            SetInputEnabled(true);

            for (int i = 0; i < _cardFeedbacks.Length; i++)
            {
                _cardFeedbacks[i]?.ResetVisualState();
            }

            BeginAppearAnimation();

            for (int i = 0; i < _cardButtons.Length; i++)
            {
                bool hasCard = cards != null && i < cards.Length;

                if (_cardButtons[i] != null)
                {
                    _cardButtons[i].gameObject.SetActive(hasCard);
                }

                if (!hasCard)
                {
                    if (i < _starPulseTweens.Length)
                    {
                        _starPulseTweens[i]?.Kill();
                        _starPulseTweens[i] = null;
                    }

                    continue;
                }

                SkillCardData card = cards[i];
                if (_nameTexts[i] != null) _nameTexts[i].text = card.Name;
                if (_descriptionTexts[i] != null) _descriptionTexts[i].text = card.Description;
                if (_iconImages[i] != null)
                {
                    _iconImages[i].sprite = card.Icon;
                    _iconImages[i].enabled = card.Icon != null;
                }

                ApplyStars(i, card.Rank, card.MaxRank);
            }

            for (int i = 0; i < _recommendMarkers.Length; i++)
            {
                if (_recommendMarkers[i] == null)
                {
                    continue;
                }

                bool hasCard = cards != null && i < cards.Length;
                _recommendMarkers[i].SetActive(hasCard && i == recommendedIndex);
            }
        }

        /// <summary>One sprite per skill the player has already learned, in the order they were learned. Slots
        /// past the end of the list keep the board art's own empty socket - a skill at rank 0 is simply never in
        /// this list (see SkillChoicePresenter), which is what makes "not learned yet = shows nothing" true.</summary>
        public void SetAcquiredSkills(IReadOnlyList<Sprite> acquiredIcons)
        {
            SetAcquiredSkills(acquiredIcons, null);
        }

        /// <param name="maxedFlags">Parallel to acquiredIcons: true where that learned skill has hit its last
        /// rank, which is what puts the boost frame on its slot.</param>
        public void SetAcquiredSkills(IReadOnlyList<Sprite> acquiredIcons, IReadOnlyList<bool> maxedFlags)
        {
            for (int i = 0; i < _boardSlotIcons.Length; i++)
            {
                if (_boardSlotIcons[i] == null)
                {
                    continue;
                }

                bool hasIcon = acquiredIcons != null && i < acquiredIcons.Count && acquiredIcons[i] != null;
                _boardSlotIcons[i].sprite = hasIcon ? acquiredIcons[i] : null;
                _boardSlotIcons[i].enabled = hasIcon;
            }

            for (int i = 0; i < _boardSlotBoosts.Length; i++)
            {
                if (_boardSlotBoosts[i] == null)
                {
                    continue;
                }

                bool maxed = maxedFlags != null && i < maxedFlags.Count && maxedFlags[i];
                bool hasIcon = acquiredIcons != null && i < acquiredIcons.Count && acquiredIcons[i] != null;
                _boardSlotBoosts[i].SetActive(hasIcon && maxed);
            }
        }

        /// <summary>Plays the "you picked this one" sequence and returns how long the caller should wait before
        /// closing the popup. Call this ONLY once the rank has actually been raised - the star count drawn here
        /// comes from `newRank`, which is the committed value read back from PlayerSkillService, never from what
        /// the card happened to be showing.</summary>
        /// <param name="selectedIndex">Card the player picked.</param>
        /// <param name="newRank">The skill's rank AFTER the upgrade. Drives how many stars end up filled.</param>
        /// <param name="maxRank">That skill's last rank, so stars past it stay hidden.</param>
        public float PlaySelection(int selectedIndex, int newRank, int maxRank)
        {
            IsPlayingSelection = true;
            SetInputEnabled(false);

            // The newly-earned star breathes while the player is deciding; that has to stop or it fights the pop
            // and leaves the star sitting at a dim tint.
            for (int i = 0; i < _starPulseTweens.Length; i++)
            {
                _starPulseTweens[i]?.Kill();
                _starPulseTweens[i] = null;
            }

            for (int i = 0; i < _cardFeedbacks.Length; i++)
            {
                _cardFeedbacks[i]?.SetDimmed(i != selectedIndex);
            }

            float duration = 0f;
            if (selectedIndex >= 0 && selectedIndex < _cardFeedbacks.Length && _cardFeedbacks[selectedIndex] != null)
            {
                duration = _cardFeedbacks[selectedIndex].PlaySelected();
            }

            duration = Mathf.Max(duration, PopStars(selectedIndex, newRank, maxRank));
            return duration + _selectionHoldSeconds;
        }

        /// <summary>Redraws card `cardIndex`'s star row at `rank` and pops every filled star. All of them are
        /// inserted at the SAME position in the sequence on purpose - the spec calls for the new star count to
        /// land as one flash, not to count up one star at a time.</summary>
        private float PopStars(int cardIndex, int rank, int maxRank)
        {
            _starPopSequence?.Kill();
            _starPopSequence = null;

            if (_cardStarRows == null || cardIndex < 0 || cardIndex >= _cardStarRows.Length ||
                _cardStarRows[cardIndex] == null)
            {
                return 0f;
            }

            ApplyStarSprites(cardIndex, rank, maxRank);

            Image[] stars = _cardStarRows[cardIndex].Stars;
            if (stars == null)
            {
                return 0f;
            }

            _starPopSequence = DOTween.Sequence().SetUpdate(true).SetLink(gameObject);

            for (int i = 0; i < stars.Length; i++)
            {
                if (stars[i] == null || i >= rank || !stars[i].gameObject.activeSelf)
                {
                    continue;
                }

                RectTransform starRect = stars[i].rectTransform;
                starRect.localScale = Vector3.one * _starPopStartScale;

                // Same insert time for every star - this is what makes them arrive together.
                _starPopSequence.Insert(_starPopDelay,
                    starRect.DOScale(_starPopOvershoot, _starPopDuration * 0.45f).SetEase(Ease.OutQuad));
                _starPopSequence.Insert(_starPopDelay + _starPopDuration * 0.45f,
                    starRect.DOScale(1f, _starPopDuration * 0.55f).SetEase(Ease.OutBack));
            }

            return _starPopSequence.Duration();
        }

        /// <summary>Locks out every button on the popup while the pick animates, so a double tap cannot upgrade
        /// twice or re-roll mid-sequence. Only touches interactivity - alpha stays where the appear animation
        /// left it, so nothing visually changes here.</summary>
        private void SetInputEnabled(bool enabled)
        {
            if (_canvasGroup == null)
            {
                return;
            }

            _canvasGroup.interactable = enabled;
            _canvasGroup.blocksRaycasts = enabled;
        }

        /// <summary>Restarts the pop-in. Every tween here runs on unscaled time because this popup is shown with
        /// the game paused (SkillChoicePresenter pauses GameSpeed while a choice is pending), where scaled
        /// deltaTime is 0 and a normal tween would never advance.</summary>
        private void BeginAppearAnimation()
        {
            _appearScaleTween?.Kill();
            _appearFadeTween?.Kill();

            if (_content != null)
            {
                _content.localScale = Vector3.one * _appearStartScale;
                _appearScaleTween = _content.DOScale(Vector3.one, _appearDuration)
                    .SetEase(Ease.OutBack) // the little overshoot every card-pick popup in this genre has
                    .SetUpdate(true);
            }

            if (_canvasGroup != null)
            {
                _canvasGroup.alpha = 0f;
                _appearFadeTween = DOTween.To(() => _canvasGroup.alpha, a => _canvasGroup.alpha = a, 1f, _appearDuration)
                    .SetEase(Ease.OutQuad)
                    .SetUpdate(true);
            }
        }

        /// <summary>Starts (or restarts) the dim-bright-dim breathing on the one star card `cardIndex` would fill
        /// in, and pins every other star on that card back to full white - so a star that stops being the
        /// highlighted one after a Refresh re-roll never stays tinted.</summary>
        private void ApplyStarPulse(int cardIndex, Image[] stars, int highlightedIndex)
        {
            if (cardIndex < _starPulseTweens.Length)
            {
                _starPulseTweens[cardIndex]?.Kill();
                _starPulseTweens[cardIndex] = null;
            }

            for (int i = 0; i < stars.Length; i++)
            {
                if (stars[i] != null)
                {
                    stars[i].color = Color.white;
                }
            }

            if (highlightedIndex < 0 || highlightedIndex >= stars.Length || stars[highlightedIndex] == null ||
                cardIndex >= _starPulseTweens.Length)
            {
                return;
            }

            Image star = stars[highlightedIndex];
            star.color = _starPulseDimTint;

            // One yoyo loop is half a dim->bright->dim cycle, hence the /2 on the period.
            float halfCycle = 1f / (_starPulseFrequency * 2f);
            _starPulseTweens[cardIndex] = DOTween.To(() => star.color, c => star.color = c, _starPulseBrightTint, halfCycle)
                .SetEase(Ease.InOutSine)
                .SetLoops(-1, LoopType.Yoyo)
                .SetUpdate(true);
        }

        private void KillAllTweens()
        {
            _appearScaleTween?.Kill();
            _appearFadeTween?.Kill();
            _appearScaleTween = null;
            _appearFadeTween = null;
            _starPopSequence?.Kill();
            _starPopSequence = null;

            for (int i = 0; i < _starPulseTweens.Length; i++)
            {
                _starPulseTweens[i]?.Kill();
                _starPulseTweens[i] = null;
            }
        }

        /// <summary>Fills the first `rank` stars of card `cardIndex` and empties the rest. Stars past maxRank are
        /// hidden entirely, so a skill with fewer ranks than the row has Images doesn't show phantom slots.</summary>
        private void ApplyStars(int cardIndex, int rank, int maxRank)
        {
            if (!ApplyStarSprites(cardIndex, rank, maxRank))
            {
                return;
            }

            Image[] stars = _cardStarRows[cardIndex].Stars;

            // The last filled star IS the one this pick would award - that's the one that breathes.
            int highlighted = rank >= 1 && rank <= stars.Length ? rank - 1 : -1;
            ApplyStarPulse(cardIndex, stars, highlighted);
        }

        /// <summary>The sprite half of ApplyStars, split out so the post-pick pop can redraw the row from the
        /// committed rank without also restarting the "still deciding" breathing tween. Returns false when there
        /// is no usable star row for that card.</summary>
        private bool ApplyStarSprites(int cardIndex, int rank, int maxRank)
        {
            if (_cardStarRows == null || cardIndex < 0 || cardIndex >= _cardStarRows.Length ||
                _cardStarRows[cardIndex] == null)
            {
                return false;
            }

            Image[] stars = _cardStarRows[cardIndex].Stars;
            if (stars == null)
            {
                return false;
            }

            for (int i = 0; i < stars.Length; i++)
            {
                if (stars[i] == null)
                {
                    continue;
                }

                bool exists = maxRank <= 0 || i < maxRank;
                stars[i].gameObject.SetActive(exists);
                if (exists)
                {
                    stars[i].sprite = i < rank ? _filledStarSprite : _emptyStarSprite;
                    // The pop tween scales these, so anything that redraws the row must undo that too.
                    stars[i].rectTransform.localScale = Vector3.one;
                }
            }

            return true;
        }

        public void Hide()
        {
            // Nothing should keep breathing behind a closed popup - the next Show restarts everything anyway.
            KillAllTweens();

            // Leave the popup usable again even if it was closed mid-animation (panel torn down early, level
            // ended, scene unloaded) - otherwise the CanvasGroup stays non-interactive for the next open.
            IsPlayingSelection = false;
            SetInputEnabled(true);

            if (_root != null)
            {
                _root.SetActive(false);
            }
        }

        private void OnDestroy()
        {
            KillAllTweens();

            for (int i = 0; i < _cardButtons.Length; i++)
            {
                _cardButtons[i]?.onClick.RemoveAllListeners();
            }

            _refreshButton?.onClick.RemoveAllListeners();
        }
    }
}
