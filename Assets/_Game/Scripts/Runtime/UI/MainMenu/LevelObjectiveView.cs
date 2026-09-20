using System;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using AlienDefense.Meta;

namespace AlienDefense.UI.MainMenu
{
    /// <summary>One objective chest slot. Tap opens per-slot reward bubble; claim from bubble when Claimable.</summary>
    public sealed class LevelObjectiveView : MonoBehaviour
    {
        [SerializeField]
        private Image _icon;

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
        private ObjectiveRewardBubbleView _rewardBubble;

        [SerializeField]
        private Color _lockedColor = new Color(0.55f, 0.55f, 0.55f, 1f);

        [SerializeField]
        private Color _unlockedColor = Color.white;

        private LevelObjectivePresentation _presentation;
        private Tween _pulseTween;

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
            _pulseTween?.Kill();
            if (_chestButton != null)
            {
                _chestButton.onClick.RemoveListener(HandleChestClicked);
            }
        }

        public void SetRewardBubble(ObjectiveRewardBubbleView bubble)
        {
            _rewardBubble = bubble;
        }

        public void Configure(LevelObjectivePresentation presentation)
        {
            _presentation = presentation;
            EnsureRaycastTargets();
            _rewardBubble?.Hide();

            if (_requirementText != null)
            {
                _requirementText.text = presentation.RequirementText;
            }

            bool claimed = presentation.RewardState == ObjectiveRewardUiState.Claimed;
            bool claimable = presentation.RewardState == ObjectiveRewardUiState.Claimable;
            bool locked = presentation.RewardState == ObjectiveRewardUiState.Locked;
            bool hasPreview = presentation.PreviewRewards != null && presentation.PreviewRewards.Length > 0;

            if (_icon != null)
            {
                _icon.color = locked ? _lockedColor : _unlockedColor;
            }

            if (_completedBadge != null)
            {
                _completedBadge.SetActive(claimed);
            }

            if (_glow != null)
            {
                _glow.gameObject.SetActive(claimable);
            }

            if (_notificationDot != null)
            {
                _notificationDot.SetActive(claimable);
            }

            if (_chestButton != null)
            {
                _chestButton.interactable = hasPreview;
            }

            _pulseTween?.Kill();
            transform.localScale = Vector3.one;
            if (claimable)
            {
                _pulseTween = transform.DOScale(1.06f, 0.9f)
                    .SetEase(Ease.InOutSine)
                    .SetLoops(-1, LoopType.Yoyo)
                    .SetUpdate(true);
            }
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
