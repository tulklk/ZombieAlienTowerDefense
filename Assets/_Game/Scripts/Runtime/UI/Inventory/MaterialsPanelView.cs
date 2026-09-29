using System.Collections.Generic;
using AlienDefense.Meta;
using AlienDefense.UI.Base;
using TMPro;
using UnityEngine;

namespace AlienDefense.UI.Inventory
{
    /// <summary>The Materials tab: a Containers strip above a Materials grid, both inside one ScrollRect.
    ///
    /// One scroll view for both sections on purpose - the design wants the headers to scroll away with their
    /// grids, and nesting a ScrollRect per section would fight the drag. The tab bar and the bottom navigation
    /// sit outside this object entirely, which is what keeps them fixed.
    ///
    /// Both grids are pooled (see UiViewPool): a refresh re-binds existing squares instead of rebuilding them.</summary>
    public sealed class MaterialsPanelView : MonoBehaviour
    {
        [Header("Containers section")]
        [SerializeField]
        [Tooltip("Header + grid together, hidden entirely when the player owns no containers.")]
        private GameObject _containersSection;

        [SerializeField]
        private Transform _containersGrid;

        [Header("Materials section")]
        [SerializeField]
        private GameObject _materialsSection;

        [SerializeField]
        private Transform _materialsGrid;

        [Header("Shared")]
        [SerializeField]
        private InventoryItemView _itemPrefab;

        [SerializeField]
        private RarityPalette _rarityPalette;

        [SerializeField]
        [Tooltip("Shown when the player owns nothing at all in this tab.")]
        private GameObject _emptyState;

        [SerializeField]
        private TMP_Text _emptyStateText;

        [SerializeField]
        private string _emptyMessage = "No materials available";

        private UiViewPool<InventoryItemView> _containerPool;
        private UiViewPool<InventoryItemView> _materialPool;
        private System.Action<string> _itemClicked;

        public void Configure(System.Action<string> itemClicked)
        {
            _itemClicked = itemClicked;

            if (_containerPool == null && _itemPrefab != null && _containersGrid != null)
            {
                _containerPool = new UiViewPool<InventoryItemView>(_itemPrefab, _containersGrid);
            }

            if (_materialPool == null && _itemPrefab != null && _materialsGrid != null)
            {
                _materialPool = new UiViewPool<InventoryItemView>(_itemPrefab, _materialsGrid);
            }

            if (_emptyStateText != null)
            {
                _emptyStateText.text = _emptyMessage;
            }
        }

        public void Bind(IReadOnlyList<MaterialEntry> containers, IReadOnlyList<MaterialEntry> materials)
        {
            int containerCount = Fill(_containerPool, containers);
            int materialCount = Fill(_materialPool, materials);

            if (_containersSection != null)
            {
                _containersSection.SetActive(containerCount > 0);
            }

            if (_materialsSection != null)
            {
                _materialsSection.SetActive(materialCount > 0);
            }

            if (_emptyState != null)
            {
                _emptyState.SetActive(containerCount == 0 && materialCount == 0);
            }
        }

        private int Fill(UiViewPool<InventoryItemView> pool, IReadOnlyList<MaterialEntry> entries)
        {
            if (pool == null)
            {
                return 0;
            }

            pool.Begin();

            int count = 0;
            if (entries != null)
            {
                for (int i = 0; i < entries.Count; i++)
                {
                    if (entries[i].Definition == null)
                    {
                        continue;
                    }

                    InventoryItemView view = pool.Take();
                    if (view == null)
                    {
                        break;
                    }

                    view.Bind(entries[i], _rarityPalette, _itemClicked);
                    count++;
                }
            }

            pool.End();
            return count;
        }
    }
}
