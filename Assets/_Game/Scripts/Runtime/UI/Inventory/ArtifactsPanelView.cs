using System.Collections.Generic;
using AlienDefense.Meta;
using AlienDefense.UI.Base;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AlienDefense.UI.Inventory
{
    /// <summary>The Artifacts tab and its merge sub-screen.
    ///
    /// Both live on this one view because they are two states of the same tab, not two tabs: the Merge button
    /// swaps which root is active and the back button swaps it back, with no reload in between. The grid keeps
    /// its pooled cards alive through the swap.</summary>
    public sealed class ArtifactsPanelView : MonoBehaviour
    {
        [Header("Inventory state")]
        [SerializeField]
        private GameObject _inventoryRoot;

        [SerializeField]
        private Transform _artifactGrid;

        [SerializeField]
        private ArtifactCardView _artifactCardPrefab;

        [SerializeField]
        private Button _openMergeButton;

        [SerializeField]
        private GameObject _emptyState;

        [SerializeField]
        private TMP_Text _emptyStateText;

        [SerializeField]
        private string _emptyMessage = "No artifacts available";

        [Header("Merge state")]
        [SerializeField]
        private GameObject _mergeRoot;

        [SerializeField]
        private Transform _mergeRowContainer;

        [SerializeField]
        private ArtifactMergeRowView _mergeRowPrefab;

        [SerializeField]
        [Tooltip("Shown in place of the rows when nothing has enough copies yet.")]
        private GameObject _noMergeState;

        [SerializeField]
        private TMP_Text _noMergeText;

        [SerializeField]
        private string _noMergeMessage = "There are no artifacts available for merging";

        [SerializeField]
        private Button _backButton;

        [Header("Shared")]
        [SerializeField]
        private RarityPalette _rarityPalette;

        private UiViewPool<ArtifactCardView> _cardPool;
        private UiViewPool<ArtifactMergeRowView> _rowPool;
        private System.Action _mergeOpened;
        private System.Action _mergeClosed;
        private System.Action<ArtifactMergePlan> _mergeRequested;

        public bool IsMergeOpen => _mergeRoot != null && _mergeRoot.activeSelf;

        public void Configure(System.Action mergeOpened, System.Action mergeClosed,
            System.Action<ArtifactMergePlan> mergeRequested)
        {
            _mergeOpened = mergeOpened;
            _mergeClosed = mergeClosed;
            _mergeRequested = mergeRequested;

            if (_cardPool == null && _artifactCardPrefab != null && _artifactGrid != null)
            {
                _cardPool = new UiViewPool<ArtifactCardView>(_artifactCardPrefab, _artifactGrid);
            }

            if (_rowPool == null && _mergeRowPrefab != null && _mergeRowContainer != null)
            {
                _rowPool = new UiViewPool<ArtifactMergeRowView>(_mergeRowPrefab, _mergeRowContainer);
            }

            if (_emptyStateText != null)
            {
                _emptyStateText.text = _emptyMessage;
            }

            if (_noMergeText != null)
            {
                _noMergeText.text = _noMergeMessage;
            }

            if (_openMergeButton != null)
            {
                _openMergeButton.onClick.RemoveListener(HandleOpenMerge);
                _openMergeButton.onClick.AddListener(HandleOpenMerge);
            }

            if (_backButton != null)
            {
                _backButton.onClick.RemoveListener(HandleBack);
                _backButton.onClick.AddListener(HandleBack);
            }
        }

        private void OnDestroy()
        {
            if (_openMergeButton != null)
            {
                _openMergeButton.onClick.RemoveListener(HandleOpenMerge);
            }

            if (_backButton != null)
            {
                _backButton.onClick.RemoveListener(HandleBack);
            }
        }

        public void ShowInventory()
        {
            SetState(false);
        }

        public void BindInventory(IReadOnlyList<ArtifactEntry> artifacts, System.Action<string> cardClicked)
        {
            int count = 0;
            if (_cardPool != null)
            {
                _cardPool.Begin();
                if (artifacts != null)
                {
                    for (int i = 0; i < artifacts.Count; i++)
                    {
                        if (artifacts[i].Definition == null)
                        {
                            continue;
                        }

                        ArtifactCardView card = _cardPool.Take();
                        if (card == null)
                        {
                            break;
                        }

                        card.Bind(artifacts[i], _rarityPalette, cardClicked);
                        count++;
                    }
                }

                _cardPool.End();
            }

            if (_emptyState != null)
            {
                _emptyState.SetActive(count == 0);
            }
        }

        public void BindMerge(IReadOnlyList<ArtifactMergePlan> plans)
        {
            int count = 0;
            if (_rowPool != null)
            {
                _rowPool.Begin();
                if (plans != null)
                {
                    for (int i = 0; i < plans.Count; i++)
                    {
                        if (!plans[i].Exists)
                        {
                            continue;
                        }

                        ArtifactMergeRowView row = _rowPool.Take();
                        if (row == null)
                        {
                            break;
                        }

                        row.Bind(plans[i], _rarityPalette, _mergeRequested);
                        row.SetInteractable(true);
                        count++;
                    }
                }

                _rowPool.End();
            }

            if (_noMergeState != null)
            {
                _noMergeState.SetActive(count == 0);
            }
        }

        /// <summary>Locks every row while a merge resolves, so a second tap cannot start another one.</summary>
        public void SetMergeInteractable(bool interactable)
        {
            if (_rowPool == null)
            {
                return;
            }

            IReadOnlyList<ArtifactMergeRowView> rows = _rowPool.Instances;
            for (int i = 0; i < rows.Count; i++)
            {
                if (rows[i] != null)
                {
                    rows[i].SetInteractable(interactable);
                }
            }
        }

        private void HandleOpenMerge()
        {
            SetState(true);
            _mergeOpened?.Invoke();
        }

        private void HandleBack()
        {
            SetState(false);
            _mergeClosed?.Invoke();
        }

        private void SetState(bool mergeOpen)
        {
            if (_inventoryRoot != null)
            {
                _inventoryRoot.SetActive(!mergeOpen);
            }

            if (_mergeRoot != null)
            {
                _mergeRoot.SetActive(mergeOpen);
            }
        }
    }
}
