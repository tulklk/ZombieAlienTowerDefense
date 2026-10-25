using System;
using AlienDefense.Meta;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace AlienDefense.UI.MainMenu
{
    /// <summary>Row of 3 objective slots with per-chest reward bubbles. SetObjectives(null) hides panel (Level 1).</summary>
    public sealed class LevelObjectivePanelView : MonoBehaviour
    {
        [SerializeField]
        private LevelObjectiveView[] _objectiveSlots = Array.Empty<LevelObjectiveView>();

        [SerializeField]
        [Tooltip("Green progress fills lying over the board's dark pipes, left to right: element i joins chest i and " +
            "chest i+1. Image Type Filled / Horizontal / Left - only fillAmount is driven.")]
        private Image[] _connectors = Array.Empty<Image>();

        [Header("Reveal of newly achieved chests")]
        [SerializeField, Min(0f)]
        private float _revealStartDelay = 0.2f;

        [SerializeField, Min(0.05f)]
        private float _connectorFillDuration = 0.28f;

        [SerializeField, Min(0f)]
        [Tooltip("Pause after a chest has settled, before the green starts running to the next one.")]
        private float _revealStepDelay = 0.1f;

        [SerializeField]
        private Button _dismissBlocker;

        [SerializeField]
        private ObjectiveRewardOverlayView _detailOverlay;

        private LevelObjectivePresentation[] _current;
        private LevelObjectiveKind _openKind;
        private bool _hasOpen;
        private Sequence _revealSequence;
        private int _preparedRevealFrom = -1;

        public event Action<LevelObjectiveKind> ClaimClicked;

        private void OnEnable()
        {
            for (int i = 0; i < _objectiveSlots.Length; i++)
            {
                if (_objectiveSlots[i] != null)
                {
                    _objectiveSlots[i].PreviewClicked += HandlePreviewClicked;
                }
            }

            if (_dismissBlocker != null)
            {
                _dismissBlocker.onClick.AddListener(HideAllBubbles);
            }
        }

        private void OnDisable()
        {
            for (int i = 0; i < _objectiveSlots.Length; i++)
            {
                if (_objectiveSlots[i] != null)
                {
                    _objectiveSlots[i].PreviewClicked -= HandlePreviewClicked;
                }
            }

            if (_dismissBlocker != null)
            {
                _dismissBlocker.onClick.RemoveListener(HideAllBubbles);
            }

            HideAllBubbles();
            CancelReveal(); // hidden mid-reveal: come back showing the saved state, never a half-run animation
        }

        private void OnDestroy()
        {
            _revealSequence?.Kill();
            _revealSequence = null;
        }

        public void SetDetailOverlay(ObjectiveRewardOverlayView overlay)
        {
            _detailOverlay = overlay;
        }

        public void SetDismissBlocker(Button dismiss)
        {
            _dismissBlocker = dismiss;
        }

        /// <summary>Shows the chests exactly as given - no animation. Also ends any reveal in progress.</summary>
        public void SetObjectives(LevelObjectivePresentation[] objectives)
        {
            HideAllBubbles();
            _revealSequence?.Kill();
            _revealSequence = null;
            _preparedRevealFrom = -1;

            bool hasObjectives = objectives != null && objectives.Length > 0;
            _current = objectives;
            gameObject.SetActive(hasObjectives);

            if (!hasObjectives)
            {
                return;
            }

            ApplyFinalState();
        }

        /// <summary>Shows the chests as they were BEFORE the level was just won - the first
        /// <paramref name="previouslyAchieved"/> already open with their pipes green, the newly achieved ones still
        /// closed - and arms the reveal. Nothing moves until PlayPreparedReveal. With nothing new to reveal this is
        /// the same as SetObjectives.</summary>
        public void PrepareReveal(LevelObjectivePresentation[] objectives, int previouslyAchieved)
        {
            SetObjectives(objectives);
            if (objectives == null || !gameObject.activeInHierarchy)
            {
                return;
            }

            int achieved = CountAchieved();
            int from = Mathf.Clamp(previouslyAchieved, 0, achieved);
            if (from >= achieved)
            {
                return;
            }

            for (int i = from; i < achieved; i++)
            {
                _objectiveSlots[i]?.ShowAsNotYetRevealed();
                SetConnectorFill(i - 1, 0f);
            }

            _preparedRevealFrom = from;
        }

        /// <summary>Runs the armed reveal once, left to right: for each newly achieved chest the green runs along the
        /// pipe leading to it (not for the first chest - nothing leads to it), then the chest pops and opens. One
        /// sequence, unscaled time, linked to this object; however it ends, the chests land on the saved state.</summary>
        public void PlayPreparedReveal()
        {
            if (_preparedRevealFrom < 0 || _current == null || !gameObject.activeInHierarchy)
            {
                return;
            }

            int from = _preparedRevealFrom;
            int achieved = CountAchieved();
            _preparedRevealFrom = -1;

            _revealSequence?.Kill();
            _revealSequence = DOTween.Sequence().SetUpdate(true).SetLink(gameObject);
            _revealSequence.AppendInterval(_revealStartDelay);

            for (int i = from; i < achieved; i++)
            {
                Image connector = GetConnector(i - 1);
                if (connector != null)
                {
                    // DOTween.To rather than DOFillAmount: DOTween's UI module lives outside this assembly.
                    _revealSequence.Append(DOTween
                        .To(() => connector.fillAmount, value => connector.fillAmount = value, 1f, _connectorFillDuration)
                        .SetEase(Ease.InOutSine));
                }

                if (_objectiveSlots[i] != null)
                {
                    _revealSequence.Append(_objectiveSlots[i].CreateUnlockTween());
                }

                _revealSequence.AppendInterval(_revealStepDelay);
            }

            _revealSequence.OnComplete(HandleRevealCompleted);
        }

        private void HandleRevealCompleted()
        {
            _revealSequence = null;
            ApplyFinalState();
        }

        /// <summary>Stops an armed or running reveal and snaps everything to the saved state.</summary>
        private void CancelReveal()
        {
            bool wasRevealing = _revealSequence != null || _preparedRevealFrom >= 0;
            _revealSequence?.Kill();
            _revealSequence = null;
            _preparedRevealFrom = -1;

            if (wasRevealing && _current != null)
            {
                ApplyFinalState();
            }
        }

        private void ApplyFinalState()
        {
            int slotCount = Mathf.Min(_objectiveSlots.Length, _current.Length);
            for (int i = 0; i < slotCount; i++)
            {
                _objectiveSlots[i]?.Configure(_current[i]);
            }

            // Pipe i leads to chest i+1, so it is green exactly when that chest's objective is achieved.
            for (int i = 0; i < _connectors.Length; i++)
            {
                bool reached = i + 1 < slotCount && _current[i + 1].RewardState != ObjectiveRewardUiState.Locked;
                SetConnectorFill(i, reached ? 1f : 0f);
            }
        }

        /// <summary>Achieved objectives counted from the left (each one implies those before it).</summary>
        private int CountAchieved()
        {
            int slotCount = Mathf.Min(_objectiveSlots.Length, _current.Length);
            int count = 0;
            while (count < slotCount && _current[count].RewardState != ObjectiveRewardUiState.Locked)
            {
                count++;
            }

            return count;
        }

        private Image GetConnector(int index)
        {
            return index >= 0 && index < _connectors.Length ? _connectors[index] : null;
        }

        private void SetConnectorFill(int index, float amount)
        {
            Image connector = GetConnector(index);
            if (connector != null)
            {
                connector.fillAmount = amount;
            }
        }

        public void HidePreview()
        {
            HideAllBubbles();
        }

        private void HandlePreviewClicked(LevelObjectiveKind kind)
        {
            if (_hasOpen && _openKind == kind)
            {
                HideAllBubbles();
                return;
            }

            LevelObjectiveView slot = FindSlot(kind);
            LevelObjectivePresentation presentation = default;
            bool found = false;
            if (_current != null)
            {
                for (int i = 0; i < _current.Length; i++)
                {
                    if (_current[i].Kind == kind)
                    {
                        presentation = _current[i];
                        found = true;
                        break;
                    }
                }
            }

            if (!found || presentation.PreviewRewards == null || presentation.PreviewRewards.Length == 0 || slot == null)
            {
                HideAllBubbles();
                return;
            }

            HideAllBubbles();
            SetDismissActive(true);

            bool showClaim = presentation.RewardState == ObjectiveRewardUiState.Claimable;
            LevelObjectiveKind claimKind = kind;
            slot.ShowBubble(
                showClaim,
                () => ClaimClicked?.Invoke(claimKind),
                HandleBubbleItemClicked);
            _openKind = kind;
            _hasOpen = true;
        }

        private void HandleBubbleItemClicked(string itemId, int amount, bool isMystery)
        {
            if (_detailOverlay == null)
            {
                return;
            }

            _detailOverlay.ShowItemDetail(itemId, amount, isMystery);
        }

        private void HideAllBubbles()
        {
            for (int i = 0; i < _objectiveSlots.Length; i++)
            {
                _objectiveSlots[i]?.HideBubble();
            }

            _hasOpen = false;
            SetDismissActive(false);
        }

        private void SetDismissActive(bool active)
        {
            if (_dismissBlocker != null)
            {
                _dismissBlocker.gameObject.SetActive(active);
            }
        }

        private LevelObjectiveView FindSlot(LevelObjectiveKind kind)
        {
            for (int i = 0; i < _objectiveSlots.Length; i++)
            {
                if (_objectiveSlots[i] != null && _objectiveSlots[i].Kind == kind)
                {
                    return _objectiveSlots[i];
                }
            }

            return null;
        }
    }
}
