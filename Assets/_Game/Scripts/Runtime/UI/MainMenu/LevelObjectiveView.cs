using System;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using AlienDefense.Meta;

namespace AlienDefense.UI.MainMenu
{
    /// <summary>One objective chest slot. Tap opens per-slot reward bubble; claim from bubble when Claimable.
    /// An achieved objective (Claimable or Claimed) shows the open chest, a locked one the closed chest. The reveal
    /// of a newly achieved chest is sequenced by LevelObjectivePanelView; this view only supplies its own pop.</summary>
    public sealed class LevelObjectiveView : MonoBehaviour
    {
        private const float PopScale = 1.22f;
        private const float PopUpDuration = 0.16f;
        private const float PopDipScale = 0.94f;
        private const float PopDipDuration = 0.10f;
        private const float PopSettleDuration = 0.14f;

        [SerializeField]
        private Image _icon;

        [SerializeField]
        [Tooltip("Chest of an objective not achieved yet (Reward/chest).")]
        private Sprite _closedSprite;

        [SerializeField]
        [Tooltip("Chest of an achieved objective (Reward/openchest).")]
        private Sprite _openSprite;

        [SerializeField]
        private GameObject _completedBadge;

        [SerializeField]
        private TMP_Text _requirementText;

        [SerializeField]
        private Button _chestButton;

        [SerializeField]
        private Image _glow;

        [SerializeField]
        private GameObject _notificationDot;

        [SerializeField]
        [Tooltip("Optional. Light-ray halo behind an opened chest: slowly turns and breathes; hidden while locked.")]
        private RectTransform _aura;

        [SerializeField, Min(1f)]
        [Tooltip("Seconds per full turn of the halo.")]
        private float _auraTurnSeconds = 14f;

        [SerializeField]
        [Tooltip("Optional. The ray layer that turns (inside the halo); empty = the whole halo turns.")]
        private RectTransform _auraRays;

        [SerializeField]
        [Tooltip("Optional. A second ray layer inside the halo that turns the other way, slower - the shimmer.")]
        private RectTransform _auraCounterRays;

        [SerializeField]
        [Tooltip("Optional. Little star glints around the chest that twinkle one after another.")]
        private Image[] _auraSparkles = Array.Empty<Image>();

        [SerializeField]
        private ObjectiveRewardBubbleView _rewardBubble;

        [SerializeField]
        private Color _lockedColor = new Color(0.55f, 0.55f, 0.55f, 1f);

        [SerializeField]
        private Color _unlockedColor = Color.white;

        private LevelObjectivePresentation _presentation;
        private Tween _auraSpin;
        private Tween _auraPulse;
        private CanvasGroup _auraGroup;
        private Tween _auraCounterSpin;
        private Sequence _sparkleSequence;

        public event Action<LevelObjectiveKind> PreviewClicked;

        public LevelObjectiveKind Kind => _presentation.Kind;
        public LevelObjectivePresentation CurrentPresentation => _presentation;
        public RectTransform RectTransform => transform as RectTransform;
        public ObjectiveRewardBubbleView RewardBubble => _rewardBubble;

        private void Awake()
        {
            EnsureRaycastTargets();
            if (_chestButton != null)
            {
                _chestButton.onClick.AddListener(HandleChestClicked);
            }

            _rewardBubble?.Hide();
        }

        private void OnDestroy()
        {
            StopAura();
            if (_chestButton != null)
            {
                _chestButton.onClick.RemoveListener(HandleChestClicked);
            }
        }

        public void SetRewardBubble(ObjectiveRewardBubbleView bubble)
        {
            _rewardBubble = bubble;
        }

        /// <summary>Shows the slot exactly as the saved progress says - no animation.</summary>
        public void Configure(LevelObjectivePresentation presentation)
        {
            _presentation = presentation;
            EnsureRaycastTargets();
            _rewardBubble?.Hide();

            if (_requirementText != null)
            {
                _requirementText.text = presentation.RequirementText;
            }

            if (_chestButton != null)
            {
                _chestButton.interactable = presentation.PreviewRewards != null && presentation.PreviewRewards.Length > 0;
            }

            transform.localScale = Vector3.one;
            SnapToConfigured();
        }

        /// <summary>The final look for the configured state, chest back at its normal size.</summary>
        public void SnapToConfigured()
        {
            if (_icon != null)
            {
                _icon.transform.localScale = Vector3.one;
            }

            ApplyVisual(_presentation.RewardState);
        }

        /// <summary>Before its reveal a newly achieved chest still looks the way it did last time: closed.</summary>
        public void ShowAsNotYetRevealed()
        {
            if (_icon != null)
            {
                _icon.transform.localScale = Vector3.one;
            }

            ApplyVisual(ObjectiveRewardUiState.Locked);
        }

        /// <summary>The unlock pop: the closed chest swells, turns into the open chest at the top of the pop, dips
        /// and settles at scale 1. Not started here - the caller appends it to its own sequence, which then owns
        /// (and kills) it.</summary>
        public Tween CreateUnlockTween()
        {
            if (_icon == null)
            {
                return DOTween.Sequence().AppendCallback(SnapToConfigured);
            }

            Transform chest = _icon.transform;
            return DOTween.Sequence()
                .Append(chest.DOScale(PopScale, PopUpDuration).SetEase(Ease.OutQuad))
                .AppendCallback(ApplyConfiguredVisual)
                .Append(chest.DOScale(PopDipScale, PopDipDuration).SetEase(Ease.InOutSine))
                .Append(chest.DOScale(1f, PopSettleDuration).SetEase(Ease.OutSine));
        }

        private void ApplyConfiguredVisual()
        {
            ApplyVisual(_presentation.RewardState);
        }

        private void ApplyVisual(ObjectiveRewardUiState state)
        {
            bool locked = state == ObjectiveRewardUiState.Locked;
            bool claimable = state == ObjectiveRewardUiState.Claimable;

            if (_icon != null)
            {
                _icon.color = locked ? _lockedColor : _unlockedColor;
                Sprite sprite = locked ? _closedSprite : _openSprite;
                if (sprite != null)
                {
                    _icon.sprite = sprite;
                }
            }

            if (_completedBadge != null)
            {
                _completedBadge.SetActive(state == ObjectiveRewardUiState.Claimed);
            }

            // No wash over the chest and no idle pulse: a claimable chest is marked by the notification dot alone.
            if (_glow != null)
            {
                _glow.gameObject.SetActive(false);
            }

            if (_notificationDot != null)
            {
                _notificationDot.SetActive(claimable);
            }

            SetAura(!locked);
        }

        /// <summary>Opened chests (claimable or claimed) sit on a slowly turning, softly breathing burst of light.</summary>
        private void SetAura(bool on)
        {
            if (_aura == null)
            {
                return;
            }

            if (!on)
            {
                StopAura();
                _aura.gameObject.SetActive(false);
                return;
            }

            _aura.gameObject.SetActive(true);
            if (_auraSpin != null && _auraSpin.IsActive())
            {
                return;
            }

            if (_auraGroup == null)
            {
                _auraGroup = _aura.GetComponent<CanvasGroup>();
            }

            RectTransform spinner = _auraRays != null ? _auraRays : _aura;
            _auraSpin = spinner.DOLocalRotate(new Vector3(0f, 0f, -360f), _auraTurnSeconds, RotateMode.FastBeyond360)
                .SetEase(Ease.Linear)
                .SetLoops(-1, LoopType.Incremental)
                .SetLink(_aura.gameObject);
            if (_auraGroup != null)
            {
                _auraGroup.alpha = 1f;
                _auraPulse = DOTween.To(() => _auraGroup.alpha, a => _auraGroup.alpha = a, 0.72f, 1.3f)
                    .SetEase(Ease.InOutSine)
                    .SetLoops(-1, LoopType.Yoyo)
                    .SetLink(_aura.gameObject);
            }

            if (_auraCounterRays != null)
            {
                _auraCounterSpin = _auraCounterRays.DOLocalRotate(new Vector3(0f, 0f, 360f), _auraTurnSeconds * 1.7f, RotateMode.LocalAxisAdd)
                    .SetEase(Ease.Linear)
                    .SetLoops(-1, LoopType.Incremental)
                    .SetLink(_aura.gameObject);
            }

            StartSparkles();
        }

        /// <summary>Each glint pops (scale 0 -> 1, a quarter turn) and fades, staggered so one is always lit.</summary>
        private void StartSparkles()
        {
            if (_auraSparkles == null || _auraSparkles.Length == 0)
            {
                return;
            }

            _sparkleSequence = DOTween.Sequence().SetLink(_aura.gameObject).SetLoops(-1);
            for (int i = 0; i < _auraSparkles.Length; i++)
            {
                Image sparkle = _auraSparkles[i];
                if (sparkle == null)
                {
                    continue;
                }

                Transform t = sparkle.transform;
                t.localScale = Vector3.zero;
                float at = i * 0.55f;
                _sparkleSequence.Insert(at, t.DOScale(1f, 0.35f).SetEase(Ease.OutBack));
                _sparkleSequence.Insert(at, t.DOLocalRotate(new Vector3(0f, 0f, 45f), 0.9f, RotateMode.LocalAxisAdd));
                _sparkleSequence.Insert(at + 0.45f, t.DOScale(0f, 0.45f).SetEase(Ease.InQuad));
            }

            _sparkleSequence.AppendInterval(0.4f);
        }

        private void StopAura()
        {
            _auraSpin?.Kill();
            _auraPulse?.Kill();
            _auraCounterSpin?.Kill();
            _sparkleSequence?.Kill();
            _auraSpin = null;
            _auraPulse = null;
            _auraCounterSpin = null;
            _sparkleSequence = null;
        }

        public void ShowBubble(bool showClaim, Action onClaim, Action<string, int, bool> onItemClicked)
        {
            if (_rewardBubble == null)
            {
                return;
            }

            _rewardBubble.SetItemClickedHandler(onItemClicked);
            _rewardBubble.Show(_presentation.PreviewRewards, showClaim, onClaim);
        }

        public void HideBubble()
        {
            _rewardBubble?.Hide();
        }

        public bool IsBubbleOpen => _rewardBubble != null && _rewardBubble.IsOpen;

        private void EnsureRaycastTargets()
        {
            if (_icon != null)
            {
                _icon.raycastTarget = true;
            }

            if (_chestButton != null && _chestButton.targetGraphic != null)
            {
                _chestButton.targetGraphic.raycastTarget = true;
            }
        }

        private void HandleChestClicked()
        {
            PreviewClicked?.Invoke(_presentation.Kind);
        }
    }
}
