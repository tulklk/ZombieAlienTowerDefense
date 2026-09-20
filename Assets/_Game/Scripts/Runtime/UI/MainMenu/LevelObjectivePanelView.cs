using System;
using AlienDefense.Meta;
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
        private Image[] _connectors = Array.Empty<Image>();

        [SerializeField]
        private Button _dismissBlocker;

        [SerializeField]
        private ObjectiveRewardOverlayView _detailOverlay;

        [SerializeField]
        private Color _connectorIncompleteColor = new Color(0.25f, 0.25f, 0.3f, 1f);

        [SerializeField]
        private Color _connectorCompletedColor = new Color(0.4f, 0.85f, 0.5f, 1f);

        private LevelObjectivePresentation[] _current;
        private LevelObjectiveKind _openKind;
        private bool _hasOpen;

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
        }

        public void SetDetailOverlay(ObjectiveRewardOverlayView overlay)
        {
            _detailOverlay = overlay;
        }

        public void SetDismissBlocker(Button dismiss)
        {
            _dismissBlocker = dismiss;
        }

        public void SetObjectives(LevelObjectivePresentation[] objectives)
        {
            HideAllBubbles();

            bool hasObjectives = objectives != null && objectives.Length > 0;
            gameObject.SetActive(hasObjectives);
            _current = objectives;

            if (!hasObjectives)
            {
                return;
            }

            int slotCount = Mathf.Min(_objectiveSlots.Length, objectives.Length);
            for (int i = 0; i < slotCount; i++)
            {
                _objectiveSlots[i]?.Configure(objectives[i]);
            }

            int connectorCount = Mathf.Min(_connectors.Length, slotCount - 1);
            for (int i = 0; i < connectorCount; i++)
            {
                if (_connectors[i] == null)
                {
                    continue;
                }

                bool achieved = objectives[i].RewardState != ObjectiveRewardUiState.Locked;
                _connectors[i].color = achieved ? _connectorCompletedColor : _connectorIncompleteColor;
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
