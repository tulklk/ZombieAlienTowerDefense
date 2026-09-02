using System;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AlienDefense.UI.MainMenu
{
    /// <summary>Looping idle animation a selected tab's icon plays while it stays selected. Only ever plays
    /// _idleAnimationLoops times per selection, then settles back to rest and stays still (re-selecting the tab
    /// — i.e. deselecting then selecting it again — restarts it). None = icon just sits still (still gets the
    /// pop/scale from SetSelected, just no extra looping motion).</summary>
    public enum BottomNavIconIdleAnimation
    {
        None,
        BounceUpDown,
        RotateTwoTurnsBothWays
    }

    /// <summary>One bottom-nav tab. Dumb view: Normal/Selected/Disabled visuals (icon keeps its own art color
    /// always; the label is only shown at all while selected; the always-visible tile background swaps between
    /// its normal/selected sprite; plus a pop on selection — the tile grows taller, the icon gets a UNIFORM
    /// scale-up, and the tab's own COLUMN widens via the HorizontalLayoutGroup, all animated together via
    /// DOTween) plus an optional notification badge, forwards clicks. Never navigates itself.
    ///
    /// IMPORTANT: the Tile's "grow taller" pop is done by tweening its RectTransform.sizeDelta (a real resize),
    /// never by scaling a parent around Icon. An earlier version scaled a shared IconGroup parent on the Y axis
    /// only, and separately gave Icon a compensating non-uniform local scale (X≠Y) to cancel that inherited
    /// stretch back out into a uniform look — that worked fine at rest, but non-uniform scale + rotation on the
    /// same visual element always shears in any 2D transform hierarchy (matrix multiplication order), so the
    /// rotating idle animation looked warped/skewed the moment RotateTwoTurnsBothWays played. Keeping Tile's
    /// resize as a plain size change (never a scale) and Icon's own scale strictly uniform sidesteps that
    /// entirely — Icon's rotation and scale can now never interact badly, at rest or mid-spin.
    ///
    /// The selected tab's column is widened by giving it a larger LayoutElement.flexibleWidth share than its
    /// neighbors (see SetSelected/_columnLayoutElement) — since every tab receives a SetSelected call on every
    /// selection change (see BottomNavigationPresenter/MenuShellPresenter), the other 4 tabs simultaneously fall
    /// back to their own baseline share in the same layout pass, so the row always sums to exactly the bar's
    /// width: the selected tab grows wider, its neighbors shrink to make room, and none of them ever overlap.
    ///
    /// While selected, the ICON object specifically (never Tile/Label) can also play a short looping idle
    /// animation (see _iconIdleAnimation) — bouncing up/down, or spinning two full turns clockwise then two back
    /// counter-clockwise — for _idleAnimationLoops repeats, then it settles back to rest, set per-tab at scaffold
    /// time (MainMenuSceneScaffolder.BuildBottomNavigation).</summary>
    public sealed class BottomNavTabView : MonoBehaviour
    {
        [SerializeField]
        private Button _button;

        [SerializeField]
        [Tooltip("The tab's own LayoutElement (on the same GameObject as _button) — its flexibleWidth is what " +
            "actually resizes this tab's column in the parent HorizontalLayoutGroup on selection. Distinct from " +
            "_tileBackground: this is the real column width; Tile just stretches to always match it.")]
        private LayoutElement _columnLayoutElement;

        [SerializeField]
        [Tooltip("The Icon+Label(+badge) container — nudged upward, in sync with the tile-height tween, by half " +
            "of whatever height the tile gains when selected. Tile is bottom-pivoted and only ever grows " +
            "UPWARD, so its center rises by half its height gain; without this counter-shift, Icon/Label (which " +
            "have fixed positions of their own) would stay put and end up looking pushed toward the bottom of " +
            "the taller selected tile instead of staying centered in it.")]
        private RectTransform _contentGroup;

        [SerializeField]
        private Image _icon;

        [SerializeField]
        private TMP_Text _label;

        [SerializeField]
        private NotificationBadgeView _notificationBadge;

        [SerializeField]
        [Tooltip("The tile behind the icon — matches the tab's own (LayoutGroup-sized) column width always (so " +
            "neighboring tabs' tiles sit flush, no gap/overlap) and grows taller — via a real RectTransform " +
            "resize, never a non-uniform scale (see the class doc) — when selected, always visible, shows " +
            "_normalTileSprite (\"Rectangle\") normally and swaps to _selectedTileSprite (\"Rectangle Select\") " +
            "when selected. Separate from the tab's own full-column raycast Image.")]
        private Image _tileBackground;

        [SerializeField]
        [Tooltip("Sprite shown on _tileBackground when this tab is NOT selected (\"Rectangle\").")]
        private Sprite _normalTileSprite;

        [SerializeField]
        [Tooltip("Sprite shown on _tileBackground when this tab IS selected (\"Rectangle Select\").")]
        private Sprite _selectedTileSprite;

        [SerializeField]
        private Color _disabledColor = new Color(0.5f, 0.5f, 0.5f, 0.6f);

        [SerializeField, Range(1f, 1.6f)]
        [Tooltip("How much taller (RectTransform height, not scale) the tile grows when selected.")]
        private float _selectedScale = 1.25f;

        [SerializeField, Min(1f)]
        [Tooltip("How many times this tab's baseline flexibleWidth share it claims when selected — e.g. 1.5 " +
            "means it claims 1.5x an unselected tab's share of the bar's width, and the other 4 (unselected, " +
            "each still at their own baseline share) shrink to fit around it. The bar's total width is always " +
            "fully accounted for, so this never causes overlap in either direction.")]
        private float _selectedFlexibleWidthMultiplier = 1.5f;

        [SerializeField, Range(1f, 1.5f)]
        [Tooltip("UNIFORM (both axes always equal, so it can rotate without shearing) scale-up applied to the " +
            "icon when selected, on top of the taller tile behind it.")]
        private float _iconSelectedScale = 1.15f;

        [SerializeField, Min(0f)]
        private float _popAnimationDuration = 0.15f;

        [SerializeField]
        [Tooltip("Looping idle animation the ICON object plays for _idleAnimationLoops repeats after this tab " +
            "gets selected, then settles back to rest. Set per-tab at scaffold time.")]
        private BottomNavIconIdleAnimation _iconIdleAnimation = BottomNavIconIdleAnimation.None;

        [SerializeField, Min(1)]
        [Tooltip("How many times the idle animation repeats per selection before settling back to rest.")]
        private int _idleAnimationLoops = 2;

        [SerializeField, Min(0f)]
        [Tooltip("BounceUpDown: how far up/down (px, before canvas scale) the icon oscillates from its rest " +
            "position.")]
        private float _bounceAmplitude = 10f;

        [SerializeField, Min(0.05f)]
        [Tooltip("BounceUpDown: seconds for one full up-down-up cycle.")]
        private float _bouncePeriod = 0.8f;

        [SerializeField, Min(0.05f)]
        [Tooltip("RotateTwoTurnsBothWays: seconds to complete one full turn in EACH direction (so one full " +
            "clockwise-then-counter-clockwise loop takes 2x this).")]
        private float _rotateDirectionDuration = 0.8f;

        public event Action Clicked;

        private Vector3 _iconBaseScale = Vector3.one;
        private Vector2 _iconBaseAnchoredPosition;
        private float _baseFlexibleWidth = 1f;
        private float _tileBaseHeight;
        private Sequence _popSequence;
        private Tween _idleTween;
        private bool _isSelected;

        private void Awake()
        {
            if (_icon != null)
            {
                _iconBaseScale = _icon.rectTransform.localScale;
                _iconBaseAnchoredPosition = _icon.rectTransform.anchoredPosition;
            }

            if (_columnLayoutElement != null)
            {
                _baseFlexibleWidth = _columnLayoutElement.flexibleWidth;
            }

            if (_tileBackground != null)
            {
                _tileBaseHeight = _tileBackground.rectTransform.sizeDelta.y;
            }

            if (_button != null)
            {
                _button.onClick.AddListener(HandleClicked);
            }
        }

        /// <summary>Grows the tile taller (a real resize, bottom-pivoted so it grows upward from a fixed point —
        /// never a scale, see the class doc), scales the icon up UNIFORMLY, and widens this tab's own column
        /// (all animated together) when selected, so the selected tab visibly stands out — taller AND wider —
        /// than its neighbors, with no overlap either way. The label is only shown at all while this tab is
        /// selected, per the reference art (its color/outline are static, set once at scaffold time — see
        /// MainMenuSceneScaffolder.BuildNavTab).</summary>
        public void SetSelected(bool selected)
        {
            _isSelected = selected;

            if (_label != null)
            {
                _label.gameObject.SetActive(selected);
            }

            if (_tileBackground != null)
            {
                _tileBackground.sprite = selected ? _selectedTileSprite : _normalTileSprite;
            }

            float multiplier = selected ? _selectedScale : 1f;
            float targetTileHeight = _tileBaseHeight * multiplier;
            float targetFlexibleWidth = _baseFlexibleWidth * (selected ? _selectedFlexibleWidthMultiplier : 1f);
            Vector3 targetIconScale = _iconBaseScale * (selected ? _iconSelectedScale : 1f);
            // Tile grows upward only (bottom-pivoted), so its center rises by half the height it gains — shift
            // ContentGroup by that same amount to keep icon+label centered in the tile at every size.
            float targetContentGroupY = (targetTileHeight - _tileBaseHeight) / 2f;

            AnimatePop(targetTileHeight, targetFlexibleWidth, targetIconScale, targetContentGroupY);
            SetIdleAnimationRunning(selected);
        }

        public void SetDisabled(bool disabled)
        {
            if (_button != null)
            {
                _button.interactable = !disabled;
            }

            if (_icon != null)
            {
                _icon.color = disabled ? _disabledColor : Color.white;
            }

            if (_tileBackground != null)
            {
                _tileBackground.color = disabled ? _disabledColor : Color.white;
            }
        }

        public void SetNotification(bool hasNotification)
        {
            if (_notificationBadge == null)
            {
                return;
            }

            if (hasNotification)
            {
                _notificationBadge.ShowDot();
            }
            else
            {
                _notificationBadge.Hide();
            }
        }

        private void AnimatePop(float targetTileHeight, float targetFlexibleWidth, Vector3 targetIconScale, float targetContentGroupY)
        {
            _popSequence?.Kill();

            if (!isActiveAndEnabled || _popAnimationDuration <= 0f)
            {
                SetTileHeight(targetTileHeight);
                SetFlexibleWidth(targetFlexibleWidth);
                if (_icon != null)
                {
                    _icon.rectTransform.localScale = targetIconScale;
                }

                if (_contentGroup != null)
                {
                    _contentGroup.anchoredPosition = new Vector2(0f, targetContentGroupY);
                }

                return;
            }

            Sequence seq = DOTween.Sequence().SetUpdate(true); // unscaled time, like the old coroutines
            seq.Join(DOTween.To(() => _tileBackground != null ? _tileBackground.rectTransform.sizeDelta.y : targetTileHeight,
                SetTileHeight, targetTileHeight, _popAnimationDuration).SetEase(Ease.OutQuad));
            seq.Join(DOTween.To(() => _columnLayoutElement != null ? _columnLayoutElement.flexibleWidth : targetFlexibleWidth,
                SetFlexibleWidth, targetFlexibleWidth, _popAnimationDuration).SetEase(Ease.OutQuad));
            if (_icon != null)
            {
                seq.Join(_icon.rectTransform.DOScale(targetIconScale, _popAnimationDuration).SetEase(Ease.OutQuad));
            }

            if (_contentGroup != null)
            {
                seq.Join(DOTween.To(() => _contentGroup.anchoredPosition.y,
                    y => _contentGroup.anchoredPosition = new Vector2(0f, y),
                    targetContentGroupY, _popAnimationDuration).SetEase(Ease.OutQuad));
            }

            _popSequence = seq;
        }

        /// <summary>Resizes the tile's RectTransform height directly (it's bottom-pivoted, so this grows it
        /// upward from a fixed point) — a real size change, never a transform scale, so it can never interact
        /// with (and shear) anything that rotates.</summary>
        private void SetTileHeight(float height)
        {
            if (_tileBackground == null)
            {
                return;
            }

            Vector2 sizeDelta = _tileBackground.rectTransform.sizeDelta;
            sizeDelta.y = height;
            _tileBackground.rectTransform.sizeDelta = sizeDelta;
        }

        /// <summary>Sets this tab's flexibleWidth share and marks the parent HorizontalLayoutGroup dirty so the
        /// whole row re-flows immediately — every other tab does the same on every selection change (see the
        /// class doc), so the row always sums to exactly the bar's width with no overlap.</summary>
        private void SetFlexibleWidth(float flexibleWidth)
        {
            if (_columnLayoutElement == null)
            {
                return;
            }

            _columnLayoutElement.flexibleWidth = flexibleWidth;

            var parentRect = _columnLayoutElement.transform.parent as RectTransform;
            if (parentRect != null)
            {
                LayoutRebuilder.MarkLayoutForRebuild(parentRect);
            }
        }

        /// <summary>Starts/stops the icon's looping idle animation (see _iconIdleAnimation) — it only ever plays
        /// while this tab is selected, and only for _idleAnimationLoops repeats before settling back to rest on
        /// its own. Deselecting mid-animation kills it immediately and resets the icon's position/rotation back
        /// to rest, so it never gets left mid-bounce or mid-spin when a different tab gets selected.</summary>
        private void SetIdleAnimationRunning(bool running)
        {
            _idleTween?.Kill();
            _idleTween = null;

            if (_icon == null)
            {
                return;
            }

            RectTransform iconRect = _icon.rectTransform;

            if (!running || _iconIdleAnimation == BottomNavIconIdleAnimation.None)
            {
                iconRect.anchoredPosition = _iconBaseAnchoredPosition;
                iconRect.localRotation = Quaternion.identity;
                return;
            }

            if (!isActiveAndEnabled)
            {
                return;
            }

            switch (_iconIdleAnimation)
            {
                case BottomNavIconIdleAnimation.BounceUpDown:
                    // Built on DOTween.To (core engine, in the DLL) rather than the RectTransform.DOAnchorPosY
                    // shortcut (a loose, non-asmdef Module script that compiles into Assembly-CSharp and is
                    // therefore NOT visible from this asmdef-scoped runtime assembly).
                    // One full up-down-up-down cycle = 2 Yoyo loops (there, back), so _idleAnimationLoops cycles
                    // = _idleAnimationLoops * 2 Yoyo loops.
                    _idleTween = DOTween.To(
                            () => iconRect.anchoredPosition.y,
                            y => iconRect.anchoredPosition = new Vector2(_iconBaseAnchoredPosition.x, y),
                            _iconBaseAnchoredPosition.y + _bounceAmplitude,
                            _bouncePeriod / 2f)
                        .SetEase(Ease.InOutSine)
                        .SetLoops(_idleAnimationLoops * 2, LoopType.Yoyo)
                        .SetUpdate(true)
                        .OnComplete(() => iconRect.anchoredPosition = _iconBaseAnchoredPosition);
                    break;

                case BottomNavIconIdleAnimation.RotateTwoTurnsBothWays:
                    // Icon's own scale is always uniform (see SetSelected/class doc), so rotating it here can
                    // never shear it, at rest or mid-pop.
                    Sequence rotateSeq = DOTween.Sequence().SetUpdate(true);
                    // Negative Z = clockwise on screen. Relative (LocalAxisAdd) so Append composes cleanly and
                    // SetLoops(Restart) below just replays this same clockwise-then-back pair from identity again.
                    rotateSeq.Append(iconRect.DOLocalRotate(new Vector3(0f, 0f, -360f), _rotateDirectionDuration, RotateMode.LocalAxisAdd).SetEase(Ease.InOutSine));
                    rotateSeq.Append(iconRect.DOLocalRotate(new Vector3(0f, 0f, 360f), _rotateDirectionDuration, RotateMode.LocalAxisAdd).SetEase(Ease.InOutSine));
                    rotateSeq.SetLoops(_idleAnimationLoops, LoopType.Restart);
                    rotateSeq.OnComplete(() => iconRect.localRotation = Quaternion.identity);
                    _idleTween = rotateSeq;
                    break;
            }
        }

        private void HandleClicked()
        {
            Clicked?.Invoke();
        }

        private void OnDestroy()
        {
            _popSequence?.Kill();
            _idleTween?.Kill();

            if (_button != null)
            {
                _button.onClick.RemoveListener(HandleClicked);
            }
        }
    }
}
