using AlienDefense.Meta;
using AlienDefense.UI.Base;
using UnityEngine;
using UnityEngine.UI;

namespace AlienDefense.UI.Inventory
{
    /// <summary>One "[A][A][A][A][A] -> [A+]" row on the merge screen.
    ///
    /// The input slots are pooled rather than fixed at five, because the merge size comes from the artifact's own
    /// definition (MergeInputCount) - a family that merges three copies draws three slots without any code here
    /// knowing that number.</summary>
    public sealed class ArtifactMergeRowView : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Parent of the input slots. One slot per copy the merge consumes.")]
        private Transform _inputSlotContainer;

        [SerializeField]
        private Image _inputSlotPrefab;

        [SerializeField]
        [Tooltip("The single result slot on the right of the arrow.")]
        private Image _resultIcon;

        [SerializeField]
        private Image _resultFrame;

        [SerializeField]
        private Button _mergeButton;

        private UiViewPool<Image> _inputPool;
        private ArtifactMergePlan _plan;
        private System.Action<ArtifactMergePlan> _mergeRequested;

        private void Awake()
        {
            if (_mergeButton != null)
            {
                _mergeButton.onClick.AddListener(HandleMergeClicked);
            }
        }

        private void OnDestroy()
        {
            if (_mergeButton != null)
            {
                _mergeButton.onClick.RemoveListener(HandleMergeClicked);
            }
        }

        public void Bind(ArtifactMergePlan plan, RarityPalette palette, System.Action<ArtifactMergePlan> mergeRequested)
        {
            _plan = plan;
            _mergeRequested = mergeRequested;

            if (_inputPool == null && _inputSlotPrefab != null && _inputSlotContainer != null)
            {
                _inputPool = new UiViewPool<Image>(_inputSlotPrefab, _inputSlotContainer);
            }

            Sprite icon = plan.Definition != null ? plan.Definition.Icon : null;

            if (_inputPool != null)
            {
                _inputPool.Begin();
                for (int i = 0; i < plan.InputCount; i++)
                {
                    Image slot = _inputPool.Take();
                    if (slot == null)
                    {
                        break;
                    }

                    slot.sprite = icon;
                    slot.enabled = icon != null;
                    slot.color = palette != null ? palette.GetFrameColor(plan.SourceRarity) : Color.white;
                }

                _inputPool.End();
            }

            if (_resultIcon != null)
            {
                _resultIcon.sprite = icon;
                _resultIcon.enabled = icon != null;
            }

            InventoryItemView.ApplyRarity(_resultFrame, palette, plan.ResultRarity);
        }

        /// <summary>Locked for the length of the merge animation so a double tap cannot spend two batches.</summary>
        public void SetInteractable(bool interactable)
        {
            if (_mergeButton != null)
            {
                _mergeButton.interactable = interactable;
            }
        }

        private void HandleMergeClicked()
        {
            _mergeRequested?.Invoke(_plan);
        }
    }
}
